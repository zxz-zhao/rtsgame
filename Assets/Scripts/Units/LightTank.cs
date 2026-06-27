using UnityEngine;

public class LightTank : Tank
{
    protected override string TankUnitDisplayName => "一级小坦克";
    protected override int TankMaxHP => 360;
    protected override int TankAttackDamage => 60;
    protected override float TankAttackRange => 11.5f;
    protected override float TankAttackInterval => 1.2f;
    protected override float TankSightRange => 17f;
    protected override int TankGoldCost => 260;
    protected override int TankPopCost => 2;
    protected override float TankMoveSpeed => 6.2f;
    protected override float TankSplashRadius => 1.8f;
    protected override float TankSplashFalloff => 0.35f;
    protected override float TankProjectileSpeed => 48f;
    protected override float TankProjectileArcHeight => 1.1f;
    protected override float TankProjectileImpactRadius => 0.9f;
    protected override Color TankProjectileTint => new Color(1f, 0.72f, 0.26f, 1f);
    protected override float TankVisualHeight => 2.7f;
    protected override float TankVisualFootprint => 3.2f;
    protected override float TankHealthBarHeight => 3.2f;
    protected override float TankUnitLabelHeight => 2.9f;
    protected override float TankSelectionRingRadius => 1.7f;
    protected override float TankCapsuleRadius => 1.0f;
    protected override float TankCapsuleHeight => 0.9f;
    protected override float TankTrackStep => 2.0f;
    protected override float TankTrackHalfWidth => 0.68f;
    protected override string RuntimeTankModelResourcePath => "Models/PanzerIV/pzIV";
    protected override string RuntimeTankModelInstanceName => "PanzerIV_Instance";
    protected override bool ShouldAimHullAtCombatTarget => false;
    protected override string[] TankTurretPartTokens => new[] { "turret_exterior", "turret_details", "turret_details_2", "barrel", "gun" };
    protected override string[] TankTurretPivotTokens => new[] { "GroupedTurretPivot", "turret_exterior", "turret_details", "turret" };
    protected override string[] TankTurretAimEndTokens => new[] { "barrel", "gun" };
    protected override string[] TankTurretRendererTokens => new[] { "turret_exterior", "turret_details", "turret", "barrel", "gun" };
    protected override bool ShouldExtractTankTurretMesh => false;
    protected override bool ShouldAutoOrientTankModelInstance => true;
    protected override Vector3[] TankModelInstanceEulerCandidates => new[]
    {
        Vector3.zero,
        new Vector3(0f, 90f, 0f),
        new Vector3(0f, -90f, 0f),
        new Vector3(0f, 180f, 0f),
        new Vector3(90f, 0f, 0f),
        new Vector3(-90f, 0f, 0f),
        new Vector3(0f, 0f, 90f),
        new Vector3(0f, 0f, -90f),
        new Vector3(90f, 180f, 0f),
        new Vector3(-90f, 180f, 0f),
    };
}
