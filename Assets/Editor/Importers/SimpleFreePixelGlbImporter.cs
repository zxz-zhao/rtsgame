using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

[ScriptedImporter(1, "fpglb")]
public class SimpleFreePixelGlbImporter : ScriptedImporter
{
    const uint JsonChunk = 0x4E4F534A;
    const uint BinaryChunk = 0x004E4942;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(ctx.assetPath));
        var assetNames = new HashSet<string>();

        try
        {
            byte[] binaryBuffer;
            GltfFile gltf = ReadGlb(ctx.assetPath, out binaryBuffer);
            Material[] materials = BuildMaterials(ctx, gltf, binaryBuffer, assetNames);
            BuildScene(ctx, root.transform, gltf, binaryBuffer, materials, assetNames);
        }
        catch (Exception ex)
        {
            Debug.LogError("[SimpleFreePixelGlbImporter] Failed to import " + ctx.assetPath + "\n" + ex);
            BuildFallback(root.transform);
        }

        ctx.AddObjectToAsset("main", root);
        ctx.SetMainObject(root);
    }

    static GltfFile ReadGlb(string assetPath, out byte[] binaryBuffer)
    {
        byte[] bytes = File.ReadAllBytes(assetPath);
        if (bytes.Length < 20 || bytes[0] != (byte)'g' || bytes[1] != (byte)'l' || bytes[2] != (byte)'T' || bytes[3] != (byte)'F')
            throw new InvalidDataException("Not a GLB file.");

        uint version = BitConverter.ToUInt32(bytes, 4);
        if (version != 2)
            throw new InvalidDataException("Only glTF 2.0 GLB files are supported.");

        string json = null;
        binaryBuffer = null;
        int offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            int chunkLength = (int)BitConverter.ToUInt32(bytes, offset);
            uint chunkType = BitConverter.ToUInt32(bytes, offset + 4);
            offset += 8;
            if (offset + chunkLength > bytes.Length)
                throw new InvalidDataException("Invalid GLB chunk length.");

            if (chunkType == JsonChunk)
                json = Encoding.UTF8.GetString(bytes, offset, chunkLength).TrimEnd('\0', ' ', '\t', '\r', '\n');
            else if (chunkType == BinaryChunk)
            {
                binaryBuffer = new byte[chunkLength];
                Buffer.BlockCopy(bytes, offset, binaryBuffer, 0, chunkLength);
            }

            offset += chunkLength;
        }

        if (string.IsNullOrEmpty(json))
            throw new InvalidDataException("GLB JSON chunk missing.");
        if (binaryBuffer == null)
            binaryBuffer = new byte[0];

        return JsonUtility.FromJson<GltfFile>(json);
    }

    static void BuildScene(AssetImportContext ctx, Transform root, GltfFile gltf, byte[] binaryBuffer, Material[] materials, HashSet<string> assetNames)
    {
        if (gltf.nodes == null || gltf.nodes.Length == 0)
            return;

        int[] sceneRoots = null;
        if (gltf.scenes != null && gltf.scenes.Length > 0)
        {
            int sceneIndex = Mathf.Clamp(gltf.scene, 0, gltf.scenes.Length - 1);
            sceneRoots = gltf.scenes[sceneIndex].nodes;
        }

        if (sceneRoots == null || sceneRoots.Length == 0)
        {
            sceneRoots = new int[gltf.nodes.Length];
            for (int i = 0; i < sceneRoots.Length; i++)
                sceneRoots[i] = i;
        }

        for (int i = 0; i < sceneRoots.Length; i++)
            BuildNode(ctx, root, gltf, binaryBuffer, materials, sceneRoots[i], assetNames);
    }

    static void BuildNode(AssetImportContext ctx, Transform parent, GltfFile gltf, byte[] binaryBuffer, Material[] materials, int nodeIndex, HashSet<string> assetNames)
    {
        if (nodeIndex < 0 || nodeIndex >= gltf.nodes.Length)
            return;

        GltfNode node = gltf.nodes[nodeIndex];
        var nodeObject = new GameObject(SafeName(node.name, "Node_" + nodeIndex));
        nodeObject.transform.SetParent(parent, false);
        ApplyTransform(nodeObject.transform, node);

        if (node.mesh >= 0 && gltf.meshes != null && node.mesh < gltf.meshes.Length)
            BuildMesh(ctx, nodeObject.transform, gltf, binaryBuffer, materials, node.mesh, assetNames);

        if (node.children != null)
        {
            for (int i = 0; i < node.children.Length; i++)
                BuildNode(ctx, nodeObject.transform, gltf, binaryBuffer, materials, node.children[i], assetNames);
        }
    }

    static void BuildMesh(AssetImportContext ctx, Transform parent, GltfFile gltf, byte[] binaryBuffer, Material[] materials, int meshIndex, HashSet<string> assetNames)
    {
        GltfMesh meshDef = gltf.meshes[meshIndex];
        if (meshDef.primitives == null)
            return;

        string meshName = SafeName(meshDef.name, "Mesh_" + meshIndex);
        for (int i = 0; i < meshDef.primitives.Length; i++)
        {
            GltfPrimitive primitive = meshDef.primitives[i];
            if (primitive.attributes == null || primitive.attributes.POSITION < 0)
                continue;

            Vector3[] vertices = ReadVec3(gltf, binaryBuffer, primitive.attributes.POSITION);
            Vector2[] uvs = primitive.attributes.TEXCOORD_0 >= 0
                ? ReadVec2(gltf, binaryBuffer, primitive.attributes.TEXCOORD_0)
                : null;
            int[] indices = primitive.indices >= 0
                ? ReadIndices(gltf, binaryBuffer, primitive.indices)
                : Sequential(vertices.Length);
            int[] triangles = BuildTriangles(indices, primitive.mode == 0 ? 4 : primitive.mode);
            if (triangles.Length == 0)
                continue;

            var mesh = new Mesh();
            mesh.name = meshName + "_" + i;
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            if (uvs != null && uvs.Length == vertices.Length)
                mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ctx.AddObjectToAsset(UniqueAssetName(assetNames, mesh.name), mesh);

            var part = new GameObject(mesh.name);
            part.transform.SetParent(parent, false);
            var filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PickMaterial(materials, primitive.material);
        }
    }

    static Material[] BuildMaterials(AssetImportContext ctx, GltfFile gltf, byte[] binaryBuffer, HashSet<string> assetNames)
    {
        if (gltf.materials == null || gltf.materials.Length == 0)
        {
            var mat = MakeMaterial("Default", Color.gray, null);
            ctx.AddObjectToAsset(UniqueAssetName(assetNames, mat.name), mat);
            return new[] { mat };
        }

        var result = new Material[gltf.materials.Length];
        for (int i = 0; i < gltf.materials.Length; i++)
        {
            GltfMaterial source = gltf.materials[i];
            Color color = new Color(0.75f, 0.75f, 0.75f, 1f);
            Texture2D texture = null;
            if (source.pbrMetallicRoughness != null)
            {
                float[] factor = source.pbrMetallicRoughness.baseColorFactor;
                if (factor != null && factor.Length >= 3)
                    color = new Color(factor[0], factor[1], factor[2], factor.Length > 3 ? factor[3] : 1f);

                if (source.pbrMetallicRoughness.baseColorTexture != null)
                    texture = BuildTexture(ctx, gltf, binaryBuffer, source.pbrMetallicRoughness.baseColorTexture.index, assetNames);
            }

            var mat = MakeMaterial(SafeName(source.name, "Material_" + i), color, texture);
            ctx.AddObjectToAsset(UniqueAssetName(assetNames, mat.name), mat);
            result[i] = mat;
        }

        return result;
    }

    static Material MakeMaterial(string name, Color color, Texture2D texture)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
        var mat = new Material(shader);
        mat.name = name;
        if (mat.HasProperty("_Color"))
            mat.color = color;
        if (texture != null && mat.HasProperty("_MainTex"))
            mat.mainTexture = texture;
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.35f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        return mat;
    }

    static Texture2D BuildTexture(AssetImportContext ctx, GltfFile gltf, byte[] binaryBuffer, int textureIndex, HashSet<string> assetNames)
    {
        if (gltf.textures == null || textureIndex < 0 || textureIndex >= gltf.textures.Length)
            return null;
        int imageIndex = gltf.textures[textureIndex].source;
        if (gltf.images == null || imageIndex < 0 || imageIndex >= gltf.images.Length)
            return null;

        GltfImage image = gltf.images[imageIndex];
        if (image.bufferView < 0 || gltf.bufferViews == null || image.bufferView >= gltf.bufferViews.Length)
            return null;

        GltfBufferView view = gltf.bufferViews[image.bufferView];
        if (view.byteOffset + view.byteLength > binaryBuffer.Length)
            return null;

        byte[] imageBytes = new byte[view.byteLength];
        Buffer.BlockCopy(binaryBuffer, view.byteOffset, imageBytes, 0, view.byteLength);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
        texture.name = SafeName(image.name, "Texture_" + imageIndex);
        if (!texture.LoadImage(imageBytes))
            return null;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        ctx.AddObjectToAsset(UniqueAssetName(assetNames, texture.name), texture);
        return texture;
    }

    static Material PickMaterial(Material[] materials, int materialIndex)
    {
        if (materials == null || materials.Length == 0)
            return null;
        if (materialIndex >= 0 && materialIndex < materials.Length)
            return materials[materialIndex];
        return materials[0];
    }

    static Vector3[] ReadVec3(GltfFile gltf, byte[] binaryBuffer, int accessorIndex)
    {
        GltfAccessor accessor = gltf.accessors[accessorIndex];
        var result = new Vector3[accessor.count];
        for (int i = 0; i < accessor.count; i++)
        {
            float x = ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 0);
            float y = ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 1);
            float z = ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 2);
            result[i] = new Vector3(x, y, z);
        }
        return result;
    }

    static Vector2[] ReadVec2(GltfFile gltf, byte[] binaryBuffer, int accessorIndex)
    {
        GltfAccessor accessor = gltf.accessors[accessorIndex];
        var result = new Vector2[accessor.count];
        for (int i = 0; i < accessor.count; i++)
        {
            float x = ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 0);
            float y = ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 1);
            result[i] = new Vector2(x, 1f - y);
        }
        return result;
    }

    static int[] ReadIndices(GltfFile gltf, byte[] binaryBuffer, int accessorIndex)
    {
        GltfAccessor accessor = gltf.accessors[accessorIndex];
        var result = new int[accessor.count];
        for (int i = 0; i < accessor.count; i++)
            result[i] = Mathf.RoundToInt(ReadAccessorComponent(gltf, binaryBuffer, accessorIndex, i, 0));
        return result;
    }

    static float ReadAccessorComponent(GltfFile gltf, byte[] binaryBuffer, int accessorIndex, int elementIndex, int componentIndex)
    {
        GltfAccessor accessor = gltf.accessors[accessorIndex];
        GltfBufferView view = gltf.bufferViews[accessor.bufferView];
        int componentSize = ComponentSize(accessor.componentType);
        int componentCount = ComponentCount(accessor.type);
        int stride = view.byteStride > 0 ? view.byteStride : componentSize * componentCount;
        int offset = view.byteOffset + accessor.byteOffset + elementIndex * stride + componentIndex * componentSize;
        return ReadComponent(binaryBuffer, offset, accessor.componentType, accessor.normalized);
    }

    static float ReadComponent(byte[] data, int offset, int componentType, bool normalized)
    {
        switch (componentType)
        {
            case 5120:
            {
                sbyte value = unchecked((sbyte)data[offset]);
                return normalized ? Mathf.Max(value / 127f, -1f) : value;
            }
            case 5121:
            {
                byte value = data[offset];
                return normalized ? value / 255f : value;
            }
            case 5122:
            {
                short value = BitConverter.ToInt16(data, offset);
                return normalized ? Mathf.Max(value / 32767f, -1f) : value;
            }
            case 5123:
            {
                ushort value = BitConverter.ToUInt16(data, offset);
                return normalized ? value / 65535f : value;
            }
            case 5125:
                return BitConverter.ToUInt32(data, offset);
            case 5126:
                return BitConverter.ToSingle(data, offset);
            default:
                throw new NotSupportedException("Unsupported glTF component type: " + componentType);
        }
    }

    static int[] BuildTriangles(int[] indices, int mode)
    {
        var triangles = new List<int>();
        if (mode == 4)
        {
            for (int i = 0; i + 2 < indices.Length; i += 3)
                AddTriangle(triangles, indices[i], indices[i + 1], indices[i + 2]);
        }
        else if (mode == 5)
        {
            for (int i = 0; i + 2 < indices.Length; i++)
            {
                if ((i & 1) == 0)
                    AddTriangle(triangles, indices[i], indices[i + 1], indices[i + 2]);
                else
                    AddTriangle(triangles, indices[i + 1], indices[i], indices[i + 2]);
            }
        }
        else if (mode == 6)
        {
            for (int i = 1; i + 1 < indices.Length; i++)
                AddTriangle(triangles, indices[0], indices[i], indices[i + 1]);
        }
        return triangles.ToArray();
    }

    static void AddTriangle(List<int> triangles, int a, int b, int c)
    {
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(b);
    }

    static int[] Sequential(int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
            result[i] = i;
        return result;
    }

    static int ComponentSize(int componentType)
    {
        switch (componentType)
        {
            case 5120:
            case 5121:
                return 1;
            case 5122:
            case 5123:
                return 2;
            case 5125:
            case 5126:
                return 4;
            default:
                throw new NotSupportedException("Unsupported glTF component type: " + componentType);
        }
    }

    static int ComponentCount(string type)
    {
        switch (type)
        {
            case "SCALAR": return 1;
            case "VEC2": return 2;
            case "VEC3": return 3;
            case "VEC4": return 4;
            case "MAT2": return 4;
            case "MAT3": return 9;
            case "MAT4": return 16;
            default: return 1;
        }
    }

    static void ApplyTransform(Transform target, GltfNode node)
    {
        if (node.matrix != null && node.matrix.Length == 16)
        {
            Matrix4x4 matrix = new Matrix4x4();
            for (int column = 0; column < 4; column++)
            {
                for (int row = 0; row < 4; row++)
                    matrix[row, column] = node.matrix[column * 4 + row];
            }

            Vector3 position = matrix.GetColumn(3);
            Vector3 x = matrix.GetColumn(0);
            Vector3 y = matrix.GetColumn(1);
            Vector3 z = matrix.GetColumn(2);
            Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            Quaternion rotation = Quaternion.identity;
            if (scale.x > 0.0001f && scale.y > 0.0001f && scale.z > 0.0001f)
                rotation = Quaternion.LookRotation(z / scale.z, y / scale.y);

            target.localPosition = position;
            target.localRotation = rotation;
            target.localScale = scale;
            return;
        }

        float[] t = node.translation;
        float[] r = node.rotation;
        float[] s = node.scale;
        target.localPosition = t != null && t.Length >= 3 ? new Vector3(t[0], t[1], t[2]) : Vector3.zero;
        target.localRotation = r != null && r.Length >= 4 ? new Quaternion(r[0], r[1], r[2], r[3]) : Quaternion.identity;
        target.localScale = s != null && s.Length >= 3 ? new Vector3(s[0], s[1], s[2]) : Vector3.one;
    }

    static void BuildFallback(Transform root)
    {
        // Do not create placeholder geometry; failed imports should stay visually empty.
    }

    static string SafeName(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                chars[i] = '_';
        }
        string cleaned = new string(chars).Trim('_', '.', '-');
        return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
    }

    static string UniqueAssetName(HashSet<string> used, string desired)
    {
        desired = SafeName(desired, "Asset");
        string name = desired;
        int index = 1;
        while (used.Contains(name))
        {
            name = desired + "_" + index;
            index++;
        }
        used.Add(name);
        return name;
    }

    [Serializable]
    public class GltfFile
    {
        public GltfScene[] scenes;
        public int scene;
        public GltfNode[] nodes;
        public GltfMesh[] meshes;
        public GltfAccessor[] accessors;
        public GltfBufferView[] bufferViews;
        public GltfMaterial[] materials;
        public GltfTexture[] textures;
        public GltfImage[] images;
    }

    [Serializable]
    public class GltfScene
    {
        public int[] nodes;
    }

    [Serializable]
    public class GltfNode
    {
        public string name;
        public int mesh = -1;
        public int[] children;
        public float[] translation;
        public float[] rotation;
        public float[] scale;
        public float[] matrix;
    }

    [Serializable]
    public class GltfMesh
    {
        public string name;
        public GltfPrimitive[] primitives;
    }

    [Serializable]
    public class GltfPrimitive
    {
        public GltfAttributes attributes;
        public int indices = -1;
        public int material = -1;
        public int mode = 4;
    }

    [Serializable]
    public class GltfAttributes
    {
        public int POSITION = -1;
        public int NORMAL = -1;
        public int TEXCOORD_0 = -1;
    }

    [Serializable]
    public class GltfAccessor
    {
        public int bufferView = -1;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
        public bool normalized;
    }

    [Serializable]
    public class GltfBufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int byteStride;
    }

    [Serializable]
    public class GltfMaterial
    {
        public string name;
        public GltfPbr pbrMetallicRoughness;
    }

    [Serializable]
    public class GltfPbr
    {
        public float[] baseColorFactor;
        public GltfTextureInfo baseColorTexture;
    }

    [Serializable]
    public class GltfTextureInfo
    {
        public int index = -1;
    }

    [Serializable]
    public class GltfTexture
    {
        public int source = -1;
    }

    [Serializable]
    public class GltfImage
    {
        public string name;
        public int bufferView = -1;
        public string mimeType;
    }
}
