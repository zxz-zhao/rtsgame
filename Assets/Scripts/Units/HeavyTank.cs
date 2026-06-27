using UnityEngine;

public class HeavyTank : Tank
{
    protected override string TankUnitDisplayName => "三级重坦克";
    protected override int TankMaxHP => 780;
    protected override int TankAttackDamage => 140;
    protected override float TankAttackRange => 15.5f;
    protected override float TankAttackInterval => 1.8f;
    protected override float TankSightRange => 19.5f;
    protected override int TankGoldCost => 620;
    protected override int TankPopCost => 4;
    protected override float TankMoveSpeed => 4.35f;
    protected override float TankSplashRadius => 4.8f;
    protected override float TankSplashFalloff => 0.5f;
    protected override float TankProjectileSpeed => 42f;
    protected override float TankProjectileArcHeight => 2.25f;
    protected override float TankProjectileImpactRadius => 1.5f;
    protected override Color TankProjectileTint => new Color(1f, 0.42f, 0.12f, 1f);
    protected override float TankTracerLifetime => 0.075f;
    protected override float TankVisualHeight => 3.55f;
    protected override float TankVisualFootprint => 4.35f;
    protected override float TankHealthBarHeight => 4.15f;
    protected override float TankUnitLabelHeight => 3.8f;
    protected override float TankSelectionRingRadius => 2.25f;
    protected override float TankCapsuleRadius => 1.45f;
    protected override float TankCapsuleHeight => 1.2f;
    protected override float TankTrackStep => 2.45f;
    protected override float TankTrackHalfWidth => 0.96f;
    protected override float TankInfantryDamageMultiplier => 0.08f;
    protected override float TankFlamethrowerDamageMultiplier => 0.18f;
    protected override float TankInfantryArtilleryDamageMultiplier => 0.5f;
    protected override bool ShouldAimHullAtCombatTarget => true;
    protected override bool ShouldUseTankTurretPivot => false;
    protected override bool ShouldAutoOrientTankModelInstance => true;
    protected override Vector3[] TankModelInstanceEulerCandidates => new[]
    {
        Vector3.zero,
        new Vector3(0f, 180f, 0f),
        new Vector3(90f, 0f, 0f),
        new Vector3(-90f, 0f, 0f),
        new Vector3(0f, 0f, 90f),
        new Vector3(0f, 0f, -90f),
        new Vector3(90f, 180f, 0f),
        new Vector3(-90f, 180f, 0f),
    };
}
