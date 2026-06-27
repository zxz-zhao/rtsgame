using UnityEngine;
public class Barracks : UpgradeableBuilding
{
    // Barracks stay relatively compact so infantry production visuals fit tightly near the front line.
    protected override float DesiredVisualHeight => 5f;
    protected override float DesiredVisualFootprint => 8f;

    private static readonly float[] DefaultProductionTimes = { 5f, 10f, 8f };
    private static readonly int[] DefaultProductionCosts = { 100, 160, 220 };
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            800,
            0,
            1f),
        new UpgradeLevelDefinition(
            2,
            980,
            260,
            1.15f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 2)),
        new UpgradeLevelDefinition(
            3,
            1200,
            420,
            1.30f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 3)),
    };

    /// <summary>
    /// Applies the baseline economy and survivability values for the infantry production building.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "兵工厂"; MaxHP = 800; GoldCost = 300; PowerCost = 20;
        PopCapBonus = 0; PowerProvide = 0;
        bIsMainBase = false; bIsPowerPlant = false; bIsGoldMine = false;
        GoldIncomeAmount = 0; bAutoAttack = false;
        ApplyConfiguredUpgradeLevel(false);
    }

    protected override UpgradeLevelDefinition[] GetUpgradeLevelDefinitions() => UpgradeLevels;

    /// <summary>
    /// Keeps the roster aligned with the intended infantry / artillery / flamethrower lineup even if old prefab data exists.
    /// </summary>
    protected override void Start()
    {
        if (NeedsDefaultProductionRoster())
            BuildDefaultProductionRoster();

        base.Start();
    }

    bool NeedsDefaultProductionRoster()
    {
        if (ProductionUnits == null || ProductionUnits.Length != 3)
            return true;

        return !HasProductionType<Infantry>(0)
            || !HasProductionType<InfantryArtillery>(1)
            || !HasProductionType<InfantryFlamethrower>(2);
    }

    bool HasProductionType<T>(int idx) where T : RTSUnit
    {
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length)
            return false;
        return ProductionUnits[idx] != null && ProductionUnits[idx].GetComponent<T>() != null;
    }

    void BuildDefaultProductionRoster()
    {
        var infantry = LoadProductionPrefab("Infantry");
        var artillery = LoadProductionPrefab("InfantryArtillery");
        var flamethrower = LoadProductionPrefab("InfantryFlamethrower");

        var units = new System.Collections.Generic.List<GameObject>(3);
        var times = new System.Collections.Generic.List<float>(3);
        var costs = new System.Collections.Generic.List<int>(3);

        AddProductionEntry(units, times, costs, infantry, 0);
        AddProductionEntry(units, times, costs, artillery, 1);
        AddProductionEntry(units, times, costs, flamethrower, 2);

        ProductionUnits = units.ToArray();
        ProductionTimes = times.ToArray();
        ProductionCosts = costs.ToArray();
    }

    void AddProductionEntry(
        System.Collections.Generic.List<GameObject> units,
        System.Collections.Generic.List<float> times,
        System.Collections.Generic.List<int> costs,
        GameObject prefab,
        int defaultIdx)
    {
        if (prefab == null)
            return;

        units.Add(prefab);
        times.Add(DefaultProductionTimes[Mathf.Clamp(defaultIdx, 0, DefaultProductionTimes.Length - 1)]);
        costs.Add(DefaultProductionCosts[Mathf.Clamp(defaultIdx, 0, DefaultProductionCosts.Length - 1)]);
    }

    GameObject LoadProductionPrefab(string baseName)
    {
        string ownedPath = bPlayerOwned ? $"Prefabs/{baseName}_P" : $"Prefabs/{baseName}_E";
        return Resources.Load<GameObject>(ownedPath)
            ?? Resources.Load<GameObject>($"Prefabs/{baseName}");
    }

    protected override void OnUnitSpawnedFromProduction(RTSUnit unit, int idx)
    {
        base.OnUnitSpawnedFromProduction(unit, idx);
        if (unit is InfantryArtillery)
            unit.DisplayName = "炮兵";
        else if (unit is InfantryFlamethrower)
            unit.DisplayName = "喷火兵";
    }
}
