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

    Camera3D camera = null!;
    bool dragging;
    readonly Dictionary<int, Vector2> touches = new();
    bool touchGestureActive;
    float lastPinchDistance;
    Vector2 lastTouchCenter;
    ulong suppressMouseUntilMs;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");
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
        if (viewport is not null && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = viewport.GetMousePosition();
            var windowSize = viewport.GetVisibleRect().Size;
            const float edgeMargin = 12f; // 敏感判定宽度调回 12 像素，防止日常鼠标操作误触滚动

            // 仅在鼠标位于窗口内部且贴近边缘时生效，防止鼠标移出窗口后镜头疯狂滚动
            if (mousePos.X >= 0f && mousePos.X <= windowSize.X && mousePos.Y >= 0f && mousePos.Y <= windowSize.Y)
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

        if (evt is InputEventMouseButton button)
        {
            if (IsPointerOverBlockingHud(button.Position))
                return;

            if (button.ButtonIndex == MouseButton.Middle)
                dragging = button.Pressed;
            else if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
                AdjustZoom(-ZoomSpeed);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
                AdjustZoom(ZoomSpeed);
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
            touches[touch.Index] = touch.Position;
        else
            touches.Remove(touch.Index);

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
        if (touches.Count < 2)
            return;

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

        AdjustZoom((lastPinchDistance - distance) * 0.035f);
        lastTouchCenter = center;
        lastPinchDistance = distance;
        GetViewport().SetInputAsHandled();
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

    void AdjustZoom(float amount)
    {
        var p = camera.Position;
        p.Y = Mathf.Clamp(p.Y + amount, MinHeight, MaxHeight);
        p.Z = Mathf.Clamp(p.Z + amount, MinHeight, MaxHeight);
        camera.Position = p;
        ClampVisibleAreaToMap();
    }

    bool IsPointerOverBlockingHud(Vector2 screenPosition)
    {
        return GetTree().GetFirstNodeInGroup("battle_hud") is BattleHud hud
            && GodotObject.IsInstanceValid(hud)
            && hud.IsPointerOverBlockingHud(screenPosition);
    }

    public void JumpTo(Vector3 worldPosition)
    {
        var center = GetGroundCenter();
        var delta = worldPosition - center;
        GlobalPosition += new Vector3(delta.X, 0f, delta.Z);
        ClampVisibleAreaToMap();
    }

    public Vector3 GetGroundCenter()
    {
        var viewport = camera.GetViewport();
        if (viewport is not null)
        {
            var center = viewport.GetVisibleRect().Size * 0.5f;
            if (TryProjectScreenPointToGround(center, out var point))
                return point;
        }

        return GlobalPosition;
    }

    public bool TryGetViewportGroundPolygon(out Vector3[] polygon)
    {
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
        polygon = new Vector3[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            if (!TryProjectScreenPointToGround(samples[i], out polygon[i]))
            {
                polygon = System.Array.Empty<Vector3>();
                return false;
            }
        }

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
        if (!TryGetViewportGroundPolygon(out var polygon) || polygon.Length == 0)
            return;

        var map = BattleMapCatalog.Get(BattleMapCatalog.RequestedMapName(GameState.Instance?.SelectedMapName));
        var limit = BattleMapCatalog.GetMapHalfSize(map);
        var minX = polygon.Min(point => point.X);
        var maxX = polygon.Max(point => point.X);
        var minZ = polygon.Min(point => point.Z);
        var maxZ = polygon.Max(point => point.Z);

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
