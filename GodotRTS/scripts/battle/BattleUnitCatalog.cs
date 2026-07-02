using Godot;
using System;

public readonly record struct BattleUnitDefinition(
    string Key,
    string DisplayName,
    int GoldCost,
    int PopCost,
    float MaxHealth,
    float MoveSpeed,
    float AttackDamage,
    float AttackRange,
    float AttackCooldown,
    float ProductionTime,
    float VisualScale,
    Color Tint);

public static class BattleUnitCatalog
{
    public static readonly BattleUnitDefinition Infantry = new(
        "infantry", "步兵", 100, 1, 200f, 8f, 25f, 8f, 1.0f, 4f, 0.55f,
        new Color(0.38f, 0.47f, 0.30f));

    public static readonly BattleUnitDefinition InfantryArtillery = new(
        "infantry_artillery", "迫击炮兵", 160, 2, 150f, 5f, 65f, 18f, 2.0f, 7f, 0.62f,
        new Color(0.42f, 0.42f, 0.34f));

    public static readonly BattleUnitDefinition InfantryFlamethrower = new(
        "infantry_flamethrower", "喷火兵", 220, 2, 280f, 4.8f, 45f, 5f, 0.5f, 8f, 0.66f,
        new Color(0.42f, 0.31f, 0.24f));

    public static readonly BattleUnitDefinition LightTank = new(
        "light_tank", "轻型坦克", 260, 2, 360f, 7.6f, 60f, 11.5f, 1.2f, 7f, 0.78f,
        new Color(0.52f, 0.54f, 0.36f));

    public static readonly BattleUnitDefinition Tank = new(
        "tank", "中型坦克", 420, 3, 520f, 6.9f, 92f, 13f, 1.45f, 8f, 0.95f,
        new Color(0.45f, 0.48f, 0.32f));

    public static readonly BattleUnitDefinition HeavyTank = new(
        "heavy_tank", "重型坦克", 620, 4, 780f, 5.7f, 140f, 15.5f, 1.8f, 13f, 1.12f,
        new Color(0.38f, 0.40f, 0.28f));

    public static readonly BattleUnitDefinition Artillery = new(
        "artillery", "自行火炮", 200, 2, 150f, 5f, 65f, 18f, 2.0f, 10f, 0.88f,
        new Color(0.44f, 0.42f, 0.34f));

    public static readonly BattleUnitDefinition AntiAirGun = new(
        "anti_air_gun", "防空车", 260, 2, 320f, 5.6f, 52f, 16f, 0.45f, 9f, 0.86f,
        new Color(0.34f, 0.40f, 0.37f));

    public static readonly BattleUnitDefinition ScoutPlane = new(
        "scout_plane", "侦察机", 150, 1, 80f, 18f, 10f, 12f, 1.0f, 8f, 0.78f,
        new Color(0.42f, 0.58f, 0.68f));

    public static readonly BattleUnitDefinition Fighter = new(
        "fighter", "战斗机", 300, 2, 180f, 16f, 55f, 14f, 1.0f, 11f, 0.95f,
        new Color(0.36f, 0.48f, 0.58f));

    public static readonly BattleUnitDefinition Bomber = new(
        "bomber", "轰炸机", 500, 3, 300f, 10.5f, 130f, 8f, 3.0f, 15f, 1.18f,
        new Color(0.34f, 0.38f, 0.44f));

    public static readonly BattleUnitDefinition PatrolBoat = new(
        "patrol_boat", "巡逻艇", 180, 1, 220f, 7.4f, 38f, 17f, 0.85f, 8f, 0.95f,
        new Color(0.22f, 0.42f, 0.48f));

    public static readonly BattleUnitDefinition DestroyerShip = new(
        "destroyer_ship", "驱逐舰", 420, 3, 560f, 4.8f, 92f, 24f, 1.45f, 13f, 1.18f,
        new Color(0.20f, 0.34f, 0.42f));

