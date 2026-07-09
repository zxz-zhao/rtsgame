using Godot;
using System.Collections.Generic;
using System.Linq;

public readonly record struct BattleTechDefinition(
    string Key,
    string DisplayName,
    string Description,
    int GoldCost,
    string RequiredBuildingKey,
    string Glyph,
    string BackgroundTexturePath,
    float Radius,
    float Duration,
    float Cooldown,
    float MoveMultiplier,
    float DamageMultiplier,
    float AttackRangeBonus,
    float AttackCooldownMultiplier,
    float DefenseReduction,
    float VisionBonus,
    float RegenPerSecond,
    Color Tint);

public static class BattleTechCatalog
{
    const string TechTextureRoot = "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/";

    public static readonly BattleTechDefinition SpeedBoost = new(
        "speed",
        "机动强化",
        "范围内友军获得更高机动能力。",
        0,
        "main_base",
        "速",
        TechTextureRoot + "tech_bg_speed_wings.png",
        13f,
        14f,
        28f,
        1.32f,
        1f,
        0f,
        1f,
        0f,
        0f,
        0f,
        new Color(0.38f, 0.92f, 1f, 0.82f));

    public static readonly BattleTechDefinition ArmorBoost = new(
        "armor",
        "装甲强化",
        "范围内友军减少受到的伤害。",
        0,
        "main_base",
        "甲",
        TechTextureRoot + "tech_bg_armor_plate.png",
        12f,
        16f,
        34f,
        1f,
        1f,
        0f,
        1f,
        0.28f,
        0f,
        0f,
        new Color(0.55f, 0.78f, 1f, 0.82f));

    public static readonly BattleTechDefinition FirepowerBoost = new(
        "firepower",
        "火力强化",
        "范围内友军提升攻击火力。",
        0,
        "main_base",
        "火",
        TechTextureRoot + "tech_bg_firepower_barrage.png",
        12f,
        12f,
        32f,
        1f,
        1.30f,
        0f,
        1f,
        0f,
        0f,
        0f,
        new Color(1f, 0.48f, 0.22f, 0.84f));

    public static readonly BattleTechDefinition RepairBoost = new(
        "repair",
        "战场维修",
        "范围内友军持续恢复，并减少少量伤害。",
        0,
        "main_base",
        "修",
        TechTextureRoot + "tech_bg_repair_workshop.png",
        11f,
        10f,
        30f,
        1f,
        1f,
        0f,
        1f,
        0.10f,
        0f,
        18f,
        new Color(0.42f, 1f, 0.58f, 0.82f));

    public static readonly BattleTechDefinition ReconBoost = new(
        "radar",
        "侦察雷达",
        "范围内友军提升视野与射程。",
        0,
        "main_base",
        "侦",
        TechTextureRoot + "tech_bg_radar_map.png",
        14f,
        18f,
        30f,
        1f,
        1f,
        2f,
        1f,
        0f,
        16f,
        0f,
        new Color(0.96f, 0.88f, 0.36f, 0.82f));

    public static readonly BattleTechDefinition RapidFireBoost = new(
        "rapid",
        "急速射击",
        "范围内友军缩短攻击间隔。",
        0,
        "main_base",
        "速",
        TechTextureRoot + "tech_bg_rapid_ammo.png",
        11f,
        12f,
        36f,
        1f,
        1f,
        0f,
        0.72f,
        0f,
        0f,
        0f,
        new Color(1f, 0.70f, 0.30f, 0.82f));

    public static readonly BattleTechDefinition HoldBoost = new(
        "hold",
        "阵地坚守",
        "范围内友军略微降低机动，但会更耐打。",
        0,
        "main_base",
        "守",
        TechTextureRoot + "tech_bg_hold_bunker.png",
        10f,
        18f,
        40f,
        0.92f,
        1.12f,
        1.2f,
        1f,
        0.18f,
        0f,
        0f,
        new Color(0.72f, 1f, 0.78f, 0.82f));

    public static readonly BattleTechDefinition AssaultBoost = new(
        "assault",
        "突击号令",
        "范围内友军同时提升速度、火力与攻速。",
        0,
        "main_base",
        "突",
        TechTextureRoot + "tech_bg_assault_arrows.png",
        13f,
        14f,
        38f,
        1.18f,
        1.16f,
        0f,
        0.90f,
        0f,
        0f,
        0f,
        new Color(1f, 0.34f, 0.48f, 0.82f));

    public static BattleTechDefinition[] All { get; } =
    {
        SpeedBoost,
        ArmorBoost,
        FirepowerBoost,
        RepairBoost,
        ReconBoost,
        RapidFireBoost,
        HoldBoost,
        AssaultBoost
    };

    public static BattleTechDefinition Get(string key)
        => All.FirstOrDefault(tech => tech.Key == key, SpeedBoost);

    public static IReadOnlyList<BattleTechDefinition> GetForBuilding(string buildKey)
        => All.Where(tech => tech.RequiredBuildingKey == buildKey).ToArray();
}
