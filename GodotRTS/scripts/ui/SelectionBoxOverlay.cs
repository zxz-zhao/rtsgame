using Godot;

public partial class SelectionBoxOverlay : Control
{
    PlayerController? controller;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        controller = GetTree().CurrentScene?.GetNodeOrNull<PlayerController>("PlayerController");
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        controller ??= GetTree().CurrentScene?.GetNodeOrNull<PlayerController>("PlayerController");
        if (controller is null || !controller.IsDraggingSelection)
            return;

        var start = controller.DragStart;
        var end = controller.DragEnd;
        var min = new Vector2(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        var max = new Vector2(Mathf.Max(start.X, end.X), Mathf.Max(start.Y, end.Y));
        var rect = new Rect2(min, max - min);
        DrawRect(rect, new Color(0.25f, 0.78f, 1f, 0.14f), true);
        DrawRect(rect, new Color(0.45f, 0.92f, 1f, 0.92f), false, 1.5f);
    }
}
