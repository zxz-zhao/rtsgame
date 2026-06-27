using System.IO;
using UnityEditor;
using UnityEngine;

public static class LobbyReferenceAssetExtractor
{
    const string LobbyGen = "Assets/Resources/LobbyGen";
    const string SourcePath = LobbyGen + "/_ref_lobby_master.png";

    [MenuItem("RTS/资源接入/从大厅参考图提取背景和按钮")]
    public static void Extract()
    {
        if (!File.Exists(SourcePath))
        {
            EditorUtility.DisplayDialog("LobbyGen", "未找到参考图：\n" + SourcePath, "确定");
            return;
        }

        Directory.CreateDirectory(LobbyGen);
        byte[] bytes = File.ReadAllBytes(SourcePath);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(source, bytes))
        {
            Object.DestroyImmediate(source);
            EditorUtility.DisplayDialog("LobbyGen", "参考图读取失败。", "确定");
            return;
        }

        File.WriteAllBytes(Path.Combine(LobbyGen, "reference_lobby_full_background.png"), bytes);
        WriteCrop(source, "gen_exact_nav_shop.png", new RectInt(0, 612, 190, 70), Vector4.zero);
        WriteCrop(source, "gen_exact_nav_warehouse.png", new RectInt(190, 612, 240, 70), Vector4.zero);
        WriteCrop(source, "gen_exact_nav_campaign.png", new RectInt(430, 600, 164, 82), Vector4.zero);
        WriteCrop(source, "gen_exact_nav_rank.png", new RectInt(594, 612, 254, 70), Vector4.zero);
        WriteCrop(source, "gen_exact_nav_mail.png", new RectInt(848, 612, 176, 70), Vector4.zero);

        WriteCrop(source, "gen_exact_mode_match.png", new RectInt(306, 151, 412, 145), Vector4.zero);
        WriteCrop(source, "gen_exact_mode_custom.png", new RectInt(306, 300, 412, 132), Vector4.zero);
        WriteCrop(source, "gen_exact_mode_global.png", new RectInt(306, 444, 412, 132), Vector4.zero);
        WriteModeFrame(source);

        WriteScaledCrop(source, "gen_button_gold.png", new RectInt(922, 184, 78, 46), 160, 64, 1f, new Vector4(20, 20, 20, 20));
        WriteScaledCrop(source, "gen_button_dark.png", new RectInt(336, 328, 352, 77), 160, 64, 0.82f, new Vector4(20, 20, 20, 20));
        WriteScaledCrop(source, "gen_button_tech_metal.png", new RectInt(332, 173, 360, 100), 192, 72, 0.9f, new Vector4(28, 22, 28, 22));

        Object.DestroyImmediate(source);
        AssetDatabase.Refresh();
        ConfigureSprite(LobbyGen + "/reference_lobby_full_background.png", Vector4.zero);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("LobbyGen", "已从大厅参考图提取背景和按钮素材。", "确定");
    }

    static void WriteCrop(Texture2D source, string fileName, RectInt topRect, Vector4 border)
    {
        var rect = ToBottomRect(source, topRect);
        var output = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
        output.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
        output.Apply();
        WriteTexture(fileName, output, border);
    }

    static void WriteScaledCrop(Texture2D source, string fileName, RectInt topRect, int width, int height, float brightness, Vector4 border)
    {
        var rect = ToBottomRect(source, topRect);
        var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        float sx = rect.width / (float)width;
        float sy = rect.height / (float)height;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sourceX = Mathf.Clamp(rect.x + Mathf.RoundToInt((x + 0.5f) * sx), rect.x, rect.xMax - 1);
            int sourceY = Mathf.Clamp(rect.y + Mathf.RoundToInt((y + 0.5f) * sy), rect.y, rect.yMax - 1);
            Color color = source.GetPixel(sourceX, sourceY);
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            output.SetPixel(x, y, color);
        }
        output.Apply();
        WriteTexture(fileName, output, border);
    }

    static void WriteModeFrame(Texture2D source)
    {
        RectInt topRect = new RectInt(306, 151, 412, 145);
        var rect = ToBottomRect(source, topRect);
        var output = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
        output.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
        for (int y = 25; y < output.height - 18; y++)
        for (int x = 28; x < output.width - 28; x++)
            output.SetPixel(x, y, Color.clear);
        output.Apply();
        WriteTexture("gen_exact_mode_frame_button.png", output, new Vector4(34, 30, 34, 30));
    }

    static RectInt ToBottomRect(Texture2D source, RectInt topRect)
    {
        int x = Mathf.Clamp(topRect.x, 0, source.width - 1);
        int y = Mathf.Clamp(source.height - topRect.y - topRect.height, 0, source.height - 1);
        int width = Mathf.Clamp(topRect.width, 1, source.width - x);
        int height = Mathf.Clamp(topRect.height, 1, source.height - y);
        return new RectInt(x, y, width, height);
    }

    static void WriteTexture(string fileName, Texture2D texture, Vector4 border)
    {
        string assetPath = LobbyGen + "/" + fileName;
        File.WriteAllBytes(assetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureSprite(assetPath, border);
    }

    static void ConfigureSprite(string assetPath, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
