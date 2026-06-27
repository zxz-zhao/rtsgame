using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 游戏HUD管理器：顶部信息栏、单位信息、建筑生产、游戏结束面板
public class RTSHUD : MonoBehaviour
{
    public static RTSHUD Instance { get; private set; }

    [Header("顶部信息栏")]
    public Text GoldText;
    public Text PopText;
    public Text KillText;
    public Text PowerText;

    [Header("选中单位面板")]
    public GameObject UnitInfoPanel;
    public Text UnitNameText;
    public Slider UnitHPBar;
    public Text UnitHPText;
    public Button SkillButton;

    [Header("建筑生产面板")]
    public GameObject BuildingPanel;
    public Text BuildingNameText;
    public Slider BuildingHPBar;
    public Text BuildingHPText;
    public Slider ProductionBar;
    public Text ProductionText;
    public Button[] ProductionButtons;

    [Header("游戏结束面板")]
    public GameObject GameOverPanel;
    public Text GameOverText;
    public Text GameOverStatsText;
    public Button RestartButton;
    public Button LobbyButton;
    public Button MenuButton;

    [Header("战局菜单")]
    public GameObject PausePanel;
    public Button ResumeButton;
    public Button SurrenderButton;
    public Button PauseLobbyButton;

    [Header("游戏内设置")]
    public Button GameSettingsButton;
    public GameObject GameSettingsPanel;
    public Slider MasterVolumeSlider;
    public Text MasterVolumeValueText;
    public Slider SfxVolumeSlider;
    public Text SfxVolumeValueText;
    public Button GameSettingsCloseButton;
    public Button GameSettingsReturnLobbyButton;

    [Header("计时显示")]
    public Text GameTimerText;

    [Header("小地图")]
    public RawImage   MinimapImage;
    public RectTransform MinimapCameraRect;

    [Header("警报提示")]
    public Text AlertText;          // 尅中顶部的警报文本
    public float AlertDuration = 3f;

    [Header("建筑Prefab（快捷键 + 移动端按钮用）")]
    public GameObject BarracksPrefab;
    public GameObject AirFactoryPrefab;
    public GameObject AirfieldPrefab;
    public GameObject TankFactoryPrefab;
    public GameObject ArmorFactoryPrefab;
    public GameObject TurretPrefab;
    public GameObject GoldMinePrefab;
    public GameObject PowerPlantPrefab;
    public GameObject NavalYardPrefab;

    [Header("建造面板（移动端）")]
    public GameObject BuildMenuPanel;     // 折叠/展开的建造菜单
    public Button     BuildMenuToggle;    // 底部建造按钮（展开/收起）
    public Button[]   BuildButtons;       // 建造按钮：兵工厂/飞机厂/停机场/特需厂/坦克厂/炮塔/金矿/电厂/船坞
    const int BuildButtonCount = 9;
    static readonly string[] BuildButtonLabels = { "兵工厂", "飞机厂", "停机场", "\u7279\u9700\u5382", "坦克厂", "炮塔", "金矿", "电厂", "船坞" };
    static readonly Color[] BuildButtonColors = {
        new Color(0.18f, 0.42f, 0.18f, 0.96f),
        new Color(0.10f, 0.38f, 0.58f, 0.96f),
        new Color(0.18f, 0.34f, 0.58f, 0.96f),
        new Color(0.42f, 0.28f, 0.08f, 0.96f),
        new Color(0.28f, 0.34f, 0.12f, 0.96f),
        new Color(0.48f, 0.12f, 0.12f, 0.96f),
        new Color(0.50f, 0.44f, 0.04f, 0.96f),
        new Color(0.38f, 0.20f, 0.52f, 0.96f),
        new Color(0.05f, 0.44f, 0.56f, 0.96f)
    };
    private const string BuildButtonArtObjectName = "_BuildButtonArt";
    private const int BuildButtonArtTextureWidth = 144;
    private const int BuildButtonArtTextureHeight = 84;
    private static Sprite[] _buildButtonArtSprites;
    private static readonly string[] BuildButtonBackdropNames = {
        "build_barracks", "build_air_factory", "build_airfield", "build_special_factory",
        "build_tank_factory", "build_turret", "build_gold_mine", "build_power_plant", "build_naval_yard"
    };
    private bool bBuildMenuOpen = false;
    private int _activeBuildCategory = 0;
    private Button[] _buildCategoryButtons;
    private Text _buildEmptyText;
    private static readonly string[] BuildCategoryNames = { "建筑", "战斗", "经济", "海上", "空军", "陆地" };
    // 建造弹窗网格与容器尺寸常量，生成器和运行时都复用这一套基准。
    private const int BuildCategoryColumnCount = 4;
    private const float BuildCategoryButtonWidth = 144f;
    private const float BuildCategoryButtonHeight = 84f;
    private const float BuildCategoryButtonStepX = 154f;
    private const float BuildCategoryButtonStepY = 96f;
    private const float BuildCategoryGridStartX = -231f;
    private const float BuildCategoryGridStartY = 90f;
    private static readonly Vector2 BuildMenuPopupSize = new Vector2(676f, 426f);
    private static readonly Vector2 BuildMenuPopupPosition = Vector2.zero;
    private static readonly Vector2 BuildMenuBackdropSize = new Vector2(676f, 426f);
    private static readonly Vector2 BuildMenuFeatureGroupSize = new Vector2(640f, 398f);
    private static readonly Vector2 BuildMenuContentFrameSize = new Vector2(640f, 292f);
    // 左下角战斗操作区布局常量，保证小地图、建造入口和科技入口始终对齐。
    private const float BattleHudMinimapLeft = 10f;
    private const float BattleHudMinimapBottom = 68f;
    private const float BattleHudMinimapWidth = 260f;
    private const float BattleHudMinimapHeight = 195f;
    private const float BattleHudActionGap = 10f;
    private const float BattleHudActionButtonLeft = BattleHudMinimapLeft;
    private const float BattleHudActionButtonWidth = 136f;
    private const float BattleHudActionButtonHeight = 42f;
    private const float BattleHudTechButtonBottom = BattleHudMinimapBottom + BattleHudMinimapHeight + BattleHudActionGap;
    private const float BattleHudBuildButtonBottom = BattleHudTechButtonBottom + BattleHudActionButtonHeight + BattleHudActionGap;
    private const float BattleHudChatDockLeft = BattleHudActionButtonLeft + 6f;
    private const float BattleHudChatDockBottom = 382f;
    private const float BattleHudChatDockSize = 44f;
    private static readonly Vector2 UnitPanelPreferredSize = new Vector2(248f, 150f);
    private static readonly Vector2 BuildingPanelPreferredSize = new Vector2(414f, 300f);
    private const float ProductionButtonWidth = 120f;
    private const float ProductionButtonHeight = 58f;
    private const float ProductionButtonGapX = 10f;
    private const float ProductionButtonGapY = 8f;
    private const float ProductionButtonLeft = 18f;
    private const float ProductionButtonMinTop = 126f;
    private const float ProductionButtonBottomPadding = 20f;
    private const int ProductionButtonFontSize = 13;
    private const int ProductionButtonMinFontSize = 8;
    private const int ProductionQueueVisibleSlots = 8;
    private const float ProductionQueueSlotSize = 38f;
    private const float ProductionQueueSlotGap = 4f;
    private const float ProductionQueueMinTop = 132f;
    private const float ProductionQueueGapToButtons = 18f;
    private GameObject TechPanel;
    private bool _techPanelOpen = false;
    private Vector2 _lastTechPanelCanvasSize = Vector2.zero;
    private const float TechPanelMaxWidth = 560f;
    private const float TechPanelMaxHeight = 320f;
    private const float TechPanelMinWidth = 300f;
    private const float TechPanelMinHeight = 220f;
    private struct BattleTechSpec
    {
        public string Id;
        public string Name;
        public string Glyph;
        public string BackgroundSpritePath;
        public float Radius;
        public float Duration;
        public float Cooldown;
        public float MoveMultiplier;
        public float DamageMultiplier;
        public float AttackRangeBonus;
        public float AttackIntervalMultiplier;
        public float DefenseReduction;
        public float SightRangeBonus;
        public float RegenPerSecond;
        public Color Color;
    }

    private BattleTechSpec[] _battleTechs;
    private Button[] _techButtons;
    private Image[] _techCooldownFills;
    private Text[] _techCooldownTexts;
    private float[] _techCooldownEnds;
    private int _techTargetingIndex = -1;
    private Vector2 _techTargetStartScreen;
    private float _techTargetStartTime;
    private GameObject _techTargetRing;
    private Renderer _techTargetRingRenderer;
    private Vector3 _techTargetPoint;
    private int _techConsumedInputFrame = -1;
    private bool _techIgnoreInitialRelease;

    private RTSPlayerState playerState;
    private List<RTSUnit> currentSelectedUnits = new List<RTSUnit>();
    private RTSBuilding currentSelectedBuilding;
    private RTSBuilding _productionBarOwner;
    private float _productionBarDisplayValue;
    private bool bPaused = false;
    private float alertTimer = 0f;
    private int   _prevGold = -1;
    private float _goldFlashTimer = 0f;
    private Color _goldFlashColor;
    private int   _prevKill = -1;
    private int   _prevPopUsed = -1;
    private int   _prevPowerCap = -1;
    private _HudVignettePulse _vignette;
    private float _baseAttackChatCooldown = 0f;
    private const float BaseLowHealthVignetteThreshold = 0.5f;
    private int   _cachedGoldRate = 0;
    private float _goldRateTimer = 0f;
    private Coroutine _gameOverAutoLobbyRoutine;
    private Coroutine _gameOverCardIntroRoutine;
    private bool _settingsOpen = false;
    private bool _skillButtonActionBound = false;
    private GameObject _rightClickCommandMenu;
    private System.Action<string> _rightClickCommandHandler;
    private GameObject BuildingUpgradePanel;
    private Text BuildingUpgradeTitleText;
    private Text BuildingUpgradeBodyText;
    private Button BuildingUpgradeConfirmButton;
    private Button BuildingUpgradeCancelButton;
    private RTSBuilding _pendingUpgradeBuilding;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        playerState = RTSPlayerState.Instance;
        var startupCanvas = GetHudCanvas();
        ResolveHudActionUiReferences(startupCanvas);
        // 编辑器未赋值时从 Resources 加载建筑Prefab（保证快捷键和按钮可用）
        if (BarracksPrefab    == null) BarracksPrefab    = Resources.Load<GameObject>("Prefabs/Barracks_P");
        if (AirFactoryPrefab  == null) AirFactoryPrefab  = Resources.Load<GameObject>("Prefabs/AirFactory_P");
        if (AirfieldPrefab    == null) AirfieldPrefab    = Resources.Load<GameObject>("Prefabs/Airfield_P");
        if (AirfieldPrefab    == null) AirfieldPrefab    = CreateRuntimeAirfieldPrefab();
        if (TankFactoryPrefab == null) TankFactoryPrefab = Resources.Load<GameObject>("Prefabs/TankFactory_P");
        if (ArmorFactoryPrefab == null) ArmorFactoryPrefab = Resources.Load<GameObject>("Prefabs/ArmorFactory_P");
        if (ArmorFactoryPrefab == null) ArmorFactoryPrefab = CreateRuntimeArmorFactoryPrefab();
        if (TurretPrefab      == null) TurretPrefab      = Resources.Load<GameObject>("Prefabs/Turret_P");
        if (GoldMinePrefab    == null) GoldMinePrefab    = Resources.Load<GameObject>("Prefabs/GoldMine_P");
        if (PowerPlantPrefab  == null) PowerPlantPrefab  = Resources.Load<GameObject>("Prefabs/PowerPlant_P");
        if (NavalYardPrefab   == null) NavalYardPrefab   = Resources.Load<GameObject>("Prefabs/NavalYard_P");
        RestartButton?.onClick.AddListener(() => GameManager.Instance?.RestartGame());
        LobbyButton?.onClick.AddListener(() => GameManager.Instance?.ReturnToLobby());
        MenuButton?.onClick.AddListener(() => GameManager.Instance?.ReturnToLobby());
        ResumeButton?.onClick.AddListener(() => TogglePauseMenu());
        SurrenderButton?.onClick.AddListener(OnSurrender);
        GameSettingsButton?.onClick.AddListener(ToggleGameSettings);
        GameSettingsCloseButton?.onClick.AddListener(CloseGameSettings);
        GameSettingsReturnLobbyButton?.onClick.AddListener(ReturnLobbyFromSettings);
        if (PauseLobbyButton != null) PauseLobbyButton.gameObject.SetActive(false);
        ApplySavedAudioSettings();

        // 建造菜单
        PrimeBuildMenuPopupUi(startupCanvas);
        if (BuildMenuToggle != null)
        {
            BuildMenuToggle.onClick.RemoveListener(OnBuildMenuToggleClicked);
            BuildMenuToggle.onClick.AddListener(OnBuildMenuToggleClicked);
        }
        WireBuildButtons();
        // 二战风 RTS 没有主动技能：永久隐藏旧场景里残留的 SkillButton（不绑定 onClick，不创建冷却圆环）
        if (SkillButton != null) SkillButton.gameObject.SetActive(false);
        HideAll();
        try
        {
            RuntimeUIPolish();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
        }
        RebuildBattleHudLayout();
        // 屏幕边缘红光 vignette
        var canvas = GetHudCanvas();
        if (canvas != null) _vignette = _HudVignettePulse.CreateOnCanvas(canvas);
        // 战场环境背景音乐（程序化合成）
        _BattleAmbientMusic.CreateOnScene();
    }

    /// <summary>提前补齐建造弹窗骨架，避免后续 HUD 流程中断时退回旧横排面板。</summary>
    void PrimeBuildMenuPopupUi(Canvas canvas)
    {
        if (canvas == null)
            canvas = GetHudCanvas();
        if (canvas == null)
            return;

        ResolveHudActionUiReferences(canvas);
        ApplyBuildMenuPopupLayout();
        if (BuildMenuPanel != null)
            BuildMenuPanel.SetActive(bBuildMenuOpen);
    }

    void OnDisable()
    {
        StopVoiceCapture();
    }

    Canvas GetHudCanvas()
    {
        var parentCanvas = GetComponentInParent<Canvas>();
        if (IsScreenHudCanvas(parentCanvas))
            return parentCanvas;

        var namedHud = GameObject.Find("HUDCanvas");
        var namedCanvas = namedHud != null ? namedHud.GetComponent<Canvas>() : null;
        if (IsScreenHudCanvas(namedCanvas))
            return namedCanvas;

        var canvases = FindObjectsOfType<Canvas>();
        Canvas firstScreenCanvas = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!IsScreenHudCanvas(canvas)) continue;
            if (canvas.name == "HUDCanvas")
                return canvas;
            if (firstScreenCanvas == null)
                firstScreenCanvas = canvas;
        }

        return firstScreenCanvas;
    }

    bool IsScreenHudCanvas(Canvas canvas)
    {
        return canvas != null && canvas.renderMode != RenderMode.WorldSpace;
    }

    /// <summary>战场 HUD 完整重新布局：顶部指挥官栏 + 左下地图/建造/科技 + 底部路线。</summary>
    void RebuildBattleHudLayout()
    {
        var canvas = GetHudCanvas();
        if (canvas == null) return;

        // ── 1. TopBar 拉到屏幕顶部全宽 ────────────────────────
        var topBarGO = GoldText?.transform.parent?.gameObject;
        if (topBarGO != null)
        {
            var rt = topBarGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 76f);
            }
            MakeTopBarBackgroundTransparent(topBarGO.transform);
            RepositionTopBarField(GoldText,      new Vector2(0.17f, 0.56f), Vector2.zero, TextAnchor.MiddleCenter);
            RepositionTopBarField(PopText,       new Vector2(0.32f, 0.56f), Vector2.zero, TextAnchor.MiddleCenter);
            RepositionTopBarField(PowerText,     new Vector2(0.64f, 0.56f), Vector2.zero, TextAnchor.MiddleCenter);
            RepositionTopBarField(GameTimerText, new Vector2(0.77f, 0.56f), Vector2.zero, TextAnchor.MiddleCenter);
            if (KillText != null) KillText.gameObject.SetActive(false);
            var oldGoldIcon = topBarGO.transform.Find("_GoldIcon");
            if (oldGoldIcon != null) oldGoldIcon.gameObject.SetActive(false);
            var oldPowerIcon = topBarGO.transform.Find("_PowerIcon");
            if (oldPowerIcon != null) oldPowerIcon.gameObject.SetActive(false);
        }

        // ── 2. 小地图：固定到左下，避免只露出一小块或被旧父节点裁剪。────
        if (MinimapImage != null)
        {
            var stalePanel = canvas.transform.Find("MinimapPanel");
            if (stalePanel != null)
                stalePanel.gameObject.SetActive(false);

            if (MinimapImage.transform.parent != canvas.transform)
                MinimapImage.transform.SetParent(canvas.transform, false);

            var rt = MinimapImage.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(BattleHudMinimapLeft, BattleHudMinimapBottom);
            rt.sizeDelta = new Vector2(BattleHudMinimapWidth, BattleHudMinimapHeight);
            MinimapImage.transform.SetAsLastSibling();
        }

        // ── 3. 左侧中部战术入口：建造 + 科技。──
        if (BuildMenuToggle != null)
        {
            if (BuildMenuToggle.transform.parent != canvas.transform)
                BuildMenuToggle.transform.SetParent(canvas.transform, false);

            var rt = BuildMenuToggle.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(BattleHudActionButtonLeft, BattleHudBuildButtonBottom);
                rt.sizeDelta = new Vector2(BattleHudActionButtonWidth, BattleHudActionButtonHeight);
            }
            StyleSideHudButton(BuildMenuToggle, bBuildMenuOpen ? "收起" : "建造", "⌂",
                new Color(0.90f, 0.68f, 0.22f, 0.95f));
            BuildMenuToggle.transform.SetAsLastSibling();
        }
        EnsureTechButton(canvas);
        EnsureBattleTechPanel(canvas);

        // ── 4. 建造菜单 Panel：恢复为底部横向建造栏 ───────────
        if (BuildMenuPanel != null)
        {
            if (BuildMenuPanel.transform.parent != canvas.transform)
                BuildMenuPanel.transform.SetParent(canvas.transform, false);

            var rt = BuildMenuPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = BuildMenuPopupPosition;
                rt.sizeDelta = BuildMenuPopupSize;
            }
            ApplyBuildMenuPopupLayout();
        }

        // ── 5. 底部路线快捷栏（新建）───────────────────────
        LayoutSelectionPanels(canvas);
        EnsureCommandBar(canvas);
        // ── 6. 战场设置入口：音量、继续、返回大厅。────────────
        EnsureGameSettingsUi(canvas);
        // ── 7. 战场内不显示旧返回大厅浮钮，避免误触；设置内提供返回大厅。─────
        RemoveReturnLobbyButton(canvas);
        if (PauseLobbyButton != null) PauseLobbyButton.gameObject.SetActive(false);
        // ── 8. 战地通讯：固定在建造入口上方，可发文字和队伍语聊 ─────
        EnsureChatPanel(canvas);
        RestoreHudActionUiStacking(canvas);
    }

    /// <summary>尝试从场景中回收现有战斗入口；缺失时再补建运行时版本。</summary>
    void ResolveHudActionUiReferences(Canvas canvas)
    {
        if (canvas == null) return;

        if (BuildMenuPanel == null)
        {
            var panel = FindUiTransform(canvas.transform, "BuildMenuPanel");
            if (panel != null) BuildMenuPanel = panel.gameObject;
            else BuildMenuPanel = CreateRuntimeBuildMenuPanel(canvas.transform);
        }
        NormalizeBuildMenuForCanvas(canvas);
        if (BuildMenuToggle == null)
        {
            BuildMenuToggle = FindUiComponent<Button>(canvas.transform, "BuildMenuToggle");
            if (BuildMenuToggle == null)
                BuildMenuToggle = CreateRuntimeBuildToggle(canvas.transform);
        }

        ResolveBuildingProductionUiReferences(canvas);

        bool needsBuildButtons = BuildButtons == null || BuildButtons.Length < BuildButtonCount;
        if (!needsBuildButtons)
        {
            for (int i = 0; i < BuildButtonCount; i++)
            {
                if (BuildButtons[i] == null)
                {
                    needsBuildButtons = true;
                    break;
                }
            }
        }
        if (needsBuildButtons)
        {
            var buttons = new Button[BuildButtonCount];
            for (int i = 0; i < BuildButtonCount; i++)
            {
                if (BuildButtons != null && i < BuildButtons.Length)
                    buttons[i] = BuildButtons[i];
                if (buttons[i] != null) continue;
                var button = FindUiComponent<Button>(canvas.transform, "BuildButton" + i);
                if (button != null) buttons[i] = button;
            }
            if (BuildMenuPanel != null)
            {
                for (int i = 0; i < BuildButtonCount; i++)
                {
                    if (buttons[i] == null)
                        buttons[i] = CreateRuntimeBuildButton(BuildMenuPanel.transform, i);
                }
            }
            BuildButtons = buttons;
        }
    }

    /// <summary>确保建造弹窗始终挂在屏幕 HUD Canvas 下，避免被世界空间 Canvas 带到地形里。</summary>
    void NormalizeBuildMenuForCanvas(Canvas canvas)
    {
        if (canvas == null || BuildMenuPanel == null) return;

        var rt = BuildMenuPanel.GetComponent<RectTransform>();
        if (rt == null)
        {
            BuildMenuPanel.SetActive(false);
            BuildMenuPanel = CreateRuntimeBuildMenuPanel(canvas.transform);
            rt = BuildMenuPanel.GetComponent<RectTransform>();
        }

        if (BuildMenuPanel.transform.parent != canvas.transform)
            BuildMenuPanel.transform.SetParent(canvas.transform, false);
        BuildMenuPanel.layer = canvas.gameObject.layer;

        if (BuildMenuPanel.GetComponent<CanvasRenderer>() == null)
            BuildMenuPanel.AddComponent<CanvasRenderer>();
        if (BuildMenuPanel.GetComponent<Image>() == null)
            BuildMenuPanel.AddComponent<Image>();

        LayoutBuildMenuPanelRoot();

        if (BuildButtons == null || BuildButtons.Length != BuildButtonCount)
        {
            var buttons = new Button[BuildButtonCount];
            for (int i = 0; i < BuildButtonCount; i++)
            {
                if (BuildButtons != null && i < BuildButtons.Length)
                    buttons[i] = BuildButtons[i];
            }
            BuildButtons = buttons;
        }

        for (int i = 0; i < BuildButtonCount; i++)
        {
            if (BuildButtons[i] != null && BuildButtons[i].GetComponent<RectTransform>() == null)
            {
                BuildButtons[i].gameObject.SetActive(false);
                BuildButtons[i] = null;
            }
            if (BuildButtons[i] == null)
                BuildButtons[i] = FindUiComponent<Button>(canvas.transform, "BuildButton" + i);
            if (BuildButtons[i] == null)
                BuildButtons[i] = CreateRuntimeBuildButton(BuildMenuPanel.transform, i);
        }
    }

    /// <summary>当场景里没有现成建造弹窗时，创建一个最小可用的运行时容器。</summary>
    GameObject CreateRuntimeBuildMenuPanel(Transform canvas)
    {
        var panel = new GameObject("BuildMenuPanel");
        panel.transform.SetParent(canvas, false);
        panel.SetActive(false);

        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = BuildMenuPopupPosition;
        rt.sizeDelta = BuildMenuPopupSize;

        var image = panel.AddComponent<Image>();
        image.color = new Color(0.05f, 0.07f, 0.13f, 0.96f);

        return panel;
    }

    /// <summary>创建左下角建造入口按钮，方便缺失引用时自动补齐。</summary>
    Button CreateRuntimeBuildToggle(Transform canvas)
    {
        var button = CreatePopupButton(canvas, "BuildMenuToggle", "\u5efa\u9020",
            new Vector2(0f, 0f),
            new Vector2(BattleHudActionButtonLeft, BattleHudBuildButtonBottom),
            new Vector2(BattleHudActionButtonWidth, BattleHudActionButtonHeight));
        return button;
    }

    /// <summary>按索引补齐运行时建造按钮，避免旧场景缺按钮时无法打开建造面板。</summary>
    Button CreateRuntimeBuildButton(Transform parent, int index)
    {
        var button = CreatePopupButton(parent, "BuildButton" + index,
            GetBuildButtonLabel(index),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(BuildCategoryButtonWidth, BuildCategoryButtonHeight));
        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
            rt.pivot = new Vector2(0.5f, 0.5f);
        button.gameObject.SetActive(true);
        return button;
    }

    string GetBuildButtonLabel(int index)
    {
        return index >= 0 && index < BuildButtonLabels.Length ? BuildButtonLabels[index] : "建筑";
    }

    Color GetBuildButtonColor(int index)
    {
        return index >= 0 && index < BuildButtonColors.Length
            ? BuildButtonColors[index]
            : new Color(0.18f, 0.22f, 0.13f, 0.96f);
    }

    void StyleBuildMenuStrip()
    {
        if (BuildMenuPanel == null) return;

        var bg = BuildMenuPanel.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0.05f, 0.07f, 0.13f, 0.96f);
            bg.raycastTarget = true;
        }

        var outline = BuildMenuPanel.GetComponent<Outline>() ?? BuildMenuPanel.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = new Color(0.18f, 0.42f, 0.26f, 0.80f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        EnsureBuildStripLine("BmpTopLine", new Color(0.12f, 0.22f, 0.16f, 0.85f), 2f);
        HideBuildPopupExtras();
    }

    void LayoutBuildMenuPanelRoot()
    {
        if (BuildMenuPanel == null) return;

        var rt = BuildMenuPanel.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = BuildMenuPopupPosition;
        rt.sizeDelta = BuildMenuPopupSize;
    }

    void ApplyBuildMenuPopupLayout()
    {
        if (BuildMenuPanel == null) return;
        LayoutBuildMenuPanelRoot();

        var bg = BuildMenuPanel.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0f, 0f, 0f, 0.01f);
            bg.raycastTarget = true;
        }

        var outline = BuildMenuPanel.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        EnsureBuildMenuPopupChrome();
        SetBuildPopupExtrasActive(true);
        var contentFrame = EnsureBuildPopupContentFrame();
        if (contentFrame == null) return;

        var panelTransform = GetBuildMenuPanelTransform();
        var topLine = SafeFindBuildPopupChild(panelTransform, "BmpTopLine");
        if (topLine != null)
            SetBuildPopupTransformActive(topLine, false);

        if (BuildButtons != null)
        {
            for (int i = 0; i < BuildButtons.Length; i++)
            {
                var button = BuildButtons[i];
                if (button == null) continue;
                if (button.transform.parent != contentFrame)
                    button.transform.SetParent(contentFrame, false);

                var rt = button.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(BuildCategoryButtonWidth, BuildCategoryButtonHeight);
                }
                button.gameObject.SetActive(true);
                StyleBuildStripButton(button, i);
            }
        }

        RefreshBuildCategoryVisibility();
        BringBuildPopupChromeToFront();
    }

    void EnsureBuildStripLine(string name, Color color, float height)
    {
        if (BuildMenuPanel == null) return;
        var panelTransform = GetBuildMenuPanelTransform();
        if (panelTransform == null) return;

        var line = SafeFindBuildPopupChild(panelTransform, name);
        GameObject go = line != null ? line.gameObject : new GameObject(name);
        if (line == null)
            go.transform.SetParent(panelTransform, false);
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, -height);
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    void HideBuildPopupExtras()
    {
        SetBuildPopupExtrasActive(false);
    }

    void SetBuildPopupExtrasActive(bool active)
    {
        if (BuildMenuPanel == null) return;
        string[] names = { "_BuildPopupBackdrop", "_BuildPopupBody", "_BuildPopupTitle", "_BuildPopupClose", "_BuildCategoryTabs", "_BuildContentFrame", "_BuildEmptyText" };
        for (int i = 0; i < names.Length; i++)
        {
            var child = FindBuildPopupNode(names[i]);
            SetBuildPopupTransformActive(child, active);
        }
    }

    void BringBuildPopupChromeToFront()
    {
        if (BuildMenuPanel == null) return;
        var backdrop = FindBuildPopupNode("_BuildPopupBackdrop");
        SetBuildPopupSibling(backdrop, true);

        var body = FindBuildPopupNode("_BuildPopupBody");
        SetBuildPopupSibling(body, true);

        string[] names = { "_BuildContentFrame", "_BuildCategoryTabs", "_BuildPopupTitle", "_BuildPopupClose" };
        for (int i = 0; i < names.Length; i++)
        {
            var child = FindBuildPopupNode(names[i]);
            SetBuildPopupSibling(child, true);
        }
    }

    void StyleBuildStripButton(Button button, int index)
    {
        if (button == null) return;
        button.enabled = true;
        button.interactable = true;

        var bg = button.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = GetBuildButtonColor(index);
            bg.raycastTarget = true;
        }
        EnsureBuildButtonBackdropArt(button, index, true);

        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = Color.Lerp(GetBuildButtonColor(index), Color.white, 0.45f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var line = button.transform.Find("BtnLine");
        GameObject lineGO = line != null ? line.gameObject : new GameObject("BtnLine");
        if (line == null)
            lineGO.transform.SetParent(button.transform, false);
        lineGO.SetActive(true);
        var lineRT = lineGO.GetComponent<RectTransform>();
        if (lineRT == null) lineRT = lineGO.AddComponent<RectTransform>();
        lineRT.anchorMin = Vector2.zero;
        lineRT.anchorMax = new Vector2(1f, 0f);
        lineRT.offsetMin = Vector2.zero;
        lineRT.offsetMax = new Vector2(0f, 3f);
        var lineImg = lineGO.GetComponent<Image>();
        if (lineImg == null) lineImg = lineGO.AddComponent<Image>();
        lineImg.color = Color.Lerp(GetBuildButtonColor(index), Color.white, 0.45f);
        lineImg.raycastTarget = false;

        var icon = button.transform.Find("_BigIcon");
        if (icon != null)
            icon.gameObject.SetActive(false);

        var texts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            if (text.transform.name == "_BigIcon")
            {
                text.gameObject.SetActive(false);
                continue;
            }
            if (text.name == "PriceTag")
            {
                text.fontSize = 14;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                var priceRT = text.rectTransform;
                priceRT.anchorMin = new Vector2(0f, 0f);
                priceRT.anchorMax = new Vector2(1f, 0f);
                priceRT.pivot = new Vector2(0.5f, 0f);
                priceRT.anchoredPosition = new Vector2(0f, 1f);
                priceRT.sizeDelta = new Vector2(0f, 22f);
                continue;
            }

            text.gameObject.SetActive(true);
            text.text = GetBuildButtonLabel(index);
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.lineSpacing = 0.9f;
            text.color = new Color(0.96f, 1f, 0.96f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var labelRT = text.rectTransform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(4f, 22f);
            labelRT.offsetMax = new Vector2(-4f, -2f);
            EnsureReadableTextShadow(text, new Vector2(1f, -1f));
            EnsureReadableTextOutline(text, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.70f));
        }
        BringBuildButtonTextToFront(button);
    }

    void EnsureBuildButtonBackdropArt(Button button, int index, bool enabled)
    {
        if (button == null) return;

        var art = button.transform.Find(BuildButtonArtObjectName);
        GameObject artGO = art != null ? art.gameObject : new GameObject(BuildButtonArtObjectName);
        if (art == null)
            artGO.transform.SetParent(button.transform, false);
        artGO.SetActive(true);

        var rt = artGO.GetComponent<RectTransform>();
        if (rt == null) rt = artGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = artGO.GetComponent<Image>();
        if (image == null) image = artGO.AddComponent<Image>();
        image.sprite = GetBuildButtonArtSprite(index);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = enabled ? Color.white : new Color(0.56f, 0.56f, 0.56f, 0.68f);
        artGO.transform.SetAsFirstSibling();
    }

    void SetBuildButtonArtEnabled(Button button, bool enabled)
    {
        var image = button != null
            ? button.transform.Find(BuildButtonArtObjectName)?.GetComponent<Image>()
            : null;
        if (image != null)
            image.color = enabled ? Color.white : new Color(0.56f, 0.56f, 0.56f, 0.68f);
    }

    void BringBuildButtonTextToFront(Button button)
    {
        if (button == null) return;
        var texts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null || text.transform.name == "_BigIcon") continue;
            text.transform.SetAsLastSibling();
        }
    }

    Sprite GetBuildButtonArtSprite(int index)
    {
        if (_buildButtonArtSprites == null || _buildButtonArtSprites.Length != BuildButtonCount)
            _buildButtonArtSprites = new Sprite[BuildButtonCount];

        int safeIndex = Mathf.Clamp(index, 0, BuildButtonCount - 1);
        if (_buildButtonArtSprites[safeIndex] == null)
        {
            string backdropName = safeIndex >= 0 && safeIndex < BuildButtonBackdropNames.Length
                ? BuildButtonBackdropNames[safeIndex]
                : null;
            var downloadedSprite = LoadButtonBackdropSprite(backdropName);
            if (downloadedSprite != null)
            {
                _buildButtonArtSprites[safeIndex] = downloadedSprite;
                return downloadedSprite;
            }

            var texture = CreateBuildButtonArtTexture(safeIndex);
            var sprite = Sprite.Create(texture,
                new Rect(0f, 0f, BuildButtonArtTextureWidth, BuildButtonArtTextureHeight),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _buildButtonArtSprites[safeIndex] = sprite;
        }
        return _buildButtonArtSprites[safeIndex];
    }

    Texture2D CreateBuildButtonArtTexture(int index)
    {
        const int width = BuildButtonArtTextureWidth;
        const int height = BuildButtonArtTextureHeight;
        var pixels = new Color32[width * height];
        Color32 topGlow = new Color32(255, 236, 150, 34);
        Color32 bottomShade = new Color32(0, 0, 0, 58);
        Color32 gridBright = new Color32(255, 238, 150, 20);
        Color32 gridDark = new Color32(0, 0, 0, 22);

        for (int y = 0; y < height; y++)
        {
            byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(12f, 48f, 1f - (float)y / Mathf.Max(1, height - 1)));
            DrawRect(pixels, width, height, 0, y, width, 1, new Color32(0, 0, 0, shade));
        }

        DrawRect(pixels, width, height, 0, 0, width, 23, bottomShade);
        DrawRect(pixels, width, height, 0, height - 5, width, 5, topGlow);
        for (int x = -height; x < width + height; x += 18)
            DrawLine(pixels, width, height, x, 0, x + height, height, gridBright, 1);
        for (int x = 0; x < width + height; x += 32)
            DrawLine(pixels, width, height, x, height, x - height, 0, gridDark, 1);

        DrawBuildButtonIcon(pixels, width, height, index, 4, -3, new Color32(0, 0, 0, 92));
        DrawBuildButtonIcon(pixels, width, height, index, 0, 0, new Color32(255, 238, 168, 128));
        DrawBuildButtonIconAccent(pixels, width, height, index);

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    static void DrawBuildButtonIcon(Color32[] pixels, int width, int height, int index, int ox, int oy, Color32 color)
    {
        switch (index)
        {
            case 0:
                DrawTriangle(pixels, width, height, 24 + ox, 51 + oy, 72 + ox, 69 + oy, 120 + ox, 51 + oy, color);
                DrawRect(pixels, width, height, 30 + ox, 27 + oy, 84, 25, color);
                DrawRect(pixels, width, height, 42 + ox, 52 + oy, 54, 9, color);
                DrawRect(pixels, width, height, 65 + ox, 27 + oy, 14, 18, color);
                DrawRect(pixels, width, height, 99 + ox, 34 + oy, 8, 18, color);
                break;
            case 1:
                DrawRect(pixels, width, height, 24 + ox, 28 + oy, 92, 25, color);
                DrawTriangle(pixels, width, height, 24 + ox, 53 + oy, 70 + ox, 70 + oy, 116 + ox, 53 + oy, color);
                DrawLine(pixels, width, height, 36 + ox, 42 + oy, 108 + ox, 42 + oy, color, 4);
                DrawLine(pixels, width, height, 62 + ox, 58 + oy, 108 + ox, 34 + oy, color, 5);
                DrawTriangle(pixels, width, height, 78 + ox, 49 + oy, 58 + ox, 38 + oy, 88 + ox, 39 + oy, color);
                break;
            case 2:
                DrawLine(pixels, width, height, 28 + ox, 25 + oy, 114 + ox, 63 + oy, color, 12);
                DrawLine(pixels, width, height, 34 + ox, 28 + oy, 108 + ox, 60 + oy, new Color32(color.r, color.g, color.b, (byte)(color.a / 2)), 3);
                DrawLine(pixels, width, height, 72 + ox, 59 + oy, 102 + ox, 42 + oy, color, 4);
                DrawTriangle(pixels, width, height, 93 + ox, 46 + oy, 111 + ox, 43 + oy, 98 + ox, 54 + oy, color);
                DrawTriangle(pixels, width, height, 84 + ox, 50 + oy, 70 + ox, 66 + oy, 91 + ox, 55 + oy, color);
                break;
            case 3:
                DrawRect(pixels, width, height, 28 + ox, 28 + oy, 88, 22, color);
                DrawCircle(pixels, width, height, 72 + ox, 52 + oy, 15, color);
                DrawLine(pixels, width, height, 72 + ox, 52 + oy, 72 + ox, 72 + oy, color, 3);
                DrawCircle(pixels, width, height, 92 + ox, 66 + oy, 8, color);
                DrawLine(pixels, width, height, 72 + ox, 68 + oy, 92 + ox, 66 + oy, color, 3);
                break;
            case 4:
                DrawRect(pixels, width, height, 26 + ox, 27 + oy, 94, 23, color);
                DrawRect(pixels, width, height, 38 + ox, 50 + oy, 18, 18, color);
                DrawRect(pixels, width, height, 64 + ox, 50 + oy, 24, 14, color);
                DrawRect(pixels, width, height, 96 + ox, 44 + oy, 12, 24, color);
                DrawRect(pixels, width, height, 39 + ox, 22 + oy, 62, 10, color);
                break;
            case 5:
                DrawRect(pixels, width, height, 52 + ox, 24 + oy, 38, 13, color);
                DrawRect(pixels, width, height, 60 + ox, 37 + oy, 22, 14, color);
                DrawCircle(pixels, width, height, 71 + ox, 55 + oy, 15, color);
                DrawLine(pixels, width, height, 80 + ox, 61 + oy, 119 + ox, 72 + oy, color, 6);
                DrawRect(pixels, width, height, 62 + ox, 20 + oy, 18, 8, color);
                break;
            case 6:
                DrawTriangle(pixels, width, height, 28 + ox, 27 + oy, 58 + ox, 66 + oy, 90 + ox, 27 + oy, color);
                DrawTriangle(pixels, width, height, 62 + ox, 27 + oy, 92 + ox, 60 + oy, 121 + ox, 27 + oy, color);
                DrawRect(pixels, width, height, 45 + ox, 24 + oy, 52, 12, color);
                DrawCircle(pixels, width, height, 55 + ox, 22 + oy, 5, color);
                DrawCircle(pixels, width, height, 87 + ox, 22 + oy, 5, color);
                break;
            case 7:
                DrawTriangle(pixels, width, height, 35 + ox, 25 + oy, 49 + ox, 67 + oy, 67 + ox, 25 + oy, color);
                DrawTriangle(pixels, width, height, 78 + ox, 25 + oy, 96 + ox, 67 + oy, 110 + ox, 25 + oy, color);
                DrawRect(pixels, width, height, 38 + ox, 23 + oy, 74, 10, color);
                DrawLine(pixels, width, height, 75 + ox, 69 + oy, 64 + ox, 49 + oy, color, 5);
                DrawLine(pixels, width, height, 64 + ox, 49 + oy, 80 + ox, 49 + oy, color, 5);
                DrawLine(pixels, width, height, 80 + ox, 49 + oy, 68 + ox, 29 + oy, color, 5);
                break;
            default:
                DrawLine(pixels, width, height, 24 + ox, 28 + oy, 120 + ox, 28 + oy, color, 5);
                DrawLine(pixels, width, height, 32 + ox, 22 + oy, 32 + ox, 61 + oy, color, 4);
                DrawLine(pixels, width, height, 32 + ox, 61 + oy, 70 + ox, 49 + oy, color, 4);
                DrawTriangle(pixels, width, height, 58 + ox, 34 + oy, 94 + ox, 34 + oy, 111 + ox, 50 + oy, color);
                DrawRect(pixels, width, height, 48 + ox, 30 + oy, 52, 10, color);
                break;
        }
    }

    static void DrawBuildButtonIconAccent(Color32[] pixels, int width, int height, int index)
    {
        Color32 accent = new Color32(255, 236, 124, 64);
        switch (index)
        {
            case 2:
                DrawLine(pixels, width, height, 42, 31, 102, 58, accent, 2);
                break;
            case 6:
                DrawCircle(pixels, width, height, 73, 50, 7, accent);
                break;
            case 7:
                DrawLine(pixels, width, height, 75, 69, 65, 50, accent, 3);
                DrawLine(pixels, width, height, 65, 50, 80, 50, accent, 3);
                DrawLine(pixels, width, height, 80, 50, 69, 31, accent, 3);
                break;
            default:
                DrawRect(pixels, width, height, 24, 24, 96, 2, accent);
                break;
        }
    }

    static void DrawRect(Color32[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, Color32 color)
    {
        for (int yy = y; yy < y + rectHeight; yy++)
        {
            for (int xx = x; xx < x + rectWidth; xx++)
                BlendPixel(pixels, width, height, xx, yy, color);
        }
    }

    static void DrawCircle(Color32[] pixels, int width, int height, int cx, int cy, int radius, Color32 color)
    {
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= r2)
                    BlendPixel(pixels, width, height, cx + x, cy + y, color);
            }
        }
    }

    static void DrawLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 color, int thickness)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0)
        {
            DrawRect(pixels, width, height, x0, y0, thickness, thickness, color);
            return;
        }

        int half = Mathf.Max(0, thickness / 2);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            DrawRect(pixels, width, height, x - half, y - half, thickness, thickness, color);
        }
    }

    static void DrawTriangle(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, int x2, int y2, Color32 color)
    {
        int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
        int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
        int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
        int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));
        float area = EdgeFunction(x0, y0, x1, y1, x2, y2);
        if (Mathf.Approximately(area, 0f)) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float w0 = EdgeFunction(x1, y1, x2, y2, x, y);
                float w1 = EdgeFunction(x2, y2, x0, y0, x, y);
                float w2 = EdgeFunction(x0, y0, x1, y1, x, y);
                if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                    BlendPixel(pixels, width, height, x, y, color);
            }
        }
    }

    static float EdgeFunction(int ax, int ay, int bx, int by, int cx, int cy)
    {
        return (cx - ax) * (by - ay) - (cy - ay) * (bx - ax);
    }

    static void BlendPixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || color.a == 0) return;

        int index = y * width + x;
        Color32 dst = pixels[index];
        float srcA = color.a / 255f;
        float dstA = dst.a / 255f;
        float outA = srcA + dstA * (1f - srcA);
        if (outA <= 0f) return;

        byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((color.r * srcA + dst.r * dstA * (1f - srcA)) / outA), 0, 255);
        byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((color.g * srcA + dst.g * dstA * (1f - srcA)) / outA), 0, 255);
        byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((color.b * srcA + dst.b * dstA * (1f - srcA)) / outA), 0, 255);
        byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255);
        pixels[index] = new Color32(r, g, b, a);
    }

    void ResolveBuildingProductionUiReferences(Canvas canvas)
    {
        if (canvas == null) return;

        if (BuildingPanel == null)
        {
            var panel = FindUiTransform(canvas.transform, "BuildingPanel");
            if (panel != null) BuildingPanel = panel.gameObject;
        }

        if (BuildingNameText == null) BuildingNameText = FindUiComponent<Text>(canvas.transform, "BuildingNameText");
        if (BuildingHPBar == null) BuildingHPBar = FindUiComponent<Slider>(canvas.transform, "BuildingHPBar");
        if (BuildingHPText == null) BuildingHPText = FindUiComponent<Text>(canvas.transform, "BuildingHPText");
        if (BuildingHPText == null && BuildingPanel != null) BuildingHPText = CreateRuntimeBuildingHPText(BuildingPanel.transform);
        if (ProductionBar == null) ProductionBar = FindUiComponent<Slider>(canvas.transform, "ProductionBar");
        if (ProductionText == null) ProductionText = FindUiComponent<Text>(canvas.transform, "ProductionText");

        bool needsProductionButtons = ProductionButtons == null || ProductionButtons.Length < 6;
        if (!needsProductionButtons)
        {
            for (int i = 0; i < ProductionButtons.Length; i++)
            {
                if (ProductionButtons[i] == null)
                {
                    needsProductionButtons = true;
                    break;
                }
            }
        }

        if (needsProductionButtons)
        {
            var buttons = new List<Button>(6);
            for (int i = 0; i < 6; i++)
            {
                var button = FindUiComponent<Button>(canvas.transform, "ProductionButton" + i);
                if (button == null && BuildingPanel != null)
                    button = CreateRuntimeProductionButton(BuildingPanel.transform, i);
                if (button != null) buttons.Add(button);
            }

            if (buttons.Count > 0)
                ProductionButtons = buttons.ToArray();
        }
    }

    Text CreateRuntimeBuildingHPText(Transform parent)
    {
        var go = new GameObject("BuildingHPText");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 20f);

        var text = go.AddComponent<Text>();
        text.text = "---/---";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleRight;
        text.color = new Color(0.82f, 1f, 0.78f);
        text.raycastTarget = false;
        EnsureReadableTextShadow(text, new Vector2(1f, -1f));
        return text;
    }

    Button CreateRuntimeProductionButton(Transform parent, int index)
    {
        var go = new GameObject("ProductionButton" + index);
        go.transform.SetParent(parent, false);
        go.SetActive(false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ProductionButtonWidth, ProductionButtonHeight);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.22f, 0.32f, 0.96f);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.42f, 0.70f, 0.95f, 0.72f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6f, 4f);
        textRt.offsetMax = new Vector2(-6f, -4f);

        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "";
        ConfigureProductionButtonText(text, new Vector2(6f, 4f));
        text.color = new Color(1f, 0.92f, 0.55f);

        return button;
    }

    static Transform FindUiTransform(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    static T FindUiComponent<T>(Transform root, string name) where T : Component
    {
        var transform = FindUiTransform(root, name);
        return transform != null ? transform.GetComponent<T>() : null;
    }

    void RestoreHudActionUiStacking(Canvas canvas)
    {
        if (canvas == null) return;

        var chat = canvas.transform.Find("_ChatPanel");
        if (chat != null)
            chat.SetAsLastSibling();

        if (BuildMenuToggle != null)
            BuildMenuToggle.transform.SetAsLastSibling();

        var tech = canvas.transform.Find("_TechButton");
        if (tech != null)
            tech.SetAsLastSibling();

        if (GameSettingsButton != null)
            GameSettingsButton.transform.SetAsLastSibling();

        var commandBar = canvas.transform.Find("_CommandBar");
        if (commandBar != null)
            commandBar.SetAsLastSibling();

        if (TechPanel != null && TechPanel.activeSelf)
            TechPanel.transform.SetAsLastSibling();

        if (BuildMenuPanel != null && BuildMenuPanel.activeSelf)
            BuildMenuPanel.transform.SetAsLastSibling();

        if (GameSettingsPanel != null && GameSettingsPanel.activeSelf)
            GameSettingsPanel.transform.SetAsLastSibling();

        if (_rightClickCommandMenu != null && _rightClickCommandMenu.activeSelf)
            _rightClickCommandMenu.transform.SetAsLastSibling();

        var chatDock = canvas.transform.Find("_ChatDockButton");
        if (chatDock != null)
            chatDock.SetAsLastSibling();
    }

    void OnBuildMenuToggleClicked()
    {
        _UiClickAudio.PlayClick();
        ToggleBuildMenu();
    }

    void RepositionTopBarField(Text field, Vector2 anchor, Vector2 offset, TextAnchor align)
    {
        if (field == null) return;
        field.gameObject.SetActive(true);
        var rt = field.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        float width = 140f;
        if (field == GoldText) width = 230f;
        else if (field == PowerText) width = 160f;
        else if (field == GameTimerText) width = 110f;
        else if (field == PopText) width = 150f;
        rt.sizeDelta = new Vector2(width, 32f);
        field.alignment = align;
        field.fontSize = 22;
        field.fontStyle = FontStyle.Bold;
        field.color = field == GoldText ? new Color(1f, 0.88f, 0.22f) :
            field == PopText ? new Color(0.70f, 0.92f, 1f) :
            field == PowerText ? new Color(0.50f, 1f, 0.62f) :
            new Color(0.94f, 0.97f, 1f);
        var shadow = field.GetComponent<Shadow>() ?? field.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
        shadow.effectDistance = new Vector2(1.8f, -1.8f);
    }

    void EnsureResourceIcon(Transform parent, string name, Vector2 anchor, Vector2 offset, string glyph, Color color)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
            go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(28f, 28f);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.03f, 0.05f, 0.06f, 0.52f);
        img.raycastTarget = false;
        var ol = go.GetComponent<Outline>();
        if (ol == null) ol = go.AddComponent<Outline>();
        ol.enabled = true;
        ol.effectColor = new Color(0.20f, 0.32f, 0.42f, 0.70f);
        ol.effectDistance = new Vector2(1f, -1f);

        var spriteGO = go.transform.Find("SpriteIcon")?.gameObject;
        if (spriteGO != null) spriteGO.SetActive(false);
        // 文字 glyph
        var glyphGO = go.transform.Find("Glyph")?.gameObject;
        if (glyphGO == null)
        {
            glyphGO = new GameObject("Glyph");
            glyphGO.transform.SetParent(go.transform, false);
            glyphGO.AddComponent<RectTransform>();
            glyphGO.AddComponent<Text>();
        }
        glyphGO.SetActive(true);
        var trt = glyphGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = glyphGO.GetComponent<Text>();
        if (t == null) t = glyphGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = glyph;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
    }

    void LayoutBuildButtonTextLayers(Button button, string iconGlyph, Text priceText)
    {
        if (button == null) return;

        var icon = button.transform.Find("_BigIcon")?.GetComponent<Text>();
        if (icon == null)
        {
            var iconGO = new GameObject("_BigIcon");
            iconGO.transform.SetParent(button.transform, false);
            icon = iconGO.AddComponent<Text>();
            icon.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (icon.font == null) icon.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            icon.raycastTarget = false;
        }

        icon.text = iconGlyph;
        icon.fontSize = 30;
        icon.fontStyle = FontStyle.Bold;
        icon.alignment = TextAnchor.MiddleCenter;
        icon.color = new Color(1f, 0.94f, 0.62f);
        icon.horizontalOverflow = HorizontalWrapMode.Overflow;
        icon.verticalOverflow = VerticalWrapMode.Overflow;
        EnsureReadableTextShadow(icon, new Vector2(1.4f, -1.4f));
        EnsureReadableTextOutline(icon, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.72f));
        LayoutButtonTextRect(icon.rectTransform, 0.58f, 1f, 0f, 0f);

        if (priceText == null)
            priceText = button.transform.Find("PriceTag")?.GetComponent<Text>();
        if (priceText != null)
        {
            priceText.fontSize = 16;
            priceText.fontStyle = FontStyle.Bold;
            priceText.alignment = TextAnchor.MiddleCenter;
            priceText.raycastTarget = false;
            priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceText.verticalOverflow = VerticalWrapMode.Overflow;
            EnsureReadableTextShadow(priceText, new Vector2(1.2f, -1.2f));
            EnsureReadableTextOutline(priceText, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.78f));
            LayoutButtonTextRect(priceText.rectTransform, 0f, 0.25f, 0f, 0f);
        }

        var texts = button.GetComponentsInChildren<Text>(true);
        for (int j = 0; j < texts.Length; j++)
        {
            var text = texts[j];
            if (text == null || text == icon || text == priceText) continue;
            text.text = NormalizeBuildButtonLabel(text.text);
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.lineSpacing = 0.9f;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.96f, 1f, 0.96f);
            EnsureReadableTextShadow(text, new Vector2(1.4f, -1.4f));
            EnsureReadableTextOutline(text, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.82f));
            LayoutButtonTextRect(text.rectTransform, 0.25f, 0.58f, 4f, 2f);
        }
    }

    string NormalizeBuildButtonLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return "";
        int hotkeyLine = label.IndexOf('\n');
        if (hotkeyLine >= 0)
            label = label.Substring(0, hotkeyLine);
        return label.Replace(" ", "");
    }

    void LayoutButtonTextRect(RectTransform rt, float bottom, float top, float leftRightPadding, float bottomPadding)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, bottom);
        rt.anchorMax = new Vector2(1f, top);
        rt.offsetMin = new Vector2(leftRightPadding, bottomPadding);
        rt.offsetMax = new Vector2(-leftRightPadding, 0f);
    }

    /// <summary>统一创建建造弹窗的装饰层，避免旧场景和运行时生成的层级不一致。</summary>
    void EnsureBuildMenuPopupChrome()
    {
        if (BuildMenuPanel == null) return;

        var panelImage = BuildMenuPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0f, 0f, 0f, 0.01f);
            panelImage.raycastTarget = true;
        }

        var panelOutline = BuildMenuPanel.GetComponent<Outline>();
        if (panelOutline != null)
            panelOutline.enabled = false;

        EnsureBuildPopupBackdrop();
        EnsureBuildPopupTitle();
        EnsureBuildPopupCloseButton();
        EnsureBuildCategoryTabs();
        EnsureBuildPopupContentFrame();
        EnsureBuildEmptyLabel();
    }

    Transform FindBuildPopupNode(string name)
    {
        if (BuildMenuPanel == null || string.IsNullOrEmpty(name)) return null;
        var panelTransform = GetBuildMenuPanelTransform();
        if (panelTransform == null) return null;

        var direct = SafeFindBuildPopupChild(panelTransform, name);
        if (direct != null) return direct;

        var backdrop = SafeFindBuildPopupChild(panelTransform, "_BuildPopupBackdrop");
        if (backdrop != null)
        {
            var nested = SafeFindBuildPopupChild(backdrop, name);
            if (nested != null) return nested;

            var body = SafeFindBuildPopupChild(backdrop, "_BuildPopupBody");
            if (body != null)
            {
                var bodyNested = SafeFindBuildPopupChild(body, name);
                if (bodyNested != null) return bodyNested;

                var contentFrame = SafeFindBuildPopupChild(body, "_BuildContentFrame");
                if (contentFrame != null)
                {
                    var deepNested = SafeFindBuildPopupChild(contentFrame, name);
                    if (deepNested != null) return deepNested;
                }
            }
            else
            {
                var contentFrame = SafeFindBuildPopupChild(backdrop, "_BuildContentFrame");
                if (contentFrame == null)
                    contentFrame = SafeFindBuildPopupChild(backdrop, name == "_BuildContentFrame" ? name : "_BuildContentFrame");
                if (contentFrame != null)
                {
                    var deepNested = SafeFindBuildPopupChild(contentFrame, name);
                    if (deepNested != null) return deepNested;
                }
            }
        }

        return null;
    }

    Transform GetBuildMenuPanelTransform()
    {
        if (BuildMenuPanel == null) return null;
        try
        {
            return BuildMenuPanel.transform;
        }
        catch (MissingReferenceException)
        {
            BuildMenuPanel = null;
            return null;
        }
    }

    bool IsBuildPopupTransformAlive(Transform transform)
    {
        try
        {
            return transform != null && transform.gameObject != null;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
    }

    Transform SafeFindBuildPopupChild(Transform parent, string name)
    {
        if (!IsBuildPopupTransformAlive(parent) || string.IsNullOrEmpty(name)) return null;
        try
        {
            var child = parent.Find(name);
            return IsBuildPopupTransformAlive(child) ? child : null;
        }
        catch (MissingReferenceException)
        {
            return null;
        }
    }

    void SetBuildPopupTransformActive(Transform transform, bool active)
    {
        if (!IsBuildPopupTransformAlive(transform)) return;
        try
        {
            transform.gameObject.SetActive(active);
        }
        catch (MissingReferenceException)
        {
        }
    }

    void SetBuildPopupSibling(Transform transform, bool last)
    {
        if (!IsBuildPopupTransformAlive(transform)) return;
        try
        {
            if (last) transform.SetAsLastSibling();
            else transform.SetAsFirstSibling();
        }
        catch (MissingReferenceException)
        {
        }
    }

    RectTransform EnsureBuildPopupBody()
    {
        var backdrop = EnsureBuildPopupBackdrop();
        if (backdrop == null) return null;
        var bodyRoot = SafeFindBuildPopupChild(backdrop, "_BuildPopupBody");
        if (bodyRoot == null)
        {
            var go = new GameObject("_BuildPopupBody");
            go.transform.SetParent(backdrop, false);
            bodyRoot = go.transform;
        }

        var rt = bodyRoot.GetComponent<RectTransform>();
        if (rt == null)
        {
            rt = bodyRoot.gameObject.AddComponent<RectTransform>();
            bodyRoot = rt.transform;
        }
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = BuildMenuFeatureGroupSize;
        return rt;
    }

    /// <summary>确保建造弹窗存在独立的外层底板，用来承载轮廓线和标题分隔线。</summary>
    RectTransform EnsureBuildPopupBackdrop()
    {
        var panelTransform = GetBuildMenuPanelTransform();
        if (panelTransform == null) return null;

        var backdropRoot = SafeFindBuildPopupChild(panelTransform, "_BuildPopupBackdrop");
        if (backdropRoot == null)
        {
            var go = new GameObject("_BuildPopupBackdrop");
            go.transform.SetParent(panelTransform, false);
            backdropRoot = go.transform;
        }

        var rt = backdropRoot.GetComponent<RectTransform>();
        if (rt == null)
        {
            rt = backdropRoot.gameObject.AddComponent<RectTransform>();
            backdropRoot = rt.transform;
        }
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = BuildMenuBackdropSize;

        var image = backdropRoot.GetComponent<Image>();
        if (image == null) image = backdropRoot.gameObject.AddComponent<Image>();
        image.color = new Color(0.035f, 0.045f, 0.025f, 0.94f);
        image.raycastTarget = true;

        var outline = backdropRoot.GetComponent<Outline>();
        if (outline == null) outline = backdropRoot.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = new Color(0.86f, 0.68f, 0.18f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);

        var headerLine = SafeFindBuildPopupChild(backdropRoot, "_HeaderLine");
        GameObject headerLineGo = headerLine != null ? headerLine.gameObject : new GameObject("_HeaderLine");
        if (headerLine == null)
            headerLineGo.transform.SetParent(backdropRoot, false);
        var headerLineRt = headerLineGo.GetComponent<RectTransform>();
        if (headerLineRt == null) headerLineRt = headerLineGo.AddComponent<RectTransform>();
        headerLineRt.anchorMin = new Vector2(0f, 1f);
        headerLineRt.anchorMax = new Vector2(1f, 1f);
        headerLineRt.pivot = new Vector2(0.5f, 1f);
        headerLineRt.anchoredPosition = new Vector2(0f, -38f);
        headerLineRt.sizeDelta = new Vector2(-22f, 1f);
        var headerLineImage = headerLineGo.GetComponent<Image>();
        if (headerLineImage == null) headerLineImage = headerLineGo.AddComponent<Image>();
        headerLineImage.color = new Color(0.84f, 0.68f, 0.20f, 0.55f);
        headerLineImage.raycastTarget = false;

        SetBuildPopupSibling(backdropRoot, false);
        return rt;
    }

    /// <summary>确保建造按钮被放进单独的内容框，避免和标题栏、分类页签互相覆盖。</summary>
    RectTransform EnsureBuildPopupContentFrame()
    {
        var body = EnsureBuildPopupBody();
        if (body == null) return null;
        var frameRoot = FindBuildPopupNode("_BuildContentFrame");
        if (frameRoot == null)
        {
            var go = new GameObject("_BuildContentFrame");
            go.transform.SetParent(body, false);
            frameRoot = go.transform;
        }
        else if (frameRoot.parent != body)
        {
            frameRoot.SetParent(body, false);
        }

        var rt = frameRoot.GetComponent<RectTransform>();
        if (rt == null)
        {
            rt = frameRoot.gameObject.AddComponent<RectTransform>();
            frameRoot = rt.transform;
        }
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -104f);
        rt.sizeDelta = BuildMenuContentFrameSize;

        var image = frameRoot.GetComponent<Image>();
        if (image == null) image = frameRoot.gameObject.AddComponent<Image>();
        image.color = new Color(0.045f, 0.055f, 0.025f, 0.96f);
        image.raycastTarget = true;

        var outline = frameRoot.GetComponent<Outline>();
        if (outline == null) outline = frameRoot.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = new Color(0.54f, 0.42f, 0.14f, 0.72f);
        outline.effectDistance = new Vector2(1.3f, -1.3f);

        var glow = SafeFindBuildPopupChild(frameRoot, "_FrameTopGlow");
        GameObject glowGo = glow != null ? glow.gameObject : new GameObject("_FrameTopGlow");
        if (glow == null)
            glowGo.transform.SetParent(frameRoot, false);
        var glowRt = glowGo.GetComponent<RectTransform>();
        if (glowRt == null) glowRt = glowGo.AddComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0f, 1f);
        glowRt.anchorMax = new Vector2(1f, 1f);
        glowRt.pivot = new Vector2(0.5f, 1f);
        glowRt.anchoredPosition = Vector2.zero;
        glowRt.sizeDelta = new Vector2(0f, 3f);
        var glowImage = glowGo.GetComponent<Image>();
        if (glowImage == null) glowImage = glowGo.AddComponent<Image>();
        glowImage.color = new Color(0.96f, 0.80f, 0.26f, 0.95f);
        glowImage.raycastTarget = false;

        return rt;
    }

    void EnsureBuildPopupTitle()
    {
        var body = EnsureBuildPopupBody();
        if (body == null) return;
        var titleRoot = FindBuildPopupNode("_BuildPopupTitle");
        if (titleRoot == null)
        {
            var go = new GameObject("_BuildPopupTitle");
            go.transform.SetParent(body, false);
            titleRoot = go.transform;
        }
        else if (titleRoot.parent != body)
        {
            titleRoot.SetParent(body, false);
        }

        var rt = titleRoot.GetComponent<RectTransform>();
        if (rt == null)
        {
            rt = titleRoot.gameObject.AddComponent<RectTransform>();
            titleRoot = rt.transform;
        }
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(0f, 30f);

        var text = titleRoot.GetComponent<Text>();
        if (text == null) text = titleRoot.gameObject.AddComponent<Text>();
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "建造指挥台";
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.94f, 0.62f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        EnsureReadableTextShadow(text, new Vector2(1.8f, -1.8f));
        EnsureReadableTextOutline(text, new Vector2(1.2f, -1.2f), new Color(0f, 0f, 0f, 0.78f));
    }

    void EnsureBuildPopupCloseButton()
    {
        var backdrop = EnsureBuildPopupBackdrop();
        if (backdrop == null) return;
        var closeRoot = FindBuildPopupNode("_BuildPopupClose");
        var button = closeRoot != null ? closeRoot.GetComponent<Button>() : null;
        if (button == null)
        {
            button = CreatePopupButton(backdrop, "_BuildPopupClose", "×", new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(34f, 30f));
        }
        else if (button.transform.parent != backdrop)
        {
            button.transform.SetParent(backdrop, false);
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => { _UiClickAudio.PlayClick(); CloseBuildMenu(); });

        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-22f, -20f);
            rt.sizeDelta = new Vector2(34f, 30f);
        }
    }

    void EnsureBuildCategoryTabs()
    {
        if (_buildCategoryButtons == null || _buildCategoryButtons.Length != BuildCategoryNames.Length)
            _buildCategoryButtons = new Button[BuildCategoryNames.Length];

        var body = EnsureBuildPopupBody();
        if (body == null) return;
        var tabsRoot = FindBuildPopupNode("_BuildCategoryTabs");
        if (tabsRoot == null)
        {
            var tabs = new GameObject("_BuildCategoryTabs");
            tabs.transform.SetParent(body, false);
            var trt = tabs.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -36f);
            trt.sizeDelta = new Vector2(620f, 40f);
            tabsRoot = tabs.transform;
        }
        else if (tabsRoot.parent != body)
        {
            tabsRoot.SetParent(body, false);
        }
        else
        {
            var trt = tabsRoot.GetComponent<RectTransform>();
            if (trt != null)
            {
                trt.anchoredPosition = new Vector2(0f, -36f);
                trt.sizeDelta = new Vector2(620f, 40f);
            }
        }

        for (int i = 0; i < BuildCategoryNames.Length; i++)
        {
            int idx = i;
            var button = _buildCategoryButtons[i];
            if (button == null)
            {
                var existing = SafeFindBuildPopupChild(tabsRoot, "_BuildTab_" + i);
                button = existing != null ? existing.GetComponent<Button>() : null;
            }
            if (button == null)
            {
                button = CreatePopupButton(tabsRoot, "_BuildTab_" + i, BuildCategoryNames[i],
                    new Vector2(0f, 0.5f), new Vector2(50f + i * 100f, 0f), new Vector2(88f, 34f));
            }
            else if (button.transform.parent != tabsRoot)
            {
                button.transform.SetParent(tabsRoot, false);
            }

            var rt = button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(50f + i * 100f, 0f);
                rt.sizeDelta = new Vector2(88f, 34f);
            }
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = BuildCategoryNames[i];
                label.fontSize = 17;
                label.fontStyle = FontStyle.Bold;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                EnsureReadableTextShadow(label, new Vector2(1.2f, -1.2f));
                EnsureReadableTextOutline(label, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.70f));
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { _UiClickAudio.PlayClick(); SetBuildCategory(idx); });
            _buildCategoryButtons[i] = button;
        }
    }

    void EnsureBuildEmptyLabel()
    {
        var contentFrame = EnsureBuildPopupContentFrame();
        if (contentFrame == null) return;
        if (_buildEmptyText != null)
        {
            try
            {
                if (_buildEmptyText.transform.parent != contentFrame)
                    _buildEmptyText.transform.SetParent(contentFrame, false);
                return;
            }
            catch (MissingReferenceException)
            {
                _buildEmptyText = null;
            }
        }
        var existing = SafeFindBuildPopupChild(contentFrame, "_BuildEmptyText");
        if (existing == null)
            existing = FindBuildPopupNode("_BuildEmptyText");
        if (existing != null)
        {
            _buildEmptyText = existing.GetComponent<Text>();
            if (_buildEmptyText != null && _buildEmptyText.transform.parent != contentFrame)
                _buildEmptyText.transform.SetParent(contentFrame, false);
            return;
        }

        var go = new GameObject("_BuildEmptyText");
        go.transform.SetParent(contentFrame, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(460f, 70f);
        _buildEmptyText = go.AddComponent<Text>();
        _buildEmptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_buildEmptyText.font == null) _buildEmptyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _buildEmptyText.text = "";
        _buildEmptyText.fontSize = 17;
        _buildEmptyText.fontStyle = FontStyle.Bold;
        _buildEmptyText.alignment = TextAnchor.MiddleCenter;
        _buildEmptyText.color = new Color(0.72f, 0.86f, 1f);
        _buildEmptyText.raycastTarget = false;
    }

    Button CreatePopupButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        Sprite buttonSprite = LoadGeneratedButtonSprite(label == "取消" || label == "关闭" || label == "×" || label == "脳" ? "neutral" : "secondary");
        if (buttonSprite != null)
        {
            img.sprite = buttonSprite;
            img.type = HasSpriteBorder(buttonSprite) ? Image.Type.Sliced : Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else
        {
            img.color = new Color(0.14f, 0.18f, 0.11f, 0.92f);
        }
        var outline = go.AddComponent<Outline>();
        outline.enabled = buttonSprite == null;
        outline.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.72f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = label;
        text.fontSize = label == "×" ? 26 : 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.94f, 0.62f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        EnsureReadableTextShadow(text, new Vector2(1.2f, -1.2f));
        EnsureReadableTextOutline(text, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.70f));
        return button;
    }

    void EnsureBuildingUpgradePanel(Canvas canvas)
    {
        if (canvas == null) return;
        if (BuildingUpgradePanel != null) return;

        var existing = canvas.transform.Find("_BuildingUpgradePanel");
        if (existing != null)
            BuildingUpgradePanel = existing.gameObject;
        else
            BuildingUpgradePanel = CreateBuildingUpgradePanel(canvas.transform);

        BuildingUpgradeTitleText = BuildingUpgradePanel.transform.Find("_UpgradeTitle")?.GetComponent<Text>();
        BuildingUpgradeBodyText = BuildingUpgradePanel.transform.Find("_UpgradeBody")?.GetComponent<Text>();
        BuildingUpgradeConfirmButton = BuildingUpgradePanel.transform.Find("_UpgradeConfirmButton")?.GetComponent<Button>();
        BuildingUpgradeCancelButton = BuildingUpgradePanel.transform.Find("_UpgradeCancelButton")?.GetComponent<Button>();
        if (BuildingUpgradeConfirmButton != null)
        {
            BuildingUpgradeConfirmButton.onClick.RemoveAllListeners();
            BuildingUpgradeConfirmButton.onClick.AddListener(ConfirmBuildingUpgrade);
        }
        if (BuildingUpgradeCancelButton != null)
        {
            BuildingUpgradeCancelButton.onClick.RemoveAllListeners();
            BuildingUpgradeCancelButton.onClick.AddListener(() => {
                _UiClickAudio.PlayClick();
                CloseBuildingUpgradePanel();
            });
        }
        BuildingUpgradePanel.SetActive(false);
    }

    GameObject CreateBuildingUpgradePanel(Transform canvas)
    {
        var panel = new GameObject("_BuildingUpgradePanel");
        panel.transform.SetParent(canvas, false);
        panel.AddComponent<CanvasRenderer>();
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(540f, 430f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.08f, 0.96f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.80f, 0.64f, 0.22f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var shadow = GetOrAddUiShadow(panel);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(4f, -4f);

        BuildingUpgradeTitleText = CreateUpgradePanelText(panel.transform, "_UpgradeTitle",
            "升级确认", new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(480f, 40f),
            24, TextAnchor.MiddleCenter, new Color(1f, 0.94f, 0.62f));

        BuildingUpgradeBodyText = CreateUpgradePanelText(panel.transform, "_UpgradeBody",
            "", new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(468f, 226f),
            17, TextAnchor.UpperLeft, new Color(0.86f, 0.95f, 1f));
        BuildingUpgradeBodyText.supportRichText = true;
        BuildingUpgradeBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        BuildingUpgradeBodyText.verticalOverflow = VerticalWrapMode.Truncate;
        BuildingUpgradeBodyText.resizeTextForBestFit = true;
        BuildingUpgradeBodyText.resizeTextMinSize = 13;
        BuildingUpgradeBodyText.resizeTextMaxSize = 17;

        BuildingUpgradeConfirmButton = CreatePopupButton(panel.transform, "_UpgradeConfirmButton",
            "确认升级", new Vector2(0.5f, 0f), new Vector2(-104f, 66f), new Vector2(176f, 48f));
        BuildingUpgradeCancelButton = CreatePopupButton(panel.transform, "_UpgradeCancelButton",
            "取消", new Vector2(0.5f, 0f), new Vector2(104f, 66f), new Vector2(140f, 48f));

        return panel;
    }

    Text CreateUpgradePanelText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos,
        Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        EnsureReadableTextShadow(label, new Vector2(1.2f, -1.2f));
        EnsureReadableTextOutline(label, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.72f));
        return label;
    }

    void OpenBuildingUpgradePanel(RTSBuilding building)
    {
        if (building == null || building.GetHP() <= 0) return;
        if (!building.SupportsBuildingUpgrade || !building.bPlayerOwned)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert("该建筑不可升级");
            return;
        }

        var canvas = GetHudCanvas();
        EnsureBuildingUpgradePanel(canvas);
        if (BuildingUpgradePanel == null) return;

        _pendingUpgradeBuilding = building;
        CloseBuildMenu();
        SetTechPanelOpen(false);
        if (_settingsOpen)
            SetGameSettingsOpen(false);

        BuildingUpgradePanel.SetActive(true);
        BuildingUpgradePanel.transform.SetAsLastSibling();
        RefreshBuildingUpgradePanel();
        _UiClickAudio.PlayClick();
    }

    void CloseBuildingUpgradePanel()
    {
        if (BuildingUpgradePanel != null)
            BuildingUpgradePanel.SetActive(false);
        _pendingUpgradeBuilding = null;
    }

    void RefreshBuildingUpgradePanel()
    {
        if (BuildingUpgradePanel == null || !BuildingUpgradePanel.activeSelf) return;
        if (_pendingUpgradeBuilding == null || _pendingUpgradeBuilding.GetHP() <= 0)
        {
            CloseBuildingUpgradePanel();
            return;
        }

        RTSBuilding b = _pendingUpgradeBuilding;
        if (BuildingUpgradeTitleText != null)
            BuildingUpgradeTitleText.text = $"{b.DisplayName} 升级";

        bool canUpgrade = b.CanUpgradeBuilding(out string failureReason);
        if (BuildingUpgradeBodyText != null)
        {
            BuildingUpgradeBodyText.supportRichText = true;
            string body = b.GetUpgradePanelStatusText(true);
            string failureSummary = GetBuildingUpgradeFailureSummary(failureReason);
            if (!canUpgrade && !string.IsNullOrEmpty(failureSummary))
                body += $"\n\n<color=#FF8B7D><b>{failureSummary}</b></color>";
            BuildingUpgradeBodyText.text = body;
        }

        if (BuildingUpgradeConfirmButton != null)
        {
            BuildingUpgradeConfirmButton.interactable = canUpgrade;
            var img = BuildingUpgradeConfirmButton.GetComponent<Image>();
            if (img != null)
            {
                var upgradeSprite = LoadButtonBackdropSprite("action_upgrade");
                if (upgradeSprite != null)
                {
                    img.sprite = upgradeSprite;
                    img.type = Image.Type.Simple;
                    img.preserveAspect = false;
                    img.color = canUpgrade ? Color.white : new Color(0.55f, 0.58f, 0.62f, 0.86f);
                }
                else
                {
                    img.color = canUpgrade
                        ? new Color(0.18f, 0.34f, 0.18f, 0.98f)
                        : new Color(0.16f, 0.18f, 0.20f, 0.82f);
                }
            }
            var txt = BuildingUpgradeConfirmButton.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                txt.text = canUpgrade ? "确认升级" : "不符合条件";
                txt.color = canUpgrade ? new Color(1f, 0.94f, 0.62f) : new Color(0.72f, 0.76f, 0.82f);
            }
        }
    }

    string GetBuildingUpgradeFailureSummary(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            return string.Empty;

        string summary = failureReason.Trim();
        int lineBreak = summary.IndexOf('\n');
        if (lineBreak >= 0)
            summary = summary.Substring(0, lineBreak).Trim();

        return summary == "升级条件" ? string.Empty : summary;
    }

    void ConfirmBuildingUpgrade()
    {
        RTSBuilding building = _pendingUpgradeBuilding;
        if (building == null || building.GetHP() <= 0)
        {
            CloseBuildingUpgradePanel();
            return;
        }

        if (building.TryUpgradeBuilding(out string failureReason))
        {
            _UiClickAudio.PlayConfirm();
            CloseBuildingUpgradePanel();
            LayoutBuildingPanelControls();
        }
        else
        {
            _UiClickAudio.PlayDeny();
            if (!string.IsNullOrEmpty(failureReason))
                ShowAlert(failureReason);
            RefreshBuildingUpgradePanel();
        }
    }

    static readonly Dictionary<string, Sprite> HudSpriteCache = new Dictionary<string, Sprite>(8);
    const string DefaultTechBackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_generic_plate";

    static Sprite LoadHudSprite(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (HudSpriteCache.TryGetValue(path, out var cached) && cached != null)
            return cached;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite == null)
        {
            foreach (var obj in Resources.LoadAll(path))
            {
                if (obj is Sprite sp)
                {
                    sprite = sp;
                    break;
                }
            }
        }
        if (sprite == null)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                Vector4 border = path.IndexOf("GeneratedButtons/", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("ButtonBackdrops/", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Vector4(16f, 16f, 16f, 16f)
                    : Vector4.zero;
                sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    border);
            }
        }

        if (sprite != null) HudSpriteCache[path] = sprite;
        return sprite;
    }

    static Sprite LoadPlayerAvatarHudSprite(string avatarResource)
    {
        string normalized = string.IsNullOrWhiteSpace(avatarResource)
            ? "gen_avatar_player"
            : avatarResource.Trim().Replace('\\', '/');

        Sprite sprite = null;
        if (normalized.StartsWith("LobbyGen/", System.StringComparison.OrdinalIgnoreCase))
            sprite = LoadHudSprite(normalized);
        else
            sprite = LoadHudSprite("LobbyGen/" + normalized) ?? LoadHudSprite(normalized);

        return sprite
            ?? LoadHudSprite("LobbyGen/gen_avatar_player")
            ?? LoadHudSprite("BattleHud/battle_avatar_placeholder");
    }

    static Sprite LoadGeneratedButtonSprite(string role)
    {
        string name;
        switch (role)
        {
            case "primary":
                name = "generated_button_primary";
                break;
            case "danger":
                name = "generated_button_danger";
                break;
            case "neutral":
                name = "generated_button_neutral";
                break;
            case "ghost":
                name = "generated_button_ghost";
                break;
            default:
                name = "generated_button_secondary";
                break;
        }

        return LoadHudSprite("UI/GeneratedButtons/" + name);
    }

    static Sprite LoadButtonBackdropSprite(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return LoadHudSprite("UI/ButtonBackdrops/" + name);
    }

    static Sprite GetProductionButtonBackdropSprite(RTSUnit unit)
    {
        string key = unit != null ? GetProductionIconKey(unit) : null;
        return LoadButtonBackdropSprite(GetProductionBackdropName(key));
    }

    static string GetProductionBackdropName(string key)
    {
        switch (key)
        {
            case "infantry":
                return "prod_infantry";
            case "infantry_artillery":
                return "prod_infantry_artillery";
            case "infantry_flamethrower":
                return "prod_infantry_flamethrower";
            case "flamethrower":
                return "prod_flamethrower";
            case "anti_air":
                return "prod_anti_air";
            case "tank":
                return "prod_tank";
            case "artillery":
                return "prod_artillery";
            case "fighter":
                return "prod_fighter";
            case "bomber":
                return "prod_bomber";
            case "scout_plane":
                return "prod_scout_plane";
            case "patrol_boat":
                return "prod_patrol_boat";
            case "destroyer_ship":
                return "prod_destroyer_ship";
            case "battleship":
                return "prod_battleship";
            default:
                return null;
        }
    }

    static bool HasSpriteBorder(Sprite sprite)
    {
        return sprite != null && sprite.border.sqrMagnitude > 0.01f;
    }

    void ApplyHudButtonSkin(Button button, string role)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
            button.targetGraphic = image;
        }

        Sprite sprite = LoadGeneratedButtonSprite(role);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = HasSpriteBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = true;
        }
    }

    static Sprite GetProductionIconSprite(GameObject unitPrefab)
    {
        if (unitPrefab == null) return null;
        return GetProductionIconSprite(unitPrefab.GetComponent<RTSUnit>());
    }

    static Sprite GetProductionIconSprite(RTSUnit unit)
    {
        if (unit == null) return null;

        string key = GetProductionIconKey(unit);
        if (string.IsNullOrEmpty(key))
            key = unit.GetType().Name;

        if (ProductionUnitIconCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var sprite = LoadHudSprite("BattleHud/UnitIcons/" + key);
        if (sprite == null)
        {
            string backdropName = GetProductionBackdropName(key);
            if (!string.IsNullOrEmpty(backdropName))
                sprite = LoadButtonBackdropSprite(backdropName + "_icon");
        }
        if (sprite == null)
            sprite = BuildRuntimeProductionIconSprite(key);

        if (sprite != null)
            ProductionUnitIconCache[key] = sprite;

        return sprite;
    }

    static string GetProductionIconKey(RTSUnit unit)
    {
        if (unit is InfantryArtillery) return "infantry_artillery";
        if (unit is InfantryFlamethrower) return "infantry_flamethrower";
        if (unit is Infantry) return "infantry";
        if (unit is Flamethrower) return "flamethrower";
        if (unit is AntiAirGun) return "anti_air";
        if (unit is Tank) return "tank";
        if (unit is Artillery) return "artillery";
        if (unit is Fighter) return "fighter";
        if (unit is Bomber) return "bomber";
        if (unit is ScoutPlane) return "scout_plane";
        if (unit is PatrolBoat) return "patrol_boat";
        if (unit is DestroyerShip) return "destroyer_ship";
        if (unit is TransportShip) return "battleship";
        return unit.GetType().Name.ToLowerInvariant();
    }

    static Sprite BuildRuntimeProductionIconSprite(string key)
    {
        const int size = 64;
        var pixels = new Color32[size * size];
        var ink = new Color32(232, 240, 255, 255);

        switch (key)
        {
            case "infantry_artillery":
                key = "artillery";
                goto case "artillery";
            case "infantry_flamethrower":
                key = "flamethrower";
                goto case "flamethrower";
            case "infantry":
                DrawCircle(pixels, size, 22, 45, 7, ink);
                DrawRect(pixels, size, 18, 26, 26, 38, ink);
                DrawLine(pixels, size, 26, 34, 42, 28, 4, ink);
                DrawLine(pixels, size, 18, 20, 24, 29, 3, ink);
                DrawLine(pixels, size, 20, 26, 14, 14, 3, ink);
                DrawLine(pixels, size, 24, 26, 28, 14, 3, ink);
                break;
            case "flamethrower":
                DrawCircle(pixels, size, 22, 45, 7, ink);
                DrawRect(pixels, size, 18, 26, 26, 38, ink);
                DrawRect(pixels, size, 28, 31, 36, 36, ink);
                DrawLine(pixels, size, 36, 34, 48, 28, 4, ink);
                DrawLine(pixels, size, 48, 28, 54, 34, 3, ink);
                DrawLine(pixels, size, 48, 28, 56, 25, 3, ink);
                DrawLine(pixels, size, 20, 26, 14, 14, 3, ink);
                DrawLine(pixels, size, 24, 26, 28, 14, 3, ink);
                break;
            case "tank":
                DrawRect(pixels, size, 10, 18, 42, 26, ink);
                DrawRect(pixels, size, 16, 26, 40, 36, ink);
                DrawCircle(pixels, size, 30, 32, 5, ink);
                DrawLine(pixels, size, 35, 32, 52, 36, 4, ink);
                break;
            case "anti_air":
                DrawRect(pixels, size, 12, 18, 40, 24, ink);
                DrawRect(pixels, size, 18, 26, 38, 34, ink);
                DrawLine(pixels, size, 31, 33, 49, 49, 4, ink);
                DrawCircle(pixels, size, 50, 50, 8, ink);
                DrawLine(pixels, size, 42, 50, 58, 50, 3, ink);
                DrawLine(pixels, size, 50, 42, 50, 58, 3, ink);
                break;
            case "artillery":
                DrawCircle(pixels, size, 16, 18, 6, ink);
                DrawCircle(pixels, size, 36, 18, 6, ink);
                DrawLine(pixels, size, 14, 22, 38, 22, 4, ink);
                DrawLine(pixels, size, 26, 26, 50, 40, 4, ink);
                DrawLine(pixels, size, 28, 24, 22, 14, 3, ink);
                break;
            case "fighter":
                DrawLine(pixels, size, 32, 12, 32, 52, 4, ink);
                DrawLine(pixels, size, 32, 34, 12, 24, 4, ink);
                DrawLine(pixels, size, 32, 34, 52, 24, 4, ink);
                DrawLine(pixels, size, 32, 20, 22, 12, 4, ink);
                DrawLine(pixels, size, 32, 20, 42, 12, 4, ink);
                break;
            case "bomber":
                DrawLine(pixels, size, 32, 10, 32, 52, 5, ink);
                DrawLine(pixels, size, 32, 30, 10, 24, 5, ink);
                DrawLine(pixels, size, 32, 30, 54, 24, 5, ink);
                DrawRect(pixels, size, 18, 21, 24, 27, ink);
                DrawRect(pixels, size, 40, 21, 46, 27, ink);
                DrawLine(pixels, size, 32, 18, 22, 12, 4, ink);
                DrawLine(pixels, size, 32, 18, 42, 12, 4, ink);
                break;
            case "scout_plane":
                DrawLine(pixels, size, 32, 10, 32, 52, 3, ink);
                DrawLine(pixels, size, 32, 30, 14, 24, 3, ink);
                DrawLine(pixels, size, 32, 30, 50, 24, 3, ink);
                DrawLine(pixels, size, 32, 18, 26, 13, 3, ink);
                DrawLine(pixels, size, 32, 18, 38, 13, 3, ink);
                break;
            case "patrol_boat":
                DrawLine(pixels, size, 10, 18, 48, 18, 4, ink);
                DrawLine(pixels, size, 48, 18, 56, 24, 4, ink);
                DrawLine(pixels, size, 10, 18, 16, 26, 4, ink);
                DrawRect(pixels, size, 22, 20, 32, 30, ink);
                DrawLine(pixels, size, 30, 30, 30, 46, 3, ink);
                break;
            case "destroyer_ship":
                DrawLine(pixels, size, 8, 16, 50, 16, 4, ink);
                DrawLine(pixels, size, 50, 16, 58, 24, 4, ink);
                DrawLine(pixels, size, 8, 16, 14, 25, 4, ink);
                DrawRect(pixels, size, 18, 18, 38, 28, ink);
                DrawRect(pixels, size, 22, 28, 30, 38, ink);
                DrawLine(pixels, size, 34, 30, 46, 36, 3, ink);
                break;
            case "battleship":
            case "transport_ship":
                DrawLine(pixels, size, 8, 16, 48, 16, 4, ink);
                DrawLine(pixels, size, 48, 16, 56, 23, 4, ink);
                DrawLine(pixels, size, 8, 16, 16, 24, 4, ink);
                DrawRect(pixels, size, 18, 20, 42, 30, ink);
                DrawRect(pixels, size, 20, 30, 30, 39, ink);
                DrawRect(pixels, size, 34, 29, 44, 37, ink);
                DrawLine(pixels, size, 30, 36, 48, 42, 4, ink);
                DrawLine(pixels, size, 24, 30, 12, 37, 3, ink);
                break;
            default:
                DrawRect(pixels, size, 16, 16, 48, 48, ink);
                break;
        }

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.SetPixels32(pixels);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "rt_prod_" + key;
        return sprite;
    }

    static void DrawRect(Color32[] pixels, int width, int xMin, int yMin, int xMax, int yMax, Color32 color)
    {
        xMin = Mathf.Clamp(xMin, 0, width - 1);
        xMax = Mathf.Clamp(xMax, 0, width - 1);
        yMin = Mathf.Clamp(yMin, 0, width - 1);
        yMax = Mathf.Clamp(yMax, 0, width - 1);

        for (int y = yMin; y <= yMax; y++)
            for (int x = xMin; x <= xMax; x++)
                pixels[y * width + x] = color;
    }

    static void DrawCircle(Color32[] pixels, int width, int cx, int cy, int radius, Color32 color)
    {
        int minX = Mathf.Max(0, cx - radius);
        int maxX = Mathf.Min(width - 1, cx + radius);
        int minY = Mathf.Max(0, cy - radius);
        int maxY = Mathf.Min(width - 1, cy + radius);
        int radiusSq = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy <= radiusSq)
                    pixels[y * width + x] = color;
            }
        }
    }

    static void DrawLine(Color32[] pixels, int width, int x0, int y0, int x1, int y1, int thickness, Color32 color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawCircle(pixels, width, x0, y0, Mathf.Max(1, thickness / 2), color);
            if (x0 == x1 && y0 == y1) break;

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    void StyleSideHudButton(Button button, string label, string iconGlyph, Color accent)
    {
        if (button == null) return;
        button.enabled = true;
        button.interactable = true;

        var bg = button.GetComponent<Image>();
        if (bg != null)
        {
            var sprite = LoadGeneratedButtonSprite("secondary");
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.type = HasSpriteBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
                bg.preserveAspect = false;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.07f, 0.095f, 0.075f, 0.95f);
            }
            bg.raycastTarget = true;
        }

        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.enabled = true;
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(1.3f, -1.3f);

        var oldIcon = button.transform.Find("_HudIcon");
        if (oldIcon != null)
            oldIcon.gameObject.SetActive(false);

        var icon = button.transform.Find("_HudIconGlyph")?.GetComponent<Text>();
        if (icon == null)
        {
            var iconGO = new GameObject("_HudIconGlyph");
            iconGO.transform.SetParent(button.transform, false);
            icon = iconGO.AddComponent<Text>();
            icon.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (icon.font == null) icon.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            icon.raycastTarget = false;
        }
        var iconRT = icon.rectTransform;
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(23f, 0f);
        iconRT.sizeDelta = new Vector2(30f, 30f);
        icon.text = iconGlyph;
        icon.fontSize = 25;
        icon.fontStyle = FontStyle.Bold;
        icon.alignment = TextAnchor.MiddleCenter;
        icon.color = new Color(0.96f, 0.98f, 1f);

        var text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = 17;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(1f, 0.92f, 0.55f);
            text.raycastTarget = false;
            var textRT = text.rectTransform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(46f, 0f);
            textRT.offsetMax = new Vector2(-8f, 0f);
        }
    }

    void StyleIconOnlyHudButton(Button button, string spritePath, string fallbackGlyph, Color accent)
    {
        if (button == null) return;
        button.enabled = true;
        button.interactable = true;
        button.gameObject.SetActive(true);

        var bg = button.GetComponent<Image>();
        if (bg != null)
        {
            var bgSprite = LoadGeneratedButtonSprite("ghost");
            if (bgSprite != null)
            {
                bg.sprite = bgSprite;
                bg.type = HasSpriteBorder(bgSprite) ? Image.Type.Sliced : Image.Type.Simple;
                bg.preserveAspect = false;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0f, 0f, 0f, 0f);
            }
            bg.raycastTarget = true;
            button.targetGraphic = bg;
        }

        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.enabled = false;

        var label = button.transform.Find("Text")?.GetComponent<Text>();
        if (label != null)
        {
            label.text = "";
            label.raycastTarget = false;
        }

        var sprite = LoadHudSprite(spritePath);
        var imageGO = button.transform.Find("_HudIcon")?.gameObject;
        var glyph = button.transform.Find("_HudIconGlyph")?.GetComponent<Text>();
        if (sprite != null)
        {
            if (imageGO == null)
            {
                imageGO = new GameObject("_HudIcon");
                imageGO.transform.SetParent(button.transform, false);
                imageGO.AddComponent<RectTransform>();
                imageGO.AddComponent<Image>();
            }

            imageGO.SetActive(true);
            var iconRT = imageGO.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = new Vector2(4f, 4f);
            iconRT.offsetMax = new Vector2(-4f, -4f);

            var iconImage = imageGO.GetComponent<Image>();
            if (iconImage == null) iconImage = imageGO.AddComponent<Image>();
            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            if (glyph != null) glyph.gameObject.SetActive(false);
            return;
        }

        if (imageGO != null) imageGO.SetActive(false);
        if (glyph == null)
        {
            var glyphGO = new GameObject("_HudIconGlyph");
            glyphGO.transform.SetParent(button.transform, false);
            glyph = glyphGO.AddComponent<Text>();
            glyph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (glyph.font == null) glyph.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            glyph.raycastTarget = false;
        }

        glyph.gameObject.SetActive(true);
        var glyphRT = glyph.rectTransform;
        glyphRT.anchorMin = Vector2.zero;
        glyphRT.anchorMax = Vector2.one;
        glyphRT.offsetMin = Vector2.zero;
        glyphRT.offsetMax = Vector2.zero;
        glyph.text = fallbackGlyph;
        glyph.fontSize = 26;
        glyph.fontStyle = FontStyle.Bold;
        glyph.alignment = TextAnchor.MiddleCenter;
        glyph.color = new Color(0.96f, 0.98f, 1f);
        var shadow = glyph.GetComponent<Shadow>() ?? glyph.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
    }

    void EnsureTechButton(Canvas canvas)
    {
        if (canvas == null) return;
        var existing = canvas.transform.Find("_TechButton");
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        var rt = button != null ? button.GetComponent<RectTransform>() : null;
        if (button == null)
        {
            button = CreatePopupButton(canvas.transform, "_TechButton", "科技",
                new Vector2(0f, 0f),
                new Vector2(BattleHudActionButtonLeft, BattleHudTechButtonBottom),
                new Vector2(BattleHudActionButtonWidth, BattleHudActionButtonHeight));
            rt = button.GetComponent<RectTransform>();
            button.onClick.AddListener(() =>
            {
                _UiClickAudio.PlayClick();
                CloseBuildMenu();
                var baseBuilding = GameManager.Instance?.PlayerMainBase;
                if (baseBuilding == null || baseBuilding.GetHP() <= 0)
                {
                    _UiClickAudio.PlayDeny();
                    ShowAlert("主基地已摧毁，科技不可用");
                    return;
                }

                var pc = RTSPlayerController.Instance;
                if (pc != null)
                    pc.SelectOwnedBuilding(baseBuilding, true);
                else
                    OnSelectionChanged(new List<RTSUnit>(), baseBuilding);

                ShowAlert("科技中心：已定位主基地");
            });
        }
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnTechButtonClicked);
        EnsureHudButtonInput(button);
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(BattleHudActionButtonLeft, BattleHudTechButtonBottom);
            rt.sizeDelta = new Vector2(BattleHudActionButtonWidth, BattleHudActionButtonHeight);
        }

        StyleSideHudButton(button, "科技", "◎",
            new Color(0.46f, 0.82f, 1f, 0.92f));
        EnsureHudButtonInput(button);
        button.transform.SetAsLastSibling();
    }

    void EnsureHudButtonInput(Button button)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        button.enabled = true;
        button.interactable = true;

        var image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        button.targetGraphic = image;

        var group = button.GetComponent<CanvasGroup>();
        if (group == null)
            group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = true;
    }

    void OnTechButtonClicked()
    {
        _UiClickAudio.PlayClick();
        CloseBuildMenu();
        SetTechPanelOpen(true);

        var baseBuilding = GameManager.Instance?.PlayerMainBase;
        if (baseBuilding == null || baseBuilding.GetHP() <= 0)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert("科技中心已打开，主基地不可定位");
            return;
        }

        var pc = RTSPlayerController.Instance;
        if (pc != null)
            pc.SelectOwnedBuilding(baseBuilding, true);
        else
            OnSelectionChanged(new List<RTSUnit>(), baseBuilding);

        ShowAlert("科技中心已打开");
    }

    void EnsureBattleTechPanel(Canvas canvas)
    {
        if (canvas == null) return;
        EnsureBattleTechDefinitions();
        if (TechPanel == null)
        {
            var existing = FindUiTransform(canvas.transform, "_BattleTechPanel");
            TechPanel = existing != null ? existing.gameObject : CreateBattleTechPanel(canvas.transform);
        }

        ApplyBattleTechPanelLayout(canvas);
        EnsureBattleTechButtons(TechPanel.transform);
        TechPanel.SetActive(_techPanelOpen);
        if (_techPanelOpen) TechPanel.transform.SetAsLastSibling();
    }

    Vector2 GetCanvasRectSize(Canvas canvas)
    {
        var canvasRt = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (canvasRt != null && canvasRt.rect.width > 1f && canvasRt.rect.height > 1f)
            return canvasRt.rect.size;

        return new Vector2(Screen.width, Screen.height);
    }

    void ApplyBattleTechPanelLayout(Canvas canvas)
    {
        if (TechPanel == null || canvas == null) return;

        Vector2 canvasSize = GetCanvasRectSize(canvas);
        _lastTechPanelCanvasSize = canvasSize;

        float maxVisibleWidth = Mathf.Max(120f, canvasSize.x - 16f);
        float maxVisibleHeight = Mathf.Max(120f, canvasSize.y - 16f);
        float desiredWidth = Mathf.Min(TechPanelMaxWidth, maxVisibleWidth);
        float desiredHeight = Mathf.Min(TechPanelMaxHeight, maxVisibleHeight);
        float panelWidth = maxVisibleWidth >= TechPanelMinWidth ? Mathf.Max(TechPanelMinWidth, desiredWidth) : maxVisibleWidth;
        float panelHeight = maxVisibleHeight >= TechPanelMinHeight ? Mathf.Max(TechPanelMinHeight, desiredHeight) : maxVisibleHeight;

        var rt = TechPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelWidth, panelHeight);
        }

        var titleRt = TechPanel.transform.Find("_TechTitle") as RectTransform;
        if (titleRt != null)
        {
            titleRt.anchorMin = titleRt.anchorMax = titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -26f);
            titleRt.sizeDelta = new Vector2(Mathf.Max(160f, panelWidth - 112f), 34f);
            var title = titleRt.GetComponent<Text>();
            if (title != null) title.fontSize = panelHeight < 250f ? 18 : 22;
        }

        var closeButton = TechPanel.transform.Find("_TechCloseButton")?.GetComponent<Button>();
        if (closeButton != null)
        {
            var closeText = closeButton.GetComponentInChildren<Text>(true);
            if (closeText != null)
            {
                closeText.text = "×";
                closeText.fontSize = 22;
                closeText.alignment = TextAnchor.MiddleCenter;
            }
        }

        var closeRt = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (closeRt != null)
        {
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-28f, -24f);
            closeRt.sizeDelta = new Vector2(34f, 30f);
        }

        if (_techButtons == null) return;
        for (int i = 0; i < _techButtons.Length; i++)
            LayoutBattleTechButton(_techButtons[i], i);
    }

    GameObject CreateBattleTechPanel(Transform canvas)
    {
        var panel = new GameObject("_BattleTechPanel");
        panel.transform.SetParent(canvas, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(TechPanelMaxWidth, TechPanelMaxHeight);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.018f, 0.024f, 0.030f, 0.96f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.46f, 0.82f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateSettingsBar(panel.transform, "_TopBlue", new Vector2(0f, 1f), Vector2.one, new Color(0.20f, 0.56f, 0.90f, 0.92f));
        CreateSettingsText(panel.transform, "_TechTitle", "科技中心",
            new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(260f, 36f), 22,
            TextAnchor.MiddleCenter, new Color(0.86f, 0.96f, 1f));

        var closeButton = CreatePopupButton(panel.transform, "_TechCloseButton", "关闭",
            new Vector2(1f, 1f), new Vector2(-34f, -28f), new Vector2(48f, 34f));
        var closeText = closeButton.GetComponentInChildren<Text>(true);
        if (closeText != null)
        {
            closeText.text = "×";
            closeText.fontSize = 22;
            closeText.alignment = TextAnchor.MiddleCenter;
        }
        closeButton.onClick.AddListener(() =>
        {
            _UiClickAudio.PlayClick();
            SetTechPanelOpen(false);
        });

        EnsureBattleTechButtons(panel.transform);
        panel.SetActive(false);
        return panel;
    }

    void EnsureBattleTechDefinitions()
    {
        if (_battleTechs != null && _battleTechs.Length == 8) return;

        _battleTechs = new BattleTechSpec[]
        {
            new BattleTechSpec { Id = "speed", Name = "加速", Glyph = "速", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_speed_wings", Radius = 13f, Duration = 14f, Cooldown = 28f, MoveMultiplier = 1.32f, DamageMultiplier = 1f, AttackIntervalMultiplier = 1f, Color = new Color(0.38f, 0.92f, 1f, 0.78f) },
            new BattleTechSpec { Id = "armor", Name = "加防御", Glyph = "甲", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_armor_plate", Radius = 12f, Duration = 16f, Cooldown = 34f, MoveMultiplier = 1f, DamageMultiplier = 1f, AttackIntervalMultiplier = 1f, DefenseReduction = 0.28f, Color = new Color(0.55f, 0.78f, 1f, 0.78f) },
            new BattleTechSpec { Id = "firepower", Name = "加火力", Glyph = "火", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_firepower_barrage", Radius = 12f, Duration = 12f, Cooldown = 32f, MoveMultiplier = 1f, DamageMultiplier = 1.30f, AttackIntervalMultiplier = 1f, Color = new Color(1f, 0.48f, 0.22f, 0.80f) },
            new BattleTechSpec { Id = "repair", Name = "维修", Glyph = "修", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_repair_workshop", Radius = 11f, Duration = 10f, Cooldown = 30f, MoveMultiplier = 1f, DamageMultiplier = 1f, AttackIntervalMultiplier = 1f, DefenseReduction = 0.10f, RegenPerSecond = 18f, Color = new Color(0.42f, 1f, 0.58f, 0.78f) },
            new BattleTechSpec { Id = "radar", Name = "侦察", Glyph = "视", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_radar_map", Radius = 14f, Duration = 18f, Cooldown = 30f, MoveMultiplier = 1f, DamageMultiplier = 1f, AttackIntervalMultiplier = 1f, AttackRangeBonus = 2f, SightRangeBonus = 16f, Color = new Color(0.96f, 0.88f, 0.36f, 0.78f) },
            new BattleTechSpec { Id = "rapid", Name = "速射", Glyph = "射", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_rapid_ammo", Radius = 11f, Duration = 12f, Cooldown = 36f, MoveMultiplier = 1f, DamageMultiplier = 1f, AttackIntervalMultiplier = 0.72f, Color = new Color(1f, 0.70f, 0.30f, 0.78f) },
            new BattleTechSpec { Id = "hold", Name = "坚守", Glyph = "守", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_hold_bunker", Radius = 10f, Duration = 18f, Cooldown = 40f, MoveMultiplier = 0.92f, DamageMultiplier = 1.12f, AttackIntervalMultiplier = 1f, AttackRangeBonus = 1.2f, DefenseReduction = 0.18f, Color = new Color(0.72f, 1f, 0.78f, 0.78f) },
            new BattleTechSpec { Id = "assault", Name = "突击", Glyph = "突", BackgroundSpritePath = "BattleHud/TechBackgrounds/tech_bg_assault_arrows", Radius = 13f, Duration = 14f, Cooldown = 38f, MoveMultiplier = 1.18f, DamageMultiplier = 1.16f, AttackIntervalMultiplier = 0.90f, Color = new Color(1f, 0.34f, 0.48f, 0.78f) }
        };

        if (_techCooldownEnds == null || _techCooldownEnds.Length != _battleTechs.Length)
            _techCooldownEnds = new float[_battleTechs.Length];
    }

    void EnsureBattleTechButtons(Transform panel)
    {
        if (panel == null) return;
        EnsureBattleTechDefinitions();

        if (_techButtons == null || _techButtons.Length != _battleTechs.Length)
        {
            _techButtons = new Button[_battleTechs.Length];
            _techCooldownFills = new Image[_battleTechs.Length];
            _techCooldownTexts = new Text[_battleTechs.Length];
        }

        var legacySummary = panel.Find("_TechSummary");
        if (legacySummary != null) legacySummary.gameObject.SetActive(false);
        var legacyFocus = panel.Find("_TechFocusBaseButton");
        if (legacyFocus != null) legacyFocus.gameObject.SetActive(false);

        for (int i = 0; i < _battleTechs.Length; i++)
        {
            if (_techButtons[i] == null)
                _techButtons[i] = CreateBattleTechButton(panel, i);

            LayoutBattleTechButton(_techButtons[i], i);
        }
    }

    Button CreateBattleTechButton(Transform parent, int index)
    {
        BattleTechSpec spec = _battleTechs[index];
        Button button = CreatePopupButton(parent, "_BattleTech_" + index, spec.Name,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 82f));
        button.onClick.RemoveAllListeners();

        var img = button.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = LoadHudSprite(spec.BackgroundSpritePath) ?? LoadHudSprite(DefaultTechBackgroundSpritePath);
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = new Color(1f, 1f, 1f, 0.96f);
        }
        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = spec.Color;
        outline.effectDistance = new Vector2(1.3f, -1.3f);

        var glyphGo = new GameObject("Glyph");
        glyphGo.transform.SetParent(button.transform, false);
        var grt = glyphGo.AddComponent<RectTransform>();
        grt.anchorMin = new Vector2(0.5f, 1f);
        grt.anchorMax = new Vector2(0.5f, 1f);
        grt.pivot = new Vector2(0.5f, 1f);
        grt.anchoredPosition = new Vector2(0f, -9f);
        grt.sizeDelta = new Vector2(78f, 38f);
        var glyph = glyphGo.AddComponent<Text>();
        glyph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (glyph.font == null) glyph.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        glyph.text = spec.Glyph;
        glyph.fontSize = 28;
        glyph.fontStyle = FontStyle.Bold;
        glyph.alignment = TextAnchor.MiddleCenter;
        glyph.color = new Color(0.95f, 0.98f, 1f);
        glyph.raycastTarget = false;
        EnsureReadableTextShadow(glyph, new Vector2(1.4f, -1.4f));
        EnsureReadableTextOutline(glyph, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.78f));

        var label = button.transform.Find("Text")?.GetComponent<Text>();
        if (label != null)
        {
            label.text = spec.Name;
            label.fontSize = 16;
            label.alignment = TextAnchor.LowerCenter;
            label.color = new Color(1f, 0.92f, 0.55f);
            label.rectTransform.offsetMin = new Vector2(4f, 5f);
            label.rectTransform.offsetMax = new Vector2(-4f, -44f);
        }

        var fillGo = new GameObject("CooldownFill");
        fillGo.transform.SetParent(button.transform, false);
        var frt = fillGo.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f);
        frt.offsetMax = new Vector2(-2f, -2f);
        var fill = fillGo.AddComponent<Image>();
        fill.color = new Color(0f, 0f, 0f, 0.58f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = 1;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        fillGo.transform.SetSiblingIndex(0);
        _techCooldownFills[index] = fill;

        var cdGo = new GameObject("CooldownText");
        cdGo.transform.SetParent(button.transform, false);
        var crt = cdGo.AddComponent<RectTransform>();
        crt.anchorMin = Vector2.zero;
        crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero;
        crt.offsetMax = Vector2.zero;
        var cd = cdGo.AddComponent<Text>();
        cd.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cd.font == null) cd.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        cd.text = "";
        cd.fontSize = 22;
        cd.fontStyle = FontStyle.Bold;
        cd.alignment = TextAnchor.MiddleCenter;
        cd.color = Color.white;
        cd.raycastTarget = false;
        _techCooldownTexts[index] = cd;

        var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        AddTechPointerEvent(trigger, EventTriggerType.PointerDown, index, true);
        AddTechPointerEvent(trigger, EventTriggerType.PointerUp, index, false);

        return button;
    }

    void AddTechPointerEvent(EventTrigger trigger, EventTriggerType type, int index, bool down)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((data) =>
        {
            var pointer = data as PointerEventData;
            Vector2 screen = pointer != null ? pointer.position : (Vector2)Input.mousePosition;
            if (down) BeginTechTargeting(index, screen);
            else ReleaseTechTargeting(index, screen, false);
        });
        trigger.triggers.Add(entry);
    }

    void LayoutBattleTechButton(Button button, int index)
    {
        if (button == null) return;
        var rt = button.GetComponent<RectTransform>();
        if (rt == null) return;

        var panelRt = button.transform.parent as RectTransform;
        float panelWidth = panelRt != null && panelRt.rect.width > 1f ? panelRt.rect.width : TechPanelMaxWidth;
        float panelHeight = panelRt != null && panelRt.rect.height > 1f ? panelRt.rect.height : TechPanelMaxHeight;

        float sidePadding = Mathf.Clamp(panelWidth * 0.05f, 10f, 28f);
        float minTopPadding = panelHeight < 180f ? 32f : 48f;
        float minButtonHeight = panelHeight < 180f ? 28f : 40f;
        float topPadding = Mathf.Clamp(panelHeight * 0.20f, minTopPadding, 66f);
        float bottomPadding = Mathf.Clamp(panelHeight * 0.08f, 12f, 24f);
        float gapX = Mathf.Clamp(panelWidth * 0.018f, 5f, 10f);
        float gapY = Mathf.Clamp(panelHeight * 0.035f, 6f, 12f);
        float buttonWidth = Mathf.Min(112f, Mathf.Max(48f, (panelWidth - sidePadding * 2f - gapX * 3f) / 4f));
        float buttonHeight = Mathf.Min(82f, Mathf.Max(minButtonHeight, (panelHeight - topPadding - bottomPadding - gapY) / 2f));

        float gridWidth = buttonWidth * 4f + gapX * 3f;
        float startX = -gridWidth * 0.5f + buttonWidth * 0.5f;
        float startY = panelHeight * 0.5f - topPadding - buttonHeight * 0.5f;

        int col = index % 4;
        int row = index / 4;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(startX + col * (buttonWidth + gapX), startY - row * (buttonHeight + gapY));
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

        var glyphRt = button.transform.Find("Glyph") as RectTransform;
        if (glyphRt != null)
        {
            glyphRt.anchorMin = glyphRt.anchorMax = new Vector2(0.5f, 1f);
            glyphRt.pivot = new Vector2(0.5f, 1f);
            glyphRt.anchoredPosition = new Vector2(0f, -Mathf.Clamp(buttonHeight * 0.10f, 4f, 9f));
            glyphRt.sizeDelta = new Vector2(Mathf.Max(34f, buttonWidth - 16f), Mathf.Clamp(buttonHeight * 0.46f, 22f, 38f));
            var glyph = glyphRt.GetComponent<Text>();
            if (glyph != null) glyph.fontSize = Mathf.RoundToInt(Mathf.Clamp(buttonHeight * 0.34f, 14f, 28f));
        }

        var labelRt = button.transform.Find("Text") as RectTransform;
        if (labelRt != null)
        {
            labelRt.offsetMin = new Vector2(3f, Mathf.Clamp(buttonHeight * 0.06f, 3f, 5f));
            labelRt.offsetMax = new Vector2(-3f, -Mathf.Clamp(buttonHeight * 0.52f, 24f, 44f));
            var label = labelRt.GetComponent<Text>();
            if (label != null) label.fontSize = Mathf.RoundToInt(Mathf.Clamp(buttonHeight * 0.20f, 10f, 16f));
        }

        var cd = button.transform.Find("CooldownText")?.GetComponent<Text>();
        if (cd != null) cd.fontSize = Mathf.RoundToInt(Mathf.Clamp(buttonHeight * 0.30f, 12f, 22f));
    }

    void BeginTechTargeting(int index, Vector2 screenPos)
    {
        EnsureBattleTechDefinitions();
        if (index < 0 || index >= _battleTechs.Length) return;

        var baseBuilding = GameManager.Instance?.PlayerMainBase;
        if (baseBuilding == null || baseBuilding.GetHP() <= 0)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert("主基地已摧毁，科技不可用");
            return;
        }

        var pc = RTSPlayerController.Instance;
        if (pc != null && pc.IsInPlacementMode)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert("建造放置中，无法释放科技");
            return;
        }

        float remain = GetTechCooldownRemaining(index);
        if (remain > 0f)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert($"{_battleTechs[index].Name} 冷却中：{Mathf.CeilToInt(remain)}秒");
            if (_techButtons != null && index < _techButtons.Length && _techButtons[index] != null)
                StartCoroutine(ShakeButton(_techButtons[index].GetComponent<RectTransform>()));
            return;
        }

        _techTargetingIndex = index;
        _techTargetStartScreen = screenPos;
        _techTargetStartTime = Time.unscaledTime;
        _techConsumedInputFrame = Time.frameCount;
        _techIgnoreInitialRelease = true;
        SetTechPanelOpen(false);
        CloseBuildMenu();
        EnsureTechTargetRing(index);
        UpdateTechTargetPoint(screenPos);
        _UiClickAudio.PlayClick();
        ShowAlert($"选择{_battleTechs[index].Name}释放范围");
    }

    void ReleaseTechTargeting(int index, Vector2 screenPos, bool force)
    {
        if (_techTargetingIndex < 0) return;
        if (force && _techConsumedInputFrame == Time.frameCount) return;
        if (index >= 0 && index != _techTargetingIndex) return;

        int active = _techTargetingIndex;
        bool overUiRelease = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool initialButtonRelease = _techIgnoreInitialRelease
            && Vector2.Distance(screenPos, _techTargetStartScreen) < 18f;
        bool tinyButtonTap = initialButtonRelease || (!force && overUiRelease);

        if (tinyButtonTap)
        {
            _techIgnoreInitialRelease = false;
            _techConsumedInputFrame = Time.frameCount;
            return;
        }
        _techIgnoreInitialRelease = false;

        if (!TryGetTechWorldPoint(screenPos, out Vector3 point))
        {
            _UiClickAudio.PlayDeny();
            ShowAlert("请选择战场地面释放科技");
            _techConsumedInputFrame = Time.frameCount;
            return;
        }

        CastBattleTech(active, point);
        CancelTechTargeting(false);
        _techConsumedInputFrame = Time.frameCount;
    }

    void CancelTechTargeting(bool notify)
    {
        if (_techTargetingIndex < 0) return;
        _techTargetingIndex = -1;
        if (_techTargetRing != null)
        {
            Destroy(_techTargetRing);
            _techTargetRing = null;
            _techTargetRingRenderer = null;
        }
        _techIgnoreInitialRelease = false;
        if (notify) ShowAlert("已取消科技释放");
    }

    void EnsureTechTargetRing(int index)
    {
        BattleTechSpec spec = _battleTechs[index];
        if (_techTargetRing == null)
        {
            _techTargetRing = FxResources.MakeGroundDisc(null, "TechTargetRange", spec.Radius,
                spec.Color, FxResources.DiscStyle.MediumRing, 0.06f);
            _techTargetRingRenderer = _techTargetRing.GetComponent<Renderer>();
        }

        _techTargetRing.transform.localScale = new Vector3(spec.Radius * 2f, spec.Radius * 2f, 1f);
        if (_techTargetRingRenderer != null)
            RendererColorUtil.TrySetColor(_techTargetRingRenderer, spec.Color);
    }

    void UpdateTechTargeting()
    {
        if (_techTargetingIndex < 0) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelTechTargeting(true);
            _techConsumedInputFrame = Time.frameCount;
            return;
        }

        if (TryGetTechPointerScreenPosition(out Vector2 screenPos))
            UpdateTechTargetPoint(screenPos);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (t.phase == TouchPhase.Canceled) CancelTechTargeting(false);
                else ReleaseTechTargeting(_techTargetingIndex, t.position, true);
            }
        }
#else
        if (Input.GetMouseButtonUp(0))
            ReleaseTechTargeting(_techTargetingIndex, Input.mousePosition, true);
#endif
    }

    bool TryGetTechPointerScreenPosition(out Vector2 screenPos)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount > 0)
        {
            screenPos = Input.GetTouch(0).position;
            return true;
        }
        screenPos = Vector2.zero;
        return false;
#else
        screenPos = Input.mousePosition;
        return true;
#endif
    }

    void UpdateTechTargetPoint(Vector2 screenPos)
    {
        if (_techTargetingIndex < 0) return;
        if (!TryGetTechWorldPoint(screenPos, out _techTargetPoint)) return;

        if (_techTargetRing != null)
        {
            BattleTechSpec spec = _battleTechs[_techTargetingIndex];
            _techTargetRing.transform.position = new Vector3(_techTargetPoint.x, _techTargetPoint.y + 0.06f, _techTargetPoint.z);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.035f;
            _techTargetRing.transform.localScale = new Vector3(spec.Radius * 2f * pulse, spec.Radius * 2f * pulse, 1f);
        }
    }

    bool TryGetTechWorldPoint(Vector2 screenPos, out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (!ground.Raycast(ray, out float distance)) return false;

        point = ray.GetPoint(distance);
        point.y = 0f;
        return true;
    }

    void CastBattleTech(int index, Vector3 point)
    {
        if (index < 0 || index >= _battleTechs.Length) return;
        BattleTechSpec spec = _battleTechs[index];
        int affected = ApplyBattleTechToUnits(spec, point);
        _techCooldownEnds[index] = Time.time + spec.Cooldown;
        SpawnTechCastPulse(spec, point);
        _UiClickAudio.PlayConfirm();
        ShowAlert($"{spec.Name} 已释放：影响 {affected} 个单位");
    }

    int ApplyBattleTechToUnits(BattleTechSpec spec, Vector3 point)
    {
        int affected = 0;
        var units = GameManager.Instance?.GetAllUnits();
        if (units == null) return 0;

        Vector2 center = new Vector2(point.x, point.z);
        for (int i = 0; i < units.Count; i++)
        {
            RTSUnit unit = units[i];
            if (unit == null || unit.IsDead() || !unit.IsPlayerOwned()) continue;

            Vector3 pos = unit.transform.position;
            if (Vector2.Distance(center, new Vector2(pos.x, pos.z)) > spec.Radius) continue;

            unit.ApplyTechBuff(spec.Id, spec.Duration, spec.MoveMultiplier, spec.DamageMultiplier,
                spec.AttackRangeBonus, spec.AttackIntervalMultiplier, spec.DefenseReduction,
                spec.SightRangeBonus, spec.RegenPerSecond, spec.Color);
            affected++;
        }

        return affected;
    }

    void SpawnTechCastPulse(BattleTechSpec spec, Vector3 point)
    {
        var pulse = FxResources.MakeGroundDisc(null, "TechCastPulse_" + spec.Id, spec.Radius,
            spec.Color, FxResources.DiscStyle.SoftDisc, 0.065f);
        pulse.transform.position = new Vector3(point.x, point.y + 0.065f, point.z);
        StartCoroutine(AnimateTechCastPulse(pulse, spec.Color));
    }

    System.Collections.IEnumerator AnimateTechCastPulse(GameObject pulse, Color color)
    {
        if (pulse == null) yield break;
        Renderer rd = pulse.GetComponent<Renderer>();
        Vector3 origin = pulse.transform.localScale;
        float t = 0f;
        const float dur = 0.42f;
        while (t < dur && pulse != null)
        {
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / dur);
            float s = Mathf.Lerp(0.35f, 1.08f, 1f - Mathf.Pow(1f - r, 2f));
            pulse.transform.localScale = origin * s;
            if (rd != null)
            {
                Color c = color;
                c.a = Mathf.Lerp(0.34f, 0f, r);
                RendererColorUtil.TrySetColor(rd, c);
            }
            yield return null;
        }
        if (pulse != null) Destroy(pulse);
    }

    float GetTechCooldownRemaining(int index)
    {
        if (_techCooldownEnds == null || index < 0 || index >= _techCooldownEnds.Length) return 0f;
        return Mathf.Max(0f, _techCooldownEnds[index] - Time.time);
    }

    void UpdateTechCooldownUi()
    {
        EnsureBattleTechDefinitions();
        if (_techButtons == null || _techButtons.Length == 0) return;

        for (int i = 0; i < _techButtons.Length; i++)
        {
            if (_techButtons[i] == null) continue;
            BattleTechSpec spec = _battleTechs[i];
            float remain = GetTechCooldownRemaining(i);
            bool cooling = remain > 0f;
            bool targeting = _techTargetingIndex == i;

            _techButtons[i].interactable = !cooling && _techTargetingIndex < 0;

            if (_techCooldownFills != null && i < _techCooldownFills.Length && _techCooldownFills[i] != null)
                _techCooldownFills[i].fillAmount = cooling ? Mathf.Clamp01(remain / Mathf.Max(0.01f, spec.Cooldown)) : 0f;

            if (_techCooldownTexts != null && i < _techCooldownTexts.Length && _techCooldownTexts[i] != null)
                _techCooldownTexts[i].text = cooling ? Mathf.CeilToInt(remain).ToString() : "";

            var img = _techButtons[i].GetComponent<Image>();
            if (img != null)
            {
                Color baseColor = new Color(0.075f, 0.095f, 0.090f, 0.96f);
                img.color = cooling
                    ? new Color(0.035f, 0.045f, 0.045f, 0.78f)
                    : targeting
                        ? Color.Lerp(baseColor, spec.Color, 0.48f)
                        : baseColor;
            }
        }
    }

    public bool IsTechTargetingInputActive => _techTargetingIndex >= 0 || _techConsumedInputFrame == Time.frameCount;

    void SetTechPanelOpen(bool open)
    {
        var canvas = GetHudCanvas();
        if (TechPanel == null && canvas != null && open)
            EnsureBattleTechPanel(canvas);

        _techPanelOpen = open;
        if (TechPanel != null)
        {
            if (open && canvas != null)
                ApplyBattleTechPanelLayout(canvas);
            TechPanel.SetActive(open);
            if (open) TechPanel.transform.SetAsLastSibling();
        }
    }

    void EnsureGameSettingsUi(Canvas canvas)
    {
        if (canvas == null) return;

        GameSettingsButton = EnsureSettingsButton(canvas.transform);
        GameSettingsPanel = EnsureSettingsPanel(canvas.transform);

        BindSettingsUiRefs();
        ApplySettingsPanelActionLayout();
        WireSettingsUi();
        SyncSettingsSlidersFromPrefs();

        if (GameSettingsPanel != null)
            GameSettingsPanel.SetActive(_settingsOpen);
        if (GameSettingsButton != null)
        {
            EnsureSettingsButtonInput(GameSettingsButton);
            GameSettingsButton.transform.SetAsLastSibling();
        }
    }

    Button EnsureSettingsButton(Transform canvas)
    {
        var existing = canvas.Find("GameSettingsButton");
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            button = CreatePopupButton(canvas, "GameSettingsButton", "",
                new Vector2(0.86f, 1f), new Vector2(0f, -34f), new Vector2(38f, 38f));
        }

        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.86f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -34f);
            rt.sizeDelta = new Vector2(38f, 38f);
        }

        StyleIconOnlyHudButton(button, "icons3/gear", "⚙",
            new Color(0.80f, 0.70f, 0.42f, 0.95f));
        EnsureSettingsButtonInput(button);
        return button;
    }

    void EnsureSettingsButtonInput(Button button)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        button.enabled = true;
        button.interactable = true;

        var image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        button.targetGraphic = image;

        var group = button.GetComponent<CanvasGroup>();
        if (group == null)
            group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = true;

        button.onClick.RemoveListener(ToggleGameSettings);
        button.onClick.AddListener(ToggleGameSettings);
    }

    void MaintainGameSettingsEntry(Canvas canvas)
    {
        if (canvas == null || IsGameOverVisible()) return;

        if (GameSettingsButton == null)
            GameSettingsButton = FindUiComponent<Button>(canvas.transform, "GameSettingsButton");

        if (GameSettingsPanel == null)
        {
            var panel = FindUiTransform(canvas.transform, "GameSettingsPanel");
            if (panel != null) GameSettingsPanel = panel.gameObject;
        }

        if (GameSettingsButton == null || GameSettingsPanel == null)
        {
            EnsureGameSettingsUi(canvas);
            return;
        }

        EnsureSettingsButtonInput(GameSettingsButton);

        if (GameSettingsCloseButton == null || GameSettingsReturnLobbyButton == null ||
            MasterVolumeSlider == null || SfxVolumeSlider == null)
        {
            BindSettingsUiRefs();
            ApplySettingsPanelActionLayout();
            WireSettingsUi();
        }

        if (_settingsOpen)
        {
            GameSettingsPanel.SetActive(true);
            GameSettingsPanel.transform.SetAsLastSibling();
        }
        else
        {
            GameSettingsButton.transform.SetAsLastSibling();
        }
    }

    GameObject EnsureSettingsPanel(Transform canvas)
    {
        var existing = canvas.Find("GameSettingsPanel");
        if (existing != null) return existing.gameObject;

        var panel = new GameObject("GameSettingsPanel");
        panel.transform.SetParent(canvas, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.64f);

        var card = new GameObject("SettingsCard");
        card.transform.SetParent(panel.transform, false);
        var crt = card.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(480f, 360f);
        card.AddComponent<Image>().color = new Color(0.045f, 0.060f, 0.075f, 0.98f);
        var outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateSettingsBar(card.transform, "SettingsTopLine", new Vector2(0f, 1f), Vector2.one,
            new Color(0.78f, 0.62f, 0.18f, 0.95f));
        CreateSettingsText(card.transform, "SettingsTitle", "设置",
            new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(360f, 48f), 28, TextAnchor.MiddleCenter, Color.white);
        var closeButton = CreatePopupButton(card.transform, "GameSettingsCloseButton", "×",
            new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(36f, 32f));
        var closeImg = closeButton.GetComponent<Image>();
        if (closeImg != null) closeImg.color = new Color(0.10f, 0.12f, 0.15f, 0.96f);
        CreateSettingsText(card.transform, "MasterVolumeLabel", "总音量",
            new Vector2(0f, 1f), new Vector2(96f, -118f), new Vector2(110f, 30f), 18, TextAnchor.MiddleLeft, new Color(0.90f, 0.96f, 1f));
        CreateSettingsText(card.transform, "MasterVolumeValueText", "80%",
            new Vector2(1f, 1f), new Vector2(-68f, -118f), new Vector2(70f, 30f), 17, TextAnchor.MiddleRight, new Color(1f, 0.90f, 0.55f));
        CreateSettingsSlider(card.transform, "MasterVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(20f, -155f), new Vector2(330f, 24f), new Color(0.42f, 0.72f, 0.95f));

        CreateSettingsText(card.transform, "SfxVolumeLabel", "音效音量",
            new Vector2(0f, 1f), new Vector2(96f, -204f), new Vector2(130f, 30f), 18, TextAnchor.MiddleLeft, new Color(0.90f, 0.96f, 1f));
        CreateSettingsText(card.transform, "SfxVolumeValueText", "100%",
            new Vector2(1f, 1f), new Vector2(-68f, -204f), new Vector2(70f, 30f), 17, TextAnchor.MiddleRight, new Color(1f, 0.90f, 0.55f));
        CreateSettingsSlider(card.transform, "SfxVolumeSlider",
            new Vector2(0.5f, 1f), new Vector2(20f, -241f), new Vector2(330f, 24f), new Color(0.70f, 0.88f, 0.42f));

        var lobbyButton = CreatePopupButton(card.transform, "GameSettingsReturnLobbyButton", "返回大厅",
            new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(180f, 44f));
        var lobbyImg = lobbyButton.GetComponent<Image>();
        if (lobbyImg != null) lobbyImg.color = new Color(0.54f, 0.16f, 0.16f, 0.98f);

        panel.SetActive(false);
        return panel;
    }

    void BindSettingsUiRefs()
    {
        if (GameSettingsPanel == null) return;
        var root = GameSettingsPanel.transform;
        GameSettingsCloseButton = FindSettingsComponent<Button>(root, "GameSettingsCloseButton");
        GameSettingsReturnLobbyButton = FindSettingsComponent<Button>(root, "GameSettingsReturnLobbyButton");
        MasterVolumeSlider = FindSettingsComponent<Slider>(root, "MasterVolumeSlider");
        MasterVolumeValueText = FindSettingsComponent<Text>(root, "MasterVolumeValueText");
        SfxVolumeSlider = FindSettingsComponent<Slider>(root, "SfxVolumeSlider");
        SfxVolumeValueText = FindSettingsComponent<Text>(root, "SfxVolumeValueText");
    }

    void ApplySettingsPanelActionLayout()
    {
        if (GameSettingsCloseButton == null && GameSettingsPanel != null)
        {
            var card = GameSettingsPanel.transform.Find("SettingsCard");
            if (card != null)
                GameSettingsCloseButton = CreatePopupButton(card, "GameSettingsCloseButton", "×",
                    new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(36f, 32f));
        }

        if (GameSettingsCloseButton != null)
        {
            GameSettingsCloseButton.gameObject.SetActive(true);
            var closeRt = GameSettingsCloseButton.GetComponent<RectTransform>();
            if (closeRt != null)
            {
                closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(0.5f, 0.5f);
                closeRt.anchoredPosition = new Vector2(-24f, -24f);
                closeRt.sizeDelta = new Vector2(36f, 32f);
            }
            var closeImg = GameSettingsCloseButton.GetComponent<Image>();
            if (closeImg != null) closeImg.color = new Color(0.10f, 0.12f, 0.15f, 0.96f);
            SetCloseButtonIcon(GameSettingsCloseButton);
        }

        if (GameSettingsReturnLobbyButton == null) return;
        GameSettingsReturnLobbyButton.gameObject.SetActive(true);
        var rt = GameSettingsReturnLobbyButton.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 24f);
        rt.sizeDelta = new Vector2(180f, 44f);
    }

    void WireSettingsUi()
    {
        if (GameSettingsButton != null)
        {
            GameSettingsButton.onClick.RemoveListener(ToggleGameSettings);
            GameSettingsButton.onClick.AddListener(ToggleGameSettings);
        }
        if (GameSettingsCloseButton != null)
        {
            GameSettingsCloseButton.onClick.RemoveListener(CloseGameSettings);
            GameSettingsCloseButton.onClick.AddListener(CloseGameSettings);
        }
        if (GameSettingsReturnLobbyButton != null)
        {
            GameSettingsReturnLobbyButton.onClick.RemoveListener(ReturnLobbyFromSettings);
            GameSettingsReturnLobbyButton.onClick.AddListener(ReturnLobbyFromSettings);
        }
        if (MasterVolumeSlider != null)
        {
            MasterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            MasterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }
        if (SfxVolumeSlider != null)
        {
            SfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            SfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
    }

    void SyncSettingsSlidersFromPrefs()
    {
        float master = Mathf.Clamp01(PlayerPrefs.GetFloat("music_vol", 0.8f));
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat("sfx_vol", 1f));

        if (MasterVolumeSlider != null)
            MasterVolumeSlider.SetValueWithoutNotify(master);
        if (SfxVolumeSlider != null)
            SfxVolumeSlider.SetValueWithoutNotify(sfx);

        UpdateVolumeLabels(master, sfx);
    }

    void ApplySavedAudioSettings()
    {
        float master = Mathf.Clamp01(PlayerPrefs.GetFloat("music_vol", 0.8f));
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat("sfx_vol", 1f));
        AudioListener.volume = master;
        _UiClickAudio.SetVolumeScale(sfx);
        _CombatSfx.SetVolumeScale(sfx);
    }

    void ToggleGameSettings()
    {
        SetGameSettingsOpen(!_settingsOpen);
    }

    void CloseGameSettings()
    {
        SetGameSettingsOpen(false);
    }

    void SetGameSettingsOpen(bool open)
    {
        var canvas = GetHudCanvas();
        if ((GameSettingsPanel == null || GameSettingsButton == null) && canvas != null)
            EnsureGameSettingsUi(canvas);

        if (open)
            SetTechPanelOpen(false);

        _settingsOpen = open;
        if (GameSettingsPanel != null)
        {
            GameSettingsPanel.SetActive(open);
            if (open) GameSettingsPanel.transform.SetAsLastSibling();
        }

        if (open)
        {
            CloseBuildMenu();
            CloseBuildingUpgradePanel();
            _UiClickAudio.PlayClick();
            EnsureBattleTimeRunning();
        }
    }

    void ReturnLobbyFromSettings()
    {
        _UiClickAudio.PlayConfirm();
        if (GameSettingsPanel != null) GameSettingsPanel.SetActive(false);
        _settingsOpen = false;
        Time.timeScale = 1f;
        GameManager.Instance?.ReturnToLobby();
    }

    void OnMasterVolumeChanged(float value)
    {
        float master = Mathf.Clamp01(value);
        AudioListener.volume = master;
        PlayerPrefs.SetFloat("music_vol", master);
        PlayerPrefs.Save();
        UpdateVolumeLabels(master, SfxVolumeSlider != null ? SfxVolumeSlider.value : _UiClickAudio.GetVolumeScale());
    }

    void OnSfxVolumeChanged(float value)
    {
        float sfx = Mathf.Clamp01(value);
        _UiClickAudio.SetVolumeScale(sfx);
        _CombatSfx.SetVolumeScale(sfx);
        PlayerPrefs.Save();
        UpdateVolumeLabels(MasterVolumeSlider != null ? MasterVolumeSlider.value : AudioListener.volume, sfx);
    }

    void UpdateVolumeLabels(float master, float sfx)
    {
        if (MasterVolumeValueText != null)
            MasterVolumeValueText.text = Mathf.RoundToInt(master * 100f) + "%";
        if (SfxVolumeValueText != null)
            SfxVolumeValueText.text = Mathf.RoundToInt(sfx * 100f) + "%";
    }

    Text CreateSettingsText(Transform parent, string name, string text, Vector2 anchor, Vector2 pos,
        Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    void CreateSettingsBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(0f, -3f);
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    Slider CreateSettingsSlider(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgrt = bg.AddComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = Vector2.zero;
        bgrt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0.10f, 0.13f, 0.15f, 1f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var fart = fillArea.AddComponent<RectTransform>();
        fart.anchorMin = Vector2.zero;
        fart.anchorMax = Vector2.one;
        fart.offsetMin = new Vector2(2f, 2f);
        fart.offsetMax = new Vector2(-2f, -2f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var sliderFillRT = fill.AddComponent<RectTransform>();
        sliderFillRT.anchorMin = Vector2.zero;
        sliderFillRT.anchorMax = Vector2.one;
        sliderFillRT.offsetMin = Vector2.zero;
        sliderFillRT.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var hart = handleArea.AddComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.offsetMin = new Vector2(8f, -5f);
        hart.offsetMax = new Vector2(-8f, 5f);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var hrt = handle.AddComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(26f, 34f);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.96f, 0.98f, 1f, 1f);

        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = sliderFillRT;
        slider.handleRect = hrt;
        slider.targetGraphic = handleImg;
        slider.value = 1f;
        return slider;
    }

    T FindSettingsComponent<T>(Transform root, string name) where T : Component
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child.GetComponent<T>();
        return null;
    }

    void RemoveReturnLobbyButton(Canvas canvas)
    {
        if (canvas == null) return;
        var existing = canvas.transform.Find("_ReturnLobbyButton");
        if (existing != null)
            Destroy(existing.gameObject);
    }

    /// <summary>底部两个选择工具按钮：全选、框选。</summary>
    void EnsureBottomRouteBar(Canvas canvas)
    {
        EnsureCommandBar(canvas);
    }

    void EnsureCommandBar(Canvas canvas)
    {
        if (canvas == null) return;
        var legacy = canvas.transform.Find("_RouteBar");
        if (legacy != null) DestroyHudObject(legacy.gameObject);
        var oldSingle = canvas.transform.Find("_SelectAllBtn");
        if (oldSingle != null) DestroyHudObject(oldSingle.gameObject);

        var bar = canvas.transform.Find("_CommandBar");
        if (bar != null)
        {
            if (RebindCommandBarButtons(bar))
            {
                LayoutCommandBar(bar);
                UpdateCommandBarState();
                return;
            }

            DestroyHudObject(bar.gameObject);
            bar = null;
        }

        _commandButtons = new Button[CommandButtonCount];
        _commandButtonImages = new Image[CommandButtonCount];
        _commandButtonBaseColors = new Color[CommandButtonCount];
        _commandButtonAccentColors = new Color[CommandButtonCount];

        var barGO = new GameObject("_CommandBar");
        barGO.transform.SetParent(canvas.transform, false);
        barGO.AddComponent<RectTransform>();
        bar = barGO.transform;
        LayoutCommandBar(bar);

        RegisterCommandButton(0, CreateCommandIconButton(bar, 0, "_Cmd_SelectAll", "◎", new Color(0.65f, 0.90f, 1f),
            () => SelectAllOwnedUnits(), "icons/btn_select_all", "全选"));
        RegisterCommandButton(1, CreateCommandIconButton(bar, 1, "_Cmd_BoxSelect", "□", new Color(1f, 0.78f, 0.32f),
            () => FindObjectOfType<RTSPlayerController>()?.ArmBoxSelectMode(), "icons/btn_box_select", "框选"));
        RegisterCommandButton(2, CreateCommandIconButton(bar, 2, "_Cmd_ParkAircraft", "P", new Color(0.42f, 0.96f, 0.78f),
            () => ParkSelectedAircraft(), null, "停机"));
        RegisterCommandButton(3, CreateCommandIconButton(bar, 3, "_Cmd_AttackGround", "G", new Color(1f, 0.46f, 0.22f),
            () => RTSPlayerController.Instance?.RequestAttackGroundMode(), null, "\u70ae\u51fb"));
        UpdateCommandBarState();
    }

    void LayoutSelectionPanels(Canvas canvas)
    {
        if (canvas == null) return;

        const float bottomClearance = 34f;
        PositionSelectionPanel(UnitInfoPanel, canvas.transform, UnitPanelPreferredSize, bottomClearance);
        PositionSelectionPanel(BuildingPanel, canvas.transform, BuildingPanelPreferredSize, bottomClearance);
        LayoutUnitPanelControls();
        LayoutBuildingPanelControls();
        ConfigurePanelCanvasGroup(UnitInfoPanel, 0.88f, false);
        ConfigurePanelCanvasGroup(BuildingPanel, 0.96f, true);
    }

    void PositionSelectionPanel(GameObject panel, Transform canvasRoot, Vector2 preferredSize, float bottomClearance)
    {
        if (panel == null || canvasRoot == null) return;
        if (panel.transform.parent != canvasRoot)
            panel.transform.SetParent(canvasRoot, false);

        var rt = panel.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-12f, bottomClearance);
        rt.sizeDelta = preferredSize;
    }

    void LayoutUnitPanelControls()
    {
        if (UnitInfoPanel == null) return;
        Transform panelRoot = UnitInfoPanel.transform;

        LayoutTopCenterText(UnitNameText, panelRoot, 4f, 28f, 18, TextAnchor.MiddleCenter);
        SetUiActive(FindPanelText(panelRoot, "UnitHPLabel"), false);
        LayoutTopStretch(UnitHPBar != null ? UnitHPBar.GetComponent<RectTransform>() : null, panelRoot, 14f, 14f, 50f, 14f);
        NormalizeUnitHpSlider();
        LayoutTopCenterText(UnitHPText, panelRoot, 64f, 78f, 12, TextAnchor.UpperCenter);
        if (UnitHPText != null)
        {
            UnitHPText.verticalOverflow = VerticalWrapMode.Overflow;
            UnitHPText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UnitHPText.lineSpacing = 0.88f;
            UnitHPText.fontStyle = FontStyle.Bold;
            EnsureReadableTextShadow(UnitHPText, new Vector2(1f, -1f));
        }

        if (SkillButton != null)
        {
            var rt = SkillButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 10f);
                rt.sizeDelta = new Vector2(106f, 30f);
            }

            var image = SkillButton.GetComponent<Image>();
            if (image == null)
                image = SkillButton.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.10f, 0.08f, 0.96f);
            SkillButton.targetGraphic = image;

            var outline = SkillButton.GetComponent<Outline>();
            if (outline == null)
                outline = SkillButton.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.74f, 0.28f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            var label = EnsureButtonLabel(SkillButton, "\u8f70\u70b8");
            label.fontSize = 13;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.95f, 0.86f);
            EnsureReadableTextShadow(label, new Vector2(1f, -1f));
            _skillButtonActionBound = false;
            SkillButton.gameObject.SetActive(false);
        }
    }

    void NormalizeUnitHpSlider()
    {
        if (UnitHPBar == null) return;

        UnitHPBar.interactable = false;
        UnitHPBar.transition = Selectable.Transition.None;
        UnitHPBar.direction = Slider.Direction.LeftToRight;
        UnitHPBar.minValue = 0f;
        UnitHPBar.maxValue = 1f;
        UnitHPBar.handleRect = null;

        var root = UnitHPBar.transform;
        var children = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child == null || child.transform == root) continue;

            string n = child.name;
            bool isTrackPart = n == "Background" || n == "BG" || n == "Fill Area" || n == "FillArea" || n == "Fill";
            if (!isTrackPart)
            {
                if (n.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    child.gameObject.SetActive(false);
                continue;
            }

            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.pivot = new Vector2(0.5f, 0.5f);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        var background = root.Find("Background") ?? root.Find("BG");
        var backgroundImage = background != null ? background.GetComponent<Image>() : null;
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(0.06f, 0.09f, 0.12f, 1f);
            backgroundImage.raycastTarget = false;
        }

        if (UnitHPBar.fillRect != null)
        {
            var fillImage = UnitHPBar.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.raycastTarget = false;
                UnitHPBar.targetGraphic = fillImage;
            }
        }
    }

    void ConfigurePanelCanvasGroup(GameObject panel, float alpha, bool blocksRaycasts)
    {
        if (panel == null) return;

        var group = panel.GetComponent<CanvasGroup>();
        if (group == null)
            group = panel.AddComponent<CanvasGroup>();

        group.alpha = alpha;
        group.interactable = blocksRaycasts;
        group.blocksRaycasts = blocksRaycasts;
    }

    void LayoutBuildingPanelControls()
    {
        if (BuildingPanel == null) return;
        Transform panelRoot = BuildingPanel.transform;
        RTSBuilding selectedBuilding = currentSelectedBuilding;
        bool isConstructing = selectedBuilding != null && selectedBuilding.bUnderConstruction;
        bool hasProduction = selectedBuilding != null
            && selectedBuilding.ProductionUnits != null
            && selectedBuilding.ProductionUnits.Length > 0;
        bool canShowUpgradeStatus = selectedBuilding != null
            && selectedBuilding.SupportsBuildingUpgrade
            && selectedBuilding.bPlayerOwned;
        bool showProductionSection = isConstructing || hasProduction;
        bool showEmbeddedUpgradeDetails = false;
        bool showStandaloneUpgradeDetails = false;
        bool showStatusOnlyText = false;
        bool showProductionText = showProductionSection || showStatusOnlyText;

        LayoutTopCenterText(BuildingNameText, panelRoot, 5f, 30f, 23, TextAnchor.MiddleCenter);
        EnsureReadableTextShadow(BuildingNameText, new Vector2(1.2f, -1.2f));
        const float statLabelLeft = 18f;
        const float statLabelWidth = 54f;
        const float statGap = 8f;
        const float hpValueRight = 18f;
        const float hpValueWidth = 86f;
        float statBarLeft = statLabelLeft + statLabelWidth + statGap;
        float hpBarRight = hpValueRight + hpValueWidth + statGap;

        var hpLabel = EnsurePanelLabel(panelRoot, "BldHPLabel", "耐久", new Color(0.80f, 1f, 0.74f));
        var prodLabel = EnsurePanelLabel(panelRoot, "ProdLabel", "进度", new Color(0.62f, 0.92f, 1f));
        LayoutTopStretch(BuildingHPBar != null ? BuildingHPBar.GetComponent<RectTransform>() : null, panelRoot, statBarLeft, hpBarRight, 47f, 10f);
        LayoutTopLeftText(hpLabel, panelRoot, statLabelLeft, 42f, statLabelWidth, 16f, 13, TextAnchor.MiddleLeft);
        StylePanelLabel(hpLabel, "耐久", new Color(0.80f, 1f, 0.74f));
        LayoutTopRightText(BuildingHPText, panelRoot, hpValueRight, 42f, hpValueWidth, 16f, 13, TextAnchor.MiddleRight);
        if (BuildingHPText != null)
        {
            BuildingHPText.fontStyle = FontStyle.Bold;
            BuildingHPText.color = new Color(0.82f, 1f, 0.78f);
            BuildingHPText.horizontalOverflow = HorizontalWrapMode.Overflow;
            EnsureReadableTextShadow(BuildingHPText, new Vector2(1f, -1f));
        }

        SetUiActive(prodLabel, showProductionSection);
        SetUiActive(ProductionBar, showProductionSection);
        SetUiActive(ProductionText, showProductionText);
        if (showProductionSection)
        {
            LayoutTopStretch(ProductionBar != null ? ProductionBar.GetComponent<RectTransform>() : null, panelRoot, statBarLeft, 18f, 75f, 10f);
            LayoutTopLeftText(prodLabel, panelRoot, statLabelLeft, 70f, statLabelWidth, 16f, 13, TextAnchor.MiddleLeft);
            StylePanelLabel(prodLabel, "进度", new Color(0.62f, 0.92f, 1f));
        }

        if (ProductionText != null)
        {
            if (ProductionText.transform.parent != panelRoot)
                ProductionText.transform.SetParent(panelRoot, false);
            ProductionText.transform.SetAsLastSibling();
            var rt = ProductionText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            if (showProductionSection)
            {
                if (showEmbeddedUpgradeDetails)
                {
                    rt.offsetMin = new Vector2(18f, -146f);
                    rt.offsetMax = new Vector2(-18f, -92f);
                    ProductionText.fontSize = 12;
                }
                else
                {
                    rt.offsetMin = new Vector2(18f, -120f);
                    rt.offsetMax = new Vector2(-18f, -94f);
                    ProductionText.fontSize = 13;
                }
            }
            else if (showStandaloneUpgradeDetails)
            {
                rt.offsetMin = new Vector2(18f, -196f);
                rt.offsetMax = new Vector2(-18f, -74f);
                ProductionText.fontSize = 13;
            }
            else
            {
                rt.offsetMin = new Vector2(18f, -96f);
                rt.offsetMax = new Vector2(-18f, -72f);
                ProductionText.fontSize = showStatusOnlyText ? 14 : 13;
            }
            ProductionText.alignment = (showStandaloneUpgradeDetails || showEmbeddedUpgradeDetails)
                ? TextAnchor.UpperLeft
                : TextAnchor.MiddleCenter;
            ProductionText.fontStyle = FontStyle.Bold;
            ProductionText.lineSpacing = (showStandaloneUpgradeDetails || showEmbeddedUpgradeDetails) ? 1f : 0.9f;
            ProductionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ProductionText.verticalOverflow = (showStandaloneUpgradeDetails || showEmbeddedUpgradeDetails)
                ? VerticalWrapMode.Overflow
                : VerticalWrapMode.Truncate;
            ProductionText.resizeTextForBestFit = showStandaloneUpgradeDetails || showEmbeddedUpgradeDetails;
            ProductionText.resizeTextMinSize = (showStandaloneUpgradeDetails || showEmbeddedUpgradeDetails) ? 10 : ProductionText.fontSize;
            ProductionText.resizeTextMaxSize = ProductionText.fontSize;
            EnsureReadableTextShadow(ProductionText, new Vector2(1.2f, -1.2f));
        }

        if (ProductionButtons != null)
        {
            const int columns = 3;
            bool showStandaloneUpgradeButton = canShowUpgradeStatus && !hasProduction;
            bool showEmbeddedUpgradeButton = canShowUpgradeStatus && hasProduction;
            int upgradeButtonIndex = canShowUpgradeStatus ? selectedBuilding.GetUpgradeButtonSlotIndex() : -1;
            int maxVisibleButtonIndex = -1;
            if (selectedBuilding != null && selectedBuilding.ProductionUnits != null)
            {
                int count = Mathf.Min(ProductionButtons.Length, selectedBuilding.ProductionUnits.Length);
                for (int i = 0; i < count; i++)
                {
                    if (ProductionButtons[i] != null && selectedBuilding.ProductionUnits[i] != null)
                        maxVisibleButtonIndex = i;
                }
            }
            if (showStandaloneUpgradeButton && upgradeButtonIndex >= 0 && upgradeButtonIndex < ProductionButtons.Length)
                maxVisibleButtonIndex = Mathf.Max(maxVisibleButtonIndex, upgradeButtonIndex);
            int rows = Mathf.Max(1, maxVisibleButtonIndex / columns + 1);
            var panelRect = panelRoot.GetComponent<RectTransform>();
            float panelHeight = panelRect != null ? Mathf.Max(panelRect.rect.height, panelRect.sizeDelta.y) : BuildingPanelPreferredSize.y;
            float panelWidth = panelRect != null ? Mathf.Max(panelRect.rect.width, panelRect.sizeDelta.x) : BuildingPanelPreferredSize.x;
            float safeRight = ProductionButtonLeft;
            float contentWidth = Mathf.Max(1f, panelWidth - ProductionButtonLeft - safeRight);
            float gapX = Mathf.Min(ProductionButtonGapX, Mathf.Max(4f, contentWidth * 0.025f));
            float buttonWidth = Mathf.Clamp(
                (contentWidth - (columns - 1) * gapX) / columns,
                1f,
                ProductionButtonWidth);
            float gridHeight = rows * ProductionButtonHeight + (rows - 1) * ProductionButtonGapY;
            const float embeddedUpgradeHeight = 36f;
            const float embeddedUpgradeGap = 12f;
            float upgradeReserve = showEmbeddedUpgradeButton ? embeddedUpgradeHeight + embeddedUpgradeGap : 0f;
            float top = Mathf.Max(ProductionButtonMinTop, panelHeight - ProductionButtonBottomPadding - upgradeReserve - gridHeight);
            if (showEmbeddedUpgradeButton)
                top = Mathf.Max(top, 178f);
            bool showQueueSlots = selectedBuilding != null
                && hasProduction
                && !selectedBuilding.bUnderConstruction;
            LayoutProductionQueueSlots(panelRoot, selectedBuilding, showQueueSlots, top);
            for (int i = 0; i < ProductionButtons.Length; i++)
            {
                var btn = ProductionButtons[i];
                if (btn == null) continue;
                if (btn.transform.parent != panelRoot)
                    btn.transform.SetParent(panelRoot, false);
                btn.transform.SetAsLastSibling();
                var rt = btn.GetComponent<RectTransform>();
                if (rt == null) continue;
                if ((showStandaloneUpgradeButton || showEmbeddedUpgradeButton) && i == upgradeButtonIndex)
                {
                    float upgradeButtonHeight = showEmbeddedUpgradeButton ? embeddedUpgradeHeight : 54f;
                    float upgradeButtonTop = showEmbeddedUpgradeButton
                        ? panelHeight - ProductionButtonBottomPadding - upgradeButtonHeight
                        : Mathf.Max(214f, panelHeight - ProductionButtonBottomPadding - upgradeButtonHeight);
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(18f, -upgradeButtonTop - upgradeButtonHeight);
                    rt.offsetMax = new Vector2(-18f, -upgradeButtonTop);

                    var mainBaseText = btn.GetComponentInChildren<Text>();
                    if (mainBaseText != null)
                    {
                        ConfigureProductionButtonText(mainBaseText, new Vector2(10f, 4f));
                        mainBaseText.alignment = TextAnchor.MiddleCenter;
                        mainBaseText.fontSize = showEmbeddedUpgradeButton ? 13 : 14;
                        mainBaseText.resizeTextForBestFit = true;
                        mainBaseText.resizeTextMinSize = 10;
                        mainBaseText.resizeTextMaxSize = showEmbeddedUpgradeButton ? 13 : 14;
                    }
                    continue;
                }
                int col = i % columns;
                int row = i / columns;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(
                    ProductionButtonLeft + col * (buttonWidth + gapX),
                    -top - row * (ProductionButtonHeight + ProductionButtonGapY));
                rt.sizeDelta = new Vector2(buttonWidth, ProductionButtonHeight);

                var text = btn.GetComponentInChildren<Text>();
                if (text != null)
                    ConfigureProductionButtonText(text, new Vector2(6f, 3f));
            }
        }
        else
        {
            LayoutProductionQueueSlots(panelRoot, selectedBuilding, false, 0f);
        }

        LayoutCancelProductionButton();
    }

    void LayoutTopCenterText(Text text, Transform panelRoot, float top, float height, int fontSize, TextAnchor alignment)
    {
        if (text == null || panelRoot == null) return;
        if (text.transform.parent != panelRoot)
            text.transform.SetParent(panelRoot, false);
        text.transform.SetAsLastSibling();
        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        var panelRect = panelRoot.GetComponent<RectTransform>();
        float panelWidth = panelRect != null ? Mathf.Max(panelRect.rect.width, panelRect.sizeDelta.x) : 276f;
        float width = Mathf.Max(220f, panelWidth - 36f);
        rt.sizeDelta = new Vector2(width, height);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    void LayoutTopLeftText(Text text, Transform panelRoot, float left, float top, float width, float height, int fontSize, TextAnchor alignment)
    {
        if (text == null || panelRoot == null) return;
        if (text.transform.parent != panelRoot)
            text.transform.SetParent(panelRoot, false);
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();
        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, height);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void LayoutTopRightText(Text text, Transform panelRoot, float right, float top, float width, float height, int fontSize, TextAnchor alignment)
    {
        if (text == null || panelRoot == null) return;
        if (text.transform.parent != panelRoot)
            text.transform.SetParent(panelRoot, false);
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();
        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-right, -top);
        rt.sizeDelta = new Vector2(width, height);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    void EnsureReadableTextShadow(Text text, Vector2 distance)
    {
        if (text == null) return;
        var shadow = GetOrAddUiShadow(text.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
        shadow.effectDistance = distance;
    }

    void ConfigureProductionButtonText(Text text, Vector2 padding)
    {
        if (text == null) return;

        text.fontSize = ProductionButtonFontSize;
        text.fontStyle = FontStyle.Bold;
        text.lineSpacing = 0.86f;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = ProductionButtonMinFontSize;
        text.resizeTextMaxSize = ProductionButtonFontSize;
        text.raycastTarget = false;

        var textRt = text.rectTransform;
        if (textRt != null)
        {
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = padding;
            textRt.offsetMax = -padding;
        }

        EnsureReadableTextShadow(text, new Vector2(1f, -1f));
    }

    void LayoutProductionQueueSlots(Transform panelRoot, RTSBuilding building, bool visible, float buttonTop)
    {
        if (!visible || panelRoot == null || building == null)
        {
            if (_productionQueueRoot != null)
                _productionQueueRoot.gameObject.SetActive(false);
            return;
        }

        EnsureProductionQueueSlots(panelRoot);
        if (_productionQueueRoot == null) return;

        float queueWidth = ProductionQueueVisibleSlots * ProductionQueueSlotSize
            + (ProductionQueueVisibleSlots - 1) * ProductionQueueSlotGap;
        float queueTop = Mathf.Max(
            ProductionQueueMinTop,
            buttonTop - ProductionQueueSlotSize - ProductionQueueGapToButtons);

        _productionQueueRoot.gameObject.SetActive(true);
        _productionQueueRoot.SetParent(panelRoot, false);
        _productionQueueRoot.SetAsLastSibling();
        _productionQueueRoot.anchorMin = new Vector2(0.5f, 1f);
        _productionQueueRoot.anchorMax = new Vector2(0.5f, 1f);
        _productionQueueRoot.pivot = new Vector2(0.5f, 1f);
        _productionQueueRoot.anchoredPosition = new Vector2(0f, -queueTop);
        _productionQueueRoot.sizeDelta = new Vector2(queueWidth, ProductionQueueSlotSize);

        UpdateProductionQueueSlots(building);
    }

    void EnsureProductionQueueSlots(Transform panelRoot)
    {
        if (panelRoot == null) return;

        if (_productionQueueRoot != null)
        {
            if (_productionQueueRoot.parent != panelRoot)
                _productionQueueRoot.SetParent(panelRoot, false);
            return;
        }

        var root = new GameObject("_ProductionQueue");
        root.transform.SetParent(panelRoot, false);
        _productionQueueRoot = root.AddComponent<RectTransform>();
        _productionQueueSlotBackgrounds = new Image[ProductionQueueVisibleSlots];
        _productionQueueSlotIcons = new Image[ProductionQueueVisibleSlots];
        _productionQueueSlotProgressFills = new Image[ProductionQueueVisibleSlots];

        for (int i = 0; i < ProductionQueueVisibleSlots; i++)
        {
            var slot = new GameObject("Slot" + i);
            slot.transform.SetParent(_productionQueueRoot, false);
            var slotRt = slot.AddComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0f, 0.5f);
            slotRt.anchorMax = new Vector2(0f, 0.5f);
            slotRt.pivot = new Vector2(0f, 0.5f);
            slotRt.anchoredPosition = new Vector2(
                i * (ProductionQueueSlotSize + ProductionQueueSlotGap),
                0f);
            slotRt.sizeDelta = new Vector2(ProductionQueueSlotSize, ProductionQueueSlotSize);

            var bg = slot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.09f, 0.72f);
            bg.raycastTarget = false;
            var outline = slot.AddComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.24f, 0.30f, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            _productionQueueSlotBackgrounds[i] = bg;

            var icon = new GameObject("Icon");
            icon.transform.SetParent(slot.transform, false);
            var iconRt = icon.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.18f);
            iconRt.anchorMax = new Vector2(0.86f, 0.82f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImage = icon.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            _productionQueueSlotIcons[i] = iconImage;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(slot.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 0f);
            fillRt.pivot = new Vector2(0.5f, 0f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = new Vector2(0f, 4f);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.32f, 0.54f, 0.82f, 0.68f);
            fillImage.raycastTarget = false;
            _productionQueueSlotProgressFills[i] = fillImage;
        }
    }

    void UpdateProductionQueueSlots(RTSBuilding building)
    {
        if (_productionQueueRoot == null
            || _productionQueueSlotBackgrounds == null
            || _productionQueueSlotIcons == null
            || _productionQueueSlotProgressFills == null)
            return;

        int queueCount = building != null && building.ProductionQueue != null
            ? building.ProductionQueue.Count
            : 0;
        float currentProgress = building != null ? Mathf.Clamp01(building.ProductionProgress) : 0f;

        for (int i = 0; i < ProductionQueueVisibleSlots; i++)
        {
            bool filled = building != null && i < queueCount;
            bool isCurrent = filled && i == 0;
            var bg = _productionQueueSlotBackgrounds[i];
            var icon = _productionQueueSlotIcons[i];
            var fill = _productionQueueSlotProgressFills[i];
            if (bg == null || icon == null || fill == null) continue;

            bg.color = filled
                ? new Color(0.10f, 0.16f, 0.21f, 0.96f)
                : new Color(0.04f, 0.07f, 0.09f, 0.72f);

            var outline = bg.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = isCurrent
                    ? new Color(1f, 0.82f, 0.26f, 0.85f)
                    : filled
                        ? new Color(0.43f, 0.68f, 0.92f, 0.65f)
                        : new Color(0.16f, 0.24f, 0.30f, 0.55f);
            }

            Sprite sprite = null;
            if (filled
                && building.ProductionQueue[i] >= 0
                && building.ProductionUnits != null
                && building.ProductionQueue[i] < building.ProductionUnits.Length)
            {
                sprite = GetProductionIconSprite(building.ProductionUnits[building.ProductionQueue[i]]);
            }

            icon.gameObject.SetActive(filled && sprite != null);
            icon.sprite = sprite;
            icon.color = isCurrent
                ? new Color(1f, 0.95f, 0.72f, 1f)
                : new Color(0.90f, 0.94f, 1f, 0.92f);

            fill.gameObject.SetActive(filled);
            var fillRt = fill.rectTransform;
            if (filled)
            {
                fill.color = isCurrent
                    ? new Color(1f, 0.80f, 0.25f, 0.96f)
                    : new Color(0.32f, 0.54f, 0.82f, 0.68f);
                fillRt.anchorMin = new Vector2(0f, 0f);
                fillRt.anchorMax = new Vector2(isCurrent ? currentProgress : 1f, 0f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = new Vector2(0f, 4f);
            }
        }
    }

    static void SetUiActive(Component component, bool active)
    {
        if (component == null) return;
        if (component.gameObject.activeSelf != active)
            component.gameObject.SetActive(active);
    }

    void EnsureReadableTextOutline(Text text, Vector2 distance, Color color)
    {
        if (text == null) return;
        var outline = text.GetComponent<Outline>();
        if (outline == null)
            outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    Text FindPanelText(Transform panelRoot, string name)
    {
        if (panelRoot == null || string.IsNullOrEmpty(name)) return null;

        var direct = panelRoot.Find(name);
        if (direct != null)
        {
            var directText = direct.GetComponent<Text>();
            if (directText != null) return directText;
        }

        var texts = panelRoot.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == name)
                return texts[i];
        }

        return null;
    }

    Text EnsurePanelLabel(Transform panelRoot, string name, string label, Color color)
    {
        if (panelRoot == null) return null;

        var text = FindPanelText(panelRoot, name);
        if (text == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panelRoot, false);
            go.AddComponent<RectTransform>();
            text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.raycastTarget = false;
        }

        StylePanelLabel(text, label, color);
        return text;
    }

    void StylePanelLabel(Text text, string label, Color color)
    {
        if (text == null) return;
        text.gameObject.SetActive(true);
        text.text = label;
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.transform.SetAsLastSibling();
        EnsureReadableTextShadow(text, new Vector2(1f, -1f));
        EnsureReadableTextOutline(text, new Vector2(0.8f, -0.8f), new Color(0f, 0f, 0f, 0.72f));
    }

    void LayoutTopStretchText(Text text, Transform panelRoot, float left, float right, float top, float height, int fontSize, TextAnchor alignment)
    {
        if (text == null || panelRoot == null) return;
        if (text.transform.parent != panelRoot)
            text.transform.SetParent(panelRoot, false);
        text.gameObject.SetActive(true);
        text.transform.SetAsLastSibling();
        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    void LayoutTopStretch(RectTransform rt, Transform panelRoot, float left, float right, float top, float height)
    {
        if (rt == null || panelRoot == null) return;
        if (rt.transform.parent != panelRoot)
            rt.transform.SetParent(panelRoot, false);
        rt.transform.SetAsLastSibling();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    void LayoutCancelProductionButton()
    {
        if (_cancelProductionBtn == null || BuildingPanel == null) return;
        if (_cancelProductionBtn.transform.parent != BuildingPanel.transform)
            _cancelProductionBtn.transform.SetParent(BuildingPanel.transform, false);
        _cancelProductionBtn.transform.SetAsLastSibling();
        var rt = _cancelProductionBtn.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-10f, -7f);
        rt.sizeDelta = new Vector2(38f, 30f);
    }

    struct CommandButtonParts
    {
        public Button Button;
        public Image Image;
        public Color BaseColor;
        public Color AccentColor;
    }

    private Button[] _commandButtons;
    private Image[] _commandButtonImages;
    private Color[] _commandButtonBaseColors;
    private Color[] _commandButtonAccentColors;
    private RectTransform _productionQueueRoot;
    private Image[] _productionQueueSlotBackgrounds;
    private Image[] _productionQueueSlotIcons;
    private Image[] _productionQueueSlotProgressFills;
    private const int CommandButtonCount = 4;
    private const float CommandButtonPad = 8f;
    private const float CommandButtonWidth = 58f;
    private const float CommandButtonStep = 66f;
    private const float CommandBarBottom = 8f;
    private const float CommandBarHeight = 62f;
    private const float CommandBarWidth = CommandButtonPad * 2f + CommandButtonWidth + CommandButtonStep * (CommandButtonCount - 1);
    static readonly Dictionary<string, Sprite> ProductionUnitIconCache = new Dictionary<string, Sprite>(16);

    void DestroyHudObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    void LayoutCommandBar(Transform bar)
    {
        if (bar == null) return;

        bar.gameObject.SetActive(true);
        var rt = bar.GetComponent<RectTransform>();
        if (rt == null)
            rt = bar.gameObject.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, CommandBarBottom);
        rt.sizeDelta = new Vector2(CommandBarWidth, CommandBarHeight);

        var bg = bar.GetComponent<Image>();
        if (bg == null)
            bg = bar.gameObject.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.025f, 0.025f, 0.78f);
        bg.raycastTarget = false;

        var ol = bar.GetComponent<Outline>();
        if (ol == null)
            ol = bar.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.95f, 0.76f, 0.32f, 0.65f);
        ol.effectDistance = new Vector2(1.2f, -1.2f);

        var group = bar.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        bar.SetAsLastSibling();
    }

    bool RebindCommandBarButtons(Transform bar)
    {
        if (bar == null) return false;

        var selectAll = FindUiComponent<Button>(bar, "_Cmd_SelectAll");
        var boxSelect = FindUiComponent<Button>(bar, "_Cmd_BoxSelect");
        var parkAircraft = FindUiComponent<Button>(bar, "_Cmd_ParkAircraft");
        var attackGround = FindUiComponent<Button>(bar, "_Cmd_AttackGround");
        if (selectAll == null || boxSelect == null || parkAircraft == null || attackGround == null)
            return false;

        _commandButtons = new Button[CommandButtonCount];
        _commandButtonImages = new Image[CommandButtonCount];
        _commandButtonBaseColors = new Color[CommandButtonCount];
        _commandButtonAccentColors = new Color[CommandButtonCount];

        RegisterExistingCommandButton(0, selectAll, new Color(0.65f, 0.90f, 1f), () => SelectAllOwnedUnits());
        RegisterExistingCommandButton(1, boxSelect, new Color(1f, 0.78f, 0.32f), () => FindObjectOfType<RTSPlayerController>()?.ArmBoxSelectMode());
        RegisterExistingCommandButton(2, parkAircraft, new Color(0.42f, 0.96f, 0.78f), () => ParkSelectedAircraft());
        RegisterExistingCommandButton(3, attackGround, new Color(1f, 0.46f, 0.22f), () => RTSPlayerController.Instance?.RequestAttackGroundMode());
        return true;
    }

    void RegisterExistingCommandButton(int index, Button button, Color accent, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(CommandButtonPad + index * CommandButtonStep, 0f);
            rt.sizeDelta = new Vector2(CommandButtonWidth, 50f);
        }

        var img = button.GetComponent<Image>();
        if (img == null)
            img = button.gameObject.AddComponent<Image>();
        Color baseColor = new Color(0.08f, 0.10f, 0.105f, 0.98f);
        img.color = baseColor;
        button.targetGraphic = img;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        RegisterCommandButton(index, new CommandButtonParts
        {
            Button = button,
            Image = img,
            BaseColor = baseColor,
            AccentColor = accent
        });
    }

    void RegisterCommandButton(int index, CommandButtonParts parts)
    {
        if (_commandButtons == null || index < 0 || index >= _commandButtons.Length) return;
        _commandButtons[index] = parts.Button;
        _commandButtonImages[index] = parts.Image;
        _commandButtonBaseColors[index] = parts.BaseColor;
        _commandButtonAccentColors[index] = parts.AccentColor;
    }

    CommandButtonParts CreateCommandIconButton(Transform parent, int index, string name, string glyph, Color accent, UnityEngine.Events.UnityAction action, string spritePath = null, string caption = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(CommandButtonPad + index * CommandButtonStep, 0f);
        rt.sizeDelta = new Vector2(CommandButtonWidth, 50f);

        var img = go.AddComponent<Image>();
        Color baseColor = new Color(0.08f, 0.10f, 0.105f, 0.98f);
        img.color = baseColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var outline = go.AddComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(1f, -1f);

        var stripe = new GameObject("Accent");
        stripe.transform.SetParent(go.transform, false);
        var srt = stripe.AddComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(0f, 3f);
        var si = stripe.AddComponent<Image>();
        si.color = accent;
        si.raycastTarget = false;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(2f, 1f);
        trt.offsetMax = new Vector2(-2f, -1f);
        if (!string.IsNullOrEmpty(caption))
        {
            trt.anchorMax = new Vector2(1f, 0.78f);
            trt.offsetMin = new Vector2(2f, 6f);
            trt.offsetMax = new Vector2(-2f, -1f);
        }
        var tx = textGO.AddComponent<Text>();
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tx.font == null) tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.text = glyph;
        tx.fontSize = 30;
        tx.fontStyle = FontStyle.Bold;
        tx.alignment = TextAnchor.MiddleCenter;
        tx.color = new Color(0.96f, 0.98f, 1f);
        tx.raycastTarget = false;
        tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        tx.verticalOverflow = VerticalWrapMode.Overflow;

        // 有图片路径时，覆盖文字 glyph，用 Sprite 显示
        if (!string.IsNullOrEmpty(spritePath))
        {
            var sp = LoadHudSprite(spritePath);
            if (sp != null)
            {
                tx.text = "";
                var iconGO = new GameObject("SpriteIcon");
                iconGO.transform.SetParent(go.transform, false);
                var irt = iconGO.AddComponent<RectTransform>();
                irt.anchorMin = !string.IsNullOrEmpty(caption) ? new Vector2(0.14f, 0.30f) : new Vector2(0.1f, 0.1f);
                irt.anchorMax = !string.IsNullOrEmpty(caption) ? new Vector2(0.86f, 0.88f) : new Vector2(0.9f, 0.9f);
                irt.offsetMin = Vector2.zero;
                irt.offsetMax = Vector2.zero;
                var simg = iconGO.AddComponent<Image>();
                simg.sprite = sp;
                simg.preserveAspect = true;
                simg.raycastTarget = false;
            }
        }

        if (!string.IsNullOrEmpty(caption))
        {
            var captionGO = new GameObject("Caption");
            captionGO.transform.SetParent(go.transform, false);
            var crt = captionGO.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = new Vector2(1f, 0.34f);
            crt.offsetMin = new Vector2(1f, 1f);
            crt.offsetMax = new Vector2(-1f, -1f);
            var ctext = captionGO.AddComponent<Text>();
            ctext.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (ctext.font == null) ctext.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            ctext.text = caption;
            ctext.fontSize = 11;
            ctext.fontStyle = FontStyle.Bold;
            ctext.alignment = TextAnchor.MiddleCenter;
            ctext.color = new Color(0.88f, 0.96f, 1f);
            ctext.raycastTarget = false;
            ctext.horizontalOverflow = HorizontalWrapMode.Overflow;
            ctext.verticalOverflow = VerticalWrapMode.Overflow;
            EnsureReadableTextShadow(ctext, new Vector2(1f, -1f));
        }

        return new CommandButtonParts
        {
            Button = btn,
            Image = img,
            BaseColor = baseColor,
            AccentColor = accent
        };
    }

    public void ShowRightClickCommandMenu(Vector2 screenPos, bool canAttackGround, bool hasAircraft, System.Action<string> handler)
    {
        var canvas = GetHudCanvas();
        if (canvas == null)
            return;

        EnsureRightClickCommandMenu(canvas);
        if (_rightClickCommandMenu == null)
            return;

        _rightClickCommandHandler = handler;
        ConfigureRightClickCommandButton("move", true);
        ConfigureRightClickCommandButton("attackMove", true);
        ConfigureRightClickCommandButton("attackGround", canAttackGround);
        ConfigureRightClickCommandButton("patrol", true);
        ConfigureRightClickCommandButton("guard", true);
        ConfigureRightClickCommandButton("stop", true);
        ConfigureRightClickCommandButton("park", hasAircraft);
        PositionRightClickCommandMenu(canvas, screenPos);
        _rightClickCommandMenu.SetActive(true);
        RestoreHudActionUiStacking(canvas);
    }

    public void HideRightClickCommandMenu()
    {
        if (_rightClickCommandMenu != null)
            _rightClickCommandMenu.SetActive(false);
        _rightClickCommandHandler = null;
    }

    void EnsureRightClickCommandMenu(Canvas canvas)
    {
        if (canvas == null)
            return;

        if (_rightClickCommandMenu != null)
        {
            if (_rightClickCommandMenu.transform.parent != canvas.transform)
                _rightClickCommandMenu.transform.SetParent(canvas.transform, false);
            return;
        }

        var existing = canvas.transform.Find("_RightClickCommandMenu");
        if (existing != null)
        {
            _rightClickCommandMenu = existing.gameObject;
            WireRightClickCommandButtons();
            return;
        }

        _rightClickCommandMenu = CreateRightClickCommandMenu(canvas.transform);
        WireRightClickCommandButtons();
        _rightClickCommandMenu.SetActive(false);
    }

    GameObject CreateRightClickCommandMenu(Transform parent)
    {
        var menu = new GameObject("_RightClickCommandMenu");
        menu.transform.SetParent(parent, false);
        var rt = menu.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(178f, 292f);

        var image = menu.AddComponent<Image>();
        image.color = new Color(0.03f, 0.045f, 0.05f, 0.96f);
        image.raycastTarget = true;

        var outline = menu.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.72f, 0.28f, 0.78f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var shadow = GetOrAddUiShadow(menu);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        shadow.effectDistance = new Vector2(4f, -4f);

        CreateRightClickMenuLabel(menu.transform, "快速命令", new Vector2(0f, 1f), new Vector2(89f, -18f), new Vector2(156f, 26f), 15);
        string[] commands = { "move", "attackMove", "attackGround", "patrol", "guard", "stop", "park" };
        string[] labels = { "移动", "攻击移动", "炮击地点", "巡逻", "守卫", "停止", "飞机返场" };
        for (int i = 0; i < commands.Length; i++)
        {
            var button = CreatePopupButton(
                menu.transform,
                "_RClick_" + commands[i],
                labels[i],
                new Vector2(0.5f, 1f),
                new Vector2(0f, -50f - i * 34f),
                new Vector2(154f, 30f));
            button.gameObject.AddComponent<RightClickCommandButton>().Command = commands[i];
        }

        return menu;
    }

    void CreateRightClickMenuLabel(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
    {
        var go = new GameObject("_RightClickTitle");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null) label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.92f, 0.88f, 0.70f);
        label.raycastTarget = false;
        EnsureReadableTextShadow(label, new Vector2(1f, -1f));
    }

    void WireRightClickCommandButtons()
    {
        if (_rightClickCommandMenu == null)
            return;

        var buttons = _rightClickCommandMenu.GetComponentsInChildren<RightClickCommandButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var marker = buttons[i];
            if (marker == null) continue;
            var button = marker.GetComponent<Button>();
            if (button == null) continue;
            string command = marker.Command;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => InvokeRightClickCommand(command));
        }
    }

    void ConfigureRightClickCommandButton(string command, bool enabled)
    {
        if (_rightClickCommandMenu == null)
            return;

        var marker = FindRightClickCommandButton(command);
        var button = marker != null ? marker.GetComponent<Button>() : null;
        if (button != null)
            button.interactable = enabled;

        var image = marker != null ? marker.GetComponent<Image>() : null;
        if (image != null)
            image.color = enabled
                ? (image.sprite != null ? Color.white : new Color(0.14f, 0.18f, 0.11f, 0.92f))
                : new Color(0.42f, 0.42f, 0.42f, 0.82f);
    }

    RightClickCommandButton FindRightClickCommandButton(string command)
    {
        if (_rightClickCommandMenu == null)
            return null;

        var buttons = _rightClickCommandMenu.GetComponentsInChildren<RightClickCommandButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].Command == command)
                return buttons[i];
        }
        return null;
    }

    void PositionRightClickCommandMenu(Canvas canvas, Vector2 screenPos)
    {
        if (canvas == null || _rightClickCommandMenu == null)
            return;

        var canvasRt = canvas.transform as RectTransform;
        var menuRt = _rightClickCommandMenu.GetComponent<RectTransform>();
        if (canvasRt == null || menuRt == null)
            return;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, uiCamera, out Vector2 local))
            local = Vector2.zero;

        Vector2 canvasSize = canvasRt.rect.size;
        Vector2 menuSize = menuRt.sizeDelta;
        float halfW = canvasSize.x * 0.5f;
        float halfH = canvasSize.y * 0.5f;
        local.x = Mathf.Clamp(local.x + 10f, -halfW + 8f, halfW - menuSize.x - 8f);
        local.y = Mathf.Clamp(local.y - 10f, -halfH + menuSize.y + 8f, halfH - 8f);
        menuRt.anchoredPosition = local;
    }

    void InvokeRightClickCommand(string command)
    {
        var handler = _rightClickCommandHandler;
        HideRightClickCommandMenu();
        handler?.Invoke(command);
    }

    void UpdateCommandBarState()
    {
        if (_commandButtons == null || _commandButtonImages == null) return;
        var pc = RTSPlayerController.Instance;
        bool inPlacement = pc != null && pc.IsInPlacementMode;

        SetCommandButtonState(0, pc != null && !inPlacement, false);
        SetCommandButtonState(1, pc != null && !inPlacement, pc != null && pc.IsBoxSelectArmed);
        SetCommandButtonState(2, pc != null && !inPlacement && HasParkableSelectedAircraft(), HasSelectedAircraftParking());
        SetCommandButtonState(3, pc != null && !inPlacement && HasAttackGroundSelectedUnits(), pc != null && pc.IsAttackGroundPending);
    }

    void SetCommandButtonState(int index, bool enabled, bool active)
    {
        if (_commandButtons == null || index < 0 || index >= _commandButtons.Length) return;
        var btn = _commandButtons[index];
        var img = _commandButtonImages != null ? _commandButtonImages[index] : null;
        if (btn != null) btn.interactable = enabled;
        if (img == null) return;

        Color baseColor = _commandButtonBaseColors != null ? _commandButtonBaseColors[index] : new Color(0.16f, 0.19f, 0.12f, 0.94f);
        Color accent = _commandButtonAccentColors != null ? _commandButtonAccentColors[index] : new Color(1f, 0.92f, 0.55f);
        if (!enabled)
            img.color = new Color(0.07f, 0.08f, 0.06f, 0.55f);
        else if (active)
            img.color = Color.Lerp(baseColor, accent, 0.55f);
        else
            img.color = baseColor;
    }

    void SelectAllOwnedUnits()
    {
        var pc = FindObjectOfType<RTSPlayerController>();
        pc?.SelectAllOwnedUnits();
    }

    void ParkSelectedAircraft()
    {
        RTSPlayerController.Instance?.ParkSelectedAircraft();
    }

    void UpdateSelectedUnitSkillButton()
    {
        if (SkillButton == null)
            return;

        bool hasBomber = false;
        for (int i = 0; i < currentSelectedUnits.Count; i++)
        {
            if (currentSelectedUnits[i] is Bomber)
            {
                hasBomber = true;
                break;
            }
        }

        if (!hasBomber)
        {
            SkillButton.gameObject.SetActive(false);
            return;
        }

        var pc = RTSPlayerController.Instance;
        bool pending = pc != null && pc.IsBombingRunPending;
        bool awaitingDirection = pc != null && pc.IsBombingRunDirectionPending;
        if (!_skillButtonActionBound)
        {
            SkillButton.onClick.RemoveAllListeners();
            SkillButton.onClick.AddListener(() => RTSPlayerController.Instance?.RequestBombingRunMode());
            _skillButtonActionBound = true;
        }

        var image = SkillButton.GetComponent<Image>();
        if (image != null)
            image.color = awaitingDirection
                ? new Color(0.54f, 0.18f, 0.10f, 0.98f)
                : (pending ? new Color(0.42f, 0.14f, 0.09f, 0.98f) : new Color(0.26f, 0.10f, 0.08f, 0.96f));

        var label = EnsureButtonLabel(SkillButton, "\u8f70\u70b8");
        label.text = awaitingDirection ? "\u5b9a\u65b9\u5411" : (pending ? "\u8f70\u70b8\u4e2d" : "\u8f70\u70b8");
        SkillButton.gameObject.SetActive(true);
    }

    Text EnsureButtonLabel(Button button, string fallback)
    {
        if (button == null)
            return null;

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
            return label;

        var go = new GameObject("Text");
        go.transform.SetParent(button.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 1f);
        rt.offsetMax = new Vector2(-2f, -1f);

        label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null)
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = fallback;
        label.fontSize = 13;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    bool HasParkableSelectedAircraft()
    {
        for (int i = 0; i < currentSelectedUnits.Count; i++)
        {
            var air = currentSelectedUnits[i] as AirUnit;
            if (air != null && air.CanParkAtAirfield)
                return true;
        }
        return false;
    }

    bool HasAttackGroundSelectedUnits()
    {
        for (int i = 0; i < currentSelectedUnits.Count; i++)
        {
            RTSUnit unit = currentSelectedUnits[i];
            if (unit != null && !unit.IsDead() && unit.CanAttackGroundPoint)
                return true;
        }
        return false;
    }

    bool HasSelectedAircraftParking()
    {
        for (int i = 0; i < currentSelectedUnits.Count; i++)
        {
            var air = currentSelectedUnits[i] as AirUnit;
            if (air != null && air.IsReturningToRefuel)
                return true;
        }
        return false;
    }

    // 战地通讯消息队列
    private System.Collections.Generic.List<Text> _chatLines;
    private const int ChatMaxLines = 4;
    // 聊天消息按真实文本高度排版，避免自动换行后上下重叠。
    private const float ChatLineMinHeight = 19f;
    private const float ChatLineSpacing = 2f;
    private const int BattleVoiceSampleRate = 12000;
    private const int BattleVoiceLoopSeconds = 2;
    private const float BattleVoiceChunkSeconds = 0.16f;
    private const float BattleVoiceMinChunkSeconds = 0.08f;
    private Transform _chatBody;
    private InputField _chatInput;
    private Button _chatSendButton;
    private Button _voiceButton;
    private Text _voiceButtonLabel;
    private bool _chatSeeded;
    private bool _voiceRecording;
    private string _voiceDeviceName;
    private AudioClip _voiceClip;
    private AudioSource _voicePlaybackSource;
    private int _voiceReadPosition;
    private float _remoteVoiceActiveUntil;
    private string _remoteVoiceSpeaker;
    private RectTransform _chatPanelRect;
    private Button _chatDockButton;
    private Image _chatDockButtonImage;
    private Image _chatDockTailImage;
    private Image _chatDockUnreadDot;
    private Text _chatDockButtonLabel;
    private Sprite _chatDockSprite;
    private const string ChatIconResourcePath = "UI/Icons/chat_icon";
    private bool _chatPanelCollapsed = true;
    private bool _chatUnreadWhileCollapsed;

    /// <summary>外部调用：在战地通讯面板追加一条消息（自动滚动）。</summary>
    public void AppendChatMessage(string speaker, string message, Color color = default)
    {
        if (!ShouldShowBattleChatPanel()) return;
        if (_chatBody == null || _chatLines == null)
        {
            var canvas = GetHudCanvas();
            EnsureChatPanel(canvas);
        }
        if (_chatBody == null || _chatLines == null) return;

        if (color.a < 0.01f) color = new Color(1f, 0.92f, 0.55f);
        speaker = string.IsNullOrWhiteSpace(speaker) ? "通讯" : speaker.Trim();
        message = string.IsNullOrWhiteSpace(message) ? "" : message.Trim();
        if (message.Length > 80) message = message.Substring(0, 80) + "...";

        if (_chatLines.Count >= ChatMaxLines)
            RemoveChatLineAt(0);

        var ln = new GameObject("Line");
        ln.transform.SetParent(_chatBody, false);
        var rt = ln.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 19f);
        var tx = ln.AddComponent<Text>();
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tx.font == null) tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.text = $"[{speaker}] {message}";
        tx.fontSize = 13;
        tx.alignment = TextAnchor.MiddleLeft;
        tx.color = color;
        tx.raycastTarget = false;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = ln.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        shadow.effectDistance = new Vector2(1f, -1f);
        _chatLines.Add(tx);
        RefreshChatLineLayout();

        if (_chatPanelCollapsed)
        {
            _chatUnreadWhileCollapsed = true;
            RefreshChatDockButtonState();
        }
    }

    /// <summary>销毁指定聊天行，并把它从当前消息队列中移除。</summary>
    void RemoveChatLineAt(int index)
    {
        if (_chatLines == null || index < 0 || index >= _chatLines.Count) return;

        var line = _chatLines[index];
        if (line != null)
        {
            line.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(line.gameObject);
            else DestroyImmediate(line.gameObject);
        }
        _chatLines.RemoveAt(index);
    }

    /// <summary>根据文本实际高度重新排布聊天行，并裁掉超出正文区域的旧消息。</summary>
    void RefreshChatLineLayout()
    {
        if (_chatBody == null || _chatLines == null) return;

        for (int i = _chatLines.Count - 1; i >= 0; i--)
        {
            if (_chatLines[i] == null)
                _chatLines.RemoveAt(i);
        }

        float bodyHeight = GetChatBodyHeight();
        if (bodyHeight > 0f)
        {
            float keptHeight = 0f;
            for (int i = _chatLines.Count - 1; i >= 0; i--)
            {
                var line = _chatLines[i];
                if (line == null) continue;

                float lineHeight = MeasureChatLineHeight(line);
                float spacing = keptHeight > 0f ? ChatLineSpacing : 0f;
                if (keptHeight + spacing + lineHeight > bodyHeight)
                {
                    RemoveChatLineAt(i);
                    continue;
                }

                keptHeight += spacing + lineHeight;
            }
        }

        float y = 0f;
        for (int i = 0; i < _chatLines.Count; i++)
        {
            var line = _chatLines[i];
            if (line == null) continue;

            float lineHeight = MeasureChatLineHeight(line);
            var rt = line.rectTransform;
            rt.sizeDelta = new Vector2(0f, lineHeight);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += lineHeight + ChatLineSpacing;
        }
    }

    /// <summary>优先读取聊天正文区域高度，拿不到时再回退到整个聊天面板高度。</summary>
    float GetChatBodyHeight()
    {
        var bodyRect = _chatBody as RectTransform;
        if (bodyRect != null && bodyRect.rect.height > 0f)
            return bodyRect.rect.height;

        if (_chatPanelRect != null && _chatPanelRect.rect.height > 0f)
            return Mathf.Max(0f, _chatPanelRect.rect.height - 54f);

        return 0f;
    }

    /// <summary>测量一条聊天消息的真实高度，供自动换行后的布局计算使用。</summary>
    float MeasureChatLineHeight(Text line)
    {
        if (line == null) return ChatLineMinHeight;

        float availableWidth = 0f;
        var lineRect = line.rectTransform;
        if (lineRect != null && lineRect.rect.width > 0f)
            availableWidth = lineRect.rect.width;

        var bodyRect = _chatBody as RectTransform;
        if (availableWidth <= 0f && bodyRect != null && bodyRect.rect.width > 0f)
            availableWidth = bodyRect.rect.width;

        if (availableWidth <= 0f && _chatPanelRect != null && _chatPanelRect.rect.width > 0f)
            availableWidth = Mathf.Max(32f, _chatPanelRect.rect.width - 20f);

        availableWidth = Mathf.Max(32f, availableWidth);
        var settings = line.GetGenerationSettings(new Vector2(availableWidth, 0f));
        float preferredHeight = line.cachedTextGeneratorForLayout.GetPreferredHeight(line.text ?? string.Empty, settings) / line.pixelsPerUnit;
        return Mathf.Max(ChatLineMinHeight, Mathf.Ceil(preferredHeight) + 2f);
    }

    void EnsureChatPanel(Canvas canvas)
    {
        if (canvas == null) return;
        var existing = canvas.transform.Find("_ChatPanel");
        if (existing != null)
        {
            if (_chatBody != null && _chatInput != null)
            {
                _chatPanelRect = existing as RectTransform;
                LayoutChatPanel(_chatPanelRect);
                RefreshChatLineLayout();
                EnsureChatDockButton(canvas);
                SetChatPanelCollapsed(_chatPanelCollapsed);
                return;
            }
            Destroy(existing.gameObject);
        }

        var p = new GameObject("_ChatPanel");
        p.transform.SetParent(canvas.transform, false);
        var rt = p.AddComponent<RectTransform>();
        _chatPanelRect = rt;
        LayoutChatPanel(rt);
        var bg = p.AddComponent<Image>();
        bg.color = new Color(0.025f, 0.035f, 0.040f, 0.78f);
        bg.raycastTarget = true;
        var ol = p.AddComponent<Outline>();
        ol.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.55f);
        ol.effectDistance = new Vector2(1.2f, -1.2f);

        var body = new GameObject("Body");
        body.transform.SetParent(p.transform, false);
        var brt = body.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(10f, 45f);
        brt.offsetMax = new Vector2(-10f, -9f);
        body.AddComponent<RectMask2D>();
        _chatBody = body.transform;
        _chatLines = new System.Collections.Generic.List<Text>();

        _voiceButton = CreateChatButton(p.transform, "VoiceButton", "语聊",
            new Vector2(8f, 8f), new Vector2(52f, 30f), new Color(0.16f, 0.30f, 0.54f, 0.96f));
        _voiceButtonLabel = _voiceButton.GetComponentInChildren<Text>(true);
        if (_voiceButtonLabel != null)
        {
            _voiceButtonLabel.text = "\u260E";
            _voiceButtonLabel.fontSize = 20;
        }
        var voicePress = _voiceButton.gameObject.AddComponent<BattleChatVoicePressHandler>();
        voicePress.Owner = this;
        RefreshVoiceButtonState();

        _chatInput = CreateChatInputField(p.transform, "ChatInput", "输入文字...",
            new Vector2(68f, 8f), new Vector2(248f, 30f));
        _chatInput.onEndEdit.AddListener(_ =>
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                OnChatSendClicked();
        });

        _chatSendButton = CreateChatButton(p.transform, "SendButton", "发送",
            new Vector2(324f, 8f), new Vector2(68f, 30f), new Color(0.12f, 0.42f, 0.18f, 0.96f));
        _chatSendButton.onClick.AddListener(OnChatSendClicked);

        if (!_chatSeeded)
        {
            _chatSeeded = true;
            AppendChatMessage("司令部", "战斗开始，全军进入备战状态", new Color(1f, 0.92f, 0.55f));
            AppendChatMessage("情报部", "侦察到敌方基地位置已锁定", new Color(0.55f, 0.85f, 1f));
        }

        EnsureChatDockButton(canvas);
        SetChatPanelCollapsed(_chatPanelCollapsed);
    }

    void LayoutChatPanel(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(BattleHudChatDockLeft + BattleHudChatDockSize + 8f, BattleHudChatDockBottom);
        rt.sizeDelta = new Vector2(400f, 150f);
    }

    void EnsureChatDockButton(Canvas canvas)
    {
        if (canvas == null) return;

        var existing = canvas.transform.Find("_ChatDockButton");
        if (existing != null)
        {
            _chatDockButton = existing.GetComponent<Button>();
            _chatDockButtonImage = existing.GetComponent<Image>();
            _chatDockButtonLabel = existing.GetComponentInChildren<Text>(true);
            var tail = existing.Find("Tail");
            _chatDockTailImage = tail != null ? tail.GetComponent<Image>() : null;
            var unreadDot = existing.Find("UnreadDot");
            _chatDockUnreadDot = unreadDot != null ? unreadDot.GetComponent<Image>() : null;
            LayoutChatDockButton(existing as RectTransform);
            ApplyChatDockIconSprite();
            RefreshChatDockButtonState();
            return;
        }

        _chatDockButton = CreateChatButton(canvas.transform, "_ChatDockButton", "",
            new Vector2(BattleHudChatDockLeft, BattleHudChatDockBottom),
            new Vector2(BattleHudChatDockSize, BattleHudChatDockSize),
            new Color(0.10f, 0.16f, 0.22f, 0.96f));
        _chatDockButton.onClick.AddListener(ToggleChatPanelVisibility);
        _chatDockButtonImage = _chatDockButton.GetComponent<Image>();
        _chatDockButtonLabel = _chatDockButton.GetComponentInChildren<Text>(true);
        if (_chatDockButtonLabel != null)
        {
            _chatDockButtonLabel.fontSize = 20;
            _chatDockButtonLabel.alignment = TextAnchor.MiddleCenter;
            _chatDockButtonLabel.rectTransform.anchoredPosition = new Vector2(0f, -2f);
        }

        var outline = _chatDockButton.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.55f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);

        var tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(_chatDockButton.transform, false);
        var tailRt = tailGo.AddComponent<RectTransform>();
        tailRt.anchorMin = tailRt.anchorMax = tailRt.pivot = new Vector2(0f, 0f);
        tailRt.anchoredPosition = new Vector2(8f, -2f);
        tailRt.sizeDelta = new Vector2(12f, 12f);
        tailRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        _chatDockTailImage = tailGo.AddComponent<Image>();
        _chatDockTailImage.raycastTarget = false;

        var unreadGo = new GameObject("UnreadDot");
        unreadGo.transform.SetParent(_chatDockButton.transform, false);
        var unreadRt = unreadGo.AddComponent<RectTransform>();
        unreadRt.anchorMin = unreadRt.anchorMax = unreadRt.pivot = new Vector2(1f, 1f);
        unreadRt.anchoredPosition = new Vector2(-5f, -5f);
        unreadRt.sizeDelta = new Vector2(10f, 10f);
        _chatDockUnreadDot = unreadGo.AddComponent<Image>();
        _chatDockUnreadDot.color = new Color(1f, 0.80f, 0.24f, 0.98f);
        _chatDockUnreadDot.raycastTarget = false;
        var unreadOutline = unreadGo.AddComponent<Outline>();
        unreadOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        unreadOutline.effectDistance = new Vector2(1f, -1f);

        LayoutChatDockButton(_chatDockButton.transform as RectTransform);
        ApplyChatDockIconSprite();
        RefreshChatDockButtonState();
    }

    void LayoutChatDockButton(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(BattleHudChatDockLeft, BattleHudChatDockBottom);
        rt.sizeDelta = new Vector2(BattleHudChatDockSize, BattleHudChatDockSize);
    }

    void ToggleChatPanelVisibility()
    {
        SetChatPanelCollapsed(!_chatPanelCollapsed);
    }

    void SetChatPanelCollapsed(bool collapsed)
    {
        _chatPanelCollapsed = collapsed;

        if (_chatPanelRect != null)
            _chatPanelRect.gameObject.SetActive(!collapsed);

        if (collapsed)
        {
            if (_voiceRecording)
                EndVoiceRecording();
        }
        else
        {
            _chatUnreadWhileCollapsed = false;
            if (_chatInput != null)
                _chatInput.ActivateInputField();
        }

        RefreshChatDockButtonState();
    }

    void RefreshChatDockButtonState()
    {
        bool usingIconSprite = ApplyChatDockIconSprite();
        Color bubbleColor;
        if (_chatPanelCollapsed)
            bubbleColor = _chatUnreadWhileCollapsed ? new Color(0.30f, 0.24f, 0.08f, 0.98f) : new Color(0.10f, 0.16f, 0.22f, 0.96f);
        else
            bubbleColor = new Color(0.16f, 0.30f, 0.54f, 0.96f);

        SetChatButtonColor(_chatDockButton, usingIconSprite ? Color.white : bubbleColor);
        if (_chatDockButtonImage != null)
            _chatDockButtonImage.color = usingIconSprite
                ? (_chatUnreadWhileCollapsed ? new Color(1f, 0.96f, 0.78f, 1f) : Color.white)
                : bubbleColor;
        if (_chatDockTailImage != null)
        {
            _chatDockTailImage.gameObject.SetActive(!usingIconSprite);
            _chatDockTailImage.color = bubbleColor;
        }
        if (_chatDockButtonLabel != null)
        {
            _chatDockButtonLabel.gameObject.SetActive(!usingIconSprite);
            _chatDockButtonLabel.color = _chatUnreadWhileCollapsed ? new Color(1f, 0.95f, 0.76f) : new Color(0.96f, 0.98f, 1f);
        }
        if (_chatDockUnreadDot != null)
            _chatDockUnreadDot.gameObject.SetActive(_chatPanelCollapsed && _chatUnreadWhileCollapsed);

        var outline = _chatDockButton != null ? _chatDockButton.GetComponent<Outline>() : null;
        if (outline != null)
        {
            outline.effectColor = _chatUnreadWhileCollapsed
                ? new Color(1f, 0.80f, 0.26f, 0.92f)
                : (_chatPanelCollapsed ? new Color(0.78f, 0.62f, 0.18f, 0.55f) : new Color(0.46f, 0.72f, 1f, 0.75f));
        }
    }

    bool ApplyChatDockIconSprite()
    {
        if (_chatDockButtonImage == null)
            return false;

        if (_chatDockSprite == null)
            _chatDockSprite = Resources.Load<Sprite>(ChatIconResourcePath);

        bool hasSprite = _chatDockSprite != null;
        _chatDockButtonImage.sprite = hasSprite ? _chatDockSprite : null;
        _chatDockButtonImage.type = Image.Type.Simple;
        _chatDockButtonImage.preserveAspect = hasSprite;
        _chatDockButtonImage.raycastTarget = true;

        if (_chatDockButtonLabel != null)
        {
            _chatDockButtonLabel.text = hasSprite ? "" : "...";
            _chatDockButtonLabel.gameObject.SetActive(!hasSprite);
        }

        if (_chatDockTailImage != null)
            _chatDockTailImage.gameObject.SetActive(!hasSprite);

        return hasSprite;
    }

    Button CreateChatButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = color;
        var button = go.AddComponent<Button>();
        button.targetGraphic = img;
        SetChatButtonColor(button, color);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = label;
        text.fontSize = 14;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.96f, 0.98f, 1f);
        text.raycastTarget = false;
        EnsureReadableTextShadow(text, new Vector2(1f, -1f));
        return button;
    }

    void SetChatButtonColor(Button button, Color color)
    {
        if (button == null) return;

        var img = button.GetComponent<Image>();
        if (img != null)
            img.color = color;

        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.20f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.24f);
        colors.selectedColor = Color.Lerp(color, Color.white, 0.12f);
        button.colors = colors;
    }

    InputField CreateChatInputField(Transform parent, string name, string placeholder, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.055f, 0.065f, 0.96f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(go.transform, false);
        var phrt = phGO.AddComponent<RectTransform>();
        phrt.anchorMin = Vector2.zero;
        phrt.anchorMax = Vector2.one;
        phrt.offsetMin = new Vector2(8f, 0f);
        phrt.offsetMax = new Vector2(-8f, 0f);
        var ph = phGO.AddComponent<Text>();
        ph.font = font;
        ph.text = placeholder;
        ph.fontSize = 13;
        ph.alignment = TextAnchor.MiddleLeft;
        ph.color = new Color(0.65f, 0.72f, 0.78f, 0.75f);
        ph.raycastTarget = false;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 0f);
        trt.offsetMax = new Vector2(-8f, 0f);
        var txt = txtGO.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = 14;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;

        var input = go.AddComponent<InputField>();
        input.textComponent = txt;
        input.placeholder = ph;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 60;
        return input;
    }

    void OnChatSendClicked()
    {
        if (_chatInput == null) return;
        string msg = (_chatInput.text ?? "").Trim();
        if (string.IsNullOrEmpty(msg))
        {
            _chatInput.ActivateInputField();
            return;
        }

        _chatInput.text = "";
        SendLocalChatText(msg);
        _chatInput.ActivateInputField();
    }

    void SendLocalChatText(string msg)
    {
        AppendChatMessage("我", msg, new Color(0.80f, 1f, 0.78f));
        GameNetworkSync.Instance?.SendChatText(GetLocalChatName(), msg);
    }

    string GetLocalChatName()
    {
        var net = NetworkClient.Instance;
        if (net != null && !string.IsNullOrWhiteSpace(net.UserName))
            return net.UserName.Trim();
        return "队友";
    }

    public void BeginVoiceRecording()
    {
        if (_voiceRecording) return;
        var netSync = GameNetworkSync.Instance;
        if (netSync == null || !netSync.IsNetworkGame || !netSync.PeerConnected)
        {
            AppendChatMessage("系统", "团队语聊需要先连接房间内另一名玩家", new Color(1f, 0.75f, 0.45f));
            return;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            AppendChatMessage("系统", "已请求麦克风权限，请再次按住语聊", new Color(1f, 0.75f, 0.45f));
            return;
        }
#endif
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            AppendChatMessage("系统", "当前设备没有可用麦克风", new Color(1f, 0.52f, 0.42f));
            return;
        }

        try
        {
            _voiceDeviceName = Microphone.devices[0];
            _voiceClip = Microphone.Start(_voiceDeviceName, true, BattleVoiceLoopSeconds, BattleVoiceSampleRate);
            _voiceReadPosition = 0;
            _voiceRecording = true;
            RefreshVoiceButtonState();
        }
        catch (System.Exception e)
        {
            AppendChatMessage("系统", "无法接入团队语聊：" + e.Message, new Color(1f, 0.52f, 0.42f));
            StopVoiceCapture();
            RefreshVoiceButtonState();
        }
    }

    public void EndVoiceRecording()
    {
        if (!_voiceRecording) return;
        FlushVoiceRecordingChunks(true);
        StopVoiceCapture();
        RefreshVoiceButtonState();
    }

    void UpdateVoiceRecording()
    {
        if (_voiceRecording)
            FlushVoiceRecordingChunks(false);

        if (!string.IsNullOrEmpty(_remoteVoiceSpeaker) && !IsRemoteVoiceActive())
        {
            _remoteVoiceSpeaker = null;
            RefreshVoiceButtonState();
        }
    }

    int GetVoiceChunkSamples()
    {
        return Mathf.Max(1, Mathf.RoundToInt(BattleVoiceSampleRate * BattleVoiceChunkSeconds));
    }

    int GetVoiceMinChunkSamples()
    {
        return Mathf.Max(1, Mathf.RoundToInt(BattleVoiceSampleRate * BattleVoiceMinChunkSeconds));
    }

    void FlushVoiceRecordingChunks(bool flushPartial)
    {
        if (!_voiceRecording || _voiceClip == null || string.IsNullOrEmpty(_voiceDeviceName))
            return;

        int currentPosition;
        try { currentPosition = Microphone.GetPosition(_voiceDeviceName); }
        catch { return; }

        int availableSamples = GetBufferedVoiceSampleCount(currentPosition);
        int chunkSamples = GetVoiceChunkSamples();
        while (availableSamples >= chunkSamples)
        {
            SendVoiceChunk(chunkSamples);
            availableSamples -= chunkSamples;
        }

        if (flushPartial && availableSamples >= GetVoiceMinChunkSamples())
            SendVoiceChunk(availableSamples);
    }

    int GetBufferedVoiceSampleCount(int currentPosition)
    {
        if (_voiceClip == null || currentPosition < 0)
            return 0;
        if (currentPosition >= _voiceReadPosition)
            return currentPosition - _voiceReadPosition;
        return (_voiceClip.samples - _voiceReadPosition) + currentPosition;
    }

    void SendVoiceChunk(int sampleCount)
    {
        if (_voiceClip == null || sampleCount <= 0) return;

        var samples = ReadVoiceSamples(_voiceReadPosition, sampleCount);
        _voiceReadPosition = (_voiceReadPosition + sampleCount) % Mathf.Max(1, _voiceClip.samples);
        if (samples == null || samples.Length == 0) return;

        string pcm = EncodeVoiceSamples(samples);
        if (string.IsNullOrEmpty(pcm)) return;

        GameNetworkSync.Instance?.SendVoiceStreamChunk(GetLocalChatName(), BattleVoiceSampleRate, pcm);
    }

    float[] ReadVoiceSamples(int startSample, int sampleCount)
    {
        if (_voiceClip == null || sampleCount <= 0) return null;
        sampleCount = Mathf.Clamp(sampleCount, 0, _voiceClip.samples);
        if (sampleCount <= 0) return null;

        var samples = new float[sampleCount];
        if (startSample + sampleCount <= _voiceClip.samples)
        {
            if (!_voiceClip.GetData(samples, startSample)) return null;
            return samples;
        }

        int firstCount = _voiceClip.samples - startSample;
        if (firstCount > 0)
        {
            var first = new float[firstCount];
            if (!_voiceClip.GetData(first, startSample)) return null;
            System.Array.Copy(first, 0, samples, 0, firstCount);
        }

        int secondCount = sampleCount - firstCount;
        if (secondCount > 0)
        {
            var second = new float[secondCount];
            if (!_voiceClip.GetData(second, 0)) return null;
            System.Array.Copy(second, 0, samples, firstCount, secondCount);
        }

        return samples;
    }

    string EncodeVoiceSamples(float[] samples)
    {
        if (samples == null || samples.Length == 0) return "";
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short v = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xff);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        return System.Convert.ToBase64String(bytes);
    }

    public void ReceiveVoiceChatStream(string speaker, int sampleRate, string pcmBase64)
    {
        _remoteVoiceSpeaker = string.IsNullOrWhiteSpace(speaker) ? "队友" : speaker;
        _remoteVoiceActiveUntil = Time.realtimeSinceStartup + 0.35f;
        RefreshVoiceButtonState();
        PlayVoiceMessage(sampleRate, pcmBase64);
    }

    bool IsRemoteVoiceActive()
    {
        return !string.IsNullOrEmpty(_remoteVoiceSpeaker) && Time.realtimeSinceStartup <= _remoteVoiceActiveUntil;
    }

    void RefreshVoiceButtonState()
    {
        if (_voiceButtonLabel != null)
        {
            _voiceButtonLabel.text = "\u260E";
            _voiceButtonLabel.fontSize = _voiceRecording ? 21 : 20;
            _voiceButtonLabel.color = IsRemoteVoiceActive()
                ? new Color(0.94f, 0.98f, 1f)
                : new Color(0.96f, 0.98f, 1f);
        }

        if (_voiceRecording)
            SetChatButtonColor(_voiceButton, new Color(0.65f, 0.14f, 0.12f, 0.98f));
        else if (IsRemoteVoiceActive())
            SetChatButtonColor(_voiceButton, new Color(0.24f, 0.46f, 0.72f, 0.98f));
        else
            SetChatButtonColor(_voiceButton, new Color(0.16f, 0.30f, 0.54f, 0.96f));
    }

    void PlayVoiceMessage(int sampleRate, string pcmBase64)
    {
        if (string.IsNullOrEmpty(pcmBase64) || sampleRate <= 0) return;
        byte[] bytes;
        try { bytes = System.Convert.FromBase64String(pcmBase64); }
        catch { return; }

        int count = bytes.Length / 2;
        if (count <= 0) return;
        var data = new float[count];
        for (int i = 0; i < count; i++)
        {
            short v = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            data[i] = Mathf.Clamp(v / 32768f, -1f, 1f);
        }

        var clip = AudioClip.Create("BattleChatVoice", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        if (_voicePlaybackSource == null)
        {
            _voicePlaybackSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            _voicePlaybackSource.playOnAwake = false;
            _voicePlaybackSource.spatialBlend = 0f;
            _voicePlaybackSource.ignoreListenerPause = true;
        }
        _voicePlaybackSource.PlayOneShot(clip, 1f);
        Destroy(clip, clip.length + 0.25f);
    }

    void StopVoiceCapture()
    {
        if (!string.IsNullOrEmpty(_voiceDeviceName))
        {
            try { Microphone.End(_voiceDeviceName); }
            catch { }
        }

        _voiceRecording = false;
        _voiceDeviceName = null;
        _voiceClip = null;
        _voiceReadPosition = 0;
    }

    void RemoveChatPanel(Canvas canvas)
    {
        StopVoiceCapture();
        if (canvas == null) return;
        var existing = canvas.transform.Find("_ChatPanel");
        if (existing != null)
            Destroy(existing.gameObject);
        var dock = canvas.transform.Find("_ChatDockButton");
        if (dock != null)
            Destroy(dock.gameObject);
        _chatPanelRect = null;
        _chatBody = null;
        _chatInput = null;
        _chatSendButton = null;
        _voiceButton = null;
        _voiceButtonLabel = null;
        _chatDockButton = null;
        _chatDockButtonImage = null;
        _chatDockTailImage = null;
        _chatDockUnreadDot = null;
        _chatDockButtonLabel = null;
        _chatUnreadWhileCollapsed = false;
        _chatLines = null;
    }

    void DisableLegacyPanelChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return;
        var child = parent.Find(childName);
        if (child != null && child.gameObject.activeSelf)
            child.gameObject.SetActive(false);
    }

    void MakeTopBarBackgroundTransparent(Transform topBar)
    {
        if (topBar == null) return;

        MakeUiImageTransparent(topBar.GetComponent<Image>());

        foreach (Transform child in topBar)
        {
            if (IsLegacyTopBarDecoration(child.name))
            {
                child.gameObject.SetActive(false);
                MakeUiImageTransparent(child.GetComponent<Image>());
            }
        }

        var commanderGroup = topBar.Find("_CommanderHeader/AvatarRankGroup");
        if (commanderGroup != null)
            MakeCommanderGroupBackgroundTransparent(commanderGroup);
    }

    static bool IsLegacyTopBarDecoration(string name)
    {
        return name == "Div"
            || name == "TopBarAccent"
            || name == "_TopAccent"
            || name == "_Accent"
            || name == "_GoldIcon"
            || name == "_PowerIcon"
            || name.StartsWith("_Sep");
    }

    static void MakeUiImageTransparent(Image image)
    {
        if (image == null) return;
        var c = image.color;
        c.a = 0f;
        image.color = c;
        image.raycastTarget = false;
    }

    void MakeCommanderGroupBackgroundTransparent(Transform group)
    {
        if (group == null) return;

        MakeUiImageTransparent(group.GetComponent<Image>());

        var outline = group.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        EnsureTransparentRectBorder(group, "_CommanderHeaderBorder", new Color(0.92f, 0.78f, 0.32f, 0.78f), 1.35f);
    }

    void EnsureTransparentRectBorder(Transform parent, string name, Color color, float thickness)
    {
        if (parent == null) return;

        var root = parent.Find(name);
        GameObject rootGO;
        RectTransform rootRt;
        if (root == null)
        {
            rootGO = new GameObject(name);
            rootGO.transform.SetParent(parent, false);
            rootRt = rootGO.AddComponent<RectTransform>();
        }
        else
        {
            rootGO = root.gameObject;
            rootRt = rootGO.GetComponent<RectTransform>() ?? rootGO.AddComponent<RectTransform>();
        }

        rootGO.SetActive(true);
        rootGO.transform.SetAsLastSibling();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        EnsureRectBorderLine(rootGO.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, thickness), color);
        EnsureRectBorderLine(rootGO.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
        EnsureRectBorderLine(rootGO.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), color);
        EnsureRectBorderLine(rootGO.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(thickness, 0f), color);
    }

    static void EnsureRectBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var line = parent.Find(name);
        GameObject lineGO;
        RectTransform rt;
        if (line == null)
        {
            lineGO = new GameObject(name);
            lineGO.transform.SetParent(parent, false);
            rt = lineGO.AddComponent<RectTransform>();
            lineGO.AddComponent<Image>();
        }
        else
        {
            lineGO = line.gameObject;
            rt = lineGO.GetComponent<RectTransform>() ?? lineGO.AddComponent<RectTransform>();
            if (lineGO.GetComponent<Image>() == null) lineGO.AddComponent<Image>();
        }

        lineGO.SetActive(true);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        var image = lineGO.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    void RuntimeUIPolish()
    {
        // ── TopBar 指挥官 HUD ───────────────────────────────
        var topBarGO = GoldText?.transform.parent?.gameObject;
        if (topBarGO != null)
        {
            MakeTopBarBackgroundTransparent(topBarGO.transform);
            if (KillText != null) KillText.gameObject.SetActive(false);
            EnsureCommanderHeader(topBarGO.transform);
        }
        // 文字加粗
        foreach (var t in new Text[]{ GoldText, PopText, KillText, PowerText, GameTimerText })
            if (t != null) { t.fontStyle = FontStyle.Bold; if (t.fontSize < 20) t.fontSize = 20; }

        // ── UnitInfoPanel 军用风 ────────────────────────────
        if (UnitInfoPanel != null)
        {
            var bg = UnitInfoPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.018f, 0.026f, 0.034f, 0.82f);
            DisableLegacyPanelChild(UnitInfoPanel.transform, "UnitPanelHeader");
            if (UnitInfoPanel.transform.Find("_Header") == null)
            {
                var h = new GameObject("_Header");
                h.transform.SetParent(UnitInfoPanel.transform, false);
                h.transform.SetAsFirstSibling();
                var hr = h.AddComponent<RectTransform>();
                hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = Vector2.one;
                hr.offsetMin = new Vector2(0f, -30f); hr.offsetMax = Vector2.zero;
                h.AddComponent<Image>().color = new Color(0.04f, 0.12f, 0.16f, 1f);
            }
            // 顶部金色描边
            if (UnitInfoPanel.transform.Find("_TopGold") == null)
            {
                var g = new GameObject("_TopGold");
                g.transform.SetParent(UnitInfoPanel.transform, false);
                var grt = g.AddComponent<RectTransform>();
                grt.anchorMin = new Vector2(0f, 1f); grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(0f, -2f); grt.offsetMax = Vector2.zero;
                g.AddComponent<Image>().color = new Color(0.14f, 0.44f, 0.55f, 0.90f);
            }
        }
        if (UnitNameText != null) UnitNameText.fontStyle = FontStyle.Bold;

        // ── BuildingPanel 军用风 ─────────────────────────────
        if (BuildingPanel != null)
        {
            var bg = BuildingPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.018f, 0.026f, 0.034f, 0.90f);
            DisableLegacyPanelChild(BuildingPanel.transform, "BldPanelHeader");
            if (BuildingPanel.transform.Find("_Header") == null)
            {
                var h = new GameObject("_Header");
                h.transform.SetParent(BuildingPanel.transform, false);
                h.transform.SetAsFirstSibling();
                var hr = h.AddComponent<RectTransform>();
                hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = Vector2.one;
                hr.offsetMin = new Vector2(0f, -30f); hr.offsetMax = Vector2.zero;
                h.AddComponent<Image>().color = new Color(0.05f, 0.10f, 0.18f, 1f);
            }
            // 顶部金色描边
            if (BuildingPanel.transform.Find("_TopGold") == null)
            {
                var g = new GameObject("_TopGold");
                g.transform.SetParent(BuildingPanel.transform, false);
                var grt = g.AddComponent<RectTransform>();
                grt.anchorMin = new Vector2(0f, 1f); grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(0f, -2f); grt.offsetMax = Vector2.zero;
                g.AddComponent<Image>().color = new Color(0.24f, 0.40f, 0.72f, 0.86f);
            }
        }
        if (BuildingNameText != null) { BuildingNameText.fontStyle = FontStyle.Bold; BuildingNameText.color = new Color(0.92f, 0.96f, 1f); }
        if (UnitNameText != null) UnitNameText.color = new Color(0.92f, 0.96f, 1f);

        // ── BuildMenuPanel 军用风格 ─────────────────────────
        if (BuildMenuPanel != null)
        {
            ApplyBuildMenuPopupLayout();
        }
        // BuildMenuToggle 也调成军用风
        if (BuildMenuToggle != null)
        {
            var bImg = BuildMenuToggle.GetComponent<Image>();
            if (bImg != null) bImg.color = new Color(0.20f, 0.30f, 0.14f, 0.96f);
            var btxt = BuildMenuToggle.GetComponentInChildren<Text>();
            if (btxt != null) btxt.color = new Color(1f, 0.92f, 0.55f);
        }

        // ── GameOverPanel 卡片背景加深 ───────────────────────
        if (GameOverPanel != null)
        {
            var bg = GameOverPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0f, 0f, 0f, 0.75f);
            // GoCard 子节点（SceneBuilder版）军用风
            var card = GameOverPanel.transform.Find("GoCard");
            if (card != null)
            {
                var crt = card.GetComponent<RectTransform>();
                if (crt != null) crt.sizeDelta = new Vector2(620f, 430f);
                var ci = card.GetComponent<Image>();
                // 顶部金色描边 + 底部金色描边
                if (card.Find("_TopGold") == null)
                {
                    var g = new GameObject("_TopGold");
                    g.transform.SetParent(card, false);
                    var grt = g.AddComponent<RectTransform>();
                    grt.anchorMin = new Vector2(0f, 1f); grt.anchorMax = Vector2.one;
                    grt.offsetMin = new Vector2(0f, -3f); grt.offsetMax = Vector2.zero;
                    g.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.95f);
                }
                if (card.Find("_BottomGold") == null)
                {
                    var g = new GameObject("_BottomGold");
                    g.transform.SetParent(card, false);
                    var grt = g.AddComponent<RectTransform>();
                    grt.anchorMin = Vector2.zero; grt.anchorMax = new Vector2(1f, 0f);
                    grt.offsetMin = Vector2.zero; grt.offsetMax = new Vector2(0f, 3f);
                    g.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.95f);
                }
                if (ci != null) ci.color = new Color(0.10f, 0.12f, 0.07f, 0.98f);
            }
        }
        if (GameOverText != null)
        {
            GameOverText.fontStyle = FontStyle.Bold;
            var ol = GameOverText.GetComponent<Outline>() ?? GameOverText.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(2f, -2f);
        }
        if (GameOverStatsText != null)
        {
            GameOverStatsText.fontSize = 17;
            GameOverStatsText.alignment = TextAnchor.UpperLeft;
            GameOverStatsText.lineSpacing = 1.08f;
            var rt = GameOverStatsText.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 520f), 190f);
            rt.anchoredPosition = new Vector2(0f, -8f);
        }

        // ── 小地图金色边框 + 标题 ─────────────────────────────
        if (MinimapImage != null)
        {
            // Low-contrast border; keep the map readable instead of decorative.
            if (MinimapImage.GetComponent<Outline>() == null)
            {
                var ol = MinimapImage.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(0.10f, 0.18f, 0.24f, 0.95f);
                ol.effectDistance = new Vector2(1f, -1f);
            }
            // 顶部"战场地图"标题（暗橄榄 bar + 金黄文字）
            if (MinimapImage.transform.Find("_TitleBar") == null)
            {
                var tb = new GameObject("_TitleBar");
                tb.transform.SetParent(MinimapImage.transform, false);
                tb.transform.SetAsLastSibling();
                var trt = tb.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.anchoredPosition = Vector2.zero;
                trt.sizeDelta = new Vector2(0f, 18f);
                tb.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.04f, 0.86f);

                var tx = new GameObject("Text");
                tx.transform.SetParent(tb.transform, false);
                var txrt = tx.AddComponent<RectTransform>();
                txrt.anchorMin = Vector2.zero; txrt.anchorMax = Vector2.one;
                txrt.offsetMin = Vector2.zero; txrt.offsetMax = Vector2.zero;
                var tt = tx.AddComponent<Text>();
                tt.text = "战场地图";
                tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (tt.font == null) tt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                tt.fontSize = 11;
                tt.fontStyle = FontStyle.Bold;
                tt.alignment = TextAnchor.MiddleCenter;
                tt.color = new Color(0.86f, 0.94f, 1f);
                tt.raycastTarget = false;
                // 标题底下金色细线（与 minimap 内容分隔）
                var sl = new GameObject("_Sep");
                sl.transform.SetParent(tb.transform, false);
                var srt2 = sl.AddComponent<RectTransform>();
                srt2.anchorMin = Vector2.zero; srt2.anchorMax = new Vector2(1f, 0f);
                srt2.offsetMin = Vector2.zero; srt2.offsetMax = new Vector2(0f, 1.5f);
                sl.AddComponent<Image>().color = new Color(0.78f, 0.62f, 0.18f, 0.85f);
            }
        }

        // ── PausePanel 军用风 ─────────────────────────────────
        if (PausePanel != null)
        {
            var bg = PausePanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.08f, 0.10f, 0.06f, 0.97f);
            if (PausePanel.transform.Find("_Accent") == null)
            {
                var a = new GameObject("_Accent");
                a.transform.SetParent(PausePanel.transform, false);
                var ar = a.AddComponent<RectTransform>();
                ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = Vector2.one;
                ar.offsetMin = new Vector2(0f, -3f); ar.offsetMax = Vector2.zero;
                a.AddComponent<Image>().color = new Color(1f, 0.72f, 0.1f, 0.9f);
            }
        }
    }

    /// <summary>缓存每个建造按钮的价格文字（一次性创建，避免每帧分配）。</summary>
    Text[] _buildPriceTexts;
    int[]  _buildPrices;
    int[]  _buildPowerCosts;     // 该建筑的电力消耗（电厂为 0）
    bool[] _buildIsPowerPlant;   // 是否电厂（电厂不检查电力）
    bool[] _buildIgnoresPowerShortage;
    GameObject[] _buildPrefabs;

    void WireBuildButtons()
    {
        if (BuildButtons == null) return;
        _buildPrefabs = new[] {
            BarracksPrefab, AirFactoryPrefab, AirfieldPrefab, TankFactoryPrefab,
            ArmorFactoryPrefab, TurretPrefab, GoldMinePrefab, PowerPlantPrefab, NavalYardPrefab
        };
        _buildPriceTexts = new Text[BuildButtons.Length];
        _buildPrices = new int[BuildButtons.Length];
        _buildPowerCosts = new int[BuildButtons.Length];
        _buildIsPowerPlant = new bool[BuildButtons.Length];
        _buildIgnoresPowerShortage = new bool[BuildButtons.Length];
        for (int i = 0; i < BuildButtons.Length; i++)
        {
            if (BuildButtons[i] == null) continue;
            int idx = i;
            BuildButtons[i].onClick.RemoveAllListeners();
            SetButtonText(BuildButtons[i], GetBuildButtonLabel(i));
            BuildButtons[i].onClick.AddListener(() => {
                if (_buildPrefabs != null && idx < _buildPrefabs.Length && _buildPrefabs[idx] != null)
                {
                    if (!TryAffordBuild(idx, BuildButtons[idx])) return;
                    _UiClickAudio.PlayConfirm();
                    RTSPlayerController.Instance?.StartPlacement(_buildPrefabs[idx]);
                    CloseBuildMenu();
                }
            });
            // 读取建筑价格 / 电力消耗 / 是否电厂
            int price = 0;
            int powerCost = 0;
            bool isPlant = false;
            bool ignoresPowerShortage = false;
            if (_buildPrefabs != null && idx < _buildPrefabs.Length && _buildPrefabs[idx] != null)
            {
                var b = _buildPrefabs[idx].GetComponent<RTSBuilding>();
                if (b != null)
                {
                    b.ApplyDefinitionDefaults();
                    price = b.GoldCost;
                    powerCost = b.PowerCost;
                    isPlant = b.bIsPowerPlant;
                    ignoresPowerShortage = b.bIsPowerPlant || b.bIsGoldMine;
                }
            }
            _buildPrices[i] = price;
            _buildPowerCosts[i] = powerCost;
            _buildIsPowerPlant[i] = isPlant;
            _buildIgnoresPowerShortage[i] = ignoresPowerShortage;
            // 在按钮右下角创建价格 Text
            _buildPriceTexts[i] = CreateBuildPriceTag(BuildButtons[i].transform, price);
        }
    }

    GameObject CreateRuntimeAirfieldPrefab()
    {
        var go = new GameObject("Airfield_P_RuntimePrefab");
        go.SetActive(false);

        var collider = go.AddComponent<BoxCollider>();
        Airfield.ConfigureCollider(collider);

        BuildRuntimeAirfieldVisual(go.transform, true);

        var airfield = go.AddComponent<Airfield>();
        airfield.bPlayerOwned = true;
        airfield.ApplyDefinitionDefaults();
        return go;
    }

    GameObject CreateRuntimeArmorFactoryPrefab()
    {
        var go = new GameObject("ArmorFactory_P_RuntimePrefab");
        go.SetActive(false);

        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(3.5f, 1.4f, 3f);
        collider.center = new Vector3(0f, 0.7f, 0f);

        var modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(go.transform, false);
        BuildRuntimeArmorFactoryVisual(modelRoot.transform);

        var armorFactory = go.AddComponent<ArmorFactory>();
        armorFactory.bPlayerOwned = true;
        armorFactory.ApplyDefinitionDefaults();
        return go;
    }

    void BuildRuntimeAirfieldVisual(Transform parent, bool playerOwned)
    {
        Airfield.BuildVisual(parent, playerOwned, AddRuntimeAirfieldPart);
    }

    void BuildRuntimeArmorFactoryVisual(Transform parent)
    {
        if (TryCloneModelChildren(TankFactoryPrefab, parent))
            return;

        AddRuntimeAirfieldPart(parent, "ArmorFactoryHall", new Vector3(0f, 0.75f, 0f), new Vector3(3.0f, 1.5f, 2.5f), new Color(0.34f, 0.36f, 0.18f));
        AddRuntimeAirfieldPart(parent, "ArmorFactoryBay", new Vector3(0f, 0.45f, -1.18f), new Vector3(1.7f, 0.9f, 0.22f), new Color(0.12f, 0.12f, 0.12f));
        AddRuntimeAirfieldPart(parent, "ArmorFactoryStackA", new Vector3(-0.8f, 1.75f, 0.45f), new Vector3(0.32f, 1.0f, 0.32f), new Color(0.30f, 0.30f, 0.32f));
        AddRuntimeAirfieldPart(parent, "ArmorFactoryStackB", new Vector3(0.85f, 1.55f, 0.45f), new Vector3(0.28f, 0.82f, 0.28f), new Color(0.30f, 0.30f, 0.32f));
        AddRuntimeAirfieldPart(parent, "ArmorFactoryTankPreview", new Vector3(1.0f, 0.18f, -0.55f), new Vector3(1.15f, 0.34f, 1.65f), new Color(0.18f, 0.28f, 0.16f));
    }

    bool TryCloneModelChildren(GameObject prefab, Transform parent)
    {
        if (prefab == null || parent == null)
            return false;

        var model = prefab.transform.Find("Model");
        if (model == null)
            return false;

        foreach (Transform child in model)
        {
            var clone = Instantiate(child.gameObject, parent, false);
            clone.name = child.name;
        }
        return true;
    }

    void AddRuntimeAirfieldPart(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = localScale;
        var col = part.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }

    Text CreateBuildPriceTag(Transform parent, int price)
    {
        var go = new GameObject("PriceTag");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        // 横跨按钮底部
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 24f);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = $"$ {price}";
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.94f, 0.58f);
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        EnsureReadableTextShadow(t, new Vector2(1.2f, -1.2f));
        EnsureReadableTextOutline(t, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.78f));
        return t;
    }

    /// <summary>每帧检查玩家金币，不足时按钮变暗 + 价格变红（保留 interactable=true 让点击仍可触发 deny 反馈）。</summary>
    void RefreshBuildButtonStates()
    {
        if (_buildPriceTexts == null || playerState == null) return;
        int gold = playerState.Gold;
        int powerAvail = playerState.PowerCap - playerState.PowerUsed;
        for (int i = 0; i < _buildPriceTexts.Length; i++)
        {
            if (BuildButtons[i] == null) continue;
            int price = _buildPrices[i];
            int powerCost = _buildPowerCosts != null && i < _buildPowerCosts.Length ? _buildPowerCosts[i] : 0;
            bool ignoresPowerShortage = _buildIgnoresPowerShortage != null && i < _buildIgnoresPowerShortage.Length && _buildIgnoresPowerShortage[i];
            bool goldOk  = price <= gold;
            bool powerOk = ignoresPowerShortage || powerCost <= powerAvail;
            bool countOk = true;
            if (_buildPrefabs != null && i < _buildPrefabs.Length && _buildPrefabs[i] != null)
            {
                var template = _buildPrefabs[i].GetComponent<RTSBuilding>();
                if (template != null)
                    countOk = MainBase.CanConstructBuilding(template, true);
            }
            bool enabled = goldOk && powerOk && countOk;
            // 保留 interactable=true，点击时给反馈（deny 音 + 抖动）
            var img = BuildButtons[i].GetComponent<Image>();
            if (img != null)
            {
                Color baseC = GetBuildButtonColor(i);
                img.color = enabled ? baseC : new Color(baseC.r * 0.6f, baseC.g * 0.6f, baseC.b * 0.6f, 0.85f);
            }
            SetBuildButtonArtEnabled(BuildButtons[i], enabled);
            // 价格文字：金币不足=红，电力不足=橙，都正常=金
            if (_buildPriceTexts[i] != null)
            {
                _buildPriceTexts[i].color = !countOk ? new Color(0.74f, 0.80f, 0.88f)
                                          : !goldOk  ? new Color(1f, 0.40f, 0.34f)
                                          : !powerOk ? new Color(1f, 0.78f, 0.30f)
                                          : new Color(1f, 0.92f, 0.55f);
            }
        }
    }

    void SetBuildCategory(int category)
    {
        _activeBuildCategory = Mathf.Clamp(category, 0, BuildCategoryNames.Length - 1);
        RefreshBuildCategoryVisibility();
    }

    void RefreshBuildCategoryVisibility()
    {
        int visibleCount = 0;
        if (BuildButtons != null)
        {
            for (int i = 0; i < BuildButtons.Length; i++)
            {
                if (BuildButtons[i] == null) continue;
                bool visible = IsBuildButtonInCategory(i, _activeBuildCategory);
                BuildButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;

                int col = visibleCount % BuildCategoryColumnCount;
                int row = visibleCount / BuildCategoryColumnCount;
                var rt = BuildButtons[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(
                        BuildCategoryGridStartX + col * BuildCategoryButtonStepX,
                        BuildCategoryGridStartY - row * BuildCategoryButtonStepY);
                    rt.sizeDelta = new Vector2(BuildCategoryButtonWidth, BuildCategoryButtonHeight);
                }
                visibleCount++;
            }
        }

        if (_buildEmptyText != null)
        {
            bool empty = visibleCount == 0;
            _buildEmptyText.gameObject.SetActive(empty);
            _buildEmptyText.text = empty
                ? "该分类暂无可建造项目"
                : "";
        }

        RefreshBuildCategoryTabs();
    }

    bool IsBuildButtonInCategory(int buildIndex, int category)
    {
        switch (category)
        {
            case 0: return true;
            case 1: return buildIndex == 0 || buildIndex == 1 || buildIndex == 2 || buildIndex == 3 || buildIndex == 4 || buildIndex == 5;
            case 2: return buildIndex == 6 || buildIndex == 7;
            case 3: return buildIndex == 8;
            case 4: return buildIndex == 1 || buildIndex == 2;
            case 5: return buildIndex == 0 || buildIndex == 3 || buildIndex == 4 || buildIndex == 5;
            default: return true;
        }
    }

    void RefreshBuildCategoryTabs()
    {
        if (_buildCategoryButtons == null) return;
        for (int i = 0; i < _buildCategoryButtons.Length; i++)
        {
            var button = _buildCategoryButtons[i];
            if (button == null) continue;
            bool active = i == _activeBuildCategory;
            var rt = button.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(50f + i * 100f, active ? 2f : -1f);
                rt.sizeDelta = active ? new Vector2(92f, 38f) : new Vector2(88f, 34f);
            }
            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = active
                    ? new Color(0.19f, 0.16f, 0.06f, 0.98f)
                    : new Color(0.12f, 0.10f, 0.05f, 0.92f);
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = active
                    ? new Color(0.92f, 0.76f, 0.24f, 0.95f)
                    : new Color(0.58f, 0.46f, 0.16f, 0.72f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 17;
                text.fontStyle = FontStyle.Bold;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.color = active
                    ? new Color(1f, 1f, 0.94f)
                    : new Color(0.96f, 0.88f, 0.62f);
                EnsureReadableTextShadow(text, new Vector2(1.2f, -1.2f));
                EnsureReadableTextOutline(text, new Vector2(1f, -1f), new Color(0f, 0f, 0f, 0.70f));
            }
            if (active)
                button.transform.SetAsLastSibling();
        }
    }

    /// <summary>建造按钮点击：钱不够或电力不够时播 deny 音 + 抖动。</summary>
    public bool TryAffordBuild(int idx, Button btn)
    {
        if (_buildPrices == null || idx >= _buildPrices.Length || playerState == null) return true;
        if (_buildPrefabs != null && idx < _buildPrefabs.Length && _buildPrefabs[idx] != null)
        {
            var template = _buildPrefabs[idx].GetComponent<RTSBuilding>();
            if (template != null && !MainBase.CanConstructBuilding(template, true, out string buildFailureReason))
            {
                _UiClickAudio.PlayDeny();
                ShowAlert(buildFailureReason);
                if (btn != null) StartCoroutine(ShakeButton(btn.GetComponent<RectTransform>()));
                return false;
            }
        }
        int price = _buildPrices[idx];
        if (playerState.Gold < price)
        {
            _UiClickAudio.PlayDeny();
            ShowAlert($"金币不足：需要 {price}");
            if (btn != null) StartCoroutine(ShakeButton(btn.GetComponent<RectTransform>()));
            return false;
        }
        // 电力检查：电厂和金矿属于基础经济建筑，缺电时也允许建，避免一级发展被卡死。
        bool ignoresPowerShortage = _buildIgnoresPowerShortage != null && idx < _buildIgnoresPowerShortage.Length && _buildIgnoresPowerShortage[idx];
        int powerCost = _buildPowerCosts != null && idx < _buildPowerCosts.Length ? _buildPowerCosts[idx] : 0;
        if (!ignoresPowerShortage && powerCost > playerState.PowerCap - playerState.PowerUsed)
        {
            _UiClickAudio.PlayDeny();
            int deficit = powerCost - (playerState.PowerCap - playerState.PowerUsed);
            ShowAlert($"电力不足：先建电厂（还差 {Mathf.Max(1, deficit)}）");
            if (btn != null) StartCoroutine(ShakeButton(btn.GetComponent<RectTransform>()));
            return false;
        }
        return true;
    }

    System.Collections.IEnumerator ShakeButton(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 origin = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.32f)
        {
            t += Time.unscaledDeltaTime;
            float r = 1f - (t / 0.32f);
            float dx = (Random.value * 2f - 1f) * 6f * r;
            rt.anchoredPosition = origin + new Vector2(dx, 0f);
            yield return null;
        }
        rt.anchoredPosition = origin;
    }

    void ToggleBuildMenu()
    {
        var canvas = GetHudCanvas();
        PrimeBuildMenuPopupUi(canvas);
        bBuildMenuOpen = !bBuildMenuOpen;
        if (bBuildMenuOpen)
        {
            CancelTechTargeting(false);
            CloseBuildingUpgradePanel();
        }
        if (bBuildMenuOpen)
            SetTechPanelOpen(false);
        if (BuildMenuPanel)
        {
            NormalizeBuildMenuForCanvas(canvas);
            BuildMenuPanel.SetActive(bBuildMenuOpen);
            if (bBuildMenuOpen)
            {
                BuildMenuPanel.transform.SetAsLastSibling();
                ApplyBuildMenuPopupLayout();
            }
        }
        // 更新按钮文本
        if (BuildMenuToggle)
        {
            StyleSideHudButton(BuildMenuToggle, bBuildMenuOpen ? "收起" : "建造", "⌂",
                new Color(0.90f, 0.68f, 0.22f, 0.95f));
        }
    }

    void CloseBuildMenu()
    {
        bBuildMenuOpen = false;
        if (BuildMenuPanel) BuildMenuPanel.SetActive(false);
        if (BuildMenuToggle)
        {
            StyleSideHudButton(BuildMenuToggle, "建造", "⌂",
                new Color(0.90f, 0.68f, 0.22f, 0.95f));
        }
    }

    void Update()
    {
        var canvas = GetHudCanvas();
        EnsureChatPanel(canvas);
        if (TechPanel != null && TechPanel.activeSelf && canvas != null)
        {
            Vector2 canvasSize = GetCanvasRectSize(canvas);
            if ((canvasSize - _lastTechPanelCanvasSize).sqrMagnitude > 1f)
                ApplyBattleTechPanelLayout(canvas);
        }
        EnsureBattleTimeRunning();
        if (IsOnlineMatch() && (bPaused || (PausePanel != null && PausePanel.activeSelf)))
            HidePauseMenu();
        MaintainGameSettingsEntry(canvas);
        UpdateVoiceRecording();
        UpdateTechTargeting();
        UpdateTopBar();
        UpdateSelectionUI();
        RefreshBuildingUpgradePanel();
        UpdateAlert();
        RefreshBuildButtonStates();
        UpdateCommandBarState();
        UpdateTechCooldownUi();
        UpdateResourceWarnings();
        UpdateEdgeThreatIndicators();
    }

    bool ShouldShowBattleChatPanel()
    {
        return true;
    }

    // 屏幕边缘威胁箭头：指向屏幕外靠近基地的敌人
    private RectTransform _edgeArrowContainer;
    private System.Collections.Generic.List<RectTransform> _edgeArrowPool;
    private float _edgeThreatTimer;
    private const int EdgeArrowPoolSize = 6;
    private const float EdgeThreatRadius = 90f;     // 检测敌方半径
    private const float EdgeThreatUpdateInterval = 0.2f;

    void EnsureEdgeArrowPool()
    {
        if (_edgeArrowContainer != null) return;
        var canvas = GetHudCanvas();
        if (canvas == null) return;

        var go = new GameObject("EdgeThreatArrows");
        go.transform.SetParent(canvas.transform, false);
        _edgeArrowContainer = go.AddComponent<RectTransform>();
        _edgeArrowContainer.anchorMin = Vector2.zero;
        _edgeArrowContainer.anchorMax = Vector2.one;
        _edgeArrowContainer.offsetMin = Vector2.zero;
        _edgeArrowContainer.offsetMax = Vector2.zero;
        _edgeArrowPool = new System.Collections.Generic.List<RectTransform>();
        for (int i = 0; i < EdgeArrowPoolSize; i++)
        {
            var ag = new GameObject("EdgeArrow_" + i);
            ag.transform.SetParent(_edgeArrowContainer, false);
            var rt = ag.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56f, 56f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var img = ag.AddComponent<UnityEngine.UI.Image>();
            img.sprite = null;
            img.color = new Color(1f, 0.32f, 0.18f, 0.95f);
            img.raycastTarget = false;
            // 用默认 sprite（白方块），通过 fillCenter + 形状裁切做箭头：简单起见用三角形 mesh 替代
            BuildArrowMesh(ag);
            ag.SetActive(false);
            _edgeArrowPool.Add(rt);
        }
    }

    /// <summary>用一个简单的旋转 RectTransform 箭头（用 Image 显示填充三角形不便，改用顶点 mesh）。</summary>
    static void BuildArrowMesh(GameObject host)
    {
        // 用一个 child 上的 RawImage + 程序生成贴图（一个红色三角形）
        var raw = host.GetComponent<UnityEngine.UI.Image>();
        if (raw == null) return;
        // 程序生成 64×64 透明背景的红箭头三角形
        var tex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            // 上指箭头：在 |x-32| < (y) * 0.6 且 y < 60 范围内染色
            float dx = Mathf.Abs(x - 32f);
            float maxDx = (y - 6f) * 0.55f;
            bool fill = (y > 6 && y < 60 && dx < maxDx);
            // 黑边框
            bool border = (y > 6 && y < 60 && Mathf.Abs(dx - maxDx) < 1.6f);
            if (border)      px[y * 64 + x] = new Color32(40, 0, 0, 220);
            else if (fill)   px[y * 64 + x] = new Color32(255, 60, 30, 235);
            else             px[y * 64 + x] = new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        raw.sprite = sp;
        raw.type = UnityEngine.UI.Image.Type.Simple;
        raw.color = Color.white;
    }

    void UpdateEdgeThreatIndicators()
    {
        _edgeThreatTimer -= Time.deltaTime;
        if (_edgeThreatTimer > 0f) return;
        _edgeThreatTimer = EdgeThreatUpdateInterval;

        EnsureEdgeArrowPool();
        if (_edgeArrowPool == null || _edgeArrowContainer == null) return;

        var gm = GameManager.Instance;
        var mb = gm?.PlayerMainBase;
        if (gm == null || mb == null)
        {
            for (int i = 0; i < _edgeArrowPool.Count; i++)
                _edgeArrowPool[i].gameObject.SetActive(false);
            return;
        }
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 basePos = mb.transform.position;
        float r2 = EdgeThreatRadius * EdgeThreatRadius;

        var canvasRT = _edgeArrowContainer;
        Rect canvasRect = canvasRT.rect;
        float halfW = canvasRect.width * 0.5f - 40f;
        float halfH = canvasRect.height * 0.5f - 40f;

        int idx = 0;
        var units = gm.GetAllUnits();
        if (units != null)
        {
            for (int i = 0; i < units.Count && idx < _edgeArrowPool.Count; i++)
            {
                var u = units[i];
                if (u == null || u.IsDead() || u.IsPlayerOwned()) continue;
                Vector3 d = u.transform.position - basePos;
                if (d.x * d.x + d.z * d.z > r2) continue;
                // 计算屏幕坐标
                Vector3 sp = cam.WorldToViewportPoint(u.transform.position);
                bool offScreen = sp.z < 0f || sp.x < 0f || sp.x > 1f || sp.y < 0f || sp.y > 1f;
                if (!offScreen) continue;

                // 屏幕中心 → 敌人方向（投影到 XZ 后用相机朝向算）
                Vector3 worldFromCam = u.transform.position - cam.transform.position;
                Vector3 right = cam.transform.right;
                Vector3 up = Vector3.up;
                float xx = Vector3.Dot(worldFromCam, right);
                float yy = Vector3.Dot(worldFromCam, cam.transform.forward);
                Vector3 forwardOnGround = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                // 用世界 XZ 方向 + 相机前向夹角更稳定
                Vector3 baseToEnemy = u.transform.position - basePos; baseToEnemy.y = 0f;
                Vector2 dir2 = new Vector2(
                    Vector3.Dot(baseToEnemy.normalized, right),
                    Vector3.Dot(baseToEnemy.normalized, forwardOnGround));
                if (dir2.sqrMagnitude < 0.0001f) continue;
                dir2.Normalize();

                // 边缘箭头位置（沿 dir2 推到 canvas 边框）
                float scale = Mathf.Min(halfW / Mathf.Max(0.01f, Mathf.Abs(dir2.x)),
                                         halfH / Mathf.Max(0.01f, Mathf.Abs(dir2.y)));
                Vector2 pos = dir2 * scale;
                var arrow = _edgeArrowPool[idx];
                arrow.gameObject.SetActive(true);
                arrow.anchoredPosition = pos;
                // 旋转：默认箭头朝上，转向 dir2
                float ang = Mathf.Atan2(dir2.x, dir2.y) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0, 0, -ang);
                idx++;
            }
        }
        // 关闭未使用的箭头
        for (int i = idx; i < _edgeArrowPool.Count; i++)
            _edgeArrowPool[i].gameObject.SetActive(false);
    }

    // 资源警告状态：避免每帧持续刷消息和报警
    private bool _warnedLowGold = false;
    private bool _warnedPowerOverload = false;
    private bool _warnedPopFull = false;
    private float _resourceWarnCooldown = 0f;

    /// <summary>金币告急、电力超载、人口已满 时仅在状态切入时报警一次。</summary>
    void UpdateResourceWarnings()
    {
        if (playerState == null) return;
        _resourceWarnCooldown -= Time.deltaTime;

        // 金币 < 80：告急（恢复到 200 以上才能再次告警）
        bool lowGold = playerState.Gold < 80;
        if (lowGold && !_warnedLowGold && _resourceWarnCooldown <= 0f)
        {
            _UiClickAudio.PlayDeny();
            AppendChatMessage("后勤部", "金币告急，建议派工兵采集！", new Color(1f, 0.55f, 0.30f));
            _warnedLowGold = true;
            _resourceWarnCooldown = 1.0f;
        }
        else if (playerState.Gold > 200) _warnedLowGold = false;

        // 电力超载
        bool powerOver = playerState.PowerCap > 0 && playerState.PowerUsed > playerState.PowerCap;
        if (powerOver && !_warnedPowerOverload && _resourceWarnCooldown <= 0f)
        {
            _UiClickAudio.PlayWarn();
            AppendChatMessage("工程部", "电力超载，需建造更多发电机！", new Color(1f, 0.45f, 0.25f));
            _warnedPowerOverload = true;
            _resourceWarnCooldown = 1.0f;
        }
        else if (!powerOver) _warnedPowerOverload = false;

        // 人口已满
        bool popFull = playerState.PopCap > 0 && playerState.PopUsed >= playerState.PopCap;
        if (popFull && !_warnedPopFull && _resourceWarnCooldown <= 0f)
        {
            _UiClickAudio.PlayWarn();
            AppendChatMessage("征兵部", "人口已满，需扩充兵营！", new Color(1f, 0.65f, 0.20f));
            _warnedPopFull = true;
            _resourceWarnCooldown = 1.0f;
        }
        else if (playerState.PopUsed < playerState.PopCap - 1) _warnedPopFull = false;
    }

    void UpdateAlert()
    {
        if (alertTimer > 0f)
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer <= 0f && AlertText)
                AlertText.gameObject.SetActive(false);
        }
        if (_baseAttackChatCooldown > 0f)
            _baseAttackChatCooldown -= Time.deltaTime;
        UpdateVignetteAlert();
    }

    /// <summary>主基地低血量时决定屏幕边缘红光强度。</summary>
    void UpdateVignetteAlert()
    {
        if (_vignette == null) return;

        var mainBase = GameManager.Instance?.PlayerMainBase;
        if (mainBase == null || mainBase.GetHP() <= 0 || mainBase.GetMaxHP() <= 0)
        {
            _vignette.SetState(false, 0f);
            return;
        }

        float hpRatio = (float)mainBase.GetHP() / mainBase.GetMaxHP();
        if (hpRatio > BaseLowHealthVignetteThreshold)
        {
            _vignette.SetState(false, 0f);
            return;
        }

        float danger = 1f - Mathf.Clamp01(hpRatio / BaseLowHealthVignetteThreshold);
        float intensity = Mathf.Lerp(0.45f, 1f, danger);
        _vignette.SetState(true, intensity);
    }

    /// <summary>外部调用：基地受攻击时写入聊天提示，并做短冷却避免刷屏。</summary>
    public void NotifyBaseUnderAttack()
    {
        // 仅在新一波警告（之前未触发或快结束）时写入聊天，避免刷屏
        if (_baseAttackChatCooldown < 1.5f)
            AppendChatMessage("情报部", "警告！我方基地遭受攻击！", new Color(1f, 0.42f, 0.22f));
        _baseAttackChatCooldown = 4f;
    }

    /// <summary>大型爆炸时屏幕全屏闪光（短促）。intensity 0~1，duration 秒。</summary>
    public void FlashScreen(Color color, float intensity = 0.55f, float duration = 0.18f)
    {
        StartCoroutine(FlashScreenCoroutine(color, intensity, duration));
    }

    System.Collections.IEnumerator FlashScreenCoroutine(Color baseColor, float intensity, float duration)
    {
        var canvas = GetHudCanvas();
        if (canvas == null) yield break;
        var go = new GameObject("ScreenFlash");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        var c = baseColor; c.a = intensity;
        img.color = c;
        img.raycastTarget = false;
        // 始终在最顶层（不挡输入 raycastTarget=false）
        go.transform.SetAsLastSibling();

        float t = 0f;
        while (t < duration)
        {
            // 场景切换/重开时闪屏对象可能已销毁，协程要安静退出。
            if (go == null || img == null) yield break;
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / duration);
            // 前 20% 上升到峰值 intensity，后 80% 衰减到 0
            float alpha = r < 0.2f
                ? Mathf.Lerp(0f, intensity, r / 0.2f)
                : Mathf.Lerp(intensity, 0f, (r - 0.2f) / 0.8f);
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>屏幕右侧弹出击杀小卡片，依次堆叠。</summary>
    private readonly System.Collections.Generic.List<RectTransform> _killFeedItems = new System.Collections.Generic.List<RectTransform>();
    public void ShowKillFeed(string victimName)
    {
        StartCoroutine(SpawnKillFeed(victimName));
        // 重要击杀（敌方建筑）写入战地通讯
        if (victimName.Contains("基地") || victimName.Contains("兵营") || victimName.Contains("飞机厂") ||
            victimName.Contains("坦克厂") || victimName.Contains("电厂") || victimName.Contains("金矿") || victimName.Contains("防御塔"))
            AppendChatMessage("前线", $"摧毁敌方 {victimName}!", new Color(0.55f, 1f, 0.55f));
    }

    System.Collections.IEnumerator SpawnKillFeed(string victimName)
    {
        var canvas = GetHudCanvas();
        if (canvas == null) yield break;

        var go = new GameObject("KillFeedItem");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        // 初始位置：从屏幕外右侧滑入
        const float itemHeight = 32f, gap = 4f;
        int slotIndex = _killFeedItems.Count;
        float topOffset = -200f - slotIndex * (itemHeight + gap);
        rt.anchoredPosition = new Vector2(220f, topOffset); // 屏幕外
        rt.sizeDelta = new Vector2(220f, itemHeight);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.65f);
        bg.raycastTarget = false;

        // 文字
        var tGO = new GameObject("Text");
        tGO.transform.SetParent(go.transform, false);
        var trt = tGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = new Vector2(-8f, 0f);
        var t = tGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = new Color(1f, 0.92f, 0.55f);
        t.text = $"⚔ 击杀 {victimName}";
        t.raycastTarget = false;

        _killFeedItems.Add(rt);
        // 阶段 1: 0~0.3s 滑入
        float t1 = 0f;
        Vector2 targetPos = new Vector2(-12f, topOffset);
        Vector2 startPos = rt.anchoredPosition;
        while (t1 < 0.3f)
        {
            t1 += Time.deltaTime;
            float r = Mathf.Clamp01(t1 / 0.3f);
            float ease = 1f - Mathf.Pow(1f - r, 2.5f);
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
            yield return null;
        }
        rt.anchoredPosition = targetPos;
        // 阶段 2: 停留 2.0s
        yield return new WaitForSeconds(2.0f);
        // 阶段 3: 0~0.3s 滑出 + 渐隐
        float t3 = 0f;
        Vector2 outPos = targetPos + new Vector2(220f, 0f);
        Color startBg = bg.color, startText = t.color;
        while (t3 < 0.3f)
        {
            t3 += Time.deltaTime;
            float r = Mathf.Clamp01(t3 / 0.3f);
            rt.anchoredPosition = Vector2.Lerp(targetPos, outPos, r);
            bg.color = new Color(startBg.r, startBg.g, startBg.b, startBg.a * (1f - r));
            t.color = new Color(startText.r, startText.g, startText.b, 1f - r);
            yield return null;
        }
        _killFeedItems.Remove(rt);
        Destroy(go);
        // 重排剩余 feed 上移
        for (int i = 0; i < _killFeedItems.Count; i++)
        {
            if (_killFeedItems[i] == null) continue;
            var p = _killFeedItems[i].anchoredPosition;
            _killFeedItems[i].anchoredPosition = new Vector2(p.x, -200f - i * (itemHeight + gap));
        }
    }

    /// <summary>击杀连击：屏幕中央偏上弹出 "X 连击!" 文字 + 缩放弹性进入。</summary>
    public void ShowCombo(int comboCount)
    {
        StartCoroutine(SpawnComboText(comboCount));
    }

    System.Collections.IEnumerator SpawnComboText(int count)
    {
        var canvas = GetHudCanvas();
        if (canvas == null) yield break;

        // 颜色梯度：3 黄、5 橙、8+ 红
        Color color = count >= 8 ? new Color(1f, 0.30f, 0.20f)
                    : count >= 5 ? new Color(1f, 0.60f, 0.20f)
                    : new Color(1f, 0.92f, 0.35f);

        var go = new GameObject("ComboText");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(600f, 80f);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = count >= 8 ? 64 : count >= 5 ? 56 : 48;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = $"{count} 连击!";
        t.raycastTarget = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(3f, -3f);

        // 阶段 1: 0~0.18s 弹性放大 0.3 → 1.25
        float t1 = 0f;
        while (t1 < 0.18f)
        {
            t1 += Time.deltaTime;
            float r = Mathf.Clamp01(t1 / 0.18f);
            rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.25f, r);
            yield return null;
        }
        // 阶段 2: 0~0.10s 回弹 1.25 → 1.0
        float t2 = 0f;
        while (t2 < 0.10f)
        {
            t2 += Time.deltaTime;
            float r = Mathf.Clamp01(t2 / 0.10f);
            rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1.0f, r);
            yield return null;
        }
        // 阶段 3: 停留 0.7s（向上飘 + 微微缩放呼吸）
        float t3 = 0f;
        Vector2 baseAnchor = rt.anchoredPosition;
        while (t3 < 0.7f)
        {
            t3 += Time.deltaTime;
            float r = t3 / 0.7f;
            rt.anchoredPosition = baseAnchor + new Vector2(0f, r * 30f);
            float k = 1f + 0.04f * Mathf.Sin(Time.time * 6f);
            rt.localScale = Vector3.one * k;
            yield return null;
        }
        // 阶段 4: 0~0.3s 渐隐
        float t4 = 0f;
        Color startC = t.color;
        while (t4 < 0.3f)
        {
            t4 += Time.deltaTime;
            float r = Mathf.Clamp01(t4 / 0.3f);
            t.color = new Color(startC.r, startC.g, startC.b, 1f - r);
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>AI 进攻波次预警横幅。</summary>
    public void ShowWaveWarning(int waveIndex, int unitCount, bool twoProng)
    {
        string body = twoProng
            ? $"敌方第 {waveIndex} 波进攻 — {unitCount} 个单位 · 两路包抄!"
            : $"敌方第 {waveIndex} 波进攻 — {unitCount} 个单位来袭!";
        StartCoroutine(SpawnWaveBanner(body));
        // 写入战地通讯
        AppendChatMessage("侦察兵", body, new Color(1f, 0.55f, 0.22f));
        // 警报音
        _UiClickAudio.PlayWarn();
    }

    System.Collections.IEnumerator SpawnWaveBanner(string text)
    {
        var canvas = GetHudCanvas();
        if (canvas == null) yield break;
        var go = new GameObject("WaveWarningBanner");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 80f); // 从屏幕外上方滑入
        rt.sizeDelta = new Vector2(0f, 56f);
        // 红色半透明背景
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.65f, 0.10f, 0.10f, 0.78f);
        bg.raycastTarget = false;
        // 文字
        var tGO = new GameObject("Text");
        tGO.transform.SetParent(go.transform, false);
        var trt = tGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 4f); trt.offsetMax = new Vector2(-20f, -4f);
        var t = tGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 24;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.92f, 0.55f);
        t.text = "⚠  " + text + "  ⚠";
        t.raycastTarget = false;
        var ol = tGO.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(2f, -2f);

        // 阶段 1：滑入 0.4s
        float t1 = 0f, d1 = 0.4f;
        while (t1 < d1)
        {
            // 返回大厅或重开战斗会销毁 HUD 对象，避免横幅协程继续写空引用。
            if (go == null || rt == null) yield break;
            t1 += Time.deltaTime;
            float r = t1 / d1;
            float ease = 1f - Mathf.Pow(1f - r, 2.5f);
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(80f, -10f, ease));
            yield return null;
        }
        if (go == null || rt == null) yield break;
        rt.anchoredPosition = new Vector2(0f, -10f);
        // 阶段 2：停留 2.5s（伴随脉动）
        float t2 = 0f, d2 = 2.5f;
        while (t2 < d2)
        {
            if (go == null || rt == null) yield break;
            t2 += Time.deltaTime;
            float k = 1f + 0.05f * Mathf.Sin(Time.time * 4.5f);
            rt.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        if (go == null || rt == null) yield break;
        rt.localScale = Vector3.one;
        // 阶段 3：滑出 0.35s
        float t3 = 0f, d3 = 0.35f;
        Color startColor = bg.color, txtColor = t.color;
        while (t3 < d3)
        {
            if (go == null || rt == null || bg == null || t == null) yield break;
            t3 += Time.deltaTime;
            float r = t3 / d3;
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(-10f, 80f, r));
            bg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - r));
            t.color = new Color(txtColor.r, txtColor.g, txtColor.b, 1f - r);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>在指定 Text 上方弹出 +/-XXX 浮动数字。</summary>
    void SpawnFloatingDelta(Text anchor, int delta, Color color)
    {
        if (anchor == null || delta == 0) return;
        var go = new GameObject("FloatingDelta");
        go.transform.SetParent(anchor.transform.parent, false);
        var rt = go.AddComponent<RectTransform>();
        var srcRT = anchor.rectTransform;
        rt.anchorMin = srcRT.anchorMin;
        rt.anchorMax = srcRT.anchorMax;
        rt.pivot = srcRT.pivot;
        rt.anchoredPosition = srcRT.anchoredPosition + new Vector2(srcRT.sizeDelta.x * 0.6f, 0f);
        rt.sizeDelta = new Vector2(120f, 28f);
        var t = go.AddComponent<Text>();
        t.font = anchor.font;
        t.fontSize = Mathf.RoundToInt(anchor.fontSize * 0.95f);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.text = (delta > 0 ? "+" : "") + delta.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        t.color = color;
        t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(1.4f, -1.4f);
        go.AddComponent<_HudFloatingDelta>().Init(0.95f, 38f);
        // 同时让源文字短促放大脉动
        StartCoroutine(PulseTextScale(srcRT));
    }

    // 取消生产按钮（动态创建于 ProductionBar 右侧）
    private Button _cancelProductionBtn;
    void EnsureCancelProductionButton(RTSBuilding b)
    {
        if (BuildingPanel == null) return;
        if (_cancelProductionBtn == null)
        {
            Transform parent = BuildingPanel.transform;
            var go = new GameObject("CancelProdBtn");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.55f, 0.18f, 0.10f, 0.95f);
            var btn = go.AddComponent<Button>();
            // 文字
            var tg = new GameObject("Text");
            tg.transform.SetParent(go.transform, false);
            var trt = tg.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = tg.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = "✕";
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 18;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.92f, 0.85f);
            t.raycastTarget = false;
            _cancelProductionBtn = btn;
        }
        LayoutCancelProductionButton();
        // 重新绑定（每次选中不同建筑时刷新 onClick）
        _cancelProductionBtn.onClick.RemoveAllListeners();
        _cancelProductionBtn.onClick.AddListener(() =>
        {
            if (b == null || b.GetHP() <= 0) return;
            if (b.ProductionQueue == null || b.ProductionQueue.Count == 0) return;
            b.CancelLast();
            _UiClickAudio.PlayWarn();
        });
        bool canCancel = b != null && b.bPlayerOwned
            && b.ProductionQueue != null && b.ProductionQueue.Count > 0;
        _cancelProductionBtn.gameObject.SetActive(canCancel);
    }

    /// <summary>Text 短促放大 1.18× 后回弹（0.32s 弹性）。</summary>
    System.Collections.IEnumerator PulseTextScale(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 origin = rt.localScale;
        float t = 0f;
        const float dur = 0.32f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            // 0 → 1.18 → 1.0 弹性
            float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.18f;
            rt.localScale = new Vector3(origin.x * s, origin.y * s, origin.z * s);
            yield return null;
        }
        rt.localScale = origin;
    }

    // 指挥官头像/军衔引用；战场 HUD 中头像和军衔作为一个整体显示。
    private Text _commanderRankText;
    private Text _commanderNameText;
    // 主基地血量条
    private Image _baseHpFill;
    private Text  _baseHpText;
    // 军衔变化检测
    private string _prevRank = "";

    /// <summary>顶部中央插入头像 + 军衔组。</summary>
    void EnsureCommanderHeader(Transform topBar)
    {
        if (topBar.Find("_CommanderHeader") != null) return;
        var holder = new GameObject("_CommanderHeader");
        holder.transform.SetParent(topBar, false);
        var hrt = holder.AddComponent<RectTransform>();
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -2f);
        hrt.sizeDelta = new Vector2(224f, 82f);

        var groupGO = new GameObject("AvatarRankGroup");
        groupGO.transform.SetParent(holder.transform, false);
        var groupRT = groupGO.AddComponent<RectTransform>();
        groupRT.anchorMin = groupRT.anchorMax = new Vector2(0.5f, 1f);
        groupRT.pivot = new Vector2(0.5f, 1f);
        groupRT.anchoredPosition = new Vector2(0f, -1f);
        groupRT.sizeDelta = new Vector2(208f, 64f);
        var groupBg = groupGO.AddComponent<Image>();
        groupBg.color = new Color(0.17f, 0.13f, 0.05f, 0f);
        groupBg.raycastTarget = false;

        var frameGO = new GameObject("AvatarFrame");
        frameGO.transform.SetParent(groupGO.transform, false);
        var frameRT = frameGO.AddComponent<RectTransform>();
        frameRT.anchorMin = frameRT.anchorMax = new Vector2(0f, 1f);
        frameRT.pivot = new Vector2(0.5f, 1f);
        frameRT.anchoredPosition = new Vector2(36f, -1f);
        frameRT.sizeDelta = new Vector2(62f, 62f);
        var frame = frameGO.AddComponent<Image>();
        frame.sprite = LoadHudSprite("LobbyGen/gen_portrait_ring");
        frame.preserveAspect = true;
        frame.color = frame.sprite != null ? Color.white : new Color(0.88f, 0.72f, 0.32f, 0.95f);
        frame.raycastTarget = false;

        var avatarGO = new GameObject("Avatar");
        avatarGO.transform.SetParent(groupGO.transform, false);
        var art = avatarGO.AddComponent<RectTransform>();
        art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0.5f, 1f);
        art.anchoredPosition = new Vector2(36f, -6f);
        art.sizeDelta = new Vector2(52f, 52f);
        var avatar = avatarGO.AddComponent<Image>();
        string avatarRes = PlayerPrefs.GetString("player_avatar_resource", "gen_avatar_player");
        avatar.sprite = LoadPlayerAvatarHudSprite(avatarRes);
        avatar.preserveAspect = true;
        avatar.color = Color.white;
        avatar.raycastTarget = false;

        // 名字（顶行，居中）
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(holder.transform, false);
        var nrt = nameGO.AddComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0.5f, 1f);
        nrt.anchorMax = new Vector2(0.5f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.anchoredPosition = new Vector2(0f, -10f);
        nrt.sizeDelta = new Vector2(220f, 20f);
        _commanderNameText = nameGO.AddComponent<Text>();
        _commanderNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_commanderNameText.font == null) _commanderNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _commanderNameText.text = "指挥官";
        _commanderNameText.fontSize = 14;
        _commanderNameText.fontStyle = FontStyle.Bold;
        _commanderNameText.alignment = TextAnchor.MiddleCenter;
        _commanderNameText.color = new Color(1f, 0.92f, 0.55f);
        _commanderNameText.raycastTarget = false;
        nameGO.SetActive(false);

        // 军衔行容器（放在头像右侧：[军衔章] + [军衔文字]）
        var rankRowGO = new GameObject("RankRow");
        rankRowGO.transform.SetParent(groupGO.transform, false);
        var rrowRT = rankRowGO.AddComponent<RectTransform>();
        rrowRT.anchorMin = rrowRT.anchorMax = new Vector2(0f, 1f);
        rrowRT.pivot = new Vector2(0f, 1f);
        rrowRT.anchoredPosition = new Vector2(76f, -10f);
        rrowRT.sizeDelta = new Vector2(128f, 48f);

        var iconGO = new GameObject("RankBadge");
        iconGO.transform.SetParent(rankRowGO.transform, false);
        var irt = iconGO.AddComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(0f, 0f);
        irt.sizeDelta = new Vector2(50f, 50f);
        var badge = iconGO.AddComponent<Image>();
        badge.sprite = LoadHudSprite("BattleHud/battle_rank_badge_placeholder")
            ?? LoadHudSprite("icons_final/medal")
            ?? LoadHudSprite("LobbyGen/gen_icon_star");
        badge.preserveAspect = true;
        badge.color = badge.sprite != null ? Color.white : new Color(1f, 0.92f, 0.55f);
        badge.raycastTarget = false;

        // 军衔文字（居中显示在 RankRow 内）
        var rankGO = new GameObject("RankText");
        rankGO.transform.SetParent(rankRowGO.transform, false);
        var rrt = rankGO.AddComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0f, 0.5f);
        rrt.pivot = new Vector2(0f, 0.5f);
        rrt.anchoredPosition = new Vector2(56f, 0f);
        rrt.sizeDelta = new Vector2(70f, 30f);
        _commanderRankText = rankGO.AddComponent<Text>();
        _commanderRankText.font = _commanderNameText.font;
        _commanderRankText.text = GetBattleRankTitle();
        _commanderRankText.fontSize = 14;
        _commanderRankText.fontStyle = FontStyle.Bold;
        _commanderRankText.alignment = TextAnchor.MiddleLeft;
        _commanderRankText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _commanderRankText.verticalOverflow = VerticalWrapMode.Overflow;
        _commanderRankText.resizeTextForBestFit = true;
        _commanderRankText.resizeTextMinSize = 10;
        _commanderRankText.resizeTextMaxSize = 14;
        _commanderRankText.color = new Color(1f, 0.90f, 0.55f);
        _commanderRankText.raycastTarget = false;
        var rankShadow = rankGO.AddComponent<Shadow>();
        rankShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        rankShadow.effectDistance = new Vector2(1.2f, -1.2f);
        _prevRank = _commanderRankText.text;
        rankRowGO.SetActive(true);

        // 主基地血量条（在头像正下方，超出 TopBar 显示）
        var bhBg = new GameObject("BaseHpBg");
        bhBg.transform.SetParent(holder.transform, false);
        var bhrt = bhBg.AddComponent<RectTransform>();
        bhrt.anchorMin = new Vector2(0.5f, 1f);
        bhrt.anchorMax = new Vector2(0.5f, 1f);
        bhrt.pivot = new Vector2(0.5f, 1f);
        bhrt.anchoredPosition = new Vector2(0f, -64f);
        bhrt.sizeDelta = new Vector2(180f, 14f);
        var bhBgImg = bhBg.AddComponent<Image>();
        bhBgImg.color = new Color(0f, 0f, 0f, 0.85f);
        bhBgImg.raycastTarget = false;
        var bhol = bhBg.AddComponent<Outline>();
        bhol.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.95f);
        bhol.effectDistance = new Vector2(1.5f, -1.5f);
        // 填充
        var fill = new GameObject("Fill");
        fill.transform.SetParent(bhBg.transform, false);
        var fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(2f, 2f); fillRT.offsetMax = new Vector2(-2f, -2f);
        _baseHpFill = fill.AddComponent<Image>();
        _baseHpFill.color = new Color(0.20f, 0.85f, 0.30f);
        _baseHpFill.type = Image.Type.Filled;
        _baseHpFill.fillMethod = Image.FillMethod.Horizontal;
        _baseHpFill.fillAmount = 1f;
        _baseHpFill.raycastTarget = false;
        // 数字 "3000/3000"
        var bnTxt = new GameObject("Txt");
        bnTxt.transform.SetParent(bhBg.transform, false);
        var btrt = bnTxt.AddComponent<RectTransform>();
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;
        _baseHpText = bnTxt.AddComponent<Text>();
        _baseHpText.font = _commanderNameText.font;
        _baseHpText.fontSize = 11;
        _baseHpText.fontStyle = FontStyle.Bold;
        _baseHpText.alignment = TextAnchor.MiddleCenter;
        _baseHpText.color = Color.white;
        _baseHpText.text = "基地 3000/3000";
        _baseHpText.raycastTarget = false;
        var sh = bnTxt.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(1f, -1f);
        bhBg.SetActive(false);

        MakeCommanderGroupBackgroundTransparent(groupGO.transform);
    }

    string GetBattleRankTitle()
    {
        string rank = "";
        var net = NetworkClient.Instance;
        if (net != null && !string.IsNullOrEmpty(net.RankTitle))
            rank = net.RankTitle;
        if (string.IsNullOrEmpty(rank))
            rank = PlayerPrefs.GetString("net_rank_title", "");
        if (string.IsNullOrEmpty(rank))
            rank = PlayerPrefs.GetString("player_rank_title", "");
        if (string.IsNullOrEmpty(rank))
            rank = "列兵";
        return rank.Trim();
    }

    void SyncBattleRankTitle()
    {
        if (_commanderRankText == null) return;

        string rank = GetBattleRankTitle();
        if (rank != _prevRank || _commanderRankText.text != rank)
        {
            _commanderRankText.text = rank;
            _prevRank = rank;
        }

        var rankRow = _commanderRankText.transform.parent;
        if (rankRow != null && !rankRow.gameObject.activeSelf)
            rankRow.gameObject.SetActive(true);
    }

    /// <summary>计算玩家所有产金建筑的总收入速率（金币/秒）。</summary>
    int ComputePlayerGoldRate()
    {
        var bldgs = GameManager.Instance?.GetAllBuildings();
        if (bldgs == null) return 0;
        float rate = 0f;
        foreach (var b in bldgs)
        {
            if (b == null || !b.bPlayerOwned || b.bUnderConstruction || b.GoldIncomeAmount <= 0) continue;
            float interval = Mathf.Max(0.1f, b.GoldIncomeInterval);
            rate += b.GoldIncomeAmount / interval;
        }
        return Mathf.RoundToInt(rate);
    }

    void UpdateTopBar()
    {
        if (playerState == null) return;
        SyncBattleRankTitle();
        // 同步主基地血量
        var mb = GameManager.Instance?.PlayerMainBase;
        if (mb != null)
        {
            int curHp = mb.GetHP();
            int maxHp = mb.GetMaxHP();
            if (_baseHpFill != null)
            {
                float r = maxHp > 0 ? (float)curHp / maxHp : 0f;
                _baseHpFill.fillAmount = Mathf.Clamp01(r);
                // 颜色随血量变化
                _baseHpFill.color = r > 0.55f
                    ? Color.Lerp(new Color(0.85f, 0.82f, 0.10f), new Color(0.20f, 0.85f, 0.30f), (r - 0.55f) / 0.45f)
                    : Color.Lerp(new Color(0.90f, 0.18f, 0.18f), new Color(0.85f, 0.82f, 0.10f), r / 0.55f);
            }
            if (_baseHpText != null)
                _baseHpText.text = $"基地 {curHp}/{maxHp}";
        }
        if (GoldText)
        {
            // 每 1 秒重算收入速率
            _goldRateTimer -= Time.deltaTime;
            if (_goldRateTimer <= 0f)
            {
                _goldRateTimer = 1f;
                _cachedGoldRate = ComputePlayerGoldRate();
            }
            GoldText.text = _cachedGoldRate > 0
                ? $"$ {playerState.Gold:N0}  +{_cachedGoldRate}/s"
                : $"$ {playerState.Gold:N0}";
            int curGold = playerState.Gold;
            if (_prevGold >= 0 && curGold != _prevGold)
            {
                int delta = curGold - _prevGold;
                _goldFlashColor = delta > 0
                    ? new Color(0.7f, 1f, 0.45f)   // 增加：亮绿
                    : new Color(1f, 0.42f, 0.18f);  // 减少：橙红
                _goldFlashTimer = 0.35f;
                SpawnFloatingDelta(GoldText, delta, _goldFlashColor);
            }
            _prevGold = curGold;
            if (_goldFlashTimer > 0f)
            {
                _goldFlashTimer -= Time.deltaTime;
                float t = _goldFlashTimer / 0.35f;
                Color baseColor = curGold < 200 ? new Color(1f, 0.32f, 0.32f) : new Color(1f, 0.88f, 0.2f);
                GoldText.color = Color.Lerp(baseColor, _goldFlashColor, t);
            }
            else
            {
                GoldText.color = curGold < 200
                    ? new Color(1f, 0.32f, 0.32f)
                    : new Color(1f, 0.88f, 0.2f);
            }
        }
        if (PopText)
        {
            PopText.text = $"人口 {playerState.PopUsed}/{playerState.PopCap}";
            bool popFull = playerState.PopUsed >= playerState.PopCap;
            PopText.color = popFull ? new Color(1f, 0.55f, 0.2f) : new Color(0.65f, 0.9f, 1f);
            int curPop = playerState.PopUsed;
            if (_prevPopUsed >= 0 && curPop != _prevPopUsed)
            {
                int dp = curPop - _prevPopUsed;
                SpawnFloatingDelta(PopText, dp, dp > 0 ? new Color(0.65f, 0.95f, 1f) : new Color(1f, 0.55f, 0.2f));
            }
            _prevPopUsed = curPop;
        }
        if (KillText)
        {
            var sync = GameNetworkSync.Instance;
            if (sync != null && sync.IsNetworkGame)
            {
                string roleTag = sync.IsHost ? "[HOST]" : "[联机]";
                string connTag = sync.PeerConnected ? "▲" : "…";
                KillText.text  = $"{roleTag}{connTag} 击杀 {playerState.EnemyKillCount}";
                KillText.color = sync.PeerConnected ? new Color(0.5f,1f,0.5f) : new Color(1f,0.8f,0.3f);
            }
            else
                KillText.text = $"击杀 {playerState.EnemyKillCount}";
            int curKill = playerState.EnemyKillCount;
            if (_prevKill >= 0 && curKill > _prevKill)
                SpawnFloatingDelta(KillText, curKill - _prevKill, new Color(1f, 0.62f, 0.32f));
            _prevKill = curKill;
        }
        if (PowerText)
        {
            PowerText.text = $"电力 {playerState.PowerUsed}/{playerState.PowerCap}";
            bool overload = playerState.PowerUsed > playerState.PowerCap;
            PowerText.color = overload ? new Color(1f, 0.45f, 0.2f) : new Color(0.4f, 1f, 0.55f);
            // 电力上限变化飘字（建发电机 → +N，建电厂被拆 → -N）
            int curCap = playerState.PowerCap;
            if (_prevPowerCap >= 0 && curCap != _prevPowerCap)
            {
                int dp = curCap - _prevPowerCap;
                SpawnFloatingDelta(PowerText, dp, dp > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.45f, 0.2f));
            }
            _prevPowerCap = curCap;
        }
        if (GameTimerText && GameManager.Instance != null)
        {
            int t = (int)GameManager.Instance.GameTime;
            GameTimerText.text = $"{t/60:00}:{t%60:00}";
        }

        // 警告脉动：当资源/人口处于危险状态时，文字 alpha 周期性闪烁，强化感知。
        // 金币 < 200 = 即将买不起单位；人口已满 = 无法继续生产；电力超载 = 生产/攻击效率受影响。
        float warnPulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.6f));
        if (GoldText && playerState.Gold < 200 && _goldFlashTimer <= 0f)
            ApplyTextAlpha(GoldText, warnPulse);
        if (PopText && playerState.PopUsed >= playerState.PopCap)
            ApplyTextAlpha(PopText, warnPulse);
        if (PowerText && playerState.PowerUsed > playerState.PowerCap)
            ApplyTextAlpha(PowerText, warnPulse);
    }

    /// <summary>把 Text 的当前 color 的 alpha 调制为指定值（0~1），保留 RGB。</summary>
    static void ApplyTextAlpha(Text t, float alpha)
    {
        if (t == null) return;
        Color c = t.color;
        c.a = Mathf.Clamp01(alpha);
        t.color = c;
    }

    private struct SelectedForceGroup
    {
        public string Name;
        public int Count;
        public int HP;
        public int MaxHP;
    }

    static List<SelectedForceGroup> BuildSelectedForceGroups(
        List<RTSUnit> units,
        out int totalCount,
        out int totalHP,
        out int totalMaxHP,
        out int airCount,
        out float airFuelSum,
        out int parkedAirCount,
        out int returningAirCount)
    {
        var groups = new List<SelectedForceGroup>();
        totalCount = 0;
        totalHP = 0;
        totalMaxHP = 0;
        airCount = 0;
        airFuelSum = 0f;
        parkedAirCount = 0;
        returningAirCount = 0;

        if (units == null) return groups;

        for (int i = 0; i < units.Count; i++)
        {
            RTSUnit unit = units[i];
            if (unit == null || unit.IsDead()) continue;

            int hp = Mathf.Max(0, unit.GetHP());
            int maxHP = Mathf.Max(1, unit.GetMaxHP());
            string name = GetUnitDisplayNameStatic(unit);
            int groupIndex = FindSelectedForceGroup(groups, name);
            SelectedForceGroup group;
            if (groupIndex >= 0)
            {
                group = groups[groupIndex];
                group.Count++;
                group.HP += hp;
                group.MaxHP += maxHP;
                groups[groupIndex] = group;
            }
            else
            {
                group = new SelectedForceGroup
                {
                    Name = name,
                    Count = 1,
                    HP = hp,
                    MaxHP = maxHP
                };
                groups.Add(group);
            }

            totalCount++;
            totalHP += hp;
            totalMaxHP += maxHP;

            AirUnit air = unit as AirUnit;
            if (air != null)
            {
                airCount++;
                airFuelSum += Mathf.Clamp01(air.FuelRatio);
                if (air.IsParked) parkedAirCount++;
                else if (air.IsReturningToRefuel) returningAirCount++;
            }
        }

        return groups;
    }

    static int FindSelectedForceGroup(List<SelectedForceGroup> groups, string name)
    {
        for (int i = 0; i < groups.Count; i++)
            if (groups[i].Name == name)
                return i;
        return -1;
    }

    static string BuildSelectedForceSummary(List<SelectedForceGroup> groups, int maxGroups = 0)
    {
        if (groups == null || groups.Count == 0) return "";

        int shown = maxGroups > 0 ? Mathf.Min(maxGroups, groups.Count) : groups.Count;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) sb.Append(" · ");
            sb.Append(groups[i].Name);
            sb.Append(" x");
            sb.Append(groups[i].Count);
        }

        int hidden = groups.Count - shown;
        if (hidden > 0)
        {
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append("+");
            sb.Append(hidden);
            sb.Append("类");
        }

        return sb.ToString();
    }

    static string BuildAirSelectionState(int airCount, int parkedAirCount, int returningAirCount)
    {
        if (airCount <= 0) return "";
        if (parkedAirCount == airCount) return "停机加油";
        if (returningAirCount == airCount) return "返场加油";
        if (parkedAirCount + returningAirCount == airCount) return "加油中";
        if (parkedAirCount > 0 || returningAirCount > 0) return "飞行/加油";
        return "飞行";
    }

    static string BuildUnitCombatStatsText(RTSUnit unit)
    {
        if (unit == null) return string.Empty;
        string areaDamage = unit.SplashRadius > 0.05f ? "是" : "否";
        return $"攻击距离 {unit.AttackRange:0.#}\n攻击力 {unit.AttackDamage}\n范围伤害 {areaDamage}";
    }

    void UpdateSelectionUI()
    {
        // 清除已死亡或已销毁的引用，防止 MissingReferenceException
        currentSelectedUnits.RemoveAll(u => u == null || u.IsDead());
        // 选中建筑被摧毁时自动关闭建筑面板
        if (currentSelectedBuilding != null && currentSelectedBuilding.GetHP() <= 0)
            currentSelectedBuilding = null;

        if (currentSelectedUnits.Count > 0)
        {
            RTSUnit u = currentSelectedUnits[0];
            int selectedCount;
            int selectedHP;
            int selectedMaxHP;
            int airCount;
            float airFuelSum;
            int parkedAirCount;
            int returningAirCount;
            List<SelectedForceGroup> forceGroups = BuildSelectedForceGroups(
                currentSelectedUnits,
                out selectedCount,
                out selectedHP,
                out selectedMaxHP,
                out airCount,
                out airFuelSum,
                out parkedAirCount,
                out returningAirCount);
            string forceSummary = BuildSelectedForceSummary(forceGroups);
            if (UnitInfoPanel) UnitInfoPanel.SetActive(true);
            if (BuildingPanel) BuildingPanel.SetActive(false);
            if (UnitNameText)
            {
                // 老兵星级（金色 ★/★★/★★★）+ 选中兵种统计
                int sumKills = 0;
                int maxKillsThisGroup = 0;
                for (int k = 0; k < currentSelectedUnits.Count; k++)
                {
                    var ku = currentSelectedUnits[k]; if (ku == null) continue;
                    sumKills += ku.Kills;
                    if (ku.Kills > maxKillsThisGroup) maxKillsThisGroup = ku.Kills;
                }
                string starPrefix = "";
                if (maxKillsThisGroup >= 5)      starPrefix = "<color=#FFD93B>★★★</color> ";
                else if (maxKillsThisGroup >= 3) starPrefix = "<color=#FFEC80>★★</color> ";
                else if (maxKillsThisGroup >= 1) starPrefix = "<color=#D8E0F2>★</color> ";
                string suffix = sumKills > 0 ? $"  <color=#FFB266>击杀{sumKills}</color>" : "";
                UnitNameText.supportRichText = true;
                UnitNameText.resizeTextForBestFit = currentSelectedUnits.Count > 1;
                UnitNameText.resizeTextMinSize = 12;
                UnitNameText.resizeTextMaxSize = 18;
                if (currentSelectedUnits.Count > 1)
                {
                    string titleSummary = forceSummary.Length <= 24 ? forceSummary : $"选中 {selectedCount} 兵力";
                    UnitNameText.text = $"{starPrefix}{titleSummary}{suffix}";
                }
                else
                {
                    UnitNameText.text = $"{starPrefix}{GetUnitDisplayNameStatic(u)}{suffix}";
                }
            }
            float uhpRatio = currentSelectedUnits.Count > 1
                ? (float)selectedHP / Mathf.Max(1, selectedMaxHP)
                : (float)u.GetHP() / Mathf.Max(1, u.GetMaxHP());
            if (UnitHPBar) { UnitHPBar.value = uhpRatio; SetHPBarColor(UnitHPBar, uhpRatio); }
            if (UnitHPText)
            {
                UnitHPText.resizeTextForBestFit = false;
                UnitHPText.resizeTextMinSize = 10;
                UnitHPText.resizeTextMaxSize = 12;
                UnitHPText.fontSize = 12;
                UnitHPText.alignment = TextAnchor.UpperCenter;
                UnitHPText.horizontalOverflow = HorizontalWrapMode.Wrap;
                UnitHPText.verticalOverflow = VerticalWrapMode.Overflow;
                UnitHPText.lineSpacing = 0.88f;
                if (currentSelectedUnits.Count > 1)
                {
                    string hpText = $"总生命 {selectedHP}/{selectedMaxHP}";
                    if (airCount > 0)
                    {
                        int avgFuel = Mathf.RoundToInt((airFuelSum / Mathf.Max(1, airCount)) * 100f);
                        string airState = BuildAirSelectionState(airCount, parkedAirCount, returningAirCount);
                        hpText += $"\n{forceSummary} · 燃油均 {avgFuel}% · {airState}";
                    }
                    else
                    {
                        hpText += $"\n{forceSummary}";
                    }
                    UnitHPText.text = hpText;
                }
                else
                {
                    string hpText = $"{u.GetHP()}/{u.GetMaxHP()}";
                    var air = u as AirUnit;
                    if (air != null)
                    {
                        string airState = air.IsParked ? "停机加油" : (air.IsReturningToRefuel ? "返场加油" : "飞行");
                        hpText += $"\n燃油 {Mathf.RoundToInt(air.FuelRatio * 100f)}% · {airState}";
                    }
                    if (u is Tank)
                    {
                        hpText += "\n" + BuildUnitCombatStatsText(u);
                    }
                    UnitHPText.text = hpText;
                }
            }
            UpdateSelectedUnitSkillButton();
        }
        else if (currentSelectedBuilding != null)
        {
            RTSBuilding b = currentSelectedBuilding;
            bool supportsUpgrade = b.SupportsBuildingUpgrade;
            bool showOwnedUpgrade = supportsUpgrade && b.bPlayerOwned;
            if (UnitInfoPanel) UnitInfoPanel.SetActive(false);
            if (SkillButton && SkillButton.gameObject.activeSelf)
                SkillButton.gameObject.SetActive(false);
            if (BuildingPanel) BuildingPanel.SetActive(true);
            LayoutBuildingPanelControls();
            if (BuildingNameText) BuildingNameText.text = b.DisplayName;
            float bhpRatio = (float)b.GetHP() / Mathf.Max(1, b.GetMaxHP());
            if (BuildingHPBar) { BuildingHPBar.value = bhpRatio; SetHPBarColor(BuildingHPBar, bhpRatio); }
            if (BuildingHPText) BuildingHPText.text = $"{b.GetHP()}/{b.GetMaxHP()}";
            if (b.bUnderConstruction)
            {
                float buildRatio = Mathf.Clamp01(b.ConstructionProgress);
                if (ProductionBar) ProductionBar.value = buildRatio;
                EnsureCancelProductionButton(b);
                if (ProductionText)
                {
                    ProductionText.supportRichText = true;
                    ProductionText.text = $"<color=#FFD56B>建造中</color> ({Mathf.RoundToInt(buildRatio * 100f)}%)";
                }
                if (ProductionButtons != null)
                {
                    for (int i = 0; i < ProductionButtons.Length; i++)
                        if (ProductionButtons[i] != null)
                            ProductionButtons[i].gameObject.SetActive(false);
                }
                return;
            }
            if (ProductionBar) ProductionBar.value = GetSmoothedProductionBarValue(b);
            EnsureCancelProductionButton(b);
            if (ProductionText)
            {
                bool hasProd = b.ProductionUnits != null && b.ProductionUnits.Length > 0;
                ProductionText.supportRichText = true;
                if (!hasProd)
                    ProductionText.text = string.Empty;
                else if (b.ProductionQueue.Count > 0)
                {
                    string current = "";
                    int firstIdx = b.ProductionQueue[0];
                    if (firstIdx >= 0 && firstIdx < b.ProductionUnits.Length && b.ProductionUnits[firstIdx] != null)
                    {
                        var p0 = b.ProductionUnits[firstIdx].GetComponent<RTSUnit>();
                        string n0 = p0 != null ? GetProductionUnitDisplayName(b, firstIdx, p0) : $"单位{firstIdx+1}";
                        current = $"<color=#FFD56B>生产 {n0}</color>  <color=#EAF2FF>{Mathf.RoundToInt(Mathf.Clamp01(b.ProductionProgress) * 100f)}%</color>";
                    }
                    ProductionText.text = current;
                }
                else if (showOwnedUpgrade)
                    ProductionText.text = "<color=#7E867F>空闲</color>";
                else
                    ProductionText.text = "<color=#7E867F>空闲</color>";
            }
            // 更新生产按鈕显示（onClick 已在 OnSelectionChanged 里绑定，这里只更新文字和可见性）
            if (ProductionButtons != null)
            {
                bool hasProd = b.ProductionUnits != null && b.ProductionUnits.Length > 0;
                int upgradeButtonIndex = showOwnedUpgrade ? b.GetUpgradeButtonSlotIndex() : -1;
                bool canUpgrade = showOwnedUpgrade && b.CanUpgradeBuilding(out _);
                for (int i = 0; i < ProductionButtons.Length; i++)
                {
                    if (ProductionButtons[i] == null) continue;
                    bool isProductionButton = hasProd && i < b.ProductionUnits.Length && b.ProductionUnits[i] != null;
                    bool isUpgradeButton = showOwnedUpgrade && i == upgradeButtonIndex;
                    bool active = isProductionButton || isUpgradeButton;
                    ProductionButtons[i].gameObject.SetActive(active);
                    if (!active)
                        continue;

                    if (isUpgradeButton)
                    {
                        var upgradeSprite = LoadButtonBackdropSprite("action_upgrade");
                        var upgradeBtnImg = ProductionButtons[i].GetComponent<Image>();
                        if (upgradeBtnImg != null)
                        {
                            if (upgradeSprite != null)
                            {
                                upgradeBtnImg.sprite = upgradeSprite;
                                upgradeBtnImg.type = Image.Type.Simple;
                                upgradeBtnImg.preserveAspect = false;
                                upgradeBtnImg.color = canUpgrade ? Color.white : new Color(0.55f, 0.58f, 0.62f, 0.86f);
                            }
                            else
                            {
                                upgradeBtnImg.color = canUpgrade
                                    ? new Color(0.18f, 0.30f, 0.17f, 0.98f)
                                    : new Color(0.12f, 0.16f, 0.22f, 0.88f);
                            }
                        }

                        var upgradeText = ProductionButtons[i].GetComponentInChildren<Text>();
                        if (upgradeText)
                        {
                            upgradeText.text = b.GetUpgradeButtonText();
                            upgradeText.color = canUpgrade
                                ? new Color(1f, 0.92f, 0.55f)
                                : new Color(0.78f, 0.82f, 0.88f);
                            upgradeText.alignment = TextAnchor.MiddleCenter;
                        }

                        ProductionButtons[i].interactable = true;
                        continue;
                    }

                    RTSUnit proto = b.ProductionUnits[i].GetComponent<RTSUnit>();
                    int cost = (b.ProductionCosts != null && i < b.ProductionCosts.Length) ? b.ProductionCosts[i] : 0;
                    int popCost = proto != null ? Mathf.Max(1, proto.PopCost) : 1;
                    var airfield = b as AirFactory;
                    bool hasAircraftSlot = airfield == null || airfield.HasAircraftSlotForProductionIndex(i);
                    bool queueFull = b.IsProductionQueueFull();
                    var productionText = ProductionButtons[i].GetComponentInChildren<Text>();
                    var txt = productionText;
                    if (productionText)
                    {
                        string uname = proto != null ? GetProductionUnitDisplayName(b, i, proto) : $"单位{i+1}";
                        productionText.text = popCost > 1
                            ? $"{uname}\n({cost}金 · {popCost}人)"
                            : $"{uname}\n({cost}金)";
                    }
                    bool canAfford = playerState == null || playerState.Gold >= cost;
                    if (productionText != null && !hasAircraftSlot)
                        txt.text += "\n机位满";
                    bool hasRoom   = playerState == null || playerState.PopUsed + popCost <= playerState.PopCap;
                    bool ok = canAfford && hasRoom && hasAircraftSlot && !queueFull;
                    ProductionButtons[i].interactable = true;
                    var productionBtnImg = ProductionButtons[i].GetComponent<Image>();
                    if (productionBtnImg != null)
                    {
                        var backdrop = GetProductionButtonBackdropSprite(proto);
                        if (backdrop != null)
                        {
                            productionBtnImg.sprite = backdrop;
                            productionBtnImg.type = Image.Type.Simple;
                            productionBtnImg.preserveAspect = false;
                            productionBtnImg.color = ok ? Color.white : new Color(0.52f, 0.56f, 0.62f, 0.84f);
                        }
                        else
                        {
                        Color baseC = new Color(0.16f, 0.22f, 0.32f, 0.96f);
                        productionBtnImg.color = ok ? baseC
                            : new Color(baseC.r * 0.55f, baseC.g * 0.55f, baseC.b * 0.55f, 0.85f);
                        }
                    }
                    if (txt != null)
                    {
                        txt.color = queueFull ? new Color(0.67f, 0.76f, 0.86f)
                                   : !canAfford ? new Color(1f, 0.4f, 0.34f)
                                   : !hasRoom ? new Color(1f, 0.78f, 0.30f)
                                   : !hasAircraftSlot ? new Color(0.55f, 0.78f, 1f)
                                   : new Color(1f, 0.92f, 0.55f);
                    }
                }
            }
        }
        else
        {
            if (UnitInfoPanel) UnitInfoPanel.SetActive(false);
            if (BuildingPanel) BuildingPanel.SetActive(false);
            if (SkillButton && SkillButton.gameObject.activeSelf)
                SkillButton.gameObject.SetActive(false);
        }
    }

    float GetSmoothedProductionBarValue(RTSBuilding b)
    {
        if (b == null)
        {
            _productionBarOwner = null;
            _productionBarDisplayValue = 0f;
            return 0f;
        }

        bool hasQueue = b.ProductionQueue != null && b.ProductionQueue.Count > 0;
        float target = hasQueue ? Mathf.Clamp01(b.ProductionDisplayProgress) : 0f;
        if (_productionBarOwner != b)
        {
            _productionBarOwner = b;
            _productionBarDisplayValue = target;
            return _productionBarDisplayValue;
        }

        float speed = target < _productionBarDisplayValue ? 3.5f : 8f;
        _productionBarDisplayValue = Mathf.MoveTowards(
            _productionBarDisplayValue,
            target,
            speed * Time.unscaledDeltaTime);
        return _productionBarDisplayValue;
    }

    public void OnSelectionChanged(List<RTSUnit> units, RTSBuilding building)
    {
        HideRightClickCommandMenu();
        currentSelectedUnits = units ?? new List<RTSUnit>();
        currentSelectedBuilding = building;
        if (_pendingUpgradeBuilding != null && _pendingUpgradeBuilding != building)
            CloseBuildingUpgradePanel();
        if (building != _productionBarOwner)
        {
            _productionBarOwner = null;
            _productionBarDisplayValue = 0f;
        }
        BindProductionButtons(building);
        UpdateCommandBarState();
        UpdateSelectedUnitSkillButton();
    }

    void BindProductionButtons(RTSBuilding b)
    {
        if (ProductionButtons == null) return;
        for (int i = 0; i < ProductionButtons.Length; i++)
        {
            if (ProductionButtons[i] == null) continue;
            ProductionButtons[i].onClick.RemoveAllListeners();
        }

        int upgradeButtonIndex = b != null && b.SupportsBuildingUpgrade && b.bPlayerOwned
            ? b.GetUpgradeButtonSlotIndex()
            : -1;
        if (upgradeButtonIndex >= 0
            && upgradeButtonIndex < ProductionButtons.Length
            && ProductionButtons[upgradeButtonIndex] != null)
        {
            int capturedUpgradeIndex = upgradeButtonIndex;
            ProductionButtons[capturedUpgradeIndex].onClick.AddListener(() => {
                if (b == null || b.GetHP() <= 0) return;
                OpenBuildingUpgradePanel(b);
            });
        }

        for (int i = 0; i < ProductionButtons.Length; i++)
        {
            if (ProductionButtons[i] == null) continue;
            if (b == null || b.ProductionUnits == null || i >= b.ProductionUnits.Length || b.ProductionUnits[i] == null) continue;
            int idx = i;
            ProductionButtons[i].onClick.AddListener(() => {
                if (b == null || b.GetHP() <= 0) return;
                if (b.IsProductionQueueFull())
                {
                    _UiClickAudio.PlayDeny();
                    ShowAlert("生产队列已满");
                    StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                    return;
                }
                // 实时 affordability 检查：金币 / 人口（基于该单位的 PopCost）。不足则 deny 反馈
                int cost = (b.ProductionCosts != null && idx < b.ProductionCosts.Length) ? b.ProductionCosts[idx] : 0;
                RTSUnit proto = b.ProductionUnits[idx] != null ? b.ProductionUnits[idx].GetComponent<RTSUnit>() : null;
                int popCost = proto != null ? Mathf.Max(1, proto.PopCost) : 1;
                string unitName = proto != null ? GetProductionUnitDisplayName(b, idx, proto) : $"单位{idx + 1}";
                if (playerState != null)
                {
                    if (playerState.Gold < cost)
                    {
                        _UiClickAudio.PlayDeny();
                        ShowAlert($"金币不足：生产 {unitName} 需要 {cost} 金（当前 {playerState.Gold}）");
                        StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                        return;
                    }
                    if (playerState.PopUsed + popCost > playerState.PopCap)
                    {
                        _UiClickAudio.PlayDeny();
                        ShowAlert($"人口不足：生产 {unitName} 需要 {popCost} 人口（当前 {playerState.PopUsed}/{playerState.PopCap}）");
                        StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                        return;
                    }
                }
                var airfield = b as AirFactory;
                if (airfield != null && !airfield.HasAircraftSlotForProductionIndex(idx))
                {
                    _UiClickAudio.PlayDeny();
                    ShowAlert("停机场机位已满：每座只能停放4架飞机");
                    StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                    return;
                }
                if (b.EnqueueUnit(idx))
                {
                    _UiClickAudio.PlayConfirm();
                    ShowAlert($"{unitName} 已加入生产队列");
                }
                else
                {
                    _UiClickAudio.PlayDeny();
                    ShowAlert($"暂时无法生产 {unitName}");
                    StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                }
            });
        }
    }

    public void ShowGameOver(bool win, int kills = 0, int seconds = 0)
    {
        if (_settingsOpen)
            SetGameSettingsOpen(false);
        PrepareGameOverOverlay();
        // 结算开场冲击：一次轻震屏 + 全屏白闪过渡（胜利金色，失败暗红）
        var rtsCam = RTSCamera.Instance;
        if (rtsCam != null) RTSCamera.Shake(win ? 0.55f : 0.40f, 0.35f);
        SpawnGameOverFlash(win);
        if (GameOverPanel)
        {
            GameOverPanel.SetActive(true);
            GameOverPanel.transform.SetAsLastSibling();
        }
        EnsureGameOverPanelLayout();
        SetGameOverResultBadge(win);
        PlayGameOverCardIntro();
        GameManager.BattleReportData reportData = GameManager.Instance != null
            ? GameManager.Instance.BuildBattleReportData(kills, seconds)
            : BuildFallbackBattleReportData(kills, seconds);
        UpdateGameOverSummaryCards(kills, seconds);
        UpdateGameOverBattleTable(reportData);
        if (GameOverText)
        {
            GameOverText.text = win ? "胜利" : "失败";
            GameOverText.color = win ? new Color(0.9f, 0.8f, 0.1f) : new Color(0.9f, 0.3f, 0.3f);
            StartCoroutine(AnimateGameOverTitle(GameOverText, win));
        }
        if (GameOverStatsText)
        {
            GameOverStatsText.text = BuildBattleReportText(reportData);
            GameOverStatsText.gameObject.SetActive(false);
        }
        // 联机模式：隐藏"再来一局"（单端重启无意义），突出"大厅"按钮
        var netSync = GameNetworkSync.Instance;
        bool isNet = netSync != null && netSync.IsNetworkGame;
        if (RestartButton) RestartButton.gameObject.SetActive(!isNet);
        if (LobbyButton)   LobbyButton.gameObject.SetActive(true);
        if (MenuButton)    MenuButton.gameObject.SetActive(false);
        LayoutGameOverButtons(isNet);
        if (_gameOverAutoLobbyRoutine != null)
            StopCoroutine(_gameOverAutoLobbyRoutine);
        SetGameOverCountdownText("30秒后自动返回大厅");
        SetGameOverCountdownProgress(1f, win);
        _gameOverAutoLobbyRoutine = StartCoroutine(AutoReturnToLobbyCountdown(30, win));
        // 胜利烟花
        if (win) StartCoroutine(SpawnVictoryFireworks());
    }

    void PrepareGameOverOverlay()
    {
        if (UnitInfoPanel != null) UnitInfoPanel.SetActive(false);
        if (BuildingPanel != null) BuildingPanel.SetActive(false);
        if (BuildMenuPanel != null) BuildMenuPanel.SetActive(false);
        if (TechPanel != null) TechPanel.SetActive(false);
        if (PausePanel != null) PausePanel.SetActive(false);
        if (AlertText != null) AlertText.gameObject.SetActive(false);
        if (MinimapImage != null) MinimapImage.gameObject.SetActive(false);
        if (MinimapCameraRect != null) MinimapCameraRect.gameObject.SetActive(false);
        bBuildMenuOpen = false;
        bPaused = false;
        Time.timeScale = 1f;
    }

    GameManager.BattleReportData BuildFallbackBattleReportData(int kills, int seconds)
    {
        var ps = RTSPlayerState.Instance;
        int gold = ps != null ? ps.Gold : 0;
        int popUsed = ps != null ? ps.PopUsed : 0;
        int popCap = ps != null ? ps.PopCap : 0;
        int powerUsed = ps != null ? ps.PowerUsed : 0;
        int powerCap = ps != null ? ps.PowerCap : 0;
        int m = seconds / 60, s = seconds % 60;

        var sync = GameNetworkSync.Instance;
        string teamLine = sync != null && sync.IsNetworkGame
            ? $"{(sync.IsHost ? "我方HOST" : "我方GUEST")}  VS  对手"
            : "我方部队  VS  敌方";

        return new GameManager.BattleReportData
        {
            TeamLine = teamLine,
            PlayerHeader = sync != null && sync.IsNetworkGame ? (sync.IsHost ? "我方 HOST" : "我方 GUEST") : "我方",
            EnemyHeader = sync != null && sync.IsNetworkGame ? "对手" : "敌方",
            DurationText = $"{m:00}:{s:00}",
            Rows = new GameManager.BattleReportRow[]
            {
                new GameManager.BattleReportRow("战果", $"击杀{kills}", "--"),
                new GameManager.BattleReportRow("兵力", "--", "--"),
                new GameManager.BattleReportRow("生产", "--", "--"),
                new GameManager.BattleReportRow("人口", $"{popUsed}/{popCap}", "--"),
                new GameManager.BattleReportRow("伤害", "--", "--"),
                new GameManager.BattleReportRow("经济", $"金{gold:N0}", "--"),
                new GameManager.BattleReportRow("电力", $"{powerUsed}/{powerCap}", "--"),
                new GameManager.BattleReportRow("主基", "--", "--")
            }
        };
    }

    string BuildBattleReportText(GameManager.BattleReportData report)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(report.TeamLine).Append("    用时 ").Append(report.DurationText);
        if (report.Rows != null)
        {
            for (int i = 0; i < report.Rows.Length; i++)
            {
                var row = report.Rows[i];
                sb.Append('\n')
                    .Append(row.Label).Append("：")
                    .Append(report.PlayerHeader).Append(" ").Append(row.PlayerValue)
                    .Append(" | ")
                    .Append(report.EnemyHeader).Append(" ").Append(row.EnemyValue);
            }
        }
        return sb.ToString();
    }

    void UpdateGameOverSummaryCards(int kills, int seconds)
    {
        if (GameOverPanel == null) return;
        var card = GameOverPanel.transform.Find("GoCard");
        if (card == null) return;

        var ps = RTSPlayerState.Instance;
        string goldText = ps != null ? ps.Gold.ToString("N0") : "0";
        string powerText = ps != null ? $"{ps.PowerUsed}/{ps.PowerCap}" : "0/0";
        string baseText = GameManager.Instance != null ? GameManager.Instance.GetPlayerBaseSummaryText() : "--";
        int m = seconds / 60, s = seconds % 60;

        SetSummaryCard(card, 0, "金币", goldText, new Color(0.98f, 0.78f, 0.22f));
        SetSummaryCard(card, 1, "电力", powerText, new Color(0.42f, 0.94f, 0.76f));
        SetSummaryCard(card, 2, "主基地", baseText, new Color(0.54f, 0.76f, 1f));
        SetSummaryCard(card, 3, "战果", $"{kills} 杀  {m:00}:{s:00}", new Color(1f, 0.50f, 0.42f));
    }

    void SetSummaryCard(Transform card, int index, string label, string value, Color accentColor)
    {
        var itemName = $"GoSummary{index}";
        var item = card.Find(itemName);
        if (item == null)
        {
            var go = new GameObject(itemName);
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            item = go.transform;

            var accent = new GameObject("Accent");
            accent.transform.SetParent(item, false);
            accent.AddComponent<RectTransform>();
            accent.AddComponent<Image>();

            CreateGameOverText(item, "Label", 14, TextAnchor.UpperLeft, new Color(0.64f, 0.72f, 0.76f));
            CreateGameOverText(item, "Value", 19, TextAnchor.LowerLeft, Color.white);
        }

        var rt = item.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.675f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 66f);
            rt.anchoredPosition = new Vector2(-240f + index * 160f, 0f);
        }
        var image = item.GetComponent<Image>();
        if (image != null) image.color = new Color(0.030f, 0.048f, 0.050f, 0.98f);
        var outline = item.GetComponent<Outline>() ?? item.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        var accentRt = item.Find("Accent")?.GetComponent<RectTransform>();
        if (accentRt != null)
        {
            accentRt.anchorMin = new Vector2(0f, 1f);
            accentRt.anchorMax = Vector2.one;
            accentRt.offsetMin = new Vector2(0f, -3f);
            accentRt.offsetMax = Vector2.zero;
        }
        var accentImg = item.Find("Accent")?.GetComponent<Image>();
        if (accentImg != null) accentImg.color = accentColor;

        var labelText = item.Find("Label")?.GetComponent<Text>();
        if (labelText != null)
        {
            var labelRt = labelText.rectTransform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.64f);
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = new Vector2(126f, 22f);
            labelText.text = label;
            labelText.color = new Color(0.64f, 0.72f, 0.76f);
            labelText.alignment = TextAnchor.MiddleLeft;
        }

        var valueText = item.Find("Value")?.GetComponent<Text>();
        if (valueText != null)
        {
            var valueRt = valueText.rectTransform;
            valueRt.anchorMin = valueRt.anchorMax = new Vector2(0.5f, 0.30f);
            valueRt.pivot = new Vector2(0.5f, 0.5f);
            valueRt.anchoredPosition = Vector2.zero;
            valueRt.sizeDelta = new Vector2(126f, 26f);
            valueText.text = value;
            valueText.color = accentColor;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 13;
            valueText.resizeTextMaxSize = 20;
        }
    }

    Text CreateGameOverText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = name == "Label" ? new Vector2(12f, 28f) : new Vector2(12f, 8f);
        rt.offsetMax = name == "Label" ? new Vector2(-10f, -8f) : new Vector2(-10f, -26f);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    void EnsureGameOverPanelLayout()
    {
        if (GameOverPanel == null) return;

        var panelRt = GameOverPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = Vector2.zero;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
        }
        var panelBg = GameOverPanel.GetComponent<Image>();
        if (panelBg != null) panelBg.color = new Color(0f, 0f, 0f, 0.78f);

        Transform card = GameOverPanel.transform.Find("GoCard");
        if (card == null)
        {
            var oldChildren = new List<Transform>();
            for (int i = 0; i < GameOverPanel.transform.childCount; i++)
                oldChildren.Add(GameOverPanel.transform.GetChild(i));

            var cardGo = new GameObject("GoCard");
            cardGo.transform.SetParent(GameOverPanel.transform, false);
            cardGo.AddComponent<RectTransform>();
            cardGo.AddComponent<Image>();
            card = cardGo.transform;

            foreach (var child in oldChildren)
                if (child != null && child != card) child.SetParent(card, false);
        }

        var cardRt = card.GetComponent<RectTransform>();
        if (cardRt != null)
        {
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(720f, 560f);
        }
        var cardImage = card.GetComponent<Image>();
        if (cardImage != null) cardImage.color = new Color(0.045f, 0.058f, 0.060f, 0.98f);
        var cardOutline = card.GetComponent<Outline>() ?? card.gameObject.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.94f, 0.72f, 0.22f, 0.70f);
        cardOutline.effectDistance = new Vector2(2f, -2f);
        var cardShadow = GetOrAddUiShadow(card.gameObject);
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
        cardShadow.effectDistance = new Vector2(5f, -5f);

        EnsureCardStrip(card, "GoTopStrip", true);
        EnsureCardStrip(card, "GoBottomStrip", false);
        Transform reportPanel = EnsureGameOverReportPanel(card);
        EnsureGameOverResultBadge(card);
        EnsureGameOverButtonDeck(card);

        if (GameOverText != null)
        {
            if (GameOverText.transform.parent == GameOverPanel.transform)
                GameOverText.transform.SetParent(card, false);
            var rt = GameOverText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.855f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(620f, 76f);
            GameOverText.fontSize = 54;
            GameOverText.alignment = TextAnchor.MiddleCenter;
            GameOverText.horizontalOverflow = HorizontalWrapMode.Overflow;
            GameOverText.verticalOverflow = VerticalWrapMode.Overflow;
            var titleOutline = GameOverText.GetComponent<Outline>() ?? GameOverText.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            titleOutline.effectDistance = new Vector2(2f, -2f);
            var titleShadow = GetOrAddUiShadow(GameOverText.gameObject);
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            titleShadow.effectDistance = new Vector2(3f, -3f);
        }

        if (GameOverStatsText == null)
        {
            GameOverStatsText = card.Find("GameOverStatsText")?.GetComponent<Text>();
            if (GameOverStatsText == null && reportPanel != null)
                GameOverStatsText = reportPanel.Find("GameOverStatsText")?.GetComponent<Text>();
            if (GameOverStatsText == null)
                GameOverStatsText = EnsureGameOverChildText(reportPanel != null ? reportPanel : card, "GameOverStatsText");
        }

        if (GameOverStatsText != null)
        {
            Transform statsParent = reportPanel != null ? reportPanel : card;
            if (GameOverStatsText.transform.parent != statsParent)
                GameOverStatsText.transform.SetParent(statsParent, false);
            var rt = GameOverStatsText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(20f, 14f);
            rt.offsetMax = new Vector2(-18f, -48f);
            GameOverStatsText.fontSize = 13;
            GameOverStatsText.alignment = TextAnchor.UpperLeft;
            GameOverStatsText.lineSpacing = 1.04f;
            GameOverStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            GameOverStatsText.verticalOverflow = VerticalWrapMode.Overflow;
            GameOverStatsText.color = new Color(0.84f, 0.93f, 1f);
            var statsShadow = GetOrAddUiShadow(GameOverStatsText.gameObject);
            statsShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            statsShadow.effectDistance = new Vector2(1f, -1f);
            GameOverStatsText.gameObject.SetActive(false);
        }
        if (reportPanel != null)
            EnsureGameOverBattleTable(reportPanel, 8);
        EnsureGameOverReportPanel(card);
        EnsureGameOverResultBadge(card);
        EnsureGameOverButtonDeck(card);
        EnsureGameOverCountdownText(card);
    }

    void PlayGameOverCardIntro()
    {
        if (GameOverPanel == null) return;
        var card = GameOverPanel.transform.Find("GoCard");
        if (card == null) return;
        if (_gameOverCardIntroRoutine != null)
            StopCoroutine(_gameOverCardIntroRoutine);
        _gameOverCardIntroRoutine = StartCoroutine(AnimateGameOverCardIntro(card));
    }

    System.Collections.IEnumerator AnimateGameOverCardIntro(Transform card)
    {
        if (card == null) yield break;
        float duration = 0.24f;
        float t = 0f;
        Vector3 from = Vector3.one * 0.94f;
        Vector3 to = Vector3.one;
        card.localScale = from;
        var image = card.GetComponent<Image>();
        Color baseColor = image != null ? image.color : Color.white;
        if (image != null) image.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / duration);
            float ease = 1f - Mathf.Pow(1f - r, 3f);
            card.localScale = Vector3.LerpUnclamped(from, to, ease);
            if (image != null)
                image.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.15f, baseColor.a, ease));
            yield return null;
        }
        card.localScale = to;
        if (image != null) image.color = baseColor;
        _gameOverCardIntroRoutine = null;
    }

    Transform EnsureGameOverResultBadge(Transform card)
    {
        if (card == null) return null;
        var badge = card.Find("GoResultBadge");
        if (badge == null)
        {
            var go = new GameObject("GoResultBadge");
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            badge = go.transform;
        }

        var rt = badge.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.855f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-260f, 0f);
            rt.sizeDelta = new Vector2(56f, 56f);
        }
        var image = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
        if (image != null && image.color.a <= 0f)
            image.color = new Color(0.22f, 0.18f, 0.05f, 0.94f);
        var outline = badge.GetComponent<Outline>() ?? badge.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var shadow = GetOrAddUiShadow(badge.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.68f);
        shadow.effectDistance = new Vector2(2f, -2f);

        var markGo = badge.Find("Mark");
        if (markGo == null)
        {
            var go = new GameObject("Mark");
            go.transform.SetParent(badge, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Text>();
            markGo = go.transform;
        }

        var mark = markGo.GetComponent<Text>();
        if (mark != null)
        {
            var markRt = mark.rectTransform;
            markRt.anchorMin = Vector2.zero;
            markRt.anchorMax = Vector2.one;
            markRt.offsetMin = Vector2.zero;
            markRt.offsetMax = Vector2.zero;
            mark.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (mark.font == null) mark.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            mark.fontSize = 30;
            mark.fontStyle = FontStyle.Bold;
            mark.alignment = TextAnchor.MiddleCenter;
            mark.raycastTarget = false;
        }
        return badge;
    }

    void SetGameOverResultBadge(bool win)
    {
        if (GameOverPanel == null) return;
        var card = GameOverPanel.transform.Find("GoCard");
        var badge = EnsureGameOverResultBadge(card);
        if (badge == null) return;

        Color accent = win ? new Color(0.95f, 0.76f, 0.20f, 0.96f) : new Color(0.94f, 0.30f, 0.24f, 0.96f);
        var image = badge.GetComponent<Image>();
        if (image != null) image.color = win ? new Color(0.22f, 0.18f, 0.05f, 0.94f) : new Color(0.22f, 0.06f, 0.055f, 0.94f);
        var outline = badge.GetComponent<Outline>() ?? badge.gameObject.AddComponent<Outline>();
        outline.effectColor = accent;
        var mark = badge.Find("Mark")?.GetComponent<Text>();
        if (mark != null)
        {
            mark.text = win ? "V" : "X";
            mark.color = accent;
        }
    }

    Transform EnsureGameOverButtonDeck(Transform card)
    {
        if (card == null) return null;
        var deck = card.Find("GoButtonDeck");
        if (deck == null)
        {
            var go = new GameObject("GoButtonDeck");
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            deck = go.transform;
        }

        var rt = deck.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.105f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(430f, 70f);
        }
        var image = deck.GetComponent<Image>();
        if (image != null) image.color = new Color(0.012f, 0.018f, 0.020f, 0.48f);
        var outline = deck.GetComponent<Outline>() ?? deck.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.60f, 0.60f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        if (RestartButton != null && RestartButton.transform.parent == card)
            deck.SetSiblingIndex(Mathf.Max(0, RestartButton.transform.GetSiblingIndex() - 1));
        else if (LobbyButton != null && LobbyButton.transform.parent == card)
            deck.SetSiblingIndex(Mathf.Max(0, LobbyButton.transform.GetSiblingIndex() - 1));
        return deck;
    }

    Transform EnsureGameOverReportPanel(Transform card)
    {
        if (card == null) return null;
        var report = card.Find("GoReportPanel");
        if (report == null)
        {
            var go = new GameObject("GoReportPanel");
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            report = go.transform;
        }

        var rt = report.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.410f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(640f, 225f);
        }
        var image = report.GetComponent<Image>();
        if (image != null) image.color = new Color(0.018f, 0.027f, 0.030f, 0.88f);
        var outline = report.GetComponent<Outline>() ?? report.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.34f, 0.58f, 0.58f, 0.48f);
        outline.effectDistance = new Vector2(1f, -1f);

        var title = EnsureGameOverChildText(report, "GoReportTitle");
        if (title != null)
        {
            title.text = "战斗报告";
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(1f, 0.86f, 0.42f);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(22f, -10f);
            titleRt.sizeDelta = new Vector2(180f, 26f);
        }

        EnsureGameOverReportBar(report, "GoReportRule", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -42f), new Vector2(-44f, 2f), new Color(0.94f, 0.72f, 0.22f, 0.35f));
        EnsureGameOverReportBar(report, "GoReportAccent", new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(4f, 0f), new Color(0.42f, 0.94f, 0.76f, 0.55f));

        if (GameOverStatsText != null && GameOverStatsText.transform.parent == card)
        {
            int statsIndex = GameOverStatsText.transform.GetSiblingIndex();
            report.SetSiblingIndex(Mathf.Max(0, statsIndex - 1));
        }
        return report;
    }

    void UpdateGameOverBattleTable(GameManager.BattleReportData report)
    {
        if (GameOverPanel == null) return;
        Transform card = GameOverPanel.transform.Find("GoCard");
        if (card == null) return;
        Transform reportPanel = EnsureGameOverReportPanel(card);
        if (reportPanel == null) return;

        var rows = report.Rows ?? new GameManager.BattleReportRow[0];
        Transform table = EnsureGameOverBattleTable(reportPanel, rows.Length);
        if (table == null) return;

        Text meta = EnsureGameOverChildText(reportPanel, "GoReportMeta");
        if (meta != null)
        {
            meta.text = $"{report.TeamLine}    用时 {report.DurationText}";
            meta.fontSize = 13;
            meta.fontStyle = FontStyle.Bold;
            meta.alignment = TextAnchor.MiddleRight;
            meta.color = new Color(0.72f, 0.86f, 0.92f, 0.95f);
            meta.horizontalOverflow = HorizontalWrapMode.Overflow;
            meta.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = meta.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-22f, -10f);
            rt.sizeDelta = new Vector2(390f, 26f);
        }

        SetGameOverTableCellText(table, "GoReportHeader", "PlayerValue",
            string.IsNullOrEmpty(report.PlayerHeader) ? "我方" : report.PlayerHeader);
        SetGameOverTableCellText(table, "GoReportHeader", "MetricLabel", "项目");
        SetGameOverTableCellText(table, "GoReportHeader", "EnemyValue",
            string.IsNullOrEmpty(report.EnemyHeader) ? "敌方" : report.EnemyHeader);

        for (int i = 0; i < rows.Length; i++)
        {
            Transform row = EnsureGameOverTableRow(table, i);
            if (row == null) continue;
            row.gameObject.SetActive(true);
            SetGameOverTableCellText(table, $"GoReportRow{i}", "PlayerValue", rows[i].PlayerValue);
            SetGameOverTableCellText(table, $"GoReportRow{i}", "MetricLabel", rows[i].Label);
            SetGameOverTableCellText(table, $"GoReportRow{i}", "EnemyValue", rows[i].EnemyValue);
        }

        for (int i = rows.Length; i < 12; i++)
        {
            Transform oldRow = table.Find($"GoReportRow{i}");
            if (oldRow != null) oldRow.gameObject.SetActive(false);
        }
    }

    Transform EnsureGameOverBattleTable(Transform reportPanel, int rowCount)
    {
        if (reportPanel == null) return null;
        Transform table = reportPanel.Find("GoReportTable");
        if (table == null)
        {
            var go = new GameObject("GoReportTable");
            go.transform.SetParent(reportPanel, false);
            go.AddComponent<RectTransform>();
            table = go.transform;
        }

        var rt = table.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -50f);
            rt.sizeDelta = new Vector2(604f, 165f);
        }

        Transform header = EnsureGameOverTableRow(table, -1);
        if (header != null)
        {
            header.name = "GoReportHeader";
            var headerImage = header.GetComponent<Image>();
            if (headerImage != null) headerImage.color = new Color(0.055f, 0.085f, 0.085f, 0.95f);
            var headerRt = header.GetComponent<RectTransform>();
            if (headerRt != null)
            {
                headerRt.anchorMin = headerRt.anchorMax = new Vector2(0.5f, 1f);
                headerRt.pivot = new Vector2(0.5f, 1f);
                headerRt.anchoredPosition = Vector2.zero;
                headerRt.sizeDelta = new Vector2(604f, 21f);
            }
        }

        int safeRows = Mathf.Max(0, rowCount);
        for (int i = 0; i < safeRows; i++)
            EnsureGameOverTableRow(table, i);
        EnsureGameOverTableDivider(table, "GoReportTableDividerLeft", -46f);
        EnsureGameOverTableDivider(table, "GoReportTableDividerRight", 46f);
        return table;
    }

    Transform EnsureGameOverTableRow(Transform table, int index)
    {
        if (table == null) return null;
        string name = index < 0 ? "GoReportHeader" : $"GoReportRow{index}";
        Transform row = table.Find(name);
        if (row == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(table, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            row = go.transform;
            EnsureGameOverChildText(row, "PlayerValue");
            EnsureGameOverChildText(row, "MetricLabel");
            EnsureGameOverChildText(row, "EnemyValue");
        }

        var rt = row.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = index < 0 ? Vector2.zero : new Vector2(0f, -23f - index * 17.5f);
            rt.sizeDelta = index < 0 ? new Vector2(604f, 21f) : new Vector2(604f, 17f);
        }

        var image = row.GetComponent<Image>();
        if (image != null && index >= 0)
            image.color = (index % 2 == 0)
                ? new Color(0.030f, 0.046f, 0.048f, 0.72f)
                : new Color(0.022f, 0.034f, 0.036f, 0.50f);

        bool header = index < 0;
        LayoutGameOverTableCell(row, "PlayerValue", -172f, 250f, TextAnchor.MiddleLeft,
            header ? 13 : 12, header ? new Color(0.42f, 0.94f, 0.76f) : new Color(0.78f, 0.96f, 0.90f), header);
        LayoutGameOverTableCell(row, "MetricLabel", 0f, 82f, TextAnchor.MiddleCenter,
            header ? 12 : 12, header ? new Color(1f, 0.86f, 0.42f) : new Color(0.68f, 0.75f, 0.78f), header);
        LayoutGameOverTableCell(row, "EnemyValue", 172f, 250f, TextAnchor.MiddleRight,
            header ? 13 : 12, header ? new Color(1f, 0.50f, 0.42f) : new Color(1f, 0.78f, 0.74f), header);
        return row;
    }

    void LayoutGameOverTableCell(Transform row, string name, float x, float width, TextAnchor alignment,
        int fontSize, Color color, bool bold)
    {
        Text text = EnsureGameOverChildText(row, name);
        if (text == null) return;

        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(width, 17f);
        text.fontSize = fontSize;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 9;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
    }

    void SetGameOverTableCellText(Transform table, string rowName, string cellName, string value)
    {
        Text text = table?.Find(rowName)?.Find(cellName)?.GetComponent<Text>();
        if (text != null)
            text.text = string.IsNullOrEmpty(value) ? "--" : value;
    }

    void EnsureGameOverTableDivider(Transform table, string name, float x)
    {
        if (table == null) return;
        Transform divider = table.Find(name);
        if (divider == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(table, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            divider = go.transform;
        }

        var rt = divider.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -1f);
            rt.sizeDelta = new Vector2(1f, 160f);
        }
        var image = divider.GetComponent<Image>();
        if (image != null) image.color = new Color(0.34f, 0.58f, 0.58f, 0.32f);
        divider.SetAsLastSibling();
    }

    void EnsureGameOverReportBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var bar = parent.Find(name);
        if (bar == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            bar = go.transform;
        }

        var rt = bar.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
        }
        var image = bar.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    Text EnsureGameOverCountdownText(Transform card)
    {
        if (card == null) return null;
        var text = EnsureGameOverChildText(card, "GoCountdownText");
        if (text == null) return null;

        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.195f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(580f, 28f);
        if (string.IsNullOrEmpty(text.text))
            text.text = "30秒后自动返回大厅";
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.72f, 0.86f, 0.92f, 0.95f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = GetOrAddUiShadow(text.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.76f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        EnsureGameOverCountdownBar(card);
        return text;
    }

    Transform EnsureGameOverCountdownBar(Transform card)
    {
        if (card == null) return null;
        var bar = card.Find("GoCountdownBar");
        if (bar == null)
        {
            var go = new GameObject("GoCountdownBar");
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            bar = go.transform;

            var fillGo = new GameObject("GoCountdownFill");
            fillGo.transform.SetParent(bar, false);
            fillGo.AddComponent<RectTransform>();
            fillGo.AddComponent<Image>();
        }

        var rt = bar.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.165f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(440f, 6f);
        }
        var image = bar.GetComponent<Image>();
        if (image != null) image.color = new Color(0.010f, 0.016f, 0.018f, 0.90f);

        var fill = bar.Find("GoCountdownFill");
        if (fill != null)
        {
            var fillRt = fill.GetComponent<RectTransform>();
            if (fillRt != null)
            {
                fillRt.anchorMin = new Vector2(0f, 0f);
                fillRt.anchorMax = new Vector2(1f, 1f);
                fillRt.pivot = new Vector2(0f, 0.5f);
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
            }
            var fillImage = fill.GetComponent<Image>();
            if (fillImage != null && fillImage.color.a <= 0f)
                fillImage.color = new Color(0.42f, 0.94f, 0.76f, 0.95f);
        }
        return bar;
    }

    void SetGameOverCountdownProgress(float ratio, bool win)
    {
        if (GameOverPanel == null) return;
        var card = GameOverPanel.transform.Find("GoCard");
        var bar = EnsureGameOverCountdownBar(card);
        var fill = bar != null ? bar.Find("GoCountdownFill") : null;
        if (fill == null) return;

        float clamped = Mathf.Clamp01(ratio);
        var rt = fill.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(clamped, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        var image = fill.GetComponent<Image>();
        if (image != null)
        {
            Color accent = win ? new Color(0.95f, 0.76f, 0.20f, 0.95f) : new Color(0.94f, 0.30f, 0.24f, 0.95f);
            image.color = Color.Lerp(new Color(0.42f, 0.94f, 0.76f, 0.95f), accent, 1f - clamped);
        }
    }

    Text EnsureGameOverChildText(Transform parent, string name)
    {
        if (parent == null) return null;
        var child = parent.Find(name);
        Text text = child != null ? child.GetComponent<Text>() : null;
        if (text == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            text = go.AddComponent<Text>();
        }

        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.raycastTarget = false;
        return text;
    }

    void SetGameOverCountdownText(string text)
    {
        if (GameOverPanel == null) return;
        var card = GameOverPanel.transform.Find("GoCard");
        if (card == null) return;
        var countdown = card.Find("GoCountdownText")?.GetComponent<Text>() ?? EnsureGameOverCountdownText(card);
        if (countdown != null)
            countdown.text = text;
    }

    void EnsureCardStrip(Transform card, string name, bool top)
    {
        if (card == null) return;
        var strip = card.Find(name);
        if (strip == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(card, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            strip = go.transform;
        }

        var rt = strip.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
            rt.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
            rt.offsetMin = top ? new Vector2(0f, -4f) : Vector2.zero;
            rt.offsetMax = top ? Vector2.zero : new Vector2(0f, 4f);
        }
        var image = strip.GetComponent<Image>();
        if (image != null) image.color = new Color(0.92f, 0.70f, 0.18f, 0.96f);
    }

    void LayoutGameOverButtons(bool isNet)
    {
        // 结算面板只保留两个明确动作：单机可“再来一局”，所有模式都能“返回大厅”。
        // 运行时强制改文字，避免旧场景序列化里两个按钮都显示成“返回大厅”。
        Transform card = GameOverPanel != null ? GameOverPanel.transform.Find("GoCard") : null;
        if (RestartButton != null)
        {
            SetButtonText(RestartButton, "再来一局");
            if (card != null && RestartButton.transform.parent == GameOverPanel.transform)
                RestartButton.transform.SetParent(card, false);
            var rt = RestartButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.105f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-110f, 0f);
                rt.sizeDelta = new Vector2(180f, 50f);
            }
            StyleGameOverButton(RestartButton, new Color(0.12f, 0.48f, 0.22f, 0.98f),
                new Color(0.48f, 1f, 0.58f, 0.92f));
        }

        if (LobbyButton != null)
        {
            SetButtonText(LobbyButton, "返回大厅");
            if (card != null && LobbyButton.transform.parent == GameOverPanel.transform)
                LobbyButton.transform.SetParent(card, false);
            var rt = LobbyButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.105f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(isNet ? 0f : 110f, 0f);
                rt.sizeDelta = new Vector2(180f, 50f);
            }
            StyleGameOverButton(LobbyButton, new Color(0.16f, 0.33f, 0.66f, 0.98f),
                new Color(0.58f, 0.78f, 1f, 0.92f));
        }
    }

    void StyleGameOverButton(Button button, Color baseColor, Color accentColor)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
            button.targetGraphic = image;
        }

        var colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.28f);
        colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.12f);
        colors.disabledColor = new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, 0.70f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.10f;
        button.colors = colors;

        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        var shadow = GetOrAddUiShadow(button.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);

        var line = button.transform.Find("GoButtonTopLine") ?? button.transform.Find("TopLine");
        if (line == null)
        {
            var go = new GameObject("GoButtonTopLine");
            go.transform.SetParent(button.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
            line = go.transform;
        }
        else
        {
            line.name = "GoButtonTopLine";
        }
        var lineRt = line.GetComponent<RectTransform>();
        if (lineRt != null)
        {
            lineRt.anchorMin = new Vector2(0.04f, 1f);
            lineRt.anchorMax = new Vector2(0.96f, 1f);
            lineRt.offsetMin = new Vector2(0f, -3f);
            lineRt.offsetMax = Vector2.zero;
        }
        var lineImage = line.GetComponent<Image>();
        if (lineImage != null) lineImage.color = Color.Lerp(accentColor, Color.white, 0.18f);

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.fontSize = 19;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.96f, 0.98f, 1f);
            label.raycastTarget = false;
            var labelShadow = GetOrAddUiShadow(label.gameObject);
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            labelShadow.effectDistance = new Vector2(1.2f, -1.2f);
        }
    }

    static void SetButtonText(Button button, string text)
    {
        var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null) label.text = text;
    }

    static void SetCloseButtonIcon(Button button)
    {
        var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label == null) return;

        label.text = "×";
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>结算面板出现时全屏闪光过渡（胜利金色、失败暗红），叠在 Canvas 最顶层 0.45 秒。</summary>
    void SpawnGameOverFlash(bool win)
    {
        var canvas = GetHudCanvas();
        if (canvas == null) return;
        var go = new GameObject("_GameOverFlash");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        Color flashCol = win
            ? new Color(1f, 0.92f, 0.55f, 0.85f)   // 胜利金色
            : new Color(0.85f, 0.18f, 0.16f, 0.80f); // 失败暗红
        img.color = flashCol;
        StartCoroutine(FadeOutAndDestroy(img, 0.45f));
    }

    System.Collections.IEnumerator FadeOutAndDestroy(Image img, float duration)
    {
        if (img == null) yield break;
        Color c = img.color;
        float t = 0f;
        while (t < duration && img != null)
        {
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / duration);
            // ease-out cubic
            float ease = 1f - Mathf.Pow(1f - r, 3f);
            c.a = Mathf.Lerp(img.color.a, 0f, ease);
            img.color = new Color(c.r, c.g, c.b, c.a);
            yield return null;
        }
        if (img != null) Destroy(img.gameObject);
    }

    System.Collections.IEnumerator AnimateGameOverTitle(Text title, bool win)
    {
        if (title == null) yield break;
        var rt = title.rectTransform;
        Vector3 baseScale = Vector3.one;
        // 0~0.45 秒：从 0.2 弹性放大到 1.15 再回到 1.0
        float dur = 0.55f, t = 0f;
        Color baseColor = title.color;
        title.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / dur);
            float ease = 1f - Mathf.Pow(1f - r, 3f);
            float scale;
            if (r < 0.7f)
                scale = Mathf.Lerp(0.2f, 1.18f, r / 0.7f);
            else
                scale = Mathf.Lerp(1.18f, 1.0f, (r - 0.7f) / 0.3f);
            rt.localScale = baseScale * scale;
            title.color = new Color(baseColor.r, baseColor.g, baseColor.b, ease);
            yield return null;
        }
        rt.localScale = baseScale;
        title.color = baseColor;
        // 胜利时持续微微脉动
        if (win)
        {
            while (GameOverPanel != null && GameOverPanel.activeSelf)
            {
                float k = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 3.2f);
                rt.localScale = baseScale * k;
                yield return null;
            }
        }
    }

    System.Collections.IEnumerator AnimateStatsRoll(Text text, string finalReport)
    {
        if (text == null) yield break;
        text.text = "";
        yield return new WaitForSecondsRealtime(0.35f); // 等标题先弹出
        float dur = 0.65f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(t / dur);
            int chars = Mathf.RoundToInt(Mathf.Lerp(0, finalReport.Length, 1f - Mathf.Pow(1f - r, 2f)));
            text.text = finalReport.Substring(0, Mathf.Clamp(chars, 0, finalReport.Length));
            yield return null;
        }
        text.text = finalReport;
    }

    static Shadow GetOrAddUiShadow(GameObject go)
    {
        if (go == null) return null;
        var shadows = go.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            if (!(shadows[i] is Outline))
                return shadows[i];
        }
        return go.AddComponent<Shadow>();
    }

    System.Collections.IEnumerator AutoReturnToLobbyCountdown(int seconds, bool win)
    {
        yield return new WaitForSecondsRealtime(1.25f);
        for (int left = seconds; left > 0; left--)
        {
            if (GameOverPanel == null || !GameOverPanel.activeSelf)
                yield break;
            SetGameOverCountdownText($"{left}秒后自动返回大厅");
            SetGameOverCountdownProgress((float)left / Mathf.Max(1, seconds), win);
            yield return new WaitForSecondsRealtime(1f);
        }

        SetGameOverCountdownText("正在返回大厅...");
        SetGameOverCountdownProgress(0f, win);
        if (GameOverPanel != null && GameOverPanel.activeSelf)
            GameManager.Instance?.ReturnToLobby();
    }

    System.Collections.IEnumerator SpawnVictoryFireworks()
    {
        if (GameOverPanel == null) yield break;
        var canvas = GameOverPanel.GetComponentInParent<Canvas>();
        if (canvas == null) yield break;
        // 持续 3 秒，每 0.3 秒一发
        float endTime = Time.unscaledTime + 3f;
        while (Time.unscaledTime < endTime && GameOverPanel != null && GameOverPanel.activeSelf)
        {
            SpawnFireworkBurst(canvas);
            yield return new WaitForSecondsRealtime(0.32f);
        }
    }

    void SpawnFireworkBurst(Canvas canvas)
    {
        // 随机屏幕位置（避开中心标题区域）
        var crt = canvas.GetComponent<RectTransform>();
        Rect r = crt.rect;
        float x = Random.Range(-r.width * 0.45f, r.width * 0.45f);
        float y = Random.Range(r.height * 0.05f, r.height * 0.45f);
        // 颜色：金色/橙色/绿色/蓝色 随机
        Color[] colors = {
            new Color(1f, 0.85f, 0.20f),
            new Color(1f, 0.55f, 0.20f),
            new Color(0.45f, 1f, 0.55f),
            new Color(0.40f, 0.78f, 1f),
            new Color(1f, 0.50f, 0.85f),
        };
        Color baseC = colors[Random.Range(0, colors.Length)];
        // 径向发射 16 个火花
        int count = 16;
        for (int i = 0; i < count; i++)
        {
            float ang = (360f / count) * i + Random.Range(-6f, 6f);
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            var go = new GameObject("Spark");
            go.transform.SetParent(canvas.transform, false);
            var srt = go.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(x, y);
            srt.sizeDelta = new Vector2(10f, 10f);
            var img = go.AddComponent<Image>();
            img.color = baseC;
            img.raycastTarget = false;
            go.AddComponent<_FireworkSpark>().Init(dir * Random.Range(180f, 280f), 1.0f);
        }
    }

    void OnSurrender()
    {
        if (IsOnlineMatch())
        {
            HidePauseMenu();
            return;
        }
        TogglePauseMenu();
        GameManager.Instance?.ReturnToLobby();
    }

    public void TogglePauseMenu()
    {
        if (_settingsOpen)
        {
            SetGameSettingsOpen(false);
            return;
        }
        if (IsOnlineMatch())
        {
            HidePauseMenu();
            return;
        }
        bPaused = !bPaused;
        if (PausePanel) PausePanel.SetActive(bPaused);
        EnsureBattleTimeRunning();
    }

    bool IsOnlineMatch()
    {
        return GameNetworkSync.Instance != null && GameNetworkSync.Instance.IsNetworkGame;
    }

    void EnsureBattleTimeRunning()
    {
        if (!Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = 1f;
    }

    void HidePauseMenu()
    {
        bPaused = false;
        if (PausePanel) PausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    void HideAll()
    {
        if (UnitInfoPanel)  UnitInfoPanel.SetActive(false);
        if (BuildingPanel)  BuildingPanel.SetActive(false);
        if (GameOverPanel)  GameOverPanel.SetActive(false);
        if (PausePanel)     PausePanel.SetActive(false);
        if (GameSettingsPanel) GameSettingsPanel.SetActive(false);
        if (AlertText)      AlertText.gameObject.SetActive(false);
        if (BuildMenuPanel) BuildMenuPanel.SetActive(false);
        if (TechPanel)      TechPanel.SetActive(false);
        if (BuildingUpgradePanel) BuildingUpgradePanel.SetActive(false);
        HideRightClickCommandMenu();
        _pendingUpgradeBuilding = null;
        _settingsOpen = false;
        _techPanelOpen = false;
    }

    // 判断结算面板是否展示中
    public bool IsGameOverVisible() =>
        GameOverPanel != null && GameOverPanel.activeSelf;

    // 居中警报（基地被攻等）
    public void ShowAlert(string msg)
    {
        if (AlertText == null) return;
        AlertText.text = msg;
        AlertText.gameObject.SetActive(true);
        alertTimer = AlertDuration;
    }

    static void SetHPBarColor(Slider bar, float ratio)
    {
        if (bar == null) return;
        Color c = ratio > 0.55f
            ? Color.Lerp(new Color(0.85f, 0.82f, 0.1f), new Color(0.15f, 0.85f, 0.35f), (ratio - 0.55f) / 0.45f)
            : Color.Lerp(new Color(0.92f, 0.18f, 0.18f), new Color(0.85f, 0.82f, 0.1f), ratio / 0.55f);
        var fillImg = bar.fillRect?.GetComponent<Image>();
        if (fillImg) fillImg.color = c;
    }

    static string GetProductionUnitDisplayName(RTSBuilding building, int idx, RTSUnit unit)
    {
        if (building is Barracks && unit is InfantryArtillery)
            return "炮兵";
        if (building is Barracks && unit is InfantryFlamethrower)
            return "喷火兵";
        if (building is TankFactory && unit is Artillery)
            return "火炮";
        if (building is TankFactory && unit is Flamethrower)
            return "喷火车";
        return GetUnitDisplayNameStatic(unit);
    }

    // 按类型查表，避免依赖 prefab 序列化的 DisplayName 字段
    public static string GetUnitDisplayNameStatic(RTSUnit unit)
    {
        if (unit == null)
            return string.Empty;
        if (unit is InfantryArtillery) return "炮兵";
        if (unit is InfantryFlamethrower) return "喷火兵";
        if (unit is Artillery && !string.IsNullOrEmpty(unit.DisplayName) && unit.DisplayName != "单位")
            return unit.DisplayName;
        if (unit is Flamethrower && !string.IsNullOrEmpty(unit.DisplayName) && unit.DisplayName != "单位")
            return unit.DisplayName;
        if (unit is Infantry)     return "步兵";
        if (unit is Artillery)    return "火炮";
        if (unit is AntiAirGun)   return "防空炮";
        if (unit is LightTank)    return "一级小坦克";
        if (unit is MediumTank)   return "二级中坦克";
        if (unit is HeavyTank)    return "三级重坦克";
        if (unit is Tank)         return "坦克";
        if (unit is Flamethrower) return "喷火车";
        if (unit is Fighter)      return "战斗机";
        if (unit is Bomber)       return "轰炸机";
        if (unit is ScoutPlane)   return "侦察机";
        if (unit is PatrolBoat)   return "巡逻艇";
        if (unit is DestroyerShip)return "驱逐舰";
        if (unit is TransportShip)return "\u6218\u5217\u8230";
        // 兜底：Awake 里设置的值（运行时实例有效）
        return string.IsNullOrEmpty(unit.DisplayName) ? unit.GetType().Name : unit.DisplayName;
    }
}

public class BattleChatVoicePressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public RTSHUD Owner;
    private bool _pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        Owner?.BeginVoiceRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressed) return;
        _pressed = false;
        Owner?.EndVoiceRecording();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_pressed) return;
        _pressed = false;
        Owner?.EndVoiceRecording();
    }
}

public class RightClickCommandButton : MonoBehaviour
{
    public string Command;
}
