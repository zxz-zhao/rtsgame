using UnityEngine;
public class GoldMine : RTSBuilding
{
    // Gold mines need a slightly fuller body so they do not look undersized inside the mining footprint ring.
    protected override float DesiredVisualHeight => 6.4f;
    protected override float DesiredVisualFootprint => 10f;

    /// <summary>
    /// Applies the baseline stats and passive income values for a gold mine.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "金矿"; MaxHP = 500; GoldCost = 250; PowerCost = 10;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false;
        bIsGoldMine = true; GoldIncomeAmount = 50; GoldIncomeInterval = 5f;
        bAutoAttack = false;
    }

    /// <summary>
    /// Runs the shared building startup flow; the override exists for consistency with other building subclasses.
    /// </summary>
    protected override void Start()
    {
        base.Start();
    }
}
