using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 大厅 UI 皮肤替换工具。
/// 用法：菜单 RTS → 资源接入 → 大厅 UI 皮肤替换。
///   1) 下载 Kenney UI Pack RPG / Fantasy Wooden GUI / 任意 9-slice 包；
///   2) 把对应 Sprite 拖到下面槽位，并设置 9-slice 边距；
///   3) 点 "应用" → 自动覆盖 Resources/LobbyGen 中关键 PNG，
///      下次重新生成大厅或重启时即生效。
/// </summary>
public class LobbyUiSkinIntegrator : EditorWindow
{
    Sprite buttonGold, buttonDark, panelFrame, cardFrame, progressTrack, progressFill;
    Sprite avatarPlayer, avatarFriendA, avatarFriendB;
    Vector4 buttonBorder = new Vector4(20, 20, 20, 20);
    Vector4 panelBorder  = new Vector4(22, 22, 22, 22);
    Vector4 cardBorder   = new Vector4(26, 26, 26, 26);
    Vector4 progressBorder = new Vector4(10, 5, 10, 5);

    const string LobbyGen = "Assets/Resources/LobbyGen";

    [MenuItem("RTS/资源接入/大厅 UI 皮肤替换")]
    static void Open() => GetWindow<LobbyUiSkinIntegrator>("UI 皮肤");

    void OnGUI()
    {
        EditorGUILayout.LabelField("大厅 UI 皮肤替换", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "为大厅几个核心 9-slice 元素指定新 Sprite。\n"
            + "应用会覆盖 Resources/LobbyGen 中：\n"
            + " · gen_button_gold.png / gen_button_dark.png\n"
            + " · gen_panel_frame.png / gen_card_frame.png\n"
            + " · gen_progress_track.png / gen_progress_fill.png\n"
            + " · gen_avatar_player/friend_a/friend_b.png\n"
            + "建议资源：Kenney UI Pack RPG (CC0) 或 Fantasy Wooden GUI Free。",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("9-Slice 元素", EditorStyles.miniBoldLabel);
        buttonGold     = (Sprite)EditorGUILayout.ObjectField("金色按钮底", buttonGold, typeof(Sprite), false);
        buttonBorder   = EditorGUILayout.Vector4Field("按钮 border (L T R B)", buttonBorder);
        buttonDark     = (Sprite)EditorGUILayout.ObjectField("深色按钮底", buttonDark, typeof(Sprite), false);
        panelFrame     = (Sprite)EditorGUILayout.ObjectField("面板边框", panelFrame, typeof(Sprite), false);
        panelBorder    = EditorGUILayout.Vector4Field("面板 border", panelBorder);
        cardFrame      = (Sprite)EditorGUILayout.ObjectField("卡片边框", cardFrame, typeof(Sprite), false);
        cardBorder     = EditorGUILayout.Vector4Field("卡片 border", cardBorder);
        progressTrack  = (Sprite)EditorGUILayout.ObjectField("进度条 底", progressTrack, typeof(Sprite), false);
        progressFill   = (Sprite)EditorGUILayout.ObjectField("进度条 填充", progressFill, typeof(Sprite), false);
        progressBorder = EditorGUILayout.Vector4Field("进度条 border", progressBorder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("头像", EditorStyles.miniBoldLabel);
        avatarPlayer  = (Sprite)EditorGUILayout.ObjectField("玩家头像",   avatarPlayer,  typeof(Sprite), false);
        avatarFriendA = (Sprite)EditorGUILayout.ObjectField("好友头像 A", avatarFriendA, typeof(Sprite), false);
        avatarFriendB = (Sprite)EditorGUILayout.ObjectField("好友头像 B", avatarFriendB, typeof(Sprite), false);

        EditorGUILayout.Space(8);
        if (GUILayout.Button("应用并刷新", GUILayout.Height(36)))
            ApplyAll();

        EditorGUILayout.Space(2);
        if (GUILayout.Button("打开 LobbyGen 文件夹"))
        {
            EnsureFolder();
            EditorUtility.RevealInFinder(LobbyGen);
        }
    }

    void ApplyAll()
    {
        EnsureFolder();
        int n = 0;
        n += CopyAndConfigure(buttonGold,    "gen_button_gold.png",     buttonBorder,   true);
        n += CopyAndConfigure(buttonDark,    "gen_button_dark.png",     buttonBorder,   true);
        n += CopyAndConfigure(panelFrame,    "gen_panel_frame.png",     panelBorder,    true);
        n += CopyAndConfigure(cardFrame,     "gen_card_frame.png",      cardBorder,     true);
        n += CopyAndConfigure(progressTrack, "gen_progress_track.png",  progressBorder, true);
        n += CopyAndConfigure(progressFill,  "gen_progress_fill.png",   progressBorder, true);
        n += CopyAndConfigure(avatarPlayer,  "gen_avatar_player.png",   Vector4.zero,   false);
        n += CopyAndConfigure(avatarFriendA, "gen_avatar_friend_a.png", Vector4.zero,   false);
        n += CopyAndConfigure(avatarFriendB, "gen_avatar_friend_b.png", Vector4.zero,   false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("UI 皮肤", $"已写入 {n} 个 sprite。请关闭再重新打开 LobbyScene 查看。", "确定");
    }

    static void EnsureFolder()
    {
        if (!Directory.Exists(LobbyGen))
            Directory.CreateDirectory(LobbyGen);
        AssetDatabase.Refresh();
    }

    static int CopyAndConfigure(Sprite src, string dstFileName, Vector4 border, bool sliced)
    {
        if (src == null) return 0;
        string srcPath = AssetDatabase.GetAssetPath(src.texture);
        if (string.IsNullOrEmpty(srcPath)) return 0;
        string dstPath = $"{LobbyGen}/{dstFileName}";

        if (Path.GetFullPath(srcPath) == Path.GetFullPath(dstPath))
        {
            ConfigureImporter(dstPath, border, sliced);
            return 1;
        }

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(dstPath) != null)
            AssetDatabase.DeleteAsset(dstPath);

        if (!AssetDatabase.CopyAsset(srcPath, dstPath))
        {
            Debug.LogError($"[LobbyUiSkinIntegrator] 复制失败: {srcPath} → {dstPath}");
            return 0;
        }
        ConfigureImporter(dstPath, border, sliced);
        return 1;
    }

    static void ConfigureImporter(string assetPath, Vector4 border, bool sliced)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        if (sliced) ti.spriteBorder = border;
        ti.SaveAndReimport();
    }
}
