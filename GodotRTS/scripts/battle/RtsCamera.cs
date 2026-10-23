using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RtsCamera : Node3D
{
    [Export] public float PanSpeed { get; set; } = 35f;
    [Export] public float EdgePanSpeedScale { get; set; } = 1.6f;
    [Export] public float ZoomSpeed { get; set; } = 5f;
    [Export] public float MinHeight { get; set; } = 14f;
    [Export] public float MaxHeight { get; set; } = 48f;
    [Export] public float DragPanSensitivity { get; set; } = 0.04f;

    Camera3D _camera = null!;
    Camera3D camera => _camera ??= GetNode<Camera3D>("Camera3D");
    bool dragging;
    readonly Dictionary<int, Vector2> touches = new();
    readonly Dictionary<int, Vector2> touchStartPositions = new();
    bool touchGestureActive;
    float lastPinchDistance;
    Vector2 lastTouchCenter;
    ulong suppressMouseUntilMs;
    float initialRatio = 1.1f;

    public static RtsCamera? Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        Instance = this;
        if (!IsInGroup("rts_cameras"))
            AddToGroup("rts_cameras");
        EnsureCameraConfigured();
    }

    public void EnsureCameraConfigured()
    {
        var cam = camera;
        cam.Near = 1f;
        cam.Far = 800f;

        if (Mathf.Abs(cam.Position.X) > 0.1f || cam.Position.Y < 5f)
        {
            cam.Position = new Vector3(0f, 32f, -38f);
            cam.Rotation = new Vector3(Mathf.DegToRad(-37.7f), Mathf.DegToRad(180f), 0f);
            cam.ForceUpdateTransform();
        }

        if (cam.Position.Y > 0.01f)
        {
            initialRatio = cam.Position.Z / cam.Position.Y;
        }
    }

    /// <summary>
    /// 将镜头立即定位对齐到玩家当前的选定部队或主力部队集结处
    /// </summary>
    /// <summary>
    /// 将镜头立即定位对齐到己方主基地上空
    /// </summary>
    public void FocusOnMainBase()
    {
        if (BattleGameManager.Instance is { } manager)
        {
            manager.FocusCameraOnMainBase();
            return;
        }

        var buildings = GetTree().GetNodesInGroup("player_owned")
            .OfType<RtsBuilding>()
            .Where(b => GodotObject.IsInstanceValid(b) && !b.IsRuined)
            .ToList();
        var mainBase = buildings.FirstOrDefault(b => b.IsMainBase) ?? buildings.FirstOrDefault();
        if (mainBase is not null)
        {
            JumpTo(mainBase.GlobalPosition);
        }
    }

    public void FocusOnPlayerForces()
    {
        if (BattleGameManager.Instance is { } manager)
        {
            manager.FocusCameraOnPlayerForces();
            return;
        }

        var units = GetTree().GetNodesInGroup("player_owned")
            .OfType<RtsUnit>()
            .Where(u => GodotObject.IsInstanceValid(u) && !u.IsDead)
            .ToList();

        if (units.Count > 0)
        {
            var center = Vector3.Zero;
            foreach (var u in units) center += u.GlobalPosition;
            center /= units.Count;
            JumpTo(center);
            return;
        }

        var buildings = GetTree().GetNodesInGroup("player_owned")
            .OfType<RtsBuilding>()
            .Where(b => GodotObject.IsInstanceValid(b) && !b.IsRuined)
            .ToList();
        var mainBase = buildings.FirstOrDefault(b => b.IsMainBase) ?? buildings.FirstOrDefault();
        if (mainBase is not null)
        {
            JumpTo(mainBase.GlobalPosition);
        }
    }

    float shakeIntensity;
    float shakeDuration;
    float shakeTimer;

    public void TriggerShake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Min(intensity, 0.10f);
        shakeDuration = duration;
        shakeTimer = duration;
    }

    public override void _Process(double delta)
    {
        var dir = Vector3.Zero;
        if (Input.IsActionPressed("camera_pan_left"))
            dir.X -= 1f;
        if (Input.IsActionPressed("camera_pan_right"))
            dir.X += 1f;
        if (Input.IsActionPressed("camera_pan_forward"))
            dir.Z -= 1f;
        if (Input.IsActionPressed("camera_pan_back"))
            dir.Z += 1f;

        // 鼠标边界滚屏检测 (Edge Scrolling)
        var viewport = GetViewport();
        bool isEdgePanning = false;
        if (viewport is not null && !Input.IsMouseButtonPressed(MouseButton.Left) && !CommandLineArgs.Get().Any(arg => arg.Contains("capture")))
        {
            var mousePos = viewport.GetMousePosition();
            var windowSize = viewport.GetVisibleRect().Size;
            const float edgeMargin = 12f; // 敏感判定宽度调回 12 像素，防止日常鼠标操作误触滚动

            // 仅在鼠标位于窗口内部且贴近边缘、且未处于 UI 控件之上时生效，防止在操作 UI 时镜头意外发生边缘滚动
            if (mousePos.X >= 0f && mousePos.X <= windowSize.X && mousePos.Y >= 0f && mousePos.Y <= windowSize.Y && !IsPointerOverBlockingHud(mousePos))
            {
                if (mousePos.X < edgeMargin)
                {
                    dir.X -= 1f;
                    isEdgePanning = true;
                }
                else if (mousePos.X > windowSize.X - edgeMargin)
                {
                    dir.X += 1f;
                    isEdgePanning = true;
                }

                if (mousePos.Y < edgeMargin)
                {
                    dir.Z -= 1f;
                    isEdgePanning = true;
                }
                else if (mousePos.Y > windowSize.Y - edgeMargin)
                {
                    dir.Z += 1f;
                    isEdgePanning = true;
                }
            }
        }

        if (dir != Vector3.Zero)
        {
            float speed = isEdgePanning ? PanSpeed * EdgePanSpeedScale : PanSpeed;
            GlobalPosition += dir.Normalized() * speed * (float)delta;
        }

        ClampVisibleAreaToMap();

        if (shakeTimer > 0f)
        {
            shakeTimer -= (float)delta;
            if (shakeTimer <= 0f)
            {
                camera.HOffset = 0f;
                camera.VOffset = 0f;
            }
            else
            {
                var ratio = shakeTimer / shakeDuration;
                var currentIntensity = shakeIntensity * (ratio * ratio);
                const float ShakeFreq = 26.0f;
                camera.HOffset = Mathf.Sin(shakeTimer * ShakeFreq) * currentIntensity;
                camera.VOffset = Mathf.Cos(shakeTimer * (ShakeFreq * 0.82f)) * (currentIntensity * 0.65f);
            }
        }
        else if (camera.HOffset != 0f || camera.VOffset != 0f)
        {
            camera.HOffset = 0f;
            camera.VOffset = 0f;
        }
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
            HandleTouchDrag(drag);
            return;
        }

        if (Time.GetTicksMsec() < suppressMouseUntilMs)
            return;

        if (evt is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode is Key.Space or Key.Home)
            {
                FocusOnPlayerForces();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (evt is InputEventMouseButton button)
        {
            if (IsPointerOverBlockingHud(button.Position))
                return;

            if (button.ButtonIndex == MouseButton.Middle)
                dragging = button.Pressed;
            else if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
                AdjustZoom(-ZoomSpeed, button.Position);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
                AdjustZoom(ZoomSpeed, button.Position);
        }
        else if (evt is InputEventMouseMotion motion && dragging)
        {
            if (IsPointerOverBlockingHud(motion.Position))
                return;

            PanByScreenDelta(motion.Relative);
        }
    }

    public void PanByScreenDelta(Vector2 screenDelta, float sensitivityScale = 1f)
    {
        var amount = DragPanSensitivity * sensitivityScale;
        GlobalPosition = new Vector3(
            GlobalPosition.X - screenDelta.X * amount,
            GlobalPosition.Y,
            GlobalPosition.Z - screenDelta.Y * amount);
        ClampVisibleAreaToMap();
    }

    public void PanByWorldDelta(Vector3 worldDelta)
    {
        GlobalPosition += new Vector3(worldDelta.X, 0f, worldDelta.Z);
        ClampVisibleAreaToMap();
    }

    void HandleTouch(InputEventScreenTouch touch)
    {
        suppressMouseUntilMs = Time.GetTicksMsec() + 250UL;
        if (touch.Pressed)
        {
            if (IsPointerOverBlockingHud(touch.Position))
                return;
            touches[touch.Index] = touch.Position;
            touchStartPositions[touch.Index] = touch.Position;
        }
        else
        {
            touches.Remove(touch.Index);
            touchStartPositions.Remove(touch.Index);
        }

        if (touches.Count >= 2)
            BeginTouchGesture();
        else
            touchGestureActive = false;
    }

    void HandleTouchDrag(InputEventScreenDrag drag)
    {
        suppressMouseUntilMs = Time.GetTicksMsec() + 250UL;
        if (!touches.ContainsKey(drag.Index))
            return;

        touches[drag.Index] = drag.Position;

        if (touches.Count >= 2)
        {
            var points = touches.Values.Take(2).ToArray();
            var center = (points[0] + points[1]) * 0.5f;
            var distance = points[0].DistanceTo(points[1]);
            if (!touchGestureActive)
            {
                BeginTouchGesture();
                return;
            }

            var centerDelta = center - lastTouchCenter;
            PanByScreenDelta(centerDelta, 1.15f);

            var pinchDelta = lastPinchDistance - distance;
            if (Mathf.Abs(pinchDelta) > 0.5f)
            {
                AdjustZoom(pinchDelta * 0.035f, center);
            }
            lastTouchCenter = center;
            lastPinchDistance = distance;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (touches.Count == 1)
        {
            // Do not pan camera if placing a building preview
            if (BattleGameManager.Instance is { PendingBuildKey.Length: > 0 })
                return;

            if (touchStartPositions.TryGetValue(drag.Index, out var startPos))
            {
                if (IsPointerOverBlockingHud(startPos))
                    return;

                if (startPos.DistanceTo(drag.Position) < 8f)
                    return;
            }

            PanByScreenDelta(drag.Relative);
            GetViewport().SetInputAsHandled();
        }
    }

    void BeginTouchGesture()
    {
        var points = touches.Values.Take(2).ToArray();
        if (points.Length < 2)
            return;

        lastTouchCenter = (points[0] + points[1]) * 0.5f;
        lastPinchDistance = points[0].DistanceTo(points[1]);
        touchGestureActive = true;
    }

    public void AdjustZoom(float amount, Vector2? screenFocusPoint = null)
    {
        var viewport = GetViewport();
        var focus = screenFocusPoint ?? (viewport?.GetVisibleRect().Size * 0.5f) ?? Vector2.Zero;

        Vector3 groundBefore = Vector3.Zero;
        bool projectedBefore = TryProjectScreenPointToGround(focus, out groundBefore);

        var p = camera.Position;
        p.Y = Mathf.Clamp(p.Y + amount, MinHeight, MaxHeight);
        p.Z = p.Y * initialRatio;
        camera.Position = p;

        camera.ForceUpdateTransform();

        if (projectedBefore && TryProjectScreenPointToGround(focus, out Vector3 groundAfter))
        {
            var delta = groundBefore - groundAfter;
            GlobalPosition += new Vector3(delta.X, 0f, delta.Z);
        }

        ClampVisibleAreaToMap();
    }

    bool IsPointerOverBlockingHud(Vector2 screenPosition)
    {
        return GetTree().GetFirstNodeInGroup("battle_hud") is BattleHud hud
            && GodotObject.IsInstanceValid(hud)
            && hud.IsPointerOverBlockingHud(screenPosition);
    }

    public void SetHeight(float height)
    {
        var p = camera.Position;
        p.Y = Mathf.Clamp(height, MinHeight, MaxHeight);
        p.Z = p.Y * initialRatio;
        camera.Position = p;
        camera.ForceUpdateTransform();
        ClampVisibleAreaToMap();
    }

    public void JumpTo(Vector3 worldPosition)
    {
        EnsureCameraConfigured();
        var center = GetGroundCenter();
        var delta = worldPosition - center;
        GlobalPosition += new Vector3(delta.X, 0f, delta.Z);
        ClampVisibleAreaToMap();
    }

    public Vector3 GetGroundCenter()
    {
        EnsureCameraConfigured();
        var viewport = camera.GetViewport();
        if (viewport is not null)
        {
            var center = viewport.GetVisibleRect().Size * 0.5f;
            if (TryProjectScreenPointToGround(center, out var point))
                return point;
        }

        return GlobalPosition;
    }

    readonly Vector3[] cachedFrustumPolygon = new Vector3[4];

    public bool TryGetViewportGroundPolygon(out Vector3[] polygon)
    {
        EnsureCameraConfigured();
        var viewport = camera.GetViewport();
        if (viewport is null)
        {
            polygon = System.Array.Empty<Vector3>();
            return false;
        }

        var rect = viewport.GetVisibleRect();
        var samples = new[]
        {
            rect.Position,
            rect.Position + new Vector2(rect.Size.X, 0f),
            rect.Position + rect.Size,
            rect.Position + new Vector2(0f, rect.Size.Y)
        };
        for (var i = 0; i < 4; i++)
        {
            if (!TryProjectScreenPointToGround(samples[i], out cachedFrustumPolygon[i]))
            {
                polygon = System.Array.Empty<Vector3>();
                return false;
            }
        }

        polygon = cachedFrustumPolygon;
        return true;
    }

    public bool TryProjectScreenPointToGround(Vector2 screenPosition, out Vector3 point)
    {
        var origin = camera.ProjectRayOrigin(screenPosition);
        var direction = camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < 0.001f)
        {
            point = Vector3.Zero;
            return false;
        }

        var t = -origin.Y / direction.Y;
        if (t <= 0f)
        {
            point = Vector3.Zero;
            return false;
        }

        point = origin + direction * t;
        return true;
    }

    void ClampVisibleAreaToMap()
    {
        if (CommandLineArgs.Get().Any(arg => arg.Contains("capture")))
            return;

        var map = BattleMapCatalog.Get(BattleMapCatalog.RequestedMapName(GameState.Instance?.SelectedMapName));
        var halfSize = BattleMapCatalog.GetMapHalfSize(map);
        var limit = halfSize + 55f;

        if (!TryGetViewportGroundPolygon(out var polygon) || polygon.Length == 0)
        {
            var center = GetGroundCenter();
            var clampedX = Mathf.Clamp(center.X, -limit, limit);
            var clampedZ = Mathf.Clamp(center.Z, -limit, limit);
            var sX = clampedX - center.X;
            var sZ = clampedZ - center.Z;
            if (Mathf.Abs(sX) > 0.01f || Mathf.Abs(sZ) > 0.01f)
                GlobalPosition += new Vector3(sX, 0f, sZ);
            return;
        }

        float minX = polygon[0].X, maxX = polygon[0].X;
        float minZ = polygon[0].Z, maxZ = polygon[0].Z;
        for (int i = 1; i < polygon.Length; i++)
        {
            var p = polygon[i];
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Z < minZ) minZ = p.Z;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        var shiftX = 0f;
        if (minX < -limit)
            shiftX += -limit - minX;
        if (maxX + shiftX > limit)
            shiftX += limit - (maxX + shiftX);

        var shiftZ = 0f;
        if (minZ < -limit)
            shiftZ += -limit - minZ;
        if (maxZ + shiftZ > limit)
            shiftZ += limit - (maxZ + shiftZ);

        if (Mathf.Abs(shiftX) > 0.01f || Mathf.Abs(shiftZ) > 0.01f)
            GlobalPosition += new Vector3(shiftX, 0f, shiftZ);
    }
}
