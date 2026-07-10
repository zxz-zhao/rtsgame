using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DarkGoldLobbyPreviewBuilder
{
    const string ScenePath = "Assets/Scenes/DarkGoldLobbyPreview.unity";

    static readonly Color Bg = new Color(0.020f, 0.022f, 0.026f, 1f);
    static readonly Color Fog = new Color(0.065f, 0.070f, 0.080f, 0.72f);
    static readonly Color Ink = new Color(0.006f, 0.007f, 0.008f, 0.98f);
    static readonly Color Brass = new Color(0.365f, 0.318f, 0.190f, 1f);
    static readonly Color BrassHot = new Color(0.910f, 0.840f, 0.620f, 0.95f);
    static readonly Color PanelFill = new Color(0.055f, 0.060f, 0.050f, 0.94f);
    static readonly Color CardFill = new Color(0.084f, 0.077f, 0.055f, 0.96f);
    static readonly Color SoftText = new Color(0.80f, 0.76f, 0.64f, 1f);
    static readonly Color MutedText = new Color(0.57f, 0.54f, 0.47f, 1f);
    static readonly Color ActiveRed = new Color(0.62f, 0.20f, 0.16f, 0.98f);

    [MenuItem("RTS/UI/Generate Dark Gold Lobby Preview")]
    public static void BuildPreviewScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "DarkGoldLobbyPreview";

        Camera previewCamera = CreatePreviewCamera();
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = previewCamera;
        canvas.planeDistance = 10f;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
        Stretch(canvasRt);

        EnsureEventSystem();
        BuildBackground(canvasRt);
        BuildTopChrome(canvasRt);
        BuildLeftRail(canvasRt);
        BuildMainShowcase(canvasRt);
        BuildBottomTabs(canvasRt);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[DarkGoldLobbyPreviewBuilder] Preview scene generated: " + ScenePath);
    }

    [MenuItem("RTS/UI/Render Dark Gold Lobby Preview PNG")]
    public static void RenderPreviewPng()
    {
        BuildPreviewScene();

        Camera previewCamera = GameObject.Find("PreviewCamera")?.GetComponent<Camera>();
        if (previewCamera == null)
        {
            Debug.LogError("[DarkGoldLobbyPreviewBuilder] PreviewCamera is missing.");
            return;
        }

        const int width = 1920;
        const int height = 1080;
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../PreviewOutput/DarkGoldLobbyPreview_unity.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        RenderTexture previousTarget = previewCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);

        try
        {
            Canvas.ForceUpdateCanvases();
            previewCamera.targetTexture = rt;
            RenderTexture.active = rt;
            previewCamera.Render();
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            Debug.Log("[DarkGoldLobbyPreviewBuilder] Preview PNG rendered: " + outputPath);
        }
        finally
        {
            previewCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
        }
    }

    static Camera CreatePreviewCamera()
    {
        GameObject cameraGo = new GameObject("PreviewCamera", typeof(Camera));
        Camera camera = cameraGo.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Bg;
        camera.orthographic = true;
        camera.orthographicSize = 540f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        return camera;
    }

    static void BuildBackground(RectTransform root)
    {
        CreateSolid(root, "BaseBg", Bg, Vector2.zero, new Vector2(1920f, 1080f));

        Sprite deskSprite = LoadSprite("LobbyGen/gen_bg_desk");
        if (deskSprite != null)
            CreateImage(root, "DeskBg", deskSprite, new Color(0.18f, 0.19f, 0.15f, 1f), Vector2.zero, new Vector2(1920f, 1080f), preserveAspect: false);

        CreateSolid(root, "VignetteTop", new Color(0f, 0f, 0f, 0.34f), new Vector2(0f, 406f), new Vector2(1920f, 268f));
        CreateSolid(root, "VignetteBottom", new Color(0f, 0f, 0f, 0.44f), new Vector2(0f, -430f), new Vector2(1920f, 220f));
        CreateSolid(root, "LeftShade", new Color(0f, 0f, 0f, 0.22f), new Vector2(-842f, 0f), new Vector2(280f, 1080f));
        CreateSolid(root, "RightShade", new Color(0f, 0f, 0f, 0.18f), new Vector2(838f, 0f), new Vector2(244f, 1080f));
        CreateSolid(root, "CenterFog", Fog, new Vector2(50f, 12f), new Vector2(1220f, 820f));
    }

    static void BuildTopChrome(RectTransform root)
    {
        Sprite topBar = LoadSprite("UI/LobbyHandmade/Slices/top_bar");
        if (topBar != null)
            CreateImage(root, "TopBar", topBar, Color.white, new Vector2(0f, 463f), new Vector2(1840f, 154f), preserveAspect: false);

        RectTransform titlePlate = CreateShape(root, "TitlePlate", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(490f, 106f), new Vector2(42f, 446f));
        ProceduralMilitaryUiGraphic titlePlateGraphic = titlePlate.GetComponent<ProceduralMilitaryUiGraphic>();
        titlePlateGraphic.thickness = 5.4f;
        titlePlateGraphic.cornerCut = 24f;
        titlePlateGraphic.fillColor = new Color(0.050f, 0.054f, 0.041f, 0.92f);
        CreateText(root, "TitleRoot", "暗金指挥中心", new Vector2(42f, 456f), new Vector2(420f, 40f), 38, new Color(0.97f, 0.87f, 0.62f, 1f), TextAnchor.MiddleCenter, true);
        CreateText(root, "SubtitleRoot", "METAL FRAME / COMMAND LOBBY / UNITY MOCKUP", new Vector2(42f, 420f), new Vector2(420f, 18f), 14, new Color(0.62f, 0.58f, 0.46f, 0.96f), TextAnchor.MiddleCenter, false);
        CreateMetalRule(root, new Vector2(42f, 394f), new Vector2(360f, 12f), 2.8f);
        CreateText(root, "CommanderName", "指挥官 K-47", new Vector2(122f, 454f), new Vector2(250f, 28f), 18, new Color(0.93f, 0.90f, 0.82f, 0.96f), TextAnchor.MiddleCenter, false);
        CreateText(root, "CommanderRank", "少校 III", new Vector2(122f, 430f), new Vector2(180f, 18f), 14, new Color(0.72f, 0.68f, 0.56f, 0.96f), TextAnchor.MiddleCenter, false);

        CreateCounter(root, new Vector2(-480f, 446f), "12,345");
        CreateCounter(root, new Vector2(534f, 446f), "2,560");
    }

    static void BuildLeftRail(RectTransform root)
    {
        RectTransform rail = CreateShape(root, "LeftRail", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(284f, 864f), new Vector2(-770f, -14f));
        ProceduralMilitaryUiGraphic railGraphic = rail.GetComponent<ProceduralMilitaryUiGraphic>();
        railGraphic.thickness = 5.5f;
        railGraphic.cornerCut = 24f;
        railGraphic.fillColor = new Color(0.032f, 0.035f, 0.031f, 0.94f);

        CreateText(root, "BackLabelRoot", "个人信息", new Vector2(-788f, 342f), new Vector2(190f, 40f), 30, new Color(0.92f, 0.90f, 0.86f, 1f), TextAnchor.MiddleLeft, true);
        CreateChevron(root, new Vector2(-872f, 342f), 28f, new Color(0.92f, 0.90f, 0.86f, 1f));

        string[] items = { "个人档案", "详细数据", "历史战绩", "亲密关系", "时装展示", "大金收藏" };
        for (int i = 0; i < items.Length; i++)
        {
            bool active = i == 4;
            float y = 220f - i * 96f;
            RectTransform itemRoot = active
                ? CreateShape(root, "NavActive_" + i, ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(248f, 74f), new Vector2(-770f, y))
                : null;

            if (active)
            {
                ProceduralMilitaryUiGraphic g = itemRoot.GetComponent<ProceduralMilitaryUiGraphic>();
                g.thickness = 4.2f;
                g.cornerCut = 16f;
                g.fillColor = ActiveRed;
                g.brassColor = new Color(0.42f, 0.17f, 0.10f, 0.96f);
                g.highlightColor = new Color(0.88f, 0.54f, 0.42f, 0.72f);
                CreateText(root, "NavText_" + i, items[i], new Vector2(-784f, y), new Vector2(190f, 28f), 22, new Color(1f, 0.94f, 0.88f, 1f), TextAnchor.MiddleLeft, true);
            }
            else
            {
                CreateText(root, "Nav_" + i, items[i], new Vector2(-784f, y), new Vector2(190f, 28f), 22, new Color(0.90f, 0.90f, 0.90f, 0.96f), TextAnchor.MiddleLeft, true);
            }

            if (i < items.Length - 1)
                CreateSolid(root, "NavDivider_" + i, new Color(1f, 1f, 1f, 0.08f), new Vector2(-770f, y - 48f), new Vector2(214f, 2f));
        }

        RectTransform footer = CreateShape(root, "LeftRailFooter", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(230f, 62f), new Vector2(-770f, -382f));
        ProceduralMilitaryUiGraphic footerGraphic = footer.GetComponent<ProceduralMilitaryUiGraphic>();
        footerGraphic.thickness = 3.4f;
        footerGraphic.cornerCut = 12f;
        footerGraphic.fillColor = new Color(0.18f, 0.16f, 0.10f, 0.92f);
        CreateText(root, "FooterLabel", "查看更多", new Vector2(-792f, -382f), new Vector2(160f, 22f), 18, new Color(0.97f, 0.90f, 0.66f, 1f), TextAnchor.MiddleLeft, true);
    }

    static void BuildMainShowcase(RectTransform root)
    {
        RectTransform boardShadow = CreateShape(root, "MainBoardShadow", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(1316f, 780f), new Vector2(184f, -20f));
        ProceduralMilitaryUiGraphic boardShadowGraphic = boardShadow.GetComponent<ProceduralMilitaryUiGraphic>();
        boardShadowGraphic.thickness = 0f;
        boardShadowGraphic.cornerCut = 28f;
        boardShadowGraphic.fillColor = new Color(0f, 0f, 0f, 0.34f);
        boardShadowGraphic.brassColor = new Color(0f, 0f, 0f, 0f);
        boardShadowGraphic.highlightColor = new Color(0f, 0f, 0f, 0f);

        RectTransform board = CreateShape(root, "MainBoard", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(1280f, 744f), new Vector2(172f, -4f));
        ProceduralMilitaryUiGraphic boardGraphic = board.GetComponent<ProceduralMilitaryUiGraphic>();
        boardGraphic.thickness = 7.0f;
        boardGraphic.cornerCut = 28f;
        boardGraphic.fillColor = new Color(0.86f, 0.85f, 0.82f, 0.96f);
        boardGraphic.brassColor = new Color(0.34f, 0.30f, 0.22f, 1f);
        boardGraphic.highlightColor = new Color(0.96f, 0.92f, 0.80f, 0.82f);

        CreateSolid(board, "BoardWash", new Color(0.98f, 0.97f, 0.93f, 0.28f), new Vector2(0f, 18f), new Vector2(1176f, 618f));
        CreateSolid(board, "BoardTopTone", new Color(0.62f, 0.58f, 0.48f, 0.10f), new Vector2(0f, 246f), new Vector2(1134f, 80f));
        CreateSolid(board, "BoardBottomTone", new Color(0f, 0f, 0f, 0.05f), new Vector2(0f, -258f), new Vector2(1134f, 72f));
        CreateText(board, "CollectionLabel", "时装收藏度 20/96", new Vector2(-456f, 316f), new Vector2(280f, 24f), 16, new Color(0.28f, 0.28f, 0.28f, 0.88f), TextAnchor.MiddleLeft, false);
        CreateSolid(board, "TopShelf", new Color(0.72f, 0.70f, 0.62f, 0.10f), new Vector2(0f, 210f), new Vector2(1134f, 86f));
        CreateSolid(board, "BottomShelf", new Color(0.70f, 0.69f, 0.64f, 0.10f), new Vector2(0f, -248f), new Vector2(1134f, 86f));

        float startX = -338f;
        float startY = 190f;
        float gapX = 248f;
        float gapY = 258f;

        Sprite[] portraits =
        {
            LoadSprite("LobbyGen/gen_avatar_player"),
            LoadSprite("LobbyGen/gen_avatar_friend_a"),
            LoadSprite("LobbyGen/gen_avatar_friend_b"),
            LoadSprite("LobbyGen/gen_avatar_player"),
            LoadSprite("LobbyGen/gen_avatar_friend_a"),
            LoadSprite("LobbyGen/gen_avatar_friend_b"),
            LoadSprite("LobbyGen/gen_avatar_player"),
            LoadSprite("LobbyGen/gen_avatar_friend_a"),
            LoadSprite("LobbyGen/gen_avatar_friend_b"),
            LoadSprite("LobbyGen/gen_avatar_player"),
        };

        string[] labels = { "莲华渡", "耀眼的你", "迷途僵宝", "庆宴绮愿", "万达云华", "仙锋小姐", "除妖小师妹", "莓果陷阱", "黎明猎手", "焰卓之礼" };
        string[] ribbons = { "救援限定", "限定", "", "1周年限定", "非遗联名", "非遗联名", "", "魔盒限定", "魔盒限定", "魔盒限定" };

        for (int i = 0; i < 10; i++)
        {
            int col = i % 5;
            int row = i / 5;
            Vector2 pos = new Vector2(startX + col * gapX, startY - row * gapY);
            bool featured = i == 0;
            BuildShowcaseCard(board, "Card_" + i, pos, new Vector2(188f, 212f), portraits[i % portraits.Length], labels[i], ribbons[i], featured);
        }

        CreateScrollBar(board, new Vector2(0f, -312f), new Vector2(744f, 14f));
    }

    static void BuildBottomTabs(RectTransform root)
    {
        string[] tabs = { "商店", "仓库", "战役", "排行榜", "邮件" };
        float startX = -640f;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == 2;
            RectTransform tab = CreateShape(root, "BottomTab_" + i, ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(208f, 92f), new Vector2(startX + i * 322f, -474f));
            ProceduralMilitaryUiGraphic g = tab.GetComponent<ProceduralMilitaryUiGraphic>();
            g.thickness = 4.8f;
            g.cornerCut = 18f;
            g.fillColor = active ? new Color(0.30f, 0.25f, 0.14f, 0.98f) : new Color(0.050f, 0.054f, 0.045f, 0.96f);
            g.brassColor = active ? new Color(0.52f, 0.43f, 0.20f, 1f) : Brass;
            g.highlightColor = active ? new Color(0.98f, 0.88f, 0.56f, 0.92f) : BrassHot;

            CreateText(tab, "Label", tabs[i], new Vector2(0f, -2f), new Vector2(160f, 28f), 20, active ? new Color(0.98f, 0.90f, 0.66f, 1f) : SoftText, TextAnchor.MiddleCenter, true);
            if (active)
                CreateSolid(tab, "Glow", new Color(1f, 0.86f, 0.36f, 0.16f), new Vector2(0f, 16f), new Vector2(150f, 24f));
        }

        CreateMetalRule(root, new Vector2(0f, -422f), new Vector2(1250f, 12f), 2.5f);
    }

    static void BuildShowcaseCard(RectTransform parent, string name, Vector2 pos, Vector2 size, Sprite portrait, string label, string ribbon, bool featured)
    {
        RectTransform card = CreateShape(parent, name, ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, size, pos);
        ProceduralMilitaryUiGraphic cardGraphic = card.GetComponent<ProceduralMilitaryUiGraphic>();
        cardGraphic.thickness = featured ? 5.2f : 4.5f;
        cardGraphic.cornerCut = 16f;
        cardGraphic.fillColor = featured ? new Color(0.52f, 0.38f, 0.08f, 0.98f) : new Color(0.58f, 0.44f, 0.70f, 0.96f);
        cardGraphic.brassColor = featured ? new Color(0.82f, 0.66f, 0.20f, 1f) : new Color(0.50f, 0.44f, 0.62f, 1f);
        cardGraphic.highlightColor = featured ? new Color(1.0f, 0.94f, 0.72f, 0.92f) : new Color(0.90f, 0.78f, 0.92f, 0.74f);

        CreateSolid(card, "InnerMatte", new Color(1f, 1f, 1f, featured ? 0.10f : 0.07f), new Vector2(0f, 0f), new Vector2(size.x - 26f, size.y - 28f));
        if (portrait != null)
            CreateImage(card, "Portrait", portrait, Color.white, new Vector2(0f, 10f), new Vector2(size.x - 34f, size.y - 70f), preserveAspect: false);
        CreateSolid(card, "BottomShade", new Color(0f, 0f, 0f, 0.34f), new Vector2(0f, -62f), new Vector2(size.x - 26f, 44f));
        CreateText(card, "Name", label, new Vector2(0f, -70f), new Vector2(size.x - 20f, 28f), 16, new Color(0.98f, 0.96f, 0.94f, 1f), TextAnchor.MiddleCenter, true);

        if (!string.IsNullOrEmpty(ribbon))
        {
            RectTransform ribbonRoot = CreateShape(card, "Ribbon", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(118f, 34f), new Vector2(32f, 74f));
            ProceduralMilitaryUiGraphic ribbonGraphic = ribbonRoot.GetComponent<ProceduralMilitaryUiGraphic>();
            ribbonGraphic.thickness = 3.0f;
            ribbonGraphic.cornerCut = 12f;
            ribbonGraphic.fillColor = featured ? new Color(0.60f, 0.32f, 0.06f, 0.97f) : new Color(0.52f, 0.34f, 0.10f, 0.97f);
            ribbonGraphic.brassColor = new Color(0.60f, 0.42f, 0.14f, 1f);
            ribbonGraphic.highlightColor = new Color(1.0f, 0.88f, 0.46f, 0.88f);
            CreateText(ribbonRoot, "Text", ribbon, new Vector2(0f, 0f), new Vector2(104f, 20f), 14, new Color(1f, 0.90f, 0.62f, 1f), TextAnchor.MiddleCenter, true);
        }
    }

    static void CreateCounter(RectTransform parent, Vector2 pos, string value)
    {
        RectTransform counter = CreateShape(parent, "Counter_" + value.Replace(",", string.Empty), ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(246f, 66f), pos);
        ProceduralMilitaryUiGraphic g = counter.GetComponent<ProceduralMilitaryUiGraphic>();
        g.thickness = 4.0f;
        g.cornerCut = 18f;
        g.fillColor = new Color(0.040f, 0.046f, 0.038f, 0.95f);
        CreateHexIcon(counter, new Vector2(-86f, 0f), new Vector2(44f, 38f), "");
        CreateText(parent, "CounterValue_" + value.Replace(",", string.Empty), value, pos + new Vector2(22f, 0f), new Vector2(118f, 24f), 24, new Color(0.96f, 0.92f, 0.84f, 1f), TextAnchor.MiddleCenter, true);
    }

    static void CreateMetalRule(RectTransform parent, Vector2 pos, Vector2 size, float thickness)
    {
        RectTransform rule = CreateShape(parent, "MetalRule", ProceduralMilitaryUiGraphic.ShapeKind.LayeredLine, size, pos);
        ProceduralMilitaryUiGraphic g = rule.GetComponent<ProceduralMilitaryUiGraphic>();
        g.thickness = thickness;
        g.brassColor = Brass;
        g.highlightColor = BrassHot;
        g.darkLineColor = new Color(0.06f, 0.066f, 0.058f, 0.96f);
    }

    static void CreateHexIcon(RectTransform parent, Vector2 pos, Vector2 size, string text)
    {
        RectTransform icon = CreateShape(parent, "HexIcon", ProceduralMilitaryUiGraphic.ShapeKind.HexBadge, size, pos);
        ProceduralMilitaryUiGraphic g = icon.GetComponent<ProceduralMilitaryUiGraphic>();
        g.thickness = 3.2f;
        g.fillColor = new Color(0.18f, 0.14f, 0.06f, 0.95f);
        g.brassColor = new Color(0.62f, 0.50f, 0.18f, 1f);
        g.highlightColor = new Color(0.98f, 0.88f, 0.56f, 0.90f);
        if (!string.IsNullOrWhiteSpace(text))
            CreateText(icon, "Text", text, Vector2.zero, size * 0.7f, 16, new Color(0.9f, 0.84f, 0.62f, 1f), TextAnchor.MiddleCenter, true);
    }

    static void CreateChevron(RectTransform parent, Vector2 pos, float size, Color color)
    {
        GameObject go = new GameObject("Chevron", typeof(RectTransform), typeof(ProceduralChevronGraphic));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, new Vector2(size, size));
        ProceduralChevronGraphic graphic = go.GetComponent<ProceduralChevronGraphic>();
        graphic.color = color;
        graphic.raycastTarget = false;
    }

    static void CreateScrollBar(RectTransform parent, Vector2 pos, Vector2 size)
    {
        RectTransform track = CreateShape(parent, "ScrollTrack", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, size, pos);
        ProceduralMilitaryUiGraphic trackGraphic = track.GetComponent<ProceduralMilitaryUiGraphic>();
        trackGraphic.thickness = 2.8f;
        trackGraphic.cornerCut = 8f;
        trackGraphic.fillColor = new Color(0.74f, 0.74f, 0.74f, 0.22f);
        trackGraphic.brassColor = new Color(0.32f, 0.30f, 0.25f, 1f);
        trackGraphic.highlightColor = new Color(0.92f, 0.92f, 0.92f, 0.24f);

        RectTransform thumb = CreateShape(track, "Thumb", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(136f, 22f), new Vector2(0f, 0f));
        ProceduralMilitaryUiGraphic thumbGraphic = thumb.GetComponent<ProceduralMilitaryUiGraphic>();
        thumbGraphic.thickness = 2.8f;
        thumbGraphic.cornerCut = 8f;
        thumbGraphic.fillColor = new Color(0.92f, 0.92f, 0.92f, 0.78f);
        thumbGraphic.brassColor = new Color(0.58f, 0.58f, 0.58f, 1f);
        thumbGraphic.highlightColor = new Color(1f, 1f, 1f, 0.82f);
    }

    static RectTransform CreateShape(RectTransform parent, string name, ProceduralMilitaryUiGraphic.ShapeKind kind, Vector2 size, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(ProceduralMilitaryUiGraphic));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);

        ProceduralMilitaryUiGraphic g = go.GetComponent<ProceduralMilitaryUiGraphic>();
        g.shape = kind;
        g.fillColor = PanelFill;
        g.shadowColor = Ink;
        g.darkLineColor = new Color(0.080f, 0.084f, 0.074f, 1f);
        g.brassColor = Brass;
        g.highlightColor = BrassHot;
        g.raycastTarget = false;
        return rt;
    }

    static void CreateImage(RectTransform parent, string name, Sprite sprite, Color tint, Vector2 pos, Vector2 size, bool preserveAspect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = tint;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
    }

    static void CreateSolid(RectTransform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static Text CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor anchor, bool shadow)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);

        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = LoadFont("Assets/Fonts/msyhbd.ttc") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = anchor;
        label.raycastTarget = false;

        if (shadow)
        {
            Shadow drop = go.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.62f);
            drop.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return label;
    }

    static Font LoadFont(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Font>(assetPath);
    }

    static Sprite LoadSprite(string resourcePath)
    {
        return Resources.Load<Sprite>(resourcePath);
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
            return;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));
    }
}

public sealed class ProceduralChevronGraphic : Graphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0.01f || r.height <= 0.01f)
            return;

        float inset = Mathf.Min(r.width, r.height) * 0.18f;
        Vector2 a = new Vector2(r.xMax - inset, r.yMax);
        Vector2 b = new Vector2(r.xMin + inset, r.center.y);
        Vector2 c = new Vector2(r.xMax - inset, r.yMin);
        float thickness = Mathf.Max(2f, r.width * 0.16f);

        AddSegment(vh, a, b, thickness, color);
        AddSegment(vh, b, c, thickness, color);
    }

    static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
