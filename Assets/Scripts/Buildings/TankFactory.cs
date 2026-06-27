using UnityEngine;

public class TankFactory : UpgradeableBuilding
{
    public static readonly Vector3 ColliderSize = new Vector3(9f, 4f, 9f);
    public static readonly Vector3 ColliderCenter = new Vector3(0f, 2f, 0f);

    // Special factories keep a broad footprint so support-vehicle prefabs scale cleanly at runtime.
    protected override float DesiredVisualHeight => 5.5f;
    protected override float DesiredVisualFootprint => 10f;

    private static readonly float[] DefaultProductionTimes = { 10f, 9f, 8f };
    private static readonly int[] DefaultProductionCosts = { 160, 180, 220 };
    private static readonly UpgradeLevelDefinition[] UpgradeLevels =
    {
        new UpgradeLevelDefinition(
            1,
            850,
            0,
            1f),
        new UpgradeLevelDefinition(
            2,
            1020,
            320,
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
            1220,
            520,
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
    /// Applies the baseline economy and durability values for the special factory.
    /// </summary>
    public override void ApplyDefinitionDefaults()
    {
        DisplayName = "\u7279\u9700\u5382";
        MaxHP = 850;
        GoldCost = 400;
        PowerCost = 35;
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
    /// Keeps the roster aligned with the intended artillery / anti-air / flamethrower lineup even if old prefab data exists.
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

        return !HasProductionType<Artillery>(0)
            || !HasProductionType<AntiAirGun>(1)
            || !HasProductionType<Flamethrower>(2);
    }

    bool HasProductionType<T>(int idx) where T : RTSUnit
    {
        if (ProductionUnits == null || idx < 0 || idx >= ProductionUnits.Length)
            return false;
        return ProductionUnits[idx] != null && ProductionUnits[idx].GetComponent<T>() != null;
    }

    void BuildDefaultProductionRoster()
    {
        var artillery = LoadProductionPrefab("Artillery");
        var antiAirGun = LoadProductionPrefab("AntiAirGun");
        var flamethrower = LoadProductionPrefab("Flamethrower");

        var units = new System.Collections.Generic.List<GameObject>(3);
        var times = new System.Collections.Generic.List<float>(3);
        var costs = new System.Collections.Generic.List<int>(3);

        AddProductionEntry(units, times, costs, artillery, 0);
        AddProductionEntry(units, times, costs, antiAirGun, 1);
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
        if (unit is Artillery)
            unit.DisplayName = "火炮";
        else if (unit is Flamethrower)
            unit.DisplayName = "喷火车";
    }
}
