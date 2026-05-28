using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 第三方特效资源接入工具。打开方式：菜单栏 RTS → 资源接入 → 特效绑定。
/// 用法：
///   1) 下载 Cartoon FX Free / Unity Particle Pack 等，导入到项目任意位置；
///   2) 打开本窗口，把目标 prefab 拖到对应槽位（死亡/命中/枪口/建造完成）；
///   3) 点 "应用并保存" → 自动复制到 Assets/Resources/Effects/ 并重命名为
///      EffectsManager 期望的名字，立即生效。
/// </summary>
public class EffectsAssetIntegrator : EditorWindow
{
    const string ResFolder = "Assets/Resources/Effects";

    GameObject deathSmall, deathMedium, deathLarge;
    GameObject impactBullet, impactShell, impactRocket, impactFire;
    GameObject muzzleFlash;
    GameObject buildComplete, levelUp;

    [MenuItem("RTS/资源接入/特效绑定")]
    static void Open()
    {
        GetWindow<EffectsAssetIntegrator>("特效绑定");
    }

    void OnEnable()
    {
        // 自动回填已有映射
        deathSmall    = LoadExisting("death_small");
        deathMedium   = LoadExisting("death_medium");
        deathLarge    = LoadExisting("death_large");
        impactBullet  = LoadExisting("impact_bullet");
        impactShell   = LoadExisting("impact_shell");
        impactRocket  = LoadExisting("impact_rocket");
        impactFire    = LoadExisting("impact_fire");
        muzzleFlash   = LoadExisting("muzzle_flash");
        buildComplete = LoadExisting("build_complete");
        levelUp       = LoadExisting("level_up");
    }

    static GameObject LoadExisting(string name)
    {
        string path = $"{ResFolder}/{name}.prefab";
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("第三方特效绑定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "把下载好的特效 prefab 拖到下面对应槽位。点击底部按钮后会自动复制到\n"
            + "Assets/Resources/Effects/ 并重命名为标准名。资源缺失时游戏会回落到内置效果。\n\n"
            + "推荐资源（免费）：\n"
            + " · Cartoon FX Free（Asset Store，搜 Jean Moreno）\n"
            + " · Unity Particle Pack（Asset Store，官方）\n"
            + " · Hovl Studio Magic Effects Free", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("死亡 / 爆炸（按单位人口分级）", EditorStyles.miniBoldLabel);
        deathSmall  = (GameObject)EditorGUILayout.ObjectField("步兵死亡 (1 人口)", deathSmall, typeof(GameObject), false);
        deathMedium = (GameObject)EditorGUILayout.ObjectField("坦克死亡 (2-3 人口)", deathMedium, typeof(GameObject), false);
        deathLarge  = (GameObject)EditorGUILayout.ObjectField("重型/飞机/建筑爆炸", deathLarge, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("命中冲击", EditorStyles.miniBoldLabel);
        impactBullet = (GameObject)EditorGUILayout.ObjectField("子弹命中", impactBullet, typeof(GameObject), false);
        impactShell  = (GameObject)EditorGUILayout.ObjectField("炮弹命中", impactShell, typeof(GameObject), false);
        impactRocket = (GameObject)EditorGUILayout.ObjectField("火箭命中", impactRocket, typeof(GameObject), false);
        impactFire   = (GameObject)EditorGUILayout.ObjectField("火焰命中", impactFire, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("其它", EditorStyles.miniBoldLabel);
        muzzleFlash   = (GameObject)EditorGUILayout.ObjectField("枪口火焰", muzzleFlash, typeof(GameObject), false);
        buildComplete = (GameObject)EditorGUILayout.ObjectField("建造完成", buildComplete, typeof(GameObject), false);
        levelUp       = (GameObject)EditorGUILayout.ObjectField("升级", levelUp, typeof(GameObject), false);

        EditorGUILayout.Space(12);
        if (GUILayout.Button("应用并保存", GUILayout.Height(34)))
        {
            ApplyAll();
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("打开 Resources/Effects 文件夹"))
        {
            EnsureFolder();
            EditorUtility.RevealInFinder(ResFolder);
        }
    }

    void ApplyAll()
    {
        EnsureFolder();
        int n = 0;
        n += CopyAs(deathSmall,    "death_small");
        n += CopyAs(deathMedium,   "death_medium");
        n += CopyAs(deathLarge,    "death_large");
        n += CopyAs(impactBullet,  "impact_bullet");
        n += CopyAs(impactShell,   "impact_shell");
        n += CopyAs(impactRocket,  "impact_rocket");
        n += CopyAs(impactFire,    "impact_fire");
        n += CopyAs(muzzleFlash,   "muzzle_flash");
        n += CopyAs(buildComplete, "build_complete");
        n += CopyAs(levelUp,       "level_up");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("特效绑定", $"已写入 {n} 个特效到 {ResFolder}", "确定");
    }

    static void EnsureFolder()
    {
        if (!Directory.Exists(ResFolder))
            Directory.CreateDirectory(ResFolder);
        AssetDatabase.Refresh();
    }

    /// <summary>把源 prefab 复制为 Resources/Effects/{newName}.prefab。若同名已存在则覆盖。</summary>
    static int CopyAs(GameObject src, string newName)
    {
        if (src == null) return 0;
        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath)) return 0;
        string dstPath = $"{ResFolder}/{newName}.prefab";

        // 如果目标已存在且就是源 prefab（用户从 Resources 直接拖回来），跳过
        if (Path.GetFullPath(srcPath) == Path.GetFullPath(dstPath))
            return 1;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(dstPath) != null)
            AssetDatabase.DeleteAsset(dstPath);

        bool ok = AssetDatabase.CopyAsset(srcPath, dstPath);
        if (!ok)
        {
            Debug.LogError($"[EffectsAssetIntegrator] 复制失败：{srcPath} → {dstPath}");
            return 0;
        }
        return 1;
    }
}
