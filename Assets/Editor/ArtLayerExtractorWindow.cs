using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class ArtLayerExtractorWindow : EditorWindow
{
    enum ExtractionMode
    {
        AutoForeground,
        FocusedRect,
    }

    enum BackgroundMode
    {
        LightKey,
        CornerSample,
    }

    sealed class ExtractedElement
    {
        public string Name;
        public RectInt TightBounds;
        public RectInt ExportBounds;
        public int PixelCount;
        public int PlatePixelCount;
        public int DetailPixelCount;
        public Texture2D FullTexture;
        public Texture2D PlateTexture;
        public Texture2D DetailTexture;
    }

    readonly struct ComponentPixel
    {
        public readonly int X;
        public readonly int Y;

        public ComponentPixel(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    readonly struct FocusRegionPreset
    {
        public readonly string Name;
        public readonly RectInt ApproxBounds;

        public FocusRegionPreset(string name, RectInt approxBounds)
        {
            Name = name;
            ApproxBounds = approxBounds;
        }
    }

    readonly struct EdgeCandidate
    {
        public readonly int Position;
        public readonly float Score;

        public EdgeCandidate(int position, float score)
        {
            Position = position;
            Score = score;
        }
    }

    const string PrefOutputFolder = "ArtLayerExtractor.OutputFolder";
    const string DefaultOutputFolderName = "ExtractedLayers";

    Texture2D sourceAsset;
    string sourcePath;
    string outputFolder;
    Texture2D sourcePreview;
    Texture2D compositePreview;
    readonly List<ExtractedElement> elements = new List<ExtractedElement>();
    Vector2 scroll;
    ExtractionMode extractionMode = ExtractionMode.FocusedRect;
    BackgroundMode backgroundMode = BackgroundMode.LightKey;
    int alphaThreshold = 12;
    int lightThreshold = 230;
    int cornerTolerance = 42;
    int componentPadding = 6;
    int minComponentPixels = 80;
    int recoverySeedTolerance = 72;
    int recoveryGrowTolerance = 30;
    int backgroundRejectTolerance = 18;
    int plateSeedTolerance = 52;
    int plateGrowTolerance = 24;
    int minDetailPixels = 24;
    RectInt focusRect = new RectInt(305, 150, 414, 148);
    bool autoSnapFocusedRect;
    int focusedMaskMargin = 14;
    int focusedBackgroundColorTolerance = 26;
    int focusedBackgroundBrightnessTolerance = 38;
    int frameSearchMargin = 18;
    int frameDarkThreshold = 110;
    float frameCoverageThreshold = 0.68f;
    int frameSnapPadding = 0;
    string status = "Load an image to begin.";
    MessageType statusType = MessageType.Info;

    [MenuItem("RTS/Art/Image Layer Extractor")]
    public static void Open()
    {
        var window = GetWindow<ArtLayerExtractorWindow>("Layer Extractor");
        window.minSize = new Vector2(720f, 700f);
        window.Show();
    }

    public static void RunBatchSmokeTest()
    {
        var window = CreateInstance<ArtLayerExtractorWindow>();
        try
        {
            string source = FindBatchTestSource();
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
                throw new FileNotFoundException("No batch test source image was found.");

            window.sourcePath = source;
            window.outputFolder = Path.Combine(GetDefaultOutputFolder(), "batch_test");
            Directory.CreateDirectory(window.outputFolder);
            window.RunBatchScenarios(source);

            UnityEngine.Debug.Log($"[ArtLayerExtractor] Batch smoke test finished. Source: {source}. Elements: {window.elements.Count}. Output: {window.outputFolder}");
            AssetDatabase.Refresh();
        }
        finally
        {
            window.ClearResults();
            DestroyImmediate(window);
        }
    }

    void OnEnable()
    {
        outputFolder = EditorPrefs.GetString(PrefOutputFolder, GetDefaultOutputFolder());
    }

    void OnDisable()
    {
        ClearResults();
    }

    void OnGUI()
    {
        DrawSourceSection();
        EditorGUILayout.Space(8f);
        DrawSettingsSection();
        EditorGUILayout.Space(8f);
        DrawActionBar();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(status, statusType);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPreviewSection();
        DrawElementsSection();
        EditorGUILayout.EndScrollView();
    }

    void DrawSourceSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            sourceAsset = (Texture2D)EditorGUILayout.ObjectField("Texture", sourceAsset, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && sourceAsset != null)
            {
                sourcePath = AssetDatabase.GetAssetPath(sourceAsset);
                SetStatus("Source texture selected.", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load Image File...", GUILayout.Height(24f)))
                    LoadFromFile();
                if (GUILayout.Button("Use Selected", GUILayout.Height(24f)))
                    UseSelectedTexture();
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                    ResetAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Output");
                outputFolder = EditorGUILayout.TextField(outputFolder ?? string.Empty);
                if (GUILayout.Button("Browse...", GUILayout.Width(88f)))
                    PickOutputFolder();
                if (GUILayout.Button("Open", GUILayout.Width(60f)))
                    OpenOutputFolder();
            }

            EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(sourcePath) ? "(none)" : sourcePath);
        }
    }

    void DrawSettingsSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            extractionMode = (ExtractionMode)EditorGUILayout.EnumPopup("Extraction Mode", extractionMode);

            EditorGUILayout.LabelField("Segmentation", EditorStyles.boldLabel);
            backgroundMode = (BackgroundMode)EditorGUILayout.EnumPopup("Background Mode", backgroundMode);
            alphaThreshold = EditorGUILayout.IntSlider("Alpha Threshold", alphaThreshold, 0, 255);

            if (backgroundMode == BackgroundMode.LightKey)
                lightThreshold = EditorGUILayout.IntSlider("Light Threshold", lightThreshold, 0, 255);
            else
                cornerTolerance = EditorGUILayout.IntSlider("Corner Tolerance", cornerTolerance, 0, 128);

            componentPadding = EditorGUILayout.IntSlider("Export Padding", componentPadding, 0, 24);
            minComponentPixels = Mathf.Max(1, EditorGUILayout.IntField("Min Component Pixels", minComponentPixels));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Recovery", EditorStyles.boldLabel);
            recoverySeedTolerance = EditorGUILayout.IntSlider("Seed Recovery", recoverySeedTolerance, 0, 128);
            recoveryGrowTolerance = EditorGUILayout.IntSlider("Grow Recovery", recoveryGrowTolerance, 0, 64);
            backgroundRejectTolerance = EditorGUILayout.IntSlider("Background Reject", backgroundRejectTolerance, 0, 64);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Button Split", EditorStyles.boldLabel);
            plateSeedTolerance = EditorGUILayout.IntSlider("Plate Seed Tolerance", plateSeedTolerance, 0, 128);
            plateGrowTolerance = EditorGUILayout.IntSlider("Plate Grow Tolerance", plateGrowTolerance, 0, 64);
            minDetailPixels = Mathf.Max(1, EditorGUILayout.IntField("Min Detail Pixels", minDetailPixels));

            if (extractionMode == ExtractionMode.FocusedRect)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Focused Rect", EditorStyles.boldLabel);
                focusRect.x = EditorGUILayout.IntField("Rect X", focusRect.x);
                focusRect.y = EditorGUILayout.IntField("Rect Y", focusRect.y);
                focusRect.width = Mathf.Max(8, EditorGUILayout.IntField("Rect Width", focusRect.width));
                focusRect.height = Mathf.Max(8, EditorGUILayout.IntField("Rect Height", focusRect.height));
                focusedMaskMargin = EditorGUILayout.IntSlider("Mask Margin", focusedMaskMargin, 0, 32);
                focusedBackgroundColorTolerance = EditorGUILayout.IntSlider("BG Color Tol", focusedBackgroundColorTolerance, 4, 80);
                focusedBackgroundBrightnessTolerance = EditorGUILayout.IntSlider("BG Bright Tol", focusedBackgroundBrightnessTolerance, 4, 80);
                autoSnapFocusedRect = EditorGUILayout.Toggle("Auto Snap Frame", autoSnapFocusedRect);
                if (autoSnapFocusedRect)
                {
                    frameSearchMargin = EditorGUILayout.IntSlider("Frame Search Margin", frameSearchMargin, 4, 96);
                    frameDarkThreshold = EditorGUILayout.IntSlider("Frame Dark Threshold", frameDarkThreshold, 0, 180);
                    frameCoverageThreshold = EditorGUILayout.Slider("Frame Coverage", frameCoverageThreshold, 0.25f, 0.95f);
                    frameSnapPadding = EditorGUILayout.IntSlider("Frame Snap Padding", frameSnapPadding, 0, 12);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preset Match", GUILayout.Height(22f)))
                        ApplyFocusPreset(GetFocusedRectPresets()[0]);
                    if (GUILayout.Button("Preset Custom", GUILayout.Height(22f)))
                        ApplyFocusPreset(GetFocusedRectPresets()[1]);
                    if (GUILayout.Button("Preset Conquest", GUILayout.Height(22f)))
                        ApplyFocusPreset(GetFocusedRectPresets()[2]);
                }
            }
        }
    }

    void DrawActionBar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !string.IsNullOrEmpty(sourcePath);
            if (GUILayout.Button("Process", GUILayout.Height(32f)))
                ProcessImage();

            GUI.enabled = !string.IsNullOrEmpty(sourcePath) && extractionMode == ExtractionMode.FocusedRect;
            if (GUILayout.Button("Process Focused Rect", GUILayout.Height(32f)))
                ProcessFocusedRectOnly();

            GUI.enabled = compositePreview != null;
            if (GUILayout.Button("Export Composite", GUILayout.Height(32f)))
                ExportComposite();

            GUI.enabled = elements.Count > 0;
            if (GUILayout.Button("Export Elements", GUILayout.Height(32f)))
                ExportAllElements();

            GUI.enabled = true;
        }
    }

    void DrawPreviewSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewPane("Source", sourcePreview, 220f);
                DrawPreviewPane("Composite", compositePreview, 220f);
            }
        }
    }

    void DrawElementsSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"Detected Elements ({elements.Count})", EditorStyles.boldLabel);
            if (elements.Count == 0)
            {
                EditorGUILayout.LabelField("No extracted elements yet.");
                return;
            }

            for (int i = 0; i < elements.Count; i++)
            {
                ExtractedElement element = elements[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(element.Name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"Bounds {element.TightBounds.width} x {element.TightBounds.height} | Pixels {element.PixelCount} | Plate {element.PlatePixelCount} | Detail {element.DetailPixelCount}");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawPreviewPane("Full", element.FullTexture, 120f);
                        DrawPreviewPane("Plate", element.PlateTexture, 120f);
                        DrawPreviewPane("Detail", element.DetailTexture, 120f);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Export Full", GUILayout.Height(24f)))
                            ExportTextureVariant(element, element.FullTexture, "full");

                        GUI.enabled = element.PlateTexture != null;
                        if (GUILayout.Button("Export Plate", GUILayout.Height(24f)))
                            ExportTextureVariant(element, element.PlateTexture, "plate");

                        GUI.enabled = element.DetailTexture != null;
                        if (GUILayout.Button("Export Detail", GUILayout.Height(24f)))
                            ExportTextureVariant(element, element.DetailTexture, "detail");

                        GUI.enabled = true;
                    }
                }
            }
        }
    }

    void DrawPreviewPane(string label, Texture2D texture, float height)
    {
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(rect, "No Preview", EditorStyles.centeredGreyMiniLabel);
        }
    }

    void LoadFromFile()
    {
        string defaultDirectory = string.IsNullOrEmpty(sourcePath)
            ? Application.dataPath
            : Path.GetDirectoryName(ResolveFullPath(sourcePath));
        string path = EditorUtility.OpenFilePanel("Choose image", defaultDirectory, "png,jpg,jpeg,tga,bmp");
        if (string.IsNullOrEmpty(path))
            return;

        sourceAsset = null;
        sourcePath = path;
        ClearResults();
        SetStatus("Image file loaded.", MessageType.Info);
        Repaint();
    }

    void UseSelectedTexture()
    {
        if (Selection.activeObject is Texture2D texture)
        {
            sourceAsset = texture;
            sourcePath = AssetDatabase.GetAssetPath(texture);
            ClearResults();
            SetStatus("Using selected texture asset.", MessageType.Info);
            Repaint();
        }
        else
        {
            SetStatus("Select a Texture2D first.", MessageType.Warning);
        }
    }

    void PickOutputFolder()
    {
        string path = EditorUtility.OpenFolderPanel("Choose output folder", ResolveFullPath(outputFolder), string.Empty);
        if (string.IsNullOrEmpty(path))
            return;

        outputFolder = path;
        EditorPrefs.SetString(PrefOutputFolder, outputFolder);
    }

    void OpenOutputFolder()
    {
        string folder = EnsureOutputFolder();
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    void ResetAll()
    {
        sourceAsset = null;
        sourcePath = string.Empty;
        ClearResults();
        SetStatus("Cleared.", MessageType.Info);
        Repaint();
    }

    void ProcessImage()
    {
        ClearResults();

        if (!TryLoadSourceTexture(out Texture2D sourceTexture))
            return;

        try
        {
            sourcePreview = DuplicateTexture(sourceTexture, "SourcePreview");

            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            ProcessImagePixels(sourcePixels, width, height, "Done.");
        }
        finally
        {
            DestroyImmediate(sourceTexture);
        }

        Repaint();
    }

    void ProcessFocusedRectOnly()
    {
        ClearResults();

        if (!TryLoadSourceTexture(out Texture2D sourceTexture))
            return;

        try
        {
            sourcePreview = DuplicateTexture(sourceTexture, "SourcePreview");
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();
            if (TryBuildFocusedElement(sourcePixels, width, height, focusRect, "Focused", out ExtractedElement element, out RectInt snappedRect))
            {
                elements.Add(element);
                compositePreview = CreateTextureFromRect(sourcePixels, width, snappedRect, "FocusedCompositePreview");
                SetStatus(
                    autoSnapFocusedRect
                        ? $"Focused extraction done. Snapped to {snappedRect.x},{snappedRect.y},{snappedRect.width},{snappedRect.height}."
                        : $"Focused extraction done. Used exact rect {snappedRect.x},{snappedRect.y},{snappedRect.width},{snappedRect.height}.",
                    MessageType.Info);
            }
            else
            {
                SetStatus(
                    autoSnapFocusedRect
                        ? "Focused extraction could not lock onto a full frame. Try expanding the rect or raising frame coverage."
                        : "Focused extraction failed. Check the rect size and image bounds.",
                    MessageType.Warning);
            }
        }
        finally
        {
            DestroyImmediate(sourceTexture);
        }

        Repaint();
    }

    void ProcessImagePixels(Color32[] sourcePixels, int width, int height, string successPrefix)
    {
        if (extractionMode == ExtractionMode.FocusedRect)
        {
            if (TryBuildFocusedElement(sourcePixels, width, height, focusRect, "Focused", out ExtractedElement focusedElement, out RectInt snappedRect))
            {
                elements.Add(focusedElement);
                compositePreview = CreateTextureFromRect(sourcePixels, width, snappedRect, "FocusedCompositePreview");
                SetStatus(
                    autoSnapFocusedRect
                        ? $"{successPrefix} Focused extraction locked onto {snappedRect.width} x {snappedRect.height}."
                        : $"{successPrefix} Focused extraction exported the exact rect {snappedRect.width} x {snappedRect.height}.",
                    MessageType.Info);
            }
            else
            {
                SetStatus(
                    autoSnapFocusedRect
                        ? "Focused extraction could not lock onto a full frame. Try expanding the rect or raising frame coverage."
                        : "Focused extraction failed. Check the rect size and image bounds.",
                    MessageType.Warning);
            }
            return;
        }

        bool[] backgroundMask = BuildBackgroundMask(sourcePixels, width, height);
        Color32[] processedPixels = ApplyExternalMask(sourcePixels, backgroundMask);

        if (TryGetForegroundBounds(processedPixels, width, height, alphaThreshold, out RectInt compositeBounds))
            compositePreview = CreateTextureFromRect(processedPixels, width, compositeBounds, "CompositePreview");

        List<ExtractedElement> extracted = ExtractElements(processedPixels, width, height);
        elements.AddRange(extracted);

        if (elements.Count == 0)
        {
            SetStatus("No foreground elements found. Try relaxing the background thresholds.", MessageType.Warning);
        }
        else
        {
            SetStatus(
                $"{successPrefix} Found {elements.Count} element(s). Each element exports full art, plate, and inner detail when detected.",
                MessageType.Info);
        }
    }

    bool TryLoadSourceTexture(out Texture2D texture)
    {
        texture = null;

        if (string.IsNullOrEmpty(sourcePath))
        {
            SetStatus("Choose an image first.", MessageType.Warning);
            return false;
        }

        string fullPath = ResolveFullPath(sourcePath);
        if (!File.Exists(fullPath))
        {
            SetStatus("Source file not found.", MessageType.Error);
            return false;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!ImageConversion.LoadImage(texture, bytes, false))
        {
            DestroyImmediate(texture);
            texture = null;
            SetStatus("Failed to decode the image file.", MessageType.Error);
            return false;
        }

        return true;
    }

    bool[] BuildBackgroundMask(Color32[] pixels, int width, int height)
    {
        bool[] background = new bool[width * height];
        bool[] visited = new bool[width * height];
        Stack<int> stack = new Stack<int>();
        Color32[] cornerSamples = GetCornerSamples(pixels, width, height);

        void TrySeed(int x, int y)
        {
            int index = y * width + x;
            if (visited[index])
                return;

            if (!IsBackgroundPixel(pixels[index], cornerSamples))
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

        return background;
    }

    bool IsBackgroundPixel(Color32 pixel, Color32[] cornerSamples)
    {
        if (pixel.a <= alphaThreshold)
            return true;

        if (backgroundMode == BackgroundMode.LightKey)
            return pixel.r >= lightThreshold && pixel.g >= lightThreshold && pixel.b >= lightThreshold;

        for (int i = 0; i < cornerSamples.Length; i++)
        {
            if (ColorDistanceSquared(pixel, cornerSamples[i]) <= cornerTolerance * cornerTolerance)
                return true;
        }

        return false;
    }

    static Color32[] GetCornerSamples(Color32[] pixels, int width, int height)
    {
        return new[]
        {
            pixels[0],
            pixels[width - 1],
            pixels[(height - 1) * width],
            pixels[height * width - 1],
        };
    }

    static Color32[] ApplyExternalMask(Color32[] sourcePixels, bool[] backgroundMask)
    {
        Color32[] output = new Color32[sourcePixels.Length];
        for (int i = 0; i < sourcePixels.Length; i++)
        {
            Color32 pixel = sourcePixels[i];
            if (backgroundMask[i])
                pixel.a = 0;
            output[i] = pixel;
        }

        return output;
    }

    List<ExtractedElement> ExtractElements(Color32[] pixels, int width, int height)
    {
        List<ExtractedElement> results = new List<ExtractedElement>();
        bool[] visited = new bool[width * height];

        for (int index = 0; index < pixels.Length; index++)
        {
            if (visited[index] || pixels[index].a <= alphaThreshold)
                continue;

            List<ComponentPixel> componentPixels = CollectComponent(pixels, width, height, index, visited, out RectInt tightBounds);
            if (componentPixels.Count < minComponentPixels)
                continue;

            ExtractedElement element = BuildExtractedElement(results.Count + 1, pixels, width, height, componentPixels, tightBounds);
            results.Add(element);
        }

        results.Sort((a, b) =>
        {
            int yCompare = a.TightBounds.y.CompareTo(b.TightBounds.y);
            return yCompare != 0 ? yCompare : a.TightBounds.x.CompareTo(b.TightBounds.x);
        });

        return results;
    }

    bool TryBuildFocusedElement(
        Color32[] pixels,
        int width,
        int height,
        RectInt approxRect,
        string elementName,
        out ExtractedElement element,
        out RectInt snappedRect)
    {
        element = null;
        snappedRect = default;

        RectInt pixelSpaceApprox = ConvertTopLeftRectToPixelRect(approxRect, height);
        RectInt clampedApprox = ClampRectToImage(pixelSpaceApprox, width, height);
        if (clampedApprox.width < 8 || clampedApprox.height < 8)
            return false;

        snappedRect = clampedApprox;
        if (autoSnapFocusedRect)
        {
            if (!TrySnapFocusRectToFrame(pixels, width, height, clampedApprox, out snappedRect))
                return false;
        }

        if (autoSnapFocusedRect)
        {
            List<ComponentPixel> componentPixels = BuildFocusedComponentPixels(snappedRect);
            element = BuildExtractedElement(
                1,
                pixels,
                width,
                height,
                componentPixels,
                snappedRect,
                elementName,
                0);
        }
        else
        {
            element = BuildMaskedFocusedElement(pixels, width, height, snappedRect, elementName, out snappedRect);
        }

        return element != null;
    }

    bool TrySnapFocusRectToFrame(
        Color32[] pixels,
        int width,
        int height,
        RectInt approxRect,
        out RectInt snappedRect)
    {
        RectInt searchRect = ExpandBounds(approxRect, width, height, frameSearchMargin);
        float bestScore = float.MinValue;
        snappedRect = default;

        List<EdgeCandidate> topCandidates = CollectHorizontalEdgeCandidates(pixels, width, searchRect, true, approxRect.width);
        List<EdgeCandidate> bottomCandidates = CollectHorizontalEdgeCandidates(pixels, width, searchRect, false, approxRect.width);
        List<EdgeCandidate> leftCandidates = CollectVerticalEdgeCandidates(pixels, width, searchRect, true, approxRect.height);
        List<EdgeCandidate> rightCandidates = CollectVerticalEdgeCandidates(pixels, width, searchRect, false, approxRect.height);

        for (int ti = 0; ti < topCandidates.Count; ti++)
        {
            for (int bi = 0; bi < bottomCandidates.Count; bi++)
            {
                int top = Mathf.Min(topCandidates[ti].Position, bottomCandidates[bi].Position);
                int bottom = Mathf.Max(topCandidates[ti].Position, bottomCandidates[bi].Position);
                if (bottom - top + 1 < approxRect.height * 0.75f)
                    continue;

                for (int li = 0; li < leftCandidates.Count; li++)
                {
                    for (int ri = 0; ri < rightCandidates.Count; ri++)
                    {
                        int left = Mathf.Min(leftCandidates[li].Position, rightCandidates[ri].Position);
                        int right = Mathf.Max(leftCandidates[li].Position, rightCandidates[ri].Position);
                        RectInt candidate = RectFromMinMax(left, top, right, bottom);
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
            }
        }

        if (bestScore <= 0f)
            return false;

        snappedRect = ExpandBounds(snappedRect, width, height, frameSnapPadding);
        return true;
    }

    List<EdgeCandidate> CollectHorizontalEdgeCandidates(Color32[] pixels, int width, RectInt searchRect, bool fromTop, int approxWidth)
    {
        List<EdgeCandidate> candidates = new List<EdgeCandidate>();
        int start = fromTop ? searchRect.y : searchRect.yMax - 1;
        int end = fromTop ? searchRect.yMax : searchRect.y - 1;
        int step = fromTop ? 1 : -1;

        for (int y = start; y != end; y += step)
        {
            int darkCount = 0;
            int sampleCount = 0;
            for (int x = searchRect.x; x < searchRect.xMax; x++)
            {
                sampleCount++;
                if (IsDarkFramePixel(pixels[y * width + x]))
                    darkCount++;
            }

            float coverage = sampleCount > 0 ? darkCount / (float)sampleCount : 0f;
            if (coverage >= frameCoverageThreshold * 0.85f)
                candidates.Add(new EdgeCandidate(y, coverage));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        TrimEdgeCandidates(candidates, 10, approxWidth / 6);
        return candidates;
    }

    List<EdgeCandidate> CollectVerticalEdgeCandidates(Color32[] pixels, int width, RectInt searchRect, bool fromLeft, int approxHeight)
    {
        List<EdgeCandidate> candidates = new List<EdgeCandidate>();
        int start = fromLeft ? searchRect.x : searchRect.xMax - 1;
        int end = fromLeft ? searchRect.xMax : searchRect.x - 1;
        int step = fromLeft ? 1 : -1;

        for (int x = start; x != end; x += step)
        {
            int darkCount = 0;
            int sampleCount = 0;
            for (int y = searchRect.y; y < searchRect.yMax; y++)
            {
                sampleCount++;
                if (IsDarkFramePixel(pixels[y * width + x]))
                    darkCount++;
            }

            float coverage = sampleCount > 0 ? darkCount / (float)sampleCount : 0f;
            if (coverage >= frameCoverageThreshold * 0.85f)
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

        List<EdgeCandidate> filtered = new List<EdgeCandidate>(maxCount);
        for (int i = 0; i < candidates.Count; i++)
        {
            bool tooClose = false;
            for (int j = 0; j < filtered.Count; j++)
            {
                if (Mathf.Abs(filtered[j].Position - candidates[i].Position) <= minSpacing)
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
        if (candidate.width < approxRect.width * 0.75f || candidate.height < approxRect.height * 0.75f)
            return false;

        if (candidate.width > approxRect.width + frameSearchMargin * 2 + 24)
            return false;

        if (candidate.height > approxRect.height + frameSearchMargin * 2 + 24)
            return false;

        return true;
    }

    float ScoreFocusedFrame(Color32[] pixels, int width, RectInt rect, RectInt targetRect)
    {
        int darkEdges = 0;
        int totalEdges = 0;
        int interiorBright = 0;
        int interiorSamples = 0;

        for (int x = rect.x; x < rect.xMax; x++)
        {
            if (IsDarkFramePixel(pixels[rect.y * width + x])) darkEdges++;
            if (IsDarkFramePixel(pixels[(rect.yMax - 1) * width + x])) darkEdges++;
            totalEdges += 2;
        }

        for (int y = rect.y + 1; y < rect.yMax - 1; y++)
        {
            if (IsDarkFramePixel(pixels[y * width + rect.x])) darkEdges++;
            if (IsDarkFramePixel(pixels[y * width + rect.xMax - 1])) darkEdges++;
            totalEdges += 2;
        }

        float edgeCoverage = totalEdges > 0 ? darkEdges / (float)totalEdges : 0f;
        if (edgeCoverage < frameCoverageThreshold)
            return -1f;

        int innerLeft = Mathf.Min(rect.xMax - 1, rect.x + Mathf.Max(6, rect.width / 8));
        int innerRight = Mathf.Max(innerLeft + 1, rect.xMax - Mathf.Max(6, rect.width / 8));
        int innerTop = Mathf.Min(rect.yMax - 1, rect.y + Mathf.Max(6, rect.height / 8));
        int innerBottom = Mathf.Max(innerTop + 1, rect.yMax - Mathf.Max(6, rect.height / 8));

        for (int y = innerTop; y < innerBottom; y += 3)
        {
            for (int x = innerLeft; x < innerRight; x += 3)
            {
                interiorSamples++;
                if (PixelBrightness(pixels[y * width + x]) >= frameDarkThreshold)
                    interiorBright++;
            }
        }

        float interiorBrightness = interiorSamples > 0 ? interiorBright / (float)interiorSamples : 0f;
        float centerOffset =
            Mathf.Abs(rect.center.x - targetRect.center.x) * 0.01f
            + Mathf.Abs(rect.center.y - targetRect.center.y) * 0.02f;
        return edgeCoverage * 3f
            + interiorBrightness
            - Mathf.Abs(rect.width - targetRect.width) * 0.002f
            - Mathf.Abs(rect.height - targetRect.height) * 0.006f
            - centerOffset;
    }

    bool IsDarkFramePixel(Color32 pixel)
    {
        return PixelBrightness(pixel) <= frameDarkThreshold;
    }

    static int PixelBrightness(Color32 pixel)
    {
        return (pixel.r * 30 + pixel.g * 59 + pixel.b * 11) / 100;
    }

    static List<ComponentPixel> BuildFocusedComponentPixels(RectInt bounds)
    {
        List<ComponentPixel> componentPixels = new List<ComponentPixel>(bounds.width * bounds.height);
        for (int y = bounds.y; y < bounds.yMax; y++)
        {
            for (int x = bounds.x; x < bounds.xMax; x++)
                componentPixels.Add(new ComponentPixel(x, y));
        }

        return componentPixels;
    }

    ExtractedElement BuildMaskedFocusedElement(Color32[] pixels, int width, int height, RectInt exactRect, string elementName, out RectInt snappedRect)
    {
        snappedRect = exactRect;
        if (TrySnapFocusRectToFrame(pixels, width, height, exactRect, out RectInt refinedRect))
            snappedRect = refinedRect;

        Color32[] fullPixels = CopyRectPixels(pixels, width, snappedRect);
        return new ExtractedElement
        {
            Name = elementName,
            TightBounds = snappedRect,
            ExportBounds = snappedRect,
            PixelCount = snappedRect.width * snappedRect.height,
            PlatePixelCount = 0,
            DetailPixelCount = 0,
            FullTexture = CreateTexture(fullPixels, snappedRect.width, snappedRect.height, $"{elementName}_Full"),
            PlateTexture = null,
            DetailTexture = null,
        };
    }

    bool[] BuildFocusedForegroundMask(Color32[] pixels, int width, RectInt expandedRect, RectInt exactRect)
    {
        int localWidth = expandedRect.width;
        int localHeight = expandedRect.height;
        bool[] visited = new bool[localWidth * localHeight];
        bool[] backgroundMask = new bool[localWidth * localHeight];
        Stack<int> stack = new Stack<int>();

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
            Color32 current = pixels[(expandedRect.y + y) * width + expandedRect.x + x];
            int currentBrightness = PixelBrightness(current);

            Visit(x - 1, y, current, currentBrightness);
            Visit(x + 1, y, current, currentBrightness);
            Visit(x, y - 1, current, currentBrightness);
            Visit(x, y + 1, current, currentBrightness);
        }

        bool[] foregroundMask = new bool[localWidth * localHeight];
        int exactLocalX = exactRect.x - expandedRect.x;
        int exactLocalY = exactRect.y - expandedRect.y;

        for (int y = 0; y < exactRect.height; y++)
        {
            for (int x = 0; x < exactRect.width; x++)
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
            Color32 candidate = pixels[(expandedRect.y + y) * width + expandedRect.x + x];
            if (!LooksLikeFocusedBackground(current, currentBrightness, candidate))
                return;

            backgroundMask[localIndex] = true;
            stack.Push(localIndex);
        }
    }

    bool LooksLikeFocusedBackground(Color32 current, int currentBrightness, Color32 candidate)
    {
        int colorDelta =
            Mathf.Abs(current.r - candidate.r)
            + Mathf.Abs(current.g - candidate.g)
            + Mathf.Abs(current.b - candidate.b);
        int brightnessDelta = Mathf.Abs(PixelBrightness(candidate) - currentBrightness);

        return colorDelta <= focusedBackgroundColorTolerance
            && brightnessDelta <= focusedBackgroundBrightnessTolerance;
    }

    List<ComponentPixel> CollectComponent(Color32[] pixels, int width, int height, int startIndex, bool[] visited, out RectInt tightBounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        List<ComponentPixel> points = new List<ComponentPixel>(256);
        Stack<int> stack = new Stack<int>();
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
            if (visited[index] || pixels[index].a <= alphaThreshold)
                return;

            visited[index] = true;
            stack.Push(index);
        }
    }

    ExtractedElement BuildExtractedElement(
        int index,
        Color32[] pixels,
        int width,
        int height,
        List<ComponentPixel> componentPixels,
        RectInt tightBounds)
    {
        return BuildExtractedElement(index, pixels, width, height, componentPixels, tightBounds, $"Element_{index:00}", -1);
    }

    ExtractedElement BuildExtractedElement(
        int index,
        Color32[] pixels,
        int width,
        int height,
        List<ComponentPixel> componentPixels,
        RectInt tightBounds,
        string elementName)
    {
        return BuildExtractedElement(index, pixels, width, height, componentPixels, tightBounds, elementName, -1);
    }

    ExtractedElement BuildExtractedElement(
        int index,
        Color32[] pixels,
        int width,
        int height,
        List<ComponentPixel> componentPixels,
        RectInt tightBounds,
        string elementName,
        int exportPaddingOverride)
    {
        int exportPadding = exportPaddingOverride >= 0 ? exportPaddingOverride : componentPadding;
        RectInt exportBounds = ExpandBounds(tightBounds, width, height, exportPadding);
        bool[] seedMask = BuildComponentMask(componentPixels, exportBounds);
        bool[] componentMask = RecoverComponentMask(pixels, width, exportBounds, seedMask);
        bool[] plateMask = BuildPlateMask(pixels, width, componentMask, exportBounds);

        Color32[] fullPixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Full);
        Color32[] platePixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Plate);
        Color32[] detailPixels = CopyVariantRectPixels(pixels, width, exportBounds, componentMask, plateMask, LayerVariant.Detail);

        RectInt finalBounds = TryGetMaskBounds(componentMask, exportBounds.width, exportBounds.height, out RectInt localRecoveredBounds)
            ? new RectInt(exportBounds.x + localRecoveredBounds.x, exportBounds.y + localRecoveredBounds.y, localRecoveredBounds.width, localRecoveredBounds.height)
            : tightBounds;

        int platePixelsCount = CountOpaquePixels(platePixels);
        int detailPixelsCount = CountOpaquePixels(detailPixels);

        Texture2D plateTexture = platePixelsCount > 0 ? CreateTexture(platePixels, exportBounds.width, exportBounds.height, $"Element_{index:00}_Plate") : null;
        Texture2D detailTexture = detailPixelsCount >= minDetailPixels ? CreateTexture(detailPixels, exportBounds.width, exportBounds.height, $"Element_{index:00}_Detail") : null;

        if (detailTexture == null)
            detailPixelsCount = 0;
        if (platePixelsCount == 0)
        {
            plateTexture = null;
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
            FullTexture = CreateTexture(fullPixels, exportBounds.width, exportBounds.height, $"Element_{index:00}_Full"),
            PlateTexture = plateTexture,
            DetailTexture = detailTexture,
        };
    }

    static bool[] BuildComponentMask(List<ComponentPixel> componentPixels, RectInt bounds)
    {
        int localWidth = bounds.width;
        int localHeight = bounds.height;
        bool[] componentMask = new bool[localWidth * localHeight];
        for (int i = 0; i < componentPixels.Count; i++)
        {
            ComponentPixel pixel = componentPixels[i];
            int localX = pixel.X - bounds.x;
            int localY = pixel.Y - bounds.y;
            componentMask[localY * localWidth + localX] = true;
        }

        return componentMask;
    }

    bool[] RecoverComponentMask(Color32[] pixels, int width, RectInt bounds, bool[] seedMask)
    {
        bool[] recoveredMask = new bool[seedMask.Length];
        Array.Copy(seedMask, recoveredMask, seedMask.Length);

        int localWidth = bounds.width;
        int localHeight = bounds.height;
        List<Vector2Int> edgeSeeds = new List<Vector2Int>();
        List<Color32> seedColors = new List<Color32>();
        List<Color32> backgroundSamples = new List<Color32>();

        for (int y = 0; y < localHeight; y++)
        {
            for (int x = 0; x < localWidth; x++)
            {
                int localIndex = y * localWidth + x;
                if (seedMask[localIndex])
                {
                    if (IsMaskEdge(seedMask, localWidth, localHeight, x, y))
                    {
                        edgeSeeds.Add(new Vector2Int(x, y));
                        seedColors.Add(pixels[(bounds.y + y) * width + bounds.x + x]);
                    }
                }
                else if (x == 0 || y == 0 || x == localWidth - 1 || y == localHeight - 1)
                {
                    backgroundSamples.Add(pixels[(bounds.y + y) * width + bounds.x + x]);
                }
            }
        }

        if (edgeSeeds.Count == 0)
            return recoveredMask;

        Color32 averageSeed = AverageColor(seedColors);
        Color32 averageBackground = AverageColor(backgroundSamples);
        Stack<Vector2Int> stack = new Stack<Vector2Int>(edgeSeeds);

        while (stack.Count > 0)
        {
            Vector2Int point = stack.Pop();
            Color32 current = pixels[(bounds.y + point.y) * width + bounds.x + point.x];

            Visit(point.x - 1, point.y, current);
            Visit(point.x + 1, point.y, current);
            Visit(point.x, point.y - 1, current);
            Visit(point.x, point.y + 1, current);
            Visit(point.x - 1, point.y - 1, current);
            Visit(point.x + 1, point.y - 1, current);
            Visit(point.x - 1, point.y + 1, current);
            Visit(point.x + 1, point.y + 1, current);
        }

        return recoveredMask;

        void Visit(int x, int y, Color32 current)
        {
            if (x < 0 || x >= localWidth || y < 0 || y >= localHeight)
                return;

            int localIndex = y * localWidth + x;
            if (recoveredMask[localIndex])
                return;

            Color32 candidate = pixels[(bounds.y + y) * width + bounds.x + x];
            if (!ShouldRecoverPixel(candidate, current, averageSeed, averageBackground, backgroundSamples))
                return;

            recoveredMask[localIndex] = true;
            stack.Push(new Vector2Int(x, y));
        }
    }

    bool ShouldRecoverPixel(Color32 candidate, Color32 current, Color32 averageSeed, Color32 averageBackground, List<Color32> backgroundSamples)
    {
        if (candidate.a <= alphaThreshold)
            return false;

        int distToCurrent = ColorDistanceSquared(candidate, current);
        int distToSeed = ColorDistanceSquared(candidate, averageSeed);
        if (distToCurrent > recoveryGrowTolerance * recoveryGrowTolerance
            && distToSeed > recoverySeedTolerance * recoverySeedTolerance)
            return false;

        if (backgroundSamples == null || backgroundSamples.Count == 0)
            return true;

        int backgroundPenalty = backgroundRejectTolerance * backgroundRejectTolerance;
        int distToBackground = ColorDistanceSquared(candidate, averageBackground);
        if (distToSeed + backgroundPenalty < distToBackground)
            return true;

        int step = Mathf.Max(1, backgroundSamples.Count / 12);
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
        int localWidth = bounds.width;
        int localHeight = bounds.height;

        List<Vector2Int> seedPoints = new List<Vector2Int>();
        List<Color32> seedColors = new List<Color32>();
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
                        seedPoints.Add(new Vector2Int(x, y));
                        seedColors.Add(pixels[(bounds.y + y) * width + bounds.x + x]);
                    }
                    continue;
                }

                if (touchesOuterBand)
                {
                    seedPoints.Add(new Vector2Int(x, y));
                    seedColors.Add(pixels[(bounds.y + y) * width + bounds.x + x]);
                }
            }
        }

        if (seedPoints.Count == 0)
            return new bool[localWidth * localHeight];

        Color32 seedAverage = AverageColor(seedColors);
        bool[] plateMask = new bool[localWidth * localHeight];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();

        for (int i = 0; i < seedPoints.Count; i++)
        {
            Vector2Int point = seedPoints[i];
            int localIndex = point.y * localWidth + point.x;
            if (plateMask[localIndex])
                continue;

            plateMask[localIndex] = true;
            stack.Push(point);
        }

        while (stack.Count > 0)
        {
            Vector2Int point = stack.Pop();
            Color32 current = pixels[(bounds.y + point.y) * width + bounds.x + point.x];

            Visit(point.x - 1, point.y, current);
            Visit(point.x + 1, point.y, current);
            Visit(point.x, point.y - 1, current);
            Visit(point.x, point.y + 1, current);
        }

        return plateMask;

        void Visit(int x, int y, Color32 current)
        {
            if (x < 0 || x >= localWidth || y < 0 || y >= localHeight)
                return;

            int localIndex = y * localWidth + x;
            if (plateMask[localIndex] || !componentMask[localIndex])
                return;

            Color32 candidate = pixels[(bounds.y + y) * width + bounds.x + x];
            if (!LooksLikePlate(candidate, current, seedAverage, seedColors))
                return;

            plateMask[localIndex] = true;
            stack.Push(new Vector2Int(x, y));
        }
    }

    bool LooksLikePlate(Color32 candidate, Color32 current, Color32 averageSeed, List<Color32> seedColors)
    {
        if (ColorDistanceSquared(candidate, current) > plateGrowTolerance * plateGrowTolerance)
            return false;

        if (ColorDistanceSquared(candidate, averageSeed) <= plateSeedTolerance * plateSeedTolerance)
            return true;

        int step = Mathf.Max(1, seedColors.Count / 12);
        for (int i = 0; i < seedColors.Count; i += step)
        {
            if (ColorDistanceSquared(candidate, seedColors[i]) <= plateSeedTolerance * plateSeedTolerance)
                return true;
        }

        return false;
    }

    static Color32 AverageColor(List<Color32> colors)
    {
        if (colors == null || colors.Count == 0)
            return new Color32(0, 0, 0, 0);

        int r = 0;
        int g = 0;
        int b = 0;
        int a = 0;
        for (int i = 0; i < colors.Count; i++)
        {
            r += colors[i].r;
            g += colors[i].g;
            b += colors[i].b;
            a += colors[i].a;
        }

        return new Color32(
            (byte)(r / colors.Count),
            (byte)(g / colors.Count),
            (byte)(b / colors.Count),
            (byte)(a / colors.Count));
    }

    static RectInt ExpandBounds(RectInt bounds, int width, int height, int padding)
    {
        int minX = Mathf.Max(0, bounds.x - padding);
        int minY = Mathf.Max(0, bounds.y - padding);
        int maxX = Mathf.Min(width - 1, bounds.xMax - 1 + padding);
        int maxY = Mathf.Min(height - 1, bounds.yMax - 1 + padding);
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
                if (pixels[rowIndex + x].a <= alphaCutoff)
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
        Color32[] output = new Color32[bounds.width * bounds.height];
        for (int y = 0; y < bounds.height; y++)
        {
            int sourceIndex = (bounds.y + y) * sourceWidth + bounds.x;
            int targetIndex = y * bounds.width;
            Array.Copy(pixels, sourceIndex, output, targetIndex, bounds.width);
        }

        return output;
    }

    enum LayerVariant
    {
        Full,
        Plate,
        Detail,
    }

    static Color32[] CopyVariantRectPixels(
        Color32[] pixels,
        int sourceWidth,
        RectInt exportBounds,
        bool[] componentMask,
        bool[] plateMask,
        LayerVariant variant)
    {
        Color32[] output = new Color32[exportBounds.width * exportBounds.height];
        int localWidth = exportBounds.width;
        int localHeight = exportBounds.height;

        for (int y = 0; y < exportBounds.height; y++)
        {
            for (int x = 0; x < exportBounds.width; x++)
            {
                int globalX = exportBounds.x + x;
                int globalY = exportBounds.y + y;
                int outputIndex = y * exportBounds.width + x;
                Color32 pixel = pixels[globalY * sourceWidth + globalX];
                if (pixel.a == 0)
                {
                    output[outputIndex] = pixel;
                    continue;
                }

                int localX = x;
                int localY = y;
                bool insideComponent = localWidth > 0
                    && componentMask[localY * localWidth + localX];

                if (!insideComponent)
                {
                    pixel.a = 0;
                    output[outputIndex] = pixel;
                    continue;
                }

                bool isPlate = plateMask[localY * localWidth + localX];

                if ((variant == LayerVariant.Plate && !isPlate)
                    || (variant == LayerVariant.Detail && isPlate))
                    pixel.a = 0;

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
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 0)
                count++;
        }

        return count;
    }

    static int CountMaskPixels(bool[] mask)
    {
        int count = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i])
                count++;
        }

        return count;
    }

    static Texture2D DuplicateTexture(Texture2D texture, string name)
    {
        Texture2D duplicate = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
        duplicate.name = name;
        duplicate.SetPixels32(texture.GetPixels32());
        duplicate.Apply(false, false);
        return duplicate;
    }

    static Texture2D CreateTextureFromRect(Color32[] pixels, int sourceWidth, RectInt bounds, string name)
    {
        return CreateTexture(CopyRectPixels(pixels, sourceWidth, bounds), bounds.width, bounds.height, name);
    }

    static Texture2D CreateTexture(Color32[] pixels, int width, int height, string name)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.name = name;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    void ExportComposite()
    {
        if (compositePreview == null)
        {
            SetStatus("Nothing to export yet.", MessageType.Warning);
            return;
        }

        string folder = EnsureOutputFolder();
        WriteTexture(Path.Combine(folder, $"{GetBaseName()}_composite.png"), compositePreview);
        SetStatus("Composite exported.", MessageType.Info);
    }

    void ExportAllElements()
    {
        if (elements.Count == 0)
        {
            SetStatus("Nothing to export yet.", MessageType.Warning);
            return;
        }

        string folder = EnsureOutputFolder();
        for (int i = 0; i < elements.Count; i++)
        {
            ExportElementVariants(folder, elements[i]);
        }

        SetStatus($"Exported {elements.Count} extracted element(s).", MessageType.Info);
    }

    void ExportTextureVariant(ExtractedElement element, Texture2D texture, string suffix)
    {
        if (texture == null)
        {
            SetStatus($"No {suffix} layer available for {element.Name}.", MessageType.Warning);
            return;
        }

        string folder = EnsureOutputFolder();
        string path = Path.Combine(folder, $"{GetBaseName()}_{element.Name.ToLowerInvariant()}_{suffix}.png");
        WriteTexture(path, texture);
        SetStatus($"{element.Name} {suffix} exported.", MessageType.Info);
    }

    void ExportElementVariants(string folder, ExtractedElement element)
    {
        WriteTexture(Path.Combine(folder, $"{GetBaseName()}_{element.Name.ToLowerInvariant()}_full.png"), element.FullTexture);
        if (element.PlateTexture != null)
            WriteTexture(Path.Combine(folder, $"{GetBaseName()}_{element.Name.ToLowerInvariant()}_plate.png"), element.PlateTexture);
        if (element.DetailTexture != null)
            WriteTexture(Path.Combine(folder, $"{GetBaseName()}_{element.Name.ToLowerInvariant()}_detail.png"), element.DetailTexture);
    }

    void WriteTexture(string path, Texture2D texture)
    {
        File.WriteAllBytes(path, texture.EncodeToPNG());
    }

    string EnsureOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
            outputFolder = GetDefaultOutputFolder();

        outputFolder = ResolveFullPath(outputFolder);
        Directory.CreateDirectory(outputFolder);
        EditorPrefs.SetString(PrefOutputFolder, outputFolder);
        return outputFolder;
    }

    string GetBaseName()
    {
        if (!string.IsNullOrEmpty(sourcePath))
            return SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));

        return sourceAsset != null ? SanitizeFileName(sourceAsset.name) : "extracted";
    }

    static string GetDefaultOutputFolder()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, DefaultOutputFolderName);
    }

    static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "extracted" : value;
    }

    static string ResolveFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GetDefaultOutputFolder();

        if (Path.IsPathRooted(path))
            return path;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    static int ColorDistanceSquared(Color32 a, Color32 b)
    {
        int dr = a.r - b.r;
        int dg = a.g - b.g;
        int db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    void SetStatus(string message, MessageType type)
    {
        status = message;
        statusType = type;
    }

    void WriteBatchReport(string source)
    {
        string folder = EnsureOutputFolder();
        using (var writer = new StreamWriter(Path.Combine(folder, "report.txt"), false))
        {
            writer.WriteLine($"source={source}");
            writer.WriteLine($"elements={elements.Count}");
            for (int i = 0; i < elements.Count; i++)
            {
                ExtractedElement element = elements[i];
                writer.WriteLine(
                    $"{element.Name} tight={element.TightBounds.x},{element.TightBounds.y},{element.TightBounds.width},{element.TightBounds.height} export={element.ExportBounds.x},{element.ExportBounds.y},{element.ExportBounds.width},{element.ExportBounds.height} pixels={element.PixelCount} plate={element.PlatePixelCount} detail={element.DetailPixelCount}");
            }
        }
    }

    static string FindBatchTestSource()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string[] candidates =
        {
            Path.Combine(projectRoot, "Assets", "Resources", "LobbyGen", "_ref_lobby_master.png"),
            Path.Combine(projectRoot, "Assets", "ArtReferences", "ww2_mobile_hud_reference.png"),
            Path.Combine(projectRoot, "lobby_hall_final.png"),
            Path.Combine(projectRoot, "hud_final.png"),
            Path.Combine(projectRoot, "icons_preview.png"),
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
                return candidates[i];
        }

        return null;
    }

    void RunBatchScenarios(string source)
    {
        if (!TryLoadSourceTexture(out Texture2D sourceTexture))
            return;

        ExtractionMode previousMode = extractionMode;
        try
        {
            sourcePreview = DuplicateTexture(sourceTexture, "SourcePreview");
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color32[] sourcePixels = sourceTexture.GetPixels32();

            extractionMode = ExtractionMode.FocusedRect;
            autoSnapFocusedRect = false;
            List<ExtractedElement> batchElements = new List<ExtractedElement>();
            for (int i = 0; i < GetFocusedRectPresets().Length; i++)
            {
                FocusRegionPreset preset = GetFocusedRectPresets()[i];
                if (TryBuildFocusedElement(sourcePixels, width, height, preset.ApproxBounds, preset.Name, out ExtractedElement element, out RectInt snappedRect))
                {
                    element.Name = preset.Name;
                    batchElements.Add(element);
                    UnityEngine.Debug.Log($"[ArtLayerExtractor] Batch preset {preset.Name} snapped to {snappedRect.x},{snappedRect.y},{snappedRect.width},{snappedRect.height}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[ArtLayerExtractor] Batch preset {preset.Name} failed to snap to a frame.");
                }
            }

            elements.AddRange(batchElements);
            if (TryGetUnionBounds(elements, out RectInt unionBounds))
                compositePreview = CreateTextureFromRect(sourcePixels, width, unionBounds, "BatchCompositePreview");

            if (compositePreview != null)
                ExportComposite();
            if (elements.Count > 0)
                ExportAllElements();
            WriteBatchReport(source);
        }
        finally
        {
            extractionMode = previousMode;
            DestroyImmediate(sourceTexture);
        }
    }

    static FocusRegionPreset[] GetFocusedRectPresets()
    {
        return new[]
        {
            new FocusRegionPreset("Match", new RectInt(305, 156, 414, 148)),
            new FocusRegionPreset("Custom", new RectInt(305, 299, 414, 136)),
            new FocusRegionPreset("Conquest", new RectInt(305, 443, 414, 136)),
        };
    }

    void ApplyFocusPreset(FocusRegionPreset preset)
    {
        focusRect = preset.ApproxBounds;
        SetStatus($"Loaded preset {preset.Name}.", MessageType.Info);
    }

    static RectInt ClampRectToImage(RectInt rect, int width, int height)
    {
        int minX = Mathf.Clamp(rect.x, 0, width - 1);
        int minY = Mathf.Clamp(rect.y, 0, height - 1);
        int maxX = Mathf.Clamp(rect.xMax, minX + 1, width);
        int maxY = Mathf.Clamp(rect.yMax, minY + 1, height);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }

    static RectInt ConvertTopLeftRectToPixelRect(RectInt rect, int imageHeight)
    {
        int y = imageHeight - rect.y - rect.height;
        return new RectInt(rect.x, y, rect.width, rect.height);
    }

    static RectInt RectFromMinMax(int minX, int minY, int maxXInclusive, int maxYInclusive)
    {
        return new RectInt(minX, minY, maxXInclusive - minX + 1, maxYInclusive - minY + 1);
    }

    static bool TryGetUnionBounds(List<ExtractedElement> extractedElements, out RectInt bounds)
    {
        if (extractedElements == null || extractedElements.Count == 0)
        {
            bounds = default;
            return false;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int i = 0; i < extractedElements.Count; i++)
        {
            RectInt rect = extractedElements[i].ExportBounds;
            if (rect.x < minX) minX = rect.x;
            if (rect.y < minY) minY = rect.y;
            if (rect.xMax > maxX) maxX = rect.xMax;
            if (rect.yMax > maxY) maxY = rect.yMax;
        }

        bounds = new RectInt(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    void ClearResults()
    {
        DestroyPreview(ref sourcePreview);
        DestroyPreview(ref compositePreview);

        for (int i = 0; i < elements.Count; i++)
        {
            DestroyPreview(ref elements[i].FullTexture);
            DestroyPreview(ref elements[i].PlateTexture);
            DestroyPreview(ref elements[i].DetailTexture);
        }

        elements.Clear();
    }

    static void DestroyPreview(ref Texture2D texture)
    {
        if (texture != null)
            DestroyImmediate(texture);
        texture = null;
    }
}
