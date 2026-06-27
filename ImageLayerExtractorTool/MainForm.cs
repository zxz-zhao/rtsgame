using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ImageLayerExtractorTool;

internal sealed class MainForm : Form
{
    readonly ExtractionSettings settings = new();
    readonly ImageExtractor extractor;

    PictureBox sourcePreview = null!;
    PictureBox compositePreview = null!;
    FlowLayoutPanel elementsPanel = null!;
    TextBox sourcePathBox = null!;
    TextBox outputPathBox = null!;
    Label statusLabel = null!;
    ComboBox modeCombo = null!;
    ComboBox backgroundCombo = null!;
    CheckBox autoSnapCheck = null!;
    NumericUpDown alphaUpDown = null!;
    NumericUpDown lightUpDown = null!;
    NumericUpDown cornerToleranceUpDown = null!;
    NumericUpDown componentPaddingUpDown = null!;
    NumericUpDown minComponentUpDown = null!;
    NumericUpDown recoverySeedUpDown = null!;
    NumericUpDown recoveryGrowUpDown = null!;
    NumericUpDown backgroundRejectUpDown = null!;
    NumericUpDown plateSeedUpDown = null!;
    NumericUpDown plateGrowUpDown = null!;
    NumericUpDown minDetailUpDown = null!;
    NumericUpDown focusXUpDown = null!;
    NumericUpDown focusYUpDown = null!;
    NumericUpDown focusWUpDown = null!;
    NumericUpDown focusHUpDown = null!;
    NumericUpDown focusMaskMarginUpDown = null!;
    NumericUpDown focusBgColorUpDown = null!;
    NumericUpDown focusBgBrightUpDown = null!;
    NumericUpDown frameSearchMarginUpDown = null!;
    NumericUpDown frameDarkUpDown = null!;
    NumericUpDown frameCoverageUpDown = null!;
    NumericUpDown frameSnapPaddingUpDown = null!;

    Bitmap? currentSourceBitmap;
    Bitmap? currentCompositeBitmap;
    readonly List<ExtractedElement> currentElements = new();

    public MainForm()
    {
        extractor = new ImageExtractor(settings);
        Text = "Image Layer Extractor";
        Width = 1500;
        Height = 980;
        StartPosition = FormStartPosition.CenterScreen;
        BuildUi();
        SyncSettingsFromUi();
        UpdateFocusControlsEnabled();
        UpdateStatus("Open an image to begin.");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        DisposeBitmaps();
        base.OnFormClosed(e);
    }

