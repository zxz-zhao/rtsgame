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

public readonly record struct GlobalConquestFactionDefinition(
    string Key,
    string DisplayName,
    string Description,
    string StarterUnitKey,
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
        "infantry_flamethrower", "喷火兵", 220, 1, 280f, 4.8f, 45f, 5f, 0.5f, 8f, 0.66f,
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
        "anti_air_gun", "防空炮车", 260, 2, 320f, 5.6f, 52f, 16f, 0.45f, 9f, 0.86f,
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

    /// <summary>
    /// 航母：海上浮动停机场，自身不攻击，每艘提供 4 个舰载机机位。
    /// 战斗机 / 轰炸机可以把航母当作停机基地使用。
    /// </summary>
    public static readonly BattleUnitDefinition AircraftCarrier = new(
        "aircraft_carrier", "航母", 900, 5, 1400f, 2.8f, 0f, 0f, 0f, 20f, 1.48f,
        new Color(0.22f, 0.32f, 0.38f));

    public static readonly BattleUnitDefinition Submarine = new(
        "submarine", "攻击潜艇", 500, 3, 380f, 6.2f, 75f, 22f, 1.2f, 11f, 1.05f,
        new Color(0.18f, 0.28f, 0.35f));

    public static readonly BattleUnitDefinition Battleship = new(
        "battleship", "战列舰", 850, 4, 1150f, 3.6f, 135f, 28f, 1.8f, 16f, 1.35f,
        new Color(0.18f, 0.26f, 0.35f));

    public static BattleUnitDefinition[] PlayerRoster { get; } =
    {
        Infantry, InfantryArtillery, InfantryFlamethrower,
        LightTank, Tank, HeavyTank, Artillery, AntiAirGun,
        ScoutPlane, Fighter, Bomber,
        PatrolBoat, DestroyerShip, Battleship, TransportShip, AircraftCarrier, Submarine
    };

    public static BattleUnitDefinition[] GlobalConquestStarterRoster { get; } =
    {
        LightTank, Tank, HeavyTank, Artillery
    };

    public static readonly GlobalConquestFactionDefinition GuardArmy = new(
        "guard_army", "联盟", "正面推进最稳，适合稳扎稳打与阵地扩张。", "tank",
        new Color(0.74f, 0.84f, 0.68f));

    public static readonly GlobalConquestFactionDefinition ResistanceArmy = new(
        "resistance_army", "反抗", "机动穿插最快，擅长侦察、抢点和侧翼牵制。", "light_tank",
        new Color(0.90f, 0.76f, 0.46f));

    public static readonly GlobalConquestFactionDefinition IntelligenceArmy = new(
        "intelligence_army", "机械", "精英火力更强，适合高压推进与重点突破。", "heavy_tank",
        new Color(0.64f, 0.84f, 0.94f));

    public static GlobalConquestFactionDefinition[] GlobalConquestFactions { get; } =
    {
        GuardArmy, ResistanceArmy, IntelligenceArmy
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
        LightTank, Tank, HeavyTank, Artillery, AntiAirGun, Fighter, Bomber, AircraftCarrier, Submarine
    };

    public static BattleUnitDefinition Get(string key) => key switch
    {
        "infantry" => Infantry,
        "infantry_artillery" => InfantryArtillery,
        "infantry_flamethrower" or "flamethrower" => InfantryFlamethrower,
        "light_tank" => LightTank,
        "tank" or "medium_tank" => Tank,
        "artillery" => Artillery,
        "heavy_tank" => HeavyTank,
        "anti_air_gun" => AntiAirGun,
        "scout_plane" => ScoutPlane,
        "fighter" => Fighter,
        "bomber" => Bomber,
        "patrol_boat" => PatrolBoat,
        "destroyer_ship" => DestroyerShip,
        "battleship" => Battleship,
        "transport_ship" => TransportShip,
        "aircraft_carrier" => AircraftCarrier,
        "submarine" => Submarine,
        _ => Tank
    };

    public static bool IsGlobalConquestStarter(string key)
        => key is "light_tank" or "tank" or "heavy_tank" or "artillery";

    public static GlobalConquestFactionDefinition GetGlobalConquestFaction(string key) => key switch
    {
        "resistance_army" => ResistanceArmy,
        "intelligence_army" => IntelligenceArmy,
        _ => GuardArmy
    };

    public static GlobalConquestFactionDefinition GetGlobalConquestFactionByStarter(string unitKey) => unitKey switch
    {
        "light_tank" => ResistanceArmy,
        "heavy_tank" or "artillery" => IntelligenceArmy,
        "tank" or "medium_tank" => GuardArmy,
        _ => GuardArmy
    };

    public static bool IsInfantryLike(string key)
        => key is "infantry" or "infantry_artillery" or "infantry_flamethrower" or "flamethrower";

    public static bool IsAirUnit(string key)
        => key is "scout_plane" or "fighter" or "bomber";

    public static bool RequiresAirfieldSlot(string key)
        => key is "fighter" or "bomber";

    public static bool IsNavalUnit(string key)
        => key is "patrol_boat" or "destroyer_ship" or "battleship" or "transport_ship" or "aircraft_carrier" or "submarine";

    /// <summary>航母：海上浮动停机场，自身不攻击，为舰载机提供机位。</summary>
    public static bool IsCarrier(string key) => key == "aircraft_carrier";

    /// <summary>每艘航母提供的舰载机槽位数量（与陆基停机坪相同）。</summary>
    public const int CarrierSlotCount = 4;

    public static bool IsArtilleryLike(string key)
        => key is "artillery" or "infantry_artillery" or "bomber" or "destroyer_ship" or "battleship" or "transport_ship";

    public static float GetDamageMultiplier(string attackerKey, string targetKey)
    {
        bool attackerIsAir = IsAirUnit(attackerKey);
        bool attackerIsNaval = IsNavalUnit(attackerKey);
        bool attackerIsLand = !attackerIsAir && !attackerIsNaval;

        bool targetIsAir = IsAirUnit(targetKey);
        bool targetIsNaval = IsNavalUnit(targetKey);
        bool targetIsLand = !targetIsAir && !targetIsNaval;

        // 航母自身没有武装，无法攻击任何目标
        if (IsCarrier(attackerKey)) return 0f;

        // 1. Air vs Land/Sea/Air
        if (attackerIsAir)
        {
            if (attackerKey == "fighter")
            {
                if (targetIsAir) return 2.0f; // Fighter counters Air (+100%)
                if (targetIsLand || targetIsNaval) return 0.5f; // Fighter is weak vs Land/Sea (-50%)
            }
            else if (attackerKey == "bomber")
            {
                if (targetIsLand) return 1.8f; // Bomber counters Land (+80%)
                if (targetIsNaval) return 1.5f; // Bomber counters Sea (+50%)
                if (targetIsAir) return 0f; // Bomber cannot attack Air (damage 0)
            }
            else if (attackerKey == "scout_plane")
            {
                if (targetIsAir) return 0.3f; // Scout plane is very weak vs Air
                return 1.0f;
            }
        }

        // 2. Land vs Air/Sea/Land
        if (attackerIsLand)
        {
            if (attackerKey == "anti_air_gun")
            {
                if (targetIsAir) return 2.5f; // AA Gun counters Air (+150%)
                if (targetIsLand) return 0.6f; // AA Gun is weak vs Land (-40%)
            }
            else if (attackerKey is "infantry" or "infantry_artillery" or "infantry_flamethrower" or "flamethrower")
            {
                if (targetIsAir)
                {
                    if (attackerKey == "infantry")       return 0.8f; // Infantry: light anti-air
                    if (attackerKey == "infantry_artillery") return 0f; // Mortar: cannot target Air
                    return 0f; // Flamethrower: cannot target Air（射程不足，火焰无法打空中目标）
                }
                if (targetIsNaval) return 0.4f; // Infantry is weak vs Ships

                // 喷火兵特有克制：火焰对无防护软目标（步兵类）高效，对钢甲低效
                if (attackerKey is "infantry_flamethrower" or "flamethrower")
                {
                    if (IsInfantryLike(targetKey)) return 2.0f;   // Flamethrower counters Infantry (+100%)
                    if (targetKey is "light_tank" or "tank" or "medium_tank" or "heavy_tank"
                                  or "artillery" or "anti_air_gun")
                        return 0.35f; // Flamethrower is weak vs Armor (-65%)
                }
            }
            else // Tanks: light_tank, tank, heavy_tank, artillery
            {
                if (targetIsAir) return 0f; // Tanks and Artillery cannot target Air (damage 0)
                if (targetIsNaval)
                {
                    if (attackerKey == "artillery") return 1.8f; // Artillery counters Ships (+80%)
                    if (attackerKey == "heavy_tank") return 1.0f;
                    return 0.5f; // Other tanks are weak vs Ships
                }
            }
        }

        // 3. Sea vs Air/Land/Sea
        if (attackerIsNaval)
        {
            if (attackerKey == "patrol_boat")
            {
                if (targetIsAir) return 1.5f; // Patrol boat counters Air (+50%)
                if (targetIsLand) return 0.7f; // Patrol boat is weak vs Land (-30%)
                if (targetIsNaval) return 1.0f;
            }
            else if (attackerKey == "destroyer_ship")
            {
                if (targetIsNaval) return 1.8f; // Destroyer counters Sea (+80%)
                if (targetIsLand) return 1.5f; // Destroyer counters Land (+50%)
                if (targetIsAir) return 1.0f; // Destroyer has decent anti-air
            }
            else if (attackerKey == "transport_ship")
            {
                if (targetIsAir) return 0f; // Transport ship cannot target Air
                if (targetIsNaval) return 0.8f;
                if (targetIsLand) return 0.8f;
            }
        }

        return 1.0f;
    }

    public static float GetDamageMultiplierToBuilding(string attackerKey)
    {
        // Bomber counters buildings
        if (attackerKey == "bomber") return 1.8f;
        // Fighter is weak vs buildings
        if (attackerKey == "fighter") return 0.4f;
        // Artillery and Destroyer counters buildings
        if (attackerKey is "artillery" or "destroyer_ship") return 1.6f;
        // Anti-air gun is weak vs buildings
        if (attackerKey == "anti_air_gun") return 0.4f;
        // Flamethrower is strong vs buildings
        if (attackerKey is "infantry_flamethrower" or "flamethrower") return 1.4f;
        // Infantry is slightly weaker vs buildings
        if (attackerKey == "infantry") return 0.8f;
        
        return 1.0f;
    }

    public static bool CanAttackTargetType(string attackerKey, string targetUnitKey)
    {
        return GetDamageMultiplier(attackerKey, targetUnitKey) > 0f;
    }


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
