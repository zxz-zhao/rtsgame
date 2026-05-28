using UnityEngine;
using UnityEditor;
using System.IO;

// 将 Assets/Prefabs/ 里的 Prefab 同步复制到 Assets/Resources/Prefabs/（供运行时 Resources.Load 使用）
[InitializeOnLoad]
public class ResourcesSync
{
    static ResourcesSync()
    {
        EditorApplication.delayCall += Sync;
    }

    [MenuItem("RTS/同步Prefabs到Resources")]
    public static void Sync()
    {
        string src = "Assets/Prefabs";
        string dst = "Assets/Resources/Prefabs";

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(dst))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

        if (!Directory.Exists(src)) return;

        int count = 0;
        foreach (var file in Directory.GetFiles(src, "*.prefab"))
        {
            string name = Path.GetFileName(file);
            string srcPath = src + "/" + name;
            string dstPath = dst + "/" + name;
            // 只在源比目标新时才复制
            if (!File.Exists(dstPath) ||
                File.GetLastWriteTime(file) > File.GetLastWriteTime(Application.dataPath + "/../" + dstPath))
            {
                AssetDatabase.CopyAsset(srcPath, dstPath);
                count++;
            }
        }
        if (count > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"✅ ResourcesSync: 同步了 {count} 个Prefab 到 {dst}");
        }
    }
}
