using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 模型替换工具：将下载的 FBX 模型应用到各单位/建筑 Prefab 的 Model 节点。
/// 菜单：星火RTS → 模型 → 替换为下载模型
/// </summary>
public class ModelSwapperWindow : EditorWindow
{
    // FBX 资产路径（相对 Assets 目录）
    const string UnitModelPath     = "Assets/External/Models/Units";
    const string BuildingModelPath = "Assets/External/Models/Buildings";

    // Prefab 路径（真正的游戏 Prefab 在 Resources/Prefabs）
    const string UnitPrefabPath     = "Assets/Resources/Prefabs";
    const string BuildingPrefabPath = "Assets/Resources/Prefabs";

    // (fbx名, prefab名, 描述, 旋转修正Euler)
    static readonly (string fbx, string prefabName, string label, Vector3 rot)[] UnitMappings =
    {
        // 步兵：玩家版 _Player/_Enemy，AI 用裸名 Infantry
        ("Infantry_Model",    "Infantry_Player",    "步兵（蓝）",    Vector3.zero),
        ("Infantry_Model",    "Infantry_Enemy",     "步兵（红）",    Vector3.zero),
        ("Infantry_Model",    "Infantry",           "步兵（AI）",    Vector3.zero),
        // 喷火：游戏加载 Flamethrower_P / Flamethrower_E
        ("Flamethrower_Model","Flamethrower_P",     "喷火兵（蓝）",  Vector3.zero),
        ("Flamethrower_Model","Flamethrower_E",     "喷火兵（红）",  Vector3.zero),
        ("Flamethrower_Model","Flamethrower",       "喷火兵（AI）",  Vector3.zero),
        // 坦克：_P/_E 是从建筑生产出来的，_Player/_Enemy 为兼容旧场景保留
        ("Tank_Model",        "Tank_P",             "坦克（蓝）",    new Vector3(0,-90,0)),
        ("Tank_Model",        "Tank_E",             "坦克（红）",    new Vector3(0,-90,0)),
        ("Tank_Model",        "Tank_Player",        "坦克（蓝旧）",  new Vector3(0,-90,0)),
        ("Tank_Model",        "Tank_Enemy",         "坦克（红旧）",  new Vector3(0,-90,0)),
        ("Tank_Model",        "Tank",               "坦克（AI）",    new Vector3(0,-90,0)),
        // 战斗机
        ("Fighter_Model",     "Fighter_P",          "战斗机（蓝）",  new Vector3(0,-90,0)),
        ("Fighter_Model",     "Fighter_E",          "战斗机（红）",  new Vector3(0,-90,0)),
        ("Fighter_Model",     "Fighter_Player",     "战斗机（蓝旧）",new Vector3(0,-90,0)),
        ("Fighter_Model",     "Fighter_Enemy",      "战斗机（红旧）",new Vector3(0,-90,0)),
        ("Fighter_Model",     "Fighter",            "战斗机（AI）",  new Vector3(0,-90,0)),
        // 轰炸机
        ("Bomber_Model",      "Bomber_P",           "轰炸机（蓝）",  new Vector3(0,-90,0)),
        ("Bomber_Model",      "Bomber_E",           "轰炸机（红）",  new Vector3(0,-90,0)),
        ("Bomber_Model",      "Bomber_Player",      "轰炸机（蓝旧）",new Vector3(0,-90,0)),
        ("Bomber_Model",      "Bomber_Enemy",       "轰炸机（红旧）",new Vector3(0,-90,0)),
        ("Bomber_Model",      "Bomber",             "轰炸机（AI）",  new Vector3(0,-90,0)),
        // 侦察机：只有 _P/_E，无 _Player/_Enemy
        ("ScoutPlane_Model",  "ScoutPlane_P",       "侦察机（蓝）",  new Vector3(0,-90,0)),
        ("ScoutPlane_Model",  "ScoutPlane_E",       "侦察机（红）",  new Vector3(0,-90,0)),
        ("ScoutPlane_Model",  "ScoutPlane",         "侦察机（AI）",  new Vector3(0,-90,0)),
        ("ScoutPlane_Model",  "Scout_Player",       "侦察（蓝旧）",  new Vector3(0,-90,0)),
        ("ScoutPlane_Model",  "Scout_Enemy",        "侦察（红旧）",  new Vector3(0,-90,0)),
    };

