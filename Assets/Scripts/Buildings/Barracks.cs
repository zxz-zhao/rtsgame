using UnityEngine;
public class Barracks : RTSBuilding
{
    protected override float DesiredVisualHeight => 5f;
    protected override float DesiredVisualFootprint => 8f;

    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "兵工厂"; MaxHP = 800; GoldCost = 300; PowerCost = 20;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false; bIsGoldMine = false;
        GoldIncomeAmount = 0; bAutoAttack = false;
    }

    protected override void Start()
    {
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            var p = Resources.Load<GameObject>(bPlayerOwned ? "Prefabs/Infantry_P" : "Prefabs/Infantry_E");
            if (p == null)
                p = Resources.Load<GameObject>("Prefabs/Infantry");
            if (p != null)
            {
                ProductionUnits = new GameObject[] { p };
                ProductionTimes = new float[] { 5f };
                ProductionCosts = new int[]   { 100 };
            }
        }
        base.Start();
    }
}
