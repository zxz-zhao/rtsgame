using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared infantry crew tinting rules for riflemen and artillery crews.
/// </summary>
public static class InfantryUniformVisuals
{
    const string MaterialRoot = "Materials/InfantryUniforms/";

    static readonly string[] HeadBones = { "Head", "mixamorig:Head" };
    static readonly string[] ChestBones = { "UpperChest", "Chest", "Spine", "mixamorig:Spine2", "mixamorig:Spine1" };

    static Material playerJacket;
    static Material playerHelmet;
    static Material enemyJacket;
    static Material enemyHelmet;
    static Material webbing;
    static Material boots;
    static Material bedroll;

    /// <summary>
    /// Applies readable uniform colors while preserving weapons, exposed skin, and faction patches.
    /// </summary>
    public static void Apply(GameObject root, bool playerOwned)
    {
        if (root == null)
            return;

        CleanupDuplicateWeapons(root);

        UnitVisualAnimator visualAnimator = root.GetComponent<UnitVisualAnimator>();
        if (visualAnimator == null || visualAnimator.Style == UnitVisualAnimator.VisualStyle.Infantry)
            EnsureRuntimeUniform(root, playerOwned);

        Color fallbackUniform = playerOwned
            ? new Color(0.31f, 0.50f, 0.27f)
            : new Color(0.58f, 0.44f, 0.23f);
        Color faction = playerOwned
            ? new Color(0.16f, 0.62f, 1f)
            : new Color(1f, 0.24f, 0.18f);

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (ShouldSkipInfantryTint(renderer.transform)) continue;

            if (HasAncestorNamed(renderer.transform, "FactionPlate"))
            {
                RendererColorUtil.TrySetColor(renderer, faction);
                continue;
            }

            if (IsWeaponOrSkin(renderer.transform, renderer.name))
            {
                RendererColorUtil.ClearPropertyBlock(renderer);
                continue;
            }

            Material uniformMaterial = ResolveUniformMaterial(renderer.transform, playerOwned);
            if (uniformMaterial != null)
            {
                ApplySharedMaterial(renderer, uniformMaterial);
                continue;
            }

            RendererColorUtil.TrySetColor(renderer, fallbackUniform);
        }
    }

    static void CleanupDuplicateWeapons(GameObject root)
    {
        Transform visualRoot = ResolveVisualRoot(root.transform);
        if (visualRoot == null)
            return;

        BasicShooterRifleHandBinder handBinder = root.GetComponent<BasicShooterRifleHandBinder>();
        Transform preferredWeapon = ResolvePreferredWeaponRoot(visualRoot, handBinder);
        if (preferredWeapon == null)
            return;

        List<Transform> weaponRoots = CollectWeaponRootCandidates(visualRoot);
        if (weaponRoots.Count <= 1)
        {
            if (handBinder != null)
                handBinder.WeaponRoot = preferredWeapon;
            return;
        }

        for (int i = 0; i < weaponRoots.Count; i++)
        {
            Transform candidate = weaponRoots[i];
            if (candidate == null || candidate == preferredWeapon)
                continue;

            if (candidate.IsChildOf(preferredWeapon) || preferredWeapon.IsChildOf(candidate))
                continue;

            candidate.gameObject.SetActive(false);
        }

        if (handBinder != null)
            handBinder.WeaponRoot = preferredWeapon;
    }

    static Transform ResolvePreferredWeaponRoot(Transform visualRoot, BasicShooterRifleHandBinder handBinder)
    {
        Transform preferredWeapon = handBinder != null ? handBinder.WeaponRoot : null;
        if (preferredWeapon != null && preferredWeapon.IsChildOf(visualRoot))
        {
            preferredWeapon = PromoteToWeaponRoot(preferredWeapon, visualRoot);
            if (IsWeaponRootCandidate(preferredWeapon))
                return preferredWeapon;
        }

        return FindByName(visualRoot, "KenneyWeapon");
    }

    static List<Transform> CollectWeaponRootCandidates(Transform visualRoot)
    {
        var results = new List<Transform>();
        if (visualRoot == null)
            return results;

        Transform[] nodes = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (node == null || node == visualRoot)
                continue;
            if (!IsWeaponRootCandidate(node))
                continue;
            if (HasWeaponRootAncestor(node, visualRoot))
                continue;

            results.Add(node);
        }

        return results;
    }

    static bool IsWeaponRootCandidate(Transform node)
    {
        if (node == null || !HasWeaponPayload(node))
            return false;

        string name = node.name;
        if (string.IsNullOrEmpty(name))
            return false;

        if (string.Equals(name, "KenneyWeapon", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Rifle", System.StringComparison.OrdinalIgnoreCase))
            return true;

        return name.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Merrick", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("WW2Rifle", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("WW2Shoulder", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool HasWeaponRootAncestor(Transform node, Transform visualRoot)
    {
        for (Transform cur = node.parent; cur != null && cur != visualRoot; cur = cur.parent)
        {
            if (IsWeaponRootCandidate(cur))
                return true;
        }

        return false;
    }

    static bool HasWeaponPayload(Transform node)
    {
        return node.GetComponentInChildren<Renderer>(true) != null
            || FindByName(node, "Muzzle") != null;
    }

    static Transform PromoteToWeaponRoot(Transform node, Transform visualRoot)
    {
        Transform best = node;
        for (Transform cur = node; cur != null && cur != visualRoot; cur = cur.parent)
        {
            if (IsWeaponRootCandidate(cur))
                best = cur;
        }

        return best;
    }

    static void EnsureRuntimeUniform(GameObject root, bool playerOwned)
    {
        Transform visualRoot = ResolveVisualRoot(root.transform);
        if (visualRoot == null)
            return;

        Animator animator = root.GetComponentInChildren<Animator>(true);
        Transform animatedRoot = animator != null ? animator.transform : visualRoot;
        Transform head = FindBestBone(animatedRoot, HeadBones, animator) ?? visualRoot;
        Transform chest = FindBestBone(animatedRoot, ChestBones, animator) ?? visualRoot;

        if (FindByName(visualRoot, "InfantryUniformHelmet") == null)
            BuildRuntimeHelmet(head, playerOwned);
        if (FindByName(visualRoot, "InfantryUniformJacket") == null)
            BuildRuntimeJacket(chest, playerOwned);
        if (FindByName(visualRoot, "InfantryUniformBackpack") == null)
            BuildRuntimeBackpack(chest);
    }

    static Transform ResolveVisualRoot(Transform unitRoot)
    {
        if (unitRoot == null)
            return null;

        Transform model = unitRoot.Find("Model");
        return model != null ? model : unitRoot;
    }

    static void BuildRuntimeHelmet(Transform parent, bool playerOwned)
    {
        GameObject root = CreateAttachmentRoot(
            "InfantryUniformHelmet",
            parent,
            new Vector3(0f, 0.045f, 0.005f));
        Material helmet = playerOwned ? PlayerHelmet : EnemyHelmet;
        AddSphere(root.transform, "InfantryUniformHelmetDome", new Vector3(0f, 0.035f, 0f), new Vector3(0.16f, 0.075f, 0.16f), helmet);
        AddCylinder(root.transform, "InfantryUniformHelmetBrim", new Vector3(0f, -0.008f, 0.012f), new Vector3(0.20f, 0.018f, 0.20f), Vector3.zero, helmet);
        AddCube(root.transform, "InfantryUniformHelmetLip", new Vector3(0f, -0.006f, 0.15f), new Vector3(0.17f, 0.018f, 0.04f), helmet);
    }

    static void BuildRuntimeJacket(Transform parent, bool playerOwned)
    {
        GameObject root = CreateAttachmentRoot(
            "InfantryUniformJacket",
            parent,
            new Vector3(0f, 0.02f, 0.055f));
        Material jacket = playerOwned ? PlayerJacket : EnemyJacket;
        AddCube(root.transform, "InfantryUniformCoat", new Vector3(0f, 0f, 0.02f), new Vector3(0.25f, 0.32f, 0.09f), jacket);
        AddCube(root.transform, "InfantryUniformChestBand", new Vector3(0f, 0.025f, 0.075f), new Vector3(0.29f, 0.045f, 0.035f), Webbing);
        AddCube(root.transform, "InfantryUniformStrapL", new Vector3(-0.095f, 0f, 0.08f), new Vector3(0.035f, 0.34f, 0.035f), Webbing);
        AddCube(root.transform, "InfantryUniformStrapR", new Vector3(0.095f, 0f, 0.08f), new Vector3(0.035f, 0.34f, 0.035f), Webbing);
    }

    static void BuildRuntimeBackpack(Transform parent)
    {
        GameObject root = CreateAttachmentRoot(
            "InfantryUniformBackpack",
            parent,
            new Vector3(0f, 0.01f, -0.13f));
        AddCube(root.transform, "InfantryUniformPackBody", new Vector3(0f, -0.02f, 0f), new Vector3(0.22f, 0.30f, 0.10f), Webbing);
        AddCylinder(root.transform, "InfantryUniformBedroll", new Vector3(0f, 0.16f, -0.02f), new Vector3(0.10f, 0.24f, 0.10f), new Vector3(0f, 0f, 90f), Bedroll);
    }

    static GameObject CreateAttachmentRoot(string name, Transform parent, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    static void AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ConfigurePrimitive(go, parent, name, localPosition, Quaternion.identity, localScale, material);
    }

    static void AddSphere(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ConfigurePrimitive(go, parent, name, localPosition, Quaternion.identity, localScale, material);
    }

    static void AddCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ConfigurePrimitive(go, parent, name, localPosition, Quaternion.Euler(localEulerAngles), localScale, material);
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
            RemoveRuntimeObject(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
    }

    static void RemoveRuntimeObject(Object obj)
    {
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }

    static Material ResolveUniformMaterial(Transform t, bool playerOwned)
    {
        string name = t != null ? t.name : string.Empty;

        if (HasAncestorNamed(t, "InfantryUniformHelmet")
            || HasAncestorNamed(t, "WW2Helmet")
            || name.IndexOf("Helmet", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return playerOwned ? PlayerHelmet : EnemyHelmet;

        if (HasAncestorNamed(t, "InfantryUniformBackpack")
            || name.IndexOf("Bedroll", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return Bedroll;

        if (name.IndexOf("Boot", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return Boots;

        if (name.IndexOf("Webbing", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Strap", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Pack", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return Webbing;

        if (HasAncestorNamed(t, "InfantryUniformJacket")
            || name.IndexOf("Jacket", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Coat", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Body", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Arm", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Leg", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return playerOwned ? PlayerJacket : EnemyJacket;

        return null;
    }

    static bool IsWeaponOrSkin(Transform t, string rendererName)
    {
        return HasAncestorNamed(t, "KenneyWeapon")
            || HasAncestorNamed(t, "SkinFace")
            || HasAncestorNamed(t, "SkinLeftHand")
            || HasAncestorNamed(t, "SkinRightHand")
            || (!string.IsNullOrEmpty(rendererName)
                && rendererName.IndexOf("Rifle", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static bool ShouldSkipInfantryTint(Transform t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n == "FactionRing" || n == "FactionDot" || n == "SelectionRing" || n == "SelectionCircle"
                || n == "HPLabel" || n == "UnitLabel" || n == "AttackLine"
                || n.EndsWith("Aura") || n.EndsWith("Disc") || n.EndsWith("Ring"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static Transform FindBestBone(Transform root, string[] candidates, Animator animator)
    {
        Transform humanoidBone = FindHumanoidBone(candidates, animator);
        if (humanoidBone != null)
            return humanoidBone;

        if (root == null || candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform exact = FindByName(root, candidates[i]);
            if (exact != null)
                return exact;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform compatible = FindCompatibleBoneName(root, candidates[i]);
            if (compatible != null)
                return compatible;
        }

        return null;
    }

    static Transform FindHumanoidBone(string[] candidates, Animator animator)
    {
        if (animator == null || !animator.isHuman || candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
                continue;

            if (NameMatches(candidate, "Head"))
                return animator.GetBoneTransform(HumanBodyBones.Head);
            if (NameMatches(candidate, "UpperChest"))
                return animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (NameMatches(candidate, "Chest"))
                return animator.GetBoneTransform(HumanBodyBones.Chest);
            if (NameMatches(candidate, "Spine"))
                return animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        return null;
    }

    static Transform FindCompatibleBoneName(Transform root, string candidate)
    {
        if (root == null || string.IsNullOrEmpty(candidate))
            return null;

        if (NameMatches(root.name, candidate))
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindCompatibleBoneName(child, candidate);
            if (found != null)
                return found;
        }

        return null;
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

    static bool NameMatches(string actualName, string candidate)
    {
        if (string.IsNullOrEmpty(actualName) || string.IsNullOrEmpty(candidate))
            return false;

        if (string.Equals(actualName, candidate, System.StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedActual = NormalizeBoneName(actualName);
        string normalizedCandidate = NormalizeBoneName(candidate);
        return string.Equals(normalizedActual, normalizedCandidate, System.StringComparison.OrdinalIgnoreCase)
            || normalizedActual.EndsWith(normalizedCandidate, System.StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeBoneName(string name)
    {
        return name.Replace("mixamorig:", string.Empty)
            .Replace("mixamorig", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);
    }

    static bool HasAncestorNamed(Transform t, string name)
    {
        while (t != null)
        {
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    static void ApplySharedMaterial(Renderer renderer, Material material)
    {
        if (renderer == null || material == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterial = material;
        }
        else
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
        }

        RendererColorUtil.ClearPropertyBlock(renderer);
    }

    static Material PlayerJacket => playerJacket != null
        ? playerJacket
        : (playerJacket = Resources.Load<Material>(MaterialRoot + "InfantryUniform_PlayerJacket"));

    static Material PlayerHelmet => playerHelmet != null
        ? playerHelmet
        : (playerHelmet = Resources.Load<Material>(MaterialRoot + "InfantryUniform_PlayerHelmet"));

    static Material EnemyJacket => enemyJacket != null
        ? enemyJacket
        : (enemyJacket = Resources.Load<Material>(MaterialRoot + "InfantryUniform_EnemyJacket"));

    static Material EnemyHelmet => enemyHelmet != null
        ? enemyHelmet
        : (enemyHelmet = Resources.Load<Material>(MaterialRoot + "InfantryUniform_EnemyHelmet"));

    static Material Webbing => webbing != null
        ? webbing
        : (webbing = Resources.Load<Material>(MaterialRoot + "InfantryUniform_Webbing"));

    static Material Boots => boots != null
        ? boots
        : (boots = Resources.Load<Material>(MaterialRoot + "InfantryUniform_Boots"));

    static Material Bedroll => bedroll != null
        ? bedroll
        : (bedroll = Resources.Load<Material>(MaterialRoot + "InfantryUniform_Bedroll"));
}
