using System;
using UnityEngine;

public static class UnitScaleNormalizer
{
    public static bool Normalize(Transform unitRoot, float targetHeight, float targetFootprint, bool includeAttachmentVisuals = false)
    {
        if (unitRoot == null || targetHeight <= 0f || targetFootprint <= 0f) return false;

        Transform visualRoot = ResolveVisualRoot(unitRoot);
        if (visualRoot == null) return false;

        Bounds bounds;
        if (!TryComputeRendererBounds(visualRoot, out bounds, includeAttachmentVisuals)) return false;
        if (bounds.size.x <= 0.001f && bounds.size.y <= 0.001f && bounds.size.z <= 0.001f) return false;

        float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
        float currentFootprint = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z), 0.01f);
        float heightScale = targetHeight / currentHeight;
        float footprintScale = targetFootprint / currentFootprint;
        float scale = Mathf.Min(heightScale, footprintScale);

        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) return false;
        if (Mathf.Abs(scale - 1f) < 0.02f) return false;

        if (scale > 20f)
        {
            Debug.LogWarning($"[UnitScaleNormalizer] {unitRoot.name} scale factor {scale:F1} exceeds limit 20. " +
                             $"Current H={currentHeight:F3}m FP={currentFootprint:F3}m, target H={targetHeight}m FP={targetFootprint}m. " +
                             "Check FBX import scale. Clamped to 20.");
            scale = 20f;
        }

        visualRoot.localScale *= scale;
        return true;
    }

    public static Transform ResolveVisualRoot(Transform unitRoot)
    {
        if (unitRoot == null) return null;
        Transform model = unitRoot.Find("Model");
        return model != null ? model : unitRoot;
    }

    static bool TryComputeRendererBounds(Transform visualRoot, out Bounds bounds, bool includeAttachmentVisuals)
    {
        bounds = new Bounds(visualRoot.position, Vector3.zero);
        bool first = true;
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
            if (ShouldExclude(renderer.transform, includeAttachmentVisuals)) continue;
            if (renderer is LineRenderer || renderer is TrailRenderer || renderer is ParticleSystemRenderer) continue;

            if (first)
            {
                bounds = renderer.bounds;
                first = false;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return !first;
    }

    static bool ShouldExclude(Transform transform, bool includeAttachmentVisuals)
    {
        if (transform == null) return true;

        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            if (IsAlwaysExcluded(name)) return true;
            if (!includeAttachmentVisuals && IsAttachmentExcluded(name)) return true;

            current = current.parent;
        }

        return false;
    }

    static bool IsAlwaysExcluded(string name)
    {
        return name.StartsWith("Label_", StringComparison.OrdinalIgnoreCase)
            || name == "UnitLabel"
            || name == "HealthBar"
            || name == "ProductionBar"
            || name == "SelectionRing"
            || name == "SelectionCircle"
            || name == "FactionRing"
            || name == "FactionDot"
            || name == "FactionPlate"
            || name == "AirShadow"
            || name == "HealAura"
            || name == "MinimapDot"
            || name == "RallyMarker"
            || name.StartsWith("RallyMarker_", StringComparison.OrdinalIgnoreCase)
            || name == "FlagPole"
            || name == "FlagCloth"
            || name == "RallyFlagModel"
            || name == "Contrail"
            || name.EndsWith("Aura", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Disc", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Ring", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsAttachmentExcluded(string name)
    {
        return name == "KenneyWeapon"
            || name.StartsWith("WW2Shoulder", StringComparison.OrdinalIgnoreCase)
            || name == "ShoulderCannon"
            || name == "Muzzle"
            || name.IndexOf("Rifle", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
