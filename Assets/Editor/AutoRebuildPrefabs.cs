using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// 检测 Resources/Prefabs/Props 下是否缺少新增的 prop prefab（CastleKit/TrainKit 等）。
/// 缺失则在编辑器空闲时自动调用 PrefabBuilder.BuildAllPrefabs 一次。
/// 通过 EditorPrefs 记录已检测的 prop 集合，避免每次启动重复跑。
/// </summary>
[InitializeOnLoad]
public static class AutoRebuildPrefabs
{
    // 当前期望存在的 prop prefab 列表（与 BattlefieldPropSpawner.GeneratedPropNames 一致）
    static readonly string[] ExpectedProps = new[]
    {
        "BattlefieldProp_SupplyCrate",
        "BattlefieldProp_AmmoCrate",
        "BattlefieldProp_TargetMarker",
        "BattlefieldProp_VehicleWreck",
        "BattlefieldProp_BarrierStrong",
        "BattlefieldProp_SandbagWall",
        "BattlefieldProp_BrickRubble",
        "BattlefieldProp_StreetLight",
        "BattlefieldProp_RuinedTower",
        "BattlefieldProp_DamagedRail",
        "BattlefieldProp_PirateShip",
        "BattlefieldProp_Dock",
        "BattlefieldProp_Palm",
        "BattlefieldProp_SandRocks",
        "BattlefieldProp_PirateFlag",
    };

    const string EditorPrefKey = "RTS_AutoRebuildPrefabs_LastVersion";
    // 修改此版本号即可强制触发一次重建
    const string CurrentVersion = "2026-05-23-external-model-map-v13";

    static AutoRebuildPrefabs()
    {
        EditorApplication.delayCall += CheckAndRebuild;
    }

    static void CheckAndRebuild()
    {
        EditorApplication.delayCall -= CheckAndRebuild;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= RebuildAfterPlayMode;
            EditorApplication.playModeStateChanged += RebuildAfterPlayMode;
            return;
        }

        string saved = EditorPrefs.GetString(EditorPrefKey, "");
        bool versionMatches = saved == CurrentVersion;

        string propsDir = "Assets/Resources/Prefabs/Props";
        bool allExist = Directory.Exists(propsDir);
        if (allExist)
        {
            foreach (var n in ExpectedProps)
            {
                if (!File.Exists(Path.Combine(propsDir, n + ".prefab"))) { allExist = false; break; }
            }
        }
        if (versionMatches && allExist) return;

        Debug.Log("[AutoRebuildPrefabs] 检测到 Prop prefab 缺失或版本不匹配，自动调用 PrefabBuilder.BuildAllPrefabs ...");
        try
        {
            PrefabBuilder.BuildAllPrefabs();
            EditorPrefs.SetString(EditorPrefKey, CurrentVersion);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AutoRebuildPrefabs] ✓ 重建完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AutoRebuildPrefabs] 重建失败: {e.Message}\n{e.StackTrace}");
        }
    }

    static void RebuildAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= RebuildAfterPlayMode;
        EditorApplication.delayCall += CheckAndRebuild;
    }

    [MenuItem("RTS/资源接入/强制重建所有Prefab")]
    public static void ForceRebuild()
    {
        EditorPrefs.DeleteKey(EditorPrefKey);
        PrefabBuilder.BuildAllPrefabs();
        EditorPrefs.SetString(EditorPrefKey, CurrentVersion);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Prefab 重建", "全部 Prefab 已重新生成（含 CastleKit 旗帜 + TrainKit 铁路 + 攻城塔残骸）。", "好的");
    }
}
