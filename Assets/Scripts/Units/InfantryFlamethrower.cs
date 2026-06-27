using UnityEngine;

public class InfantryFlamethrower : RTSUnit
{
    protected override float DesiredVisualHeight => 3.6f;
    protected override float DesiredVisualFootprint => 2.0f;
    protected override float AgentAngularSpeed => 540f;
    protected override bool CanTraverseForestZones => true;

    protected override void Awake()
    {
        DisplayName = "\u55b7\u706b\u5175";
        MaxHP = 280; AttackDamage = 45; AttackRange = 5f;
        AttackInterval = 0.5f; SightRange = 12f;
        GoldCost = 220; PopCost = 2; MoveSpeed = 4.8f;
        SplashRadius = 3f; SplashFalloff = 0.5f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Flame";
        ProjectileSpeed = 30f; ProjectileArcHeight = 0f; ProjectileImpactRadius = 1.05f;
        ProjectileTint = new Color(1f, 0.34f, 0.08f, 1f);
        TracerDuration = 0.08f;
        InfantrySupportWeaponVisuals.Configure(gameObject, InfantrySupportWeaponVisuals.WeaponKind.Flamethrower);
        base.Awake();
    }

    protected override void ApplyMilitaryTint()
    {
        InfantryUniformVisuals.Apply(gameObject, bPlayerOwned);
    }
}
