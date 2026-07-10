using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ImageLayerExtractorTool;

internal enum ExtractionMode
{
    AutoForeground,
    FocusedRect,
}

internal enum BackgroundMode
{
    LightKey,
    CornerSample,
}

internal enum LayerVariant
{
    Full,
    Plate,
    Detail,
}

internal enum ExactPieceMask
{
    None,
    Rect,
    FriendAvatar,
    FriendAvatarFrame,
    FriendRowFrame,
    FriendPanelFrame,
    Mode,
    Panel,
}

internal readonly record struct RectInt(int X, int Y, int Width, int Height)
{
    public int XMax => X + Width;
    public int YMax => Y + Height;
    public Point Center => new(X + Width / 2, Y + Height / 2);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static RectInt FromMinMax(int minX, int minY, int maxXInclusive, int maxYInclusive)
        => new(minX, minY, maxXInclusive - minX + 1, maxYInclusive - minY + 1);
}

internal readonly record struct ComponentPixel(int X, int Y);
internal readonly record struct EdgeCandidate(int Position, float Score);
internal readonly record struct FocusedRectPreset(string Name, RectInt Rect, ExactPieceMask ExactMask, bool ClearMatchButtonBorderContent, RectInt? ExportTrim = null);

internal sealed class ExtractedElement
{
    public string Name { get; init; } = string.Empty;
    public RectInt TightBounds { get; init; }
    public RectInt ExportBounds { get; init; }
    public int PixelCount { get; init; }
    public int PlatePixelCount { get; init; }
    public int DetailPixelCount { get; init; }
    public Bitmap FullBitmap { get; init; } = default!;
    public Bitmap? PlateBitmap { get; init; }
    public Bitmap? DetailBitmap { get; init; }
}

internal sealed class ExtractionSettings
{
    public ExtractionMode Mode { get; set; } = ExtractionMode.FocusedRect;
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.LightKey;
    public int AlphaThreshold { get; set; } = 12;
    public int LightThreshold { get; set; } = 230;
    public int CornerTolerance { get; set; } = 42;
    public int ComponentPadding { get; set; } = 6;
    public int MinComponentPixels { get; set; } = 80;
    public int RecoverySeedTolerance { get; set; } = 72;
    public int RecoveryGrowTolerance { get; set; } = 30;
    public int BackgroundRejectTolerance { get; set; } = 18;
    public int PlateSeedTolerance { get; set; } = 52;
    public int PlateGrowTolerance { get; set; } = 24;
    public int MinDetailPixels { get; set; } = 24;
    public RectInt FocusRect { get; set; } = new(305, 150, 414, 148);
    public bool AutoSnapFocusedRect { get; set; }
    public int FocusedMaskMargin { get; set; } = 14;
    public int FocusedBackgroundColorTolerance { get; set; } = 26;
    public int FocusedBackgroundBrightnessTolerance { get; set; } = 38;
    public int FrameSearchMargin { get; set; } = 18;
    public int FrameDarkThreshold { get; set; } = 110;
    public float FrameCoverageThreshold { get; set; } = 0.68f;
    public int FrameSnapPadding { get; set; }
    public ExactPieceMask ExactMask { get; set; }
    public bool ClearMatchButtonBorderContent { get; set; }
    public string FocusedElementName { get; set; } = "Focused";
    public RectInt? ExactExportTrim { get; set; }
}

internal sealed class ImageExtractor
{
    readonly ExtractionSettings settings;

    public ImageExtractor(ExtractionSettings settings)
    {
        this.settings = settings;
    }

    public ExtractionResult Process(Bitmap source)
    {
        using Bitmap normalized = Ensure32bppArgb(source);
        var pixels = ReadPixels(normalized);
        int width = normalized.Width;
        int height = normalized.Height;

        if (settings.Mode == ExtractionMode.FocusedRect)
        {
            if (!TryBuildFocusedElement(pixels, width, height, settings.FocusRect, settings.FocusedElementName, out ExtractedElement? focusedElement, out RectInt snappedRect))
            {
                return new ExtractionResult(Array.Empty<ExtractedElement>(), null, "Focused extraction failed.");
            }

            return new ExtractionResult(new[] { focusedElement! }, CreateBitmapFromRect(pixels, width, snappedRect), "Focused extraction complete.");
        }

        bool[] backgroundMask = BuildBackgroundMask(pixels, width, height);
        Color32[] processedPixels = ApplyExternalMask(pixels, backgroundMask);

        Bitmap? composite = TryGetForegroundBounds(processedPixels, width, height, settings.AlphaThreshold, out RectInt compositeBounds)
            ? CreateBitmapFromRect(processedPixels, width, compositeBounds)
            : null;

        List<ExtractedElement> extracted = ExtractElements(processedPixels, width, height);
        string message = extracted.Count == 0
            ? "No foreground elements found."
            : $"Found {extracted.Count} element(s).";
        return new ExtractionResult(extracted, composite, message);
    }

    public static FocusedRectPreset[] GetFocusedRectPresets()
        => new[]
        {
            new FocusedRectPreset("ExactFriendPanel", new RectInt(8, 100, 256, 494), ExactPieceMask.Panel, false),
            new FocusedRectPreset("ExactFriendPanelFrame", new RectInt(8, 100, 256, 494), ExactPieceMask.FriendPanelFrame, false),
            new FocusedRectPreset("FriendRow01", new RectInt(20, 144, 236, 80), ExactPieceMask.Rect, false),
            new FocusedRectPreset("FriendRow02", new RectInt(20, 227, 236, 80), ExactPieceMask.Rect, false),
            new FocusedRectPreset("FriendRow03", new RectInt(20, 309, 236, 80), ExactPieceMask.Rect, false),
            new FocusedRectPreset("FriendRow04", new RectInt(20, 392, 236, 80), ExactPieceMask.Rect, false),
            new FocusedRectPreset("FriendRow05", new RectInt(20, 474, 236, 80), ExactPieceMask.Rect, false),
            new FocusedRectPreset("FriendRowFrame01", new RectInt(20, 144, 236, 80), ExactPieceMask.FriendRowFrame, false),
            new FocusedRectPreset("FriendRowFrame02", new RectInt(20, 227, 236, 80), ExactPieceMask.FriendRowFrame, false),
            new FocusedRectPreset("FriendRowFrame03", new RectInt(20, 309, 236, 80), ExactPieceMask.FriendRowFrame, false),
            new FocusedRectPreset("FriendRowFrame04", new RectInt(20, 392, 236, 80), ExactPieceMask.FriendRowFrame, false),
            new FocusedRectPreset("FriendRowFrame05", new RectInt(20, 474, 236, 80), ExactPieceMask.FriendRowFrame, false),
            new FocusedRectPreset("FriendAvatar01", new RectInt(36, 155, 54, 54), ExactPieceMask.FriendAvatar, false),
            new FocusedRectPreset("FriendAvatar02", new RectInt(36, 238, 54, 54), ExactPieceMask.FriendAvatar, false),
            new FocusedRectPreset("FriendAvatar03", new RectInt(36, 320, 54, 54), ExactPieceMask.FriendAvatar, false),
            new FocusedRectPreset("FriendAvatar04", new RectInt(36, 403, 54, 54), ExactPieceMask.FriendAvatar, false),
            new FocusedRectPreset("FriendAvatar05", new RectInt(36, 485, 54, 54), ExactPieceMask.FriendAvatar, false),
            new FocusedRectPreset("FriendAvatarFrame01", new RectInt(26, 150, 70, 68), ExactPieceMask.FriendAvatarFrame, false),
            new FocusedRectPreset("FriendAvatarFrame02", new RectInt(26, 233, 70, 68), ExactPieceMask.FriendAvatarFrame, false),
            new FocusedRectPreset("FriendAvatarFrame03", new RectInt(26, 315, 70, 68), ExactPieceMask.FriendAvatarFrame, false),
            new FocusedRectPreset("FriendAvatarFrame04", new RectInt(26, 398, 70, 68), ExactPieceMask.FriendAvatarFrame, false),
            new FocusedRectPreset("FriendAvatarFrame05", new RectInt(26, 480, 70, 68), ExactPieceMask.FriendAvatarFrame, false),
            new FocusedRectPreset("ExactClaimButton", new RectInt(922, 184, 78, 46), ExactPieceMask.Panel, false, new RectInt(5, 7, 66, 29)),
            new FocusedRectPreset("ExactMatchButtonBorder", new RectInt(306, 151, 412, 160), ExactPieceMask.Mode, false, new RectInt(3, 5, 406, 151)),
            new FocusedRectPreset("Match", new RectInt(306, 151, 412, 160), ExactPieceMask.Mode, false, new RectInt(3, 5, 406, 151)),
            new FocusedRectPreset("Custom", new RectInt(306, 307, 412, 150), ExactPieceMask.Mode, false, new RectInt(3, 5, 406, 132)),
            new FocusedRectPreset("Conquest", new RectInt(306, 451, 412, 150), ExactPieceMask.Mode, false, new RectInt(3, 5, 406, 132)),
        };

    static Bitmap Ensure32bppArgb(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format32bppArgb)
            return (Bitmap)source.Clone();

