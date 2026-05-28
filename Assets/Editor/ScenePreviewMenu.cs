using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器预览：不按 Play 也能看到 GameInitializer 生成的建筑/单位/地形布局。
/// 菜单：RTS/场景预览/【EditMode】生成战场建筑
///       RTS/场景预览/【EditMode】清除预览生成对象
/// 注意：生成的对象会被标记 hideFlags = DontSaveInEditor，关闭场景或重新打开时自动清理，
/// 不会污染场景文件。
/// </summary>
public static class ScenePreviewMenu
{
    [MenuItem("RTS/场景预览/【EditMode】生成战场建筑")]
    public static void SpawnPreview()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("场景预览", "请先停止 Play Mode。", "好的");
            return;
        }

        // 找到当前场景的 GameInitializer
        var initializer = Object.FindObjectOfType<GameInitializer>();
        if (initializer == null)
        {
            EditorUtility.DisplayDialog("场景预览",
                "找不到 GameInitializer。请在 GameScene 中调用此菜单。", "好的");
            return;
        }

        ClearPreview();
        try
        {
            initializer.EditorPreviewSpawn();
            // 标记新生成的根对象为 EditMode-only（关场景自动清理）
            int marked = MarkRootObjects();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // 把 Scene 视图相机移到中央俯视，方便立刻看到全景
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(
                    new Vector3(0f, 0f, 0f),
                    Quaternion.Euler(55f, 30f, 0f),
                    280f);
                SceneView.lastActiveSceneView.Repaint();
            }

            EditorUtility.DisplayDialog("场景预览",
                $"已在场景中生成战场建筑/地形/装饰。\n标记 {marked} 个根对象为预览态。\n保存场景前会被自动清理。",
                "好的");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ScenePreview] 生成失败: {e.Message}\n{e.StackTrace}");
        }
    }

    [MenuItem("RTS/场景预览/【EditMode】清除预览生成对象")]
    public static void ClearPreview()
    {
        var all = Object.FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (var go in all)
        {
            if (go == null) continue;
            if (go.transform.parent != null) continue; // 只看根对象

            string n = go.name;
            // 与 RuntimeBattleMapBuilder.RuntimePrefixes/Names + GameInitializer 生成的命名匹配
            if (IsPreviewObjectName(n))
            {
                Object.DestroyImmediate(go);
                count++;
            }
        }
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[ScenePreview] 清除 {count} 个预览对象");
        }
    }

    static bool IsPreviewObjectName(string n)
    {
        if (string.IsNullOrEmpty(n)) return false;
        // 建筑（保留 _P/_E/_Player/_Enemy 后缀的根 prefab 实例）
        string[] bldKeys = {
            "MainBase", "Barracks", "AirFactory", "TankFactory",
            "PowerPlant", "GoldMine", "Turret"
        };
        foreach (var k in bldKeys) if (n.StartsWith(k)) return true;
        // 地形/装饰（与 RuntimeBattleMapBuilder 一致）
        string[] terrainPrefix = { "TerrainPatch", "Road_", "RoadEdge_", "Water_", "Wall", "BattlefieldProp_" };
        foreach (var p in terrainPrefix) if (n.StartsWith(p)) return true;
        string[] decoNames = {
            "Rock", "Tree", "Leaves", "RuinWall", "Debris",
            "Sandbag", "BagMark", "Post", "RuinedTower", "DamagedRail"
        };
        foreach (var d in decoNames) if (n == d) return true;
        return false;
    }

    static int MarkRootObjects()
    {
        var all = Object.FindObjectsOfType<GameObject>();
        int count = 0;
        foreach (var go in all)
        {
            if (go == null) continue;
            if (go.transform.parent != null) continue;
            if (!IsPreviewObjectName(go.name)) continue;
            // DontSaveInEditor: 不被保存到场景文件，关闭/重新打开场景时清除
            go.hideFlags = HideFlags.DontSaveInEditor;
            count++;
        }
        return count;
    }
}
