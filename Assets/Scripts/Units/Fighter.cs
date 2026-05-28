using UnityEngine;
public class Fighter : AirUnit
{
    protected override float DesiredVisualHeight => 1.6f;
    protected override float DesiredVisualFootprint => 4.0f;

    protected override void Awake()
    {
        DisplayName = "战斗机";
        MaxHP = 180; AttackDamage = 55; AttackRange = 14f;
        AttackInterval = 1f; SightRange = 25f;
        GoldCost = 300; PopCost = 2; MoveSpeed = 16f;
        FlyHeight = 9f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 95f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.48f;
        ProjectileTint = new Color(0.55f, 0.92f, 1f, 1f);
        TracerDuration = 0.035f;
        base.Awake();
    }
}
