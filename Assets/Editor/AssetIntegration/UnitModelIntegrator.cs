using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 单位 / 建筑 视觉模型替换工具。
/// 用法：菜单 RTS → 资源接入 → 单位/建筑模型绑定。
///   1) 下载 Kenney（CC0）/ Quaternius / 自有 fbx 模型，导入到项目；
///   2) 把模型 prefab 拖到对应槽位（每种单位/建筑一格）；
///   3) 点 "应用到所有 Prefab"，会遍历 Assets/Prefabs 与 Resources/Prefabs 下
///      所有匹配命名的 prefab，把它们的 "Model" 子节点替换为新模型。
/// 不会改任何脚本组件、NavMeshAgent、碰撞体等运行时设置。
/// </summary>
public class UnitModelIntegrator : EditorWindow
{
    static readonly string[] UnitKeys = {
        "Infantry", "Tank", "Artillery", "Flamethrower",
        "Fighter", "Bomber", "ScoutPlane"
    };
    static readonly string[] BuildingKeys = {
        "MainBase", "Barracks", "TankFactory", "ArmorFactory", "AirFactory",
        "Turret", "GoldMine", "PowerPlant"
    };

    Dictionary<string, GameObject> unitModels = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> buildingModels = new Dictionary<string, GameObject>();
    Dictionary<string, Vector3> unitScales = new Dictionary<string, Vector3>();
    Dictionary<string, Vector3> buildingScales = new Dictionary<string, Vector3>();
    bool keepProgrammaticVisual = false;
    Vector2 scroll;

    [MenuItem("RTS/资源接入/单位与建筑模型绑定")]
    static void Open()
    {
        GetWindow<UnitModelIntegrator>("模型绑定").minSize = new Vector2(500f, 600f);
    }

    void OnEnable()
    {
        foreach (var k in UnitKeys)
        {
            unitModels[k] = null;
            unitScales[k] = Vector3.one;
        }
        foreach (var k in BuildingKeys)
        {
            buildingModels[k] = null;
            buildingScales[k] = Vector3.one;
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("单位与建筑模型绑定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "为每种单位/建筑指定一个新模型 prefab（或 fbx）。\n"
            + "应用后，会扫描 Assets/Prefabs 与 Assets/Resources/Prefabs 下所有匹配的 prefab，\n"
            + "替换其 \"Model\" 子节点为新模型，保留原 NavMeshAgent / 碰撞 / 脚本不变。\n"
            + "推荐资源（CC0 免费）：\n"
            + " · Kenney Tower Defense Kit / Tanks / Marble Race（步兵替代）\n"
            + " · Quaternius Modular Buildings / RTS Modular Buildings",
            MessageType.Info);

        keepProgrammaticVisual = EditorGUILayout.ToggleLeft(
            "保留原程序化模型（仅在新模型为空时使用，建议关闭）", keepProgrammaticVisual);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("== 单位 ==", EditorStyles.boldLabel);
        foreach (var k in UnitKeys) DrawSlot(k, unitModels, unitScales);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("== 建筑 ==", EditorStyles.boldLabel);
        foreach (var k in BuildingKeys) DrawSlot(k, buildingModels, buildingScales);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);
        if (GUILayout.Button("应用到所有 Prefab（含 Resources）", GUILayout.Height(36)))
            ApplyAll();

        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox("应用后请重新打开战斗场景查看效果。Resources 下的 prefab 也会更新（联机/AI 会用到）。", MessageType.None);
    }

    void DrawSlot(string key, Dictionary<string, GameObject> models, Dictionary<string, Vector3> scales)
    {
        EditorGUILayout.BeginHorizontal();
        models[key] = (GameObject)EditorGUILayout.ObjectField(key, models[key], typeof(GameObject), false, GUILayout.Height(20));
        var sc = scales[key];
        EditorGUI.BeginChangeCheck();
        float uniform = EditorGUILayout.FloatField(sc.x, GUILayout.Width(60));
        if (EditorGUI.EndChangeCheck())
            scales[key] = Vector3.one * Mathf.Max(0.01f, uniform);
        EditorGUILayout.EndHorizontal();
    }

    void ApplyAll()
    {
        int unitCount = 0, bldCount = 0;
        // 单位
        foreach (var k in UnitKeys)
        {
            if (unitModels[k] == null && !keepProgrammaticVisual) continue;
            unitCount += ApplyToMatchingPrefabs(k, unitModels[k], unitScales[k]);
        }
        // 建筑
        foreach (var k in BuildingKeys)
        {
            if (buildingModels[k] == null && !keepProgrammaticVisual) continue;
            bldCount += ApplyToMatchingPrefabs(k, buildingModels[k], buildingScales[k]);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("模型绑定", $"已替换 {unitCount} 个单位 prefab、{bldCount} 个建筑 prefab。", "确定");
    }

    /// <summary>遍历 Prefabs 与 Resources/Prefabs，名字以 baseKey 开头的所有 prefab 替换 Model 子节点。</summary>
    int ApplyToMatchingPrefabs(string baseKey, GameObject newModel, Vector3 scale)
    {
        if (newModel == null) return 0;
        int n = 0;
        string[] dirs = {
            "Assets/Prefabs",
            "Assets/Resources/Prefabs"
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!IsMatch(fileName, baseKey)) continue;
                if (ReplaceModelInPrefab(path, newModel, scale)) n++;
            }
        }
        return n;
    }

    static bool IsMatch(string fileName, string baseKey)
    {
        // 匹配 "Infantry"、"Infantry_P"、"Infantry_E"、"Infantry_Player"、"Infantry_Enemy"
        if (fileName == baseKey) return true;
        return fileName.StartsWith(baseKey + "_", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>打开 prefab，找到 "Model" 子节点，删其所有子物体，挂上 newModel 实例。</summary>
    static bool ReplaceModelInPrefab(string prefabPath, GameObject newModel, Vector3 scale)
    {
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents == null) return false;

        try
        {
            var modelTr = contents.transform.Find("Model");
            if (modelTr == null)
            {
                var m = new GameObject("Model");
                m.transform.SetParent(contents.transform, false);
                modelTr = m.transform;
            }
            // 清空 Model 子节点
            for (int i = modelTr.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(modelTr.GetChild(i).gameObject);
            // 移除 Model 上的 Renderer / Mesh / Collider（如果有）
            foreach (var r in modelTr.GetComponents<Renderer>()) Object.DestroyImmediate(r);
            foreach (var mf in modelTr.GetComponents<MeshFilter>()) Object.DestroyImmediate(mf);

            // 实例化新模型作为 Model 的子物体
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(newModel, modelTr);
            if (inst == null)
            {
                inst = Object.Instantiate(newModel, modelTr);
            }
            inst.name = "Visual";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = scale;

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