    void BuildUi()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 500,
            Panel1MinSize = 440,
            Panel2MinSize = 760,
        };
        Controls.Add(root);

        var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        root.Panel1.Controls.Add(leftScroll);

        var leftStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
        };
        leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftScroll.Controls.Add(leftStack);

        AddRow(leftStack, "Source", BuildSourceRow());
        AddRow(leftStack, "Output", BuildOutputRow());
        AddRow(leftStack, "Mode", modeCombo = BuildCombo(new[] { "AutoForeground", "FocusedRect" }));
        AddRow(leftStack, "Background", backgroundCombo = BuildCombo(new[] { "LightKey", "CornerSample" }));
        AddRow(leftStack, "Alpha", alphaUpDown = BuildNumber(0, 255, settings.AlphaThreshold));
        AddRow(leftStack, "Light Thresh", lightUpDown = BuildNumber(0, 255, settings.LightThreshold));
        AddRow(leftStack, "Corner Tol", cornerToleranceUpDown = BuildNumber(0, 128, settings.CornerTolerance));
        AddRow(leftStack, "Pad", componentPaddingUpDown = BuildNumber(0, 24, settings.ComponentPadding));
        AddRow(leftStack, "Min Pixels", minComponentUpDown = BuildNumber(1, 100000, settings.MinComponentPixels));
        AddRow(leftStack, "Seed Recov", recoverySeedUpDown = BuildNumber(0, 128, settings.RecoverySeedTolerance));
        AddRow(leftStack, "Grow Recov", recoveryGrowUpDown = BuildNumber(0, 64, settings.RecoveryGrowTolerance));
        AddRow(leftStack, "Bg Reject", backgroundRejectUpDown = BuildNumber(0, 64, settings.BackgroundRejectTolerance));
        AddRow(leftStack, "Plate Seed", plateSeedUpDown = BuildNumber(0, 128, settings.PlateSeedTolerance));
        AddRow(leftStack, "Plate Grow", plateGrowUpDown = BuildNumber(0, 64, settings.PlateGrowTolerance));
        AddRow(leftStack, "Min Detail", minDetailUpDown = BuildNumber(1, 100000, settings.MinDetailPixels));
        AddRow(leftStack, "Auto Snap", autoSnapCheck = new CheckBox { Checked = settings.AutoSnapFocusedRect, AutoSize = true, Anchor = AnchorStyles.Left });
        AddRow(leftStack, "Focus X", focusXUpDown = BuildNumber(-100000, 100000, settings.FocusRect.X));
        AddRow(leftStack, "Focus Y", focusYUpDown = BuildNumber(-100000, 100000, settings.FocusRect.Y));
        AddRow(leftStack, "Focus W", focusWUpDown = BuildNumber(8, 100000, settings.FocusRect.Width));
        AddRow(leftStack, "Focus H", focusHUpDown = BuildNumber(8, 100000, settings.FocusRect.Height));
        AddRow(leftStack, "Mask Margin", focusMaskMarginUpDown = BuildNumber(0, 32, settings.FocusedMaskMargin));
        AddRow(leftStack, "BG Color Tol", focusBgColorUpDown = BuildNumber(4, 80, settings.FocusedBackgroundColorTolerance));
        AddRow(leftStack, "BG Bright Tol", focusBgBrightUpDown = BuildNumber(4, 80, settings.FocusedBackgroundBrightnessTolerance));
        AddRow(leftStack, "Frame Margin", frameSearchMarginUpDown = BuildNumber(4, 96, settings.FrameSearchMargin));
        AddRow(leftStack, "Frame Dark", frameDarkUpDown = BuildNumber(0, 180, settings.FrameDarkThreshold));
        AddRow(leftStack, "Frame Cover", frameCoverageUpDown = BuildNumber(25, 95, (int)(settings.FrameCoverageThreshold * 100f)));
        AddRow(leftStack, "Frame Pad", frameSnapPaddingUpDown = BuildNumber(0, 12, settings.FrameSnapPadding));

        modeCombo.SelectedIndexChanged += (_, _) =>
        {
            SyncSettingsFromUi();
            UpdateFocusControlsEnabled();
        };
        autoSnapCheck.CheckedChanged += (_, _) =>
        {
            SyncSettingsFromUi();
            UpdateFocusControlsEnabled();
        };

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 8, 0, 8) };
        var processBtn = new Button { Text = "Process", AutoSize = true };
        processBtn.Click += (_, _) => ProcessCurrentImage();
        var exportCompositeBtn = new Button { Text = "Export Composite", AutoSize = true };
        exportCompositeBtn.Click += (_, _) => ExportComposite();
        var exportAllBtn = new Button { Text = "Export Elements", AutoSize = true };
        exportAllBtn.Click += (_, _) => ExportAllElements();
        var openOutputBtn = new Button { Text = "Open Output", AutoSize = true };
        openOutputBtn.Click += (_, _) => OpenOutputFolder();
        actions.Controls.AddRange(new Control[] { processBtn, exportCompositeBtn, exportAllBtn, openOutputBtn });
        leftScroll.Controls.Add(actions);

        var presetRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 0, 0, 8) };
        foreach (var preset in ImageExtractor.GetFocusedRectPresets())
        {
            var button = new Button { Text = $"{preset.X},{preset.Y},{preset.Width}x{preset.Height}", AutoSize = true };
            button.Click += (_, _) =>
            {
                settings.FocusRect = preset;
                SyncSettingsToUi();
                UpdateFocusControlsEnabled();
                UpdateStatus("Loaded preset.");
            };
            presetRow.Controls.Add(button);
        }
        leftScroll.Controls.Add(presetRow);

        statusLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            Text = "Ready",
        };
        leftScroll.Controls.Add(statusLabel);

        var rightRoot = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
        };
        root.Panel2.Controls.Add(rightRoot);

        var previewPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previewPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rightRoot.Panel1.Controls.Add(previewPanel);

        sourcePreview = BuildPreviewBox("Source");
        compositePreview = BuildPreviewBox("Composite");
        previewPanel.Controls.Add(WrapPreview(sourcePreview), 0, 0);
        previewPanel.Controls.Add(WrapPreview(compositePreview), 1, 0);

        elementsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8),
        };
        rightRoot.Panel2.Controls.Add(elementsPanel);
    }

    Control BuildSourceRow()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        sourcePathBox = new TextBox { Width = 260, ReadOnly = true };
        var loadBtn = new Button { Text = "Load", AutoSize = true };
        loadBtn.Click += (_, _) => LoadImage();
        var clearBtn = new Button { Text = "Clear", AutoSize = true };
        clearBtn.Click += (_, _) => ClearSource();
        row.Controls.AddRange(new Control[] { sourcePathBox, loadBtn, clearBtn });
        return row;
    }

    Control BuildOutputRow()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        outputPathBox = new TextBox { Width = 260 };
        outputPathBox.Text = GetDefaultOutputFolder();
        var browseBtn = new Button { Text = "Browse", AutoSize = true };
        browseBtn.Click += (_, _) => BrowseOutputFolder();
        row.Controls.AddRange(new Control[] { outputPathBox, browseBtn });
        return row;
    }

    static ComboBox BuildCombo(string[] items)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        combo.Items.AddRange(items);
        combo.SelectedIndex = 0;
        return combo;
    }

    static NumericUpDown BuildNumber(decimal min, decimal max, decimal value)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Width = 160,
        };
    }

    static Control WrapPreview(PictureBox box)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        box.Dock = DockStyle.Fill;
        panel.Controls.Add(box);
        return panel;
    }

    static PictureBox BuildPreviewBox(string label)
    {
        return new PictureBox
        {
            BackColor = Color.FromArgb(30, 30, 30),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Tag = label,
        };
    }

    static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        int rowIndex = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 0, 0),
        };
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        table.Controls.Add(labelControl, 0, rowIndex);
        table.Controls.Add(control, 1, rowIndex);
    }

    void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tga|All files|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        sourcePathBox.Text = dialog.FileName;
        LoadSourceBitmap(dialog.FileName);
    }

    void LoadSourceBitmap(string path)
    {
        ClearRenderedResults();
        DisposeBitmap(ref currentSourceBitmap);
        using var temp = new Bitmap(path);
        currentSourceBitmap = new Bitmap(temp);
        sourcePreview.Image = currentSourceBitmap;
        UpdateStatus($"Loaded {Path.GetFileName(path)}.");
    }

    void ClearSource()
    {
        sourcePathBox.Clear();
        ClearRenderedResults();
        DisposeBitmaps();
        UpdateStatus("Cleared.");
    }

    void BrowseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = outputPathBox.Text,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            outputPathBox.Text = dialog.SelectedPath;
    }

    void ProcessCurrentImage()
    {
        if (currentSourceBitmap == null)
        {
            UpdateStatus("Load an image first.");
            return;
        }

        SyncSettingsFromUi();
        ClearRenderedResults();

        using Bitmap sourceCopy = new(currentSourceBitmap);
        ExtractionResult result = extractor.Process(sourceCopy);
        currentElements.AddRange(result.Elements);
        UpdateStatus(result.StatusMessage);

        if (result.CompositeBitmap != null)
        {
            currentCompositeBitmap = result.CompositeBitmap;
            compositePreview.Image = currentCompositeBitmap;
        }
        else
        {
            compositePreview.Image = null;
            DisposeBitmap(ref currentCompositeBitmap);
        }

        RenderElementCards();
    }

    void RenderElementCards()
    {
        foreach (ExtractedElement element in currentElements)
        {
            var card = new Panel
            {
                Width = Math.Max(200, elementsPanel.ClientSize.Width - 24),
                Height = 260,
                Margin = new Padding(0, 0, 0, 8),
                BorderStyle = BorderStyle.FixedSingle,
            };

            var title = new Label
            {
                Text = $"{element.Name}  {element.TightBounds.Width}x{element.TightBounds.Height}  pixels={element.PixelCount}",
                Dock = DockStyle.Top,
                Height = 24,
            };
            card.Controls.Add(title);

            var thumbs = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 3 };
            thumbs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            thumbs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            thumbs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            thumbs.Controls.Add(MiniPreview(element.FullBitmap, "Full"), 0, 0);
            thumbs.Controls.Add(MiniPreview(element.PlateBitmap, "Plate"), 1, 0);
            thumbs.Controls.Add(MiniPreview(element.DetailBitmap, "Detail"), 2, 0);
            card.Controls.Add(thumbs);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.Add(BuildExportButton(element, "full", element.FullBitmap));
            if (element.PlateBitmap != null)
                buttons.Controls.Add(BuildExportButton(element, "plate", element.PlateBitmap));
            if (element.DetailBitmap != null)
                buttons.Controls.Add(BuildExportButton(element, "detail", element.DetailBitmap));
            card.Controls.Add(buttons);

            elementsPanel.Controls.Add(card);
        }
    }

    Control MiniPreview(Bitmap? bitmap, string label)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var box = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(35, 35, 35),
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = bitmap,
        };
        var name = new Label { Text = label, Dock = DockStyle.Top, Height = 18 };
        panel.Controls.Add(box);
        panel.Controls.Add(name);
        return panel;
    }

    Button BuildExportButton(ExtractedElement element, string suffix, Bitmap bitmap)
    {
        var button = new Button { Text = $"Export {suffix}", AutoSize = true };
        button.Click += (_, _) => ExportElementBitmap(element, suffix, bitmap);
        return button;
    }

    void ExportComposite()
    {
        if (currentCompositeBitmap == null)
        {
            UpdateStatus("Nothing to export.");
            return;
        }

        string folder = EnsureOutputFolder();
        string name = GetBaseName() + "_composite.png";
        currentCompositeBitmap.Save(Path.Combine(folder, name), System.Drawing.Imaging.ImageFormat.Png);
        UpdateStatus("Composite exported.");
    }

    void ExportAllElements()
    {
        if (currentElements.Count == 0)
        {
            UpdateStatus("Nothing to export.");
            return;
        }

        string folder = EnsureOutputFolder();
        foreach (ExtractedElement element in currentElements)
        {
            SaveElementVariant(folder, element, "full", element.FullBitmap);
            if (element.PlateBitmap != null)
                SaveElementVariant(folder, element, "plate", element.PlateBitmap);
            if (element.DetailBitmap != null)
                SaveElementVariant(folder, element, "detail", element.DetailBitmap);
        }

        UpdateStatus($"Exported {currentElements.Count} element(s).");
    }

    void ExportElementBitmap(ExtractedElement element, string suffix, Bitmap bitmap)
    {
        string folder = EnsureOutputFolder();
        SaveElementVariant(folder, element, suffix, bitmap);
        UpdateStatus($"{element.Name} {suffix} exported.");
    }

    void SaveElementVariant(string folder, ExtractedElement element, string suffix, Bitmap bitmap)
    {
        string baseName = GetBaseName();
        string file = Path.Combine(folder, $"{baseName}_{element.Name.ToLowerInvariant()}_{suffix}.png");
        bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
    }

    void OpenOutputFolder()
    {
        string folder = EnsureOutputFolder();
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    string EnsureOutputFolder()
    {
        string folder = outputPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
            folder = GetDefaultOutputFolder();

        Directory.CreateDirectory(folder);
        outputPathBox.Text = folder;
        return folder;
    }

    string GetBaseName()
    {
        if (!string.IsNullOrWhiteSpace(sourcePathBox.Text))
            return Path.GetFileNameWithoutExtension(sourcePathBox.Text);
        return "extracted";
    }

    static string GetDefaultOutputFolder()
    {
        return Path.Combine(AppContext.BaseDirectory, "ExtractedLayers");
    }

    void UpdateStatus(string text)
    {
        statusLabel.Text = text;
    }

    void SyncSettingsFromUi()
    {
        settings.Mode = (ExtractionMode)modeCombo.SelectedIndex;
        settings.BackgroundMode = (BackgroundMode)backgroundCombo.SelectedIndex;
        settings.AlphaThreshold = (int)alphaUpDown.Value;
        settings.LightThreshold = (int)lightUpDown.Value;
        settings.CornerTolerance = (int)cornerToleranceUpDown.Value;
        settings.ComponentPadding = (int)componentPaddingUpDown.Value;
        settings.MinComponentPixels = (int)minComponentUpDown.Value;
        settings.RecoverySeedTolerance = (int)recoverySeedUpDown.Value;
        settings.RecoveryGrowTolerance = (int)recoveryGrowUpDown.Value;
        settings.BackgroundRejectTolerance = (int)backgroundRejectUpDown.Value;
        settings.PlateSeedTolerance = (int)plateSeedUpDown.Value;
        settings.PlateGrowTolerance = (int)plateGrowUpDown.Value;
        settings.MinDetailPixels = (int)minDetailUpDown.Value;
        settings.AutoSnapFocusedRect = autoSnapCheck.Checked;
        settings.FocusRect = new RectInt((int)focusXUpDown.Value, (int)focusYUpDown.Value, (int)focusWUpDown.Value, (int)focusHUpDown.Value);
        settings.FocusedMaskMargin = (int)focusMaskMarginUpDown.Value;
        settings.FocusedBackgroundColorTolerance = (int)focusBgColorUpDown.Value;
        settings.FocusedBackgroundBrightnessTolerance = (int)focusBgBrightUpDown.Value;
        settings.FrameSearchMargin = (int)frameSearchMarginUpDown.Value;
        settings.FrameDarkThreshold = (int)frameDarkUpDown.Value;
        settings.FrameCoverageThreshold = (float)frameCoverageUpDown.Value / 100f;
        settings.FrameSnapPadding = (int)frameSnapPaddingUpDown.Value;
    }

    void SyncSettingsToUi()
    {
        modeCombo.SelectedIndex = (int)settings.Mode;
        backgroundCombo.SelectedIndex = (int)settings.BackgroundMode;
        alphaUpDown.Value = settings.AlphaThreshold;
        lightUpDown.Value = settings.LightThreshold;
        cornerToleranceUpDown.Value = settings.CornerTolerance;
        componentPaddingUpDown.Value = settings.ComponentPadding;
        minComponentUpDown.Value = settings.MinComponentPixels;
        recoverySeedUpDown.Value = settings.RecoverySeedTolerance;
        recoveryGrowUpDown.Value = settings.RecoveryGrowTolerance;
        backgroundRejectUpDown.Value = settings.BackgroundRejectTolerance;
        plateSeedUpDown.Value = settings.PlateSeedTolerance;
        plateGrowUpDown.Value = settings.PlateGrowTolerance;
        minDetailUpDown.Value = settings.MinDetailPixels;
        autoSnapCheck.Checked = settings.AutoSnapFocusedRect;
        focusXUpDown.Value = settings.FocusRect.X;
        focusYUpDown.Value = settings.FocusRect.Y;
        focusWUpDown.Value = settings.FocusRect.Width;
        focusHUpDown.Value = settings.FocusRect.Height;
        focusMaskMarginUpDown.Value = settings.FocusedMaskMargin;
        focusBgColorUpDown.Value = settings.FocusedBackgroundColorTolerance;
        focusBgBrightUpDown.Value = settings.FocusedBackgroundBrightnessTolerance;
        frameSearchMarginUpDown.Value = settings.FrameSearchMargin;
        frameDarkUpDown.Value = settings.FrameDarkThreshold;
        frameCoverageUpDown.Value = (decimal)(settings.FrameCoverageThreshold * 100f);
        frameSnapPaddingUpDown.Value = settings.FrameSnapPadding;
    }

    void UpdateFocusControlsEnabled()
    {
        bool focused = modeCombo.SelectedIndex == (int)ExtractionMode.FocusedRect;
        bool autoSnap = autoSnapCheck.Checked;
        focusXUpDown.Enabled = focused;
        focusYUpDown.Enabled = focused;
        focusWUpDown.Enabled = focused;
        focusHUpDown.Enabled = focused;
        focusMaskMarginUpDown.Enabled = focused;
        focusBgColorUpDown.Enabled = focused;
        focusBgBrightUpDown.Enabled = focused;
        frameSearchMarginUpDown.Enabled = focused && autoSnap;
        frameDarkUpDown.Enabled = focused && autoSnap;
        frameCoverageUpDown.Enabled = focused && autoSnap;
        frameSnapPaddingUpDown.Enabled = focused && autoSnap;
    }

    void DisposeBitmaps()
    {
        ClearPreviewImages();
        DisposeBitmap(ref currentSourceBitmap);
        DisposeBitmap(ref currentCompositeBitmap);
        DisposeElements();
    }

    void ClearRenderedResults()
    {
        ClearPreviewImages();
        DisposeElements();
        elementsPanel.Controls.Clear();
        currentElements.Clear();
        DisposeBitmap(ref currentCompositeBitmap);
    }

    void ClearPreviewImages()
    {
        sourcePreview.Image = null;
        compositePreview.Image = null;

        foreach (Control card in elementsPanel.Controls)
        {
            DisposeControlImages(card);
        }
    }

    static void DisposeControlImages(Control control)
    {
        if (control is PictureBox pictureBox)
        {
            pictureBox.Image = null;
            return;
        }

        foreach (Control child in control.Controls)
            DisposeControlImages(child);
    }

    void DisposeElements()
    {
        foreach (ExtractedElement element in currentElements)
        {
            element.FullBitmap.Dispose();
            element.PlateBitmap?.Dispose();
            element.DetailBitmap?.Dispose();
        }

        currentElements.Clear();
    }

    static void DisposeBitmap(ref Bitmap? bitmap)
    {
        bitmap?.Dispose();
        bitmap = null;
    }
}
