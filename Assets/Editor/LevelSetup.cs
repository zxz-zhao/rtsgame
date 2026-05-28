using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

// 在游戏场景中自动摆放玩家基地和敌方基地，可直接Press Play测试
public class LevelSetup
{
    [MenuItem("RTS/生成场景/④ 在游戏场景中放置基地和单位")]
    public static void SetupLevel()
    {
        // 确保在 GameScene
        string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (current != "GameScene")
        {
            bool open = EditorUtility.DisplayDialog("提示",
                "请先打开 GameScene（双击 Assets/Scenes/GameScene.unity）后再执行此操作", "确定");
            return;
        }

        // ── 玩家主基地（左下角）──
        PlaceBuilding("MainBase_Player", new Vector3(-80, 0, -80));
        PlaceBuilding("GoldMine_Player", new Vector3(-64, 0, -80));
        PlaceBuilding("PowerPlant_Player", new Vector3(-80, 0, -64));

        // 玩家初始单位
        PlaceUnit("Infantry_Player", new Vector3(-70, 0, -70));
        PlaceUnit("Infantry_Player", new Vector3(-66, 0, -70));
        PlaceUnit("Tank_Player",     new Vector3(-72, 0, -76));

        // ── 敌方主基地（右上角）──
        PlaceBuilding("MainBase_Enemy",    new Vector3(80, 0, 80));
        PlaceBuilding("Barracks_Enemy",    new Vector3(66, 0, 80));
        PlaceBuilding("GoldMine_Enemy",    new Vector3(80, 0, 66));
        PlaceBuilding("PowerPlant_Enemy",  new Vector3(64, 0, 66));

        // 敌方初始单位（由AIController控制生成更多）
        PlaceUnit("Infantry_Enemy", new Vector3(70, 0, 70));
        PlaceUnit("Infantry_Enemy", new Vector3(74, 0, 70));
        PlaceUnit("Tank_Enemy",     new Vector3(72, 0, 76));

        // ── 中立金矿（中间）──
        PlaceBuilding("GoldMine_Player", new Vector3(0, 0, 10));
        PlaceBuilding("GoldMine_Enemy",  new Vector3(0, 0, -10));

        // 保存场景
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✅ 关卡布置完毕！按 Play 即可开始测试。");
    }

    static void PlaceBuilding(string prefabName, Vector3 pos)
    {
        string path = $"Assets/Prefabs/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"Prefab 不存在: {path}，请先执行 ③ 生成Prefab"); return; }
        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (go != null) go.transform.position = pos;
    }

    static void PlaceUnit(string prefabName, Vector3 pos)
    {
        string path = $"Assets/Prefabs/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"Prefab 不存在: {path}，请先执行 ③ 生成Prefab"); return; }
        GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (go != null) go.transform.position = pos;
    }
}
