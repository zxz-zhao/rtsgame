using UnityEngine;
/// <summary>
/// Baseline foot soldier with custom tint rules so uniform and faction details read cleanly at runtime.
/// </summary>
public class Infantry : RTSUnit
{
    const float GrenadeRange = 6.6f;
    const float GrenadeReloadDuration = 5.2f;
    const float GrenadeRecoveryCooldown = 1.35f;
    const int GrenadeDamage = 42;
    const float GrenadeSplashRadius = 2.2f;
    const float GrenadeSplashFalloff = 0.42f;
    const float GrenadeProjectileSpeed = 18f;
    const float GrenadeProjectileArcHeight = 2.9f;
    const float GrenadeProjectileImpactRadius = 0.82f;
    const float GrenadeSmokeIntensity = 0.4f;
    static readonly Color GrenadeProjectileTint = new Color(0.86f, 0.84f, 0.56f, 1f);

    static readonly string[] PreferredVisualPrefabPaths =
    {
        "Prefabs/Infantry_P",
        "Prefabs/Infantry_Player",
        "Prefabs/Infantry_Enemy",
        "Prefabs/Infantry_E",
        "Prefabs/Infantry",
    };

    protected override float DesiredVisualHeight => 3.6f;
    protected override float DesiredVisualFootprint => 2.0f;
    protected override float AgentAngularSpeed => 540f;
    protected override bool CanTraverseForestZones => true;
    float _nextGrenadeReadyTime;