    public static readonly BattleUnitDefinition TransportShip = new(
        "transport_ship", "运输舰", 620, 4, 860f, 3.7f, 122f, 27f, 1.9f, 15f, 1.28f,
        new Color(0.24f, 0.36f, 0.40f));

    public static BattleUnitDefinition[] PlayerRoster { get; } =
    {
        Infantry, InfantryArtillery, InfantryFlamethrower,
        LightTank, Tank, HeavyTank, Artillery, AntiAirGun,
        ScoutPlane, Fighter, Bomber,
        PatrolBoat, DestroyerShip, TransportShip
    };

    public static BattleUnitDefinition[] GlobalConquestStarterRoster { get; } =
    {
        LightTank, Tank, HeavyTank, Artillery
    };

    public static BattleUnitDefinition[] AiEarlyRoster { get; } =
    {
        Infantry, Infantry, LightTank
    };

    public static BattleUnitDefinition[] AiMidRoster { get; } =
    {
        Infantry, InfantryArtillery, InfantryFlamethrower, LightTank, Tank, Artillery
    };

    public static BattleUnitDefinition[] AiLateRoster { get; } =
    {
        LightTank, Tank, HeavyTank, Artillery, AntiAirGun, Fighter, Bomber
    };

    public static BattleUnitDefinition Get(string key) => key switch
    {
        "infantry" => Infantry,
        "infantry_artillery" => InfantryArtillery,
        "infantry_flamethrower" => InfantryFlamethrower,
        "flamethrower" => InfantryFlamethrower,
        "light_tank" => LightTank,
        "tank" => Tank,
        "medium_tank" => Tank,
        "artillery" => Artillery,
        "heavy_tank" => HeavyTank,
        "anti_air_gun" => AntiAirGun,
        "scout_plane" => ScoutPlane,
        "fighter" => Fighter,
        "bomber" => Bomber,
        "patrol_boat" => PatrolBoat,
        "destroyer_ship" => DestroyerShip,
        "transport_ship" => TransportShip,
        _ => Tank
    };

    public static bool IsGlobalConquestStarter(string key)
        => key is "light_tank" or "tank" or "heavy_tank" or "artillery";

    public static bool IsInfantryLike(string key)
        => key is "infantry" or "infantry_artillery" or "infantry_flamethrower" or "flamethrower";

    public static bool IsAirUnit(string key)
        => key is "scout_plane" or "fighter" or "bomber";

    public static bool RequiresAirfieldSlot(string key)
        => key is "fighter" or "bomber";

    public static bool IsNavalUnit(string key)
        => key is "patrol_boat" or "destroyer_ship" or "transport_ship";

    public static bool IsArtilleryLike(string key)
        => key is "artillery" or "infantry_artillery" or "bomber" or "destroyer_ship" or "transport_ship";

    public static float SplashRadius(string key)
        => key switch
        {
            "infantry_artillery" => 5f,
            "infantry_flamethrower" or "flamethrower" => 3f,
            "light_tank" => 1.8f,
            "tank" or "medium_tank" => 3.1f,
            "heavy_tank" => 4.8f,
            "artillery" => 5f,
            "bomber" => 7f,
            "destroyer_ship" => 2.6f,
            _ => 0f
        };

    public static float SplashFalloff(string key)
        => key switch
        {
            "infantry_artillery" or "artillery" => 0.4f,
            "infantry_flamethrower" or "flamethrower" => 0.5f,
            "light_tank" => 0.35f,
            "tank" or "medium_tank" => 0.45f,
            "heavy_tank" or "bomber" or "destroyer_ship" or "transport_ship" => 0.5f,
            _ => 0.5f
        };

    public static float SpawnHeight(string key)
        => IsAirUnit(key) ? 9.5f : IsNavalUnit(key) ? 0.25f : 0f;

    public static BattleUnitDefinition PickAi(float gameTime, RandomNumberGenerator rng)
    {
        var roster = gameTime < 75f
            ? AiEarlyRoster
            : gameTime < 210f
                ? AiMidRoster
                : AiLateRoster;
        return roster[rng.RandiRange(0, roster.Length - 1)];
    }
}
