using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;

// 一键完成所有剩余工作并进入Play模式测试
public class QuickTest
{
    [MenuItem("RTS/▶ 一键完成并测试 GameScene")]
    public static void RunAll()
    {
        // 如果在Play模式，先退出再执行
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.playModeStateChanged += OnExitedPlayMode;
            return;
        }
        DoSetupAndPlay();
    }

    static void OnExitedPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.playModeStateChanged -= OnExitedPlayMode;
            EditorApplication.delayCall += DoSetupAndPlay;
        }
    }

    public static void DoSetupAndPlay()
    {
        string scenePath = "Assets/Scenes/GameScene.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("错误", "GameScene 不存在，请先执行 RTS→强制重新初始化", "确定");
            return;
        }

        // 1. 打开 GameScene
        EditorSceneManager.OpenScene(scenePath);

        // 2. 修复所有粉色材质
        FixMaterials();

        // 3. 烘焙 NavMesh
        BakeNavMesh();

        // 4. 保存场景
        EditorSceneManager.SaveOpenScenes();

        // 5. 进入 Play 模式
        EditorApplication.delayCall += () =>
        {
            EditorApplication.isPlaying = true;
            Debug.Log("✅ 已进入Play模式！左键选单位，右键移动/攻击，滚轮缩放");
        };
    }

    static void FixMaterials()
    {
        // 确保材质目录存在
        System.IO.Directory.CreateDirectory("Assets/Resources/Materials");

        // 创建并保存常用材质
        SaveMat("Mat_Blue",       new Color(0.15f, 0.35f, 1f));
        SaveMat("Mat_Red",        new Color(1f, 0.15f, 0.15f));
        SaveMat("Mat_Green",      new Color(0.2f, 0.6f, 0.2f));
        SaveMat("Mat_Yellow",     new Color(1f, 0.85f, 0f));
        SaveMat("Mat_Cyan",       new Color(0f, 0.8f, 0.9f));
        SaveMat("Mat_White",      Color.white);
        SaveMat("Mat_Ground",     new Color(0.28f, 0.45f, 0.28f));
        SaveMat("Mat_EnemyRed",   new Color(0.9f, 0.1f, 0.1f));

        // 重新赋给场景中所有 Renderer
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (r == null || r.sharedMaterial == null) continue;
            Color c = r.sharedMaterial.color;
            string matName = BestMatName(r.gameObject.name, c);
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Resources/Materials/{matName}.mat");
            if (mat != null) r.sharedMaterial = mat;
        }

        // 地面单独处理
        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            var gmat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Mat_Ground.mat");
            if (gmat != null) ground.GetComponent<Renderer>().sharedMaterial = gmat;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("✅ 材质修复完毕");
    }

    static string BestMatName(string goName, Color c)
    {
        // 根据对象名判断
        string n = goName.ToLower();
        if (n.Contains("enemy")) return "Mat_EnemyRed";
        if (n.Contains("player") || n.Contains("front")) return "Mat_Blue";
        if (n.Contains("flag")) return n.Contains("enemy") ? "Mat_EnemyRed" : "Mat_Blue";
        if (n.Contains("ground")) return "Mat_Ground";
        // 根据颜色近似
        if (c.r > 0.7f && c.g < 0.4f) return "Mat_Red";
        if (c.b > 0.7f && c.r < 0.4f) return "Mat_Blue";
        if (c.g > 0.6f && c.r < 0.4f) return "Mat_Green";
        if (c.r > 0.8f && c.g > 0.7f && c.b < 0.3f) return "Mat_Yellow";
        return "Mat_White";
    }

    static void SaveMat(string name, Color color)
    {
        string path = $"Assets/Resources/Materials/{name}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var mat = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(mat, path);
    }

    static void BakeNavMesh()
    {
        try
        {
            // 设置地面为 Navigation Static
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
#pragma warning disable 0618
                GameObjectUtility.SetStaticEditorFlags(ground,
                    GameObjectUtility.GetStaticEditorFlags(ground) | StaticEditorFlags.NavigationStatic);
#pragma warning restore 0618
            }

            // 烘焙
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("✅ NavMesh 烘焙完毕");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"NavMesh 烘焙警告（不影响运行）: {e.Message}");
        }
    }
}
