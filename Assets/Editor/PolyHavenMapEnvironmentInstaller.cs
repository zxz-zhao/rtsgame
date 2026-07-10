using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PolyHavenMapEnvironmentInstaller
{
    const string SourceRoot = "Assets/External/PolyHaven/EnvironmentTextures";
    const string MaterialRoot = "Assets/Resources/Materials/MapEnvironment";
    const string GeneratedTextureRoot = "Assets/Resources/Textures/MapEnvironmentGenerated";
    const string SourceNotesPath = MaterialRoot + "/SOURCE_LICENSE.txt";

    readonly struct SurfaceSpec
    {
        public readonly string MaterialName;
        public readonly string AssetId;
        public readonly float Smoothness;
        public readonly float BumpScale;
        public readonly float OcclusionStrength;

        public SurfaceSpec(string materialName, string assetId, float smoothness, float bumpScale, float occlusionStrength)
        {
            MaterialName = materialName;
            AssetId = assetId;
            Smoothness = smoothness;
            BumpScale = bumpScale;
            OcclusionStrength = occlusionStrength;
        }
    }

    static readonly SurfaceSpec[] SurfaceSpecs =
    {
        new SurfaceSpec("Env_Grassland", "forrest_ground_01", 0.11f, 0.34f, 0.80f),
        new SurfaceSpec("Env_ForestFloor", "forest_ground_04", 0.08f, 0.62f, 0.92f),
        new SurfaceSpec("Env_DirtRoad", "rocky_trail", 0.10f, 0.54f, 0.86f),
        new SurfaceSpec("Env_Gravel", "gravel_road", 0.08f, 0.44f, 0.84f),
        new SurfaceSpec("Env_Shore", "aerial_beach_01", 0.12f, 0.26f, 0.76f),
        new SurfaceSpec("Env_Mud", "aerial_mud_1", 0.22f, 0.32f, 0.78f),
        new SurfaceSpec("Env_Snow", "snow_03", 0.18f, 0.22f, 0.72f),
        new SurfaceSpec("Env_Concrete", "concrete_floor_damaged_01", 0.18f, 0.22f, 0.78f),
        new SurfaceSpec("Env_Asphalt", "asphalt_02", 0.14f, 0.20f, 0.76f),
        new SurfaceSpec("Env_BasePad", "concrete_floor_damaged_01", 0.16f, 0.20f, 0.76f),
        new SurfaceSpec("Env_BattleScar", "rock_ground_02", 0.09f, 0.42f, 0.84f),
        new SurfaceSpec("Env_RuinDust", "gravel_road", 0.08f, 0.36f, 0.80f),
    };

    [MenuItem("RTS/Art/Map Environment/Install Poly Haven Materials")]
    public static void InstallFromMenu()
    {
        InstallMaterials();
    }

    public static void InstallMaterials()
    {
        EnsureFolder(MaterialRoot);
        EnsureFolder(GeneratedTextureRoot);

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < SurfaceSpecs.Length; i++)
                ConfigureSourceTextures(SurfaceSpecs[i]);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        int installed = 0;
        for (int i = 0; i < SurfaceSpecs.Length; i++)
        {
            if (CreateSurfaceMaterial(SurfaceSpecs[i]) != null)
                installed++;
        }

        WriteSourceNotes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PolyHavenMapEnvironmentInstaller] Installed " + installed + " map environment materials.");
    }

    static Material CreateSurfaceMaterial(SurfaceSpec spec)
    {
        string colorPath = FindTexturePath(spec.AssetId, new[] { "diff" }, new[] { ".jpg", ".jpeg", ".png" });
        if (string.IsNullOrEmpty(colorPath))
        {
            Debug.LogWarning("[PolyHavenMapEnvironmentInstaller] Missing diffuse texture for " + spec.AssetId);
            return null;
        }

        string normalPath = FindTexturePath(spec.AssetId, new[] { "nor_gl" }, new[] { ".png", ".jpg", ".jpeg" });
        string aoPath = FindTexturePath(spec.AssetId, new[] { "_ao_", "ao", "rough_ao" }, new[] { ".jpg", ".jpeg", ".png" });
        string roughnessPath = FindTexturePath(spec.AssetId, new[] { "_rough_", "rough" }, new[] { ".jpg", ".jpeg", ".png" });
        if (string.IsNullOrEmpty(roughnessPath))
            roughnessPath = FindTexturePath(spec.AssetId, new[] { "arm" }, new[] { ".jpg", ".jpeg", ".png" });
        string smoothnessPath = !string.IsNullOrEmpty(roughnessPath)
            ? CreateSmoothnessMap(GeneratedTextureRoot + "/" + spec.MaterialName + "_Smoothness.png", roughnessPath)
            : null;

        Texture2D color = TryLoadTexture(colorPath, true);
        if (color == null)
            return null;

        Texture2D normal = TryLoadTexture(normalPath, false);
        Texture2D ao = TryLoadTexture(aoPath, false);
        Texture2D metallicSmoothness = TryLoadTexture(smoothnessPath, false);

        string materialPath = MaterialRoot + "/" + spec.MaterialName + ".mat";
        return CreatePbrMaterial(
            materialPath,
            spec.MaterialName,
            color,
            normal,
            metallicSmoothness,
            ao,
            spec.Smoothness,
            spec.BumpScale,
            spec.OcclusionStrength);
    }

    static void ConfigureSourceTextures(SurfaceSpec spec)
    {
        ConfigureTexture(FindTexturePath(spec.AssetId, new[] { "diff" }, new[] { ".jpg", ".jpeg", ".png" }), TextureImporterType.Default, true);
        ConfigureTexture(FindTexturePath(spec.AssetId, new[] { "nor_gl" }, new[] { ".png", ".jpg", ".jpeg" }), TextureImporterType.NormalMap, false);
        ConfigureTexture(FindTexturePath(spec.AssetId, new[] { "_ao_", "ao", "rough_ao" }, new[] { ".jpg", ".jpeg", ".png" }), TextureImporterType.Default, false);
        ConfigureTexture(FindTexturePath(spec.AssetId, new[] { "_rough_", "rough" }, new[] { ".jpg", ".jpeg", ".png" }), TextureImporterType.Default, false);
        ConfigureTexture(FindTexturePath(spec.AssetId, new[] { "arm" }, new[] { ".jpg", ".jpeg", ".png" }), TextureImporterType.Default, false);
    }

    static Material CreatePbrMaterial(
        string materialPath,
        string materialName,
        Texture2D colorMap,
        Texture2D normalMap,
        Texture2D metallicSmoothnessMap,
        Texture2D aoMap,
        float smoothness,
        float bumpScale,
        float occlusionStrength)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (shader != null)
        {
            material.shader = shader;
        }

        material.name = materialName;
        RendererColorUtil.TrySetColor(material, Color.white);
        SetTexture(material, "_MainTex", colorMap);
        SetTexture(material, "_BaseMap", colorMap);

        if (normalMap != null)
        {
            SetTexture(material, "_BumpMap", normalMap);
            SetTexture(material, "_NormalMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", bumpScale);
        }

        if (metallicSmoothnessMap != null)
        {
            SetTexture(material, "_MetallicGlossMap", metallicSmoothnessMap);
            material.EnableKeyword("_METALLICGLOSSMAP");
            if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
        }

        if (aoMap != null)
        {
            SetTexture(material, "_OcclusionMap", aoMap);
            material.EnableKeyword("_OCCLUSIONMAP");
            if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", occlusionStrength);
        }

        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", smoothness);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    static string CreateSmoothnessMap(string targetPath, string roughnessAssetPath)
    {
        Texture2D roughness;
        try
        {
            roughness = LoadImageFile(roughnessAssetPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[PolyHavenMapEnvironmentInstaller] Could not decode roughness texture: " + roughnessAssetPath + "\n" + ex.Message);
            return null;
        }

        var packed = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);

        Color32[] sourcePixels = roughness.GetPixels32();
        Color32[] outputPixels = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            byte gray = ToGray(sourcePixels[i]);
            byte smooth = (byte)(255 - gray);
            outputPixels[i] = new Color32(0, 0, 0, smooth);
        }

        packed.SetPixels32(outputPixels);
        packed.Apply(false, false);

        EnsureFolder(Path.GetDirectoryName(targetPath).Replace('\\', '/'));
        File.WriteAllBytes(AssetPathToFullPath(targetPath), packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(packed);
        UnityEngine.Object.DestroyImmediate(roughness);

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(targetPath, TextureImporterType.Default, false);
        return targetPath;
    }

    static void ConfigureTexture(string assetPath, TextureImporterType textureType, bool srgb)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = textureType;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.sRGBTexture = srgb;
        importer.SaveAndReimport();
    }

    static string FindTexturePath(string assetId, string[] tokens, string[] preferredExtensions)
    {
        string folder = SourceRoot + "/" + assetId;
        string fullFolder = AssetPathToFullPath(folder);
        if (!Directory.Exists(fullFolder))
            return null;

        string[] files = Directory.GetFiles(fullFolder, "*.*", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int e = 0; e < preferredExtensions.Length; e++)
        {
            string extension = preferredExtensions[e];
            for (int t = 0; t < tokens.Length; t++)
            {
                string token = tokens[t].ToLowerInvariant();
                for (int i = 0; i < files.Length; i++)
                {
                    if (files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string lower = Path.GetFileName(files[i]).ToLowerInvariant();
                    if (!lower.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!lower.Contains(token))
                        continue;
                    return FullPathToAssetPath(files[i]);
                }
            }
        }
        return null;
    }

    static Texture2D TryLoadTexture(string assetPath, bool required)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
        {
            string prefix = required ? "Missing required texture" : "Skipping optional texture";
            Debug.LogWarning("[PolyHavenMapEnvironmentInstaller] " + prefix + ": " + assetPath);
            return null;
        }
        return texture;
    }

    static Texture2D LoadImageFile(string assetPath)
    {
        byte[] bytes = File.ReadAllBytes(AssetPathToFullPath(assetPath));
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!texture.LoadImage(bytes))
            throw new InvalidOperationException("Could not decode texture: " + assetPath);
        return texture;
    }

    static byte ToGray(Color32 color)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt((color.r + color.g + color.b) / 3f), 0, 255);
    }

    static void SetTexture(Material material, string propertyName, Texture2D texture)
    {
        if (material == null || texture == null || !material.HasProperty(propertyName))
            return;
        material.SetTexture(propertyName, texture);
    }

    static void WriteSourceNotes()
    {
        EnsureFolder(Path.GetDirectoryName(SourceNotesPath).Replace('\\', '/'));
        string text =
            "Poly Haven map environment materials\n" +
            "\n" +
            "Provider: Poly Haven\n" +
            "License: CC0 / public domain dedication\n" +
            "License URL: https://polyhaven.com/license\n" +
            "Source root: Assets/External/PolyHaven/EnvironmentTextures\n" +
            "Download helper: Tools/Downloads/polyhaven/download_rts_map_environment.py\n" +
            "\n" +
            "Selected texture asset pages:\n" +
            "- https://polyhaven.com/a/forrest_ground_01\n" +
            "- https://polyhaven.com/a/forest_ground_04\n" +
            "- https://polyhaven.com/a/rocky_trail\n" +
            "- https://polyhaven.com/a/gravel_road\n" +
            "- https://polyhaven.com/a/aerial_beach_01\n" +
            "- https://polyhaven.com/a/aerial_mud_1\n" +
            "- https://polyhaven.com/a/snow_03\n" +
            "- https://polyhaven.com/a/concrete_floor_damaged_01\n" +
            "- https://polyhaven.com/a/asphalt_02\n" +
            "- https://polyhaven.com/a/rock_ground_02\n";

        File.WriteAllText(AssetPathToFullPath(SourceNotesPath), text);
        AssetDatabase.ImportAsset(SourceNotesPath, ImportAssetOptions.ForceUpdate);
    }

    static void EnsureFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
            return;

        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(assetFolder))
            AssetDatabase.CreateFolder(parent, name);
    }

    static string AssetPathToFullPath(string assetPath)
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot, assetPath).Replace('/', Path.DirectorySeparatorChar);
    }

    static string FullPathToAssetPath(string fullPath)
    {
        string normalized = fullPath.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;
        return "Assets" + normalized.Substring(dataPath.Length);
    }
}
