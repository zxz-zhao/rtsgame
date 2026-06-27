using UnityEngine;

/// <summary>
/// Vehicle artillery unit with camouflage materials and optional animation for an existing model shell.
/// </summary>
public class Artillery : RTSUnit
{
    const float ReadyShellRaiseDuration = 0.12f;
    const float ReadyShellLaunchHideDelay = 0.04f;
    const float ReadyShellReloadLeadTime = 0.42f;
    const float ReadyShellMinimumHiddenDuration = 0.18f;

    static readonly string[] ObsoleteArtilleryModelParts =
    {
        "WW2ShellBandolier",
        "KenneyAmmoBall",
        "ArtilleryWeaponSocket",
    };

    static readonly string[] ReadyShellNameTokens =
    {
        "readyshell",
        "ready_shell",
        "shell",
        "ammo",
        "missile",
        "rocket",
        "projectile",
        "round",
        "warhead",
        "cannonball",
        "cannon_ball",
    };

    static readonly string[] NonReadyShellNameTokens =
    {
        "muzzle",
        "barrel",
        "cannon",
        "gun",
        "tube",
        "turret",
        "hull",
        "body",
        "chassis",
        "track",
        "wheel",
        "tire",
        "tyre",
        "vehicle",
        "faction",
        "selection",
    };

    static readonly string[] ForbiddenReadyShellAncestorTokens =
    {
        "faction",
        "selection",
        "track",
        "wheel",
        "tire",
        "tyre",
    };

    const string PlayerHullMaterialPath = "Materials/VehicleCamo/Artillery_PlayerHull";
    const string PlayerTurretMaterialPath = "Materials/VehicleCamo/Artillery_PlayerTurret";
    const string PlayerGunMaterialPath = "Materials/VehicleCamo/Artillery_PlayerGun";
    const string EnemyHullMaterialPath = "Materials/VehicleCamo/Artillery_EnemyHull";
    const string EnemyTurretMaterialPath = "Materials/VehicleCamo/Artillery_EnemyTurret";
    const string EnemyGunMaterialPath = "Materials/VehicleCamo/Artillery_EnemyGun";

    static Material s_playerHullMaterial;
    static Material s_playerTurretMaterial;
    static Material s_playerGunMaterial;
    static Material s_enemyHullMaterial;
    static Material s_enemyTurretMaterial;
    static Material s_enemyGunMaterial;

    public bool UseInfantryVisualProfile;

    protected override float DesiredVisualHeight => UseInfantryVisualProfile ? 3.6f : 3.2f;
    protected override float DesiredVisualFootprint => UseInfantryVisualProfile ? 2.0f : 5.2f;
    protected override float HealthBarHeight => UseInfantryVisualProfile ? 2.5f : 3.7f;
    protected override float UnitLabelHeight => UseInfantryVisualProfile ? 2.0f : 3.25f;
    protected override float SelectionRingRadius => UseInfantryVisualProfile ? 1.5f : 2.3f;
    protected override bool CanTraverseForestZones => UseInfantryVisualProfile;

    Transform _readyShellRoot;
    Vector3 _readyShellRestPosition;
    Vector3 _readyShellRaisedPosition;
    Quaternion _readyShellRestRotation;
    Quaternion _readyShellRaisedRotation;
    Coroutine _readyShellRoutine;

