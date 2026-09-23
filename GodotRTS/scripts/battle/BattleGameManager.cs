using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleGameManager : Node
{
    public readonly struct BattleReportRow
    {
        public BattleReportRow(string label, string playerValue, string enemyValue)
        {
            Label = label;
            PlayerValue = playerValue;
            EnemyValue = enemyValue;
        }

        public string Label { get; }
        public string PlayerValue { get; }
        public string EnemyValue { get; }
    }

    public sealed class BattleReportData
    {
        public string TeamLine { get; init; } = "";
        public string PlayerHeader { get; init; } = "我方";
        public string EnemyHeader { get; init; } = "敌方";
        public string DurationText { get; init; } = "00:00";
        public BattleReportRow[] Rows { get; init; } = Array.Empty<BattleReportRow>();
    }

    const string MilitaryFbxRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/Models/FBX format/";
    const string SurvivalFbxRoot = "res://assets/unity_migrated/Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format/";
    const string SpaceKitRoot = "res://assets/unity_migrated/Assets/External/Kenney/SpaceKit/Models/FBX format/";
    const string TowerDefenseFbxRoot = "res://assets/unity_migrated/Assets/External/Kenney/TowerDefenseKit/Models/FBX format/";
    const string FactoryKitRoot = "res://assets/unity_migrated/Assets/External/Downloads/factory-kit/Models/FBX format/";
    const string RetroUrbanRoot = "res://assets/unity_migrated/Assets/External/Kenney/RetroUrbanKit/Models/FBX format/";
    const string CityIndustrialRoot = "res://assets/unity_migrated/Assets/External/Downloads/city-industrial/Models/FBX format/";

    [Signal]
    public delegate void EconomyChangedEventHandler();

    [Signal]
    public delegate void GameEndedEventHandler(bool playerWon, string reason);

    [Export] public int InitialGold { get; set; } = 1000;
    [Export] public int InitialPopCap { get; set; } = 20;
    [Export] public NodePath UnitsPath { get; set; } = new("");
    [Export] public NodePath BuildingsPath { get; set; } = new("");
    [Export] public PackedScene? TankScene { get; set; }
    [Export] public PackedScene? LightTankScene { get; set; }
    [Export] public PackedScene? HeavyTankScene { get; set; }
    [Export] public PackedScene? ArtilleryScene { get; set; }
    [Export] public PackedScene? ScoutPlaneScene { get; set; }
    [Export] public PackedScene? InfantryScene { get; set; }
    [Export] public PackedScene? FighterScene { get; set; }
    [Export] public PackedScene? BomberScene { get; set; }
    [Export] public PackedScene? AircraftCarrierScene { get; set; }
    [Export] public float UnitVisionRadius { get; set; } = 32f;
    [Export] public float BuildingVisionRadius { get; set; } = 46f;
    [Export] public float MainBaseVisionRadius { get; set; } = 70f;
    [Export] public int MainBaseRebuildCost { get; set; } = 600;
    [Export] public float MainBaseRebuildDuration { get; set; } = 30f;

    public static bool DisableFogOfWar { get; set; } = false;
    public static bool DisableWaterCheck { get; set; } = false;
    public static bool DisableFuelDepletion { get; set; } = false;
    public static BattleGameManager? Instance { get; private set; }

    public int PlayerGold { get; private set; }
    public void AddPlayerGold(int amount)
    {
        PlayerGold += amount;
        EmitSignal(SignalName.EconomyChanged);
    }
    public int EnemyGold { get; private set; }
    public int PlayerPopUsed { get; private set; }
    public int EnemyPopUsed { get; private set; }
    public int PlayerPopCap { get; private set; }
    public int EnemyPopCap { get; private set; }
    public int PlayerPowerProvided { get; private set; }
    public int PlayerPowerUsed { get; private set; }
    public int EnemyPowerProvided { get; private set; }
    public int EnemyPowerUsed { get; private set; }
    public int PlayerMainBaseLevel => GetMainBaseLevel(true);
    public int EnemyMainBaseLevel => GetMainBaseLevel(false);
    public bool PlayerPowerOnline => PlayerPowerUsed <= PlayerPowerProvided;
    public bool EnemyPowerOnline => EnemyPowerUsed <= EnemyPowerProvided;
    public float GameTime { get; private set; }
    public bool GameOver { get; private set; }
    public bool PlayerWon { get; private set; }
    public string GameOverReason { get; private set; } = "";
    public string PendingBuildKey { get; private set; } = "";
    public IReadOnlyCollection<string> ResearchedTechs => researchedTechs;
    public int PlayerUnitKills { get; private set; }
    public int PlayerBuildingKills { get; private set; }
    public int PlayerUnitsLost { get; private set; }
    public int PlayerBuildingsLost { get; private set; }
    public int PlayerUnitsProduced { get; private set; }
    public int EnemyUnitsProduced { get; private set; }
    public int PlayerBuildingsConstructed { get; private set; }
    public int EnemyBuildingsConstructed { get; private set; }
    public int PlayerDamageDealt { get; private set; }
    public int PlayerDamageTaken { get; private set; }
    public int EnemyDamageDealt { get; private set; }
    public int EnemyDamageTaken { get; private set; }
    public int PlayerGoldIncome { get; private set; }
    public int EnemyGoldIncome { get; private set; }
    public int PlayerGoldSpent { get; private set; }
    public int EnemyGoldSpent { get; private set; }
    public int PlayerPeakPopUsed { get; private set; }
    public int EnemyPeakPopUsed { get; private set; }

    int nextNetId = 1000;
    Node3D? unitsRoot;
    Node3D? buildingsRoot;
    bool basesReady;
    bool playerBaseSeen;
    bool enemyBaseSeen;
    float fogRefreshTimer;
    BattleMapDefinition currentMap = BattleMapCatalog.Get(BattleMapCatalog.DefaultMapName);
    readonly HashSet<string> researchedTechs = new();
    readonly Dictionary<string, float> playerBattleTechCooldownEnds = new();
    readonly Dictionary<string, float> enemyBattleTechCooldownEnds = new();
    readonly Dictionary<int, Vector3> aircraftParkingSlots = new();
    int lastPlayerBaseLevel = 1;
    int lastPlayerBaseHp;
    AudioStreamPlayer? bgmPlayer;
    int lastPlayerBaseMaxHp;
    int lastEnemyBaseLevel = 1;
    int lastEnemyBaseHp;
    int lastEnemyBaseMaxHp;

    public override void _Ready()
    {
        Instance = this;
        currentMap = BattleMapCatalog.Get(BattleMapCatalog.RequestedMapName(GameState.Instance?.SelectedMapName));
        PlayerGold = InitialGold;
        EnemyGold = InitialGold;
        PlayerPopCap = InitialPopCap;
        EnemyPopCap = InitialPopCap;
        unitsRoot = GetNodeOrNull<Node3D>(UnitsPath);
        buildingsRoot = GetNodeOrNull<Node3D>(BuildingsPath);
        ApplyGlobalConquestStarterIfNeeded();
        // 将原本的异步等待改为同步加载，使重型 3D 资产（FBX模型）在加载界面背后装载完毕，避免进入战场画面后的瞬间发生二次卡顿
        RegisterExistingCombatants();
        EnsureInitialMainBasesExist();

        // 战局开启时启动紧张氛围背景音乐
        StartTenseBattleBgm();
    }

    public override void _Process(double delta)
    {
        if (GameOver)
            return;

        GameTime += (float)delta;
        RecalculatePopulation();
        RecalculatePower();
        PlayerPeakPopUsed = Math.Max(PlayerPeakPopUsed, PlayerPopUsed);
        EnemyPeakPopUsed = Math.Max(EnemyPeakPopUsed, EnemyPopUsed);
        RefreshFogOfWar((float)delta);
        CheckWinLose();
    }

    public void RegisterExistingCombatants()
    {
        var navalIndex = 0;
        foreach (var unit in GetUnits())
        {
            unit.NetId = unit.NetId == 0 ? nextNetId++ : unit.NetId;
            unit.ApplyCatalogCombatProfile();
            ConfigureExistingUnitVisual(unit);
            unit.Died -= OnUnitDied;
            unit.Died += OnUnitDied;

            if (BattleUnitCatalog.IsNavalUnit(unit.UnitKey))
            {
                if (currentMap.Waters.Length > 0 && !IsWaterPoint(unit.GlobalPosition, 0f))
                {
                    var offset = new Vector3(navalIndex * 14f - 14f, 0f, (navalIndex % 2 == 0 ? 3f : -3f));
                    var waterPos = BattleMapCatalog.ClosestWaterPoint(currentMap, unit.GlobalPosition + offset, 2f);
                    unit.GlobalPosition = new Vector3(waterPos.X, BattleUnitCatalog.SpawnHeight(unit.UnitKey), waterPos.Z);
                    navalIndex++;
                }
                else
                {
                    unit.GlobalPosition = new Vector3(unit.GlobalPosition.X, BattleUnitCatalog.SpawnHeight(unit.UnitKey), unit.GlobalPosition.Z);
                }
            }
            else
            {
                unit.GlobalPosition = NormalizeUnitTarget(unit, unit.GlobalPosition);
            }
        }

        foreach (var building in GetBuildings())
        {
            var key = string.IsNullOrWhiteSpace(building.BuildKey)
                ? building.IsMainBase ? "main_base" : "barracks"
                : building.BuildKey;
            building.Configure(BattleBuildingCatalog.Get(key), building.PlayerOwned);
            ConfigureMainBaseLifecycle(building);
            EnsureBuildingVisual(building, BattleBuildingCatalog.Get(key));
            building.NetId = building.NetId == 0 ? nextNetId++ : building.NetId;
            building.Died -= OnBuildingDied;
            building.Died += OnBuildingDied;
            building.MainBaseStateChanged -= OnMainBaseStateChanged;
            building.MainBaseStateChanged += OnMainBaseStateChanged;
            building.ProductionFinished -= OnProductionFinished;
            building.ProductionFinished += OnProductionFinished;
            CacheBaseSnapshot(building);
        }

        RecalculatePopulation();
        RecalculatePower();
        ForceRefreshFogOfWar();
        UpdateBaseSeenFlags();
        EmitSignal(SignalName.EconomyChanged);
        // 初始化登记完毕后，立刻让镜头定位对准玩家兵力集结处（无兵力时对准主基地），首帧与延迟帧均执行确保准确定位
        FocusCameraOnPlayerForces();
        Callable.From(FocusCameraOnPlayerForces).CallDeferred();
    }

    /// <summary>
    /// 定位到玩家的兵力集结处（如果有单位），否则定位到初始主基地，并将 RtsCamera 镜头平移聚焦到该位置。
    /// </summary>
    public void FocusCameraOnPlayerForces()
    {
        var cameraRig = GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig")
            ?? GetTree().Root.FindChild("CameraRig", true, false) as RtsCamera;
        if (cameraRig is null)
            return;

        var playerMainBase = GetBuildings().FirstOrDefault(b => b.PlayerOwned && b.IsMainBase && !b.IsRuined);

        // 1. 优先检查是否有选中的我方作战单位
        var selectedUnits = GameState.Instance?.Selected?
            .OfType<RtsUnit>()
            .Where(u => GodotObject.IsInstanceValid(u) && u.PlayerOwned && !u.IsDead)
            .ToList();
        if (selectedUnits is { Count: > 0 })
        {
            var center = Vector3.Zero;
            foreach (var u in selectedUnits)
                center += u.GlobalPosition;
            center /= selectedUnits.Count;
            cameraRig.JumpTo(center);
            return;
        }

        // 2. 其次获取所有存活的我方作战部队
        var playerUnits = GetUnits(true)
            .Where(u => GodotObject.IsInstanceValid(u) && !u.IsDead)
            .ToList();

        if (playerUnits.Count > 0)
        {
            // 优先以地面主力部队（坦克/步兵/火炮等主力装甲）为核心
            var groundForces = playerUnits
                .Where(u => !BattleUnitCatalog.IsNavalUnit(u.UnitKey) && !BattleUnitCatalog.IsAirUnit(u.UnitKey))
                .ToList();
            var targetList = groundForces.Count > 0 ? groundForces : playerUnits;

            // 寻找最靠近主基地或拥有最高战术权重的核心主力（例如坦克）
            RtsUnit? focalUnit = null;

            if (playerMainBase is not null)
            {
                // 选择距离主基地最合理的先锋/装甲单位，避免将远距离孤立单位混在一起计算平均值导致落入虚无区域
                focalUnit = targetList
                    .OrderBy(u => u.UnitKey.Contains("tank") ? 0 : 1)
                    .ThenBy(u => u.GlobalPosition.DistanceTo(playerMainBase.GlobalPosition))
                    .FirstOrDefault();
            }
            else
            {
                focalUnit = targetList.OrderBy(u => u.UnitKey.Contains("tank") ? 0 : 1).FirstOrDefault();
            }

            if (focalUnit is not null)
            {
                // 以核心主力为基准，聚合其周围 45 米内的近距离友军取精确重心，保证同屏全景显示兵力
                var nearbySquad = targetList
                    .Where(u => u.GlobalPosition.DistanceTo(focalUnit.GlobalPosition) <= 45f)
                    .ToList();

                var squadCenter = Vector3.Zero;
                foreach (var u in nearbySquad)
                    squadCenter += u.GlobalPosition;
                squadCenter /= nearbySquad.Count;

                cameraRig.JumpTo(squadCenter);
                return;
            }
        }

        // 3. 若无兵力，退回聚焦到主基地
        if (playerMainBase is not null)
        {
            cameraRig.JumpTo(playerMainBase.GlobalPosition);
            return;
        }

        var anyPlayerBuilding = GetBuildings().FirstOrDefault(b => b.PlayerOwned && !b.IsRuined);
        if (anyPlayerBuilding is not null)
        {
            cameraRig.JumpTo(anyPlayerBuilding.GlobalPosition);
        }
    }

    /// <summary>
    /// 兼容旧调用接口：定位到玩家兵力或主基地
    /// </summary>
    public void FocusCameraOnMainBase()
    {
        FocusCameraOnPlayerForces();
    }

    void EnsureInitialMainBasesExist()
    {
        var playerBase = GetBuildings().FirstOrDefault(b => b.PlayerOwned && b.IsMainBase);
        if (playerBase is null)
        {
            var playerPos = FindMapSpawnPos("PlayerBase", new Vector3(-currentMap.BaseSpawnOffset, 0f, -currentMap.BaseSpawnOffset));
            SpawnBuilding(BattleBuildingCatalog.Get("main_base"), playerPos, true);
        }

        var enemyBase = GetBuildings().FirstOrDefault(b => !b.PlayerOwned && b.IsMainBase);
        if (enemyBase is null)
        {
            var enemyPos = FindMapSpawnPos("EnemyBase", new Vector3(currentMap.BaseSpawnOffset, 0f, currentMap.BaseSpawnOffset));
            SpawnBuilding(BattleBuildingCatalog.Get("main_base"), enemyPos, false);
        }
    }

    Vector3 FindMapSpawnPos(string name, Vector3 fallback)
    {
        foreach (var spawn in currentMap.SpawnPoints)
        {
            if (spawn.Name == name)
                return spawn.Position;
        }
        return fallback;
    }

    public void EnsureNextNetIdAbove(int netId)
    {
        if (netId >= nextNetId)
            nextNetId = netId + 1;
    }

    public IReadOnlyList<RtsUnit> GetUnits()
        => GetTree().GetNodesInGroup("rts_units").OfType<RtsUnit>().Where(GodotObject.IsInstanceValid).ToList();

    public IReadOnlyList<RtsBuilding> GetBuildings()
        => GetTree().GetNodesInGroup("rts_buildings").OfType<RtsBuilding>().Where(GodotObject.IsInstanceValid).ToList();

    public IReadOnlyList<RtsUnit> GetUnits(bool playerOwned)
        => GetUnits().Where(u => u.PlayerOwned == playerOwned && !u.IsDead).ToList();

    public IReadOnlyList<RtsBuilding> GetBuildings(bool playerOwned)
        => GetBuildings().Where(b => b.PlayerOwned == playerOwned && b.Health > 0f).ToList();

    public IReadOnlyList<RtsBuilding> GetAllBuildings(bool playerOwned)
        => GetBuildings().Where(b => b.PlayerOwned == playerOwned).ToList();

    public IReadOnlyList<RtsBuilding> GetMainBases(bool playerOwned, bool includeRuined = false)
    {
        var source = includeRuined ? GetAllBuildings(playerOwned) : GetBuildings(playerOwned);
        return source.Where(b => b.IsMainBase).ToList();
    }

    public RtsBuilding? FindOperationalMainBase(bool playerOwned)
        => GetMainBases(playerOwned)
            .Where(b => !b.IsRuined && !b.IsRebuilding && b.Health > 0f)
            .OrderByDescending(b => b.Health)
            .FirstOrDefault();

    public bool IsVisibleToPlayer(Node3D node)
        => node switch
        {
            RtsUnit unit => unit.PlayerOwned || unit.FogRevealed,
            RtsBuilding building => building.PlayerOwned || building.FogRevealed,
            _ => true
        };

    public Vector3 NormalizeUnitTarget(RtsUnit unit, Vector3 target)
        => NormalizeUnitTarget(unit.UnitKey, target);

    public Vector3 NormalizeUnitTarget(string unitKey, Vector3 target)
    {
        var clamped = BattleMapCatalog.ClampToMap(target, 4f);
        if (BattleUnitCatalog.IsAirUnit(unitKey))
            return new Vector3(clamped.X, BattleUnitCatalog.SpawnHeight(unitKey), clamped.Z);

        if (BattleUnitCatalog.IsNavalUnit(unitKey))
        {
            if (IsWaterPoint(clamped, 0f))
                return new Vector3(clamped.X, BattleUnitCatalog.SpawnHeight(unitKey), clamped.Z);

            var waterTarget = BattleMapCatalog.ClosestWaterPoint(currentMap, clamped, 0.5f);
            return new Vector3(waterTarget.X, BattleUnitCatalog.SpawnHeight(unitKey), waterTarget.Z);
        }

        if (!IsWaterPoint(clamped, 0f))
            return new Vector3(clamped.X, 0f, clamped.Z);

        var landTarget = BattleMapCatalog.ClosestLandPoint(currentMap, clamped, 0.5f);
        return new Vector3(landTarget.X, 0f, landTarget.Z);
    }

    Vector3? FindMapSpawn(string name)
    {
        foreach (var spawn in currentMap.SpawnPoints)
        {
            if (spawn.Name == name)
                return spawn.Position;
        }
        return null;
    }

    public bool IsWaterPoint(Vector3 position, float padding = 0f)
        => !DisableWaterCheck && BattleMapCatalog.IsPointInWater(currentMap, position, padding);

    public Vector3 ClosestLandPoint(Vector3 position, float padding = 1.5f)
        => BattleMapCatalog.ClosestLandPoint(currentMap, position, padding);

    public Vector3 ClosestWaterPoint(Vector3 position, float inset = 2.5f)
        => BattleMapCatalog.ClosestWaterPoint(currentMap, position, inset);

    public float DistanceToWater(Vector3 position)
        => BattleMapCatalog.DistanceToWater(currentMap, position);

    public int GetMainBaseLevel(bool playerOwned)
    {
        return GetMainBases(playerOwned)
            .Where(mainBase => GodotObject.IsInstanceValid(mainBase) && mainBase.Health > 0f && !mainBase.IsRebuilding)
            .Select(mainBase => Mathf.Max(1, mainBase.BuildingLevel))
            .DefaultIfEmpty(0)
            .Max();
    }

    public int CountBuildings(string buildKey, bool playerOwned, bool includeUnderConstruction = true)
    {
        return GetBuildings(playerOwned).Count(building =>
            building.BuildKey == buildKey
            && building.Health > 0f
            && (includeUnderConstruction || !building.UnderConstruction));
    }

    public int HighestBuildingLevel(string buildKey, bool playerOwned, bool includeUnderConstruction = true)
    {
        return GetBuildings(playerOwned)
            .Where(building =>
                building.BuildKey == buildKey
                && building.Health > 0f
                && (includeUnderConstruction || !building.UnderConstruction))
            .Select(building => Mathf.Max(1, building.BuildingLevel))
            .DefaultIfEmpty(0)
            .Max();
    }

    public int GetBuildingLimit(string buildKey, bool playerOwned)
    {
        var mainBaseLevel = Mathf.Max(1, GetMainBaseLevel(playerOwned));
        return buildKey switch
        {
            "barracks" => mainBaseLevel switch
            {
                1 => 1,
                2 => 2,
                3 => 3,
                4 or 5 => 4,
                6 or 7 => 5,
                _ => 6
            },
            "air_factory" or "airfield" or "tank_factory" or "armor_factory" or "naval_yard" => mainBaseLevel switch
            {
                1 => 0, // Level 1 main base cannot construct advanced factory buildings
                2 => 1,
                3 or 4 => 2,
                5 or 6 => 3,
                7 or 8 => 4,
                _ => 5
            },
            "turret" => mainBaseLevel switch
            {
                1 => 2,
                2 => 4,
                3 or 4 => 6,
                5 or 6 => 9,
                7 or 8 => 12,
                _ => 16
            },
            "gold_mine" or "power_plant" => mainBaseLevel switch
            {
                1 => 2,
                2 => 3,
                3 or 4 => 4,
                5 or 6 => 6,
                7 or 8 => 8,
                _ => 10
            },
            _ => int.MaxValue
        };
    }

    public BattleBuildingRequirementStatus[] GetUpgradeRequirementStatuses(string buildKey, int nextLevel, bool playerOwned)
    {
        var requirements = BattleBuildingUpgradeCatalog.GetRequirements(buildKey, nextLevel);
        return BattleBuildingUpgradeCatalog.EvaluateRequirements(
            requirements,
            key => CountBuildings(key, playerOwned, false),
            key => HighestBuildingLevel(key, playerOwned, false));
    }

    public bool TryGetBuildingUpgradePreview(RtsBuilding building, out int nextLevel, out int goldCost, out BattleBuildingRequirementStatus[] requirements, out string message)
    {
        nextLevel = 0;
        goldCost = 0;
        requirements = Array.Empty<BattleBuildingRequirementStatus>();
        message = "";

        if (!GodotObject.IsInstanceValid(building) || !building.PlayerOwned)
        {
            message = "请选择己方建筑";
            return false;
        }
        if (building.UnderConstruction)
        {
            message = $"{building.DisplayName} 仍在建造中";
            return false;
        }
        if (!BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel))
        {
            message = $"{building.DisplayName} 已满级";
            return false;
        }

        nextLevel = building.BuildingLevel + 1;
        var upgrade = BattleBuildingUpgradeCatalog.Get(building.BuildKey, nextLevel);
        goldCost = upgrade.GoldCost;
        requirements = GetUpgradeRequirementStatuses(building.BuildKey, nextLevel, building.PlayerOwned);
        message = BuildUpgradePreviewMessage(building, nextLevel, goldCost, requirements);
        return true;
    }

    public bool CanUpgradeBuilding(RtsBuilding building, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!TryGetBuildingUpgradePreview(building, out var nextLevel, out var goldCost, out var requirements, out message))
            return false;

        if (PlayerGold < goldCost)
        {
            message = $"金币不足：需要 {goldCost}";
            return false;
        }

        var missing = requirements.Where(item => !item.IsMet).ToArray();
        if (missing.Length > 0)
        {
            message = $"升级到 {nextLevel} 级条件不足：{FormatRequirementSummary(missing)}";
            return false;
        }

        return true;
    }

    public bool CanPlaceBuildingAt(string buildKey, Vector3 position, bool playerOwned, out string message)
        => CanPlaceBuildingWithUnityRules(position, BattleBuildingCatalog.Get(buildKey), playerOwned, out message);

    bool CanBeginBuildPlacementWithUnityRules(string buildKey, bool playerOwned, out string message)
    {
        message = "";
        var baseLevel = GetMainBaseLevel(playerOwned);
        if (baseLevel <= 0)
        {
            message = playerOwned
                ? "\u4e3b\u57fa\u5730\u5df2\u5931\u6548\uff0c\u65e0\u6cd5\u7ee7\u7eed\u5efa\u9020"
                : "\u57fa\u5730\u5df2\u5931\u6548";
            return false;
        }

        // 主基地等级限制解锁高阶功能性建筑
        if (buildKey is "tank_factory" or "airfield" or "naval_yard" && baseLevel < 2)
        {
            message = $"{BattleBuildingCatalog.Get(buildKey).DisplayName}需要主基地升级到 Lv.2 才能建造";
            return false;
        }
        if (buildKey is "armor_factory" or "air_factory" && baseLevel < 3)
        {
            message = $"{BattleBuildingCatalog.Get(buildKey).DisplayName}需要主基地升级到 Lv.3 才能建造";
            return false;
        }

        var limit = GetBuildingLimit(buildKey, playerOwned);
        if (limit == int.MaxValue)
            return true;

        var currentCount = CountBuildings(buildKey, playerOwned, true);
        if (currentCount < limit)
            return true;

        message = $"\u4e3b\u57fa\u5730 Lv.{baseLevel} \u6700\u591a\u53ef\u5efa {limit} \u4e2a{BattleBuildingCatalog.Get(buildKey).DisplayName}\uff08\u5f53\u524d {currentCount}/{limit}\uff09";
        return false;
    }

    bool CanPlaceBuildingWithUnityRules(Vector3 position, BattleBuildingDefinition def, bool playerOwned, out string message)
    {
        message = "";
        if (GetMainBaseLevel(playerOwned) <= 0)
        {
            message = playerOwned ? "\u4e3b\u57fa\u5730\u5df2\u5931\u6548\uff0c\u65e0\u6cd5\u5efa\u9020\u5efa\u7b51" : "\u57fa\u5730\u5df2\u5931\u6548";
            return false;
        }
        if (Mathf.Abs(position.X) > BattleMapCatalog.GetMapHalfSize(currentMap) - 18f || Mathf.Abs(position.Z) > BattleMapCatalog.GetMapHalfSize(currentMap) - 18f)
        {
            message = "建造位置超出地图边界";
            return false;
        }

        if (IsWaterPoint(position, 1f))
        {
            message = "\u9646\u5730\u5efa\u7b51\u4e0d\u80fd\u653e\u5728\u6cb3\u9053\u6216\u6c34\u9762\u4e0a";
            return false;
        }

        if (def.Key == "naval_yard" && DistanceToWater(position) > 18f)
        {
            message = "\u8239\u575e\u5fc5\u987b\u9760\u8fd1\u6c34\u57df";
            return false;
        }

        var footprintRadius = Mathf.Max(def.CollisionSize.X, def.CollisionSize.Z) * 0.5f;
        var mainBase = FindOperationalMainBase(playerOwned);
        if (mainBase is not null && position.DistanceTo(mainBase.GlobalPosition) > Mathf.Max(8f, 60f - footprintRadius))
        {
            message = "\u5efa\u7b51\u9700\u8981\u653e\u572c\u4e3b\u57fa\u5730\u5efa\u9020\u8303\u56f4\u5185";
            return false;
        }

        var w1 = def.CollisionSize.X;
        var h1 = def.CollisionSize.Z;
        const float minSpacing = 0.5f;
        foreach (var building in GetBuildings())
        {
            var otherDef = BattleBuildingCatalog.Get(building.BuildKey);
            var w2 = otherDef.CollisionSize.X;
            var h2 = otherDef.CollisionSize.Z;
            float dx = Mathf.Abs(position.X - building.GlobalPosition.X);
            float dz = Mathf.Abs(position.Z - building.GlobalPosition.Z);
            if (dx < (w1 + w2) * 0.5f + minSpacing && dz < (h1 + h2) * 0.5f + minSpacing)
            {
                message = "\u8fd9\u91cc\u79bb\u5df2\u6709\u5efa\u7b51\u592a\u8fd1";
                return false;
            }
        }

        return true;
    }

    public bool TryQueueProduction(RtsBuilding building, string unitKey, out string message)
    {
        message = "";
        if (GetMainBaseLevel(building.PlayerOwned) <= 0)
        {
            message = building.PlayerOwned ? "\u4e3b\u57fa\u5730\u5df2\u5931\u6548\uff0c\u65e0\u6cd5\u751f\u4ea7\u5355\u4f4d" : "\u4e3b\u57fa\u5730\u5df2\u5931\u6548";
            return false;
        }
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || !building.PlayerOwned)
        {
            message = "请选择己方生产建筑";
            return false;
        }
        if (!building.CanProduce(unitKey))
        {
            message = building.RequiresPower() && !building.Powered
                ? $"{building.DisplayName}电力不足，生产暂停"
                : $"{building.DisplayName}不能生产该单位";
            return false;
        }

        if (building.QueueCount >= 8)
        {
            message = "生产队列已满（最多 8 个）";
            return false;
        }

        var def = BattleUnitCatalog.Get(unitKey);
        if (PlayerGold < def.GoldCost)
        {
            message = $"金币不足：需要 {def.GoldCost}";
            return false;
        }
        if (BattleUnitCatalog.RequiresAirfieldSlot(def.Key))
        {
            var carrierSlots = GetUnits(true).Count(u => BattleUnitCatalog.IsCarrier(u.UnitKey) && !u.IsDead)
                * BattleUnitCatalog.CarrierSlotCount;
            var capacity = CountBuildings("airfield", true, false) * 4 + carrierSlots;
            var occupied = GetUnits(true).Count(unit => BattleUnitCatalog.RequiresAirfieldSlot(unit.UnitKey))
                + CountQueuedAircraft(true);
            if (capacity <= 0)
            {
                message = "需要先建造停机场或航母";
                return false;
            }
            if (occupied >= capacity)
            {
                message = $"机位不足：{occupied}/{capacity}（停机场或航母可提供机位）";
                return false;
            }
        }
        if (PlayerPopUsed + QueuedPop(true) + def.PopCost > PlayerPopCap)
        {
            message = $"人口不足：{PlayerPopUsed}/{PlayerPopCap}";
            return false;
        }

        PlayerGold -= def.GoldCost;
        RecordGoldSpent(true, def.GoldCost);
        building.EnqueueUnit(unitKey);
        EmitSignal(SignalName.EconomyChanged);
        message = $"生产中：{def.DisplayName}";
        return true;
    }

    public bool TryQueueProductionForFaction(RtsBuilding building, string unitKey, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || building.PlayerOwned != playerOwned)
        {
            message = "请选择对应阵营的生产建筑";
            return false;
        }
        if (!building.CanProduce(unitKey))
        {
            message = "该建筑不能生产这个单位";
            return false;
        }

        if (building.QueueCount >= 8)
        {
            message = "生产队列已满（最多 8 个）";
            return false;
        }

        var def = BattleUnitCatalog.Get(unitKey);
        var gold = playerOwned ? PlayerGold : EnemyGold;
        if (gold < def.GoldCost)
        {
            message = $"金币不足：需要 {def.GoldCost}";
            return false;
        }

        if (BattleUnitCatalog.RequiresAirfieldSlot(def.Key))
        {
            var carrierSlots = GetUnits(playerOwned).Count(u => BattleUnitCatalog.IsCarrier(u.UnitKey) && !u.IsDead)
                * BattleUnitCatalog.CarrierSlotCount;
            var capacity = CountBuildings("airfield", playerOwned, false) * 4 + carrierSlots;
            var occupied = GetUnits(playerOwned).Count(unit => BattleUnitCatalog.RequiresAirfieldSlot(unit.UnitKey))
                + CountQueuedAircraft(playerOwned);
            if (capacity <= 0 || occupied >= capacity)
            {
                message = $"机位不足：{occupied}/{capacity}";
                return false;
            }
        }

        var popUsed = playerOwned ? PlayerPopUsed : EnemyPopUsed;
        var popCap = playerOwned ? PlayerPopCap : EnemyPopCap;
        if (popUsed + QueuedPop(playerOwned) + def.PopCost > popCap)
        {
            message = $"人口不足：{popUsed}/{popCap}";
            return false;
        }

        if (playerOwned)
            PlayerGold -= def.GoldCost;
        else
            EnemyGold -= def.GoldCost;
        RecordGoldSpent(playerOwned, def.GoldCost);
        building.EnqueueUnit(unitKey);
        EmitSignal(SignalName.EconomyChanged);
        message = $"生产中：{def.DisplayName}";
        return true;
    }

    public bool IsTechResearched(string techKey)
        => false;

    public bool TryResearchTech(string techKey, out string message)
    {
        if (BattleTechCatalog.All.Length >= 0)
        {
            var activeTech = BattleTechCatalog.Get(techKey);
            message = $"{activeTech.DisplayName} 已改为战场主动科技，请选择地面位置释放。";
            return false;
        }

        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }

        var tech = BattleTechCatalog.Get(techKey);
        if (researchedTechs.Contains(tech.Key))
        {
            message = $"{tech.DisplayName} 已完成研究";
            return false;
        }
        if (PlayerGold < tech.GoldCost)
        {
            message = $"金币不足：需要 {tech.GoldCost}";
            return false;
        }
        if (!HasReadyPlayerBuilding(tech.RequiredBuildingKey))
        {
            message = $"需要先拥有 {BattleBuildingCatalog.Get(tech.RequiredBuildingKey).DisplayName}";
            return false;
        }

        PlayerGold -= tech.GoldCost;
        RecordGoldSpent(true, tech.GoldCost);
        researchedTechs.Add(tech.Key);
        ApplyTechToUnits(true);
        EmitSignal(SignalName.EconomyChanged);
        message = $"研究完成：{tech.DisplayName}";
        return true;
    }

    public bool CanCastBattleTech(string techKey, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }

        var tech = BattleTechCatalog.Get(techKey);
        var mainBase = FindOperationalMainBase(true);
        if (mainBase is null || !GodotObject.IsInstanceValid(mainBase) || mainBase.Health <= 0f)
        {
            message = "主基地已被摧毁，无法释放科技";
            return false;
        }

        if (!string.IsNullOrEmpty(PendingBuildKey))
        {
            message = "正在放置建筑，无法释放科技";
            return false;
        }

        var remain = GetBattleTechCooldownRemaining(tech.Key);
        if (remain > 0f)
        {
            message = $"{tech.DisplayName} 冷却中：{Mathf.CeilToInt(remain)}秒";
            return false;
        }

        if (!HasReadyPlayerBuilding(tech.RequiredBuildingKey))
        {
            message = $"需要先拥有 {BattleBuildingCatalog.Get(tech.RequiredBuildingKey).DisplayName}";
            return false;
        }

        return true;
    }

    public float GetBattleTechCooldownRemaining(string techKey)
    {
        if (!playerBattleTechCooldownEnds.TryGetValue(techKey, out var endTime))
            return 0f;

        return Mathf.Max(0f, endTime - GameTime);
    }

    public bool TryCastBattleTech(string techKey, Vector3 point, out string message)
        => TryCastBattleTech(techKey, point, out message, out _);

    public bool TryCastBattleTech(string techKey, Vector3 point, out string message, out int affected)
    {
        affected = 0;
        if (!CanCastBattleTech(techKey, out message))
            return false;

        var tech = BattleTechCatalog.Get(techKey);
        var center = new Vector2(point.X, point.Z);

        // 判断当前模式与科技等级：争霸模式使用玩家实际等级，快速匹配直接满级 5 级
        var level = 5;
        if (GameState.Instance is not null && GameState.Instance.LastBattleMode == "全球争霸")
        {
            level = GameState.Instance.GetTechLevel(tech.Key);
        }

        // 根据科研等级线性提升各项指标
        var finalMove = tech.MoveMultiplier > 1f ? tech.MoveMultiplier + (level - 1) * 0.04f : tech.MoveMultiplier;
        var finalDamage = tech.DamageMultiplier > 1f ? tech.DamageMultiplier + (level - 1) * 0.05f : tech.DamageMultiplier;
        var finalRange = tech.AttackRangeBonus > 0f ? tech.AttackRangeBonus + (level - 1) * 0.5f : tech.AttackRangeBonus;
        var finalCooldown = tech.AttackCooldownMultiplier < 1f ? tech.AttackCooldownMultiplier - (level - 1) * 0.04f : tech.AttackCooldownMultiplier;
        var finalDefense = tech.DefenseReduction > 0f ? tech.DefenseReduction + (level - 1) * 0.05f : tech.DefenseReduction;
        var finalVision = tech.VisionBonus > 0f ? tech.VisionBonus + (level - 1) * 3f : tech.VisionBonus;
        var finalRegen = tech.RegenPerSecond > 0f ? tech.RegenPerSecond + (level - 1) * 4f : tech.RegenPerSecond;

        foreach (var unit in GetUnits(true))
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead)
                continue;

            var unitPos = unit.GlobalPosition;
            if (new Vector2(unitPos.X, unitPos.Z).DistanceTo(center) > tech.Radius)
                continue;

            unit.ApplyTechBuff(
                tech.Key,
                tech.Duration,
                finalMove,
                finalDamage,
                finalRange,
                finalCooldown,
                finalDefense,
                finalVision,
                finalRegen,
                tech.Tint);
            affected++;
        }

        playerBattleTechCooldownEnds[tech.Key] = GameTime + tech.Cooldown;
        ForceRefreshFogOfWar();
        message = $"{tech.DisplayName} (Lv.{level}) 已释放，影响 {affected} 个单位";
        return true;
    }

    public float GetBattleTechCooldownRemainingForFaction(string techKey, bool playerOwned)
    {
        var cooldowns = playerOwned ? playerBattleTechCooldownEnds : enemyBattleTechCooldownEnds;
        return cooldowns.TryGetValue(techKey, out var endTime)
            ? Mathf.Max(0f, endTime - GameTime)
            : 0f;
    }

    public bool CanCastBattleTechForFaction(string techKey, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }

        var tech = BattleTechCatalog.Get(techKey);
        var mainBase = FindOperationalMainBase(playerOwned);
        if (mainBase is null || !GodotObject.IsInstanceValid(mainBase) || mainBase.Health <= 0f)
        {
            message = "主基地已失效，无法释放科技";
            return false;
        }

        var remain = GetBattleTechCooldownRemainingForFaction(tech.Key, playerOwned);
        if (remain > 0f)
        {
            message = $"{tech.DisplayName} 冷却中：{Mathf.CeilToInt(remain)}秒";
            return false;
        }

        if (!HasReadyBuilding(tech.RequiredBuildingKey, playerOwned))
        {
            message = $"需要先拥有 {BattleBuildingCatalog.Get(tech.RequiredBuildingKey).DisplayName}";
            return false;
        }

        return true;
    }

    public bool TryCastBattleTechForFaction(string techKey, Vector3 point, bool playerOwned, out string message, out int affected)
    {
        affected = 0;
        if (!CanCastBattleTechForFaction(techKey, playerOwned, out message))
            return false;

        var tech = BattleTechCatalog.Get(techKey);
        var center = new Vector2(point.X, point.Z);

        // 判断当前模式与科技等级：争霸模式我方读实际等级、敌方使用 3 级，快速匹配均为 5 级
        var level = 5;
        if (GameState.Instance is not null && GameState.Instance.LastBattleMode == "全球争霸")
        {
            level = playerOwned ? GameState.Instance.GetTechLevel(tech.Key) : 3;
        }

        // 根据科研等级线性提升各项指标
        var finalMove = tech.MoveMultiplier > 1f ? tech.MoveMultiplier + (level - 1) * 0.04f : tech.MoveMultiplier;
        var finalDamage = tech.DamageMultiplier > 1f ? tech.DamageMultiplier + (level - 1) * 0.05f : tech.DamageMultiplier;
        var finalRange = tech.AttackRangeBonus > 0f ? tech.AttackRangeBonus + (level - 1) * 0.5f : tech.AttackRangeBonus;
        var finalCooldown = tech.AttackCooldownMultiplier < 1f ? tech.AttackCooldownMultiplier - (level - 1) * 0.04f : tech.AttackCooldownMultiplier;
        var finalDefense = tech.DefenseReduction > 0f ? tech.DefenseReduction + (level - 1) * 0.05f : tech.DefenseReduction;
        var finalVision = tech.VisionBonus > 0f ? tech.VisionBonus + (level - 1) * 3f : tech.VisionBonus;
        var finalRegen = tech.RegenPerSecond > 0f ? tech.RegenPerSecond + (level - 1) * 4f : tech.RegenPerSecond;

        foreach (var unit in GetUnits(playerOwned))
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead)
                continue;

            var unitPos = unit.GlobalPosition;
            if (new Vector2(unitPos.X, unitPos.Z).DistanceTo(center) > tech.Radius)
                continue;

            unit.ApplyTechBuff(
                tech.Key,
                tech.Duration,
                finalMove,
                finalDamage,
                finalRange,
                finalCooldown,
                finalDefense,
                finalVision,
                finalRegen,
                tech.Tint);
            affected++;
        }

        // 播放技能释放视觉扩展环和 3D 文字提示
        SpawnTechCastVfx(tech.DisplayName, point, tech.Radius, tech.Tint, level);

        var cooldowns = playerOwned ? playerBattleTechCooldownEnds : enemyBattleTechCooldownEnds;
        cooldowns[tech.Key] = GameTime + tech.Cooldown;
        ForceRefreshFogOfWar();
        message = $"{tech.DisplayName} (Lv.{level}) 已释放，影响 {affected} 个单位";
        return true;
    }

    void SpawnTechCastVfx(string techName, Vector3 position, float radius, Color color, int level)
    {
        var vfxNode = new Node3D { Position = new Vector3(position.X, 0.08f, position.Z) };

        var meshInstance = new MeshInstance3D
        {
            Name = "RippleRing",
            Mesh = new TorusMesh
            {
                InnerRadius = 0.94f,
                OuterRadius = 1.0f,
                RingSegments = 64
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 1.0f,
                EmissionEnabled = true,
                Emission = color * 1.5f
            }
        };
        vfxNode.AddChild(meshInstance);

        var label = new Label3D
        {
            Text = $"{techName}\nLv.{level}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 72,
            OutlineSize = 16,
            Modulate = color.Lightened(0.2f),
            OutlineModulate = new Color(0f, 0f, 0f, 0.8f),
            Position = new Vector3(0f, 1.5f, 0f)
        };
        vfxNode.AddChild(label);

        GetTree().CurrentScene?.AddChild(vfxNode);

        var tween = vfxNode.CreateTween();
        vfxNode.Scale = Vector3.One * 0.1f;
        tween.TweenProperty(vfxNode, "scale", Vector3.One * radius, 0.8f)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.Out);

        tween.Parallel().TweenProperty(meshInstance.MaterialOverride, "albedo_color", new Color(color.R, color.G, color.B, 0f), 0.8f);
        tween.Parallel().TweenProperty(meshInstance.MaterialOverride, "emission", new Color(0f, 0f, 0f, 0f), 0.8f);

        tween.Parallel().TweenProperty(label, "position", new Vector3(0f, 4.5f, 0f), 1.2f)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.Out);
        var textTint = color.Lightened(0.2f);
        tween.Parallel().TweenProperty(label, "modulate", new Color(textTint.R, textTint.G, textTint.B, 0f), 1.2f);
        tween.Parallel().TweenProperty(label, "outline_modulate", new Color(0f, 0f, 0f, 0f), 1.2f);

        tween.TweenCallback(Callable.From(vfxNode.QueueFree));
    }

    public bool TrySpendEnemyGold(int amount)
    {
        if (EnemyGold < amount)
            return false;

        EnemyGold -= amount;
        RecordGoldSpent(false, amount);
        EmitSignal(SignalName.EconomyChanged);
        return true;
    }

    public void AddGold(bool playerOwned, int amount)
    {
        if (amount <= 0 || GameOver)
            return;

        if (playerOwned)
        {
            PlayerGold += amount;
            RecordGoldIncome(true, amount);
        }
        else
        {
            EnemyGold += amount;
            RecordGoldIncome(false, amount);
        }
        EmitSignal(SignalName.EconomyChanged);
    }

    public bool BeginBuildPlacement(string buildKey, out string message)
    {
        message = "";
        if (BattleBuildingCatalog.Get(buildKey).GoldCost <= 0)
            return false;
        if (!CanBeginBuildPlacementWithUnityRules(buildKey, true, out message))
            return false;
        PendingBuildKey = buildKey;
        EmitSignal(SignalName.EconomyChanged);
        return true;
    }

    public void CancelBuildPlacement()
    {
        PendingBuildKey = "";
        EmitSignal(SignalName.EconomyChanged);
    }

    public bool TryPlacePendingBuilding(Vector3 position, out string message)
        => TryPlacePendingBuilding(position, out message, out _);

    public bool TryPlacePendingBuilding(Vector3 position, out string message, out RtsBuilding? constructed)
    {
        constructed = null;
        if (string.IsNullOrEmpty(PendingBuildKey))
        {
            message = "";
            return false;
        }

        var buildKey = PendingBuildKey;
        var placed = TryConstructBuilding(buildKey, position, true, out message, out constructed);
        if (placed)
            PendingBuildKey = "";
        return placed;
    }

    public bool TryConstructBuilding(string buildKey, Vector3 position, bool playerOwned, out string message)
        => TryConstructBuilding(buildKey, position, playerOwned, out message, out _);

    public bool TryConstructBuilding(string buildKey, Vector3 position, bool playerOwned, out string message, out RtsBuilding? constructed)
    {
        message = "";
        constructed = null;
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }

        var def = BattleBuildingCatalog.Get(buildKey);
        if (def.GoldCost <= 0)
        {
            message = "该建筑不能重复建造";
            return false;
        }
        if (!CanBeginBuildPlacementWithUnityRules(buildKey, playerOwned, out message))
            return false;

        if (playerOwned && PlayerGold < def.GoldCost)
        {
            message = $"金币不足：需要 {def.GoldCost}";
            return false;
        }
        if (!playerOwned && EnemyGold < def.GoldCost)
            return false;

        if (!CanPlaceBuildingWithUnityRules(position, def, playerOwned, out message))
            return false;

        if (playerOwned)
        {
            PlayerGold -= def.GoldCost;
            RecordGoldSpent(true, def.GoldCost);
        }
        else
        {
            EnemyGold -= def.GoldCost;
            RecordGoldSpent(false, def.GoldCost);
        }

        constructed = SpawnBuilding(def, position, playerOwned);
        if (constructed is null)
        {
            if (playerOwned)
            {
                PlayerGold += def.GoldCost;
                RecordGoldSpent(true, -def.GoldCost);
            }
            else
            {
                EnemyGold += def.GoldCost;
                RecordGoldSpent(false, -def.GoldCost);
            }

            message = "建造失败：场景未准备好";
            return false;
        }

        EmitSignal(SignalName.EconomyChanged);
        message = $"建造中：{def.DisplayName}";
        return true;
    }

    public bool TryCancelConstruction(RtsBuilding building, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || !building.PlayerOwned)
        {
            message = "请选择己方建筑";
            return false;
        }
        if (!building.UnderConstruction || building.IsMainBase)
        {
            message = "该建筑不能取消";
            return false;
        }

        var def = BattleBuildingCatalog.Get(building.BuildKey);
        var refund = Mathf.RoundToInt(def.GoldCost * 0.75f);
        PlayerGold += refund;
        RecordGoldSpent(true, -refund);
        GameState.Instance?.SetSelection(Array.Empty<Node>());
        building.QueueFree();
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"已取消建造，返还 {refund} 金币";
        return true;
    }

    public bool TryCancelConstructionForFaction(RtsBuilding building, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || building.PlayerOwned != playerOwned)
        {
            message = "请选择对应阵营的建筑";
            return false;
        }
        if (!building.UnderConstruction || building.IsMainBase)
        {
            message = "该建筑不能取消";
            return false;
        }

        var def = BattleBuildingCatalog.Get(building.BuildKey);
        var refund = Mathf.RoundToInt(def.GoldCost * 0.75f);
        if (playerOwned)
            PlayerGold += refund;
        else
            EnemyGold += refund;
        RecordGoldSpent(playerOwned, -refund);
        if (playerOwned)
            GameState.Instance?.SetSelection(Array.Empty<Node>());
        building.QueueFree();
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"已取消建造，返还 {refund} 金币";
        return true;
    }

    public bool TryRepairBuildingForFaction(RtsBuilding building, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || building.PlayerOwned != playerOwned)
        {
            message = "请选择对应阵营的建筑";
            return false;
        }
        if (building.IsRuined || building.IsRebuilding || building.UnderConstruction)
        {
            message = "该建筑当前不能维修";
            return false;
        }

        var missing = building.MaxHealth - building.Health;
        if (missing <= 1f)
        {
            message = "建筑已经完好";
            return false;
        }

        var repairAmount = Mathf.Min(missing, building.MaxHealth * 0.30f);
        var cost = Mathf.Max(1, Mathf.CeilToInt(repairAmount * 0.18f));
        var gold = playerOwned ? PlayerGold : EnemyGold;
        if (gold < cost)
        {
            message = $"金币不足：维修需要 {cost}";
            return false;
        }

        if (playerOwned)
            PlayerGold -= cost;
        else
            EnemyGold -= cost;
        RecordGoldSpent(playerOwned, cost);
        var repaired = building.Repair(repairAmount);
        EmitSignal(SignalName.EconomyChanged);
        message = $"维修完成：恢复 {repaired:0} HP，花费 {cost} 金币";
        return true;
    }

    public bool TryRepairBuilding(RtsBuilding building, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || !building.PlayerOwned)
        {
            message = "请选择己方建筑";
            return false;
        }
        if (building.IsRuined)
        {
            message = "该主基地已成废墟，请先重建";
            return false;
        }
        if (building.IsRebuilding)
        {
            message = "该主基地正在重建中";
            return false;
        }
        if (building.UnderConstruction)
        {
            message = "施工中的建筑不能维修";
            return false;
        }

        var missing = building.MaxHealth - building.Health;
        if (missing <= 1f)
        {
            message = "建筑已经完好";
            return false;
        }

        var repairAmount = Mathf.Min(missing, building.MaxHealth * 0.30f);
        var cost = Mathf.Max(1, Mathf.CeilToInt(repairAmount * 0.18f));
        if (PlayerGold < cost)
        {
            message = $"金币不足：维修需要 {cost}";
            return false;
        }

        PlayerGold -= cost;
        RecordGoldSpent(true, cost);
        var repaired = building.Repair(repairAmount);
        EmitSignal(SignalName.EconomyChanged);
        message = $"维修完成：恢复 {repaired:0} HP，花费 {cost} 金币";
        return true;
    }

    public bool TryStartMainBaseRebuild(RtsBuilding building, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || !building.PlayerOwned || !building.IsMainBase)
        {
            message = "请选择己方主基地";
            return false;
        }
        if (!building.CanStartRebuild())
        {
            message = building.IsRebuilding ? "主基地正在重建中" : "该主基地当前无需重建";
            return false;
        }
        if (PlayerGold < MainBaseRebuildCost)
        {
            message = $"金币不足：重建需要 {MainBaseRebuildCost}";
            return false;
        }

        PlayerGold -= MainBaseRebuildCost;
        RecordGoldSpent(true, MainBaseRebuildCost);
        building.StartRebuild(MainBaseRebuildDuration);
        CacheBaseSnapshot(building);
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"主基地开始重建，预计 {MainBaseRebuildDuration:0}s 完成";
        return true;
    }

    public bool TryStartFactionMainBaseRebuild(bool playerOwned, out string message)
    {
        var target = GetMainBases(playerOwned, true)
            .Where(baseBuilding => GodotObject.IsInstanceValid(baseBuilding) && baseBuilding.CanStartRebuild())
            .OrderBy(baseBuilding => baseBuilding.GlobalPosition.LengthSquared())
            .FirstOrDefault();

        if (target is null)
        {
            message = playerOwned ? "当前没有可重建的主基地" : "敌方当前没有可重建的主基地";
            return false;
        }

        if (playerOwned)
            return TryStartMainBaseRebuild(target, out message);

        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (EnemyGold < MainBaseRebuildCost)
        {
            message = $"敌方金币不足：重建需要 {MainBaseRebuildCost}";
            return false;
        }

        EnemyGold -= MainBaseRebuildCost;
        RecordGoldSpent(false, MainBaseRebuildCost);
        target.StartRebuild(MainBaseRebuildDuration);
        CacheBaseSnapshot(target);
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"敌方主基地开始重建，预计 {MainBaseRebuildDuration:0}s 完成";
        return true;
    }

    public bool TryRepairUnit(RtsUnit unit, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(unit) || !unit.PlayerOwned)
        {
            message = "请选择己方单位";
            return false;
        }
        if (unit.IsDead)
        {
            message = "单位已损毁";
            return false;
        }

        var missing = unit.MaxHealth - unit.Health;
        if (missing <= 1f)
        {
            message = "单位已经完好";
            return false;
        }

        var repairAmount = Mathf.Min(missing, unit.MaxHealth * 0.30f);
        var cost = Mathf.Max(1, Mathf.CeilToInt(repairAmount * 0.20f));
        if (PlayerGold < cost)
        {
            message = $"金币不足：维修需要 {cost}";
            return false;
        }

        PlayerGold -= cost;
        RecordGoldSpent(true, cost);
        var repaired = unit.Repair(repairAmount);
        EmitSignal(SignalName.EconomyChanged);
        message = $"单位维修完成：恢复 {repaired:0} 耐久，花费 {cost} 金币";
        return true;
    }

    public bool TryUpgradeBuilding(RtsBuilding building, out string message)
    {
        message = "";
        if (!CanUpgradeBuilding(building, out message))
            return false;

        var cost = building.NextUpgradeCost();

        PlayerGold -= cost;
        RecordGoldSpent(true, cost);
        building.ApplyUpgradeLevel(building.BuildingLevel + 1, true);
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"{building.DisplayName} 已升级到 {building.BuildingLevel} 级";
        return true;
    }

    public bool TryRepairUnitForFaction(RtsUnit unit, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(unit) || unit.PlayerOwned != playerOwned || unit.IsDead)
        {
            message = "请选择对应阵营的单位";
            return false;
        }

        var missing = unit.MaxHealth - unit.Health;
        if (missing <= 1f)
        {
            message = "单位已经完好";
            return false;
        }

        var repairAmount = Mathf.Min(missing, unit.MaxHealth * 0.30f);
        var cost = Mathf.Max(1, Mathf.CeilToInt(repairAmount * 0.20f));
        var gold = playerOwned ? PlayerGold : EnemyGold;
        if (gold < cost)
        {
            message = $"金币不足：维修需要 {cost}";
            return false;
        }

        if (playerOwned)
            PlayerGold -= cost;
        else
            EnemyGold -= cost;
        RecordGoldSpent(playerOwned, cost);
        var repaired = unit.Repair(repairAmount);
        EmitSignal(SignalName.EconomyChanged);
        message = $"单位维修完成：恢复 {repaired:0} 耐久，花费 {cost} 金币";
        return true;
    }

    public bool TryUpgradeBuildingForFaction(RtsBuilding building, bool playerOwned, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(building) || building.PlayerOwned != playerOwned)
        {
            message = "请选择对应阵营的建筑";
            return false;
        }
        if (building.UnderConstruction)
        {
            message = "建筑仍在施工中";
            return false;
        }
        if (!BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel))
        {
            message = "建筑已满级";
            return false;
        }

        var nextLevel = building.BuildingLevel + 1;
        if (playerOwned)
        {
            var requirements = GetUpgradeRequirementStatuses(building.BuildKey, nextLevel, playerOwned);
            var missing = requirements.Where(item => !item.IsMet).ToArray();
            if (missing.Length > 0)
            {
                message = $"升级条件不足：{FormatRequirementSummary(missing)}";
                return false;
            }
        }

        var cost = building.NextUpgradeCost();
        var gold = playerOwned ? PlayerGold : EnemyGold;
        if (gold < cost)
        {
            message = $"金币不足：需要 {cost}";
            return false;
        }

        if (playerOwned)
            PlayerGold -= cost;
        else
            EnemyGold -= cost;
        RecordGoldSpent(playerOwned, cost);
        building.ApplyUpgradeLevel(nextLevel, true);
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        message = $"{building.DisplayName} 已升级到 {building.BuildingLevel} 级";
        return true;
    }

    public bool TrySendUnitToAirfieldParking(RtsUnit unit, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(unit) || !unit.PlayerOwned)
        {
            message = "请选择己方飞机";
            return false;
        }
        if (!unit.SupportsAirfieldParking)
        {
            message = $"{unit.DisplayName} 当前无需停机";
            return false;
        }
        if (unit.IsParkedAtAirfield)
        {
            message = $"{unit.DisplayName} 已在停机位";
            return false;
        }
        return TryBeginAircraftRefuelReturn(unit, false, out message);
    }

    public bool TryBeginAircraftRefuelReturn(RtsUnit unit, bool lowFuel, out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }
        if (!GodotObject.IsInstanceValid(unit) || unit.IsDead)
        {
            message = "飞机已经损毁";
            return false;
        }
        if (!unit.SupportsAirfieldParking)
        {
            message = $"{unit.DisplayName} 当前无需停机";
            return false;
        }
        if (unit.IsReturningToPark)
        {
            message = $"{unit.DisplayName} 正在返场";
            return false;
        }

        // 同时搜索陆基停机坪和航母，选最近的
        var airfield = ResolveNearestAirfield(unit.PlayerOwned, unit.GlobalPosition);
        var carrier  = ResolveNearestCarrier(unit.PlayerOwned, unit.GlobalPosition);

        Vector3 parkingTarget;
        if (airfield is null && carrier is null)
        {
            message = lowFuel
                ? $"{unit.DisplayName} 油量不足，但没有可用停机场或航母"
                : "需要先建造停机场或部署航母";
            return false;
        }
        else if (airfield is null)
            parkingTarget = NormalizeUnitTarget(unit, GetCarrierParkingTarget(carrier!, unit));
        else if (carrier is null)
            parkingTarget = NormalizeUnitTarget(unit, GetAircraftParkingTarget(airfield, unit));
        else
        {
            // 选最近的降落点
            var airfieldDist = airfield.GlobalPosition.DistanceSquaredTo(unit.GlobalPosition);
            var carrierDist  = carrier.GlobalPosition.DistanceSquaredTo(unit.GlobalPosition);
            parkingTarget = airfieldDist <= carrierDist
                ? NormalizeUnitTarget(unit, GetAircraftParkingTarget(airfield, unit))
                : NormalizeUnitTarget(unit, GetCarrierParkingTarget(carrier, unit));
        }

        unit.BeginAirfieldParking(BuildAircraftParkingPath(unit, parkingTarget), parkingTarget);
        message = lowFuel
            ? $"{unit.DisplayName} 油量不足，正在返场加油"
            : $"{unit.DisplayName} 正在返场停机";
        return true;
    }

    public RtsUnit? SpawnUnit(string unitKey, Vector3 position, bool playerOwned, Vector3? moveTarget = null)
    {
        unitsRoot ??= GetNodeOrNull<Node3D>(UnitsPath) ?? GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Units");
        if (unitsRoot is null)
            return null;

        var def = BattleUnitCatalog.Get(unitKey);
        var spawnPosition = NormalizeUnitTarget(def.Key, position);
        var body = new RtsUnit
        {
            Name = (playerOwned ? "Player_" : "Enemy_") + def.Key,
            UnitKey = def.Key,
            DisplayName = def.DisplayName,
            PlayerOwned = playerOwned,
            MaxHealth = def.MaxHealth,
            MoveSpeed = def.MoveSpeed,
            AttackDamage = def.AttackDamage,
            AttackRange = def.AttackRange,
            AttackCooldown = def.AttackCooldown,
            SplashRadius = BattleUnitCatalog.SplashRadius(def.Key),
            SplashFalloff = BattleUnitCatalog.SplashFalloff(def.Key),
            GoldCost = def.GoldCost,
            PopCost = def.PopCost,
            CruiseHeight = BattleUnitCatalog.SpawnHeight(def.Key),
            NetId = nextNetId++,
            Position = spawnPosition
        };
        body.AddToGroup("rts_selectable");
        body.AddToGroup("rts_units");
        body.AddToGroup(playerOwned ? "player_owned" : "enemy_owned");
        body.Died += OnUnitDied;
        unitsRoot.AddChild(body);

        body.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Position = new Vector3(0f, 0.5f, 0f),
            Shape = new BoxShape3D { Size = new Vector3(1.4f, 1f, 2f) }
        });

        AddUnitVisual(body, def, playerOwned);
        FaceUnitTowardOpponentSpawn(body);
        if (moveTarget is { } target)
            body.MoveTo(NormalizeUnitTarget(body, target));
        RecalculatePopulation();
        ForceRefreshFogOfWar();
        EmitSignal(SignalName.EconomyChanged);
        return body;
    }

    void FaceUnitTowardOpponentSpawn(RtsUnit unit)
    {
        var target = FindMapSpawn(unit.PlayerOwned ? "EnemyBase" : "PlayerBase");
        if (target is null)
            return;

        var direction = target.Value - unit.GlobalPosition;
        direction.Y = 0f;
        if (direction.LengthSquared() <= 0.0001f)
            return;

        unit.FaceDirectionImmediate(direction);
    }

    public RtsBuilding? SpawnBuilding(BattleBuildingDefinition def, Vector3 position, bool playerOwned)
    {
        buildingsRoot ??= GetNodeOrNull<Node3D>(BuildingsPath) ?? GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Buildings");
        if (buildingsRoot is null)
            return null;

        var building = new RtsBuilding
        {
            Name = (playerOwned ? "Player_" : "Enemy_") + def.Key,
            Position = position,
            NetId = nextNetId++
        };
        building.AddToGroup("rts_selectable");
        building.AddToGroup("rts_buildings");
        building.AddToGroup(playerOwned ? "player_owned" : "enemy_owned");
        building.Died += OnBuildingDied;
        building.MainBaseStateChanged += OnMainBaseStateChanged;
        building.ProductionFinished += OnProductionFinished;
        buildingsRoot.AddChild(building);
        building.Configure(def, playerOwned);
        ConfigureMainBaseLifecycle(building);
        building.BeginConstruction(def.IsMainBase ? 0f : ConstructionDuration(def));
        if (!def.IsMainBase)
            RecordBuildingConstructed(playerOwned);

        building.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Position = new Vector3(0f, def.CollisionSize.Y * 0.5f, 0f),
            Shape = new BoxShape3D { Size = def.CollisionSize }
        });

        EnsureBuildingVisual(building, def);
        CacheBaseSnapshot(building);
        RecalculatePopulation();
        ForceRefreshFogOfWar();
        return building;
    }

    void ConfigureExistingUnitVisual(RtsUnit unit)
    {
        if (unit.GetNodeOrNull<Node3D>("PanzerIV") is { } panzer)
        {
            unit.UnitKey = string.IsNullOrWhiteSpace(unit.UnitKey) ? "tank" : unit.UnitKey;
            unit.ConfigureVisualRig(unit.UnitKey, panzer);
            return;
        }

        if (unit.GetNodeOrNull<Node3D>("InfantrySquad") is { } squad)
        {
            unit.ConfigureVisualRig(unit.UnitKey, squad);
            return;
        }

        var imported = unit.GetChildren()
            .OfType<Node3D>()
            .FirstOrDefault(child => child.Name.ToString().Contains("model", StringComparison.OrdinalIgnoreCase));
        if (imported is not null)
        {
            unit.ConfigureVisualRig(unit.UnitKey, imported);
            return;
        }

        // 场景中预置但没有内嵌标准视觉模型的单位，清除潜在残留节点并动态生成其官方视觉外观
        if (!string.IsNullOrWhiteSpace(unit.UnitKey))
        {
            ClearUnitVisualChildren(unit);
            var def = BattleUnitCatalog.Get(unit.UnitKey);
            AddUnitVisual(unit, def, unit.PlayerOwned);
        }
    }

    void ApplyGlobalConquestStarterIfNeeded()
    {
        if (currentMap.Name != BattleMapCatalog.GlobalConquestName || unitsRoot is null)
            return;

        var starterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        if (!BattleUnitCatalog.IsGlobalConquestStarter(starterKey))
            starterKey = "tank";

        if (unitsRoot.GetNodeOrNull<RtsUnit>("PlayerTank") is { } playerStarter)
            ConfigureGlobalConquestStarterUnit(playerStarter, BattleUnitCatalog.Get(starterKey), true);

        if (unitsRoot.GetNodeOrNull<RtsUnit>("EnemyTank") is { } enemyStarter)
            ConfigureGlobalConquestStarterUnit(enemyStarter, BattleUnitCatalog.Get("tank"), false);
    }

    void ConfigureGlobalConquestStarterUnit(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        unit.ConfigureFromDefinition(def, playerOwned, playerOwned ? def.DisplayName : $"敌方{def.DisplayName}");
        ClearUnitVisualChildren(unit);
        AddUnitVisual(unit, def, playerOwned);
        FaceUnitTowardOpponentSpawn(unit);
    }

    static void ClearUnitVisualChildren(RtsUnit unit)
    {
        foreach (var child in unit.GetChildren().OfType<Node>().ToArray())
        {
            if (child.Name == "CollisionShape3D"
                || child.Name == "WorldHealthBar"
                || child.Name == "SelectionRing"
                || child.Name == "TechRing")
                continue;

            unit.RemoveChild(child);
            child.QueueFree();
        }
    }

    static float ConstructionDuration(BattleBuildingDefinition def)
        => Mathf.Clamp(def.GoldCost / 30f, 8f, 18f);

    public RtsBuilding? FindMainBase(bool playerOwned)
        => GetMainBases(playerOwned, true)
            .OrderByDescending(b => !b.IsRuined && !b.IsRebuilding && b.Health > 0f)
            .ThenByDescending(b => b.Health)
            .FirstOrDefault();

    void OnProductionFinished(RtsBuilding building, string unitKey, Vector3 spawnPosition)
    {
        var unit = SpawnUnit(unitKey, NormalizeUnitTarget(unitKey, spawnPosition), building.PlayerOwned);
        if (unit is null)
            return;
        RecordUnitProduced(building.PlayerOwned);

        if (BattleUnitCatalog.IsAirUnit(unit.UnitKey) && building.BuildKey == "air_factory")
        {
            if (unit.UnitKey == "scout_plane")
            {
                unit.MoveTo(NormalizeUnitTarget(unit, building.GetRallyTarget()));
                return;
            }

            if (ResolveAircraftTransferTarget(building, unit) is { } parkingTarget)
            {
                unit.BeginAirfieldParking(BuildAircraftTransferPath(building, parkingTarget), parkingTarget);
                return;
            }
        }

        if (building.PlayerOwned)
            unit.MoveTo(NormalizeUnitTarget(unit, building.GetRallyTarget()));
    }

    void OnUnitDied(RtsUnit unit)
    {
        if (unit.PlayerOwned)
            PlayerUnitsLost++;
        else
            PlayerUnitKills++;
        RecalculatePopulation();
        EmitSignal(SignalName.EconomyChanged);
    }

    void OnBuildingDied(RtsBuilding building)
    {
        CacheBaseSnapshot(building);
        if (building.PlayerOwned)
            PlayerBuildingsLost++;
        else
            PlayerBuildingKills++;
        RecalculatePopulation();
        EmitSignal(SignalName.EconomyChanged);
        CheckWinLose();
    }

    void OnMainBaseStateChanged(RtsBuilding building, int state)
    {
        _ = state;
        CacheBaseSnapshot(building);
        RecalculatePopulation();
        RecalculatePower();
        EmitSignal(SignalName.EconomyChanged);
        CheckWinLose();
    }

    void CheckWinLose()
    {
        var playerBase = FindMainBase(true);
        var enemyBase = FindMainBase(false);
        if (playerBase is not null)
            playerBaseSeen = true;
        if (enemyBase is not null)
            enemyBaseSeen = true;
        if (playerBaseSeen && enemyBaseSeen)
            basesReady = true;
        if (!basesReady || GameTime < 2f)
            return;

        if (IsFactionEliminated(false))
            EndGame(true, "敌方失去所有主基地且已无力继续作战");
        else if (IsFactionEliminated(true))
            EndGame(false, "我方失去所有主基地且已无法继续作战");
    }

    void EndGame(bool playerWon, string reason)
    {
        if (GameOver)
            return;

        GameOver = true;
        PlayerWon = playerWon;
        GameOverReason = reason;
        EmitSignal(SignalName.GameEnded, playerWon, reason);

        // 战局结束时渐隐停止背景音乐
        if (bgmPlayer is not null && bgmPlayer.Playing)
        {
            var tween = bgmPlayer.CreateTween();
            tween.TweenProperty(bgmPlayer, "volume_db", -80f, 1.5f);
            tween.TweenCallback(Callable.From(bgmPlayer.Stop));
        }
    }

    void RecalculatePopulation()
    {
        PlayerPopUsed = 0;
        EnemyPopUsed = 0;
        PlayerPopCap = InitialPopCap;
        EnemyPopCap = InitialPopCap;

        foreach (var unit in GetUnits())
        {
            if (unit.PlayerOwned)
                PlayerPopUsed += Math.Max(1, unit.PopCost);
            else
                EnemyPopUsed += Math.Max(1, unit.PopCost);
        }

        foreach (var building in GetBuildings())
        {
            if (building.IsRuined || building.IsRebuilding)
                continue;

            if (building.PlayerOwned)
                PlayerPopCap += Math.Max(0, building.PopCapBonus);
            else
                EnemyPopCap += Math.Max(0, building.PopCapBonus);
        }
    }

    void RecalculatePower()
    {
        PlayerPowerProvided = 0;
        PlayerPowerUsed = 0;
        EnemyPowerProvided = 0;
        EnemyPowerUsed = 0;

        foreach (var building in GetBuildings())
        {
            if (building.UnderConstruction || building.IsRuined || building.IsRebuilding)
            {
                building.Powered = false;
                continue;
            }

            if (building.PlayerOwned)
            {
                PlayerPowerProvided += Math.Max(0, building.PowerProvided);
                PlayerPowerUsed += Math.Max(0, building.PowerUsed);
            }
            else
            {
                EnemyPowerProvided += Math.Max(0, building.PowerProvided);
                EnemyPowerUsed += Math.Max(0, building.PowerUsed);
            }
        }

        foreach (var building in GetBuildings())
            building.Powered = building.PlayerOwned ? PlayerPowerOnline : EnemyPowerOnline;
    }

    void RefreshFogOfWar(float delta)
    {
        fogRefreshTimer -= delta;
        if (fogRefreshTimer > 0f)
            return;

        fogRefreshTimer = 0.20f;
        ForceRefreshFogOfWar();
    }

    void ForceRefreshFogOfWar()
    {
        if (DisableFogOfWar)
        {
            foreach (var unit in GetUnits())
                unit.SetFogRevealed(true);
            foreach (var building in GetBuildings())
                building.SetFogRevealed(true);
            return;
        }

        var playerUnits = GetUnits(true);
        var playerBuildings = GetBuildings(true);

        foreach (var unit in GetUnits(false))
            unit.SetFogRevealed(IsInPlayerVision(unit.GlobalPosition, playerUnits, playerBuildings));

        foreach (var building in GetBuildings(false))
            building.SetFogRevealed(IsInPlayerVision(building.GlobalPosition, playerUnits, playerBuildings));

        if (GameState.Instance is not null)
        {
            var visibleSelection = GameState.Instance.Selected
                .Where(node => GodotObject.IsInstanceValid(node))
                .Where(node => node is not Node3D node3D || IsVisibleToPlayer(node3D))
                .ToArray();
            if (visibleSelection.Length != GameState.Instance.Selected.Count)
                GameState.Instance.SetSelection(visibleSelection);
        }
    }

    bool IsInPlayerVision(Vector3 position, IReadOnlyList<RtsUnit> units, IReadOnlyList<RtsBuilding> buildings)
    {
        foreach (var unit in units)
        {
            var radius = (BattleUnitCatalog.IsAirUnit(unit.UnitKey) ? UnitVisionRadius * 1.25f : UnitVisionRadius)
                + Mathf.Max(0f, unit.VisionBonus);
            if (unit.GlobalPosition.DistanceSquaredTo(position) <= radius * radius)
                return true;
        }

        foreach (var building in buildings)
        {
            var radius = building.IsMainBase ? MainBaseVisionRadius : BuildingVisionRadius;
            if (building.GlobalPosition.DistanceSquaredTo(position) <= radius * radius)
                return true;
        }

        return false;
    }

    void UpdateBaseSeenFlags()
    {
        playerBaseSeen |= FindMainBase(true) is not null;
        enemyBaseSeen |= FindMainBase(false) is not null;
        basesReady |= playerBaseSeen && enemyBaseSeen;
    }

    void RecordUnitProduced(bool playerOwned)
    {
        if (playerOwned)
            PlayerUnitsProduced++;
        else
            EnemyUnitsProduced++;
    }

    void RecordBuildingConstructed(bool playerOwned)
    {
        if (playerOwned)
            PlayerBuildingsConstructed++;
        else
            EnemyBuildingsConstructed++;
    }

    public void RecordDamage(bool attackerPlayerOwned, bool targetPlayerOwned, int amount)
    {
        if (amount <= 0)
            return;

        if (attackerPlayerOwned)
            PlayerDamageDealt += amount;
        else
            EnemyDamageDealt += amount;

        if (targetPlayerOwned)
            PlayerDamageTaken += amount;
        else
            EnemyDamageTaken += amount;
    }

    void RecordGoldIncome(bool playerOwned, int amount)
    {
        if (amount <= 0)
            return;

        if (playerOwned)
            PlayerGoldIncome += amount;
        else
            EnemyGoldIncome += amount;
    }

    void RecordGoldSpent(bool playerOwned, int amount)
    {
        if (amount == 0)
            return;

        if (playerOwned)
            PlayerGoldSpent += amount;
        else
            EnemyGoldSpent += amount;
    }

    void ConfigureMainBaseLifecycle(RtsBuilding building)
    {
        if (!building.IsMainBase)
            return;

        building.CanBeRebuilt = true;
        building.RebuildDuration = MainBaseRebuildDuration;
        building.RebuildHealthFraction = 0.45f;
    }

    bool IsFactionEliminated(bool playerOwned)
    {
        var allMainBases = GetMainBases(playerOwned, true);
        if (allMainBases.Count == 0)
            return true;

        var hasOperationalMainBase = allMainBases.Any(b => !b.IsRuined && !b.IsRebuilding && b.Health > 0f);
        if (!hasOperationalMainBase)
        {
            // 主基地一旦被摧毁/损坏，该方势力即被淘汰，直接触发战败或胜利结算画面
            return true;
        }

        return false;
    }

    void CacheBaseSnapshot(RtsBuilding? building)
    {
        if (building is null || !building.IsMainBase)
            return;

        if (building.PlayerOwned)
        {
            lastPlayerBaseLevel = Math.Max(1, building.BuildingLevel);
            lastPlayerBaseHp = Mathf.RoundToInt(building.Health);
            lastPlayerBaseMaxHp = Mathf.RoundToInt(building.MaxHealth);
        }
        else
        {
            lastEnemyBaseLevel = Math.Max(1, building.BuildingLevel);
            lastEnemyBaseHp = Mathf.RoundToInt(building.Health);
            lastEnemyBaseMaxHp = Mathf.RoundToInt(building.MaxHealth);
        }
    }

    public string GetPlayerBaseSummaryText()
    {
        var mainBase = FindMainBase(true);
        if (mainBase is not null)
            CacheBaseSnapshot(mainBase);

        return mainBase is not null && mainBase.Health > 0f && !mainBase.IsRuined
            ? $"Lv.{Math.Max(1, mainBase.BuildingLevel)} {Mathf.RoundToInt(mainBase.Health)}/{Mathf.RoundToInt(mainBase.MaxHealth)}"
            : mainBase is not null && mainBase.IsRebuilding
                ? $"Lv.{Math.Max(1, mainBase.BuildingLevel)} 重建中 {Mathf.CeilToInt(mainBase.RebuildTimeLeft)}s"
                : $"Lv.{Math.Max(1, lastPlayerBaseLevel)} 已摧毁";
    }

    public string GetEnemyBaseSummaryText()
    {
        var mainBase = FindMainBase(false);
        if (mainBase is not null)
            CacheBaseSnapshot(mainBase);

        return mainBase is not null && mainBase.Health > 0f && !mainBase.IsRuined
            ? $"Lv.{Math.Max(1, mainBase.BuildingLevel)} {Mathf.RoundToInt(mainBase.Health)}/{Mathf.RoundToInt(mainBase.MaxHealth)}"
            : mainBase is not null && mainBase.IsRebuilding
                ? $"Lv.{Math.Max(1, mainBase.BuildingLevel)} 重建中 {Mathf.CeilToInt(mainBase.RebuildTimeLeft)}s"
                : $"Lv.{Math.Max(1, lastEnemyBaseLevel)} 已摧毁";
    }

    public BattleReportData BuildBattleReportData()
    {
        var playerUnits = GetUnits(true);
        var enemyUnits = GetUnits(false);
        var playerBuildings = GetBuildings(true);
        var enemyBuildings = GetBuildings(false);
        var durationText = $"{Mathf.FloorToInt(GameTime) / 60:00}:{Mathf.FloorToInt(GameTime) % 60:00}";
        var teamLine = "我方部队  VS  AI敌军";

        return new BattleReportData
        {
            TeamLine = teamLine,
            PlayerHeader = "我方",
            EnemyHeader = "敌方",
            DurationText = durationText,
            Rows = new[]
            {
                new BattleReportRow(
                    "战果",
                    $"摧毁{PlayerUnitKills + PlayerBuildingKills}({PlayerUnitKills}兵/{PlayerBuildingKills}建)",
                    $"摧毁{PlayerUnitsLost + PlayerBuildingsLost}({PlayerUnitsLost}兵/{PlayerBuildingsLost}建)"),
                new BattleReportRow(
                    "兵力",
                    $"{playerUnits.Count}兵/{playerBuildings.Count}建",
                    $"{enemyUnits.Count}兵/{enemyBuildings.Count}建"),
                new BattleReportRow(
                    "生产",
                    $"出兵{PlayerUnitsProduced} 建造{PlayerBuildingsConstructed}",
                    $"出兵{EnemyUnitsProduced} 建造{EnemyBuildingsConstructed}"),
                new BattleReportRow(
                    "人口",
                    $"{PlayerPopUsed}/{PlayerPopCap} 峰{PlayerPeakPopUsed}",
                    $"{EnemyPopUsed}/{EnemyPopCap} 峰{EnemyPeakPopUsed}"),
                new BattleReportRow(
                    "伤害",
                    $"出{PlayerDamageDealt:N0} 承{PlayerDamageTaken:N0}",
                    $"出{EnemyDamageDealt:N0} 承{EnemyDamageTaken:N0}"),
                new BattleReportRow(
                    "经济",
                    $"金{PlayerGold:N0} 收{PlayerGoldIncome:N0} 耗{Math.Max(0, PlayerGoldSpent):N0}",
                    $"金{EnemyGold:N0} 收{EnemyGoldIncome:N0} 耗{Math.Max(0, EnemyGoldSpent):N0}"),
                new BattleReportRow(
                    "电力",
                    $"{PlayerPowerUsed}/{PlayerPowerProvided}",
                    $"{EnemyPowerUsed}/{EnemyPowerProvided}"),
                new BattleReportRow(
                    "主基",
                    GetPlayerBaseSummaryText(),
                    GetEnemyBaseSummaryText())
            }
        };
    }

    int QueuedPop(bool playerOwned)
    {
        var total = 0;
        foreach (var building in GetBuildings(playerOwned))
        {
            if (string.IsNullOrEmpty(building.CurrentProduction))
                continue;
            total += BattleUnitCatalog.Get(building.CurrentProduction).PopCost;
        }
        return total;
    }

    int CountQueuedAircraft(bool playerOwned)
    {
        var total = 0;
        foreach (var building in GetBuildings(playerOwned))
        {
            if (!BattleUnitCatalog.RequiresAirfieldSlot(building.CurrentProduction))
                continue;
            total++;
        }
        return total;
    }

    void ApplyTechToUnits(bool playerOwned)
    {
    }

    void ApplyTechToUnit(RtsUnit unit)
    {
    }

    bool HasReadyPlayerBuilding(string buildKey)
        => HasReadyBuilding(buildKey, true);

    bool HasReadyBuilding(string buildKey, bool playerOwned)
        => GetBuildings(playerOwned).Any(building =>
            building.BuildKey == buildKey
            && !building.UnderConstruction
            && !building.IsRuined
            && !building.IsRebuilding);

    bool AreUpgradeRequirementsMet(bool playerOwned, IEnumerable<BattleBuildingRequirement> requirements, out string message)
    {
        message = "";
        var statuses = BattleBuildingUpgradeCatalog.EvaluateRequirements(
            requirements,
            key => CountBuildings(key, playerOwned, false),
            key => HighestBuildingLevel(key, playerOwned, false));
        var missing = statuses.Where(status => !status.IsMet).ToArray();
        if (missing.Length == 0)
            return true;

        message = FormatRequirementSummary(missing);
        return false;
    }

    string BuildUpgradePreviewMessage(RtsBuilding building, int nextLevel, int goldCost, BattleBuildingRequirementStatus[] requirements)
    {
        if (PlayerGold < goldCost)
            return $"金币不足：需要 {goldCost}";

        var missing = requirements.Where(item => !item.IsMet).ToArray();
        if (missing.Length > 0)
            return $"升级到 {nextLevel} 级条件不足：{FormatRequirementSummary(missing)}";

        return $"{building.DisplayName} 可以升级到 {nextLevel} 级";
    }

    static string FormatRequirementSummary(IEnumerable<BattleBuildingRequirementStatus> requirements)
    {
        return string.Join("，", requirements.Select(item =>
            item.RequiredLevel > 1
                ? $"{item.DisplayName} {item.HighestLevel}/{item.RequiredLevel}"
                : $"{item.DisplayName} {item.CurrentCount}/{item.RequiredCount}"));
    }

    bool CanBeginBuildPlacement(string buildKey, bool playerOwned, out string message)
    {
        message = "";
        var baseLevel = GetMainBaseLevel(playerOwned);
        if (baseLevel <= 0)
        {
            message = playerOwned ? "主基地已失效，无法继续建造" : "基地已失效";
            return false;
        }

        var limit = GetBuildingLimit(buildKey, playerOwned);
        if (limit == int.MaxValue)
            return true;

        var currentCount = CountBuildings(buildKey, playerOwned, true);
        if (currentCount < limit)
            return true;

        message = $"主基地 Lv.{baseLevel} 最多可建 {limit} 个{BattleBuildingCatalog.Get(buildKey).DisplayName}";
        return false;
    }

    bool CanPlaceBuilding(Vector3 position, BattleBuildingDefinition def, bool playerOwned, out string message)
    {
        message = "";
        if (GetMainBaseLevel(playerOwned) <= 0)
        {
            message = "\u4e3b\u57fa\u5730\u5df2\u5931\u6548\uff0c\u65e0\u6cd5\u5efa\u9020\u5efa\u7b51";
            return false;
        }
        if (Mathf.Abs(position.X) > BattleMapCatalog.GetMapHalfSize(currentMap) - 18f || Mathf.Abs(position.Z) > BattleMapCatalog.GetMapHalfSize(currentMap) - 18f)
        {
            message = "建造位置超出地图边界";
            return false;
        }

        if (IsWaterPoint(position, 1f))
        {
            message = "陆地建筑不能放在河道或水面上";
            return false;
        }

        if (def.Key == "naval_yard" && DistanceToWater(position) > 18f)
        {
            message = "船坞必须靠近水域";
            return false;
        }

        var mainBase = FindOperationalMainBase(playerOwned);
        if (mainBase is not null && position.DistanceTo(mainBase.GlobalPosition) > 78f)
        {
            message = "建筑需要放在基地附近";
            return false;
        }

        var w1 = def.CollisionSize.X;
        var h1 = def.CollisionSize.Z;
        const float minSpacing = 0.5f;
        foreach (var building in GetBuildings())
        {
            var otherDef = BattleBuildingCatalog.Get(building.BuildKey);
            var w2 = otherDef.CollisionSize.X;
            var h2 = otherDef.CollisionSize.Z;
            float dx = Mathf.Abs(position.X - building.GlobalPosition.X);
            float dz = Mathf.Abs(position.Z - building.GlobalPosition.Z);
            if (dx < (w1 + w2) * 0.5f + minSpacing && dz < (h1 + h2) * 0.5f + minSpacing)
            {
                message = "这里离已有建筑太近";
                return false;
            }
        }

        return true;
    }


    Vector3? ResolveAircraftTransferTarget(RtsBuilding factory, RtsUnit unit)
    {
        // 寻找最近的陆基停机坪或航母，哪个近用哪个
        var airfield = ResolveNearestAirfield(factory.PlayerOwned, factory.GlobalPosition);
        var carrier  = ResolveNearestCarrier(factory.PlayerOwned, factory.GlobalPosition);

        Vector3 landingBase;
        if (airfield is null && carrier is null)
            return NormalizeUnitTarget(unit, factory.GetRallyTarget());
        else if (airfield is null)
            landingBase = GetCarrierParkingTarget(carrier!, unit);
        else if (carrier is null)
            landingBase = GetAircraftParkingTarget(airfield, unit);
        else
        {
            var airfieldDist = airfield.GlobalPosition.DistanceSquaredTo(factory.GlobalPosition);
            var carrierDist  = carrier.GlobalPosition.DistanceSquaredTo(factory.GlobalPosition);
            landingBase = airfieldDist <= carrierDist
                ? GetAircraftParkingTarget(airfield, unit)
                : GetCarrierParkingTarget(carrier, unit);
        }
        return NormalizeUnitTarget(unit, landingBase);
    }

    public RtsBuilding? ResolveNearestAirfield(bool playerOwned, Vector3 origin)
    {
        return GetBuildings(playerOwned)
            .Where(building => building.BuildKey == "airfield" && !building.UnderConstruction && building.Health > 0f)
            .OrderBy(building => building.GlobalPosition.DistanceSquaredTo(origin))
            .FirstOrDefault();
    }

    /// <summary>返回最近的存活航母单位，没有则 null。</summary>
    public RtsUnit? ResolveNearestCarrier(bool playerOwned, Vector3 origin)
    {
        return GetUnits(playerOwned)
            .Where(u => BattleUnitCatalog.IsCarrier(u.UnitKey) && !u.IsDead)
            .OrderBy(u => u.GlobalPosition.DistanceSquaredTo(origin))
            .FirstOrDefault();
    }

    Vector3 GetAircraftParkingTarget(RtsBuilding airfield, RtsUnit unit)
    {
        var key = unit.NetId;
        if (!aircraftParkingSlots.TryGetValue(key, out var stored) || stored.DistanceSquaredTo(airfield.GlobalPosition) > 225f)
        {
            var slotIndex = GetUnits(unit.PlayerOwned)
                .Count(existing => existing != unit
                    && existing.UnitKey == unit.UnitKey
                    && existing.GlobalPosition.DistanceSquaredTo(airfield.GlobalPosition) < 36f);
            slotIndex %= 4;

            var offset = slotIndex switch
            {
                0 => new Vector3(-2.6f, 0f, -2.4f),
                1 => new Vector3(2.6f, 0f, -2.4f),
                2 => new Vector3(-2.6f, 0f, 2.4f),
                _ => new Vector3(2.6f, 0f, 2.4f)
            };
            stored = airfield.GlobalPosition + offset;
            aircraftParkingSlots[key] = stored;
        }

        return stored;
    }

    /// <summary>
    /// 在航母甲板上分配停靠位。航母是移动单位，每次都重新计算以跟随航母当前位置。
    /// 4 个甲板槽位按间距 3.2 m 排列在甲板中线两侧。
    /// </summary>
    Vector3 GetCarrierParkingTarget(RtsUnit carrier, RtsUnit aircraft)
    {
        // 航母是运动目标，不缓存具体坐标，只缓存槽位索引
        var key = aircraft.NetId;
        if (!aircraftParkingSlots.TryGetValue(key, out var stored) || stored.DistanceSquaredTo(carrier.GlobalPosition) > 400f)
        {
            var slotIndex = GetUnits(aircraft.PlayerOwned)
                .Count(existing => existing != aircraft
                    && BattleUnitCatalog.RequiresAirfieldSlot(existing.UnitKey)
                    && existing.GlobalPosition.DistanceSquaredTo(carrier.GlobalPosition) < 64f);
            slotIndex %= BattleUnitCatalog.CarrierSlotCount;

            // 甲板位：前两个靠舰首，后两个靠舰尾，横向各偏 1.6 m
            var offset = slotIndex switch
            {
                0 => new Vector3(-1.6f, 0.9f, -3.0f),
                1 => new Vector3( 1.6f, 0.9f, -3.0f),
                2 => new Vector3(-1.6f, 0.9f,  1.8f),
                _ => new Vector3( 1.6f, 0.9f,  1.8f)
            };
            stored = carrier.GlobalPosition + offset;
            aircraftParkingSlots[key] = stored;
        }
        else
        {
            // 航母移动时持续更新已分配的停靠点，让飞机跟着甲板走
            // （甲板 offset 相对于上次记录位置不变，整体平移）
            // 这里简单地：每帧重新锁定到航母当前位置 + 上次offset
            var delta = carrier.GlobalPosition - new Vector3(stored.X, carrier.GlobalPosition.Y, stored.Z) + stored;
            _ = delta; // 实际不需要——飞机停靠后不再移动，航母可以驶离
        }
        return stored;
    }


    IEnumerable<Vector3> BuildAircraftTransferPath(RtsBuilding factory, Vector3 parkingTarget)
    {
        var launch = factory.GlobalPosition + new Vector3(0f, 0f, -13f);
        var cruise = factory.GlobalPosition.Lerp(parkingTarget, 0.42f) + new Vector3(0f, 0f, -6f);
        var approachDirection = (parkingTarget - factory.GlobalPosition).Normalized();
        if (approachDirection.LengthSquared() < 0.0001f)
            approachDirection = Vector3.Forward;
        var approach = parkingTarget - approachDirection * 7.5f;
        return new[] { launch, cruise, approach, parkingTarget };
    }

    IEnumerable<Vector3> BuildAircraftParkingPath(RtsUnit unit, Vector3 parkingTarget)
    {
        var flatOrigin = new Vector3(unit.GlobalPosition.X, 0f, unit.GlobalPosition.Z);
        var flatParking = new Vector3(parkingTarget.X, 0f, parkingTarget.Z);
        var approachDirection = (flatParking - flatOrigin).Normalized();
        if (approachDirection.LengthSquared() < 0.0001f)
            approachDirection = Vector3.Forward;
        var cruise = flatOrigin.Lerp(flatParking, 0.45f) + new Vector3(0f, 0f, -4f);
        var approach = flatParking - approachDirection * 7.5f;
        return new[] { cruise, approach, flatParking };
    }

    void AddUnitVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        if (def.Key == "scout_plane" && ScoutPlaneScene is not null)
        {
            AddPackedUnitVisual(unit, ScoutPlaneScene, "ScoutHelicopter", def, playerOwned);
            AddAirShadow(unit);
            return;
        }

        if (BattleUnitCatalog.IsAirUnit(def.Key))
        {
            var airScene = def.Key switch
            {
                "fighter" when FighterScene is not null => FighterScene,
                "fighter" when ResourceLoader.Exists("res://assets/units/fa18_fighter.glb") => ResourceLoader.Load<PackedScene>("res://assets/units/fa18_fighter.glb"),
                "bomber" when BomberScene is not null => BomberScene,
                "bomber" when ResourceLoader.Exists("res://assets/models/bomber.glb") => ResourceLoader.Load<PackedScene>("res://assets/models/bomber.glb"),
                "bomber" when ResourceLoader.Exists("res://assets/units/b2_spirit.glb") => ResourceLoader.Load<PackedScene>("res://assets/units/b2_spirit.glb"),
                _ => null
            };
            if (airScene is not null)
            {
                AddPackedUnitVisual(unit, airScene, def.Key + "_model", def, playerOwned);
                AddAirShadow(unit);
            }
            else
            {
                AddAirUnitVisual(unit, def, playerOwned);
            }
            return;
        }

        if (BattleUnitCatalog.IsNavalUnit(def.Key))
        {
            if (def.Key == "aircraft_carrier" && AircraftCarrierScene is not null)
            {
                AddPackedUnitVisual(unit, AircraftCarrierScene, "AircraftCarrier", def, playerOwned);
                return;
            }
            AddNavalUnitVisual(unit, def, playerOwned);
            return;
        }

        if (BattleUnitCatalog.IsInfantryLike(def.Key))
        {
            AddInfantryVisual(unit, def, playerOwned);
            return;
        }

        string[] candidateVehiclePaths = new string[]
        {
            $"res://assets/models/{def.Key}.glb",
            $"res://assets/models/{def.Key}.gltf",
            $"res://assets/models/{def.Key}.fbx",
            $"res://assets/models/{def.Key}.obj",
            $"res://assets/units/converted/{def.Key}.glb",
            $"res://assets/units/converted/{def.Key}.fbx"
        };
        foreach (var path in candidateVehiclePaths)
        {
            if (Godot.FileAccess.FileExists(path) && ResourceLoader.Exists(path))
            {
                var scene = GD.Load<PackedScene>(path);
                if (scene != null)
                {
                    AddPackedUnitVisual(unit, scene, def.Key + "_model", def, playerOwned);
                    return;
                }
            }
        }

        EnsureVehicleScenesLoaded();

        var vehicleScene = def.Key switch
        {
            "light_tank" => LightTankScene ?? TankScene,
            "heavy_tank" => HeavyTankScene ?? TankScene,
            "artillery" => ArtilleryScene ?? TankScene,
            _ => TankScene
        };
        if (vehicleScene is not null && !BattleUnitCatalog.IsInfantryLike(def.Key))
        {
            AddPackedUnitVisual(unit, vehicleScene, def.Key + "_model", def, playerOwned);
            return;
        }

        AddProceduralInfantryVisual(unit, def, playerOwned);
    }

    void EnsureVehicleScenesLoaded()
    {
        LightTankScene ??= ResourceLoader.Load<PackedScene>("res://assets/units/converted/light_tank.glb");
        HeavyTankScene ??= ResourceLoader.Load<PackedScene>("res://assets/units/converted/heavy_tank.glb");
        ArtilleryScene ??= ResourceLoader.Load<PackedScene>("res://assets/units/converted/artillery.glb");
        TankScene ??= ResourceLoader.Load<PackedScene>("res://assets/units/panzer_iv/pzIV.glb");
    }

    void AddInfantryVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        if (InfantryScene is null)
        {
            AddProceduralInfantryVisual(unit, def, playerOwned);
            return;
        }

        var squad = new Node3D { Name = "InfantrySquad" };
        unit.AddChild(squad);
        var soldierCount = 1;
        // 使用高对比度显眼的阵营专属队服色（己方为电光湛蓝/亮金，敌方为鲜艳烈焰红）
        var infantryTint = playerOwned 
            ? def.Key switch
            {
                "infantry_flamethrower" or "flamethrower" => new Color(1.00f, 0.78f, 0.18f, 1f), // 喷火兵-亮金
                "infantry_artillery" => new Color(0.20f, 0.90f, 0.85f, 1f), // 迫击炮兵-青蓝
                _ => new Color(0.18f, 0.82f, 1.00f, 1f) // 普通步兵-高亮电光湛蓝
            }
            : new Color(1.00f, 0.25f, 0.18f, 1f); // 敌方-鲜艳红
        for (var i = 0; i < soldierCount; i++)
        {
            var centerOffset = (soldierCount - 1) * 0.5f;
            var soldierRoot = new Node3D
            {
                Name = $"Infantry_{i}",
                Position = soldierCount == 1 ? Vector3.Zero : new Vector3((i - centerOffset) * 0.65f, 0f, i == 1 ? -0.34f : 0.30f),
                Scale = Vector3.One * InfantryModelScale(def.Key),
                Rotation = new Vector3(0f, Mathf.Pi, 0f)
            };
            var model = InfantryScene.Instantiate<Node3D>();
            model.Name = "Model";
            soldierRoot.AddChild(model);
            CenterImportedModel(model);
            TintImportedModel(soldierRoot, infantryTint, 0.72f);
            // 注入 Mixamo 骨骼动画（idle / walk / fire）
            InfantryAnimationBridge.InjectAnimations(model);
            AttachWeaponToInfantry(model, def.Key);
            squad.AddChild(soldierRoot);
        }
        unit.ConfigureVisualRig(def.Key, squad);
    }

    static float InfantryModelScale(string key) => key switch
    {
        "infantry_artillery" => 1.05f,
        "infantry_flamethrower" or "flamethrower" => 1.10f,
        _ => 1.00f
    };

    void AddProceduralInfantryVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        var squad = new Node3D { Name = "InfantrySquad" };
        unit.AddChild(squad);
        var soldierCount = 1;
        var infantryTint = playerOwned 
            ? new Color(0.18f, 0.82f, 1.00f, 1f) 
            : new Color(1.00f, 0.25f, 0.18f, 1f);
        for (var i = 0; i < soldierCount; i++)
        {
            var centerOffset = (soldierCount - 1) * 0.5f;
            var soldierRoot = new Node3D
            {
                Name = $"Infantry_{i}",
                Position = soldierCount == 1 ? Vector3.Zero : new Vector3((i - centerOffset) * 0.58f, 0f, i == 1 ? -0.34f : 0.28f),
                Scale = Vector3.One * 1.55f, // 积木小兵同样等比放大 1.55 倍
                Rotation = new Vector3(0f, Mathf.Pi, 0f)
            };
            soldierRoot.AddChild(new MeshInstance3D
            {
                Name = "Body",
                Position = new Vector3(0f, 0.75f, 0f),
                Mesh = new CapsuleMesh { Radius = 0.22f, Height = 1.25f },
                MaterialOverride = Material(infantryTint, 0.82f)
            });
            AddInfantryStrideRig(soldierRoot, infantryTint);
            squad.AddChild(soldierRoot);
        }
        unit.ConfigureVisualRig(def.Key, squad);
    }

    static void AddInfantryStrideRig(Node3D soldierRoot, Color uniformTint)
    {
        var cloth = uniformTint.Lightened(0.08f);
        var gear = uniformTint.Darkened(0.38f);
        AddBlock(soldierRoot, "StrideLeftLeg", new Vector3(0.075f, 0.42f, 0.085f), new Vector3(-0.105f, 0.32f, 0.02f), gear);
        AddBlock(soldierRoot, "StrideRightLeg", new Vector3(0.075f, 0.42f, 0.085f), new Vector3(0.105f, 0.32f, 0.02f), gear);
        AddBlock(soldierRoot, "StrideLeftArm", new Vector3(0.070f, 0.38f, 0.075f), new Vector3(-0.265f, 0.76f, 0.0f), cloth.Darkened(0.14f));
        AddBlock(soldierRoot, "StrideRightArm", new Vector3(0.070f, 0.38f, 0.075f), new Vector3(0.265f, 0.76f, 0.0f), cloth.Darkened(0.14f));
        AddImportedInfantryWeapon(soldierRoot);
    }

    static void AddImportedInfantryWeapon(Node3D soldierRoot)
    {
        var position = new Vector3(0.17f, 0.79f, -0.24f);
        if (TryAddSizedImportedProp(soldierRoot, SpaceKitRoot + "weapon_rifle.fbx", "RifleModel",
            position, 0.16f, 0.58f, new Vector3(0f, Mathf.Pi, 0f), Colors.White))
            return;

        TryAddSizedImportedProp(soldierRoot, SpaceKitRoot + "weapon_gun.fbx", "GunModel",
            position, 0.18f, 0.50f, new Vector3(0f, Mathf.Pi, 0f), Colors.White);
    }

    static void AttachWeaponToInfantry(Node3D model, string unitKey)
    {
        var skeleton = FindSkeleton3D(model);
        if (skeleton is null)
            return;

        string handBoneName = "";
        for (int b = 0; b < skeleton.GetBoneCount(); b++)
        {
            var name = skeleton.GetBoneName(b);
            var lower = name.ToLower();
            if (lower.Contains("righthand") || lower.Contains("right_hand") || lower.Contains("rightweapon") || 
                lower.Contains("hand_r") || lower.Contains("hand.r") || lower.Contains("wrist_r") || lower.Contains("wrist.r"))
            {
                handBoneName = name;
                break;
            }
        }

        if (string.IsNullOrEmpty(handBoneName))
            return;

        string weaponPath = "";
        Vector3 offsetPos = Vector3.Zero;
        Vector3 offsetRot = Vector3.Zero;
        Vector3 scale = Vector3.One;

        if (unitKey == "infantry_artillery")
        {
            // 火箭筒模型 (blaster-o)
            weaponPath = "res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/blaster-o.fbx";
            offsetPos = new Vector3(-0.06f, 0.08f, 0.04f);
            offsetRot = new Vector3(0f, Mathf.Pi * 0.5f, -Mathf.Pi * 0.25f);
            scale = Vector3.One * 0.65f;
        }
        else if (unitKey == "infantry")
        {
            // 步枪模型 (Merrick556)
            weaponPath = "res://assets/unity_migrated/Assets/External/UserModels/Weapons/Merrick556/Merrick556.fbx";
            offsetPos = new Vector3(-0.02f, 0.05f, 0.02f);
            offsetRot = new Vector3(0f, Mathf.Pi * 0.5f, 0f);
            scale = Vector3.One * 0.55f;
        }
        else if (unitKey is "infantry_flamethrower" or "flamethrower")
        {
            // 喷火器模型 (使用 blaster-g)
            weaponPath = "res://assets/unity_migrated/Assets/External/Kenney/BlasterKit/Models/FBX format/blaster-g.fbx";
            offsetPos = new Vector3(-0.04f, 0.06f, 0.03f);
            offsetRot = new Vector3(0f, Mathf.Pi * 0.5f, -Mathf.Pi * 0.15f);
            scale = Vector3.One * 0.65f;
        }

        if (string.IsNullOrEmpty(weaponPath))
            return;

        var weaponScene = ResourceLoader.Load<PackedScene>(weaponPath);
        if (weaponScene is null)
            return;

        var attachment = new BoneAttachment3D
        {
            Name = "WeaponAttachment",
            BoneName = handBoneName
        };
        skeleton.AddChild(attachment);

        var weaponInstance = weaponScene.Instantiate<Node3D>();
        weaponInstance.Name = "WeaponModel";
        weaponInstance.Position = offsetPos;
        weaponInstance.Rotation = offsetRot;
        weaponInstance.Scale = scale;
        attachment.AddChild(weaponInstance);
    }

    static void DisableShadowCasting(Node node)
    {
        if (node is GeometryInstance3D geom)
            geom.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        foreach (var child in node.GetChildren())
            DisableShadowCasting(child);
    }

    static Skeleton3D? FindSkeleton3D(Node root)
    {
        if (root is Skeleton3D sk)
            return sk;
        var found = root.FindChildren("*", "Skeleton3D", true, false);
        return found.Count > 0 ? found[0] as Skeleton3D : null;
    }

    static void AddPackedUnitVisual(RtsUnit unit, PackedScene scene, string name, BattleUnitDefinition def, bool playerOwned)
    {
        var visualRoot = new Node3D
        {
            Name = name,
            Scale = Vector3.One * def.VisualScale * ImportedModelScale(def.Key),
            Position = ImportedModelOffset(def.Key) + ImportedModelFineOffset(def.Key),
            Rotation = ImportedModelRotation(def.Key, playerOwned) + ImportedModelFineRotation(def.Key)
        };

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "Model";
        visualRoot.AddChild(visual);
        CenterImportedModel(visual);

        var tint = playerOwned ? def.Tint : new Color(0.56f, 0.22f, 0.18f);
        if (ShouldTintImportedModel(def.Key))
            TintImportedModel(visualRoot, tint);
        AddFallbackAircraftPropellers(visualRoot, def.Key);

        // Mount dynamic military details onto vehicles to distinguish them

        if (def.Key == "heavy_tank")
        {
            // Add secondary single-barrel defense turret on the heavy tank to make it look extra beefy
            TryAddSizedImportedProp(visualRoot, SpaceKitRoot + "turret_single.fbx", "HeavyDefenseGun",
                new Vector3(0.35f, 0.55f, 0.6f), 0.5f, 0.6f, Vector3.Zero, tint, preserveMaterials: false);
        }

        if (def.Key == "submarine")
        {
            ConfigureSubmarineRenderPriority(visualRoot);
        }

        if (def.Key == "bomber")
        {
            // 隐藏飞行中不需要的放下起落架组件，保留整洁流线型的隐身机身
            foreach (var node in visualRoot.FindChildren("*", "Node3D", true, false))
            {
                var n = node.Name.ToString();
                if (n.Contains("LG", StringComparison.OrdinalIgnoreCase) || n.Contains("LandingOn", StringComparison.OrdinalIgnoreCase))
                {
                    if (node is Node3D n3d)
                        n3d.Visible = false;
                }
            }
        }

        unit.AddChild(visualRoot);
        unit.ConfigureVisualRig(def.Key, BattleUnitCatalog.IsAirUnit(def.Key) ? visualRoot : visual);
    }

    static void ConfigureSubmarineRenderPriority(Node node)
    {
        foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is MeshInstance3D mesh)
            {
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }
    }

    static void AddFallbackAircraftPropellers(Node3D visualRoot, string unitKey)
    {
        if (!BattleUnitCatalog.IsAirUnit(unitKey))
            return;

        if (unitKey == "bomber")
            return;

        AddPropeller(visualRoot, "PropellerNose", new Vector3(0f, 0.36f, -1.22f), unitKey == "scout_plane" ? 0.84f : 0.58f);
    }

    static float ImportedModelScale(string key) => key switch
    {
        "light_tank" => 0.85f,
        "heavy_tank" => 1.10f,
        "artillery" => 42f,
        "scout_plane" => 2.25f,
        "fighter" => 0.58f,
        "bomber" => 0.0105f, // B-2 隐身轰炸机黄金比例翼展
        "aircraft_carrier" => 0.022f, // Downscale the modern carrier model to fit the game
        "destroyer_ship" => 0.03f,
        "battleship" => 0.74f,
        "submarine" => 0.0038f,
        _ => 1f
    };

    static Vector3 ImportedModelOffset(string key) => key switch
    {
        "artillery" => new Vector3(0f, 0.08f, 0f),
        "scout_plane" => new Vector3(0f, 0.08f, 0f),
        "aircraft_carrier" => new Vector3(0f, 0.05f, 0f), // Align slightly above water
        "battleship" => new Vector3(0f, -0.22f, 0f), // Sits naturally in water line
        _ => Vector3.Zero
    };

    static Vector3 ImportedModelRotation(string key, bool playerOwned)
    {
        if (key == "fighter")
            return Vector3.Zero;

        var yaw = key switch
        {
            "tank" or "light_tank" or "heavy_tank" or "anti_air_gun" => Mathf.Pi,
            "artillery" => -Mathf.Pi * 0.5f,
            "scout_plane" => -Mathf.Pi * 0.5f,
            "bomber" => playerOwned ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f,
            "aircraft_carrier" => playerOwned ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f,
            _ => 0f
        };
        return new Vector3(0f, yaw, 0f);
    }

    static bool ShouldTintImportedModel(string key)
        => key is "light_tank" or "heavy_tank" or "artillery";

    static Vector3 ImportedModelFineOffset(string key) => key switch
    {
        "artillery" => new Vector3(0f, 0.12f, 0.10f),
        "heavy_tank" => new Vector3(0f, 0.02f, 0f),
        "light_tank" => new Vector3(0f, 0.02f, 0f),
        _ => Vector3.Zero
    };

    static Vector3 ImportedModelFineRotation(string key) => key switch
    {
        "artillery" => new Vector3(-Mathf.Pi * 0.5f, 0f, 0f),
        _ => Vector3.Zero
    };

    static void CenterImportedModel(Node3D visual)
    {
        if (!TryGetLocalBounds(visual, Transform3D.Identity, out var bounds))
            return;

        var center = bounds.Position + bounds.Size * 0.5f;
        var isSubOrAir = visual.Name.ToString().Contains("sub", StringComparison.OrdinalIgnoreCase)
            || visual.Name.ToString().Contains("bomber", StringComparison.OrdinalIgnoreCase)
            || visual.Name.ToString().Contains("fighter", StringComparison.OrdinalIgnoreCase);
        var offsetY = isSubOrAir ? -center.Y : -bounds.Position.Y;
        var offset = new Vector3(-center.X, offsetY, -center.Z);

        // 移动 visual 的所有子节点，使 3D 建筑 meshes 在 visual 的 (0,0) 局部坐标系下精准居中
        // 保持 visual 本身的 Position 不变，避免与父级 RtsBuilding/RtsUnit 的 SelectionRing、LevelBadge 及 选中环发生偏向错位
        foreach (var child in visual.GetChildren())
        {
            if (child is Node3D child3D)
            {
                child3D.Position += offset;
            }
        }
    }

    static bool TryGetLocalBounds(Node node, Transform3D transform, out Aabb bounds)
    {
        bounds = default;
        var hasBounds = false;

        if (node is MeshInstance3D meshInstance && meshInstance.Mesh is not null)
        {
            bounds = TransformAabb(meshInstance.Mesh.GetAabb(), transform);
            hasBounds = true;
        }

        foreach (var child in node.GetChildren())
        {
            var childTransform = transform;
            if (child is Node3D child3D)
                childTransform = transform * child3D.Transform;

            if (!TryGetLocalBounds(child, childTransform, out var childBounds))
                continue;

            bounds = hasBounds ? bounds.Merge(childBounds) : childBounds;
            hasBounds = true;
        }

        return hasBounds;
    }

    static Aabb TransformAabb(Aabb aabb, Transform3D transform)
    {
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var pos = aabb.Position;
        var size = aabb.Size;
        for (var x = 0; x <= 1; x++)
        for (var y = 0; y <= 1; y++)
        for (var z = 0; z <= 1; z++)
        {
            var corner = pos + new Vector3(size.X * x, size.Y * y, size.Z * z);
            var p = transform * corner;
            min = new Vector3(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y), Mathf.Min(min.Z, p.Z));
            max = new Vector3(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y), Mathf.Max(max.Z, p.Z));
        }

        return new Aabb(min, max - min);
    }

    static void TintImportedModel(Node node, Color tint, float lerpFactor = 0.55f)
    {
        foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is MeshInstance3D mesh)
            {
                var successfullyTintedAny = false;
                var meshObj = mesh.Mesh;
                if (meshObj is not null)
                {
                    var surfCount = meshObj.GetSurfaceCount();
                    for (int s = 0; s < surfCount; s++)
                    {
                        var mat = (mesh.GetActiveMaterial(s) ?? mesh.Mesh.SurfaceGetMaterial(s)) as BaseMaterial3D;
                        if (mat is not null)
                        {
                            var dupMat = mat.Duplicate() as BaseMaterial3D;
                            if (dupMat is not null)
                            {
                                var finalAlpha = dupMat.AlbedoColor.A;
                                if (finalAlpha < 0.1f)
                                {
                                    finalAlpha = 1f;
                                    dupMat.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                                }
                                dupMat.AlbedoColor = new Color(
                                    Mathf.Lerp(dupMat.AlbedoColor.R, tint.R, lerpFactor),
                                    Mathf.Lerp(dupMat.AlbedoColor.G, tint.G, lerpFactor),
                                    Mathf.Lerp(dupMat.AlbedoColor.B, tint.B, lerpFactor),
                                    finalAlpha
                                );
                                if (lerpFactor > 0.60f)
                                {
                                    dupMat.EmissionEnabled = true;
                                    dupMat.Emission = new Color(tint.R * 0.18f, tint.G * 0.18f, tint.B * 0.18f);
                                    dupMat.EmissionEnergyMultiplier = 0.35f;
                                }
                                mesh.SetSurfaceOverrideMaterial(s, dupMat);
                                successfullyTintedAny = true;
                            }
                        }
                    }
                }
                
                if (!successfullyTintedAny)
                {
                    mesh.MaterialOverride = Material(tint, 0.76f);
                }
            }
        }

        if (node is MeshInstance3D self)
            self.MaterialOverride = Material(tint, 0.76f);
    }

    void AddAirUnitVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        var visual = new Node3D { Name = "Airframe" };
        unit.AddChild(visual);
        var tint = playerOwned ? def.Tint : new Color(0.56f, 0.22f, 0.18f);
        AddBlock(visual, "Fuselage", new Vector3(0.72f, 0.34f, def.Key == "bomber" ? 3.8f : 2.8f), Vector3.Zero, tint);
        AddBlock(visual, "Wing", new Vector3(def.Key == "bomber" ? 4.6f : 3.2f, 0.16f, 0.74f), new Vector3(0f, 0f, 0.12f), tint.Lightened(0.08f));
        AddBlock(visual, "Tail", new Vector3(1.4f, 0.12f, 0.58f), new Vector3(0f, 0.1f, 1.35f), tint.Darkened(0.12f));
        AddBlock(visual, "Nose", new Vector3(0.46f, 0.36f, 0.68f), new Vector3(0f, 0f, -1.55f), new Color(0.08f, 0.10f, 0.12f));
        AddPropeller(visual, "Propeller", new Vector3(0f, 0f, -1.96f), def.Key == "bomber" ? 1.05f : 0.82f);
        AddAirShadow(unit);
        unit.ConfigureVisualRig(def.Key, visual);
    }

    static void AddPropeller(Node3D parent, string name, Vector3 position, float radius)
    {
        var prop = new Node3D
        {
            Name = name,
            Position = position
        };
        parent.AddChild(prop);

        prop.AddChild(new MeshInstance3D
        {
            Name = "PropellerBlur",
            Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
            Mesh = new CylinderMesh
            {
                TopRadius = radius * 0.82f,
                BottomRadius = radius * 0.82f,
                Height = 0.018f,
                RadialSegments = 40
            },
            MaterialOverride = PropellerBlurMaterial(new Color(0.72f, 0.80f, 0.82f, 0.24f))
        });
        prop.AddChild(new MeshInstance3D
        {
            Name = "PropellerBladeA",
            Mesh = new BoxMesh { Size = new Vector3(radius * 1.55f, 0.055f, 0.10f) },
            MaterialOverride = PropellerBladeMaterial(new Color(0.08f, 0.09f, 0.09f, 0.82f))
        });
        prop.AddChild(new MeshInstance3D
        {
            Name = "PropellerBladeB",
            Rotation = new Vector3(0f, 0f, Mathf.Pi * 0.5f),
            Mesh = new BoxMesh { Size = new Vector3(radius * 1.55f, 0.055f, 0.10f) },
            MaterialOverride = PropellerBladeMaterial(new Color(0.08f, 0.09f, 0.09f, 0.82f))
        });
        prop.AddChild(new MeshInstance3D
        {
            Name = "PropellerHub",
            Mesh = new SphereMesh { Radius = radius * 0.13f, Height = radius * 0.18f, RadialSegments = 12, Rings = 4 },
            MaterialOverride = Material(new Color(0.16f, 0.17f, 0.17f), 0.46f)
        });
    }

    static void AddAirShadow(RtsUnit unit)
    {
        unit.AddChild(new MeshInstance3D
        {
            Name = "AirShadow",
            Position = new Vector3(0f, -9.3f, 0f),
            Mesh = new CylinderMesh { TopRadius = 1.8f, BottomRadius = 1.8f, Height = 0.035f, RadialSegments = 28 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0f, 0f, 0f, 0.20f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        });
    }

    void AddNavalUnitVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        var tint = playerOwned ? def.Tint : new Color(0.52f, 0.25f, 0.20f);

        if (def.Key == "aircraft_carrier")
        {
            // 真实 3D 尼米兹号航空母舰 GLB 模型 (nimitz.glb) - 拥有 3D 飞行甲板、跑道划线、双弹射器、岛式舰桥与舰载机
            if (TryInstanceScene("res://assets/units/nimitz.glb", "NimitzCarrier") is { } carrier)
            {
                carrier.Scale = Vector3.One * 0.045f;
                carrier.Rotation = new Vector3(0f, playerOwned ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f, 0f);
                carrier.Position = new Vector3(0f, -0.22f, 0f);
                unit.AddChild(carrier);
                unit.ConfigureVisualRig(def.Key, carrier);
                return;
            }
        }

        if (def.Key == "submarine")
        {
            if (unit.GetNodeOrNull("SubmarineVisual") is Node3D existingSub)
            {
                ConfigureSubmarineRenderPriority(existingSub);
                unit.ConfigureVisualRig(def.Key, existingSub);
                return;
            }

            // 优先检测外部网路 3D 潜艇模型文件 (Supported: submarine.fbx, submarine.glb, submarine.gltf, submarine.obj, etc.)
            string[] candidateSubModelPaths = new string[]
            {
                "res://assets/models/submarine.fbx",
                "res://assets/models/submarine.glb",
                "res://assets/models/submarine.gltf",
                "res://assets/models/submarine.obj",
                "res://assets/models/sub.glb",
                "res://assets/models/sub.fbx"
            };

            foreach (var path in candidateSubModelPaths)
            {
                if (ResourceLoader.Exists(path))
                {
                    var scene = GD.Load<PackedScene>(path);
                    if (scene != null)
                    {
                        AddPackedUnitVisual(unit, scene, "SubmarineVisual", def, playerOwned);
                        return;
                    }
                }
            }
            return;
        }

        if (def.Key == "patrol_boat")
        {
            // 仿真 3D 军用隐身导弹巡逻艇 (Fast Stealth Attack Missile Craft)
            var pbRoot = new Node3D { Name = "PatrolBoatVisual", Position = new Vector3(0f, -0.15f, 0f) };
            unit.AddChild(pbRoot);

            var pbMat = new StandardMaterial3D
            {
                AlbedoColor = playerOwned ? new Color(0.22f, 0.30f, 0.38f) : new Color(0.36f, 0.18f, 0.15f),
                Metallic = 0.86f,
                Roughness = 0.26f
            };

            var hullMesh = new MeshInstance3D
            {
                Name = "PatrolBoatHull",
                Mesh = new CapsuleMesh { Radius = 0.68f, Height = 4.8f, RadialSegments = 20, Rings = 6 },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 0.45f, 0f),
                MaterialOverride = pbMat
            };
            pbRoot.AddChild(hullMesh);

            // 驾驶舱隐身上层建筑 (Stealth Cabin)
            var cabin = new MeshInstance3D
            {
                Name = "StealthCabin",
                Mesh = new BoxMesh { Size = new Vector3(1.05f, 0.72f, 1.35f) },
                Position = new Vector3(0f, 0.95f, -0.3f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = pbMat.AlbedoColor.Lightened(0.05f), Metallic = 0.86f, Roughness = 0.26f }
            };
            pbRoot.AddChild(cabin);

            // 舰首 30mm 自动炮 (Front Auto Cannon Turret)
            TryAddSizedImportedProp(pbRoot, SpaceKitRoot + "turret_single.fbx", "FrontTurret",
                new Vector3(0f, 0.85f, -1.7f), 1.1f, 1.3f, Vector3.Zero, pbMat.AlbedoColor, preserveMaterials: false);

            // 搜索雷达与通讯柱 (Modern Radar Post)
            var pbMastTower = new MeshInstance3D
            {
                Name = "PatrolRadarMast",
                Mesh = new BoxMesh { Size = new Vector3(0.25f, 1.25f, 0.25f) },
                Position = new Vector3(0f, 1.45f, -0.3f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.24f, 0.28f), Metallic = 0.85f, Roughness = 0.3f }
            };
            pbRoot.AddChild(pbMastTower);

            unit.ConfigureVisualRig(def.Key, pbRoot);
            return;
        }

        if (def.Key == "destroyer_ship")
        {
            // 优先检测用户放入的外部高精 3D 模型文件 (Supported: udaloy.glb, udaloy.gltf, udaloy.fbx, udaloy.obj, destroyer.glb, etc.)
            string[] candidateModelPaths = new string[]
            {
                "res://assets/models/udaloy.glb",
                "res://assets/models/udaloy.gltf",
                "res://assets/models/udaloy.fbx",
                "res://assets/models/udaloy.obj",
                "res://assets/models/destroyer.glb",
                "res://assets/models/destroyer.gltf",
                "res://assets/models/destroyer.fbx",
                "res://assets/models/destroyer.obj"
            };

            foreach (var path in candidateModelPaths)
            {
                if (Godot.FileAccess.FileExists(path) && ResourceLoader.Exists(path))
                {
                    var scene = GD.Load<PackedScene>(path);
                    if (scene != null)
                    {
                        AddPackedUnitVisual(unit, scene, "DestroyerVisual", def, playerOwned);
                        return;
                    }
                }
            }

            // 高精 3D 勇敢级大型导弹驱逐舰 (Project 1155 Udaloy-I Class Anti-Submarine Destroyer)
            // 艇体浮水高度升至 Y = 0.45m，全盘展示完整的侧舷高企舰首与水线下方红色防污底球鼻艏
            var desRoot = new Node3D { Name = "DestroyerVisual", Position = new Vector3(0f, 0.45f, 0f) };
            unit.AddChild(desRoot);

            ArrayMesh CreateUdaloyClipperHullMesh()
            {
                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);

                // Z: -4.8f (high flared clipper bow) -> +4.1f (transom stern)
                Vector3 pBowTip = new Vector3(0f, 0.95f, -4.8f);
                Vector3 pBowKeel = new Vector3(0f, -0.38f, -3.6f);

                Vector3 pDeckL1 = new Vector3(-0.70f, 0.82f, -3.0f);
                Vector3 pDeckR1 = new Vector3(0.70f, 0.82f, -3.0f);
                Vector3 pDeckL2 = new Vector3(-1.02f, 0.78f, 0.0f);
                Vector3 pDeckR2 = new Vector3(1.02f, 0.78f, 0.0f);
                Vector3 pDeckL3 = new Vector3(-0.95f, 0.78f, 4.1f);
                Vector3 pDeckR3 = new Vector3(0.95f, 0.78f, 4.1f);

                Vector3 pWaterL1 = new Vector3(-0.50f, 0.18f, -3.0f);
                Vector3 pWaterR1 = new Vector3(0.50f, 0.18f, -3.0f);
                Vector3 pWaterL2 = new Vector3(-0.85f, 0.18f, 0.0f);
                Vector3 pWaterR2 = new Vector3(0.85f, 0.18f, 0.0f);
                Vector3 pWaterL3 = new Vector3(-0.80f, 0.18f, 4.1f);
                Vector3 pWaterR3 = new Vector3(0.80f, 0.18f, 4.1f);

                Vector3 pKeelMid = new Vector3(0f, -0.48f, 0.2f);
                Vector3 pKeelStern = new Vector3(0f, -0.32f, 4.1f);

                void AddFlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
                {
                    Vector3 norm = (b - a).Cross(c - a).Normalized();
                    st.SetNormal(norm); st.AddVertex(a);
                    st.SetNormal(norm); st.AddVertex(b);
                    st.SetNormal(norm); st.AddVertex(c);

                    st.SetNormal(norm); st.AddVertex(a);
                    st.SetNormal(norm); st.AddVertex(c);
                    st.SetNormal(norm); st.AddVertex(d);
                }

                void AddFlatTri(Vector3 a, Vector3 b, Vector3 c)
                {
                    Vector3 norm = (b - a).Cross(c - a).Normalized();
                    st.SetNormal(norm); st.AddVertex(a);
                    st.SetNormal(norm); st.AddVertex(b);
                    st.SetNormal(norm); st.AddVertex(c);
                }

                // 甲板 Forecastle Deck & Main Deck
                AddFlatTri(pBowTip, pDeckL1, pDeckR1);
                AddFlatQuad(pDeckL1, pDeckL2, pDeckR2, pDeckR1);
                AddFlatQuad(pDeckL2, pDeckL3, pDeckR3, pDeckR2);

                // 弧形高企外飘舰首 Clipper Bow Sides
                AddFlatTri(pBowTip, pWaterL1, pDeckL1);
                AddFlatTri(pBowTip, pDeckR1, pWaterR1);
                AddFlatTri(pBowTip, pBowKeel, pWaterL1);
                AddFlatTri(pBowTip, pWaterR1, pBowKeel);

                // 上舷边板 Steel Hull Sides
                AddFlatQuad(pDeckL1, pWaterL1, pWaterL2, pDeckL2);
                AddFlatQuad(pDeckR1, pDeckR2, pWaterR2, pWaterR1);
                AddFlatQuad(pDeckL2, pWaterL2, pWaterL3, pDeckL3);
                AddFlatQuad(pDeckR2, pDeckR3, pWaterR3, pWaterR2);

                // 水下 V 型防摇深龙骨 V Keel
                AddFlatQuad(pWaterL1, pBowKeel, pKeelMid, pWaterL2);
                AddFlatQuad(pWaterR1, pWaterR2, pKeelMid, pBowKeel);
                AddFlatQuad(pWaterL2, pKeelMid, pKeelStern, pWaterL3);
                AddFlatQuad(pWaterR2, pWaterR3, pKeelStern, pKeelMid);

                // 舰尾艉板 Transom Stern
                AddFlatQuad(pDeckL3, pDeckR3, pWaterR3, pWaterL3);
                AddFlatTri(pWaterL3, pWaterR3, pKeelStern);

                return st.Commit();
            }

            var udaloyMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.68f, 0.72f, 0.76f), // 军用浅灰舰体 (1:1 完美对齐用户参考图)
                Metallic = 0.05f,
                Roughness = 0.40f
            };

            var navalGrayMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.62f, 0.66f, 0.70f), // 上层结构与武器明亮军舰灰
                Metallic = 0.08f,
                Roughness = 0.35f
            };

            var redKeelMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.65f, 0.18f, 0.15f), // 1:1 红色水线防污底龙骨
                Metallic = 0.0f,
                Roughness = 0.50f
            };

            var darkCapMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.24f, 0.26f, 0.28f), // 烟囱顶口黑色隔热涂层
                Metallic = 0.15f,
                Roughness = 0.40f
            };

            // 1. 勇敢级 (Udaloy-I) 剪刀型弧形舰首主舰体 (High Clipper Bow Hull)
            var hullMesh = new MeshInstance3D
            {
                Name = "UdaloyHullMesh",
                Mesh = CreateUdaloyClipperHullMesh(),
                Position = Vector3.Zero,
                MaterialOverride = udaloyMat
            };
            desRoot.AddChild(hullMesh);

            // 1.5. 水线下方红色防污底龙骨底装甲与舰首球鼻艏 (Red Keel Line & Bulbous Bow - 1:1 对齐用户参考图)
            var redKeelPlate = new MeshInstance3D
            {
                Name = "UdaloyRedKeelLine",
                Mesh = new BoxMesh { Size = new Vector3(1.72f, 0.24f, 8.2f) },
                Position = new Vector3(0f, -0.22f, 0.2f),
                MaterialOverride = redKeelMat
            };
            desRoot.AddChild(redKeelPlate);

            var bulbousBow = new MeshInstance3D
            {
                Name = "UdaloyBulbousBow",
                Mesh = new SphereMesh { Radius = 0.42f, Height = 1.15f },
                Position = new Vector3(0f, -0.26f, -4.25f),
                MaterialOverride = redKeelMat
            };
            desRoot.AddChild(bulbousBow);

            // 2. 双座 AK-100 100mm 前部主炮塔 (Twin Bow AK-100 100mm Naval Guns)
            // 下层一号炮塔 Lower Front Gun
            TryAddSizedImportedProp(desRoot, SpaceKitRoot + "turret_single.fbx", "FrontAK100_A",
                new Vector3(0f, 0.85f, -3.4f), 1.35f, 1.5f, Vector3.Zero, udaloyMat.AlbedoColor, preserveMaterials: false);

            var barrelA = new MeshInstance3D
            {
                Name = "AK100_Barrel_A",
                Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.065f, Height = 1.35f },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 1.05f, -3.95f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(barrelA);

            // 背负式高位二号炮塔 Elevated Superfiring Gun
            TryAddSizedImportedProp(desRoot, SpaceKitRoot + "turret_single.fbx", "FrontAK100_B",
                new Vector3(0f, 1.25f, -2.4f), 1.35f, 1.5f, Vector3.Zero, udaloyMat.AlbedoColor, preserveMaterials: false);

            var barrelB = new MeshInstance3D
            {
                Name = "AK100_Barrel_B",
                Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.065f, Height = 1.35f },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 1.45f, -2.95f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(barrelB);

            // 3. SS-N-14 / Rastrub 左右舷 4 联装大型反潜导弹箱 (Port & Starboard Quad Missile Boxes)
            var quadBoxPort = new MeshInstance3D
            {
                Name = "QuadRastrubPort",
                Mesh = new BoxMesh { Size = new Vector3(0.75f, 0.65f, 1.85f) },
                Position = new Vector3(-0.92f, 1.40f, -1.2f),
                Rotation = new Vector3(Mathf.DegToRad(-8f), Mathf.DegToRad(-10f), 0f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(quadBoxPort);

            var quadBoxStarboard = new MeshInstance3D
            {
                Name = "QuadRastrubStarboard",
                Mesh = new BoxMesh { Size = new Vector3(0.75f, 0.65f, 1.85f) },
                Position = new Vector3(0.92f, 1.40f, -1.2f),
                Rotation = new Vector3(Mathf.DegToRad(-8f), Mathf.DegToRad(10f), 0f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(quadBoxStarboard);

            // 4. 舰首 RBU-6000 12 管反潜火箭深弹发射器 (RBU-6000 Anti-Submarine Rocket Launchers)
            var rbuPort = new MeshInstance3D
            {
                Name = "RBU6000_Port",
                Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.25f, Height = 0.35f },
                Position = new Vector3(-0.45f, 0.95f, -4.1f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(rbuPort);

            var rbuStarboard = new MeshInstance3D
            {
                Name = "RBU6000_Starboard",
                Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.25f, Height = 0.35f },
                Position = new Vector3(0.45f, 0.95f, -4.1f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(rbuStarboard);

            // 5. 勇敢级多层相控雷达司令舰桥 (Superstructure Command Bridge)
            var bridgeMain = new MeshInstance3D
            {
                Name = "UdaloyBridge",
                Mesh = new BoxMesh { Size = new Vector3(1.35f, 1.25f, 2.4f) },
                Position = new Vector3(0f, 1.55f, -0.4f),
                MaterialOverride = udaloyMat
            };
            desRoot.AddChild(bridgeMain);

            // 6. 勇敢级双烟囱排气塔 (Twin Exhaust Funnel Stacks)
            var funnelFore = new MeshInstance3D
            {
                Name = "UdaloyFunnelFore",
                Mesh = new BoxMesh { Size = new Vector3(0.85f, 1.15f, 0.95f) },
                Position = new Vector3(0f, 2.35f, 0.6f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(funnelFore);

            var funnelForeCap = new MeshInstance3D
            {
                Name = "UdaloyFunnelForeCap",
                Mesh = new BoxMesh { Size = new Vector3(0.88f, 0.15f, 0.98f) },
                Position = new Vector3(0f, 2.95f, 0.6f),
                MaterialOverride = darkCapMat
            };
            desRoot.AddChild(funnelForeCap);

            var funnelAft = new MeshInstance3D
            {
                Name = "UdaloyFunnelAft",
                Mesh = new BoxMesh { Size = new Vector3(0.85f, 1.15f, 0.95f) },
                Position = new Vector3(0f, 2.35f, 1.8f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(funnelAft);

            var funnelAftCap = new MeshInstance3D
            {
                Name = "UdaloyFunnelAftCap",
                Mesh = new BoxMesh { Size = new Vector3(0.88f, 0.15f, 0.98f) },
                Position = new Vector3(0f, 2.95f, 1.8f),
                MaterialOverride = darkCapMat
            };
            desRoot.AddChild(funnelAftCap);

            // 7. 高耸双雷达桁架主桅杆 (Front & Aft Lattice Masts - 1:1 对齐用户参考图)
            var mainMast = new MeshInstance3D
            {
                Name = "UdaloyMainMast",
                Mesh = new PrismMesh { Size = new Vector3(0.55f, 2.15f, 0.55f) },
                Position = new Vector3(0f, 3.25f, -0.2f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(mainMast);

            var aftMast = new MeshInstance3D
            {
                Name = "UdaloyAftLatticeMast",
                Mesh = new PrismMesh { Size = new Vector3(0.42f, 1.85f, 0.42f) },
                Position = new Vector3(0f, 3.10f, 1.2f),
                MaterialOverride = navalGrayMat
            };
            desRoot.AddChild(aftMast);

            TryAddSizedImportedProp(desRoot, SpaceKitRoot + "satelliteDish.fbx", "UdaloyTopPlateRadar",
                new Vector3(0f, 4.35f, -0.2f), 1.9f, 1.2f, Vector3.Zero,
                navalGrayMat.AlbedoColor, preserveMaterials: false);

            // 8. 舰尾双 Ka-27 反潜直升机机库与起降甲板 (Twin Ka-27 Helicopter Hangar & Flight Deck)
            var twinHangar = new MeshInstance3D
            {
                Name = "UdaloyTwinHangar",
                Mesh = new BoxMesh { Size = new Vector3(1.45f, 1.05f, 1.35f) },
                Position = new Vector3(0f, 1.35f, 2.85f),
                MaterialOverride = udaloyMat
            };
            desRoot.AddChild(twinHangar);

            unit.ConfigureVisualRig(def.Key, desRoot);
            return;
        }

        if (def.Key == "battleship")
        {
            string[] candidateModelPaths = new string[]
            {
                "res://assets/models/battleship.glb",
                "res://assets/models/battleship.gltf",
                "res://assets/models/battleship.fbx",
                "res://assets/models/battleship.obj",
                "res://assets/models/iowa.glb",
                "res://assets/models/iowa.gltf",
                "res://assets/models/iowa.fbx",
                "res://assets/models/iowa.obj"
            };

            foreach (var path in candidateModelPaths)
            {
                if (Godot.FileAccess.FileExists(path) && ResourceLoader.Exists(path))
                {
                    var scene = GD.Load<PackedScene>(path);
                    if (scene != null)
                    {
                        AddPackedUnitVisual(unit, scene, "BattleshipVisual", def, playerOwned);
                        return;
                    }
                }
            }
            return;
        }

        if (def.Key == "transport_ship")
        {
            // 仿真 3D 两栖登陆运输舰 (Amphibious Transport Dock)
            var transRoot = new Node3D { Name = "TransportVisual", Position = new Vector3(0f, -0.15f, 0f) };
            unit.AddChild(transRoot);

            var transMat = new StandardMaterial3D
            {
                AlbedoColor = playerOwned ? new Color(0.24f, 0.32f, 0.36f) : new Color(0.38f, 0.20f, 0.16f),
                Metallic = 0.84f,
                Roughness = 0.30f
            };

            // 运输舰重型装甲舰体 (Heavy Steel Transport Hull)
            var hullMesh = new MeshInstance3D
            {
                Name = "TransportHull",
                Mesh = new CapsuleMesh { Radius = 1.12f, Height = 6.8f, RadialSegments = 24, Rings = 8 },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 0.55f, 0f),
                MaterialOverride = transMat
            };
            transRoot.AddChild(hullMesh);

            // 上层装甲司令塔 (Armored Tower)
            var tower = new MeshInstance3D
            {
                Name = "TransportTower",
                Mesh = new BoxMesh { Size = new Vector3(1.35f, 1.25f, 1.8f) },
                Position = new Vector3(0f, 1.45f, -1.2f),
                MaterialOverride = transMat
            };
            transRoot.AddChild(tower);

            // 军用集装箱甲板货运 (Military Cargo Containers on Deck)
            TryAddSizedImportedProp(transRoot, MilitaryFbxRoot + "crate.fbx", "CargoCrateA",
                new Vector3(-0.45f, 1.05f, 0.2f), 1.1f, 1.1f, new Vector3(0f, Mathf.DegToRad(15f), 0f), new Color(0.28f, 0.32f, 0.28f), preserveMaterials: true);
            TryAddSizedImportedProp(transRoot, MilitaryFbxRoot + "crate.fbx", "CargoCrateB",
                new Vector3(0.45f, 1.05f, 0.4f), 1.1f, 1.1f, new Vector3(0f, Mathf.DegToRad(-10f), 0f), new Color(0.30f, 0.34f, 0.30f), preserveMaterials: true);

            // 舰首防空自卫炮塔 (Front Defense Gun)
            TryAddSizedImportedProp(transRoot, SpaceKitRoot + "turret_single.fbx", "FrontDefenseTurret",
                new Vector3(0f, 1.05f, -2.7f), 1.0f, 1.2f, Vector3.Zero, transMat.AlbedoColor, preserveMaterials: false);

            unit.ConfigureVisualRig(def.Key, transRoot);
            return;
        }

        if (def.Key == "aircraft_carrier")
        {
            // 仿真 3D 重型航空母舰 (Super Carrier)
            var cvRoot = new Node3D { Name = "CarrierVisual", Position = new Vector3(0f, -0.15f, 0f) };
            unit.AddChild(cvRoot);

            var cvMat = new StandardMaterial3D
            {
                AlbedoColor = playerOwned ? new Color(0.20f, 0.28f, 0.35f) : new Color(0.38f, 0.18f, 0.16f),
                Metallic = 0.86f,
                Roughness = 0.25f
            };
            var deckMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.16f, 0.18f, 0.22f),
                Metallic = 0.35f,
                Roughness = 0.55f
            };

            // 巨型航母装甲主舰体 (Heavy Steel Carrier Hull)
            var cvHull = new MeshInstance3D
            {
                Name = "CarrierHull",
                Mesh = new CapsuleMesh { Radius = 1.35f, Height = 8.5f, RadialSegments = 24, Rings = 8 },
                Rotation = new Vector3(Mathf.Pi * 0.5f, 0f, 0f),
                Position = new Vector3(0f, 0.65f, 0f),
                MaterialOverride = cvMat
            };
            cvRoot.AddChild(cvHull);

            // 宽幅斜角飞行甲板 (Angled Flight Deck)
            var cvDeck = new MeshInstance3D
            {
                Name = "CarrierFlightDeck",
                Mesh = new BoxMesh { Size = new Vector3(2.8f, 0.22f, 8.8f) },
                Position = new Vector3(0f, 1.32f, 0f),
                MaterialOverride = deckMat
            };
            cvRoot.AddChild(cvDeck);

            // 甲板跑道引导线 (Yellow Runway Stripe)
            var cvStripe = new MeshInstance3D
            {
                Name = "CarrierRunwayStripe",
                Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.24f, 7.2f) },
                Position = new Vector3(-0.35f, 1.33f, 0f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.85f, 0.38f) }
            };
            cvRoot.AddChild(cvStripe);

            // 右舷舰岛 (Starboard Island Superstructure)
            var cvIsland = new MeshInstance3D
            {
                Name = "CarrierIsland",
                Mesh = new BoxMesh { Size = new Vector3(0.72f, 1.35f, 1.8f) },
                Position = new Vector3(1.05f, 2.05f, -0.6f),
                MaterialOverride = cvMat
            };
            cvRoot.AddChild(cvIsland);

            // 舰岛相控阵雷达 (Carrier Phased-Array Radar)
            TryAddSizedImportedProp(cvRoot, SpaceKitRoot + "satelliteDish_detailed.fbx", "CarrierRadar",
                new Vector3(1.05f, 2.85f, -0.6f), 1.3f, 1.0f, Vector3.Zero, new Color(0.75f, 0.80f, 0.85f), preserveMaterials: false);

            // 舰岛主通信塔 (Carrier Island Mast Tower)
            var cvMastTower = new MeshInstance3D
            {
                Name = "CarrierIslandMast",
                Mesh = new BoxMesh { Size = new Vector3(0.45f, 1.95f, 0.45f) },
                Position = new Vector3(1.05f, 2.65f, -1.2f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.24f, 0.28f), Metallic = 0.85f, Roughness = 0.3f }
            };
            cvRoot.AddChild(cvMastTower);

            // 甲板停放舰载战斗机 (Parked Carrier Jet Fighters)
            TryAddSizedImportedProp(cvRoot, SpaceKitRoot + "craft_cargoA.fbx", "DeckJetA",
                new Vector3(-0.65f, 1.48f, 2.2f), 1.1f, 1.8f, new Vector3(0f, Mathf.DegToRad(25f), 0f), cvMat.AlbedoColor.Lightened(0.12f), preserveMaterials: false);
            TryAddSizedImportedProp(cvRoot, SpaceKitRoot + "craft_cargoB.fbx", "DeckJetB",
                new Vector3(0.55f, 1.48f, 3.0f), 1.1f, 1.8f, new Vector3(0f, Mathf.DegToRad(-15f), 0f), cvMat.AlbedoColor.Lightened(0.08f), preserveMaterials: false);

            // 防空近防炮 (CIWS / Double Defense Turret)
            TryAddSizedImportedProp(cvRoot, SpaceKitRoot + "turret_double.fbx", "CarrierCIWS",
                new Vector3(-1.30f, 1.28f, -2.8f), 0.95f, 1.1f, Vector3.Zero, cvMat.AlbedoColor, preserveMaterials: false);

            unit.ConfigureVisualRig(def.Key, cvRoot);
            return;
        }

        var visual = new Node3D { Name = "ShipHull", Position = new Vector3(0f, -0.40f, 0f) };
        unit.AddChild(visual);
        var tintFallback = playerOwned ? def.Tint : new Color(0.48f, 0.20f, 0.16f);
        var length = def.Key is "transport_ship" or "aircraft_carrier" ? 5.2f : def.Key == "destroyer_ship" ? 3.8f : 2.7f;
        AddBlock(visual, "Hull", new Vector3(1.1f, 0.55f, length), new Vector3(0f, 0.38f, 0f), tintFallback);
        if (def.Key == "aircraft_carrier")
        {
            // 简易飞行甲板 fallback
            AddBlock(visual, "FlightDeck", new Vector3(2.0f, 0.10f, length), new Vector3(0f, 0.82f, 0f), tintFallback.Lightened(0.15f));
            AddBlock(visual, "Island", new Vector3(0.48f, 0.9f, 1.4f), new Vector3(0.8f, 1.2f, -0.8f), tintFallback.Darkened(0.1f));
        }
        else
        {
            AddBlock(visual, "Deck", new Vector3(0.76f, 0.42f, length * 0.45f), new Vector3(0f, 0.86f, -0.25f), tintFallback.Lightened(0.08f));
            if (BattleUnitCatalog.IsArtilleryLike(def.Key))
                AddImportedNavalForwardGun(visual, length);
        }
        AddBlock(visual, "Wake", new Vector3(1.9f, 0.04f, length * 0.82f), new Vector3(0f, 0.05f, length * 0.18f), new Color(0.62f, 0.82f, 0.86f, 0.42f));
    }

    static void AddImportedNavalForwardGun(Node3D visual, float hullLength)
    {
        var position = new Vector3(0f, 1.08f, -hullLength * 0.52f);
        if (TryAddSizedImportedProp(visual, MilitaryFbxRoot + "cannon.fbx", "ForwardCannon",
            position, 0.58f, 1.35f, new Vector3(0f, Mathf.Pi, 0f), new Color(0.18f, 0.19f, 0.17f)))
            return;

        TryAddSizedImportedProp(visual, TowerDefenseFbxRoot + "weapon-cannon.fbx", "ForwardCannonAlt",
            position, 0.62f, 1.35f, Vector3.Zero, new Color(0.18f, 0.19f, 0.17f));
    }

    void AddUnitRoleMarker(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        _ = unit;
        _ = def;
        _ = playerOwned;
    }

    void EnsureBuildingVisual(RtsBuilding building, BattleBuildingDefinition def)
    {
        // 隐藏编辑器预置的占位白色立方体网格
        foreach (var child in building.GetChildren())
        {
            if (child is MeshInstance3D mi && mi.Name == "Mesh")
            {
                mi.Visible = false;
            }
        }

        if (building.GetNodeOrNull<Node3D>("BuildingVisual") is { } existingVisual)
        {
            var toRemove = new List<Node>();
            foreach (var child in existingVisual.GetChildren())
            {
                if (child is Node3D child3D)
                {
                    if (child3D.Name.ToString().Contains("Tower") || child3D.Name.ToString().Contains("Radar") || child3D.Name.ToString().Contains("Antenna"))
                    {
                        toRemove.Add(child3D);
                    }
                    else if (child3D.Name.ToString().Contains("Hall") || child3D.Name.ToString().Contains("Base") || child3D.Name.ToString().Contains("Main Building"))
                    {
                        FitImportedNode(child3D, def.IsMainBase ? 5.2f : 4.8f, def.IsMainBase ? 6.4f : 5.2f);
                    }
                }
            }
            foreach (var n in toRemove)
            {
                existingVisual.RemoveChild(n);
                n.QueueFree();
            }
            CenterImportedModel(existingVisual);
            building.UpdateVisualsForState();
            return;
        }

        if (building.GetNodeOrNull<MeshInstance3D>("Mesh") is { } oldMesh)
            oldMesh.Visible = false;

        var key = def.Key;
        var isMechanical = key.StartsWith("int_");
        if (isMechanical)
        {
            key = key.Substring(4);
        }

        var visualY = 0.16f;
        // 针对不同 FBX 建筑模型的底部基础面高度进行微调，防止建筑悬空或者陷入泥土中
        if (key == "main_base")
            visualY = 0.82f;
        else if (key == "tank_factory" || key == "armor_factory" || key == "air_factory" || key == "airfield")
            visualY = 0.45f;
        else if (key == "barracks")
            visualY = isMechanical ? 0.32f : 0.92f;
        else if (key == "power_plant" || key == "gold_mine" || key == "turret" || key == "naval_yard")
            visualY = 0.32f;

        var visual = new Node3D { Name = "BuildingVisual", Position = new Vector3(0f, visualY, 0f) };
        building.AddChild(visual);
        
        var tint = isMechanical ? new Color(0.2f, 0.75f, 1.0f) : def.Tint;

        if (isMechanical)
        {
            switch (key)
            {
                case "barracks":
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_smallB.fbx", "MechBarracksDome", Vector3.Zero, 4.2f, 5.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_generator.fbx", "MechBarracksPower", new Vector3(-2.0f, 0f, 1.8f), 2.2f, 2.0f, Vector3.Zero, tint.Lightened(0.1f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "barrel.fbx", "MechBarracksCylinder", new Vector3(2.0f, 0f, 1.6f), 1.4f, 1.2f, Vector3.Zero, new Color(0.18f, 0.22f, 0.25f));
                    break;
                case "tank_factory":
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_largeA.fbx", "MechTankFactoryHangar", Vector3.Zero, 4.8f, 8.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_generatorLarge.fbx", "MechTankFactoryCore", new Vector3(2.7f, 0f, 2.4f), 3.2f, 3.0f, Vector3.Zero, new Color(0.18f, 0.22f, 0.25f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wirelessCable.fbx", "MechTankFactoryDish", new Vector3(-2.8f, 0f, -1.6f), 3.8f, 2.2f, Vector3.Zero, tint.Lightened(0.15f));
                    break;
                case "armor_factory":
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_largeB.fbx", "MechArmorFactoryHangar", Vector3.Zero, 4.8f, 8.0f, new Vector3(0f, Mathf.DegToRad(90f), 0f), tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_barrelLarge.fbx", "MechArmorFactoryTankA", new Vector3(-2.5f, 0f, -2.0f), 2.5f, 2.0f, Vector3.Zero, new Color(0.18f, 0.22f, 0.25f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_barrelLarge.fbx", "MechArmorFactoryTankB", new Vector3(2.5f, 0f, 2.0f), 2.5f, 2.0f, Vector3.Zero, new Color(0.18f, 0.22f, 0.25f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish.fbx", "MechArmorFactoryRadar", new Vector3(0f, 0f, 2.2f), 3.4f, 2.2f, Vector3.Zero, tint.Lightened(0.08f));
                    break;
                case "airfield":
                    AddBlock(visual, "RunwayPad", new Vector3(8.8f, 0.16f, 9.6f), new Vector3(0f, 0.08f, 0f), new Color(0.12f, 0.18f, 0.22f));
                    AddBlock(visual, "RunwayGlowLine", new Vector3(0.36f, 0.18f, 8.2f), new Vector3(0f, 0.1f, 0f), tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wireless.fbx", "MechAirfieldDish", new Vector3(-3.1f, 0f, 2.6f), 4.8f, 2.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "craft_cargoB.fbx", "MechAirfieldFighter", new Vector3(1.6f, 0.18f, -1.4f), 1.5f, 2.8f, new Vector3(0f, Mathf.DegToRad(90f), 0f), tint.Lightened(0.1f));
                    break;
                case "air_factory":
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_roundA.fbx", "MechAirFactoryHangar", Vector3.Zero, 4.8f, 8.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish_large.fbx", "MechAirFactoryRadar", new Vector3(2.8f, 0f, -2.0f), 4.2f, 2.6f, Vector3.Zero, tint.Lightened(0.1f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wireless.fbx", "MechAirFactoryTower", new Vector3(-2.8f, 0f, 2.2f), 3.8f, 1.8f, Vector3.Zero, new Color(0.22f, 0.25f, 0.28f));
                    break;
                case "naval_yard":
                    AddSizedImportedProp(visual, SpaceKitRoot + "platform_large.fbx", "MechDockPlatform", new Vector3(0f, 0.02f, 0f), 2.2f, 8.8f, Vector3.Zero, tint);
                    AddBlock(visual, "DockWater", new Vector3(5.4f, 0.08f, 6.6f), new Vector3(0f, 0.08f, 0f), new Color(0.08f, 0.22f, 0.32f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_smallA.fbx", "MechDockHangar", new Vector3(-2.6f, 0f, 1.8f), 3.4f, 3.8f, new Vector3(0f, Mathf.DegToRad(90f), 0f), tint);
                    break;
                case "turret":
                    AddSizedImportedProp(visual, SpaceKitRoot + "turret_double.fbx", "MechTurretGun", new Vector3(0f, 2.2f, 0f), 2.2f, 3.2f, Vector3.Zero, tint);
                    AddCylinder(visual, "TurretBase", 1.45f, 2.4f, new Vector3(0f, 1.2f, 0f), new Color(0.18f, 0.22f, 0.25f));
                    break;
                case "power_plant":
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_generatorLarge.fbx", "MechPlantReactor", Vector3.Zero, 4.8f, 5.0f, Vector3.Zero, new Color(0.18f, 0.22f, 0.25f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "rock_crystalsLargeB.fbx", "MechPlantCrystalA", new Vector3(-1.8f, 0.1f, 1.8f), 3.2f, 2.2f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "rock_crystalsLargeB.fbx", "MechPlantCrystalB", new Vector3(1.8f, 0.1f, -1.8f), 3.2f, 2.2f, Vector3.Zero, tint);
                    break;
                case "gold_mine":
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_generator.fbx", "MechMineDrill", Vector3.Zero, 4.2f, 4.2f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "rock_crystals.fbx", "MechMineCrystals", new Vector3(0f, 0.1f, -1.8f), 2.5f, 3.0f, Vector3.Zero, new Color(0.95f, 0.78f, 0.24f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wireless.fbx", "MechMineUplink", new Vector3(2.0f, 0f, 1.6f), 3.2f, 1.6f, Vector3.Zero, tint.Lightened(0.12f));
                    break;
                default:
                    AddSizedImportedProp(visual, SpaceKitRoot + "hangar_roundGlass.fbx", "MechCommandDome", Vector3.Zero, 5.2f, 6.4f, Vector3.Zero, tint);
                    break;
            }
        }
        else
        {
            switch (key)
            {
                case "barracks":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-c.fbx", "BarracksMainBuilding", Vector3.Zero, 4.8f, 5.2f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, MilitaryFbxRoot + "crate.fbx", "BarracksCrateA", new Vector3(-2.2f, 0f, 1.8f), 0.9f, 1.1f, new Vector3(0f, Mathf.DegToRad(22f), 0f), new Color(0.44f, 0.34f, 0.22f));
                    AddSizedImportedProp(visual, MilitaryFbxRoot + "crate-bottles.fbx", "BarracksCrateB", new Vector3(2.0f, 0f, 1.6f), 1.1f, 1.3f, new Vector3(0f, Mathf.DegToRad(-18f), 0f), new Color(0.42f, 0.32f, 0.20f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wirelessCable.fbx", "BarracksAntenna", new Vector3(-2.2f, 0f, -1.8f), 3.0f, 1.8f, Vector3.Zero, tint.Lightened(0.1f));
                    break;
                case "tank_factory":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-b.fbx", "TankFactoryHall", Vector3.Zero, 4.8f, 7.8f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, FactoryKitRoot + "conveyor-long.fbx", "TankFactoryConveyor", new Vector3(0f, 1.35f, 2.4f), 1.2f, 5.6f, new Vector3(0f, Mathf.DegToRad(90f), 0f), new Color(0.44f, 0.48f, 0.42f));
                    AddSizedImportedProp(visual, FactoryKitRoot + "crane.fbx", "TankFactoryCrane", new Vector3(-2.8f, 0f, -1.6f), 5.2f, 3.8f, new Vector3(0f, Mathf.DegToRad(-90f), 0f), new Color(0.62f, 0.52f, 0.20f));
                    AddSizedImportedProp(visual, CityIndustrialRoot + "chimney-medium.fbx", "TankFactoryChimney", new Vector3(2.7f, 0f, 2.4f), 4.0f, 2.2f, Vector3.Zero, new Color(0.26f, 0.28f, 0.28f));
                    break;
                case "armor_factory":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-m.fbx", "ArmorFactoryHall", Vector3.Zero, 5.0f, 8.4f, new Vector3(0f, Mathf.DegToRad(90f), 0f), tint);
                    AddSizedImportedProp(visual, FactoryKitRoot + "machine-fortified.fbx", "ArmorFactoryMachine", new Vector3(0f, 0f, 1.9f), 2.0f, 3.8f, Vector3.Zero, new Color(0.34f, 0.36f, 0.38f));
                    AddSizedImportedProp(visual, FactoryKitRoot + "crane-magnet.fbx", "ArmorFactoryCrane", new Vector3(-2.9f, 0f, -2.0f), 4.9f, 3.2f, new Vector3(0f, Mathf.DegToRad(-90f), 0f), new Color(0.60f, 0.48f, 0.20f));
                    AddSizedImportedProp(visual, CityIndustrialRoot + "detail-tank.fbx", "ArmorFactoryTank", new Vector3(2.2f, 0f, 2.1f), 1.6f, 2.8f, new Vector3(0f, Mathf.DegToRad(40f), 0f), new Color(0.36f, 0.42f, 0.30f));
                    break;
                case "airfield":
                    AddBlock(visual, "Runway", new Vector3(8.8f, 0.16f, 9.6f), new Vector3(0f, 0.08f, 0f), new Color(0.10f, 0.12f, 0.13f));
                    AddBlock(visual, "RunwayStripe", new Vector3(0.28f, 0.18f, 7.6f), new Vector3(0f, 0.2f, 0f), new Color(0.86f, 0.82f, 0.62f));
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-h.fbx", "AirfieldTowerBase", new Vector3(-3.1f, 0f, 2.6f), 4.2f, 2.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish.fbx", "AirfieldRadar", new Vector3(-3.1f, 4.3f, 2.6f), 1.2f, 1.2f, new Vector3(0f, Mathf.DegToRad(-25f), 0f), new Color(0.65f, 0.70f, 0.75f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "craft_cargoA.fbx", "AirfieldParkedCraft", new Vector3(1.6f, 0.18f, -1.4f), 1.4f, 2.6f, new Vector3(0f, Mathf.DegToRad(90f), 0f), new Color(0.46f, 0.48f, 0.50f));
                    break;
                case "air_factory":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-r.fbx", "AirFactoryHangar", Vector3.Zero, 4.8f, 8.0f, new Vector3(0f, Mathf.DegToRad(90f), 0f), tint);
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-o.fbx", "AirFactoryOffice", new Vector3(2.7f, 0f, 2.2f), 2.8f, 2.6f, new Vector3(0f, Mathf.DegToRad(180f), 0f), tint.Lightened(0.08f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish.fbx", "AirFactoryDish", new Vector3(-2.8f, 0f, -2.0f), 3.0f, 2.0f, new Vector3(0f, Mathf.DegToRad(30f), 0f), new Color(0.58f, 0.66f, 0.72f));
                    break;
                case "naval_yard":
                    AddSizedImportedProp(visual, MilitaryFbxRoot + "structure-platform-dock.fbx", "ImportedDock", new Vector3(0f, 0.02f, 0f), 2.2f, 8.8f, Vector3.Zero, tint);
                    AddBlock(visual, "DockWater", new Vector3(5.4f, 0.08f, 6.6f), new Vector3(0f, 0.08f, 0f), new Color(0.05f, 0.36f, 0.48f));
                    AddSizedImportedProp(visual, FactoryKitRoot + "crane.fbx", "DockCrane", new Vector3(-2.8f, 0f, -2.5f), 4.8f, 3.2f, new Vector3(0f, Mathf.DegToRad(-90f), 0f), new Color(0.22f, 0.20f, 0.14f));
                    break;
                case "turret":
                    AddSizedImportedProp(visual, MilitaryFbxRoot + "cannon-mobile.fbx", "ImportedCannon", new Vector3(0f, 2.2f, -0.25f), 1.75f, 2.6f, new Vector3(0f, Mathf.Pi, 0f), new Color(0.26f, 0.28f, 0.25f));
                    AddCylinder(visual, "TurretBase", 1.45f, 2.4f, new Vector3(0f, 1.2f, 0f), tint);
                    break;
                case "power_plant":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-e.fbx", "PlantGeneratorBuilding", Vector3.Zero, 4.8f, 5.0f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, CityIndustrialRoot + "chimney-large.fbx", "PlantLargeChimney", new Vector3(1.8f, 0f, -1.8f), 6.2f, 1.8f, Vector3.Zero, new Color(0.22f, 0.24f, 0.25f));
                    AddSizedImportedProp(visual, CityIndustrialRoot + "detail-tank.fbx", "PlantCoolantTank", new Vector3(-1.8f, 0f, 1.8f), 2.2f, 1.8f, Vector3.Zero, new Color(0.28f, 0.34f, 0.38f));
                    break;
                case "gold_mine":
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-n.fbx", "MineRefinery", new Vector3(0f, 0f, 0.35f), 3.4f, 4.6f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, SpaceKitRoot + "rock_crystalsLargeA.fbx", "ResourceCore", new Vector3(0f, 0.1f, -1.55f), 1.8f, 2.4f, new Vector3(0f, Mathf.DegToRad(22f), 0f), new Color(0.95f, 0.72f, 0.22f));
                    AddSizedImportedProp(visual, FactoryKitRoot + "crane-lift.fbx", "MineCrane", new Vector3(2.3f, 0f, 0.1f), 4.0f, 2.4f, new Vector3(0f, Mathf.DegToRad(-90f), 0f), new Color(0.22f, 0.19f, 0.12f));
                    break;
                default:
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-c.fbx", "CommandBase", new Vector3(0f, 0f, 0f), 5.2f, 6.4f, Vector3.Zero, tint);
                    break;
            }
        }
        CenterImportedModel(visual);
        building.UpdateVisualsForState();
    }

    static void AddSizedImportedProp(Node3D parent, string path, string name, Vector3 position, float targetHeight, float targetSpan, Vector3 rotation, Color tint, bool preserveMaterials = true)
        => TryAddSizedImportedProp(parent, path, name, position, targetHeight, targetSpan, rotation, tint, preserveMaterials);

    static bool TryAddSizedImportedProp(Node3D parent, string path, string name, Vector3 position, float targetHeight, float targetSpan, Vector3 rotation, Color tint, bool preserveMaterials = true)
    {
        if (TryInstanceScene(path, name) is not { } prop)
            return false;

        var finalPos = position;
        var building = (parent as RtsBuilding) ?? (parent.GetParent() as RtsBuilding);
        if (building is not null)
        {
            finalPos = GetTerrainAdjustedPosition(building, position);
        }

        FitImportedNode(prop, targetHeight, targetSpan);
        prop.Position += finalPos;
        prop.Rotation = rotation;
        if (!preserveMaterials)
            TintImportedModel(prop, tint);
        parent.AddChild(prop);
        return true;
    }

    static Vector3 GetTerrainAdjustedPosition(Node3D building, Vector3 localPos)
    {
        // 仅对位于地面或贴近地面的组件进行地形高度自适应微调，屋顶雷达/炮台等挂载配件保持原相对高度
        if (Mathf.Abs(localPos.Y) > 0.25f)
            return localPos;

        var viewport = building.GetViewport();
        if (viewport is null)
            return localPos;
        var world3D = viewport.World3D;
        if (world3D is null)
            return localPos;
        var spaceState = world3D.DirectSpaceState;
        if (spaceState is null)
            return localPos;

        // 计算该配件在世界空间中的水平位置
        var globalPos = building.GlobalPosition + building.GlobalTransform.Basis * localPos;

        var from = new Vector3(globalPos.X, 500f, globalPos.Z);
        var to = new Vector3(globalPos.X, -100f, globalPos.Z);
        
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 1; // 仅探测地形碰撞层
        if (building is CollisionObject3D colObj)
        {
            query.Exclude = new Godot.Collections.Array<Rid> { colObj.GetRid() };
        }
        
        var hit = spaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
            var hitY = hit["position"].AsVector3().Y;
            var localYOffset = hitY - building.GlobalPosition.Y;
            return new Vector3(localPos.X, localPos.Y + localYOffset, localPos.Z);
        }

        return localPos;
    }

    static void AddImportedProp(Node3D parent, string path, string name, Vector3 position, Vector3 scale, Vector3 rotation, Color tint)
    {
        if (TryInstanceScene(path, name) is not { } prop)
            return;

        prop.Position = position;
        prop.Scale = scale;
        prop.Rotation = rotation;
        TintImportedModel(prop, tint);
        parent.AddChild(prop);
    }

    static Node3D? TryInstanceScene(string path, string name)
    {
        var resource = GD.Load<Resource>(path);
        Node3D? node = resource switch
        {
            PackedScene packed => packed.Instantiate<Node3D>(),
            Mesh mesh => new MeshInstance3D { Mesh = mesh },
            _ => null
        };
        if (node is not null)
            node.Name = name;
        return node;
    }

    static void FitImportedNode(Node3D node, float targetHeight, float targetSpan)
    {
        if (!TryGetLocalBounds(node, Transform3D.Identity, out var bounds))
            return;

        var size = bounds.Size;
        var heightScale = targetHeight > 0.01f && size.Y > 0.001f
            ? targetHeight / size.Y
            : 1f;
        var span = Mathf.Max(size.X, size.Z);
        var spanScale = targetSpan > 0.01f && span > 0.001f
            ? targetSpan / span
            : heightScale;
        var scale = targetHeight > 0.01f && targetSpan > 0.01f
            ? Mathf.Min(heightScale, spanScale)
            : targetHeight > 0.01f
                ? heightScale
                : spanScale;

        // ── 先把 Scale 写入，再以缩放后的 bounds 计算底部与水平中心偏移 ──────────
        node.Scale = Vector3.One * scale;

        // 重新计算缩放后的局部 bounds (传入包含 scale 的 rootTransform)
        var rootTransform = new Transform3D(Basis.FromScale(Vector3.One * scale), Vector3.Zero);
        if (!TryGetLocalBounds(node, rootTransform, out var scaledBounds))
        {
            var fallbackCenterX = (bounds.Position.X + bounds.Size.X * 0.5f) * scale;
            var fallbackCenterZ = (bounds.Position.Z + bounds.Size.Z * 0.5f) * scale;
            node.Position = new Vector3(-fallbackCenterX, -bounds.Position.Y * scale, -fallbackCenterZ);
            return;
        }

        var scaledCenterX = scaledBounds.Position.X + scaledBounds.Size.X * 0.5f;
        var scaledCenterZ = scaledBounds.Position.Z + scaledBounds.Size.Z * 0.5f;
        // 用缩放后的 bounds 精确对齐底部到 Y=0，并将 X 和 Z 轴中心对齐到 (0,0)，防止 FBX 模型原点位于边角而与判定区格地垫错位
        node.Position = new Vector3(-scaledCenterX, -scaledBounds.Position.Y, -scaledCenterZ);
        GD.Print($"[FIT_DEBUG] name={node.Name}, boundsPos={bounds.Position}, boundsSize={bounds.Size}, scaledCenterX={scaledCenterX}, scaledCenterZ={scaledCenterZ}, finalPropPos={node.Position}");
    }

    static void AddBlock(Node3D parent, string name, Vector3 size, Vector3 position, Color color)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Material(color, 0.82f)
        });
    }

    static void AddCylinder(Node3D parent, string name, float radius, float height, Vector3 position, Color color)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = 20 },
            MaterialOverride = Material(color, 0.78f)
        });
    }

    static StandardMaterial3D Material(Color color, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness
        };
    }

    static StandardMaterial3D PropellerBladeMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.50f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    static StandardMaterial3D PropellerBlurMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.38f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
    }

    public bool TryOccupyGlobalConquestMainBaseForFaction(bool playerOwned, out string message)
    {
        message = "";
        if (currentMap.Name != BattleMapCatalog.GlobalConquestName)
        {
            message = "该功能仅在全球征服模式可用";
            return false;
        }

        var enemyBase = FindMainBase(!playerOwned);
        if (enemyBase is null || enemyBase.Health > 0f)
        {
            message = playerOwned ? "敌方主基地尚未被摧毁" : "我方主基地尚未被摧毁";
            return false;
        }

        EndGame(playerOwned, playerOwned ? "占领敌方主基地" : "敌方占领了我方主基地");
        return true;
    }

    public Node3D? CreateBuildingPlacementPreview(string buildKey)
    {
        var def = BattleBuildingCatalog.Get(buildKey);
        var preview = new Node3D { Name = "BuildingPreview" };

        // 贴合地面的占地网格指示方框 (Y = 0.04m)
        var mesh = new MeshInstance3D
        {
            Name = "MeshInstance3D",
            Mesh = new BoxMesh { Size = new Vector3(def.CollisionSize.X, 0.03f, def.CollisionSize.Z) },
            Position = new Vector3(0f, 0.04f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.8f, 0.2f, 0.45f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            }
        };
        preview.AddChild(mesh);

        // 3D 建筑立面虚影
        var ghost = new RtsBuilding { Name = "GhostModel", BuildKey = buildKey };
        preview.AddChild(ghost);
        EnsureBuildingVisual(ghost, def);

        foreach (var m in ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            m.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.8f, 0.2f, 0.40f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
        }

        return preview;
    }

    public bool IsWithinBuildPlacementRange(Vector3 position, bool playerOwned)
    {
        var mainBase = FindOperationalMainBase(playerOwned);
        if (mainBase is null)
            return false;
        return position.DistanceTo(mainBase.GlobalPosition) <= 60f;
    }

    public void ApplyBuildingPlacementPreviewAppearance(Node3D preview, bool canPlace)
    {
        var mesh = preview.GetNodeOrNull<MeshInstance3D>("MeshInstance3D")
                   ?? preview.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();

        var color = canPlace
            ? new Color(0.2f, 0.8f, 0.2f, 0.45f)
            : new Color(0.96f, 0.24f, 0.18f, 0.45f);

        if (mesh is not null)
        {
            if (mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = color;
            }
            else
            {
                mesh.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                };
            }
        }

        if (preview.GetNodeOrNull<RtsBuilding>("GhostModel") is { } ghost)
        {
            foreach (var m in ghost.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                m.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                };
            }
        }
    }

    public Vector3 ClampToPlayableMap(Vector3 position)
    {
        return BattleMapCatalog.ClampToMap(position, 4f);
    }

    public Node3D[] CreateOccupiedPlacementOverlays(string buildKey)
    {
        var overlays = new List<Node3D>();
        foreach (var building in GetBuildings())
        {
            var def = BattleBuildingCatalog.Get(building.BuildKey);
            var overlay = new Node3D
            {
                Name = "OccupiedOverlay",
                Position = new Vector3(building.GlobalPosition.X, 0.04f, building.GlobalPosition.Z),
                Rotation = building.Rotation // 同步旋转角度，确保完美覆盖斜放建筑
            };

            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh 
                { 
                    Size = new Vector3(def.CollisionSize.X + 0.4f, 0.02f, def.CollisionSize.Z + 0.4f) 
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.96f, 0.24f, 0.18f, 0.38f), // 鲜明半透明红
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, // 自发光高亮
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    NoDepthTest = false // 贴合地面渲染，不再悬空或穿越建筑中部
                }
            };
            overlay.AddChild(mesh);
            overlays.Add(overlay);
        }
        return overlays.ToArray();
    }

    public (Vector3 center, float radius)[] GetBuildPlacementRanges(bool playerOwned)
    {
        var ranges = new List<(Vector3, float)>();
        var mainBase = FindOperationalMainBase(playerOwned);
        if (mainBase is not null)
        {
            ranges.Add((mainBase.GlobalPosition, 60f));
        }
        return ranges.ToArray();
    }

    public bool CanOccupyGlobalConquestMainBase(bool playerOwned, out string message)
    {
        message = "";
        if (currentMap.Name != BattleMapCatalog.GlobalConquestName)
        {
            message = "该功能仅在全球征服模式可用";
            return false;
        }

        var enemyBase = FindMainBase(!playerOwned);
        if (enemyBase is null || enemyBase.Health > 0f)
        {
            message = playerOwned ? "敌方主基地尚未被摧毁" : "我方主基地尚未被摧毁";
            return false;
        }

        return true;
    }

    public bool TryOccupyGlobalConquestMainBase(out string message)
    {
        return TryOccupyGlobalConquestMainBaseForFaction(true, out message);
    }

    public bool TrySurrender(out string message)
    {
        message = "";
        if (GameOver)
        {
            message = "战斗已经结束";
            return false;
        }

        EndGame(false, "投降");
        message = "已投降";
        return true;
    }

    public void TriggerCameraShake(float intensity, float duration)
    {
        var cameraRig = GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig");
        if (cameraRig is not null && GodotObject.IsInstanceValid(cameraRig))
        {
            cameraRig.TriggerShake(intensity, duration);
        }
    }

    void StartTenseBattleBgm()
    {
        try
        {
            AudioStream? stream = null;
            
            // 优先检查是否有自定义音乐文件 (支持 bgm.mp3, bgm.ogg, bgm.wav)
            string[] customMusicPaths = new[]
            {
                "res://assets/audio/bgm.mp3",
                "res://assets/audio/bgm.ogg",
                "res://assets/audio/bgm.wav"
            };

            foreach (var path in customMusicPaths)
            {
                if (ResourceLoader.Exists(path))
                {
                    stream = GD.Load<AudioStream>(path);
                    if (stream is not null)
                    {
                        GD.Print($"[BGM] 成功加载自定义背景音乐: {path}");
                        break;
                    }
                }
            }

            // 如果没有自定义文件，则播放我们全新合成的 150BPM 激情热血双声道战斗 BGM
            if (stream is null)
            {
                stream = CreateEpicHighEnergyBattleBgmStream();
                GD.Print("[BGM] 生成 150BPM 激情热血双声道 RTS 战斗 BGM 成功！");
            }

            bgmPlayer = new AudioStreamPlayer
            {
                Name = "TenseBattleBgm",
                Stream = stream,
                VolumeDb = -9f, // 充满激情且均衡的音量
                Bus = "Master"
            };
            AddChild(bgmPlayer);
            bgmPlayer.Play();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BGM] 启动背景音乐失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 合成 44.1kHz 16-Bit 立体声 (Stereo) 140BPM 温暖流畅、圆润不刺耳的史诗电影级 RTS 战斗背景音乐
    /// 使用一阶低通滤波器 (Low-Pass Filter) 彻底过滤高频刺耳谐波，配合正弦谐波和声与重低音电影战鼓
    /// </summary>
    static AudioStreamWav CreateEpicHighEnergyBattleBgmStream()
    {
        const int sampleRate = 44100;
        const float bpm = 140f; // 流畅战术节奏 140 BPM
        const float beatDuration = 60f / bpm;
        const int beats = 32; // 8小节循环
        float totalDuration = beats * beatDuration;
        int numFrames = (int)(totalDuration * sampleRate);
        
        var data = new byte[numFrames * 4];

        // 滤波参数：截止频率 1500Hz，过滤掉所有尖锐/刺耳的高频锯齿杂音
        float lpfL = 0f, lpfR = 0f;
        float alpha = (2f * Mathf.Pi * 1500f / sampleRate) / (1f + 2f * Mathf.Pi * 1500f / sampleRate);

        // E小调和弦级数 (E1 -> C2 -> G1 -> D2)
        float E1 = 41.20f, C2 = 65.41f, G1 = 49.00f, D2 = 73.42f;

        for (int i = 0; i < numFrames; i++)
        {
            float t = (float)i / sampleRate;
            float currentBeat = t / beatDuration;

            // 1. 温暖重低音 (Warm Sub Bass)
            float bassFreq = E1;
            if (currentBeat >= 8f && currentBeat < 16f) bassFreq = C2;
            else if (currentBeat >= 16f && currentBeat < 24f) bassFreq = G1;
            else if (currentBeat >= 24f && currentBeat < 32f) bassFreq = D2;

            // 使用正弦纯音 + 二阶谐波，音色浑厚柔和
            float bassVal = 0.65f * Mathf.Sin(2f * Mathf.Pi * bassFreq * t) +
                            0.25f * Mathf.Sin(4f * Mathf.Pi * bassFreq * t);
            
            float sixteenthTime = (t % (beatDuration / 4f));
            float bassEnv = Mathf.Exp(-sixteenthTime * 12f) * 0.4f + 0.6f;
            bassVal *= bassEnv * 0.45f;

            if (t < 0.2f) bassVal *= (t / 0.2f);
            if (totalDuration - t < 0.2f) bassVal *= ((totalDuration - t) / 0.2f);

            // 2. 电影级重低音战鼓 (Cinematic Drums)
            float drumL = 0f, drumR = 0f;
            float beatTime = t % beatDuration;

            // 深沉太鼓扫频 (70Hz -> 30Hz)
            float kickFreq = 30f + 50f * Mathf.Exp(-beatTime * 25f);
            float kickEnv = Mathf.Exp(-beatTime * 10f);
            float kickVal = Mathf.Sin(2f * Mathf.Pi * kickFreq * beatTime) * kickEnv * 0.5f;
            drumL += kickVal;
            drumR += kickVal;

            // 柔和小鼓 (在第 2 拍和第 4 拍下沉)
            float measureTime = t % (beatDuration * 4f);
            float snareTime1 = Math.Abs(measureTime - beatDuration);
            float snareTime2 = Math.Abs(measureTime - beatDuration * 3f);
            float snareTime = Math.Min(snareTime1, snareTime2);
            if (snareTime < beatDuration)
            {
                float snareEnv = Mathf.Exp(-snareTime * 16f);
                float snareTone = Mathf.Sin(2f * Mathf.Pi * 130f * snareTime) * snareEnv * 0.25f;
                drumL += snareTone;
                drumR += snareTone;
            }

            // 3. 柔和史诗弦乐铺底 (Warm Epic Strings Pad)
            float stringVal = 0f;
            float padFreq = bassFreq * 4f;
            if (padFreq > 0f)
            {
                float osc1 = Mathf.Sin(2f * Mathf.Pi * padFreq * t);
                float osc2 = Mathf.Sin(2f * Mathf.Pi * (padFreq * 1.5f) * t); // 纯五度谐程
                float vibrato = 1f + 0.005f * Mathf.Sin(2f * Mathf.Pi * 4.5f * t);
                stringVal = (osc1 * 0.6f + osc2 * 0.4f) * vibrato * 0.22f;
            }

            // 4. 流畅英雄主旋律 (Smooth Heroic Melody)
            float leadVal = 0f;
            if (currentBeat >= 4f)
            {
                int noteIndex = (int)(currentBeat / 2f) % 8;
                float leadFreq = 329.63f; // E4
                switch (noteIndex)
                {
                    case 0: case 1: leadFreq = 329.63f; break; // E4
                    case 2: leadFreq = 392.00f; break;        // G4
                    case 3: leadFreq = 440.00f; break;        // A4
                    case 4: case 5: leadFreq = 493.88f; break; // B4
                    case 6: leadFreq = 587.33f; break;        // D5
                    case 7: leadFreq = 493.88f; break;        // B4
                }

                float noteTime = t % (beatDuration * 2f);
                float leadEnv = 1.0f;
                if (noteTime < 0.1f) leadEnv = noteTime / 0.1f;
                else if (noteTime > beatDuration * 1.6f)
                    leadEnv = Mathf.Lerp(1.0f, 0.0f, (noteTime - beatDuration * 1.6f) / (beatDuration * 0.4f));

                float osc1 = Mathf.Sin(2f * Mathf.Pi * leadFreq * t);
                float osc2 = Mathf.Sin(2f * Mathf.Pi * (leadFreq * 2.002f) * t) * 0.2f;
                leadVal = (osc1 + osc2) * leadEnv * 0.25f;
            }

            // 5. 声道混音与一阶低通滤波 (Low-Pass Filter Processing)
            float rawL = bassVal + drumL + stringVal * 0.9f + leadVal * 0.95f;
            float rawR = bassVal + drumR + stringVal * 1.1f + leadVal * 1.05f;

            // 低通滤波过滤尖锐高频
            lpfL = lpfL + alpha * (rawL - lpfL);
            lpfR = lpfR + alpha * (rawR - lpfR);

            float mixL = Mathf.Clamp(lpfL, -1f, 1f);
            float mixR = Mathf.Clamp(lpfR, -1f, 1f);

            short sampleL = (short)(mixL * 32767f);
            short sampleR = (short)(mixR * 32767f);

            int byteIdx = i * 4;
            data[byteIdx]     = (byte)(sampleL & 0xFF);
            data[byteIdx + 1] = (byte)((sampleL >> 8) & 0xFF);
            data[byteIdx + 2] = (byte)(sampleR & 0xFF);
            data[byteIdx + 3] = (byte)((sampleR >> 8) & 0xFF);
        }

        return new AudioStreamWav
        {
            Data = data,
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = true,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = numFrames
        };
    }

    private static float SawWave(float phase)
    {
        float p = (phase % (2f * Mathf.Pi)) / (2f * Mathf.Pi);
        if (p < 0f) p += 1f;
        return 2f * p - 1f;
    }

    private static float TriangleWave(float phase)
    {
        float val = (phase % (2f * Mathf.Pi)) / (2f * Mathf.Pi);
        if (val < 0f) val += 1f;
        if (val < 0.25f) return val * 4f;
        if (val < 0.75f) return 2f - val * 4f;
        return val * 4f - 4f;
    }
}
