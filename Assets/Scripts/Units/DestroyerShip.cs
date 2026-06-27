using UnityEngine;

/// <summary>
/// Long-range heavy ship that uses shell projectiles and light splash damage to pressure clustered targets.
/// </summary>
public class DestroyerShip : NavalUnit
{
    // Destroyers need slightly higher HUD anchors to clear their taller bridge and longer hull.
    protected override float DesiredVisualHeight => 1.8f;
    protected override float DesiredVisualFootprint => 5.7f;
    protected override float HealthBarHeight => 3.0f;
    protected override float UnitLabelHeight => 2.65f;
    protected override float SelectionRingRadius => 2.75f;

    /// <summary>
    /// Applies the destroyer's combat stats before the shared naval setup runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = "驱逐舰";
        MaxHP = 560; AttackDamage = 92; AttackRange = 24f;
        AttackInterval = 1.45f; SightRange = 34f;
        GoldCost = 420; PopCost = 3; MoveSpeed = 4.8f;
        SplashRadius = 2.6f; SplashFalloff = 0.5f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 48f; ProjectileArcHeight = 2.1f; ProjectileImpactRadius = 1.2f;
        ProjectileTint = new Color(1f, 0.62f, 0.22f, 1f);
        TracerDuration = 0.065f;
        base.Awake();
    }
}
