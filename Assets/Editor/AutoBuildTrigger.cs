using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoBuildTrigger
{
    private const string FullBuildTriggerName = "BUILD_NOW";
    private const string PrefabRebuildTriggerName = "REBUILD_PREFABS_NOW";

    private static readonly string WatchDir = Application.dataPath;
    private static FileSystemWatcher watcher;
    private static volatile bool pendingFullBuild;
    private static volatile bool pendingPrefabRebuild;

    static AutoBuildTrigger()
    {
        StartWatcher();
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.quitting += StopWatcher;
    }

    private static void StartWatcher()
    {
        StopWatcher();

        watcher = new FileSystemWatcher(WatchDir)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };
        watcher.Created += OnTrigger;
        watcher.Changed += OnTrigger;

        // Unity 脚本重载时，触发文件可能已经存在；这里主动补扫一次，避免漏掉构建请求。
        pendingFullBuild |= File.Exists(Path.Combine(WatchDir, FullBuildTriggerName));
        pendingPrefabRebuild |= File.Exists(Path.Combine(WatchDir, PrefabRebuildTriggerName));

        Debug.Log("[AutoBuildTrigger] Watching Assets/BUILD_NOW and Assets/REBUILD_PREFABS_NOW.");
    }

    private static void StopWatcher()
    {
        if (watcher == null) return;

        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
        watcher = null;
    }

    private static void OnTrigger(object sender, FileSystemEventArgs e)
    {
        string name = Path.GetFileName(e.FullPath);
        if (string.Equals(name, FullBuildTriggerName, StringComparison.OrdinalIgnoreCase))
        {
            pendingFullBuild = true;
            return;
        }

        if (string.Equals(name, PrefabRebuildTriggerName, StringComparison.OrdinalIgnoreCase))
        {
            pendingPrefabRebuild = true;
        }
    }

    private static void OnEditorUpdate()
    {
        if (!pendingFullBuild && !pendingPrefabRebuild) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying)
        {
            // 构建不能在 Play Mode 中执行；收到触发后自动退出播放，下一帧再继续构建。
            Debug.Log("[AutoBuildTrigger] Build trigger is pending, exiting Play Mode first.");
            EditorApplication.ExitPlaymode();
            return;
        }

        bool runFullBuild = pendingFullBuild;
        bool runPrefabRebuild = pendingPrefabRebuild;
        pendingFullBuild = false;
        pendingPrefabRebuild = false;

        DeleteTriggerFile(FullBuildTriggerName);
        DeleteTriggerFile(PrefabRebuildTriggerName);

        if (runFullBuild)
        {
            RunFullBuild();
            return;
        }

        if (runPrefabRebuild)
        {
            RunPrefabRebuild();
        }
    }

    private static void DeleteTriggerFile(string triggerName)
    {
        string path = Path.Combine(WatchDir, triggerName);
        if (!File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AutoBuildTrigger] Could not delete trigger file " + triggerName + ": " + ex.Message);
        }
    }

    private static void RunFullBuild()
    {
        try
        {
            Debug.Log("[AutoBuildTrigger] BUILD_NOW detected - starting full build.");
            BuildAll.RunAll();
        }
        catch (Exception ex)
        {
            Debug.LogError("[AutoBuildTrigger] Full build failed: " + ex);
        }
    }

    private static void RunPrefabRebuild()
    {
        try
        {
            Debug.Log("[AutoBuildTrigger] REBUILD_PREFABS_NOW detected - rebuilding downloaded-model prefabs.");
            PrefabBuilder.BuildAllPrefabs();
            Debug.Log("[AutoBuildTrigger] Downloaded-model prefab rebuild complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[AutoBuildTrigger] Prefab rebuild failed: " + ex);
        }
    }
}
