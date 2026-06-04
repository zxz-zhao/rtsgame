using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class DownloadedSciFiVehicleMaterials
{
    const string SourceRoot = "Assets/External/ambientCG/SciFiVehicleMaterials";
    const string MaterialRoot = "Assets/Resources/Materials/DownloadedSciFiVehicles";
    const string PreviewScenePath = "Assets/Scenes/DownloadedSciFiVehicleMaterialPreview.unity";
    const string SourceNotesPath = MaterialRoot + "/SOURCE_LICENSE.txt";

    static readonly string[] AssetIds =
    {
        "MetalPlates017A",
        "MetalPlates017B",
        "MetalPlates015A",
        "MetalPlates015B",
        "MetalPlates016A",
        "DiamondPlate009",
        "DiamondPlate008C",
    };

    [MenuItem("RTS/Art/Import Downloaded Sci-Fi Vehicle Materials")]
    public static void ImportFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ImportDownloadedMaterials();
    }

    public static void ImportDownloadedMaterials()
    {
        EnsureFolder(MaterialRoot);

        Material[] materials = new Material[AssetIds.Length];
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < AssetIds.Length; i++)
            {
                ConfigureDownloadedTextures(AssetIds[i]);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();

        for (int i = 0; i < AssetIds.Length; i++)
            materials[i] = CreateMaterial(AssetIds[i]);

        WriteSourceNotes();
        CreatePreviewScene(materials);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DownloadedSciFiVehicleMaterials] Imported " + materials.Length + " ambientCG material sets.");
    }

    static Material CreateMaterial(string assetId)
    {
        string materialPath = MaterialRoot + "/DL_ambientCG_" + assetId + ".mat";
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        Texture2D color = LoadTexture(assetId, "Color");
        Texture2D normal = LoadTexture(assetId, "NormalGL");
        Texture2D metalness = LoadTexture(assetId, "Metalness");
        Texture2D ao = LoadTexture(assetId, "AmbientOcclusion");

        SetTexture(material, "_MainTex", color);
        SetTexture(material, "_BumpMap", normal);
        SetTexture(material, "_MetallicGlossMap", metalness);
        SetTexture(material, "_OcclusionMap", ao);

        if (normal != null)
            material.EnableKeyword("_NORMALMAP");
        if (metalness != null)
            material.EnableKeyword("_METALLICGLOSSMAP");

        SetFloat(material, "_BumpScale", 0.72f);
        SetFloat(material, "_OcclusionStrength", 0.82f);
        SetFloat(material, "_Metallic", assetId.StartsWith("DiamondPlate") ? 0.82f : 0.90f);
        SetFloat(material, "_Glossiness", assetId.StartsWith("DiamondPlate") ? 0.48f : 0.58f);
        SetFloat(material, "_GlossMapScale", assetId.StartsWith("DiamondPlate") ? 0.48f : 0.58f);
        SetFloat(material, "_SpecularHighlights", 1f);
        SetFloat(material, "_GlossyReflections", 1f);

        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ConfigureDownloadedTextures(string assetId)
    {
        ConfigureTexture(FindTexturePath(assetId, "Color"), TextureImporterType.Default, true);
        ConfigureTexture(FindTexturePath(assetId, "AmbientOcclusion"), TextureImporterType.Default, false);
        ConfigureTexture(FindTexturePath(assetId, "Metalness"), TextureImporterType.Default, false);
        ConfigureTexture(FindTexturePath(assetId, "Roughness"), TextureImporterType.Default, false);
        ConfigureTexture(FindTexturePath(assetId, "NormalGL"), TextureImporterType.NormalMap, false);
        ConfigureTexture(FindTexturePath(assetId, "NormalDX"), TextureImporterType.NormalMap, false);
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
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.sRGBTexture = srgb;
        importer.SaveAndReimport();
    }

    static Texture2D LoadTexture(string assetId, string suffix)
    {
        string path = FindTexturePath(assetId, suffix);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string FindTexturePath(string assetId, string suffix)
    {
        string folder = SourceRoot + "/" + assetId;
        string expected = folder + "/" + assetId + "_1K-JPG_" + suffix + ".jpg";
        if (File.Exists(AssetPathToFullPath(expected)))
            return expected;

        string fullFolder = AssetPathToFullPath(folder);
        if (!Directory.Exists(fullFolder))
            return null;

        string[] matches = Directory.GetFiles(fullFolder, "*" + suffix + ".jpg", SearchOption.TopDirectoryOnly);
        if (matches.Length == 0)
            return null;

        return FullPathToAssetPath(matches[0]);
    }

    static void CreatePreviewScene(Material[] materials)
    {
        EnsureFolder("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.40f, 0.43f, 0.45f);
        RenderSettings.reflectionIntensity = 0.45f;
        RenderSettings.skybox = null;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.075f, 0.08f);
        camera.fieldOfView = 38f;
        camera.transform.position = new Vector3(0f, 5.8f, -8.6f);
        camera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

        GameObject keyLight = new GameObject("Key Light");
        Light key = keyLight.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.18f;
        key.color = new Color(0.96f, 0.98f, 1f);
        keyLight.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

        int columns = 4;
        for (int i = 0; i < materials.Length; i++)
        {
            int col = i % columns;
            int row = i / columns;
            Vector3 center = new Vector3((col - 1.5f) * 2.55f, 0f, row * 2.25f - 0.6f);
            CreatePreviewObjects(AssetIds[i], materials[i], center);
        }

        if (materials.Length > 0)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Downloaded_Material_Preview_Floor";
            floor.transform.position = new Vector3(0f, -0.12f, 0.9f);
            floor.transform.localScale = new Vector3(11f, 0.10f, 5.8f);
            SetMaterial(floor, materials[materials.Length - 1]);
        }

        EditorSceneManager.SaveScene(scene, PreviewScenePath);
    }

    static void CreatePreviewObjects(string assetId, Material material, Vector3 center)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Preview_" + assetId + "_Sphere";
        sphere.transform.position = center + new Vector3(0f, 1.04f, 0f);
        sphere.transform.localScale = Vector3.one * 0.86f;
        SetMaterial(sphere, material);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Preview_" + assetId + "_Panel";
        panel.transform.position = center + new Vector3(0f, 0.34f, 0.82f);
        panel.transform.localScale = new Vector3(1.26f, 0.24f, 0.64f);
        SetMaterial(panel, material);
    }

    static void SetMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static void WriteSourceNotes()
    {
        string text =
            "Downloaded sci-fi vehicle material sources\n" +
            "\n" +
            "Provider: ambientCG\n" +
            "License: CC0 / public domain dedication, see https://ambientcg.com/license\n" +
            "Downloads used: 1K-JPG zip packages\n" +
            "Original zip files kept under Tools/Downloads/ambientCG/SciFiVehicleMaterials\n" +
            "\n" +
            "Asset pages:\n" +
            "- https://ambientcg.com/view?id=MetalPlates017A\n" +
            "- https://ambientcg.com/view?id=MetalPlates017B\n" +
            "- https://ambientcg.com/view?id=MetalPlates015A\n" +
            "- https://ambientcg.com/view?id=MetalPlates015B\n" +
            "- https://ambientcg.com/view?id=MetalPlates016A\n" +
            "- https://ambientcg.com/view?id=DiamondPlate009\n" +
            "- https://ambientcg.com/view?id=DiamondPlate008C\n";

        string fullPath = AssetPathToFullPath(SourceNotesPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, text);
        AssetDatabase.ImportAsset(SourceNotesPath, ImportAssetOptions.ForceUpdate);
    }

    static void SetTexture(Material material, string property, Texture texture)
    {
        if (material != null && material.HasProperty(property))
            material.SetTexture(property, texture);
    }

    static void SetFloat(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace("\\", "/");
        if (assetPath == "Assets" || AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath).Replace("\\", "/");
        string name = Path.GetFileName(assetPath);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(assetPath))
            AssetDatabase.CreateFolder(parent, name);
    }

    static string AssetPathToFullPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    static string FullPathToAssetPath(string fullPath)
    {
        string normalized = fullPath.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (!normalized.StartsWith(dataPath))
            return null;

        return "Assets" + normalized.Substring(dataPath.Length);
    }
}
