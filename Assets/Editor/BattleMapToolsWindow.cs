using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BattleMapToolsWindow : EditorWindow
{
    const string CatalogPath = "Assets/Scripts/Core/BattleMapCatalog.cs";
    const string GuidePath = "TERRAIN_MAP_GUIDE.md";

    int selectedMapIndex;
    Vector2 scroll;

    [MenuItem("RTS/地图/地图工具窗口")]
    public static void Open()
    {
        var window = GetWindow<BattleMapToolsWindow>("地图工具");
        window.minSize = new Vector2(380f, 430f);
    }

    void OnGUI()
    {
        string[] mapNames = BattleMapCatalog.GetAllMapNames();
        if (mapNames.Length == 0)
        {
            EditorGUILayout.HelpBox("没有可用地图，请检查 BattleMapCatalog。", MessageType.Warning);
            return;
        }

        selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, mapNames.Length - 1);
        BattleMapDefinition map = BattleMapCatalog.Get(mapNames[selectedMapIndex]);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("地形地图工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("这里用于快速预览地图、设置本机测试地图，并定位到地形数据。实际布局仍统一维护在 BattleMapCatalog.cs。", MessageType.Info);

        selectedMapIndex = EditorGUILayout.Popup("当前地图", selectedMapIndex, mapNames);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawMapInfo(map);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6f);
        if (GUILayout.Button("生成当前地图预览", GUILayout.Height(32f)))
        {
            SceneBuilder.BuildGameScene(map.Name);
            ShowNotification(new GUIContent("地图预览已生成"));
        }

        if (GUILayout.Button("设为本机运行地图", GUILayout.Height(28f)))
        {
            PlayerPrefs.SetString("current_map", map.Name);
            PlayerPrefs.Save();
            ShowNotification(new GUIContent("已设置：" + map.Name));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("打开地图数据脚本"))
            OpenCatalogScript();
        if (GUILayout.Button("打开修改说明"))
            OpenGuide();
        EditorGUILayout.EndHorizontal();
    }

    static void DrawMapInfo(BattleMapDefinition map)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("地图名", map.Name);
        EditorGUILayout.LabelField("随机种子", map.RandomSeed.ToString());
        EditorGUILayout.LabelField("地图半径", BattleMapCatalog.MapHalfSize.ToString("0"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("布局元素", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("道路", Count(map.Roads).ToString());
        EditorGUILayout.LabelField("水面", Count(map.Waters).ToString());
        EditorGUILayout.LabelField("地面色块", Count(map.Patches).ToString());
        EditorGUILayout.LabelField("岩石群", Count(map.RockClusters).ToString());
        EditorGUILayout.LabelField("树木", Count(map.TreePositions).ToString());
        EditorGUILayout.LabelField("废墟墙", Count(map.RuinWalls).ToString());
        EditorGUILayout.LabelField("沙袋阵地", Count(map.SandbagRings).ToString());

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("视觉参数", EditorStyles.boldLabel);
        DrawColor("地面", map.GroundColor);
        DrawColor("道路", map.RoadColor);
        DrawColor("水面", map.WaterColor);
        DrawColor("岩石", map.RockColor);
        DrawColor("树冠", map.FoliageColor);
        DrawColor("雾色", map.FogColor);
        EditorGUILayout.LabelField("雾效距离", map.FogStart.ToString("0") + " - " + map.FogEnd.ToString("0"));
    }

    static int Count<T>(T[] values)
    {
        return values == null ? 0 : values.Length;
    }

    static void DrawColor(string label, Color color)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(64f));
            EditorGUILayout.ColorField(color);
        }
    }

    static void OpenCatalogScript()
    {
        var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(CatalogPath);
        if (asset == null) return;

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        AssetDatabase.OpenAsset(asset);
    }

    static void OpenGuide()
    {
        string fullPath = Path.GetFullPath(GuidePath);
        if (!File.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("地图修改说明", "找不到 " + GuidePath, "确定");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }
}
