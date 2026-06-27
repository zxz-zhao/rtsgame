using UnityEngine;

public class MediumTank : Tank
{
    protected override string TankUnitDisplayName => "二级中坦克";
    protected override int TankMaxHP => 520;
    protected override int TankAttackDamage => 92;
    protected override float TankAttackRange => 13f;
    protected override float TankAttackInterval => 1.45f;
    protected override float TankSightRange => 18f;
    protected override int TankGoldCost => 420;
    protected override int TankPopCost => 3;
    protected override float TankMoveSpeed => 5.2f;
    protected override float TankSplashRadius => 3.1f;
    protected override float TankSplashFalloff => 0.45f;
    protected override float TankProjectileSpeed => 46f;
    protected override float TankProjectileArcHeight => 1.7f;
    protected override float TankProjectileImpactRadius => 1.15f;
    protected override Color TankProjectileTint => new Color(1f, 0.56f, 0.18f, 1f);
    protected override float TankVisualHeight => 3.05f;
    protected override float TankVisualFootprint => 3.75f;
    protected override float TankHealthBarHeight => 3.7f;
    protected override float TankUnitLabelHeight => 3.35f;
    protected override float TankSelectionRingRadius => 1.95f;
}
