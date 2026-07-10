using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ProceduralMilitaryUiGraphic : MaskableGraphic
{
    public enum ShapeKind
    {
        LayeredLine,
        HexBadge,
        LevelBadge,
        ChamferedPanel,
        AvatarFrame,
        FriendRowFrame,
    }

    public ShapeKind shape = ShapeKind.ChamferedPanel;
    public bool drawFill = true;
    public float thickness = 5f;
    public float cornerCut = 10f;
    public Color fillColor = new Color(0.030f, 0.035f, 0.034f, 0.96f);
    public Color shadowColor = new Color(0.003f, 0.004f, 0.004f, 0.98f);
    public Color darkLineColor = new Color(0.060f, 0.072f, 0.070f, 1f);
    public Color brassColor = new Color(0.360f, 0.325f, 0.220f, 1f);
    public Color highlightColor = new Color(0.860f, 0.800f, 0.580f, 0.86f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0.01f || r.height <= 0.01f)
            return;

        switch (shape)
        {
            case ShapeKind.LayeredLine:
                DrawLayeredLine(vh, r);
                break;
            case ShapeKind.HexBadge:
                DrawHexBadge(vh, r);
                break;
            case ShapeKind.LevelBadge:
                DrawLevelBadge(vh, r);
                break;
            case ShapeKind.AvatarFrame:
                DrawAvatarFrame(vh, r);
                break;
            case ShapeKind.FriendRowFrame:
                DrawFriendRowFrame(vh, r);
                break;
            default:
                DrawChamferedPanel(vh, r, drawFill);
                break;
        }
    }

    void DrawLayeredLine(VertexHelper vh, Rect r)
    {
        Vector2 a = new Vector2(r.xMin, r.center.y);
        Vector2 b = new Vector2(r.xMax, r.center.y);
        AddLine(vh, a, b, thickness * 2.8f, shadowColor);
        AddLine(vh, a, b, thickness * 1.55f, darkLineColor);
        AddMetalLine(vh, a, b, thickness * 1.10f, brassColor, highlightColor);
        AddLine(vh, a + Vector2.up * thickness * 0.58f, b + Vector2.up * thickness * 0.58f, Mathf.Max(1f, thickness * 0.30f), WithAlpha(highlightColor, highlightColor.a * 0.72f));
        AddLine(vh, a - Vector2.up * thickness * 0.50f, b - Vector2.up * thickness * 0.50f, Mathf.Max(1f, thickness * 0.36f), new Color(0f, 0f, 0f, 0.62f));
        AddBrushedLineAccents(vh, a, b, thickness * 1.35f, brassColor, highlightColor, 17);
    }

    void DrawHexBadge(VertexHelper vh, Rect r)
    {
        Vector2 center = r.center;
        float rx = r.width * 0.50f;
        float ry = r.height * 0.48f;
        Vector2[] outer = Hex(center, rx, ry);
        Vector2[] mid = Hex(center, rx - thickness * 0.95f, ry - thickness * 0.95f);
        Vector2[] inner = Hex(center, rx - thickness * 1.95f, ry - thickness * 1.95f);

        AddRing(vh, outer, mid, shadowColor);
        AddMetalRing(vh, mid, inner, r, brassColor);
        AddPolygon(vh, inner, fillColor);

        Rect plate = new Rect(
            center.x - r.width * 0.255f,
            center.y - r.height * 0.205f,
            r.width * 0.510f,
            r.height * 0.410f);
        float plateCut = Mathf.Min(plate.width, plate.height) * 0.18f;
        Vector2[] plateOuter = ChamferRect(plate, plateCut);
        Vector2[] plateInner = ChamferRect(
            Inset(plate, Mathf.Max(1f, thickness * 0.38f)),
            Mathf.Max(1f, plateCut - thickness * 0.38f));
        AddMetalRing(vh, plateOuter, plateInner, plate, darkLineColor);
        AddPolygon(vh, plateInner, new Color(fillColor.r * 0.72f, fillColor.g * 0.75f, fillColor.b * 0.75f, fillColor.a));

        AddLine(vh, outer[0], outer[1], Mathf.Max(1f, thickness * 0.45f), highlightColor);
        AddLine(vh, outer[5], outer[0], Mathf.Max(1f, thickness * 0.35f), new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a * 0.62f));
        AddLine(vh, plateOuter[0], plateOuter[1], Mathf.Max(1f, thickness * 0.16f), new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a * 0.42f));
        AddMetalEdgeOverlay(vh, outer, r, Mathf.Max(1f, thickness * 0.42f), brassColor, highlightColor);
        AddMetalEdgeOverlay(vh, plateOuter, plate, Mathf.Max(1f, thickness * 0.24f), darkLineColor, highlightColor);
        AddBrushedMetalAccents(vh, outer, r, thickness, brassColor, highlightColor, 31);
        AddBrushedMetalAccents(vh, plateOuter, plate, thickness * 0.56f, darkLineColor, highlightColor, 43);
    }

    void DrawLevelBadge(VertexHelper vh, Rect r)
    {
        Vector2 center = r.center;
        float rx = r.width * 0.50f;
        float ry = r.height * 0.48f;
        Vector2[] outer = Hex(center, rx, ry);
        Vector2[] mid = Hex(center, rx - thickness * 0.92f, ry - thickness * 0.92f);
        Vector2[] inner = Hex(center, rx - thickness * 1.95f, ry - thickness * 1.95f);
        Vector2[] matte = Hex(center, rx - thickness * 2.55f, ry - thickness * 2.55f);

        AddRing(vh, outer, mid, shadowColor);
        AddMetalRing(vh, mid, inner, r, brassColor);
        AddPolygon(vh, inner, new Color(fillColor.r, fillColor.g, fillColor.b, 1f));
        AddPolygon(vh, matte, new Color(fillColor.r * 0.78f, fillColor.g * 0.80f, fillColor.b * 0.82f, 1f));

        AddLine(vh, outer[0], outer[1], Mathf.Max(1f, thickness * 0.34f), highlightColor);
        AddLine(vh, outer[5], outer[0], Mathf.Max(1f, thickness * 0.24f), WithAlpha(highlightColor, highlightColor.a * 0.54f));
        AddLine(vh, outer[3], outer[4], Mathf.Max(1f, thickness * 0.26f), new Color(0f, 0f, 0f, 0.40f));
        AddMetalEdgeOverlay(vh, outer, r, Mathf.Max(1f, thickness * 0.34f), brassColor, highlightColor);
        AddBrushedMetalAccents(vh, outer, r, thickness * 0.55f, brassColor, highlightColor, 149);
    }

    void DrawChamferedPanel(VertexHelper vh, Rect r, bool fill)
    {
        Vector2[] outer = ChamferRect(r, cornerCut);
        Vector2[] ringA = ChamferRect(Inset(r, thickness * 0.90f), Mathf.Max(1f, cornerCut - thickness * 0.90f));
        Vector2[] ringB = ChamferRect(Inset(r, thickness * 1.85f), Mathf.Max(1f, cornerCut - thickness * 1.85f));
        Vector2[] inner = ChamferRect(Inset(r, thickness * 3.00f), Mathf.Max(1f, cornerCut - thickness * 3.00f));

        AddRing(vh, outer, ringA, shadowColor);
        AddMetalRing(vh, ringA, ringB, r, brassColor);
        AddMetalRing(vh, ringB, inner, r, darkLineColor);
        if (fill)
            AddPolygon(vh, inner, fillColor);

        AddLine(vh, outer[0], outer[1], Mathf.Max(1f, thickness * 0.33f), highlightColor);
        AddLine(vh, outer[7], outer[0], Mathf.Max(1f, thickness * 0.28f), new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a * 0.52f));
        AddLine(vh, outer[4], outer[5], Mathf.Max(1f, thickness * 0.30f), new Color(0f, 0f, 0f, 0.42f));
        AddMetalEdgeOverlay(vh, outer, r, Mathf.Max(1f, thickness * 0.44f), brassColor, highlightColor);
        AddMetalEdgeOverlay(vh, inner, r, Mathf.Max(1f, thickness * 0.22f), darkLineColor, WithAlpha(highlightColor, highlightColor.a * 0.46f));
        AddBrushedMetalAccents(vh, outer, r, thickness, brassColor, highlightColor, 71);
        AddBrushedMetalAccents(vh, inner, r, thickness * 0.48f, darkLineColor, highlightColor, 89);
    }

    void DrawAvatarFrame(VertexHelper vh, Rect r)
    {
        Rect main = new Rect(r.xMin + r.width * 0.17f, r.yMin + r.height * 0.12f, r.width * 0.78f, r.height * 0.80f);
        DrawChamferFrameOnly(vh, main, Mathf.Max(5f, cornerCut * 0.55f), Mathf.Max(3f, thickness));

        Rect badge = new Rect(r.xMin + r.width * 0.02f, r.yMin + r.height * 0.02f, r.width * 0.42f, r.height * 0.34f);
        DrawHexFrameOnly(vh, badge, Mathf.Max(3f, thickness * 0.88f));

        Vector2 a = new Vector2(badge.xMax - thickness * 0.2f, badge.center.y + badge.height * 0.18f);
        Vector2 b = new Vector2(main.xMin + thickness * 0.5f, main.yMin + main.height * 0.20f);
        AddLine(vh, a, b, Mathf.Max(2f, thickness * 0.70f), shadowColor);
        AddMetalLine(vh, a, b, Mathf.Max(1f, thickness * 0.35f), brassColor, highlightColor);
    }

    void DrawFriendRowFrame(VertexHelper vh, Rect r)
    {
        DrawChamferedPanel(vh, r, true);

        float railX = r.xMin + r.width * 0.12f;
        AddLine(vh, new Vector2(railX, r.yMin + r.height * 0.14f), new Vector2(railX + r.width * 0.025f, r.yMax - r.height * 0.14f), thickness * 1.25f, darkLineColor);
        AddMetalLine(vh, new Vector2(railX + thickness, r.yMin + r.height * 0.18f), new Vector2(railX + r.width * 0.025f + thickness, r.yMax - r.height * 0.18f), Mathf.Max(1f, thickness * 0.55f), brassColor, highlightColor);

        float y = r.yMin + r.height * 0.20f;
        AddLine(vh, new Vector2(r.xMin + r.width * 0.30f, y), new Vector2(r.xMax - r.width * 0.08f, y), Mathf.Max(1f, thickness * 0.35f), new Color(brassColor.r, brassColor.g, brassColor.b, 0.38f));
    }

    void DrawChamferFrameOnly(VertexHelper vh, Rect r, float cut, float lineWidth)
    {
        Vector2[] outer = ChamferRect(r, cut);
        Vector2[] mid = ChamferRect(Inset(r, lineWidth * 0.80f), Mathf.Max(1f, cut - lineWidth * 0.80f));
        Vector2[] inner = ChamferRect(Inset(r, lineWidth * 1.75f), Mathf.Max(1f, cut - lineWidth * 1.75f));
        AddRing(vh, outer, mid, shadowColor);
        AddMetalRing(vh, mid, inner, r, brassColor);
        AddLine(vh, outer[0], outer[1], Mathf.Max(1f, lineWidth * 0.35f), highlightColor);
        AddMetalEdgeOverlay(vh, outer, r, Mathf.Max(1f, lineWidth * 0.30f), brassColor, highlightColor);
        AddBrushedMetalAccents(vh, outer, r, lineWidth * 0.82f, brassColor, highlightColor, 113);
    }

    void DrawHexFrameOnly(VertexHelper vh, Rect r, float lineWidth)
    {
        Vector2 center = r.center;
        Vector2[] outer = Hex(center, r.width * 0.50f, r.height * 0.48f);
        Vector2[] mid = Hex(center, r.width * 0.50f - lineWidth * 0.85f, r.height * 0.48f - lineWidth * 0.85f);
        Vector2[] inner = Hex(center, r.width * 0.50f - lineWidth * 1.70f, r.height * 0.48f - lineWidth * 1.70f);
        AddRing(vh, outer, mid, shadowColor);
        AddMetalRing(vh, mid, inner, r, brassColor);
        AddLine(vh, outer[0], outer[1], Mathf.Max(1f, lineWidth * 0.28f), highlightColor);
        AddMetalEdgeOverlay(vh, outer, r, Mathf.Max(1f, lineWidth * 0.26f), brassColor, highlightColor);
        AddBrushedMetalAccents(vh, outer, r, lineWidth * 0.72f, brassColor, highlightColor, 127);
    }

    static Rect Inset(Rect r, float amount)
    {
        return new Rect(r.xMin + amount, r.yMin + amount, Mathf.Max(1f, r.width - amount * 2f), Mathf.Max(1f, r.height - amount * 2f));
    }

    static Vector2[] Hex(Vector2 c, float rx, float ry)
    {
        rx = Mathf.Max(1f, rx);
        ry = Mathf.Max(1f, ry);
        return new[]
        {
            c + new Vector2(-rx * 0.58f, ry),
            c + new Vector2(rx * 0.58f, ry),
            c + new Vector2(rx, 0f),
            c + new Vector2(rx * 0.58f, -ry),
            c + new Vector2(-rx * 0.58f, -ry),
            c + new Vector2(-rx, 0f),
        };
    }

    static Vector2[] ChamferRect(Rect r, float cut)
    {
        cut = Mathf.Clamp(cut, 0f, Mathf.Min(r.width, r.height) * 0.46f);
        return new[]
        {
            new Vector2(r.xMin + cut, r.yMax),
            new Vector2(r.xMax - cut, r.yMax),
            new Vector2(r.xMax, r.yMax - cut),
            new Vector2(r.xMax, r.yMin + cut),
            new Vector2(r.xMax - cut, r.yMin),
            new Vector2(r.xMin + cut, r.yMin),
            new Vector2(r.xMin, r.yMin + cut),
            new Vector2(r.xMin, r.yMax - cut),
        };
    }

    static void AddPolygon(VertexHelper vh, Vector2[] points, Color color)
    {
        if (points == null || points.Length < 3 || color.a <= 0f)
            return;

        int start = vh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
            vh.AddVert(points[i], color, Vector2.zero);

        for (int i = 1; i < points.Length - 1; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    static void AddRing(VertexHelper vh, Vector2[] outer, Vector2[] inner, Color color)
    {
        if (outer == null || inner == null || outer.Length != inner.Length || color.a <= 0f)
            return;

        for (int i = 0; i < outer.Length; i++)
        {
            int j = (i + 1) % outer.Length;
            AddQuad(vh, outer[i], outer[j], inner[j], inner[i], color);
        }
    }

    static void AddMetalRing(VertexHelper vh, Vector2[] outer, Vector2[] inner, Rect bounds, Color baseColor)
    {
        if (outer == null || inner == null || outer.Length != inner.Length || baseColor.a <= 0f)
            return;

        for (int i = 0; i < outer.Length; i++)
        {
            int j = (i + 1) % outer.Length;
            AddQuad(
                vh,
                outer[i],
                outer[j],
                inner[j],
                inner[i],
                MetalColor(baseColor, outer[i], bounds),
                MetalColor(baseColor, outer[j], bounds),
                MetalColor(baseColor, inner[j], bounds),
                MetalColor(baseColor, inner[i], bounds));
        }
    }

    static void AddMetalEdgeOverlay(VertexHelper vh, Vector2[] points, Rect bounds, float width, Color baseColor, Color highlight)
    {
        if (points == null || points.Length < 2 || width <= 0.01f)
            return;

        Vector2 center = bounds.center;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            Vector2 mid = (a + b) * 0.5f;
            float yLight = Mathf.InverseLerp(bounds.yMin, bounds.yMax, mid.y);
            float xLight = 1f - Mathf.InverseLerp(bounds.xMin, bounds.xMax, mid.x);
            float light = Mathf.Clamp01(yLight * 0.72f + xLight * 0.28f);

            Color edgeBase = Color.Lerp(ScaleColor(baseColor, 0.48f), ScaleColor(baseColor, 1.42f), light);
            Color edgeHigh = Color.Lerp(ScaleColor(highlight, 0.48f), highlight, Mathf.Clamp01(light * 1.25f));
            AddMetalLine(vh, a, b, width, edgeBase, edgeHigh);

            if (light > 0.58f)
            {
                Vector2 glintA = Vector2.Lerp(a, b, 0.10f);
                Vector2 glintB = Vector2.Lerp(a, b, Mathf.Lerp(0.38f, 0.74f, light));
                AddLine(vh, glintA, glintB, Mathf.Max(1f, width * 0.34f), WithAlpha(edgeHigh, edgeHigh.a * 0.72f));
            }

            if (mid.y < center.y)
                AddLine(vh, a, b, Mathf.Max(1f, width * 0.34f), new Color(0f, 0f, 0f, 0.42f));
        }
    }

    static void AddBrushedMetalAccents(VertexHelper vh, Vector2[] points, Rect bounds, float width, Color baseColor, Color highlight, int seed)
    {
        if (points == null || points.Length < 2 || width <= 0.01f)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Length];
            float length = Vector2.Distance(a, b);
            if (length < 14f)
                continue;

            int count = Mathf.Clamp(Mathf.RoundToInt(length / 95f) + 1, 1, 6);
            for (int k = 0; k < count; k++)
            {
                float h = Hash01(seed + i * 37 + k * 101);
                float t0 = Mathf.Lerp(0.06f, 0.86f, h);
                float span = Mathf.Lerp(0.055f, 0.18f, Hash01(seed + i * 41 + k * 73));
                float t1 = Mathf.Min(0.96f, t0 + span);
                Vector2 p0 = Vector2.Lerp(a, b, t0);
                Vector2 p1 = Vector2.Lerp(a, b, t1);
                Vector2 dir = (b - a).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x);

                float side = Hash01(seed + i * 59 + k * 17) > 0.48f ? 1f : -1f;
                float offset = Mathf.Lerp(width * 0.10f, width * 0.48f, Hash01(seed + i * 83 + k * 29)) * side;
                p0 += n * offset;
                p1 += n * offset;

                float yLight = Mathf.InverseLerp(bounds.yMin, bounds.yMax, (p0.y + p1.y) * 0.5f);
                Color scratch = Color.Lerp(
                    WithAlpha(new Color(0f, 0f, 0f, 1f), 0.28f),
                    WithAlpha(highlight, Mathf.Lerp(0.30f, 0.86f, yLight)),
                    Hash01(seed + i * 97 + k * 53));
                if (Hash01(seed + i * 131 + k * 7) > 0.72f)
                    scratch = WithAlpha(new Color(0.55f, 0.70f, 0.66f, 1f), 0.42f);

                AddLine(vh, p0, p1, Mathf.Max(1f, width * Mathf.Lerp(0.10f, 0.22f, h)), scratch);
            }
        }
    }

    static void AddBrushedLineAccents(VertexHelper vh, Vector2 a, Vector2 b, float width, Color baseColor, Color highlight, int seed)
    {
        float length = Vector2.Distance(a, b);
        int count = Mathf.Clamp(Mathf.RoundToInt(length / 130f) + 2, 2, 10);
        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x);
        for (int i = 0; i < count; i++)
        {
            float h = Hash01(seed + i * 67);
            float t0 = Mathf.Lerp(0.04f, 0.86f, h);
            float t1 = Mathf.Min(0.97f, t0 + Mathf.Lerp(0.045f, 0.16f, Hash01(seed + i * 31)));
            float offset = Mathf.Lerp(-width * 0.36f, width * 0.42f, Hash01(seed + i * 43));
            Vector2 p0 = Vector2.Lerp(a, b, t0) + n * offset;
            Vector2 p1 = Vector2.Lerp(a, b, t1) + n * offset;
            Color color = Hash01(seed + i * 19) > 0.35f
                ? WithAlpha(highlight, Mathf.Lerp(0.32f, 0.82f, Hash01(seed + i * 11)))
                : WithAlpha(new Color(0f, 0f, 0f, 1f), 0.30f);
            AddLine(vh, p0, p1, Mathf.Max(1f, width * 0.12f), color);
        }
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        if (width <= 0.01f || color.a <= 0f)
            return;

        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
        AddQuad(vh, a - n, a + n, b + n, b - n, color);
    }

    static void AddMetalLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color baseColor, Color highlight)
    {
        if (width <= 0.01f || baseColor.a <= 0f)
            return;

        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
        Rect bounds = BoundsOf(a - n, a + n, b + n, b - n);
        Color lower = ScaleColor(MetalColor(baseColor, a - n, bounds), 0.58f);
        Color upper = MetalColor(highlight, a + n, bounds);
        AddQuad(
            vh,
            a - n,
            a + n,
            b + n,
            b - n,
            lower,
            upper,
            MetalColor(highlight, b + n, bounds),
            ScaleColor(MetalColor(baseColor, b - n, bounds), 0.58f));

        AddLine(vh, a + n * 0.62f, b + n * 0.62f, Mathf.Max(1f, width * 0.22f), WithAlpha(highlight, highlight.a * 0.78f));
        AddLine(vh, a + n * 0.16f, b + n * 0.16f, Mathf.Max(1f, width * 0.34f), ScaleColor(baseColor, 1.20f));
        AddLine(vh, a - n * 0.62f, b - n * 0.62f, Mathf.Max(1f, width * 0.26f), new Color(0f, 0f, 0f, 0.58f));
    }

    static Rect BoundsOf(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    static Color MetalColor(Color baseColor, Vector2 p, Rect bounds)
    {
        float x = Mathf.InverseLerp(bounds.xMin, bounds.xMax, p.x);
        float y = Mathf.InverseLerp(bounds.yMin, bounds.yMax, p.y);
        float directional = Mathf.Clamp01(y * 0.72f + (1f - x) * 0.28f);
        float topGlint = Mathf.Clamp01(1f - Mathf.Abs(y - 0.86f) / 0.085f);
        float lowerDirt = Mathf.Clamp01(1f - y);
        float shade = Mathf.Lerp(0.34f, 1.72f, directional) + topGlint * 0.58f - lowerDirt * 0.16f;

        return new Color(
            Mathf.Clamp01(baseColor.r * shade + topGlint * 0.090f),
            Mathf.Clamp01(baseColor.g * shade + topGlint * 0.080f),
            Mathf.Clamp01(baseColor.b * shade + topGlint * 0.050f),
            baseColor.a);
    }

    static Color ScaleColor(Color color, float scale)
    {
        return new Color(
            Mathf.Clamp01(color.r * scale),
            Mathf.Clamp01(color.g * scale),
            Mathf.Clamp01(color.b * scale),
            color.a);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    static float Hash01(int n)
    {
        unchecked
        {
            uint x = (uint)n;
            x ^= x >> 16;
            x *= 0x7feb352dU;
            x ^= x >> 15;
            x *= 0x846ca68bU;
            x ^= x >> 16;
            return (x & 0x00FFFFFF) / 16777215f;
        }
    }

    static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colorA, Color colorB, Color colorC, Color colorD)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, colorA, Vector2.zero);
        vh.AddVert(b, colorB, Vector2.zero);
        vh.AddVert(c, colorC, Vector2.zero);
        vh.AddVert(d, colorD, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
