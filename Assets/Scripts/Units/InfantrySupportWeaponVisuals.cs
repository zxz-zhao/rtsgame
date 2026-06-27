using UnityEngine;

public static class InfantrySupportWeaponVisuals
{
    public enum WeaponKind
    {
        ShoulderCannon,
        Flamethrower,
    }

    static Material s_metal;
    static Material s_darkMetal;
    static Material s_wood;
    static Material s_fuel;
    static Material s_flame;

    public static void Configure(GameObject unitRoot, WeaponKind kind)
    {
        if (unitRoot == null)
            return;

        Transform model = unitRoot.transform.Find("Model") ?? unitRoot.transform;
        UnitVisualAnimator visualAnimator = unitRoot.GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null)
        {
            visualAnimator.Style = UnitVisualAnimator.VisualStyle.Infantry;
            visualAnimator.VisualRoot = model;
        }

        Transform weaponRoot = FindByName(model, "KenneyWeapon");
        if (weaponRoot == null)
        {
            GameObject weaponGo = new GameObject("KenneyWeapon");
            weaponGo.transform.SetParent(model, false);
            weaponRoot = weaponGo.transform;
        }

        ClearChildren(weaponRoot);
        if (kind == WeaponKind.ShoulderCannon)
            BuildShoulderCannon(weaponRoot);
        else
            BuildFlamethrower(weaponRoot);

