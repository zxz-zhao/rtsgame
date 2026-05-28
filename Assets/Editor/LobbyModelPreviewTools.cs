using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LobbyModelPreviewTools
{
    const string PreviewPrefabPath = "Assets/Resources/Models/LobbyHero.prefab";
    const string ImportedModelDir = "Assets/Models";
    const string ImportedModelBaseName = "LobbyHeroSource";
    const string BundleName = "lobbyhero";
    const string BundleOutputDir = "Assets/StreamingAssets/LobbyModels";
    const string RuntimeObjPath = "Assets/StreamingAssets/LobbyModels/LobbyHero.obj";
    static readonly string[] NameHints = { "LobbyHero", "Tank", "Hero", "Unit", "Vehicle", "Soldier" };

    [InitializeOnLoadMethod]
    static void RegisterPlayModeCacheCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
            LobbyModelPreview.UnloadCachedAssetBundles(false);
    }

    [MenuItem("RTS/模型/使用选中模型作为大厅预览")]
    public static void UseSelectedModelAsLobbyPreview()
    {
        var source = Selection.activeObject;
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : null;
        var model = LoadModelAsset(sourcePath);

        if (model == null)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "请先在 Project 面板选中一个已导入的 FBX、模型 prefab 或普通 prefab。",
                "确定");
            return;
        }

        SavePreviewPrefab(model, sourcePath);
        RefreshCurrentLobbyScene();
    }

    [MenuItem("RTS/模型/自动查找并生成大厅预览模型")]
    public static void AutoFindLobbyPreviewModel()
    {
        string sourcePath = FindBestModelPath();
        var model = LoadModelAsset(sourcePath);

        if (model == null)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "没有在 Assets 目录里找到可用模型。导入 FBX 或创建 prefab 后，再执行此菜单。",
                "确定");
            return;
        }

        SavePreviewPrefab(model, sourcePath);
        RefreshCurrentLobbyScene();
    }

    [MenuItem("RTS/模型/一键配置大厅模型预览")]
    public static void ConfigureLobbyPreviewModelOneClick()
    {
        string sourcePath = AssetDatabase.GetAssetPath(Selection.activeObject);
        var model = LoadModelAsset(sourcePath);
        if (model == null)
        {
            sourcePath = FindBestModelPath();
            model = LoadModelAsset(sourcePath);
        }

        if (model == null)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "没有找到可用模型。请先导入 FBX 或创建 prefab，也可以在 Project 面板选中模型后重试。",
                "确定");
            return;
        }

        SavePreviewPrefab(model, sourcePath, false);
        BuildLobbyModelAssetBundle(false);
        RefreshCurrentLobbyScene();

        EditorUtility.DisplayDialog("Lobby Model Preview",
            "大厅模型预览已配置完成：\n" + PreviewPrefabPath + "\n" + BundleOutputDir + "/" + BundleName,
            "确定");
    }

    [MenuItem("RTS/模型/从磁盘选择模型导入Prefab管线")]
    public static void ImportExternalModelToPrefabPipeline()
    {
        string sourcePath = EditorUtility.OpenFilePanel("选择模型文件", "", "");
        if (string.IsNullOrEmpty(sourcePath))
            return;

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!IsUnityModelExtension(extension))
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "请选择 Unity 可导入的模型文件，例如 .fbx / .obj / .dae / .blend / .3ds。",
                "确定");
            return;
        }

        string importedAssetPath = ImportedModelDir + "/" + ImportedModelBaseName + extension;
        string importedFullPath = AssetPathToFullPath(importedAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(importedFullPath));
        File.Copy(sourcePath, importedFullPath, true);
        CopyExternalModelSidecars(sourcePath, importedFullPath);

        AssetDatabase.ImportAsset(importedAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        var model = LoadModelAsset(importedAssetPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "模型已复制，但 Unity 没有成功导入为 GameObject：\n" + importedAssetPath,
                "确定");
            return;
        }

        SavePreviewPrefab(model, importedAssetPath, false);
        BuildLobbyModelAssetBundle(false);
        RefreshCurrentLobbyScene();

        EditorUtility.DisplayDialog("Lobby Model Preview",
            "模型已导入并配置为大厅预览：\n" + importedAssetPath,
            "确定");
    }

    [MenuItem("RTS/模型/定位大厅预览Prefab")]
    public static void PingLobbyPreviewPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "尚未生成大厅预览 prefab。先执行“使用选中模型作为大厅预览”或“自动查找并生成大厅预览模型”。",
                "确定");
            return;
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    [MenuItem("RTS/模型/复制选中OBJ到运行时模型目录")]
    public static void CopySelectedObjToRuntimeModelDir()
    {
        string sourcePath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "请先在 Project 面板选中一个 .obj 模型文件。",
                "确定");
            return;
        }

        CopyObjToRuntimeModelDir(AssetPathToFullPath(sourcePath), true);
    }

    [MenuItem("RTS/模型/从磁盘选择OBJ导入运行时目录")]
    public static void ImportExternalObjToRuntimeModelDir()
    {
        string sourcePath = EditorUtility.OpenFilePanel("选择 OBJ 模型", "", "obj");
        if (string.IsNullOrEmpty(sourcePath))
            return;

        CopyObjToRuntimeModelDir(sourcePath, true);
    }

    [MenuItem("RTS/模型/生成示例OBJ模型")]
    public static void GenerateSampleRuntimeObjModel()
    {
        string targetFullPath = AssetPathToFullPath(RuntimeObjPath);
        string targetDir = Path.GetDirectoryName(targetFullPath);
        Directory.CreateDirectory(targetDir);

        File.WriteAllText(targetFullPath, CreateSampleTankObjText());
        File.WriteAllText(Path.ChangeExtension(targetFullPath, ".mtl"),
            "newmtl LobbyHero\nKd 0.24 0.34 0.18\n");

        AssetDatabase.ImportAsset(RuntimeObjPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(Path.ChangeExtension(RuntimeObjPath, ".mtl"), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        RefreshCurrentLobbyScene();

        Debug.Log("[LobbyModelPreviewTools] Sample runtime OBJ generated: " + RuntimeObjPath);
        EditorUtility.DisplayDialog("Lobby Model Preview",
            "示例 OBJ 已生成：\n" + RuntimeObjPath,
            "确定");
    }

    [MenuItem("RTS/模型/打开运行时模型目录")]
    public static void RevealRuntimeModelDir()
    {
        string targetDir = Path.GetDirectoryName(AssetPathToFullPath(RuntimeObjPath));
        Directory.CreateDirectory(targetDir);
        EditorUtility.RevealInFinder(targetDir);
    }

    [MenuItem("RTS/模型/复制选中OBJ到运行时模型目录", true)]
    static bool ValidateCopySelectedObjToRuntimeModelDir()
    {
        string sourcePath = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(sourcePath) && sourcePath.EndsWith(".obj", System.StringComparison.OrdinalIgnoreCase);
    }

    static void CopyObjSidecarFiles(string sourceObjPath)
    {
        string sourceFullPath = AssetPathToFullPath(sourceObjPath);
        string targetFullPath = AssetPathToFullPath(RuntimeObjPath);
        string sourceDir = Path.GetDirectoryName(sourceFullPath);
        string sourceStem = Path.Combine(sourceDir, Path.GetFileNameWithoutExtension(sourceFullPath));
        string targetDir = Path.GetDirectoryName(targetFullPath);
        string targetStem = Path.Combine(targetDir, "LobbyHero");
        string[] extensions = { ".mtl", ".png", ".jpg", ".jpeg" };

        for (int i = 0; i < extensions.Length; i++)
        {
            string source = sourceStem + extensions[i];
            if (!File.Exists(source))
                continue;

            string target = targetStem + extensions[i];
            if (!PathsEqual(source, target))
                File.Copy(source, target, true);
            AssetDatabase.ImportAsset(FullPathToAssetPath(target), ImportAssetOptions.ForceUpdate);
        }

        CopyFirstMtlTextureAsLobbyHeroTexture(sourceStem + ".mtl", targetStem);
    }

    static void CopyObjToRuntimeModelDir(string sourceFullPath, bool showDialog)
    {
        if (string.IsNullOrEmpty(sourceFullPath) || !File.Exists(sourceFullPath))
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Lobby Model Preview",
                    "没有找到 OBJ 文件：\n" + sourceFullPath,
                    "确定");
            }
            return;
        }

        string targetFullPath = AssetPathToFullPath(RuntimeObjPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));
        if (!PathsEqual(sourceFullPath, targetFullPath))
            File.Copy(sourceFullPath, targetFullPath, true);

        CopyObjSidecarFiles(sourceFullPath);
        AssetDatabase.ImportAsset(RuntimeObjPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        RefreshCurrentLobbyScene();

        Debug.Log("[LobbyModelPreviewTools] Runtime OBJ copied: " + sourceFullPath + " -> " + RuntimeObjPath);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Lobby Model Preview",
                "OBJ 已导入运行时模型目录：\n" + RuntimeObjPath,
                "确定");
        }
    }

    static void CopyFirstMtlTextureAsLobbyHeroTexture(string sourceMtlPath, string targetStem)
    {
        if (!File.Exists(sourceMtlPath))
            return;

        string sourceDir = Path.GetDirectoryName(sourceMtlPath);
        var lines = File.ReadAllLines(sourceMtlPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string textureRelativePath = ParseMtlTexturePath(lines[i]);
            if (string.IsNullOrEmpty(textureRelativePath))
                continue;

            string textureFullPath = Path.IsPathRooted(textureRelativePath)
                ? textureRelativePath
                : Path.GetFullPath(Path.Combine(sourceDir, textureRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(textureFullPath))
                continue;

            string extension = Path.GetExtension(textureFullPath);
            string targetTexturePath = targetStem + extension;
            if (!PathsEqual(textureFullPath, targetTexturePath))
                File.Copy(textureFullPath, targetTexturePath, true);
            AssetDatabase.ImportAsset(FullPathToAssetPath(targetTexturePath), ImportAssetOptions.ForceUpdate);
            return;
        }
    }

    static string ParseMtlTexturePath(string line)
    {
        if (string.IsNullOrEmpty(line))
            return null;

        line = line.Trim();
        if (line.Length == 0 || line[0] == '#')
            return null;

        if (!line.StartsWith("map_Kd ", System.StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("map_BaseColor ", System.StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("map_diffuse ", System.StringComparison.OrdinalIgnoreCase))
            return null;

        string[] parts = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        return parts[parts.Length - 1].Trim('"');
    }

    static string AssetPathToFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        if (Path.IsPathRooted(assetPath))
            return assetPath;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    static string FullPathToAssetPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return null;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
        if (normalized.StartsWith(projectRoot + "/", System.StringComparison.OrdinalIgnoreCase))
            return normalized.Substring(projectRoot.Length + 1);

        return normalized;
    }

    static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        string fullA = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullB = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullA, fullB, System.StringComparison.OrdinalIgnoreCase);
    }

    static string CreateSampleTankObjText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Runtime lobby preview sample model");
        sb.AppendLine("mtllib LobbyHero.mtl");
        sb.AppendLine("o LobbyHeroSampleTank");
        sb.AppendLine("usemtl LobbyHero");

        int vertexOffset = 1;
        AppendObjBox(sb, ref vertexOffset, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.5f, 1.25f));
        AppendObjBox(sb, ref vertexOffset, new Vector3(0f, 0.18f, -0.72f), new Vector3(2.65f, 0.28f, 0.28f));
        AppendObjBox(sb, ref vertexOffset, new Vector3(0f, 0.18f, 0.72f), new Vector3(2.65f, 0.28f, 0.28f));
        AppendObjBox(sb, ref vertexOffset, new Vector3(0.05f, 0.86f, 0f), new Vector3(1.05f, 0.38f, 0.78f));
        AppendObjBox(sb, ref vertexOffset, new Vector3(0.9f, 0.88f, 0f), new Vector3(1.25f, 0.12f, 0.12f));

        return sb.ToString();
    }

    static void AppendObjBox(StringBuilder sb, ref int vertexOffset, Vector3 center, Vector3 size)
    {
        Vector3 h = size * 0.5f;
        Vector3[] vertices =
        {
            center + new Vector3(-h.x, -h.y, -h.z),
            center + new Vector3(h.x, -h.y, -h.z),
            center + new Vector3(h.x, -h.y, h.z),
            center + new Vector3(-h.x, -h.y, h.z),
            center + new Vector3(-h.x, h.y, -h.z),
            center + new Vector3(h.x, h.y, -h.z),
            center + new Vector3(h.x, h.y, h.z),
            center + new Vector3(-h.x, h.y, h.z)
        };

        for (int i = 0; i < vertices.Length; i++)
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2}\n", vertices[i].x, vertices[i].y, vertices[i].z);

        int v = vertexOffset;
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 0, v + 1, v + 2, v + 3);
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 4, v + 7, v + 6, v + 5);
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 0, v + 4, v + 5, v + 1);
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 1, v + 5, v + 6, v + 2);
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 2, v + 6, v + 7, v + 3);
        sb.AppendFormat("f {0} {1} {2} {3}\n", v + 3, v + 7, v + 4, v + 0);

        vertexOffset += vertices.Length;
    }

    [MenuItem("RTS/模型/构建大厅模型AssetBundle")]
    public static void BuildLobbyModelAssetBundle()
    {
        BuildLobbyModelAssetBundle(true);
    }

    static void BuildLobbyModelAssetBundle(bool showDialog)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
        if (prefab == null)
        {
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Lobby Model Preview",
                    "尚未生成大厅预览 prefab。先执行“使用选中模型作为大厅预览”或“自动查找并生成大厅预览模型”。",
                    "确定");
            }
            return;
        }

        string bundleOutputFullDir = AssetPathToFullPath(BundleOutputDir);
        Directory.CreateDirectory(bundleOutputFullDir);
        var importer = AssetImporter.GetAtPath(PreviewPrefabPath);
        if (importer == null)
        {
            Debug.LogError("[LobbyModelPreviewTools] Missing importer for " + PreviewPrefabPath);
            return;
        }

        LobbyModelPreview.UnloadCachedAssetBundles(false);
        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();

        BuildPipeline.BuildAssetBundles(
            bundleOutputFullDir,
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);

        AssetDatabase.Refresh();

        string bundlePath = BundleOutputDir + "/" + BundleName;
        if (File.Exists(AssetPathToFullPath(bundlePath)))
        {
            Debug.Log("[LobbyModelPreviewTools] Lobby model AssetBundle built: " + bundlePath);
            RefreshCurrentLobbyScene();
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Lobby Model Preview",
                    "大厅模型 AssetBundle 已构建：\n" + bundlePath,
                    "确定");
            }
        }
        else
        {
            Debug.LogError("[LobbyModelPreviewTools] AssetBundle build finished but file was not found: " + bundlePath);
        }
    }

    [MenuItem("RTS/模型/清理大厅模型加载缓存")]
    public static void ClearLobbyModelLoadCache()
    {
        LobbyModelPreview.UnloadCachedAssetBundles(false);
        Resources.UnloadUnusedAssets();
        Debug.Log("[LobbyModelPreviewTools] Lobby model load cache cleared.");
    }

    [MenuItem("RTS/模型/诊断大厅模型加载路径")]
    public static void DiagnoseLobbyModelLoadPaths()
    {
        var lines = new List<string>();
        AppendAssetStatus(lines, "Resources Prefab", PreviewPrefabPath);
        AppendFileStatus(lines, "AssetBundle", BundleOutputDir + "/" + BundleName);
        AppendFileStatus(lines, "AssetBundle .bundle", BundleOutputDir + "/" + BundleName + ".bundle");
        AppendFileStatus(lines, "AssetBundle .unity3d", BundleOutputDir + "/" + BundleName + ".unity3d");
        AppendFileStatus(lines, "Runtime OBJ", RuntimeObjPath);
        AppendFileStatus(lines, "Runtime OBJ MTL", Path.ChangeExtension(RuntimeObjPath, ".mtl"));
        AppendFileStatus(lines, "Runtime OBJ PNG", Path.ChangeExtension(RuntimeObjPath, ".png"));
        AppendFileStatus(lines, "Runtime OBJ JPG", Path.ChangeExtension(RuntimeObjPath, ".jpg"));
        AppendFileStatus(lines, "Runtime OBJ JPEG", Path.ChangeExtension(RuntimeObjPath, ".jpeg"));

        var previews = Resources.FindObjectsOfTypeAll<LobbyModelPreview>();
        int scenePreviewCount = 0;
        string lastLoadSource = "None";
        string lastLoadPath = "";
        string lastLoadWarning = "";
        for (int i = 0; i < previews.Length; i++)
        {
            if (EditorUtility.IsPersistent(previews[i]))
                continue;

            scenePreviewCount++;
            lastLoadSource = previews[i].LastLoadSource;
            lastLoadPath = previews[i].LastLoadPath;
            lastLoadWarning = previews[i].LastLoadWarning;
        }

        lines.Add("Scene Preview Component: " + (scenePreviewCount > 0 ? "OK (" + scenePreviewCount + ")" : "Missing"));
        lines.Add("Last Load Source: " + lastLoadSource);
        lines.Add("Last Load Path: " + (string.IsNullOrEmpty(lastLoadPath) ? "None" : lastLoadPath));
        lines.Add("Last Load Warning: " + (string.IsNullOrEmpty(lastLoadWarning) ? "None" : lastLoadWarning));

        string report = string.Join("\n", lines.ToArray());
        Debug.Log("[LobbyModelPreviewTools] Model load diagnostics:\n" + report);
        EditorUtility.DisplayDialog("Lobby Model Preview Diagnostics", report, "确定");
    }

    static void AppendAssetStatus(List<string> lines, string label, string assetPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        lines.Add(label + ": " + (asset != null ? "OK - " + assetPath : "Missing - " + assetPath));
    }

    static void AppendFileStatus(List<string> lines, string label, string assetPath)
    {
        string fullPath = AssetPathToFullPath(assetPath);
        lines.Add(label + ": " + (File.Exists(fullPath) ? "OK - " + assetPath : "Missing - " + assetPath));
    }

    [MenuItem("RTS/模型/使用选中模型作为大厅预览", true)]
    static bool ValidateUseSelectedModelAsLobbyPreview()
    {
        return LoadModelAsset(AssetDatabase.GetAssetPath(Selection.activeObject)) != null;
    }

    static bool IsUnityModelExtension(string extension)
    {
        switch (extension)
        {
            case ".fbx":
            case ".obj":
            case ".dae":
            case ".blend":
            case ".3ds":
            case ".dxf":
                return true;
            default:
                return false;
        }
    }

    static void CopyExternalModelSidecars(string sourceModelPath, string importedModelPath)
    {
        string sourceDir = Path.GetDirectoryName(sourceModelPath);
        string sourceStem = Path.Combine(sourceDir, Path.GetFileNameWithoutExtension(sourceModelPath));
        string targetDir = Path.GetDirectoryName(importedModelPath);
        string targetStem = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(importedModelPath));
        string[] sidecarExtensions = { ".mtl", ".png", ".jpg", ".jpeg", ".tga", ".psd" };

        for (int i = 0; i < sidecarExtensions.Length; i++)
        {
            string source = sourceStem + sidecarExtensions[i];
            if (!File.Exists(source))
                continue;

            string target = targetStem + sidecarExtensions[i];
            if (!PathsEqual(source, target))
                File.Copy(source, target, true);
            AssetDatabase.ImportAsset(FullPathToAssetPath(target), ImportAssetOptions.ForceUpdate);
        }

        CopyMtlReferencedTexturesToImportedDir(sourceStem + ".mtl", targetDir);
    }

    static void CopyMtlReferencedTexturesToImportedDir(string sourceMtlPath, string targetDir)
    {
        if (!File.Exists(sourceMtlPath))
            return;

        string sourceDir = Path.GetDirectoryName(sourceMtlPath);
        var lines = File.ReadAllLines(sourceMtlPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string textureRelativePath = ParseMtlTexturePath(lines[i]);
            if (string.IsNullOrEmpty(textureRelativePath))
                continue;

            string sourceTexturePath = Path.IsPathRooted(textureRelativePath)
                ? textureRelativePath
                : Path.GetFullPath(Path.Combine(sourceDir, textureRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(sourceTexturePath))
                continue;

            string targetTexturePath = Path.Combine(targetDir, textureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetTexturePath));
            if (!PathsEqual(sourceTexturePath, targetTexturePath))
                File.Copy(sourceTexturePath, targetTexturePath, true);
            AssetDatabase.ImportAsset(FullPathToAssetPath(targetTexturePath), ImportAssetOptions.ForceUpdate);
        }
    }

    static GameObject LoadModelAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        if (!assetPath.StartsWith("Assets/"))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }

    static string FindBestModelPath()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets" });
        string bestPath = null;
        int bestScore = int.MinValue;

        EvaluateModelGuids(prefabGuids, ref bestPath, ref bestScore);
        EvaluateModelGuids(modelGuids, ref bestPath, ref bestScore);

        return bestPath;
    }

    static void EvaluateModelGuids(string[] guids, ref string bestPath, ref int bestScore)
    {
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || path.StartsWith("Assets/Editor/"))
                continue;
            if (path == PreviewPrefabPath)
                continue;

            var model = LoadModelAsset(path);
            if (model == null)
                continue;

            int score = ScoreModelPath(path);
            if (score > bestScore)
            {
                bestScore = score;
                bestPath = path;
            }
        }
    }

    static int ScoreModelPath(string path)
    {
        string lowerPath = path.ToLowerInvariant();
        int score = lowerPath.Contains("/resources/") ? 12 : 0;

        for (int i = 0; i < NameHints.Length; i++)
        {
            string hint = NameHints[i].ToLowerInvariant();
            if (lowerPath.Contains(hint))
                score += 80 - i * 7;
        }

        if (lowerPath.EndsWith(".prefab"))
            score += 10;
        if (lowerPath.Contains("/models/"))
            score += 20;
        if (lowerPath.Contains("/lobby/"))
            score += 18;
        return score;
    }

    static void SavePreviewPrefab(GameObject source, string sourcePath)
    {
        SavePreviewPrefab(source, sourcePath, true);
    }

    static void SavePreviewPrefab(GameObject source, string sourcePath, bool showDialog)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPathToFullPath(PreviewPrefabPath)));

        var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            instance = UnityEngine.Object.Instantiate(source);

        instance.name = "LobbyHero";
        PreparePreviewPrefab(instance);

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, PreviewPrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);

        AssetDatabase.ImportAsset(PreviewPrefabPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(PreviewPrefabPath);
        if (importer != null)
        {
            importer.assetBundleName = BundleName;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (savedPrefab != null)
        {
            Debug.Log("[LobbyModelPreviewTools] Lobby preview prefab generated from " + sourcePath + " -> " + PreviewPrefabPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Lobby Model Preview",
                    "大厅预览模型已生成：\n" + PreviewPrefabPath,
                    "确定");
            }
        }
        else
        {
            Debug.LogError("[LobbyModelPreviewTools] Failed to generate " + PreviewPrefabPath);
        }
    }

    static void PreparePreviewPrefab(GameObject root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            UnityEngine.Object.DestroyImmediate(colliders[i]);

        var rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            UnityEngine.Object.DestroyImmediate(rigidbodies[i]);

        var audioSources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
            UnityEngine.Object.DestroyImmediate(audioSources[i]);

        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                UnityEngine.Object.DestroyImmediate(behaviours[i]);
        }

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
    }

    static void RefreshCurrentLobbyScene()
    {
        if (!File.Exists(AssetPathToFullPath("Assets/Scenes/LobbyScene.unity")))
            return;

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.path == "Assets/Scenes/LobbyScene.unity")
            LobbySceneBuilder.AddOrUpdateLobbyModelPreview();
    }
}
