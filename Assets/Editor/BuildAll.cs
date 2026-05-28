using UnityEngine;
using UnityEditor;

// 一键：生成大厅场景 → 修复登录场景 → 构建APK
// Helper: create a Resources/Materials/MinimapDot material to force Unlit/Color into build
[InitializeOnLoad]
public class BuildAll
{
    static BuildAll() { EditorPrefs.DeleteKey(kBuilding); }  // 启动时清理遗留锁

    const string kBuilding = "RTS_Building";

    [MenuItem("RTS/⚡ 一键全流程（生成场景+打包APK）")]
    public static void RunAll()
    {
        if (EditorPrefs.GetBool(kBuilding, false))
        {
            Debug.LogWarning("[BuildAll] 构建已在进行，跳过重复触发");
            return;
        }
        EditorPrefs.SetBool(kBuilding, true);
        try { RunAllInternal(); }
        finally { EditorPrefs.DeleteKey(kBuilding); }
    }

    static void RunAllInternal()
    {
        // 确保 Unlit/Color shader 包含在 build 中（通过 Resources 材质引用）
        EnsureUnlitColorShader();
        EnsureFactionMaterials();

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤0/4：生成单位Prefab...", 0.02f);
        try { PrefabBuilder.BuildAllPrefabs(); }
        catch (System.Exception e) { Debug.LogError("生成Prefab失败: " + e.Message); }

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤1/4：生成游戏场景（小地图+环境）...", 0.05f);
        try { SceneBuilder.BuildGameScene(); }
        catch (System.Exception e) { Debug.LogError("生成游戏场景失败: " + e.Message); }

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤2/4：生成大厅场景...", 0.3f);
        try { LobbySceneBuilder.BuildLobbyScene(); }
        catch (System.Exception e) { Debug.LogError("生成大厅场景失败: " + e.Message); }

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤3/4：重建登录场景UI...", 0.50f);
        try { SceneBuilder.BuildLoginScene(); }
        catch (System.Exception e) { Debug.LogError("重建登录场景失败: " + e.Message); }

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤3b/4：修复登录场景绑定...", 0.58f);
        try { FixLoginScene.Fix(); }
        catch (System.Exception e) { Debug.LogError("修复登录场景失败: " + e.Message); }

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "自动测试：检查战场、大厅、模型资源...", 0.68f);
        AutomatedProjectTest.RunSmokeTest(true);

        EditorUtility.DisplayProgressBar("星火RTS 自动构建", "步骤4/4：构建 Android APK...", 0.75f);
        EditorUtility.ClearProgressBar();
        AndroidBuildSetup.BuildAndroid();
    }

    static void EnsureUnlitColorShader()
    {
        const string matPath = "Assets/Resources/Materials/MinimapDot.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null) return;
        Shader sh = Shader.Find("Unlit/Color");
        if (sh == null) { Debug.LogWarning("[BuildAll] Unlit/Color shader not found – minimap markers will use Standard."); return; }
        System.IO.Directory.CreateDirectory("Assets/Resources/Materials");
        var mat = new Material(sh);
        mat.color = Color.green;
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildAll] Created Resources/Materials/MinimapDot.mat to force Unlit/Color into build.");
    }

    static void EnsureFactionMaterials()
    {
        // 创建蓝色（玩家）和红色（敌方）阵营材质，确保 shader 打包进 APK
        string matDir = "Assets/Resources/Materials";
        System.IO.Directory.CreateDirectory(matDir);

        CreateFactionMat(matDir + "/FactionBlue.mat", new Color(0.25f, 0.55f, 0.95f));
        CreateFactionMat(matDir + "/FactionRed.mat",  new Color(0.85f, 0.20f, 0.20f));
        AssetDatabase.SaveAssets();
    }

    static void CreateFactionMat(string path, Color c)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var sh = Shader.Find("Standard");
        if (sh == null) { Debug.LogWarning("[BuildAll] Standard shader not found"); return; }
        var mat = new Material(sh);
        mat.color = c;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.3f);
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log("[BuildAll] Created " + path);
    }
}
