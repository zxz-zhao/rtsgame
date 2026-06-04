using UnityEngine;
public class Bomber : AirUnit
{
    protected override float DesiredVisualHeight => 2.0f;
    protected override float DesiredVisualFootprint => 6.0f;

    protected override void Awake()
    {
        DisplayName = "轰炸机";
        MaxHP = 300; AttackDamage = 130; AttackRange = 8f;
        AttackInterval = 3f; SightRange = 20f;
        GoldCost = 500; PopCost = 3; MoveSpeed = 11f;
        SplashRadius = 7f; SplashFalloff = 0.3f;
        FlyHeight = 11f;
        MaxFuelSeconds = DefaultBattleFuelSeconds;
        RefuelSeconds = 16f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bomb";
        ProjectileSpeed = 30f; ProjectileArcHeight = 4f; ProjectileImpactRadius = 2.1f;
        ProjectileTint = new Color(1f, 0.36f, 0.08f, 1f);
        TracerDuration = 0.08f;
        base.Awake();
    }
}
