using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapEnvironmentPreviewRenderer
{
    const string PreviewScenePath = "Assets/Scenes/MapEditorPreview.unity";
    const string GameScenePath = "Assets/Scenes/GameScene.unity";
    const string RuntimeMapFolder = "Assets/Resources/MapAssets";
    const int OutputWidth = 1920;
    const int OutputHeight = 1080;

    [MenuItem("RTS/Art/Map Environment/Render Showcase PNGs")]
    public static void RenderShowcasePngs()
    {
        BattleMapDefinition map = LoadPreferredMap();
        if (map == null)
        {
            Debug.LogError("[MapEnvironmentPreviewRenderer] No battle map is available for showcase rendering.");
            return;
        }

        BattleMapDefinitionUtility.Sanitize(map, map.Name);
        PolyHavenMapEnvironmentInstaller.InstallMaterials();

        string outputFolder = GetOutputFolder();
        Directory.CreateDirectory(outputFolder);

        string previousMapName = PlayerPrefs.GetString("current_map", BattleMapCatalog.DefaultMapName);
        try
        {
            PlayerPrefs.SetString("current_map", map.Name);
            PlayerPrefs.Save();

            RenderPreviewOverview(map, Path.Combine(outputFolder, "map_editor_preview_overview.png"));
            RenderRuntimeShowcase(map, Path.Combine(outputFolder, "runtime_battle_overview.png"));
            RenderRuntimeDetail(map, Path.Combine(outputFolder, "runtime_tree_water_detail.png"));
        }
        finally
        {
            PlayerPrefs.SetString("current_map", previousMapName);
            PlayerPrefs.Save();
        }

        AssetDatabase.Refresh();
        Debug.Log("[MapEnvironmentPreviewRenderer] Showcase images rendered to: " + outputFolder);
    }

    public static void RenderShowcasePngsBatch()
    {
        RenderShowcasePngs();
    }

    static BattleMapDefinition LoadPreferredMap()
    {
        string[] guids = AssetDatabase.FindAssets("t:BattleMapAsset", new[] { RuntimeMapFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            BattleMapAsset asset = AssetDatabase.LoadAssetAtPath<BattleMapAsset>(path);
            if (asset != null)
                return asset.ToDefinition();
        }

        string currentMapName = PlayerPrefs.GetString("current_map", BattleMapCatalog.DefaultMapName);
        return BattleMapCatalog.Get(currentMapName);
    }

    static void RenderPreviewOverview(BattleMapDefinition map, string outputPath)
    {
        SceneBuilder.BuildPreviewScene(map, PreviewScenePath);
        EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);

        Camera camera = EnsureMainCamera();
        ConfigureRenderCamera(camera, map);

        Vector3 lookTarget = Vector3.zero;
        Vector3 playerAnchor = BattleMapDefinitionUtility.GetPlayerBaseAnchor(map);
        Vector3 enemyAnchor = BattleMapDefinitionUtility.GetEnemyBaseAnchor(map);
        if ((playerAnchor - enemyAnchor).sqrMagnitude > 0.01f)
            lookTarget = (playerAnchor + enemyAnchor) * 0.5f;

        PositionCamera(camera, lookTarget + new Vector3(-18f, 0f, 10f), new Vector3(-168f, 220f, -150f), 39f);
        RenderCameraToPng(camera, outputPath, OutputWidth, OutputHeight);
    }

    static void RenderRuntimeShowcase(BattleMapDefinition map, string outputPath)
    {
        SceneBuilder.BuildPreviewScene(map, PreviewScenePath);
        EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);

        RuntimePreviewSpawn();

        Camera camera = EnsureMainCamera();
        ConfigureRenderCamera(camera, map);

        Vector3 playerAnchor = BattleMapDefinitionUtility.GetPlayerBaseAnchor(map);
        Vector3 enemyAnchor = BattleMapDefinitionUtility.GetEnemyBaseAnchor(map);
        Vector3 center = (playerAnchor + enemyAnchor) * 0.5f;
        PositionCamera(camera, center + new Vector3(6f, 0f, 0f), center + new Vector3(-140f, 112f, -138f), 34f);
        RenderCameraToPng(camera, outputPath, OutputWidth, OutputHeight);
    }

    static void RenderRuntimeDetail(BattleMapDefinition map, string outputPath)
    {
        EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
        RuntimePreviewSpawn();

        Camera camera = EnsureMainCamera();
        ConfigureRenderCamera(camera, map);

        Vector3 focus = PickTreeWaterFocus(map);
        PositionCamera(camera, focus + new Vector3(-10f, 5f, 4f), focus + new Vector3(-42f, 32f, -56f), 40f);
        RenderCameraToPng(camera, outputPath, OutputWidth, OutputHeight);
    }

    static void RuntimePreviewSpawn()
    {
        RuntimeBattleMapBuilder.ClearGeneratedObjects();
        DestroySpawnMarkers();

        GameInitializer initializer = Object.FindObjectOfType<GameInitializer>();
        if (initializer == null)
        {
            Debug.LogError("[MapEnvironmentPreviewRenderer] GameInitializer is missing in the opened scene.");
            return;
        }

        initializer.EditorPreviewSpawn();
    }

    static void DestroySpawnMarkers()
    {
        GameObject[] all = Object.FindObjectsOfType<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null || !go.name.StartsWith("SpawnMarker_"))
                continue;

            Object.DestroyImmediate(go);
        }
    }

    static Camera EnsureMainCamera()
    {
        Camera camera = Camera.main;
        if (camera != null)
            return camera;

        camera = Object.FindObjectOfType<Camera>();
        if (camera != null)
        {
            camera.tag = "MainCamera";
            return camera;
        }

        var cameraGo = new GameObject("Main Camera", typeof(Camera));
        camera = cameraGo.GetComponent<Camera>();
        camera.tag = "MainCamera";
        return camera;
    }

    static void ConfigureRenderCamera(Camera camera, BattleMapDefinition map)
    {
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.Lerp(map.SkyColor, Color.white, 0.18f);
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 800f;
        camera.orthographic = false;
        camera.allowHDR = false;
        camera.allowMSAA = true;
    }

    static void PositionCamera(Camera camera, Vector3 lookTarget, Vector3 cameraPosition, float fieldOfView)
    {
        camera.transform.position = cameraPosition;
        camera.transform.LookAt(lookTarget + Vector3.up * 4f);
        camera.fieldOfView = fieldOfView;
    }

    static void RenderCameraToPng(Camera camera, string outputPath, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? GetOutputFolder());

        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);

        try
        {
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
        }
    }

    static Vector3 PickTreeWaterFocus(BattleMapDefinition map)
    {
        if (map != null && map.TreePositions != null && map.TreePositions.Length > 0)
        {
            Vector3 bestTree = map.TreePositions[0];
            float bestScore = float.MaxValue;
            bool foundWater = false;

            if (map.Waters != null)
            {
                for (int i = 0; i < map.TreePositions.Length; i++)
                {
                    Vector3 tree = map.TreePositions[i];
                    for (int j = 0; j < map.Waters.Length; j++)
                    {
                        float score = (tree - map.Waters[j].Center).sqrMagnitude;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestTree = tree;
                            foundWater = true;
                        }
                    }
                }
            }

            if (foundWater)
                return bestTree;

            return map.TreePositions[0];
        }

        if (map != null && map.Waters != null && map.Waters.Length > 0)
            return map.Waters[0].Center;

        return Vector3.zero;
    }

    static string GetOutputFolder()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../PreviewOutput/MapEnvironmentShowcase"));
    }
}
