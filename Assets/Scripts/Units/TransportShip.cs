using UnityEngine;

/// <summary>
/// Heavy battleship that trades speed and cost for long-range shell pressure.
/// </summary>
public class TransportShip : NavalUnit
{
    // Kept as TransportShip for existing resource paths; presented and balanced as a battleship.
    protected override float DesiredVisualHeight => 2.2f;
    protected override float DesiredVisualFootprint => 7.2f;
    protected override float HealthBarHeight => 3.25f;
    protected override float UnitLabelHeight => 2.95f;
    protected override float SelectionRingRadius => 3.35f;

    /// <summary>
    /// Applies the battleship's durable long-range stat line before the shared naval setup runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = "\u6218\u5217\u8230";
        MaxHP = 860; AttackDamage = 122; AttackRange = 27f;
        AttackInterval = 1.9f; SightRange = 36f;
        GoldCost = 620; PopCost = 4; MoveSpeed = 3.7f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 42f; ProjectileArcHeight = 2.8f; ProjectileImpactRadius = 1.55f;
        ProjectileTint = new Color(1f, 0.58f, 0.18f, 1f);
        TracerDuration = 0.075f;
        base.Awake();
    }
}
