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
        var size = max - min;
        if (size.X < 2f || size.Y < 2f)
            return;

        var rect = new Rect2(min, size);

        // 1. 战术全息半透明底色
        DrawRect(rect, new Color(0.10f, 0.55f, 0.88f, 0.12f), true);

        // 2. 超细精细边框线
        DrawRect(rect, new Color(0.30f, 0.82f, 1.0f, 0.50f), false, 1.0f);

        // 3. 四角高科技 L 型战术折角瞄准准星
        float cornerLen = Mathf.Clamp(Mathf.Min(size.X, size.Y) * 0.22f, 8f, 22f);
        var cornerColor = new Color(0.60f, 1.0f, 1.0f, 0.95f);
        const float cornerWidth = 2.5f;

        // 左上角
        DrawLine(min, new Vector2(min.X + cornerLen, min.Y), cornerColor, cornerWidth);
        DrawLine(min, new Vector2(min.X, min.Y + cornerLen), cornerColor, cornerWidth);

        // 右上角
        DrawLine(new Vector2(max.X - cornerLen, min.Y), new Vector2(max.X, min.Y), cornerColor, cornerWidth);
        DrawLine(new Vector2(max.X, min.Y), new Vector2(max.X, min.Y + cornerLen), cornerColor, cornerWidth);

        // 左下角
        DrawLine(new Vector2(min.X, max.Y), new Vector2(min.X + cornerLen, max.Y), cornerColor, cornerWidth);
        DrawLine(new Vector2(min.X, max.Y - cornerLen), new Vector2(min.X, max.Y), cornerColor, cornerWidth);

        // 右下角
        DrawLine(new Vector2(max.X - cornerLen, max.Y), max, cornerColor, cornerWidth);
        DrawLine(new Vector2(max.X, max.Y - cornerLen), max, cornerColor, cornerWidth);

        // 4. 当拉框尺寸较大时，在四边中心点绘制微型战术对齐刻度
        if (size.X >= 60f && size.Y >= 60f)
        {
            var tickColor = new Color(0.40f, 0.90f, 1.0f, 0.75f);
            const float tickWidth = 1.5f;
            float midX = (min.X + max.X) * 0.5f;
            float midY = (min.Y + max.Y) * 0.5f;

            // 上下中点刻度
            DrawLine(new Vector2(midX, min.Y - 3f), new Vector2(midX, min.Y + 3f), tickColor, tickWidth);
            DrawLine(new Vector2(midX, max.Y - 3f), new Vector2(midX, max.Y + 3f), tickColor, tickWidth);

            // 左右中点刻度
            DrawLine(new Vector2(min.X - 3f, midY), new Vector2(min.X + 3f, midY), tickColor, tickWidth);
            DrawLine(new Vector2(max.X - 3f, midY), new Vector2(max.X + 3f, midY), tickColor, tickWidth);
        }
    }
}
