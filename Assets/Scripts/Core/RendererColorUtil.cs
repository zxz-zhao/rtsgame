using UnityEngine;

// 所有 Renderer 级别操作统一走 MaterialPropertyBlock，
// 避免与 ApplyMilitaryTint 的 SetPropertyBlock 产生冲突。
public static class RendererColorUtil
{
    const string LegacyColor = "_Color";
    const string BaseColor   = "_BaseColor";

    static readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    // ── Renderer 重载：全走 PropertyBlock ──────────────────────────
    public static bool TryGetColor(Renderer renderer, out Color color)
    {
        color = Color.white;
        if (renderer == null) return false;

        renderer.GetPropertyBlock(_block);
        // PropertyBlock 中已有值（由 SetPropertyBlock 设置）
        Color pb = _block.GetColor(LegacyColor);
        if (pb == Color.clear || pb == default)
            pb = _block.GetColor(BaseColor);
        if (pb != Color.clear && pb != default)
        {
            color = pb;
            return true;
        }
        // 回退到 sharedMaterial
        if (renderer.sharedMaterial == null) return false;
        return TryGetColor(renderer.sharedMaterial, out color);
    }

    public static bool TrySetColor(Renderer renderer, Color color)
    {
        if (renderer == null) return false;
        renderer.GetPropertyBlock(_block);
        _block.SetColor(LegacyColor, color);
        _block.SetColor(BaseColor, color);
        renderer.SetPropertyBlock(_block);
        return true;
    }

    // ── Material 重载：直接写 Material（Editor 用，运行时尽量不用）──
    public static bool TryGetColor(Material material, out Color color)
    {
        color = Color.white;
        if (material == null) return false;
        if (material.HasProperty(LegacyColor)) { color = material.GetColor(LegacyColor); return true; }
        if (material.HasProperty(BaseColor))   { color = material.GetColor(BaseColor);   return true; }
        return false;
    }

    public static bool TrySetColor(Material material, Color color)
    {
        if (material == null) return false;
        if (material.HasProperty(LegacyColor)) { material.SetColor(LegacyColor, color); return true; }
        if (material.HasProperty(BaseColor))   { material.SetColor(BaseColor,   color); return true; }
        return false;
    }

    // ── 工具：清除 PropertyBlock（重置为材质默认）────────────────────
    public static void ClearPropertyBlock(Renderer renderer)
    {
        if (renderer == null) return;
        renderer.SetPropertyBlock(null);
    }
}
