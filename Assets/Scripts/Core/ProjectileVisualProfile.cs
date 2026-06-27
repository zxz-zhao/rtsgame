using UnityEngine;

public static class ProjectileVisualProfile
{
    public static ProjectileType ResolveType(string prefabPath, float arcHeight, float impactRadius, Color tint)
    {
        string path = string.IsNullOrEmpty(prefabPath) ? string.Empty : prefabPath.ToLowerInvariant();
        if (path.Contains("bomb"))
            return ProjectileType.Bomb;
        if (path.Contains("flame") || path.Contains("fire"))
            return ProjectileType.Fire;
        if (path.Contains("rocket") || path.Contains("missile"))
            return ProjectileType.Rocket;
        if (path.Contains("shell") || path.Contains("cannon"))
            return ProjectileType.Shell;
        if (path.Contains("bullet"))
            return ProjectileType.Bullet;

        if (impactRadius >= 1.8f && arcHeight >= 2.5f)
            return ProjectileType.Bomb;
        if (tint.r > 0.88f && tint.g < 0.46f && tint.b < 0.22f && arcHeight < 0.5f && impactRadius <= 1.2f)
            return ProjectileType.Fire;
        if (impactRadius >= 0.65f)
            return ProjectileType.Shell;
        if (arcHeight >= 1.6f)
            return ProjectileType.Rocket;
        return ProjectileType.Bullet;
    }

    public static bool ShouldShowTracer(ProjectileType type)
    {
        return type == ProjectileType.Bullet;
    }

    public static void ApplyTracerStyle(LineRenderer line, ProjectileType type, bool flying)
    {
        if (line == null)
            return;

        float startWidth;
        float endWidth;
        switch (type)
        {
            case ProjectileType.Shell:
                startWidth = flying ? 0.15f : 0.11f;
                endWidth = flying ? 0.05f : 0.025f;
                break;
            case ProjectileType.Rocket:
                startWidth = flying ? 0.18f : 0.13f;
                endWidth = flying ? 0.07f : 0.035f;
                break;
            default:
                startWidth = flying ? 0.20f : 0.14f;
                endWidth = flying ? 0.07f : 0.04f;
                break;
        }

        line.startWidth = startWidth;
        line.endWidth = endWidth;
    }

    public static float GetTracerDuration(ProjectileType type, float fallbackDuration)
    {
        switch (type)
        {
            case ProjectileType.Shell:
                return Mathf.Clamp(fallbackDuration * 0.82f, 0.025f, 0.065f);
            case ProjectileType.Rocket:
                return Mathf.Clamp(fallbackDuration * 0.92f, 0.03f, 0.075f);
            default:
                return Mathf.Clamp(fallbackDuration, 0.02f, 0.08f);
        }
    }

    public static float GetImpactHeightOffset(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Bomb:
                return 0.18f;
            case ProjectileType.Shell:
                return 0.65f;
            case ProjectileType.Rocket:
                return 0.75f;
            case ProjectileType.Fire:
                return 0.8f;
            default:
                return 1f;
        }
    }

    public static float GetMuzzleFlashIntensity(ProjectileType type, float impactRadius)
    {
        float radius = Mathf.Max(0.25f, impactRadius);
        switch (type)
        {
            case ProjectileType.Bomb:
                return 0f;
            case ProjectileType.Shell:
                return Mathf.Lerp(1f, 1.55f, Mathf.InverseLerp(0.8f, 1.8f, radius));
            case ProjectileType.Rocket:
                return Mathf.Lerp(1.05f, 1.7f, Mathf.InverseLerp(0.8f, 1.8f, radius));
            case ProjectileType.Fire:
                return 1.1f;
            default:
                return 0.8f;
        }
    }

    public static float GetLaunchShakeMagnitude(ProjectileType type, bool flying, float impactRadius)
    {
        float radius = Mathf.Max(0.25f, impactRadius);
        switch (type)
        {
            case ProjectileType.Shell:
                return Mathf.Lerp(0.045f, 0.10f, Mathf.InverseLerp(0.8f, 1.8f, radius));
            case ProjectileType.Rocket:
                return Mathf.Lerp(0.055f, 0.12f, Mathf.InverseLerp(0.8f, 1.8f, radius));
            case ProjectileType.Bomb:
                return flying ? 0.035f : 0.05f;
            case ProjectileType.Fire:
                return 0.03f;
            default:
                return flying ? 0.045f : 0.02f;
        }
    }

    public static float GetLaunchShakeDuration(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Shell:
            case ProjectileType.Rocket:
                return 0.08f;
            case ProjectileType.Bomb:
                return 0.06f;
            default:
                return 0.05f;
        }
    }
}
