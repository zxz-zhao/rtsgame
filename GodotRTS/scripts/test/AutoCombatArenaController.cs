using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class AutoCombatArenaController : Node3D
{
    [Export] public Camera3D? ArenaCamera { get; set; }
    [Export] public Node3D? UnitsRoot { get; set; }

    const float BlueBaseX = -42f;
    const float RedBaseX = 42f;
    const float WaveInterval = 9.0f;

    bool autoWaveEnabled = true;
    float waveTimer = 4.0f; // 开局 4 秒后开始首波
    float gameTimer = 0f;
    float retargetTimer = 0f;
    int blueKills = 0;
    int redKills = 0;
    public static AutoCombatArenaController? Instance { get; private set; }

    int timeScaleIndex = 0; // 0: 1x, 1: 2x, 2: 4x
    bool isPaused = false;
    int maxCaptureFrames = -1;
    int currentCapturedFrames = 0;

    enum CameraMode
    {
        CinematicDirector,
        RtsTopDown,
        FrontlineCloseUp,
        BirdEyeOverview
    }
    CameraMode currentCamMode = CameraMode.CinematicDirector;

    readonly List<RtsUnit> blueUnits = new();
    readonly List<RtsUnit> redUnits = new();
    readonly RandomNumberGenerator rng = new();

    // 基地建造、建筑升级与出兵集结三阶段控制器
    bool phase1Constructed = false;
    bool phase2Upgraded = false;
    bool phase3UnitsProduced = false;
    bool phase4TechCasted = false;
    RtsBuilding? blueBase;
    RtsBuilding? blueArmorFactory;
    RtsBuilding? blueBarracks;
    RtsBuilding? blueTankFactory;
    RtsBuilding? blueAirFactory;
    RtsBuilding? blueAirfield;
    RtsBuilding? blueNavalYard;
    RtsBuilding? blueTurret;
    RtsBuilding? bluePowerPlant;
    RtsBuilding? blueGoldMine;

    RtsBuilding? redBase;
    RtsBuilding? redArmorFactory;
    RtsBuilding? redBarracks;
    RtsBuilding? redTankFactory;
    RtsBuilding? redAirFactory;
    RtsBuilding? redAirfield;
    RtsBuilding? redNavalYard;
    RtsBuilding? redTurret;
    RtsBuilding? redPowerPlant;
    RtsBuilding? redGoldMine;

    // 建筑摆放实机演示控制器
    Node3D? demoPlacementPreview;
    bool demoPlacementSpawned = false;
    bool demoPlacementConfirmed = false;

    // UI Controls
    GodotRTS.Scripts.UI.ArenaBattleHud? _battleHud;

    // Camera Drag & Shake
    bool isMiddleDragging = false;
    Vector2 lastMousePos;
    float shakeIntensity = 0f;
    float shakeDuration = 0f;
    float shakeTimer = 0f;

    public void TriggerCameraShake(float intensity, float duration)
    {
        float clamped = Mathf.Clamp(intensity, 0f, 0.09f);
        shakeIntensity = Mathf.Max(shakeIntensity, clamped);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeTimer = shakeDuration;
    }

    public void RegisterUnit(RtsUnit unit, bool isBlue)
    {
        if (!GodotObject.IsInstanceValid(unit)) return;
        unit.Died += OnUnitDied;
        if (isBlue)
        {
            if (!blueUnits.Contains(unit)) blueUnits.Add(unit);
        }
        else
        {
            if (!redUnits.Contains(unit)) redUnits.Add(unit);
        }
    }

    public override void _EnterTree()
    {
        Instance = this;
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = false;
        BattleGameManager.DisableFuelDepletion = false;
    }

    public override void _Ready()
    {
        EffectPool.Ensure(this);
        BattleFeedback.Ensure(this);
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableFuelDepletion = false;
        BattleGameManager.DisableWaterCheck = false;
        if (BattleGameManager.Instance is { } mgr)
        {
            var arenaMap = new BattleMapDefinition
            {
                Name = "AutoCombatArena",
                MapHalfSize = 120f,
                Waters = new[]
                {
                    new TerrainStripSpec("ArenaWaterBay", new Vector3(0f, 0f, -22.0f), new Vector2(240f, 36f), 0f)
                }
            };
            mgr.SetCurrentMap(arenaMap);
            BattleGameManager.DisableWaterCheck = false;
        }
        else
        {
            BattleGameManager.DisableWaterCheck = false;
        }
        rng.Randomize();

        // 强制窗口显示、居中并置顶激活（防止在后台启动时继承隐藏窗口标记导致只闻其声不见其窗）
        try
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetCurrentScreen(0);
            var screenBounds = DisplayServer.ScreenGetUsableRect(0);
            var winSize = new Vector2I(1280, 720);
            DisplayServer.WindowSetSize(winSize);
            var centerPos = new Vector2I(
                screenBounds.Position.X + (screenBounds.Size.X - winSize.X) / 2,
                screenBounds.Position.Y + (screenBounds.Size.Y - winSize.Y) / 2
            );
            DisplayServer.WindowSetPosition(centerPos);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, true);
            DisplayServer.WindowMoveToForeground();
            DisplayServer.WindowRequestAttention();

            // 1.5 秒后自动解除强制置顶，恢复正常多任务操作
            GetTree().CreateTimer(1.5).Timeout += () =>
            {
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.AlwaysOnTop, false);
            };
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[WindowSetup] Error: {ex.Message}");
        }

        // 默认镜头配置与帧捕获参数
        var cmdArgs = CommandLineArgs.Get();
        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (cmdArgs[i] == "--capture-frames" && i + 1 < cmdArgs.Length && int.TryParse(cmdArgs[i + 1], out var targetFrames))
            {
                maxCaptureFrames = targetFrames;
            }
        }

        if (ArenaCamera is not null)
        {
            if (System.Array.Exists(cmdArgs, a => a == "--camera-closeup"))
            {
                ApplyCameraMode(CameraMode.FrontlineCloseUp);
            }
            else if (System.Array.Exists(cmdArgs, a => a == "--camera-topdown"))
            {
                ApplyCameraMode(CameraMode.RtsTopDown);
            }
            else
            {
                ApplyCameraMode(CameraMode.CinematicDirector);
            }
        }

        InitBattleHud();

        // 为岸边岩石赋予深灰花岗岩 PBR 材质，消除未赋予材质的塑料反光
        var rockMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.32f, 0.35f),
            Roughness = 0.90f,
            Metallic = 0.05f
        };
        var nature = GetNodeOrNull<Node3D>("NatureDecorations");
        if (nature is not null)
        {
            foreach (var mesh in nature.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            {
                if (mesh.Name.ToString().ToLower().Contains("rock") || mesh.GetParent()?.Name.ToString().ToLower().Contains("rock") == true)
                {
                    mesh.MaterialOverride = rockMat;
                }
            }
        }

        // 初始化双方海军舰队（位于 Z: -30 ~ -14 南侧海湾水道，小地图/竞技场无航母，以战列舰与巡逻艇为主力）
        SpawnUnitForSide("battleship", true, new Vector3(-28f, 0.0f, -22.0f));
        SpawnUnitForSide("battleship", true, new Vector3(-18f, 0.0f, -29.0f));
        SpawnUnitForSide("destroyer_ship", true, new Vector3(-16f, 0.0f, -14.5f));
        SpawnUnitForSide("submarine", true, new Vector3(-10f, 0.0f, -21.0f));

        SpawnUnitForSide("battleship", false, new Vector3(28f, 0.0f, -22.0f));
        SpawnUnitForSide("battleship", false, new Vector3(18f, 0.0f, -29.0f));
        SpawnUnitForSide("destroyer_ship", false, new Vector3(16f, 0.0f, -14.5f));
        SpawnUnitForSide("submarine", false, new Vector3(10f, 0.0f, -21.0f));

        // 阶段一：开始建筑（双方主基地、重工工厂、机场、兵营、发电厂、金矿与防御工事全套红警建筑群施工）
        if (BattleGameManager.Instance is { } battleMgr)
        {
            blueBase = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("main_base"), new Vector3(-38f, 0f, 14f), true);
            blueArmorFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("armor_factory"), new Vector3(-27f, 0f, 10.5f), true);
            blueBarracks = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("barracks"), new Vector3(-27f, 0f, 17.5f), true);
            blueTankFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("tank_factory"), new Vector3(-38f, 0f, 25f), true);
            bluePowerPlant = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("power_plant"), new Vector3(-48f, 0f, 7f), true);
            blueGoldMine = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("gold_mine"), new Vector3(-48f, 0f, 20f), true);
            blueAirFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("air_factory"), new Vector3(-38f, 0f, 3.5f), true);
            blueAirfield = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("airfield"), new Vector3(-48f, 0f, -2.5f), true);
            blueNavalYard = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("naval_yard"), new Vector3(-30f, 0f, -8f), true);
            blueTurret = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("turret"), new Vector3(-16f, 0f, 14f), true);

            redBase = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("main_base"), new Vector3(38f, 0f, 14f), false);
            redArmorFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("armor_factory"), new Vector3(27f, 0f, 10.5f), false);
            redBarracks = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("barracks"), new Vector3(27f, 0f, 17.5f), false);
            redTankFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("tank_factory"), new Vector3(38f, 0f, 25f), false);
            redPowerPlant = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("power_plant"), new Vector3(48f, 0f, 7f), false);
            redGoldMine = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("gold_mine"), new Vector3(48f, 0f, 20f), false);
            redAirFactory = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("air_factory"), new Vector3(38f, 0f, 3.5f), false);
            redAirfield = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("airfield"), new Vector3(48f, 0f, -2.5f), false);
            redNavalYard = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("naval_yard"), new Vector3(30f, 0f, -8f), false);
            redTurret = battleMgr.SpawnBuilding(BattleBuildingCatalog.Get("turret"), new Vector3(16f, 0f, 14f), false);

            var buildingsToConstruct = new[]
            {
                blueArmorFactory, blueBarracks, blueTankFactory, bluePowerPlant, blueGoldMine, blueAirFactory, blueAirfield, blueNavalYard, blueTurret,
                redArmorFactory, redBarracks, redTankFactory, redPowerPlant, redGoldMine, redAirFactory, redAirfield, redNavalYard, redTurret
            };
            foreach (var b in buildingsToConstruct)
            {
                b?.BeginConstruction(3.5f);
            }

            phase1Constructed = true;
            blueBase?.SetSelected(true);
            _battleHud?.AddCombatLog("🏗️", "【阶段一：红警基地集群展开】发电厂、矿厂、重工厂房、机场与防御炮塔同步施工！", new Color(0.2f, 0.85f, 1f));
            BattleFeedback.Command(this, new Vector3(-28f, 3.8f, 14f), "【阶段一：基地展开】全套红警风格军工建筑群施工中...", new Color(0.2f, 0.85f, 1f));
        }

        // 自动挂载实机战斗与步态量化检测器（延迟至阶段三出兵后执行完整校验）
        var verifier = new CombatLocomotionVerifier { Name = "CombatLocomotionVerifier" };
        AddChild(verifier);
        GetTree().CreateTimer(9.2).Timeout += () =>
        {
            var testInfantry = blueUnits.FirstOrDefault(u => BattleUnitCatalog.IsInfantryLike(u.UnitKey))
                ?? redUnits.FirstOrDefault(u => BattleUnitCatalog.IsInfantryLike(u.UnitKey));
            var testTank = blueUnits.FirstOrDefault(u => !BattleUnitCatalog.IsAirUnit(u.UnitKey) && !BattleUnitCatalog.IsNavalUnit(u.UnitKey) && !BattleUnitCatalog.IsInfantryLike(u.UnitKey))
                ?? redUnits.FirstOrDefault(u => !BattleUnitCatalog.IsAirUnit(u.UnitKey) && !BattleUnitCatalog.IsNavalUnit(u.UnitKey) && !BattleUnitCatalog.IsInfantryLike(u.UnitKey));
            var testHeli = blueUnits.FirstOrDefault(u => u.UnitKey == "scout_plane")
                ?? redUnits.FirstOrDefault(u => u.UnitKey == "scout_plane");
            var testArtillery = blueUnits.FirstOrDefault(u => BattleUnitCatalog.SupportsArtilleryDeploy(u.UnitKey))
                ?? redUnits.FirstOrDefault(u => BattleUnitCatalog.SupportsArtilleryDeploy(u.UnitKey));
            var testBomber = blueUnits.FirstOrDefault(u => u.UnitKey == "bomber")
                ?? redUnits.FirstOrDefault(u => u.UnitKey == "bomber");
            RtsUnit? testCarrier = null;

            verifier.RunAllChecks(testInfantry, testTank, testHeli, testArtillery, testBomber, testCarrier);
        };

        // 自动挂载全套科技与建筑体系量化核验套件
        var techVerifier = new TechAndBuildingVerifier { Name = "TechAndBuildingVerifier" };
        AddChild(techVerifier);
        GetTree().CreateTimer(9.6).Timeout += () =>
        {
            techVerifier.RunAllChecks();
        };
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        Engine.TimeScale = 1.0;
    }

    public override void _Process(double delta)
    {
        if (maxCaptureFrames > 0)
        {
            currentCapturedFrames++;
            if (currentCapturedFrames >= maxCaptureFrames)
            {
                GetTree().Quit();
                return;
            }
        }

        float dt = (float)delta;
        gameTimer += dt;
        waveTimer += dt;
        retargetTimer += dt;

        // 演示：实机战术建筑摆放选点与占地网格指示器 (t >= 0.8s)
        if (gameTimer >= 0.8f && !demoPlacementSpawned)
        {
            demoPlacementSpawned = true;
            if (BattleGameManager.Instance is { } mgr)
            {
                demoPlacementPreview = mgr.CreateBuildingPlacementPreview("turret");
                if (demoPlacementPreview is not null)
                {
                    demoPlacementPreview.Position = new Vector3(-20f, 0f, 7.5f);
                    AddChild(demoPlacementPreview);
                    mgr.ApplyBuildingPlacementPreviewAppearance(demoPlacementPreview, true);
                    _battleHud?.AddCombatLog("📐", "【战术建筑摆放模式】防御炮台选点就绪：地表绿色占地网格与射程虚影已激活", new Color(0.2f, 1f, 0.5f));
                    BattleFeedback.Command(this, new Vector3(-20f, 2.5f, 7.5f), "【建筑摆放】战术防御炮塔定位选点中...", new Color(0.2f, 1f, 0.5f));
                }
            }
        }

        // 演示：完成战术建筑摆放并落座施工 (t >= 3.6f)
        if (gameTimer >= 3.6f && !demoPlacementConfirmed)
        {
            demoPlacementConfirmed = true;
            if (demoPlacementPreview is not null && GodotObject.IsInstanceValid(demoPlacementPreview))
            {
                demoPlacementPreview.QueueFree();
                demoPlacementPreview = null;
            }
            if (BattleGameManager.Instance is { } mgr)
            {
                var placedTurret = mgr.SpawnBuilding(BattleBuildingCatalog.Get("turret"), new Vector3(-20f, 0f, 7.5f), true);
                placedTurret?.BeginConstruction(2.8f);
                _battleHud?.AddCombatLog("🔨", "【战术建筑完成摆放】前沿哨所防御炮塔落座！脚手架与粒子施工启动！", new Color(0.3f, 0.95f, 0.4f));
                BattleFeedback.Command(this, new Vector3(-20f, 3.2f, 7.5f), "【建筑落座】防御炮塔摆放成功，开始构筑！", new Color(0.3f, 0.95f, 0.4f));
            }
        }

        // 阶段二：建筑升级调度 (t >= 4.2s)
        if (gameTimer >= 4.2f && !phase2Upgraded)
        {
            phase2Upgraded = true;
            blueArmorFactory?.ApplyUpgradeLevel(2, true);
            blueBarracks?.ApplyUpgradeLevel(2, true);
            blueTankFactory?.ApplyUpgradeLevel(2, true);
            blueAirFactory?.ApplyUpgradeLevel(2, true);
            redArmorFactory?.ApplyUpgradeLevel(2, true);
            redBarracks?.ApplyUpgradeLevel(2, true);
            redTankFactory?.ApplyUpgradeLevel(2, true);
            redAirFactory?.ApplyUpgradeLevel(2, true);

            _battleHud?.AddCombatLog("⭐", "【阶段二：升级建筑】重工与战术机库升级至 Lv.2！解锁现代坦克歼击车、特种喷火兵与高阶战机！", Colors.Gold);
            BattleFeedback.Command(this, new Vector3(-28f, 5.0f, 10.5f), "★ 装甲工厂升级至 Lv.2！解锁现代坦克歼击车！", Colors.Gold);
            BattleFeedback.Command(this, new Vector3(-28f, 5.0f, 17.5f), "★ 突击兵营升级至 Lv.2！解锁特种喷火兵！", Colors.Gold);
        }

        // 阶段三：基地出兵调度 (t >= 7.8s)
        if (gameTimer >= 7.8f && !phase3UnitsProduced)
        {
            phase3UnitsProduced = true;

            // 蓝方装甲力量出库 (坦克歼击车主炮朝前，重装坦克整备前出)
            SpawnUnitForSide("tank_destroyer", true, new Vector3(-23f, 0f, 10.5f));
            SpawnUnitForSide("heavy_tank", true, new Vector3(-23f, 0f, 13.0f));
            SpawnUnitForSide("tank", true, new Vector3(-21f, 0f, 15.5f));
            SpawnUnitForSide("anti_air_gun", true, new Vector3(-26f, 0f, 12.0f));
            SpawnUnitForSide("artillery", true, new Vector3(-27f, 0f, 14.5f));

            // 蓝方步兵排
            SpawnUnitForSide("infantry", true, new Vector3(-19f, 0f, 6.0f));
            SpawnUnitForSide("infantry_flamethrower", true, new Vector3(-18f, 0f, 7.2f));
            SpawnUnitForSide("infantry_artillery", true, new Vector3(-21f, 0f, 5.0f));

            // 红方装甲力量与步兵
            SpawnUnitForSide("heavy_tank", false, new Vector3(23f, 0f, 10.5f));
            SpawnUnitForSide("tank", false, new Vector3(23f, 0f, 13.0f));
            SpawnUnitForSide("tank_destroyer", false, new Vector3(21f, 0f, 15.5f));
            SpawnUnitForSide("anti_air_gun", false, new Vector3(26f, 0f, 12.0f));
            SpawnUnitForSide("artillery", false, new Vector3(27f, 0f, 14.5f));

            SpawnUnitForSide("infantry", false, new Vector3(19f, 0f, 6.0f));
            SpawnUnitForSide("infantry_flamethrower", false, new Vector3(18f, 0f, 7.2f));
            SpawnUnitForSide("infantry_artillery", false, new Vector3(21f, 0f, 5.0f));

            // 空军战术打击群：直升机(8.5m低空掠地) vs 战机(18.5m高空截击) vs 轰炸机(24.0m同温层空袭)
            SpawnUnitForSide("bomber", true, new Vector3(-26f, BattleUnitCatalog.SpawnHeight("bomber"), 7.0f));
            SpawnUnitForSide("bomber", false, new Vector3(26f, BattleUnitCatalog.SpawnHeight("bomber"), 7.0f));
            SpawnUnitForSide("fighter", true, new Vector3(-24f, BattleUnitCatalog.SpawnHeight("fighter"), -15.0f));
            SpawnUnitForSide("fighter", false, new Vector3(24f, BattleUnitCatalog.SpawnHeight("fighter"), -15.0f));
            SpawnUnitForSide("scout_plane", true, new Vector3(-18f, BattleUnitCatalog.SpawnHeight("scout_plane"), 6.0f));
            SpawnUnitForSide("scout_plane", false, new Vector3(18f, BattleUnitCatalog.SpawnHeight("scout_plane"), 6.0f));
            SpawnUnitForSide("scout_helicopter", true, new Vector3(-16f, BattleUnitCatalog.SpawnHeight("scout_helicopter"), 9.0f));
            SpawnUnitForSide("scout_helicopter", false, new Vector3(16f, BattleUnitCatalog.SpawnHeight("scout_helicopter"), 9.0f));

            _battleHud?.AddCombatLog("🚀", "【阶段三：出兵出库】坦克歼击车、重装坦克与突击兵团出厂集结，向前推进！", Colors.Cyan);
            BattleFeedback.Command(this, new Vector3(-20f, 4.0f, 12.0f), "【阶段三：出兵】装甲集群出库集结，向前推进！", Colors.Cyan);

            var leadTank = blueUnits.FirstOrDefault(u => u.UnitKey == "tank" || u.UnitKey == "medium_tank") ?? blueUnits.FirstOrDefault();
            leadTank?.SetSelected(true);
        }

        // 阶段四：战场科技主动释放实战演练 (t >= 10.5s)
        if (gameTimer >= 10.5f && !phase4TechCasted)
        {
            phase4TechCasted = true;
            if (BattleGameManager.Instance is { } mgr)
            {
                mgr.TryCastBattleTech("firepower", new Vector3(-15f, 0f, 12f), out var msg1, out var aff1);
                mgr.TryCastBattleTech("speed", new Vector3(-20f, 0f, 10f), out var msg2, out var aff2);
                mgr.TryCastBattleTech("air_refit", new Vector3(-24f, 11f, -15f), out var msg3, out var aff3);
                mgr.TryCastBattleTech("overdrive", new Vector3(-26f, 0f, 12f), out var msg4, out var aff4);
                mgr.TryCastBattleTech("radar_scan", new Vector3(0f, 0f, 0f), out var msg5, out var aff5);

                _battleHud?.AddCombatLog("⚡", "【阶段四：科技实战释放】指挥官启动【火力强化】与【机动冲锋】！战机完成【战鹰整备】！激活【雷达侦测】！", Colors.Aqua);
                BattleFeedback.Command(this, new Vector3(-15f, 5.0f, 12f), "⚡【科技启动】火力强化覆盖前锋部队！", Colors.Aqua);
            }
        }

        if (autoWaveEnabled && gameTimer >= 18.0f && waveTimer >= WaveInterval)
        {
            waveTimer = 0f;
            SpawnBalancedWave(true);
            SpawnBalancedWave(false);
        }

        // 定期检索战线与自动接敌推进
        if (retargetTimer >= 0.45f)
        {
            retargetTimer = 0f;
            UpdateArmyOrders();
        }

        // 移除阵亡单位引用并刷新统计
        blueUnits.RemoveAll(u => !GodotObject.IsInstanceValid(u) || u.IsDead);
        redUnits.RemoveAll(u => !GodotObject.IsInstanceValid(u) || u.IsDead);

        // 严格空间域物理硬约束：杜绝水面舰艇上岸、陆军战车下水
        EnforceDomainBoundaries(blueUnits);
        EnforceDomainBoundaries(redUnits);

        UpdateUiStats();

        // 键盘 WASD / 方向键平移镜头
        if (ArenaCamera is not null)
        {
            Vector3 camMove = Vector3.Zero;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) camMove -= ArenaCamera.GlobalTransform.Basis.X;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) camMove += ArenaCamera.GlobalTransform.Basis.X;
            var fwd = -ArenaCamera.GlobalTransform.Basis.Z;
            fwd.Y = 0f;
            if (fwd.LengthSquared() > 0.001f) fwd = fwd.Normalized();
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) camMove += fwd;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) camMove -= fwd;

            if (camMove.LengthSquared() > 0.001f || isMiddleDragging)
            {
                if (currentCamMode == CameraMode.CinematicDirector)
                    currentCamMode = CameraMode.RtsTopDown;

                if (camMove.LengthSquared() > 0.001f)
                {
                    ArenaCamera.GlobalPosition += camMove.Normalized() * (35f * dt);
                }
            }
            else if (currentCamMode == CameraMode.CinematicDirector)
            {
                UpdateCinematicDirector(dt);
            }

            // 物理爆炸镜头柔和震颤与阻尼 (Harmonic Camera Shake using HOffset / VOffset)
            if (shakeTimer > 0f)
            {
                shakeTimer -= dt;
                if (shakeTimer <= 0f)
                {
                    shakeIntensity = 0f;
                    ArenaCamera.HOffset = 0f;
                    ArenaCamera.VOffset = 0f;
                }
                else
                {
                    float ratio = shakeTimer / shakeDuration;
                    float curr = shakeIntensity * (ratio * ratio);
                    const float ShakeFreq = 26.0f;
                    ArenaCamera.HOffset = Mathf.Sin(shakeTimer * ShakeFreq) * curr;
                    ArenaCamera.VOffset = Mathf.Cos(shakeTimer * (ShakeFreq * 0.82f)) * (curr * 0.65f);
                }
            }
            else if (ArenaCamera.HOffset != 0f || ArenaCamera.VOffset != 0f)
            {
                ArenaCamera.HOffset = 0f;
                ArenaCamera.VOffset = 0f;
            }
        }
    }

    public override void _UnhandledInput(InputEvent evt)
    {
        if (ArenaCamera is null) return;

        // 鼠标滚轮缩放
        if (evt is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                if (currentCamMode == CameraMode.CinematicDirector)
                    currentCamMode = CameraMode.RtsTopDown;
                ArenaCamera.GlobalPosition += ArenaCamera.GlobalTransform.Basis.Z * -1.8f;
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                if (currentCamMode == CameraMode.CinematicDirector)
                    currentCamMode = CameraMode.RtsTopDown;
                ArenaCamera.GlobalPosition += ArenaCamera.GlobalTransform.Basis.Z * 1.8f;
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
                if (mb.Pressed && currentCamMode == CameraMode.CinematicDirector)
                    currentCamMode = CameraMode.RtsTopDown;
                isMiddleDragging = mb.Pressed;
                lastMousePos = mb.Position;
            }
        }
        else if (evt is InputEventMouseMotion mm && isMiddleDragging)
        {
            var diff = mm.Position - lastMousePos;
            lastMousePos = mm.Position;
            var right = ArenaCamera.GlobalTransform.Basis.X;
            var forward = -ArenaCamera.GlobalTransform.Basis.Z;
            forward.Y = 0f;
            forward = forward.Normalized();
            ArenaCamera.GlobalPosition += (-right * diff.X + forward * diff.Y) * 0.08f;
        }
    }

    void EnforceDomainBoundaries(List<RtsUnit> units)
    {
        const float NavalMinZ = -34.0f;
        const float NavalMaxZ = -10.0f;
        const float LandMinZ = 3.5f;
        const float LandMaxZ = 22.0f;

        foreach (var unit in units)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.IsDead) continue;

            if (BattleUnitCatalog.IsNavalUnit(unit.UnitKey))
            {
                // 海军单位：Z 严格限制在海湾安全深水水道 [-34.0, -10.0]，绝不可上岸搁浅
                var pos = unit.GlobalPosition;
                if (pos.Z > NavalMaxZ || pos.Z < NavalMinZ)
                {
                    float clampedZ = Mathf.Clamp(pos.Z, NavalMinZ, NavalMaxZ);
                    unit.GlobalPosition = new Vector3(pos.X, pos.Y, clampedZ);
                }
                var target = unit.TargetPosition;
                if (target.Z > NavalMaxZ || target.Z < NavalMinZ)
                {
                    float clampedTargetZ = Mathf.Clamp(target.Z, NavalMinZ, NavalMaxZ);
                    unit.TargetPosition = new Vector3(target.X, target.Y, clampedTargetZ);
                }
                if ((unit.GlobalPosition.Z >= NavalMaxZ - 0.5f && unit.Velocity.Z > 0) ||
                    (unit.GlobalPosition.Z <= NavalMinZ + 0.5f && unit.Velocity.Z < 0))
                {
                    unit.Velocity = new Vector3(unit.Velocity.X, unit.Velocity.Y, 0f);
                }
            }
            else if (!BattleUnitCatalog.IsAirUnit(unit.UnitKey))
            {
                // 陆军单位：Z 严格限制在陆地开阔平原 [3.5, 22.0]，绝不可涉水下海
                var pos = unit.GlobalPosition;
                if (pos.Z < LandMinZ || pos.Z > LandMaxZ)
                {
                    float clampedZ = Mathf.Clamp(pos.Z, LandMinZ, LandMaxZ);
                    unit.GlobalPosition = new Vector3(pos.X, pos.Y, clampedZ);
                }
                var target = unit.TargetPosition;
                if (target.Z < LandMinZ || target.Z > LandMaxZ)
                {
                    float clampedTargetZ = Mathf.Clamp(target.Z, LandMinZ, LandMaxZ);
                    unit.TargetPosition = new Vector3(target.X, target.Y, clampedTargetZ);
                }
                if ((unit.GlobalPosition.Z <= LandMinZ + 0.5f && unit.Velocity.Z < 0) ||
                    (unit.GlobalPosition.Z >= LandMaxZ - 0.5f && unit.Velocity.Z > 0))
                {
                    unit.Velocity = new Vector3(unit.Velocity.X, unit.Velocity.Y, 0f);
                }
            }
        }
    }

    void UpdateArmyOrders()
    {
        // 蓝军推进行动
        foreach (var blue in blueUnits)
        {
            if (!GodotObject.IsInstanceValid(blue) || blue.IsDead) continue;
            if (blue.AttackTarget is null || !GodotObject.IsInstanceValid(blue.AttackTarget) || ((RtsUnit)blue.AttackTarget).IsDead || !blue.CanAttackTarget(blue.AttackTarget))
            {
                var nearestRed = FindNearestLiveUnit(blue.GlobalPosition, redUnits, blue.UnitKey);
                if (nearestRed is not null)
                {
                    blue.Attack(nearestRed);
                }
                else
                {
                    // 若是大型水面舰艇（如航母、战列舰）且已经在平稳巡航中，不重复派发微小位移指令，避免打断巡航航向造成晃动
                    if (BattleUnitCatalog.IsNavalUnit(blue.UnitKey) && blue.AttackMoving && (blue.TargetPosition - blue.GlobalPosition).LengthSquared() > 9.0f)
                    {
                        continue;
                    }
                    float targetZ = BattleUnitCatalog.IsNavalUnit(blue.UnitKey)
                        ? Mathf.Clamp(blue.GlobalPosition.Z, -32f, -12f)
                        : Mathf.Clamp(blue.GlobalPosition.Z, 4.0f, 18.0f);
                    blue.AttackMoveTo(new Vector3(RedBaseX, blue.CruiseHeight, targetZ));
                }
            }
        }

        // 红军推进行动
        foreach (var red in redUnits)
        {
            if (!GodotObject.IsInstanceValid(red) || red.IsDead) continue;
            if (red.AttackTarget is null || !GodotObject.IsInstanceValid(red.AttackTarget) || ((RtsUnit)red.AttackTarget).IsDead || !red.CanAttackTarget(red.AttackTarget))
            {
                var nearestBlue = FindNearestLiveUnit(red.GlobalPosition, blueUnits, red.UnitKey);
                if (nearestBlue is not null)
                {
                    red.Attack(nearestBlue);
                }
                else
                {
                    if (BattleUnitCatalog.IsNavalUnit(red.UnitKey) && red.AttackMoving && (red.TargetPosition - red.GlobalPosition).LengthSquared() > 9.0f)
                    {
                        continue;
                    }
                    float targetZ = BattleUnitCatalog.IsNavalUnit(red.UnitKey)
                        ? Mathf.Clamp(red.GlobalPosition.Z, -32f, -12f)
                        : Mathf.Clamp(red.GlobalPosition.Z, 4.0f, 18.0f);
                    red.AttackMoveTo(new Vector3(BlueBaseX, red.CruiseHeight, targetZ));
                }
            }
        }
    }

    RtsUnit? FindNearestLiveUnit(Vector3 fromPos, List<RtsUnit> targets, string attackerKey)
    {
        bool isNavalAttacker = BattleUnitCatalog.IsNavalUnit(attackerKey);
        bool isAirAttacker = BattleUnitCatalog.IsAirUnit(attackerKey);
        bool isLandAttacker = !isNavalAttacker && !isAirAttacker;

        // 第 0 优先级：防空单位（防空炮/制空战机）绝对优先猎杀空中敌机，解除空袭威胁
        if (attackerKey is "anti_air_gun" or "fighter")
        {
            RtsUnit? bestAirTarget = null;
            float minAirDistSq = float.MaxValue;
            foreach (var t in targets)
            {
                if (!GodotObject.IsInstanceValid(t) || t.IsDead) continue;
                if (!BattleUnitCatalog.IsAirUnit(t.UnitKey)) continue;
                if (!BattleUnitCatalog.CanAttackTargetType(attackerKey, t.UnitKey)) continue;

                float distSq = fromPos.DistanceSquaredTo(t.GlobalPosition);
                if (distSq < minAirDistSq)
                {
                    minAirDistSq = distSq;
                    bestAirTarget = t;
                }
            }
            if (bestAirTarget is not null)
                return bestAirTarget;
        }

        // 第 1 优先级：同作战域优先（海军优先打海军/空军，陆军优先打陆军/空军，彻底杜绝跨域冲锋导致的冲上海岸或开进海里）
        RtsUnit? bestSameDomain = null;
        float minSameDomainDistSq = float.MaxValue;

        foreach (var t in targets)
        {
            if (!GodotObject.IsInstanceValid(t) || t.IsDead) continue;
            if (!BattleUnitCatalog.CanAttackTargetType(attackerKey, t.UnitKey)) continue;

            bool isNavalTarget = BattleUnitCatalog.IsNavalUnit(t.UnitKey);
            bool isAirTarget = BattleUnitCatalog.IsAirUnit(t.UnitKey);
            bool isLandTarget = !isNavalTarget && !isAirTarget;

            bool preferredDomain = isAirAttacker ||
                (isNavalAttacker && (isNavalTarget || isAirTarget)) ||
                (isLandAttacker && (isLandTarget || isAirTarget));

            if (!preferredDomain) continue;

            float distSq = fromPos.DistanceSquaredTo(t.GlobalPosition);
            if (distSq < minSameDomainDistSq)
            {
                minSameDomainDistSq = distSq;
                bestSameDomain = t;
            }
        }

        if (bestSameDomain is not null)
            return bestSameDomain;

        // 第 2 优先级：跨作战域打击（如战列舰远距离岸轰陆军，火炮防空打击水面等）
        RtsUnit? bestCrossDomain = null;
        float minCrossDistSq = float.MaxValue;

        foreach (var t in targets)
        {
            if (!GodotObject.IsInstanceValid(t) || t.IsDead) continue;
            if (!BattleUnitCatalog.CanAttackTargetType(attackerKey, t.UnitKey)) continue;

            float distSq = fromPos.DistanceSquaredTo(t.GlobalPosition);
            if (distSq < minCrossDistSq)
            {
                minCrossDistSq = distSq;
                bestCrossDomain = t;
            }
        }

        return bestCrossDomain;
    }

    public void SpawnUnitForSide(string unitKey, bool isBlue, Vector3? customPos = null)
    {
        if (BattleGameManager.Instance is not { } mgr) return;

        Vector3 spawnPos;
        if (customPos.HasValue)
        {
            spawnPos = customPos.Value;
        }
        else
        {
            float baseX = isBlue ? BlueBaseX : RedBaseX;
            float jitterX = rng.RandfRange(-3f, 3f);
            float jitterZ;
            if (BattleUnitCatalog.IsNavalUnit(unitKey))
            {
                // 海水区域适航走廊 Z: -32 ~ -12 (居于 Z=-22 宽阔演习海湾水面)
                jitterZ = rng.RandfRange(-32f, -12f);
            }
            else if (BattleUnitCatalog.IsAirUnit(unitKey))
            {
                // 空中巡航走廊 Z: -30 ~ 15
                jitterZ = rng.RandfRange(-30f, 15f);
            }
            else
            {
                // 陆军交火走廊 Z: 4.0 ~ 18.0
                jitterZ = rng.RandfRange(4.0f, 18.0f);
            }

            float spawnY = BattleUnitCatalog.SpawnHeight(unitKey);
            spawnPos = new Vector3(baseX + jitterX, spawnY, jitterZ);
        }

        var targetX = isBlue ? RedBaseX : BlueBaseX;
        float destZ = BattleUnitCatalog.IsNavalUnit(unitKey)
            ? Mathf.Clamp(spawnPos.Z, -32f, -12f)
            : Mathf.Clamp(spawnPos.Z, 4.0f, 18.0f);
        var marchDest = new Vector3(targetX, spawnPos.Y, destZ);

        var unit = mgr.SpawnUnit(unitKey, spawnPos, isBlue, marchDest);
        if (unit is not null)
        {
            var marchDir = marchDest - spawnPos;
            marchDir.Y = 0f;
            if (marchDir.LengthSquared() > 0.001f)
                unit.FaceDirectionImmediate(marchDir);
            unit.Died += OnUnitDied;
            if (isBlue) blueUnits.Add(unit);
            else redUnits.Add(unit);

            // 立即朝敌方前线挺进
            unit.AttackMoveTo(marchDest);
        }
    }

    void OnUnitDied(RtsUnit unit)
    {
        string unitName = BattleUnitCatalog.Get(unit.UnitKey).DisplayName;
        bool isNaval = BattleUnitCatalog.IsNavalUnit(unit.UnitKey);
        bool isAir = BattleUnitCatalog.IsAirUnit(unit.UnitKey);
        bool isHeavy = unit.UnitKey is "battleship" or "aircraft_carrier" or "heavy_tank" or "bomber";

        if (unit.PlayerOwned)
        {
            redKills++;
            if (isNaval)
            {
                _battleHud?.AddCombatLog("🌊", $"【战损】蓝方舰艇 [{unitName}] 破损进水正在沉没！", new Color(1f, 0.45f, 0.45f));
            }
            else if (isAir)
            {
                _battleHud?.AddCombatLog("🔥", $"【战损】蓝方战机 [{unitName}] 遭凌空击落！", new Color(1f, 0.45f, 0.45f));
            }
            else
            {
                _battleHud?.AddCombatLog("⚔️", $"红方歼灭了蓝方 [{unitName}]", new Color(1f, 0.42f, 0.42f));
            }
        }
        else
        {
            blueKills++;
            if (isNaval)
            {
                _battleHud?.AddCombatLog("⚓", $"【战果】蓝方击沉红方战舰 [{unitName}] (+核心战果)！", new Color(0.35f, 0.90f, 1f));
            }
            else if (isAir)
            {
                _battleHud?.AddCombatLog("🎯", $"【战果】蓝方防空火力击坠红方战机 [{unitName}]！", new Color(0.35f, 0.85f, 1f));
            }
            else
            {
                _battleHud?.AddCombatLog("💥", $"蓝方摧毁了红方 [{unitName}] (+战果)", new Color(0.35f, 0.85f, 1f));
            }
        }
    }

    public void SpawnBalancedWave(bool isBlue)
    {
        // 陆海空三军联合作战波次
        // 1. 陆军主力：1 重坦/中坦/轻坦 + 1 支援火炮/防空车 + 2 步兵
        var landArmor = rng.RandiRange(0, 2) switch
        {
            0 => "heavy_tank",
            1 => "tank",
            _ => "light_tank"
        };
        var landSupport = rng.RandiRange(0, 1) == 0 ? "anti_air_gun" : "artillery";

        SpawnUnitForSide(landArmor, isBlue);
        SpawnUnitForSide(landSupport, isBlue);
        // 全面协同：每波增援均必须包含突击步枪兵、近战喷火兵与远程迫击炮兵！
        SpawnUnitForSide("infantry", isBlue);
        SpawnUnitForSide("infantry_flamethrower", isBlue);
        SpawnUnitForSide("infantry_artillery", isBlue);

        // 2. 空军打击群：战斗机 / 轰炸机 / 侦察机
        var airType = rng.RandiRange(0, 2) switch
        {
            0 => "fighter",
            1 => "bomber",
            _ => "scout_plane"
        };
        SpawnUnitForSide(airType, isBlue);

        // 3. 海军战舰梯队：驱逐舰 / 潜艇 / 战列舰
        var navalType = rng.RandiRange(0, 2) switch
        {
            0 => "destroyer_ship",
            1 => "submarine",
            _ => "battleship"
        };
        SpawnUnitForSide(navalType, isBlue);

        _battleHud?.AddCombatLog("🚀", isBlue ? "蓝方海陆空混编主力已抵达前线" : "红方重型装甲援军完成战场集结", isBlue ? new Color(0.35f, 0.85f, 1f) : new Color(1f, 0.45f, 0.45f));
    }

    public void TriggerAllOutClash()
    {
        _battleHud?.AddCombatLog("⚔️", "⚔️ 全军突击！海陆空大兵团全面总决战打响！", new Color(1f, 0.85f, 0.25f));

        // ⚔️ 陆海空全军三维立体大兵团对抗
        // 陆军机械化集群 (Z: 4.5 ~ 20.0 陆地区域，坚决不上水面)
        string[] groundFleet = { "heavy_tank", "tank", "tank", "light_tank", "artillery", "anti_air_gun", "infantry", "infantry", "infantry_flamethrower", "infantry_artillery" };
        for (int i = 0; i < groundFleet.Length; i++)
        {
            float z = 4.5f + i * 2.2f;
            SpawnUnitForSide(groundFleet[i], true, new Vector3(BlueBaseX + rng.RandfRange(-2f, 2f), 0f, z));
            SpawnUnitForSide(groundFleet[i], false, new Vector3(RedBaseX + rng.RandfRange(-2f, 2f), 0f, z));
        }

        // 空军战机巡航集群
        string[] airFleet = { "fighter", "fighter", "bomber", "scout_plane" };
        for (int i = 0; i < airFleet.Length; i++)
        {
            float z = -28f + i * 10.0f;
            SpawnUnitForSide(airFleet[i], true, new Vector3(BlueBaseX - 3f, 9.5f, z));
            SpawnUnitForSide(airFleet[i], false, new Vector3(RedBaseX + 3f, 9.5f, z));
        }

        // 海军水面水下舰队群 (Z: -30.0 ~ -12.0 深水区域，航母禁用，替换为巡逻快艇)
        string[] seaFleet = { "destroyer_ship", "battleship", "submarine", "patrol_boat" };
        for (int i = 0; i < seaFleet.Length; i++)
        {
            float z = -30.0f + i * 5.0f;
            SpawnUnitForSide(seaFleet[i], true, new Vector3(BlueBaseX + rng.RandfRange(-1f, 1f), 0f, z));
            SpawnUnitForSide(seaFleet[i], false, new Vector3(RedBaseX + rng.RandfRange(-1f, 1f), 0f, z));
        }
    }

    public void ClearBattlefield()
    {
        foreach (var u in blueUnits.Concat(redUnits))
        {
            if (GodotObject.IsInstanceValid(u) && !u.IsDead)
            {
                u.QueueFree();
            }
        }
        blueUnits.Clear();
        redUnits.Clear();
    }

    void ApplyCameraMode(CameraMode mode)
    {
        if (ArenaCamera is null) return;
        currentCamMode = mode;
        switch (mode)
        {
            case CameraMode.CinematicDirector:
                ArenaCamera.GlobalPosition = new Vector3(0f, 29.0f, 16.0f);
                ArenaCamera.LookAt(new Vector3(0f, 0.8f, -4.0f), Vector3.Up);
                ArenaCamera.Fov = 48f;
                break;
            case CameraMode.RtsTopDown:
                ArenaCamera.GlobalPosition = new Vector3(0f, 30.0f, 16.0f);
                ArenaCamera.LookAt(new Vector3(0f, 0.8f, -4.0f), Vector3.Up);
                ArenaCamera.Fov = 48f;
                break;
            case CameraMode.FrontlineCloseUp:
                ArenaCamera.GlobalPosition = new Vector3(-8f, 22.0f, 18.0f);
                ArenaCamera.LookAt(new Vector3(0f, 1.0f, 2.0f), Vector3.Up);
                ArenaCamera.Fov = 48f;
                break;
            case CameraMode.BirdEyeOverview:
                ArenaCamera.GlobalPosition = new Vector3(0f, 38.0f, 18.0f);
                ArenaCamera.LookAt(new Vector3(0f, 0.8f, -5.0f), Vector3.Up);
                ArenaCamera.Fov = 52f;
                break;
        }

        string modeName = mode switch
        {
            CameraMode.CinematicDirector => "🎬 电影导演巡航 (Auto Cine)",
            CameraMode.RtsTopDown => "📐 RTS 战术俯视 (TopDown)",
            CameraMode.FrontlineCloseUp => "🔍 前沿交火特写 (CloseUp)",
            _ => "🦅 战场鹰眼全览 (BirdEye)"
        };
        _battleHud?.AddCombatLog("🎥", $"已切换至镜头视角: {modeName}", new Color(0.85f, 0.9f, 1f));
    }

    void UpdateCinematicDirector(float dt)
    {
        if (ArenaCamera is null) return;

        Vector3 targetPos;
        Vector3 targetLookAt;
        float targetFov;

        float cycleTime = gameTimer % 31.0f;

        var activeCarrier = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "aircraft_carrier");
        var activeBlueTank = blueUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && (u.UnitKey == "tank" || u.UnitKey == "medium_tank"));
        var activeBlueHeavy = blueUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "heavy_tank");
        var activeArtillery = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "artillery");
        var activeAntiAir = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "anti_air_gun");
        var activeRedTank = redUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && (u.UnitKey == "tank" || u.UnitKey == "heavy_tank"));

        var activeBlueInfantry = blueUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && BattleUnitCatalog.IsInfantryLike(u.UnitKey));
        var activeRedInfantry = redUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && BattleUnitCatalog.IsInfantryLike(u.UnitKey));
        var activeFlamer = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && (u.UnitKey is "infantry_flamethrower" or "flamethrower"));
        var activeRifle = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "infantry");
        var activeMortar = blueUnits.Concat(redUnits).FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && u.UnitKey == "infantry_artillery");

        if (cycleTime < 6.5f)
        {
            // 阶段一：上帝战术俯瞰——全套红警基地军工建筑群展开与军备集结 (Full Red Alert Base Complex & Muster)
            targetPos = new Vector3(-28f, 32.0f, 32.0f);
            targetLookAt = new Vector3(-32f, 1.0f, 12.0f);
            targetFov = 54f;
        }
        else if (cycleTime < 14.0f)
        {
            // 阶段二：上帝战术俯瞰——装甲集群推进与空降防空协同 (Armored Column Advance God's Eye)
            var leadBlue = blueUnits.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && !u.IsDead && (u.UnitKey == "heavy_tank" || u.UnitKey == "tank" || u.UnitKey == "tank_destroyer"));
            var px = leadBlue is not null ? leadBlue.GlobalPosition.X : -8.0f;
            targetPos = new Vector3(px, 27.5f, 20.0f);
            targetLookAt = new Vector3(px + 2.0f, 0.8f, 2.0f);
            targetFov = 48f;
        }
        else if (cycleTime < 22.0f)
        {
            // 阶段三：上帝战术俯瞰——前线海陆空大决火与殉爆 (Central Clash & Domain Destruction God's Eye)
            targetPos = new Vector3(0f, 29.0f, 16.0f);
            targetLookAt = new Vector3(0f, 0.8f, -4.0f);
            targetFov = 49f;
        }
        else
        {
            // 阶段四：上帝战术俯瞰——海陆空三维全景宏大巡航 (Grand Panoramic Theatre of War God's Eye)
            float t4 = (cycleTime - 22.0f) / 9.0f;
            float sweepX = Mathf.Lerp(-8.0f, 8.0f, t4);
            targetPos = new Vector3(sweepX, 32.0f, 18.0f);
            targetLookAt = new Vector3(sweepX * 0.4f, 0.8f, -5.0f);
            targetFov = 50f;
        }

        // 平滑阻尼插值 (Smooth Camera Director Interpolation: 独立解耦位置与旋转插值，杜绝 Transform 混合震颤)
        ArenaCamera.GlobalPosition = ArenaCamera.GlobalPosition.Lerp(targetPos, Mathf.Clamp(dt * 3.5f, 0f, 1f));
        var dirToTarget = targetLookAt - ArenaCamera.GlobalPosition;
        if (dirToTarget.LengthSquared() > 0.01f)
        {
            var targetBasis = Basis.LookingAt(dirToTarget.Normalized(), Vector3.Up);
            ArenaCamera.Basis = ArenaCamera.Basis.Slerp(targetBasis, Mathf.Clamp(dt * 3.8f, 0f, 1f));
        }
        ArenaCamera.Fov = Mathf.Lerp(ArenaCamera.Fov, targetFov, Mathf.Clamp(dt * 3.0f, 0f, 1f));
    }

    void InitBattleHud()
    {
        var hudScene = GD.Load<PackedScene>("res://scenes/ui/ArenaBattleHud.tscn");
        if (hudScene is not null)
        {
            _battleHud = hudScene.Instantiate<GodotRTS.Scripts.UI.ArenaBattleHud>();
            AddChild(_battleHud);

            _battleHud.SpawnRequested += (unitType, isBlue) => SpawnUnitForSide(unitType, isBlue);
            _battleHud.WaveRequested += (isBlue) => SpawnBalancedWave(isBlue);
            _battleHud.ClashRequested += TriggerAllOutClash;
            _battleHud.ClearRequested += ClearBattlefield;
            _battleHud.AutoWaveToggled += () => { autoWaveEnabled = !autoWaveEnabled; };
            _battleHud.SpeedToggled += () => {
                timeScaleIndex = (timeScaleIndex + 1) % 3;
                double scale = timeScaleIndex == 0 ? 1.0 : timeScaleIndex == 1 ? 2.0 : 4.0;
                if (!isPaused) Engine.TimeScale = scale;
            };
            _battleHud.PauseToggled += () => {
                isPaused = !isPaused;
                if (isPaused)
                {
                    Engine.TimeScale = 0.0;
                }
                else
                {
                    double scale = timeScaleIndex == 0 ? 1.0 : timeScaleIndex == 1 ? 2.0 : 4.0;
                    Engine.TimeScale = scale;
                }
            };
            _battleHud.CameraModeRequested += () => {
                var nextMode = currentCamMode switch
                {
                    CameraMode.CinematicDirector => CameraMode.RtsTopDown,
                    CameraMode.RtsTopDown => CameraMode.FrontlineCloseUp,
                    CameraMode.FrontlineCloseUp => CameraMode.BirdEyeOverview,
                    _ => CameraMode.CinematicDirector
                };
                ApplyCameraMode(nextMode);
            };
            _battleHud.ExitRequested += () => {
                Engine.TimeScale = 1.0;
                GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn");
            };
        }
    }

    void UpdateUiStats()
    {
        int blueCount = blueUnits.Count(u => GodotObject.IsInstanceValid(u) && !u.IsDead);
        int redCount = redUnits.Count(u => GodotObject.IsInstanceValid(u) && !u.IsDead);
        int fps = (int)Engine.GetFramesPerSecond();
        double currentScale = isPaused ? 0.0 : (timeScaleIndex == 0 ? 1.0 : timeScaleIndex == 1 ? 2.0 : 4.0);

        _battleHud?.UpdateBattleTelemetry(blueCount, blueKills, redCount, redKills, gameTimer, fps, currentScale, isPaused, autoWaveEnabled);
    }
}
