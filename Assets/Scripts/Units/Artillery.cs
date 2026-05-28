using UnityEngine;
public class Artillery : RTSUnit
{
    protected override float DesiredVisualHeight => 2.0f;
    protected override float DesiredVisualFootprint => 2.5f;

    protected override void Awake()
    {
        DisplayName = "炮兵";
        MaxHP = 150; AttackDamage = 65; AttackRange = 18f;
        AttackInterval = 2f; SightRange = 20f;
        GoldCost = 200; PopCost = 2; MoveSpeed = 5f;
        SplashRadius = 5f; SplashFalloff = 0.4f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 34f; ProjectileArcHeight = 5.5f; ProjectileImpactRadius = 1.55f;
        ProjectileTint = new Color(1f, 0.48f, 0.14f, 1f);
        TracerDuration = 0.07f;
        base.Awake();
    }
}
