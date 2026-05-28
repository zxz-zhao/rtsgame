using UnityEngine;
public class Flamethrower : RTSUnit
{
    protected override float DesiredVisualHeight => 1.55f;
    protected override float DesiredVisualFootprint => 2.25f;

    protected override void Awake()
    {
        DisplayName = "喷火车";
        MaxHP = 280; AttackDamage = 45; AttackRange = 5f;
        AttackInterval = 0.5f; SightRange = 12f;
        GoldCost = 300; PopCost = 2; MoveSpeed = 4.8f;
        SplashRadius = 3f; SplashFalloff = 0.5f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Flame";
        ProjectileSpeed = 30f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 1.05f;
        ProjectileTint = new Color(1f, 0.34f, 0.08f, 1f);
        TracerDuration = 0.08f;
        base.Awake();
    }
}
