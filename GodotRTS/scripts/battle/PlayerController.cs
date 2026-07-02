using Godot;
using System.Collections.Generic;
using System.Linq;


public partial class PlayerController : Node
{
    public enum CommandMode
    {
        None,
        Patrol,
        AttackGround,
        BombingRunStart,
        BombingRunDirection
    }

    [Export] public NodePath CameraPath { get; set; } = "";
    [Export] public float SelectionRadius { get; set; } = 0.75f;
    [Export] public float DragThreshold { get; set; } = 8f;

    Camera3D camera = null!;
    Vector2 dragStart;
    Vector2 dragEnd;
    bool dragging;
    bool touchTracking;
    int activeTouchIndex = -1;
    ulong suppressMouseUntilMs;
    CommandMode activeCommandMode;
    Vector3 bombingRunStartPoint;

    public bool IsDraggingSelection => dragging && dragStart.DistanceTo(dragEnd) >= DragThreshold;
    public Vector2 DragStart => dragStart;
    public Vector2 DragEnd => dragEnd;
    public bool IsPatrolModeActive => activeCommandMode == CommandMode.Patrol;
    public bool IsAttackGroundModeActive => activeCommandMode == CommandMode.AttackGround;
    public bool IsBombingRunModeActive => activeCommandMode is CommandMode.BombingRunStart or CommandMode.BombingRunDirection;
    public bool IsBombingRunAwaitingDirection => activeCommandMode == CommandMode.BombingRunDirection;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>(CameraPath);
    }

    public override void _UnhandledInput(InputEvent evt)
    {
        if (evt is InputEventScreenTouch touch)
        {
            HandleTouch(touch);
            return;
        }

        if (evt is InputEventScreenDrag drag)
        {
            if (touchTracking && drag.Index == activeTouchIndex)
                dragEnd = drag.Position;
            return;
        }

        if (Time.GetTicksMsec() < suppressMouseUntilMs)
            return;

        var hud = FindHud();
        if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left } left)
        {
            if (left.Pressed)
            {
                dragStart = left.Position;
                dragEnd = left.Position;
                dragging = true;
            }
            else
            {
                dragging = false;
                HandlePrimaryRelease(left.Position, false);
            }
        }
        else if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right)
        {
            if (hud?.CancelActiveTechTargeting() == true)
                return;
            if (CancelCommandMode(false))
            {
                hud?.ShowAlert("已取消当前命令");
                return;
            }
            if (BattleGameManager.Instance is { PendingBuildKey.Length: > 0 } manager)
            {
                manager.CancelBuildPlacement();
                return;
            }
            IssueOrder(right.Position, right.ShiftPressed, right.AltPressed, right.CtrlPressed);
        }
        else if (evt is InputEventMouseMotion motion && dragging)
        {
            dragEnd = motion.Position;
        }
    }

    void HandleTouch(InputEventScreenTouch touch)
    {
        suppressMouseUntilMs = Time.GetTicksMsec() + 250UL;

        if (touch.Pressed)
        {
            if (touchTracking && activeTouchIndex != touch.Index)
                return;

            activeTouchIndex = touch.Index;
            touchTracking = true;
            dragStart = touch.Position;
            dragEnd = touch.Position;
            dragging = true;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!touchTracking || touch.Index != activeTouchIndex)
            return;

        touchTracking = false;
        activeTouchIndex = -1;
        dragging = false;
        HandlePrimaryRelease(touch.Position, true);
        GetViewport().SetInputAsHandled();
    }

    void HandlePrimaryRelease(Vector2 screenPos, bool mobileTouch)
    {
        var hud = FindHud();
        if (hud?.IsPointerOverBlockingHud(screenPos) == true)
            return;
        if (hud?.TryCastActiveTech(screenPos) == true)
            return;
        if (TryPlaceBuilding(screenPos))
            return;

        dragEnd = screenPos;
        var dragDistance = dragStart.DistanceTo(dragEnd);
        if (dragDistance < DragThreshold && TryHandleCommandClick(screenPos))
            return;
        if (dragDistance >= DragThreshold)
        {
            BoxSelect(dragStart, dragEnd);
            return;
        }

        if (mobileTouch && TryHandleTouchTap(screenPos))
            return;

        SelectAt(screenPos);
    }

    bool TryHandleTouchTap(Vector2 screenPos)
    {
        var hit = Raycast(screenPos);
        var selected = GetSelectedOwnedUnits();
        Node? target = null;
        if (hit.Count > 0)
        {
            target = FindSelectable(hit["collider"].AsGodotObject() as Node);
            if (target is Node3D target3D && BattleGameManager.Instance is { } manager && !manager.IsVisibleToPlayer(target3D))
                target = null;
        }

        if (target is not null)
        {
            if (target.IsInGroup("enemy_owned") && selected.Count > 0)
            {
                IssueOrder(screenPos, false, false, false);
                return true;
            }

            GameState.Instance?.SetSelection(new[] { target });
            return true;
        }

        if (selected.Count > 0)
        {
            IssueOrder(screenPos, false, false, false);
            return true;
        }

        GameState.Instance?.SetSelection(System.Array.Empty<Node>());
        return true;
    }

    public bool BeginPatrolMode(out string message)
    {
        message = "";
        if (GetSelectedOwnedUnits().Count == 0)
        {
            message = "请先选择己方单位";
            return false;
        }

        activeCommandMode = CommandMode.Patrol;
        message = "巡逻路线：点击地面设置巡逻终点";
        return true;
    }

    public bool BeginAttackGroundMode(out string message)
    {
        message = "";
        if (GetSelectedAttackGroundUnits().Count == 0)
        {
            message = "请选择坦克、炮兵或驱逐舰等范围攻击单位";
            return false;
        }

        activeCommandMode = CommandMode.AttackGround;
        message = "炮击地点：点击地面指定持续攻击点";
        return true;
    }

    public bool BeginBombingRunMode(out string message)
    {
        message = "";
        if (GetSelectedBombers().Count == 0)
        {
            message = "当前选择中没有轰炸机";
            return false;
        }

        activeCommandMode = CommandMode.BombingRunStart;
        message = "区域轰炸：先点起点，再点终点";
        return true;
    }

    public bool ParkSelectedAircraft(out string message)
    {
        message = "";
        var manager = BattleGameManager.Instance;
        if (manager is null)
        {
            message = "战场系统初始化中";
            return false;
        }

        var selected = GetSelectedParkableAircraft();
        if (selected.Count == 0)
        {
            message = "没有可停机的飞机";
            return false;
        }

        var parkedCount = 0;
        foreach (var unit in selected)
        {
            if (!manager.TrySendUnitToAirfieldParking(unit, out _))
                continue;

            parkedCount++;
            GameRelay.Instance?.SendPark(unit.NetId);
        }

        if (parkedCount == 0)
        {
            message = "当前没有可返场的飞机";
            return false;
        }

        CancelCommandMode(false);
        message = parkedCount == 1 ? "飞机正在返回停机场" : $"{parkedCount} 架飞机正在返回停机场";
        return true;
    }

    public bool CancelCommandMode(bool notifyHud)
    {
        var hadMode = activeCommandMode != CommandMode.None;
        activeCommandMode = CommandMode.None;
        bombingRunStartPoint = Vector3.Zero;
        if (notifyHud && hadMode)
            FindHud()?.ShowAlert("已取消当前命令");
        return hadMode;
    }

    bool TryHandleCommandClick(Vector2 screenPos)
    {
        if (activeCommandMode == CommandMode.None)
            return false;

        if (!TryProjectGroundPoint(screenPos, out var position))
        {
            FindHud()?.ShowAlert("请选择地面位置");
            return true;
        }

        switch (activeCommandMode)
        {
            case CommandMode.Patrol:
                IssuePatrolOrder(position);
                break;
            case CommandMode.AttackGround:
                IssueAttackGroundOrder(position);
                break;
            case CommandMode.BombingRunStart:
                bombingRunStartPoint = position;
                activeCommandMode = CommandMode.BombingRunDirection;
                FindHud()?.ShowAlert("已锁定起点，请点击终点确定轰炸方向");
                break;
            case CommandMode.BombingRunDirection:
                IssueBombingRunOrder(position);
                break;
        }

        return true;
    }

    bool TryPlaceBuilding(Vector2 screenPos)
    {
        var manager = BattleGameManager.Instance;
        if (manager is null || string.IsNullOrEmpty(manager.PendingBuildKey))
            return false;

        if (!TryProjectGroundPoint(screenPos, out var position))
        {
            FindHud()?.ShowAlert("请选择地面位置建造");
            return true;
        }

        var pendingKey = manager.PendingBuildKey;
        var placed = manager.TryPlacePendingBuilding(position, out var message, out var constructed);
        if (placed && constructed is not null)
        {
            GameRelay.Instance?.SendBuild(pendingKey, constructed.GlobalPosition, constructed.NetId);
            BattleFeedback.Command(this, constructed.GlobalPosition, "BUILD", new Color(0.64f, 1f, 0.62f));
        }
        if (!string.IsNullOrWhiteSpace(message))
            FindHud()?.ShowAlert(message);
        return true;
    }

    void SelectAt(Vector2 screenPos)
    {
        if (FindHud()?.IsPointerOverBlockingHud(screenPos) == true)
            return;

        var hit = Raycast(screenPos);
        if (hit.Count == 0)
        {
            GameState.Instance?.SetSelection(System.Array.Empty<Node>());
            return;
        }

        var target = FindSelectable(hit["collider"].AsGodotObject() as Node);
        if (target is Node3D target3D && BattleGameManager.Instance is { } manager && !manager.IsVisibleToPlayer(target3D))
            target = null;
        GameState.Instance?.SetSelection(target is null ? System.Array.Empty<Node>() : new[] { target });
    }

    void BoxSelect(Vector2 start, Vector2 end)
    {
        var min = new Vector2(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        var max = new Vector2(Mathf.Max(start.X, end.X), Mathf.Max(start.Y, end.Y));
        var rect = new Rect2(min, max - min);
        var selected = new List<Node>();

        foreach (var unit in GetTree().GetNodesInGroup("rts_units").OfType<RtsUnit>())
        {
            if (!GodotObject.IsInstanceValid(unit) || !unit.PlayerOwned || unit.IsDead)
                continue;
            var screen = camera.UnprojectPosition(unit.GlobalPosition + Vector3.Up);
            if (rect.HasPoint(screen))
                selected.Add(unit);
        }

        if (selected.Count == 0)
        {
            SelectAt(end);
            return;
        }

        GameState.Instance?.SetSelection(selected);
    }

    void IssueOrder(Vector2 screenPos, bool attackMove, bool patrolOrder, bool guardOrder)
    {
        if (FindHud()?.IsPointerOverBlockingHud(screenPos) == true)
            return;

        var hit = Raycast(screenPos);
        if (hit.Count == 0 || GameState.Instance is null)
            return;

        var target = FindSelectable(hit["collider"].AsGodotObject() as Node);
        if (target is Node3D target3D && BattleGameManager.Instance is { } manager && !manager.IsVisibleToPlayer(target3D))
            target = null;
        var selected = GameState.Instance.Selected.OfType<RtsUnit>().Where(GodotObject.IsInstanceValid).ToList();
        var selectedProductionBuilding = GameState.Instance.Selected
            .OfType<RtsBuilding>()
            .FirstOrDefault(b => GodotObject.IsInstanceValid(b) && b.PlayerOwned && b.CanSetRallyPoint());

        if (guardOrder && target is RtsUnit ally && ally.PlayerOwned)
        {
            foreach (var unit in selected.Where(unit => unit != ally))
            {
                unit.Guard(ally);
                GameRelay.Instance?.SendGuard(unit.NetId, ally.NetId);
            }
            return;
        }

        if (target is Node3D enemy && target.IsInGroup("enemy_owned"))
        {
            if (selected.Count == 0)
                return;

            foreach (var unit in selected)
            {
                unit.Attack(enemy);
                GameRelay.Instance?.SendAttack(unit.NetId, GetNetId(enemy), target.IsInGroup("rts_buildings"));
            }
            BattleFeedback.Command(this, enemy.GlobalPosition, "ATTACK", new Color(1f, 0.38f, 0.30f));
            return;
        }

        var position = hit["position"].AsVector3();
        if (selected.Count == 0 && selectedProductionBuilding is not null)
        {
            var rallyPoint = NormalizeRallyPoint(selectedProductionBuilding, position);
            selectedProductionBuilding.SetRallyPoint(rallyPoint);
            GameRelay.Instance?.SendRally(selectedProductionBuilding.NetId, rallyPoint);
            BattleFeedback.Command(this, rallyPoint, "RALLY", new Color(0.58f, 0.94f, 1f));
            return;
        }
        if (selected.Count == 0)
            return;

        if (guardOrder)
        {
            var attackGroundUnits = selected.Where(unit => unit.CanAttackGroundPoint).ToList();
            if (attackGroundUnits.Count == 0)
                return;

            foreach (var unit in attackGroundUnits)
            {
                if (unit.AttackGround(position))
                    GameRelay.Instance?.SendAttackGround(unit.NetId, position);
            }
            GameState.Instance?.SetSelection(attackGroundUnits);
            BattleFeedback.Command(this, position, "FIRE", new Color(1f, 0.58f, 0.28f));
            return;
        }

        for (var i = 0; i < selected.Count; i++)
        {
            var offset = FormationOffset(i, selected.Count);
            var orderTarget = NormalizeOrderTarget(selected[i], position + offset);
            if (patrolOrder)
            {
                selected[i].PatrolTo(orderTarget);
                GameRelay.Instance?.SendPatrol(selected[i].NetId, selected[i].GlobalPosition, orderTarget);
            }
            else if (attackMove)
            {
                selected[i].AttackMoveTo(orderTarget);
                GameRelay.Instance?.SendAttackMove(selected[i].NetId, orderTarget);
            }
            else
            {
                selected[i].MoveTo(orderTarget);
                GameRelay.Instance?.SendMove(selected[i].NetId, orderTarget);
            }
        }

        BattleFeedback.Command(
            this,
            position,
            patrolOrder ? "PATROL" : attackMove ? "A-MOVE" : "MOVE",
            patrolOrder ? new Color(0.60f, 1f, 0.64f) : attackMove ? new Color(1f, 0.72f, 0.36f) : new Color(0.58f, 0.88f, 1f));
    }

    void IssuePatrolOrder(Vector3 position)
    {
        var selected = GetSelectedOwnedUnits();
        if (selected.Count == 0)
        {
            CancelCommandMode(false);
            FindHud()?.ShowAlert("请先选择己方单位");
            return;
        }

        for (var i = 0; i < selected.Count; i++)
        {
            var offset = FormationOffset(i, selected.Count);
            var orderTarget = NormalizeOrderTarget(selected[i], position + offset);
            selected[i].PatrolTo(orderTarget);
            GameRelay.Instance?.SendPatrol(selected[i].NetId, selected[i].GlobalPosition, orderTarget);
        }

        CancelCommandMode(false);
        FindHud()?.ShowAlert(selected.Count == 1 ? "已指定巡逻路线" : $"{selected.Count} 个单位已指定巡逻路线");
        BattleFeedback.Command(this, position, "PATROL", new Color(0.60f, 1f, 0.64f));
    }

    void IssueAttackGroundOrder(Vector3 position)
    {
        var selected = GetSelectedAttackGroundUnits();
        if (selected.Count == 0)
        {
            CancelCommandMode(false);
            FindHud()?.ShowAlert("当前选择中没有可炮击地点的单位");
            return;
        }

        foreach (var unit in selected)
        {
            if (unit.AttackGround(position))
                GameRelay.Instance?.SendAttackGround(unit.NetId, position);
        }

        GameState.Instance?.SetSelection(selected);
        CancelCommandMode(false);
        FindHud()?.ShowAlert(selected.Count == 1 ? "已指定炮击地点" : $"{selected.Count} 个单位已指定炮击地点");
        BattleFeedback.Command(this, position, "FIRE", new Color(1f, 0.58f, 0.28f));
    }

    void IssueBombingRunOrder(Vector3 endPoint)
    {
        var bombers = GetSelectedBombers();
        if (bombers.Count == 0)
        {
            CancelCommandMode(false);
            FindHud()?.ShowAlert("当前选择中没有可执行轰炸的轰炸机");
            return;
        }

        var start = new Vector3(bombingRunStartPoint.X, 0f, bombingRunStartPoint.Z);
        var end = new Vector3(endPoint.X, 0f, endPoint.Z);
        var delta = end - start;
        if (delta.LengthSquared() < 0.01f)
            delta = Vector3.Forward * RtsUnit.BomberMinBombingRunLength;

        var direction = delta.Normalized();
        var length = Mathf.Clamp(delta.Length(), RtsUnit.BomberMinBombingRunLength, RtsUnit.BomberMaxBombingRunLength);
        var perpendicular = Vector3.Up.Cross(direction).Normalized();
        var normalizedEnd = start + direction * length;

        for (var i = 0; i < bombers.Count; i++)
        {
            var laneIndex = i - (bombers.Count - 1) * 0.5f;
            var laneOffset = perpendicular * (laneIndex * RtsUnit.BomberBombingRunLaneSpacing);
            var laneStart = start + laneOffset;
            var laneEnd = normalizedEnd + laneOffset;
            bombers[i].ApplyBombingRunCommand(laneStart, laneEnd);
            GameRelay.Instance?.SendBombingRun(bombers[i].NetId, laneStart, laneEnd);
        }

        CancelCommandMode(false);
        FindHud()?.ShowAlert(bombers.Count == 1 ? "轰炸机开始执行区域轰炸" : $"{bombers.Count} 架轰炸机开始执行区域轰炸");
        BattleFeedback.Command(this, normalizedEnd, "BOMB", new Color(1f, 0.52f, 0.22f));
    }

    static Vector3 FormationOffset(int index, int count)
    {
        if (count <= 1)
            return Vector3.Zero;

        var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        var rows = Mathf.CeilToInt(count / (float)columns);
        var column = index % columns;
        var row = index / columns;
        var spacing = count > 8 ? 2.2f : 1.65f;
        return new Vector3(
            (column - (columns - 1) * 0.5f) * spacing,
            0f,
            (row - (rows - 1) * 0.5f) * spacing);
    }

    static Vector3 NormalizeOrderTarget(RtsUnit unit, Vector3 position)
    {
        if (BattleGameManager.Instance is { } manager)
            return manager.NormalizeUnitTarget(unit, position);
        return BattleMapCatalog.ClampToMap(position, 4f);
    }

    static Vector3 NormalizeRallyPoint(RtsBuilding building, Vector3 position)
    {
        var roster = building.GetProductionRoster();
        if (roster.Length > 0 && BattleGameManager.Instance is { } manager)
            return manager.NormalizeUnitTarget(roster[0], position);
        return BattleMapCatalog.ClampToMap(position, 4f);
    }

    List<RtsUnit> GetSelectedOwnedUnits()
    {
        return GameState.Instance?.Selected
            .OfType<RtsUnit>()
            .Where(unit => GodotObject.IsInstanceValid(unit) && unit.PlayerOwned && !unit.IsDead)
            .ToList() ?? new List<RtsUnit>();
    }

    List<RtsUnit> GetSelectedAttackGroundUnits()
        => GetSelectedOwnedUnits().Where(unit => unit.CanAttackGroundPoint).ToList();

    List<RtsUnit> GetSelectedBombers()
        => GetSelectedOwnedUnits().Where(unit => unit.SupportsBombingRun).ToList();

    List<RtsUnit> GetSelectedParkableAircraft()
    {
        return GetSelectedOwnedUnits()
            .Where(unit => unit.SupportsAirfieldParking && !unit.IsParkedAtAirfield && !unit.IsReturningToPark)
            .ToList();
    }

    Godot.Collections.Dictionary Raycast(Vector2 screenPos)
    {
        var origin = camera.ProjectRayOrigin(screenPos);
        var end = origin + camera.ProjectRayNormal(screenPos) * 2000f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        return GetViewport().World3D.DirectSpaceState.IntersectRay(query);
    }

    bool TryProjectGroundPoint(Vector2 screenPos, out Vector3 position)
    {
        var origin = camera.ProjectRayOrigin(screenPos);
        var direction = camera.ProjectRayNormal(screenPos);
        if (Mathf.Abs(direction.Y) <= 0.0001f)
        {
            position = Vector3.Zero;
            return false;
        }

        var distance = -origin.Y / direction.Y;
        if (distance <= 0f)
        {
            position = Vector3.Zero;
            return false;
        }

        position = origin + direction * distance;
        position.Y = 0f;
        return true;
    }

    BattleHud? FindHud()
        => GetParent()?.GetNodeOrNull<BattleHud>("HUD");

    static Node? FindSelectable(Node? node)
    {
        while (node is not null && !node.IsInGroup("rts_selectable"))
            node = node.GetParent();
        return node;
    }

    static int GetNetId(Node node)
        => node switch
        {
            RtsUnit unit => unit.NetId,
            RtsBuilding building => building.NetId,
            _ => 0
        };
}
