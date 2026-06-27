using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    // Touching this file forces Unity to re-evaluate editor-side import helpers in the current workspace.
    private const string FullBuildTriggerName = "BUILD_NOW";
    private const string PrefabRebuildTriggerName = "REBUILD_PREFABS_NOW";

    private static readonly string WatchDir = Application.dataPath;
    private static FileSystemWatcher watcher;
    private static volatile bool pendingFullBuild;
    private static volatile bool pendingPrefabRebuild;

    static AutoBuildTrigger()
    {
        StartWatcher();
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.quitting += StopWatcher;
    }

    private static void StartWatcher()
    {
        StopWatcher();

        watcher = new FileSystemWatcher(WatchDir)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };
        watcher.Created += OnTrigger;
        watcher.Changed += OnTrigger;

        // Unity 脚本重载时，触发文件可能已经存在；这里主动补扫一次，避免漏掉构建请求。
        pendingFullBuild |= File.Exists(Path.Combine(WatchDir, FullBuildTriggerName));
        pendingPrefabRebuild |= File.Exists(Path.Combine(WatchDir, PrefabRebuildTriggerName));

        Debug.Log("[AutoBuildTrigger] Watching Assets/BUILD_NOW and Assets/REBUILD_PREFABS_NOW.");
    }

    private static void StopWatcher()
    {
        if (watcher == null) return;

        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        watcher = null;
    }

    private static void OnTrigger(object sender, FileSystemEventArgs e)
    {
        string name = Path.GetFileName(e.FullPath);
        if (string.Equals(name, FullBuildTriggerName, StringComparison.OrdinalIgnoreCase))
        {
            pendingFullBuild = true;
            return;
        }

        if (string.Equals(name, PrefabRebuildTriggerName, StringComparison.OrdinalIgnoreCase))
        {
            pendingPrefabRebuild = true;
        }
    }

    private static void OnEditorUpdate()
    {
        if (!pendingFullBuild && !pendingPrefabRebuild) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying)
        {
            // 构建不能在 Play Mode 中执行；收到触发后自动退出播放，下一帧再继续构建。
            Debug.Log("[AutoBuildTrigger] Build trigger is pending, exiting Play Mode first.");
            EditorApplication.ExitPlaymode();
            return;
        }

        bool runFullBuild = pendingFullBuild;
        bool runPrefabRebuild = pendingPrefabRebuild;
        pendingFullBuild = false;
        pendingPrefabRebuild = false;

        DeleteTriggerFile(FullBuildTriggerName);
        DeleteTriggerFile(PrefabRebuildTriggerName);

        if (runFullBuild)
        {
            RunFullBuild();
            return;
        }

        if (runPrefabRebuild)
        {
            RunPrefabRebuild();
        }
    }

    private static void DeleteTriggerFile(string triggerName)
    {
        string path = Path.Combine(WatchDir, triggerName);
        if (!File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AutoBuildTrigger] Could not delete trigger file " + triggerName + ": " + ex.Message);
        }
    }

    private static void RunFullBuild()
    {
        try
        {
            Debug.Log("[AutoBuildTrigger] BUILD_NOW detected - starting full build.");
            BuildAll.RunAll();
        }
        catch (Exception ex)
        {
            Debug.LogError("[AutoBuildTrigger] Full build failed: " + ex);
        }
    }

    private static void RunPrefabRebuild()
    {
        try
        {
            Debug.Log("[AutoBuildTrigger] REBUILD_PREFABS_NOW detected - rebuilding downloaded-model prefabs.");
            PrefabBuilder.BuildAllPrefabs();
            Debug.Log("[AutoBuildTrigger] Downloaded-model prefab rebuild complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[AutoBuildTrigger] Prefab rebuild failed: " + ex);
        }
    }
}

[InitializeOnLoad]
public static class UserDownloadedModelIntegrator
{
    const string ImportRoot = "Assets/External/UserModels";
    const string UnitRoot = ImportRoot + "/Units";
    const string PropRoot = ImportRoot + "/Props";
    const string WeaponRoot = ImportRoot + "/Weapons";
    const string ResourcePropRoot = "Assets/Resources/Prefabs/Props";
    const string ResourceWeaponRoot = "Assets/Resources/Prefabs/Weapons";
    const string VersionKey = "RTS_UserDownloadedModelIntegrator_Version";
    const string VersionValue = "2026-06-08-user-models-v10-enemy-infantry-repair";
    const string ReportPath = "ExternalRawAssets/UserImport_20260606/import_report.txt";

