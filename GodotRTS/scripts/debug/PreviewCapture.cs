using Godot;
using System.Collections.Generic;

public partial class PreviewCapture : Node
{
    int framesLeft = -1;
    string outputPath = "user://preview.png";
    bool selectPlayerBase;
    bool selectEnemyBase;
    string queueUnit = "";
    string damageTarget = "";
    float damageAmount;
    int damageAfterFrames = 3;
    bool repairSelectedBuilding;
    bool repairSelectedUnit;
    bool upgradeSelectedBuilding;
    float damageSpawnedPlayer;
    readonly List<(string key, Vector3 position)> buildOrders = new();
    readonly List<(string key, Vector3 position, bool playerOwned)> spawnOrders = new();
    readonly List<(string key, Vector3 position)> techOrders = new();
    readonly List<string> researchOrders = new();
    bool focusSpawnedUnits;
    bool disableAi;
    bool hideHud;
    string focusUnitKey = "";
    bool selectLastBuilt;
    bool finishBuilt;
    bool selectSpawned;
    int cancelBuiltAfterFrames = -1;
    bool showGameOver;
    bool gameOverPlayerWon = false;
    string gameOverReason = "我方主基地被摧毁";
    Vector3? rallyPoint;
    Vector3? attackMoveTarget;
    Vector3? attackGroundTarget;
    Vector3? moveTarget;
    Vector3? patrolTarget;
    bool guardFirstSpawned;
    Vector3? focusPoint;
    float focusZoom = 1.0f;
    string selectedMap = "";
    bool openBuildMenu;
    bool parkSpawned;

