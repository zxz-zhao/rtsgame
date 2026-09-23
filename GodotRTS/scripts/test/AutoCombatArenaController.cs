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
    Label blueStatusLabel = null!;
    Label redStatusLabel = null!;
    Label centerTimerLabel = null!;
    Button autoWaveToggleBtn = null!;
    Button pauseBtn = null!;
    Button speedBtn = null!;

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

        BuildArenaUi();

        // 开局初始化双方海陆空先遣部队
        // 陆军主力
        SpawnUnitForSide("tank", true, new Vector3(-16f, 0f, 3f));
        SpawnUnitForSide("infantry", true, new Vector3(-13f, 0f, -2.5f));
        SpawnUnitForSide("tank", false, new Vector3(16f, 0f, 3f));
        SpawnUnitForSide("infantry", false, new Vector3(13f, 0f, -2.5f));

        // 空军前哨
        SpawnUnitForSide("fighter", true, new Vector3(-22f, 9.5f, 6f));
        SpawnUnitForSide("fighter", false, new Vector3(22f, 9.5f, 6f));

        // 海军护卫
        SpawnUnitForSide("destroyer_ship", true, new Vector3(-18f, 0f, -28f));
        SpawnUnitForSide("destroyer_ship", false, new Vector3(18f, 0f, -28f));
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
                        ? Mathf.Clamp(blue.GlobalPosition.Z, -32f, -22f)
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
                // 海水区域适航走廊 Z: -32 ~ -22
                jitterZ = rng.RandfRange(-32f, -22f);
            }
            else if (BattleUnitCatalog.IsAirUnit(unitKey))
            {
                // 空中巡航走廊 Z: -15 ~ 15
                jitterZ = rng.RandfRange(-15f, 15f);
            }
            else
            {
                // 陆军交火走廊 Z: -10 ~ 14
                jitterZ = rng.RandfRange(-10f, 14f);
            }

            float spawnY = BattleUnitCatalog.SpawnHeight(unitKey);
            spawnPos = new Vector3(baseX + jitterX, spawnY, jitterZ);
        }

        var targetX = isBlue ? RedBaseX : BlueBaseX;
        float destZ = BattleUnitCatalog.IsNavalUnit(unitKey)
            ? Mathf.Clamp(spawnPos.Z, -32f, -22f)
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

    void UpdateUiStats()
    {
        int blueCount = blueUnits.Count(u => GodotObject.IsInstanceValid(u) && !u.IsDead);
        int redCount = redUnits.Count(u => GodotObject.IsInstanceValid(u) && !u.IsDead);

        blueStatusLabel.Text = $"🔵 蓝军部队: {blueCount} | 击杀: {blueKills}";
        redStatusLabel.Text = $"🔴 红军部队: {redCount} | 击杀: {redKills}";

        int mins = (int)(gameTimer / 60);
        int secs = (int)(gameTimer % 60);
        int fps = (int)Engine.GetFramesPerSecond();
        centerTimerLabel.Text = $"⏱️ {mins:D2}:{secs:D2}  |  FPS: {fps}  |  倍速: {Engine.TimeScale:0.0}x";
    }

    void BuildArenaUi()
    {
        var canvas = new CanvasLayer { Name = "AutoCombatUI" };
        AddChild(canvas);

        // 1. 顶部战况看板
        var topBanner = new Panel
        {
            Position = new Vector2(225, 12),
            Size = new Vector2(830, 46),
            CustomMinimumSize = new Vector2(830, 46)
        };
        MetalUiStyle.ApplyMetalPanel(topBanner, MetalUiStyle.Steel, 1, 6, 3);
        canvas.AddChild(topBanner);

        var topBox = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        topBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        topBox.AddThemeConstantOverride("separation", 24);
        topBox.Alignment = BoxContainer.AlignmentMode.Center;
        topBanner.AddChild(topBox);

        blueStatusLabel = new Label();
        blueStatusLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.75f, 1f));
        blueStatusLabel.AddThemeFontSizeOverride("font_size", 14);
        topBox.AddChild(blueStatusLabel);

        centerTimerLabel = new Label();
        centerTimerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.40f));
        centerTimerLabel.AddThemeFontSizeOverride("font_size", 14);
        topBox.AddChild(centerTimerLabel);

        redStatusLabel = new Label();
        redStatusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.40f));
        redStatusLabel.AddThemeFontSizeOverride("font_size", 14);
        topBox.AddChild(redStatusLabel);

        // 2. 左侧控制栏：蓝军快速部署
        var bluePanel = new Panel
        {
            Position = new Vector2(14, 68),
            Size = new Vector2(138, 560)
        };
        MetalUiStyle.ApplyMetalPanel(bluePanel, MetalUiStyle.Steel, 1, 6, 3);
        canvas.AddChild(bluePanel);

        var blueBox = new VBoxContainer();
        blueBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        blueBox.AddThemeConstantOverride("margin_left", 6);
        blueBox.AddThemeConstantOverride("margin_right", 6);
        blueBox.AddThemeConstantOverride("margin_top", 6);
        blueBox.AddThemeConstantOverride("margin_bottom", 6);
        blueBox.AddThemeConstantOverride("separation", 3);
        bluePanel.AddChild(blueBox);

        var blueTitle = new Label { Text = "🔵 蓝军快速增援", HorizontalAlignment = HorizontalAlignment.Center };
        blueTitle.AddThemeColorOverride("font_color", new Color(0.40f, 0.75f, 1f));
        blueTitle.AddThemeFontSizeOverride("font_size", 12);
        blueBox.AddChild(blueTitle);

        AddCategoryHeader(blueBox, "—— 空中战机 ——", new Color(0.6f, 0.85f, 1f));
        AddDeployButton(blueBox, "✈️ 战斗机", () => SpawnUnitForSide("fighter", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "💣 轰炸机", () => SpawnUnitForSide("bomber", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "🚁 侦察机", () => SpawnUnitForSide("scout_plane", true), MetalUiStyle.Steel);

        AddCategoryHeader(blueBox, "—— 海军舰队 ——", new Color(0.4f, 0.8f, 0.9f));
        AddDeployButton(blueBox, "⚓ 驱逐舰", () => SpawnUnitForSide("destroyer_ship", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "⚓ 战列舰", () => SpawnUnitForSide("battleship", true), MetalUiStyle.Gold);
        AddDeployButton(blueBox, "⚓ 攻击潜艇", () => SpawnUnitForSide("submarine", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "🚢 航空母舰", () => SpawnUnitForSide("aircraft_carrier", true), MetalUiStyle.Gold);

        AddCategoryHeader(blueBox, "—— 陆军装甲 ——", new Color(0.85f, 0.85f, 0.7f));
        AddDeployButton(blueBox, "🛡️ 中型坦克", () => SpawnUnitForSide("tank", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "🛡️ 重型坦克", () => SpawnUnitForSide("heavy_tank", true), MetalUiStyle.Gold);
        AddDeployButton(blueBox, "🎯 自行火炮", () => SpawnUnitForSide("artillery", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "⚡ 防空炮车", () => SpawnUnitForSide("anti_air_gun", true), MetalUiStyle.Steel);
        AddDeployButton(blueBox, "🎖️ 步兵 / 喷火", () => SpawnUnitForSide(rng.RandiRange(0, 1) == 0 ? "infantry" : "infantry_flamethrower", true), MetalUiStyle.Steel);

        AddCategoryHeader(blueBox, "—— 特遣编制 ——", new Color(0.4f, 0.95f, 0.65f));
        AddDeployButton(blueBox, "🌊 蓝军三军混编", () => SpawnBalancedWave(true), MetalUiStyle.Green);

        // 3. 右侧控制栏：红军快速部署
        var redPanel = new Panel
        {
            Position = new Vector2(1280 - 138 - 14, 68),
            Size = new Vector2(138, 560)
        };
        MetalUiStyle.ApplyMetalPanel(redPanel, MetalUiStyle.Steel, 1, 6, 3);
        canvas.AddChild(redPanel);

        var redBox = new VBoxContainer();
        redBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        redBox.AddThemeConstantOverride("margin_left", 6);
        redBox.AddThemeConstantOverride("margin_right", 6);
        redBox.AddThemeConstantOverride("margin_top", 6);
        redBox.AddThemeConstantOverride("margin_bottom", 6);
        redBox.AddThemeConstantOverride("separation", 3);
        redPanel.AddChild(redBox);

        var redTitle = new Label { Text = "🔴 红军快速增援", HorizontalAlignment = HorizontalAlignment.Center };
        redTitle.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.40f));
        redTitle.AddThemeFontSizeOverride("font_size", 12);
        redBox.AddChild(redTitle);

        AddCategoryHeader(redBox, "—— 空中战机 ——", new Color(1f, 0.7f, 0.6f));
        AddDeployButton(redBox, "✈️ 战斗机", () => SpawnUnitForSide("fighter", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "💣 轰炸机", () => SpawnUnitForSide("bomber", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "🚁 侦察机", () => SpawnUnitForSide("scout_plane", false), MetalUiStyle.Steel);

        AddCategoryHeader(redBox, "—— 海军舰队 ——", new Color(0.9f, 0.65f, 0.5f));
        AddDeployButton(redBox, "⚓ 驱逐舰", () => SpawnUnitForSide("destroyer_ship", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "⚓ 战列舰", () => SpawnUnitForSide("battleship", false), MetalUiStyle.Gold);
        AddDeployButton(redBox, "⚓ 攻击潜艇", () => SpawnUnitForSide("submarine", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "🚢 航空母舰", () => SpawnUnitForSide("aircraft_carrier", false), MetalUiStyle.Gold);

        AddCategoryHeader(redBox, "—— 陆军装甲 ——", new Color(0.85f, 0.85f, 0.7f));
        AddDeployButton(redBox, "🛡️ 中型坦克", () => SpawnUnitForSide("tank", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "🛡️ 重型坦克", () => SpawnUnitForSide("heavy_tank", false), MetalUiStyle.Gold);
        AddDeployButton(redBox, "🎯 自行火炮", () => SpawnUnitForSide("artillery", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "⚡ 防空炮车", () => SpawnUnitForSide("anti_air_gun", false), MetalUiStyle.Steel);
        AddDeployButton(redBox, "🎖️ 步兵 / 喷火", () => SpawnUnitForSide(rng.RandiRange(0, 1) == 0 ? "infantry" : "infantry_flamethrower", false), MetalUiStyle.Steel);

        AddCategoryHeader(redBox, "—— 特遣编制 ——", new Color(1f, 0.55f, 0.5f));
        AddDeployButton(redBox, "🌊 红军三军混编", () => SpawnBalancedWave(false), MetalUiStyle.Red);

        // 4. 底部功能控制条
        var bottomPanel = new Panel
        {
            Position = new Vector2(205, 656),
            Size = new Vector2(870, 48),
            CustomMinimumSize = new Vector2(870, 48)
        };
        MetalUiStyle.ApplyMetalPanel(bottomPanel, MetalUiStyle.Steel, 1, 6, 3);
        canvas.AddChild(bottomPanel);

        var bottomBox = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        bottomBox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bottomBox.AddThemeConstantOverride("separation", 10);
        bottomBox.Alignment = BoxContainer.AlignmentMode.Center;
        bottomPanel.AddChild(bottomBox);

        // ⚔️ 海陆空大会战
        var clashBtn = new Button { Text = "⚔️ 海陆空大会战", CustomMinimumSize = new Vector2(124, 32) };
        MetalUiStyle.ApplyMetalButton(clashBtn, MetalUiStyle.Gold, 12, true);
        clashBtn.Pressed += TriggerAllOutClash;
        bottomBox.AddChild(clashBtn);

        // 🔄 自动刷兵开关
        autoWaveToggleBtn = new Button { Text = "🔄 自动出兵: 开", CustomMinimumSize = new Vector2(110, 32) };
        MetalUiStyle.ApplyMetalButton(autoWaveToggleBtn, MetalUiStyle.Green, 12);
        autoWaveToggleBtn.Pressed += () => {
            autoWaveEnabled = !autoWaveEnabled;
            autoWaveToggleBtn.Text = autoWaveEnabled ? "🔄 自动出兵: 开" : "🔄 自动出兵: 关";
            MetalUiStyle.ApplyMetalButton(autoWaveToggleBtn, autoWaveEnabled ? MetalUiStyle.Green : MetalUiStyle.Steel, 12);
        };
        bottomBox.AddChild(autoWaveToggleBtn);

        // ⏩ 倍速调节
        speedBtn = new Button { Text = "⏩ 1.0x", CustomMinimumSize = new Vector2(80, 32) };
        MetalUiStyle.ApplyMetalButton(speedBtn, MetalUiStyle.Steel, 12);
        speedBtn.Pressed += () => {
            timeScaleIndex = (timeScaleIndex + 1) % 3;
            double scale = timeScaleIndex == 0 ? 1.0 : timeScaleIndex == 1 ? 2.0 : 4.0;
            if (!isPaused) Engine.TimeScale = scale;
            speedBtn.Text = $"⏩ {scale:0.0}x";
        };
        bottomBox.AddChild(speedBtn);

        // ⏸️ 暂停/恢复
        pauseBtn = new Button { Text = "⏸️ 暂停", CustomMinimumSize = new Vector2(80, 32) };
        MetalUiStyle.ApplyMetalButton(pauseBtn, MetalUiStyle.Steel, 12);
        pauseBtn.Pressed += () => {
            isPaused = !isPaused;
            if (isPaused)
            {
                Engine.TimeScale = 0.0;
                pauseBtn.Text = "▶️ 继续";
            }
            else
            {
                double scale = timeScaleIndex == 0 ? 1.0 : timeScaleIndex == 1 ? 2.0 : 4.0;
                Engine.TimeScale = scale;
                pauseBtn.Text = "⏸️ 暂停";
            }
        };
        bottomBox.AddChild(pauseBtn);

        // 🧹 清理战场
        var clearBtn = new Button { Text = "🧹 清空战场", CustomMinimumSize = new Vector2(90, 32) };
        MetalUiStyle.ApplyMetalButton(clearBtn, MetalUiStyle.Steel, 12);
        clearBtn.Pressed += ClearBattlefield;
        bottomBox.AddChild(clearBtn);

        // 🎥 镜头视角切换
        var camBtn = new Button { Text = "🎥 切换镜头", CustomMinimumSize = new Vector2(90, 32) };
        MetalUiStyle.ApplyMetalButton(camBtn, MetalUiStyle.Steel, 12);
        camBtn.Pressed += () => {
            var nextMode = currentCamMode switch
            {
                CameraMode.RtsTopDown => CameraMode.FrontlineCloseUp,
                CameraMode.FrontlineCloseUp => CameraMode.BirdEyeOverview,
                _ => CameraMode.RtsTopDown
            };
            ApplyCameraMode(nextMode);
        };
        bottomBox.AddChild(camBtn);

        // 🚪 返回大厅
        var exitBtn = new Button { Text = "🚪 返回大厅", CustomMinimumSize = new Vector2(90, 32) };
        MetalUiStyle.ApplyMetalButton(exitBtn, MetalUiStyle.Red, 12);
        exitBtn.Pressed += () => {
            Engine.TimeScale = 1.0;
            GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn");
        };
        bottomBox.AddChild(exitBtn);
    }

    static void AddCategoryHeader(VBoxContainer parent, string title, Color color)
    {
        var lbl = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeFontSizeOverride("font_size", 9);
        parent.AddChild(lbl);
    }

    static void AddDeployButton(VBoxContainer parent, string text, System.Action onClick, MetalUiStyle.MetalPalette? palette = null)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(120, 24)
        };
        MetalUiStyle.ApplyMetalButton(btn, palette ?? MetalUiStyle.Steel, 10);
        btn.Pressed += onClick;
        parent.AddChild(btn);
    }
}
