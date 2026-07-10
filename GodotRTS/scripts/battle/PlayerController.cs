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
    [Export] public double LeftMouseCameraHoldDelayMs { get; set; } = 250.0;
    [Export] public float LeftMouseCameraPanScale { get; set; } = 1.0f;

    Camera3D camera = null!;
    RtsCamera? cameraRig;
    Vector2 dragStart;
    Vector2 dragEnd;
    bool dragging;
    bool leftCameraPanCandidate;
    bool leftCameraPanActive;
    ulong leftPressStartedAtMs;
    bool touchTracking;
    int activeTouchIndex = -1;
    ulong suppressMouseUntilMs;
    CommandMode activeCommandMode;
    Vector3 bombingRunStartPoint;
    Node3D? buildPlacementPreview;
    readonly List<Node3D> buildPlacementRangeIndicators = new();
    readonly List<Node3D> occupiedPlacementOverlays = new();
    string buildPlacementPreviewKey = "";
    string occupiedPlacementOverlayKey = "";
    bool isMousePressedInPlacementMode;
    string buildPlacementRangeIndicatorKey = "";
    bool buildPlacementPreviewWasValid;
    bool hasBuildPlacementPreviewPoint;
    Vector3 buildPlacementPreviewPoint;
    float buildPlacementRangeIndicatorRadius;

    ulong lastClickTimeMs;
    Node? lastClickedTarget;
    const ulong DoubleClickTimeThresholdMs = 350;

    public string? BuildPlacementError { get; private set; }

    public bool IsDraggingSelection => dragging && !leftCameraPanActive && dragStart.DistanceTo(dragEnd) >= DragThreshold;
    public Vector2 DragStart => dragStart;
    public Vector2 DragEnd => dragEnd;
    public bool IsPatrolModeActive => activeCommandMode == CommandMode.Patrol;
    public bool IsAttackGroundModeActive => activeCommandMode == CommandMode.AttackGround;
    public bool IsBombingRunModeActive => activeCommandMode is CommandMode.BombingRunStart or CommandMode.BombingRunDirection;
    public bool IsBombingRunAwaitingDirection => activeCommandMode == CommandMode.BombingRunDirection;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>(CameraPath);
        cameraRig = camera.GetParent() as RtsCamera;
    }

    public override void _Process(double delta)
    {
        UpdateBuildPlacementPreview();
    }

    public override void _ExitTree()
    {
        ClearBuildPlacementPreview();
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
                leftCameraPanCandidate = CanStartLeftMouseCameraPan(left.Position);
                leftCameraPanActive = false;
                leftPressStartedAtMs = Time.GetTicksMsec();

                if (BattleGameManager.Instance is not null && !string.IsNullOrEmpty(BattleGameManager.Instance.PendingBuildKey))
                {
                    isMousePressedInPlacementMode = true;
                }
            }
            else
            {
                if (leftCameraPanActive || IsLeftMouseCameraHoldSatisfied())
                {
                    dragging = false;
                    leftCameraPanCandidate = false;
                    leftCameraPanActive = false;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                dragging = false;
                HandlePrimaryRelease(left.Position, false);
                leftCameraPanCandidate = false;
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
                ClearBuildPlacementPreview();
                return;
            }
            IssueOrder(right.Position, right.ShiftPressed, right.AltPressed, right.CtrlPressed);
        }
        else if (evt is InputEventMouseMotion motion && dragging)
        {
            dragEnd = motion.Position;
            if (ShouldPanCameraWithLeftMouse(motion.Position, motion.Relative))
            {
                leftCameraPanActive = true;
                cameraRig?.PanByScreenDelta(motion.Relative, LeftMouseCameraPanScale);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    bool CanStartLeftMouseCameraPan(Vector2 screenPos)
    {
        if (cameraRig is null || activeCommandMode != CommandMode.None)
            return false;
        if (FindHud()?.IsPointerOverBlockingHud(screenPos) == true)
            return false;
        if (BattleGameManager.Instance is { PendingBuildKey.Length: > 0 })
            return false;
        if (TryGetVisibleSelectable(screenPos) is not null)
            return false;
        return true;
    }

    bool IsLeftMouseCameraHoldSatisfied()
    {
        if (!leftCameraPanCandidate)
            return false;

        var heldForMs = (double)(Time.GetTicksMsec() - leftPressStartedAtMs);
        return heldForMs >= LeftMouseCameraHoldDelayMs;
    }

    bool ShouldPanCameraWithLeftMouse(Vector2 screenPos, Vector2 motionDelta)
    {
        if (!leftCameraPanCandidate || cameraRig is null)
            return false;
        if (!IsLeftMouseCameraHoldSatisfied())
            return false;
        if (dragStart.DistanceTo(screenPos) < DragThreshold)
            return false;
        return motionDelta != Vector2.Zero;
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

            if (BattleGameManager.Instance is not null && !string.IsNullOrEmpty(BattleGameManager.Instance.PendingBuildKey))
            {
                isMousePressedInPlacementMode = true;
            }

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
        BattleFeedback.UiConfirm(this);
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
        BattleFeedback.UiConfirm(this);
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
        BattleFeedback.UiConfirm(this);
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
        if (hadMode)
            BattleFeedback.UiCancel(this);
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
                BattleFeedback.UiConfirm(this);
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

        if (!isMousePressedInPlacementMode)
            return true;

        Vector3 position;
        if (buildPlacementPreviewWasValid && hasBuildPlacementPreviewPoint)
        {
            position = buildPlacementPreviewPoint;
        }
        else if (!TryProjectGroundPoint(screenPos, out position))
        {
            FindHud()?.ShowAlert("请选择地面位置建造");
            return true;
        }

        var pendingKey = manager.PendingBuildKey;
        var placed = manager.TryPlacePendingBuilding(position, out var message, out var constructed);
        if (placed && constructed is not null)
        {
            ClearBuildPlacementPreview();
            GameRelay.Instance?.SendBuild(pendingKey, constructed.GlobalPosition, constructed.NetId);
            BattleFeedback.Command(this, constructed.GlobalPosition, "BUILD", new Color(0.64f, 1f, 0.62f));
        }
        if (!string.IsNullOrWhiteSpace(message))
            FindHud()?.ShowAlert(message);
        return true;
    }

    void UpdateBuildPlacementPreview()
    {
        var manager = BattleGameManager.Instance;
        if (manager is null || string.IsNullOrWhiteSpace(manager.PendingBuildKey))
        {
            ClearBuildPlacementPreview();
            return;
        }

        var pendingKey = manager.PendingBuildKey.Trim();
        if (buildPlacementPreview is null
            || !GodotObject.IsInstanceValid(buildPlacementPreview)
            || !string.Equals(buildPlacementPreviewKey, pendingKey, System.StringComparison.Ordinal))
        {
            ClearBuildPlacementPreview();
            buildPlacementPreview = manager.CreateBuildingPlacementPreview(pendingKey);
            buildPlacementPreviewKey = pendingKey;
            GetParent()?.AddChild(buildPlacementPreview);
            buildPlacementPreviewWasValid = false;
            isMousePressedInPlacementMode = false;
        }

        EnsureOccupiedPlacementOverlays(manager, pendingKey);
        UpdateBuildPlacementRangeIndicator(manager, pendingKey, true);

        var screenPos = CurrentPlacementPointerScreenPosition();
        if (FindHud()?.IsPointerOverBlockingHud(screenPos) == true || !TryProjectGroundPoint(screenPos, out var position))
        {
            hasBuildPlacementPreviewPoint = false;
            buildPlacementPreviewWasValid = false;
            BuildPlacementError = null;
            if (buildPlacementPreview is not null && GodotObject.IsInstanceValid(buildPlacementPreview))
                buildPlacementPreview.Visible = false;
            SetOccupiedPlacementOverlaysVisible(false);
            return;
        }

        if (buildPlacementPreview is null || !GodotObject.IsInstanceValid(buildPlacementPreview))
            return;

        var previewPosition = new Vector3(position.X, 0f, position.Z);
        buildPlacementPreviewPoint = previewPosition;
        hasBuildPlacementPreviewPoint = true;

        var canPlace = manager.CanPlaceBuildingAt(pendingKey, previewPosition, true, out var placementError);
        BuildPlacementError = canPlace ? null : placementError;

        var inBuildRange = manager.IsWithinBuildPlacementRange(previewPosition, true);
        UpdateBuildPlacementRangeIndicator(manager, pendingKey, inBuildRange);
        buildPlacementPreview.Position = previewPosition;
        buildPlacementPreview.Visible = true;
        SetOccupiedPlacementOverlaysVisible(true);

        manager.ApplyBuildingPlacementPreviewAppearance(buildPlacementPreview, canPlace);
        buildPlacementPreviewWasValid = canPlace;
    }

    Vector2 CurrentPlacementPointerScreenPosition()
        => touchTracking ? dragEnd : GetViewport().GetMousePosition();

    void ClearBuildPlacementPreview()
    {
        buildPlacementPreviewKey = "";
        buildPlacementPreviewWasValid = false;
        hasBuildPlacementPreviewPoint = false;
        buildPlacementPreviewPoint = Vector3.Zero;
        BuildPlacementError = null;
        if (buildPlacementPreview is not null && GodotObject.IsInstanceValid(buildPlacementPreview))
            buildPlacementPreview.QueueFree();
        buildPlacementPreview = null;
        ClearOccupiedPlacementOverlays();
        ClearBuildPlacementRangeIndicator();
        isMousePressedInPlacementMode = false;
    }

    void EnsureOccupiedPlacementOverlays(BattleGameManager manager, string buildKey)
    {
        if (occupiedPlacementOverlays.Count > 0
            && string.Equals(occupiedPlacementOverlayKey, buildKey, System.StringComparison.Ordinal)
            && occupiedPlacementOverlays.All(GodotObject.IsInstanceValid))
        {
            return;
        }

        ClearOccupiedPlacementOverlays();
        occupiedPlacementOverlayKey = buildKey;
        foreach (var overlay in manager.CreateOccupiedPlacementOverlays(buildKey))
        {
            occupiedPlacementOverlays.Add(overlay);
            GetParent()?.AddChild(overlay);
        }
    }

    void ClearOccupiedPlacementOverlays()
    {
        occupiedPlacementOverlayKey = "";
        foreach (var overlay in occupiedPlacementOverlays)
        {
            if (GodotObject.IsInstanceValid(overlay))
                overlay.QueueFree();
        }
        occupiedPlacementOverlays.Clear();
    }

    void SetOccupiedPlacementOverlaysVisible(bool visible)
    {
        foreach (var overlay in occupiedPlacementOverlays)
        {
            if (GodotObject.IsInstanceValid(overlay))
                overlay.Visible = visible;
        }
    }

    void UpdateBuildPlacementRangeIndicator(BattleGameManager manager, string buildKey, bool inRange)
    {
        var ranges = manager.GetBuildPlacementRanges(true);
        if (ranges.Length == 0)
        {
            ClearBuildPlacementRangeIndicator();
            return;
        }

        var radius = ranges[0].radius;
        if (buildPlacementRangeIndicators.Count != ranges.Length
            || !string.Equals(buildPlacementRangeIndicatorKey, buildKey, System.StringComparison.Ordinal)
            || !Mathf.IsEqualApprox(buildPlacementRangeIndicatorRadius, radius))
        {
            ClearBuildPlacementRangeIndicator();
            for (var i = 0; i < ranges.Length; i++)
            {
                var indicator = CreateBuildPlacementRangeIndicator(radius);
                buildPlacementRangeIndicators.Add(indicator);
                GetParent()?.AddChild(indicator);
            }
            buildPlacementRangeIndicatorKey = buildKey;
            buildPlacementRangeIndicatorRadius = radius;
        }

        for (var i = 0; i < buildPlacementRangeIndicators.Count; i++)
        {
            var indicator = buildPlacementRangeIndicators[i];
            if (!GodotObject.IsInstanceValid(indicator))
                continue;

            indicator.Position = new Vector3(ranges[i].center.X, 0.035f, ranges[i].center.Z);
            indicator.Visible = true;
            ApplyBuildPlacementRangeIndicatorAppearance(indicator, inRange);
        }
    }

    void ClearBuildPlacementRangeIndicator()
    {
        buildPlacementRangeIndicatorKey = "";
        buildPlacementRangeIndicatorRadius = 0f;
        foreach (var indicator in buildPlacementRangeIndicators)
        {
            if (GodotObject.IsInstanceValid(indicator))
                indicator.QueueFree();
        }
        buildPlacementRangeIndicators.Clear();
    }

    static Node3D CreateBuildPlacementRangeIndicator(float radius)
    {
        var indicator = new Node3D
        {
            Name = "BuildPlacementRangeIndicator"
        };

        indicator.AddChild(new MeshInstance3D
        {
            Name = "RangeFill",
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.02f,
                RadialSegments = 64
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        indicator.AddChild(new MeshInstance3D
        {
            Name = "RangeOutline",
            Position = new Vector3(0f, 0.012f, 0f),
            Mesh = new TorusMesh
            {
                InnerRadius = Mathf.Max(0.2f, radius - 0.38f),
                OuterRadius = radius,
                RingSegments = 96,
                Rings = 12
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        ApplyBuildPlacementRangeIndicatorAppearance(indicator, true);
        return indicator;
    }

    static readonly StandardMaterial3D RangeFillMaterialGreen = CreateStandardRangeMaterial(new Color(0.18f, 0.92f, 0.36f, 0.08f));
    static readonly StandardMaterial3D RangeFillMaterialRed = CreateStandardRangeMaterial(new Color(1f, 0.24f, 0.18f, 0.10f));
    static readonly StandardMaterial3D RangeOutlineMaterialGreen = CreateStandardRangeMaterial(new Color(0.38f, 1f, 0.60f, 0.44f));
    static readonly StandardMaterial3D RangeOutlineMaterialRed = CreateStandardRangeMaterial(new Color(1f, 0.38f, 0.28f, 0.52f));

    static StandardMaterial3D CreateStandardRangeMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true
        };
    }

    static void ApplyBuildPlacementRangeIndicatorAppearance(Node3D indicator, bool inRange)
    {
        var fillMat = inRange ? RangeFillMaterialGreen : RangeFillMaterialRed;
        var outlineMat = inRange ? RangeOutlineMaterialGreen : RangeOutlineMaterialRed;

        if (indicator.GetNodeOrNull<MeshInstance3D>("RangeFill") is { } fill)
            fill.MaterialOverride = fillMat;
        if (indicator.GetNodeOrNull<MeshInstance3D>("RangeOutline") is { } outline)
            outline.MaterialOverride = outlineMat;
    }

    void SelectAt(Vector2 screenPos)
    {
        if (FindHud()?.IsPointerOverBlockingHud(screenPos) == true)
            return;

        var target = TryGetVisibleSelectable(screenPos);
        if (target is null)
        {
            GameState.Instance?.SetSelection(System.Array.Empty<Node>());
            lastClickTimeMs = 0;
            lastClickedTarget = null;
            return;
        }

        // 双击同兵种选择判定
        if (target is RtsUnit clickUnit && clickUnit.PlayerOwned && !clickUnit.IsDead)
        {
            var now = Time.GetTicksMsec();
            if (lastClickedTarget == clickUnit && (now - lastClickTimeMs) <= DoubleClickTimeThresholdMs)
            {
                SelectNearbyUnitsOfSameType(clickUnit);
                lastClickTimeMs = 0;
                lastClickedTarget = null;
                return;
            }
            else
            {
                lastClickTimeMs = now;
                lastClickedTarget = clickUnit;
            }
        }
        else
        {
            lastClickTimeMs = 0;
            lastClickedTarget = null;
        }

        GameState.Instance?.SetSelection(new[] { target });
    }

    void SelectNearbyUnitsOfSameType(RtsUnit clickUnit)
    {
        if (GameState.Instance is null || camera is null)
            return;

        var selected = new List<Node>();
        var visibleRect = GetViewport().GetVisibleRect();
        var clickPos = clickUnit.GlobalPosition;

        foreach (var node in GetTree().GetNodesInGroup("rts_units"))
        {
            if (node is RtsUnit unit && GodotObject.IsInstanceValid(unit) && !unit.IsDead && unit.PlayerOwned)
            {
                if (unit.UnitKey == clickUnit.UnitKey)
                {
                    // 屏幕内可见性检测
                    var screenPos = camera.UnprojectPosition(unit.GlobalPosition);
                    bool isVisible = visibleRect.HasPoint(screenPos) && !camera.IsPositionBehind(unit.GlobalPosition);

                    // 距离检测
                    bool isNear = unit.GlobalPosition.DistanceTo(clickPos) <= 45f;

                    if (isVisible || isNear)
                    {
                        selected.Add(unit);
                    }
                }
            }
        }

        if (selected.Count > 0)
        {
            GameState.Instance.SetSelection(selected);
            BattleFeedback.UiConfirm(this);
        }
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

        var target = TryGetVisibleSelectable(screenPos, hit);
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

        bool hasVehicles = selected.Any(u => !BattleUnitCatalog.IsInfantryLike(u.UnitKey));
        for (var i = 0; i < selected.Count; i++)
        {
            var offset = FormationOffset(i, selected.Count, hasVehicles);
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

        bool hasVehicles = selected.Any(u => !BattleUnitCatalog.IsInfantryLike(u.UnitKey));
        for (var i = 0; i < selected.Count; i++)
        {
            var offset = FormationOffset(i, selected.Count, hasVehicles);
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

    static Vector3 FormationOffset(int index, int count, bool hasVehicles)
    {
        if (count <= 1)
            return Vector3.Zero;

        var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        var rows = Mathf.CeilToInt(count / (float)columns);
        var column = index % columns;
        var row = index / columns;
        var spacing = hasVehicles ? 3.6f : 2.6f; // 载具群间距为 3.6m，纯步兵群间距为 2.6m
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
        if (BattleGameManager.Instance is { } fallbackManager)
            return fallbackManager.ClampToPlayableMap(position);
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
        var hit = Raycast(screenPos);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
            var hitPos = hit["position"].AsVector3();
            position = new Vector3(hitPos.X, 0f, hitPos.Z);
            return true;
        }

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

    Node? TryGetVisibleSelectable(Vector2 screenPos, Godot.Collections.Dictionary? hit = null)
    {
        hit ??= Raycast(screenPos);
        if (hit.Count == 0)
            return null;

        var target = FindSelectable(hit["collider"].AsGodotObject() as Node);
        if (target is Node3D target3D && BattleGameManager.Instance is { } manager && !manager.IsVisibleToPlayer(target3D))
            return null;
        return target;
    }

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
