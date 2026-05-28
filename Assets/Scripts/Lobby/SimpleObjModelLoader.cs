using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class SimpleObjModelLoader
{
    struct ObjVertexKey
    {
        public int Position;
        public int TexCoord;
        public int Normal;

        public ObjVertexKey(int position, int texCoord, int normal)
        {
            Position = position;
            TexCoord = texCoord;
            Normal = normal;
        }
    }

    sealed class ObjVertexKeyComparer : IEqualityComparer<ObjVertexKey>
    {
        public bool Equals(ObjVertexKey a, ObjVertexKey b)
        {
            return a.Position == b.Position && a.TexCoord == b.TexCoord && a.Normal == b.Normal;
        }

        public int GetHashCode(ObjVertexKey key)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + key.Position;
                hash = hash * 31 + key.TexCoord;
                hash = hash * 31 + key.Normal;
                return hash;
            }
        }
    }

    public static GameObject LoadFromFile(string filePath, Material material)
    {
        return LoadFromFile(filePath, material, 120000, 180000);
    }

    public static GameObject LoadFromFile(string filePath, Material material, int maxOutputVertices, int maxTriangles)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        string text = File.ReadAllText(filePath);
        return LoadFromText(text, Path.GetFileNameWithoutExtension(filePath), material, maxOutputVertices, maxTriangles);
    }

    public static GameObject LoadFromText(string objText, string modelName, Material material)
    {
        return LoadFromText(objText, modelName, material, 120000, 180000);
    }

    public static GameObject LoadFromText(string objText, string modelName, Material material, int maxOutputVertices, int maxTriangles)
    {
        if (string.IsNullOrEmpty(objText))
            return null;

        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var meshVertices = new List<Vector3>();
        var meshTexCoords = new List<Vector2>();
        var meshNormals = new List<Vector3>();
        var triangles = new List<int>();
        var vertexMap = new Dictionary<ObjVertexKey, int>(new ObjVertexKeyComparer());

        using (var reader = new StringReader(objText))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (line.StartsWith("v "))
                {
                    var parts = SplitWords(line);
                    if (parts.Length >= 4)
                        positions.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                }
                else if (line.StartsWith("vt "))
                {
                    var parts = SplitWords(line);
                    if (parts.Length >= 3)
                        texCoords.Add(new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                }
                else if (line.StartsWith("vn "))
                {
                    var parts = SplitWords(line);
                    if (parts.Length >= 4)
                        normals.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])).normalized);
                }
                else if (line.StartsWith("f "))
                {
                    var parts = SplitWords(line);
                    if (parts.Length < 4)
                        continue;

                    int first = AddFaceVertex(parts[1], positions, texCoords, normals, meshVertices, meshTexCoords, meshNormals, vertexMap);
                    int previous = AddFaceVertex(parts[2], positions, texCoords, normals, meshVertices, meshTexCoords, meshNormals, vertexMap);

                    for (int i = 3; i < parts.Length; i++)
                    {
                        int current = AddFaceVertex(parts[i], positions, texCoords, normals, meshVertices, meshTexCoords, meshNormals, vertexMap);
                        if (first >= 0 && previous >= 0 && current >= 0)
                        {
                            triangles.Add(first);
                            triangles.Add(previous);
                            triangles.Add(current);
                        }

                        previous = current;

                        if (ExceedsLimits(meshVertices.Count, triangles.Count / 3, maxOutputVertices, maxTriangles))
                            return null;
                    }
                }
            }
        }

        if (meshVertices.Count == 0 || triangles.Count == 0)
            return null;

        var mesh = new Mesh();
        mesh.name = string.IsNullOrEmpty(modelName) ? "RuntimeObjMesh" : modelName + "_Mesh";
        mesh.hideFlags = HideFlags.DontSave;
#if UNITY_2017_3_OR_NEWER
        if (meshVertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#else
        if (meshVertices.Count > 65000)
            return null;
#endif

        mesh.SetVertices(meshVertices);
        mesh.SetTriangles(triangles, 0);
        if (meshTexCoords.Count == meshVertices.Count)
            mesh.SetUVs(0, meshTexCoords);
        if (AllNormalsUsable(meshNormals, meshVertices.Count))
            mesh.SetNormals(meshNormals);
        else
            mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(string.IsNullOrEmpty(modelName) ? "RuntimeObjModel" : modelName);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        if (material != null)
            renderer.sharedMaterial = material;

        return go;
    }

    static string[] SplitWords(string line)
    {
        return line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
    }

    static int AddFaceVertex(
        string token,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals,
        List<Vector3> meshVertices,
        List<Vector2> meshTexCoords,
        List<Vector3> meshNormals,
        Dictionary<ObjVertexKey, int> vertexMap)
    {
        var key = ParseFaceToken(token, positions.Count, texCoords.Count, normals.Count);
        if (key.Position < 0 || key.Position >= positions.Count)
            return -1;

        int existingIndex;
        if (vertexMap.TryGetValue(key, out existingIndex))
            return existingIndex;

        int index = meshVertices.Count;
        vertexMap[key] = index;
        meshVertices.Add(positions[key.Position]);
        meshTexCoords.Add(key.TexCoord >= 0 && key.TexCoord < texCoords.Count ? texCoords[key.TexCoord] : Vector2.zero);
        meshNormals.Add(key.Normal >= 0 && key.Normal < normals.Count ? normals[key.Normal] : Vector3.zero);
        return index;
    }

    static ObjVertexKey ParseFaceToken(string token, int positionCount, int texCoordCount, int normalCount)
    {
        var parts = token.Split('/');
        int position = ResolveObjIndex(ParseInt(parts, 0), positionCount);
        int texCoord = parts.Length > 1 && parts[1].Length > 0 ? ResolveObjIndex(ParseInt(parts, 1), texCoordCount) : -1;
        int normal = parts.Length > 2 && parts[2].Length > 0 ? ResolveObjIndex(ParseInt(parts, 2), normalCount) : -1;
        return new ObjVertexKey(position, texCoord, normal);
    }

    static int ResolveObjIndex(int objIndex, int count)
    {
        if (objIndex > 0)
            return objIndex - 1;
        if (objIndex < 0)
            return count + objIndex;
        return -1;
    }

    static int ParseInt(string[] parts, int index)
    {
        int value;
        if (index < parts.Length && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            return value;

        return 0;
    }

    static float ParseFloat(string value)
    {
        float result;
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return result;

        return 0f;
    }

    static bool AllNormalsUsable(List<Vector3> normals, int vertexCount)
    {
        if (normals.Count != vertexCount)
            return false;

        for (int i = 0; i < normals.Count; i++)
        {
            if (normals[i] == Vector3.zero)
                return false;
        }

        return true;
    }

    static bool ExceedsLimits(int vertexCount, int triangleCount, int maxOutputVertices, int maxTriangles)
    {
        if (maxOutputVertices > 0 && vertexCount > maxOutputVertices)
            return true;
        if (maxTriangles > 0 && triangleCount > maxTriangles)
            return true;

        return false;
    }
}