        Bitmap copy = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(copy);
        g.Clear(Color.Transparent);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }

    static Color32[] ReadPixels(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int absStride = Math.Abs(stride);
            byte[] bytes = new byte[absStride * bitmap.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            Color32[] pixels = new Color32[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                int srcY = stride > 0 ? y : bitmap.Height - 1 - y;
                int rowOffset = srcY * absStride;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int i = rowOffset + x * 4;
                    byte b = bytes[i + 0];
                    byte g = bytes[i + 1];
                    byte r = bytes[i + 2];
                    byte a = bytes[i + 3];
                    pixels[y * bitmap.Width + x] = new Color32(r, g, b, a);
                }
            }

            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    static Bitmap CreateBitmapFromRect(Color32[] pixels, int sourceWidth, RectInt bounds)
    {
        Color32[] output = CopyRectPixels(pixels, sourceWidth, bounds);
        return CreateBitmap(output, bounds.Width, bounds.Height);
    }

    static Bitmap CreateBitmap(Color32[] pixels, int width, int height)
    {
        Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            int absStride = Math.Abs(stride);
            byte[] bytes = new byte[absStride * height];
            for (int y = 0; y < height; y++)
            {
                int dstY = stride > 0 ? y : height - 1 - y;
                int rowOffset = dstY * absStride;
                for (int x = 0; x < width; x++)
                {
                    Color32 p = pixels[y * width + x];
                    int i = rowOffset + x * 4;
                    bytes[i + 0] = p.B;
                    bytes[i + 1] = p.G;
                    bytes[i + 2] = p.R;
                    bytes[i + 3] = p.A;
                }
            }

            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    bool[] BuildBackgroundMask(Color32[] pixels, int width, int height)
    {
        var background = new bool[width * height];
        var visited = new bool[width * height];
        var stack = new Stack<int>();
        Color32[] cornerSamples = GetCornerSamples(pixels, width, height);

        void TrySeed(int x, int y)
        {
            int index = y * width + x;
            if (visited[index] || !IsBackgroundPixel(pixels[index], cornerSamples))
                return;

            visited[index] = true;
            stack.Push(index);
        }

        for (int x = 0; x < width; x++)
        {
            TrySeed(x, 0);
            TrySeed(x, height - 1);
        }

        for (int y = 0; y < height; y++)
        {
            TrySeed(0, y);
            TrySeed(width - 1, y);
        }

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            background[index] = true;
            int x = index % width;
            int y = index / width;

            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
        }

        return background;

        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;
            if (visited[index] || !IsBackgroundPixel(pixels[index], cornerSamples))
                return;

            visited[index] = true;
            stack.Push(index);
        }
    }

    bool IsBackgroundPixel(Color32 pixel, Color32[] cornerSamples)
    {
        if (pixel.A <= settings.AlphaThreshold)
            return true;

        if (settings.BackgroundMode == BackgroundMode.LightKey)
            return pixel.R >= settings.LightThreshold && pixel.G >= settings.LightThreshold && pixel.B >= settings.LightThreshold;

        int limit = settings.CornerTolerance * settings.CornerTolerance;
        for (int i = 0; i < cornerSamples.Length; i++)
        {
            if (ColorDistanceSquared(pixel, cornerSamples[i]) <= limit)
                return true;
        }

        return false;
    }

    static Color32[] GetCornerSamples(Color32[] pixels, int width, int height)
    {
        return new[] { pixels[0], pixels[width - 1], pixels[(height - 1) * width], pixels[height * width - 1] };
    }

    Color32[] ApplyExternalMask(Color32[] sourcePixels, bool[] backgroundMask)
    {
        Color32[] output = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color32 pixel = sourcePixels[i];
            if (backgroundMask[i])
                pixel.A = 0;
            output[i] = pixel;
        }

        return output;
    }

    List<ExtractedElement> ExtractElements(Color32[] pixels, int width, int height)
    {
        List<ExtractedElement> results = new();
        bool[] visited = new bool[width * height];

        for (int index = 0; index < pixels.Length; index++)
        {
            if (visited[index] || pixels[index].A <= settings.AlphaThreshold)
                continue;

            List<ComponentPixel> componentPixels = CollectComponent(pixels, width, height, index, visited, out RectInt tightBounds);
            if (componentPixels.Count < settings.MinComponentPixels)
                continue;

            ExtractedElement element = BuildExtractedElement(results.Count + 1, pixels, width, height, componentPixels, tightBounds);
            results.Add(element);
        }

        results.Sort((a, b) =>
        {
            int yCompare = a.TightBounds.Y.CompareTo(b.TightBounds.Y);
            return yCompare != 0 ? yCompare : a.TightBounds.X.CompareTo(b.TightBounds.X);
        });

        return results;
    }

    bool TryBuildFocusedElement(Color32[] pixels, int width, int height, RectInt approxRect, string elementName, out ExtractedElement? element, out RectInt snappedRect)
    {
        element = null;
        RectInt clampedApprox = ClampRectToImage(approxRect, width, height);
        if (clampedApprox.Width < 8 || clampedApprox.Height < 8)
        {
            snappedRect = default;
            return false;
        }

        snappedRect = clampedApprox;
        if (settings.AutoSnapFocusedRect && !TrySnapFocusRectToFrame(pixels, width, height, clampedApprox, out snappedRect))
            return false;

        element = BuildMaskedFocusedElement(pixels, width, height, snappedRect, elementName);

        return element != null;
    }

    bool TrySnapFocusRectToFrame(Color32[] pixels, int width, int height, RectInt approxRect, out RectInt snappedRect)
    {
        RectInt searchRect = ExpandBounds(approxRect, width, height, settings.FrameSearchMargin);
        float bestScore = float.MinValue;
        snappedRect = default;

        List<EdgeCandidate> topCandidates = CollectHorizontalEdgeCandidates(pixels, width, searchRect, true, approxRect.Width);
        List<EdgeCandidate> bottomCandidates = CollectHorizontalEdgeCandidates(pixels, width, searchRect, false, approxRect.Width);
        List<EdgeCandidate> leftCandidates = CollectVerticalEdgeCandidates(pixels, width, searchRect, true, approxRect.Height);
        List<EdgeCandidate> rightCandidates = CollectVerticalEdgeCandidates(pixels, width, searchRect, false, approxRect.Height);

        for (int ti = 0; ti < topCandidates.Count; ti++)
        for (int bi = 0; bi < bottomCandidates.Count; bi++)
        {
            int top = Math.Min(topCandidates[ti].Position, bottomCandidates[bi].Position);
            int bottom = Math.Max(topCandidates[ti].Position, bottomCandidates[bi].Position);
            if (bottom - top + 1 < approxRect.Height * 0.75f)
                continue;

            for (int li = 0; li < leftCandidates.Count; li++)
            for (int ri = 0; ri < rightCandidates.Count; ri++)
            {
                int left = Math.Min(leftCandidates[li].Position, rightCandidates[ri].Position);
                int right = Math.Max(leftCandidates[li].Position, rightCandidates[ri].Position);
                RectInt candidate = RectInt.FromMinMax(left, top, right, bottom);
                if (!IsReasonableFocusedCandidate(candidate, approxRect))
                    continue;

                float score = ScoreFocusedFrame(pixels, width, candidate, approxRect);
                if (score > bestScore)
                {
                    bestScore = score;
                    snappedRect = candidate;
                }
            }
        }

        if (bestScore <= 0f)
            return false;

        snappedRect = ExpandBounds(snappedRect, width, height, settings.FrameSnapPadding);
        return true;
    }

    List<EdgeCandidate> CollectHorizontalEdgeCandidates(Color32[] pixels, int width, RectInt searchRect, bool fromTop, int approxWidth)
    {
        List<EdgeCandidate> candidates = new();
        int start = fromTop ? searchRect.Y : searchRect.YMax - 1;
        int end = fromTop ? searchRect.YMax : searchRect.Y - 1;
        int step = fromTop ? 1 : -1;

        for (int y = start; y != end; y += step)
        {
            int darkCount = 0;
            int sampleCount = 0;
            for (int x = searchRect.X; x < searchRect.XMax; x++)
            {
                sampleCount++;
                if (IsDarkFramePixel(pixels[y * width + x]))
                    darkCount++;
            }

            float coverage = sampleCount > 0 ? darkCount / (float)sampleCount : 0f;
            if (coverage >= settings.FrameCoverageThreshold * 0.85f)
                candidates.Add(new EdgeCandidate(y, coverage));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        TrimEdgeCandidates(candidates, 10, approxWidth / 6);
        return candidates;
    }

    List<EdgeCandidate> CollectVerticalEdgeCandidates(Color32[] pixels, int width, RectInt searchRect, bool fromLeft, int approxHeight)
    {
        List<EdgeCandidate> candidates = new();
        int start = fromLeft ? searchRect.X : searchRect.XMax - 1;
        int end = fromLeft ? searchRect.XMax : searchRect.X - 1;
        int step = fromLeft ? 1 : -1;

        for (int x = start; x != end; x += step)
        {
            int darkCount = 0;
            int sampleCount = 0;
            for (int y = searchRect.Y; y < searchRect.YMax; y++)
            {
                sampleCount++;
                if (IsDarkFramePixel(pixels[y * width + x]))
                    darkCount++;
            }

            float coverage = sampleCount > 0 ? darkCount / (float)sampleCount : 0f;
            if (coverage >= settings.FrameCoverageThreshold * 0.85f)
                candidates.Add(new EdgeCandidate(x, coverage));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        TrimEdgeCandidates(candidates, 10, approxHeight / 6);
        return candidates;
    }

    static void TrimEdgeCandidates(List<EdgeCandidate> candidates, int maxCount, int minSpacing)
    {
        if (candidates.Count <= maxCount)
            return;

        List<EdgeCandidate> filtered = new(maxCount);
        for (int i = 0; i < candidates.Count; i++)
        {
            bool tooClose = false;
            for (int j = 0; j < filtered.Count; j++)
            {
                if (Math.Abs(filtered[j].Position - candidates[i].Position) <= minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            filtered.Add(candidates[i]);
            if (filtered.Count >= maxCount)
                break;
        }

        candidates.Clear();
        candidates.AddRange(filtered);
    }

    bool IsReasonableFocusedCandidate(RectInt candidate, RectInt approxRect)
    {
        if (candidate.Width < approxRect.Width * 0.75f || candidate.Height < approxRect.Height * 0.75f)
            return false;

        if (candidate.Width > approxRect.Width + settings.FrameSearchMargin * 2 + 24)
            return false;

        if (candidate.Height > approxRect.Height + settings.FrameSearchMargin * 2 + 24)
            return false;

        return true;
    }

    float ScoreFocusedFrame(Color32[] pixels, int width, RectInt rect, RectInt targetRect)
    {
        int darkEdges = 0;
        int totalEdges = 0;
        int interiorBright = 0;
        int interiorSamples = 0;

        for (int x = rect.X; x < rect.XMax; x++)
        {
            if (IsDarkFramePixel(pixels[rect.Y * width + x])) darkEdges++;
            if (IsDarkFramePixel(pixels[(rect.YMax - 1) * width + x])) darkEdges++;
            totalEdges += 2;
        }

        for (int y = rect.Y + 1; y < rect.YMax - 1; y++)
        {
            if (IsDarkFramePixel(pixels[y * width + rect.X])) darkEdges++;
            if (IsDarkFramePixel(pixels[y * width + rect.XMax - 1])) darkEdges++;
            totalEdges += 2;
        }

        float edgeCoverage = totalEdges > 0 ? darkEdges / (float)totalEdges : 0f;
        if (edgeCoverage < settings.FrameCoverageThreshold)
            return -1f;

        int innerLeft = Math.Min(rect.XMax - 1, rect.X + Math.Max(6, rect.Width / 8));
        int innerRight = Math.Max(innerLeft + 1, rect.XMax - Math.Max(6, rect.Width / 8));
        int innerTop = Math.Min(rect.YMax - 1, rect.Y + Math.Max(6, rect.Height / 8));
        int innerBottom = Math.Max(innerTop + 1, rect.YMax - Math.Max(6, rect.Height / 8));

        for (int y = innerTop; y < innerBottom; y += 3)
        for (int x = innerLeft; x < innerRight; x += 3)
        {
            interiorSamples++;
            if (PixelBrightness(pixels[y * width + x]) >= settings.FrameDarkThreshold)
                interiorBright++;
        }

        float interiorBrightness = interiorSamples > 0 ? interiorBright / (float)interiorSamples : 0f;
        float centerOffset = Math.Abs(rect.Center.X - targetRect.Center.X) * 0.01f + Math.Abs(rect.Center.Y - targetRect.Center.Y) * 0.02f;
        return edgeCoverage * 3f
            + interiorBrightness
            - Math.Abs(rect.Width - targetRect.Width) * 0.002f
            - Math.Abs(rect.Height - targetRect.Height) * 0.006f
            - centerOffset;
    }

    bool IsDarkFramePixel(Color32 pixel) => PixelBrightness(pixel) <= settings.FrameDarkThreshold;

    static int PixelBrightness(Color32 pixel) => (pixel.R * 30 + pixel.G * 59 + pixel.B * 11) / 100;

    ExtractedElement BuildMaskedFocusedElement(Color32[] pixels, int width, int height, RectInt exactRect, string elementName)
    {
        if (settings.ExactMask != ExactPieceMask.None)
            return BuildExactMaskedFocusedElement(pixels, width, exactRect, elementName);

        RectInt maskRect = ExpandBounds(exactRect, width, height, settings.FocusedMaskMargin);
        bool[] foregroundMask = BuildFocusedForegroundMask(pixels, width, maskRect, exactRect);

        Color32[] fullPixels = CopyRectPixels(pixels, width, exactRect);
        int maskOffsetX = exactRect.X - maskRect.X;
        int maskOffsetY = exactRect.Y - maskRect.Y;
        int maskWidth = maskRect.Width;

        for (int y = 0; y < exactRect.Height; y++)
        {
            int maskRow = (maskOffsetY + y) * maskWidth + maskOffsetX;
            int outputRow = y * exactRect.Width;
            for (int x = 0; x < exactRect.Width; x++)
            {
                if (foregroundMask[maskRow + x])
                    continue;

                fullPixels[outputRow + x].A = 0;
            }
        }

        RectInt tightBounds = TryGetForegroundBounds(fullPixels, exactRect.Width, exactRect.Height, settings.AlphaThreshold, out RectInt localBounds)
            ? new RectInt(exactRect.X + localBounds.X, exactRect.Y + localBounds.Y, localBounds.Width, localBounds.Height)
            : exactRect;

        return new ExtractedElement
        {
            Name = elementName,
            TightBounds = tightBounds,
            ExportBounds = exactRect,
            PixelCount = CountOpaquePixels(fullPixels),
            PlatePixelCount = 0,
            DetailPixelCount = 0,
            FullBitmap = CreateBitmap(fullPixels, exactRect.Width, exactRect.Height),
        };
    }

    ExtractedElement BuildExactMaskedFocusedElement(Color32[] pixels, int width, RectInt exactRect, string elementName)
    {
        Color32[] fullPixels = CopyRectPixels(pixels, width, exactRect);
        ApplyExactPieceMask(fullPixels, exactRect.Width, exactRect.Height, settings.ExactMask);

        if (settings.ClearMatchButtonBorderContent)
        {
            ClearTransparentRect(fullPixels, exactRect.Width, exactRect.Height, 27, 24, 385, 135);
            ClearTransparentRect(fullPixels, exactRect.Width, exactRect.Height, 140, 135, 275, 144);
        }

        RectInt exportRect = exactRect;
        if (settings.ExactExportTrim is RectInt trim && !trim.IsEmpty)
        {
            RectInt clampedTrim = ClampRectToImage(trim, exactRect.Width, exactRect.Height);
            fullPixels = CopyRectPixels(fullPixels, exactRect.Width, clampedTrim);
            exportRect = new RectInt(exactRect.X + clampedTrim.X, exactRect.Y + clampedTrim.Y, clampedTrim.Width, clampedTrim.Height);
        }

        bool[] componentMask = new bool[exportRect.Width * exportRect.Height];
        for (int i = 0; i < fullPixels.Length; i++)
        {
            componentMask[i] = fullPixels[i].A > settings.AlphaThreshold;
        }

        bool[] plateMask = BuildPlateMask(pixels, width, componentMask, exportRect);
        Color32[] platePixels = CopyVariantRectPixels(pixels, width, exportRect, componentMask, plateMask, LayerVariant.Plate);
        Color32[] detailPixels = CopyVariantRectPixels(pixels, width, exportRect, componentMask, plateMask, LayerVariant.Detail);

        int platePixelsCount = CountOpaquePixels(platePixels);
        int detailPixelsCount = CountOpaquePixels(detailPixels);

        Bitmap? plateBitmap = platePixelsCount > 0 ? CreateBitmap(platePixels, exportRect.Width, exportRect.Height) : null;
        Bitmap? detailBitmap = detailPixelsCount >= settings.MinDetailPixels ? CreateBitmap(detailPixels, exportRect.Width, exportRect.Height) : null;

        if (detailBitmap == null)
            detailPixelsCount = 0;
        if (platePixelsCount == 0)
        {
            plateBitmap = null;
            platePixelsCount = 0;
        }

        return new ExtractedElement
        {
            Name = elementName,
            TightBounds = exportRect,
            ExportBounds = exportRect,
            PixelCount = CountOpaquePixels(fullPixels),
            PlatePixelCount = platePixelsCount,
            DetailPixelCount = detailPixelsCount,
            FullBitmap = CreateBitmap(fullPixels, exportRect.Width, exportRect.Height),
            PlateBitmap = plateBitmap,
            DetailBitmap = detailBitmap,
        };
    }

    List<ComponentPixel> CollectComponent(Color32[] pixels, int width, int height, int startIndex, bool[] visited, out RectInt tightBounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        List<ComponentPixel> points = new(256);
        Stack<int> stack = new();
        stack.Push(startIndex);
        visited[startIndex] = true;

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            int x = index % width;
            int y = index / width;

            points.Add(new ComponentPixel(x, y));
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;

            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
            Visit(x - 1, y - 1);
            Visit(x + 1, y - 1);
            Visit(x - 1, y + 1);
            Visit(x + 1, y + 1);
        }

        tightBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return points;

        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;
            if (visited[index] || pixels[index].A <= settings.AlphaThreshold)
                return;

            visited[index] = true;
            stack.Push(index);
        }
    }

    ExtractedElement BuildExtractedElement(int index, Color32[] pixels, int width, int height, List<ComponentPixel> componentPixels, RectInt tightBounds)
        => BuildExtractedElement(index, pixels, width, height, componentPixels, tightBounds, $"Element_{index:00}", -1);

    ExtractedElement BuildExtractedElement(int index, Color32[] pixels, int width, int height, List<ComponentPixel> componentPixels, RectInt tightBounds, string elementName)
        => BuildExtractedElement(index, pixels, width, height, componentPixels, tightBounds, elementName, -1);

    ExtractedElement BuildExtractedElement(int index, Color32[] pixels, int width, int height, List<ComponentPixel> componentPixels, RectInt tightBounds, string elementName, int exportPaddingOverride)
    {
        int exportPadding = exportPaddingOverride >= 0 ? exportPaddingOverride : settings.ComponentPadding;
        RectInt exportBounds = ExpandBounds(tightBounds, width, height, exportPadding);
        bool[] seedMask = BuildComponentMask(componentPixels, exportBounds);
        bool[] componentMask = RecoverComponentMask(pixels, width, exportBounds, seedMask);
        bool[] plateMask = BuildPlateMask(pixels, width, componentMask, exportBounds);

        Color32[] fullPixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Full);
        Color32[] platePixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Plate);
        Color32[] detailPixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Detail);

        RectInt finalBounds = TryGetMaskBounds(componentMask, exportBounds.Width, exportBounds.Height, out RectInt localRecoveredBounds)
            ? new RectInt(exportBounds.X + localRecoveredBounds.X, exportBounds.Y + localRecoveredBounds.Y, localRecoveredBounds.Width, localRecoveredBounds.Height)
            : tightBounds;

        int platePixelsCount = CountOpaquePixels(platePixels);
        int detailPixelsCount = CountOpaquePixels(detailPixels);
        Bitmap? plateBitmap = platePixelsCount > 0 ? CreateBitmap(platePixels, exportBounds.Width, exportBounds.Height) : null;
        Bitmap? detailBitmap = detailPixelsCount >= settings.MinDetailPixels ? CreateBitmap(detailPixels, exportBounds.Width, exportBounds.Height) : null;

        if (detailBitmap == null)
            detailPixelsCount = 0;
        if (platePixelsCount == 0)
        {
            plateBitmap = null;
            platePixelsCount = 0;
        }

        return new ExtractedElement
        {
            Name = elementName,
            TightBounds = finalBounds,
            ExportBounds = exportBounds,
            PixelCount = componentPixels.Count,
            PlatePixelCount = platePixelsCount,
            DetailPixelCount = detailPixelsCount,
            FullBitmap = CreateBitmap(fullPixels, exportBounds.Width, exportBounds.Height),
            PlateBitmap = plateBitmap,
            DetailBitmap = detailBitmap,
        };
    }

    static bool[] BuildComponentMask(List<ComponentPixel> componentPixels, RectInt bounds)
    {
        int localWidth = bounds.Width;
        bool[] componentMask = new bool[localWidth * bounds.Height];
        for (int i = 0; i < componentPixels.Count; i++)
        {
            ComponentPixel pixel = componentPixels[i];
            int localX = pixel.X - bounds.X;
            int localY = pixel.Y - bounds.Y;
            componentMask[localY * localWidth + localX] = true;
        }
        return componentMask;
    }

    bool[] RecoverComponentMask(Color32[] pixels, int width, RectInt bounds, bool[] seedMask)
    {
        bool[] recoveredMask = new bool[seedMask.Length];
        Array.Copy(seedMask, recoveredMask, seedMask.Length);

        int localWidth = bounds.Width;
        int localHeight = bounds.Height;
        List<Point> edgeSeeds = new();
        List<Color32> seedColors = new();
        List<Color32> backgroundSamples = new();

        for (int y = 0; y < localHeight; y++)
        {
            for (int x = 0; x < localWidth; x++)
            {
                int localIndex = y * localWidth + x;
                if (seedMask[localIndex])
                {
                    if (IsMaskEdge(seedMask, localWidth, localHeight, x, y))
                    {
                        edgeSeeds.Add(new Point(x, y));
                        seedColors.Add(pixels[(bounds.Y + y) * width + bounds.X + x]);
                    }
                }
                else if (x == 0 || y == 0 || x == localWidth - 1 || y == localHeight - 1)
                {
                    backgroundSamples.Add(pixels[(bounds.Y + y) * width + bounds.X + x]);
                }
            }
        }

        if (edgeSeeds.Count == 0)
            return recoveredMask;

        Color32 averageSeed = AverageColor(seedColors);
        Color32 averageBackground = AverageColor(backgroundSamples);
        Stack<Point> stack = new(edgeSeeds);

        while (stack.Count > 0)
        {
            Point point = stack.Pop();
            Color32 current = pixels[(bounds.Y + point.Y) * width + bounds.X + point.X];

            Visit(point.X - 1, point.Y, current);
            Visit(point.X + 1, point.Y, current);
            Visit(point.X, point.Y - 1, current);
            Visit(point.X, point.Y + 1, current);
            Visit(point.X - 1, point.Y - 1, current);
            Visit(point.X + 1, point.Y - 1, current);
            Visit(point.X - 1, point.Y + 1, current);
            Visit(point.X + 1, point.Y + 1, current);
        }

        return recoveredMask;

        void Visit(int x, int y, Color32 current)
        {
            if (x < 0 || x >= localWidth || y < 0 || y >= localHeight)
                return;

            int localIndex = y * localWidth + x;
            if (recoveredMask[localIndex])
                return;

            Color32 candidate = pixels[(bounds.Y + y) * width + bounds.X + x];
            if (!ShouldRecoverPixel(candidate, current, averageSeed, averageBackground, backgroundSamples))
                return;

            recoveredMask[localIndex] = true;
            stack.Push(new Point(x, y));
        }
    }

    bool ShouldRecoverPixel(Color32 candidate, Color32 current, Color32 averageSeed, Color32 averageBackground, List<Color32> backgroundSamples)
    {
        if (candidate.A <= settings.AlphaThreshold)
            return false;

        int distToCurrent = ColorDistanceSquared(candidate, current);
        int distToSeed = ColorDistanceSquared(candidate, averageSeed);
        if (distToCurrent > settings.RecoveryGrowTolerance * settings.RecoveryGrowTolerance
            && distToSeed > settings.RecoverySeedTolerance * settings.RecoverySeedTolerance)
            return false;

        if (backgroundSamples.Count == 0)
            return true;

        int backgroundPenalty = settings.BackgroundRejectTolerance * settings.BackgroundRejectTolerance;
        int distToBackground = ColorDistanceSquared(candidate, averageBackground);
        if (distToSeed + backgroundPenalty < distToBackground)
            return true;

        int step = Math.Max(1, backgroundSamples.Count / 12);
        for (int i = 0; i < backgroundSamples.Count; i += step)
        {
            if (distToSeed + backgroundPenalty < ColorDistanceSquared(candidate, backgroundSamples[i]))
                return true;
        }

        return false;
    }

    static bool IsMaskEdge(bool[] mask, int width, int height, int x, int y)
    {
        int index = y * width + x;
        if (!mask[index])
            return false;

        return x == 0 || y == 0 || x == width - 1 || y == height - 1
            || !MaskAt(mask, width, height, x - 1, y)
            || !MaskAt(mask, width, height, x + 1, y)
            || !MaskAt(mask, width, height, x, y - 1)
            || !MaskAt(mask, width, height, x, y + 1);
    }

    static bool MaskAt(bool[] mask, int width, int height, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;
        return mask[y * width + x];
    }

    bool[] BuildPlateMask(Color32[] pixels, int width, bool[] componentMask, RectInt bounds)
    {
        int localWidth = bounds.Width;
        int localHeight = bounds.Height;
        List<Point> seedPoints = new();
        List<Color32> seedColors = new();
        bool hasOuterBackground = false;

        for (int y = 0; y < localHeight; y++)
        {
            for (int x = 0; x < localWidth; x++)
            {
                if (!componentMask[y * localWidth + x])
                    continue;

                bool touchesOuterBand = x <= 1 || y <= 1 || x >= localWidth - 2 || y >= localHeight - 2;
                if (touchesOuterBand)
                    hasOuterBackground = true;

                if (!hasOuterBackground)
                {
                    if (IsMaskEdge(componentMask, localWidth, localHeight, x, y))
                    {
                        seedPoints.Add(new Point(x, y));
                        seedColors.Add(pixels[(bounds.Y + y) * width + bounds.X + x]);
                    }
                    continue;
                }

                if (touchesOuterBand)
                {
                    seedPoints.Add(new Point(x, y));
                    seedColors.Add(pixels[(bounds.Y + y) * width + bounds.X + x]);
                }
            }
        }

        if (seedPoints.Count == 0)
            return new bool[localWidth * localHeight];

        Color32 seedAverage = AverageColor(seedColors);
        bool[] plateMask = new bool[localWidth * localHeight];
        Stack<Point> stack = new();

        foreach (Point point in seedPoints)
        {
            int localIndex = point.Y * localWidth + point.X;
            if (plateMask[localIndex])
                continue;

            plateMask[localIndex] = true;
            stack.Push(point);
        }

        while (stack.Count > 0)
        {
            Point point = stack.Pop();
            Color32 current = pixels[(bounds.Y + point.Y) * width + bounds.X + point.X];

            Visit(point.X - 1, point.Y, current);
            Visit(point.X + 1, point.Y, current);
            Visit(point.X, point.Y - 1, current);
            Visit(point.X, point.Y + 1, current);
        }

        return plateMask;

        void Visit(int x, int y, Color32 current)
        {
            if (x < 0 || x >= localWidth || y < 0 || y >= localHeight)
                return;

            int localIndex = y * localWidth + x;
            if (plateMask[localIndex] || !componentMask[localIndex])
                return;

            Color32 candidate = pixels[(bounds.Y + y) * width + bounds.X + x];
            if (!LooksLikePlate(candidate, current, seedAverage, seedColors))
                return;

            plateMask[localIndex] = true;
            stack.Push(new Point(x, y));
        }
    }

    bool LooksLikePlate(Color32 candidate, Color32 current, Color32 averageSeed, List<Color32> seedColors)
    {
        int plateGrowLimit = settings.PlateGrowTolerance * settings.PlateGrowTolerance;
        int plateSeedLimit = settings.PlateSeedTolerance * settings.PlateSeedTolerance;
        if (ColorDistanceSquared(candidate, current) > plateGrowLimit)
            return false;

        if (ColorDistanceSquared(candidate, averageSeed) <= plateSeedLimit)
            return true;

        int step = Math.Max(1, seedColors.Count / 12);
        for (int i = 0; i < seedColors.Count; i += step)
        {
            if (ColorDistanceSquared(candidate, seedColors[i]) <= plateSeedLimit)
                return true;
        }

        return false;
    }

    static Color32 AverageColor(List<Color32> colors)
    {
        if (colors.Count == 0)
            return new Color32(0, 0, 0, 0);

        int r = 0, g = 0, b = 0, a = 0;
        foreach (Color32 color in colors)
        {
            r += color.R;
            g += color.G;
            b += color.B;
            a += color.A;
        }

        return new Color32((byte)(r / colors.Count), (byte)(g / colors.Count), (byte)(b / colors.Count), (byte)(a / colors.Count));
    }

    static RectInt ExpandBounds(RectInt bounds, int width, int height, int padding)
    {
        int minX = Math.Max(0, bounds.X - padding);
        int minY = Math.Max(0, bounds.Y - padding);
        int maxX = Math.Min(width - 1, bounds.XMax - 1 + padding);
        int maxY = Math.Min(height - 1, bounds.YMax - 1 + padding);
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    static bool TryGetForegroundBounds(Color32[] pixels, int width, int height, int alphaCutoff, out RectInt bounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int rowIndex = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[rowIndex + x].A <= alphaCutoff)
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    static Color32[] CopyRectPixels(Color32[] pixels, int sourceWidth, RectInt bounds)
    {
        Color32[] output = new Color32[bounds.Width * bounds.Height];
        for (int y = 0; y < bounds.Height; y++)
        {
            int sourceIndex = (bounds.Y + y) * sourceWidth + bounds.X;
            int targetIndex = y * bounds.Width;
            Array.Copy(pixels, sourceIndex, output, targetIndex, bounds.Width);
        }

        return output;
    }

    static Color32[] CopyVariantRectPixels(Color32[] pixels, int sourceWidth, RectInt exportBounds, bool[] componentMask, bool[] plateMask, LayerVariant variant)
    {
        Color32[] output = new Color32[exportBounds.Width * exportBounds.Height];
        int localWidth = exportBounds.Width;

        for (int y = 0; y < exportBounds.Height; y++)
        {
            for (int x = 0; x < exportBounds.Width; x++)
            {
                int globalX = exportBounds.X + x;
                int globalY = exportBounds.Y + y;
                int outputIndex = y * exportBounds.Width + x;
                Color32 pixel = pixels[globalY * sourceWidth + globalX];
                if (pixel.A == 0)
                {
                    output[outputIndex] = pixel;
                    continue;
                }

                bool insideComponent = localWidth > 0 && componentMask[y * localWidth + x];
                if (!insideComponent)
                {
                    pixel.A = 0;
                    output[outputIndex] = pixel;
                    continue;
                }

                bool isPlate = plateMask[y * localWidth + x];
                if ((variant == LayerVariant.Plate && !isPlate) || (variant == LayerVariant.Detail && isPlate))
                    pixel.A = 0;

                output[outputIndex] = pixel;
            }
        }

        return output;
    }

    static bool TryGetMaskBounds(bool[] mask, int width, int height, out RectInt bounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!mask[y * width + x])
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    static int CountOpaquePixels(Color32[] pixels)
    {
        int count = 0;
        foreach (Color32 pixel in pixels)
        {
            if (pixel.A > 0)
                count++;
        }

        return count;
    }

    static void ApplyExactPieceMask(Color32[] pixels, int width, int height, ExactPieceMask mask)
    {
        if (mask == ExactPieceMask.None || mask == ExactPieceMask.Rect)
            return;

        if (mask == ExactPieceMask.FriendAvatar)
        {
            ClearFriendAvatarBadgeOverlap(pixels, width, height);
            return;
        }

        if (mask == ExactPieceMask.FriendAvatarFrame)
        {
            ApplyFriendAvatarFrameOnlyMask(pixels, width, height);
            return;
        }

        if (mask == ExactPieceMask.FriendRowFrame)
        {
            ClearFriendRowContent(pixels, width, height);
            return;
        }

        if (mask == ExactPieceMask.FriendPanelFrame)
        {
            ApplyExactPieceMask(pixels, width, height, ExactPieceMask.Panel);
            ClearFriendPanelContentTransparent(pixels, width, height);
            return;
        }

        PointF[] polygon;
        if (mask == ExactPieceMask.Mode)
        {
            polygon = new[]
            {
                new PointF(9f, 4f), new PointF(width - 10f, 4f),
                new PointF(width - 3f, 12f), new PointF(width - 3f, height - 14f),
                new PointF(width - 12f, height - 5f), new PointF(10f, height - 5f),
                new PointF(3f, height - 13f), new PointF(3f, 12f),
            };
        }
        else if (mask == ExactPieceMask.Panel)
        {
            polygon = new[]
            {
                new PointF(4f, 1.5f), new PointF(width - 5f, 1.5f),
                new PointF(width - 1.5f, 5f), new PointF(width - 1.5f, height - 6f),
                new PointF(width - 6f, height - 1.5f), new PointF(5f, height - 1.5f),
                new PointF(1.5f, height - 5f), new PointF(1.5f, 5f),
            };
        }
        else
        {
            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int inside = 0;
                float textureY = height - 1 - y;
                if (PointInPolygon(new PointF(x + 0.25f, textureY + 0.25f), polygon)) inside++;
                if (PointInPolygon(new PointF(x + 0.75f, textureY + 0.25f), polygon)) inside++;
                if (PointInPolygon(new PointF(x + 0.25f, textureY + 0.75f), polygon)) inside++;
                if (PointInPolygon(new PointF(x + 0.75f, textureY + 0.75f), polygon)) inside++;
                if (inside == 4)
                    continue;

                int index = y * width + x;
                Color32 pixel = pixels[index];
                pixel.A = (byte)MathF.Round(pixel.A * inside / 4f);
                pixels[index] = pixel;
            }
        }
    }

    static void ClearFriendAvatarBadgeOverlap(Color32[] pixels, int width, int height)
    {
        int startY = Math.Clamp((int)MathF.Round(height * 0.67f), 0, height);
        int baseLimit = Math.Clamp((int)MathF.Round(width * 0.28f), 0, width - 1);

        for (int y = startY; y < height; y++)
        {
            int limit = Math.Clamp(baseLimit + (int)MathF.Round((y - startY) * 0.55f), 0, width - 1);
            for (int x = 0; x <= limit; x++)
            {
                Color32 pixel = pixels[y * width + x];
                pixel.R = 0;
                pixel.G = 0;
                pixel.B = 0;
                pixel.A = 0;
                pixels[y * width + x] = pixel;
            }
        }
    }

    static void ApplyFriendAvatarSilhouetteMask(Color32[] pixels, int width, int height)
    {
        PointF[] silhouette = new[]
        {
            new PointF(width * 0.16f, 0f),
            new PointF(width * 0.78f, 0f),
            new PointF(width - 1f, height * 0.20f),
            new PointF(width - 1f, height * 0.50f),
            new PointF(width * 0.86f, height * 0.60f),
            new PointF(width * 0.95f, height - 1f),
            new PointF(width * 0.34f, height - 1f),
            new PointF(width * 0.25f, height * 0.84f),
            new PointF(width * 0.12f, height * 0.76f),
            new PointF(0f, height * 0.62f),
            new PointF(width * 0.04f, height * 0.32f),
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (PointInPolygon(new PointF(x + 0.5f, y + 0.5f), silhouette))
                    continue;

                Color32 pixel = pixels[y * width + x];
                pixel.R = 0;
                pixel.G = 0;
                pixel.B = 0;
                pixel.A = 0;
                pixels[y * width + x] = pixel;
            }
        }
    }

    static void ClearFriendAvatarEdgeBackdrop(Color32[] pixels, int width, int height)
    {
        int edgeX = Math.Max(5, (int)MathF.Round(width * 0.16f));
        int edgeBottom = Math.Max(6, (int)MathF.Round(height * 0.18f));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool nearEdge = x < edgeX || x >= width - edgeX || y >= height - edgeBottom;
                if (!nearEdge)
                    continue;

                Color32 pixel = pixels[y * width + x];
                if (pixel.A == 0)
                    continue;

                int brightness = PixelBrightness(pixel);
                int saturation = PixelSaturation(pixel);
                bool darkBackdrop = brightness < 46 && saturation < 36;
                bool blueGrayBackdrop = brightness < 68 && saturation < 46 && pixel.B >= pixel.R - 4;
                if (!darkBackdrop && !blueGrayBackdrop)
                    continue;

                pixel.R = 0;
                pixel.G = 0;
                pixel.B = 0;
                pixel.A = 0;
                pixels[y * width + x] = pixel;
            }
        }
    }

    static void KeepLargestOpaqueComponent(Color32[] pixels, int width, int height)
    {
        bool[] visited = new bool[pixels.Length];
        bool[] keep = new bool[pixels.Length];
        List<int> best = new();
        Stack<int> stack = new();
        List<int> current = new();
        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 0, -1, 1, -1, -1, 1, 1 };

        for (int start = 0; start < pixels.Length; start++)
        {
            if (visited[start] || pixels[start].A == 0)
                continue;

            current.Clear();
            stack.Clear();
            stack.Push(start);
            visited[start] = true;

            while (stack.Count > 0)
            {
                int index = stack.Pop();
                current.Add(index);
                int x = index % width;
                int y = index / width;

                for (int i = 0; i < dx.Length; i++)
                {
                    int nx = x + dx[i];
                    int ny = y + dy[i];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int next = ny * width + nx;
                    if (visited[next] || pixels[next].A == 0)
                        continue;

                    visited[next] = true;
                    stack.Push(next);
                }
            }

            if (current.Count > best.Count)
                best = new List<int>(current);
        }

        foreach (int index in best)
            keep[index] = true;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].A == 0 || keep[i])
                continue;

            Color32 pixel = pixels[i];
            pixel.R = 0;
            pixel.G = 0;
            pixel.B = 0;
            pixel.A = 0;
            pixels[i] = pixel;
        }
    }

    static void RemoveFriendAvatarBackdrop(Color32[] pixels, int width, int height)
    {
        bool[] background = new bool[width * height];
        Queue<int> queue = new();

        void TrySeed(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;
            if (background[index])
                return;

            if (!IsFriendAvatarBackdropSeed(pixels[index]))
                return;

            background[index] = true;
            queue.Enqueue(index);
        }

        for (int y = 0; y < height; y++)
        {
            TrySeed(0, y);
            TrySeed(width - 1, y);
        }

        for (int x = 0; x < width; x++)
        {
            if (x < width * 0.22f || x > width * 0.78f)
                TrySeed(x, 0);
            TrySeed(x, height - 1);
        }

        int notchStartY = Math.Clamp((int)MathF.Round(height * 0.64f), 0, height);
        int notchEndX = Math.Clamp((int)MathF.Round(width * 0.42f), 0, width);
        for (int y = notchStartY; y < height; y++)
        for (int x = 0; x < notchEndX; x++)
            TrySeed(x, y);

        int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
        int[] dy = { 0, 0, -1, 1, -1, -1, 1, 1 };
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;
            Color32 source = pixels[index];

            for (int i = 0; i < dx.Length; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                int nextIndex = ny * width + nx;
                if (background[nextIndex])
                    continue;

                Color32 candidate = pixels[nextIndex];
                if (!IsFriendAvatarBackdropGrow(candidate, source))
                    continue;

                background[nextIndex] = true;
                queue.Enqueue(nextIndex);
            }
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (!background[i])
                continue;

            Color32 pixel = pixels[i];
            pixel.R = 0;
            pixel.G = 0;
            pixel.B = 0;
            pixel.A = 0;
            pixels[i] = pixel;
        }
    }

    static bool IsFriendAvatarBackdropSeed(Color32 pixel)
    {
        if (pixel.A == 0)
            return true;

        int brightness = PixelBrightness(pixel);
        int saturation = PixelSaturation(pixel);
        if (brightness < 30)
            return true;

        return brightness < 58 && saturation < 34 && !LooksLikeSkinOrGold(pixel);
    }

    static bool IsFriendAvatarBackdropGrow(Color32 candidate, Color32 source)
    {
        if (candidate.A == 0)
            return true;

        int brightness = PixelBrightness(candidate);
        int saturation = PixelSaturation(candidate);
        if (brightness < 26)
            return true;

        if (LooksLikeSkinOrGold(candidate))
            return false;

        if (brightness < 72 && saturation < 42)
            return true;

        return brightness < 90
            && saturation < 56
            && ColorDistance(candidate, source) < 48;
    }

    static void ApplyFriendAvatarFrameOnlyMask(Color32[] pixels, int width, int height)
    {
        PointF[] badge = new[]
        {
            new PointF(0f, 43f),
            new PointF(16f, 37f),
            new PointF(31f, 44f),
            new PointF(31f, 60f),
            new PointF(17f, height - 1f),
            new PointF(1f, 61f),
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool keepMainFrame = IsInsideMainAvatarFrameBorder(x, y, width, height);
                bool keepBadgeFrame = IsInsideBadgeFrameBorder(new PointF(x + 0.5f, y + 0.5f), badge);
                if (keepMainFrame || keepBadgeFrame)
                {
                    pixels[y * width + x] = BuildCleanFriendAvatarFramePixel(x, y, width, height, keepBadgeFrame);
                    continue;
                }

                Color32 clear = pixels[y * width + x];
                clear.R = 0;
                clear.G = 0;
                clear.B = 0;
                clear.A = 0;
                pixels[y * width + x] = clear;
            }
        }
    }

    static bool IsInsideMainAvatarFrameBorder(int x, int y, int width, int height)
    {
        int outerLeft = Math.Max(0, (int)MathF.Round(width * 0.07f));
        int outerRight = Math.Min(width - 1, (int)MathF.Round(width * 0.98f));
        int outerTop = 0;
        int outerBottom = Math.Min(height - 1, (int)MathF.Round(height * 0.98f));
        int thicknessX = Math.Max(4, (int)MathF.Round(width * 0.060f));
        int thicknessY = Math.Max(4, (int)MathF.Round(height * 0.060f));

        if (x < outerLeft || x > outerRight || y < outerTop || y > outerBottom)
            return false;

        bool onBorder = x <= outerLeft + thicknessX
            || x >= outerRight - thicknessX
            || y <= outerTop + thicknessY
            || y >= outerBottom - thicknessY;
        if (!onBorder)
            return false;

        bool inBadgePocket = x < width * 0.38f && y > height * 0.56f;
        return !inBadgePocket;
    }

    static bool IsInsideBadgeFrameBorder(PointF point, PointF[] badge)
    {
        if (!PointInPolygon(point, badge))
            return false;

        PointF center = new(16f, 53f);
        float shrink = 0.58f;
        PointF[] inner = new PointF[badge.Length];
        for (int i = 0; i < badge.Length; i++)
        {
            inner[i] = new PointF(
                center.X + (badge[i].X - center.X) * shrink,
                center.Y + (badge[i].Y - center.Y) * shrink);
        }

        return !PointInPolygon(point, inner);
    }

    static bool LooksLikeFriendFramePixel(Color32 pixel)
    {
        if (pixel.A == 0)
            return false;

        int brightness = PixelBrightness(pixel);
        int saturation = PixelSaturation(pixel);
        bool warmMetal = pixel.R >= pixel.G - 2 && pixel.G >= pixel.B + 12 && brightness >= 28 && brightness <= 178 && saturation <= 96;
        bool darkFrame = brightness < 32 && saturation < 42;
        bool brightEdge = brightness > 128 && pixel.R >= pixel.G - 4 && pixel.G >= pixel.B + 10;
        return warmMetal || darkFrame || brightEdge;
    }

    static Color32 BuildCleanFriendAvatarFramePixel(int x, int y, int width, int height, bool badge)
    {
        int distanceToOuter = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y));
        float edge = Math.Clamp(distanceToOuter / 5f, 0f, 1f);
        byte r = 30;
        byte g = 27;
        byte b = 16;

        if (badge)
        {
            r = 86;
            g = 72;
            b = 36;
        }
        else if (edge > 0.25f && edge < 0.85f)
        {
            r = 118;
            g = 98;
            b = 48;
        }
        else if (edge >= 0.85f)
        {
            r = 52;
            g = 45;
            b = 26;
        }

        if ((x + y) % 7 == 0)
        {
            r = (byte)Math.Clamp(r + 13, 0, 255);
            g = (byte)Math.Clamp(g + 10, 0, 255);
            b = (byte)Math.Clamp(b + 4, 0, 255);
        }

        return new Color32(r, g, b, 255);
    }

    static void ClearFriendRowContent(Color32[] pixels, int width, int height)
    {
        ClearTransparentRect(pixels, width, height, 10, 8, width - 10, height - 8);
        KeepFriendRowOuterFrame(pixels, width, height);
    }

    static void KeepFriendRowOuterFrame(Color32[] pixels, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool keep = x < 8 || x >= width - 8 || y < 4 || y >= height - 4;
                if (keep)
                    continue;

                Color32 pixel = pixels[y * width + x];
                pixel.R = 0;
                pixel.G = 0;
                pixel.B = 0;
                pixel.A = 0;
                pixels[y * width + x] = pixel;
            }
        }
    }

    static void ClearFriendPanelContentTransparent(Color32[] pixels, int width, int height)
    {
        int left = Math.Clamp((int)MathF.Round(width * 0.04f), 0, width - 1);
        int right = Math.Clamp((int)MathF.Round(width * 0.946f), left, width - 1);
        int top = Math.Clamp((int)MathF.Round(height * 0.084f), 0, height - 1);
        int bottom = Math.Clamp((int)MathF.Round(height * 0.892f), top, height - 1);
        ClearTransparentRect(pixels, width, height, left, top, right, bottom);
    }

    static bool LooksLikeSkinOrGold(Color32 pixel)
    {
        int brightness = PixelBrightness(pixel);
        if (brightness < 42)
            return false;

        bool skin = pixel.R > pixel.G + 8 && pixel.G >= pixel.B + 5 && pixel.R > 70;
        bool gold = pixel.R >= pixel.G - 4 && pixel.G > pixel.B + 12 && pixel.R > 65;
        return skin || gold;
    }

    static int PixelSaturation(Color32 pixel)
    {
        int max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        int min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        return max - min;
    }

    static int ColorDistance(Color32 a, Color32 b)
    {
        int dr = a.R - b.R;
        int dg = a.G - b.G;
        int db = a.B - b.B;
        return (int)MathF.Sqrt(dr * dr + dg * dg + db * db);
    }

    static void ClearTransparentRect(Color32[] pixels, int width, int height, int left, int topFromImageTop, int right, int bottomFromImageTop)
    {
        int x0 = Math.Clamp(left, 0, width - 1);
        int x1 = Math.Clamp(right, 0, width - 1);
        int y0 = Math.Clamp(topFromImageTop, 0, height - 1);
        int y1 = Math.Clamp(bottomFromImageTop, 0, height - 1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Color32 pixel = pixels[y * width + x];
                pixel.R = 0;
                pixel.G = 0;
                pixel.B = 0;
                pixel.A = 0;
                pixels[y * width + x] = pixel;
            }
        }
    }

    static bool PointInPolygon(PointF point, PointF[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool crosses = ((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y))
                && (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X);
            if (crosses)
                inside = !inside;
        }

        return inside;
    }

    bool[] BuildFocusedForegroundMask(Color32[] pixels, int width, RectInt expandedRect, RectInt exactRect)
    {
        int localWidth = expandedRect.Width;
        int localHeight = expandedRect.Height;
        bool[] visited = new bool[localWidth * localHeight];
        bool[] backgroundMask = new bool[localWidth * localHeight];
        Stack<int> stack = new();

        void Seed(int x, int y)
        {
            int index = y * localWidth + x;
            if (visited[index])
                return;

            visited[index] = true;
            backgroundMask[index] = true;
            stack.Push(index);
        }

        for (int x = 0; x < localWidth; x++)
        {
            Seed(x, 0);
            Seed(x, localHeight - 1);
        }

        for (int y = 0; y < localHeight; y++)
        {
            Seed(0, y);
            Seed(localWidth - 1, y);
        }

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            int x = index % localWidth;
            int y = index / localWidth;
            Color32 current = pixels[(expandedRect.Y + y) * width + expandedRect.X + x];
            int currentBrightness = PixelBrightness(current);

            Visit(x - 1, y, current, currentBrightness);
            Visit(x + 1, y, current, currentBrightness);
            Visit(x, y - 1, current, currentBrightness);
            Visit(x, y + 1, current, currentBrightness);
        }

        bool[] foregroundMask = new bool[localWidth * localHeight];
        int exactLocalX = exactRect.X - expandedRect.X;
        int exactLocalY = exactRect.Y - expandedRect.Y;

        for (int y = 0; y < exactRect.Height; y++)
        {
            for (int x = 0; x < exactRect.Width; x++)
            {
                int localX = exactLocalX + x;
                int localY = exactLocalY + y;
                int localIndex = localY * localWidth + localX;
                foregroundMask[localIndex] = !backgroundMask[localIndex];
            }
        }

        return foregroundMask;

        void Visit(int x, int y, Color32 current, int currentBrightness)
        {
            if (x < 0 || x >= localWidth || y < 0 || y >= localHeight)
                return;

            int localIndex = y * localWidth + x;
            if (visited[localIndex])
                return;

            visited[localIndex] = true;
            Color32 candidate = pixels[(expandedRect.Y + y) * width + expandedRect.X + x];
            if (!LooksLikeFocusedBackground(current, currentBrightness, candidate))
                return;

            backgroundMask[localIndex] = true;
            stack.Push(localIndex);
        }
    }

    bool LooksLikeFocusedBackground(Color32 current, int currentBrightness, Color32 candidate)
    {
        int colorDelta = Math.Abs(current.R - candidate.R) + Math.Abs(current.G - candidate.G) + Math.Abs(current.B - candidate.B);
        int brightnessDelta = Math.Abs(PixelBrightness(candidate) - currentBrightness);
        return colorDelta <= settings.FocusedBackgroundColorTolerance
            && brightnessDelta <= settings.FocusedBackgroundBrightnessTolerance;
    }

    static RectInt ClampRectToImage(RectInt rect, int width, int height)
    {
        int minX = Math.Clamp(rect.X, 0, width - 1);
        int minY = Math.Clamp(rect.Y, 0, height - 1);
        int maxX = Math.Clamp(rect.XMax, minX + 1, width);
        int maxY = Math.Clamp(rect.YMax, minY + 1, height);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    static RectInt ConvertTopLeftRectToPixelRect(RectInt rect, int imageHeight)
    {
        int y = imageHeight - rect.Y - rect.Height;
        return new RectInt(rect.X, y, rect.Width, rect.Height);
    }

    static int ColorDistanceSquared(Color32 a, Color32 b)
    {
        int dr = a.R - b.R;
        int dg = a.G - b.G;
        int db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }
}