    static readonly (string fbx, string prefabName, string label, Vector3 rot)[] BuildingMappings =
    {
        ("MainBase_Model",    "MainBase_P",    "主基地（蓝）",   Vector3.zero),
        ("MainBase_Model",    "MainBase_E",    "主基地（红）",   Vector3.zero),
        ("Barracks_Model",    "Barracks_P",    "兵工厂（蓝）",   Vector3.zero),
        ("Barracks_Model",    "Barracks_E",    "兵工厂（红）",   Vector3.zero),
        ("AirFactory_Model",  "AirFactory_P",  "飞机厂（蓝）",   Vector3.zero),
        ("AirFactory_Model",  "AirFactory_E",  "飞机厂（红）",   Vector3.zero),
        ("TankFactory_Model", "TankFactory_P", "特需厂（蓝）",   Vector3.zero),
        ("TankFactory_Model", "TankFactory_E", "特需厂（红）",   Vector3.zero),
        ("TankFactory_Model", "ArmorFactory_P","坦克厂（蓝）",   Vector3.zero),
        ("TankFactory_Model", "ArmorFactory_E","坦克厂（红）",   Vector3.zero),
        ("PowerPlant_Model",  "PowerPlant_P",  "电厂（蓝）",    Vector3.zero),
        ("PowerPlant_Model",  "PowerPlant_E",  "电厂（红）",    Vector3.zero),
        ("GoldMine_Model",    "GoldMine_P",    "金矿（蓝）",    Vector3.zero),
        ("GoldMine_Model",    "GoldMine_E",    "金矿（红）",    Vector3.zero),
        ("Turret_Model",      "Turret_Player", "炮塔（蓝）",    Vector3.zero),
        ("Turret_Model",      "Turret_Enemy",  "炮塔（红）",    Vector3.zero),
    };

    Vector2 scroll;
    string log = "";
    bool forceReplace = false;

    [MenuItem("星火RTS/模型/替换为下载模型")]
    static void Open() => GetWindow<ModelSwapperWindow>("模型替换");

    void OnGUI()
    {
        EditorGUILayout.LabelField("模型替换工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "此工具会在各 Prefab 的 Model 子节点下添加下载的 FBX 模型，并移除旧的手工几何体。\n" +
            "确保已切到 Unity Editor 让 FBX 完成导入（Assets/External/Models/ 下出现 .meta 文件）再操作。",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("① 刷新 AssetDatabase（先导入FBX）", GUILayout.Height(30)))
        {
            AssetDatabase.Refresh();
            log += "[刷新] AssetDatabase 刷新完成\n";
        }

        EditorGUILayout.Space();

        forceReplace = EditorGUILayout.ToggleLeft("强制覆盖（删除旧实例重新替换）", forceReplace);
        EditorGUILayout.Space();

        if (GUILayout.Button("② 替换所有单位模型", GUILayout.Height(30)))
            SwapAll(UnitMappings, UnitModelPath);

        if (GUILayout.Button("③ 替换所有建筑模型", GUILayout.Height(30)))
            SwapAll(BuildingMappings, BuildingModelPath);

        EditorGUILayout.Space();
        if (GUILayout.Button("④ 一键全部替换", GUILayout.Height(36)))
        {
            AssetDatabase.Refresh();
            SwapAll(UnitMappings, UnitModelPath);
            SwapAll(BuildingMappings, BuildingModelPath);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("清空日志")) log = "";

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void SwapAll((string fbx, string prefabName, string label, Vector3 rot)[] mappings, string fbxBasePath)
    {
        // 根据 fbxBasePath 选择对应 Prefab 目录
        string prefabBasePath = fbxBasePath.Contains("Units")
            ? UnitPrefabPath : BuildingPrefabPath;
        int ok = 0, skip = 0, fail = 0;
        foreach (var (fbx, prefabName, label, rot) in mappings)
        {
            string result = SwapPrefabModel(fbx, prefabName, fbxBasePath, prefabBasePath, rot, forceReplace);
            if (result.StartsWith("OK"))   { ok++;   log += $"[✓] {label}: {result}\n"; }
            else if (result.StartsWith("SKIP")) { skip++; log += $"[·] {label}: {result}\n"; }
            else                               { fail++; log += $"[✗] {label}: {result}\n"; }
        }
        log += $"--- 完成: {ok} 成功 / {skip} 跳过 / {fail} 失败 ---\n";
        AssetDatabase.SaveAssets();
        Repaint();
    }

    static string SwapPrefabModel(string fbxName, string prefabName, string fbxBasePath, string prefabBasePath, Vector3 eulerCorrection = default, bool forceReplace = false)
    {
        // 找 FBX 资产
        string modelPath = FindModelAssetPath(fbxBasePath, fbxName);
        if (string.IsNullOrEmpty(modelPath))
            return $"SKIP: Model not found ({fbxBasePath}/{fbxName}.glb/.gltf/.fbx/.prefab)";

        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (fbxAsset == null)
            return $"SKIP: Model import failed ({modelPath})";

        // 找 Prefab（优先 Resources/Prefabs）
        string prefabPath = $"{prefabBasePath}/{prefabName}.prefab";
        if (!File.Exists(Path.Combine(Application.dataPath, "..", prefabPath)))
            prefabPath = $"Assets/Resources/Prefabs/{prefabName}.prefab";
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null)
            return $"SKIP: Prefab 未找到 ({prefabPath})";

        float appliedScale = 1f;
        using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = scope.prefabContentsRoot;

            // 找 Model 子节点（UnitScaleNormalizer 在此节点下缩放）
            Transform modelNode = root.transform.Find("Model");
            if (modelNode == null)
            {
                // 没有 Model 节点则创建
                var go = new GameObject("Model");
                go.transform.SetParent(root.transform, false);
                modelNode = go.transform;
            }

            // 检查是否已添加过
            var existing = modelNode.Find(fbxName + "_Instance");
            if (existing != null)
            {
                if (!forceReplace) return "SKIP: 模型已存在，跳过";
                Object.DestroyImmediate(existing.gameObject, true);
            }

            // 隐藏 Model 节点下所有旧子节点（不删除，避免嵌套 Prefab 报错）
            foreach (Transform child in modelNode)
            {
                string n = child.name;
                if (n == "HPBar" || n == "Label" || n == "FX" || n == "SelectionCircle")
                    continue;
                if (n.EndsWith("_Instance")) continue;
                child.gameObject.SetActive(false);
            }

            // 实例化 FBX 并附到 Model 节点
            GameObject instance = Object.Instantiate(fbxAsset);
            instance.name = fbxName + "_Instance";
            instance.transform.SetParent(modelNode, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = eulerCorrection == Vector3.zero
                ? Quaternion.identity
                : Quaternion.Euler(eulerCorrection);
            instance.transform.localScale    = Vector3.one;
            appliedScale = ApplyTargetScaleIfKnown(instance, prefabName);
        }

        return appliedScale != 1f ? $"OK ({modelPath}, scale x{appliedScale:F3})" : $"OK ({modelPath})";
    }

