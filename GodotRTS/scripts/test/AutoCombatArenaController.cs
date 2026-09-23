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
    int timeScaleIndex = 0; // 0: 1x, 1: 2x, 2: 4x
    bool isPaused = false;

    enum CameraMode
    {
        RtsTopDown,
        FrontlineCloseUp,
        BirdEyeOverview
    }
    CameraMode currentCamMode = CameraMode.RtsTopDown;

    readonly List<RtsUnit> blueUnits = new();
    readonly List<RtsUnit> redUnits = new();
    readonly RandomNumberGenerator rng = new();

    // UI Controls
    GodotRTS.Scripts.UI.ArenaBattleHud? _battleHud;

    // Camera Drag
    bool isMiddleDragging = false;
    Vector2 lastMousePos;

    public override void _EnterTree()
    {
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = true;
        BattleGameManager.DisableFuelDepletion = true;
    }

    public override void _Ready()
    {
        BattleGameManager.DisableFogOfWar = true;
        BattleGameManager.DisableWaterCheck = true;
        BattleGameManager.DisableFuelDepletion = true;
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

        // 默认镜头配置
        if (ArenaCamera is not null)
        {
            ApplyCameraMode(CameraMode.RtsTopDown);
        }

        InitBattleHud();

        // 开局初始化双方海陆空先遣部队
        // 陆军主力：装备精良的现代重装主战坦克
        SpawnUnitForSide("heavy_tank", true, new Vector3(-16f, 0f, 3f));
        SpawnUnitForSide("infantry", true, new Vector3(-13f, 0f, -2.5f));
        SpawnUnitForSide("heavy_tank", false, new Vector3(16f, 0f, 3f));
        SpawnUnitForSide("infantry", false, new Vector3(13f, 0f, -2.5f));

        // 空军前哨
        SpawnUnitForSide("fighter", true, new Vector3(-22f, 9.5f, 6f));
        SpawnUnitForSide("fighter", false, new Vector3(22f, 9.5f, 6f));

        // 海军护卫战舰（航行于镜头可见的湛蓝河流水道 Z: -10）
        SpawnUnitForSide("destroyer_ship", true, new Vector3(-18f, 0.5f, -10f));
        SpawnUnitForSide("destroyer_ship", false, new Vector3(18f, 0.5f, -10f));
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1.0;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        gameTimer += dt;
        waveTimer += dt;
        retargetTimer += dt;

        if (autoWaveEnabled && waveTimer >= WaveInterval)
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

            if (camMove.LengthSquared() > 0.001f)
            {
                ArenaCamera.GlobalPosition += camMove.Normalized() * (35f * dt);
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
                ArenaCamera.GlobalPosition += ArenaCamera.GlobalTransform.Basis.Z * -1.8f;
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                ArenaCamera.GlobalPosition += ArenaCamera.GlobalTransform.Basis.Z * 1.8f;
            }
            else if (mb.ButtonIndex == MouseButton.Middle)
            {
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

    void UpdateArmyOrders()
    {
        // 蓝军推进行动
        foreach (var blue in blueUnits)
        {
            if (!GodotObject.IsInstanceValid(blue) || blue.IsDead) continue;
            if (blue.AttackTarget is null || !GodotObject.IsInstanceValid(blue.AttackTarget) || ((RtsUnit)blue.AttackTarget).IsDead)
            {
                var nearestRed = FindNearestLiveUnit(blue.GlobalPosition, redUnits, blue.UnitKey);
                if (nearestRed is not null)
                {
                    blue.Attack(nearestRed);
                }
                else
                {
                    float targetZ = BattleUnitCatalog.IsNavalUnit(blue.UnitKey)
                        ? Mathf.Clamp(blue.GlobalPosition.Z, -12f, -8f)
                        : blue.GlobalPosition.Z * 0.4f;
                    blue.AttackMoveTo(new Vector3(RedBaseX, blue.CruiseHeight, targetZ));
                }
            }
        }

        // 红军推进行动
        foreach (var red in redUnits)
        {
            if (!GodotObject.IsInstanceValid(red) || red.IsDead) continue;
            if (red.AttackTarget is null || !GodotObject.IsInstanceValid(red.AttackTarget) || ((RtsUnit)red.AttackTarget).IsDead)
            {
                var nearestBlue = FindNearestLiveUnit(red.GlobalPosition, blueUnits, red.UnitKey);
                if (nearestBlue is not null)
                {
                    red.Attack(nearestBlue);
                }
                else
                {
                    float targetZ = BattleUnitCatalog.IsNavalUnit(red.UnitKey)
                        ? Mathf.Clamp(red.GlobalPosition.Z, -32f, -22f)
                        : red.GlobalPosition.Z * 0.4f;
                    red.AttackMoveTo(new Vector3(BlueBaseX, red.CruiseHeight, targetZ));
                }
            }
        }
    }

    RtsUnit? FindNearestLiveUnit(Vector3 fromPos, List<RtsUnit> targets, string attackerKey)
    {
        RtsUnit? best = null;
        float minDistSq = float.MaxValue;

        // 优先筛选能够有效攻击的目标（避免陆军火炮/坦克瞄准无法攻击的战机导致卡死）
        foreach (var t in targets)
        {
            if (!GodotObject.IsInstanceValid(t) || t.IsDead) continue;
            if (!BattleUnitCatalog.CanAttackTargetType(attackerKey, t.UnitKey)) continue;

            float distSq = fromPos.DistanceSquaredTo(t.GlobalPosition);
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                best = t;
            }
        }

        // 若无克制或能攻击的目标，回退至最近单位
        if (best is null)
        {
            foreach (var t in targets)
            {
                if (!GodotObject.IsInstanceValid(t) || t.IsDead) continue;
                float distSq = fromPos.DistanceSquaredTo(t.GlobalPosition);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    best = t;
                }
            }
        }

        return best;
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
                // 海水区域适航走廊 Z: -12 ~ -8 (镜头主视野清晰可见河道)
                jitterZ = rng.RandfRange(-12f, -8f);
            }
            else if (BattleUnitCatalog.IsAirUnit(unitKey))
            {
                // 空中巡航走廊 Z: -15 ~ 15
                jitterZ = rng.RandfRange(-15f, 15f);
            }
            else
            {
                // 陆军交火走廊 Z: -5 ~ 10
                jitterZ = rng.RandfRange(-5f, 10f);
            }

            float spawnY = BattleUnitCatalog.SpawnHeight(unitKey);
            spawnPos = new Vector3(baseX + jitterX, spawnY, jitterZ);
        }

        var targetX = isBlue ? RedBaseX : BlueBaseX;
        float destZ = BattleUnitCatalog.IsNavalUnit(unitKey)
            ? Mathf.Clamp(spawnPos.Z, -12f, -8f)
            : spawnPos.Z * 0.5f;
        var marchDest = new Vector3(targetX, spawnPos.Y, destZ);

        var unit = mgr.SpawnUnit(unitKey, spawnPos, isBlue, marchDest);
        if (unit is not null)
        {
            unit.Rotation = new Vector3(0f, isBlue ? 0f : Mathf.Pi, 0f);
            unit.Died += OnUnitDied;
            if (isBlue) blueUnits.Add(unit);
            else redUnits.Add(unit);

            // 立即朝敌方前线挺进
            unit.AttackMoveTo(marchDest);
        }
    }

    void OnUnitDied(RtsUnit unit)
    {
        if (unit.PlayerOwned)
        {
            redKills++;
        }
        else
        {
            blueKills++;
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
        var infantryVariant = rng.RandiRange(0, 1) == 0 ? "infantry_flamethrower" : "infantry";

        SpawnUnitForSide(landArmor, isBlue);
        SpawnUnitForSide(landSupport, isBlue);
        SpawnUnitForSide(infantryVariant, isBlue);
        SpawnUnitForSide("infantry", isBlue);

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
    }

    public void TriggerAllOutClash()
    {
        // ⚔️ 陆海空全军三维立体大兵团对抗
        // 陆军机械化集群
        string[] groundFleet = { "heavy_tank", "tank", "tank", "light_tank", "artillery", "anti_air_gun", "infantry", "infantry_flamethrower" };
        for (int i = 0; i < groundFleet.Length; i++)
        {
            float z = -8f + i * 2.8f;
            SpawnUnitForSide(groundFleet[i], true, new Vector3(BlueBaseX + rng.RandfRange(-2f, 2f), 0f, z));
            SpawnUnitForSide(groundFleet[i], false, new Vector3(RedBaseX + rng.RandfRange(-2f, 2f), 0f, z));
        }

        // 空军战机巡航集群
        string[] airFleet = { "fighter", "fighter", "bomber", "scout_plane" };
        for (int i = 0; i < airFleet.Length; i++)
        {
            float z = -12f + i * 7.5f;
            SpawnUnitForSide(airFleet[i], true, new Vector3(BlueBaseX - 3f, 9.5f, z));
            SpawnUnitForSide(airFleet[i], false, new Vector3(RedBaseX + 3f, 9.5f, z));
        }

        // 海军水面水下舰队群
        string[] seaFleet = { "destroyer_ship", "battleship", "submarine", "aircraft_carrier" };
        for (int i = 0; i < seaFleet.Length; i++)
        {
            float z = -32f + i * 3.2f;
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
            case CameraMode.RtsTopDown:
                ArenaCamera.GlobalPosition = new Vector3(0f, 28f, 22f);
                ArenaCamera.LookAt(new Vector3(0f, 0f, 0f), Vector3.Up);
                ArenaCamera.Fov = 48f;
                break;
            case CameraMode.FrontlineCloseUp:
                ArenaCamera.GlobalPosition = new Vector3(-6f, 6.5f, 16f);
                ArenaCamera.LookAt(new Vector3(2f, 1.2f, 0f), Vector3.Up);
                ArenaCamera.Fov = 52f;
                break;
            case CameraMode.BirdEyeOverview:
                ArenaCamera.GlobalPosition = new Vector3(0f, 48f, 16f);
                ArenaCamera.LookAt(new Vector3(0f, 0f, 0f), Vector3.Up);
                ArenaCamera.Fov = 55f;
                break;
        }
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
                    CameraMode.RtsTopDown => CameraMode.FrontlineCloseUp,
                    CameraMode.FrontlineCloseUp => CameraMode.BirdEyeOverview,
                    _ => CameraMode.RtsTopDown
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
