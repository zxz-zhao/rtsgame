using UnityEngine;

public class InfantryArtillery : RTSUnit
{
    protected override float DesiredVisualHeight => 3.6f;
    protected override float DesiredVisualFootprint => 2.0f;
    protected override float AgentAngularSpeed => 540f;
    protected override bool CanTraverseForestZones => true;

    protected override void Awake()
    {
        DisplayName = "\u70ae\u5175";
        MaxHP = 150; AttackDamage = 65; AttackRange = 18f;
        AttackInterval = 2f; SightRange = 20f;
        GoldCost = 160; PopCost = 2; MoveSpeed = 5f;
        SplashRadius = 5f; SplashFalloff = 0.4f;
        ProjectilePrefabPath = "Prefabs/Projectiles/BattleProjectile_Shell";
        ProjectileSpeed = 34f; ProjectileArcHeight = 5.5f; ProjectileImpactRadius = 1.55f;
        ProjectileTint = new Color(1f, 0.48f, 0.14f, 1f);
        TracerDuration = 0.07f;
        InfantrySupportWeaponVisuals.Configure(gameObject, InfantrySupportWeaponVisuals.WeaponKind.ShoulderCannon);
        base.Awake();
    }

    protected override void ApplyMilitaryTint()
    {
        InfantryUniformVisuals.Apply(gameObject, bPlayerOwned);
    }
}