    static float ApplyTargetScaleIfKnown(GameObject modelRoot, string prefabName)
    {
        if (modelRoot == null || string.IsNullOrEmpty(prefabName)) return 1f;
        if (!TryGetTargetSize(prefabName, out float targetHeight, out float targetFootprint)) return 1f;
        if (!TryGetRendererBounds(modelRoot, out Bounds bounds)) return 1f;

        float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
        float currentFootprint = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z), 0.01f);
        float scale = Mathf.Min(targetHeight / currentHeight, targetFootprint / currentFootprint);
        if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) return 1f;

        modelRoot.transform.localScale *= scale;
        return scale;
    }

    static bool TryGetTargetSize(string prefabName, out float targetHeight, out float targetFootprint)
    {
        string key = NormalizePrefabKey(prefabName);
        switch (key)
        {
            case "Infantry":
                targetHeight = 3.6f; targetFootprint = 2.0f; return true;
            case "Artillery":
                targetHeight = 3.6f; targetFootprint = 2.0f; return true;
            case "Flamethrower":
                targetHeight = 1.3f; targetFootprint = 1.0f; return true;
            case "Tank":
                targetHeight = 3.0f; targetFootprint = 3.6f; return true;
            case "Fighter":
                targetHeight = 1.2f; targetFootprint = 2.6f; return true;
            case "Bomber":
                targetHeight = 2.0f; targetFootprint = 6.0f; return true;
            case "ScoutPlane":
            case "Scout":
                targetHeight = 1.5f; targetFootprint = 3.5f; return true;
            case "MainBase":
                targetHeight = 10f; targetFootprint = 12f; return true;
            case "Barracks":
                targetHeight = 5f; targetFootprint = 8f; return true;
            case "AirFactory":
                targetHeight = 6f; targetFootprint = 10f; return true;
            case "TankFactory":
            case "ArmorFactory":
                targetHeight = 5.5f; targetFootprint = 10f; return true;
            case "PowerPlant":
                targetHeight = 6f; targetFootprint = 8f; return true;
            case "GoldMine":
                targetHeight = 5.2f; targetFootprint = 8f; return true;
            case "Turret":
                targetHeight = 4f; targetFootprint = 4f; return true;
            default:
                targetHeight = 0f; targetFootprint = 0f; return false;
        }
    }

    static string NormalizePrefabKey(string prefabName)
    {
        string key = prefabName;
        string[] suffixes = { "_Player", "_Enemy", "_P", "_E" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (key.EndsWith(suffixes[i], System.StringComparison.OrdinalIgnoreCase))
                return key.Substring(0, key.Length - suffixes[i].Length);
        }

        return key;
    }

    static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
        if (root == null) return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool first = true;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
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

    static string FindModelAssetPath(string basePath, string modelName)
    {
        string[] extensions = { ".glb", ".gltf", ".fbx", ".prefab" };
        for (int i = 0; i < extensions.Length; i++)
        {
            string candidate = $"{basePath}/{modelName}{extensions[i]}";
            if (File.Exists(Path.Combine(Application.dataPath, "..", candidate)))
                return candidate;
        }

        return null;
    }
}
