using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public class LobbySceneBuilder
{
    enum ExactPieceMask
    {
        None,
        Mode,
        Panel
    }

    static readonly Color BG_DARK   = new Color(0.06f, 0.08f, 0.15f);
    static readonly Color BG_PANEL  = new Color(0.1f,  0.13f, 0.22f, 0.97f);
    static readonly Color BG_CARD   = new Color(0.12f, 0.16f, 0.26f);
    static readonly Color C_BLUE    = new Color(0.15f, 0.55f, 0.95f);
    static readonly Color C_GREEN   = new Color(0.12f, 0.72f, 0.35f);
    static readonly Color C_GOLD    = new Color(0.82f, 0.62f, 0.1f);
    static readonly Color C_RED     = new Color(0.65f, 0.15f, 0.15f);
    static readonly Color C_PURPLE  = new Color(0.45f, 0.18f, 0.75f);
    static readonly Color C_TEAL    = new Color(0.1f,  0.5f,  0.65f);
    static readonly Color C_GRAY    = new Color(0.22f, 0.24f, 0.32f);
    static readonly Color C_ORANGE  = new Color(0.85f, 0.45f, 0.1f);
    /// <summary>模式卡共用描边/顶线色，保持样本里的统一金色战场调。</summary>
    static readonly Color C_MODE_ACCENT = new Color(0.78f, 0.62f, 0.22f);
    /// <summary>明显可见的布局定位框颜色，避免被大厅底图和装饰边吃掉。</summary>
    static readonly Color C_LAYOUT_GUIDE = new Color(1f, 0.34f, 0.16f, 0.96f);

    static Sprite _sprBgDesk, _sprRefMaster, _sprThumbMatch, _sprThumbCustom, _sprThumbGlobal, _sprCardFade, _sprMetalPanel, _sprDot, _sprPortraitRing;
    static Sprite _sprExactFriendPanel, _sprExactModeMatch, _sprExactModeCustom, _sprExactModeGlobal, _sprExactTaskPanel, _sprExactTechPanel, _sprExactClaimButton, _sprExactMatchButtonBorder;
    static Sprite _sprExactNavShop, _sprExactNavWarehouse, _sprExactNavCampaign, _sprExactNavRank, _sprExactNavMail;
    static Sprite _sprPanelFrame, _sprCardFrame, _sprModeCardFrame, _sprFriendRow, _sprMissionRow, _sprTaskCard, _sprTechCard, _sprProgressTrack, _sprProgressFill;
    static Sprite _sprButtonGold, _sprButtonDark, _sprButtonTechMetal, _sprNavTab, _sprNavTabActive;
    /// <summary>本次生成是否使用了参考图合成自定义模式条图。</summary>
    static bool _customThumbFromRefs;
    static Sprite _sprAvatarPlayer, _sprAvatarFriendA, _sprAvatarFriendB;
    static Sprite _sprUiKnob;
    static Sprite _sprIconGear, _sprIconBell, _sprIconStar, _sprIconGem, _sprIconHelmet, _sprIconClipboard;
    static Sprite _sprIconMatch, _sprIconCustom, _sprIconWar, _sprIconTank, _sprIconTaskCrate, _sprIconTechBlueprint;
    static Sprite _sprNavShop, _sprNavWarehouse, _sprNavWarehouseButton, _sprNavCampaign, _sprNavRank, _sprNavMail;

    static Sprite BuiltinUiSprite()
    {
        if (_sprUiKnob == null)
            _sprUiKnob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        return _sprUiKnob;
    }

    static void AssignUiSprite(Image img)
    {
        if (img == null) return;
        if (img.sprite == null)
        {
            var s = BuiltinUiSprite();
            if (s != null) img.sprite = s;
        }
    }

    static void ApplySprite(Image img, Sprite spr, Color tint, bool sliced = false)
    {
        if (img == null) return;
        if (spr != null)
        {
            img.sprite = spr;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            AssignUiSprite(img);
        }
        img.color = tint;
    }

    [MenuItem("RTS/生成场景/① 从桌面复制参考背景到 LobbyGen")]
    public static void PullLobbyReferenceBackgroundFromDesktop()
    {
        bool copied = TryPullReferenceDeskFromDesktop(overwrite: true);
        string destDisk = Path.Combine(Application.dataPath, "Resources", "LobbyGen", "_ref_lobby_master.png");
        AssetDatabase.Refresh();
        if (copied)
            EditorUtility.DisplayDialog("LobbyGen", "已将大厅参考背景复制到 Assets/Resources/LobbyGen。接下来请执行“生成大厅场景”。", "确定");
        else if (File.Exists(destDisk) || File.Exists(Path.Combine(Application.dataPath, "Resources", "LobbyGen", "lobby_desk_only.png")) || File.Exists(Path.Combine(Application.dataPath, "Resources", "LobbyGen", "ref_lobby_desk_no_bottom_map.png")))
            EditorUtility.DisplayDialog("LobbyGen", "桌面上没有新的参考图，但现有 LobbyGen 参考资源仍可直接使用。", "确定");
        else
            EditorUtility.DisplayDialog("LobbyGen", "桌面未找到：\nref_lobby_desk_no_bottom_map\n_ref_lobby_master / lobby_ref / lobby_background / 大厅参考\n或 lobby_desk_only / 大厅桌子背景", "确定");
    }

    [MenuItem("RTS/生成场景/② 从桌面复制自定义条背景/图徽到 LobbyGen")]
    public static void PullCustomModeThumbRefsMenu()
    {
        bool copied = TryPullCustomModeThumbRefsFromDesktop(overwrite: true);
        AssetDatabase.Refresh();
        string gen = Path.Combine(Application.dataPath, "Resources", "LobbyGen");
        bool hasBg = File.Exists(Path.Combine(gen, "ref_thumb_custom_bg.png"));
        bool hasEm = File.Exists(Path.Combine(gen, "ref_thumb_custom_emblem.png"));
        if (copied || hasBg || hasEm)
            EditorUtility.DisplayDialog("LobbyGen",
                "已尝试从桌面复制到 Resources/LobbyGen：\n"
                + "· ref_thumb_custom_bg.png（条背景，宽横幅）\n"
                + "· ref_thumb_custom_emblem.png（左侧图徽，可选）\n\n"
                + "桌面文件名可为：mode_custom_bg.png / 自定义模式条背景.png；\n"
                + "mode_custom_emblem.png / 自定义模式图徽.png\n\n"
                + "然后执行“生成大厅场景”，即可合成 gen_thumb_custom.png。",
                "确定");
        else
            EditorUtility.DisplayDialog("LobbyGen",
                "桌面未找到上述文件。也可手动把 PNG 放进：\nAssets/Resources/LobbyGen/\n同名 ref_thumb_custom_bg / ref_thumb_custom_emblem",
                "确定");
    }

    [MenuItem("RTS/生成场景/③ 生成大厅场景")]
    public static void BuildLobbyScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        }

        CreateEventSystem();
        GameObject canvasGO = CreateCanvas("LobbyCanvas");

        // 底层背景：使用桌面/场景参考图（不用同一张UI图否则透明面板看到的还是白）
        Sprite bgSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/LobbyGen/ref_lobby_desk_no_bottom_map.png")
            ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/LobbyGen/user_lobby_background.png")
            ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/LobbyGen/_ref_lobby_master.png")
            ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/icons/background.png");

        // 全屏 Background 图（铺满 Canvas）
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.color = Color.white;
        bgImg.preserveAspect = false;
        bgImg.raycastTarget = false;

        // 在 background 之上叠加 back.png，保留原图白色区域，透明像素可透出底层背景。
        var overlaySprite =
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/icons/back.png")
            ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/icons/groundback.png");
        if (overlaySprite != null)
        {
            var overlayGO = new GameObject("BackgroundOverlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.sprite = overlaySprite;
            overlayImg.color = Color.white;
            overlayImg.preserveAspect = false;
            overlayImg.raycastTarget = false;
        }

        // HallPanel 占位，铺满 Canvas（保持空白，由后续手工或提取的图标贴图填充）
        var hall = new GameObject("HallPanel");
        hall.transform.SetParent(canvasGO.transform, false);
        var hallRT = hall.AddComponent<RectTransform>();
        hallRT.anchorMin = Vector2.zero;
        hallRT.anchorMax = Vector2.one;
        hallRT.offsetMin = Vector2.zero;
        hallRT.offsetMax = Vector2.zero;
        CreateLobbyModelPreview(hall.transform);

        // ── 顶栏点击区 ───────────────────────────────────
        var settingsBtn = CreateLobbyHitButton(hall.transform, "SettingsIconBtn", new Vector2(0.043f, 0.896f), new Vector2(72f, 72f));
        AddSettingsGearIcon(settingsBtn.transform, new Vector2(58f, 58f));
        var bellBtn = CreateLobbyHitButton(hall.transform, "NotifyBellBtn", new Vector2(0.111f, 0.896f), new Vector2(72f, 72f));
        var goldPlusBtn = CreateLobbyHitButton(hall.transform, "GoldPlusBtn", new Vector2(0.265f, 0.896f), new Vector2(246f, 56f));
        var gemPlusBtn = CreateLobbyHitButton(hall.transform, "GemPlusBtn", new Vector2(0.847f, 0.896f), new Vector2(192f, 56f));

        // 金币 / 钻石 数字（作为按钮的子节点，相对按钮居中偏左 — 避开右侧"+"按钮区域）
        var goldText = CreateTopBarCountTextChild(goldPlusBtn.transform, "GoldCountText", "10,000",
            new Vector2(-30f, 34f), new Color(1f, 0.92f, 0.56f, 1f));
        var gemText = CreateTopBarCountTextChild(gemPlusBtn.transform, "GemCountText", "888",
            new Vector2(-30f, 34f), new Color(0.74f, 0.96f, 1f, 1f));

        // ── 中央模式卡片点击区 ──────────────────────────
        var quickMatchBtn = CreateLobbyHitButton(hall.transform, "QuickMatchButton", new Vector2(0.493f, 0.630f), new Vector2(432f, 112f));
        var customRoomBtn = CreateLobbyHitButton(hall.transform, "CustomRoomButton", new Vector2(0.493f, 0.440f), new Vector2(432f, 116f));
        var globalConquestBtn = CreateLobbyHitButton(hall.transform, "GlobalConquestButton", new Vector2(0.493f, 0.224f), new Vector2(432f, 122f));

        // ── 底部导航点击区 ──────────────────────────────
        var navShopBtn = CreateLobbyHitButton(hall.transform, "NavShopBtn", new Vector2(0.098f, 0.052f), new Vector2(200f, 68f));
        var navWarehouseBtn = CreateLobbyHitButton(hall.transform, "NavWarehouseBtn", new Vector2(0.303f, 0.052f), new Vector2(205f, 68f));
        var navCampaignBtn = CreateLobbyHitButton(hall.transform, "NavCampaignBtn", new Vector2(0.498f, 0.052f), new Vector2(195f, 80f));
        var navRankBtn = CreateLobbyHitButton(hall.transform, "NavRankBtn", new Vector2(0.698f, 0.052f), new Vector2(205f, 68f));
        var navMailBtn = CreateLobbyHitButton(hall.transform, "NavMailBtn", new Vector2(0.913f, 0.052f), new Vector2(200f, 68f));

        // ── 左侧好友列表 ────────────────────────────────
        var friendRows = new GameObject[5];
        const float friendXNorm = 0.125f;
        float[] friendYNorm = { 0.681f, 0.575f, 0.469f, 0.363f, 0.257f };
        for (int i = 0; i < 5; i++)
            friendRows[i] = CreateFriendRow(hall.transform, i, new Vector2(friendXNorm, friendYNorm[i]), new Vector2(216f, 64f));
        var addFriendIconBtn = CreateLobbyHitButton(hall.transform, "AddFriendIconBtn", new Vector2(0.215f, 0.773f), new Vector2(36f, 36f));
        var viewAllFriendsBtn = CreateLobbyHitButton(hall.transform, "ViewAllFriendsBtn", new Vector2(0.215f, 0.152f), new Vector2(36f, 36f));

        var rightPanels = LobbyRightPanelLayoutTools.RebuildRightPanels(hall.transform);

        // ── 匹配状态/反馈文字 ───────────────────────────
        var matchStatusText = CreateLabel(hall.transform, "MatchStatusText", "", new Vector2(0.5f, 0.110f), new Vector2(720f, 28f), 16, new Color(1f, 0.86f, 0.42f, 1f), TextAnchor.MiddleCenter);

        GameObject mgrGO = new GameObject("LobbyManager");
        var lm = mgrGO.AddComponent<LobbyManager>();
        lm.HallPanel = hall;
        lm.NotifyBellBtn = bellBtn;
        lm.SettingsIconBtn = settingsBtn;
        lm.GoldText = goldText;
        lm.GemText = gemText;
        lm.GoldPlusBtn = goldPlusBtn;
        lm.GemPlusBtn = gemPlusBtn;
        lm.TechButton = rightPanels.TechButton;
        lm.QuickMatchButton = quickMatchBtn;
        lm.CustomRoomButton = customRoomBtn;
        lm.GlobalConquestButton = globalConquestBtn;
        lm.NavShopBtn = navShopBtn;
        lm.NavWarehouseBtn = navWarehouseBtn;
        lm.NavCampaignBtn = navCampaignBtn;
        lm.NavRankBtn = navRankBtn;
        lm.NavMailBtn = navMailBtn;
        lm.AddFriendIconBtn = addFriendIconBtn;
        lm.ViewAllFriendsBtn = viewAllFriendsBtn;
        lm.TaskTitleTexts = rightPanels.TaskTitles;
        lm.TaskProgTexts = rightPanels.TaskProgress;
        lm.TaskProgSliders = rightPanels.TaskSliders;
        lm.TaskClaimButtons = rightPanels.TaskClaims;
        lm.MoreTasksBtn = rightPanels.MoreTasks;
        lm.TechResearchNameText = rightPanels.TechName;
        lm.TechResearchDescText = rightPanels.TechDesc;
        lm.TechTimerBarText = rightPanels.TechTimer;
        lm.TechProgressSlider = rightPanels.TechSlider;
        lm.TechSpeedBtn = rightPanels.TechSpeed;
        lm.TechStartBtn = rightPanels.TechStart;
        lm.TechTreeBtn = rightPanels.TechTree;
        lm.MatchStatusText = matchStatusText;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/LobbyScene.unity");
        AssetDatabase.Refresh();
        Debug.Log("[LobbySceneBuilder] LobbyScene generated with Background + HallPanel. Background sprite: "
            + (bgSprite != null ? bgSprite.name : "NULL"));
    }

    [MenuItem("RTS/生成场景/④ 添加或更新大厅模型预览")]
    public static void AddOrUpdateLobbyModelPreview()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != "Assets/Scenes/LobbyScene.unity")
            scene = EditorSceneManager.OpenScene("Assets/Scenes/LobbyScene.unity", OpenSceneMode.Single);

        var hall = GameObject.Find("HallPanel");
        if (hall == null)
        {
            Debug.LogError("[LobbySceneBuilder] HallPanel not found. Generate the lobby scene first.");
            return;
        }

        CreateLobbyModelPreview(hall.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[LobbySceneBuilder] Lobby model preview added/updated. Put a model prefab under Assets/Resources/Models/LobbyHero.prefab or Assets/Resources/Models/LobbyHero.fbx.");
    }

    static void CreateLobbyModelPreview(Transform parent)
    {
        if (parent == null)
            return;

        string[] previewChromeNames = { "LobbyModelPreviewGlow", "LobbyModelPreviewPlinth", "LobbyModelPreview" };
        for (int i = 0; i < previewChromeNames.Length; i++)
        {
            var existing = parent.Find(previewChromeNames[i]);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        var glowGO = CreateLobbyImage(parent, "LobbyModelPreviewGlow",
            new Vector2(0.5f, 0.755f), new Vector2(0.5f, 0.755f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -4f), new Vector2(372f, 246f), new Color(0.95f, 0.76f, 0.36f, 0.12f));
        glowGO.transform.SetSiblingIndex(0);
        var glowImg = glowGO.GetComponent<Image>();
        glowImg.sprite = Resources.Load<Sprite>("LobbyGen/gen_card_fade");
        glowImg.type = Image.Type.Sliced;
        glowImg.raycastTarget = false;

        var plinthGO = CreateLobbyImage(parent, "LobbyModelPreviewPlinth",
            new Vector2(0.5f, 0.662f), new Vector2(0.5f, 0.662f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(244f, 42f), new Color(0.05f, 0.045f, 0.026f, 0.46f));
        plinthGO.transform.SetSiblingIndex(1);
        var plinthImg = plinthGO.GetComponent<Image>();
        plinthImg.sprite = Resources.Load<Sprite>("LobbyGen/gen_card_fade");
        plinthImg.type = Image.Type.Sliced;
        plinthImg.raycastTarget = false;

        var previewGO = new GameObject("LobbyModelPreview");
        previewGO.transform.SetParent(parent, false);
        previewGO.transform.SetSiblingIndex(2);

        var rt = previewGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.758f);
        rt.anchorMax = new Vector2(0.5f, 0.758f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(332f, 238f);
        rt.anchoredPosition = Vector2.zero;

        var raw = previewGO.AddComponent<RawImage>();
        raw.color = new Color(1f, 1f, 1f, 0.96f);
        raw.raycastTarget = false;

        var preview = previewGO.AddComponent<LobbyModelPreview>();
        preview.TargetImage = raw;
        preview.PreferStreamingAssetBundle = true;
        preview.AssetBundlePath = "LobbyModels/lobbyhero";
        preview.AssetBundleAssetName = "LobbyHero";
        preview.ResourcesPath = "Models/LobbyHero";
        preview.EnableRuntimeObjFallback = false;
        preview.ShowFallbackWhileLoading = false;
        preview.RuntimeObjPath = "LobbyModels/LobbyHero.obj";
        preview.MaxRuntimeObjFileSizeMB = 24f;
        preview.MaxRuntimeObjOutputVertices = 120000;
        preview.MaxRuntimeObjTriangles = 180000;
        preview.AutoTextureSize = true;
        preview.MinTextureSize = 256;
        preview.MaxTextureSize = 1024;
        preview.AutoFrameCamera = true;
        preview.FramePadding = 1.16f;
        preview.MinCameraDistance = 2.6f;
        preview.MaxCameraDistance = 10f;
        preview.AllowPointerInteraction = true;
        preview.RequireAltForPointerInteraction = true;
        preview.DragToRotate = true;
        preview.ScrollToZoom = true;
        preview.FillMissingPreviewMaterials = true;
        preview.AlternativeResourcePaths = new[]
        {
            "Models/LobbyHeroTank",
            "Models/LobbyHero",
            "Prefabs/Tank_P",
            "Prefabs/Artillery_P",
            "Models/Tank",
            "Lobby/LobbyHero",
            "Units/Tank"
        };
        preview.TextureSize = 896;
        preview.TargetHeight = 2.62f;
        preview.ModelEulerAngles = new Vector3(0f, 138f, 0f);
        preview.AutoRotate = true;
        preview.RotateSpeed = 7.5f;
        preview.CameraDistance = 4.8f;
    }

    /// <summary>透明点击区按钮：归一化锚点居中 + 像素 sizeDelta。带 ButtonClickPulse 反馈。</summary>
    static Button CreateLobbyHitButton(Transform parent, string name, Vector2 normAnchor, Vector2 size)
    {
        var btn = CreateLobbyHitButtonRaw(parent, name, normAnchor, size);
        if (btn != null)
        {
            btn.gameObject.AddComponent<ButtonClickPulse>();
            // 不可见识别标签：alpha=0，仅 Editor 选中时可看到名字
            AddInvisibleLabel(btn.transform, GuessHitButtonLabel(name));
        }
        return btn;
    }

    /// <summary>按按钮 GameObject 名推断中文标签（用于不可见识别）。</summary>
    static string GuessHitButtonLabel(string name)
    {
        switch (name)
        {
            case "SettingsIconBtn":      return "设置";
            case "NotifyBellBtn":        return "通知";
            case "GoldPlusBtn":          return "金币+";
            case "GemPlusBtn":           return "钻石+";
            case "QuickMatchButton":     return "匹配";
            case "CustomRoomButton":     return "自定义";
            case "GlobalConquestButton": return "争霸";
            case "NavShopBtn":           return "商店";
            case "NavWarehouseBtn":      return "仓库";
            case "NavCampaignBtn":       return "战役";
            case "NavRankBtn":           return "排行榜";
            case "NavMailBtn":           return "邮件";
            case "AddFriendIconBtn":     return "加好友";
            case "ViewAllFriendsBtn":    return "查看全部好友";
            case "MoreTasksBtn":         return "更多任务";
            case "TechStartBtn":         return "开始研究";
            case "TechSpeedBtn":         return "加速";
            case "TechTreeBtn":          return "科技树";
            default:                     return name;
        }
    }

    /// <summary>添加 alpha=0 的不可见识别标签，运行时不显示但 Hierarchy 选中可看见。</summary>
    static void AddInvisibleLabel(Transform parent, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var go = new GameObject("_Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 14;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 1f, 1f, 0f); // 透明
        t.raycastTarget = false;
    }

    static Button CreateLobbyHitButtonRaw(Transform parent, string name, Vector2 normAnchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.004f); // 几乎透明，但参与 raycast
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.004f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.10f);
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f, 0.20f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.06f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.05f);
        btn.colors = colors;
        return btn;
    }

    static void AddSettingsGearIcon(Transform parent, Vector2 size)
    {
        if (parent == null) return;
        var sprite = LoadSettingsGearSprite();
        if (sprite == null) return;

        var go = new GameObject("GearIcon");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    static Sprite LoadSettingsGearSprite()
    {
        const string assetPath = "Assets/Resources/icons3/gear.png";
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.mipmapEnabled || !ti.alphaIsTransparency))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
        return sprite;
    }

    /// <summary>纯文字 Label。</summary>
    static Text CreateLabel(Transform parent, string name, string value, Vector2 normAnchor, Vector2 size, int fontSize, Color color, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = value;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 8;
        t.resizeTextMaxSize = fontSize;
        t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
        sh.effectDistance = new Vector2(1.2f, -1.2f);
        return t;
    }

    /// <summary>简易进度条 Slider。</summary>
    static Slider CreateRowSlider(Transform parent, string name, Vector2 normAnchor, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        // 科技进度条：弱化黑底，让原图科技油画透出
        bgImg.color = new Color(0f, 0f, 0f, 0.20f);
        bgImg.raycastTarget = false;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(2f, 2f); faRT.offsetMax = new Vector2(-2f, -2f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
        var fImg = fill.AddComponent<Image>();
        fImg.color = fillColor;
        fImg.raycastTarget = false;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fRT;
        slider.targetGraphic = fImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        return slider;
    }

    /// <summary>好友行：透明背景 + 名字/状态 + 头像。整行带 Button 用于点击。</summary>
    static GameObject CreateFriendRow(Transform parent, int idx, Vector2 normAnchor, Vector2 size)
    {
        var go = new GameObject("FriendRow" + idx);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.004f);
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var bc = btn.colors;
        bc.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        bc.pressedColor = new Color(1f, 1f, 1f, 0.20f);
        btn.colors = bc;

        // 头像位（小圆，左侧）
        var avatarGO = new GameObject("FriendAvatar" + idx);
        avatarGO.transform.SetParent(go.transform, false);
        var arRT = avatarGO.AddComponent<RectTransform>();
        arRT.anchorMin = new Vector2(0f, 0.5f);
        arRT.anchorMax = new Vector2(0f, 0.5f);
        arRT.pivot = new Vector2(0f, 0.5f);
        arRT.anchoredPosition = new Vector2(4f, 0f);
        arRT.sizeDelta = new Vector2(48f, 48f);
        var arImg = avatarGO.AddComponent<Image>();
        // 默认完全透明（保留原图槽位），LobbyManager 收到好友数据时设回不透明 + 头像 sprite
        arImg.color = new Color(0.25f, 0.30f, 0.20f, 0f);
        arImg.raycastTarget = false;
        arImg.preserveAspect = true;

        // 名字（靠左偏上）
        var nameGO = new GameObject("FriendName" + idx);
        nameGO.transform.SetParent(go.transform, false);
        var nRT = nameGO.AddComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 1f);
        nRT.anchorMax = new Vector2(1f, 1f);
        nRT.pivot = new Vector2(0f, 1f);
        nRT.anchoredPosition = new Vector2(58f, -4f);
        nRT.sizeDelta = new Vector2(-64f, 28f);
        var nT = nameGO.AddComponent<Text>();
        nT.text = "";
        nT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nT.fontSize = 16;
        nT.fontStyle = FontStyle.Bold;
        nT.alignment = TextAnchor.MiddleLeft;
        nT.color = new Color(1f, 0.94f, 0.74f, 1f);
        nT.horizontalOverflow = HorizontalWrapMode.Overflow;
        nT.raycastTarget = false;

        // 等级（名字右侧小字）
        var lvlGO = new GameObject("FriendLevelText" + idx);
        lvlGO.transform.SetParent(go.transform, false);
        var lRT = lvlGO.AddComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.5f);
        lRT.anchorMax = new Vector2(0f, 0.5f);
        lRT.pivot = new Vector2(0.5f, 0.5f);
        lRT.anchoredPosition = new Vector2(28f, -18f);
        lRT.sizeDelta = new Vector2(40f, 18f);
        var lT = lvlGO.AddComponent<Text>();
        lT.text = "";
        lT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lT.fontSize = 12;
        lT.fontStyle = FontStyle.Bold;
        lT.alignment = TextAnchor.MiddleCenter;
        lT.color = new Color(0.96f, 0.84f, 0.42f, 1f);
        lT.raycastTarget = false;

        // 状态（靠左下方）
        var statusGO = new GameObject("FriendStatus" + idx);
        statusGO.transform.SetParent(go.transform, false);
        var sRT = statusGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f);
        sRT.anchorMax = new Vector2(1f, 0f);
        sRT.pivot = new Vector2(0f, 0f);
        sRT.anchoredPosition = new Vector2(58f, 4f);
        sRT.sizeDelta = new Vector2(-64f, 24f);
        var sT = statusGO.AddComponent<Text>();
        sT.text = "";
        sT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        sT.fontSize = 12;
        sT.alignment = TextAnchor.MiddleLeft;
        sT.color = new Color(0.62f, 0.85f, 0.62f, 1f);
        sT.horizontalOverflow = HorizontalWrapMode.Overflow;
        sT.raycastTarget = false;

        // 元数据（与状态同行右侧）
        var metaGO = new GameObject("FriendMeta" + idx);
        metaGO.transform.SetParent(go.transform, false);
        var mRT = metaGO.AddComponent<RectTransform>();
        mRT.anchorMin = new Vector2(1f, 0f);
        mRT.anchorMax = new Vector2(1f, 0f);
        mRT.pivot = new Vector2(1f, 0f);
        mRT.anchoredPosition = new Vector2(-4f, 4f);
        mRT.sizeDelta = new Vector2(80f, 22f);
        var mT = metaGO.AddComponent<Text>();
        mT.text = "";
        mT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mT.fontSize = 12;
        mT.alignment = TextAnchor.MiddleRight;
        mT.color = new Color(0.86f, 0.78f, 0.52f, 1f);
        mT.raycastTarget = false;

        return go;
    }

    /// <summary>任务行：标题 / 进度文字 / 进度条 / 领取按钮。</summary>
    static void CreateTaskRow(Transform parent, int idx, Vector2 normAnchor, Vector2 size,
        out Text titleText, out Text progText, out Slider slider, out Button claimBtn)
    {
        var rowGO = new GameObject("TaskRow" + idx);
        rowGO.transform.SetParent(parent, false);
        var rt = rowGO.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        // 标题
        var titleGO = new GameObject("TaskTitle" + idx);
        titleGO.transform.SetParent(rowGO.transform, false);
        var tRT = titleGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 1f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.pivot = new Vector2(0f, 1f);
        tRT.anchoredPosition = new Vector2(8f, -2f);
        tRT.sizeDelta = new Vector2(-120f, 22f);
        titleText = titleGO.AddComponent<Text>();
        titleText.text = "";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = new Color(0.96f, 0.90f, 0.70f, 1f);
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Truncate;
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 8;
        titleText.resizeTextMaxSize = 13;
        titleText.raycastTarget = false;

        // 进度文字
        var pgGO = new GameObject("TaskProg" + idx);
        pgGO.transform.SetParent(rowGO.transform, false);
        var pRT = pgGO.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(1f, 1f);
        pRT.anchorMax = new Vector2(1f, 1f);
        pRT.pivot = new Vector2(1f, 1f);
        pRT.anchoredPosition = new Vector2(-50f, -2f);
        pRT.sizeDelta = new Vector2(42f, 20f);
        progText = pgGO.AddComponent<Text>();
        progText.text = "";
        progText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progText.fontSize = 12;
        progText.fontStyle = FontStyle.Bold;
        progText.alignment = TextAnchor.MiddleRight;
        progText.color = new Color(0.50f, 0.95f, 0.62f, 1f);
        progText.horizontalOverflow = HorizontalWrapMode.Wrap;
        progText.verticalOverflow = VerticalWrapMode.Truncate;
        progText.resizeTextForBestFit = true;
        progText.resizeTextMinSize = 8;
        progText.resizeTextMaxSize = 12;
        progText.raycastTarget = false;

        // 进度条
        slider = CreateRowSliderInside(rowGO.transform, "TaskSlider" + idx,
            new Vector2(0.38f, 0.18f), new Vector2(108f, 8f), new Color(0.34f, 0.92f, 0.42f, 1f));

        // 领取按钮（默认透明，由 LobbyManager 在任务可领取时显示棕色底）
        var claimGO = new GameObject("TaskClaim" + idx);
        claimGO.transform.SetParent(rowGO.transform, false);
        var cRT = claimGO.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1f, 0.5f);
        cRT.anchorMax = new Vector2(1f, 0.5f);
        cRT.pivot = new Vector2(1f, 0.5f);
        cRT.anchoredPosition = new Vector2(-4f, -8f);
        cRT.sizeDelta = new Vector2(56f, 24f);
        var cImg = claimGO.AddComponent<Image>();
        cImg.color = new Color(0.45f, 0.30f, 0.10f, 0f);
        cImg.raycastTarget = true;
        claimBtn = claimGO.AddComponent<Button>();
        claimBtn.targetGraphic = cImg;
        var cc = claimBtn.colors;
        cc.normalColor = new Color(1f, 1f, 1f, 1f);
        cc.highlightedColor = new Color(1f, 0.98f, 0.78f, 1f);
        cc.pressedColor = new Color(0.7f, 0.55f, 0.30f, 1f);
        claimBtn.colors = cc;

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(claimGO.transform, false);
        var lRT = lblGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero;
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero;
        lRT.offsetMax = Vector2.zero;
        var lT = lblGO.AddComponent<Text>();
        lT.text = "领取";
        lT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lT.fontSize = 14;
        lT.fontStyle = FontStyle.Bold;
        lT.alignment = TextAnchor.MiddleCenter;
        // 默认透明：任务行整行 SetActive(false) 时不显示，可领取时由 LobbyManager 显示
        lT.color = new Color(1f, 0.86f, 0.42f, 0f);
        lT.raycastTarget = false;
    }

    static Slider CreateRowSliderInside(Transform parent, string name, Vector2 normAnchor, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        // 弱化黑底，让原图油画质感透出
        bgImg.color = new Color(0f, 0f, 0f, 0.20f);
        bgImg.raycastTarget = false;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(2f, 2f); faRT.offsetMax = new Vector2(-2f, -2f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRT = fill.AddComponent<RectTransform>();
        fRT.anchorMin = Vector2.zero; fRT.anchorMax = Vector2.one;
        fRT.offsetMin = Vector2.zero; fRT.offsetMax = Vector2.zero;
        var fImg = fill.AddComponent<Image>();
        fImg.color = fillColor;
        fImg.raycastTarget = false;

        var slider = go.AddComponent<Slider>();
        slider.fillRect = fRT;
        slider.targetGraphic = fImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;
        return slider;
    }

    /// <summary>金币/钻石数字（按钮子节点版）：相对父按钮中心偏移，size 撑满。</summary>
    static Text CreateTopBarCountTextChild(Transform parent, string name, string value, Vector2 offsetFromCenter, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offsetFromCenter;
        rt.sizeDelta = new Vector2(180f, 38f);
        var txt = go.AddComponent<Text>();
        txt.text = value;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.resizeTextMaxSize = 22;
        txt.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return txt;
    }

    /// <summary>顶栏金币/钻石数字。归一化锚点定位（相对 HallPanel 整个画布）。</summary>
    static Text CreateTopBarCountText(Transform parent, string name, string value, Vector2 normAnchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = normAnchor;
        rt.anchorMax = normAnchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(180f, 38f);
        var txt = go.AddComponent<Text>();
        txt.text = value;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 10;
        txt.resizeTextMaxSize = 22;
        txt.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return txt;
    }

    // ── 大厅 UI 辅助生成 ───────────────────────────────
    static GameObject CreateLobbyImage(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static Text CreateLobbyText(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, int fontSize,
        TextAnchor align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = color;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.raycastTarget = false;
        return txt;
    }

    static Button CreateLobbyButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        AssignUiSprite(img);
        img.type = Image.Type.Sliced;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddButtonEdgeShadow(img);
        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r * 1.15f, bgColor.g * 1.15f, bgColor.b * 1.15f, bgColor.a);
        colors.pressedColor = new Color(bgColor.r * 0.8f, bgColor.g * 0.8f, bgColor.b * 0.8f, bgColor.a);
        btn.colors = colors;
        // 按钮文字
        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.AddComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;
        var t = txtGO.AddComponent<Text>();
        t.text = label;
        t.fontSize = 30;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.96f, 0.85f, 1f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;
        return btn;
    }

    // 程序化军事风贴图，写入 Resources/LobbyGen，方便打包引用。
    static void EnsureLobbyVisualSprites()
    {
        const string ap = "Assets/Resources/LobbyGen";
        BuiltinUiSprite();
        TryPullReferenceDeskFromDesktop(overwrite: false);
        _sprBgDesk = TryLoadBackgroundReference(ap);
        _sprRefMaster = ImportReferenceSpriteIfPresent(ap + "/_ref_lobby_master.png")
            ?? ImportReferenceSpriteIfPresent(ap + "/ref_lobby_master.png");
        _sprExactFriendPanel = ImportOrCropReferencePiece(ap + "/gen_exact_friend_panel.png", new RectInt(8, 88, 256, 494), ExactPieceMask.Panel);
        _sprExactModeMatch  = ImportOrCropReferencePiece(ap + "/gen_exact_mode_match.png",  new RectInt(306, 386, 412, 145), ExactPieceMask.Mode);
        _sprExactModeCustom = ImportOrCropReferencePiece(ap + "/gen_exact_mode_custom.png", new RectInt(306, 238, 412, 132), ExactPieceMask.Mode);
        _sprExactModeGlobal = ImportOrCropReferencePiece(ap + "/gen_exact_mode_global.png", new RectInt(306,  94, 412, 132), ExactPieceMask.Mode);
        _sprExactTaskPanel  = ImportOrCropReferencePiece(ap + "/gen_exact_task_panel.png",  new RectInt(760, 270, 250, 310), ExactPieceMask.Panel);
        _sprExactTechPanel  = ImportOrCropReferencePiece(ap + "/gen_exact_tech_panel.png",  new RectInt(764,  96, 246, 166), ExactPieceMask.Panel);
        _sprExactClaimButton = ImportOrCropReferencePiece(ap + "/gen_exact_claim_button.png", new RectInt(922, 452, 78, 46), ExactPieceMask.Panel);
        _sprExactMatchButtonBorder = ImportOrCropReferencePiece(ap + "/gen_exact_match_button_border.png", new RectInt(306, 386, 412, 145), ExactPieceMask.Mode);
        _sprExactNavShop      = ImportOrCropReferencePiece(ap + "/gen_exact_nav_shop.png",      new RectInt(0,   0, 190, 70), ExactPieceMask.Panel);
        _sprExactNavWarehouse = ImportOrCropReferencePiece(ap + "/gen_exact_nav_warehouse.png", new RectInt(190, 0, 240, 70), ExactPieceMask.Panel);
        _sprExactNavCampaign  = ImportOrCropReferencePiece(ap + "/gen_exact_nav_campaign.png",  new RectInt(430, 0, 164, 82), ExactPieceMask.Panel);
        _sprExactNavRank      = ImportOrCropReferencePiece(ap + "/gen_exact_nav_rank.png",      new RectInt(594, 0, 254, 70), ExactPieceMask.Panel);
        _sprExactNavMail      = ImportOrCropReferencePiece(ap + "/gen_exact_nav_mail.png",      new RectInt(848, 0, 176, 70), ExactPieceMask.Panel);
        if (_sprBgDesk == null)
        {
            Debug.Log("[LobbySceneBuilder] No reference desk background found. Using generated gen_bg_desk.");
            _sprBgDesk = ImportOrGenerateSprite(ap + "/gen_bg_desk.png", GenWarRoomDesk);
        }
        _sprThumbMatch   = ImportOrGenerateSprite(ap + "/gen_thumb_match.png", () => GenModeThumb(1024, 288, 2));
        _sprThumbCustom  = ImportOrGenerateCustomModeThumb(ap);
        _sprThumbGlobal  = ImportOrGenerateSprite(ap + "/gen_thumb_global.png", () => GenModeThumb(1024, 288, 2));
        _sprCardFade     = ImportOrGenerateSprite(ap + "/gen_card_fade.png", GenCardFade);
        _sprMetalPanel   = ImportOrGenerateSprite(ap + "/gen_metal_noise.png", GenMetalNoise);
        _sprPanelFrame   = ImportOrGenerateSprite(ap + "/gen_panel_frame.png", GenPanelFrame, new Vector4(22, 22, 22, 22));
        _sprCardFrame    = ImportOrGenerateSprite(ap + "/gen_card_frame.png", GenCardFrame, new Vector4(26, 26, 26, 26));
        _sprModeCardFrame = ImportOrGenerateSprite(ap + "/gen_mode_card_frame.png", GenModeCardFrame, new Vector4(30, 24, 30, 24));
        _sprFriendRow    = ImportOrGenerateSprite(ap + "/gen_friend_row.png", GenFriendRowFrame, new Vector4(18, 18, 18, 18));
        _sprMissionRow   = ImportOrGenerateSprite(ap + "/gen_mission_row.png", GenMissionRowFrame, new Vector4(24, 16, 24, 16));
        _sprTaskCard     = ImportOrGenerateSprite(ap + "/gen_task_card.png", GenTaskCardFrame, new Vector4(24, 24, 24, 24));
        _sprTechCard     = ImportOrGenerateSprite(ap + "/gen_tech_card.png", GenTechCardFrame, new Vector4(24, 24, 24, 24));
        _sprProgressTrack = ImportOrGenerateSprite(ap + "/gen_progress_track.png", () => GenProgressBar(false), new Vector4(10, 5, 10, 5));
        _sprProgressFill  = ImportOrGenerateSprite(ap + "/gen_progress_fill.png", () => GenProgressBar(true), new Vector4(9, 4, 9, 4));
        ImportOrBuildNineSlicePreview(ap + "/gen_match_frame_9slice_preview.png",
            ap + "/ref_match_frame_source.png", 548, 136, new RectOffset(180, 180, 180, 180));
        _sprButtonGold   = ImportOrGenerateSprite(ap + "/gen_button_gold.png", () => GenButtonFrame(true), new Vector4(20, 20, 20, 20));
        _sprButtonDark   = ImportOrGenerateSprite(ap + "/gen_button_dark.png", () => GenButtonFrame(false), new Vector4(20, 20, 20, 20));
        _sprButtonTechMetal = ImportOrGenerateSprite(ap + "/gen_button_tech_metal.png", GenTechMetalButtonFrame, new Vector4(28, 22, 28, 22));
        _sprNavTab       = ImportOrGenerateSprite(ap + "/gen_nav_tab.png", () => GenNavTabFrame(false), new Vector4(18, 18, 18, 18));
        _sprNavTabActive = ImportOrGenerateSprite(ap + "/gen_nav_tab_active.png", () => GenNavTabFrame(true), new Vector4(18, 18, 18, 18));
        _sprDot          = ImportOrGenerateSprite(ap + "/gen_dot.png", GenRedDot);
        _sprPortraitRing = ImportOrGenerateSprite(ap + "/gen_portrait_ring.png", GenPortraitRing);
        _sprAvatarPlayer   = ImportOrGenerateSprite(ap + "/gen_avatar_player.png", () => GenAvatarFace(0));
        _sprAvatarFriendA  = ImportOrGenerateSprite(ap + "/gen_avatar_friend_a.png", () => GenAvatarFace(1));
        _sprAvatarFriendB  = ImportOrGenerateSprite(ap + "/gen_avatar_friend_b.png", () => GenAvatarFace(2));
        _sprIconGear     = ImportOrGenerateSprite(ap + "/gen_icon_gear.png", GenIconGear);
        _sprIconBell     = ImportOrGenerateSprite(ap + "/gen_icon_bell.png", GenIconBell);
        _sprIconStar     = ImportOrGenerateSprite(ap + "/gen_icon_star.png", GenIconStarGold);
        _sprIconGem      = ImportOrGenerateSprite(ap + "/gen_icon_gem.png", GenIconGem);
        _sprIconHelmet   = ImportOrGenerateSprite(ap + "/gen_icon_helmet.png", GenIconHelmet);
        _sprIconClipboard = ImportOrGenerateSprite(ap + "/gen_icon_clipboard.png", GenIconClipboard);
        _sprIconMatch    = ImportOrGenerateSprite(ap + "/gen_icon_match.png", GenIconMatch);
        _sprIconCustom   = ImportOrGenerateSprite(ap + "/gen_icon_custom.png", GenIconCustom);
        _sprIconWar      = ImportOrGenerateSprite(ap + "/gen_icon_war.png", GenIconWar);
        _sprIconTank     = ImportOrGenerateSprite(ap + "/gen_icon_tank.png", GenIconTank);
        _sprIconTaskCrate = ImportOrGenerateSprite(ap + "/gen_icon_task_crate.png", GenIconTaskCrate);
        _sprIconTechBlueprint = ImportOrGenerateSprite(ap + "/gen_icon_tech_blueprint.png", GenIconTechBlueprint);
        _sprNavShop      = ImportOrGenerateSprite(ap + "/gen_nav_shop.png", () => GenNavIcon(0));
        _sprNavWarehouse = ImportOrGenerateSprite(ap + "/gen_nav_wh.png", () => GenNavIcon(1));
        _sprNavWarehouseButton = ImportOrGenerateSprite(ap + "/gen_nav_wh_button.png", GenWarehouseNavButtonFrame, new Vector4(32, 28, 32, 28));
        _sprNavCampaign  = ImportOrGenerateSprite(ap + "/gen_nav_map.png", () => GenNavIcon(2));
        _sprNavRank      = ImportOrGenerateSprite(ap + "/gen_nav_rank.png", () => GenNavIcon(3));
        _sprNavMail      = ImportOrGenerateSprite(ap + "/gen_nav_mail.png", () => GenNavIcon(4));
    }

    static Sprite ImportOrGenerateSprite(string assetPath, System.Func<Texture2D> gen, Vector4? spriteBorder = null)
    {
        if (!assetPath.StartsWith("Assets/"))
            throw new System.ArgumentException(assetPath);
        string rel = assetPath.Substring(7).Replace('/', Path.DirectorySeparatorChar);
        string diskPath = Path.Combine(Application.dataPath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(diskPath));
        var tex = gen();
        File.WriteAllBytes(diskPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        if (assetPath.EndsWith("gen_exact_mode_frame_button.png", StringComparison.Ordinal)
            || assetPath.EndsWith("gen_exact_match_button_border.png", StringComparison.Ordinal))
            ti.spriteBorder = new Vector4(34f, 30f, 34f, 30f);
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.anisoLevel = 0;
        ti.alphaIsTransparency = true;
        if (spriteBorder.HasValue)
            ti.spriteBorder = spriteBorder.Value;
        ApplyCrispSpritePlatformSettings(ti);
        ti.SaveAndReimport();

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (spr == null)
            Debug.LogError("[LobbySceneBuilder] Sprite import failed: " + assetPath);
        return spr;
    }

    static Sprite ImportOrBuildNineSlicePreview(string assetPath, string sourceAssetPath,
        int width, int height, RectOffset border)
    {
        if (!sourceAssetPath.StartsWith("Assets/"))
            throw new System.ArgumentException(sourceAssetPath);

        string rel = sourceAssetPath.Substring(7).Replace('/', Path.DirectorySeparatorChar);
        string sourceDisk = Path.Combine(Application.dataPath, rel);
        if (!File.Exists(sourceDisk))
            return null;

        var src = LoadPngFromDisk(sourceDisk);
        if (src == null)
            return null;

        var preview = BuildNineSliceTexture(src, width, height, border);
        UnityEngine.Object.DestroyImmediate(src);
        return ImportGeneratedLobbySprite(assetPath, preview);
    }

    static Texture2D BuildNineSliceTexture(Texture2D src, int width, int height, RectOffset border)
    {
        int left = Mathf.Clamp(border.left, 1, Mathf.Max(1, src.width / 2 - 1));
        int right = Mathf.Clamp(border.right, 1, Mathf.Max(1, src.width - left - 1));
        int top = Mathf.Clamp(border.top, 1, Mathf.Max(1, src.height / 2 - 1));
        int bottom = Mathf.Clamp(border.bottom, 1, Mathf.Max(1, src.height - top - 1));
        var dst = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float sx = MapNineSliceCoord(x, width, src.width, left, right);
            float sy = MapNineSliceCoord(y, height, src.height, bottom, top);
            dst.SetPixel(x, y, src.GetPixelBilinear(
                Mathf.Clamp01((sx + 0.5f) / src.width),
                Mathf.Clamp01((sy + 0.5f) / src.height)));
        }

        dst.Apply();
        return dst;
    }

    static float MapNineSliceCoord(int d, int dstSize, int srcSize, int startBorder, int endBorder)
    {
        int dstEndStart = Mathf.Max(startBorder, dstSize - endBorder);
        int srcEndStart = Mathf.Max(startBorder, srcSize - endBorder);
        if (d < startBorder)
            return d;
        if (d >= dstEndStart)
            return srcEndStart + (d - dstEndStart);

        float dstMid = Mathf.Max(1f, dstEndStart - startBorder);
        float srcMid = Mathf.Max(1f, srcEndStart - startBorder);
        return startBorder + (d - startBorder + 0.5f) / dstMid * srcMid - 0.5f;
    }

    /// <summary>从桌面拉取参考图：ref_lobby_desk_no_bottom_map、lobby_desk_only、_ref_lobby_master。</summary>
    static bool TryPullReferenceDeskFromDesktop(bool overwrite)
    {
        string genDir = Path.Combine(Application.dataPath, "Resources", "LobbyGen");
        if (!string.IsNullOrEmpty(genDir))
            Directory.CreateDirectory(genDir);

        string destMaster = Path.Combine(genDir, "_ref_lobby_master.png");
        if (!File.Exists(destMaster) || overwrite)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            foreach (var name in new[] { "_ref_lobby_master.png", "lobby_ref.png", "lobby_background.png", "大厅参考.png" })
            {
                string src = Path.Combine(desk, name);
                if (!File.Exists(src)) continue;
                File.Copy(src, destMaster, overwrite: true);
                Debug.Log("[LobbySceneBuilder] Copied reference background from Desktop: " + src);
                break;
            }
        }

        string destDeskOnly = Path.Combine(genDir, "lobby_desk_only.png");
        if (!File.Exists(destDeskOnly) || overwrite)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            foreach (var name in new[] { "lobby_desk_only.png", "_ref_lobby_desk_only.png", "大厅桌子背景.png" })
            {
                string src = Path.Combine(desk, name);
                if (!File.Exists(src)) continue;
                File.Copy(src, destDeskOnly, overwrite: true);
                Debug.Log("[LobbySceneBuilder] Copied desk-only background from Desktop: " + src);
                break;
            }
        }

        string destNoBottomMap = Path.Combine(genDir, "ref_lobby_desk_no_bottom_map.png");
        if (!File.Exists(destNoBottomMap) || overwrite)
        {
            string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            foreach (var name in new[] { "ref_lobby_desk_no_bottom_map.png" })
            {
                string src = Path.Combine(desk, name);
                if (!File.Exists(src)) continue;
                File.Copy(src, destNoBottomMap, overwrite: true);
                Debug.Log("[LobbySceneBuilder] Copied desk no-bottom-map background from Desktop: " + src);
                break;
            }
        }

        return File.Exists(destMaster) || File.Exists(destDeskOnly) || File.Exists(destNoBottomMap);
    }

    /// <summary>从桌面复制「自定义」模式条背景 / 左侧图徽到 LobbyGen（与 ref 同名，供合成 gen_thumb_custom）。</summary>
    static bool TryPullCustomModeThumbRefsFromDesktop(bool overwrite)
    {
        string genDir = Path.Combine(Application.dataPath, "Resources", "LobbyGen");
        Directory.CreateDirectory(genDir);
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        bool any = false;

        string destBg = Path.Combine(genDir, "ref_thumb_custom_bg.png");
        if (!File.Exists(destBg) || overwrite)
        {
            foreach (var name in new[] {
                "ref_thumb_custom_bg.png", "mode_custom_bg.png", "自定义模式条背景.png", "RTS_mode_custom_bg.png"
            })
            {
                string src = Path.Combine(desk, name);
                if (!File.Exists(src)) continue;
                File.Copy(src, destBg, overwrite: true);
                Debug.Log("[LobbySceneBuilder] Copied custom mode thumb background: " + src);
                any = true;
                break;
            }
        }

        string destEm = Path.Combine(genDir, "ref_thumb_custom_emblem.png");
        if (!File.Exists(destEm) || overwrite)
        {
            foreach (var name in new[] {
                "ref_thumb_custom_emblem.png", "mode_custom_emblem.png", "自定义模式图徽.png", "RTS_mode_custom_emblem.png"
            })
            {
                string src = Path.Combine(desk, name);
                if (!File.Exists(src)) continue;
                File.Copy(src, destEm, overwrite: true);
                Debug.Log("[LobbySceneBuilder] Copied custom mode thumb emblem: " + src);
                any = true;
                break;
            }
        }

        return any || File.Exists(destBg) || File.Exists(destEm);
    }

    static Texture2D LoadPngFromDisk(string diskPath)
    {
        if (string.IsNullOrEmpty(diskPath) || !File.Exists(diskPath)) return null;
        byte[] raw = File.ReadAllBytes(diskPath);
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(raw))
        {
            UnityEngine.Object.DestroyImmediate(t);
            return null;
        }
        return t;
    }

    /// <summary>等比放大后居中裁剪，铺满 dw×dh（易裁掉超宽横幅左右/上下重点，仅备用）。</summary>
    static Texture2D ScaleCoverTo(Texture2D src, int dw, int dh)
    {
        if (src == null || dw < 1 || dh < 1) return null;
        var dst = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
        float s = Mathf.Max(dw / (float)src.width, dh / (float)src.height);
        float ox = (src.width * s - dw) * 0.5f;
        float oy = (src.height * s - dh) * 0.5f;
        for (int y = 0; y < dh; y++)
        {
            for (int x = 0; x < dw; x++)
            {
                float sx = (x + ox) / s;
                float sy = (y + oy) / s;
                float u = (sx + 0.5f) / src.width;
                float v = (sy + 0.5f) / src.height;
                dst.SetPixel(x, y, src.GetPixelBilinear(
                    Mathf.Clamp01(u), Mathf.Clamp01(v)));
            }
        }
        dst.Apply();
        return dst;
    }

    /// <summary>等比完整放入 dw×dh，不足处 letterbox（适合超宽「自定义」条背景，不裁切画面主体）。</summary>
    static Texture2D ScaleContainLetterbox(Texture2D src, int dw, int dh, Color letterbox)
    {
        if (src == null || dw < 1 || dh < 1) return null;
        float s = Mathf.Min(dw / (float)src.width, dh / (float)src.height);
        int nw = Mathf.Max(1, Mathf.RoundToInt(src.width * s));
        int nh = Mathf.Max(1, Mathf.RoundToInt(src.height * s));
        var dst = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
        for (int y = 0; y < dh; y++)
        {
            for (int x = 0; x < dw; x++)
                dst.SetPixel(x, y, letterbox);
        }
        int ox = (dw - nw) / 2;
        int oy = (dh - nh) / 2;
        for (int y = 0; y < nh; y++)
        {
            float v = (y + 0.5f) / nh;
            for (int x = 0; x < nw; x++)
            {
                float u = (x + 0.5f) / nw;
                dst.SetPixel(ox + x, oy + y, src.GetPixelBilinear(u, v));
            }
        }
        dst.Apply();
        return dst;
    }

    /// <summary>等比缩放到不超过 maxW×maxH。</summary>
    static Texture2D ScaleContainTo(Texture2D src, int maxW, int maxH)
    {
        if (src == null || maxW < 1 || maxH < 1) return null;
        float s = Mathf.Min(maxW / (float)src.width, maxH / (float)src.height);
        int dw = Mathf.Max(1, Mathf.RoundToInt(src.width * s));
        int dh = Mathf.Max(1, Mathf.RoundToInt(src.height * s));
        var dst = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
        for (int y = 0; y < dh; y++)
        {
            for (int x = 0; x < dw; x++)
            {
                float u = (x + 0.5f) / dw;
                float v = (y + 0.5f) / dh;
                dst.SetPixel(x, y, src.GetPixelBilinear(u, v));
            }
        }
        dst.Apply();
        return dst;
    }

    static void AlphaBlendOnto(Texture2D dst, Texture2D src, int dstX0, int dstY0)
    {
        if (dst == null || src == null) return;
        for (int y = 0; y < src.height; y++)
        {
            int dy = dstY0 + y;
            if (dy < 0 || dy >= dst.height) continue;
            for (int x = 0; x < src.width; x++)
            {
                int dx = dstX0 + x;
                if (dx < 0 || dx >= dst.width) continue;
                Color b = dst.GetPixel(dx, dy);
                Color s = src.GetPixel(x, y);
                float a = Mathf.Clamp01(s.a);
                dst.SetPixel(dx, dy, new Color(
                    s.r * a + b.r * (1f - a),
                    s.g * a + b.g * (1f - a),
                    s.b * a + b.b * (1f - a),
                    1f));
            }
        }
        dst.Apply();
    }

    /// <summary>若有 ref_thumb_custom_bg.png：裁铺满 1024×288；若有 ref_thumb_custom_emblem.png：叠在左侧。</summary>
    static Texture2D TryBuildCustomModeThumbFromRefs(int w, int h)
    {
        string genDir = Path.Combine(Application.dataPath, "Resources", "LobbyGen");
        string bgDisk = Path.Combine(genDir, "ref_thumb_custom_bg.png");
        string emDisk = Path.Combine(genDir, "ref_thumb_custom_emblem.png");
        var bgSrc = LoadPngFromDisk(bgDisk);
        if (bgSrc == null) return null;

        var lb = new Color(0.07f, 0.065f, 0.055f, 1f);
        Texture2D bg = ScaleContainLetterbox(bgSrc, w, h, lb);
        UnityEngine.Object.DestroyImmediate(bgSrc);
        if (bg == null) return null;

        var emSrc = LoadPngFromDisk(emDisk);
        if (emSrc != null)
        {
            int maxW = Mathf.RoundToInt(w * 0.15f);
            int maxH = Mathf.RoundToInt(h * 0.78f);
            var em = ScaleContainTo(emSrc, maxW, maxH);
            UnityEngine.Object.DestroyImmediate(emSrc);
            if (em != null)
            {
                int pad = Mathf.Max(6, Mathf.RoundToInt(w * 0.018f));
                int ex = pad;
                int ey = (h - em.height) / 2;
                AlphaBlendOnto(bg, em, ex, ey);
                UnityEngine.Object.DestroyImmediate(em);
            }
        }

        return bg;
    }

    static Sprite ImportGeneratedLobbySprite(string assetPath, Texture2D tex)
    {
        if (tex == null) return null;
        if (!assetPath.StartsWith("Assets/"))
            throw new System.ArgumentException(assetPath);
        string rel = assetPath.Substring(7).Replace('/', Path.DirectorySeparatorChar);
        string diskPath = Path.Combine(Application.dataPath, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(diskPath));
        File.WriteAllBytes(diskPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.anisoLevel = 0;
        ti.alphaIsTransparency = true;
        ApplyCrispSpritePlatformSettings(ti);
        ti.SaveAndReimport();

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (spr == null)
            Debug.LogError("[LobbySceneBuilder] Sprite import failed: " + assetPath);
        return spr;
    }

    static Sprite ImportOrCropReferencePiece(string assetPath, RectInt crop, ExactPieceMask mask)
    {
        const string sourceAsset = "Assets/Resources/LobbyGen/_ref_lobby_master.png";
        string sourceDisk = Path.Combine(Application.dataPath, "Resources", "LobbyGen", "_ref_lobby_master.png");
        if (!File.Exists(sourceDisk))
            return null;

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(src, File.ReadAllBytes(sourceDisk)))
        {
            UnityEngine.Object.DestroyImmediate(src);
            return null;
        }

        crop.x = Mathf.Clamp(crop.x, 0, src.width - 1);
        crop.y = Mathf.Clamp(crop.y, 0, src.height - 1);
        crop.width = Mathf.Clamp(crop.width, 1, src.width - crop.x);
        crop.height = Mathf.Clamp(crop.height, 1, src.height - crop.y);

        var dst = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false);
        dst.SetPixels(src.GetPixels(crop.x, crop.y, crop.width, crop.height));
        ApplyExactPieceMask(dst, mask);
        // 保留原图素材的木牌/油画质感，运行时数据用透明 Text 叠加，
        // 不再"清空"面板内容（之前会变成纯黑+横线，丑）。
        // 如果以后需要清空，取消注释下方调用即可。
        // if (assetPath.EndsWith("gen_exact_friend_panel.png", StringComparison.Ordinal))
        //     ClearBakedFriendPanelContent(dst);
        // if (assetPath.EndsWith("gen_exact_task_panel.png", StringComparison.Ordinal))
        //     ClearBakedTaskPanelContent(dst);
        // if (assetPath.EndsWith("gen_exact_tech_panel.png", StringComparison.Ordinal))
        //     ClearBakedTechPanelContent(dst);
        if (assetPath.EndsWith("gen_exact_mode_frame_button.png", StringComparison.Ordinal))
            ClearExactModeButtonContent(dst);
        if (assetPath.EndsWith("gen_exact_match_button_border.png", StringComparison.Ordinal))
            ClearExactMatchButtonBorderContent(dst);
        if (assetPath.EndsWith("gen_exact_mode_match.png", StringComparison.Ordinal))
            PaintMatchModeStars(dst);
        dst.Apply();
        UnityEngine.Object.DestroyImmediate(src);
        Debug.Log("[LobbySceneBuilder] Cropped lobby reference piece from " + sourceAsset + " -> " + assetPath);
        return ImportGeneratedLobbySprite(assetPath, dst);
    }

    static void ClearExactModeButtonContent(Texture2D t)
    {
        if (t == null) return;

        Color fillA = new Color(0.085f, 0.095f, 0.058f, 1f);
        Color fillB = new Color(0.185f, 0.185f, 0.095f, 1f);
        ClearRectWithNoise(t,
            Mathf.RoundToInt(t.width * 0.075f),
            Mathf.RoundToInt(t.height * 0.21f),
            Mathf.RoundToInt(t.width * 0.930f),
            Mathf.RoundToInt(t.height * 0.795f),
            fillA, fillB);

        Color innerGold = new Color(0.72f, 0.64f, 0.36f, 0.85f);
        int left = Mathf.RoundToInt(t.width * 0.078f);
        int right = Mathf.RoundToInt(t.width * 0.925f);
        int bottom = Mathf.RoundToInt(t.height * 0.215f);
        int top = Mathf.RoundToInt(t.height * 0.790f);
        DrawThinLine(t, new Vector2(left, bottom), new Vector2(right, bottom), innerGold, 1.15f);
        DrawThinLine(t, new Vector2(left, top), new Vector2(right, top), innerGold, 1.15f);
        DrawThinLine(t, new Vector2(left, bottom), new Vector2(left, top), innerGold, 1.15f);
        DrawThinLine(t, new Vector2(right, bottom), new Vector2(right, top), new Color(0.18f, 0.15f, 0.08f, 0.9f), 1.15f);
    }

    static void ClearExactMatchButtonBorderContent(Texture2D t)
    {
        if (t == null) return;

        ClearTransparentRect(t, 27, 24, 385, 135);
        ClearTransparentRect(t, 140, 135, 275, 144);
    }

    static void ClearTransparentRect(Texture2D t, int left, int topFromImageTop, int right, int bottomFromImageTop)
    {
        if (t == null) return;

        int x0 = Mathf.Clamp(left, 0, t.width - 1);
        int x1 = Mathf.Clamp(right, 0, t.width - 1);
        int y0 = Mathf.Clamp(t.height - 1 - bottomFromImageTop, 0, t.height - 1);
        int y1 = Mathf.Clamp(t.height - 1 - topFromImageTop, 0, t.height - 1);
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            t.SetPixel(x, y, Color.clear);
    }

    static void DrawThinLine(Texture2D t, Vector2 a, Vector2 b, Color color, float width)
    {
        if (t == null) return;

        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1f), 0, t.width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1f), 0, t.width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1f), 0, t.height - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1f), 0, t.height - 1);

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float d = DistanceToSegment(new Vector2(x, y), a, b);
            if (d > width) continue;

            float k = Mathf.Clamp01(1f - d / Mathf.Max(0.001f, width));
            Color dst = t.GetPixel(x, y);
            t.SetPixel(x, y, Color.Lerp(dst, color, color.a * k));
        }
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f)
            return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    static void ClearBakedFriendPanelContent(Texture2D t)
    {
        if (t == null) return;

        // Keep the original frame/header/footer art, but clear the baked sample friends
        // so runtime friend data can be drawn without double text or avatars.
        // Bias toward clearing a little more of the inner content so no baked
        // avatars or labels survive, while still preserving the outer frame.
        int left = Mathf.RoundToInt(t.width * 0.040f);
        int right = Mathf.RoundToInt(t.width * 0.946f);
        int bottom = Mathf.RoundToInt(t.height * 0.108f);
        int top = Mathf.RoundToInt(t.height * 0.916f);
        Color baseDark = new Color(0.070f, 0.075f, 0.055f, 1f);
        Color warmDark = new Color(0.115f, 0.105f, 0.070f, 1f);

        for (int y = bottom; y <= top; y++)
        {
            float v = Mathf.InverseLerp(bottom, top, y);
            for (int x = left; x <= right; x++)
            {
                float u = Mathf.InverseLerp(left, right, x);
                Color c = Color.Lerp(baseDark, warmDark, 0.22f + v * 0.18f);
                float edge = Mathf.Min(Mathf.Min(x - left, right - x), Mathf.Min(y - bottom, top - y));
                if (edge < 5f)
                    c = Color.Lerp(c, new Color(0.035f, 0.038f, 0.028f, 1f), (5f - edge) / 5f * 0.55f);
                float grain = (Mathf.PerlinNoise(x * 0.055f, y * 0.055f) - 0.5f) * 0.035f;
                c.r += grain; c.g += grain; c.b += grain;
                t.SetPixel(x, y, c);
            }
        }

        int rows = 5;
        for (int i = 1; i < rows; i++)
        {
            int y = Mathf.RoundToInt(Mathf.Lerp(bottom, top, i / (float)rows));
            for (int yy = y - 1; yy <= y + 1; yy++)
            for (int x = left; x <= right; x++)
                t.SetPixel(x, yy, new Color(0.205f, 0.185f, 0.115f, 1f));
        }
    }

    static void ClearBakedTaskPanelContent(Texture2D t)
    {
        if (t == null) return;

        Color fillA = new Color(0.085f, 0.090f, 0.065f, 1f);
        Color fillB = new Color(0.125f, 0.112f, 0.074f, 1f);
        ClearRectWithNoise(t,
            Mathf.RoundToInt(t.width * 0.12f),
            Mathf.RoundToInt(t.height * 0.20f),
            Mathf.RoundToInt(t.width * 0.88f),
            Mathf.RoundToInt(t.height * 0.83f),
            fillA, fillB);
    }

    static void ClearBakedTechPanelContent(Texture2D t)
    {
        if (t == null) return;

        Color fillA = new Color(0.080f, 0.084f, 0.062f, 1f);
        Color fillB = new Color(0.118f, 0.106f, 0.072f, 1f);
        ClearRectWithNoise(t,
            Mathf.RoundToInt(t.width * 0.28f),
            Mathf.RoundToInt(t.height * 0.12f),
            Mathf.RoundToInt(t.width * 0.92f),
            Mathf.RoundToInt(t.height * 0.78f),
            fillA, fillB);
    }

    static void ClearRectWithNoise(Texture2D t, int left, int bottom, int right, int top, Color fillA, Color fillB)
    {
        if (t == null) return;

        left = Mathf.Clamp(left, 0, t.width - 1);
        right = Mathf.Clamp(right, left + 1, t.width);
        bottom = Mathf.Clamp(bottom, 0, t.height - 1);
        top = Mathf.Clamp(top, bottom + 1, t.height);

        for (int y = bottom; y < top; y++)
        {
            float v = Mathf.InverseLerp(bottom, top - 1, y);
            for (int x = left; x < right; x++)
            {
                float u = Mathf.InverseLerp(left, right - 1, x);
                Color c = Color.Lerp(fillA, fillB, 0.22f + v * 0.30f + u * 0.08f);
                float grain = (Mathf.PerlinNoise(x * 0.060f, y * 0.060f) - 0.5f) * 0.030f;
                c.r += grain;
                c.g += grain;
                c.b += grain;
                t.SetPixel(x, y, c);
            }
        }
    }

    static void PaintMatchModeStars(Texture2D t)
    {
        if (t == null) return;
        int cx = t.width / 2;
        int y = Mathf.Max(11, Mathf.RoundToInt(t.height * 0.085f));
        DrawFilledStar(t, new Vector2(cx - 14, y), 5.4f, new Color(0.92f, 0.78f, 0.36f, 1f));
        DrawFilledStar(t, new Vector2(cx,      y), 5.4f, new Color(0.98f, 0.86f, 0.43f, 1f));
        DrawFilledStar(t, new Vector2(cx + 14, y), 5.4f, new Color(0.92f, 0.78f, 0.36f, 1f));
    }

    static void DrawFilledStar(Texture2D t, Vector2 center, float outerR, Color fill)
    {
        Vector2[] outer = BuildStarPoints(center, outerR + 1.4f, (outerR + 1.4f) * 0.46f);
        Vector2[] inner = BuildStarPoints(center, outerR, outerR * 0.46f);
        int minX = Mathf.FloorToInt(center.x - outerR - 3f);
        int maxX = Mathf.CeilToInt(center.x + outerR + 3f);
        int minY = Mathf.FloorToInt(center.y - outerR - 3f);
        int maxY = Mathf.CeilToInt(center.y + outerR + 3f);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            if (x < 0 || x >= t.width || y < 0 || y >= t.height) continue;
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Color dst = t.GetPixel(x, y);
            if (PointInPoly(p, outer))
                dst = Color.Lerp(dst, new Color(0.05f, 0.035f, 0.015f, 1f), 0.70f);
            if (PointInPoly(p, inner))
            {
                float hi = Mathf.Clamp01(1f - Vector2.Distance(p, center + new Vector2(-1.4f, 1.8f)) / outerR);
                Color c = Color.Lerp(fill, Color.white, hi * 0.28f);
                dst = Color.Lerp(dst, c, 0.92f);
            }
            t.SetPixel(x, y, dst);
        }
    }

    static Vector2[] BuildStarPoints(Vector2 center, float outerR, float innerR)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = Mathf.Deg2Rad * (90f + i * 36f);
            float r = (i % 2 == 0) ? outerR : innerR;
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    static void ApplyExactPieceMask(Texture2D tex, ExactPieceMask mask)
    {
        if (tex == null || mask == ExactPieceMask.None) return;

        Vector2[] poly;
        if (mask == ExactPieceMask.Mode)
        {
            poly = new Vector2[] {
                new Vector2(9f, 4f), new Vector2(tex.width - 10f, 4f),
                new Vector2(tex.width - 3f, 12f), new Vector2(tex.width - 3f, tex.height - 14f),
                new Vector2(tex.width - 12f, tex.height - 5f), new Vector2(10f, tex.height - 5f),
                new Vector2(3f, tex.height - 13f), new Vector2(3f, 12f)
            };
        }
        else if (mask == ExactPieceMask.Panel)
        {
            // Panel pieces keep a much looser mask: enough to trim leaked desktop
            // background at the beveled corners, but not enough to eat the frame.
            poly = new Vector2[] {
                new Vector2(4f, 1.5f), new Vector2(tex.width - 5f, 1.5f),
                new Vector2(tex.width - 1.5f, 5f), new Vector2(tex.width - 1.5f, tex.height - 6f),
                new Vector2(tex.width - 6f, tex.height - 1.5f), new Vector2(5f, tex.height - 1.5f),
                new Vector2(1.5f, tex.height - 5f), new Vector2(1.5f, 5f)
            };
        }
        else
        {
            return;
        }

        for (int y = 0; y < tex.height; y++)
        {
            for (int x = 0; x < tex.width; x++)
            {
                int inside = 0;
                if (PointInPolygon(new Vector2(x + 0.25f, y + 0.25f), poly)) inside++;
                if (PointInPolygon(new Vector2(x + 0.75f, y + 0.25f), poly)) inside++;
                if (PointInPolygon(new Vector2(x + 0.25f, y + 0.75f), poly)) inside++;
                if (PointInPolygon(new Vector2(x + 0.75f, y + 0.75f), poly)) inside++;
                if (inside == 4) continue;

                var c = tex.GetPixel(x, y);
                c.a *= inside / 4f;
                tex.SetPixel(x, y, c);
            }
        }
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            bool crosses = ((poly[i].y > p.y) != (poly[j].y > p.y))
                && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x);
            if (crosses) inside = !inside;
        }
        return inside;
    }

    static Sprite ImportOrGenerateCustomModeThumb(string ap)
    {
        TryPullCustomModeThumbRefsFromDesktop(overwrite: false);
        var composed = TryBuildCustomModeThumbFromRefs(1024, 288);
        if (composed != null)
        {
            _customThumbFromRefs = true;
            Debug.Log("[LobbySceneBuilder] gen_thumb_custom from ref_thumb_custom_bg (+emblem if present)");
            return ImportGeneratedLobbySprite(ap + "/gen_thumb_custom.png", composed);
        }
        _customThumbFromRefs = false;
        Debug.Log("[LobbySceneBuilder] gen_thumb_custom: no ref_thumb_custom_bg.png found. Using generated sandbox strip.");
        return ImportOrGenerateSprite(ap + "/gen_thumb_custom.png", () => GenModeThumb(1024, 288, 1));
    }

    /// <summary>仅全屏底图：优先「无 UI 的桌面地图」；_ref_lobby_master 为整屏稿时易与面板叠重。</summary>
    static Sprite TryLoadBackgroundReference(string ap)
    {
        foreach (var rel in new[] {
            "/ref_lobby_desk_no_bottom_map.png",
            "/lobby_desk_only.png",
            "/_ref_lobby_desk_only.png",
            "/_ref_lobby_master.png",
            "/ref_lobby_master.png",
            "/lobby_background.png"
        })
        {
            var s = ImportReferenceSpriteIfPresent(ap + rel);
            if (s != null)
            {
                if (rel.IndexOf("desk_only", StringComparison.Ordinal) >= 0
                    || rel.IndexOf("no_bottom_map", StringComparison.Ordinal) >= 0)
                    Debug.Log("[LobbySceneBuilder] Using desk / map reference: " + ap + rel);
                else
                    Debug.Log("[LobbySceneBuilder] Using full reference as background (若与面板叠重请改用 lobby_desk_only.png): " + ap + rel);
                return s;
            }
        }
        return null;
    }

    /// <summary>若已放入参考全屏图（与菜单说明同名），优先使用以便与目标稿一致。</summary>
    static Sprite ImportReferenceSpriteIfPresent(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return null;
        string rel = assetPath.Substring(7).Replace('/', Path.DirectorySeparatorChar);
        string diskPath = Path.Combine(Application.dataPath, rel);
        if (!File.Exists(diskPath)) return null;

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (ti == null) return null;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.anisoLevel = 0;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.npotScale = TextureImporterNPOTScale.ToNearest;
        ApplyCrispSpritePlatformSettings(ti);
        ti.SaveAndReimport();
        AssetDatabase.Refresh();

        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (spr == null)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (o is Sprite sp) { spr = sp; break; }
            }
        }
        if (spr == null)
            Debug.LogWarning("[LobbySceneBuilder] Reference sprite import failed: " + assetPath);
        else
        {
            var t = spr.texture;
            int w = t != null ? t.width : 0, h = t != null ? t.height : 0;
            Debug.Log("[LobbySceneBuilder] Using reference background: " + assetPath + " (" + w + "x" + h + ")");
        }
        return spr;
    }

    /// <summary>避免 Android 默认 ETC2/ASTC 把大厅 UI 精灵压糊；体积由 LobbyGen 少量 PNG 承担。</summary>
    static void ApplyCrispSpritePlatformSettings(TextureImporter ti)
    {
        void One(string plat)
        {
            try
            {
                var st = ti.GetPlatformTextureSettings(plat);
                st.overridden = true;
                st.maxTextureSize = 4096;
                st.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
                st.textureCompression = TextureImporterCompression.Uncompressed;
                st.compressionQuality = 100;
                ti.SetPlatformTextureSettings(st);
            }
            catch { /* 部分平台名不存在则跳过 */ }
        }
        One("Android");
        One("Standalone");
    }

    /// <summary>无参考图时的程序底图：外圈深木色 + 中央大张泛黄地图纸（粗等高线、文书夹、左上模型坦克、弹壳、放大镜、铜罗盘）。</summary>
    static Texture2D GenWarRoomDesk()
    {
        int w = 1920, h = 1920;
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        Vector2 ctr = new Vector2(w * 0.5f, h * 0.5f);

        bool InRotRect(float px, float py, float rcx, float rcy, float rw, float rh, float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            float cx = px - rcx, cy = py - rcy;
            float rx = cx * Mathf.Cos(-rad) - cy * Mathf.Sin(-rad);
            float ry = cx * Mathf.Sin(-rad) + cy * Mathf.Cos(-rad);
            return Mathf.Abs(rx) < rw * 0.5f && Mathf.Abs(ry) < rh * 0.5f;
        }

        bool InTankTopDown(float px, float py)
        {
            float tcx = w * 0.13f, tcy = h * 0.8f;
            float sc = w * 0.095f;
            float ux = (px - tcx) / (sc * 0.55f), uy = (py - tcy) / (sc * 0.38f);
            if (ux * ux + uy * uy < 1f) return true;
            float tx = (px - tcx - sc * 0.35f) / (sc * 0.28f), ty = (py - tcy) / (sc * 0.28f);
            if (tx * tx + ty * ty < 1f) return true;
            float gx = px - tcx - sc * 0.62f, gy = py - tcy;
            return Mathf.Abs(gy) < sc * 0.07f && gx > -sc * 0.15f && gx < sc * 0.55f;
        }

        bool InBullet(float px, float py, float bx, float by)
        {
            float dx = (px - bx) / (w * 0.02f), dy = (py - by) / (w * 0.06f);
            return dx * dx + dy * dy < 1f;
        }

        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)w;
                float dist = Vector2.Distance(new Vector2(x, y), ctr) / (w * 0.48f);
                float woodRing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - 0.72f) / 0.38f));
                float wv = Mathf.PerlinNoise(fx * 6f, fy * 28f);
                Color wood = Color.Lerp(new Color(0.055f, 0.045f, 0.035f), new Color(0.15f, 0.11f, 0.08f), wv * 0.65f + 0.2f);

                float nPaper = Mathf.PerlinNoise(fx * 9f + 1.7f, fy * 9f + 0.9f);
                Color paper = Color.Lerp(new Color(0.62f, 0.55f, 0.4f), new Color(0.82f, 0.74f, 0.58f), nPaper);
                float topo = Mathf.PerlinNoise(fx * 11.5f + 4f, fy * 11.5f + 2f);
                float contour = Mathf.Abs(topo * 9f - Mathf.Floor(topo * 9f + 0.5f)) < 0.07f ? 0.65f : 0f;
                paper = Color.Lerp(paper, new Color(0.36f, 0.4f, 0.28f), contour);
                float fold = Mathf.Exp(-Mathf.Abs((x - y) * 0.0009f - 0.06f) * 40f) * 0.1f;
                paper *= 1f - fold;
                float centerLift = Mathf.Clamp01(1f - dist * 0.85f);
                paper = Color.Lerp(paper * 0.88f, paper, centerLift);

                Color col = Color.Lerp(paper, wood, woodRing);

                if (InRotRect(x, y, w * 0.76f, h * 0.2f, w * 0.24f, h * 0.11f, -14f))
                {
                    float line = ((x + y / 2) % 6 == 0) ? 0.1f : 0f;
                    Color bill = Color.Lerp(new Color(0.55f, 0.48f, 0.36f), new Color(0.68f, 0.6f, 0.46f), Mathf.PerlinNoise(fx * 35f, fy * 35f));
                    col = Color.Lerp(col, bill * (1f - line), 0.68f);
                }
                if (InRotRect(x, y, w * 0.22f, h * 0.3f, w * 0.15f, h * 0.12f, 10f))
                {
                    float line = (y % 4 == 0) ? 0.07f : 0f;
                    col = Color.Lerp(col, new Color(0.5f, 0.45f, 0.36f) * (1f - line), 0.5f);
                }

                if (InTankTopDown(x, y))
                {
                    float ux = (x - w * 0.13f) / (w * 0.095f), uy = (y - h * 0.8f) / (w * 0.095f);
                    float hi = Mathf.Clamp01(0.35f - uy * 0.5f);
                    Color tnk = Color.Lerp(new Color(0.1f, 0.16f, 0.07f), new Color(0.22f, 0.32f, 0.12f), hi);
                    col = Color.Lerp(col, tnk, 0.93f);
                }

                if (InBullet(x, y, w * 0.9f, h * 0.68f) || InBullet(x, y, w * 0.86f, h * 0.22f))
                    col = Color.Lerp(col, new Color(0.48f, 0.4f, 0.18f), 0.9f);

                Vector2 comp = new Vector2(w * 0.24f, h * 0.28f);
                float dc = Vector2.Distance(new Vector2(x, y), comp);
                if (dc > w * 0.075f && dc < w * 0.095f)
                    col = Color.Lerp(col, new Color(0.5f, 0.42f, 0.2f), 0.85f);
                if (dc < w * 0.065f)
                    col = Color.Lerp(col, new Color(0.2f, 0.18f, 0.14f), 0.5f);

                Vector2 mag = new Vector2(w * 0.84f, h * 0.76f);
                float dm = Vector2.Distance(new Vector2(x, y), mag);
                if (dm > w * 0.058f && dm < w * 0.078f)
                    col = Color.Lerp(col, new Color(0.42f, 0.38f, 0.24f), 0.75f);
                if (dm < w * 0.052f)
                    col = Color.Lerp(col, new Color(0.25f, 0.35f, 0.45f), 0.25f);

                float vign = Mathf.Clamp01(1.2f - Vector2.Distance(new Vector2(x, y), ctr) / (w * 0.55f));
                col *= Mathf.Lerp(0.5f, 1f, vign);
                t.SetPixel(x, y, col);
            }
        }
        t.Apply();
        return t;
    }

    /// <summary>圆形军官头像占位：大檐帽、帽饰金线、领章、五官剪影（非照片）。</summary>
    static Texture2D GenAvatarFace(int variant)
    {
        int s = 128;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float hue = variant * 0.04f;
        Color capMain = Color.Lerp(new Color(0.16f, 0.18f, 0.11f), new Color(0.22f, 0.24f, 0.14f), hue);
        Color capBand = new Color(0.55f, 0.45f, 0.12f);
        Color skin = Color.Lerp(new Color(0.58f, 0.44f, 0.34f), new Color(0.72f, 0.55f, 0.42f), hue + 0.2f);
        Color collarGold = new Color(0.72f, 0.58f, 0.18f);
        Color uni = new Color(0.2f, 0.22f, 0.28f);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c.x) / (s * 0.5f), dy = (y - c.y) / (s * 0.5f);
            float r = dx * dx + dy * dy;
            if (r > 1f) { t.SetPixel(x, y, Color.clear); continue; }

            Color col = skin;
            bool inCap = dy > 0.02f && Mathf.Abs(dx) < 0.92f - dy * 0.35f;
            if (inCap && dy > 0.18f)
                col = Color.Lerp(capMain, capMain * 1.15f, Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.2f);
            else if (inCap && dy > 0.12f && dy <= 0.22f && Mathf.Abs(dx) < 0.75f)
                col = Color.Lerp(col, capBand, 0.85f);
            else if (inCap && dy > 0.12f)
                col = Color.Lerp(col, capMain, 0.9f);

            if (dy < 0.08f && dy > -0.42f && Mathf.Abs(dx) < 0.38f)
                col = Color.Lerp(col, skin, 0.95f);

            if (dy > -0.18f && dy < -0.02f && Mathf.Abs(dx) > 0.42f && Mathf.Abs(dx) < 0.72f)
            {
                float ep = Mathf.Abs(Mathf.Abs(dx) - 0.58f);
                if (ep < 0.12f)
                    col = Color.Lerp(col, collarGold, 0.75f);
            }
            if (dy < -0.05f && dy > -0.38f && Mathf.Abs(dx) < 0.45f)
                col = Color.Lerp(col, uni, 0.35f);

            if (dy < 0.05f && Mathf.Abs(dx) < 0.38f)
            {
                float eyeL = Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.14f, -0.02f));
                float eyeR = Vector2.Distance(new Vector2(dx, dy), new Vector2(0.14f, -0.02f));
                if (eyeL < 0.055f || eyeR < 0.055f)
                    col = Color.Lerp(col, new Color(0.06f, 0.06f, 0.07f), 0.82f);
            }
            if (dy < -0.12f && dy > -0.22f && Mathf.Abs(dx) < 0.1f)
                col = Color.Lerp(col, new Color(0.28f, 0.2f, 0.18f), 0.4f);

            float shade = Mathf.Clamp01(0.35f - dy) * 0.12f;
            col *= 1f - shade;
            float edgeA = 1f;
            if (r > 0.82f)
                edgeA = Mathf.Clamp01((1f - r) / 0.18f);
            t.SetPixel(x, y, new Color(col.r, col.g, col.b, edgeA));
        }
        t.Apply();
        return t;
    }

    /// <summary>模式卡条图：匹配(陆地地图纹)/自定义(沙盘地图+路线+罗盘)/争霸(坦克编队+扛旗)。</summary>
    static Texture2D GenModeThumb(int w, int h, int variant)
    {
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);

        bool InEll(float px, float py, float cx, float cy, float rx, float ry)
        {
            if (rx < 1e-3f || ry < 1e-3f) return false;
            float dx = (px - cx) / rx, dy = (py - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        bool InTopTank(float px, float py, float cx, float cy, float sc, float yawDeg)
        {
            float rad = yawDeg * Mathf.Deg2Rad;
            float lx = px - cx, ly = py - cy;
            float rx = lx * Mathf.Cos(-rad) - ly * Mathf.Sin(-rad);
            float ry = lx * Mathf.Sin(-rad) + ly * Mathf.Cos(-rad);
            float hx = sc * 0.95f, hy = sc * 0.38f;
            if (rx * rx / (hx * hx) + ry * ry / (hy * hy) <= 1f) return true;
            float tx = rx - sc * 0.42f, ty = ry;
            float tr = sc * 0.32f;
            if (tx * tx + ty * ty <= tr * tr) return true;
            float bx = rx - sc * 0.85f, by = ry;
            return Mathf.Abs(by) < sc * 0.08f && bx >= -sc * 0.15f && bx <= sc * 0.55f;
        }

        void StampTank(ref Color col, float px, float py, float cx, float cy, float sc, float yaw, Color dark, float blend)
        {
            if (InTopTank(px, py, cx, cy, sc, yaw))
                col = Color.Lerp(col, dark, blend);
        }

        bool PointInTri(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            float s = (a.y - c.y) * (b.x - c.x) - (a.x - c.x) * (b.y - c.y);
            if (Mathf.Abs(s) < 1e-5f) return false;
            float u = ((a.y - c.y) * (px - c.x) - (a.x - c.x) * (py - c.y)) / s;
            float v = ((c.y - a.y) * (px - c.x) - (c.x - a.x) * (py - c.y)) / s;
            float w0 = 1f - u - v;
            return u >= 0f && v >= 0f && w0 >= 0f;
        }

        void StampSoldier(ref Color col, float px, float py, float cx, float cy, float sc)
        {
            if (InEll(px, py, cx, cy - sc * 0.55f, sc * 0.22f, sc * 0.24f))
                col = Color.Lerp(col, new Color(0.07f, 0.08f, 0.06f), 0.88f);
            if (Mathf.Abs(px - cx) < sc * 0.18f && py > cy - sc * 0.35f && py < cy + sc * 0.5f)
                col = Color.Lerp(col, new Color(0.09f, 0.1f, 0.08f), 0.82f);
        }

        for (int y = 0; y < h; y++)
        {
            float fy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                float fx = x / (float)w;
                float px = x + 0.5f, py = y + 0.5f;

                Color skyHi = variant == 1 ? new Color(0.23f, 0.30f, 0.26f) : new Color(0.66f, 0.43f, 0.19f);
                Color skyLo = variant == 1 ? new Color(0.07f, 0.12f, 0.13f) : new Color(0.23f, 0.12f, 0.07f);
                Color groundNear = variant == 1 ? new Color(0.17f, 0.20f, 0.15f) : new Color(0.42f, 0.30f, 0.15f);
                Color groundFar = variant == 1 ? new Color(0.08f, 0.12f, 0.10f) : new Color(0.20f, 0.14f, 0.08f);
                float horizon = variant == 1 ? 0.48f : 0.42f;
                Color col;
                if (fy > horizon)
                {
                    float k = (fy - horizon) / (1f - horizon + 1e-4f);
                    col = Color.Lerp(groundFar, groundNear, k);
                    col = Color.Lerp(col, col * (0.85f + Mathf.PerlinNoise(fx * 14f, fy * 8f) * 0.2f), 0.35f);
                }
                else
                {
                    float k = fy / (horizon + 1e-4f);
                    col = Color.Lerp(skyLo, skyHi, k);
                    float cloud = Mathf.PerlinNoise(fx * 5f + variant, fy * 4f);
                    Color dust = variant == 1 ? new Color(0.24f, 0.31f, 0.25f) : new Color(0.72f, 0.50f, 0.24f);
                    col = Color.Lerp(col, dust, cloud * 0.13f * (1f - k));
                }

                float smoke = Mathf.PerlinNoise(fx * 9f + 2f, fy * 11f + 1f);
                col = Color.Lerp(col, variant == 1 ? new Color(0.12f, 0.16f, 0.13f) : new Color(0.36f, 0.24f, 0.12f), smoke * 0.16f);

                Color tankBody = variant == 1 ? new Color(0.05f, 0.09f, 0.07f) : new Color(0.11f, 0.08f, 0.04f);
                Color tankHi = variant == 1 ? new Color(0.13f, 0.20f, 0.14f) : new Color(0.23f, 0.16f, 0.07f);

                if (variant == 0)
                {
                    StampTank(ref col, px, py, w * 0.62f, h * 0.62f, w * 0.14f, 35f, tankBody, 0.9f);
                    StampTank(ref col, px, py, w * 0.48f, h * 0.7f, w * 0.1f, -15f, tankBody, 0.88f);
                    StampTank(ref col, px, py, w * 0.78f, h * 0.58f, w * 0.09f, 110f, tankBody, 0.86f);
                    StampTank(ref col, px, py, w * 0.35f, h * 0.66f, w * 0.075f, 0f, tankHi, 0.82f);
                    if (fy < horizon - 0.02f && fx > 0.55f && fx < 0.92f && fy > 0.08f && fy < 0.28f)
                    {
                        float wing = Mathf.Abs((fx - 0.72f) * h + (fy - 0.18f) * w * 0.35f);
                        if (wing < w * 0.045f)
                            col = Color.Lerp(col, new Color(0.12f, 0.13f, 0.15f), 0.55f);
                    }
                    if (fy > horizon && fx > 0.62f)
                    {
                        float m0 = Mathf.PerlinNoise(fx * 18f + 1f, fy * 18f + 2f);
                        float c0 = Mathf.Abs(m0 * 10f - Mathf.Floor(m0 * 10f + 0.5f)) < 0.05f ? 1f : 0f;
                        col = Color.Lerp(col, new Color(0.2f, 0.22f, 0.16f), c0 * 0.22f);
                    }
                }
                else if (variant == 1)
                {
                    int g = Mathf.Max(14, w / 40);
                    if ((x % g == 0) || (y % g == 0))
                        col = Color.Lerp(col, new Color(0.56f, 0.70f, 0.48f), 0.22f);
                    float contour = Mathf.Abs(Mathf.PerlinNoise(fx * 12f, fy * 12f) * 8f - Mathf.Floor(Mathf.PerlinNoise(fx * 12f, fy * 12f) * 8f + 0.5f)) < 0.06f ? 1f : 0f;
                    col = Color.Lerp(col, new Color(0.42f, 0.55f, 0.35f), contour * 0.26f);
                    float m2 = Mathf.PerlinNoise(fx * 24f + 5f, fy * 24f + 3f);
                    float c2 = Mathf.Abs(m2 * 14f - Mathf.Floor(m2 * 14f + 0.5f)) < 0.05f ? 1f : 0f;
                    col = Color.Lerp(col, new Color(0.32f, 0.40f, 0.25f), c2 * 0.36f);
                    float route = Mathf.Abs(Mathf.Sin(fx * 11f + fy * 6.5f) - 0.35f);
                    if (route < 0.045f && fx > 0.08f && fx < 0.88f && fy > horizon)
                        col = Color.Lerp(col, new Color(0.64f, 0.52f, 0.24f), 0.38f);
                    float lat = Mathf.Abs(Mathf.Sin(fy * 38f)) < 0.04f ? 1f : 0f;
                    col = Color.Lerp(col, new Color(0.28f, 0.27f, 0.2f), lat * 0.12f * (fy > horizon ? 0.6f : 0.25f));
                    float cxC = w * 0.11f, cyC = h * 0.78f, rOut = w * 0.085f, rIn = w * 0.062f;
                    float dC = Vector2.Distance(new Vector2(px, py), new Vector2(cxC, cyC));
                    if (dC < rOut && dC > rIn && fy > 0.58f)
                        col = Color.Lerp(col, new Color(0.32f, 0.3f, 0.22f), 0.55f);
                    if (dC < rIn * 0.35f && fy > 0.72f && fy < 0.86f && Mathf.Abs(px - cxC) < w * 0.02f)
                        col = Color.Lerp(col, new Color(0.38f, 0.36f, 0.28f), 0.7f);
                    StampTank(ref col, px, py, w * 0.35f, h * 0.55f, w * 0.07f, 20f, tankBody, 0.85f);
                    StampTank(ref col, px, py, w * 0.52f, h * 0.62f, w * 0.065f, -40f, tankBody, 0.83f);
                    StampTank(ref col, px, py, w * 0.7f, h * 0.52f, w * 0.06f, 75f, tankHi, 0.8f);
                    StampTank(ref col, px, py, w * 0.58f, h * 0.72f, w * 0.055f, 5f, tankBody, 0.78f);
                }
                else
                {
                    float crater = Mathf.PerlinNoise(fx * 18f + 4f, fy * 14f + 8f);
                    if (fy > horizon && crater > 0.63f)
                        col = Color.Lerp(col, new Color(0.13f, 0.08f, 0.035f), 0.18f);
                    StampTank(ref col, px, py, w * 0.22f, h * 0.68f, w * 0.065f, 12f, tankBody, 0.88f);
                    StampTank(ref col, px, py, w * 0.36f, h * 0.72f, w * 0.06f, -8f, tankBody, 0.86f);
                    StampTank(ref col, px, py, w * 0.5f, h * 0.7f, w * 0.07f, 22f, tankHi, 0.87f);
                    StampTank(ref col, px, py, w * 0.64f, h * 0.68f, w * 0.065f, -18f, tankBody, 0.85f);
                    StampTank(ref col, px, py, w * 0.78f, h * 0.65f, w * 0.07f, 45f, tankBody, 0.84f);
                    float poleX = w * 0.84f;
                    if (Mathf.Abs(px - poleX) < w * 0.007f && py > h * 0.24f && py < h * 0.64f)
                        col = Color.Lerp(col, new Color(0.1f, 0.09f, 0.07f), 0.92f);
                    Vector2 f0 = new Vector2(poleX, h * 0.27f), f1 = new Vector2(poleX - w * 0.2f, h * 0.35f), f2 = new Vector2(poleX, h * 0.44f);
                    if (PointInTri(px, py, f0, f1, f2))
                        col = Color.Lerp(col, new Color(0.62f, 0.1f, 0.08f), 0.78f);
                    Vector2 f3 = new Vector2(poleX, h * 0.44f), f4 = new Vector2(poleX - w * 0.17f, h * 0.5f), f5 = new Vector2(poleX, h * 0.56f);
                    if (PointInTri(px, py, f3, f4, f5))
                        col = Color.Lerp(col, new Color(0.45f, 0.08f, 0.06f), 0.65f);
                    StampSoldier(ref col, px, py, poleX - w * 0.045f, h * 0.7f, w * 0.05f);
                    StampSoldier(ref col, px, py, poleX + w * 0.018f, h * 0.695f, w * 0.048f);
                }

                float vign = Mathf.Clamp01(1.1f - Vector2.Distance(new Vector2(fx, fy), new Vector2(0.5f, 0.5f)) * 0.9f);
                col *= Mathf.Lerp(0.75f, 1f, vign);
                t.SetPixel(x, y, col);
            }
        }
        t.Apply();
        return t;
    }

    static Texture2D GenCardFade()
    {
        int w = 8, h = 256;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float k = y / (float)(h - 1);
            float a = Mathf.Lerp(0f, 0.52f, k * k);
            var c = new Color(0, 0, 0, a);
            for (int x = 0; x < w; x++) t.SetPixel(x, y, c);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenMetalNoise()
    {
        int s = 512;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float nx = x / (float)s, ny = y / (float)s;
            float sweep = nx * 0.06f + ny * 0.04f;
            float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            float v = 0.38f + n * 0.08f + sweep;
            var baseC = new Color(v * 0.52f, v * 0.55f, v * 0.62f, 1f);
            t.SetPixel(x, y, baseC);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenFramedPlate(int w, int h, Color fillDark, Color fillLight, Color edgeLight,
        Color edgeShadow, Color accent, bool bolts, float cornerCut = 0f)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2[] rivets = {
            new Vector2(18f, 18f), new Vector2(w - 19f, 18f),
            new Vector2(18f, h - 19f), new Vector2(w - 19f, h - 19f)
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float fx = x / Mathf.Max(1f, w - 1f);
            float fy = y / Mathf.Max(1f, h - 1f);
            float n0 = Mathf.PerlinNoise(fx * 7.5f + 0.7f, fy * 9.5f + 1.1f);
            float n1 = Mathf.PerlinNoise(fx * 18f + 3.1f, fy * 18f + 2.7f);
            Color col = Color.Lerp(fillDark, fillLight, Mathf.Clamp01(0.2f + n0 * 0.55f - fy * 0.12f));
            col = Color.Lerp(col, col * 1.08f, n1 * 0.08f);

            int dx = Mathf.Min(x, w - 1 - x);
            int dy = Mathf.Min(y, h - 1 - y);
            int edge = Mathf.Min(dx, dy);
            if (edge <= 1)
                col = edgeLight;
            else if (edge <= 3)
                col = Color.Lerp(edgeLight, fillLight, edge / 3f);
            else if (edge >= 14)
                col = Color.Lerp(col, edgeShadow, Mathf.Clamp01((edge - 14f) / 18f) * 0.18f);

            if (dx <= 4 || dy <= 4)
            {
                float shadow = ((x > w * 0.48f) || (y > h * 0.52f)) ? 0.26f : 0.08f;
                col = Color.Lerp(col, edgeShadow, shadow);
            }
            if (dx >= 10 && dx <= 13 && dy >= 10 && dy <= 13)
                col = Color.Lerp(col, accent, 0.22f);

            if (cornerCut > 0f)
            {
                bool cutTL = x + y < cornerCut;
                bool cutTR = (w - 1 - x) + y < cornerCut;
                bool cutBL = x + (h - 1 - y) < cornerCut;
                bool cutBR = (w - 1 - x) + (h - 1 - y) < cornerCut;
                if (cutTL || cutTR || cutBL || cutBR)
                {
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    continue;
                }
            }

            if (bolts)
            {
                for (int i = 0; i < rivets.Length; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), rivets[i]);
                    if (d < 5.5f)
                    {
                        float k = 1f - d / 5.5f;
                        Color bolt = Color.Lerp(new Color(0.8f, 0.73f, 0.52f), new Color(0.34f, 0.28f, 0.14f), d / 5.5f);
                        col = Color.Lerp(col, bolt, k * 0.95f);
                    }
                }
            }

            if (y >= 7 && y <= 10)
                col = Color.Lerp(col, accent, 0.18f);
            t.SetPixel(x, y, col);
        }

        t.Apply();
        return t;
    }

    static Texture2D GenPanelFrame()
    {
        return GenFramedPlate(
            160, 160,
            new Color(0.075f, 0.085f, 0.055f, 1f),
            new Color(0.18f, 0.17f, 0.10f, 1f),
            new Color(0.82f, 0.74f, 0.48f, 1f),
            new Color(0.025f, 0.024f, 0.018f, 1f),
            new Color(0.54f, 0.45f, 0.22f, 1f),
            bolts: true,
            cornerCut: 16f);
    }

    static Texture2D GenCardFrame()
    {
        return GenFramedPlate(
            256, 128,
            new Color(0.085f, 0.085f, 0.055f, 1f),
            new Color(0.22f, 0.20f, 0.12f, 1f),
            new Color(0.9f, 0.78f, 0.48f, 1f),
            new Color(0.025f, 0.022f, 0.016f, 1f),
            new Color(0.66f, 0.52f, 0.22f, 1f),
            bolts: true,
            cornerCut: 18f);
    }

    static Texture2D GenModeCardFrame()
    {
        const int w = 560;
        const int h = 156;
        var t = GenFramedPlate(
            w, h,
            new Color(0.090f, 0.088f, 0.050f, 1f),
            new Color(0.255f, 0.225f, 0.118f, 1f),
            new Color(0.92f, 0.82f, 0.54f, 1f),
            new Color(0.020f, 0.018f, 0.012f, 1f),
            new Color(0.66f, 0.54f, 0.24f, 1f),
            bolts: true,
            cornerCut: 20f);

        Color innerDark = new Color(0.06f, 0.065f, 0.042f, 1f);
        Color innerWarm = new Color(0.18f, 0.17f, 0.09f, 1f);
        Color bevelHi = new Color(0.86f, 0.76f, 0.46f, 1f);
        Color bevelLo = new Color(0.12f, 0.10f, 0.05f, 1f);
        int innerL = 18, innerR = w - 19, innerB = 17, innerT = h - 18;

        for (int y = innerB; y <= innerT; y++)
        for (int x = innerL; x <= innerR; x++)
        {
            int dx = Mathf.Min(x - innerL, innerR - x);
            int dy = Mathf.Min(y - innerB, innerT - y);
            int edge = Mathf.Min(dx, dy);
            float fx = (x - innerL) / Mathf.Max(1f, innerR - innerL);
            float fy = (y - innerB) / Mathf.Max(1f, innerT - innerB);
            float grain = Mathf.PerlinNoise(fx * 8.5f + 0.3f, fy * 10.5f + 0.9f);
            float dust = Mathf.PerlinNoise(fx * 21f + 1.6f, fy * 17f + 2.8f);

            Color col = Color.Lerp(innerDark, innerWarm, 0.28f + grain * 0.34f);
            col = Color.Lerp(col, new Color(0.33f, 0.30f, 0.16f, 1f), dust * 0.10f);

            if (edge <= 1)
                col = bevelHi;
            else if (edge <= 4)
                col = Color.Lerp(bevelHi, innerWarm, edge / 4f);
            else if (edge >= 8 && edge <= 12)
                col = Color.Lerp(col, bevelLo, 0.32f);

            if (y < innerB + 16)
                col = Color.Lerp(col, new Color(0.40f, 0.34f, 0.16f, 1f), 0.10f);
            if (y > innerT - 18)
                col = Color.Lerp(col, new Color(0.03f, 0.03f, 0.02f, 1f), 0.22f);

            t.SetPixel(x, y, col);
        }

        for (int y = 14; y < h - 14; y++)
        for (int x = 14; x < w - 14; x++)
        {
            Color col = t.GetPixel(x, y);
            if (col.a <= 0.01f) continue;

            bool inTlCut = x + y < 42;
            bool inTrCut = (w - 1 - x) + y < 42;
            bool inBlCut = x + (h - 1 - y) < 42;
            bool inBrCut = (w - 1 - x) + (h - 1 - y) < 42;
            if (inTlCut || inTrCut || inBlCut || inBrCut)
            {
                float glow = (x < w * 0.5f ? 0.18f : 0.12f);
                col = Color.Lerp(col, new Color(0.80f, 0.72f, 0.42f, 1f), glow);
                t.SetPixel(x, y, col);
            }
        }

        int notchW = 94;
        int notchH = 10;
        int notchY = 0;
        int notchL = w / 2 - notchW / 2;
        int notchR = notchL + notchW;
        for (int y = notchY; y < notchH; y++)
        for (int x = notchL; x <= notchR; x++)
        {
            if (x - notchL < 18 - y * 2 || notchR - x < 18 - y * 2)
                continue;
            t.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
        }

        t.Apply();
        return t;
    }

    static Texture2D GenFriendRowFrame()
    {
        var t = GenFramedPlate(
            256, 96,
            new Color(0.08f, 0.085f, 0.065f, 1f),
            new Color(0.18f, 0.16f, 0.11f, 1f),
            new Color(0.65f, 0.58f, 0.38f, 1f),
            new Color(0.03f, 0.025f, 0.02f, 1f),
            new Color(0.42f, 0.36f, 0.18f, 1f),
            bolts: false,
            cornerCut: 12f);
        for (int y = 12; y < 84; y++)
        for (int x = 10; x < 18; x++)
        {
            float g = Mathf.InverseLerp(10f, 18f, x);
            t.SetPixel(x, y, Color.Lerp(new Color(0.45f, 0.38f, 0.2f), new Color(0.2f, 0.18f, 0.08f), g));
        }
        t.Apply();
        return t;
    }

    static Texture2D GenMissionRowFrame()
    {
        const int w = 256;
        const int h = 72;
        var t = GenFramedPlate(
            w, h,
            new Color(0.035f, 0.042f, 0.028f, 0.78f),
            new Color(0.115f, 0.105f, 0.062f, 0.84f),
            new Color(0.78f, 0.68f, 0.36f, 0.82f),
            new Color(0.014f, 0.014f, 0.011f, 0.74f),
            new Color(0.55f, 0.42f, 0.16f, 0.78f),
            bolts: false,
            cornerCut: 14f);

        Color leftHot = new Color(0.84f, 0.66f, 0.22f, 0.80f);
        Color leftDark = new Color(0.22f, 0.16f, 0.06f, 0.70f);
        Color scan = new Color(0.94f, 0.82f, 0.42f, 0.24f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Color col = t.GetPixel(x, y);
            if (col.a <= 0.01f)
                continue;

            float fx = x / (float)(w - 1);
            float fy = y / (float)(h - 1);
            float centerFade = Mathf.Clamp01(1f - Mathf.Abs(fy - 0.52f) * 2.4f);
            col = Color.Lerp(col, new Color(0.02f, 0.025f, 0.018f, col.a), 0.20f + centerFade * 0.20f);

            if (x >= 13 && x <= 23 && y >= 10 && y <= h - 11)
            {
                float g = Mathf.InverseLerp(13f, 23f, x);
                col = Color.Lerp(leftHot, leftDark, g);
            }
            if (x >= 28 && x <= 31 && y >= 12 && y <= h - 13)
                col = Color.Lerp(col, new Color(0.78f, 0.58f, 0.18f, col.a), 0.52f);

            float diag = Mathf.Abs((x - 34f) - (y - 10f) * 2.1f);
            if (diag < 1.2f && fx < 0.42f)
                col = Color.Lerp(col, scan, 0.38f);
            if (y == h - 13 && x > 34 && x < w - 18)
                col = Color.Lerp(col, new Color(0.78f, 0.62f, 0.28f, col.a), 0.30f);

            col.a *= Mathf.Lerp(0.72f, 0.88f, centerFade);
            t.SetPixel(x, y, col);
        }

        t.Apply();
        return t;
    }

    static Texture2D GenButtonFrame(bool warm)
    {
        if (warm)
            return GenBrightGoldMetalButtonFrame();

        Color fillDark = warm ? new Color(0.28f, 0.18f, 0.055f, 1f) : new Color(0.075f, 0.085f, 0.065f, 1f);
        Color fillLight = warm ? new Color(0.58f, 0.38f, 0.095f, 1f) : new Color(0.18f, 0.18f, 0.13f, 1f);
        Color edgeLight = warm ? new Color(0.96f, 0.82f, 0.46f, 1f) : new Color(0.74f, 0.68f, 0.45f, 1f);
        Color accent = warm ? new Color(0.78f, 0.58f, 0.17f, 1f) : new Color(0.47f, 0.40f, 0.22f, 1f);
        return GenFramedPlate(160, 64, fillDark, fillLight, edgeLight,
            new Color(0.03f, 0.02f, 0.015f, 1f), accent, bolts: false, cornerCut: 10f);
    }

    static Texture2D GenBrightGoldMetalButtonFrame()
    {
        int w = 160, h = 64;
        const int scale = 8;
        int sw = w * scale, sh = h * scale;
        var hi = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color outerShadow = new Color(0.085f, 0.040f, 0.010f, 1f);
        Color rimDark = new Color(0.16f, 0.080f, 0.018f, 1f);
        Color rimMid = new Color(0.66f, 0.43f, 0.125f, 1f);
        Color innerDark = new Color(0.34f, 0.205f, 0.060f, 1f);
        Color innerGold = new Color(0.74f, 0.46f, 0.115f, 1f);
        Color glint = new Color(1.00f, 0.89f, 0.500f, 1f);
        Color lowerWarm = new Color(0.36f, 0.180f, 0.038f, 1f);
        Color lineDark = new Color(0.055f, 0.034f, 0.012f, 1f);

        float cut = 10f * scale;
        Vector2 topLeft = new Vector2(cut, 2f * scale);
        Vector2 topRight = new Vector2(sw - cut - 1f, 2f * scale);
        Vector2 rightTop = new Vector2(sw - 2f * scale, cut);
        Vector2 rightBottom = new Vector2(sw - 2f * scale, sh - cut - 1f);
        Vector2 bottomRight = new Vector2(sw - cut - 1f, sh - 2f * scale);
        Vector2 bottomLeft = new Vector2(cut, sh - 2f * scale);
        Vector2 leftBottom = new Vector2(2f * scale, sh - cut - 1f);
        Vector2 leftTop = new Vector2(2f * scale, cut);
        Vector2[] outer = { topLeft, topRight, rightTop, rightBottom, bottomRight, bottomLeft, leftBottom, leftTop };

        for (int y = 0; y < sh; y++)
        for (int x = 0; x < sw; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            if (!PointInPoly(p, outer))
            {
                hi.SetPixel(x, y, Color.clear);
                continue;
            }

            float fx = x / (float)(sw - 1);
            float fy = y / (float)(sh - 1);
            float centerX = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.5f) * 2f);
            float centerY = Mathf.Clamp01(1f - Mathf.Abs(fy - 0.52f) * 2f);
            float faceLift = Mathf.Pow(centerX * centerY, 0.55f);
            Color face = Color.Lerp(innerDark, innerGold, Mathf.Clamp01(0.28f + faceLift * 0.48f));

            float faceSheen = Mathf.Clamp01(1f - Mathf.Abs(fy - 0.39f) * 5.8f) * Mathf.Pow(centerX, 0.58f);
            face = Color.Lerp(face, glint, faceSheen * 0.08f);
            float lowerFace = Mathf.Clamp01((fy - 0.66f) / 0.26f);
            face = Color.Lerp(face, lowerWarm, lowerFace * 0.10f);

            float dTop = DistanceToSegment(p, topLeft, topRight);
            float dTopLeft = DistanceToSegment(p, leftTop, topLeft);
            float dTopRight = DistanceToSegment(p, topRight, rightTop);
            float dBottom = DistanceToSegment(p, bottomLeft, bottomRight);
            float dBottomLeft = DistanceToSegment(p, leftBottom, bottomLeft);
            float dBottomRight = DistanceToSegment(p, bottomRight, rightBottom);
            float dLeft = DistanceToSegment(p, leftTop, leftBottom);
            float dRight = DistanceToSegment(p, rightTop, rightBottom);
            float dEdge = Mathf.Min(Mathf.Min(Mathf.Min(dTop, dBottom), Mathf.Min(dLeft, dRight)),
                Mathf.Min(Mathf.Min(dTopLeft, dTopRight), Mathf.Min(dBottomLeft, dBottomRight)));
            float rimMask = Mathf.Clamp01(1f - dEdge / (5.2f * scale));
            float rimPeak = Mathf.Clamp01(1f - Mathf.Abs(dEdge - 2.2f * scale) / (2.0f * scale));
            float innerLip = Mathf.Clamp01(1f - Mathf.Abs(dEdge - 5.1f * scale) / (0.70f * scale));
            float outerLip = Mathf.Clamp01(1f - dEdge / (0.90f * scale));
            float topLeftLight = Mathf.Clamp01((0.62f - fy) / 0.62f) * 0.78f
                + Mathf.Clamp01((0.36f - fx) / 0.36f) * 0.22f;
            float lowerRightShade = Mathf.Clamp01((fy - 0.42f) / 0.58f) * 0.72f
                + Mathf.Clamp01((fx - 0.60f) / 0.40f) * 0.28f;

            Color rim = Color.Lerp(rimDark, rimMid, Mathf.Clamp01(0.22f + rimPeak * 0.68f));
            rim = Color.Lerp(rim, glint, Mathf.Clamp01(topLeftLight) * (rimPeak * 0.36f + rimMask * 0.08f));
            rim = Color.Lerp(rim, outerShadow, Mathf.Clamp01(lowerRightShade) * (rimPeak * 0.22f + rimMask * 0.16f));

            Color col = Color.Lerp(face, rim, rimMask);
            if (innerLip > 0f)
                col = Color.Lerp(col, fy < 0.52f ? glint : outerShadow, innerLip * (fy < 0.52f ? 0.14f : 0.20f));
            if (outerLip > 0f)
                col = Color.Lerp(col, lineDark, outerLip * 0.42f);

            Vector2[] bolts = {
                new Vector2(10f * scale, 10f * scale),
                new Vector2(sw - 11f * scale, 10f * scale),
                new Vector2(10f * scale, sh - 11f * scale),
                new Vector2(sw - 11f * scale, sh - 11f * scale)
            };
            for (int i = 0; i < bolts.Length; i++)
            {
                float bd = Vector2.Distance(p, bolts[i]);
                if (bd < 2.2f * scale)
                {
                    float k = Mathf.Clamp01(1f - bd / (2.2f * scale));
                    col = Color.Lerp(col, bd < 0.85f * scale ? lineDark : glint, k * 0.45f);
                }
            }

            col.a = 1f;
            hi.SetPixel(x, y, col);
        }

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sumA = 0f;
            float sumR = 0f;
            float sumG = 0f;
            float sumB = 0f;
            for (int yy = 0; yy < scale; yy++)
            for (int xx = 0; xx < scale; xx++)
            {
                Color sample = hi.GetPixel(x * scale + xx, y * scale + yy);
                sumA += sample.a;
                sumR += sample.r * sample.a;
                sumG += sample.g * sample.a;
                sumB += sample.b * sample.a;
            }

            float alpha = sumA / (scale * scale);
            Color px = Color.clear;
            if (alpha > 0.015f)
            {
                px = new Color(sumR / sumA, sumG / sumA, sumB / sumA, alpha);
            }
            t.SetPixel(x, y, px);
        }

        UnityEngine.Object.DestroyImmediate(hi);
        t.Apply();
        return t;
    }

    static Texture2D GenTechMetalButtonFrame()
    {
        int w = 260, h = 72;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color outerBlack = new Color(0.010f, 0.011f, 0.009f, 1f);
        Color bevelDark = new Color(0.085f, 0.078f, 0.050f, 1f);
        Color bevelMid = new Color(0.34f, 0.305f, 0.165f, 1f);
        Color bevelGold = new Color(0.86f, 0.735f, 0.405f, 1f);
        Color innerDark = new Color(0.045f, 0.052f, 0.035f, 1f);
        Color innerOlive = new Color(0.175f, 0.185f, 0.105f, 1f);
        Color grime = new Color(0.018f, 0.017f, 0.013f, 1f);

        Vector2[] plate = {
            new Vector2(18, 2), new Vector2(w - 19, 2), new Vector2(w - 3, 18),
            new Vector2(w - 3, h - 16), new Vector2(w - 18, h - 3),
            new Vector2(17, h - 3), new Vector2(2, h - 17), new Vector2(2, 18)
        };
        Vector2[] inner = {
            new Vector2(32, 15), new Vector2(w - 33, 15), new Vector2(w - 17, 28),
            new Vector2(w - 17, h - 26), new Vector2(w - 32, h - 15),
            new Vector2(31, h - 15), new Vector2(16, h - 27), new Vector2(16, 28)
        };
        Vector2[] rivets = {
            new Vector2(23, 21), new Vector2(w - 24, 21),
            new Vector2(23, h - 22), new Vector2(w - 24, h - 22)
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2(x, y);
            if (!PointInPoly(p, plate))
            {
                t.SetPixel(x, y, Color.clear);
                continue;
            }

            float fx = x / (float)(w - 1);
            float fy = y / (float)(h - 1);
            float n0 = Mathf.PerlinNoise(fx * 12.0f + 4.1f, fy * 7.0f + 1.8f);
            float n1 = Mathf.PerlinNoise(fx * 54.0f + 2.7f, fy * 34.0f + 8.9f);
            float topLight = Mathf.Clamp01(1f - fy * 1.18f);
            Color col = Color.Lerp(bevelDark, bevelMid, Mathf.Clamp01(0.28f + n0 * 0.36f + topLight * 0.30f));
            col = Color.Lerp(col, grime, n1 * 0.12f);

            float dx = Mathf.Min(x, w - 1 - x);
            float dy = Mathf.Min(y, h - 1 - y);
            float edge = Mathf.Min(dx, dy);
            if (edge < 2.0f)
                col = outerBlack;
            else if (edge < 4.0f)
                col = Color.Lerp(bevelGold, outerBlack, fy > 0.58f ? 0.42f : 0.12f);
            else if (edge < 8.0f)
                col = Color.Lerp(col, bevelGold, Mathf.Lerp(0.28f, 0.62f, 1f - fy));
            else if (edge < 13.0f)
                col = Color.Lerp(col, outerBlack, 0.30f);

            bool upperChamfer = NearSegment(p, new Vector2(23, 8), new Vector2(w - 25, 8), 1.35f);
            bool lowerChamfer = NearSegment(p, new Vector2(30, h - 9), new Vector2(w - 32, h - 9), 1.15f);
            bool leftCut = NearSegment(p, new Vector2(8, 23), new Vector2(23, 8), 1.25f)
                || NearSegment(p, new Vector2(8, h - 24), new Vector2(23, h - 9), 1.25f);
            bool rightCut = NearSegment(p, new Vector2(w - 9, 23), new Vector2(w - 24, 8), 1.25f)
                || NearSegment(p, new Vector2(w - 9, h - 24), new Vector2(w - 24, h - 9), 1.25f);
            if (upperChamfer || lowerChamfer || leftCut || rightCut)
                col = Color.Lerp(col, bevelGold, upperChamfer ? 0.82f : 0.55f);

            if (PointInPoly(p, inner))
            {
                float centerBulge = Mathf.Clamp01(1f - Mathf.Abs(fy - 0.46f) * 2.1f);
                float sideFalloff = Mathf.Clamp01(1f - Mathf.Abs(fx - 0.5f) * 1.30f);
                col = Color.Lerp(innerDark, innerOlive, Mathf.Clamp01(0.20f + centerBulge * 0.28f + sideFalloff * 0.14f + n0 * 0.10f));
                col = Color.Lerp(col, grime, n1 * 0.16f);
                if (NearSegment(p, new Vector2(28, 17), new Vector2(w - 30, 17), 1.1f))
                    col = Color.Lerp(col, bevelGold, 0.38f);
                if (NearSegment(p, new Vector2(28, h - 17), new Vector2(w - 30, h - 17), 1.1f))
                    col = Color.Lerp(col, outerBlack, 0.40f);
            }

            if (y == 11 && x > 38 && x < w - 38)
                col = Color.Lerp(col, new Color(1f, 0.92f, 0.62f, 1f), 0.62f);
            if (y == h - 12 && x > 42 && x < w - 42)
                col = Color.Lerp(col, outerBlack, 0.58f);
            if (x > w * 0.62f && x < w * 0.64f && y > 11 && y < h - 11)
                col = Color.Lerp(col, Color.black, 0.18f);
            if (x > 0 && y > 0 && ((x + y) % 37 == 0))
                col = Color.Lerp(col, Color.black, 0.09f);

            for (int i = 0; i < rivets.Length; i++)
            {
                float d = Vector2.Distance(p, rivets[i]);
                if (d < 5.2f)
                {
                    float k = 1f - d / 5.2f;
                    Color rivet = Color.Lerp(new Color(0.18f, 0.16f, 0.09f, 1f), bevelGold, k);
                    col = Color.Lerp(col, rivet, 0.90f);
                    if (d < 2.0f)
                        col = Color.Lerp(col, outerBlack, 0.20f);
                }
            }

            t.SetPixel(x, y, col);
        }

        t.Apply();
        return t;
    }

    static Texture2D GenNavTabFrame(bool active)
    {
        Color fillDark = active ? new Color(0.24f, 0.18f, 0.075f, 1f) : new Color(0.065f, 0.075f, 0.055f, 1f);
        Color fillLight = active ? new Color(0.45f, 0.33f, 0.13f, 1f) : new Color(0.16f, 0.16f, 0.10f, 1f);
        Color edgeLight = active ? new Color(0.93f, 0.80f, 0.46f, 1f) : new Color(0.66f, 0.60f, 0.39f, 1f);
        Color accent = active ? new Color(0.82f, 0.64f, 0.22f, 1f) : new Color(0.38f, 0.33f, 0.18f, 1f);
        return GenFramedPlate(180, 86, fillDark, fillLight, edgeLight,
            new Color(0.03f, 0.025f, 0.02f, 1f), accent, bolts: false, cornerCut: 14f);
    }

    static Texture2D GenRedDot()
    {
        int s = 32;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float r = s * 0.38f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            float a = d < r ? 1f : Mathf.Clamp01(1f - (d - r) / 4f);
            t.SetPixel(x, y, new Color(0.85f, 0.12f, 0.1f, a));
        }
        t.Apply();
        return t;
    }

    static Texture2D GenPortraitRing()
    {
        int s = 128;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float r0 = s * 0.34f, r1 = s * 0.46f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c);
            Color col = Color.clear;
            if (d >= r0 && d < r1)
            {
                float u = (d - r0) / (r1 - r0);
                col = Color.Lerp(new Color(0.62f, 0.48f, 0.14f, 1f), new Color(0.32f, 0.26f, 0.12f, 1f), u);
            }
            else if (d >= r1 && d < r1 + 6f)
                col = new Color(0.12f, 0.1f, 0.08f, 0.55f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static bool PointInPoly(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y + 1e-6f) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    static bool NearSegment(Vector2 p, Vector2 a, Vector2 b, float width)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f)
            return Vector2.Distance(p, a) <= width;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t) <= width;
    }

    static Texture2D GenIconStarGold()
    {
        int s = 96;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float Ro = s * 0.38f, Ri = s * 0.16f;
        var v = new Vector2[10];
        for (int k = 0; k < 5; k++)
        {
            float a = (-90f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Ro;
            float a2 = (-90f + 36f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2 + 1] = c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * Ri;
        }
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            bool inS = PointInPoly(p, v);
            float d = Vector2.Distance(p, c);
            Color col = Color.clear;
            if (inS)
            {
                float edge = Mathf.Clamp01(1f - Mathf.Abs(d - Ro * 0.55f) / 8f);
                col = Color.Lerp(new Color(1f, 0.82f, 0.25f, 1f), new Color(0.75f, 0.52f, 0.08f, 1f), 1f - edge * 0.4f);
            }
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconGem()
    {
        int s = 160;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);

        Vector2 left = new Vector2(10, 86);
        Vector2 topLeft = new Vector2(34, 119);
        Vector2 crownLeft = new Vector2(61, 128);
        Vector2 crownRight = new Vector2(99, 128);
        Vector2 topRight = new Vector2(126, 119);
        Vector2 right = new Vector2(150, 86);
        Vector2 bottom = new Vector2(80, 13);
        Vector2 beltLeft = new Vector2(49, 86);
        Vector2 beltRight = new Vector2(111, 86);
        Vector2 crownCenter = new Vector2(80, 111);

        Vector2[] outline = { left, topLeft, crownLeft, crownRight, topRight, right, bottom };
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Color col = Color.clear;
            Vector2 p = new Vector2(x, y);

            if (PointInPoly(p, outline))
            {
                float v = Mathf.Clamp01(y / (float)s);
                float centerGlow = Mathf.Exp(-Mathf.Pow((x - 80f) / 34f, 2f) - Mathf.Pow((y - 62f) / 44f, 2f));
                col = Color.Lerp(new Color(0.04f, 0.20f, 0.34f, 1f), new Color(0.36f, 0.82f, 1f, 1f), centerGlow * 0.72f);
                col = Color.Lerp(col, new Color(0.02f, 0.11f, 0.20f, 1f), Mathf.Abs(x - 80f) / 112f);
                col = Color.Lerp(col, new Color(0.82f, 0.96f, 1f, 1f), Mathf.Clamp01((v - 0.56f) * 1.8f));

                if (PointInPoly(p, new[] { topLeft, crownLeft, crownCenter, beltLeft }))
                    col = Color.Lerp(new Color(0.24f, 0.70f, 0.94f, 1f), new Color(0.83f, 0.96f, 1f, 1f), Mathf.Clamp01((x - 30f) / 54f));
                if (PointInPoly(p, new[] { crownLeft, crownRight, beltRight, beltLeft, crownCenter }))
                    col = Color.Lerp(new Color(0.78f, 0.96f, 1f, 1f), Color.white, centerGlow * 0.45f);
                if (PointInPoly(p, new[] { crownCenter, crownRight, topRight, beltRight }))
                    col = Color.Lerp(new Color(0.67f, 0.91f, 1f, 1f), new Color(0.13f, 0.45f, 0.72f, 1f), Mathf.Clamp01((x - 88f) / 54f));
                if (PointInPoly(p, new[] { left, topLeft, beltLeft }))
                    col = Color.Lerp(new Color(0.05f, 0.25f, 0.42f, 1f), new Color(0.46f, 0.86f, 1f, 1f), Mathf.Clamp01((x - 10f) / 45f));
                if (PointInPoly(p, new[] { beltRight, topRight, right }))
                    col = Color.Lerp(new Color(0.45f, 0.83f, 1f, 1f), new Color(0.02f, 0.12f, 0.23f, 1f), Mathf.Clamp01((x - 111f) / 38f));
                if (PointInPoly(p, new[] { left, beltLeft, bottom }))
                    col = Color.Lerp(new Color(0.02f, 0.12f, 0.23f, 1f), new Color(0.18f, 0.58f, 0.82f, 1f), Mathf.Clamp01((x + y - 58f) / 88f));
                if (PointInPoly(p, new[] { beltLeft, beltRight, bottom }))
                    col = Color.Lerp(new Color(0.11f, 0.43f, 0.68f, 1f), new Color(0.74f, 0.94f, 1f, 1f), centerGlow * 0.62f);
                if (PointInPoly(p, new[] { beltRight, right, bottom }))
                    col = Color.Lerp(new Color(0.18f, 0.54f, 0.78f, 1f), new Color(0.02f, 0.12f, 0.24f, 1f), Mathf.Clamp01((x - 96f) / 52f));

                float grain = Mathf.PerlinNoise(x * 0.06f, y * 0.07f) * 0.06f;
                col = Color.Lerp(col, Color.white, grain);
            }

            bool line =
                NearSegment(p, left, topLeft, 1.25f) || NearSegment(p, topLeft, crownLeft, 1.25f) ||
                NearSegment(p, crownLeft, crownRight, 1.25f) || NearSegment(p, crownRight, topRight, 1.25f) ||
                NearSegment(p, topRight, right, 1.25f) || NearSegment(p, right, bottom, 1.35f) ||
                NearSegment(p, bottom, left, 1.35f) || NearSegment(p, left, right, 1.15f) ||
                NearSegment(p, topLeft, beltLeft, 1.05f) || NearSegment(p, crownLeft, beltLeft, 1.05f) ||
                NearSegment(p, crownLeft, crownCenter, 1.05f) || NearSegment(p, crownRight, crownCenter, 1.05f) ||
                NearSegment(p, crownRight, beltRight, 1.05f) || NearSegment(p, topRight, beltRight, 1.05f) ||
                NearSegment(p, beltLeft, bottom, 1.15f) || NearSegment(p, beltRight, bottom, 1.15f);
            if (line && PointInPoly(p, outline))
                col = Color.Lerp(col, Color.white, 0.62f);

            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconGear()
    {
        int s = 160;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        Color dark = new Color(0.16f, 0.14f, 0.08f, 1f);
        Color mid = new Color(0.46f, 0.41f, 0.24f, 1f);
        Color bright = new Color(0.82f, 0.76f, 0.50f, 1f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 d = new Vector2(x, y) - c;
            float r = d.magnitude;
            float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            while (deg < 0) deg += 360f;
            Color col = Color.clear;

            float nearestTooth = 999f;
            for (int k = 0; k < 8; k++)
                nearestTooth = Mathf.Min(nearestTooth, Mathf.Abs(Mathf.DeltaAngle(deg, k * 45f)));

            bool tooth = nearestTooth < 11.5f && r > 52f && r < 75f;
            bool outerRing = r > 43f && r < 59f;
            bool hub = r > 21f && r < 37f;
            bool spoke = nearestTooth < 4.6f && r >= 34f && r <= 47f;
            bool metal = tooth || outerRing || hub || spoke;

            if (metal)
            {
                float light = Mathf.Clamp01((d.x * -0.45f + d.y * 0.70f) / 82f + 0.50f);
                float radial = Mathf.Clamp01(1f - Mathf.Abs(r - 47f) / 44f);
                float grain = Mathf.PerlinNoise(x * 0.10f, y * 0.12f) * 0.10f;
                col = Color.Lerp(dark, bright, light * 0.58f + radial * 0.24f + grain);

                float edge =
                    Mathf.Min(
                        Mathf.Min(Mathf.Abs(r - 21f), Mathf.Abs(r - 37f)),
                        Mathf.Min(Mathf.Abs(r - 43f), Mathf.Abs(r - 59f)));
                if (tooth) edge = Mathf.Min(edge, Mathf.Min(Mathf.Abs(r - 52f), Mathf.Abs(r - 75f)));
                if (edge < 2.0f)
                    col = Color.Lerp(col, dark, 0.58f);
                if (edge > 2.0f && edge < 3.7f)
                    col = Color.Lerp(col, bright, 0.18f);
            }

            bool outline = !metal && r > 18f && r < 78f
                && (Mathf.Abs(r - 21f) < 1.8f || Mathf.Abs(r - 37f) < 1.8f
                    || Mathf.Abs(r - 43f) < 1.7f || Mathf.Abs(r - 59f) < 1.9f
                    || (nearestTooth < 12.6f && Mathf.Abs(r - 75f) < 2.0f));
            if (outline)
                col = new Color(0.05f, 0.045f, 0.025f, 0.74f);

            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconBell()
    {
        int s = 160;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.54f);
        Color dark = new Color(0.18f, 0.15f, 0.08f, 1f);
        Color mid = new Color(0.55f, 0.48f, 0.24f, 1f);
        Color bright = new Color(0.96f, 0.88f, 0.54f, 1f);
        Color rim = new Color(0.78f, 0.70f, 0.42f, 1f);

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;

            float arcD = Mathf.Abs(Vector2.Distance(p, c) - s * 0.47f);
            bool sideArc = arcD < 4.7f && y > s * 0.30f && y < s * 0.90f && Mathf.Abs(x - c.x) > s * 0.32f;
            if (sideArc)
            {
                float shine = Mathf.Clamp01((y - s * 0.34f) / (s * 0.45f));
                col = Color.Lerp(dark, rim, 0.52f + shine * 0.26f);
            }

            float loopOuter = Mathf.Pow((x - c.x) / (s * 0.10f), 2f) + Mathf.Pow((y - s * 0.86f) / (s * 0.15f), 2f);
            float loopInner = Mathf.Pow((x - c.x) / (s * 0.055f), 2f) + Mathf.Pow((y - s * 0.86f) / (s * 0.085f), 2f);
            if (loopOuter < 1f && loopInner > 1f && y > s * 0.78f)
                col = Color.Lerp(dark, bright, 0.55f + Mathf.Clamp01((x - c.x) / (s * 0.18f)) * 0.18f);

            float bodyTop = s * 0.83f - Mathf.Pow((x - c.x) / (s * 0.34f), 2f) * s * 0.11f;
            float bodyT = Mathf.Clamp01((bodyTop - y) / (s * 0.47f));
            float halfW = Mathf.Lerp(s * 0.22f, s * 0.39f, Mathf.Pow(bodyT, 0.78f));
            bool body = y > s * 0.31f && y < bodyTop && Mathf.Abs(x - c.x) < halfW;
            if (body)
            {
                float centerGlow = Mathf.Exp(-Mathf.Pow((x - s * 0.45f) / (s * 0.18f), 2f) - Mathf.Pow((y - s * 0.68f) / (s * 0.24f), 2f));
                float sideShade = Mathf.Abs(x - c.x) / Mathf.Max(1f, halfW);
                float grain = Mathf.PerlinNoise(x * 0.16f, y * 0.19f) * 0.08f;
                col = Color.Lerp(mid, bright, centerGlow * 0.72f + grain);
                col = Color.Lerp(col, dark, Mathf.Pow(sideShade, 2.2f) * 0.55f);
            }

            float rimOuter = Mathf.Pow((x - c.x) / (s * 0.44f), 2f) + Mathf.Pow((y - s * 0.31f) / (s * 0.095f), 2f);
            float rimInner = Mathf.Pow((x - c.x) / (s * 0.36f), 2f) + Mathf.Pow((y - s * 0.34f) / (s * 0.055f), 2f);
            if (rimOuter < 1f && (rimInner > 1f || y < s * 0.32f))
            {
                float hi = Mathf.Exp(-Mathf.Pow((x - s * 0.43f) / (s * 0.22f), 2f));
                col = Color.Lerp(dark, bright, 0.38f + hi * 0.45f);
            }

            float clapper = Mathf.Pow((x - c.x) / (s * 0.11f), 2f) + Mathf.Pow((y - s * 0.20f) / (s * 0.10f), 2f);
            if (clapper < 1f && y < s * 0.31f)
                col = Color.Lerp(dark, bright, 0.30f + Mathf.Clamp01(1f - clapper) * 0.45f);

            if (body && Mathf.Abs(y - s * 0.49f) < 1.3f)
                col = Color.Lerp(col, dark, 0.45f);
            if (body && Mathf.Abs(Mathf.Abs(x - c.x) - halfW) < 1.6f)
                col = Color.Lerp(col, dark, 0.72f);
            if (col.a > 0f && (x < 3 || x > s - 4 || y < 3 || y > s - 4))
                col.a = 0f;

            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconHelmet()
    {
        int s = 96;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.48f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 d = new Vector2(x, y) - c;
            float nx = d.x / (s * 0.35f);
            float ny = d.y / (s * 0.32f);
            Color col = Color.clear;
            if (nx * nx + ny * ny < 1f && ny > -0.25f)
                col = new Color(0.62f, 0.6f, 0.55f, 1f);
            if (Mathf.Abs(nx) < 0.85f && ny > 0.15f && ny < 0.55f)
                col = new Color(0.45f, 0.44f, 0.4f, 1f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconClipboard()
    {
        int s = 64;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Color col = Color.clear;
            if (x > s * 0.2f && x < s * 0.82f && y > s * 0.12f && y < s * 0.88f)
                col = new Color(0.72f, 0.7f, 0.62f, 1f);
            if (x > s * 0.26f && x < s * 0.76f && y > s * 0.22f && y < s * 0.8f)
                col = new Color(0.35f, 0.36f, 0.38f, 1f);
            if (x > s * 0.3f && x < s * 0.72f && y > s * 0.28f && y < s * 0.42f)
                col = new Color(0.55f, 0.57f, 0.6f, 1f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconMatch()
    {
        int s = 160;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float Ro = s * 0.32f, Ri = s * 0.12f;
        var v = new Vector2[10];
        for (int k = 0; k < 5; k++)
        {
            float a = (-90f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Ro;
            float a2 = (-90f + 36f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2 + 1] = c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * Ri;
        }
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            float d = Vector2.Distance(p, c);
            Color col = Color.clear;
            if (d > s * 0.38f && d < s * 0.44f)
                col = new Color(0.92f, 0.82f, 0.35f, 0.95f);
            if (PointInPoly(p, v))
                col = Color.Lerp(col, new Color(1f, 0.9f, 0.35f, 1f), 0.95f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconCustom()
    {
        int s = 160;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.52f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;
            if (x > s * 0.14f && x < s * 0.86f && y > s * 0.16f && y < s * 0.84f)
                col = new Color(0.62f, 0.55f, 0.4f, 0.92f);
            int g = s / 10;
            if (x % g == 0 || y % g == 0) col = Color.Lerp(col, new Color(0.45f, 0.4f, 0.3f, 1f), 0.35f);
            if (x > s * 0.58f && y < s * 0.42f && (x - s * 0.58f) + (y - s * 0.16f) < s * 0.32f)
                col = Color.Lerp(col, new Color(0.35f, 0.3f, 0.22f, 0.95f), 0.55f);
            float d = Vector2.Distance(p, c + new Vector2(s * 0.08f, -s * 0.06f));
            if (d < s * 0.06f) col = new Color(0.25f, 0.22f, 0.18f, 0.95f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconWar()
    {
        int s = 176;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        float Ro = s * 0.22f, Ri = s * 0.09f;
        var v = new Vector2[10];
        for (int k = 0; k < 5; k++)
        {
            float a = (-90f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Ro;
            float a2 = (-90f + 36f + k * 72f) * Mathf.Deg2Rad;
            v[k * 2 + 1] = c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * Ri;
        }
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;
            float lx = (x - c.x) / (s * 0.42f);
            float ly = (y - c.y) / (s * 0.52f);
            float e = lx * lx + ly * ly;
            if (e > 0.55f && e < 0.95f && Mathf.Abs(lx) > 0.12f)
                col = new Color(0.55f, 0.48f, 0.22f, 0.9f);
            if (PointInPoly(p, v))
                col = Color.Lerp(col, new Color(1f, 0.88f, 0.32f, 1f), 0.95f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconTank()
    {
        int s = 128;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.52f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Color col = Color.clear;
            if (x > s * 0.18f && x < s * 0.82f && y > s * 0.48f && y < s * 0.72f)
                col = new Color(0.42f, 0.45f, 0.38f, 0.95f);
            if (x > s * 0.12f && x < s * 0.88f && y > s * 0.58f && y < s * 0.7f)
                col = new Color(0.35f, 0.38f, 0.32f, 0.95f);
            if (Vector2.Distance(new Vector2(x, y), c + new Vector2(0, s * 0.02f)) < s * 0.14f)
                col = new Color(0.48f, 0.5f, 0.42f, 0.95f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconTaskCrate()
    {
        int s = 144;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;
            Vector2 a = new Vector2(30, 57), b = new Vector2(64, 36), c = new Vector2(111, 54), d = new Vector2(75, 78);
            Vector2 e = new Vector2(30, 57), f = new Vector2(75, 78), g = new Vector2(75, 118), h = new Vector2(31, 95);
            Vector2 i = new Vector2(75, 78), j = new Vector2(111, 54), k = new Vector2(111, 93), l = new Vector2(75, 118);
            bool top = PointInPoly(p, new [] { a, b, c, d });
            bool front = PointInPoly(p, new [] { e, f, g, h });
            bool side = PointInPoly(p, new [] { i, j, k, l });
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f;
            if (top) col = Color.Lerp(new Color(0.70f, 0.66f, 0.48f, 1f), new Color(0.98f, 0.90f, 0.62f, 1f), 0.35f + n);
            if (front) col = Color.Lerp(new Color(0.26f, 0.25f, 0.17f, 1f), new Color(0.54f, 0.50f, 0.34f, 1f), 0.35f + n);
            if (side) col = Color.Lerp(new Color(0.18f, 0.18f, 0.12f, 1f), new Color(0.42f, 0.39f, 0.26f, 1f), 0.28f + n);
            if (NearSegment(p, a, b, 1.5f) || NearSegment(p, b, c, 1.5f) || NearSegment(p, c, k, 1.5f)
                || NearSegment(p, k, l, 1.5f) || NearSegment(p, l, h, 1.5f) || NearSegment(p, h, e, 1.5f)
                || NearSegment(p, d, f, 1.2f) || NearSegment(p, f, g, 1.2f))
                col = new Color(0.08f, 0.075f, 0.05f, 1f);
            if (front && (NearSegment(p, Vector2.Lerp(e, f, 0.52f), Vector2.Lerp(h, g, 0.52f), 1f)
                || NearSegment(p, Vector2.Lerp(e, h, 0.5f), Vector2.Lerp(f, g, 0.5f), 1f)))
                col = Color.Lerp(col, new Color(0.98f, 0.88f, 0.52f, 1f), 0.40f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenIconTechBlueprint()
    {
        int s = 144;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;
            if (x > 18 && x < 126 && y > 18 && y < 126)
            {
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.08f;
                col = Color.Lerp(new Color(0.10f, 0.21f, 0.24f, 1f), new Color(0.22f, 0.37f, 0.40f, 1f), 0.25f + n);
                if (x % 16 == 0 || y % 16 == 0)
                    col = Color.Lerp(col, new Color(0.56f, 0.72f, 0.70f, 1f), 0.18f);
                if (x < 22 || x > 122 || y < 22 || y > 122)
                    col = new Color(0.50f, 0.58f, 0.48f, 1f);
            }
            if (NearSegment(p, new Vector2(36, 99), new Vector2(105, 42), 5.8f)
                || NearSegment(p, new Vector2(42, 42), new Vector2(108, 104), 5.3f))
                col = new Color(0.72f, 0.68f, 0.52f, 1f);
            if (NearSegment(p, new Vector2(36, 99), new Vector2(105, 42), 2.0f)
                || NearSegment(p, new Vector2(42, 42), new Vector2(108, 104), 1.8f))
                col = new Color(0.18f, 0.17f, 0.12f, 1f);
            if (Vector2.Distance(p, new Vector2(102, 39)) < 17f && Vector2.Distance(p, new Vector2(102, 39)) > 9f)
                col = new Color(0.82f, 0.78f, 0.58f, 1f);
            if (PointInPoly(p, new [] { new Vector2(96, 38), new Vector2(118, 24), new Vector2(122, 36), new Vector2(105, 51) }))
                col = new Color(0.11f, 0.10f, 0.075f, 1f);
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenNavIcon(int id)
    {
        int s = 144;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x, y) - c;
            Color col = Color.clear;
            float r = p.magnitude;
            if (id == 0)
            {
                Vector2 roofA = new Vector2(s * 0.20f, s * 0.42f);
                Vector2 roofB = new Vector2(s * 0.34f, s * 0.26f);
                Vector2 roofC = new Vector2(s * 0.75f, s * 0.28f);
                Vector2 roofD = new Vector2(s * 0.88f, s * 0.44f);
                if (PointInPoly(new Vector2(x, y), new [] { roofA, roofB, roofC, roofD }))
                    col = new Color(0.86f, 0.66f, 0.24f, 1f);
                if (x > s * 0.25f && x < s * 0.82f && y > s * 0.43f && y < s * 0.80f)
                    col = Color.Lerp(new Color(0.24f, 0.15f, 0.07f, 1f), new Color(0.66f, 0.42f, 0.18f, 1f), y / (float)s);
                if (x > s * 0.34f && x < s * 0.47f && y > s * 0.51f && y < s * 0.80f)
                    col = new Color(0.11f, 0.075f, 0.045f, 1f);
                if (x > s * 0.56f && x < s * 0.75f && y > s * 0.53f && y < s * 0.68f)
                    col = new Color(0.96f, 0.78f, 0.34f, 1f);
                if (r < s * 0.18f && p.x > -s * 0.02f && p.y < -s * 0.05f)
                {
                    float a = Mathf.Atan2(p.y, p.x);
                    float star = Mathf.Abs(Mathf.Sin(a * 2.5f + 1.57f));
                    if (star * r < s * 0.09f)
                        col = new Color(1f, 0.88f, 0.36f, 1f);
                }
                if (NearSegment(new Vector2(x, y), roofA, roofB, 1.2f) || NearSegment(new Vector2(x, y), roofB, roofC, 1.2f)
                    || NearSegment(new Vector2(x, y), roofC, roofD, 1.2f) || NearSegment(new Vector2(x, y), roofD, roofA, 1.2f))
                    col = new Color(0.09f, 0.055f, 0.025f, 1f);
            }
            else if (id == 1)
            {
                Vector2 topA = new Vector2(s * 0.28f, s * 0.36f);
                Vector2 topB = new Vector2(s * 0.49f, s * 0.24f);
                Vector2 topC = new Vector2(s * 0.78f, s * 0.38f);
                Vector2 topD = new Vector2(s * 0.57f, s * 0.52f);
                Vector2 frontA = topA;
                Vector2 frontB = topD;
                Vector2 frontC = new Vector2(s * 0.57f, s * 0.84f);
                Vector2 frontD = new Vector2(s * 0.29f, s * 0.68f);
                Vector2 sideA = topD;
                Vector2 sideB = topC;
                Vector2 sideC = new Vector2(s * 0.77f, s * 0.65f);
                Vector2 sideD = frontC;

                bool top = PointInPoly(new Vector2(x, y), new [] { topA, topB, topC, topD });
                bool front = PointInPoly(new Vector2(x, y), new [] { frontA, frontB, frontC, frontD });
                bool side = PointInPoly(new Vector2(x, y), new [] { sideA, sideB, sideC, sideD });
                float grain = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.08f;
                if (top)
                {
                    float shine = Mathf.InverseLerp(s * 0.78f, s * 0.23f, x) * 0.18f + grain;
                    col = Color.Lerp(new Color(0.62f, 0.52f, 0.30f, 1f), new Color(1f, 0.86f, 0.48f, 1f), shine + 0.45f);
                }
                if (front)
                {
                    float shade = Mathf.InverseLerp(s * 0.82f, s * 0.40f, y) * 0.22f + grain;
                    col = Color.Lerp(new Color(0.23f, 0.20f, 0.13f, 1f), new Color(0.72f, 0.60f, 0.34f, 1f), shade + 0.28f);
                }
                if (side)
                {
                    float shade = Mathf.InverseLerp(s * 0.77f, s * 0.52f, x) * 0.18f + grain;
                    col = Color.Lerp(new Color(0.18f, 0.16f, 0.11f, 1f), new Color(0.54f, 0.46f, 0.28f, 1f), shade + 0.20f);
                }

                bool edge =
                    NearSegment(new Vector2(x, y), topA, topB, 1.25f) || NearSegment(new Vector2(x, y), topB, topC, 1.25f) ||
                    NearSegment(new Vector2(x, y), topC, sideC, 1.25f) || NearSegment(new Vector2(x, y), sideC, frontC, 1.25f) ||
                    NearSegment(new Vector2(x, y), frontC, frontD, 1.25f) || NearSegment(new Vector2(x, y), frontD, frontA, 1.25f) ||
                    NearSegment(new Vector2(x, y), frontB, frontC, 1.1f) || NearSegment(new Vector2(x, y), frontB, sideB, 1.1f);
                bool seam =
                    NearSegment(new Vector2(x, y), Vector2.Lerp(topA, topB, 0.42f), Vector2.Lerp(topD, topC, 0.42f), 0.9f) ||
                    NearSegment(new Vector2(x, y), Vector2.Lerp(topA, topD, 0.45f), Vector2.Lerp(topB, topC, 0.45f), 0.9f) ||
                    NearSegment(new Vector2(x, y), Vector2.Lerp(frontA, frontD, 0.45f), Vector2.Lerp(frontB, frontC, 0.45f), 0.9f) ||
                    NearSegment(new Vector2(x, y), Vector2.Lerp(sideA, sideD, 0.45f), Vector2.Lerp(sideB, sideC, 0.45f), 0.9f);
                bool panelLine =
                    NearSegment(new Vector2(x, y), Vector2.Lerp(frontA, frontB, 0.33f), Vector2.Lerp(frontD, frontC, 0.33f), 0.75f) ||
                    NearSegment(new Vector2(x, y), Vector2.Lerp(frontA, frontB, 0.66f), Vector2.Lerp(frontD, frontC, 0.66f), 0.75f) ||
                    NearSegment(new Vector2(x, y), Vector2.Lerp(sideA, sideB, 0.46f), Vector2.Lerp(sideD, sideC, 0.46f), 0.75f);
                if (edge)
                    col = new Color(0.08f, 0.07f, 0.045f, 1f);
                else if (panelLine && col.a > 0f)
                    col = Color.Lerp(col, new Color(0.10f, 0.085f, 0.045f, 1f), 0.58f);
                else if (seam && col.a > 0f)
                    col = Color.Lerp(col, new Color(1f, 0.88f, 0.52f, 1f), 0.38f);

                Vector2 lockA = new Vector2(s * 0.35f, s * 0.59f);
                Vector2 lockB = new Vector2(s * 0.47f, s * 0.64f);
                Vector2 lockC = new Vector2(s * 0.47f, s * 0.74f);
                Vector2 lockD = new Vector2(s * 0.35f, s * 0.69f);
                if (PointInPoly(new Vector2(x, y), new [] { lockA, lockB, lockC, lockD }))
                    col = new Color(0.86f, 0.74f, 0.43f, 1f);
            }
            else if (id == 2)
            {
                if (x > s * 0.22f && x < s * 0.78f && y > s * 0.28f && y < s * 0.72f)
                    col = new Color(0.5f, 0.55f, 0.45f, 0.85f);
                if (x > s * 0.55f && x < s * 0.88f && y > s * 0.35f && y < s * 0.68f && (x - s * 0.55f) * 0.35f > (s * 0.68f - y))
                    col = new Color(0.38f, 0.42f, 0.36f, 0.9f);
                if (Mathf.Abs(x - s * 0.5f) < s * 0.04f && y > s * 0.18f && y < s * 0.35f)
                    col = new Color(0.35f, 0.38f, 0.32f, 1f);
            }
            else if (id == 3)
            {
                Vector2 pc = new Vector2(x, y);
                if (r > s * 0.33f && r < s * 0.40f && p.y > -s * 0.2f && p.y < s * 0.22f)
                    col = new Color(0.54f, 0.42f, 0.14f, 0.86f);
                if (Mathf.Abs(p.x) < s * 0.15f && p.y > -s * 0.04f && p.y < s * 0.30f)
                    col = new Color(0.92f, 0.75f, 0.26f, 1f);
                if (Mathf.Abs(p.x) < s * 0.25f && p.y > s * 0.20f && p.y < s * 0.34f)
                    col = new Color(0.78f, 0.59f, 0.18f, 1f);
                if (Mathf.Abs(p.x) > s * 0.18f && Mathf.Abs(p.x) < s * 0.30f && p.y > s * 0.22f && p.y < s * 0.40f)
                    col = new Color(0.68f, 0.50f, 0.14f, 1f);
                if (Vector2.Distance(pc, c + new Vector2(0, -s * 0.18f)) < s * 0.21f)
                    col = Color.Lerp(col.a > 0 ? col : Color.clear, new Color(1f, 0.84f, 0.30f, 1f), 0.95f);
                if (Vector2.Distance(pc, c + new Vector2(0, -s * 0.18f)) < s * 0.12f)
                    col = new Color(0.34f, 0.22f, 0.08f, 1f);
                float laurel = Mathf.Abs(r - s * 0.36f);
                if (laurel < 2.3f && p.y < s * 0.18f && Mathf.Abs(p.x) > s * 0.18f)
                    col = new Color(0.94f, 0.77f, 0.27f, 1f);
            }
            else
            {
                if (x > s * 0.22f && x < s * 0.78f && y > s * 0.32f && y < s * 0.62f)
                    col = new Color(0.82f, 0.8f, 0.76f, 0.95f);
                float tri = Mathf.Abs(x - c.x) * 0.55f + (y - s * 0.32f);
                if (tri < s * 0.22f && y > s * 0.28f && y < s * 0.72f)
                    col = new Color(0.55f, 0.52f, 0.48f, 0.95f);
            }
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenTaskCardFrame()
    {
        return GenMissionCardFrame(320, 250, taskStyle: true);
    }

    static Texture2D GenTechCardFrame()
    {
        return GenMissionCardFrame(320, 250, taskStyle: false);
    }

    static Texture2D GenMissionCardFrame(int w, int h, bool taskStyle)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color outerLight = new Color(0.72f, 0.68f, 0.48f, 1f);
        Color outerDark = new Color(0.04f, 0.045f, 0.032f, 1f);
        Color fillA = taskStyle ? new Color(0.14f, 0.14f, 0.08f, 1f) : new Color(0.13f, 0.13f, 0.09f, 1f);
        Color fillB = taskStyle ? new Color(0.30f, 0.29f, 0.16f, 1f) : new Color(0.26f, 0.25f, 0.15f, 1f);
        Color inner = new Color(0.04f, 0.055f, 0.035f, 1f);
        Color footer = new Color(0.24f, 0.24f, 0.12f, 1f);

        Vector2[] clip = {
            new Vector2(15, 8), new Vector2(w - 16, 8), new Vector2(w - 7, 18),
            new Vector2(w - 7, h - 18), new Vector2(w - 18, h - 8),
            new Vector2(16, h - 8), new Vector2(7, h - 18), new Vector2(7, 18)
        };
        Vector2[] topCut = {
            new Vector2(18, 14), new Vector2(w * 0.46f, 14), new Vector2(w * 0.57f, 75),
            new Vector2(18, 75)
        };
        Vector2[] topRight = {
            new Vector2(w * 0.47f, 14), new Vector2(w - 18, 14), new Vector2(w - 18, 75),
            new Vector2(w * 0.58f, 75)
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2(x, y);
            Color col = Color.clear;
            if (PointInPoly(p, clip))
            {
                float n = Mathf.PerlinNoise(x * 0.045f, y * 0.045f) * 0.10f;
                col = Color.Lerp(fillA, fillB, Mathf.Clamp01(y / (float)h * 0.35f + n));
                if (PointInPoly(p, topCut))
                    col = Color.Lerp(new Color(0.20f, 0.20f, 0.10f, 1f), new Color(0.34f, 0.33f, 0.17f, 1f), n + 0.25f);
                if (PointInPoly(p, topRight))
                    col = Color.Lerp(new Color(0.15f, 0.145f, 0.10f, 1f), new Color(0.30f, 0.29f, 0.20f, 1f), n + 0.20f);
                if (x > 14 && x < w - 14 && y > 82 && y < h - 58)
                    col = Color.Lerp(inner, new Color(0.08f, 0.09f, 0.06f, 1f), n);
                if (x > 16 && x < w - 16 && y >= h - 56 && y < h - 16)
                    col = Color.Lerp(footer, new Color(0.32f, 0.31f, 0.16f, 1f), n * 0.8f);
                if (y > 78 && y < 84)
                    col = new Color(0.02f, 0.024f, 0.018f, 1f);
                if (y > h - 60 && y < h - 55)
                    col = new Color(0.58f, 0.55f, 0.34f, 1f);
                if (NearSegment(p, new Vector2(18, 14), new Vector2(w * 0.46f, 14), 1.4f)
                    || NearSegment(p, new Vector2(w * 0.46f, 14), new Vector2(w * 0.57f, 75), 1.4f)
                    || NearSegment(p, new Vector2(w * 0.57f, 75), new Vector2(18, 75), 1.4f))
                    col = Color.Lerp(col, outerLight, 0.45f);
                if (x < 11 || x > w - 12 || y < 12 || y > h - 13)
                    col = Color.Lerp(col, (x < 5 || y < 5) ? outerLight : outerDark, 0.65f);
            }
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenProgressBar(bool fill)
    {
        int w = 160, h = 18;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = Mathf.Min(x, w - 1 - x);
            float dy = Mathf.Min(y, h - 1 - y);
            float round = Mathf.Min(dx, dy);
            Color col = Color.clear;
            if (round >= 1f)
            {
                if (fill)
                {
                    float k = y / (float)(h - 1);
                    col = Color.Lerp(new Color(0.54f, 0.67f, 0.32f, 1f), new Color(0.82f, 0.91f, 0.58f, 1f), 1f - k);
                    if (y < 4) col = Color.Lerp(col, Color.white, 0.22f);
                    if (x < 5 || x > w - 6) col = Color.Lerp(col, new Color(0.33f, 0.43f, 0.18f, 1f), 0.35f);
                }
                else
                {
                    col = Color.Lerp(new Color(0.015f, 0.02f, 0.014f, 1f), new Color(0.09f, 0.105f, 0.06f, 1f), y / (float)h);
                    if (x < 3 || x > w - 4 || y < 3 || y > h - 4)
                        col = new Color(0.46f, 0.48f, 0.30f, 1f);
                    if (x < 6 || x > w - 7 || y < 6 || y > h - 7)
                        col = Color.Lerp(col, new Color(0.02f, 0.025f, 0.016f, 1f), 0.62f);
                }
            }
            if (Vector2.Distance(new Vector2(x, y), c) > w * 0.58f)
                col.a *= 0.98f;
            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Texture2D GenWarehouseNavButtonFrame()
    {
        int w = 256, h = 112;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float cut = 20f;
            bool outer = x >= 4 && x < w - 4 && y >= 5 && y < h - 5
                && x + y > cut && (w - x) + y > cut && x + (h - y) > cut && (w - x) + (h - y) > cut;
            Color col = Color.clear;
            if (outer)
            {
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float noise = Mathf.PerlinNoise(x * 0.055f, y * 0.08f) * 0.08f;
                col = Color.Lerp(new Color(0.12f, 0.11f, 0.08f, 1f), new Color(0.72f, 0.68f, 0.54f, 1f), Mathf.Clamp01(edge / 18f + noise));
            }

            bool inner = x >= 19 && x < w - 19 && y >= 18 && y < h - 18
                && x + y > 54 && (w - x) + y > 54 && x + (h - y) > 54 && (w - x) + (h - y) > 54;
            if (inner)
            {
                float u = x / (float)(w - 1);
                float v = y / (float)(h - 1);
                float glow = Mathf.Exp(-Mathf.Pow((u - 0.58f) * 2.0f, 2f) - Mathf.Pow((v - 0.36f) * 2.2f, 2f)) * 0.28f;
                float grain = Mathf.PerlinNoise(x * 0.035f, y * 0.055f) * 0.06f;
                col = Color.Lerp(new Color(0.12f, 0.06f, 0.025f, 1f), new Color(0.46f, 0.29f, 0.16f, 1f), 0.46f + glow + grain);
            }

            bool brightRim = outer && !inner && (y < 12 || y > h - 13 || x < 12 || x > w - 13);
            if (brightRim)
                col = Color.Lerp(col, new Color(0.98f, 0.88f, 0.58f, 1f), 0.20f);

            Vector2 p = new Vector2(x, y);
            Vector2[] screws = {
                new Vector2(25, 24), new Vector2(w - 25, 24),
                new Vector2(25, h - 24), new Vector2(w - 25, h - 24)
            };
            foreach (var sc in screws)
            {
                float d = Vector2.Distance(p, sc);
                if (d < 6.5f)
                    col = Color.Lerp(new Color(0.05f, 0.045f, 0.035f, 1f), new Color(0.82f, 0.78f, 0.66f, 1f), 1f - d / 6.5f);
                if (d < 4f && Mathf.Abs((p.x - sc.x) + (p.y - sc.y)) < 1.1f)
                    col = new Color(0.08f, 0.07f, 0.055f, 1f);
            }

            t.SetPixel(x, y, col);
        }
        t.Apply();
        return t;
    }

    static Image CreateSpriteIcon(Transform parent, string name, Sprite spr, Vector2 anchor, Vector2 size, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = spr;
        img.color = tint;
        img.preserveAspect = true;
        img.raycastTarget = false;
        AssignUiSprite(img);
        return img;
    }

    static GameObject CreateIconButton(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 size, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var bgImg = go.AddComponent<Image>();
        ApplySprite(bgImg, _sprButtonDark, Color.Lerp(new Color(0.78f, 0.8f, 0.82f, 1f), bg, 0.72f), sliced: true);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        AddButtonEdgeShadow(bgImg);
        var cb = new ColorBlock();
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var ic = new GameObject("Icon");
        ic.transform.SetParent(go.transform, false);
        var irt = ic.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.1f, 0.1f);
        irt.anchorMax = new Vector2(0.9f, 0.9f);
        irt.offsetMin = irt.offsetMax = Vector2.zero;
        var iim = ic.AddComponent<Image>();
        iim.sprite = icon;
        iim.preserveAspect = true;
        iim.color = Color.white;
        iim.raycastTarget = false;
        AssignUiSprite(iim);
        return go;
    }

    static void AddButtonEdgeShadow(Graphic g)
    {
        if (g == null || g.GetComponent<Shadow>() != null) return;
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.24f);
        s.effectDistance = new Vector2(2f, -2f);
        s.useGraphicAlpha = true;
    }

    static void SetIconButtonIconSize(GameObject button, Vector2 size)
    {
        if (button == null) return;
        var icon = button.transform.Find("Icon");
        if (icon == null) return;
        var rt = icon.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    static void AddNotifyBadge(GameObject bellBtn)
    {
        if (_sprDot == null) return;
        var b = new GameObject("NotifyDot");
        b.transform.SetParent(bellBtn.transform, false);
        var rt = b.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-4, -4);
        rt.sizeDelta = new Vector2(14, 14);
        var img = b.AddComponent<Image>();
        img.sprite = _sprDot;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    static void BuildPlayerProfileCluster(Transform topBar)
    {
        var root = new GameObject("PlayerProfile");
        root.transform.SetParent(topBar, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.448f, 0.5f);
        rt.sizeDelta = new Vector2(76, 76);

        var ring = new GameObject("PortraitRing");
        ring.transform.SetParent(root.transform, false);
        var rrt = ring.AddComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.56f);
        rrt.sizeDelta = new Vector2(58, 58);
        rrt.anchoredPosition = Vector2.zero;
        var rim = ring.AddComponent<Image>();
        rim.sprite = _sprPortraitRing;
        rim.color = Color.white;

        var face = new GameObject("PortraitFace");
        face.transform.SetParent(ring.transform, false);
        var frt = face.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(7, 7);
        frt.offsetMax = new Vector2(-7, -7);
        {
            var fim = face.AddComponent<Image>();
            AssignUiSprite(fim);
            fim.sprite = _sprAvatarPlayer;
            fim.preserveAspect = true;
            fim.color = Color.white;
        }

        // 不再叠头像 emoji，由 gen_avatar_player 表现人像

        var levelBadge = CreatePanel2(root.transform, "LevelBadge", new Vector2(0.5f, 0.08f), new Vector2(38, 22));
        SetColor(levelBadge, new Color(0.18f, 0.36f, 0.16f, 0.98f));
        var lv = CreateText(levelBadge.transform, "PlayerLevelText", "28",
            new Vector2(0.5f, 0.5f), new Vector2(36, 20), 14, Color.white);
        lv.fontStyle = FontStyle.Bold;
    }

    static void BuildFriendRows(Transform parent)
    {
        var root = new GameObject("FriendRows");
        root.transform.SetParent(parent, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 12f);

        for (int i = 0; i < 5; i++)
        {
            var row = new GameObject("FriendRow" + i);
            row.transform.SetParent(root.transform, false);
            var rowRt = row.AddComponent<RectTransform>();
            rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.sizeDelta = new Vector2(226f, 56f);
            rowRt.anchoredPosition = new Vector2(0f, -i * 62f);
            var rowImg = row.AddComponent<Image>();
            ApplySprite(rowImg, _sprFriendRow, Color.white, sliced: true);

            var ring = CreateSpriteIcon(row.transform, "FriendAvatarRing" + i, _sprPortraitRing,
                new Vector2(0.12f, 0.5f), new Vector2(42f, 42f), Color.white);
            var face = CreateSpriteIcon(row.transform, "FriendAvatar" + i, _sprAvatarPlayer,
                new Vector2(0.12f, 0.5f), new Vector2(32f, 32f), Color.white);
            face.rectTransform.anchoredPosition = Vector2.zero;

            var badge = CreatePanel2(row.transform, "FriendLevelBadge" + i, new Vector2(0.12f, 0.18f), new Vector2(24f, 16f));
            var badgeImg = badge.GetComponent<Image>();
            ApplySprite(badgeImg, _sprButtonDark, new Color(0.78f, 0.8f, 0.82f, 1f), sliced: true);
            var lv = CreateText(badge.transform, "FriendLevelText" + i, "",
                new Vector2(0.5f, 0.5f), new Vector2(22f, 14f), 10, Color.white);
            lv.fontStyle = FontStyle.Bold;

            var name = CreateText(row.transform, "FriendName" + i, "",
                new Vector2(0.34f, 0.66f), new Vector2(126f, 18f), 15, new Color(0.96f, 0.93f, 0.84f));
            name.alignment = TextAnchor.MiddleLeft;
            name.fontStyle = FontStyle.Bold;
            AddTextShadow(name);

            var rank = CreateText(row.transform, "FriendMeta" + i, "",
                new Vector2(0.34f, 0.38f), new Vector2(126f, 16f), 11, new Color(0.78f, 0.72f, 0.52f));
            rank.alignment = TextAnchor.MiddleLeft;

            var st = CreateText(row.transform, "FriendStatus" + i, "",
                new Vector2(0.8f, 0.38f), new Vector2(52f, 16f), 11, new Color(0.44f, 0.92f, 0.58f));
            st.alignment = TextAnchor.MiddleCenter;
            row.SetActive(false);
        }
    }

    static void BuildExactFriendsPanel(Transform parent)
    {
        CreateHitButton(parent, "AddFriendIconBtn", new Vector2(0.88f, 0.885f), new Vector2(42f, 40f));
        CreateHitButton(parent, "ViewAllFriendsBtn", new Vector2(0.50f, 0.075f), new Vector2(224f, 34f));
        BuildExactFriendRows(parent);

        var hiddenInput = CreateInputField(parent, "AddFriendInput", "",
            new Vector2(-2f, -2f), new Vector2(1f, 1f));
        hiddenInput.SetActive(false);

        var hiddenAdd = CreateHitButton(parent, "AddFriendButton", new Vector2(-2f, -2f), new Vector2(1f, 1f));
        hiddenAdd.gameObject.SetActive(false);

        CreateHiddenText(parent, "AddFriendStatus", "", new Vector2(0.5f, 0.02f), new Vector2(1f, 1f));
        CreateHiddenText(parent, "FriendListText", "", new Vector2(0.5f, 0.5f), new Vector2(1f, 1f));
    }

    static void BuildExactFriendRows(Transform parent)
    {
        var root = new GameObject("FriendRows");
        root.transform.SetParent(parent, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        for (int i = 0; i < 5; i++)
            BuildExactFriendRow(root.transform, i, new Vector2(0.5f, 0.785f - i * 0.166f), _sprAvatarPlayer, "", "", "", "");
    }

    static void BuildExactFriendRow(Transform parent, int i, Vector2 anchor, Sprite avatar, string nameText, string metaText, string statusText, string levelText)
    {
        var row = new GameObject("FriendRow" + i);
        row.transform.SetParent(parent, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = anchor;
        rowRt.sizeDelta = new Vector2(238f, 74f);
        rowRt.anchoredPosition = Vector2.zero;

        var avatarFrame = CreatePanel2(row.transform, "FriendAvatarFrame" + i, new Vector2(0.205f, 0.52f), new Vector2(58f, 58f));
        var frameImg = avatarFrame.GetComponent<Image>();
        ApplySprite(frameImg, _sprButtonDark, new Color(0.72f, 0.66f, 0.42f, 0.18f), sliced: true);
        frameImg.raycastTarget = false;

        var face = CreateSpriteIcon(row.transform, "FriendAvatar" + i, avatar,
            new Vector2(0.205f, 0.52f), new Vector2(49f, 49f), Color.white);
        face.rectTransform.anchoredPosition = Vector2.zero;

        var badge = CreatePanel2(row.transform, "FriendLevelBadge" + i, new Vector2(0.095f, 0.17f), new Vector2(26f, 22f));
        var badgeImg = badge.GetComponent<Image>();
        ApplySprite(badgeImg, _sprButtonDark, new Color(0.78f, 0.70f, 0.45f, 0.22f), sliced: true);
        badgeImg.raycastTarget = false;
        var lv = CreateText(badge.transform, "FriendLevelText" + i, levelText,
            new Vector2(0.5f, 0.5f), new Vector2(24f, 18f), 12, new Color(0.94f, 0.82f, 0.55f));
        lv.fontStyle = FontStyle.Bold;
        AddTextShadow(lv);

        var name = CreateText(row.transform, "FriendName" + i, nameText,
            new Vector2(0.56f, 0.70f), new Vector2(132f, 22f), 17, new Color(0.92f, 0.86f, 0.70f));
        name.alignment = TextAnchor.MiddleLeft;
        name.fontStyle = FontStyle.Bold;
        AddTextShadow(name);

        var rankIcon = CreateSpriteIcon(row.transform, "FriendMeritIcon" + i, _sprIconStar,
            new Vector2(0.43f, 0.47f), new Vector2(19f, 19f), new Color(0.96f, 0.78f, 0.30f, 1f));
        rankIcon.raycastTarget = false;

        var rank = CreateText(row.transform, "FriendMeta" + i, metaText,
            new Vector2(0.61f, 0.47f), new Vector2(112f, 18f), 13, new Color(0.78f, 0.72f, 0.55f));
        rank.alignment = TextAnchor.MiddleLeft;
        AddTextShadow(rank);

        string statusLower = statusText.ToLowerInvariant();
        var stColor = (statusLower.Contains("off") || statusLower.Contains("离线"))
            ? new Color(0.60f, 0.63f, 0.60f)
            : ((statusLower.Contains("game") || statusLower.Contains("match") || statusLower.Contains("游戏"))
                ? new Color(1f, 0.70f, 0.24f)
                : new Color(0.40f, 0.90f, 0.34f));
        var st = CreateText(row.transform, "FriendStatus" + i, statusText,
            new Vector2(0.56f, 0.22f), new Vector2(132f, 18f), 13, stColor);
        st.alignment = TextAnchor.MiddleLeft;
        AddTextShadow(st);
        row.SetActive(false);
    }

    static void AddTextShadow(Graphic g)
    {
        if (g == null || g.GetComponent<Shadow>() != null) return;
        var s = g.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0, 0, 0, 0.55f);
        s.effectDistance = new Vector2(2f, -2f);
    }

    static void FitTextInside(Text text, int maxFontSize, int minFontSize = 8)
    {
        if (text == null) return;
        text.fontSize = maxFontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minFontSize;
        text.resizeTextMaxSize = maxFontSize;
    }

    static void AddGoldOutline(Text t)
    {
        if (t == null || t.GetComponent<Outline>() != null) return;
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.18f, 0.1f, 0.02f, 0.9f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
    }

    static GameObject CreateNavButton(Transform parent, string name, Sprite iconSpr, string label,
        Vector2 anchor, Vector2 size, Color bgColor, bool elevated)
    {
        bool premiumNav = name == "NavShopBtn" || name == "NavWarehouseBtn" || name == "NavRankBtn";
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = premiumNav ? new Vector2(172f, 54f) : (elevated ? new Vector2(size.x * 1.06f, size.y * 1.08f) : size);
        rt.anchoredPosition = elevated ? new Vector2(0, 4f) : Vector2.zero;
        var img = go.AddComponent<Image>();
        ApplySprite(img, premiumNav ? _sprNavWarehouseButton : (elevated ? _sprNavTabActive : _sprNavTab), Color.white, sliced: true);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddButtonEdgeShadow(img);
        var cb = new ColorBlock();
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        var iconGo = new GameObject("NavIcon");
        iconGo.transform.SetParent(go.transform, false);
        var irt = iconGo.AddComponent<RectTransform>();
        irt.anchorMin = premiumNav ? new Vector2(0.07f, 0.10f) : new Vector2(0, 0.45f);
        irt.anchorMax = premiumNav ? new Vector2(0.42f, 0.92f) : new Vector2(1, 1f);
        irt.offsetMin = premiumNav ? Vector2.zero : new Vector2(0, 0);
        irt.offsetMax = premiumNav ? Vector2.zero : new Vector2(0, -2);
        var iim = iconGo.AddComponent<Image>();
        iim.sprite = iconSpr;
        iim.preserveAspect = true;
        iim.color = new Color(1f, 0.95f, 0.78f, 1f);
        iim.raycastTarget = false;
        AssignUiSprite(iim);

        var capGo = new GameObject("Text");
        capGo.transform.SetParent(go.transform, false);
        var crt = capGo.AddComponent<RectTransform>();
        crt.anchorMin = premiumNav ? new Vector2(0.38f, 0.04f) : new Vector2(0, 0);
        crt.anchorMax = premiumNav ? new Vector2(0.98f, 0.96f) : new Vector2(1, 0.48f);
        crt.offsetMin = premiumNav ? Vector2.zero : new Vector2(0, 2);
        crt.offsetMax = new Vector2(0, 0);
        var ct = capGo.AddComponent<Text>();
        ct.text = label;
        ct.fontSize = premiumNav ? (label.Length >= 3 ? 21 : 24) : (elevated ? 15 : 13);
        ct.color = premiumNav ? new Color(1f, 0.84f, 0.46f, 1f) : new Color(0.88f, 0.9f, 0.95f, 1f);
        ct.alignment = TextAnchor.MiddleCenter;
        ct.fontStyle = premiumNav ? FontStyle.Bold : FontStyle.Normal;
        ct.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AddTextShadow(ct);
        if (premiumNav) AddGoldOutline(ct);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        return go;
    }

    static void BuildExactBottomNav(Transform parent)
    {
        CreateExactNavButton(parent, "NavShopBtn", _sprExactNavShop,
            new Vector2(0.095f, 0.50f), new Vector2(190f, 70f));
        CreateExactNavButton(parent, "NavWarehouseBtn", _sprExactNavWarehouse,
            new Vector2(0.303f, 0.50f), new Vector2(240f, 70f));
        CreateExactNavButton(parent, "NavCampaignBtn", _sprExactNavCampaign,
            new Vector2(0.50f, 0.56f), new Vector2(164f, 82f));
        CreateExactNavButton(parent, "NavRankBtn", _sprExactNavRank,
            new Vector2(0.704f, 0.50f), new Vector2(254f, 70f));
        CreateExactNavButton(parent, "NavMailBtn", _sprExactNavMail,
            new Vector2(0.914f, 0.50f), new Vector2(176f, 70f));
    }

    static GameObject CreateExactNavButton(Transform parent, string name, Sprite exactArt, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = exactArt;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = Color.white;
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddButtonEdgeShadow(img);
        var cb = new ColorBlock();
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
        cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.65f, 0.65f, 0.65f, 0.75f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        return go;
    }

    static GameObject CreateStretchColumn(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, float ol, float ob, float oright, float ot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(ol, ob);
        rt.offsetMax = new Vector2(-oright, -ot);
        var colIm = go.AddComponent<Image>();
        ApplySprite(colIm, _sprPanelFrame, new Color(1f, 1f, 1f, 0f), sliced: true);
        return go;
    }

    static void AddVisibleFrameOverlay(Transform parent, string name, Color color, float thickness, float inset)
    {
        if (parent == null) return;

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetAsLastSibling();
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);

        AddFrameLine(root.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -thickness), Vector2.zero, color);
        AddFrameLine(root.transform, "Bottom", Vector2.zero, new Vector2(1f, 0f),
            Vector2.zero, new Vector2(0f, thickness), color);
        AddFrameLine(root.transform, "Left", Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(thickness, 0f), color);
        AddFrameLine(root.transform, "Right", new Vector2(1f, 0f), Vector2.one,
            new Vector2(-thickness, 0f), Vector2.zero, color);
    }

    static void AddFrameLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img = go.AddComponent<Image>();
        AssignUiSprite(img);
        img.color = color;
        img.raycastTarget = false;
    }

    static void BuildStandaloneVisualTestButton(Transform canvas, float bottomInset)
    {
        if (canvas == null) return;

        var root = new GameObject("VisualTestShell");
        root.transform.SetParent(canvas, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(560f, 180f);
        rt.anchoredPosition = new Vector2(0f, bottomInset + 92f);

        var title = CreateText(root.transform, "VisualTestTitle", "匹配框效果测试",
            new Vector2(0.5f, 0.92f), new Vector2(280f, 24f), 15, new Color(1f, 0.88f, 0.52f));
        title.fontStyle = FontStyle.Bold;
        AddTextShadow(title);

        var hint = CreateText(root.transform, "VisualTestHint", "这里应该直接对齐你要的匹配卡金属外框",
            new Vector2(0.5f, 0.80f), new Vector2(360f, 20f), 11, new Color(0.9f, 0.84f, 0.70f, 0.92f));
        AddTextShadow(hint);

        if (_sprExactModeMatch != null)
        {
            BuildExactModeButton(root.transform, "VisualTestCard", "VisualTestButton",
                _sprExactModeMatch, new Vector2(0.5f, 0.34f), new Vector2(548f, 136f));
        }
        else
        {
            BuildBigModeButton(root.transform, "VisualTestCard", "VisualTestButton", "匹配", "快速匹配，立即开战",
                new Vector2(0.5f, 0.34f), C_MODE_ACCENT, _sprThumbMatch, _sprIconMatch,
                preserveThumbAspect: true,
                cardShadeAlpha: 0.34f);
        }
    }

    static void AddReferenceLobbySkin(Transform canvas)
    {
        if (_sprRefMaster == null)
            return;

        var go = new GameObject("ReferenceLobbySkin");
        go.transform.SetParent(canvas, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = _sprRefMaster;
        img.color = Color.white;
        img.preserveAspect = false;
        img.raycastTarget = false;
    }

    static bool HasExactLobbyChrome()
    {
        return _sprExactFriendPanel != null
            && _sprExactModeMatch != null
            && _sprExactModeCustom != null
            && _sprExactModeGlobal != null
            && _sprExactTaskPanel != null
            && _sprExactTechPanel != null
            && _sprExactNavShop != null
            && _sprExactNavWarehouse != null
            && _sprExactNavCampaign != null
            && _sprExactNavRank != null
            && _sprExactNavMail != null;
    }

    static void BuildReferenceDynamicOverlay(Transform canvas)
    {
        if (_sprRefMaster == null)
            return;

        var refSkin = canvas.Find("ReferenceLobbySkin");
        if (refSkin == null)
            return;

        var gold = CreateOverlayText(refSkin, "DynamicGoldText", "12,345",
            new Vector2(0.260f, 0.944f), new Vector2(116, 28), 22, new Color(1f, 0.92f, 0.56f));
        gold.alignment = TextAnchor.MiddleCenter;
        var gem = CreateOverlayText(refSkin, "DynamicGemText", "2,560",
            new Vector2(0.824f, 0.944f), new Vector2(116, 28), 22, new Color(0.74f, 0.96f, 1f));
        gem.alignment = TextAnchor.MiddleCenter;
        if (FindComp<Image>(canvas.gameObject, "PortraitFace") == null)
        {
            var avatar = new GameObject("DynamicPlayerAvatar");
            avatar.transform.SetParent(refSkin, false);
            var avRt = avatar.AddComponent<RectTransform>();
            avRt.anchorMin = avRt.anchorMax = avRt.pivot = new Vector2(0.448f, 0.938f);
            avRt.sizeDelta = new Vector2(46, 46);
            avRt.anchoredPosition = Vector2.zero;
            var avImg = avatar.AddComponent<Image>();
            avImg.sprite = _sprAvatarPlayer;
            avImg.color = Color.white;
            avImg.preserveAspect = true;
            avImg.raycastTarget = false;
        }

        CreateHitButton(refSkin, "DynamicQuickMatchButton", new Vector2(0.500f, 0.711f), new Vector2(430, 150));
        CreateHitButton(refSkin, "DynamicCustomRoomButton", new Vector2(0.500f, 0.473f), new Vector2(430, 132));
        CreateHitButton(refSkin, "DynamicGlobalConquestButton", new Vector2(0.500f, 0.234f), new Vector2(430, 132));
        CreateOverlayText(refSkin, "DynamicMatchStatusText", "",
            new Vector2(0.500f, 0.145f), new Vector2(520, 28), 15, new Color(1f, 0.78f, 0.28f));

        float[] taskY = { 0.748f, 0.647f, 0.546f };
        for (int i = 0; i < taskY.Length; i++)
        {
            var title = CreateOverlayText(refSkin, "DynamicTaskTitle" + i, "",
                new Vector2(0.812f, taskY[i] + 0.018f), new Vector2(118, 20), 12, new Color(0.92f, 0.89f, 0.72f));
            title.alignment = TextAnchor.MiddleLeft;
            var prog = CreateOverlayText(refSkin, "DynamicTaskProg" + i, "",
                new Vector2(0.865f, taskY[i] + 0.018f), new Vector2(44, 20), 12, new Color(0.50f, 0.95f, 0.62f));
            prog.alignment = TextAnchor.MiddleRight;
            CreateOverlaySlider(refSkin, "DynamicTaskSlider" + i,
                new Vector2(0.846f, taskY[i] - 0.020f), new Vector2(112, 8), new Color(0.34f, 0.92f, 0.42f));
            CreateHitButton(refSkin, "DynamicTaskClaim" + i,
                new Vector2(0.925f, taskY[i] - 0.002f), new Vector2(66, 40));
        }
        CreateHitButton(refSkin, "DynamicMoreTasksBtn", new Vector2(0.852f, 0.438f), new Vector2(178, 34));

        var techName = CreateOverlayText(refSkin, "DynamicTechResearchName", "",
            new Vector2(0.850f, 0.332f), new Vector2(150, 22), 13, new Color(0.92f, 0.89f, 0.72f));
        techName.alignment = TextAnchor.MiddleLeft;
        var techDesc = CreateOverlayText(refSkin, "DynamicTechResearchDesc", "",
            new Vector2(0.850f, 0.300f), new Vector2(150, 22), 11, new Color(0.82f, 0.84f, 0.66f));
        techDesc.alignment = TextAnchor.MiddleLeft;
        var timer = CreateOverlayText(refSkin, "DynamicTechTimerText", "",
            new Vector2(0.842f, 0.256f), new Vector2(82, 20), 12, new Color(0.50f, 0.95f, 0.62f));
        timer.alignment = TextAnchor.MiddleLeft;
        CreateOverlaySlider(refSkin, "DynamicTechProgressSlider",
            new Vector2(0.846f, 0.239f), new Vector2(112, 8), new Color(0.34f, 0.92f, 0.42f));

        CreateHitButton(refSkin, "DynamicTechButton", new Vector2(0.934f, 0.820f), new Vector2(84, 42));
        CreateHitButton(refSkin, "DynamicTechSpeedBtn", new Vector2(0.895f, 0.257f), new Vector2(60, 38));
        CreateHitButton(refSkin, "DynamicTechStartBtn", new Vector2(0.958f, 0.257f), new Vector2(60, 38));
        CreateHitButton(refSkin, "DynamicTechTreeBtn", new Vector2(0.830f, 0.154f), new Vector2(226, 42));

        CreateHitButton(refSkin, "DynamicNavShopBtn", new Vector2(0.095f, 0.046f), new Vector2(190, 70));
        CreateHitButton(refSkin, "DynamicNavWarehouseBtn", new Vector2(0.303f, 0.046f), new Vector2(240, 70));
        CreateHitButton(refSkin, "DynamicNavCampaignBtn", new Vector2(0.500f, 0.052f), new Vector2(164, 82));
        CreateHitButton(refSkin, "DynamicNavRankBtn", new Vector2(0.704f, 0.046f), new Vector2(254, 70));
        CreateHitButton(refSkin, "DynamicNavMailBtn", new Vector2(0.914f, 0.046f), new Vector2(176, 70));
    }

    static Text CreateOverlayText(Transform parent, string name, string content, Vector2 anchor, Vector2 size, int fontSize, Color color)
    {
        var txt = CreateText(parent, name, content, anchor, size, fontSize, color);
        txt.raycastTarget = false;
        txt.fontStyle = FontStyle.Bold;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 8;
        txt.resizeTextMaxSize = fontSize;
        AddTextShadow(txt);
        return txt;
    }

    static void CreateDynamicFriendRow(Transform parent, int index, Vector2 anchor)
    {
        var row = new GameObject("DynamicFriendRow" + index);
        row.transform.SetParent(parent, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = new Vector2(250, 70);
        rt.anchoredPosition = Vector2.zero;
        var hit = row.AddComponent<Image>();
        hit.sprite = null;
        hit.color = new Color(0f, 0f, 0f, 0.004f);
        var btn = row.AddComponent<Button>();
        btn.targetGraphic = hit;

        var avatar = new GameObject("DynamicFriendAvatar" + index);
        avatar.transform.SetParent(row.transform, false);
        var art = avatar.AddComponent<RectTransform>();
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.17f, 0.54f);
        art.sizeDelta = new Vector2(48, 48);
        art.anchoredPosition = Vector2.zero;
        var av = avatar.AddComponent<Image>();
        av.sprite = _sprAvatarFriendA != null ? _sprAvatarFriendA : _sprAvatarPlayer;
        av.color = Color.white;
        av.preserveAspect = true;
        av.raycastTarget = false;

        var lvl = CreateOverlayText(row.transform, "DynamicFriendLevelText" + index, "",
            new Vector2(0.17f, 0.18f), new Vector2(42, 18), 12, new Color(1f, 0.90f, 0.46f));
        lvl.alignment = TextAnchor.MiddleCenter;
        var name = CreateOverlayText(row.transform, "DynamicFriendName" + index, "",
            new Vector2(0.51f, 0.66f), new Vector2(130, 22), 15, new Color(0.96f, 0.91f, 0.72f));
        name.alignment = TextAnchor.MiddleLeft;
        var meta = CreateOverlayText(row.transform, "DynamicFriendMeta" + index, "",
            new Vector2(0.51f, 0.42f), new Vector2(130, 20), 12, new Color(0.76f, 0.81f, 0.76f));
        meta.alignment = TextAnchor.MiddleLeft;
        var status = CreateOverlayText(row.transform, "DynamicFriendStatus" + index, "",
            new Vector2(0.82f, 0.26f), new Vector2(72, 20), 12, new Color(0.44f, 0.92f, 0.58f));
        status.alignment = TextAnchor.MiddleRight;
    }

    static void CreateOverlaySlider(Transform parent, string name, Vector2 anchor, Vector2 size, Color fillColor)
    {
        CreateSlider(parent, name, anchor, size, fillColor);
        var sliderTr = parent.Find(name);
        if (sliderTr == null) return;
        var slider = sliderTr.GetComponent<Slider>();
        if (slider != null)
        {
            slider.interactable = false;
            slider.value = 0f;
        }
        var bg = sliderTr.Find("Background")?.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = false;
        }
        var fill = sliderTr.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null)
        {
            fill.color = fillColor;
            fill.raycastTarget = false;
        }
    }

    static void BuildBigModeButton(Transform p, string cardName, string buttonObjectName,
        string title, string subtitle, Vector2 anchorPos, Color accent, Sprite thumb, Sprite iconSpr,
        bool preserveThumbAspect = true, float cardShadeAlpha = 0.88f, bool hideLeftModeIcon = false)
    {
        var card = CreatePanel2(p, cardName, anchorPos, new Vector2(548, 136));
        var baseImg = card.GetComponent<Image>();
        ApplySprite(baseImg, _sprModeCardFrame != null ? _sprModeCardFrame : _sprCardFrame, Color.white, sliced: true);

        var thumbGo = new GameObject("CardThumb");
        thumbGo.transform.SetParent(card.transform, false);
        var thRt = thumbGo.AddComponent<RectTransform>();
        thRt.SetAsFirstSibling();
        thRt.anchorMin = Vector2.zero; thRt.anchorMax = Vector2.one;
        thRt.offsetMin = new Vector2(4, 4); thRt.offsetMax = new Vector2(-4, -4);
        var thIm = thumbGo.AddComponent<Image>();
        if (thumb != null) thIm.sprite = thumb;
        thIm.preserveAspect = preserveThumbAspect;
        thIm.color = new Color(1f, 1f, 1f, hideLeftModeIcon ? 1f : 0.92f);

        var shade = new GameObject("CardShade");
        shade.transform.SetParent(card.transform, false);
        var shRt = shade.AddComponent<RectTransform>();
        shRt.anchorMin = Vector2.zero; shRt.anchorMax = Vector2.one;
        shRt.offsetMin = new Vector2(4, 4); shRt.offsetMax = new Vector2(-4, -4);
        var shIm = shade.AddComponent<Image>();
        shIm.sprite = _sprCardFade;
        shIm.color = new Color(1f, 1f, 1f, cardShadeAlpha);

        AddCardTopLine(card.transform, accent);

        var iconIm = CreateSpriteIcon(card.transform, "CardIcon", iconSpr,
            new Vector2(0.13f, 0.52f), new Vector2(86, 86), new Color(1f, 0.96f, 0.82f, 0.95f));
        AddTextShadow(iconIm);
        if (hideLeftModeIcon)
            iconIm.gameObject.SetActive(false);

        var txtT = CreateText(card.transform, "TitleTxt", title,
            new Vector2(0.56f, 0.62f), new Vector2(330, 58), 44, new Color(1f, 0.9f, 0.55f));
        txtT.alignment = TextAnchor.MiddleCenter;
        txtT.fontStyle = FontStyle.Bold;
        AddTextShadow(txtT);
        AddGoldOutline(txtT);

        var txtS = CreateText(card.transform, "SubTxt", subtitle,
            new Vector2(0.56f, 0.34f), new Vector2(360, 28), 16, new Color(0.96f, 0.92f, 0.78f));
        txtS.alignment = TextAnchor.MiddleCenter;
        AddTextShadow(txtS);

        var stars = CreateText(card.transform, "StarDecor", "★ ★ ★",
            new Vector2(0.56f, 0.14f), new Vector2(160, 20), 13, new Color(0.9f, 0.72f, 0.32f));
        stars.alignment = TextAnchor.MiddleCenter;
        AddTextShadow(stars);

        var rim = card.AddComponent<Outline>();
        rim.effectColor = new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 0.75f);
        rim.effectDistance = new Vector2(1.5f, -1.5f);

        var goBtn = new GameObject(buttonObjectName);
        goBtn.transform.SetParent(card.transform, false);
        var rt = goBtn.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var ii = goBtn.AddComponent<Image>();
        AssignUiSprite(ii);
        ii.color = new Color(0, 0, 0, 0.004f);
        ii.raycastTarget = true;
        goBtn.AddComponent<Button>().targetGraphic = ii;
    }

    static void BuildExactModeButton(Transform p, string cardName, string buttonObjectName, Sprite exactArt,
        Vector2 anchorPos, Vector2 size)
    {
        var card = new GameObject(cardName);
        card.transform.SetParent(p, false);
        var rt = card.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchorPos;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var art = card.AddComponent<Image>();
        art.sprite = exactArt;
        art.type = Image.Type.Simple;
        art.preserveAspect = false;
        art.color = Color.white;
        art.raycastTarget = false;

        var goBtn = new GameObject(buttonObjectName);
        goBtn.transform.SetParent(card.transform, false);
        var brt = goBtn.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        var hit = goBtn.AddComponent<Image>();
        hit.sprite = null;
        hit.color = new Color(0f, 0f, 0f, 0.004f);
        hit.raycastTarget = true;
        goBtn.AddComponent<Button>().targetGraphic = hit;
    }

    static void BuildCenterModeCards(Transform p)
    {
        if (_sprExactModeMatch != null && _sprExactModeCustom != null && _sprExactModeGlobal != null)
        {
            BuildExactModeButton(p, "CardMatch", "QuickMatchButton", _sprExactModeMatch,
                new Vector2(0.5f, 0.78f), new Vector2(548f, 136f));
            BuildExactModeButton(p, "CardCustom", "CustomRoomButton", _sprExactModeCustom,
                new Vector2(0.5f, 0.48f), new Vector2(548f, 136f));
            BuildExactModeButton(p, "CardGlobal", "GlobalConquestButton", _sprExactModeGlobal,
                new Vector2(0.5f, 0.18f), new Vector2(548f, 136f));
            var exactSt = CreateText(p, "MatchStatusText", "",
                new Vector2(0.5f, 0.02f), new Vector2(520, 26), 14, C_ORANGE);
            AddTextShadow(exactSt);
            return;
        }

        BuildBigModeButton(p, "CardMatch", "QuickMatchButton", "匹配", "快速匹配，立即开战",
            new Vector2(0.5f, 0.78f), C_MODE_ACCENT, _sprThumbMatch, _sprIconMatch,
            preserveThumbAspect: true,
            cardShadeAlpha: 0.34f);
        bool customArt = _customThumbFromRefs;
        BuildBigModeButton(p, "CardCustom", "CustomRoomButton", "自定义", "创建房间，自由对战",
            new Vector2(0.5f, 0.48f), C_MODE_ACCENT, _sprThumbCustom, _sprIconCustom,
            preserveThumbAspect: true,
            cardShadeAlpha: customArt ? 0.48f : 0.88f,
            hideLeftModeIcon: customArt);
        BuildBigModeButton(p, "CardGlobal", "GlobalConquestButton", "争霸", "全球对抗，争夺荣耀",
            new Vector2(0.5f, 0.18f), C_MODE_ACCENT, _sprThumbGlobal, _sprIconWar,
            preserveThumbAspect: true,
            cardShadeAlpha: 0.34f);
        var st = CreateText(p, "MatchStatusText", "",
            new Vector2(0.5f, 0.02f), new Vector2(520, 26), 14, C_ORANGE);
        AddTextShadow(st);
    }

    static void BuildRightTasksAndTech(Transform p)
    {
        if (_sprExactTaskPanel != null && _sprExactTechPanel != null)
        {
            BuildExactRightTasksAndTech(p);
            return;
        }

        var taskHead = CreateText(p, "TaskSectionTitle", "任务",
            new Vector2(0.48f, 0.94f), new Vector2(140, 34), 24, new Color(0.98f, 0.86f, 0.24f));
        taskHead.fontStyle = FontStyle.Bold;
        AddTextShadow(taskHead);
        CreateSpriteIcon(p, "TaskSectionIcon", _sprIconClipboard,
            new Vector2(0.25f, 0.94f), new Vector2(20, 20), new Color(0.92f, 0.92f, 0.82f, 0.9f));
        var techBtn = CreateButton(p, "TechButton", "科技",
            new Vector2(0.92f, 0.94f), new Vector2(72, 34), new Color(0f, 0f, 0f, 0.004f));
        CreateSpriteIcon(techBtn.transform, "TechGearIcon", _sprIconGear,
            new Vector2(0.18f, 0.5f), new Vector2(22, 22), new Color(0.92f, 0.92f, 0.82f, 0.9f));

        Sprite[] taskSprs = { _sprIconTaskCrate, _sprNavWarehouse, _sprIconTank };
        string[] taskTitles = { "每日登录", "赢得3场战斗", "摧毁敌方单位" };
        string[] taskProg = { "2/3", "12/20", "0/1" };
        float[] taskSlider = { 0.66f, 0.6f, 0f };
        for (int i = 0; i < 3; i++)
        {
            var row = CreatePanel2(p, $"TaskRow{i}", new Vector2(0.5f, 0.80f - i * 0.15f), new Vector2(250, 62));
            var rowImg = row.GetComponent<Image>();
            if (rowImg != null)
            {
                rowImg.sprite = null;
                rowImg.color = new Color(0f, 0f, 0f, 0f);
                rowImg.raycastTarget = false;
            }
            CreateSpriteIcon(row.transform, $"TaskIcon{i}", taskSprs[i],
                new Vector2(0.13f, 0.52f), new Vector2(34, 34), Color.white);
            CreateText(row.transform, $"TaskTitle{i}", taskTitles[i],
                new Vector2(0.46f, 0.64f), new Vector2(130, 20), 13,
                new Color(0.90f, 0.90f, 0.76f)).alignment = TextAnchor.MiddleLeft;
            CreateText(row.transform, $"TaskProg{i}", taskProg[i],
                new Vector2(0.82f, 0.64f), new Vector2(54, 20), 12,
                new Color(0.55f, 0.95f, 0.68f));
            CreateSlider(row.transform, $"TaskSlider{i}",
                new Vector2(0.56f, 0.33f), new Vector2(126, 12), C_GREEN);
            var slTr = row.transform.Find($"TaskSlider{i}");
            if (slTr != null)
            {
                SkinMissionSlider(slTr, taskSlider[i]);
                var scomp = slTr.GetComponent<Slider>();
                if (scomp != null) scomp.value = taskSlider[i];
            }
            CreateButton(row.transform, $"TaskClaim{i}", "领取",
                new Vector2(0.89f, 0.30f), new Vector2(54, 28), new Color(0f, 0f, 0f, 0.004f));
        }
        var moreBtn = CreateButton(p, "MoreTasksBtn", "查看全部任务",
            new Vector2(0.50f, 0.40f), new Vector2(214, 30), new Color(0f, 0f, 0f, 0.004f));
        CreateText(p, "MoreTasksArrow", ">",
            new Vector2(0.88f, 0.40f), new Vector2(24, 30), 28, new Color(0.98f, 0.96f, 0.78f)).fontStyle = FontStyle.Bold;
        var moreTxt = moreBtn.GetComponentInChildren<Text>();
        if (moreTxt != null)
        {
            moreTxt.color = new Color(0.98f, 0.96f, 0.78f);
            moreTxt.fontSize = 17;
            moreTxt.fontStyle = FontStyle.Bold;
        }

        var techHead = CreateText(p, "TechSectionTitle", "科技",
            new Vector2(0.48f, 0.34f), new Vector2(140, 30), 20, new Color(0.98f, 0.86f, 0.24f));
        techHead.fontStyle = FontStyle.Bold;
        AddTextShadow(techHead);

        var tbox = CreatePanel2(p, "TechSummaryBox", new Vector2(0.5f, 0.18f), new Vector2(250, 92));
        var tboxImg = tbox.GetComponent<Image>();
        if (tboxImg != null)
        {
            tboxImg.sprite = null;
            tboxImg.color = new Color(0f, 0f, 0f, 0f);
            tboxImg.raycastTarget = false;
        }
        CreateSpriteIcon(tbox.transform, "TechBlueprintIcon", _sprIconTechBlueprint,
            new Vector2(0.18f, 0.52f), new Vector2(54, 54), Color.white);
        CreateText(tbox.transform, "TechResearchName", "战车装甲强化 I",
            new Vector2(0.60f, 0.70f), new Vector2(146, 22), 14, new Color(0.90f, 0.90f, 0.76f)).alignment = TextAnchor.MiddleLeft;
        CreateText(tbox.transform, "TechResearchDesc", "提升战车生命值 15%",
            new Vector2(0.60f, 0.49f), new Vector2(146, 24), 12, new Color(0.82f, 0.84f, 0.68f)).alignment = TextAnchor.MiddleLeft;
        CreateText(tbox.transform, "TechTimerText", "02:15:30",
            new Vector2(0.50f, 0.20f), new Vector2(90, 20), 13, new Color(0.55f, 0.95f, 0.68f)).alignment = TextAnchor.MiddleLeft;
        CreateSlider(tbox.transform, "TechProgressSlider",
            new Vector2(0.48f, 0.16f), new Vector2(90, 12), C_GREEN);
        var tsTr = tbox.transform.Find("TechProgressSlider");
        if (tsTr != null)
        {
            SkinMissionSlider(tsTr, 0.62f);
            var ts = tsTr.GetComponent<Slider>();
            if (ts != null) ts.value = 0.62f;
        }
        CreateButton(tbox.transform, "TechSpeedBtn", "加速",
            new Vector2(0.79f, 0.18f), new Vector2(58, 28), new Color(0f, 0f, 0f, 0.004f));
        CreateButton(tbox.transform, "TechStartBtn", "研究",
            new Vector2(0.79f, 0.18f), new Vector2(58, 28), new Color(0f, 0f, 0f, 0.004f));
        var treeBtn = CreateButton(p, "TechTreeBtn", "查看科技树",
            new Vector2(0.50f, 0.055f), new Vector2(214, 30), new Color(0f, 0f, 0f, 0.004f));
        CreateText(p, "TechTreeArrow", ">",
            new Vector2(0.88f, 0.055f), new Vector2(24, 30), 28, new Color(0.98f, 0.96f, 0.78f)).fontStyle = FontStyle.Bold;
        var treeTxt = treeBtn.GetComponentInChildren<Text>();
        if (treeTxt != null)
        {
            treeTxt.color = new Color(0.98f, 0.96f, 0.78f);
            treeTxt.fontSize = 17;
            treeTxt.fontStyle = FontStyle.Bold;
        }
    }

    static void BuildExactRightTasksAndTech(Transform p)
    {
        var taskPanel = CreateExactArt(p, "ExactTaskPanelArt", _sprExactTaskPanel,
            new Vector2(0.5f, 0.705f), new Vector2(250, 310));
        var techPanel = CreateExactArt(p, "ExactTechPanelArt", _sprExactTechPanel,
            new Vector2(0.5f, 0.060f), new Vector2(246, 166));

        CreateHiddenText(p, "TaskSectionTitle", "任务", new Vector2(0.5f, 0.94f), new Vector2(1, 1));
        CreateHiddenText(p, "TechSectionTitle", "科技", new Vector2(0.5f, 0.34f), new Vector2(1, 1));
        CreateHitButton(p, "TechButton", new Vector2(0.92f, 0.94f), new Vector2(72, 34));

        float[] rowY = { 0.70f, 0.46f, 0.22f };
        for (int i = 0; i < 3; i++)
        {
            var row = new GameObject("TaskRow" + i);
            row.transform.SetParent(taskPanel.transform, false);
            var rr = row.AddComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, rowY[i]);
            rr.sizeDelta = new Vector2(230, 62);
            rr.anchoredPosition = Vector2.zero;
            var title = CreateText(row.transform, "TaskTitle" + i, "",
                new Vector2(0.480f, 0.66f), new Vector2(100, 18), 12, new Color(0.92f, 0.90f, 0.76f));
            title.alignment = TextAnchor.MiddleLeft;
            FitTextInside(title, 12, 8);
            AddTextShadow(title);
            var prog = CreateText(row.transform, "TaskProg" + i, "",
                new Vector2(0.940f, 0.66f), new Vector2(42, 18), 12, new Color(0.55f, 0.95f, 0.68f));
            prog.alignment = TextAnchor.MiddleRight;
            FitTextInside(prog, 12, 8);
            AddTextShadow(prog);
            CreateSlider(row.transform, "TaskSlider" + i,
                new Vector2(0.500f, 0.33f), new Vector2(96, 8), C_GREEN);
            var sliderTr = row.transform.Find("TaskSlider" + i);
            if (sliderTr != null)
                SkinMissionSlider(sliderTr, 0f);
            CreateHitButton(row.transform, "TaskClaim" + i, new Vector2(0.940f, 0.12f), new Vector2(56, 24));
        }

        CreateHitButton(taskPanel.transform, "MoreTasksBtn", new Vector2(0.5f, 0.07f), new Vector2(196, 30));
        CreateHiddenText(p, "MoreTasksArrow", ">", new Vector2(0.88f, 0.40f), new Vector2(1, 1));

        var techName = CreateText(techPanel.transform, "TechResearchName", "",
            new Vector2(0.595f, 0.70f), new Vector2(110, 18), 13, new Color(0.92f, 0.90f, 0.76f));
        techName.alignment = TextAnchor.MiddleLeft;
        FitTextInside(techName, 13, 8);
        AddTextShadow(techName);
        var techDesc = CreateText(techPanel.transform, "TechResearchDesc", "",
            new Vector2(0.595f, 0.49f), new Vector2(112, 18), 11, new Color(0.82f, 0.84f, 0.68f));
        techDesc.alignment = TextAnchor.MiddleLeft;
        FitTextInside(techDesc, 11, 8);
        AddTextShadow(techDesc);
        var techTimer = CreateText(techPanel.transform, "TechTimerText", "",
            new Vector2(0.485f, 0.20f), new Vector2(76, 18), 12, new Color(0.55f, 0.95f, 0.68f));
        techTimer.alignment = TextAnchor.MiddleLeft;
        FitTextInside(techTimer, 12, 8);
        AddTextShadow(techTimer);
        CreateSlider(techPanel.transform, "TechProgressSlider",
            new Vector2(0.472f, 0.16f), new Vector2(76, 8), C_GREEN);
        var techSlider = techPanel.transform.Find("TechProgressSlider");
        if (techSlider != null)
            SkinMissionSlider(techSlider, 0f);
        CreateHitButton(techPanel.transform, "TechSpeedBtn", new Vector2(0.80f, 0.38f), new Vector2(56, 28));
        CreateHitButton(techPanel.transform, "TechStartBtn", new Vector2(0.80f, 0.38f), new Vector2(56, 28));
        CreateHitButton(techPanel.transform, "TechTreeBtn", new Vector2(0.5f, 0.08f), new Vector2(196, 30));
        CreateHiddenText(p, "TechTreeArrow", ">", new Vector2(0.88f, 0.055f), new Vector2(1, 1));
    }

    static GameObject CreateExactArt(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = Color.white;
        img.raycastTarget = false;
        return go;
    }

    static Button CreateHitButton(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = null;
        img.color = new Color(0f, 0f, 0f, 0.004f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    static Text CreateHiddenText(Transform parent, string name, string content, Vector2 anchor, Vector2 size)
    {
        var t = CreateText(parent, name, content, anchor, size, 1, new Color(1f, 1f, 1f, 0f));
        t.raycastTarget = false;
        return t;
    }

    static void HideGraphics(Transform root)
    {
        if (root == null) return;
        foreach (var g in root.GetComponentsInChildren<Graphic>(true))
        {
            var c = g.color;
            c.a = 0f;
            g.color = c;
            g.raycastTarget = false;
        }
    }

    static void BuildWarehousePanel(Transform p)
    {
        var bg = p.GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = _sprBgDesk;
            bg.color = new Color(0.28f, 0.24f, 0.18f, 1f);
            bg.preserveAspect = false;
        }

        var shell = CreatePanel2(p, "WarehouseShell", new Vector2(0.5f, 0.52f), new Vector2(940, 500));
        ApplySprite(shell.GetComponent<Image>(), _sprPanelFrame, Color.white, sliced: true);

        var hero = CreatePanel2(shell.transform, "WarehouseHeroPlate", new Vector2(0.5f, 0.82f), new Vector2(760, 126));
        ApplySprite(hero.GetComponent<Image>(), _sprNavWarehouseButton, Color.white, sliced: true);
        CreateSpriteIcon(hero.transform, "WarehouseHeroIcon", _sprNavWarehouse,
            new Vector2(0.27f, 0.52f), new Vector2(104, 104), Color.white);
        var title = CreateText(hero.transform, "WarehouseTitle", "仓库",
            new Vector2(0.61f, 0.54f), new Vector2(280, 86), 50, new Color(1f, 0.84f, 0.46f));
        title.fontStyle = FontStyle.Bold;
        AddTextShadow(title);
        AddGoldOutline(title);

        CreateText(shell.transform, "WarehouseSubtitle", "物资补给 / 装备库存 / 战备箱",
            new Vector2(0.5f, 0.64f), new Vector2(600, 28), 18, new Color(0.90f, 0.82f, 0.62f));

        var capBox = CreatePanel2(shell.transform, "WarehouseCapacityBox", new Vector2(0.5f, 0.57f), new Vector2(560, 36));
        ApplySprite(capBox.GetComponent<Image>(), _sprButtonDark, new Color(0.72f, 0.66f, 0.48f, 1f), sliced: true);
        CreateText(capBox.transform, "WarehouseCapacityText", "容量  68 / 120",
            new Vector2(0.20f, 0.5f), new Vector2(160, 24), 14, new Color(0.96f, 0.90f, 0.70f)).alignment = TextAnchor.MiddleLeft;
        CreateSlider(capBox.transform, "WarehouseCapacitySlider",
            new Vector2(0.66f, 0.5f), new Vector2(330, 12), C_GOLD);
        var capSlider = capBox.transform.Find("WarehouseCapacitySlider")?.GetComponent<Slider>();
        if (capSlider != null) capSlider.value = 0.56f;

        string[] tabs = { "全部", "补给", "装备", "材料" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = CreateButton(shell.transform, "WarehouseTab" + i, tabs[i],
                new Vector2(0.25f + i * 0.17f, 0.51f), new Vector2(116, 32),
                i == 0 ? new Color(0.42f, 0.33f, 0.12f, 0.98f) : new Color(0.14f, 0.14f, 0.12f, 0.98f));
            var tabText = tab.GetComponentInChildren<Text>();
            if (tabText != null) tabText.color = i == 0 ? new Color(1f, 0.84f, 0.46f) : new Color(0.82f, 0.78f, 0.66f);
        }

        string[] names = { "装甲补给箱", "能源核心", "战车零件", "指挥芯片", "合金钢材", "加速模块" };
        Sprite[] icons = { _sprNavWarehouse, _sprIconGem, _sprIconTank, _sprIconGear, _sprIconStar, _sprIconCustom };
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3;
            int row = i / 3;
            var slot = CreatePanel2(shell.transform, "WarehouseSlot" + i,
                new Vector2(0.25f + col * 0.25f, 0.36f - row * 0.20f), new Vector2(210, 84));
            ApplySprite(slot.GetComponent<Image>(), _sprFriendRow, Color.white, sliced: true);
            CreateSpriteIcon(slot.transform, "WarehouseSlotIcon" + i, icons[i],
                new Vector2(0.20f, 0.55f), new Vector2(48, 48), Color.white);
            var item = CreateText(slot.transform, "WarehouseSlotName" + i, names[i],
                new Vector2(0.62f, 0.63f), new Vector2(126, 24), 15, new Color(0.98f, 0.92f, 0.76f));
            item.alignment = TextAnchor.MiddleLeft;
            item.fontStyle = FontStyle.Bold;
            CreateText(slot.transform, "WarehouseSlotCount" + i, "x" + (i * 7 + 12),
                new Vector2(0.62f, 0.34f), new Vector2(126, 20), 13, new Color(0.52f, 0.92f, 0.58f)).alignment = TextAnchor.MiddleLeft;
        }

        CreateButton(shell.transform, "WarehouseBackBtn", "返回大厅",
            new Vector2(0.5f, 0.07f), new Vector2(220, 42), new Color(0.38f, 0.30f, 0.12f, 0.98f));
    }

    // 大厅主面板（旧版生成器保留，当前场景未使用）。
    static void BuildHallPanel(Transform p)
    {
        {
            var titleTxt = CreateText(p, "HallTitle", "游戏大厅",
                new Vector2(0.5f, 0.87f), new Vector2(400, 64), 38, new Color(1f, 0.86f, 0.22f));
            titleTxt.fontStyle = FontStyle.Bold;
            var ot = titleTxt.gameObject.AddComponent<Outline>();
            ot.effectColor    = new Color(0.25f, 0.10f, 0f, 0.85f);
            ot.effectDistance  = new Vector2(2, -2);
        }

        CreateText(p, "HallSubTitle", "战略指挥中心",
            new Vector2(0.5f, 0.81f), new Vector2(380, 24), 14, new Color(0.68f, 0.58f, 0.35f));

        // 公告栏（带左侧彩色竖条）
        {
            var notice = CreatePanel2(p, "NoticeBox",
                new Vector2(0.5f, 0.7f), new Vector2(680, 106));
            SetColor(notice, BG_CARD);
            // 左侧金色竖条
            var strip = new GameObject("NoticeStrip");
            strip.transform.SetParent(notice.transform, false);
            var srt = strip.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(0, 1);
            srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(5, 0);
            strip.AddComponent<Image>().color = C_GOLD;
            CreateText(notice.transform, "NoticeText",
                "公告：测试版本 v0.1 已开放，反馈问题可获得奖励。",
                new Vector2(0.52f, 0.5f), new Vector2(620, 90), 17, new Color(0.82f, 0.84f, 0.92f));
        }

        {
            var cardQuick = CreatePanel2(p, "QuickCard", new Vector2(0.3f, 0.46f), new Vector2(300, 158));
            SetColor(cardQuick, new Color(0.09f, 0.18f, 0.35f));
            // 顶部亮边
            AddCardTopLine(cardQuick.transform, new Color(0.25f, 0.72f, 1f, 0.9f));
            CreateText(cardQuick.transform, "QuickCardTitle", "快速匹配",
                new Vector2(0.5f, 0.76f), new Vector2(270, 38), 22, new Color(0.38f, 0.84f, 1f));
            CreateText(cardQuick.transform, "QuickCardDesc", "小地图 / 快节奏战斗\n获得金币与经验",
                new Vector2(0.5f, 0.50f), new Vector2(260, 52), 15, new Color(0.70f, 0.76f, 0.88f));
            var qb = CreateButton(cardQuick.transform, "QuickMatchButton", "立即匹配",
                new Vector2(0.5f, 0.17f), new Vector2(200, 42), C_BLUE);
            qb.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        }
        {
            var cardGlobal = CreatePanel2(p, "GlobalCard", new Vector2(0.7f, 0.46f), new Vector2(300, 158));
            SetColor(cardGlobal, new Color(0.13f, 0.09f, 0.26f));
            // 顶部亮边
            AddCardTopLine(cardGlobal.transform, new Color(0.72f, 0.35f, 1f, 0.9f));
            CreateText(cardGlobal.transform, "GlobalCardTitle", "全球争霸",
                new Vector2(0.5f, 0.76f), new Vector2(270, 38), 22, new Color(0.84f, 0.58f, 1f));
            CreateText(cardGlobal.transform, "GlobalCardDesc", "大地图 / 长线策略\n全球排名对战",
                new Vector2(0.5f, 0.50f), new Vector2(260, 52), 15, new Color(0.70f, 0.76f, 0.88f));
            var gb = CreateButton(cardGlobal.transform, "GlobalConquestButton", "争霸",
                new Vector2(0.5f, 0.17f), new Vector2(200, 42), C_PURPLE);
            gb.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        }

        var crb = CreateButton(p, "CustomRoomButton", "自定义房间",
            new Vector2(0.5f, 0.24f), new Vector2(250, 50), C_GRAY);
        crb.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

        CreateText(p, "MatchStatusText", "",
            new Vector2(0.5f, 0.14f), new Vector2(500, 28), 17, new Color(1f, 0.82f, 0.25f));
    }

    // 好友侧边栏
    static void BuildFriendsSidebar(Transform p)
    {
        CreateText(p, "FriendsTitle", "好友列表",
            new Vector2(0.5f, 0.93f), new Vector2(240, 40), 20, Color.white);
        CreateButton(p, "FriendsCloseBtn", "关闭",
            new Vector2(0.88f, 0.93f), new Vector2(36, 36), C_GRAY);

        GameObject box = CreatePanel2(p, "FriendBox",
            new Vector2(0.5f, 0.62f), new Vector2(262, 330));
        SetColor(box, BG_CARD);
        CreateText(box.transform, "FriendListText", "",
            new Vector2(0.5f, 0.5f), new Vector2(240, 300), 16, new Color(0.6f, 0.65f, 0.75f));

        CreateInputField(p, "AddFriendInput", "输入玩家名...",
            new Vector2(0.5f, 0.2f), new Vector2(220, 44));
        CreateButton(p, "AddFriendButton", "+ 添加",
            new Vector2(0.5f, 0.12f), new Vector2(180, 40), C_BLUE);
        CreateText(p, "AddFriendStatus", "",
            new Vector2(0.5f, 0.06f), new Vector2(260, 26), 14, new Color(0.5f, 0.9f, 0.5f));
    }

    // ── 科技面板（模态） ──────────────────────────────────────
    static void BuildTechPanel(Transform p)
    {
        // 关闭按钮
        CreateButton(p, "TechCloseBtn", "关闭",
            new Vector2(0.96f, 0.95f), new Vector2(36, 36), C_GRAY);

        CreateText(p, "TechPlayerName", "alice",
            new Vector2(0.5f, 0.9f), new Vector2(500, 48), 32, Color.white);
        CreateText(p, "TechIntroText", "勇者无惧，星火燎原 · 战略大师",
            new Vector2(0.5f, 0.82f), new Vector2(600, 30), 17, new Color(0.65f, 0.75f, 0.9f));

        string[] statsNames = { "TechWinsBox", "TechLossBox", "TechRateBox" };
        string[] statsTitle = { "胜场", "负场", "胜率" };
        string[] statsVal   = { "TechWinsText", "TechLossText", "TechRateText" };
        float[]  statsX     = { 0.22f, 0.5f, 0.78f };
        for (int i = 0; i < 3; i++)
        {
            var box = CreatePanel2(p, statsNames[i], new Vector2(statsX[i], 0.67f), new Vector2(200, 80));
            SetColor(box, BG_CARD);
            CreateText(box.transform, statsNames[i] + "Label", statsTitle[i],
                new Vector2(0.5f, 0.72f), new Vector2(180, 28), 15, new Color(0.7f, 0.78f, 0.9f));
            CreateText(box.transform, statsVal[i], "0",
                new Vector2(0.5f, 0.3f), new Vector2(180, 34), 26, Color.white);
        }

        CreateText(p, "TechDetailTitle", "科技使用详情",
            new Vector2(0.5f, 0.5f), new Vector2(700, 36), 20, new Color(0.95f, 0.78f, 0.38f));
        var detailBox = CreatePanel2(p, "TechDetailBox",
            new Vector2(0.5f, 0.3f), new Vector2(760, 190));
        SetColor(detailBox, BG_CARD);
        CreateText(detailBox.transform, "TechDetailText",
            "【初级兵营】训练步兵 x 12     【高级防御】部署炮台 x 5\n" +
            "【快速增援】使用 x 3          【战场侦察】使用 x 8\n" +
            "科技解锁记录会显示在这里。",
            new Vector2(0.5f, 0.5f), new Vector2(720, 170), 15, new Color(0.72f, 0.78f, 0.88f));
    }

    // ── 设置面板（小模态，右侧弹出）─────────────────────────
    static void BuildSettingsPanel(Transform p)
    {
        CreateText(p, "SettingsTitle", "设置",
            new Vector2(0.5f, 0.9f), new Vector2(240, 40), 22, Color.white);
        CreateButton(p, "SettingsCloseBtn", "关闭",
            new Vector2(0.9f, 0.9f), new Vector2(32, 32), C_GRAY);

        CreateText(p, "MusicLabel", "音乐",
            new Vector2(0.5f, 0.74f), new Vector2(260, 30), 18, Color.white);
        CreateSlider(p, "MusicSlider",
            new Vector2(0.5f, 0.64f), new Vector2(270, 24), C_BLUE);

        CreateText(p, "SFXLabel", "音效",
            new Vector2(0.5f, 0.52f), new Vector2(260, 30), 18, Color.white);
        CreateSlider(p, "SFXSlider",
            new Vector2(0.5f, 0.42f), new Vector2(270, 24), C_GREEN);

        CreateButton(p, "SettingsReturnBattleButton", "返回战场",
            new Vector2(0.5f, 0.18f), new Vector2(220, 42), C_GOLD);

    }

    // ── 地图匹配面板 ──────────────────────────────────────────
    static void BuildMapPanel(Transform p)
    {
        CreateButton(p, "BackToHallButton", "返回",
            new Vector2(0.07f, 0.93f), new Vector2(130, 42), C_GRAY);
        CreateText(p, "MapTitle", "选择地图",
            new Vector2(0.5f, 0.9f), new Vector2(360, 52), 32, Color.white);

        string[] maps  = { "沙漠绿洲", "冰雪要塞", "丛林战场", "城市废墟" };
        string[] emoji = { "A", "B", "C", "D" };
        Color[]  cols  = {
            new Color(0.6f, 0.42f, 0.1f), new Color(0.2f, 0.5f, 0.75f),
            new Color(0.15f, 0.5f, 0.2f), new Color(0.32f, 0.32f, 0.38f)
        };
        for (int i = 0; i < 4; i++)
        {
            float x = (i % 2 == 0) ? 0.3f : 0.7f;
            float y = (i < 2) ? 0.64f : 0.42f;
            var btn = CreateButton(p, $"MapButton{i}", $"{emoji[i]}\n{maps[i]}",
                new Vector2(x, y), new Vector2(250, 108), cols[i]);
            var bt = btn.GetComponentInChildren<Text>();
            bt.fontSize = 20; bt.fontStyle = FontStyle.Bold;
            // 顶部亮边
            AddCardTopLine(btn.transform, Color.Lerp(cols[i], Color.white, 0.45f));
            var botLine = new GameObject("BotLine");
            botLine.transform.SetParent(btn.transform, false);
            var blrt = botLine.AddComponent<RectTransform>();
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = new Vector2(1, 0);
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = new Vector2(0, 3);
            botLine.AddComponent<Image>().color = Color.Lerp(cols[i], Color.black, 0.45f);
        }

        CreateButton(p, "StartMatchButton", "开始匹配",
            new Vector2(0.5f, 0.2f), new Vector2(300, 64), C_GREEN);
        CreateText(p, "MatchTimerText", "",
            new Vector2(0.5f, 0.1f), new Vector2(400, 32), 19, new Color(1f, 0.8f, 0.2f));
    }

    // ── 自定义房间面板 ────────────────────────────────────────
    static void BuildRoomPanel(Transform p)
    {
        CreateButton(p, "RoomBackButton", "返回大厅",
            new Vector2(0.08f, 0.93f), new Vector2(140, 44), C_GRAY);
        CreateText(p, "RoomPanelTitle", "自定义房间",
            new Vector2(0.5f, 0.93f), new Vector2(420, 50), 28, Color.white);
        CreateButton(p, "RefreshRoomsButton", "刷新",
            new Vector2(0.72f, 0.93f), new Vector2(110, 44), C_TEAL);
        CreateButton(p, "CreateRoomButton", "创建",
            new Vector2(0.88f, 0.93f), new Vector2(130, 44), C_GREEN);

        // ── 列表头 ───────────────────────────────────────────────
        var header = CreatePanel2(p, "RoomListHeader",
            new Vector2(0.5f, 0.82f), new Vector2(900, 36));
        SetColor(header, new Color(0.08f, 0.11f, 0.20f));
        CreateText(header.transform, "HeaderName",    "房间名称 / 地图",
            new Vector2(0.27f, 0.5f), new Vector2(360, 32), 14, new Color(0.5f, 0.6f, 0.75f));
        CreateText(header.transform, "HeaderPlayers", "人数",
            new Vector2(0.62f, 0.5f), new Vector2(80,  32), 14, new Color(0.5f, 0.6f, 0.75f));
        CreateText(header.transform, "HeaderStatus",  "状态",
            new Vector2(0.75f, 0.5f), new Vector2(80,  32), 14, new Color(0.5f, 0.6f, 0.75f));

        // ── 房间行（4条）──────────────────────────────────────────
        Color[] stripColors = {
            new Color(0.15f,0.55f,0.95f), new Color(0.12f,0.72f,0.35f),
            new Color(0.45f,0.18f,0.75f), new Color(0.85f,0.45f,0.1f)
        };
        float[] rowY = { 0.67f, 0.52f, 0.37f, 0.22f };
        for (int i = 0; i < 4; i++)
        {
            var row = CreatePanel2(p, $"RoomRow{i}",
                new Vector2(0.5f, rowY[i]), new Vector2(900, 68));
            SetColor(row, new Color(0.10f, 0.14f, 0.24f));

            // 左侧彩色竖条
            var strip = new GameObject($"Strip{i}");
            strip.transform.SetParent(row.transform, false);
            var stripRt = strip.AddComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0, 0);
            stripRt.anchorMax = new Vector2(0, 1);
            stripRt.pivot     = new Vector2(0, 0.5f);
            stripRt.offsetMin = Vector2.zero;
            stripRt.offsetMax = new Vector2(6, 0);
            strip.AddComponent<Image>().color = stripColors[i % 4];

            CreateText(row.transform, $"RoomIcon{i}", "房",
                new Vector2(0.06f, 0.5f), new Vector2(40, 52), 22, Color.white);

            CreateText(row.transform, $"RoomRowText{i}", "--",
                new Vector2(0.30f, 0.5f), new Vector2(360, 52), 16, new Color(0.88f, 0.92f, 0.98f));

            // 人数徽章
            CreateText(row.transform, $"RoomPlayers{i}", "--",
                new Vector2(0.62f, 0.5f), new Vector2(70, 36), 15, new Color(0.7f, 0.85f, 1f));

            var statusBadge = CreatePanel2(row.transform, $"RoomStatusBadge{i}",
                new Vector2(0.76f, 0.5f), new Vector2(72, 30));
            SetColor(statusBadge, new Color(0.12f,0.45f,0.22f));
            CreateText(statusBadge.transform, $"RoomStatusText{i}", "等待中",
                new Vector2(0.5f, 0.5f), new Vector2(66, 28), 13, new Color(0.5f,1f,0.65f));

            CreateButton(row.transform, $"RoomJoinButton{i}", "加入",
                new Vector2(0.878f, 0.5f), new Vector2(84, 44), C_BLUE);

            CreateButton(row.transform, $"RoomInviteButton{i}", "邀请",
                new Vector2(0.965f, 0.5f), new Vector2(68, 44), C_TEAL);
        }

        // 底部提示
        CreateText(p, "RoomStatusText", "",
            new Vector2(0.5f, 0.10f), new Vector2(700, 34), 16, new Color(1f, 0.8f, 0.25f));
    }

    // ── 邀请好友面板（模态） ──────────────────────────────────
    static void BuildInvitePanel(Transform parent)
    {
        GameObject modal = CreateModal(parent, "InvitePanel", new Vector2(580, 500));
        Transform box = modal.transform.GetChild(0);

        CreateText(box, "InvitePanelTitle", "邀请好友",
            new Vector2(0.5f, 0.91f), new Vector2(480, 48), 24, Color.white);

        var line = new GameObject("InviteDivider");
        line.transform.SetParent(box, false);
        var lineRt = line.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.05f, 0f); lineRt.anchorMax = new Vector2(0.95f, 0f);
        lineRt.pivot     = new Vector2(0.5f, 1f);
        lineRt.anchoredPosition = new Vector2(0, -54f);
        lineRt.sizeDelta = new Vector2(0, 1);
        line.AddComponent<Image>().color = new Color(1,1,1,0.12f);

        // 好友列表（6行）
        float[] fy = { 0.79f, 0.66f, 0.53f, 0.40f, 0.27f, 0.14f };
        for (int i = 0; i < 6; i++)
        {
            var row = CreatePanel2(box, $"InviteRow{i}",
                new Vector2(0.5f, fy[i]), new Vector2(500, 50));
            SetColor(row, i % 2 == 0
                ? new Color(0.12f, 0.16f, 0.26f)
                : new Color(0.09f, 0.12f, 0.21f));

            {
                var avGo = new GameObject($"InviteAvatar{i}");
                avGo.transform.SetParent(row.transform, false);
                var avRt = avGo.AddComponent<RectTransform>();
                avRt.anchorMin = new Vector2(0.06f, 0.5f);
                avRt.anchorMax = new Vector2(0.06f, 0.5f);
                avRt.pivot = new Vector2(0.5f, 0.5f);
                avRt.sizeDelta = new Vector2(40, 40);
                avRt.anchoredPosition = Vector2.zero;
                var aim = avGo.AddComponent<Image>();
                AssignUiSprite(aim);
                aim.sprite = (i % 3 == 0) ? _sprAvatarFriendA : (i % 3 == 1 ? _sprAvatarFriendB : _sprAvatarPlayer);
                aim.preserveAspect = true;
                aim.color = Color.white;
            }

            CreateText(row.transform, $"InviteFriendName{i}", "--",
                new Vector2(0.42f, 0.5f), new Vector2(300, 42), 16, new Color(0.88f,0.92f,0.98f));
            CreateButton(row.transform, $"InviteFriendBtn{i}", "邀请",
                new Vector2(0.87f, 0.5f), new Vector2(90, 38), C_TEAL);
        }

        CreateButton(box, "InviteCloseBtn", "关闭",
            new Vector2(0.5f, 0.04f), new Vector2(160, 42), C_GRAY);

        modal.SetActive(false);
    }

    // ── 创建房间弹窗（模态） ──────────────────────────────────
    static void BuildCreateRoomPanel(Transform parent)
    {
        GameObject modal = CreateModal(parent, "CreateRoomPanel", new Vector2(560, 420));
        Transform box = modal.transform.GetChild(0);

        CreateText(box, "CreateRoomTitle", "创建自定义房间",
            new Vector2(0.5f, 0.90f), new Vector2(460, 48), 24, Color.white);

        var line = new GameObject("CRDivider");
        line.transform.SetParent(box, false);
        var lineRt = line.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0.05f, 0f); lineRt.anchorMax = new Vector2(0.95f, 0f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.anchoredPosition = new Vector2(0, -54f);
        lineRt.sizeDelta = new Vector2(0, 1);
        line.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

        CreateText(box, "CRNameLabel", "房间名称",
            new Vector2(0.5f, 0.74f), new Vector2(420, 30), 15, new Color(0.65f, 0.75f, 0.9f));

        var inputBg = CreatePanel2(box, "CRNameInputBg",
            new Vector2(0.5f, 0.64f), new Vector2(420, 46));
        SetColor(inputBg, new Color(0.08f, 0.12f, 0.22f));
        var inputGo = new GameObject("CRNameInput");
        inputGo.transform.SetParent(inputBg.transform, false);
        var inputRt = inputGo.AddComponent<RectTransform>();
        inputRt.anchorMin = Vector2.zero; inputRt.anchorMax = Vector2.one;
        inputRt.offsetMin = new Vector2(8, 2); inputRt.offsetMax = new Vector2(-8, -2);
        var inputField = inputGo.AddComponent<InputField>();
        var inputTxt   = new GameObject("InputText");
        inputTxt.transform.SetParent(inputGo.transform, false);
        var inputTxtRt = inputTxt.AddComponent<RectTransform>();
        inputTxtRt.anchorMin = Vector2.zero; inputTxtRt.anchorMax = Vector2.one;
        inputTxtRt.offsetMin = new Vector2(4, 2); inputTxtRt.offsetMax = new Vector2(-4, -2);
        var inputTxtComp = inputTxt.AddComponent<Text>();
        inputTxtComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        inputTxtComp.fontSize = 16; inputTxtComp.color = Color.white;
        inputTxtComp.alignment = TextAnchor.MiddleLeft;
        var placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(inputGo.transform, false);
        var placeholderRt = placeholderGo.AddComponent<RectTransform>();
        placeholderRt.anchorMin = Vector2.zero; placeholderRt.anchorMax = Vector2.one;
        placeholderRt.offsetMin = new Vector2(4, 2); placeholderRt.offsetMax = new Vector2(-4, -2);
        var placeholder = placeholderGo.AddComponent<Text>();
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 16; placeholder.color = new Color(0.5f, 0.55f, 0.65f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = "输入房间名称（可选）";
        placeholder.fontStyle = FontStyle.Italic;
        inputField.textComponent = inputTxtComp;
        inputField.placeholder    = placeholder;
        inputField.characterLimit = 24;

        CreateText(box, "CRMapLabel", "选择地图",
            new Vector2(0.5f, 0.50f), new Vector2(420, 30), 15, new Color(0.65f, 0.75f, 0.9f));

        string[] maps = { "沙漠绿洲", "冰雪要塞", "丛林战场", "城市废墟" };
        float[] mx = { 0.27f, 0.73f, 0.27f, 0.73f };
        float[] my = { 0.38f, 0.38f, 0.24f, 0.24f };
        for (int i = 0; i < 4; i++)
            CreateButton(box, $"CRMapBtn{i}", maps[i],
                new Vector2(mx[i], my[i]), new Vector2(186, 38),
                new Color(0.15f, 0.25f, 0.45f));

        CreateText(box, "CRSelectedMap", "已选：沙漠绿洲",
            new Vector2(0.5f, 0.14f), new Vector2(420, 28), 14, new Color(0.4f, 0.85f, 0.55f));

        CreateButton(box, "CRConfirmBtn", "确认创建",
            new Vector2(0.35f, 0.05f), new Vector2(190, 46), C_GREEN);
        CreateButton(box, "CRCancelBtn",  "取消",
            new Vector2(0.72f, 0.05f), new Vector2(140, 46), C_GRAY);

        modal.SetActive(false);
    }

    // ── 绑定 LobbyManager ─────────────────────────────────────
    static void BindLobbyManager(LobbyManager lm, GameObject canvas)
    {
        lm.PlayerNameText  = FindComp<Text>(canvas, "PlayerNameText");
        lm.PlayerLevelText = FindComp<Text>(canvas, "PlayerLevelText");
        lm.PlayerRankText  = FindComp<Text>(canvas, "PlayerRankText");
        lm.PlayerAvatarImage = FindCompPrefer<Image>(canvas, "PortraitFace", "DynamicPlayerAvatar");
        lm.GoldText        = FindCompPrefer<Text>(canvas, "GoldCountText", "GoldText", "DynamicGoldText");
        lm.GemText         = FindCompPrefer<Text>(canvas, "GemCountText", "GemText", "DynamicGemText");

        lm.TechButton       = FindCompPrefer<Button>(canvas, "TechButton", "DynamicTechButton");
        lm.SettingsIconBtn  = FindComp<Button>(canvas, "SettingsIconBtn");
        lm.NotifyBellBtn    = FindComp<Button>(canvas, "NotifyBellBtn");
        lm.AddFriendIconBtn = FindCompPrefer<Button>(canvas, "AddFriendIconBtn", "DynamicAddFriendIconBtn");

        lm.HallPanel        = FindGO(canvas, "HallPanel");
        lm.QuickMatchButton = FindCompPrefer<Button>(canvas, "QuickMatchButton", "DynamicQuickMatchButton");
        lm.GlobalConquestButton = FindCompPrefer<Button>(canvas, "GlobalConquestButton", "DynamicGlobalConquestButton");
        lm.CustomRoomButton = FindCompPrefer<Button>(canvas, "CustomRoomButton", "DynamicCustomRoomButton");
        lm.MatchStatusText  = FindCompPrefer<Text>(canvas, "MatchStatusText", "DynamicMatchStatusText");

        lm.FriendsDock      = FindGO(canvas, "FriendsDock");
        lm.FriendsSidebar   = FindGO(canvas, "FriendsSidebar") ?? FindGO(canvas, "LeftColumn");
        lm.FriendListText   = FindComp<Text>(canvas, "FriendListText");
        lm.ViewAllFriendsBtn = FindCompPrefer<Button>(canvas, "ViewAllFriendsBtn", "DynamicViewAllFriendsBtn");
        lm.AddFriendInput   = FindComp<InputField>(canvas, "AddFriendInput");
        lm.AddFriendButton  = FindComp<Button>(canvas, "AddFriendButton");
        lm.AddFriendStatus  = FindComp<Text>(canvas, "AddFriendStatus");

        var taskTitles = new System.Collections.Generic.List<Text>();
        var taskProgs  = new System.Collections.Generic.List<Text>();
        var taskSliders = new System.Collections.Generic.List<Slider>();
        var taskClaims = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 3; i++)
        {
            taskTitles.Add(FindCompPrefer<Text>(canvas, $"TaskTitle{i}", $"DynamicTaskTitle{i}"));
            taskProgs.Add(FindCompPrefer<Text>(canvas, $"TaskProg{i}", $"DynamicTaskProg{i}"));
            taskSliders.Add(FindCompPrefer<Slider>(canvas, $"TaskSlider{i}", $"DynamicTaskSlider{i}"));
            taskClaims.Add(FindCompPrefer<Button>(canvas, $"TaskClaim{i}", $"DynamicTaskClaim{i}"));
        }
        lm.TaskTitleTexts   = taskTitles.ToArray();
        lm.TaskProgTexts    = taskProgs.ToArray();
        lm.TaskProgSliders  = taskSliders.ToArray();
        lm.TaskClaimButtons = taskClaims.ToArray();

        lm.TechResearchNameText = FindCompPrefer<Text>(canvas, "TechResearchName", "DynamicTechResearchName");
        lm.TechResearchDescText = FindCompPrefer<Text>(canvas, "TechResearchDesc", "DynamicTechResearchDesc");
        lm.TechTimerBarText     = FindCompPrefer<Text>(canvas, "TechTimerText", "DynamicTechTimerText");
        lm.TechProgressSlider   = FindCompPrefer<Slider>(canvas, "TechProgressSlider", "DynamicTechProgressSlider");
        lm.TechSpeedBtn         = FindCompPrefer<Button>(canvas, "TechSpeedBtn", "DynamicTechSpeedBtn");
        lm.TechStartBtn         = FindCompPrefer<Button>(canvas, "TechStartBtn", "DynamicTechStartBtn");
        lm.TechTreeBtn          = FindCompPrefer<Button>(canvas, "TechTreeBtn", "DynamicTechTreeBtn");
        lm.MoreTasksBtn         = FindCompPrefer<Button>(canvas, "MoreTasksBtn", "DynamicMoreTasksBtn");

        lm.NavShopBtn       = FindCompPrefer<Button>(canvas, "NavShopBtn", "DynamicNavShopBtn");
        lm.NavWarehouseBtn  = FindCompPrefer<Button>(canvas, "NavWarehouseBtn", "DynamicNavWarehouseBtn");
        lm.NavCampaignBtn   = FindCompPrefer<Button>(canvas, "NavCampaignBtn", "DynamicNavCampaignBtn");
        lm.NavRankBtn       = FindCompPrefer<Button>(canvas, "NavRankBtn", "DynamicNavRankBtn");
        lm.NavMailBtn       = FindCompPrefer<Button>(canvas, "NavMailBtn", "DynamicNavMailBtn");
        lm.WarehousePanel   = FindGO(canvas, "WarehousePanel");
        lm.WarehouseBackBtn = FindComp<Button>(canvas, "WarehouseBackBtn");
        lm.GoldPlusBtn      = FindComp<Button>(canvas, "GoldPlusBtn");
        lm.GemPlusBtn       = FindComp<Button>(canvas, "GemPlusBtn");

        lm.TechPanel        = FindGO(canvas, "TechPanel");
        lm.TechPlayerName   = FindComp<Text>(canvas, "TechPlayerName");
        lm.TechWinsText     = FindComp<Text>(canvas, "TechWinsText");
        lm.TechLossText     = FindComp<Text>(canvas, "TechLossText");
        lm.TechRateText     = FindComp<Text>(canvas, "TechRateText");
        lm.TechCloseBtn     = FindComp<Button>(canvas, "TechCloseBtn");

        lm.SettingsPanel    = FindGO(canvas, "SettingsPanel");
        lm.MusicSlider      = FindComp<Slider>(canvas, "MusicSlider");
        lm.SFXSlider        = FindComp<Slider>(canvas, "SFXSlider");
        lm.SettingsReturnBattleButton = FindCompPrefer<Button>(canvas, "SettingsReturnBattleButton", "ReturnBattleButton");
        lm.LogoutButton     = FindComp<Button>(canvas, "LogoutButton");
        lm.SettingsCloseBtn = FindComp<Button>(canvas, "SettingsCloseBtn");

        lm.MapPanel         = FindGO(canvas, "MapPanel");
        lm.StartMatchButton = FindComp<Button>(canvas, "StartMatchButton");
        lm.MatchTimerText   = FindComp<Text>(canvas, "MatchTimerText");
        lm.BackToHallButton = FindComp<Button>(canvas, "BackToHallButton");

        lm.RoomPanel        = FindGO(canvas, "RoomPanel");
        lm.CreateRoomButton = FindComp<Button>(canvas, "CreateRoomButton");
        lm.RefreshRoomsButton = FindComp<Button>(canvas, "RefreshRoomsButton");
        lm.RoomBackButton   = FindComp<Button>(canvas, "RoomBackButton");
        lm.RoomStatusText   = FindComp<Text>(canvas, "RoomStatusText");
        var rowTexts    = new System.Collections.Generic.List<Text>();
        var rowBtns     = new System.Collections.Generic.List<Button>();
        var inviteBtns  = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 4; i++)
        {
            rowTexts.Add(FindComp<Text>(canvas, $"RoomRowText{i}"));
            rowBtns.Add(FindComp<Button>(canvas, $"RoomJoinButton{i}"));
            inviteBtns.Add(FindComp<Button>(canvas, $"RoomInviteButton{i}"));
        }
        lm.RoomRowTexts      = rowTexts.ToArray();
        lm.RoomJoinButtons   = rowBtns.ToArray();
        lm.RoomInviteButtons = inviteBtns.ToArray();

        lm.InvitePanel = FindGO(canvas, "InvitePanel");
        var invNames = new System.Collections.Generic.List<Text>();
        var invFBtns = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 6; i++)
        {
            invNames.Add(FindComp<Text>(canvas, $"InviteFriendName{i}"));
            invFBtns.Add(FindComp<Button>(canvas, $"InviteFriendBtn{i}"));
        }
        lm.InviteFriendNames   = invNames.ToArray();
        lm.InviteFriendButtons = invFBtns.ToArray();
        lm.InviteCloseBtn = FindComp<Button>(canvas, "InviteCloseBtn");

        lm.MatchingOverlay   = FindGO(canvas, "MatchingOverlay");
        lm.MatchingMapText   = FindComp<Text>(canvas, "MatchingMapText");
        lm.MatchingCountText = FindComp<Text>(canvas, "MatchingCountText");
        lm.CancelMatchButton = FindComp<Button>(canvas, "CancelMatchButton");

        var mapBtns = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 4; i++)
        { var b = FindComp<Button>(canvas, $"MapButton{i}"); if (b != null) mapBtns.Add(b); }
        lm.MapButtons = mapBtns.ToArray();

        // 创建房间弹窗
        lm.CreateRoomPanel     = FindGO(canvas, "CreateRoomPanel");
        lm.CRNameInput         = FindComp<InputField>(canvas, "CRNameInput");
        lm.CRSelectedMapText   = FindComp<Text>(canvas, "CRSelectedMap");
        lm.CRConfirmBtn        = FindComp<Button>(canvas, "CRConfirmBtn");
        lm.CRCancelBtn         = FindComp<Button>(canvas, "CRCancelBtn");
        var crMapBtns = new System.Collections.Generic.List<Button>();
        for (int i = 0; i < 4; i++)
            crMapBtns.Add(FindComp<Button>(canvas, $"CRMapBtn{i}"));
        lm.CRMapButtons = crMapBtns.ToArray();

        EditorUtility.SetDirty(lm);
    }

    // ── 辅助方法 ─────────────────────────────────────────────
    static GameObject CreateFill(Transform parent, string name, float topH, float botH)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, botH); rt.offsetMax = new Vector2(0, -topH);
        var fIm = go.AddComponent<Image>();
        AssignUiSprite(fIm);
        fIm.color = new Color(0, 0, 0, 0);
        return go;
    }

    static GameObject CreateModal(Transform parent, string name, Vector2 size)
    {
        // 鍏ㄥ睆閬僵
        var mask = new GameObject(name);
        mask.transform.SetParent(parent, false);
        var mRt = mask.AddComponent<RectTransform>();
        mRt.anchorMin = Vector2.zero; mRt.anchorMax = Vector2.one;
        mRt.offsetMin = mRt.offsetMax = Vector2.zero;
        var mIm = mask.AddComponent<Image>();
        AssignUiSprite(mIm);
        mIm.color = new Color(0, 0, 0, 0.55f);
        var box = new GameObject(name + "Box");
        box.transform.SetParent(mask.transform, false);
        var bRt = box.AddComponent<RectTransform>();
        bRt.anchorMin = bRt.anchorMax = bRt.pivot = new Vector2(0.5f, 0.5f);
        bRt.sizeDelta = size; bRt.anchoredPosition = Vector2.zero;
        var bIm = box.AddComponent<Image>();
        AssignUiSprite(bIm);
        bIm.color = new Color(0.1f, 0.13f, 0.22f, 0.99f);
        // 把 Build 方法在 box 上填内容，返回 mask 作根
        // 因 BindLobbyManager 需要直接找到 box 下的控件，所以把 box 设为 mask 的子节点
        // 返回 mask 便于 SetActive
        return mask;
    }

    static GameObject CreateContentPanel(Transform parent, string name, float topH, float botH)
    {
        return CreateFill(parent, name, topH, botH);
    }

    // 卡片顶部亮边（和生产按钮风格一致）
    static void AddCardTopLine(Transform parent, Color c)
    {
        var go = new GameObject("CardTopLine");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, -3); rt.offsetMax = Vector2.zero;
        var lineIm = go.AddComponent<Image>();
        AssignUiSprite(lineIm);
        lineIm.color = c;
    }

    static void SetColor(GameObject go, Color c)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = c;
    }

    static GameObject FindGO(GameObject root, string name)
    {
        var t = root.transform.Find(name);
        if (t) return t.gameObject;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child.gameObject;
        return null;
    }

    static T FindComp<T>(GameObject root, string name) where T : Component
    {
        var go = FindGO(root, name);
        return go != null ? go.GetComponent<T>() : null;
    }

    static T FindCompPrefer<T>(GameObject root, string preferredName, string fallbackName) where T : Component
    {
        var comp = FindComp<T>(root, preferredName);
        return comp != null ? comp : FindComp<T>(root, fallbackName);
    }

    static T FindCompPrefer<T>(GameObject root, string firstName, string secondName, string thirdName) where T : Component
    {
        var comp = FindComp<T>(root, firstName);
        if (comp != null) return comp;
        comp = FindComp<T>(root, secondName);
        return comp != null ? comp : FindComp<T>(root, thirdName);
    }

    static void CreateEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    static GameObject CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 0;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.15f, 0.22f, 0.96f);
        return go;
    }

    static GameObject CreatePanel2(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var p2 = go.AddComponent<Image>();
        ApplySprite(p2, _sprPanelFrame, Color.white, sliced: true);
        return go;
    }

    static void CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
    }

    static Text CreateText(Transform parent, string name, string content,
        Vector2 anchor, Vector2 size, int fontSize, Color? color = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = color ?? Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 size, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        if (bgColor.a <= 0.01f)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = bgColor;
        }
        else
        {
            bool warm = bgColor.r > bgColor.b && bgColor.r > 0.22f && (bgColor.r - bgColor.b) > 0.05f;
            Color tint = warm
                ? Color.Lerp(Color.white, bgColor, 0.18f)
                : Color.Lerp(new Color(0.76f, 0.78f, 0.82f, 1f), bgColor, 0.78f);
            ApplySprite(img, warm ? _sprButtonGold : _sprButtonDark, tint, sliced: true);
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddButtonEdgeShadow(img);
        var cb = new ColorBlock();
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor     = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.selectedColor    = new Color(0.94f, 0.94f, 0.94f, 1f);
        cb.disabledColor    = new Color(0.55f, 0.55f, 0.55f, 0.75f);
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.1f;
        btn.colors = cb;
        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return go;
    }

    static GameObject CreateTechMetalButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        bool useExactClaimArt = _sprExactClaimButton != null && label == "领取";
        bool useExactModeFrame = !useExactClaimArt && _sprExactMatchButtonBorder != null;
        if (useExactClaimArt)
        {
            img.sprite = Resources.Load<Sprite>("LobbyGen/gen_exact_claim_button_soft") ?? _sprExactClaimButton;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else if (useExactModeFrame)
        {
            img.sprite = _sprExactMatchButtonBorder;
            img.type = Image.Type.Sliced;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else
        {
            ApplySprite(img, _sprButtonTechMetal != null ? _sprButtonTechMetal : _sprButtonDark,
                Color.white, sliced: true);
        }

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        AddButtonEdgeShadow(img);
        var cb = new ColorBlock();
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.08f, 1.04f, 0.94f, 1f);
        cb.pressedColor = new Color(0.70f, 0.66f, 0.54f, 1f);
        cb.selectedColor = new Color(1.02f, 0.98f, 0.88f, 1f);
        cb.disabledColor = new Color(0.42f, 0.40f, 0.32f, 0.72f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        if (useExactClaimArt)
            return go;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(14f, 0f);
        trt.offsetMax = new Vector2(-14f, 1f);
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = Mathf.RoundToInt(Mathf.Clamp(size.y * (useExactModeFrame ? 0.33f : 0.50f), 18f, 30f));
        txt.fontStyle = FontStyle.Bold;
        txt.color = useExactModeFrame ? new Color(1f, 0.91f, 0.60f, 1f) : new Color(1f, 0.92f, 0.64f, 1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AddTextShadow(txt);
        AddGoldOutline(txt);
        return go;
    }

    static GameObject CreateInputField(Transform parent, string name, string placeholder,
        Vector2 anchor, Vector2 size, bool password = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phrt = phGO.AddComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(5, 0); phrt.offsetMax = new Vector2(-5, 0);
        var phTxt = phGO.AddComponent<Text>();
        phTxt.text = placeholder; phTxt.fontSize = 16;
        phTxt.color = new Color(0.5f, 0.5f, 0.5f);
        phTxt.alignment = TextAnchor.MiddleLeft; phTxt.font = font;
        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(5, 0); trt.offsetMax = new Vector2(-5, 0);
        var txt = txtGO.AddComponent<Text>();
        txt.fontSize = 18; txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft; txt.font = font;
        var input = go.AddComponent<InputField>();
        if (password) input.contentType = InputField.ContentType.Password;
        input.textComponent = txt;
        input.placeholder = phTxt;
        return go;
    }

    static void CreateSlider(Transform parent, string name,
        Vector2 anchor, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        var slider = go.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgIm = bg.AddComponent<Image>();
        AssignUiSprite(bgIm);
        bgIm.color = new Color(0.2f, 0.2f, 0.25f);
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0, 0.25f); faRt.anchorMax = new Vector2(1, 0.75f);
        faRt.offsetMin = faRt.offsetMax = Vector2.zero;
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fRt = fill.AddComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
        fRt.offsetMin = fRt.offsetMax = Vector2.zero;
        var fillIm = fill.AddComponent<Image>();
        AssignUiSprite(fillIm);
        fillIm.color = fillColor;
        slider.fillRect = fRt;
    }

    static void SkinMissionSlider(Transform sliderTr, float value)
    {
        if (sliderTr == null) return;
        var slider = sliderTr.GetComponent<Slider>();
        if (slider != null)
        {
            slider.value = value;
            slider.interactable = false;
        }

        var bg = sliderTr.Find("Background")?.GetComponent<Image>();
        if (bg != null)
        {
            ApplySprite(bg, _sprProgressTrack, Color.white, sliced: true);
            bg.raycastTarget = false;
        }

        var fill = sliderTr.Find("Fill Area/Fill")?.GetComponent<Image>();
        if (fill != null)
        {
            ApplySprite(fill, _sprProgressFill, Color.white, sliced: true);
            fill.raycastTarget = false;
        }

        var fillArea = sliderTr.Find("Fill Area") as RectTransform;
        if (fillArea != null)
        {
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);
        }
    }
}
