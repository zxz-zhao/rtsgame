using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MixamoBasicShooterInstaller
{
    const int InstallerVersion = 6;
    const string SourceFolder = "Assets/External/Mixamo/BasicShooter";
    const string ModelPath = SourceFolder + "/X Bot.fbx";
    const string ControllerPath = "Assets/Resources/Animations/Generated/BasicShooter.controller";
    const string AutoInstallMarker = SourceFolder + "/.install_requested";
    static bool s_AutoInstallRunning;

    static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/Infantry_Player.prefab",
        "Assets/Prefabs/Infantry_Enemy.prefab",
        "Assets/Resources/Prefabs/Infantry.prefab",
        "Assets/Resources/Prefabs/Infantry_P.prefab",
        "Assets/Resources/Prefabs/Infantry_E.prefab",
        "Assets/Resources/Prefabs/Infantry_Player.prefab",
        "Assets/Resources/Prefabs/Infantry_Enemy.prefab",
        "Assets/Prefabs/Artillery_Player.prefab",
        "Assets/Prefabs/Artillery_Enemy.prefab",
        "Assets/Resources/Prefabs/Artillery.prefab",
        "Assets/Resources/Prefabs/Artillery_P.prefab",
        "Assets/Resources/Prefabs/Artillery_E.prefab",
        "Assets/Resources/Prefabs/Artillery_Player.prefab",
        "Assets/Resources/Prefabs/Artillery_Enemy.prefab",
    };

    [MenuItem("RTS/Asset Integration/Install Mixamo Basic Shooter")]
    public static void Install()
    {
        if (!File.Exists(ModelPath))
        {
            Debug.LogError("Mixamo Basic Shooter model not found: " + ModelPath);
            return;
        }

        ImportHumanoidAssets();
        AnimatorController controller = CreateController();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null || controller == null)
        {
            Debug.LogError("Failed to load Mixamo model or generated controller.");
            return;
        }

        int updated = 0;
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            if (File.Exists(PrefabPaths[i]) && InstallIntoPrefab(PrefabPaths[i], modelAsset, controller))
                updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (File.Exists(AutoInstallMarker))
            File.Delete(AutoInstallMarker);
        Debug.Log("Mixamo Basic Shooter installer v" + InstallerVersion + " installed into " + updated + " infantry prefabs.");
    }

    [InitializeOnLoadMethod]
    static void InstallWhenRequested()
    {
        EditorApplication.update -= InstallWhenRequestedOnEditorUpdate;
        EditorApplication.update += InstallWhenRequestedOnEditorUpdate;
        EditorApplication.delayCall += InstallWhenRequestedOnEditorUpdate;
    }

    static void InstallWhenRequestedOnEditorUpdate()
    {
        if (s_AutoInstallRunning || !File.Exists(AutoInstallMarker))
            return;

        s_AutoInstallRunning = true;
        try
        {
            Install();
        }
        finally
        {
            s_AutoInstallRunning = false;
        }
    }

    static void ImportHumanoidAssets()
    {
        string[] fbxPaths = Directory.GetFiles(SourceFolder, "*.fbx", SearchOption.TopDirectoryOnly);

        ModelImporter modelImporter = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (modelImporter != null)
        {
            modelImporter.animationType = ModelImporterAnimationType.Human;
            modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            modelImporter.importAnimation = true;
            modelImporter.SaveAndReimport();
        }

        Avatar sourceAvatar = LoadAvatar(ModelPath);
        for (int i = 0; i < fbxPaths.Length; i++)
        {
            string assetPath = ToAssetPath(fbxPaths[i]);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            ConfigureLoopingClips(importer, assetPath);
            if (assetPath != ModelPath && sourceAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            importer.SaveAndReimport();
        }
    }

    static void ConfigureLoopingClips(ModelImporter importer, string assetPath)
    {
        if (importer == null || !ShouldLoopClip(assetPath))
            return;

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return;

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = true;
            clips[i].loopPose = true;
            clips[i].wrapMode = WrapMode.Loop;
        }

        importer.clipAnimations = clips;
    }

    static bool ShouldLoopClip(string assetPath)
    {
        string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        return name.Contains("idle")
            || name.Contains("walk")
            || name.Contains("run")
            || name.Contains("strafe");
    }

    static AnimatorController CreateController()
    {
        EnsureDirectory(Path.GetDirectoryName(ControllerPath));

        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimationClip idleClip = LoadClip(SourceFolder + "/rifle aiming idle.fbx");
        AnimationClip runClip = LoadClip(SourceFolder + "/rifle run.fbx") ?? LoadClip(SourceFolder + "/walking.fbx");
        AnimationClip fireClip = LoadClip(SourceFolder + "/firing rifle.fbx");
        AnimationClip hitClip = LoadClip(SourceFolder + "/hit reaction.fbx");

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle");
        idle.motion = idleClip;
        sm.defaultState = idle;

        AnimatorState run = null;
        if (runClip != null)
        {
            run = sm.AddState("Run");
            run.motion = runClip;

            AnimatorStateTransition toRun = idle.AddTransition(run);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");
            toRun.duration = 0.12f;

            AnimatorStateTransition toIdle = run.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            toIdle.duration = 0.12f;
        }

        if (fireClip != null)
        {
            AnimatorState fire = sm.AddState("Fire");
            fire.motion = fireClip;

            AnimatorStateTransition toFire = sm.AddAnyStateTransition(fire);
            toFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            toFire.duration = 0.05f;
            toFire.canTransitionToSelf = false;

            AnimatorStateTransition back = fire.AddTransition(run != null ? run : idle);
            back.hasExitTime = true;
            back.exitTime = 0.86f;
            back.duration = 0.08f;
        }

        if (hitClip != null)
        {
            AnimatorState hit = sm.AddState("Die");
            hit.motion = hitClip;

            AnimatorStateTransition toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            toHit.duration = 0.05f;
            toHit.canTransitionToSelf = false;
        }

        return controller;
    }

    static bool InstallIntoPrefab(string prefabPath, GameObject modelAsset, RuntimeAnimatorController controller)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents == null)
            return false;

        try
        {
            Transform modelRoot = contents.transform.Find("Model");
            if (modelRoot == null)
            {
                GameObject model = new GameObject("Model");
                model.transform.SetParent(contents.transform, false);
                modelRoot = model.transform;
            }

            ClearChildren(modelRoot);
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one;

            GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset, modelRoot) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(modelAsset, modelRoot);

            instance.name = "MixamoBasicShooter";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.enabled = true;

            bool artillery = IsArtilleryPrefab(prefabPath);
            bool playerOwned = IsPlayerPrefab(prefabPath);
            GameObject weapon = artillery ? BuildShoulderCannon(modelRoot) : BuildRifle(modelRoot);
            GameObject factionPlate = BuildFactionPlate(modelRoot);
            BuildSkinOverlay(modelRoot);
            BuildUniformOverlay(modelRoot, playerOwned);

            UnitVisualAnimator visualAnimator = contents.GetComponent<UnitVisualAnimator>();
            if (visualAnimator != null)
                visualAnimator.VisualRoot = modelRoot;

            AnimatedUnitAttachmentBinder binder = contents.GetComponentInChildren<AnimatedUnitAttachmentBinder>(true);
            if (binder == null)
                binder = contents.AddComponent<AnimatedUnitAttachmentBinder>();
            binder.VisualRoot = modelRoot;
            binder.AnimatedRoot = animator.transform;
            EnsureWeaponBinding(binder, artillery);

            BasicShooterRifleHandBinder handBinder = contents.GetComponent<BasicShooterRifleHandBinder>();
            if (handBinder == null)
                handBinder = contents.AddComponent<BasicShooterRifleHandBinder>();
            handBinder.WeaponRoot = weapon.transform;
            handBinder.AnimatedRoot = animator.transform;
            handBinder.ForwardOffset = artillery
                ? new Vector3(0.04f, 0.02f, 0.10f)
                : new Vector3(0.02f, -0.015f, 0.08f);
            handBinder.EulerOffset = artillery ? new Vector3(0f, 0f, -4f) : Vector3.zero;

            binder.BindAttachments();

            weapon.SetActive(true);
            factionPlate.SetActive(true);

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static bool IsArtilleryPrefab(string prefabPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(prefabPath);
        return fileName != null
            && fileName.IndexOf("Artillery", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsPlayerPrefab(string prefabPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(prefabPath);
        return fileName != null
            && (fileName.EndsWith("_P", System.StringComparison.OrdinalIgnoreCase)
                || fileName.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static void EnsureWeaponBinding(AnimatedUnitAttachmentBinder binder, bool artillery)
    {
        var weaponBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "KenneyWeapon",
            BoneCandidates = new[] { "RightHand", "mixamorig:RightHand", "RightForeArm", "RightArm", "mixamorig:RightForeArm", "mixamorig:RightArm" },
            PreserveWorldPose = false,
            LocalPosition = artillery ? new Vector3(0.16f, 0.03f, 0.26f) : new Vector3(0.18f, -0.02f, 0.26f),
            LocalEulerAngles = artillery ? new Vector3(2f, 0f, -7f) : new Vector3(4f, 0f, -8f),
            LocalScale = Vector3.one * (artillery ? 1.08f : 1.18f),
        };

        var plateBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "FactionPlate",
            BoneCandidates = new[] { "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0.12f, 0.12f),
            LocalEulerAngles = new Vector3(0f, 0f, 0f),
            LocalScale = Vector3.one * 1.35f,
        };

        var faceBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "SkinFace",
            BoneCandidates = new[] { "Head", "mixamorig:Head" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0.015f, 0.095f),
            LocalEulerAngles = new Vector3(0f, 0f, 0f),
            LocalScale = Vector3.one * 1.0f,
        };

        var leftHandBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "SkinLeftHand",
            BoneCandidates = new[] { "LeftHand", "mixamorig:LeftHand" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0f, 0.02f),
            LocalEulerAngles = new Vector3(0f, 0f, 0f),
            LocalScale = Vector3.one,
        };

        var rightHandBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "SkinRightHand",
            BoneCandidates = new[] { "RightHand", "mixamorig:RightHand" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0f, 0.02f),
            LocalEulerAngles = new Vector3(0f, 0f, 0f),
            LocalScale = Vector3.one,
        };

        var helmetBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "InfantryUniformHelmet",
            BoneCandidates = new[] { "Head", "mixamorig:Head" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0.045f, 0.005f),
            LocalEulerAngles = Vector3.zero,
            LocalScale = Vector3.one,
        };

        var jacketBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "InfantryUniformJacket",
            BoneCandidates = new[] { "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0.02f, 0.055f),
            LocalEulerAngles = Vector3.zero,
            LocalScale = Vector3.one,
        };

        var backpackBinding = new AnimatedUnitAttachmentBinder.AttachmentBinding
        {
            AttachmentName = "InfantryUniformBackpack",
            BoneCandidates = new[] { "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1" },
            PreserveWorldPose = false,
            LocalPosition = new Vector3(0f, 0.01f, -0.13f),
            LocalEulerAngles = Vector3.zero,
            LocalScale = Vector3.one,
        };

        binder.Bindings = new[] { weaponBinding, plateBinding, faceBinding, leftHandBinding, rightHandBinding, helmetBinding, jacketBinding, backpackBinding };
    }

    static AnimationClip LoadClip(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                return clip;
        }

        return null;
    }

    static Avatar LoadAvatar(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null)
                return avatar;
        }

        return null;
    }

    static void RemoveChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            Object.DestroyImmediate(child.gameObject);
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    static GameObject BuildRifle(Transform parent)
    {
        GameObject root = new GameObject("KenneyWeapon");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Material metal = MakeMaterial("BasicShooterRifleMetal", new Color(0.08f, 0.08f, 0.075f));
        Material wood = MakeMaterial("BasicShooterRifleWood", new Color(0.30f, 0.18f, 0.10f));

        AddCube(root.transform, "WW2RifleStock", new Vector3(0f, 0f, -0.18f), new Vector3(0.09f, 0.10f, 0.28f), wood);
        AddCube(root.transform, "WW2RifleReceiver", new Vector3(0f, 0f, 0.04f), new Vector3(0.09f, 0.10f, 0.26f), metal);
        AddCube(root.transform, "WW2RifleBarrel", new Vector3(0f, 0.01f, 0.43f), new Vector3(0.035f, 0.035f, 0.62f), metal);
        AddCube(root.transform, "WW2RifleMagazine", new Vector3(0f, -0.09f, 0.08f), new Vector3(0.075f, 0.18f, 0.09f), metal);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.01f, 0.58f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;

        return root;
    }

    static GameObject BuildShoulderCannon(Transform parent)
    {
        GameObject root = new GameObject("KenneyWeapon");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Material metal = MakeMaterial("BasicShooterShoulderCannonMetal", new Color(0.11f, 0.11f, 0.10f));
        Material wood = MakeMaterial("BasicShooterShoulderCannonWood", new Color(0.30f, 0.18f, 0.10f));

        AddCylinder(root.transform, "WW2ShoulderTube", new Vector3(0f, 0.005f, 0.20f), new Vector3(0.070f, 0.46f, 0.070f), new Vector3(90f, 0f, 0f), metal);
        AddCylinder(root.transform, "WW2ShoulderMuzzle", new Vector3(0f, 0.005f, 0.68f), new Vector3(0.105f, 0.070f, 0.105f), new Vector3(90f, 0f, 0f), metal);
        AddCylinder(root.transform, "WW2ShoulderBreech", new Vector3(0f, 0.005f, -0.24f), new Vector3(0.088f, 0.080f, 0.088f), new Vector3(90f, 0f, 0f), metal);
        AddCube(root.transform, "WW2ShoulderRest", new Vector3(0f, -0.07f, -0.18f), new Vector3(0.16f, 0.055f, 0.20f), wood);
        AddCube(root.transform, "WW2ShoulderGrip", new Vector3(0f, -0.14f, 0.10f), new Vector3(0.055f, 0.18f, 0.07f), wood);
        AddCube(root.transform, "WW2ShoulderSight", new Vector3(0.045f, 0.065f, 0.26f), new Vector3(0.035f, 0.055f, 0.16f), metal);

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0.005f, 0.78f);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one;

        return root;
    }

    static GameObject BuildFactionPlate(Transform parent)
    {
        GameObject root = new GameObject("FactionPlate");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Material plate = MakeMaterial("BasicShooterFactionPlate", new Color(0.16f, 0.62f, 1f));
        AddCube(root.transform, "FactionChestPatch", new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.025f, 0.11f), plate);
        AddCube(root.transform, "FactionShoulderPatch", new Vector3(0.14f, 0f, -0.02f), new Vector3(0.08f, 0.025f, 0.08f), plate);
        return root;
    }

    static void BuildSkinOverlay(Transform parent)
    {
        Material skin = MakeMaterial("BasicShooterSkin", new Color(0.78f, 0.56f, 0.36f));

        GameObject face = new GameObject("SkinFace");
        face.transform.SetParent(parent, false);
        AddSphere(face.transform, "SkinFacePatch", new Vector3(0f, 0f, 0f), new Vector3(0.115f, 0.145f, 0.035f), skin);

        GameObject leftHand = new GameObject("SkinLeftHand");
        leftHand.transform.SetParent(parent, false);
        AddSphere(leftHand.transform, "SkinLeftPalm", Vector3.zero, new Vector3(0.055f, 0.045f, 0.075f), skin);

        GameObject rightHand = new GameObject("SkinRightHand");
        rightHand.transform.SetParent(parent, false);
        AddSphere(rightHand.transform, "SkinRightPalm", Vector3.zero, new Vector3(0.055f, 0.045f, 0.075f), skin);
    }

    static void BuildUniformOverlay(Transform parent, bool playerOwned)
    {
        Material jacket = LoadUniformMaterial(
            playerOwned ? "InfantryUniform_PlayerJacket" : "InfantryUniform_EnemyJacket",
            playerOwned ? new Color(0.31f, 0.50f, 0.27f) : new Color(0.58f, 0.44f, 0.23f),
            0.06f,
            0.36f);
        Material helmet = LoadUniformMaterial(
            playerOwned ? "InfantryUniform_PlayerHelmet" : "InfantryUniform_EnemyHelmet",
            playerOwned ? new Color(0.19f, 0.36f, 0.19f) : new Color(0.42f, 0.32f, 0.17f),
            0.18f,
            0.42f);
        Material webbing = LoadUniformMaterial("InfantryUniform_Webbing", new Color(0.52f, 0.36f, 0.17f), 0.02f, 0.28f);
        Material bedroll = LoadUniformMaterial("InfantryUniform_Bedroll", new Color(0.34f, 0.40f, 0.26f), 0.04f, 0.24f);

        GameObject helmetRoot = new GameObject("InfantryUniformHelmet");
        helmetRoot.transform.SetParent(parent, false);
        AddSphere(helmetRoot.transform, "InfantryUniformHelmetDome", new Vector3(0f, 0.035f, 0f), new Vector3(0.16f, 0.075f, 0.16f), helmet);
        AddCylinder(helmetRoot.transform, "InfantryUniformHelmetBrim", new Vector3(0f, -0.008f, 0.012f), new Vector3(0.20f, 0.018f, 0.20f), Vector3.zero, helmet);
        AddCube(helmetRoot.transform, "InfantryUniformHelmetLip", new Vector3(0f, -0.006f, 0.15f), new Vector3(0.17f, 0.018f, 0.04f), helmet);

        GameObject jacketRoot = new GameObject("InfantryUniformJacket");
        jacketRoot.transform.SetParent(parent, false);
        AddCube(jacketRoot.transform, "InfantryUniformCoat", new Vector3(0f, 0f, 0.02f), new Vector3(0.25f, 0.32f, 0.09f), jacket);
        AddCube(jacketRoot.transform, "InfantryUniformChestBand", new Vector3(0f, 0.025f, 0.075f), new Vector3(0.29f, 0.045f, 0.035f), webbing);
        AddCube(jacketRoot.transform, "InfantryUniformStrapL", new Vector3(-0.095f, 0f, 0.08f), new Vector3(0.035f, 0.34f, 0.035f), webbing);
        AddCube(jacketRoot.transform, "InfantryUniformStrapR", new Vector3(0.095f, 0f, 0.08f), new Vector3(0.035f, 0.34f, 0.035f), webbing);

        GameObject backpackRoot = new GameObject("InfantryUniformBackpack");
        backpackRoot.transform.SetParent(parent, false);
        AddCube(backpackRoot.transform, "InfantryUniformPackBody", new Vector3(0f, -0.02f, 0f), new Vector3(0.22f, 0.30f, 0.10f), webbing);
        AddCylinder(backpackRoot.transform, "InfantryUniformBedroll", new Vector3(0f, 0.16f, -0.02f), new Vector3(0.10f, 0.24f, 0.10f), new Vector3(0f, 0f, 90f), bedroll);
    }

    static void AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static void AddSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static void AddCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(localEulerAngles);
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static Material MakeMaterial(string name, Color color)
    {
        string folder = "Assets/Resources/Materials/Generated";
        EnsureDirectory(folder);
        string path = folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        RendererColorUtil.TrySetColor(mat, color);
        return mat;
    }

    static Material LoadUniformMaterial(string name, Color color, float metallic, float glossiness)
    {
        string folder = "Assets/Resources/Materials/InfantryUniforms";
        EnsureDirectory(folder);
        string path = folder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        RendererColorUtil.TrySetColor(mat, color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", glossiness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void HideChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
            child.gameObject.SetActive(false);
    }

    static void EnsureDirectory(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder))
            return;

        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        EnsureDirectory(parent);
        if (!AssetDatabase.IsValidFolder(assetFolder))
            AssetDatabase.CreateFolder(parent, name);
    }

    static string ToAssetPath(string fullPath)
    {
        return fullPath.Replace('\\', '/');
    }
}
