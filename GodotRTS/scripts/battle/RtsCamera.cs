using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RtsCamera : Node3D
{
    [Export] public float PanSpeed { get; set; } = 24f;
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
        if (dir != Vector3.Zero)
            GlobalPosition += dir.Normalized() * PanSpeed * (float)delta;
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
            if (button.ButtonIndex == MouseButton.Middle)
                dragging = button.Pressed;
            else if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
                AdjustZoom(-ZoomSpeed);
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
                AdjustZoom(ZoomSpeed);
        }
        else if (evt is InputEventMouseMotion motion && dragging)
        {
            GlobalPosition = new Vector3(
                GlobalPosition.X - motion.Relative.X * DragPanSensitivity,
                GlobalPosition.Y,
                GlobalPosition.Z - motion.Relative.Y * DragPanSensitivity);
        }
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
        GlobalPosition = new Vector3(
            GlobalPosition.X - centerDelta.X * DragPanSensitivity * 1.15f,
            GlobalPosition.Y,
            GlobalPosition.Z - centerDelta.Y * DragPanSensitivity * 1.15f);

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
    }

    public void JumpTo(Vector3 worldPosition)
    {
        GlobalPosition = new Vector3(worldPosition.X, GlobalPosition.Y, worldPosition.Z);
    }

    public Vector3 GetGroundCenter()
    {
        var origin = camera.GlobalPosition;
        var direction = -camera.GlobalBasis.Z;
        if (Mathf.Abs(direction.Y) < 0.001f)
            return GlobalPosition;

        var t = -origin.Y / direction.Y;
        if (t <= 0f)
            return GlobalPosition;
        return origin + direction * t;
    }
}