    protected override void Awake()
    {
        DisplayName = "\u706b\u70ae";
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
        ApplyArtilleryCamouflage();
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
        EnsureReadyShellVisual(model);
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

    protected override void UpdateAI()
    {
        float previousAttackTimer = AttackTimer;
        base.UpdateAI();

        if (AttackTimer > Mathf.Max(0.01f, previousAttackTimer + 0.01f))
            TriggerReadyShellPose();
    }

    void EnsureReadyShellVisual(Transform model)
    {
        if (model == null)
            return;

        _readyShellRoot = ResolveExistingReadyShell(model);
        if (_readyShellRoot == null)
            return;

        _readyShellRestPosition = _readyShellRoot.localPosition;
        _readyShellRaisedPosition = _readyShellRestPosition + ResolveReadyShellRaiseOffset(model, _readyShellRoot);
        _readyShellRestRotation = _readyShellRoot.localRotation;
        _readyShellRaisedRotation = ResolveReadyShellRaisedRotation(_readyShellRoot);
        ShowReadyShellRaised();
    }

    static Transform ResolveExistingReadyShell(Transform model)
    {
        Transform explicitShell = FindByName(model, "ArtilleryReadyShell");
        if (IsReadyShellCandidate(explicitShell, model))
            return explicitShell;

        Transform best = null;
        int bestScore = int.MinValue;
        Transform[] nodes = model.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            if (!IsReadyShellCandidate(node, model))
                continue;

            int score = ScoreReadyShellCandidate(node, model);
            if (score > bestScore)
            {
                best = node;
                bestScore = score;
            }
        }

        return best != null ? best : ResolveExistingReadyShellByShape(model);
    }

    static bool IsReadyShellCandidate(Transform node, Transform model)
    {
        if (node == null || node == model)
            return false;
        if (!HasAnyNameToken(node.name, ReadyShellNameTokens))
            return false;
        if (HasAnyNameToken(node.name, NonReadyShellNameTokens))
            return false;
        if (HasForbiddenReadyShellAncestor(node, model))
            return false;

        return node.GetComponentInChildren<Renderer>(true) != null;
    }

    static bool HasForbiddenReadyShellAncestor(Transform node, Transform model)
    {
        for (Transform current = node.parent; current != null && current != model; current = current.parent)
        {
            if (HasAnyNameToken(current.name, ForbiddenReadyShellAncestorTokens))
                return true;
        }

        return false;
    }

    static Transform ResolveExistingReadyShellByShape(Transform model)
    {
        if (model == null || !TryGetLocalRendererBounds(model, out Bounds modelBounds))
            return null;

        float modelFootprint = Mathf.Max(modelBounds.size.x, modelBounds.size.z);
        float modelVolume = Mathf.Max(modelBounds.size.x * modelBounds.size.y * modelBounds.size.z, 0.001f);
        if (modelFootprint <= 0.01f)
            return null;

        Transform best = null;
        float bestScore = float.NegativeInfinity;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsExistingShellGeometryCandidate(renderer, model, modelBounds, modelFootprint, modelVolume, out float score))
                continue;

