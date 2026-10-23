using Godot;
using System;
using GodotRTS.Battle.Combat;

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
        "infantry", "步兵", 100, 1, 200f, 4.6f, 25f, 8f, 1.0f, 4f, 0.55f,
        new Color(0.38f, 0.47f, 0.30f));

    public static readonly BattleUnitDefinition InfantryArtillery = new(
        "infantry_artillery", "迫击炮兵", 160, 2, 150f, 4.0f, 65f, 18f, 2.0f, 7f, 0.62f,
        new Color(0.42f, 0.42f, 0.34f));

    public static readonly BattleUnitDefinition InfantryFlamethrower = new(
        "infantry_flamethrower", "喷火兵", 220, 1, 280f, 4.2f, 45f, 5f, 0.5f, 8f, 0.66f,
        new Color(0.42f, 0.31f, 0.24f));

    public static readonly BattleUnitDefinition LightTank = new(
        "light_tank", "轻型坦克", 260, 2, 360f, 7.6f, 60f, 11.5f, 1.2f, 7f, 0.85f,
        new Color(0.52f, 0.54f, 0.36f));

    public static readonly BattleUnitDefinition Tank = new(
        "tank", "中型坦克", 420, 3, 520f, 6.9f, 92f, 13f, 1.45f, 8f, 1.00f,
        new Color(0.45f, 0.48f, 0.32f));

    public static readonly BattleUnitDefinition HeavyTank = new(
        "heavy_tank", "重型坦克", 620, 4, 780f, 5.7f, 140f, 15.5f, 1.8f, 13f, 1.18f,
        new Color(0.38f, 0.40f, 0.28f));

    public static readonly BattleUnitDefinition TankDestroyer = new(
        "tank_destroyer", "坦克歼击车", 480, 3, 440f, 6.2f, 130f, 17f, 1.8f, 10f, 0.96f,
        new Color(0.48f, 0.46f, 0.32f));

    public static readonly BattleUnitDefinition Artillery = new(
        "artillery", "自行火炮", 200, 2, 150f, 5f, 65f, 30f, 2.0f, 10f, 0.88f,
        new Color(0.44f, 0.42f, 0.34f));

    public static readonly BattleUnitDefinition MlrsRocket = new(
        "mlrs_rocket", "多管火箭炮", 380, 3, 260f, 5.2f, 110f, 26f, 2.8f, 12f, 0.90f,
        new Color(0.40f, 0.44f, 0.32f));

    public static readonly BattleUnitDefinition AntiAirGun = new(
        "anti_air_gun", "防空炮车", 260, 2, 320f, 5.6f, 52f, 16f, 0.45f, 9f, 0.86f,
        new Color(0.34f, 0.40f, 0.37f));

    public static readonly BattleUnitDefinition SamMissile = new(
        "sam_missile", "防空导弹车", 340, 2, 280f, 5.4f, 88f, 20f, 1.6f, 10f, 0.88f,
        new Color(0.32f, 0.42f, 0.44f));

    public static readonly BattleUnitDefinition ScoutPlane = new(
        "scout_plane", "侦察机", 150, 1, 80f, 18f, 10f, 12f, 1.0f, 8f, 0.78f,
        new Color(0.42f, 0.58f, 0.68f));

    public static readonly BattleUnitDefinition ScoutHelicopter = new(
        "scout_helicopter", "武装直升机", 260, 2, 220f, 9.5f, 42f, 13.5f, 0.75f, 9f, 0.88f,
        new Color(0.35f, 0.52f, 0.45f));

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
    /// 航母：海上浮动停机场与远洋航空打击核心。自身仅配备极低伤害的近防自卫防空炮（CIWS 密集阵），
    /// 核心战斗力来自舰载机联队（战斗机、轰炸机）的远程弹射起飞与空袭。
    /// </summary>
    public static readonly BattleUnitDefinition AircraftCarrier = new(
        "aircraft_carrier", "航母", 900, 5, 1500f, 2.8f, 22f, 22f, 0.65f, 20f, 1.48f,
        new Color(0.22f, 0.32f, 0.38f));

    public static readonly BattleUnitDefinition Submarine = new(
        "submarine", "攻击潜艇", 500, 3, 380f, 6.2f, 75f, 22f, 1.2f, 11f, 1.05f,
        new Color(0.08f, 0.08f, 0.09f));

    public static readonly BattleUnitDefinition Battleship = new(
        "battleship", "战列舰", 850, 4, 1150f, 3.6f, 135f, 28f, 1.8f, 16f, 1.35f,
        new Color(0.18f, 0.26f, 0.35f));

    public static BattleUnitDefinition[] PlayerRoster { get; } =
    {
        // 步兵兵种分支 (3种)
        Infantry, InfantryArtillery, InfantryFlamethrower,
        // 装甲战车兵种分支 (4种)
        LightTank, Tank, HeavyTank, TankDestroyer,
        // 炮兵与防空支援兵种分支 (4种)
        Artillery, MlrsRocket, AntiAirGun, SamMissile,
        // 陆航与空军兵种分支 (4种)
        ScoutPlane, ScoutHelicopter, Fighter, Bomber,
        // 海军舰艇兵种分支 (5种)
        PatrolBoat, DestroyerShip, Submarine, Battleship, AircraftCarrier
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
        Infantry, InfantryArtillery, InfantryFlamethrower, LightTank, Tank, TankDestroyer, Artillery, MlrsRocket, ScoutHelicopter
    };

    public static BattleUnitDefinition[] AiLateRoster { get; } =
    {
        LightTank, Tank, HeavyTank, TankDestroyer, Artillery, MlrsRocket, AntiAirGun, SamMissile, ScoutHelicopter, Fighter, Bomber, AircraftCarrier, Submarine, Battleship
    };

    public static BattleUnitDefinition Get(string key) => key switch
    {
        "infantry" or "infantry_rifle" => Infantry,
        "infantry_artillery" or "infantry_rocket" => InfantryArtillery,
        "infantry_flamethrower" or "flamethrower" => InfantryFlamethrower,
        "light_tank" => LightTank,
        "tank" or "medium_tank" => Tank,
        "artillery" or "howitzer" => Artillery,
        "heavy_tank" => HeavyTank,
        "tank_destroyer" => TankDestroyer,
        "mlrs_rocket" or "rocket_artillery" => MlrsRocket,
        "anti_air_gun" => AntiAirGun,
        "sam_missile" => SamMissile,
        "scout_plane" => ScoutPlane,
        "scout_helicopter" => ScoutHelicopter,
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
        => key is "scout_plane" or "scout_helicopter" or "fighter" or "bomber";

    /// <summary>固定翼战机：战斗机、轰炸机、侦察机，具备真实气动前向推力与盘旋回转约束，严禁原地掉头与空中急停</summary>
    public static bool IsFixedWingAircraft(string key)
        => key is "scout_plane" or "fighter" or "bomber";

    /// <summary>旋翼飞行器：武装直升机，可悬停、定点航向旋转与原地转弯</summary>
    public static bool IsRotaryWingAircraft(string key)
        => key is "scout_helicopter";

    public static bool RequiresAirfieldSlot(string key)
        => key is "fighter" or "bomber";

    public static bool IsNavalUnit(string key)
        => key is "patrol_boat" or "destroyer_ship" or "battleship" or "transport_ship" or "aircraft_carrier" or "submarine";

    /// <summary>航母：海上浮动停机场与远洋航空母舰旗舰，提供机位并弹射起飞舰载战机与轰炸机。</summary>
    public static bool IsCarrier(string key) => key == "aircraft_carrier";

    /// <summary>每艘航母提供的舰载机槽位数量（与陆基停机坪相同）。</summary>
    public const int CarrierSlotCount = 4;

    /// <summary>航母舰载机超远程航空索敌与战术空袭最大有效半径 (米)。</summary>
    public const float CarrierAirStrikeRange = 85.0f;

    /// <summary>航母与目标保持的安全战术交火阵位距离 (米)，避免孤身冲入敌军战列舰巨炮或重炮射程。</summary>
    public const float CarrierStandoffDistance = 42.0f;

    /// <summary>航母弹射器连续弹射起飞间隔时间 (秒)。</summary>
    public const float CarrierScrambleInterval = 3.2f;

    /// <summary>航母停靠补给与机体抢修所需驻留时间 (秒)。飞机需在甲板停泊 10 秒以恢复燃油、弹药并修复机体损坏。</summary>
    public const float CarrierTurnaroundSeconds = 10.0f;

    /// <summary>获取空中单位标准备弹载弹量。轰炸机挂载 4 枚重型航弹，战斗机装备 6 枚空空/空地导弹，侦察机 10 发；其余单位无弹药限制（-1）。</summary>
    public static int GetAirUnitMaxAmmo(string key) => key switch
    {
        "bomber" => 4,
        "fighter" => 6,
        "scout_plane" => 10,
        _ => -1
    };

    /// <summary>航母飞行甲板 4 个舰载机停机位局部坐标。</summary>
    public static readonly Vector3[] CarrierDeckSlots = new Vector3[]
    {
        new Vector3(-0.45f, 0.40f, -2.2f), // Slot 0: 前左甲板
        new Vector3( 0.45f, 0.40f, -2.2f), // Slot 1: 前右甲板
        new Vector3(-0.45f, 0.40f,  1.2f), // Slot 2: 后左甲板
        new Vector3( 0.45f, 0.40f,  2.8f)  // Slot 3: 后右甲板
    };

    public static bool IsTankLike(string key)
        => key is "light_tank" or "tank" or "medium_tank" or "heavy_tank" or "anti_air_gun" or "tank_destroyer" or "mlrs_rocket" or "sam_missile";

    /// <summary>
    /// 单位是否具备行进间射击（走动动态攻击）能力。
    /// 坦克类（轻坦、中坦、重坦）、防空炮车、巡逻艇、战列舰、航母（近防炮）、低空直升机等具备 360° 旋转炮塔/行进火控稳定的载具支持边走边打。
    /// 自行火炮、迫击炮、火箭炮车、坦克歼击车与防空导弹车需停下架设驻锄。
    /// </summary>
    public static bool CanFireWhileMoving(string key)
    {
        if (key is "artillery" or "infantry_artillery" or "mlrs_rocket" or "tank_destroyer" or "sam_missile")
            return false;

        return IsTankLike(key) || IsNavalUnit(key) || key == "scout_helicopter";
    }

    /// <summary>
    /// 获取单位底盘物理旋转角速度（弧度/秒）。
    /// 步兵转向灵活（14 rad/s），轻坦机动（4.5 rad/s，约 258°/s），主战/防空车沉稳（3.4 rad/s，约 195°/s），
    /// 重装战车（2.4 rad/s，约 138°/s），舰船平稳（0.8~1.8 rad/s），杜绝超高角速度硬刹甩头与晃动。
    /// </summary>
    public static float GetTurnSpeed(string key) => key switch
    {
        "infantry" or "infantry_artillery" or "infantry_flamethrower" or "flamethrower" => 14.0f,
        "scout_helicopter" => 3.8f,
        "light_tank" => 4.5f,
        "tank" or "medium_tank" or "anti_air_gun" => 3.4f,
        "tank_destroyer" or "sam_missile" => 2.8f,
        "heavy_tank" or "artillery" or "mlrs_rocket" => 2.4f,
        "scout_plane" => 1.85f,
        "fighter" => 1.75f,
        "bomber" => 1.05f,
        "patrol_boat" => 2.6f,
        "submarine" => 1.4f,
        "destroyer_ship" => 1.2f,
        "battleship" or "transport_ship" or "aircraft_carrier" => 0.75f,
        _ => 3.4f
    };

    public static bool IsArtilleryLike(string key)
        => key is "artillery" or "infantry_artillery" or "mlrs_rocket" or "bomber" or "destroyer_ship" or "battleship" or "transport_ship";

    public static ArmorType GetArmorType(string key) => key switch
    {
        "infantry" or "infantry_artillery" => ArmorType.Unarmored,
        "infantry_flamethrower" or "flamethrower" => ArmorType.Unarmored,
        "light_tank" or "anti_air_gun" or "scout_plane" or "scout_helicopter" or "patrol_boat" or "mlrs_rocket" or "sam_missile" => ArmorType.Light,
        "tank" or "medium_tank" or "tank_destroyer" or "artillery" or "fighter" or "destroyer_ship" => ArmorType.Medium,
        "heavy_tank" or "bomber" or "submarine" or "transport_ship" => ArmorType.Heavy,
        "battleship" or "aircraft_carrier" or "nuke_silo" or "nuclear_silo" or "main_base" or "bunker" or "turret" or "defense_turret" => ArmorType.Fortified,
        _ => ArmorType.Medium
    };

    public static float GetArmorRating(string key) => key switch
    {
        "infantry" => 6f,
        "infantry_artillery" => 5f,
        "infantry_flamethrower" or "flamethrower" => 12f,
        "scout_plane" => 10f,
        "scout_helicopter" => 18f,
        "fighter" => 20f,
        "patrol_boat" => 24f,
        "mlrs_rocket" => 24f,
        "sam_missile" => 25f,
        "anti_air_gun" => 26f,
        "light_tank" => 28f,
        "artillery" => 22f,
        "bomber" => 38f,
        "transport_ship" => 45f,
        "submarine" => 48f,
        "tank_destroyer" => 52f,
        "tank" or "medium_tank" => 58f,
        "destroyer_ship" => 62f,
        "heavy_tank" => 95f,
        "aircraft_carrier" => 100f,
        "battleship" => 115f,
        // 建筑与防御塔
        "defense_turret" or "turret" => 50f,
        "bunker" or "barracks" or "factory" or "airfield" or "shipyard" => 65f,
        "main_base" or "nuke_silo" or "nuclear_silo" => 85f,
        _ => 30f
    };

    public static float GetArmorPenetration(string key) => key switch
    {
        "infantry" => 8f,
        "scout_plane" => 12f,
        "infantry_flamethrower" or "flamethrower" => 16f,
        "patrol_boat" => 32f,
        "infantry_artillery" => 36f,
        "light_tank" => 40f,
        "scout_helicopter" => 45f,
        "anti_air_gun" => 46f,
        "fighter" => 55f,
        "tank" or "medium_tank" => 65f,
        "destroyer_ship" => 72f,
        "mlrs_rocket" => 75f,
        "artillery" => 80f,
        "sam_missile" => 85f,
        "submarine" => 85f,
        "bomber" => 92f,
        "heavy_tank" => 96f,
        "tank_destroyer" => 98f,
        "battleship" => 110f,
        "defense_turret" or "turret" => 55f,
        _ => 40f
    };

    public static float GetCritChance(string key) => key switch
    {
        "submarine" => 0.20f,
        "fighter" => 0.18f,
        "tank_destroyer" => 0.16f,
        "sam_missile" => 0.15f,
        "scout_plane" => 0.15f,
        "anti_air_gun" => 0.14f,
        "heavy_tank" => 0.12f,
        "battleship" => 0.12f,
        "light_tank" => 0.12f,
        "scout_helicopter" => 0.12f,
        "tank" or "medium_tank" => 0.10f,
        "artillery" or "infantry_artillery" or "mlrs_rocket" => 0.10f,
        "infantry" => 0.08f,
        "infantry_flamethrower" or "flamethrower" => 0.05f,
        _ => 0.08f
    };

    /// <summary>
    /// 军团战术分层梯次（0: 前排重装锋线，1: 中排伴随与掩护，2: 后排火炮与重型支援）
    /// </summary>
    public static int GetFormationTier(string key) => key switch
    {
        "heavy_tank" or "tank" or "medium_tank" or "battleship" or "destroyer_ship" => 0,
        "light_tank" or "tank_destroyer" or "anti_air_gun" or "scout_helicopter" or "infantry" or "infantry_flamethrower" or "flamethrower" or "patrol_boat" or "submarine" => 1,
        "artillery" or "mlrs_rocket" or "sam_missile" or "infantry_artillery" or "transport_ship" or "aircraft_carrier" => 2,
        _ => 1
    };

    public static float GetDamageMultiplier(string attackerKey, string targetKey)
    {
        bool attackerIsAir = IsAirUnit(attackerKey);
        bool attackerIsNaval = IsNavalUnit(attackerKey);
        bool attackerIsLand = !attackerIsAir && !attackerIsNaval;

        bool targetIsAir = IsAirUnit(targetKey);
        bool targetIsNaval = IsNavalUnit(targetKey);
        bool targetIsLand = !targetIsAir && !targetIsNaval;

        // 航母：近防密集阵（CIWS）自卫拦截与远程舰载机联队空袭
        if (IsCarrier(attackerKey))
        {
            if (targetIsAir) return 1.5f; // CIWS 密集阵对空拦截与护航
            if (targetIsNaval) return 1.4f; // 航母舰载机对海反舰打击
            if (targetIsLand) return 1.4f; // 航母舰载机对陆对地空袭
            return 1.0f;
        }

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
            else if (attackerKey == "scout_helicopter")
            {
                if (targetIsLand) return 1.4f; // 武装直升机火箭巢与对地航炮压制地面坦克
                if (targetIsNaval) return 1.1f;
                if (targetIsAir) return 0.2f; // 直升机对高空喷气战机效能极低
                return 1.0f;
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
            // 防空与对空判定：
            // - 高空战机（战斗机 fighter、战略轰炸机 bomber）：地面的常规坦克、自行火炮、步兵绝不可攻击，防空炮车（anti_air_gun）和野战防空导弹车（sam_missile）具备高空拦截防空能力
            // - 低空目标（scout_plane, scout_helicopter）：防空炮、防空导弹、主战坦克（高平机枪）、轻型战车及步兵均可对其进行拦截射击！
            if (targetIsAir)
            {
                if (targetKey is "scout_plane" or "scout_helicopter")
                {
                    if (attackerKey == "sam_missile") return 2.5f; // 防空导弹精准拦截
                    if (attackerKey == "anti_air_gun") return 2.5f; // 防空高炮极强克制
                    if (attackerKey == "light_tank") return 1.2f; // 轻型战车速射高平两用机炮 (+20%)
                    if (attackerKey is "tank" or "medium_tank" or "heavy_tank") return 1.0f; // 车载高平两用高射机枪/主炮
                    if (attackerKey is "infantry" or "rifleman") return targetKey == "scout_plane" ? 0.8f : 0.6f; // 步兵集火对空射击
                    return 0f; // 喷火兵、自行火炮、火箭炮、歼击车等无法对空打击
                }
                else // fighter, bomber (高空超音速喷气战机 / 战略轰炸机)
                {
                    if (attackerKey == "sam_missile") return 2.8f; // 防空导弹车远程超视距绝杀高空战机
                    if (attackerKey == "anti_air_gun") return 2.5f; // 防空炮车高空拦截网
                    return 0f; // 重型坦克、中型坦克、轻型坦克、自行火炮、步兵等绝对无法对高空战机射击
                }
            }

            if (attackerKey == "sam_missile")
            {
                return 0f; // 专用防空导弹发射车，无法平射攻击地面与水面目标
            }
            else if (attackerKey == "anti_air_gun")
            {
                if (targetIsLand) return 0.6f; // AA Gun is weak vs Land (-40%)
                if (targetIsNaval) return 0.5f;
            }
            else if (attackerKey == "tank_destroyer")
            {
                if (targetKey is "light_tank" or "tank" or "medium_tank" or "heavy_tank") return 1.8f; // 重穿甲专克装甲战车
                if (IsInfantryLike(targetKey)) return 0.6f; // 穿甲弹打步兵过穿
                if (targetIsNaval) return 1.2f;
            }
            else if (attackerKey == "mlrs_rocket")
            {
                if (targetIsLand) return 1.5f; // 多管火箭炮弹幕面杀伤
                if (targetIsNaval) return 1.2f;
            }
            else if (attackerKey is "infantry_flamethrower" or "flamethrower")
            {
                if (IsInfantryLike(targetKey)) return 2.0f; // 喷火兵克制步兵软目标 (+100%)
                if (targetKey is "light_tank" or "tank" or "medium_tank" or "heavy_tank" or "artillery" or "anti_air_gun" or "tank_destroyer" or "mlrs_rocket" or "sam_missile")
                    return 0.35f; // 喷火兵对钢甲低效 (-65%)
                if (targetIsNaval) return 0.2f;
            }
            else if (attackerKey is "infantry" or "rifleman")
            {
                if (targetIsNaval) return 0.4f; // 步兵对舰艇低效
                if (IsInfantryLike(targetKey)) return 1.0f;
                return 0.75f;
            }
            else // Tanks: light_tank, tank, heavy_tank, artillery
            {
                if (targetIsNaval)
                {
                    if (attackerKey is "artillery" or "mlrs_rocket") return 1.8f; // 火炮岸轰舰艇 (+80%)
                    if (attackerKey is "heavy_tank" or "tank_destroyer") return 1.2f;
                    return 0.5f; // 其他轻型坦克对舰艇低效
                }
            }
        }

        // 3. Sea vs Air/Land/Sea
        if (attackerIsNaval)
        {
            if (attackerKey == "submarine")
            {
                if (targetIsAir) return 0f; // 潜艇鱼雷无法攻击空中目标
                if (targetIsNaval) return 1.8f; // 潜艇反舰鱼雷克制水面战舰 (+80%)
                if (targetIsLand) return 0.5f; // 潜艇对陆低效
            }
            else if (attackerKey == "transport_ship")
            {
                if (targetIsAir) return 0f; // 运输舰无对空武装
                if (targetIsNaval) return 0.8f;
                if (targetIsLand) return 0.8f;
            }
            else if (attackerKey == "patrol_boat")
            {
                if (targetIsAir) return 1.5f; // 巡逻艇近程防空机枪 (+50%)
                if (targetIsLand) return 0.7f;
                if (targetIsNaval) return 1.0f;
            }
            else if (attackerKey == "destroyer_ship")
            {
                if (targetIsAir) return 1.2f; // 驱逐舰近防系统与对空导弹
                if (targetIsNaval) return 1.8f;
                if (targetIsLand) return 1.5f;
            }
            else if (attackerKey == "battleship")
            {
                if (targetIsAir) return 0.9f; // 战列舰防空高炮网
                if (targetIsNaval) return 1.6f;
                if (targetIsLand) return 1.8f; // 战列舰巨炮岸轰
            }
        }

        return 1.0f;
    }

    public static float GetDamageMultiplierToBuilding(string attackerKey)
    {
        // Bomber counters buildings
        if (attackerKey == "bomber") return 1.8f;
        // Carrier counters buildings via carrier strike bombers
        if (attackerKey == "aircraft_carrier") return 1.5f;
        // Fighter is weak vs buildings
        if (attackerKey == "fighter") return 0.4f;
        // Artillery, Rocket and Destroyer counters buildings
        if (attackerKey is "artillery" or "mlrs_rocket" or "destroyer_ship") return 1.6f;
        // Anti-air and SAM are weak vs buildings
        if (attackerKey is "anti_air_gun" or "sam_missile") return 0.2f;
        // Flamethrower is strong vs buildings
        if (attackerKey is "infantry_flamethrower" or "flamethrower") return 1.4f;
        // Tank destroyer has heavy single target punch
        if (attackerKey == "tank_destroyer") return 0.8f;
        // Infantry is slightly weaker vs buildings
        if (attackerKey == "infantry") return 0.8f;
        
        return 1.0f;
    }

    public static bool CanAttackTargetType(string attackerKey, string targetUnitKey)
    {
        return GetDamageMultiplier(attackerKey, targetUnitKey) > 0f;
    }

    public static bool SupportsArtilleryDeploy(string key)
        => key is "artillery" or "infantry_artillery" or "mlrs_rocket";

    public static float DeployedRangeMultiplier(string key)
        => key switch
        {
            "artillery" => 1.85f,
            "infantry_artillery" => 1.50f,
            "mlrs_rocket" => 1.55f,
            _ => 1.0f
        };

    public static float MinAttackRange(string key)
        => key switch
        {
            "artillery" => 8.5f,
            "mlrs_rocket" => 8.0f,
            "infantry_artillery" => 5.0f,
            _ => 0f
        };

    public static float DeployedDamageMultiplier(string key)
        => key switch
        {
            "artillery" => 1.35f,
            "infantry_artillery" => 1.25f,
            "mlrs_rocket" => 1.25f,
            _ => 1.0f
        };


    public static float SplashRadius(string key)
        => key switch
        {
            "artillery" => 4.5f,           // 自行加农榴弹炮（曲射高爆榴弹，真实杀伤半径）
            "infantry_artillery" => 2.2f,  // 单兵迫击炮（小型曲射破片）
            "mlrs_rocket" => 5.0f,         // 多管火箭炮（覆盖式面杀伤火箭弹幕）
            "bomber" => 6.0f,              // 战略轰炸机（航空重磅航弹）
            // 步兵（步枪兵/喷火兵）、所有直射坦克（轻/中/重型坦克、歼击车）、防空炮、直升机均为纯单体直射伤害（SplashRadius = 0）
            _ => 0f
        };

    public static float SplashFalloff(string key)
        => key switch
        {
            "artillery" or "mlrs_rocket" or "bomber" => 0.45f,
            "infantry_artillery" => 0.50f,
            _ => 1.0f
        };

    public static float SpawnHeight(string key) => key switch
    {
        "scout_helicopter" => 8.5f,  // 武装直升机：低空树梢近地悬停与对地火力压制 (Nap-of-the-earth)
        "scout_plane" => 13.5f,      // 战术侦察机：中空机动侦察走廊
        "fighter" => 18.5f,          // 空优战斗机：中高空高速拦截与超音速机动 (与低空直升机形成超 10.0 米显著空域阶梯差)
        "bomber" => 24.0f,           // 战略轰炸机：高空战役地毯式空袭走廊
        "submarine" => -0.15f,       // 潜艇半潜吃水水线
        _ => IsNavalUnit(key) ? 0.0f : 0f
    };

    public static float CollisionRadius(string key) => key switch
    {
        "infantry" => 0.45f,
        "infantry_artillery" => 0.50f,
        "infantry_flamethrower" or "flamethrower" => 0.50f,
        "light_tank" => 1.25f,
        "tank" or "medium_tank" => 1.55f,
        "tank_destroyer" => 1.50f,
        "mlrs_rocket" => 1.45f,
        "sam_missile" => 1.40f,
        "heavy_tank" => 2.05f,
        "artillery" => 1.55f,
        "anti_air_gun" => 1.35f,
        "scout_plane" or "scout_helicopter" or "fighter" or "bomber" => 1.80f,
        "patrol_boat" => 2.2f,
        "submarine" => 3.0f,
        "destroyer_ship" => 4.5f,
        "battleship" => 6.5f,
        "aircraft_carrier" => 8.5f,
        _ => 1.20f
    };

    public static float UnitMass(string key) => key switch
    {
        "infantry" or "infantry_artillery" or "infantry_flamethrower" or "flamethrower" => 1.0f,
        "scout_helicopter" => 2.0f,
        "light_tank" or "anti_air_gun" => 3.5f,
        "sam_missile" => 4.0f,
        "mlrs_rocket" => 4.5f,
        "tank_destroyer" => 5.5f,
        "tank" or "medium_tank" or "artillery" => 6.0f,
        "heavy_tank" => 12.0f,
        "patrol_boat" => 5.0f,
        "submarine" => 10.0f,
        "destroyer_ship" => 20.0f,
        "battleship" => 35.0f,
        "aircraft_carrier" => 50.0f,
        _ => 3.0f
    };

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

