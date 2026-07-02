using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public readonly record struct BattleBuildingRequirement(string BuildingKey, int Count, int RequiredLevel = 1)
{
    public string DisplayName
    {
        get
        {
            var display = BattleBuildingCatalog.Get(BuildingKey).DisplayName;
            return RequiredLevel > 1 ? $"{display} Lv.{RequiredLevel}" : display;
        }
    }
}

public readonly record struct BattleBuildingRequirementStatus(
    string BuildingKey,
    string DisplayName,
    int RequiredCount,
    int CurrentCount,
    int RequiredLevel = 1,
    int HighestLevel = 0)
{
    public bool IsMet => CurrentCount >= RequiredCount && HighestLevel >= RequiredLevel;
}

public readonly record struct BattleBuildingUpgradeDefinition(
    int Level,
    int GoldCost,
    float HealthMultiplier,
    float ProductionSpeedMultiplier,
    float IncomeMultiplier,
    float TurretDamageMultiplier,
    float TurretRangeBonus,
    int PopCapBonusAdd,
    int PowerProvidedAdd,
    BattleBuildingRequirement[] Requirements,
    float? MaxHealthOverride = null,
    int? PopCapBonusOverride = null,
    int? GoldIncomeOverride = null,
    float? TurretDamageOverride = null,
    float? TurretRangeOverride = null,
    float? TurretCooldownOverride = null,
    int? PowerProvidedOverride = null);

public static class BattleBuildingUpgradeCatalog
{
    public const int MaxLevel = 3;

    public static BattleBuildingUpgradeDefinition Get(string buildKey, int level)
    {
        var clampedLevel = Mathf.Clamp(level, 1, GetMaxLevel(buildKey));
        if (clampedLevel == 1)
            return BaseLevel();

        return buildKey switch
        {
            "main_base" => clampedLevel == 2 ? MainBaseLevel2() : MainBaseLevel3(),
            "barracks" => clampedLevel == 2
                ? ProductionLevel2(260, 980f, 1.15f)
                : ProductionLevel3(420, 1200f, 1.30f),
            "tank_factory" => clampedLevel == 2
                ? ProductionLevel2(320, 1020f, 1.15f)
                : ProductionLevel3(520, 1220f, 1.30f),
            "armor_factory" => clampedLevel == 2
                ? ProductionLevel2(420, 1100f, 1.12f)
                : ProductionLevel3(680, 1320f, 1.24f),
            "air_factory" => clampedLevel == 2 ? AirFactoryLevel2() : AirFactoryLevel3(),
            "naval_yard" => clampedLevel == 2
                ? ProductionLevel2(340, 930f, 1.12f)
                : ProductionLevel3(540, 1120f, 1.22f),
            "turret" => clampedLevel == 2 ? TurretLevel2() : TurretLevel3(),
            _ => BaseLevel()
        };
    }

    static BattleBuildingUpgradeDefinition BaseLevel()
        => new(1, 0, 1f, 1f, 1f, 1f, 0f, 0, 0, Array.Empty<BattleBuildingRequirement>());

    static BattleBuildingUpgradeDefinition MainBaseLevel2()
        => new(
            2, 800, 1f, 1f, 1f, 1f, 0f, 0, 0,
            new[]
            {
                new BattleBuildingRequirement("barracks", 1),
                new BattleBuildingRequirement("gold_mine", 2),
                new BattleBuildingRequirement("power_plant", 2)
            },
            MaxHealthOverride: 4200f,
            PopCapBonusOverride: 15,
            GoldIncomeOverride: 40);

    static BattleBuildingUpgradeDefinition MainBaseLevel3()
        => new(
            3, 1400, 1f, 1f, 1f, 1f, 0f, 0, 0,
            new[]
            {
                new BattleBuildingRequirement("barracks", 1),
                new BattleBuildingRequirement("gold_mine", 3),
                new BattleBuildingRequirement("power_plant", 3),
                new BattleBuildingRequirement("air_factory", 1),
                new BattleBuildingRequirement("tank_factory", 1),
                new BattleBuildingRequirement("armor_factory", 1)
            },
            MaxHealthOverride: 5600f,
            PopCapBonusOverride: 20,
            GoldIncomeOverride: 55);

    static BattleBuildingUpgradeDefinition ProductionLevel2(int cost, float maxHealth, float productionSpeed)
        => new(
            2, cost, 1f, productionSpeed, 1f, 1f, 0f, 0, 0,
            new[] { new BattleBuildingRequirement("main_base", 1, 2) },
            MaxHealthOverride: maxHealth);

    static BattleBuildingUpgradeDefinition ProductionLevel3(int cost, float maxHealth, float productionSpeed)
        => new(
            3, cost, 1f, productionSpeed, 1f, 1f, 0f, 0, 0,
            new[] { new BattleBuildingRequirement("main_base", 1, 3) },
            MaxHealthOverride: maxHealth);

    static BattleBuildingUpgradeDefinition AirFactoryLevel2()
        => new(
            2, 360, 1f, 1.12f, 1f, 1f, 0f, 0, 0,
            new[]
            {
                new BattleBuildingRequirement("main_base", 1, 2),
                new BattleBuildingRequirement("airfield", 1)
            },
            MaxHealthOverride: 980f);

    static BattleBuildingUpgradeDefinition AirFactoryLevel3()
        => new(
            3, 560, 1f, 1.25f, 1f, 1f, 0f, 0, 0,
            new[]
            {
                new BattleBuildingRequirement("main_base", 1, 3),
                new BattleBuildingRequirement("airfield", 2)
            },
            MaxHealthOverride: 1180f);

    static BattleBuildingUpgradeDefinition TurretLevel2()
        => new(
            2, 220, 1f, 1f, 1f, 1f, 0f, 0, 0,
            new[] { new BattleBuildingRequirement("main_base", 1, 2) },
            MaxHealthOverride: 860f,
            TurretDamageOverride: 72f,
            TurretRangeOverride: 34f,
            TurretCooldownOverride: 1.02f);

    static BattleBuildingUpgradeDefinition TurretLevel3()
        => new(
            3, 420, 1f, 1f, 1f, 1f, 0f, 0, 0,
            new[] { new BattleBuildingRequirement("main_base", 1, 3) },
            MaxHealthOverride: 1020f,
            TurretDamageOverride: 92f,
            TurretRangeOverride: 38f,
            TurretCooldownOverride: 0.88f);

    public static int GetMaxLevel(string buildKey)
        => buildKey switch
        {
            "power_plant" or "gold_mine" or "airfield" => 1,
            _ => MaxLevel
        };

    public static bool HasNextLevel(string buildKey, int currentLevel)
        => currentLevel < GetMaxLevel(buildKey);

    public static BattleBuildingRequirement[] GetRequirements(string buildKey, int level)
        => Get(buildKey, level).Requirements;

    public static BattleBuildingRequirementStatus[] EvaluateRequirements(
        IEnumerable<BattleBuildingRequirement> requirements,
        Func<string, int> countProvider,
        Func<string, int> levelProvider)
    {
        return requirements
            .Select(requirement => new BattleBuildingRequirementStatus(
                requirement.BuildingKey,
                requirement.DisplayName,
                requirement.Count,
                countProvider(requirement.BuildingKey),
                requirement.RequiredLevel,
                levelProvider(requirement.BuildingKey)))
            .ToArray();
    }
}