        ConfigureAttachmentBinder(unitRoot, model, weaponRoot, kind);
        ConfigureHandBinder(unitRoot, weaponRoot, kind);
    }

    static void ConfigureAttachmentBinder(GameObject unitRoot, Transform model, Transform weaponRoot, WeaponKind kind)
    {
        AnimatedUnitAttachmentBinder binder = model.GetComponentInChildren<AnimatedUnitAttachmentBinder>(true)
            ?? unitRoot.GetComponent<AnimatedUnitAttachmentBinder>();
        if (binder == null)
            return;

        binder.VisualRoot = model;
        Animator animator = model.GetComponentInChildren<Animator>(true);
        binder.AnimatedRoot = animator != null ? animator.transform : null;

        if (binder.Bindings != null)
        {
            for (int i = 0; i < binder.Bindings.Length; i++)
            {
                AnimatedUnitAttachmentBinder.AttachmentBinding binding = binder.Bindings[i];
                if (binding == null || binding.AttachmentName != "KenneyWeapon")
                    continue;

                binding.PreserveWorldPose = false;
                if (kind == WeaponKind.ShoulderCannon)
                {
                    binding.LocalPosition = new Vector3(0.16f, 0.03f, 0.26f);
                    binding.LocalEulerAngles = new Vector3(2f, 0f, -7f);
                    binding.LocalScale = Vector3.one * 1.08f;
                }
                else
                {
                    binding.LocalPosition = new Vector3(0.18f, -0.02f, 0.28f);
                    binding.LocalEulerAngles = new Vector3(4f, 0f, -5f);
                    binding.LocalScale = Vector3.one * 1.05f;
                }
            }
        }

        binder.BindAttachments();
    }

    static void ConfigureHandBinder(GameObject unitRoot, Transform weaponRoot, WeaponKind kind)
    {
        BasicShooterRifleHandBinder handBinder = unitRoot.GetComponent<BasicShooterRifleHandBinder>();
        if (handBinder == null)
            handBinder = unitRoot.AddComponent<BasicShooterRifleHandBinder>();

        handBinder.WeaponRoot = weaponRoot;
        Animator animator = unitRoot.GetComponentInChildren<Animator>(true);
        handBinder.AnimatedRoot = animator != null ? animator.transform : null;
        handBinder.ForwardOffset = kind == WeaponKind.ShoulderCannon
            ? new Vector3(0.04f, 0.02f, 0.10f)
            : new Vector3(0.03f, -0.01f, 0.11f);
        handBinder.EulerOffset = kind == WeaponKind.ShoulderCannon
            ? new Vector3(0f, 0f, -4f)
            : new Vector3(0f, 0f, -2f);
    }

    static void BuildShoulderCannon(Transform root)
    {
        AddCylinder(root, "WW2ShoulderTube", new Vector3(0f, 0f, 0.22f), new Vector3(0.070f, 0.50f, 0.070f), new Vector3(90f, 0f, 0f), Metal);
        AddCylinder(root, "WW2ShoulderMuzzle", new Vector3(0f, 0f, 0.72f), new Vector3(0.105f, 0.075f, 0.105f), new Vector3(90f, 0f, 0f), Metal);
        AddCylinder(root, "WW2ShoulderBreech", new Vector3(0f, 0f, -0.30f), new Vector3(0.090f, 0.085f, 0.090f), new Vector3(90f, 0f, 0f), DarkMetal);
        AddCube(root, "WW2ShoulderRest", new Vector3(0f, -0.07f, -0.20f), new Vector3(0.16f, 0.055f, 0.20f), Wood);
        AddCube(root, "WW2ShoulderGrip", new Vector3(0f, -0.14f, 0.10f), new Vector3(0.055f, 0.18f, 0.07f), Wood);
        AddCube(root, "WW2ShoulderSight", new Vector3(0.045f, 0.065f, 0.26f), new Vector3(0.035f, 0.055f, 0.16f), DarkMetal);
        AddHardpoint(root, "Muzzle", new Vector3(0f, 0f, 0.82f));
    }

    static void BuildFlamethrower(Transform root)
    {
        AddCube(root, "WW2NozzleMount", new Vector3(0f, 0f, -0.12f), new Vector3(0.20f, 0.13f, 0.18f), Metal);
        AddCylinder(root, "WW2FlamePipe", new Vector3(0f, 0f, 0.18f), new Vector3(0.055f, 0.36f, 0.055f), new Vector3(90f, 0f, 0f), Metal);
        AddCylinder(root, "WW2FlameBell", new Vector3(0f, 0f, 0.50f), new Vector3(0.09f, 0.10f, 0.09f), new Vector3(90f, 0f, 0f), Metal);
        AddSphere(root, "WW2PilotLight", new Vector3(0f, 0.065f, 0.60f), Vector3.one * 0.045f, Flame);
        AddCube(root, "WW2FuelGrip", new Vector3(0f, -0.13f, 0.10f), new Vector3(0.055f, 0.18f, 0.07f), Wood);
        AddHardpoint(root, "Muzzle", new Vector3(0f, 0f, 0.66f));

        Transform backpackParent = ResolveBackpackParent(root);
        if (backpackParent != null && FindByName(backpackParent, "InfantryFlameFuelPack") == null)
            BuildFuelPack(backpackParent);
    }

    static Transform ResolveBackpackParent(Transform weaponRoot)
    {
        Transform model = weaponRoot;
        while (model != null && model.name != "Model")
            model = model.parent;
        if (model == null)
            return null;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest)
                ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            if (chest != null)
                return chest;
        }

        return model;
    }

    static void BuildFuelPack(Transform parent)
    {
        GameObject pack = new GameObject("InfantryFlameFuelPack");
        pack.transform.SetParent(parent, false);
        pack.transform.localPosition = new Vector3(0f, 0.02f, -0.18f);
        pack.transform.localRotation = Quaternion.identity;
        pack.transform.localScale = Vector3.one;

        AddCylinder(pack.transform, "WW2FuelTankA", new Vector3(-0.07f, 0f, 0f), new Vector3(0.055f, 0.22f, 0.055f), new Vector3(0f, 0f, 0f), Fuel);
        AddCylinder(pack.transform, "WW2FuelTankB", new Vector3(0.07f, 0f, 0f), new Vector3(0.055f, 0.22f, 0.055f), new Vector3(0f, 0f, 0f), Fuel);
        AddCube(pack.transform, "WW2FuelHarness", new Vector3(0f, 0f, 0.045f), new Vector3(0.20f, 0.22f, 0.035f), DarkMetal);
    }

    static void AddHardpoint(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
    }

    static void AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        ConfigurePrimitive(GameObject.CreatePrimitive(PrimitiveType.Cube), parent, name, localPosition, Quaternion.identity, localScale, material);
    }

    static void AddSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        ConfigurePrimitive(GameObject.CreatePrimitive(PrimitiveType.Sphere), parent, name, localPosition, Quaternion.identity, localScale, material);
    }

    static void AddCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
    {
        ConfigurePrimitive(GameObject.CreatePrimitive(PrimitiveType.Cylinder), parent, name, localPosition, Quaternion.Euler(localEuler), localScale, material);
    }

    static void ConfigurePrimitive(GameObject go, Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            RemoveObject(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            RemoveObject(root.GetChild(i).gameObject);
    }

    static void RemoveObject(Object obj)
    {
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }

    static Transform FindByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;
        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindByName(child, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    static Material Metal => s_metal != null ? s_metal : (s_metal = MakeMaterial("InfantrySupportMetal", new Color(0.42f, 0.43f, 0.40f), 0.58f, 0.68f));
    static Material DarkMetal => s_darkMetal != null ? s_darkMetal : (s_darkMetal = MakeMaterial("InfantrySupportDarkMetal", new Color(0.16f, 0.17f, 0.16f), 0.46f, 0.56f));
    static Material Wood => s_wood != null ? s_wood : (s_wood = MakeMaterial("InfantrySupportWood", new Color(0.31f, 0.18f, 0.09f), 0.04f, 0.22f));
    static Material Fuel => s_fuel != null ? s_fuel : (s_fuel = MakeMaterial("InfantrySupportFuelTank", new Color(0.23f, 0.30f, 0.20f), 0.38f, 0.42f));
    static Material Flame => s_flame != null ? s_flame : (s_flame = MakeMaterial("InfantrySupportPilotLight", new Color(1f, 0.38f, 0.06f), 0.02f, 0.85f));

    static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.name = name;
        RendererColorUtil.TrySetColor(material, color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);
        return material;
    }
}
