using UnityEngine;
public class Turret : RTSBuilding
{
    protected override float DesiredVisualHeight => 4f;
    protected override float DesiredVisualFootprint => 4f;

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "炮塔"; MaxHP = 700; GoldCost = 350; PowerCost = 15;
        bAutoAttack = true; TurretRange = 30f; TurretDamage = 55; TurretInterval = 1.2f;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false; bIsGoldMine = false;
        GoldIncomeAmount = 0;
    }

    protected override void Start()
    {
        base.Start();
    }
}
