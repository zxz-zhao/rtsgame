using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;

/// <summary>
/// 跨平台高兼容运行时纹理加载器 (Windows / Android APK / Linux)
/// 针对 Android APK 内部虚拟文件系统 (res://) 优化，彻底杜绝黑块与加载失败。
/// </summary>
public static class RuntimeTextureLoader
{
    private static readonly ConcurrentDictionary<string, Texture2D> textureCache = new();
    private static Texture2D? defaultFallbackTexture;

    public static Texture2D LoadTexture(string? resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return GetFallbackTexture();

        string normalizedPath = NormalizePath(resourcePath);

        if (textureCache.TryGetValue(normalizedPath, out var cached) && GodotObject.IsInstanceValid(cached))
            return cached;

        // 1. 优先使用引擎标准资源加载器 (已导入为 ctex 的纹理优先走硬件纹理管线)
        try
        {
            if (ResourceLoader.Exists(normalizedPath))
            {
                var tex = ResourceLoader.Load<Texture2D>(normalizedPath);
                if (tex is not null)
                {
                    textureCache[normalizedPath] = tex;
                    return tex;
                }
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[TextureLoader] ResourceLoader load failed for {normalizedPath}: {ex.Message}");
        }

        // 2. Android APK 内部文件系统兼容加载：通过 Godot.FileAccess 读取字节流后内存解码
        // 规避 Android APK 无法使用 ProjectSettings.GlobalizePath 与 Image.LoadFromFile 的硬限制
        try
        {
            var image = LoadImageViaFileAccess(normalizedPath);
            if (image is not null && !image.IsEmpty())
            {
                var imageTex = ImageTexture.CreateFromImage(image);
                textureCache[normalizedPath] = imageTex;
                return imageTex;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TextureLoader] FileAccess buffer load failed for {normalizedPath}: {ex.Message}");
        }

        // 3. 兜底返回安全单色贴图，防止 UI 报 null 崩溃
        var fallback = GetFallbackTexture();
        textureCache[normalizedPath] = fallback;
        return fallback;
    }

    public static Texture2D LoadTrimmedTexture(string? resourcePath, int padding = 2)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return GetFallbackTexture();

        string normalizedPath = NormalizePath(resourcePath);
        string cacheKey = $"{normalizedPath}_trimmed_{padding}";

        if (textureCache.TryGetValue(cacheKey, out var cached) && GodotObject.IsInstanceValid(cached))
            return cached;

        var baseTex = LoadTexture(normalizedPath);
        if (baseTex is null)
            return GetFallbackTexture();

        // Android 移动端或 ASTC/ETC2 压缩格式下，避免 CPU 遍历像素触发解包崩溃
        if (OS.GetName() == "Android" || OS.HasFeature("android"))
        {
            textureCache[cacheKey] = baseTex;
            return baseTex;
        }

        try
        {
            var image = baseTex.GetImage();
            if (image is null || image.IsEmpty())
            {
                textureCache[cacheKey] = baseTex;
                return baseTex;
            }

            if (image.IsCompressed())
            {
                var err = image.Decompress();
                if (err != Error.Ok)
                {
                    textureCache[cacheKey] = baseTex;
                    return baseTex;
                }
            }

            image.Convert(Image.Format.Rgba8);
            var bounds = FindOpaqueBounds(image);

            if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            {
                textureCache[cacheKey] = baseTex;
                return baseTex;
            }

            int left = Math.Max(0, bounds.Position.X - padding);
            int top = Math.Max(0, bounds.Position.Y - padding);
            int right = Math.Min(image.GetWidth(), bounds.End.X + padding);
            int bottom = Math.Min(image.GetHeight(), bounds.End.Y + padding);
            int width = Math.Max(1, right - left);
            int height = Math.Max(1, bottom - top);

            var cropped = image.GetRegion(new Rect2I(left, top, width, height));
            var croppedTex = ImageTexture.CreateFromImage(cropped);
            textureCache[cacheKey] = croppedTex;
            return croppedTex;
        }
        catch
        {
            textureCache[cacheKey] = baseTex;
            return baseTex;
        }
    }

