using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ImageLayerExtractorTool;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (TryRunCli(args))
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    static bool TryRunCli(string[] args)
    {
        if (args.Length == 0)
            return false;

        if (string.Equals(args[0], "--extract", StringComparison.OrdinalIgnoreCase))
            return TryRunExtractCli(args);

        if (!string.Equals(args[0], "--render-samples", StringComparison.OrdinalIgnoreCase))
            return false;

        string sourcePath = args.Length > 1
            ? args[1]
            : Path.Combine(FindRepoRoot() ?? AppContext.BaseDirectory, "Assets", "Resources", "LobbyGen", "gen_exact_claim_button.png");
        string outputDir = args.Length > 2
            ? args[2]
            : Path.Combine(AppContext.BaseDirectory, "PreviewOutput", "image_tool_samples");
        string patchText = args.Length > 3 ? args[3] : "出征";
        string goldText = args.Length > 4 ? args[4] : "锦标赛";

        Directory.CreateDirectory(outputDir);

        using Bitmap source = new(sourcePath);
        RectInt patchRect = new(
            Math.Max(4, source.Width / 14),
            Math.Max(4, source.Height / 7),
            Math.Max(10, source.Width - Math.Max(8, source.Width / 7)),
            Math.Max(10, source.Height - Math.Max(12, source.Height / 3)));

        using Bitmap patched = ImageComposer.PatchText(source, patchRect, patchText, Math.Max(14f, source.Height * 0.40f));
        patched.Save(Path.Combine(outputDir, "claim_button_patched.png"), ImageFormat.Png);

        using Bitmap gold = ImageComposer.RenderGoldBevelButton(Math.Max(220, source.Width * 3), Math.Max(64, source.Height + 18), goldText, 28f);
        gold.Save(Path.Combine(outputDir, "gold_button_generated.png"), ImageFormat.Png);

        using Bitmap rounded = ImageComposer.RenderGoldRoundedButton(Math.Max(220, source.Width * 3), Math.Max(64, source.Height + 18), goldText, 28f);
        rounded.Save(Path.Combine(outputDir, "gold_button_rounded.png"), ImageFormat.Png);

        Console.WriteLine(outputDir);
        return true;
    }

    static bool TryRunExtractCli(string[] args)
    {
        string sourcePath = args.Length > 1 ? args[1] : Path.Combine(FindRepoRoot() ?? AppContext.BaseDirectory, "Assets", "Resources", "LobbyGen", "_ref_lobby_master.png");
        string outputDir = args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "PreviewOutput", "image_tool_extracted");
        string presetName = args.Length > 3 ? args[3] : "ExactClaimButton";

        Directory.CreateDirectory(outputDir);
        using Bitmap source = new(sourcePath);

        ExtractionSettings settings = new();
        foreach (var p in ImageExtractor.GetFocusedRectPresets())
        {
            if (string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase))
            {
                settings.Mode = ExtractionMode.FocusedRect;
                settings.FocusRect = p.Rect;
                settings.ExactMask = p.ExactMask;
                settings.ClearMatchButtonBorderContent = p.ClearMatchButtonBorderContent;
                settings.FocusedElementName = p.Name;
                settings.ExactExportTrim = p.ExportTrim;
                break;
            }
        }

        ImageExtractor extractor = new(settings);
        var result = extractor.Process(source);

        Console.WriteLine(result.StatusMessage);
        if (result.CompositeBitmap != null)
            result.CompositeBitmap.Save(Path.Combine(outputDir, "composite.png"), ImageFormat.Png);

        foreach (var element in result.Elements)
        {
            Console.WriteLine($"Extracted: {element.Name} Pixels: {element.PixelCount}");
            element.FullBitmap.Save(Path.Combine(outputDir, $"{element.Name}_full.png"), ImageFormat.Png);
            if (element.PlateBitmap != null) element.PlateBitmap.Save(Path.Combine(outputDir, $"{element.Name}_plate.png"), ImageFormat.Png);
            if (element.DetailBitmap != null) element.DetailBitmap.Save(Path.Combine(outputDir, $"{element.Name}_detail.png"), ImageFormat.Png);
        }

        return true;
    }

    static string? FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