            if (score > bestScore)
            {
                best = renderer.transform;
                bestScore = score;
            }
        }

        return best;
    }

    static bool IsExistingShellGeometryCandidate(
        Renderer renderer,
        Transform model,
        Bounds modelBounds,
        float modelFootprint,
        float modelVolume,
        out float score)
    {
        score = 0f;
        if (renderer == null || renderer.transform == null || renderer.transform == model)
            return false;
        if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer)
            return false;
        if (HasAnyNameToken(renderer.transform.name, NonReadyShellNameTokens))
            return false;
        if (HasForbiddenReadyShellAncestor(renderer.transform, model))
            return false;
        if (!TryGetRendererLocalBounds(renderer, model, out Bounds localBounds))
            return false;

        Vector3 size = localBounds.size;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float shortest = Mathf.Max(0.001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
        float middle = Mathf.Max(0.001f, size.x + size.y + size.z - longest - shortest);
        float lengthRatio = longest / shortest;
        float volumeRatio = Mathf.Max(0f, size.x * size.y * size.z) / modelVolume;
        float y01 = Mathf.InverseLerp(modelBounds.min.y, modelBounds.max.y, localBounds.center.y);

        if (y01 < 0.38f)
            return false;
        if (longest < modelFootprint * 0.055f || longest > modelFootprint * 0.42f)
            return false;
        if (shortest > modelFootprint * 0.18f)
            return false;
        if (middle > modelFootprint * 0.24f)
            return false;
        if (lengthRatio < 1.35f)
            return false;
        if (volumeRatio > 0.065f)
            return false;

        float centerPenalty = Mathf.Abs(localBounds.center.x - modelBounds.center.x) / Mathf.Max(modelBounds.size.x, 0.01f);
        float frontRearBonus = Mathf.Abs(localBounds.center.z - modelBounds.center.z) / Mathf.Max(modelBounds.size.z, 0.01f);
        score = y01 * 45f
            + Mathf.Clamp(lengthRatio, 0f, 8f) * 4f
            + Mathf.Clamp01(frontRearBonus) * 5f
            - centerPenalty * 8f
            - volumeRatio * 120f;

        return true;
    }

    static int ScoreReadyShellCandidate(Transform node, Transform model)
    {
        int score = 0;
        string name = node.name ?? string.Empty;
        if (name.IndexOf("ready", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score += 50;
        if (name.IndexOf("shell", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score += 35;
        if (name.IndexOf("missile", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("rocket", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score += 30;
        if (name.IndexOf("ammo", System.StringComparison.OrdinalIgnoreCase) >= 0)
            score += 25;

        score -= GetTransformDepth(node, model);
        return score;
    }

    static Vector3 ResolveReadyShellRaiseOffset(Transform model, Transform shell)
    {
        float raise = 0.28f;
        if (TryGetLocalRendererBounds(model, out Bounds bounds))
            raise = Mathf.Clamp(bounds.size.y * 0.14f, 0.16f, 0.42f);

        Vector3 worldOffset = Vector3.up * raise;
        if (shell.parent != null)
            return shell.parent.InverseTransformVector(worldOffset);
        return worldOffset;
    }

    static Quaternion ResolveReadyShellRaisedRotation(Transform shell)
    {
        if (shell == null)
            return Quaternion.identity;

        if (shell.parent == null)
            return Quaternion.FromToRotation(GuessReadyShellWorldAxis(shell), Vector3.up) * shell.rotation;

        Vector3 shellAxis = GuessReadyShellWorldAxis(shell);
        if (shellAxis.sqrMagnitude < 0.0001f)
            return shell.localRotation;

        Vector3 parentUp = shell.parent.up;
        if (Vector3.Dot(shellAxis, parentUp) < Vector3.Dot(-shellAxis, parentUp))
            shellAxis = -shellAxis;

        Quaternion worldRotation = Quaternion.FromToRotation(shellAxis.normalized, parentUp.normalized) * shell.rotation;
        return Quaternion.Inverse(shell.parent.rotation) * worldRotation;
    }

    static Vector3 GuessReadyShellWorldAxis(Transform shell)
    {
        if (shell == null)
            return Vector3.up;

        Vector3 localAxis = Vector3.up;
        if (TryGetLocalRendererBounds(shell, out Bounds bounds))
        {
            Vector3 size = bounds.size;
            if (size.x >= size.y && size.x >= size.z)
                localAxis = Vector3.right;
            else if (size.z >= size.x && size.z >= size.y)
                localAxis = Vector3.forward;
        }

        return shell.TransformDirection(localAxis).normalized;
    }

    void TriggerReadyShellPose()
    {
        if (_readyShellRoot == null)
            return;

        if (_readyShellRoutine != null)
            StopCoroutine(_readyShellRoutine);
        _readyShellRoutine = StartCoroutine(AnimateReadyShellReloadCycle());
    }

    void ShowReadyShellRaised()
    {
        if (_readyShellRoot == null)
            return;

        _readyShellRoot.localPosition = _readyShellRaisedPosition;
        _readyShellRoot.localRotation = _readyShellRaisedRotation;
        _readyShellRoot.gameObject.SetActive(true);
    }

    System.Collections.IEnumerator AnimateReadyShellReloadCycle()
    {
        if (_readyShellRoot == null)
        {
            _readyShellRoutine = null;
            yield break;
        }

        ShowReadyShellRaised();
        yield return new WaitForSeconds(ReadyShellLaunchHideDelay);

        if (_readyShellRoot == null)
        {
            _readyShellRoutine = null;
            yield break;
        }

        _readyShellRoot.localPosition = _readyShellRestPosition;
        _readyShellRoot.localRotation = _readyShellRestRotation;
        _readyShellRoot.gameObject.SetActive(false);

        float hiddenDuration = Mathf.Max(ReadyShellMinimumHiddenDuration, AttackTimer - ReadyShellReloadLeadTime);
        yield return new WaitForSeconds(hiddenDuration);

        if (_readyShellRoot != null)
        {
            _readyShellRoot.localPosition = _readyShellRestPosition;
            _readyShellRoot.localRotation = _readyShellRestRotation;
            _readyShellRoot.gameObject.SetActive(true);
            yield return AnimateReadyShellTransform(
                _readyShellRestPosition,
                _readyShellRestRotation,
                _readyShellRaisedPosition,
                _readyShellRaisedRotation,
                ReadyShellRaiseDuration);
        }

        _readyShellRoutine = null;
    }

    System.Collections.IEnumerator AnimateReadyShellTransform(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, float duration)
    {
        if (_readyShellRoot == null)
        {
            _readyShellRoutine = null;
            yield break;
        }

        if (duration <= 0.001f)
        {
            _readyShellRoot.localPosition = toPosition;
            _readyShellRoot.localRotation = toRotation;
            yield break;
        }

        float t = 0f;
        while (t < duration && _readyShellRoot != null)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / duration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            _readyShellRoot.localPosition = Vector3.Lerp(fromPosition, toPosition, eased);
            _readyShellRoot.localRotation = Quaternion.Slerp(fromRotation, toRotation, eased);
            yield return null;
        }

        if (_readyShellRoot != null)
        {
            _readyShellRoot.localPosition = toPosition;
            _readyShellRoot.localRotation = toRotation;
        }
    }

    protected override void ApplyMilitaryTint()
    {
        UnitVisualAnimator visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null && visualAnimator.Style == UnitVisualAnimator.VisualStyle.Vehicle)
        {
            ApplyArtilleryCamouflage();
            return;
        }

        InfantryUniformVisuals.Apply(gameObject, bPlayerOwned);
    }

    void ApplyArtilleryCamouflage()
    {
        UnitVisualAnimator visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator == null || visualAnimator.Style != UnitVisualAnimator.VisualStyle.Vehicle)
            return;

        Transform model = transform.Find("Model");
        if (model == null)
            return;

        Material hullMaterial = LoadCamouflageMaterial(bPlayerOwned, VehicleMaterialRole.Hull);
        Material turretMaterial = LoadCamouflageMaterial(bPlayerOwned, VehicleMaterialRole.Turret);
        Material gunMaterial = LoadCamouflageMaterial(bPlayerOwned, VehicleMaterialRole.Gun);
        if (hullMaterial == null || turretMaterial == null || gunMaterial == null)
            return;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            if (_readyShellRoot != null && renderer.transform.IsChildOf(_readyShellRoot))
                continue;

            string rendererName = renderer.gameObject.name;
            if (rendererName == "FactionRing" || rendererName == "FactionDot" || rendererName == "SelectionCircle")
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material current = materials[i];
                if (current == null)
                    continue;

                string key = (rendererName + " " + current.name).ToLowerInvariant();
                if (IsArtilleryWheelLike(key) || IsArtilleryGlassLike(key))
                    continue;

                Material replacement = hullMaterial;
                if (IsArtilleryGunLike(key))
                    replacement = gunMaterial;
                else if (IsArtilleryTurretLike(key))
                    replacement = turretMaterial;

                if (materials[i] == replacement)
                    continue;

                materials[i] = replacement;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;

            RendererColorUtil.ClearPropertyBlock(renderer);
        }
    }

    static bool HasArtilleryWeapon(Transform root)
    {
        if (FindByName(root, "WW2ShoulderTube") != null
            && FindByName(root, "WW2ShoulderMuzzle") != null)
            return true;

        return FindByName(root, "Muzzle") != null
            || FindByName(root, "Nozzle") != null
            || FindByName(root, "Barrel") != null
            || FindByName(root, "Gun") != null
            || FindByName(root, "Cannon") != null;
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

    static bool TryGetLocalRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.transform == null)
                continue;
            if (_IsReadyShellRenderer(renderer.transform, root))
                continue;

            if (!TryGetRendererLocalBounds(renderer, root, out Bounds rendererBounds))
                continue;

            if (!hasBounds)
            {
                bounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    static bool TryGetRendererLocalBounds(Renderer renderer, Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (renderer == null || root == null)
            return false;

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
            if (c == 0)
                bounds = new Bounds(local, Vector3.zero);
            else
                bounds.Encapsulate(local);
        }

        return true;
    }

    static bool _IsReadyShellRenderer(Transform rendererTransform, Transform model)
    {
        for (Transform current = rendererTransform; current != null && current != model; current = current.parent)
        {
            if (HasAnyNameToken(current.name, ReadyShellNameTokens))
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

    static bool HasAnyNameToken(string name, string[] tokens)
    {
        if (string.IsNullOrEmpty(name) || tokens == null)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrEmpty(token) && name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static int GetTransformDepth(Transform node, Transform root)
    {
        int depth = 0;
        for (Transform current = node; current != null && current != root; current = current.parent)
            depth++;
        return depth;
    }

    enum VehicleMaterialRole
    {
        Hull,
        Turret,
        Gun,
    }

    static Material LoadCamouflageMaterial(bool playerOwned, VehicleMaterialRole role)
    {
        switch (role)
        {
            case VehicleMaterialRole.Hull:
                if (playerOwned)
                    return s_playerHullMaterial != null ? s_playerHullMaterial : (s_playerHullMaterial = Resources.Load<Material>(PlayerHullMaterialPath));
                return s_enemyHullMaterial != null ? s_enemyHullMaterial : (s_enemyHullMaterial = Resources.Load<Material>(EnemyHullMaterialPath));

            case VehicleMaterialRole.Turret:
                if (playerOwned)
                    return s_playerTurretMaterial != null ? s_playerTurretMaterial : (s_playerTurretMaterial = Resources.Load<Material>(PlayerTurretMaterialPath));
                return s_enemyTurretMaterial != null ? s_enemyTurretMaterial : (s_enemyTurretMaterial = Resources.Load<Material>(EnemyTurretMaterialPath));

            default:
                if (playerOwned)
                    return s_playerGunMaterial != null ? s_playerGunMaterial : (s_playerGunMaterial = Resources.Load<Material>(PlayerGunMaterialPath));
                return s_enemyGunMaterial != null ? s_enemyGunMaterial : (s_enemyGunMaterial = Resources.Load<Material>(EnemyGunMaterialPath));
        }
    }

    static bool IsArtilleryWheelLike(string key)
    {
        return key.Contains("wheel")
            || key.Contains("tire")
            || key.Contains("tyre")
            || key.Contains("rubber");
    }

    static bool IsArtilleryGlassLike(string key)
    {
        return key.Contains("glass")
            || key.Contains("window")
            || key.Contains("lens")
            || key.Contains("light")
            || key.Contains("lamp");
    }

    static bool IsArtilleryTurretLike(string key)
    {
        return key.Contains("turret")
            || key.Contains("mount")
            || key.Contains("shield")
            || key.Contains("hatch");
    }

    static bool IsArtilleryGunLike(string key)
    {
        return key.Contains("gun")
            || key.Contains("cannon")
            || key.Contains("barrel")
            || key.Contains("weapon")
            || key.Contains("tube")
            || key.Contains("muzzle");
    }
}
