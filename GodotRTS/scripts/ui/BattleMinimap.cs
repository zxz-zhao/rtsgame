using Godot;
using System.Linq;

public partial class BattleMinimap : Control
{
    const float CameraMarkerHitPadding = 6f;

    BattleMapDefinition map = BattleMapCatalog.Get(BattleMapCatalog.DefaultMapName);
    RtsCamera? rtsCamera;
    bool draggingCameraMarker;
    Vector2 cameraMarkerDragOffset;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        map = BattleMapCatalog.Get(GameState.Instance?.SelectedMapName);
        rtsCamera = GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig");
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                    BeginPointerDrag(LocalPointerPosition(evt));
                else
                    EndPointerDrag();
            }
            ConsumeEvent();
            return;
        }

        if (evt is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            UpdatePointerDrag(LocalPointerPosition(evt));
            ConsumeEvent();
            return;
        }

        if (evt is InputEventScreenTouch touch)
        {
            if (touch.Pressed)
                BeginPointerDrag(LocalPointerPosition(evt));
            else
                EndPointerDrag();
            ConsumeEvent();
            return;
        }

        if (evt is InputEventScreenDrag)
        {
            UpdatePointerDrag(LocalPointerPosition(evt));
            ConsumeEvent();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, new Color(0.015f, 0.024f, 0.025f, 0.92f), true);
        DrawRect(rect, new Color(0.86f, 0.70f, 0.36f, 0.92f), false, 2f);

        DrawMapLayer(map.Patches.Select(p => new TerrainStripSpec(p.Name, p.Center, p.Size, p.Angle)), map.PatchAColor, 0.56f);
        DrawMapLayer(map.Waters, map.WaterColor, 0.88f);
        DrawMapLayer(map.Roads, map.RoadColor, 0.78f);
        DrawMapLayer(map.Patches.Where(p => p.PaletteIndex is 2 or 3)
            .Select(p => new TerrainStripSpec(p.Name, p.Center, p.Size, p.Angle)), new Color(0.48f, 0.52f, 0.50f), 0.62f);

        DrawFogOverlay();
        DrawCombatants();
        DrawCameraMarker();
    }

    void DrawMapLayer(System.Collections.Generic.IEnumerable<TerrainStripSpec> strips, Color color, float alpha)
    {
        foreach (var strip in strips)
        {
            var points = RotatedRect(strip.Center, strip.Size, strip.Angle);
            DrawColoredPolygon(points, WithAlpha(color, alpha));
        }
    }

    void DrawFogOverlay()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0f, 0f, 0f, 0.22f), true);

        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        foreach (var building in manager.GetBuildings(true))
        {
            if (building.IsRuined || building.IsRebuilding)
                continue;

            var radius = building.IsMainBase ? 34f : 20f;
            DrawCircle(WorldToMap(building.GlobalPosition), WorldRadius(radius), new Color(0.56f, 0.75f, 0.70f, 0.15f));
        }

        foreach (var unit in manager.GetUnits(true))
            DrawCircle(WorldToMap(unit.GlobalPosition), WorldRadius(18f), new Color(0.56f, 0.75f, 0.70f, 0.12f));
    }

    void DrawCombatants()
    {
        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        foreach (var building in manager.GetBuildings())
        {
            if (!building.PlayerOwned && !building.FogRevealed)
                continue;
            var color = building.PlayerOwned
                ? new Color(0.28f, 0.78f, 1f, 0.98f)
                : new Color(1f, 0.30f, 0.22f, 0.98f);
            var center = WorldToMap(building.GlobalPosition);
            DrawRect(new Rect2(center - new Vector2(4f, 4f), new Vector2(8f, 8f)), color, true);
        }

        foreach (var unit in manager.GetUnits())
        {
            if (!unit.PlayerOwned && !unit.FogRevealed)
                continue;
            var color = unit.PlayerOwned
                ? new Color(0.46f, 0.95f, 0.70f, 1f)
                : new Color(1f, 0.55f, 0.28f, 1f);
            DrawCircle(WorldToMap(unit.GlobalPosition), unit.PlayerOwned ? 2.8f : 2.5f, color);
        }
    }

    void DrawCameraMarker()
    {
        if (!TryGetCameraMarkerBounds(out var bounds))
            return;

        var drawRect = bounds.Intersection(new Rect2(Vector2.Zero, Size));
        if (drawRect.Size.X <= 1f || drawRect.Size.Y <= 1f)
            return;

        var color = new Color(1f, 0.92f, 0.34f, 0.95f);
        DrawRect(drawRect, new Color(color.R, color.G, color.B, 0.08f), true);
        DrawRect(drawRect, color, false, 1.6f);
    }

    Vector2[] RotatedRect(Vector3 center, Vector2 size, float angleDeg)
    {
        var halfX = size.X * 0.5f;
        var halfZ = size.Y * 0.5f;
        var angle = Mathf.DegToRad(angleDeg);
        var corners = new[]
        {
            new Vector3(-halfX, 0f, -halfZ),
            new Vector3( halfX, 0f, -halfZ),
            new Vector3( halfX, 0f,  halfZ),
            new Vector3(-halfX, 0f,  halfZ)
        };

        for (var i = 0; i < corners.Length; i++)
            corners[i] = center + corners[i].Rotated(Vector3.Up, angle);

        return corners.Select(WorldToMap).ToArray();
    }

    Vector2 WorldToMap(Vector3 world)
    {
        var mapHalfSize = CurrentMapHalfSize();
        var nx = Mathf.Clamp(0.5f - world.X / (mapHalfSize * 2f), 0f, 1f);
        var ny = Mathf.Clamp(0.5f - world.Z / (mapHalfSize * 2f), 0f, 1f);
        return new Vector2(nx * Size.X, ny * Size.Y);
    }

    Vector3 MapToWorld(Vector2 local)
    {
        var mapHalfSize = CurrentMapHalfSize();
        var nx = Mathf.Clamp(local.X / Mathf.Max(1f, Size.X), 0f, 1f);
        var ny = Mathf.Clamp(local.Y / Mathf.Max(1f, Size.Y), 0f, 1f);
        return new Vector3((0.5f - nx) * mapHalfSize * 2f, 0f, (0.5f - ny) * mapHalfSize * 2f);
    }

    Vector2 LocalPointerPosition(InputEvent evt)
    {
        var local = evt switch
        {
            InputEventMouseButton button => button.Position,
            InputEventMouseMotion motion => motion.Position,
            InputEventScreenTouch touch => GetGlobalTransformWithCanvas().AffineInverse() * touch.Position,
            InputEventScreenDrag drag => GetGlobalTransformWithCanvas().AffineInverse() * drag.Position,
            _ => GetGlobalTransformWithCanvas().AffineInverse() * GetViewport().GetMousePosition()
        };
        return ClampLocal(local);
    }

    Vector2 ClampLocal(Vector2 local)
    {
        return new Vector2(
            Mathf.Clamp(local.X, 0f, Mathf.Max(1f, Size.X)),
            Mathf.Clamp(local.Y, 0f, Mathf.Max(1f, Size.Y)));
    }

    void ConsumeEvent()
    {
        AcceptEvent();
        GetViewport().SetInputAsHandled();
    }

    void MoveCameraTo(Vector2 localPosition)
    {
        var world = MapToWorld(ClampLocal(localPosition));
        rtsCamera ??= GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig");
        rtsCamera?.JumpTo(world);
    }

    void BeginPointerDrag(Vector2 localPosition)
    {
        var pointer = ClampLocal(localPosition);
        if (TryGetCameraMarkerBounds(out var bounds) && bounds.Grow(CameraMarkerHitPadding).HasPoint(pointer))
        {
            draggingCameraMarker = true;
            cameraMarkerDragOffset = pointer - bounds.GetCenter();
            return;
        }

        draggingCameraMarker = false;
        cameraMarkerDragOffset = Vector2.Zero;
        MoveCameraTo(pointer);
    }

    void UpdatePointerDrag(Vector2 localPosition)
    {
        var pointer = ClampLocal(localPosition);
        if (!draggingCameraMarker)
        {
            MoveCameraTo(pointer);
            return;
        }

        MoveCameraTo(pointer - cameraMarkerDragOffset);
    }

    void EndPointerDrag()
    {
        draggingCameraMarker = false;
        cameraMarkerDragOffset = Vector2.Zero;
    }

    float WorldRadius(float radius)
        => radius / (CurrentMapHalfSize() * 2f) * Mathf.Min(Size.X, Size.Y);

    float CurrentMapHalfSize()
        => Mathf.Max(1f, BattleMapCatalog.GetMapHalfSize(map));

    bool TryGetCameraMarkerPolygon(out Vector2[] polygon)
    {
        rtsCamera ??= GetTree().CurrentScene?.GetNodeOrNull<RtsCamera>("CameraRig");
        if (rtsCamera is null || !rtsCamera.TryGetViewportGroundPolygon(out var worldPolygon) || worldPolygon.Length == 0)
        {
            polygon = System.Array.Empty<Vector2>();
            return false;
        }

        polygon = worldPolygon.Select(WorldToMap).ToArray();
        return polygon.Length > 0;
    }

    bool TryGetCameraMarkerBounds(out Rect2 bounds)
    {
        if (!TryGetCameraMarkerPolygon(out var polygon) || polygon.Length == 0)
        {
            bounds = new Rect2();
            return false;
        }

        var minX = polygon.Min(point => point.X);
        var maxX = polygon.Max(point => point.X);
        var minY = polygon.Min(point => point.Y);
        var maxY = polygon.Max(point => point.Y);
        bounds = new Rect2(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));
        return true;
    }

    static Color WithAlpha(Color color, float alpha)
        => new(color.R, color.G, color.B, alpha);
}
