using UnityEngine;
public class PowerPlant : RTSBuilding
{
    // Power plants use a compact footprint but a taller profile so their silhouette reads clearly from above.
    protected override float DesiredVisualHeight => 6f;
    protected override float DesiredVisualFootprint => 8f;

    /// <summary>
    /// Applies the default power-generation and support values for a power plant.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "电厂"; MaxHP = 600; GoldCost = 200; PowerCost = 0;
        PopCapBonus = 50; PowerProvide = 50; bIsPowerPlant = true;
        bIsMainBase = false; bIsGoldMine = false;
        GoldIncomeAmount = 0; bAutoAttack = false;
    }

    /// <summary>
    /// Runs the shared building startup flow; the override is kept explicit for subclass clarity.
    /// </summary>
    protected override void Start()
    {
        base.Start();
    }
}
