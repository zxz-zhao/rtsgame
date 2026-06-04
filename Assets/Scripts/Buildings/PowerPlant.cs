using UnityEngine;
public class PowerPlant : RTSBuilding
{
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 8f;

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "电厂"; MaxHP = 600; GoldCost = 200; PowerCost = 0;
        PopCapBonus = 50; PowerProvide = 50; bIsPowerPlant = true;
        bIsMainBase = false; bIsGoldMine = false;
        GoldIncomeAmount = 0; bAutoAttack = false;
    }

    protected override void Start()
    {
        base.Start();
    }
}
