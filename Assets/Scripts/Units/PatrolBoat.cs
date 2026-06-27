using UnityEngine;

/// <summary>
/// Fast, inexpensive naval skirmisher that excels at scouting and light harassment.
/// </summary>
public class PatrolBoat : NavalUnit
{
    // Patrol boats use tighter presentation bounds so they still feel nimble in narrow channels.
    protected override float DesiredVisualHeight => 1.2f;
    protected override float DesiredVisualFootprint => 3.6f;
    protected override float HealthBarHeight => 2.4f;
    protected override float UnitLabelHeight => 2.05f;
    protected override float SelectionRingRadius => 1.9f;

    /// <summary>
    /// Applies the patrol boat's light-attack stat line before the shared naval setup runs.
    /// </summary>
    protected override void Awake()
    {
        DisplayName = "巡逻艇";
        MaxHP = 220; AttackDamage = 38; AttackRange = 17f;
        AttackInterval = 0.85f; SightRange = 27f;
        GoldCost = 180; PopCost = 1; MoveSpeed = 7.4f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 92f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.42f;
        ProjectileTint = new Color(0.68f, 0.95f, 1f, 1f);
        TracerDuration = 0.04f;
        base.Awake();
    }
}
