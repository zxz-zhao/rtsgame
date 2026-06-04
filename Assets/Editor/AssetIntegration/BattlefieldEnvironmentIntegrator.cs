using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 将 Kenney Survival Kit 战场道具集成到项目中，并为现有单位应用军事配色
/// </summary>
public class BattlefieldEnvironmentIntegrator : EditorWindow
{
    [MenuItem("星火RTS/集成/战场环境 & 军事配色")]
    public static void ShowWindow()
    {
        GetWindow<BattlefieldEnvironmentIntegrator>("战场环境集成");
    }

    private static readonly string SurvivalKitFbxPath =
        "Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format";
    private static readonly string CarKitFbxPath =
        "Assets/External/MilitaryModels/Kenney/Extracted/CarKit/Models/FBX format";
    private static readonly string PropPrefabOutputPath = "Assets/Prefabs/BattlefieldProps";

    // 战场氛围道具列表（从 Survival Kit 选取）
    private static readonly string[] BattlefieldPropModels = new[]
    {
        "barrel", "barrel-open",
        "tent", "tent-canvas",
        "fence", "fence-fortified",
        "metal-panel", "metal-panel-screws",
        "structure-metal", "structure-metal-wall",
        "box", "box-large", "box-large-open",
        "campfire-pit",
        "rock-a", "rock-b", "rock-c",
        "tree", "tree-tall",
        "signpost"
    };

    // 军事配色方案
    private static readonly Color MilitaryGreen = new Color(0.25f, 0.35f, 0.20f);
    private static readonly Color DesertSand    = new Color(0.76f, 0.70f, 0.50f);
    private static readonly Color DarkMetal     = new Color(0.22f, 0.22f, 0.22f);
    private static readonly Color DirtBrown     = new Color(0.45f, 0.32f, 0.18f);

    void OnGUI()
    {
        GUILayout.Label("战场环境 & 军事配色集成", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("① 创建战场道具 Prefab", GUILayout.Height(36)))
            CreateBattlefieldPropPrefabs();

        EditorGUILayout.Space();

        if (GUILayout.Button("② 给现有单位应用军事配色", GUILayout.Height(36)))
            ApplyMilitaryColorsToUnits();

        EditorGUILayout.Space();

        if (GUILayout.Button("③ 一键执行全部", GUILayout.Height(36)))
        {
            CreateBattlefieldPropPrefabs();
            ApplyMilitaryColorsToUnits();
        }
    }

    // ──────────────────────────────────────────────────
    // ① 创建战场道具 Prefab
    // ──────────────────────────────────────────────────
    [MenuItem("星火RTS/集成/① 创建战场道具Prefab")]
    public static void CreateBattlefieldPropPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PropPrefabOutputPath))
        {
            string parent = Path.GetDirectoryName(PropPrefabOutputPath).Replace('\\', '/');
            string folder = Path.GetFileName(PropPrefabOutputPath);
            AssetDatabase.CreateFolder(parent, folder);
        }

        int created = 0;
        foreach (var modelName in BattlefieldPropModels)
        {
            string fbxPath = $"{SurvivalKitFbxPath}/{modelName}.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[BattlefieldIntegrator] 找不到: {fbxPath}");
                continue;
            }

            string prefabPath = $"{PropPrefabOutputPath}/{modelName}.prefab";
            GameObject instance = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
            if (instance == null) continue;

            // 应用战场材质色（橄榄绿/泥土色）
            ApplyColorToRenderers(instance, GetPropColor(modelName));

            // 保存为 Prefab
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            created++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[BattlefieldIntegrator] 创建了 {created} 个战场道具 Prefab → {PropPrefabOutputPath}");
        EditorUtility.DisplayDialog("完成", $"创建了 {created} 个战场道具 Prefab\n路径: {PropPrefabOutputPath}", "OK");
    }

    // ──────────────────────────────────────────────────
    // ② 给现有单位 Prefab 应用军事配色
    // ──────────────────────────────────────────────────
    [MenuItem("星火RTS/集成/② 给单位应用军事配色")]
    public static void ApplyMilitaryColorsToUnits()
    {
        // 单位 Prefab → 配色
        var unitColors = new Dictionary<string, Color>
        {
            { "Tank",      MilitaryGreen },
            { "Artillery", MilitaryGreen },
            { "Fighter",   DarkMetal     },
            { "Bomber",    DarkMetal     },
            { "Soldier",   DirtBrown     },
            { "Infantry",  DirtBrown     },
        };

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int modified = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            Color targetColor = Color.clear;
            foreach (var kv in unitColors)
            {
                if (fileName.Contains(kv.Key))
                {
                    targetColor = kv.Value;
                    break;
                }
            }

            if (targetColor == Color.clear) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            using (var editScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                ApplyColorToRenderers(editScope.prefabContentsRoot, targetColor);
                modified++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[BattlefieldIntegrator] 已为 {modified} 个单位 Prefab 应用军事配色");
        EditorUtility.DisplayDialog("完成", $"已为 {modified} 个单位应用军事配色", "OK");
    }

    // ──────────────────────────────────────────────────
    // 工具方法
    // ──────────────────────────────────────────────────
    static void ApplyColorToRenderers(GameObject go, Color color)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                Material newMat = new Material(mats[i]);
                RendererColorUtil.TrySetColor(newMat, color);
                mats[i] = newMat;
            }
            r.sharedMaterials = mats;
        }
    }

    static Color GetPropColor(string modelName)
    {
        if (modelName.Contains("barrel") || modelName.Contains("metal") || modelName.Contains("structure"))
            return DarkMetal;
        if (modelName.Contains("box") || modelName.Contains("fence") || modelName.Contains("signpost"))
            return DirtBrown;
        if (modelName.Contains("tent") || modelName.Contains("campfire"))
            return DesertSand;
        if (modelName.Contains("rock") || modelName.Contains("tree"))
            return new Color(0.40f, 0.38f, 0.30f);
        return MilitaryGreen;
    }
}