internal struct Color32
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Color32(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

internal sealed record ExtractionResult(IReadOnlyList<ExtractedElement> Elements, Bitmap? CompositeBitmap, string StatusMessage);

internal static class ImageComposer
{
    public static Bitmap PatchText(Bitmap source, RectInt patchRect, string text, float fontSize)
    {
        Bitmap output = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(output);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        g.DrawImage(source, 0, 0, source.Width, source.Height);

        RectInt rect = NormalizePatchRect(patchRect, source.Width, source.Height);
        if (!rect.IsEmpty)
        {
            using var bgBrush = new SolidBrush(SampleAverageColor(source, rect, fallback: Color.FromArgb(215, 44, 36, 20)));
            g.FillRectangle(bgBrush, rect.X, rect.Y, rect.Width, rect.Height);
            DrawGoldLabel(g, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), text, fontSize);
        }

        return output;
    }

    public static Bitmap RenderGoldBevelButton(int width, int height, string text, float fontSize)
    {
        width = Math.Max(24, width);
        height = Math.Max(24, height);
        Bitmap output = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(output);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var outer = new RectangleF(1f, 1f, width - 2f, height - 2f);
        var inner = new RectangleF(6f, 5f, Math.Max(10f, width - 12f), Math.Max(10f, height - 10f));
        using var outerPath = BuildChamferPath(outer, MathF.Min(14f, height * 0.22f));
        using var innerPath = BuildChamferPath(inner, MathF.Min(10f, height * 0.18f));

        using (var shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
        {
            var shadowRect = outer;
            shadowRect.Offset(0f, 3f);
            using var shadowPath = BuildChamferPath(shadowRect, MathF.Min(14f, height * 0.22f));
            g.FillPath(shadowBrush, shadowPath);
        }

        using (var outerBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, outer.Top),
                   new PointF(0f, outer.Bottom),
                   Color.FromArgb(255, 104, 70, 18),
                   Color.FromArgb(255, 28, 16, 6)))
        {
            g.FillPath(outerBrush, outerPath);
        }

        using (var borderPen = new Pen(Color.FromArgb(255, 246, 209, 110), 1.5f))
        {
            g.DrawPath(borderPen, outerPath);
        }

        using (var innerBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, inner.Top),
                   new PointF(0f, inner.Bottom),
                   Color.FromArgb(255, 253, 222, 128),
                   Color.FromArgb(255, 168, 110, 28)))
        {
            var blend = new System.Drawing.Drawing2D.ColorBlend
            {
                Colors = new[]
                {
                    Color.FromArgb(255, 255, 241, 170),
                    Color.FromArgb(255, 242, 193, 86),
                    Color.FromArgb(255, 163, 104, 24)
                },
                Positions = new[] { 0f, 0.46f, 1f }
            };
            innerBrush.InterpolationColors = blend;
            g.FillPath(innerBrush, innerPath);
        }

        using (var sheenBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, inner.Top),
                   new PointF(0f, inner.Top + inner.Height * 0.55f),
                   Color.FromArgb(170, 255, 255, 240),
                   Color.FromArgb(0, 255, 255, 240)))
        {
            var sheenRect = new RectangleF(inner.X + 3f, inner.Y + 2f, inner.Width - 6f, inner.Height * 0.45f);
            using var sheenPath = BuildChamferPath(sheenRect, MathF.Min(7f, sheenRect.Height * 0.35f));
            g.FillPath(sheenBrush, sheenPath);
        }

        using (var insetPen = new Pen(Color.FromArgb(160, 114, 63, 14), 1f))
        {
            g.DrawPath(insetPen, innerPath);
        }

        DrawGoldLabel(g, new RectInt((int)MathF.Round(inner.X), (int)MathF.Round(inner.Y), (int)MathF.Round(inner.Width), (int)MathF.Round(inner.Height)), text, fontSize);
        return output;
    }

    public static Bitmap RenderGoldRoundedButton(int width, int height, string text, float fontSize)
    {
        width = Math.Max(24, width);
        height = Math.Max(24, height);
        Bitmap output = new(width, height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(output);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var outer = new RectangleF(1f, 1f, width - 2f, height - 2f);
        var inner = new RectangleF(5f, 5f, Math.Max(10f, width - 10f), Math.Max(10f, height - 10f));
        float outerRadius = MathF.Min(height * 0.44f, 20f);
        float innerRadius = MathF.Min(height * 0.34f, 16f);
        using var outerPath = BuildRoundedPath(outer, outerRadius);
        using var innerPath = BuildRoundedPath(inner, innerRadius);

        using (var shadowBrush = new SolidBrush(Color.FromArgb(88, 0, 0, 0)))
        {
            var shadowRect = outer;
            shadowRect.Offset(0f, 3f);
            using var shadowPath = BuildRoundedPath(shadowRect, outerRadius);
            g.FillPath(shadowBrush, shadowPath);
        }

        using (var outerBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, outer.Top),
                   new PointF(0f, outer.Bottom),
                   Color.FromArgb(255, 110, 72, 20),
                   Color.FromArgb(255, 32, 18, 7)))
        {
            g.FillPath(outerBrush, outerPath);
        }

        using (var borderPen = new Pen(Color.FromArgb(255, 248, 216, 126), 1.6f))
        {
            g.DrawPath(borderPen, outerPath);
        }

        using (var innerBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, inner.Top),
                   new PointF(0f, inner.Bottom),
                   Color.FromArgb(255, 255, 236, 156),
                   Color.FromArgb(255, 174, 114, 30)))
        {
            var blend = new System.Drawing.Drawing2D.ColorBlend
            {
                Colors = new[]
                {
                    Color.FromArgb(255, 255, 247, 191),
                    Color.FromArgb(255, 247, 205, 98),
                    Color.FromArgb(255, 198, 134, 34),
                    Color.FromArgb(255, 150, 95, 22)
                },
                Positions = new[] { 0f, 0.34f, 0.72f, 1f }
            };
            innerBrush.InterpolationColors = blend;
            g.FillPath(innerBrush, innerPath);
        }

        using (var topGlowBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, inner.Top),
                   new PointF(0f, inner.Top + inner.Height * 0.52f),
                   Color.FromArgb(178, 255, 255, 242),
                   Color.FromArgb(0, 255, 255, 242)))
        {
            var topGlowRect = new RectangleF(inner.X + 3f, inner.Y + 2f, inner.Width - 6f, inner.Height * 0.44f);
            using var topGlowPath = BuildRoundedPath(topGlowRect, MathF.Max(6f, innerRadius - 2f));
            g.FillPath(topGlowBrush, topGlowPath);
        }

        using (var bottomGlowBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(0f, inner.Top + inner.Height * 0.55f),
                   new PointF(0f, inner.Bottom),
                   Color.FromArgb(0, 140, 78, 10),
                   Color.FromArgb(92, 140, 78, 10)))
        {
            g.FillPath(bottomGlowBrush, innerPath);
        }

        using (var insetPen = new Pen(Color.FromArgb(168, 120, 70, 18), 1f))
        {
            g.DrawPath(insetPen, innerPath);
        }

        DrawGoldLabel(g, new RectInt((int)MathF.Round(inner.X), (int)MathF.Round(inner.Y), (int)MathF.Round(inner.Width), (int)MathF.Round(inner.Height)), text, fontSize);
        return output;
    }

    static RectInt NormalizePatchRect(RectInt patchRect, int width, int height)
    {
        if (patchRect.Width <= 0 || patchRect.Height <= 0)
            return new RectInt(width / 5, height / 4, width * 3 / 5, height / 2);

        int x = Math.Clamp(patchRect.X, 0, Math.Max(0, width - 1));
        int y = Math.Clamp(patchRect.Y, 0, Math.Max(0, height - 1));
        int w = Math.Clamp(patchRect.Width, 1, width - x);
        int h = Math.Clamp(patchRect.Height, 1, height - y);
        return new RectInt(x, y, w, h);
    }

    static Color SampleAverageColor(Bitmap source, RectInt rect, Color fallback)
    {
        long r = 0;
        long g = 0;
        long b = 0;
        long a = 0;
        long count = 0;

        int x0 = Math.Clamp(rect.X, 0, source.Width - 1);
        int y0 = Math.Clamp(rect.Y, 0, source.Height - 1);
        int x1 = Math.Clamp(rect.X + rect.Width - 1, 0, source.Width - 1);
        int y1 = Math.Clamp(rect.Y + rect.Height - 1, 0, source.Height - 1);

        for (int y = y0; y <= y1; y += Math.Max(1, rect.Height / 10))
        for (int x = x0; x <= x1; x += Math.Max(1, rect.Width / 10))
        {
            Color c = source.GetPixel(x, y);
            if (c.A == 0)
                continue;
            r += c.R;
            g += c.G;
            b += c.B;
            a += c.A;
            count++;
        }

        if (count == 0)
            return fallback;

        return Color.FromArgb(
            (int)(a / count),
            (int)(r / count),
            (int)(g / count),
            (int)(b / count));
    }

    static void DrawGoldLabel(Graphics g, RectInt rect, string text, float fontSize)
        => DrawGoldLabel(g, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), text, fontSize);

    static void DrawGoldLabel(Graphics g, Rectangle rect, string text, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        using var font = new Font("Microsoft YaHei UI", Math.Max(8f, fontSize), FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        var textRect = RectangleF.Inflate(rect, -4f, -2f);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        float emSize = g.DpiY * font.SizeInPoints / 72f;
        path.AddString(text, font.FontFamily, (int)font.Style, emSize, textRect, format);

        using (var shadowBrush = new SolidBrush(Color.FromArgb(160, 24, 10, 0)))
        using (var shadowMatrix = new System.Drawing.Drawing2D.Matrix())
        {
            shadowMatrix.Translate(0f, 2.2f);
            path.Transform(shadowMatrix);
            g.FillPath(shadowBrush, path);
            shadowMatrix.Reset();
            shadowMatrix.Translate(0f, -2.2f);
            path.Transform(shadowMatrix);
        }

        using (var fillBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new PointF(textRect.Left, textRect.Top),
                   new PointF(textRect.Left, textRect.Bottom),
                   Color.FromArgb(255, 255, 247, 191),
                   Color.FromArgb(255, 207, 149, 32)))
        {
            var blend = new System.Drawing.Drawing2D.ColorBlend
            {
                Colors = new[]
                {
                    Color.FromArgb(255, 255, 249, 204),
                    Color.FromArgb(255, 246, 201, 86),
                    Color.FromArgb(255, 186, 112, 20)
                },
                Positions = new[] { 0f, 0.38f, 1f }
            };
            fillBrush.InterpolationColors = blend;
            g.FillPath(fillBrush, path);
        }

        using var outlinePen = new Pen(Color.FromArgb(220, 72, 36, 6), Math.Max(1.6f, fontSize * 0.08f))
        {
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round
        };
        g.DrawPath(outlinePen, path);
    }

    static System.Drawing.Drawing2D.GraphicsPath BuildChamferPath(RectangleF rect, float cut)
    {
        float c = Math.Clamp(cut, 2f, Math.Min(rect.Width, rect.Height) / 2f);
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(rect.Left + c, rect.Top),
            new PointF(rect.Right - c, rect.Top),
            new PointF(rect.Right, rect.Top + c),
            new PointF(rect.Right, rect.Bottom - c),
            new PointF(rect.Right - c, rect.Bottom),
            new PointF(rect.Left + c, rect.Bottom),
            new PointF(rect.Left, rect.Bottom - c),
            new PointF(rect.Left, rect.Top + c)
        });
        return path;
    }

    static System.Drawing.Drawing2D.GraphicsPath BuildRoundedPath(RectangleF rect, float radius)
    {
        float r = Math.Clamp(radius, 2f, Math.Min(rect.Width, rect.Height) / 2f);
        float d = r * 2f;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
