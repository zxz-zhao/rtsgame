using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 战场道具尺寸测试工具
/// 对比实际模型 Bounds 与目标尺寸，自动应用缩放修正
/// 菜单：星火RTS → 集成 → 道具尺寸测试
/// </summary>
public class PropSizeTester : EditorWindow
{
    // ── 游戏单位目标尺寸参考表（米）──────────────────────
    private static readonly string UnitSizeRef =
        "Tank H=3.0 FP=3.6 | Artillery H=1.55 FP=1.35 | Infantry H=3.6 FP=2.0\n" +
        "Fighter H=1.6 FP=4.0 | Bomber H=2.0 FP=6.0 | Scout H=1.5 FP=3.5\n" +
        "建筑: MainBase H=10 FP=12 | Barracks H=5 FP=8 | PowerPlant H=6 FP=8";

    // ── 道具目标尺寸表 (name → targetHeight, targetFootprint) ──
    private static readonly Dictionary<string, Vector2> PropTargetSizes = new Dictionary<string, Vector2>
    {
        { "barrel",           new Vector2(1.2f, 0.6f) },
        { "barrel-open",      new Vector2(1.2f, 0.6f) },
        { "tent",             new Vector2(2.5f, 4.0f) },
        { "tent-canvas",      new Vector2(2.5f, 4.5f) },
        { "tent-canvas-half", new Vector2(1.8f, 3.0f) },
        { "fence",            new Vector2(1.5f, 3.0f) },
        { "fence-fortified",  new Vector2(2.0f, 3.0f) },
        { "fence-doorway",    new Vector2(2.0f, 1.5f) },
        { "metal-panel",      new Vector2(2.0f, 1.0f) },
        { "metal-panel-screws",  new Vector2(2.0f, 1.0f) },
        { "structure-metal",     new Vector2(3.0f, 4.0f) },
        { "structure-metal-wall",new Vector2(3.0f, 3.0f) },
        { "box",              new Vector2(0.8f, 0.6f) },
        { "box-large",        new Vector2(1.2f, 1.0f) },
        { "box-large-open",   new Vector2(1.2f, 1.0f) },
        { "campfire-pit",     new Vector2(0.4f, 0.8f) },
        { "rock-a",           new Vector2(1.5f, 2.0f) },
        { "rock-b",           new Vector2(1.0f, 1.5f) },
        { "rock-c",           new Vector2(0.8f, 1.2f) },
        { "tree",             new Vector2(5.0f, 3.0f) },
        { "tree-tall",        new Vector2(7.0f, 3.0f) },
        { "signpost",         new Vector2(1.8f, 0.5f) },
    };

    private static readonly string SurvivalKitFbxPath =
        "Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format";
    private static readonly string PropPrefabPath = "Assets/Prefabs/BattlefieldProps";

    // ── UI 状态 ──────────────────────────────────────────
    private Vector2 scroll;
    private List<PropSizeResult> results = new List<PropSizeResult>();
    private bool tested = false;

    private class PropSizeResult
    {
        public string name;
        public Vector3 actualSize;
        public Vector2 target;
        public float scaleApplied;
        public string status;
        public Color statusColor;
    }

    [MenuItem("星火RTS/集成/③ 道具尺寸测试 & 自动修正")]
    public static void ShowWindow() => GetWindow<PropSizeTester>("道具尺寸测试");

