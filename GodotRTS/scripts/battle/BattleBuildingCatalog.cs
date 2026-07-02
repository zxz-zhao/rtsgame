using Godot;
using System;

public readonly record struct BattleBuildingDefinition(
    string Key,
    string DisplayName,
    int GoldCost,
    float MaxHealth,
    Vector3 CollisionSize,
    int PopCapBonus,
    int GoldIncomeAmount,
    float GoldIncomeInterval,
    string[] ProductionRoster,
    bool IsMainBase,
    bool IsDefenseTurret,
    float AttackDamage,
    float AttackRange,
    float AttackCooldown,
    int PowerProvided,
    int PowerUsed,
    Color Tint);

public static class BattleBuildingCatalog
{
    public static readonly BattleBuildingDefinition MainBase = new(
        "main_base", "主基地", 0, 3000f, new Vector3(7f, 4.8f, 7f),
        10, 30, 5f, Array.Empty<string>(),
        true, false, 0f, 0f, 0f, 0, 0, new Color(0.31f, 0.38f, 0.42f));

    public static readonly BattleBuildingDefinition Barracks = new(
        "barracks", "兵工厂", 300, 800f, new Vector3(5.6f, 3.0f, 6.4f),
        0, 0, 5f, new[] { "infantry", "infantry_artillery", "infantry_flamethrower" },
        false, false, 0f, 0f, 0f, 0, 20, new Color(0.35f, 0.40f, 0.32f));

    public static readonly BattleBuildingDefinition TankFactory = new(
        "tank_factory", "特勤工厂", 400, 850f, new Vector3(7.2f, 3.6f, 8.0f),
        0, 0, 5f, new[] { "artillery", "anti_air_gun", "flamethrower" },
        false, false, 0f, 0f, 0f, 0, 35, new Color(0.30f, 0.36f, 0.38f));

    public static readonly BattleBuildingDefinition ArmorFactory = new(
        "armor_factory", "坦克工厂", 450, 900f, new Vector3(8.0f, 3.8f, 8.2f),
        0, 0, 5f, new[] { "light_tank", "tank", "heavy_tank" },
        false, false, 0f, 0f, 0f, 0, 40, new Color(0.32f, 0.36f, 0.38f));

    public static readonly BattleBuildingDefinition Airfield = new(
        "airfield", "停机坪", 260, 650f, new Vector3(8.8f, 1.8f, 9.8f),
        0, 0, 5f, Array.Empty<string>(),
        false, false, 0f, 0f, 0f, 0, 15, new Color(0.28f, 0.34f, 0.38f));

    public static readonly BattleBuildingDefinition AirFactory = new(
        "air_factory", "飞机工厂", 400, 800f, new Vector3(8.2f, 3.6f, 8.2f),
        0, 0, 5f, new[] { "fighter", "bomber", "scout_plane" },
        false, false, 0f, 0f, 0f, 0, 30, new Color(0.26f, 0.36f, 0.46f));

    public static readonly BattleBuildingDefinition NavalYard = new(
        "naval_yard", "船坞", 360, 760f, new Vector3(8.8f, 2.4f, 7.6f),
        0, 0, 5f, new[] { "patrol_boat", "destroyer_ship", "transport_ship" },
        false, false, 0f, 0f, 0f, 0, 20, new Color(0.18f, 0.32f, 0.38f));

    public static readonly BattleBuildingDefinition Turret = new(
        "turret", "炮塔", 350, 700f, new Vector3(4.2f, 4.8f, 4.2f),
        0, 0, 5f, Array.Empty<string>(),
        false, true, 55f, 30f, 1.2f, 0, 15, new Color(0.38f, 0.36f, 0.30f));

    public static readonly BattleBuildingDefinition PowerPlant = new(
        "power_plant", "发电厂", 200, 600f, new Vector3(5.0f, 3.8f, 5.0f),
        50, 0, 5f, Array.Empty<string>(),
        false, false, 0f, 0f, 0f, 50, 0, new Color(0.24f, 0.42f, 0.48f));

    public static readonly BattleBuildingDefinition GoldMine = new(
        "gold_mine", "金矿", 250, 500f, new Vector3(5.6f, 3.2f, 5.6f),
        0, 50, 5f, Array.Empty<string>(),
        false, false, 0f, 0f, 0f, 0, 10, new Color(0.50f, 0.42f, 0.22f));

    public static BattleBuildingDefinition[] PlayerBuildMenu { get; } =
    {
        Barracks,
        AirFactory,
        Airfield,
        TankFactory,
        ArmorFactory,
        Turret,
        GoldMine,
        PowerPlant,
        NavalYard
    };

    public static BattleBuildingDefinition Get(string key) => key switch
    {
        "barracks" => Barracks,
        "tank_factory" => TankFactory,
        "armor_factory" => ArmorFactory,
        "airfield" => Airfield,
        "air_factory" => AirFactory,
        "naval_yard" => NavalYard,
        "turret" => Turret,
        "power_plant" => PowerPlant,
        "gold_mine" => GoldMine,
        "main_base" => MainBase,
        _ => MainBase
    };
}
