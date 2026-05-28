using UnityEngine;

public static class RendererColorUtil
{
    const string LegacyColor = "_Color";
    const string BaseColor = "_BaseColor";

    public static bool TryGetColor(Renderer renderer, out Color color)
    {
        color = Color.white;
        if (renderer == null || renderer.material == null)
            return false;

        return TryGetColor(renderer.material, out color);
    }

    public static bool TrySetColor(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.material == null)
            return false;

        return TrySetColor(renderer.material, color);
    }

    public static bool TryGetColor(Material material, out Color color)
    {
        color = Color.white;
        if (material == null)
            return false;

        if (material.HasProperty(LegacyColor))
        {
            color = material.GetColor(LegacyColor);
            return true;
        }

        if (material.HasProperty(BaseColor))
        {
            color = material.GetColor(BaseColor);
            return true;
        }

        return false;
    }

    public static bool TrySetColor(Material material, Color color)
    {
        if (material == null)
            return false;

        if (material.HasProperty(LegacyColor))
        {
            material.SetColor(LegacyColor, color);
            return true;
        }

        if (material.HasProperty(BaseColor))
        {
            material.SetColor(BaseColor, color);
            return true;
        }

        return false;
    }
}
