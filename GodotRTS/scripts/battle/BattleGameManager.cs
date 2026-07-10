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
    [Export] public float UnitVisionRadius { get; set; } = 32f;
    [Export] public float BuildingVisionRadius { get; set; } = 46f;
    [Export] public float MainBaseVisionRadius { get; set; } = 70f;
    [Export] public int MainBaseRebuildCost { get; set; } = 600;
    [Export] public float MainBaseRebuildDuration { get; set; } = 30f;

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
        foreach (var unit in GetUnits())
        {
            unit.NetId = unit.NetId == 0 ? nextNetId++ : unit.NetId;
            unit.ApplyCatalogCombatProfile();
            ConfigureExistingUnitVisual(unit);
            unit.Died -= OnUnitDied;
            unit.Died += OnUnitDied;
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
        // 初始化登记完毕后，立刻让镜头定位对准玩家的主基地，确保首帧显示正确位置
        FocusCameraOnMainBase();
    }

    /// <summary>
    /// 在游戏启动时，定位到玩家的初始主基地，并将 RtsCamera 镜头平移聚焦到该位置。
    /// </summary>
    void FocusCameraOnMainBase()
    {
        var playerMainBase = GetBuildings().FirstOrDefault(b => b.PlayerOwned && b.IsMainBase);
        if (playerMainBase is not null)
        {
            var cameraRig = GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig");
            if (cameraRig is not null)
            {
                cameraRig.JumpTo(playerMainBase.GlobalPosition);
            }
        }
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
            var waterTarget = BattleMapCatalog.ClosestWaterPoint(currentMap, clamped);
            return new Vector3(waterTarget.X, BattleUnitCatalog.SpawnHeight(unitKey), waterTarget.Z);
        }

        return new Vector3(clamped.X, 0f, clamped.Z);
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
        => BattleMapCatalog.IsPointInWater(currentMap, position, padding);

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
                _ => 3
            },
            "air_factory" or "airfield" or "tank_factory" or "armor_factory" or "naval_yard" => mainBaseLevel switch
            {
                1 => 0, // Level 1 main base cannot construct advanced factory buildings
                2 => 1,
                _ => 2
            },
            "turret" => mainBaseLevel switch
            {
                1 => 2,
                2 => 4,
                _ => 6
            },
            "gold_mine" or "power_plant" => mainBaseLevel switch
            {
                1 => 2,
                2 => 3,
                _ => 4
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

        // 主基地 1 级时，部分高阶功能性建筑锁死不可建造
        if (baseLevel == 1)
        {
            if (buildKey == "tank_factory" || buildKey == "armor_factory" || buildKey == "airfield" || buildKey == "air_factory" || buildKey == "naval_yard")
            {
                message = $"{BattleBuildingCatalog.Get(buildKey).DisplayName}需要主基地升级到 Lv.2 才能建造";
                return false;
            }
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
        if (Mathf.Abs(position.X) > BattleMapCatalog.MapHalfSize - 18f || Mathf.Abs(position.Z) > BattleMapCatalog.MapHalfSize - 18f)
        {
            message = "\u5efa\u9020\u4f4d\u7f6e\u8d85\u51fa\u5730\u56fe\u8fb9\u754c";
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
            message = "\u5efa\u7b51\u9700\u8981\u653e\u5728\u4e3b\u57fa\u5730\u5efa\u9020\u8303\u56f4\u5185";
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
            var capacity = CountBuildings("airfield", true, false) * 4;
            var occupied = GetUnits(true).Count(unit => BattleUnitCatalog.RequiresAirfieldSlot(unit.UnitKey))
                + CountQueuedAircraft(true);
            if (capacity <= 0)
            {
                message = "需要先建造停机场";
                return false;
            }
            if (occupied >= capacity)
            {
                message = $"停机场机位不足：{occupied}/{capacity}";
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
            var capacity = CountBuildings("airfield", playerOwned, false) * 4;
            var occupied = GetUnits(playerOwned).Count(unit => BattleUnitCatalog.RequiresAirfieldSlot(unit.UnitKey))
                + CountQueuedAircraft(playerOwned);
            if (capacity <= 0 || occupied >= capacity)
            {
                message = $"停机场位不足：{occupied}/{capacity}";
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

        var cooldowns = playerOwned ? playerBattleTechCooldownEnds : enemyBattleTechCooldownEnds;
        cooldowns[tech.Key] = GameTime + tech.Cooldown;
        ForceRefreshFogOfWar();
        message = $"{tech.DisplayName} (Lv.{level}) 已释放，影响 {affected} 个单位";
        return true;
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
        var requirements = GetUpgradeRequirementStatuses(building.BuildKey, nextLevel, playerOwned);
        var missing = requirements.Where(item => !item.IsMet).ToArray();
        if (missing.Length > 0)
        {
            message = $"升级条件不足：{FormatRequirementSummary(missing)}";
            return false;
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

        if (ResolveNearestAirfield(unit.PlayerOwned, unit.GlobalPosition) is not { } airfield)
        {
            message = lowFuel
                ? $"{unit.DisplayName} 油量不足，但没有可用停机场"
                : "需要先建造停机场";
            return false;
        }

        var parkingTarget = NormalizeUnitTarget(unit, GetAircraftParkingTarget(airfield, unit));
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

        unit.Rotation = new Vector3(unit.Rotation.X, Mathf.Atan2(direction.X, direction.Z), unit.Rotation.Z);
    }

    public RtsBuilding? SpawnBuilding(BattleBuildingDefinition def, Vector3 position, bool playerOwned)
    {
        buildingsRoot ??= GetNodeOrNull<Node3D>(BuildingsPath) ?? GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Buildings");
        if (buildingsRoot is null)
            return null;

        var building = new RtsBuilding
        {
            Name = (playerOwned ? "Player_" : "Enemy_") + def.Key,
            Position = new Vector3(position.X, 0f, position.Z),
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
            unit.ConfigureVisualRig(unit.UnitKey, imported);
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
        if (Mathf.Abs(position.X) > BattleMapCatalog.MapHalfSize - 18f || Mathf.Abs(position.Z) > BattleMapCatalog.MapHalfSize - 18f)
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
        if (ResolveNearestAirfield(factory.PlayerOwned, factory.GlobalPosition) is not { } bestAirfield)
            return NormalizeUnitTarget(unit, factory.GetRallyTarget());
        return NormalizeUnitTarget(unit, GetAircraftParkingTarget(bestAirfield, unit));
    }

    public RtsBuilding? ResolveNearestAirfield(bool playerOwned, Vector3 origin)
    {
        return GetBuildings(playerOwned)
            .Where(building => building.BuildKey == "airfield" && !building.UnderConstruction && building.Health > 0f)
            .OrderBy(building => building.GlobalPosition.DistanceSquaredTo(origin))
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
                "bomber" when BomberScene is not null => BomberScene,
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
            AddNavalUnitVisual(unit, def, playerOwned);
            return;
        }

        if (BattleUnitCatalog.IsInfantryLike(def.Key))
        {
            AddInfantryVisual(unit, def, playerOwned);
            return;
        }

        var vehicleScene = def.Key switch
        {
            "light_tank" when LightTankScene is not null => LightTankScene,
            "heavy_tank" when HeavyTankScene is not null => HeavyTankScene,
            "artillery" when ArtilleryScene is not null => ArtilleryScene,
            _ => TankScene
        };
        if (vehicleScene is not null && !BattleUnitCatalog.IsInfantryLike(def.Key))
        {
            AddPackedUnitVisual(unit, vehicleScene, def.Key + "_model", def, playerOwned);
            return;
        }

        AddProceduralInfantryVisual(unit, def, playerOwned);
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
        var soldierCount = def.Key == "infantry" ? 3 : 2;
        var infantryTint = playerOwned ? def.Tint : new Color(0.58f, 0.25f, 0.18f);
        for (var i = 0; i < soldierCount; i++)
        {
            var centerOffset = (soldierCount - 1) * 0.5f;
            var soldierRoot = new Node3D
            {
                Name = $"Infantry_{i}",
                Position = new Vector3((i - centerOffset) * 0.65f, 0f, i == 1 ? -0.34f : 0.30f),
                Scale = Vector3.One * InfantryModelScale(def.Key),
                Rotation = new Vector3(0f, playerOwned ? 0f : Mathf.Pi, 0f)
            };
            var model = InfantryScene.Instantiate<Node3D>();
            model.Name = "Model";
            soldierRoot.AddChild(model);
            CenterImportedModel(model);
            TintImportedModel(soldierRoot, infantryTint);
            // 注入 Mixamo 骨骼动画（idle / walk / fire）
            InfantryAnimationBridge.InjectAnimations(model);
            AttachWeaponToInfantry(model, def.Key);
            squad.AddChild(soldierRoot);
        }
        unit.ConfigureVisualRig(def.Key, squad);
    }

    static float InfantryModelScale(string key) => key switch
    {
        "infantry_artillery" => 1.65f,
        "infantry_flamethrower" or "flamethrower" => 1.75f,
        _ => 1.60f
    };

    void AddProceduralInfantryVisual(RtsUnit unit, BattleUnitDefinition def, bool playerOwned)
    {
        var squad = new Node3D { Name = "InfantrySquad" };
        unit.AddChild(squad);
        var soldierCount = def.Key == "infantry" ? 3 : 2;
        var infantryTint = playerOwned ? def.Tint : new Color(0.58f, 0.25f, 0.18f);
        for (var i = 0; i < soldierCount; i++)
        {
            var centerOffset = (soldierCount - 1) * 0.5f;
            var soldierRoot = new Node3D
            {
                Name = $"Infantry_{i}",
                Position = new Vector3((i - centerOffset) * 0.58f, 0f, i == 1 ? -0.34f : 0.28f),
                Scale = Vector3.One * 1.55f, // 积木小兵同样等比放大 1.55 倍
                Rotation = new Vector3(0f, playerOwned ? 0f : Mathf.Pi, 0f)
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
            if (name.Contains("RightHand") || name.Contains("Right_Hand") || name.Contains("RightWeapon"))
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

        if (ShouldTintImportedModel(def.Key))
            TintImportedModel(visualRoot, playerOwned ? def.Tint : new Color(0.56f, 0.22f, 0.18f));
        AddFallbackAircraftPropellers(visualRoot, def.Key);
        unit.AddChild(visualRoot);
        unit.ConfigureVisualRig(def.Key, BattleUnitCatalog.IsAirUnit(def.Key) ? visualRoot : visual);
    }

    static void AddFallbackAircraftPropellers(Node3D visualRoot, string unitKey)
    {
        if (!BattleUnitCatalog.IsAirUnit(unitKey))
            return;

        if (unitKey == "bomber")
        {
            AddPropeller(visualRoot, "PropellerLeft", new Vector3(-1.45f, 0.42f, -0.62f), 0.62f);
            AddPropeller(visualRoot, "PropellerRight", new Vector3(1.45f, 0.42f, -0.62f), 0.62f);
            return;
        }

        AddPropeller(visualRoot, "PropellerNose", new Vector3(0f, 0.36f, -1.22f), unitKey == "scout_plane" ? 0.84f : 0.58f);
    }

    static float ImportedModelScale(string key) => key switch
    {
        "light_tank" => 0.38f,
        "heavy_tank" => 0.78f,
        "artillery" => 42f,
        "scout_plane" => 2.25f,
        "fighter" => 0.58f,
        "bomber" => 0.62f,
        _ => 1f
    };

    static Vector3 ImportedModelOffset(string key) => key switch
    {
        "artillery" => new Vector3(0f, 0.08f, 0f),
        "scout_plane" => new Vector3(0f, 0.08f, 0f),
        _ => Vector3.Zero
    };

    static Vector3 ImportedModelRotation(string key, bool playerOwned)
    {
        var yaw = key switch
        {
            "artillery" => -Mathf.Pi * 0.5f,
            "scout_plane" => -Mathf.Pi * 0.5f,
            "fighter" => playerOwned ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f,
            "bomber" => playerOwned ? -Mathf.Pi * 0.5f : Mathf.Pi * 0.5f,
            _ => 0f
        };
        if (!playerOwned && key is not "fighter" and not "bomber")
            yaw += Mathf.Pi;
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
        "heavy_tank" => new Vector3(0f, Mathf.Pi, 0f),
        "tank" => new Vector3(0f, Mathf.Pi, 0f),
        "medium_tank" => new Vector3(0f, Mathf.Pi, 0f),
        _ => Vector3.Zero
    };

    static void CenterImportedModel(Node3D visual)
    {
        if (!TryGetLocalBounds(visual, Transform3D.Identity, out var bounds))
            return;

        var center = bounds.Position + bounds.Size * 0.5f;
        visual.Position += new Vector3(-center.X, -bounds.Position.Y, -center.Z);
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

    static void TintImportedModel(Node node, Color tint)
    {
        foreach (var child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is MeshInstance3D mesh)
            {
                var materialCount = mesh.GetSurfaceOverrideMaterialCount();
                var successfullyTintedAny = false;
                if (materialCount > 0)
                {
                    for (int s = 0; s < materialCount; s++)
                    {
                        var mat = mesh.GetActiveMaterial(s) as BaseMaterial3D;
                        if (mat is not null)
                        {
                            var dupMat = mat.Duplicate() as BaseMaterial3D;
                            if (dupMat is not null)
                            {
                                dupMat.AlbedoColor = new Color(
                                    dupMat.AlbedoColor.R * tint.R,
                                    dupMat.AlbedoColor.G * tint.G,
                                    dupMat.AlbedoColor.B * tint.B,
                                    dupMat.AlbedoColor.A * tint.A
                                );
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
        var importedPath = def.Key switch
        {
            "patrol_boat" => "res://assets/props/kenney/military/ship-small.obj",
            "destroyer_ship" => "res://assets/props/kenney/military/ship-medium.obj",
            "transport_ship" => "res://assets/props/kenney/military/ship-large.obj",
            _ => ""
        };
        if (!string.IsNullOrEmpty(importedPath) && TryInstanceScene(importedPath, "ImportedShip") is { } ship)
        {
            ship.Scale = Vector3.One * (def.Key == "transport_ship" ? 0.42f : def.Key == "destroyer_ship" ? 0.36f : 0.30f);
            ship.Rotation = new Vector3(0f, playerOwned ? Mathf.Pi : 0f, 0f);
            ship.Position = new Vector3(0f, 0.02f, 0f);
            unit.AddChild(ship);
            if (!playerOwned)
                TintImportedModel(ship, new Color(0.52f, 0.25f, 0.20f));
            AddBlock(unit, "Wake", new Vector3(1.9f, 0.04f, 3.2f), new Vector3(0f, 0.04f, 0.45f), new Color(0.62f, 0.82f, 0.86f, 0.42f));
            return;
        }

        var visual = new Node3D { Name = "ShipHull" };
        unit.AddChild(visual);
        var tint = playerOwned ? def.Tint : new Color(0.48f, 0.20f, 0.16f);
        var length = def.Key == "transport_ship" ? 4.4f : def.Key == "destroyer_ship" ? 3.8f : 2.7f;
        AddBlock(visual, "Hull", new Vector3(1.1f, 0.55f, length), new Vector3(0f, 0.38f, 0f), tint);
        AddBlock(visual, "Deck", new Vector3(0.76f, 0.42f, length * 0.45f), new Vector3(0f, 0.86f, -0.25f), tint.Lightened(0.08f));
        if (BattleUnitCatalog.IsArtilleryLike(def.Key))
            AddImportedNavalForwardGun(visual, length);
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
        if (building.GetNodeOrNull<Node3D>("BuildingVisual") is not null)
            return;

        if (building.GetNodeOrNull<MeshInstance3D>("Mesh") is { } oldMesh)
            oldMesh.Visible = false;
 
        var visualY = 0.06f;
        // 针对不同 FBX 建筑模型的底部基础面高度进行微调，防止大本营等建筑的台阶和门陷入泥土中
        if (def.Key == "main_base")
            visualY = 0.58f;
        else if (def.Key == "tank_factory" || def.Key == "armor_factory" || def.Key == "air_factory")
            visualY = 0.32f;
        else if (def.Key == "barracks" || def.Key == "power_plant" || def.Key == "gold_mine")
            visualY = 0.22f;

        var visual = new Node3D { Name = "BuildingVisual", Position = new Vector3(0f, visualY, 0f) };
        building.AddChild(visual);
        
        var key = def.Key;
        var isMechanical = key.StartsWith("int_");
        if (isMechanical)
        {
            key = key.Substring(4);
        }
        
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
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish_detailed.fbx", "MechCommandRadar", new Vector3(2.2f, 0f, 2.2f), 3.6f, 2.2f, Vector3.Zero, tint.Lightened(0.1f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "machine_wireless.fbx", "MechCommandAntenna", new Vector3(-2.2f, 0f, -2.2f), 4.8f, 2.0f, Vector3.Zero, tint.Lightened(0.15f));
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
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-a.fbx", "CommandBase", new Vector3(0f, 0f, 0f), 5.2f, 6.4f, Vector3.Zero, tint);
                    AddSizedImportedProp(visual, CityIndustrialRoot + "building-f.fbx", "CommandTower", new Vector3(-2.2f, 0f, -2.2f), 4.2f, 2.2f, Vector3.Zero, tint.Lightened(0.08f));
                    AddSizedImportedProp(visual, SpaceKitRoot + "satelliteDish_detailed.fbx", "Radar", new Vector3(2.0f, 0f, 1.8f), 2.8f, 1.6f, new Vector3(0f, Mathf.DegToRad(20f), 0f), new Color(0.58f, 0.70f, 0.72f));
                    break;
            }
        }
        building.UpdateVisualsForState();
    }

    static void AddSizedImportedProp(Node3D parent, string path, string name, Vector3 position, float targetHeight, float targetSpan, Vector3 rotation, Color tint, bool preserveMaterials = true)
        => TryAddSizedImportedProp(parent, path, name, position, targetHeight, targetSpan, rotation, tint, preserveMaterials);

    static bool TryAddSizedImportedProp(Node3D parent, string path, string name, Vector3 position, float targetHeight, float targetSpan, Vector3 rotation, Color tint, bool preserveMaterials = true)
    {
        if (TryInstanceScene(path, name) is not { } prop)
            return false;

        FitImportedNode(prop, targetHeight, targetSpan);
        prop.Position += position;
        prop.Rotation = rotation;
        if (!preserveMaterials)
            TintImportedModel(prop, tint);
        parent.AddChild(prop);
        return true;
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

        // ── 先把 Scale 写入，再以缩放后的 bounds 计算底部偏移 ──────────
        node.Scale = Vector3.One * scale;

        // 重新计算缩放后的局部 bounds (传入包含 scale 的 rootTransform)
        var rootTransform = new Transform3D(Basis.FromScale(Vector3.One * scale), Vector3.Zero);
        if (!TryGetLocalBounds(node, rootTransform, out var scaledBounds))
        {
            // Fallback：无法获取缩放后 bounds，用原来的方式估算
            var center = bounds.Position + bounds.Size * 0.5f;
            node.Position = new Vector3(-center.X * scale, -bounds.Position.Y * scale, -center.Z * scale);
            return;
        }

        // 用缩放后的 bounds 精确对齐底部到 Y=0，水平居中
        var scaledCenter = scaledBounds.Position + scaledBounds.Size * 0.5f;
        node.Position = new Vector3(
            -scaledCenter.X,
            -scaledBounds.Position.Y,   // 把 AABB 最低点推到 Y=0
            -scaledCenter.Z);
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
        var mesh = new MeshInstance3D
        {
            Name = "MeshInstance3D",
            Mesh = new BoxMesh { Size = def.CollisionSize },
            Position = new Vector3(0f, def.CollisionSize.Y * 0.5f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.2f, 0.8f, 0.2f, 0.45f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
        preview.AddChild(mesh);
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

        if (mesh is not null)
        {
            if (mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = canPlace
                    ? new Color(0.2f, 0.8f, 0.2f, 0.45f)
                    : new Color(0.96f, 0.24f, 0.18f, 0.45f);
            }
            else
            {
                mesh.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = canPlace
                        ? new Color(0.2f, 0.8f, 0.2f, 0.45f)
                        : new Color(0.96f, 0.24f, 0.18f, 0.45f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
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
                Position = building.GlobalPosition + new Vector3(0f, 0.08f, 0f), // 稍微抬高防止Z-fighting
                Rotation = building.Rotation // 同步旋转角度，确保完美覆盖斜放建筑
            };

            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh 
                { 
                    Size = new Vector3(def.CollisionSize.X + 1.0f, 0.05f, def.CollisionSize.Z + 1.0f) 
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.96f, 0.24f, 0.18f, 0.28f), // 鲜明半透明红
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, // 自发光高亮
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    NoDepthTest = true // 强制透视最上层，避免被已有建筑模型遮蔽
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
}
