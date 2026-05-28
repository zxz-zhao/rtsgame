using UnityEngine;
public class Infantry : RTSUnit
{
    protected override float DesiredVisualHeight => 1.8f;
    protected override float DesiredVisualFootprint => 1.0f;

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
    }
}