    /// <summary>
    /// Applies the infantry stat line before the shared unit startup runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = "步兵";
        MaxHP = 200; AttackDamage = 25; AttackRange = 8f;
        AttackInterval = 1f; SightRange = 15f;
        GoldCost = 100; PopCost = 1; MoveSpeed = 8f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 82f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.34f;
        ProjectileTint = new Color(1f, 0.86f, 0.28f, 1f);
        TracerDuration = 0.04f;
        base.Awake();
        EnsureInfantryVisualModel();
    }

    protected override bool CanAttackAirUnit(AirUnit target)
    {
        return target is ScoutPlane;
    }

    protected override void DoAttack(RTSUnit target)
    {
        if (target == null)
            return;

        if (ShouldThrowGrenadeAt(target.transform.position, target.bFlying))
        {
            ThrowGrenadeAt(target.transform.position);
            return;
        }

        base.DoAttack(target);
    }

    protected override void DoAttackBuilding(RTSBuilding target)
    {
        if (target == null)
            return;

        if (ShouldThrowGrenadeAt(target.transform.position, false))
        {
            ThrowGrenadeAt(target.transform.position);
            return;
        }

        base.DoAttackBuilding(target);
    }

    /// <summary>
    /// Recolors infantry uniforms while preserving weapons, skin tones, and faction plate accents.
    /// </summary>
    protected override void ApplyMilitaryTint()
    {
        InfantryUniformVisuals.Apply(gameObject, bPlayerOwned);
    }

    void EnsureInfantryVisualModel()
    {
        Transform model = transform.Find("Model");
        if (!NeedsInfantryVisualRepair(model))
            return;

        model = ReplaceWithInfantryModel(model);
        if (model == null)
            return;

        BindInfantryVisual(model);
        ApplyMilitaryTint();
    }

    bool NeedsInfantryVisualRepair(Transform model)
    {
        UnitVisualAnimator visualAnimator = GetComponent<UnitVisualAnimator>();
        if (model == null || visualAnimator == null)
            return true;
        if (visualAnimator.Style != UnitVisualAnimator.VisualStyle.Infantry)
            return true;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return true;

        Transform weaponRoot = FindByName(model, "KenneyWeapon");
        if (weaponRoot == null)
            return true;

        BasicShooterRifleHandBinder handBinder = GetComponent<BasicShooterRifleHandBinder>();
        return handBinder == null
            || handBinder.WeaponRoot == null
            || !handBinder.WeaponRoot.IsChildOf(model);
    }

    Transform ReplaceWithInfantryModel(Transform existingModel)
    {
        GameObject visualPrefab = LoadPreferredInfantryVisualPrefab();
        Transform sourceModel = visualPrefab != null ? visualPrefab.transform.Find("Model") : null;
        if (sourceModel == null)
            return existingModel;

        if (existingModel != null)
        {
            existingModel.name = "RemovedInfantryModel";
            existingModel.gameObject.SetActive(false);
            Destroy(existingModel.gameObject);
        }

        Transform model = Instantiate(sourceModel, transform, false);
        model.name = "Model";
        return model;
    }

    GameObject LoadPreferredInfantryVisualPrefab()
    {
        for (int i = 0; i < PreferredVisualPrefabPaths.Length; i++)
        {
            GameObject prefab = Resources.Load<GameObject>(PreferredVisualPrefabPaths[i]);
            if (HasUsableInfantryVisualPrefab(prefab))
                return prefab;
        }

        return null;
    }

    static bool HasUsableInfantryVisualPrefab(GameObject prefab)
    {
        if (prefab == null)
            return false;

        Transform model = prefab.transform.Find("Model");
        if (model == null)
            return false;

        Animator animator = model.GetComponentInChildren<Animator>(true);
        return animator != null && FindByName(model, "KenneyWeapon") != null;
    }

    void BindInfantryVisual(Transform model)
    {
        UnitVisualAnimator visualAnimator = GetComponent<UnitVisualAnimator>();
        if (visualAnimator != null)
        {
            visualAnimator.Style = UnitVisualAnimator.VisualStyle.Infantry;
            visualAnimator.VisualRoot = model;
            visualAnimator.RebindVisualRootBasePose();
        }

        Animator animator = model.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.enabled = true;

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
        handBinder.ForwardOffset = new Vector3(0.02f, -0.015f, 0.08f);
        handBinder.EulerOffset = Vector3.zero;
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

    bool ShouldThrowGrenadeAt(Vector3 targetPosition, bool targetFlying)
    {
        if (targetFlying || Time.time < _nextGrenadeReadyTime)
            return false;

        Vector3 flatDelta = targetPosition - transform.position;
        flatDelta.y = 0f;
        return flatDelta.magnitude <= Mathf.Min(AttackRange, GrenadeRange);
    }

    void ThrowGrenadeAt(Vector3 targetPosition)
    {
        _nextGrenadeReadyTime = Time.time + GrenadeReloadDuration;
        QueueAttackCooldown(GrenadeRecoveryCooldown);
        PlayTemporaryAttackVisuals(
            targetPosition,
            "Prefabs/Projectiles/BattleProjectile_Shell",
            GrenadeProjectileSpeed,
            GrenadeProjectileArcHeight,
            GrenadeProjectileImpactRadius,
            GrenadeProjectileTint,
            0.06f,
            false);
        EffectsManager.EnsureInstance().StartCoroutine(
            ResolveGrenadeImpactAfterDelay(this, bPlayerOwned, targetPosition, GetGrenadeFlightDuration(targetPosition)));
    }

    float GetGrenadeFlightDuration(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + transform.forward * 0.35f + Vector3.up * 1.2f;
        Vector3 impact = targetPosition + Vector3.up * ProjectileVisualProfile.GetImpactHeightOffset(ProjectileType.Shell);
        return Mathf.Clamp(Vector3.Distance(origin, impact) / GrenadeProjectileSpeed, 0.08f, 1.8f);
    }

    static System.Collections.IEnumerator ResolveGrenadeImpactAfterDelay(Infantry attacker, bool attackerPlayerOwned, Vector3 center, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        PlayGrenadeSmoke(center);
        ApplyGrenadeDamage(attacker, attackerPlayerOwned, center);
    }

    static void PlayGrenadeSmoke(Vector3 center)
    {
        EffectsManager.PlayDamageSmoke(center, GrenadeSmokeIntensity, false);
    }

    static void ApplyGrenadeDamage(Infantry attacker, bool attackerPlayerOwned, Vector3 center)
    {
        var sync = GameNetworkSync.Instance;
        if (sync != null && sync.IsNetworkGame && !sync.IsHost)
            return;

        var units = GameManager.Instance?.GetAllUnits();
        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                RTSUnit unit = units[i];
                if (unit == null || unit == attacker || unit.IsDead() || unit.bPlayerOwned == attackerPlayerOwned || unit.bFlying)
                    continue;

                float distance = GroundDistance(center, unit.transform.position);
                if (distance > GrenadeSplashRadius)
                    continue;

                float damageMultiplier = Mathf.Lerp(1f, GrenadeSplashFalloff, Mathf.Clamp01(distance / GrenadeSplashRadius));
                bool wasAlive = !unit.IsDead();
                unit.TakeDamageFrom(attacker, Mathf.RoundToInt(GrenadeDamage * damageMultiplier));
                if (attacker != null && wasAlive && unit.IsDead())
                    attacker.AddKill();
            }
        }

        var buildings = GameManager.Instance?.GetAllBuildings();
        if (buildings == null)
            return;

        for (int i = 0; i < buildings.Count; i++)
        {
            RTSBuilding building = buildings[i];
            if (building == null || building.GetHP() <= 0 || building.bPlayerOwned == attackerPlayerOwned)
                continue;

            float distance = GroundDistance(center, building.transform.position);
            if (distance > GrenadeSplashRadius)
                continue;

            float damageMultiplier = Mathf.Lerp(1f, GrenadeSplashFalloff, Mathf.Clamp01(distance / GrenadeSplashRadius));
            int prevHP = building.GetHP();
            building.TakeDamageFrom(attacker, Mathf.RoundToInt(GrenadeDamage * damageMultiplier));
            if (attacker != null && prevHP > 0 && building.GetHP() <= 0)
                attacker.AddKill();
        }
    }
}
