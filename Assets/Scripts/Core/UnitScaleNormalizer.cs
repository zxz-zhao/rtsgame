using UnityEngine;

/// <summary>
/// 统一尺寸标定：把 prefab 实际渲染出来的可视模型（Renderer 包围盒）等比缩放到目标
/// 高度（Y）+ 占地直径（X/Z 最大边），解决"各 prefab 引用不同 Kenney 包、自带尺寸杂乱"的问题。
///
/// 策略：
///   1) 优先缩放 Transform.Find("Model") 子节点；找不到则缩放 root 自身（会牵动 collider，
///      使用方需自行避免在 collider 关键场景下走这条路径）。
///   2) 取 visual 子树所有 Renderer 的 world bounds；排除血条/标签/选择圈等 FX 节点。
///   3) 选 H 与 FP 两个限制中**更严格**的缩放因子（保证不超出目标盒），等比缩放。
///   4) 偏差小于 ±2% 时不缩放，避免连续微抖动。
/// </summary>
public static class UnitScaleNormalizer
{
    /// <summary>把单位/建筑的可视部分缩放到目标盒。返回是否真的执行了缩放。</summary>
    public static bool Normalize(Transform unitRoot, float targetHeight, float targetFootprint)
    {
        if (unitRoot == null || targetHeight <= 0f || targetFootprint <= 0f) return false;

        Transform visualRoot = ResolveVisualRoot(unitRoot);
        if (visualRoot == null) return false;

        Bounds b;
        if (!TryComputeRendererBounds(visualRoot, out b)) return false;
        if (b.size.x <= 0.001f && b.size.y <= 0.001f && b.size.z <= 0.001f) return false;

        float curH = Mathf.Max(b.size.y, 0.01f);
        float curFP = Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.01f);
        float kH = targetHeight / curH;
        float kFP = targetFootprint / curFP;
        // 用最严格的缩放因子（保证目标盒不超出）。Mathf.Min 让"较小"的占主导
        float k = Mathf.Min(kH, kFP);
        if (k <= 0f || float.IsNaN(k) || float.IsInfinity(k)) return false;
        if (Mathf.Abs(k - 1f) < 0.02f) return false;

        visualRoot.localScale *= k;
        return true;
    }

    /// <summary>定位可视子树根：优先 "Model" 子节点；否则取 unitRoot 自身。</summary>
    public static Transform ResolveVisualRoot(Transform unitRoot)
    {
        if (unitRoot == null) return null;
        Transform model = unitRoot.Find("Model");
        return model != null ? model : unitRoot;
    }

    /// <summary>计算 visualRoot 子树所有 Renderer 的 world bounds（排除 FX 节点）。</summary>
    static bool TryComputeRendererBounds(Transform visualRoot, out Bounds bounds)
    {
        bounds = new Bounds(visualRoot.position, Vector3.zero);
        bool first = true;
        var rends = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            if (ShouldExclude(r.transform)) continue;
            // LineRenderer/TrailRenderer 等的 bounds 可能不稳定，过滤
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        return !first;
    }

    /// <summary>排除 FX/UI 类节点：血条、标签、选择圈、阵营圈、阵营点、影子、治疗圈、小地图点、集结旗等。</summary>
    static bool ShouldExclude(Transform t)
    {
        if (t == null) return true;
        // 沿父链找有没有任一被排除的祖先
        Transform cur = t;
        while (cur != null)
        {
            string n = cur.name;
            if (n.StartsWith("Label_")
                || n == "UnitLabel"
                || n == "HealthBar"
                || n == "ProductionBar"
                || n == "SelectionRing"
                || n == "FactionRing"
                || n == "FactionDot"
                || n == "AirShadow"
                || n == "HealAura"
                || n == "MinimapDot"
                || n == "RallyMarker"
                || n.StartsWith("RallyMarker_")
                || n == "FlagPole"
                || n == "FlagCloth"
                || n == "RallyFlagModel"
                || n == "Contrail")
                return true;
            cur = cur.parent;
        }
        return false;
    }
}
