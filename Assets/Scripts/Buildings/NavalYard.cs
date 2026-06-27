using UnityEngine;
using System.Collections.Generic;

public class NavalYard : UpgradeableBuilding
{
    // Naval yards need a wider visual footprint so the dock area matches the ship prefabs they spawn.
    protected override float DesiredVisualHeight => 3.2f;
    protected override float DesiredVisualFootprint => 11f;
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            760,
            0,
            1f),
        new UpgradeLevelDefinition(
            2,
            930,
            340,
            1.12f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 2)),
        new UpgradeLevelDefinition(
            3,
            1120,
            540,
            1.22f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 3)),
    };

    /// <summary>
    /// Applies the baseline economy, health, and utility values for the naval production building.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "船坞";
        MaxHP = 760;
        GoldCost = 360;
        PowerCost = 20;
        PopCapBonus = 0;
        PowerProvide = 0;
        bIsMainBase = false;
        bIsPowerPlant = false;
        bIsGoldMine = false;
        GoldIncomeAmount = 0;
        bAutoAttack = false;
        ApplyConfiguredUpgradeLevel(false);
    }

    protected override UpgradeLevelDefinition[] GetUpgradeLevelDefinitions() => UpgradeLevels;

    /// <summary>
    /// Populates ship production arrays at runtime when the prefab was not preconfigured in the editor.
    /// </summary>
    protected override void Start()
    {
        if (ProductionUnits == null || ProductionUnits.Length == 0)
        {
            // Resolve faction-specific ship prefabs from Resources so this building can self-bootstrap.
            var patrol = Resources.Load<GameObject>(bPlayerOwned ? "Prefabs/PatrolBoat_P" : "Prefabs/PatrolBoat_E");
            var destroyer = Resources.Load<GameObject>(bPlayerOwned ? "Prefabs/DestroyerShip_P" : "Prefabs/DestroyerShip_E");
            var battleship = Resources.Load<GameObject>(bPlayerOwned ? "Prefabs/TransportShip_P" : "Prefabs/TransportShip_E");
            // Keep unit entries, build times, and gold costs aligned while composing the production roster.
            var list = new List<GameObject>();
            var times = new List<float>();
            var costs = new List<int>();
            if (patrol != null) { list.Add(patrol); times.Add(7f); costs.Add(180); }
            if (destroyer != null) { list.Add(destroyer); times.Add(16f); costs.Add(420); }
            if (battleship != null) { list.Add(battleship); times.Add(24f); costs.Add(620); }
            if (list.Count > 0)
            {
                ProductionUnits = list.ToArray();
                ProductionTimes = times.ToArray();
                ProductionCosts = costs.ToArray();
            }
        }
        base.Start();
    }
}
