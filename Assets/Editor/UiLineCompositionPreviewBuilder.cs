using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class UiLineCompositionPreviewBuilder
{
    const string ScenePath = "Assets/Scenes/UiLineCompositionPreview.unity";

    static readonly Color Bg = new Color(0.010f, 0.012f, 0.012f, 1f);
    static readonly Color PanelFill = new Color(0.030f, 0.035f, 0.034f, 0.96f);
    static readonly Color Brass = new Color(0.360f, 0.325f, 0.220f, 1f);
    static readonly Color BrassHot = new Color(0.860f, 0.800f, 0.580f, 0.86f);
    static readonly Color Ink = new Color(0.003f, 0.004f, 0.004f, 0.98f);
    static readonly Color DarkLine = new Color(0.070f, 0.078f, 0.076f, 1f);

    [MenuItem("RTS/UI/Generate Line Hex Preview")]
    public static void BuildPreviewScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "UiLineCompositionPreview";

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
        CreateSolid(canvasRt, "Background", Bg, Vector2.zero, new Vector2(1920f, 1080f));

        CreateTitle(canvasRt, "Line Hex Composition Preview", new Vector2(0f, 474f), 34, new Color(0.70f, 0.66f, 0.48f, 1f));
        CreateTitle(canvasRt, "procedural UI lines, hex badges, avatar frames, friend rows", new Vector2(0f, 434f), 16, new Color(0.48f, 0.51f, 0.48f, 0.9f));

        BuildLineSamples(canvasRt);
        BuildHexSamples(canvasRt);
        BuildAvatarSamples(canvasRt);
        BuildFriendRowSamples(canvasRt);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath);
        Selection.activeGameObject = canvasGo;
        Debug.Log("[UiLineCompositionPreviewBuilder] Preview scene generated: " + ScenePath);
    }

    [MenuItem("RTS/UI/Render Line Hex Preview PNG")]
    public static void RenderPreviewPng()
    {
        BuildPreviewScene();

        Camera previewCamera = GameObject.Find("PreviewCamera")?.GetComponent<Camera>();
        if (previewCamera == null)
        {
            Debug.LogError("[UiLineCompositionPreviewBuilder] PreviewCamera is missing.");
            return;
        }

        const int width = 1920;
        const int height = 1080;
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../PreviewOutput/UiLineCompositionPreview_unity.png"));
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
            Debug.Log("[UiLineCompositionPreviewBuilder] Preview PNG rendered: " + outputPath);
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

    static void BuildLineSamples(RectTransform root)
    {
        RectTransform section = CreateSection(root, "LineSamples", new Vector2(-610f, 170f), new Vector2(560f, 380f), "Layered lines");

        CreateLine(section, "GoldSeparator", new Vector2(0f, 96f), new Vector2(420f, 18f), 4.1f, Brass, BrassHot);
        CreateLine(section, "DimSeparator", new Vector2(0f, 48f), new Vector2(380f, 14f), 2.9f, new Color(0.260f, 0.250f, 0.185f, 0.98f), new Color(0.710f, 0.670f, 0.500f, 0.74f));
        CreateLine(section, "ThinDivider", new Vector2(0f, 8f), new Vector2(420f, 8f), 1.9f, new Color(0.220f, 0.225f, 0.180f, 0.98f), new Color(0.700f, 0.660f, 0.490f, 0.70f));

        RectTransform bracket = CreateShape(section, "CornerBracket", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(420f, 116f), new Vector2(0f, -92f));
        ProceduralMilitaryUiGraphic bracketG = bracket.GetComponent<ProceduralMilitaryUiGraphic>();
        bracketG.drawFill = false;
        bracketG.thickness = 4.4f;
        bracketG.cornerCut = 18f;
        bracketG.brassColor = new Color(0.330f, 0.300f, 0.205f, 0.98f);
        bracketG.highlightColor = new Color(0.820f, 0.760f, 0.540f, 0.78f);
    }

    static void BuildHexSamples(RectTransform root)
    {
        RectTransform section = CreateSection(root, "HexSamples", new Vector2(0f, 170f), new Vector2(560f, 380f), "Hex badges");

        CreateHexBadge(section, "Level28", new Vector2(-150f, 58f), new Vector2(92f, 76f), "28", Brass, BrassHot);
        CreateHexBadge(section, "Level16", new Vector2(0f, 58f), new Vector2(84f, 68f), "16", new Color(0.310f, 0.290f, 0.205f, 1f), new Color(0.760f, 0.710f, 0.520f, 0.78f));
        CreateHexBadge(section, "Level05", new Vector2(140f, 58f), new Vector2(72f, 58f), "05", new Color(0.210f, 0.245f, 0.235f, 1f), new Color(0.620f, 0.690f, 0.650f, 0.70f));

        RectTransform rail = CreateShape(section, "BadgeRail", ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, new Vector2(380f, 96f), new Vector2(0f, -86f));
        ProceduralMilitaryUiGraphic g = rail.GetComponent<ProceduralMilitaryUiGraphic>();
        g.fillColor = new Color(0.022f, 0.027f, 0.026f, 0.88f);
        g.thickness = 4.0f;
        g.cornerCut = 16f;
        CreateLine(section, "BadgeRailLine", new Vector2(0f, -86f), new Vector2(300f, 8f), 1.7f, new Color(0.300f, 0.275f, 0.185f, 0.98f), new Color(0.780f, 0.720f, 0.510f, 0.72f));
    }

    static void BuildAvatarSamples(RectTransform root)
    {
        RectTransform section = CreateSection(root, "AvatarSamples", new Vector2(610f, 170f), new Vector2(560f, 380f), "Avatar frames");

        CreateAvatarComposite(section, "AvatarA", new Vector2(-125f, 12f), true, "28");
        CreateAvatarComposite(section, "AvatarB", new Vector2(95f, 12f), false, "16");
    }

    static void BuildFriendRowSamples(RectTransform root)
    {
        RectTransform section = CreateSection(root, "FriendRowSamples", new Vector2(0f, -275f), new Vector2(1740f, 430f), "Friend row combinations");

        CreateFriendRow(section, "RowOnline", new Vector2(-520f, 80f), "Iron Soul", "Top Player", "Online", "28", new Color(0.40f, 0.88f, 0.34f, 1f), true);
        CreateFriendRow(section, "RowBusy", new Vector2(0f, 80f), "Free Wing", "Gold II", "In Game", "22", new Color(1f, 0.64f, 0.22f, 1f), true);
        CreateFriendRow(section, "RowOffline", new Vector2(520f, 80f), "Steel Flow", "Gold V", "Offline", "16", new Color(0.62f, 0.64f, 0.60f, 1f), false);

        CreateLine(section, "FooterLine", new Vector2(0f, -128f), new Vector2(1420f, 14f), 2.8f, new Color(0.300f, 0.275f, 0.190f, 0.98f), new Color(0.800f, 0.730f, 0.510f, 0.70f));
    }

    static void CreateAvatarComposite(RectTransform parent, string name, Vector2 pos, bool useAvatar, string level)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, new Vector2(170f, 150f));

        if (useAvatar)
            CreateAvatarImage(rt, "Face", new Vector2(12f, 8f), new Vector2(92f, 92f));
        else
            CreateSolid(rt, "FacePlaceholder", new Color(0.035f, 0.045f, 0.043f, 1f), new Vector2(12f, 8f), new Vector2(92f, 92f));

        RectTransform frame = CreateShape(rt, "Frame", ProceduralMilitaryUiGraphic.ShapeKind.AvatarFrame, new Vector2(132f, 126f), Vector2.zero);
        ProceduralMilitaryUiGraphic fg = frame.GetComponent<ProceduralMilitaryUiGraphic>();
        fg.thickness = 5.6f;
        fg.cornerCut = 10f;

        CreateText(rt, "Level", level, new Vector2(-48f, -43f), new Vector2(46f, 26f), 18, new Color(0.76f, 0.70f, 0.46f, 1f), TextAnchor.MiddleCenter);
    }

    static void CreateFriendRow(RectTransform parent, string name, Vector2 pos, string player, string rank, string status, string level, Color statusColor, bool useAvatar)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, new Vector2(440f, 104f));

        RectTransform row = CreateShape(rt, "RowFrame", ProceduralMilitaryUiGraphic.ShapeKind.FriendRowFrame, new Vector2(430f, 94f), Vector2.zero);
        ProceduralMilitaryUiGraphic rowG = row.GetComponent<ProceduralMilitaryUiGraphic>();
        rowG.thickness = 4.2f;
        rowG.cornerCut = 18f;
        rowG.fillColor = new Color(0.022f, 0.027f, 0.026f, 0.92f);

        if (useAvatar)
            CreateAvatarImage(rt, "Avatar", new Vector2(-148f, 0f), new Vector2(70f, 70f));
        else
            CreateSolid(rt, "AvatarPlaceholder", new Color(0.032f, 0.043f, 0.041f, 1f), new Vector2(-148f, 0f), new Vector2(70f, 70f));

        RectTransform frame = CreateShape(rt, "AvatarFrame", ProceduralMilitaryUiGraphic.ShapeKind.AvatarFrame, new Vector2(102f, 94f), new Vector2(-152f, 0f));
        ProceduralMilitaryUiGraphic frameG = frame.GetComponent<ProceduralMilitaryUiGraphic>();
        frameG.thickness = 4.2f;
        frameG.cornerCut = 8f;

        CreateText(rt, "Level", level, new Vector2(-185f, -31f), new Vector2(36f, 22f), 14, new Color(0.76f, 0.70f, 0.46f, 1f), TextAnchor.MiddleCenter);
        CreateText(rt, "Name", player, new Vector2(44f, 22f), new Vector2(250f, 28f), 21, new Color(0.76f, 0.73f, 0.58f, 1f), TextAnchor.MiddleLeft);
        CreateText(rt, "Rank", rank, new Vector2(44f, -6f), new Vector2(250f, 24f), 15, new Color(0.58f, 0.56f, 0.45f, 1f), TextAnchor.MiddleLeft);
        CreateText(rt, "Status", status, new Vector2(44f, -31f), new Vector2(250f, 22f), 15, statusColor, TextAnchor.MiddleLeft);
    }

    static RectTransform CreateSection(RectTransform parent, string name, Vector2 pos, Vector2 size, string title)
    {
        RectTransform rt = CreateShape(parent, name, ProceduralMilitaryUiGraphic.ShapeKind.ChamferedPanel, size, pos);
        ProceduralMilitaryUiGraphic g = rt.GetComponent<ProceduralMilitaryUiGraphic>();
        g.fillColor = new Color(0.018f, 0.022f, 0.021f, 0.82f);
        g.thickness = 4.0f;
        g.cornerCut = 22f;
        CreateText(rt, "Title", title, new Vector2(0f, size.y * 0.5f - 34f), new Vector2(size.x - 60f, 28f), 18, new Color(0.60f, 0.55f, 0.36f, 0.95f), TextAnchor.MiddleLeft);
        return rt;
    }

    static void CreateHexBadge(RectTransform parent, string name, Vector2 pos, Vector2 size, string text, Color brass, Color highlight)
    {
        RectTransform rt = CreateShape(parent, name, ProceduralMilitaryUiGraphic.ShapeKind.HexBadge, size, pos);
        ProceduralMilitaryUiGraphic g = rt.GetComponent<ProceduralMilitaryUiGraphic>();
        g.thickness = 5.1f;
        g.brassColor = brass;
        g.highlightColor = highlight;
        g.fillColor = new Color(0.030f, 0.035f, 0.034f, 0.96f);
        CreateText(rt, "Text", text, Vector2.zero, size * 0.62f, Mathf.RoundToInt(size.y * 0.34f), new Color(0.76f, 0.70f, 0.48f, 1f), TextAnchor.MiddleCenter);
    }

    static void CreateLine(RectTransform parent, string name, Vector2 pos, Vector2 size, float thickness, Color brass, Color highlight)
    {
        RectTransform rt = CreateShape(parent, name, ProceduralMilitaryUiGraphic.ShapeKind.LayeredLine, size, pos);
        ProceduralMilitaryUiGraphic g = rt.GetComponent<ProceduralMilitaryUiGraphic>();
        g.thickness = thickness;
        g.brassColor = brass;
        g.highlightColor = highlight;
        g.darkLineColor = new Color(0.060f, 0.068f, 0.066f, 0.95f);
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
        g.darkLineColor = DarkLine;
        g.brassColor = Brass;
        g.highlightColor = BrassHot;
        g.raycastTarget = false;
        return rt;
    }

    static void CreateAvatarImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
    {
        Sprite sprite = Resources.Load<Sprite>("LobbyGen/gen_avatar_player");
        if (sprite == null)
        {
            CreateSolid(parent, name, new Color(0.035f, 0.045f, 0.043f, 1f), pos, size);
            return;
        }

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    static void CreateSolid(RectTransform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    static Text CreateTitle(RectTransform parent, string text, Vector2 pos, int fontSize, Color color)
    {
        return CreateText(parent, "Title", text, pos, new Vector2(1200f, 42f), fontSize, color, TextAnchor.MiddleCenter);
    }

    static Text CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Center(rt, pos, size);

        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = anchor;
        label.raycastTarget = false;
        return label;
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