    public override void _Ready()
    {
        var args = CommandLineArgs.Get();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--capture-preview")
                framesLeft = 20;
            else if (args[i] == "--capture-frames" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out framesLeft);
            else if (args[i] == "--capture-path" && i + 1 < args.Length)
                outputPath = args[i + 1];
            else if (args[i] == "--select-player-base")
                selectPlayerBase = true;
            else if (args[i] == "--select-enemy-base")
                selectEnemyBase = true;
            else if (args[i] == "--queue-unit" && i + 1 < args.Length)
                queueUnit = args[i + 1];
            else if (args[i] == "--research" && i + 1 < args.Length)
                researchOrders.Add(args[i + 1]);
            else if (args[i] == "--damage-target" && i + 1 < args.Length)
                damageTarget = args[i + 1];
            else if (args[i] == "--damage-amount" && i + 1 < args.Length)
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out damageAmount);
            else if (args[i] == "--damage-after-frames" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out damageAfterFrames);
            else if (args[i] == "--repair-selected-building")
                repairSelectedBuilding = true;
            else if (args[i] == "--repair-selected-unit")
                repairSelectedUnit = true;
            else if (args[i] == "--upgrade-selected-building")
                upgradeSelectedBuilding = true;
            else if (args[i] == "--damage-spawned-player" && i + 1 < args.Length)
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out damageSpawnedPlayer);
            else if (args[i] == "--build" && i + 1 < args.Length)
            {
                var key = args[i + 1];
                var pos = i + 3 < args.Length && float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                    && float.TryParse(args[i + 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)
                    ? new Vector3(x, 0f, z)
                    : new Vector3(-96f, 0f, -92f);
                buildOrders.Add((key, pos));
            }
            else if (args[i] == "--build-pos" && i + 3 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                if (buildOrders.Count > 0)
                    buildOrders[^1] = (buildOrders[^1].key, new Vector3(x, 0f, z));
            }
            else if (args[i] == "--spawn" && i + 3 < args.Length)
            {
                var key = args[i + 1];
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                spawnOrders.Add((key, new Vector3(x, 0f, z), true));
            }
            else if (args[i] == "--spawn-enemy" && i + 3 < args.Length)
            {
                var key = args[i + 1];
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                spawnOrders.Add((key, new Vector3(x, 0f, z), false));
            }
            else if ((args[i] == "--cast-tech" || args[i] == "--tech") && i + 3 < args.Length)
            {
                var key = args[i + 1];
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                techOrders.Add((key, new Vector3(x, 0f, z)));
            }
            else if (args[i] == "--focus-spawned")
                focusSpawnedUnits = true;
            else if (args[i] == "--disable-ai")
                disableAi = true;
            else if (args[i] == "--focus-unit" && i + 1 < args.Length)
                focusUnitKey = args[i + 1];
            else if (args[i] == "--hide-hud")
                hideHud = true;
            else if (args[i] == "--select-built")
                selectLastBuilt = true;
            else if (args[i] == "--finish-built")
                finishBuilt = true;
            else if (args[i] == "--select-spawned")
                selectSpawned = true;
            else if (args[i] == "--cancel-built-after" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out cancelBuiltAfterFrames);
            else if (args[i] == "--show-gameover")
                showGameOver = true;
            else if (args[i] == "--gameover-win")
                gameOverPlayerWon = true;
            else if (args[i] == "--gameover-reason" && i + 1 < args.Length)
                gameOverReason = args[i + 1];
            else if (args[i] == "--rally" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                rallyPoint = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--attack-move" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                attackMoveTarget = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--attack-ground" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                attackGroundTarget = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--move-to" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                moveTarget = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--patrol" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                patrolTarget = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--guard-first-spawned")
                guardFirstSpawned = true;
            else if (args[i] == "--focus-point" && i + 2 < args.Length)
            {
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
                float.TryParse(args[i + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
                focusPoint = new Vector3(x, 0f, z);
            }
            else if (args[i] == "--focus-zoom" && i + 1 < args.Length)
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out focusZoom);
            else if (args[i] == "--map" && i + 1 < args.Length)
                selectedMap = args[i + 1];
            else if (args[i] == "--open-build-menu")
                openBuildMenu = true;
            else if (args[i] == "--park-spawned")
                parkSpawned = true;
        }

        if (!string.IsNullOrWhiteSpace(selectedMap))
            GameState.Instance?.SelectMap(selectedMap, "Debug Capture");

        if (disableAi)
            CallDeferred(MethodName.DisableAiDeferred);

        if (showGameOver || selectPlayerBase || selectEnemyBase || !string.IsNullOrWhiteSpace(queueUnit) || !string.IsNullOrWhiteSpace(damageTarget) || buildOrders.Count > 0 || spawnOrders.Count > 0 || researchOrders.Count > 0 || techOrders.Count > 0 || repairSelectedUnit || upgradeSelectedBuilding || damageSpawnedPlayer > 0f || openBuildMenu || parkSpawned)
            _ = ApplyDebugActions();
        if (hideHud)
            CallDeferred(MethodName.HideHudDeferred);
    }

    void HideHudDeferred()
    {
        if (GetTree().CurrentScene?.GetNodeOrNull<CanvasLayer>("HUD") is { } hud)
            hud.Visible = false;
        foreach (var node in GetTree().GetNodesInGroup("debug_hide_for_capture"))
        {
            if (node is CanvasItem item)
                item.Visible = false;
        }
    }

    void DisableAiDeferred()
    {
        if (GetTree().CurrentScene?.GetNodeOrNull<Node>("Services/BattleAiController") is { } ai)
        {
            ai.SetProcess(false);
            ai.SetPhysicsProcess(false);
        }
    }

    async System.Threading.Tasks.Task ApplyDebugActions()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        if (openBuildMenu)
        {
            var hud = GetTree().CurrentScene?.GetNodeOrNull<BattleHud>("HUD");
            hud?.DebugOpenBuildMenu();
        }

        var building = manager.FindMainBase(true);
        if (building is null)
            return;

        if (selectPlayerBase)
            GameState.Instance?.SetSelection(new[] { building });

        if (selectEnemyBase && manager.FindMainBase(false) is { } enemyBase)
            GameState.Instance?.SetSelection(new[] { enemyBase });

        if (rallyPoint is { } rally)
        {
            building.SetRallyPoint(rally);
            GameState.Instance?.SetSelection(new[] { building });
        }

        RtsBuilding? lastBuilt = null;
        foreach (var (key, position) in buildOrders)
        {
            if (manager.TryConstructBuilding(key, position, true, out _, out var constructed))
            {
                lastBuilt = constructed;
                if (finishBuilt && lastBuilt is not null)
                    lastBuilt.CompleteConstruction();
            }
        }

        if (!string.IsNullOrWhiteSpace(queueUnit))
            manager.TryQueueProduction(building, queueUnit, out _);

        foreach (var techKey in researchOrders)
            manager.TryResearchTech(techKey, out _);

        if (selectLastBuilt && lastBuilt is not null)
            GameState.Instance?.SetSelection(new[] { lastBuilt });

        if (upgradeSelectedBuilding && GameState.Instance is not null)
        {
            var selectedBuilding = GameState.Instance.Selected.Find(node => node is RtsBuilding) as RtsBuilding ?? lastBuilt ?? building;
            manager.TryUpgradeBuilding(selectedBuilding, out _);
            GameState.Instance.SetSelection(new[] { selectedBuilding });
        }

        if (cancelBuiltAfterFrames >= 0 && lastBuilt is not null)
        {
            for (var i = 0; i < cancelBuiltAfterFrames; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            manager.TryCancelConstruction(lastBuilt, out _);
        }

        var spawned = new System.Collections.Generic.List<RtsUnit>();
        foreach (var (key, position, playerOwned) in spawnOrders)
        {
            var unit = manager.SpawnUnit(key, position, playerOwned);
            if (unit is not null)
                spawned.Add(unit);
        }

        foreach (var (key, position) in techOrders)
        {
            manager.TryCastBattleTech(key, position, out var techMessage, out var affected);
            GD.Print($"Tech {key}: {techMessage} (affected {affected})");
        }

        if (!string.IsNullOrWhiteSpace(focusUnitKey))
        {
            var target = spawned.Find(u => u.Name.ToString().Contains(focusUnitKey, System.StringComparison.OrdinalIgnoreCase));
            if (target is not null)
                FocusCamera(new[] { target }, 1.0f);
        }
        else if (focusSpawnedUnits && spawned.Count > 0)
        {
            FocusCamera(spawned, 1.0f);
        }
        else if (focusPoint is { } point)
        {
            FocusCamera(point, focusZoom);
        }

        if (selectSpawned && spawned.Count > 0)
            GameState.Instance?.SetSelection(spawned);

        if (damageSpawnedPlayer > 0f)
        {
            foreach (var unit in spawned)
            {
                if (unit.PlayerOwned)
                    unit.ApplyDamage(damageSpawnedPlayer);
            }
            var damagedPlayerUnits = spawned.FindAll(u => u.PlayerOwned && GodotObject.IsInstanceValid(u) && !u.IsDead);
            if (damagedPlayerUnits.Count > 0)
                GameState.Instance?.SetSelection(damagedPlayerUnits);
        }

        if (attackMoveTarget is { } attackTarget && spawned.Count > 0)
        {
            foreach (var unit in spawned)
            {
                if (unit.PlayerOwned)
                    unit.AttackMoveTo(attackTarget);
            }
            GameState.Instance?.SetSelection(spawned.FindAll(u => u.PlayerOwned));
        }

        if (attackGroundTarget is { } groundTarget && spawned.Count > 0)
        {
            var attackGroundUnits = spawned.FindAll(u => u.PlayerOwned && u.CanAttackGroundPoint);
            foreach (var unit in attackGroundUnits)
                unit.AttackGround(groundTarget);
            if (attackGroundUnits.Count > 0)
                GameState.Instance?.SetSelection(attackGroundUnits);
        }

        if (moveTarget is { } destination && spawned.Count > 0)
        {
            foreach (var unit in spawned)
            {
                if (unit.PlayerOwned)
                    unit.MoveTo(destination);
            }
            GameState.Instance?.SetSelection(spawned.FindAll(u => u.PlayerOwned));
        }

        if (parkSpawned && spawned.Count > 0)
        {
            foreach (var unit in spawned)
            {
                if (unit.PlayerOwned)
                    manager.TrySendUnitToAirfieldParking(unit, out _);
            }
            GameState.Instance?.SetSelection(spawned.FindAll(u => u.PlayerOwned));
        }

        if (patrolTarget is { } patrol && spawned.Count > 0)
        {
            foreach (var unit in spawned)
            {
                if (unit.PlayerOwned)
                    unit.PatrolTo(patrol);
            }
            GameState.Instance?.SetSelection(spawned.FindAll(u => u.PlayerOwned));
        }

        if (guardFirstSpawned && spawned.Count > 1)
        {
            var leader = spawned.Find(u => u.PlayerOwned);
            if (leader is not null)
            {
                foreach (var unit in spawned)
                {
                    if (unit.PlayerOwned && unit != leader)
                        unit.Guard(leader);
                }
                GameState.Instance?.SetSelection(spawned.FindAll(u => u.PlayerOwned));
            }
        }

        if (hideHud)
            HideHudDeferred();

        if (showGameOver)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var hud = GetTree().CurrentScene?.GetNodeOrNull<BattleHud>("HUD");
            hud?.DebugShowGameOver(gameOverPlayerWon, gameOverReason);
        }

        if (!string.IsNullOrWhiteSpace(damageTarget) && damageAmount > 0f)
        {
            for (var i = 0; i < damageAfterFrames; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var target = damageTarget == "enemy_base"
                ? manager.FindMainBase(false)
                : manager.FindMainBase(true);
            target?.ApplyDamage(damageAmount);
            if (target is RtsBuilding damagedBuilding)
                GameState.Instance?.SetSelection(new[] { damagedBuilding });
        }

        if (repairSelectedBuilding && GameState.Instance is not null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var selectedBuilding = GameState.Instance.Selected.Find(node => node is RtsBuilding) as RtsBuilding;
            if (selectedBuilding is not null)
                manager.TryRepairBuilding(selectedBuilding, out _);
        }

        if (repairSelectedUnit && GameState.Instance is not null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var selectedUnit = GameState.Instance.Selected.Find(node => node is RtsUnit unit && unit.PlayerOwned && !unit.IsDead && unit.Health < unit.MaxHealth - 1f) as RtsUnit;
            if (selectedUnit is not null)
                manager.TryRepairUnit(selectedUnit, out _);
        }
    }

    void FocusCamera(System.Collections.Generic.IReadOnlyList<RtsUnit> units, float zoom)
    {
        var camera = GetTree().CurrentScene?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
        if (camera is null)
            return;

        var center = Vector3.Zero;
        foreach (var unit in units)
            center += unit.GlobalPosition;
        center /= units.Count;
        center.Y = Mathf.Max(1f, center.Y);
        camera.GlobalPosition = center + new Vector3(0f, 14f, -19f) * zoom;
        camera.LookAt(center, Vector3.Up);
        camera.Fov = 30f;
    }

    void FocusCamera(Vector3 center, float zoom)
    {
        var camera = GetTree().CurrentScene?.GetNodeOrNull<Camera3D>("CameraRig/Camera3D");
        if (camera is null)
            return;

        center.Y = Mathf.Max(1f, center.Y);
        camera.GlobalPosition = center + new Vector3(0f, 14f, -19f) * Mathf.Max(0.25f, zoom);
        camera.LookAt(center, Vector3.Up);
        camera.Fov = 30f;
    }

    public override void _Process(double delta)
    {
        if (framesLeft < 0)
            return;

        framesLeft--;
        if (framesLeft > 0)
            return;

        var texture = GetViewport().GetTexture();
        var image = texture?.GetImage();
        if (image is null || image.IsEmpty())
        {
            GD.PushError($"Failed to capture preview screenshot: viewport texture is empty ({outputPath})");
            GetTree().Quit();
            return;
        }

        var err = image.SavePng(outputPath);
        if (err != Error.Ok)
            GD.PushError($"Failed to save preview screenshot: {outputPath} ({err})");
        GetTree().Quit();
    }
}
