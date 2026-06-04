using UnityEngine;
public class ScoutPlane : AirUnit
{
    protected override float DesiredVisualHeight => 1.5f;
    protected override float DesiredVisualFootprint => 3.5f;
    public override bool RequiresAirfieldSlot => false;

    protected override void Awake()
    {
        DisplayName = "侦察机";
        MaxHP = 80; AttackDamage = 10; AttackRange = 12f;
        AttackInterval = 1f; SightRange = 50f;
        GoldCost = 150; PopCost = 1; MoveSpeed = 18f;
        FlyHeight = 7f;
        MaxFuelSeconds = DefaultBattleFuelSeconds;
        RefuelSeconds = 10f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Bullet";
        ProjectileSpeed = 88f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 0.34f;
        ProjectileTint = new Color(0.9f, 0.96f, 1f, 1f);
        TracerDuration = 0.035f;
        base.Awake();
    }
}
