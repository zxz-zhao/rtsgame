using UnityEngine;
public class GoldMine : RTSBuilding
{
    protected override float DesiredVisualHeight => 5.2f;
    protected override float DesiredVisualFootprint => 8f;

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "金矿"; MaxHP = 500; GoldCost = 250; PowerCost = 10;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false;
        bIsGoldMine = true; GoldIncomeAmount = 50; GoldIncomeInterval = 5f;
        bAutoAttack = false;
    }

    protected override void Start()
    {
        base.Start();
    }
}
