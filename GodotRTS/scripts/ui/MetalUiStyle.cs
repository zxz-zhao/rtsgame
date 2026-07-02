using Godot;

public static class MetalUiStyle
{
    public readonly struct MetalPalette
    {
        public readonly Color Fill;
        public readonly Color Border;
        public readonly Color Highlight;
        public readonly Color Shadow;
        public readonly Color Glow;

        public MetalPalette(Color fill, Color border, Color highlight, Color shadow, Color glow)
        {
            Fill = fill;
            Border = border;
            Highlight = highlight;
            Shadow = shadow;
            Glow = glow;
        }
    }

    public static readonly MetalPalette Gold = new(
        new Color(0.30f, 0.22f, 0.06f, 0.96f),
        new Color(0.96f, 0.79f, 0.30f, 0.92f),
        new Color(1f, 0.95f, 0.72f, 0.92f),
        new Color(0.17f, 0.11f, 0.03f, 0.92f),
        new Color(1f, 0.80f, 0.24f, 0.18f));

    public static readonly MetalPalette Steel = new(
        new Color(0.15f, 0.19f, 0.24f, 0.96f),
        new Color(0.66f, 0.78f, 0.88f, 0.84f),
        new Color(0.92f, 0.97f, 1f, 0.86f),
        new Color(0.05f, 0.08f, 0.11f, 0.90f),
        new Color(0.62f, 0.82f, 1f, 0.12f));

    public static readonly MetalPalette Green = new(
        new Color(0.12f, 0.22f, 0.14f, 0.96f),
        new Color(0.46f, 0.88f, 0.50f, 0.76f),
        new Color(0.86f, 1f, 0.88f, 0.86f),
        new Color(0.03f, 0.08f, 0.04f, 0.90f),
        new Color(0.46f, 0.96f, 0.58f, 0.12f));

    public static readonly MetalPalette BronzePanel = new(
        new Color(0.03f, 0.05f, 0.06f, 0.92f),
        new Color(0.84f, 0.67f, 0.28f, 0.78f),
        new Color(0.98f, 0.90f, 0.68f, 0.58f),
        new Color(0.02f, 0.03f, 0.03f, 0.92f),
        new Color(0.95f, 0.74f, 0.24f, 0.14f));

    public static StyleBoxFlat MakePanelStyle(MetalPalette palette, int borderWidth = 1, int shadowSize = 10, int cornerRadius = 4)
    {
        return new StyleBoxFlat
        {
            BgColor = palette.Fill,
            BorderColor = palette.Border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = palette.Glow,
            ShadowSize = shadowSize,
            ShadowOffset = Vector2.Zero
        };
    }

    public static StyleBoxFlat MakeButtonStyle(MetalPalette palette, float fillShift = 0f, float borderShift = 0f, int borderWidth = 1, int cornerRadius = 3)
    {
        var fill = fillShift >= 0f ? palette.Fill.Lightened(fillShift) : palette.Fill.Darkened(-fillShift);
        var border = borderShift >= 0f ? palette.Border.Lightened(borderShift) : palette.Border.Darkened(-borderShift);
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ContentMarginLeft = 10,
            ContentMarginTop = 6,
            ContentMarginRight = 10,
            ContentMarginBottom = 6
        };
    }

    public static void ApplyMetalButton(Button button, MetalPalette palette, int fontSize, bool emphasis = false)
    {
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", new Color(0.96f, 0.98f, 1f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", palette.Highlight);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.58f, 0.60f));
        button.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        button.AddThemeConstantOverride("outline_size", emphasis ? 2 : 1);

        var normal = MakeButtonStyle(palette);
        var hover = MakeButtonStyle(palette, 0.08f, 0.12f);
        hover.ShadowColor = palette.Glow;
        hover.ShadowSize = emphasis ? 10 : 6;
        hover.ShadowOffset = Vector2.Zero;

        var pressed = MakeButtonStyle(palette, -0.12f, -0.04f);
        pressed.ShadowColor = palette.Shadow;
        pressed.ShadowSize = 2;
        pressed.ShadowOffset = new Vector2(0f, 1f);

        var disabled = MakeButtonStyle(palette, -0.24f, -0.22f);

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", hover);
        button.AddThemeStyleboxOverride("disabled", disabled);
    }

    public static void ApplyMetalPanel(Panel panel, MetalPalette palette, int borderWidth = 1, int shadowSize = 10, int cornerRadius = 4)
    {
        panel.AddThemeStyleboxOverride("panel", MakePanelStyle(palette, borderWidth, shadowSize, cornerRadius));
        AddEdgeHighlights(panel, palette, cornerRadius);
    }

    static void AddEdgeHighlights(Control panel, MetalPalette palette, int cornerRadius)
    {
        RemoveExistingDecor(panel, "MetalEdgeTop");
        RemoveExistingDecor(panel, "MetalEdgeBottom");
        RemoveExistingDecor(panel, "MetalEdgeInner");

        var top = new ColorRect
        {
            Name = "MetalEdgeTop",
            Position = new Vector2(4f, 3f),
            Size = new Vector2(Mathf.Max(0f, panel.Size.X - 8f), 2f),
            Color = palette.Highlight,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(top);
        panel.MoveChild(top, 0);

        var bottom = new ColorRect
        {
            Name = "MetalEdgeBottom",
            Position = new Vector2(4f, Mathf.Max(0f, panel.Size.Y - 4f)),
            Size = new Vector2(Mathf.Max(0f, panel.Size.X - 8f), 1f),
            Color = new Color(palette.Shadow.R, palette.Shadow.G, palette.Shadow.B, 0.88f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddChild(bottom);
        panel.MoveChild(bottom, 0);

        var inner = new Panel
        {
            Name = "MetalEdgeInner",
            Position = new Vector2(3f, 3f),
            Size = new Vector2(Mathf.Max(0f, panel.Size.X - 6f), Mathf.Max(0f, panel.Size.Y - 6f)),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        inner.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(palette.Highlight.R, palette.Highlight.G, palette.Highlight.B, 0.18f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = Mathf.Max(0, cornerRadius - 1),
            CornerRadiusTopRight = Mathf.Max(0, cornerRadius - 1),
            CornerRadiusBottomLeft = Mathf.Max(0, cornerRadius - 1),
            CornerRadiusBottomRight = Mathf.Max(0, cornerRadius - 1)
        });
        panel.AddChild(inner);
        panel.MoveChild(inner, 0);
    }

    static void RemoveExistingDecor(Control panel, string name)
    {
        var child = panel.GetNodeOrNull<Node>(name);
        child?.QueueFree();
    }
}
