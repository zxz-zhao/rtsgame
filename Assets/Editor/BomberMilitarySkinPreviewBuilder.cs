using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BomberMilitarySkinPreviewBuilder
{
    const string SkinRoot = "Assets/Resources/Materials/BomberMilitarySkins";
    const string PreviewScenePath = "Assets/Scenes/BomberMilitarySkinPreview.unity";
    const string ReferenceNotesPath = SkinRoot + "/REFERENCE_NOTES.txt";

    const string ResourceBomberPlayerPrefabPath = "Assets/Resources/Prefabs/Bomber_P.prefab";
    const string ResourceBomberEnemyPrefabPath = "Assets/Resources/Prefabs/Bomber_E.prefab";
    const string LegacyBomberPlayerPrefabPath = "Assets/Prefabs/Bomber_Player.prefab";
    const string LegacyBomberEnemyPrefabPath = "Assets/Prefabs/Bomber_Enemy.prefab";

    const string AircraftMaterial0Path = "Assets/Resources/Materials/Imported/craft_cargoB_metalDark_Mat_3D80ADFF_M000_G035.mat";
    const string AircraftMaterial1Path = "Assets/Resources/Materials/Imported/craft_cargoB_metal_Mat_3D80ADFF_M000_G035.mat";
    const string AircraftMaterial2Path = "Assets/Resources/Materials/Imported/craft_cargoB_dark_Mat_3D80ADFF_M000_G035.mat";
    const string AircraftMaterial3Path = "Assets/Resources/Materials/Imported/craft_cargoB_metalRed_Mat_3D80ADFF_M000_G035.mat";
    const string BombMaterialPath = "Assets/Resources/Materials/Imported/grenade_b_colormap_Native.mat";
    const string BombBayMaterialPath = "Assets/Resources/Materials/Imported/crate_small_colormap_Mat_5C574CFF_M000_G035.mat";
    const string TurretMaterialPath = "Assets/Resources/Materials/Imported/scope_large_a_colormap_Native.mat";
    const string PlateMaterialPath = "Assets/Resources/Materials/Imported/flag_pennant_colormap_Mat_3370D1FF_M000_G035.mat";
    const string StripeDarkMaterialPath = "Assets/Resources/Materials/Generated/Mat_3370D1FF_M003_G042.mat";
    const string StripeLightMaterialPath = "Assets/Resources/Materials/Generated/Mat_C6D7F2FF_M003_G042.mat";

    static readonly string[] BomberPrefabPaths =
    {
        ResourceBomberPlayerPrefabPath,
        ResourceBomberEnemyPrefabPath,
        LegacyBomberPlayerPrefabPath,
        LegacyBomberEnemyPrefabPath,
    };

    static readonly SkinSpec[] Skins =
    {
        new SkinSpec(
            "01_GunshipGray",
            "Gunship Gray",
            C(0x5D, 0x64, 0x69),
            C(0x74, 0x7C, 0x83),
            C(0x42, 0x49, 0x4E),
            C(0x7F, 0x89, 0x90),
            C(0x4B, 0x54, 0x59),
            C(0x88, 0x92, 0x98),
            C(0xA8, 0xB2, 0xB8),
            C(0xC7, 0xCF, 0xD4)),
        new SkinSpec(
            "02_NightOps",
            "Night Ops",
            C(0x28, 0x2C, 0x31),
            C(0x3B, 0x43, 0x49),
            C(0x16, 0x19, 0x1D),
            C(0x4E, 0x58, 0x60),
            C(0x31, 0x37, 0x3D),
            C(0x58, 0x62, 0x69),
            C(0x73, 0x7C, 0x82),
            C(0x92, 0x9A, 0xA0)),
        new SkinSpec(
            "03_DesertStrike",
            "Desert Strike",
            C(0x8F, 0x82, 0x68),
            C(0xB0, 0xA0, 0x82),
            C(0x67, 0x5F, 0x4B),
            C(0x98, 0x8B, 0x71),
            C(0x72, 0x67, 0x52),
            C(0x8E, 0x85, 0x71),
            C(0xC2, 0xB7, 0x9A),
            C(0xD3, 0xC9, 0xAE)),
        new SkinSpec(
            "04_ForestWrap",
            "Forest Wrap",
            C(0x5A, 0x63, 0x4D),
            C(0x78, 0x82, 0x5E),
            C(0x38, 0x3F, 0x34),
            C(0x6A, 0x74, 0x57),
            C(0x47, 0x50, 0x3D),
            C(0x6C, 0x73, 0x62),
            C(0x9E, 0xA5, 0x8D),
            C(0xB8, 0xBE, 0xA7)),
        new SkinSpec(
            "05_NavalBlueGray",
            "Naval Blue Gray",
            C(0x53, 0x62, 0x6B),
            C(0x6F, 0x82, 0x8E),
            C(0x37, 0x43, 0x4A),
            C(0x70, 0x80, 0x8B),
            C(0x49, 0x58, 0x61),
            C(0x7B, 0x8A, 0x94),
            C(0xA7, 0xB6, 0xBF),
            C(0xC2, 0xCF, 0xD5)),
        new SkinSpec(
            "06_ArcticPatrol",
            "Arctic Patrol",
            C(0xB9, 0xC0, 0xC4),
            C(0xE1, 0xE6, 0xE8),
            C(0x8A, 0x92, 0x97),
            C(0xC5, 0xCC, 0xD0),
            C(0x9C, 0xA4, 0xA8),
            C(0xA9, 0xB1, 0xB6),
            C(0xD0, 0xD6, 0xD9),
            C(0xEB, 0xEF, 0xF1)),
        new SkinSpec(
            "07_RAFSeaGray",
            "RAF Sea Gray",
            C(0x62, 0x67, 0x63),
            C(0x80, 0x87, 0x7F),
            C(0x44, 0x48, 0x45),
            C(0x75, 0x7B, 0x75),
            C(0x55, 0x5A, 0x55),
            C(0x7A, 0x81, 0x7A),
            C(0xA9, 0xAF, 0xA6),
            C(0xC5, 0xC9, 0xC2)),
        new SkinSpec(
            "08_StormCloud",
            "Storm Cloud",
            C(0x4F, 0x56, 0x5B),
            C(0x68, 0x71, 0x77),
            C(0x2E, 0x35, 0x39),
            C(0x60, 0x68, 0x6E),
            C(0x43, 0x4A, 0x4F),
            C(0x6D, 0x75, 0x7A),
            C(0x98, 0xA0, 0xA5),
            C(0xB8, 0xBF, 0xC3)),
        new SkinSpec(
            "09_SovietBlueGray",
            "Soviet Blue Gray",
            C(0x5C, 0x6C, 0x74),
            C(0x7C, 0x8E, 0x96),
            C(0x3C, 0x49, 0x4F),
            C(0x70, 0x80, 0x88),
            C(0x4F, 0x60, 0x66),
            C(0x7A, 0x8C, 0x93),
            C(0xA9, 0xB8, 0xBE),
            C(0xC4, 0xD0, 0xD4)),
        new SkinSpec(
            "10_WeatheredOlive",
            "Weathered Olive",
            C(0x6A, 0x6A, 0x53),
            C(0x89, 0x8A, 0x6A),
            C(0x46, 0x48, 0x3A),
            C(0x78, 0x79, 0x61),
            C(0x56, 0x57, 0x46),
            C(0x73, 0x74, 0x67),
            C(0xA4, 0xA5, 0x8A),
            C(0xC0, 0xC0, 0xA5)),
    };

    [MenuItem("RTS/Art/Bomber Skins/Build Preview Scene")]
    public static void BuildPreviewSceneFromMenu()
    {
        EnsureSkinAssetsAndPreviewScene();
    }

    [MenuItem("RTS/Art/Bomber Skins/Open Preview Scene")]
    public static void OpenPreviewSceneFromMenu()
    {
        if (!File.Exists(AssetPathToFullPath(PreviewScenePath)))
            EnsureSkinAssetsAndPreviewScene();
        if (!File.Exists(AssetPathToFullPath(PreviewScenePath)))
            return;

        EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
    }

    [MenuItem("RTS/Art/Bomber Skins/Apply/01 Gunship Gray")]
    static void ApplyGunshipGray() => ApplySkinToGamePrefabs("01_GunshipGray");

    [MenuItem("RTS/Art/Bomber Skins/Apply/02 Night Ops")]
    static void ApplyNightOps() => ApplySkinToGamePrefabs("02_NightOps");

    [MenuItem("RTS/Art/Bomber Skins/Apply/03 Desert Strike")]
    static void ApplyDesertStrike() => ApplySkinToGamePrefabs("03_DesertStrike");

    [MenuItem("RTS/Art/Bomber Skins/Apply/04 Forest Wrap")]
    static void ApplyForestWrap() => ApplySkinToGamePrefabs("04_ForestWrap");

    [MenuItem("RTS/Art/Bomber Skins/Apply/05 Naval Blue Gray")]
    static void ApplyNavalBlueGray() => ApplySkinToGamePrefabs("05_NavalBlueGray");

    [MenuItem("RTS/Art/Bomber Skins/Apply/06 Arctic Patrol")]
    static void ApplyArcticPatrol() => ApplySkinToGamePrefabs("06_ArcticPatrol");

    [MenuItem("RTS/Art/Bomber Skins/Apply/07 RAF Sea Gray")]
    static void ApplyRAFSeaGray() => ApplySkinToGamePrefabs("07_RAFSeaGray");

    [MenuItem("RTS/Art/Bomber Skins/Apply/08 Storm Cloud")]
    static void ApplyStormCloud() => ApplySkinToGamePrefabs("08_StormCloud");

    [MenuItem("RTS/Art/Bomber Skins/Apply/09 Soviet Blue Gray")]
    static void ApplySovietBlueGray() => ApplySkinToGamePrefabs("09_SovietBlueGray");

    [MenuItem("RTS/Art/Bomber Skins/Apply/10 Weathered Olive")]
    static void ApplyWeatheredOlive() => ApplySkinToGamePrefabs("10_WeatheredOlive");

    static void EnsureSkinAssetsAndPreviewScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(SkinRoot);

        SkinAssetSet[] skinAssets = new SkinAssetSet[Skins.Length];
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < Skins.Length; i++)
                skinAssets[i] = CreateSkinAssets(Skins[i]);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        WriteReferenceNotes();
        CreatePreviewScene(skinAssets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[BomberMilitarySkinPreviewBuilder] Built 10 bomber skin presets and preview scene.");
    }

    static void ApplySkinToGamePrefabs(string skinId)
    {
        SkinSpec spec = FindSkinOrThrow(skinId);
        SkinAssetSet skinAssets = CreateSkinAssets(spec);

        for (int i = 0; i < BomberPrefabPaths.Length; i++)
        {
            string prefabPath = BomberPrefabPaths[i];
            if (!File.Exists(AssetPathToFullPath(prefabPath)))
                continue;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                ApplySkinToBomberRoot(scope.prefabContentsRoot, skinAssets, includeFactionMarkers: false);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BomberMilitarySkinPreviewBuilder] Applied bomber skin to gameplay prefabs: " + spec.Label);
    }

    static void CreatePreviewScene(SkinAssetSet[] skinAssets)
    {
        EnsureFolder("Assets/Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.43f);
        RenderSettings.reflectionIntensity = 0.32f;
        RenderSettings.skybox = null;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.075f, 0.085f);
        camera.fieldOfView = 36f;
        camera.transform.position = new Vector3(0f, 10.5f, -20f);
        camera.transform.rotation = Quaternion.Euler(28f, 0f, 0f);

        GameObject keyLight = new GameObject("Key Light");
        Light key = keyLight.AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.15f;
        key.color = new Color(0.97f, 0.98f, 1f);
        keyLight.transform.rotation = Quaternion.Euler(42f, -36f, 0f);

        GameObject fillLight = new GameObject("Fill Light");
        Light fill = fillLight.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.38f;
        fill.color = new Color(0.68f, 0.73f, 0.82f);
        fillLight.transform.rotation = Quaternion.Euler(56f, 132f, 0f);

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "PreviewDeck";
        floor.transform.position = new Vector3(0f, 0f, 3.5f);
        floor.transform.localScale = new Vector3(4.1f, 1f, 2.9f);
        floor.GetComponent<Renderer>().sharedMaterial = BuildPlainMaterial(
            "PreviewDeckMat",
            new Color(0.17f, 0.18f, 0.19f),
            0.12f,
            0.28f);

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "RunwayStripe";
        stripe.transform.position = new Vector3(0f, 0.01f, 3.5f);
        stripe.transform.localScale = new Vector3(1.6f, 0.02f, 17.5f);
        stripe.GetComponent<Renderer>().sharedMaterial = BuildPlainMaterial(
            "PreviewRunwayStripeMat",
            new Color(0.65f, 0.67f, 0.66f),
            0.02f,
            0.18f);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceBomberPlayerPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Bomber preview source prefab missing: " + ResourceBomberPlayerPrefabPath);

        const int columns = 5;
        const float spacingX = 7.2f;
        const float spacingZ = 8.3f;
        for (int i = 0; i < skinAssets.Length; i++)
        {
            int col = i % columns;
            int row = i / columns;
            Vector3 position = new Vector3((col - 2f) * spacingX, 0f, row * spacingZ + 0.6f);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                continue;

            instance.name = Skins[i].Id + "_" + Skins[i].Label.Replace(" ", string.Empty);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
            ApplySkinToBomberRoot(instance, skinAssets[i], includeFactionMarkers: true);
            DisablePreviewScripts(instance);
        }

        EditorSceneManager.SaveScene(scene, PreviewScenePath);
    }

    static void DisablePreviewScripts(GameObject root)
    {
        if (root == null)
            return;

        Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is Camera)
                continue;
            if (behaviours[i] is Animator)
                continue;
            behaviours[i].enabled = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    static void ApplySkinToBomberRoot(GameObject root, SkinAssetSet skinAssets, bool includeFactionMarkers)
    {
        if (root == null || skinAssets == null)
            return;

        SetRendererMaterials(root.transform.Find("Model/KenneyAircraft"), skinAssets.AircraftBody);
        SetRendererMaterial(root.transform.Find("Model/KenneyBombA"), skinAssets.Bomb);
        SetRendererMaterial(root.transform.Find("Model/KenneyBombB"), skinAssets.Bomb);
        SetRendererMaterial(root.transform.Find("Model/KenneyBombBayDetail"), skinAssets.BombBay);
        SetRendererMaterial(root.transform.Find("Model/KenneyDorsalTurret"), skinAssets.Turret);

        if (!includeFactionMarkers)
            return;

        SetRendererMaterial(root.transform.Find("Model/FactionPlate"), skinAssets.Plate);
        SetRendererMaterial(root.transform.Find("Model/FactionStripeL"), skinAssets.StripeDark);
        SetRendererMaterial(root.transform.Find("Model/FactionStripeR"), skinAssets.StripeDark);
        SetRendererMaterial(root.transform.Find("Model/AircraftForwardStripe"), skinAssets.StripeLight);
    }

    static void SetRendererMaterials(Transform target, Material[] materials)
    {
        if (target == null || materials == null || materials.Length == 0)
            return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
            return;

        Material[] shared = renderer.sharedMaterials;
        if (shared == null || shared.Length == 0)
        {
            renderer.sharedMaterial = materials[0];
            return;
        }

        for (int i = 0; i < shared.Length && i < materials.Length; i++)
            shared[i] = materials[i];
        renderer.sharedMaterials = shared;
        EditorUtility.SetDirty(renderer);
    }

    static void SetRendererMaterial(Transform target, Material material)
    {
        if (target == null || material == null)
            return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
            return;

        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
    }

    static SkinAssetSet CreateSkinAssets(SkinSpec spec)
    {
        string folder = SkinRoot + "/" + spec.Id;
        EnsureFolder(folder);

        SkinAssetSet assets = new SkinAssetSet
        {
            AircraftBody = new[]
            {
                CloneMaterial(AircraftMaterial0Path, folder + "/" + spec.Id + "_AircraftSlot0.mat", spec.Id + "_AircraftSlot0", spec.BodyDark, 0.26f, 0.20f),
                CloneMaterial(AircraftMaterial1Path, folder + "/" + spec.Id + "_AircraftSlot1.mat", spec.Id + "_AircraftSlot1", spec.BodyLight, 0.30f, 0.22f),
                CloneMaterial(AircraftMaterial2Path, folder + "/" + spec.Id + "_AircraftSlot2.mat", spec.Id + "_AircraftSlot2", spec.Underside, 0.18f, 0.16f),
                CloneMaterial(AircraftMaterial3Path, folder + "/" + spec.Id + "_AircraftSlot3.mat", spec.Id + "_AircraftSlot3", spec.Accent, 0.24f, 0.20f),
            },
            Bomb = CloneMaterial(BombMaterialPath, folder + "/" + spec.Id + "_Bomb.mat", spec.Id + "_Bomb", spec.Bomb, 0.14f, 0.12f),
            BombBay = CloneMaterial(BombBayMaterialPath, folder + "/" + spec.Id + "_BombBay.mat", spec.Id + "_BombBay", Lerp(spec.Bomb, spec.Underside, 0.35f), 0.14f, 0.10f),
            Turret = CloneMaterial(TurretMaterialPath, folder + "/" + spec.Id + "_Turret.mat", spec.Id + "_Turret", spec.Turret, 0.26f, 0.18f),
            Plate = CloneMaterial(PlateMaterialPath, folder + "/" + spec.Id + "_Plate.mat", spec.Id + "_Plate", spec.MarkingDark, 0.05f, 0.16f),
            StripeDark = CloneMaterial(StripeDarkMaterialPath, folder + "/" + spec.Id + "_StripeDark.mat", spec.Id + "_StripeDark", spec.MarkingDark, 0.03f, 0.12f),
            StripeLight = CloneMaterial(StripeLightMaterialPath, folder + "/" + spec.Id + "_StripeLight.mat", spec.Id + "_StripeLight", spec.MarkingLight, 0.04f, 0.15f),
        };

        return assets;
    }

    static Material CloneMaterial(string sourcePath, string targetPath, string materialName, Color color, float metallic, float glossiness)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null)
            throw new InvalidOperationException("Missing source material: " + sourcePath);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (material == null)
        {
            material = new Material(source);
            AssetDatabase.CreateAsset(material, targetPath);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
            material.shader = source.shader;
        }

        material.name = materialName;
        RendererColorUtil.TrySetColor(material, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", glossiness);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", glossiness);
        if (material.HasProperty("_GlossMapScale")) material.SetFloat("_GlossMapScale", glossiness);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material BuildPlainMaterial(string materialName, Color color, float metallic, float glossiness)
    {
        string path = SkinRoot + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.name = materialName;
        RendererColorUtil.TrySetColor(material, color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", glossiness);
        EditorUtility.SetDirty(material);
        return material;
    }

    static SkinSpec FindSkinOrThrow(string skinId)
    {
        for (int i = 0; i < Skins.Length; i++)
        {
            if (string.Equals(Skins[i].Id, skinId, StringComparison.Ordinal))
                return Skins[i];
        }

        throw new InvalidOperationException("Unknown bomber skin id: " + skinId);
    }

    static void WriteReferenceNotes()
    {
        string text =
            "Bomber military skin preset notes\n" +
            "\n" +
            "Preview scene: Assets/Scenes/BomberMilitarySkinPreview.unity\n" +
            "Material root: Assets/Resources/Materials/BomberMilitarySkins\n" +
            "\n" +
            "Preset inspirations:\n" +
            "- 01 Gunship Gray -> B-52H Stratofortress -> https://www.af.mil/About-Us/Fact-Sheets/Display/Article/104465/b-52-stratofortress/source/b-52h-stratofortress/\n" +
            "- 02 Night Ops -> B-2 Spirit -> https://www.afnwc.af.mil/About-Us/Fact-Sheets/Article/2074643/b-2-spirit/\n" +
            "- 03 Desert Strike -> B-52D Stratofortress -> https://www.nationalmuseum.af.mil/Visit/Museum-Exhibits/Fact-Sheets/Display/Article/195815/boeing-b-52d-stratofortress/\n" +
            "- 04 Forest Wrap -> Avro Vulcan -> https://en.wikipedia.org/wiki/Avro_Vulcan\n" +
            "- 05 Naval Blue Gray -> Tupolev Tu-95 -> https://en.wikipedia.org/wiki/Tupolev_Tu-95\n" +
            "- 06 Arctic Patrol -> Tupolev Tu-160 -> https://en.wikipedia.org/wiki/Tupolev_Tu-160\n" +
            "- 07 RAF Sea Gray -> Handley Page Victor -> https://en.wikipedia.org/wiki/Handley_Page_Victor\n" +
            "- 08 Storm Cloud -> B-1B Lancer -> https://www.8af.af.mil/About-Us/Fact-Sheets/Display/Article/1085866/b-1b-lancer/\n" +
            "- 09 Soviet Blue Gray -> Tupolev Tu-22M -> https://en.wikipedia.org/wiki/Tupolev_Tu-22M\n" +
            "- 10 Weathered Olive -> Xi'an H-6 / Ilyushin Il-28 -> https://en.wikipedia.org/wiki/Xi%27an_H-6 | https://en.wikipedia.org/wiki/Ilyushin_Il-28\n";

        string fullPath = AssetPathToFullPath(ReferenceNotesPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, text);
        AssetDatabase.ImportAsset(ReferenceNotesPath, ImportAssetOptions.ForceUpdate);
    }

    static Color Lerp(Color a, Color b, float t)
    {
        return Color.Lerp(a, b, Mathf.Clamp01(t));
    }

    static Color C(byte r, byte g, byte b)
    {
        return new Color32(r, g, b, 255);
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

    sealed class SkinAssetSet
    {
        public Material[] AircraftBody;
        public Material Bomb;
        public Material BombBay;
        public Material Turret;
        public Material Plate;
        public Material StripeDark;
        public Material StripeLight;
    }

    struct SkinSpec
    {
        public readonly string Id;
        public readonly string Label;
        public readonly Color BodyDark;
        public readonly Color BodyLight;
        public readonly Color Underside;
        public readonly Color Accent;
        public readonly Color Bomb;
        public readonly Color Turret;
        public readonly Color MarkingDark;
        public readonly Color MarkingLight;

        public SkinSpec(
            string id,
            string label,
            Color bodyDark,
            Color bodyLight,
            Color underside,
            Color accent,
            Color bomb,
            Color turret,
            Color markingDark,
            Color markingLight)
        {
            Id = id;
            Label = label;
            BodyDark = bodyDark;
            BodyLight = bodyLight;
            Underside = underside;
            Accent = accent;
            Bomb = bomb;
            Turret = turret;
            MarkingDark = markingDark;
            MarkingLight = markingLight;
        }
    }
}
