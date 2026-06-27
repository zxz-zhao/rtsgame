using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WW2MaterialSkinInstaller
{
    const string TextureRoot = "Assets/Resources/Textures/WW2Skins";
    const string GeneratedTextureRoot = TextureRoot + "/Generated";
    const string AircraftMaterialRoot = "Assets/Resources/Materials/WW2Skins/Aircraft";
    const string InfantryMaterialRoot = "Assets/Resources/Materials/InfantryUniforms";
    const string SourceNotesPath = "Assets/Resources/Materials/WW2Skins/SOURCE_LICENSE.txt";
    const string AutoInstallKey = "UnityRTS.WW2MaterialSkinInstaller.AutoInstallVersion";
    const string AutoInstallVersion = "2026-06-05.painted-metal-006";

    const string PaintedMetalColor = TextureRoot + "/PaintedMetal006/PaintedMetal006_Color.jpg";
    const string PaintedMetalNormal = TextureRoot + "/PaintedMetal006/PaintedMetal006_NormalGL.jpg";
    const string PaintedMetalRoughness = TextureRoot + "/PaintedMetal006/PaintedMetal006_Roughness.jpg";
    const string PaintedMetalMetalness = TextureRoot + "/PaintedMetal006/PaintedMetal006_Metalness.jpg";
    const string PaintedMetalAo = TextureRoot + "/PaintedMetal006/PaintedMetal006_AO.jpg";

    const string FabricColor = TextureRoot + "/Fabric0001/Fabric0001_Color.jpg";
    const string FabricNormal = TextureRoot + "/Fabric0001/Fabric0001_NormalGL.png";
    const string FabricRoughness = TextureRoot + "/Fabric0001/Fabric0001_Roughness.jpg";
    const string FabricAo = TextureRoot + "/Fabric0001/Fabric0001_AO.jpg";

    const string LeatherColor = TextureRoot + "/Leather014/Leather014_Color.jpg";
    const string LeatherNormal = TextureRoot + "/Leather014/Leather014_NormalGL.jpg";
    const string LeatherRoughness = TextureRoot + "/Leather014/Leather014_Roughness.jpg";

    static readonly string[] AircraftPrefabPaths =
    {
        "Assets/Resources/Prefabs/Fighter.prefab",
        "Assets/Resources/Prefabs/Fighter_P.prefab",
        "Assets/Resources/Prefabs/Fighter_E.prefab",
        "Assets/Resources/Prefabs/Fighter_Player.prefab",
        "Assets/Resources/Prefabs/Fighter_Enemy.prefab",
        "Assets/Resources/Prefabs/Bomber.prefab",
        "Assets/Resources/Prefabs/Bomber_P.prefab",
        "Assets/Resources/Prefabs/Bomber_E.prefab",
        "Assets/Resources/Prefabs/Bomber_Player.prefab",
        "Assets/Resources/Prefabs/Bomber_Enemy.prefab",
        "Assets/Prefabs/Fighter_Player.prefab",
        "Assets/Prefabs/Fighter_Enemy.prefab",
        "Assets/Prefabs/Bomber_Player.prefab",
        "Assets/Prefabs/Bomber_Enemy.prefab",
    };

    [MenuItem("RTS/Art/WW2 Materials/Install Aircraft And Infantry Skins")]
    public static void InstallFromMenu()
    {
        InstallSkins();
    }

    [InitializeOnLoadMethod]
    static void AutoInstallOnceWhenEditorReloads()
    {
        if (Application.isBatchMode)
            return;
        if (EditorPrefs.GetString(AutoInstallKey, string.Empty) == AutoInstallVersion)
            return;

        EditorApplication.delayCall += TryAutoInstallOnce;
    }

    static void TryAutoInstallOnce()
    {
        if (EditorPrefs.GetString(AutoInstallKey, string.Empty) == AutoInstallVersion)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoInstallOnce;
            return;
        }
        if (!File.Exists(AssetPathToFullPath(PaintedMetalColor)) || !File.Exists(AssetPathToFullPath(FabricColor)))
            return;

        try
        {
            InstallSkins();
            EditorPrefs.SetString(AutoInstallKey, AutoInstallVersion);
        }
        catch (Exception e)
        {
            Debug.LogError("[WW2MaterialSkinInstaller] Auto-install failed: " + e);
        }
    }

    public static void InstallSkins()
    {
        EnsureFolder(GeneratedTextureRoot);
        EnsureFolder(AircraftMaterialRoot);
        EnsureFolder(InfantryMaterialRoot);

        ConfigureSourceTextures();

        string paintedMetalSmooth = CreateMetallicSmoothnessMap(
            GeneratedTextureRoot + "/PaintedMetal006_MetallicSmoothness.png",
            PaintedMetalMetalness,
            PaintedMetalRoughness,
            0.45f);
        string fabricSmooth = CreateMetallicSmoothnessMap(
            GeneratedTextureRoot + "/Fabric0001_Smoothness.png",
            null,
            FabricRoughness,
            0f);
        string leatherSmooth = CreateMetallicSmoothnessMap(
            GeneratedTextureRoot + "/Leather014_Smoothness.png",
            null,
            LeatherRoughness,
            0f);

        Texture2D aircraftColor = LoadTexture(PaintedMetalColor);
        Texture2D aircraftNormal = LoadTexture(PaintedMetalNormal);
        Texture2D aircraftAo = LoadTexture(PaintedMetalAo);
        Texture2D aircraftMs = LoadTexture(paintedMetalSmooth);

        Texture2D fabricColor = LoadTexture(FabricColor);
        Texture2D fabricNormal = LoadTexture(FabricNormal);
        Texture2D fabricAo = LoadTexture(FabricAo);
        Texture2D fabricMs = LoadTexture(fabricSmooth);

        Texture2D leatherColor = LoadTexture(LeatherColor);
        Texture2D leatherNormal = LoadTexture(LeatherNormal);
        Texture2D leatherMs = LoadTexture(leatherSmooth);

        Material aircraftDark = CreatePbrMaterial(
            AircraftMaterialRoot + "/WW2_PaintedMetal006_AircraftDark.mat",
            "WW2_PaintedMetal006_AircraftDark",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.56f, 0.61f, 0.42f, 1f),
            0.46f,
            0.34f,
            0.96f,
            new Vector2(1.35f, 1.35f));
        Material aircraftLight = CreatePbrMaterial(
            AircraftMaterialRoot + "/WW2_PaintedMetal006_AircraftLight.mat",
            "WW2_PaintedMetal006_AircraftLight",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.72f, 0.76f, 0.54f, 1f),
            0.42f,
            0.38f,
            0.92f,
            new Vector2(1.25f, 1.25f));
        Material aircraftUnderside = CreatePbrMaterial(
            AircraftMaterialRoot + "/WW2_PaintedMetal006_AircraftUnderside.mat",
            "WW2_PaintedMetal006_AircraftUnderside",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.43f, 0.48f, 0.37f, 1f),
            0.36f,
            0.30f,
            1f,
            new Vector2(1.45f, 1.45f));
        Material aircraftAccent = CreatePbrMaterial(
            AircraftMaterialRoot + "/WW2_PaintedMetal006_AircraftAccent.mat",
            "WW2_PaintedMetal006_AircraftAccent",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.34f, 0.34f, 0.27f, 1f),
            0.52f,
            0.28f,
            1f,
            new Vector2(1.6f, 1.6f));

        Material[] aircraftSlots =
        {
            aircraftDark,
            aircraftLight,
            aircraftUnderside,
            aircraftAccent,
        };
        ApplyAircraftMaterials(aircraftSlots, aircraftAccent, aircraftUnderside);

        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_PlayerJacket.mat",
            "InfantryUniform_PlayerJacket",
            fabricColor,
            fabricNormal,
            fabricMs,
            fabricAo,
            new Color(0.70f, 0.82f, 0.60f, 1f),
            0f,
            0.23f,
            0.9f,
            new Vector2(1.8f, 1.8f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_EnemyJacket.mat",
            "InfantryUniform_EnemyJacket",
            fabricColor,
            fabricNormal,
            fabricMs,
            fabricAo,
            new Color(0.86f, 0.72f, 0.52f, 1f),
            0f,
            0.22f,
            0.88f,
            new Vector2(1.8f, 1.8f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_PlayerHelmet.mat",
            "InfantryUniform_PlayerHelmet",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.40f, 0.49f, 0.29f, 1f),
            0.34f,
            0.27f,
            1f,
            new Vector2(2.2f, 2.2f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_EnemyHelmet.mat",
            "InfantryUniform_EnemyHelmet",
            aircraftColor,
            aircraftNormal,
            aircraftMs,
            aircraftAo,
            new Color(0.44f, 0.37f, 0.24f, 1f),
            0.34f,
            0.25f,
            1f,
            new Vector2(2.2f, 2.2f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_Webbing.mat",
            "InfantryUniform_Webbing",
            leatherColor,
            leatherNormal,
            leatherMs,
            null,
            new Color(0.50f, 0.36f, 0.20f, 1f),
            0f,
            0.30f,
            1f,
            new Vector2(1.4f, 1.4f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_Boots.mat",
            "InfantryUniform_Boots",
            leatherColor,
            leatherNormal,
            leatherMs,
            null,
            new Color(0.26f, 0.18f, 0.11f, 1f),
            0f,
            0.27f,
            1f,
            new Vector2(1.5f, 1.5f));
        CreatePbrMaterial(
            InfantryMaterialRoot + "/InfantryUniform_Bedroll.mat",
            "InfantryUniform_Bedroll",
            fabricColor,
            fabricNormal,
            fabricMs,
            fabricAo,
            new Color(0.56f, 0.61f, 0.43f, 1f),
            0f,
            0.20f,
            0.9f,
            new Vector2(1.6f, 1.6f));

        WriteSourceNotes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WW2MaterialSkinInstaller] Installed PaintedMetal006 aircraft skins and WW2 infantry uniform materials.");
    }

    static void ConfigureSourceTextures()
    {
        ConfigureTexture(PaintedMetalColor, false, true);
        ConfigureTexture(PaintedMetalNormal, true, false);
        ConfigureTexture(PaintedMetalRoughness, false, false);
        ConfigureTexture(PaintedMetalMetalness, false, false);
        ConfigureTexture(PaintedMetalAo, false, false);

        ConfigureTexture(FabricColor, false, true);
        ConfigureTexture(FabricNormal, true, false);
        ConfigureTexture(FabricRoughness, false, false);
        ConfigureTexture(FabricAo, false, false);

        ConfigureTexture(LeatherColor, false, true);
        ConfigureTexture(LeatherNormal, true, false);
        ConfigureTexture(LeatherRoughness, false, false);
    }

    static void ConfigureTexture(string path, bool normalMap, bool srgb)
    {
        if (!File.Exists(AssetPathToFullPath(path)))
            throw new FileNotFoundException("Missing WW2 material texture", path);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normalMap && srgb;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    static string CreateMetallicSmoothnessMap(string targetPath, string metalnessPath, string roughnessPath, float fallbackMetallic)
    {
        EnsureFolder(Path.GetDirectoryName(targetPath).Replace('\\', '/'));

        Texture2D roughness = LoadImageFile(roughnessPath);
        Texture2D metalness = string.IsNullOrEmpty(metalnessPath) ? null : LoadImageFile(metalnessPath);
        int width = roughness.width;
        int height = roughness.height;
        Color32[] roughPixels = roughness.GetPixels32();
        Color32[] metalPixels = metalness != null ? metalness.GetPixels32() : null;
        Color32[] output = new Color32[roughPixels.Length];

        byte fallbackMetal = (byte)Mathf.Clamp(Mathf.RoundToInt(fallbackMetallic * 255f), 0, 255);
        for (int i = 0; i < output.Length; i++)
        {
            byte metal = fallbackMetal;
            if (metalPixels != null && i < metalPixels.Length)
                metal = ToGray(metalPixels[i]);

            byte rough = ToGray(roughPixels[i]);
            byte smooth = (byte)(255 - rough);
            output[i] = new Color32(metal, metal, metal, smooth);
        }

        Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
        packed.SetPixels32(output);
        packed.Apply(true, false);
        File.WriteAllBytes(AssetPathToFullPath(targetPath), packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(packed);
        UnityEngine.Object.DestroyImmediate(roughness);
        if (metalness != null)
            UnityEngine.Object.DestroyImmediate(metalness);

        AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        ConfigureTexture(targetPath, false, false);
        return targetPath;
    }

    static Material CreatePbrMaterial(
        string materialPath,
        string materialName,
        Texture2D colorMap,
        Texture2D normalMap,
        Texture2D metallicSmoothnessMap,
        Texture2D aoMap,
        Color tint,
        float metallic,
        float smoothness,
        float occlusionStrength,
        Vector2 tiling)
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
        RendererColorUtil.TrySetColor(material, tint);
        SetTexture(material, "_MainTex", colorMap, tiling);
        SetTexture(material, "_BaseMap", colorMap, tiling);

        if (normalMap != null)
        {
            SetTexture(material, "_BumpMap", normalMap, tiling);
            SetTexture(material, "_NormalMap", normalMap, tiling);
            material.EnableKeyword("_NORMALMAP");
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.76f);
        }

        if (metallicSmoothnessMap != null)
        {
            SetTexture(material, "_MetallicGlossMap", metallicSmoothnessMap, tiling);
            material.EnableKeyword("_METALLICGLOSSMAP");
        }

        if (aoMap != null)
        {
            SetTexture(material, "_OcclusionMap", aoMap, tiling);
            material.EnableKeyword("_OCCLUSIONMAP");
        }

        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", smoothness);
        if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", occlusionStrength);
        if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyAircraftMaterials(Material[] aircraftSlots, Material accent, Material underside)
    {
        for (int i = 0; i < AircraftPrefabPaths.Length; i++)
        {
            string prefabPath = AircraftPrefabPaths[i];
            if (!File.Exists(AssetPathToFullPath(prefabPath)))
                continue;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                SetRendererMaterials(FindChild(root.transform, "KenneyAircraft"), aircraftSlots);
                SetRendererMaterial(FindChild(root.transform, "KenneyNosePod"), accent);
                SetRendererMaterial(FindChild(root.transform, "KenneyBombA"), accent);
                SetRendererMaterial(FindChild(root.transform, "KenneyBombB"), accent);
                SetRendererMaterial(FindChild(root.transform, "KenneyBombBayDetail"), underside);
                SetRendererMaterial(FindChild(root.transform, "KenneyDorsalTurret"), accent);
            }
        }
    }

    static void SetRendererMaterials(Transform target, Material[] materials)
    {
        if (target == null || materials == null || materials.Length == 0)
            return;

        Renderer renderer = target.GetComponent<Renderer>() ?? target.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
            return;

        Material[] shared = renderer.sharedMaterials;
        if (shared == null || shared.Length == 0)
        {
            renderer.sharedMaterial = materials[0];
        }
        else
        {
            for (int i = 0; i < shared.Length; i++)
                shared[i] = materials[Mathf.Min(i, materials.Length - 1)];
            renderer.sharedMaterials = shared;
        }

        EditorUtility.SetDirty(renderer);
    }

    static void SetRendererMaterial(Transform target, Material material)
    {
        if (target == null || material == null)
            return;

        Renderer renderer = target.GetComponent<Renderer>() ?? target.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
            return;

        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }

    static Transform FindChild(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    static void SetTexture(Material material, string propertyName, Texture2D texture, Vector2 tiling)
    {
        if (material == null || texture == null || !material.HasProperty(propertyName))
            return;

        material.SetTexture(propertyName, texture);
        material.SetTextureScale(propertyName, tiling);
    }

    static Texture2D LoadTexture(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture == null)
            throw new InvalidOperationException("Could not load texture: " + assetPath);
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

    static void WriteSourceNotes()
    {
        EnsureFolder(Path.GetDirectoryName(SourceNotesPath).Replace('\\', '/'));
        File.WriteAllText(AssetPathToFullPath(SourceNotesPath),
            "WW2 skin material sources\n" +
            "\n" +
            "Aircraft and steel helmet painted metal:\n" +
            "ambientCG PaintedMetal006, 1K JPG PBR maps, CC0 / public domain.\n" +
            "https://ambientcg.com/view?id=PaintedMetal006\n" +
            "\n" +
            "Infantry camouflage fabric:\n" +
            "TextureCan Fabric 0001 camouflage vinyl fabric, 1K PBR maps, CC0.\n" +
            "https://www.texturecan.com/details/11/\n" +
            "\n" +
            "Infantry leather boots and webbing:\n" +
            "ambientCG Leather014, 1K JPG PBR maps, CC0 / public domain.\n" +
            "https://ambientcg.com/view?id=Leather014\n");
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
}