    void OnGUI()
    {
        GUILayout.Label("战场道具尺寸测试工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("单位参考尺寸:\n" + UnitSizeRef, MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("① 测试所有道具实际尺寸", GUILayout.Height(36)))
            RunSizeTest(applyFix: false);

        if (GUILayout.Button("② 测试 + 自动修正缩放", GUILayout.Height(36)))
            RunSizeTest(applyFix: true);

        EditorGUILayout.Space();

        if (!tested) return;

        // 结果表格
        GUILayout.Label($"结果（共 {results.Count} 个）:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("道具名", GUILayout.Width(180));
        GUILayout.Label("实际 H×W×D (m)", GUILayout.Width(160));
        GUILayout.Label("目标 H / FP", GUILayout.Width(110));
        GUILayout.Label("缩放", GUILayout.Width(60));
        GUILayout.Label("状态");
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(380));
        foreach (var r in results)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(r.name, GUILayout.Width(180));
            GUILayout.Label($"{r.actualSize.y:F2} × {r.actualSize.x:F2} × {r.actualSize.z:F2}", GUILayout.Width(160));

            if (r.target != Vector2.zero)
                GUILayout.Label($"H{r.target.x:F1} / FP{r.target.y:F1}", GUILayout.Width(110));
            else
                GUILayout.Label("(无目标)", GUILayout.Width(110));

            GUILayout.Label(r.scaleApplied > 0 ? $"×{r.scaleApplied:F2}" : "-", GUILayout.Width(60));

            var oldColor = GUI.color;
            GUI.color = r.statusColor;
            GUILayout.Label(r.status);
            GUI.color = oldColor;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    static void RunSizeTest(bool applyFix)
    {
        var win = GetWindow<PropSizeTester>("道具尺寸测试");
        win.results.Clear();
        win.tested = true;

        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { SurvivalKitFbxPath });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("找不到模型", $"请确认路径存在:\n{SurvivalKitFbxPath}", "OK");
            return;
        }

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            string modelName = Path.GetFileNameWithoutExtension(assetPath);
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (fbx == null) continue;

            // 实例化取实际 Bounds
            GameObject inst = PrefabUtility.InstantiatePrefab(fbx) as GameObject;
            if (inst == null) inst = Instantiate(fbx);

            Bounds b = GetRendererBounds(inst);
            Vector3 actualSize = b.size;
            DestroyImmediate(inst);

            var r = new PropSizeResult { name = modelName, actualSize = actualSize };

            PropTargetSizes.TryGetValue(modelName, out Vector2 target);
            r.target = target;

            if (target == Vector2.zero)
            {
                r.status = "无目标尺寸（跳过）";
                r.statusColor = Color.gray;
            }
            else
            {
                float tH = target.x, tFP = target.y;
                float curH  = Mathf.Max(actualSize.y, 0.01f);
                float curFP = Mathf.Max(Mathf.Max(actualSize.x, actualSize.z), 0.01f);
                float kH = tH / curH;
                float kFP = tFP / curFP;
                float k = Mathf.Min(kH, kFP);

                if (Mathf.Abs(k - 1f) < 0.05f)
                {
                    r.status = "✓ 尺寸合适";
                    r.statusColor = Color.green;
                }
                else if (k > 1f)
                {
                    r.status = $"⚠ 太小 需放大 ×{k:F2}";
                    r.statusColor = Color.yellow;
                }
                else
                {
                    r.status = $"⚠ 太大 需缩小 ×{k:F2}";
                    r.statusColor = new Color(1f, 0.5f, 0f);
                }

                r.scaleApplied = applyFix ? k : 0f;

                // 修正：更新 Prefab 的 import scale
                if (applyFix && Mathf.Abs(k - 1f) >= 0.05f)
                {
                    ApplyImportScale(assetPath, k);
                    r.status = $"✓ 已修正 ×{k:F2}";
                    r.statusColor = Color.cyan;
                }
            }

            win.results.Add(r);
        }

        AssetDatabase.Refresh();
        win.Repaint();
        Debug.Log($"[PropSizeTester] 测试完成，共 {win.results.Count} 个道具" +
                  (applyFix ? "，已自动修正缩放" : ""));
    }

    static void ApplyImportScale(string assetPath, float scaleFactor)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return;
        importer.globalScale = Mathf.Clamp(importer.globalScale * scaleFactor, 0.001f, 1000f);
        importer.SaveAndReimport();
    }

    static Bounds GetRendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
