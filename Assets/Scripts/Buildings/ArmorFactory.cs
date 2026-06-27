using UnityEngine;

public class ArmorFactory : UpgradeableBuilding
{
    public static readonly Vector3 ColliderSize = new Vector3(9f, 4f, 9f);
    public static readonly Vector3 ColliderCenter = new Vector3(0f, 2f, 0f);

    // Tank factories share the broad industrial footprint so armored production visuals stay readable.
    protected override float DesiredVisualHeight => 5.5f;
    protected override float DesiredVisualFootprint => 10f;

    private static readonly float[] DefaultProductionTimes = { 8f, 13f, 18f };
    private static readonly int[] DefaultProductionCosts = { 260, 420, 620 };
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            900,
            0,
            1f),
        new UpgradeLevelDefinition(
            2,
            1100,
            420,
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
            1320,
            680,
            1.24f,
            0,
            0,
            0,
            0,
            0f,
            0f,
            new UpgradeRequirement(typeof(MainBase), 1, "主基地", 3)),
    };

    /// <summary>
    /// Applies the baseline economy and durability values for the tank production building.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "坦克厂";
        MaxHP = 900;
        GoldCost = 450;
        PowerCost = 40;
        PopCapBonus = 0;
        PowerProvide = 0;
        bIsMainBase = false;
        bIsPowerPlant = false;
        bIsGoldMine = false;
        GoldIncomeAmount = 0;
        bAutoAttack = false;
        ConfigureCollider(GetComponent<BoxCollider>());
        ApplyConfiguredUpgradeLevel(false);
    }

    protected override UpgradeLevelDefinition[] GetUpgradeLevelDefinitions() => UpgradeLevels;

    public static void ConfigureCollider(BoxCollider collider)
    {
        if (collider == null)
            return;

        collider.size = ColliderSize;
        collider.center = ColliderCenter;
        collider.isTrigger = false;
        collider.enabled = true;
    }

    /// <summary>
    /// Keeps the roster aligned with the intended light / medium / heavy tank lineup even if old prefab data exists.
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

        return !HasProductionType<LightTank>(0)
            || !HasProductionType<MediumTank>(1)
            || !HasProductionType<HeavyTank>(2);
    }

    bool HasProductionType<T>(int idx) where T : RTSUnit
    {
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length)
            return false;
        return ProductionUnits[idx] != null && ProductionUnits[idx].GetComponent<T>() != null;
    }

    void BuildDefaultProductionRoster()
    {
        var lightTank = LoadProductionPrefab("LightTank");
        var mediumTank = LoadProductionPrefab("MediumTank");
        var heavyTank = LoadProductionPrefab("HeavyTank");

        var units = new System.Collections.Generic.List<GameObject>(3);
        var times = new System.Collections.Generic.List<float>(3);
        var costs = new System.Collections.Generic.List<int>(3);

        AddProductionEntry(units, times, costs, lightTank, 0);
        AddProductionEntry(units, times, costs, mediumTank, 1);
        AddProductionEntry(units, times, costs, heavyTank, 2);

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
}
