using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 运行时清理 prefab 视觉子树里的违和小装饰。各 prefab 引用 Kenney 多个包，子节点经常
/// 混入旗子、瞄准镜、子弹、油桶、箱子等不合时宜的小件。本工具按节点名/Mesh 名匹配
/// 隐藏（SetActive=false），不删除子节点，保持原 prefab 引用关系完整。
/// </summary>
public static class UnitVisualPolish
{
    /// <summary>
    /// 节点/Mesh 名匹配清单。匹配方式：忽略大小写的相等比较或前缀匹配。
    /// 命中后整个 GameObject 隐藏（包含其子节点）。
    /// </summary>
    static readonly HashSet<string> _hideExact = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
    {
        // 中世纪三角旗（CastleKit）—— RTS 风不合
        "flag", "flag-high", "flag-high-pennant", "flag-pennant",
        "flag-pirate", "flag-pirate-high", "flag-pirate-high-pennant", "flag-pirate-pennant",
        // BlasterKit 装饰武器配件（与单位无功能关联，只增加杂乱）
        "scope-small", "scope-large-a", "scope-large-b",
        "silencer-small", "silencer-larger",
        "bullet-foam", "bullet-foam-thick", "bullet-foam-tip", "bullet-foam-tip-thick",
        "clip-small", "clip-large",
        "grenade-a", "grenade-b",
        // 散落的物资箱（让坦克车斗里装一堆东西）
        "crate-small", "crate-medium", "crate-wide",
        // 油桶（除非是发电厂之类才合适）
        "barrel", "barrels", "barrels_rail",
        // 杂项金属面板装饰
        "metal-panel-screws", "metal-panel-screws-narrow",
        // 海盗船骨架
        "bones",
    };

    /// <summary>
    /// 前缀匹配（大小写不敏感）。命中前缀的 GameObject 都隐藏。
    /// 当前为空：所有违和件用上面的 _hideExact 精确匹配即可。如需添加前缀（如 "flag-" 系列）
    /// 在数组里直接写字符串字面量即可，例如 new string[] { "flag-", "scope-" }。
    /// </summary>
    static readonly string[] _hidePrefixes = System.Array.Empty<string>();

    /// <summary>对单位/建筑根节点执行视觉清理。</summary>
    public static void Polish(GameObject root)
    {
        if (root == null) return;
        Transform visualRoot = UnitScaleNormalizer.ResolveVisualRoot(root.transform);
        if (visualRoot == null) return;

        // 1) 按 transform.name 匹配
        var transforms = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t == visualRoot) continue;
            if (ShouldHide(t.name))
            {
                t.gameObject.SetActive(false);
                continue;
            }
        }

        // 2) 按 mesh.name 兜底（有些 PrefabInstance 把 transform 重命名了）
        var rends = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            if (r == null || !r.gameObject.activeSelf) continue;
            string meshName = TryGetMeshName(r);
            if (string.IsNullOrEmpty(meshName)) continue;
            if (ShouldHide(meshName))
                r.gameObject.SetActive(false);
        }
    }

    static bool ShouldHide(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (_hideExact.Contains(name)) return true;
        for (int i = 0; i < _hidePrefixes.Length; i++)
        {
            if (name.StartsWith(_hidePrefixes[i], System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string TryGetMeshName(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.name;
        var smr = r as SkinnedMeshRenderer;
        if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.name;
        return null;
    }
}