    static readonly string RawRoot = Path.Combine(ProjectRoot, "ExternalRawAssets", "UserImport_20260606");

    static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    static UserDownloadedModelIntegrator()
    {
        EditorApplication.delayCall += TryAutoIntegrate;
    }

    [MenuItem("RTS/Asset Integration/Import User Downloaded Models")]
    public static void Integrate()
    {
        RunIntegration(writeReport: true);
        EditorPrefs.SetString(VersionKey, VersionValue);
    }

    static void TryAutoIntegrate()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoIntegrate;
            return;
        }

        if (EditorPrefs.GetString(VersionKey, string.Empty) == VersionValue)
            return;
        if (!Directory.Exists(RawRoot))
            return;

        try
        {
            RunIntegration(writeReport: true);
            EditorPrefs.SetString(VersionKey, VersionValue);
        }
        catch (Exception ex)
        {
            WriteReport("FAILED\n" + ex);
            Debug.LogError("[UserDownloadedModelIntegrator] Auto integration failed.\n" + ex);
        }
    }

    static void RunIntegration(bool writeReport)
    {
        EnsureFolders();
        CopyRawAssetsIntoProject();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureImportedModels();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        GameObject enemyInfantry = LoadRequired<GameObject>(UnitRoot + "/EnemyInfantry/bubing.fpglb");
        GameObject artillery = LoadRequired<GameObject>(UnitRoot + "/Artillery/30f22cfb80f34e4198d2aa3020a1dff6.fbx");
        GameObject lightTank = LoadRequired<GameObject>(UnitRoot + "/LightTank/Untitled.fbx");
        GameObject heavyTank = LoadRequired<GameObject>(UnitRoot + "/HeavyTank/pt91m-pendekar.fbx");
        GameObject scoutHelicopter = LoadRequired<GameObject>(UnitRoot + "/ScoutPlane/helicopter.fpglb");
        GameObject warScene = LoadRequired<GameObject>(PropRoot + "/WarScene/Science Special Search Party.fbx");
        GameObject merrick556 = LoadRequired<GameObject>(WeaponRoot + "/Merrick556/Merrick556.fbx");

        // The imported enemy infantry GLB has a skinned skeleton but no animation clips.
        // Keep the raw asset in-project for inspection, but repair active infantry prefabs
        // from the animated player infantry source so walking, weapon binding, and hand pose work.
        WarnIfEnemyInfantryHasNoAnimation(enemyInfantry);
        RepairInfantryPrefabsFromAnimatedSource("Assets/Prefabs/Infantry_Player.prefab",
            "Assets/Prefabs/Infantry_Enemy.prefab");
        RepairInfantryPrefabsFromAnimatedSource("Assets/Resources/Prefabs/Infantry_P.prefab",
            "Assets/Resources/Prefabs/Infantry_E.prefab",
            "Assets/Resources/Prefabs/Infantry.prefab");

        ReplacePrefabModels(artillery, 1f, new Vector3(-90f, 180f, 0f), false, true,
            "Assets/Prefabs/Artillery_Player.prefab",
            "Assets/Prefabs/Artillery_Enemy.prefab",
            "Assets/Resources/Prefabs/Artillery_P.prefab",
            "Assets/Resources/Prefabs/Artillery_E.prefab",
            "Assets/Resources/Prefabs/Artillery.prefab",
            "Assets/Resources/Prefabs/Artillery_Player.prefab",
            "Assets/Resources/Prefabs/Artillery_Enemy.prefab");

        ReplacePrefabModels(lightTank, 1f, Vector3.zero, true, false,
            "Assets/Resources/Prefabs/LightTank_P.prefab",
            "Assets/Resources/Prefabs/LightTank_E.prefab");

        ReplacePrefabModels(heavyTank, 1f, Vector3.zero, true, false,
            "Assets/Resources/Prefabs/HeavyTank_P.prefab",
            "Assets/Resources/Prefabs/HeavyTank_E.prefab");

        ReplacePrefabModelsCustom(scoutHelicopter, 1f, Vector3.zero, true, false, "KenneyAircraft",
            new[] { "KenneySensor", "FactionPlate" }, UnitVisualAnimator.VisualStyle.Aircraft,
            "Assets/Resources/Prefabs/ScoutPlane_P.prefab",
            "Assets/Resources/Prefabs/ScoutPlane_E.prefab",
            "Assets/Resources/Prefabs/ScoutPlane.prefab",
            "Assets/Resources/Prefabs/Scout_Player.prefab",
            "Assets/Resources/Prefabs/Scout_Enemy.prefab",
            "Assets/Prefabs/Scout_Player.prefab",
            "Assets/Prefabs/Scout_Enemy.prefab");

        ReplacePrefabWeapons(merrick556, 0.82f, 0.03f,
            "Assets/Prefabs/Infantry_Player.prefab",
            "Assets/Resources/Prefabs/Infantry_Player.prefab",
            "Assets/Resources/Prefabs/Infantry_P.prefab");

        CreateWarScenePropPrefab(warScene);
        CreateWeaponPrefab(merrick556, "Merrick556_Weapon", 0.82f, 0.03f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (writeReport)
        {
            WriteReport(
                "OK\n" +
                "EnemyInfantry=Assets/Resources/Prefabs/Infantry_E.prefab\n" +
                "Artillery=Assets/Resources/Prefabs/Artillery_E.prefab\n" +
                "LightTank=Assets/Resources/Prefabs/LightTank_E.prefab\n" +
                "HeavyTank=Assets/Resources/Prefabs/HeavyTank_E.prefab\n" +
                "ScoutPlane=Assets/Resources/Prefabs/ScoutPlane_P.prefab\n" +
                "PlayerWeapon=Assets/Resources/Prefabs/Infantry_P.prefab\n" +
                "WeaponPrefab=Assets/Resources/Prefabs/Weapons/Merrick556_Weapon.prefab\n" +
                "WarSceneProp=Assets/Resources/Prefabs/Props/BattlefieldProp_UserWarScene.prefab\n");
        }

        Debug.Log("[UserDownloadedModelIntegrator] User downloaded models integrated.");
    }

    static void EnsureFolders()
    {
        EnsureFolder(ImportRoot);
        EnsureFolder(UnitRoot);
        EnsureFolder(UnitRoot + "/EnemyInfantry");
        EnsureFolder(UnitRoot + "/Artillery");
        EnsureFolder(UnitRoot + "/LightTank");
        EnsureFolder(UnitRoot + "/HeavyTank");
        EnsureFolder(UnitRoot + "/ScoutPlane");
        EnsureFolder(PropRoot);
        EnsureFolder(PropRoot + "/WarScene");
        EnsureFolder(ResourcePropRoot);
        EnsureFolder(WeaponRoot);
        EnsureFolder(WeaponRoot + "/Merrick556");
        EnsureFolder(ResourceWeaponRoot);
    }

    static void CopyRawAssetsIntoProject()
    {
        CopyFile(Path.Combine("F:" + Path.DirectorySeparatorChar, "\u8fc5\u96f7\u4e0b\u8f7d", "\u6a21\u578b", "bubing.glb"),
            UnitRoot + "/EnemyInfantry/bubing.fpglb");

        CopyFile(Path.Combine(RawRoot, "Artillery", "source", "30f22cfb80f34e4198d2aa3020a1dff6.fbx"),
            UnitRoot + "/Artillery/30f22cfb80f34e4198d2aa3020a1dff6.fbx");
        CopyTree(Path.Combine(RawRoot, "Artillery", "textures"), UnitRoot + "/Artillery");

        CopyFile(Path.Combine(RawRoot, "LightTank", "source", "Extracted", "Untitled.fbx"),
            UnitRoot + "/LightTank/Untitled.fbx");
        CopyTree(Path.Combine(RawRoot, "LightTank", "textures"), UnitRoot + "/LightTank");
        CopyTree(Path.Combine(RawRoot, "LightTank", "source", "Extracted"), UnitRoot + "/LightTank");

        CopyFile(Path.Combine(RawRoot, "HeavyTank", "source", "Extracted", "pt91m-pendekar.fbx"),
            UnitRoot + "/HeavyTank/pt91m-pendekar.fbx");
        CopyTree(Path.Combine(RawRoot, "HeavyTank", "textures"), UnitRoot + "/HeavyTank");
        CopyTree(Path.Combine(RawRoot, "HeavyTank", "source", "Extracted"), UnitRoot + "/HeavyTank");

        CopyFile(Path.Combine("F:" + Path.DirectorySeparatorChar, "\u8fc5\u96f7\u4e0b\u8f7d", "\u6a21\u578b", "\u76f4\u5347\u673a.glb"),
            UnitRoot + "/ScoutPlane/helicopter.fpglb");

        CopyFile(Path.Combine(RawRoot, "WarScene", "source", "Extracted", "Science Special Search Party.fbx"),
            PropRoot + "/WarScene/Science Special Search Party.fbx");
        CopyTree(Path.Combine(RawRoot, "WarScene", "textures"), PropRoot + "/WarScene");
        CopyTree(Path.Combine(RawRoot, "WarScene", "source", "Extracted"), PropRoot + "/WarScene");

        CopyFile(Path.Combine(RawRoot, "Weapons_Merrick556", "source", "Extracted", "Merrick556", "Merrick556.fbx"),
            WeaponRoot + "/Merrick556/Merrick556.fbx");
        CopyTree(Path.Combine(RawRoot, "Weapons_Merrick556", "source", "Extracted", "Merrick556"), WeaponRoot + "/Merrick556");
        CopyTree(Path.Combine(RawRoot, "Weapons_Merrick556", "textures"), WeaponRoot + "/Merrick556");
    }

    static void ConfigureImportedModels()
    {
        ConfigureModel(UnitRoot + "/Artillery/30f22cfb80f34e4198d2aa3020a1dff6.fbx", 0.01f, ModelImporterAnimationType.None);
        ConfigureModel(UnitRoot + "/LightTank/Untitled.fbx", 0.01f, ModelImporterAnimationType.None);
        ConfigureModel(UnitRoot + "/HeavyTank/pt91m-pendekar.fbx", 0.01f, ModelImporterAnimationType.None);
        ConfigureModel(PropRoot + "/WarScene/Science Special Search Party.fbx", 0.01f, ModelImporterAnimationType.None);
        ConfigureModel(WeaponRoot + "/Merrick556/Merrick556.fbx", 0.01f, ModelImporterAnimationType.None);
    }

    static void ConfigureModel(string assetPath, float scale, ModelImporterAnimationType animationType)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return;

        bool changed = false;
        if (Math.Abs(importer.globalScale - scale) > 0.0001f)
        {
            importer.globalScale = scale;
            changed = true;
        }
        if (importer.animationType != animationType)
        {
            importer.animationType = animationType;
            changed = true;
        }
        if (importer.importBlendShapes)
        {
            importer.importBlendShapes = false;
            changed = true;
        }
        if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
        {
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    static void ReplacePrefabModels(GameObject modelAsset, float sizeBias, Vector3 euler, bool preserveMuzzle, bool forceVehicleStyle, params string[] prefabPaths)
    {
        ReplacePrefabModelsInternal(modelAsset, sizeBias, euler, preserveMuzzle, forceVehicleStyle, null, null, null, prefabPaths);
    }

    static void ReplacePrefabModelsCustom(GameObject modelAsset, float sizeBias, Vector3 euler, bool preserveMuzzle, bool forceVehicleStyle,
        string instanceName, string[] preservedChildNames, UnitVisualAnimator.VisualStyle? preferredVisualStyle, params string[] prefabPaths)
    {
        ReplacePrefabModelsInternal(modelAsset, sizeBias, euler, preserveMuzzle, forceVehicleStyle, instanceName, preservedChildNames, preferredVisualStyle, prefabPaths);
    }

    static void ReplacePrefabModelsInternal(GameObject modelAsset, float sizeBias, Vector3 euler, bool preserveMuzzle, bool forceVehicleStyle,
        string instanceName, string[] preservedChildNames, UnitVisualAnimator.VisualStyle? preferredVisualStyle, string[] prefabPaths)
    {
        foreach (string prefabPath in prefabPaths)
        {
            if (!File.Exists(ToAbsolute(prefabPath)))
            {
                Debug.LogWarning("[UserDownloadedModelIntegrator] Prefab missing: " + prefabPath);
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                Transform modelNode = scope.prefabContentsRoot.transform.Find("Model");
                if (modelNode == null)
                {
                    GameObject modelGo = new GameObject("Model");
                    modelGo.transform.SetParent(scope.prefabContentsRoot.transform, false);
                    modelNode = modelGo.transform;
                }

                List<Transform> preserved = null;
                if (preservedChildNames != null && preservedChildNames.Length > 0)
                    preserved = DetachNamedChildren(modelNode, preservedChildNames);
                if (preserveMuzzle)
                {
                    List<Transform> muzzleTransforms = DetachNamedChildren(modelNode, "Muzzle");
                    if (muzzleTransforms.Count > 0)
                    {
                        preserved ??= new List<Transform>();
                        preserved.AddRange(muzzleTransforms);
                    }
                }
                for (int i = modelNode.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(modelNode.GetChild(i).gameObject);

                GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset, modelNode) as GameObject;
                if (instance == null)
                    instance = UnityEngine.Object.Instantiate(modelAsset, modelNode);

                instance.name = string.IsNullOrWhiteSpace(instanceName)
                    ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(modelAsset)) + "_Instance"
                    : instanceName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(euler);
                instance.transform.localScale = Vector3.one;

                ApplyTargetScale(instance, prefabPath, sizeBias);
                CenterModelOnGround(modelNode, instance);

                if (preserved != null)
                {
                    for (int i = 0; i < preserved.Count; i++)
                    {
                        if (preserved[i] == null) continue;
                        preserved[i].SetParent(modelNode, false);
                        preserved[i].gameObject.SetActive(true);
                    }
                    EnsureMuzzle(modelNode, instance);
                }

                var visualAnimator = scope.prefabContentsRoot.GetComponent<UnitVisualAnimator>();
                if (visualAnimator != null)
                {
                    visualAnimator.VisualRoot = modelNode;
                    if (preferredVisualStyle.HasValue)
                        visualAnimator.Style = preferredVisualStyle.Value;
                    else if (forceVehicleStyle || instance.GetComponentInChildren<Animator>(true) == null)
                        visualAnimator.Style = UnitVisualAnimator.VisualStyle.Vehicle;
                }

                EditorUtility.SetDirty(scope.prefabContentsRoot);
            }
        }
    }

    static void WarnIfEnemyInfantryHasNoAnimation(GameObject modelAsset)
    {
        if (modelAsset == null)
            return;

        AnimationClip[] clips = AnimationUtility.GetAnimationClips(modelAsset);
        if (clips != null && clips.Length > 0)
            return;

        Debug.LogWarning(
            "[UserDownloadedModelIntegrator] Enemy infantry import has bones/skins but no animation clips. " +
            "Keeping the asset for inspection and cloning the animated infantry prefab for active unit visuals.");
    }

    static void RepairInfantryPrefabsFromAnimatedSource(string sourcePrefabPath, params string[] targetPrefabPaths)
    {
        GameObject sourcePrefab = LoadRequired<GameObject>(sourcePrefabPath);
        Transform sourceModel = sourcePrefab != null ? sourcePrefab.transform.Find("Model") : null;
        if (sourceModel == null)
            throw new InvalidDataException("Animated infantry source prefab is missing its Model node: " + sourcePrefabPath);

        foreach (string targetPrefabPath in targetPrefabPaths)
        {
            if (!File.Exists(ToAbsolute(targetPrefabPath)))
            {
                Debug.LogWarning("[UserDownloadedModelIntegrator] Prefab missing: " + targetPrefabPath);
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(targetPrefabPath))
            {
                Transform root = scope.prefabContentsRoot.transform;
                Transform existingModel = root.Find("Model");
                if (existingModel != null)
                    UnityEngine.Object.DestroyImmediate(existingModel.gameObject);

                Transform model = UnityEngine.Object.Instantiate(sourceModel.gameObject, root).transform;
                model.name = "Model";
                model.localPosition = sourceModel.localPosition;
                model.localRotation = sourceModel.localRotation;
                model.localScale = sourceModel.localScale;

                Animator animator = model.GetComponentInChildren<Animator>(true);
                Transform weaponRoot = FindByName(model, "KenneyWeapon");

                UnitVisualAnimator visualAnimator = scope.prefabContentsRoot.GetComponent<UnitVisualAnimator>();
                if (visualAnimator != null)
                {
                    visualAnimator.Style = UnitVisualAnimator.VisualStyle.Infantry;
                    visualAnimator.VisualRoot = model;
                }

                AnimatedUnitAttachmentBinder attachmentBinder = model.GetComponentInChildren<AnimatedUnitAttachmentBinder>(true);
                if (attachmentBinder != null)
                {
                    attachmentBinder.VisualRoot = model;
                    attachmentBinder.AnimatedRoot = animator != null ? animator.transform : null;
                }

                BasicShooterRifleHandBinder handBinder = scope.prefabContentsRoot.GetComponent<BasicShooterRifleHandBinder>();
                if (handBinder != null)
                {
                    handBinder.WeaponRoot = weaponRoot;
                    handBinder.AnimatedRoot = animator != null ? animator.transform : null;
                    handBinder.ForwardOffset = new Vector3(0.02f, -0.015f, 0.08f);
                    handBinder.EulerOffset = Vector3.zero;
                }

                EditorUtility.SetDirty(scope.prefabContentsRoot);
            }
        }
    }

    static List<Transform> DetachNamedChildren(Transform parent, params string[] names)
    {
        var result = new List<Transform>();
        if (parent == null)
            return result;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (!NameMatches(child.name, names))
                continue;

            child.SetParent(null, true);
            result.Add(child);
        }

        return result;
    }

    static void ReplacePrefabWeapons(GameObject weaponAsset, float targetLength, float muzzlePadding, params string[] prefabPaths)
    {
        foreach (string prefabPath in prefabPaths)
        {
            if (!File.Exists(ToAbsolute(prefabPath)))
            {
                Debug.LogWarning("[UserDownloadedModelIntegrator] Prefab missing: " + prefabPath);
                continue;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                Transform weaponRoot = FindByName(scope.prefabContentsRoot.transform, "KenneyWeapon");
                if (weaponRoot == null)
                {
                    Transform modelNode = scope.prefabContentsRoot.transform.Find("Model");
                    if (modelNode == null)
                    {
                        GameObject modelGo = new GameObject("Model");
                        modelGo.transform.SetParent(scope.prefabContentsRoot.transform, false);
                        modelNode = modelGo.transform;
                    }

                    GameObject weaponRootGo = new GameObject("KenneyWeapon");
                    weaponRootGo.transform.SetParent(modelNode, false);
                    weaponRoot = weaponRootGo.transform;
                }

                for (int i = weaponRoot.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(weaponRoot.GetChild(i).gameObject);

                GameObject instance = PrefabUtility.InstantiatePrefab(weaponAsset, weaponRoot) as GameObject;
                if (instance == null)
                    instance = UnityEngine.Object.Instantiate(weaponAsset, weaponRoot);

                instance.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(weaponAsset)) + "_Instance";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                PrepareWeaponVisual(weaponRoot, instance, targetLength, muzzlePadding);
                weaponRoot.gameObject.SetActive(true);
                EditorUtility.SetDirty(scope.prefabContentsRoot);
            }
        }
    }

    static bool NameMatches(string candidate, string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(candidate, names[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static void PrepareWeaponVisual(Transform weaponRoot, GameObject instance, float targetLength, float muzzlePadding)
    {
        if (weaponRoot == null || instance == null)
            return;

        RemoveChildCamerasAndLights(instance);
        AutoOrientWeapon(instance.transform);
        ScaleWeaponToLength(instance.transform, targetLength);
        CenterModel(instance.transform, weaponRoot, centerY: true, centerZ: true);
        EnsureWeaponMuzzle(weaponRoot, instance, muzzlePadding);
    }

    static void ApplyTargetScale(GameObject instance, string prefabPath, float sizeBias)
    {
        if (!TryGetTargetSize(Path.GetFileNameWithoutExtension(prefabPath), out float targetHeight, out float targetFootprint))
            return;
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
        float currentFootprint = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z), 0.01f);
        float scale = Mathf.Min(targetHeight / currentHeight, targetFootprint / currentFootprint) * Mathf.Max(0.01f, sizeBias);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            return;

        instance.transform.localScale *= scale;
    }

    static bool TryGetTargetSize(string prefabName, out float targetHeight, out float targetFootprint)
    {
        string key = NormalizePrefabKey(prefabName);
        switch (key)
        {
            case "Infantry":
                targetHeight = 3.6f; targetFootprint = 2.0f; return true;
            case "Artillery":
                targetHeight = 3.2f; targetFootprint = 5.2f; return true;
            case "LightTank":
                targetHeight = 2.7f; targetFootprint = 3.2f; return true;
            case "HeavyTank":
                targetHeight = 3.55f; targetFootprint = 4.35f; return true;
            case "ScoutPlane":
                targetHeight = 1.15f; targetFootprint = 3.0f; return true;
            default:
                targetHeight = 0f; targetFootprint = 0f; return false;
        }
    }

    static string NormalizePrefabKey(string prefabName)
    {
        string key = prefabName;
        string[] suffixes = { "_Player", "_Enemy", "_P", "_E" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (key.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                return key.Substring(0, key.Length - suffixes[i].Length);
        }
        return key;
    }

    static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool first = true;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (first)
            {
                bounds = renderer.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return !first;
    }

    static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool first = true;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 local = root.InverseTransformPoint(corners[c]);
                if (first)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return !first;
    }

    static void CenterModelOnGround(Transform parent, GameObject instance)
    {
        if (parent == null || instance == null)
            return;
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        Vector3 centerLocal = parent.InverseTransformPoint(bounds.center);
        Vector3 minLocal = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        instance.transform.localPosition -= new Vector3(centerLocal.x, minLocal.y, centerLocal.z);
    }

    static void CenterModel(Transform model, Transform parent, bool centerY, bool centerZ)
    {
        if (model == null || parent == null)
            return;
        if (!TryGetRendererBounds(model.gameObject, out Bounds bounds))
            return;

        Vector3 centerLocal = parent.InverseTransformPoint(bounds.center);
        model.localPosition -= new Vector3(centerLocal.x, centerY ? centerLocal.y : 0f, centerZ ? centerLocal.z : 0f);
    }

    static void EnsureMuzzle(Transform modelNode, GameObject instance)
    {
        Transform muzzle = FindByName(modelNode, "Muzzle");
        if (muzzle != null)
            return;

        GameObject go = new GameObject("Muzzle");
        go.transform.SetParent(modelNode, false);
        if (TryGetRendererBounds(instance, out Bounds bounds))
            go.transform.position = new Vector3(bounds.center.x, bounds.center.y + bounds.extents.y * 0.35f, bounds.max.z);
        else
            go.transform.localPosition = new Vector3(0f, 1.2f, 1.8f);
        go.transform.localRotation = Quaternion.identity;
    }

    static void EnsureWeaponMuzzle(Transform weaponRoot, GameObject instance, float muzzlePadding)
    {
        if (weaponRoot == null)
            return;

        Transform muzzle = FindByName(weaponRoot, "Muzzle");
        if (muzzle == null)
        {
            GameObject muzzleGo = new GameObject("Muzzle");
            muzzle = muzzleGo.transform;
            muzzle.SetParent(weaponRoot, false);
        }

        if (instance != null && TryGetLocalRendererBounds(weaponRoot, out Bounds bounds))
            muzzle.localPosition = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + Mathf.Max(0.005f, muzzlePadding));
        else
            muzzle.localPosition = new Vector3(0f, 0f, 0.42f);

        muzzle.localRotation = Quaternion.identity;
        muzzle.localScale = Vector3.one;
    }

    static void AutoOrientWeapon(Transform weaponModel)
    {
        if (weaponModel == null)
            return;

        Vector3 forward = GuessWeaponForward(weaponModel);
        if (forward.sqrMagnitude < 0.0001f)
            forward = GuessPrimaryAxis(weaponModel);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.right;

        Quaternion alignForward = Quaternion.FromToRotation(forward.normalized, Vector3.forward);
        weaponModel.localRotation = alignForward * weaponModel.localRotation;

        Vector3 up = GuessWeaponUp(weaponModel);
        Vector3 projectedUp = Vector3.ProjectOnPlane(up, Vector3.forward);
        if (projectedUp.sqrMagnitude > 0.0001f)
        {
            Quaternion alignUp = Quaternion.FromToRotation(projectedUp.normalized, Vector3.up);
            weaponModel.localRotation = alignUp * weaponModel.localRotation;
        }
    }

    static void ScaleWeaponToLength(Transform weaponModel, float targetLength)
    {
        if (weaponModel == null || targetLength <= 0f)
            return;
        if (!TryGetLocalRendererBounds(weaponModel, out Bounds bounds))
            return;

        float currentLength = Mathf.Max(bounds.size.z, 0.0001f);
        float scale = targetLength / currentLength;
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            return;

        weaponModel.localScale *= scale;
    }

    static Vector3 GuessWeaponForward(Transform weaponModel)
    {
        Vector3 barrelAverage;
        Vector3 stockAverage;
        bool hasBarrel = TryGetNamedPointAverage(weaponModel,
            new[] { "barrel", "muzzle", "suppressor", "nozzle", "flash", "hider", "tip" }, out barrelAverage);
        bool hasStock = TryGetNamedPointAverage(weaponModel,
            new[] { "stock", "grip", "mag", "magazine", "receiver", "ammo" }, out stockAverage);

        if (hasBarrel && hasStock)
            return barrelAverage - stockAverage;

        if (hasBarrel && TryGetLocalRendererBounds(weaponModel, out Bounds bounds))
            return barrelAverage - bounds.center;

        return GuessPrimaryAxis(weaponModel);
    }

    static Vector3 GuessWeaponUp(Transform weaponModel)
    {
        Vector3 sightAverage;
        if (TryGetNamedPointAverage(weaponModel,
            new[] { "sight", "scope", "rail", "optic", "top" }, out sightAverage))
        {
            if (TryGetLocalRendererBounds(weaponModel, out Bounds bounds))
                return sightAverage - bounds.center;
            return sightAverage;
        }

        return Vector3.up;
    }

    static Vector3 GuessPrimaryAxis(Transform weaponModel)
    {
        if (!TryGetLocalRendererBounds(weaponModel, out Bounds bounds))
            return Vector3.zero;

        Vector3 size = bounds.size;
        if (size.x >= size.y && size.x >= size.z)
            return Vector3.right;
        if (size.y >= size.x && size.y >= size.z)
            return Vector3.up;
        return Vector3.forward;
    }

    static bool TryGetNamedPointAverage(Transform root, string[] tokens, out Vector3 average)
    {
        average = Vector3.zero;
        if (root == null || tokens == null || tokens.Length == 0)
            return false;

        var points = new List<Vector3>();
        CollectNamedPoints(root, root, tokens, points);
        if (points.Count == 0)
            return false;

        for (int i = 0; i < points.Count; i++)
            average += points[i];
        average /= points.Count;
        return true;
    }

    static void CollectNamedPoints(Transform localRoot, Transform current, string[] tokens, List<Vector3> points)
    {
        if (current == null)
            return;

        if (NameContainsAny(current.name, tokens))
            points.Add(localRoot.InverseTransformPoint(current.position));

        Renderer renderer = current.GetComponent<Renderer>();
        if (renderer != null && NameContainsAny(renderer.name, tokens))
            points.Add(localRoot.InverseTransformPoint(renderer.bounds.center));

        foreach (Transform child in current)
            CollectNamedPoints(localRoot, child, tokens, points);
    }

    static bool NameContainsAny(string value, string[] tokens)
    {
        if (string.IsNullOrEmpty(value) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static Transform FindByName(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindByName(child, targetName);
            if (found != null)
                return found;
        }
        return null;
    }

    static void CreateWarScenePropPrefab(GameObject warScene)
    {
        string prefabPath = ResourcePropRoot + "/BattlefieldProp_UserWarScene.prefab";
        GameObject root = new GameObject("BattlefieldProp_UserWarScene");
        try
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(warScene, root.transform) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(warScene, root.transform);

            instance.name = "ScienceSpecialSearchParty_Instance";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            ApplyPropScale(instance, 24f);
            CenterModelOnGround(root.transform, instance);
            RemoveChildCamerasAndLights(instance);
            EnsureModelCollider(instance);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void CreateWeaponPrefab(GameObject weaponAsset, string prefabName, float targetLength, float muzzlePadding)
    {
        string prefabPath = ResourceWeaponRoot + "/" + prefabName + ".prefab";
        GameObject root = new GameObject("KenneyWeapon");
        try
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(weaponAsset, root.transform) as GameObject;
            if (instance == null)
                instance = UnityEngine.Object.Instantiate(weaponAsset, root.transform);

            instance.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(weaponAsset)) + "_Instance";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            PrepareWeaponVisual(root.transform, instance, targetLength, muzzlePadding);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void ApplyPropScale(GameObject instance, float targetFootprint)
    {
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float footprint = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z), 0.01f);
        float scale = targetFootprint / footprint;
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            return;

        instance.transform.localScale *= scale;
    }

    static void EnsureModelCollider(GameObject instance)
    {
        if (instance.GetComponentInChildren<Collider>(true) != null)
            return;

        var collider = instance.AddComponent<BoxCollider>();
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        Vector3 localCenter = instance.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, instance.transform.lossyScale.x),
            bounds.size.y / Mathf.Max(0.0001f, instance.transform.lossyScale.y),
            bounds.size.z / Mathf.Max(0.0001f, instance.transform.lossyScale.z));
        collider.center = localCenter;
        collider.size = localSize;
    }

    static void RemoveChildCamerasAndLights(GameObject root)
    {
        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            UnityEngine.Object.DestroyImmediate(camera);
        foreach (var light in root.GetComponentsInChildren<Light>(true))
            UnityEngine.Object.DestroyImmediate(light);
    }

    static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new FileNotFoundException("Imported asset not found or not yet imported: " + assetPath);
        return asset;
    }

    static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void CopyFile(string sourceAbsolute, string destAssetPath)
    {
        string destAbsolute = ToAbsolute(destAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destAbsolute));
        if (!File.Exists(sourceAbsolute))
            throw new FileNotFoundException(sourceAbsolute);
        File.Copy(sourceAbsolute, destAbsolute, true);
    }

    static void CopyTree(string sourceAbsoluteDir, string destAssetDir)
    {
        if (!Directory.Exists(sourceAbsoluteDir))
            return;

        string dest = ToAbsolute(destAssetDir);
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(sourceAbsoluteDir, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = file.Substring(sourceAbsoluteDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }

    static string ToAbsolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    static void WriteReport(string text)
    {
        string absolute = Path.Combine(ProjectRoot, ReportPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllText(absolute, text);
    }
}
