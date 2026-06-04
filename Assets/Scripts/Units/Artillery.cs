using UnityEngine;

public class Artillery : RTSUnit
{
    static readonly string[] ObsoleteArtilleryModelParts =
    {
        "WW2ShellBandolier",
        "KenneyAmmoBall",
        "ArtilleryWeaponSocket",
    };

    protected override float DesiredVisualHeight => 3.6f;
    protected override float DesiredVisualFootprint => 2.0f;

    protected override void Awake()
    {
        DisplayName = "\u70ae\u5175";
        MaxHP = 150; AttackDamage = 65; AttackRange = 18f;
        AttackInterval = 2f; SightRange = 20f;
        GoldCost = 200; PopCost = 2; MoveSpeed = 5f;
        SplashRadius = 5f; SplashFalloff = 0.4f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 34f; ProjectileArcHeight = 5.5f; ProjectileImpactRadius = 1.55f;
        ProjectileTint = new Color(1f, 0.48f, 0.14f, 1f);
        TracerDuration = 0.07f;
        base.Awake();
        EnsureArtilleryVisualModel();
    }

    void EnsureArtilleryVisualModel()
    {
        Transform model = transform.Find("Model");
        bool mustReplace = model == null
            || !HasArtilleryWeapon(model)
            || ContainsObsoleteArtilleryPart(model);

        model = mustReplace ? ReplaceWithArtilleryModel(model) : model;
        RemoveObsoleteArtilleryParts(model);
        if (model == null)
            return;

        BindArtilleryVisual(model);
    }

    Transform ReplaceWithArtilleryModel(Transform existingModel)
    {
        GameObject artilleryPrefab = LoadArtilleryVisualPrefab();
        Transform sourceModel = artilleryPrefab != null ? artilleryPrefab.transform.Find("Model") : null;
        if (sourceModel == null)
            return existingModel;

        if (existingModel != null)
        {
            existingModel.name = "RemovedArtilleryModel";
            existingModel.gameObject.SetActive(false);
            Destroy(existingModel.gameObject);
        }

        Transform model = Instantiate(sourceModel, transform, false);
        model.name = "Model";
        return model;
    }

    GameObject LoadArtilleryVisualPrefab()
    {
        string primaryPath = bPlayerOwned ? "Prefabs/Artillery_P" : "Prefabs/Artillery_E";
        string legacyPath = bPlayerOwned ? "Prefabs/Artillery_Player" : "Prefabs/Artillery_Enemy";
        return Resources.Load<GameObject>(primaryPath)
            ?? Resources.Load<GameObject>(legacyPath)
            ?? Resources.Load<GameObject>("Prefabs/Artillery");
    }

    void BindArtilleryVisual(Transform model)
    {
        UnitVisualAnimator visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null)
            visualAnimator.VisualRoot = model;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        AnimatedUnitAttachmentBinder attachmentBinder = model.GetComponentInChildren<AnimatedUnitAttachmentBinder>(true)
            ?? GetComponent<AnimatedUnitAttachmentBinder>();
        if (attachmentBinder != null)
        {
            attachmentBinder.VisualRoot = model;
            attachmentBinder.AnimatedRoot = animator != null ? animator.transform : null;
            attachmentBinder.BindAttachments();
        }

        BasicShooterRifleHandBinder handBinder = GetComponent<BasicShooterRifleHandBinder>();
        if (handBinder == null)
            handBinder = gameObject.AddComponent<BasicShooterRifleHandBinder>();

        handBinder.WeaponRoot = FindByName(model, "KenneyWeapon");
        handBinder.AnimatedRoot = animator != null ? animator.transform : null;
        handBinder.ForwardOffset = new Vector3(0.04f, 0.02f, 0.10f);
        handBinder.EulerOffset = new Vector3(0f, 0f, -4f);
    }

    protected override void ApplyMilitaryTint()
    {
        Color uniform = bPlayerOwned ? new Color(0.23f, 0.34f, 0.18f) : new Color(0.46f, 0.39f, 0.24f);
        Color faction = bPlayerOwned ? new Color(0.16f, 0.62f, 1f) : new Color(1f, 0.24f, 0.18f);

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer) continue;
            if (ShouldSkipInfantryTint(renderer.transform)) continue;

            if (HasAncestorNamed(renderer.transform, "FactionPlate"))
            {
                RendererColorUtil.TrySetColor(renderer, faction);
                continue;
            }

            if (HasAncestorNamed(renderer.transform, "KenneyWeapon")
                || HasAncestorNamed(renderer.transform, "SkinFace")
                || HasAncestorNamed(renderer.transform, "SkinLeftHand")
                || HasAncestorNamed(renderer.transform, "SkinRightHand")
                || renderer.name.IndexOf("Rifle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                RendererColorUtil.ClearPropertyBlock(renderer);
                continue;
            }

            RendererColorUtil.TrySetColor(renderer, uniform);
        }
    }

    static bool HasArtilleryWeapon(Transform root)
    {
        return FindByName(root, "WW2ShoulderTube") != null
            && FindByName(root, "WW2ShoulderMuzzle") != null;
    }

    static bool ContainsObsoleteArtilleryPart(Transform root)
    {
        if (root == null)
            return false;
        if (IsObsoleteArtilleryPart(root.name))
            return true;

        foreach (Transform child in root)
        {
            if (ContainsObsoleteArtilleryPart(child))
                return true;
        }

        return false;
    }

    static void RemoveObsoleteArtilleryParts(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (IsObsoleteArtilleryPart(child.name))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
                continue;
            }

            RemoveObsoleteArtilleryParts(child);
        }
    }

    static bool IsObsoleteArtilleryPart(string partName)
    {
        for (int i = 0; i < ObsoleteArtilleryModelParts.Length; i++)
        {
            if (string.Equals(partName, ObsoleteArtilleryModelParts[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static Transform FindByName(Transform root, string targetName)
    {
        if (root == null)
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
}