    private static Image? LoadImageViaFileAccess(string path)
    {
        // 尝试原路径以及大小写备选路径
        string[] candidates = GetPathCandidates(path);

        foreach (var candidate in candidates)
        {
            if (!Godot.FileAccess.FileExists(candidate))
                continue;

            using var file = Godot.FileAccess.Open(candidate, Godot.FileAccess.ModeFlags.Read);
            if (file is null)
                continue;

            long length = (long)file.GetLength();
            if (length <= 0 || length > 64 * 1024 * 1024)
                continue;

            byte[] buffer = file.GetBuffer(length);
            if (buffer == null || buffer.Length == 0)
                continue;

            var image = DecodeImageBuffer(candidate, buffer);
            if (image is not null && !image.IsEmpty())
                return image;
        }

        return null;
    }

    private static Image? DecodeImageBuffer(string path, byte[] data)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        var img = new Image();

        // 1. 扩展名匹配解码
        Error err = ext switch
        {
            ".png" => img.LoadPngFromBuffer(data),
            ".jpg" or ".jpeg" => img.LoadJpgFromBuffer(data),
            ".webp" => img.LoadWebpFromBuffer(data),
            ".svg" => img.LoadSvgFromBuffer(data),
            ".bmp" => img.LoadBmpFromBuffer(data),
            ".tga" => img.LoadTgaFromBuffer(data),
            _ => Error.Failed
        };

        if (err == Error.Ok && !img.IsEmpty())
            return img;

        // 2. 文件头魔数 (Magic Bytes) 自动识别兜底 (防止误标扩展名)
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            if (img.LoadPngFromBuffer(data) == Error.Ok) return img;
        }
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            if (img.LoadJpgFromBuffer(data) == Error.Ok) return img;
        }
        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
            data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P')
        {
            if (img.LoadWebpFromBuffer(data) == Error.Ok) return img;
        }

        return null;
    }

    private static string[] GetPathCandidates(string path)
    {
        var list = new System.Collections.Generic.List<string> { path };

        // 针对 Windows 大小写不敏感但在 Android 严格敏感的问题，生成常见的大小写容错候选
        if (path.Contains("/Assets/Resources/"))
        {
            list.Add(path.Replace("/Assets/Resources/", "/assets/resources/"));
            list.Add(path.Replace("/Assets/Resources/", "/Assets/resources/"));
        }
        else if (path.Contains("/assets/resources/"))
        {
            list.Add(path.Replace("/assets/resources/", "/Assets/Resources/"));
        }

        if (path.EndsWith(".PNG"))
            list.Add(path[..^4] + ".png");
        else if (path.EndsWith(".png"))
            list.Add(path[..^4] + ".PNG");

        if (path.EndsWith(".JPG"))
            list.Add(path[..^4] + ".jpg");
        else if (path.EndsWith(".jpg"))
            list.Add(path[..^4] + ".JPG");

        return list.ToArray();
    }

    private static string NormalizePath(string path)
    {
        string p = path.Trim().Replace('\\', '/');
        if (!p.StartsWith("res://") && !p.StartsWith("user://"))
        {
            p = "res://" + p.TrimStart('/');
        }
        return p;
    }

    public static Texture2D GetFallbackTexture()
    {
        if (defaultFallbackTexture is not null && GodotObject.IsInstanceValid(defaultFallbackTexture))
            return defaultFallbackTexture;

        var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        img.Fill(new Color(0.14f, 0.20f, 0.24f, 0.90f)); // 战术军规暗青色兜底，避免纯黑或紫红爆屏
        defaultFallbackTexture = ImageTexture.CreateFromImage(img);
        return defaultFallbackTexture;
    }

    private static Rect2I FindOpaqueBounds(Image image)
    {
        int width = image.GetWidth();
        int height = image.GetHeight();
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = image.GetPixel(x, y);
                if (color.A > 0.05f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rect2I(0, 0, width, height);

        return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
