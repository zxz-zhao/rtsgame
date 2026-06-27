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
            if (!TryBuildFocusedElement(pixels, width, height, settings.FocusRect, "Focused", out ExtractedElement? focusedElement, out RectInt snappedRect))
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

    public static RectInt[] GetFocusedRectPresets()
        => new[]
        {
            new RectInt(305, 156, 414, 148),
            new RectInt(305, 299, 414, 136),
            new RectInt(305, 443, 414, 136),
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
        RectInt pixelSpaceApprox = ConvertTopLeftRectToPixelRect(approxRect, height);
        RectInt clampedApprox = ClampRectToImage(pixelSpaceApprox, width, height);
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
