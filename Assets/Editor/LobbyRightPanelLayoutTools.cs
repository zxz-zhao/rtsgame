using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbyRightPanelLayoutTools
{
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
    const string HandmadeLobbyScenePath = "Assets/Scenes/LobbyScene_Handmade.unity";

    static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.08f, 0.46f);
    static readonly Color SlotColorA = new Color(0.08f, 0.12f, 0.13f, 0.72f);
    static readonly Color SlotColorB = new Color(0.11f, 0.10f, 0.07f, 0.76f);
    static readonly Color LineColor = new Color(0.84f, 0.66f, 0.25f, 0.64f);
    static readonly Color TextGold = new Color(1f, 0.92f, 0.66f, 1f);
    static readonly Color TextMuted = new Color(0.76f, 0.80f, 0.68f, 1f);
    static readonly Color TextGreen = new Color(0.47f, 0.96f, 0.55f, 1f);

    public class RightPanelBindings
    {
        public readonly Text[] TaskTitles = new Text[3];
        public readonly Text[] TaskProgress = new Text[3];
        public readonly Slider[] TaskSliders = new Slider[3];
        public readonly Button[] TaskClaims = new Button[3];
        public Button MoreTasks;
        public Button TechButton;
        public Text TechName;
        public Text TechDesc;
        public Text TechTimer;
        public Slider TechSlider;
        public Button TechSpeed;
        public Button TechStart;
        public Button TechTree;
    }

    [MenuItem("RTS/大厅/重建当前场景任务科技分栏")]
    public static void ApplyToCurrentScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("[LobbyRightPanelLayout] 当前场景无效，无法保存任务/科技分栏。");
            return;
        }

        ApplyToScene(scene.path);
    }

    [MenuItem("RTS/大厅/重建 LobbyScene 任务科技分栏")]
    public static void ApplyToLobbyScene()
    {
        ApplyToScene(LobbyScenePath);
    }

    public static void ApplyToLobbyScenes()
    {
        ApplyToScene(LobbyScenePath);
        ApplyToScene(HandmadeLobbyScenePath);
    }

    static void ApplyToScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject canvas = EnsureCanvas();
        GameObject hall = EnsureHallPanel(canvas.transform);
        RightPanelBindings bindings = RebuildRightPanels(hall.transform);
        BindLobbyManager(canvas, hall, bindings);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[LobbyRightPanelLayout] 已重建任务/科技分栏: " + scenePath);
    }

    static GameObject EnsureCanvas()
    {
        GameObject canvas = GameObject.Find("LobbyCanvas");
        if (canvas != null)
            return canvas;

        canvas = new GameObject("LobbyCanvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.pixelPerfect = true;
        CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvas.AddComponent<GraphicRaycaster>();

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        return canvas;
    }

    static GameObject EnsureHallPanel(Transform canvas)
    {
        Transform existing = FindDeepChild(canvas, "HallPanel");
        GameObject hall = existing != null ? existing.gameObject : new GameObject("HallPanel");
        hall.transform.SetParent(canvas, false);

        RectTransform rt = EnsureRect(hall);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return hall;
    }

    public static RightPanelBindings RebuildRightPanels(Transform hall)
    {
        RightPanelBindings bindings = new RightPanelBindings();

        GameObject taskPanel = EnsureAnchoredPanel(hall, "renwu", new Vector2(0.852f, 0.575f),
            new Vector2(252f, 256f), PanelColor);
        GameObject techPanel = EnsureAnchoredPanel(hall, "keji", new Vector2(0.852f, 0.310f),
            new Vector2(252f, 176f), PanelColor);

        ClearChildren(taskPanel.transform);
        ClearChildren(techPanel.transform);

        for (int i = 0; i < 3; i++)
        {
            GameObject slot = CreateEqualSlotPanel(taskPanel.transform, "TaskSlotPanel" + i, i, 3,
                i % 2 == 0 ? SlotColorA : SlotColorB);
            BuildTaskSlot(slot.transform, i, bindings);
        }

        GameObject techInfo = CreateEqualSlotPanel(techPanel.transform, "TechSlotPanel0", 0, 2, SlotColorA);
        GameObject techAction = CreateEqualSlotPanel(techPanel.transform, "TechSlotPanel1", 1, 2, SlotColorB);
        BuildTechInfoSlot(techInfo.transform, bindings);
        BuildTechActionSlot(techAction.transform, bindings);

        return bindings;
    }

    static GameObject EnsureAnchoredPanel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        Transform existing = FindDeepChild(parent, name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = EnsureRect(go);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = EnsureImage(go, color, false);
        img.raycastTarget = false;
        return go;
    }

    static GameObject CreateEqualSlotPanel(Transform parent, string name, int index, int count, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        float top = 1f - index / (float)count;
        float bottom = 1f - (index + 1f) / count;
        rt.anchorMin = new Vector2(0f, bottom);
        rt.anchorMax = new Vector2(1f, top);
        rt.offsetMin = new Vector2(8f, 5f);
        rt.offsetMax = new Vector2(-8f, -5f);

        Image img = EnsureImage(go, color, false);
        img.raycastTarget = false;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.50f);
        outline.effectDistance = new Vector2(1f, -1f);
        AddTopLine(go.transform);
        return go;
    }

    static void BuildTaskSlot(Transform slot, int index, RightPanelBindings bindings)
    {
        string[] placeholders = { "每日登录", "完成对战", "摧毁单位" };
        Sprite[] icons =
        {
            Resources.Load<Sprite>("LobbyGen/gen_icon_task_crate"),
            Resources.Load<Sprite>("LobbyGen/gen_icon_helmet"),
            Resources.Load<Sprite>("LobbyGen/gen_icon_tank")
        };

        CreateIcon(slot, "TaskIcon" + index, icons[index], new Vector2(0.12f, 0.55f), new Vector2(34f, 34f));
        bindings.TaskTitles[index] = CreateText(slot, "TaskTitle" + index, placeholders[index],
            new Vector2(0.42f, 0.67f), new Vector2(122f, 22f), 13, TextGold, TextAnchor.MiddleLeft);
        bindings.TaskProgress[index] = CreateText(slot, "TaskProg" + index, "0/1",
            new Vector2(0.66f, 0.67f), new Vector2(46f, 20f), 12, TextGreen, TextAnchor.MiddleRight);
        bindings.TaskSliders[index] = CreateSlider(slot, "TaskSlider" + index,
            new Vector2(0.43f, 0.32f), new Vector2(126f, 10f), TextGreen);
        bindings.TaskClaims[index] = CreateButton(slot, "TaskClaim" + index, "领取",
            new Vector2(0.83f, 0.35f), new Vector2(58f, 28f),
            new Color(0.54f, 0.34f, 0.10f, 0.96f), TextGold);

        if (index == 2)
        {
            bindings.MoreTasks = CreateButton(slot, "MoreTasksBtn", "全部任务",
                new Vector2(0.42f, 0.12f), new Vector2(106f, 24f),
                new Color(0.16f, 0.22f, 0.28f, 0.94f), new Color(0.94f, 0.96f, 0.84f, 1f));
        }
    }

    static void BuildTechInfoSlot(Transform slot, RightPanelBindings bindings)
    {
        CreateIcon(slot, "TechBlueprintIcon", Resources.Load<Sprite>("LobbyGen/gen_icon_tech_blueprint"),
            new Vector2(0.15f, 0.52f), new Vector2(42f, 42f));
        bindings.TechName = CreateText(slot, "TechResearchName", "暂无进行中的研究",
            new Vector2(0.58f, 0.66f), new Vector2(146f, 22f), 13, TextGold, TextAnchor.MiddleLeft);
        bindings.TechDesc = CreateText(slot, "TechResearchDesc", "点击下方开始研究",
            new Vector2(0.58f, 0.36f), new Vector2(148f, 22f), 11, TextMuted, TextAnchor.MiddleLeft);
        bindings.TechButton = CreateButton(slot, "TechButton", "科技",
            new Vector2(0.86f, 0.86f), new Vector2(54f, 24f),
            new Color(0.16f, 0.22f, 0.28f, 0.92f), TextGold);
    }

    static void BuildTechActionSlot(Transform slot, RightPanelBindings bindings)
    {
        bindings.TechTimer = CreateText(slot, "TechTimerText", "--",
            new Vector2(0.20f, 0.68f), new Vector2(72f, 20f), 12, TextGreen, TextAnchor.MiddleLeft);
        bindings.TechSlider = CreateSlider(slot, "TechProgressSlider",
            new Vector2(0.42f, 0.60f), new Vector2(96f, 10f), TextGreen);
        bindings.TechSpeed = CreateButton(slot, "TechSpeedBtn", "加速",
            new Vector2(0.68f, 0.60f), new Vector2(46f, 26f),
            new Color(0.58f, 0.40f, 0.10f, 0.96f), TextGold);
        bindings.TechStart = CreateButton(slot, "TechStartBtn", "研究",
            new Vector2(0.90f, 0.60f), new Vector2(46f, 26f),
            new Color(0.13f, 0.30f, 0.38f, 0.96f), new Color(0.80f, 1f, 0.88f, 1f));
        bindings.TechTree = CreateButton(slot, "TechTreeBtn", "查看科技树",
            new Vector2(0.50f, 0.20f), new Vector2(154f, 24f),
            new Color(0.15f, 0.20f, 0.24f, 0.96f), new Color(0.94f, 0.96f, 0.84f, 1f));
    }

    static Text CreateText(Transform parent, string name, string content, Vector2 anchor, Vector2 size,
        int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size,
        Color color, Color labelColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = EnsureImage(go, color, true);
        img.raycastTarget = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.10f, 1.05f, 0.90f, 1f);
        colors.pressedColor = new Color(0.72f, 0.66f, 0.52f, 1f);
        colors.disabledColor = new Color(0.40f, 0.40f, 0.40f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text text = CreateText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), size,
            Mathf.Clamp(Mathf.RoundToInt(size.y * 0.48f), 11, 16), labelColor, TextAnchor.MiddleCenter);
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return button;
    }

    static Slider CreateSlider(Transform parent, string name, Vector2 anchor, Vector2 size, Color fillColor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Slider slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        RectTransform bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        EnsureImage(bg, new Color(0.02f, 0.04f, 0.03f, 0.92f), true).raycastTarget = false;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform areaRt = fillArea.AddComponent<RectTransform>();
        areaRt.anchorMin = Vector2.zero;
        areaRt.anchorMax = Vector2.one;
        areaRt.offsetMin = new Vector2(2f, 2f);
        areaRt.offsetMax = new Vector2(-2f, -2f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        EnsureImage(fill, fillColor, true).raycastTarget = false;

        slider.fillRect = fillRt;
        slider.targetGraphic = fill.GetComponent<Image>();
        return slider;
    }

    static void CreateIcon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : BuiltinSprite();
        img.color = sprite != null ? Color.white : new Color(0.76f, 0.65f, 0.34f, 0.90f);
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    static void AddTopLine(Transform parent)
    {
        GameObject line = new GameObject("TopLine");
        line.transform.SetParent(parent, false);
        RectTransform rt = line.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, -2f);
        rt.offsetMax = Vector2.zero;
        EnsureImage(line, LineColor, true).raycastTarget = false;
    }

    static Image EnsureImage(GameObject go, Color color, bool useBuiltinSprite)
    {
        Image img = go.GetComponent<Image>();
        if (img == null)
            img = go.AddComponent<Image>();
        if (useBuiltinSprite && img.sprite == null)
        {
            img.sprite = BuiltinSprite();
            img.type = Image.Type.Sliced;
        }
        img.color = color;
        return img;
    }

    static Sprite BuiltinSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    static RectTransform EnsureRect(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        return rt != null ? rt : go.AddComponent<RectTransform>();
    }

    static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    static void BindLobbyManager(GameObject canvas, GameObject hall, RightPanelBindings bindings)
    {
        LobbyManager manager = Object.FindObjectOfType<LobbyManager>();
        if (manager == null)
        {
            GameObject managerGo = new GameObject("LobbyManager");
            manager = managerGo.AddComponent<LobbyManager>();
        }

        manager.HallPanel = hall;
        manager.TaskTitleTexts = bindings.TaskTitles;
        manager.TaskProgTexts = bindings.TaskProgress;
        manager.TaskProgSliders = bindings.TaskSliders;
        manager.TaskClaimButtons = bindings.TaskClaims;
        manager.MoreTasksBtn = bindings.MoreTasks;
        manager.TechButton = bindings.TechButton;
        manager.TechResearchNameText = bindings.TechName;
        manager.TechResearchDescText = bindings.TechDesc;
        manager.TechTimerBarText = bindings.TechTimer;
        manager.TechProgressSlider = bindings.TechSlider;
        manager.TechSpeedBtn = bindings.TechSpeed;
        manager.TechStartBtn = bindings.TechStart;
        manager.TechTreeBtn = bindings.TechTree;

        if (manager.QuickMatchButton == null)
            manager.QuickMatchButton = FindDeepChild(canvas.transform, "QuickMatchButton")?.GetComponent<Button>();
        if (manager.CustomRoomButton == null)
            manager.CustomRoomButton = FindDeepChild(canvas.transform, "CustomRoomButton")?.GetComponent<Button>();
        if (manager.GlobalConquestButton == null)
            manager.GlobalConquestButton = FindDeepChild(canvas.transform, "GlobalConquestButton")?.GetComponent<Button>();

        EditorUtility.SetDirty(manager);
    }

    static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
