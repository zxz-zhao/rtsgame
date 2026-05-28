using UnityEngine;
using UnityEngine.UI;
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

    [Header("暂停菜单")]
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
    public GameObject TankFactoryPrefab;
    public GameObject TurretPrefab;
    public GameObject GoldMinePrefab;
    public GameObject PowerPlantPrefab;

    [Header("建造面板（移动端）")]
    public GameObject BuildMenuPanel;     // 折叠/展开的建造菜单
    public Button     BuildMenuToggle;    // 底部建造按钮（展开/收起）
    public Button[]   BuildButtons;       // 6个建造按钮：兵工厂/飞机厂/特需厂/炮塔/金矿/电厂
    private bool bBuildMenuOpen = false;
    private int _activeBuildCategory = 0;
    private Button[] _buildCategoryButtons;
    private Text _buildEmptyText;
    private static readonly string[] BuildCategoryNames = { "建筑", "战斗", "经济", "海上", "空军", "陆地" };

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
    private float _baseUnderAttackTimer = 0f;
    private int   _cachedGoldRate = 0;
    private float _goldRateTimer = 0f;
    private Coroutine _gameOverAutoLobbyRoutine;
    private Coroutine _gameOverCardIntroRoutine;
    private bool _settingsOpen = false;
    private bool _settingsPausedTime = false;
    private float _settingsPreviousTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        playerState = RTSPlayerState.Instance;
        // 编辑器未赋值时从 Resources 加载建筑Prefab（保证快捷键和按钮可用）
        if (BarracksPrefab    == null) BarracksPrefab    = Resources.Load<GameObject>("Prefabs/Barracks_P");
        if (AirFactoryPrefab  == null) AirFactoryPrefab  = Resources.Load<GameObject>("Prefabs/AirFactory_P");
        if (TankFactoryPrefab == null) TankFactoryPrefab = Resources.Load<GameObject>("Prefabs/TankFactory_P");
        if (TurretPrefab      == null) TurretPrefab      = Resources.Load<GameObject>("Prefabs/Turret_P");
        if (GoldMinePrefab    == null) GoldMinePrefab    = Resources.Load<GameObject>("Prefabs/GoldMine_P");
        if (PowerPlantPrefab  == null) PowerPlantPrefab  = Resources.Load<GameObject>("Prefabs/PowerPlant_P");
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
        BuildMenuToggle?.onClick.AddListener(() => { _UiClickAudio.PlayClick(); ToggleBuildMenu(); });
        WireBuildButtons();
        // 二战风 RTS 没有主动技能：永久隐藏旧场景里残留的 SkillButton（不绑定 onClick，不创建冷却圆环）
        if (SkillButton != null) SkillButton.gameObject.SetActive(false);
        HideAll();
        RuntimeUIPolish();
        RebuildBattleHudLayout();
        // 屏幕边缘红光 vignette
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas != null) _vignette = _HudVignettePulse.CreateOnCanvas(canvas);
        // 战场环境背景音乐（程序化合成）
        _BattleAmbientMusic.CreateOnScene();
    }

    /// <summary>战场 HUD 完整重新布局：顶部指挥官栏 + 左下地图/建造/科技 + 底部路线。</summary>
    void RebuildBattleHudLayout()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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
                rt.sizeDelta = new Vector2(0f, 92f);
            }
            // 重新摆放原有的 4 个文字字段到合适位置
            RepositionTopBarField(GoldText,      new Vector2(0f, 0.5f), new Vector2(58f, 0f),   TextAnchor.MiddleLeft);
            RepositionTopBarField(PowerText,     new Vector2(0f, 0.5f), new Vector2(278f, 0f),  TextAnchor.MiddleLeft);
            RepositionTopBarField(GameTimerText, new Vector2(0.5f, 1f), new Vector2(0f, -8f),   TextAnchor.MiddleCenter);
            RepositionTopBarField(KillText,      new Vector2(1f, 0.5f), new Vector2(-172f, 0f), TextAnchor.MiddleRight);
            RepositionTopBarField(PopText,       new Vector2(1f, 0.5f), new Vector2(-30f, 0f),  TextAnchor.MiddleRight);
            // 资源图标
            EnsureResourceIcon(topBarGO.transform, "_GoldIcon",  new Vector2(0f, 0.5f), new Vector2(26f, 2f),  "$",  new Color(1f, 0.92f, 0.40f));
            EnsureResourceIcon(topBarGO.transform, "_PowerIcon", new Vector2(0f, 0.5f), new Vector2(246f, 2f), "⚡", new Color(0.55f, 1f, 0.55f));
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
            rt.anchoredPosition = new Vector2(10f, 68f);
            rt.sizeDelta = new Vector2(154f, 116f);
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
                rt.anchoredPosition = new Vector2(16f, 314f);
                rt.sizeDelta = new Vector2(136f, 42f);
            }
            StyleSideHudButton(BuildMenuToggle, bBuildMenuOpen ? "收起" : "建造", "⌂",
                new Color(0.90f, 0.68f, 0.22f, 0.95f));
            BuildMenuToggle.transform.SetAsLastSibling();
        }
        EnsureTechButton(canvas);

        // ── 4. 建造菜单 Panel：屏幕中央分类分页框 ────────────
        if (BuildMenuPanel != null)
        {
            var rt = BuildMenuPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 10f);
                rt.sizeDelta = new Vector2(560f, 340f);
            }
            EnsureBuildMenuPopupChrome();

            // 把原有建造按钮放进弹窗网格，分类过滤时只显示当前页。
            string[] iconGlyphs = { "⌂", "✈", "■", "◆", "$", "⚡" };
            if (BuildButtons != null)
            {
                for (int i = 0; i < BuildButtons.Length; i++)
                {
                    if (BuildButtons[i] == null) continue;
                    var brt = BuildButtons[i].GetComponent<RectTransform>();
                    if (brt == null) continue;
                    brt.anchorMin = new Vector2(0.5f, 0.5f);
                    brt.anchorMax = new Vector2(0.5f, 0.5f);
                    brt.pivot = new Vector2(0.5f, 1f);
                    int col = i % 3;
                    int row = i / 3;
                    brt.anchoredPosition = new Vector2(-172f + col * 172f, 52f - row * 96f);
                    brt.sizeDelta = new Vector2(148f, 78f);
                    // 添加顶部 emoji 图标层
                    if (BuildButtons[i].transform.Find("_BigIcon") == null && i < iconGlyphs.Length)
                    {
                        var ig = new GameObject("_BigIcon");
                        ig.transform.SetParent(BuildButtons[i].transform, false);
                        var igrt = ig.AddComponent<RectTransform>();
                        igrt.anchorMin = new Vector2(0f, 0.45f);
                        igrt.anchorMax = new Vector2(1f, 1f);
                        igrt.offsetMin = Vector2.zero; igrt.offsetMax = Vector2.zero;
                        var igt = ig.AddComponent<Text>();
                        igt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        if (igt.font == null) igt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                        igt.text = iconGlyphs[i];
                        igt.fontSize = 26;
                        igt.alignment = TextAnchor.MiddleCenter;
                        igt.color = new Color(1f, 0.92f, 0.55f);
                        igt.raycastTarget = false;
                        // 让原文字往下挪
                        var origTxt = BuildButtons[i].GetComponentInChildren<Text>();
                        if (origTxt != null && origTxt != igt)
                        {
                            var ort = origTxt.rectTransform;
                            ort.anchorMin = new Vector2(0f, 0f);
                            ort.anchorMax = new Vector2(1f, 0.45f);
                            ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
                            origTxt.fontSize = 12;
                            origTxt.alignment = TextAnchor.MiddleCenter;
                        }
                    }
                }
            }
            SetBuildCategory(_activeBuildCategory);
        }

        // ── 5. 底部路线快捷栏（新建）───────────────────────
        EnsureCommandBar(canvas);
        // ── 6. 战场设置入口：音量、继续、返回大厅。────────────
        EnsureGameSettingsUi(canvas);
        // ── 7. 战场内不显示旧返回大厅浮钮，避免误触；设置内提供返回大厅。─────
        RemoveReturnLobbyButton(canvas);
        if (PauseLobbyButton != null) PauseLobbyButton.gameObject.SetActive(false);
        // ── 8. 战地通讯浮窗会遮挡联机画面，直接清理掉 ─────────
        RemoveChatPanel(canvas);
    }

    void RepositionTopBarField(Text field, Vector2 anchor, Vector2 offset, TextAnchor align)
    {
        if (field == null) return;
        var rt = field.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x, field == GameTimerText ? 1f : 0.5f);
        rt.anchoredPosition = offset;
        float width = 130f;
        if (field == GoldText) width = 200f;
        else if (field == PowerText) width = 150f;
        else if (field == GameTimerText) width = 96f;
        else if (field == KillText) width = 120f;
        else if (field == PopText) width = 128f;
        rt.sizeDelta = new Vector2(width, 28f);
        field.alignment = align;
        field.fontSize = 20;
        field.fontStyle = FontStyle.Bold;
        field.color = field == PowerText ? new Color(0.55f, 1f, 0.64f) :
            field == KillText ? new Color(1f, 0.54f, 0.48f) :
            new Color(0.94f, 0.97f, 1f);
    }

    void EnsureResourceIcon(Transform parent, string name, Vector2 anchor, Vector2 offset, string glyph, Color color)
    {
        if (parent.Find(name) != null) return;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(28f, 28f);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.03f, 0.05f, 0.06f, 0.52f);
        img.raycastTarget = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0.20f, 0.32f, 0.42f, 0.70f);
        ol.effectDistance = new Vector2(1f, -1f);
        // 文字 glyph
        var txGO = new GameObject("Glyph");
        txGO.transform.SetParent(go.transform, false);
        var trt = txGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = txGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = glyph;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.raycastTarget = false;
    }

    void EnsureBuildMenuPopupChrome()
    {
        if (BuildMenuPanel == null) return;

        var panelImage = BuildMenuPanel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.035f, 0.045f, 0.035f, 0.90f);

        var panelOutline = BuildMenuPanel.GetComponent<Outline>();
        if (panelOutline == null)
            panelOutline = BuildMenuPanel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.86f, 0.68f, 0.18f, 0.85f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        EnsureBuildPopupTitle();
        EnsureBuildPopupCloseButton();
        EnsureBuildCategoryTabs();
        EnsureBuildEmptyLabel();
    }

    void EnsureBuildPopupTitle()
    {
        if (BuildMenuPanel.transform.Find("_BuildPopupTitle") != null) return;

        var go = new GameObject("_BuildPopupTitle");
        go.transform.SetParent(BuildMenuPanel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -12f);
        rt.sizeDelta = new Vector2(0f, 34f);

        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "建造指挥台";
        text.fontSize = 20;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.55f);
        text.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    void EnsureBuildPopupCloseButton()
    {
        if (BuildMenuPanel.transform.Find("_BuildPopupClose") != null) return;

        var button = CreatePopupButton(BuildMenuPanel.transform, "_BuildPopupClose", "×", new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(34f, 30f));
        button.onClick.AddListener(() => { _UiClickAudio.PlayClick(); CloseBuildMenu(); });
    }

    void EnsureBuildCategoryTabs()
    {
        if (_buildCategoryButtons == null || _buildCategoryButtons.Length != BuildCategoryNames.Length)
            _buildCategoryButtons = new Button[BuildCategoryNames.Length];

        var tabsRoot = BuildMenuPanel.transform.Find("_BuildCategoryTabs");
        if (tabsRoot == null)
        {
            var tabs = new GameObject("_BuildCategoryTabs");
            tabs.transform.SetParent(BuildMenuPanel.transform, false);
            var trt = tabs.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -54f);
            trt.sizeDelta = new Vector2(516f, 40f);
            tabsRoot = tabs.transform;
        }

        for (int i = 0; i < BuildCategoryNames.Length; i++)
        {
            if (_buildCategoryButtons[i] != null) continue;

            int idx = i;
            var button = CreatePopupButton(tabsRoot, "_BuildTab_" + i, BuildCategoryNames[i],
                new Vector2(0f, 0.5f), new Vector2(43f + i * 86f, 0f), new Vector2(78f, 32f));
            button.onClick.AddListener(() => { _UiClickAudio.PlayClick(); SetBuildCategory(idx); });
            _buildCategoryButtons[i] = button;
        }
    }

    void EnsureBuildEmptyLabel()
    {
        if (_buildEmptyText != null) return;
        var existing = BuildMenuPanel.transform.Find("_BuildEmptyText");
        if (existing != null)
        {
            _buildEmptyText = existing.GetComponent<Text>();
            return;
        }

        var go = new GameObject("_BuildEmptyText");
        go.transform.SetParent(BuildMenuPanel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -18f);
        rt.sizeDelta = new Vector2(420f, 70f);
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
        img.color = new Color(0.14f, 0.18f, 0.11f, 0.92f);
        var outline = go.AddComponent<Outline>();
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
        text.fontSize = label == "×" ? 24 : 15;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.55f);
        text.raycastTarget = false;
        return button;
    }

    static readonly Dictionary<string, Sprite> HudSpriteCache = new Dictionary<string, Sprite>(8);

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
                sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        if (sprite != null) HudSpriteCache[path] = sprite;
        return sprite;
    }

    void StyleSideHudButton(Button button, string label, string iconGlyph, Color accent)
    {
        if (button == null) return;

        var bg = button.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0.07f, 0.095f, 0.075f, 0.95f);
            bg.raycastTarget = true;
        }

        var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
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

    void EnsureTechButton(Canvas canvas)
    {
        if (canvas == null) return;
        var existing = canvas.transform.Find("_TechButton");
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        var rt = button != null ? button.GetComponent<RectTransform>() : null;
        if (button == null)
        {
            button = CreatePopupButton(canvas.transform, "_TechButton", "科技",
                new Vector2(0f, 0f), new Vector2(16f, 264f), new Vector2(136f, 42f));
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
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 264f);
            rt.sizeDelta = new Vector2(136f, 42f);
        }

        StyleSideHudButton(button, "科技", "◎",
            new Color(0.46f, 0.82f, 1f, 0.92f));
        button.transform.SetAsLastSibling();
    }

    void EnsureGameSettingsUi(Canvas canvas)
    {
        if (canvas == null) return;

        GameSettingsButton = EnsureSettingsButton(canvas.transform);
        GameSettingsPanel = EnsureSettingsPanel(canvas.transform);

        BindSettingsUiRefs();
        WireSettingsUi();
        SyncSettingsSlidersFromPrefs();

        if (GameSettingsPanel != null)
            GameSettingsPanel.SetActive(_settingsOpen);
        if (GameSettingsButton != null)
            GameSettingsButton.transform.SetAsLastSibling();
    }

    Button EnsureSettingsButton(Transform canvas)
    {
        var existing = canvas.Find("GameSettingsButton");
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            button = CreatePopupButton(canvas, "GameSettingsButton", "设置",
                new Vector2(1f, 1f), new Vector2(-12f, -62f), new Vector2(92f, 40f));
        }

        var rt = button.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-12f, -62f);
            rt.sizeDelta = new Vector2(92f, 40f);
        }

        StyleSideHudButton(button, "设置", "◎",
            new Color(0.80f, 0.70f, 0.42f, 0.95f));
        return button;
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

        var closeButton = CreatePopupButton(card.transform, "GameSettingsCloseButton", "继续游戏",
            new Vector2(0.30f, 0f), new Vector2(0f, 54f), new Vector2(160f, 48f));
        var closeImg = closeButton.GetComponent<Image>();
        if (closeImg != null) closeImg.color = new Color(0.16f, 0.45f, 0.24f, 0.98f);

        var lobbyButton = CreatePopupButton(card.transform, "GameSettingsReturnLobbyButton", "返回大厅",
            new Vector2(0.70f, 0f), new Vector2(0f, 54f), new Vector2(160f, 48f));
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
        var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
        if ((GameSettingsPanel == null || GameSettingsButton == null) && canvas != null)
            EnsureGameSettingsUi(canvas);

        _settingsOpen = open;
        if (GameSettingsPanel != null)
        {
            GameSettingsPanel.SetActive(open);
            if (open) GameSettingsPanel.transform.SetAsLastSibling();
        }

        if (open)
        {
            CloseBuildMenu();
            _UiClickAudio.PlayClick();
            if (!IsOnlineMatch() && !_settingsPausedTime)
            {
                _settingsPreviousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _settingsPausedTime = true;
            }
        }
        else if (_settingsPausedTime)
        {
            Time.timeScale = bPaused ? 0f : (_settingsPreviousTimeScale <= 0f ? 1f : _settingsPreviousTimeScale);
            _settingsPausedTime = false;
        }
    }

    void ReturnLobbyFromSettings()
    {
        _UiClickAudio.PlayConfirm();
        if (GameSettingsPanel != null) GameSettingsPanel.SetActive(false);
        _settingsOpen = false;
        _settingsPausedTime = false;
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
        var frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;
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
        slider.fillRect = frt;
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
        if (legacy != null) Destroy(legacy.gameObject);
        var oldSingle = canvas.transform.Find("_SelectAllBtn");
        if (oldSingle != null) Destroy(oldSingle.gameObject);
        var existing = canvas.transform.Find("_CommandBar");
        if (existing != null)
        {
            if (_commandButtons != null && _commandButtons.Length == 2) return;
            Destroy(existing.gameObject);
        }

        _commandButtons = new Button[2];
        _commandButtonImages = new Image[2];
        _commandButtonBaseColors = new Color[2];
        _commandButtonAccentColors = new Color[2];

        var bar = new GameObject("_CommandBar");
        bar.transform.SetParent(canvas.transform, false);
        var rt = bar.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 8f);
        rt.sizeDelta = new Vector2(CommandButtonPad * 2f + CommandButtonWidth + CommandButtonStep, 62f);

        var bg = bar.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.025f, 0.025f, 0.78f);
        bg.raycastTarget = false;
        var ol = bar.AddComponent<Outline>();
        ol.effectColor = new Color(0.95f, 0.76f, 0.32f, 0.65f);
        ol.effectDistance = new Vector2(1.2f, -1.2f);

        RegisterCommandButton(0, CreateCommandIconButton(bar.transform, 0, "_Cmd_SelectAll", "◎", new Color(0.65f, 0.90f, 1f),
            () => SelectAllOwnedUnits()));
        RegisterCommandButton(1, CreateCommandIconButton(bar.transform, 1, "_Cmd_BoxSelect", "□", new Color(1f, 0.78f, 0.32f),
            () => FindObjectOfType<RTSPlayerController>()?.ArmBoxSelectMode()));
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
    private const float CommandButtonPad = 8f;
    private const float CommandButtonWidth = 58f;
    private const float CommandButtonStep = 66f;

    void RegisterCommandButton(int index, CommandButtonParts parts)
    {
        if (_commandButtons == null || index < 0 || index >= _commandButtons.Length) return;
        _commandButtons[index] = parts.Button;
        _commandButtonImages[index] = parts.Image;
        _commandButtonBaseColors[index] = parts.BaseColor;
        _commandButtonAccentColors[index] = parts.AccentColor;
    }

    CommandButtonParts CreateCommandIconButton(Transform parent, int index, string name, string glyph, Color accent, UnityEngine.Events.UnityAction action)
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

        return new CommandButtonParts
        {
            Button = btn,
            Image = img,
            BaseColor = baseColor,
            AccentColor = accent
        };
    }

    void UpdateCommandBarState()
    {
        if (_commandButtons == null || _commandButtonImages == null) return;
        var pc = RTSPlayerController.Instance;
        bool inPlacement = pc != null && pc.IsInPlacementMode;

        SetCommandButtonState(0, pc != null && !inPlacement, false);
        SetCommandButtonState(1, pc != null && !inPlacement, pc != null && pc.IsBoxSelectArmed);
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

    // 战地通讯消息队列
    private System.Collections.Generic.List<Text> _chatLines;
    private const int ChatMaxLines = 4;
    private Transform _chatBody;

    /// <summary>外部调用：在右下战地通讯面板追加一条消息（自动滚动）。</summary>
    public void AppendChatMessage(string speaker, string message, Color color = default)
    {
        // 不再显示战地通讯浮窗，避免遮挡血条、建造面板和战场视野。
        if (!ShouldShowBattleChatPanel()) return;
        if (_chatBody == null || _chatLines == null) return;
        if (color.a < 0.01f) color = new Color(1f, 0.92f, 0.55f);
        // 已满则丢弃最早一条
        if (_chatLines.Count >= ChatMaxLines)
        {
            var first = _chatLines[0];
            if (first != null) Destroy(first.gameObject);
            _chatLines.RemoveAt(0);
        }
        var ln = new GameObject("Line");
        ln.transform.SetParent(_chatBody, false);
        var rt = ln.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 18f);
        var tx = ln.AddComponent<Text>();
        tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tx.font == null) tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.text = $"[{speaker}] {message}";
        tx.fontSize = 12;
        tx.alignment = TextAnchor.MiddleLeft;
        tx.color = color;
        tx.raycastTarget = false;
        tx.horizontalOverflow = HorizontalWrapMode.Wrap;
        tx.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = ln.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        shadow.effectDistance = new Vector2(1f, -1f);
        _chatLines.Add(tx);
        // 重新摆位
        for (int i = 0; i < _chatLines.Count; i++)
        {
            if (_chatLines[i] == null) continue;
            var lrt = _chatLines[i].rectTransform;
            lrt.anchoredPosition = new Vector2(8f, -i * 20f - 6f);
        }
    }

    /// <summary>右侧会话/聊天占位面板（半透明，不挡视野）。</summary>
    void EnsureChatPanel(Canvas canvas)
    {
        if (canvas.transform.Find("_ChatPanel") != null) return;
        var p = new GameObject("_ChatPanel");
        p.transform.SetParent(canvas.transform, false);
        var rt = p.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-12f, 76f);
        rt.sizeDelta = new Vector2(260f, 116f);
        var bg = p.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.03f, 0.14f);
        bg.raycastTarget = false;
        var ol = p.AddComponent<Outline>();
        ol.effectColor = new Color(0.78f, 0.62f, 0.18f, 0.32f);
        ol.effectDistance = new Vector2(1f, -1f);
        // 标题
        var hd = new GameObject("Header");
        hd.transform.SetParent(p.transform, false);
        var hdrt = hd.AddComponent<RectTransform>();
        hdrt.anchorMin = new Vector2(0f, 1f); hdrt.anchorMax = new Vector2(1f, 1f);
        hdrt.pivot = new Vector2(0.5f, 1f);
        hdrt.anchoredPosition = Vector2.zero;
        hdrt.sizeDelta = new Vector2(0f, 22f);
        hd.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.07f, 0.30f);
        var ht = new GameObject("Txt");
        ht.transform.SetParent(hd.transform, false);
        var htrt = ht.AddComponent<RectTransform>();
        htrt.anchorMin = Vector2.zero; htrt.anchorMax = Vector2.one;
        htrt.offsetMin = Vector2.zero; htrt.offsetMax = Vector2.zero;
        var hT = ht.AddComponent<Text>();
        hT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (hT.font == null) hT.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        hT.text = "战地通讯";
        hT.fontSize = 13;
        hT.fontStyle = FontStyle.Bold;
        hT.alignment = TextAnchor.MiddleCenter;
        hT.color = new Color(1f, 0.92f, 0.55f);
        hT.raycastTarget = false;

        // 内容区
        var body = new GameObject("Body");
        body.transform.SetParent(p.transform, false);
        var brt = body.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = new Vector2(0f, 0f);
        brt.offsetMax = new Vector2(0f, -22f);
        _chatBody = body.transform;
        _chatLines = new System.Collections.Generic.List<Text>();

        // 初始欢迎消息
        AppendChatMessage("司令部", "战斗开始，全军进入备战状态", new Color(1f, 0.92f, 0.55f));
        AppendChatMessage("情报部", "侦察到敌方基地位置已锁定", new Color(0.55f, 0.85f, 1f));
    }

    void RemoveChatPanel(Canvas canvas)
    {
        if (canvas == null) return;
        var existing = canvas.transform.Find("_ChatPanel");
        if (existing != null)
            Destroy(existing.gameObject);
        _chatBody = null;
        _chatLines = null;
    }

    void RuntimeUIPolish()
    {
        // ── TopBar 指挥官 HUD ───────────────────────────────
        var topBarGO = GoldText?.transform.parent?.gameObject;
        if (topBarGO != null)
        {
            var img = topBarGO.GetComponent<Image>();
            // 半透明暗背景（地图最大化可见，但仍有可读对比）
            if (img != null) img.color = new Color(0.015f, 0.020f, 0.026f, 0.88f);
            // 顶部金色描边
            if (topBarGO.transform.Find("_TopAccent") == null)
            {
                var acc = new GameObject("_TopAccent");
                acc.transform.SetParent(topBarGO.transform, false);
                var ar = acc.AddComponent<RectTransform>();
                ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = new Vector2(1f, 1f);
                ar.offsetMin = new Vector2(0f, -2f); ar.offsetMax = Vector2.zero;
                acc.AddComponent<Image>().color = new Color(0.20f, 0.32f, 0.42f, 0.80f);
            }
            // 中央头像 + 官职 + 名字
            EnsureCommanderHeader(topBarGO.transform);
            // 字段之间垂直分隔线
            var fields = new System.Collections.Generic.List<Text>(4);
            if (PopText) fields.Add(PopText);
            if (KillText) fields.Add(KillText);
            if (PowerText) fields.Add(PowerText);
            if (GameTimerText) fields.Add(GameTimerText);
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i] == null || fields[i].rectTransform.parent != topBarGO.transform) continue;
                string sepName = "_Sep" + i;
                if (topBarGO.transform.Find(sepName) != null) continue;
                var sep = new GameObject(sepName);
                sep.transform.SetParent(topBarGO.transform, false);
                var srt = sep.AddComponent<RectTransform>();
                // 把分隔线放在每个字段的左侧 anchorMin/Max
                Vector2 anchor = fields[i].rectTransform.anchorMin;
                srt.anchorMin = new Vector2(anchor.x, 0.18f);
                srt.anchorMax = new Vector2(anchor.x, 0.82f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
                srt.sizeDelta = new Vector2(2f, 0f);
                sep.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.07f);
            }
            // 金色底部强调线
            if (topBarGO.transform.Find("_Accent") == null)
            {
                var acc = new GameObject("_Accent");
                acc.transform.SetParent(topBarGO.transform, false);
                var ar = acc.AddComponent<RectTransform>();
                ar.anchorMin = Vector2.zero; ar.anchorMax = new Vector2(1f, 0f);
                ar.offsetMin = Vector2.zero; ar.offsetMax = new Vector2(0f, 2f);
                acc.AddComponent<Image>().color = new Color(0.16f, 0.26f, 0.34f, 0.85f);
            }
        }
        // 文字加粗
        foreach (var t in new Text[]{ GoldText, PopText, KillText, PowerText, GameTimerText })
            if (t != null) { t.fontStyle = FontStyle.Bold; if (t.fontSize < 20) t.fontSize = 20; }

        // ── UnitInfoPanel 军用风 ────────────────────────────
        if (UnitInfoPanel != null)
        {
            var bg = UnitInfoPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.018f, 0.026f, 0.034f, 0.96f);
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
            if (bg != null) bg.color = new Color(0.018f, 0.026f, 0.034f, 0.96f);
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
            var bg = BuildMenuPanel.GetComponent<Image>();
            // 暗橄榄绿背景
            if (bg != null) bg.color = new Color(0.018f, 0.024f, 0.030f, 0.96f);
            // 顶部金色边线（如果还没有就加）
            if (BuildMenuPanel.transform.Find("_TopGold") == null)
            {
                var gl = new GameObject("_TopGold");
                gl.transform.SetParent(BuildMenuPanel.transform, false);
                var rt = gl.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(0f, -2f); rt.offsetMax = Vector2.zero;
                gl.AddComponent<Image>().color = new Color(0.18f, 0.42f, 0.26f, 0.92f);
            }
            // 全部建造按钮统一军用色（橄榄绿 + 金描边阴影）
            if (BuildButtons != null)
            {
                foreach (var btn in BuildButtons)
                {
                    if (btn == null) continue;
                    var bImg = btn.GetComponent<Image>();
                    if (bImg != null) bImg.color = new Color(0.07f, 0.10f, 0.11f, 0.98f);
                    // 给按钮本体加金色 Outline（如果还没加）
                    if (btn.GetComponent<Outline>() == null)
                    {
                        var ol = btn.gameObject.AddComponent<Outline>();
                        ol.effectColor = new Color(0.35f, 0.75f, 0.48f, 0.75f);
                        ol.effectDistance = new Vector2(1.2f, -1.2f);
                    }
                    // 按钮文字也调成金黄
                    var bt = btn.GetComponentInChildren<Text>();
                    if (bt != null) bt.color = new Color(0.92f, 0.98f, 0.92f);
                }
            }
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

    void WireBuildButtons()
    {
        if (BuildButtons == null) return;
        GameObject[] prefabs = {
            BarracksPrefab, AirFactoryPrefab, TankFactoryPrefab,
            TurretPrefab,   GoldMinePrefab,   PowerPlantPrefab
        };
        _buildPriceTexts = new Text[BuildButtons.Length];
        _buildPrices = new int[BuildButtons.Length];
        _buildPowerCosts = new int[BuildButtons.Length];
        _buildIsPowerPlant = new bool[BuildButtons.Length];
        for (int i = 0; i < BuildButtons.Length; i++)
        {
            if (BuildButtons[i] == null) continue;
            int idx = i;
            BuildButtons[i].onClick.AddListener(() => {
                if (idx < prefabs.Length && prefabs[idx] != null)
                {
                    if (!TryAffordBuild(idx, BuildButtons[idx])) return;
                    _UiClickAudio.PlayConfirm();
                    RTSPlayerController.Instance?.StartPlacement(prefabs[idx]);
                    CloseBuildMenu();
                }
            });
            // 读取建筑价格 / 电力消耗 / 是否电厂
            int price = 0;
            int powerCost = 0;
            bool isPlant = false;
            if (idx < prefabs.Length && prefabs[idx] != null)
            {
                var b = prefabs[idx].GetComponent<RTSBuilding>();
                if (b != null)
                {
                    price = b.GoldCost;
                    powerCost = b.PowerCost;
                    isPlant = b.bIsPowerPlant;
                }
            }
            _buildPrices[i] = price;
            _buildPowerCosts[i] = powerCost;
            _buildIsPowerPlant[i] = isPlant;
            // 在按钮右下角创建价格 Text
            _buildPriceTexts[i] = CreateBuildPriceTag(BuildButtons[i].transform, price);
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
        rt.anchoredPosition = new Vector2(0f, 2f);
        rt.sizeDelta = new Vector2(0f, 18f);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = $"$ {price}";
        t.fontSize = 14;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.92f, 0.55f);
        t.raycastTarget = false;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(1.2f, -1.2f);
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
            bool isPlant = _buildIsPowerPlant != null && i < _buildIsPowerPlant.Length && _buildIsPowerPlant[i];
            bool goldOk  = price <= gold;
            bool powerOk = isPlant || powerCost <= powerAvail;
            bool enabled = goldOk && powerOk;
            // 保留 interactable=true，点击时给反馈（deny 音 + 抖动）
            var img = BuildButtons[i].GetComponent<Image>();
            if (img != null)
            {
                Color baseC = new Color(0.18f, 0.22f, 0.13f, 0.96f);
                img.color = enabled ? baseC : new Color(baseC.r * 0.6f, baseC.g * 0.6f, baseC.b * 0.6f, 0.85f);
            }
            // 价格文字：金币不足=红，电力不足=橙，都正常=金
            if (_buildPriceTexts[i] != null)
            {
                _buildPriceTexts[i].color = !goldOk  ? new Color(1f, 0.40f, 0.34f)
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

                int col = visibleCount % 3;
                int row = visibleCount / 3;
                var rt = BuildButtons[i].GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(-172f + col * 172f, 52f - row * 96f);
                visibleCount++;
            }
        }

        if (_buildEmptyText != null)
        {
            bool empty = visibleCount == 0;
            _buildEmptyText.gameObject.SetActive(empty);
            _buildEmptyText.text = empty
                ? "海上单位正在接入：后续会放船坞、战舰和运输船"
                : "";
        }

        RefreshBuildCategoryTabs();
    }

    bool IsBuildButtonInCategory(int buildIndex, int category)
    {
        switch (category)
        {
            case 0: return true;
            case 1: return buildIndex == 0 || buildIndex == 1 || buildIndex == 2 || buildIndex == 3;
            case 2: return buildIndex == 4 || buildIndex == 5;
            case 3: return false;
            case 4: return buildIndex == 1;
            case 5: return buildIndex == 0 || buildIndex == 2 || buildIndex == 3;
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
            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = i == _activeBuildCategory
                    ? new Color(0.48f, 0.34f, 0.08f, 0.96f)
                    : new Color(0.14f, 0.18f, 0.11f, 0.86f);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.color = i == _activeBuildCategory
                    ? Color.white
                    : new Color(1f, 0.92f, 0.55f);
        }
    }

    /// <summary>建造按钮点击：钱不够或电力不够时播 deny 音 + 抖动。</summary>
    public bool TryAffordBuild(int idx, Button btn)
    {
        if (_buildPrices == null || idx >= _buildPrices.Length || playerState == null) return true;
        int price = _buildPrices[idx];
        if (playerState.Gold < price)
        {
            _UiClickAudio.PlayDeny();
            if (btn != null) StartCoroutine(ShakeButton(btn.GetComponent<RectTransform>()));
            return false;
        }
        // 电力检查：电厂自身不消耗电力，跳过
        bool isPlant = _buildIsPowerPlant != null && idx < _buildIsPowerPlant.Length && _buildIsPowerPlant[idx];
        int powerCost = _buildPowerCosts != null && idx < _buildPowerCosts.Length ? _buildPowerCosts[idx] : 0;
        if (!isPlant && powerCost > playerState.PowerCap - playerState.PowerUsed)
        {
            _UiClickAudio.PlayDeny();
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
        bBuildMenuOpen = !bBuildMenuOpen;
        if (BuildMenuPanel)
        {
            BuildMenuPanel.SetActive(bBuildMenuOpen);
            if (bBuildMenuOpen)
            {
                BuildMenuPanel.transform.SetAsLastSibling();
                SetBuildCategory(_activeBuildCategory);
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
        RemoveChatPanel(GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>());
        if (IsOnlineMatch() && (bPaused || (PausePanel != null && PausePanel.activeSelf)))
            HidePauseMenu();
        UpdateTopBar();
        UpdateSelectionUI();
        UpdateAlert();
        RefreshBuildButtonStates();
        UpdateCommandBarState();
        UpdateResourceWarnings();
        UpdateEdgeThreatIndicators();
    }

    bool ShouldShowBattleChatPanel()
    {
        return false;
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
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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
        UpdateVignetteAlert();
    }

    /// <summary>根据玩家状态决定屏幕边缘红光强度。</summary>
    void UpdateVignetteAlert()
    {
        if (_vignette == null) return;
        if (_baseUnderAttackTimer > 0f) _baseUnderAttackTimer -= Time.deltaTime;

        bool active = false;
        float intensity = 0f;
        if (playerState != null)
        {
            // 基地受攻：最强红光
            if (_baseUnderAttackTimer > 0f)
            { active = true; intensity = Mathf.Max(intensity, 1.0f); }
            // 人口爆满：中等红光
            if (playerState.PopUsed >= playerState.PopCap)
            { active = true; intensity = Mathf.Max(intensity, 0.7f); }
            // 电力超载：中等红光
            if (playerState.PowerUsed > playerState.PowerCap)
            { active = true; intensity = Mathf.Max(intensity, 0.7f); }
            // 极度缺金（< 100）：弱红光
            if (playerState.Gold < 100)
            { active = true; intensity = Mathf.Max(intensity, 0.4f); }
        }
        _vignette.SetState(active, intensity);
    }

    /// <summary>外部调用：基地受攻击时触发 4 秒红光。</summary>
    public void NotifyBaseUnderAttack()
    {
        // 仅在新一波警告（之前未触发或快结束）时写入聊天，避免刷屏
        if (_baseUnderAttackTimer < 1.5f)
            AppendChatMessage("情报部", "警告！我方基地遭受攻击！", new Color(1f, 0.42f, 0.22f));
        _baseUnderAttackTimer = 4f;
    }

    /// <summary>大型爆炸时屏幕全屏闪光（短促）。intensity 0~1，duration 秒。</summary>
    public void FlashScreen(Color color, float intensity = 0.55f, float duration = 0.18f)
    {
        StartCoroutine(FlashScreenCoroutine(color, intensity, duration));
    }

    System.Collections.IEnumerator FlashScreenCoroutine(Color baseColor, float intensity, float duration)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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

    /// <summary>AI 进攻波次预警横幅 + 红光。</summary>
    public void ShowWaveWarning(int waveIndex, int unitCount, bool twoProng)
    {
        string body = twoProng
            ? $"敌方第 {waveIndex} 波进攻 — {unitCount} 个单位 · 两路包抄!"
            : $"敌方第 {waveIndex} 波进攻 — {unitCount} 个单位来袭!";
        StartCoroutine(SpawnWaveBanner(body));
        // 同步触发 4 秒红光，提醒玩家
        _baseUnderAttackTimer = Mathf.Max(_baseUnderAttackTimer, 4f);
        // 写入战地通讯
        AppendChatMessage("侦察兵", body, new Color(1f, 0.55f, 0.22f));
        // 警报音
        _UiClickAudio.PlayWarn();
    }

    System.Collections.IEnumerator SpawnWaveBanner(string text)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
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
            // 创建在 ProductionBar 旁边或 BuildingPanel 内
            Transform parent = BuildingPanel.transform;
            if (ProductionBar != null) parent = ProductionBar.transform.parent;
            var go = new GameObject("CancelProdBtn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            // 锚定到 BuildingPanel 右上
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, -42f);
            rt.sizeDelta = new Vector2(46f, 28f);
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

    // 指挥官头像/名字/官职引用
    private Text _commanderRankText;
    private Text _commanderNameText;
    // 主基地血量条
    private Image _baseHpFill;
    private Text  _baseHpText;
    // 官职变化检测
    private string _prevRank = "";

    /// <summary>顶部中央插入圆形头像 + 官职（少校等）+ 玩家名。</summary>
    void EnsureCommanderHeader(Transform topBar)
    {
        if (topBar.Find("_CommanderHeader") != null) return;
        var holder = new GameObject("_CommanderHeader");
        holder.transform.SetParent(topBar, false);
        var hrt = holder.AddComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0.5f, 0f);
        hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(280f, 0f);

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

        // 军衔行容器（居中放置：[★ 图标] + [军衔文字]）
        var rankRowGO = new GameObject("RankRow");
        rankRowGO.transform.SetParent(holder.transform, false);
        var rrowRT = rankRowGO.AddComponent<RectTransform>();
        rrowRT.anchorMin = new Vector2(0.5f, 1f);
        rrowRT.anchorMax = new Vector2(0.5f, 1f);
        rrowRT.pivot = new Vector2(0.5f, 1f);
        rrowRT.anchoredPosition = new Vector2(0f, -38f);
        rrowRT.sizeDelta = new Vector2(220f, 20f);

        // 军衔星形图标（在军衔文字左边）
        var iconGO = new GameObject("RankIcon");
        iconGO.transform.SetParent(rankRowGO.transform, false);
        var irt = iconGO.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(1f, 0.5f);
        // 文字宽度估算 ~28 (两个汉字)，图标紧贴在文字左侧 4px 间距
        irt.anchoredPosition = new Vector2(-16f, 0f);
        irt.sizeDelta = new Vector2(18f, 18f);
        var iconText = iconGO.AddComponent<Text>();
        iconText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (iconText.font == null) iconText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        iconText.text = "★";
        iconText.fontSize = 16;
        iconText.fontStyle = FontStyle.Bold;
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.color = new Color(1f, 0.92f, 0.55f);
        iconText.raycastTarget = false;

        // 军衔文字（居中显示在 RankRow 内）
        var rankGO = new GameObject("RankText");
        rankGO.transform.SetParent(rankRowGO.transform, false);
        var rrt = rankGO.AddComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0.5f);
        rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(8f, 0f);
        rrt.sizeDelta = new Vector2(120f, 18f);
        _commanderRankText = rankGO.AddComponent<Text>();
        _commanderRankText.font = _commanderNameText.font;
        _commanderRankText.text = "上尉";
        _commanderRankText.fontSize = 13;
        _commanderRankText.alignment = TextAnchor.MiddleLeft;
        _commanderRankText.color = new Color(0.85f, 0.82f, 0.60f);
        _commanderRankText.raycastTarget = false;

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
        var frt = fill.AddComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
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
    }

    /// <summary>晋升新官职时屏幕中央弹出"晋升 少校!"金字。</summary>
    System.Collections.IEnumerator SpawnRankUpText(string newRank)
    {
        AppendChatMessage("司令部", $"恭喜晋升【{newRank}】！", new Color(1f, 0.92f, 0.45f));
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) yield break;
        var go = new GameObject("RankUp");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(700f, 100f);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.text = $"⭐ 晋升 {newRank}! ⭐";
        t.fontSize = 56;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.92f, 0.45f);
        t.raycastTarget = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
        ol.effectDistance = new Vector2(3f, -3f);
        // 阶段 1：弹性放大 0.3 → 1.3
        float t1 = 0f;
        while (t1 < 0.25f)
        {
            t1 += Time.deltaTime;
            float r = Mathf.Clamp01(t1 / 0.25f);
            rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.30f, r);
            yield return null;
        }
        // 回弹
        float t2 = 0f;
        while (t2 < 0.12f)
        {
            t2 += Time.deltaTime;
            float r = Mathf.Clamp01(t2 / 0.12f);
            rt.localScale = Vector3.one * Mathf.Lerp(1.30f, 1.0f, r);
            yield return null;
        }
        // 停留 + 微脉动
        float t3 = 0f;
        while (t3 < 1.6f)
        {
            t3 += Time.deltaTime;
            float k = 1f + 0.04f * Mathf.Sin(Time.time * 5.5f);
            rt.localScale = Vector3.one * k;
            yield return null;
        }
        // 渐隐
        float t4 = 0f;
        Color sc = t.color;
        while (t4 < 0.35f)
        {
            t4 += Time.deltaTime;
            float r = Mathf.Clamp01(t4 / 0.35f);
            t.color = new Color(sc.r, sc.g, sc.b, 1f - r);
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>按击杀数计算官职。</summary>
    string GetRankByKills(int kills)
    {
        if (kills >= 100) return "元帅";
        if (kills >= 60)  return "上将";
        if (kills >= 40)  return "中将";
        if (kills >= 25)  return "少将";
        if (kills >= 15)  return "上校";
        if (kills >= 8)   return "中校";
        if (kills >= 3)   return "少校";
        return "上尉";
    }

    /// <summary>计算玩家所有产金建筑的总收入速率（金币/秒）。</summary>
    int ComputePlayerGoldRate()
    {
        var bldgs = GameManager.Instance?.GetAllBuildings();
        if (bldgs == null) return 0;
        float rate = 0f;
        foreach (var b in bldgs)
        {
            if (b == null || !b.bPlayerOwned || b.GoldIncomeAmount <= 0) continue;
            float interval = Mathf.Max(0.1f, b.GoldIncomeInterval);
            rate += b.GoldIncomeAmount / interval;
        }
        return Mathf.RoundToInt(rate);
    }

    void UpdateTopBar()
    {
        if (playerState == null) return;
        // 同步官职（按击杀数）
        if (_commanderRankText != null)
        {
            string rank = GetRankByKills(playerState.EnemyKillCount);
            _commanderRankText.text = rank;
            // 官职变化时弹出晋升提示
            if (!string.IsNullOrEmpty(_prevRank) && rank != _prevRank)
                StartCoroutine(SpawnRankUpText(rank));
            _prevRank = rank;
        }
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
            if (UnitInfoPanel) UnitInfoPanel.SetActive(true);
            if (BuildingPanel) BuildingPanel.SetActive(false);
            if (UnitNameText)
            {
                // 老兵星级（金色 ★/★★/★★★）+ 名字 + 多选 +N
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
                UnitNameText.text = currentSelectedUnits.Count > 1
                    ? $"{starPrefix}{u.DisplayName} +{currentSelectedUnits.Count - 1}{suffix}"
                    : $"{starPrefix}{u.DisplayName}{suffix}";
            }
            float uhpRatio = (float)u.GetHP() / Mathf.Max(1, u.GetMaxHP());
            if (UnitHPBar) { UnitHPBar.value = uhpRatio; SetHPBarColor(UnitHPBar, uhpRatio); }
            if (UnitHPText) UnitHPText.text = $"{u.GetHP()}/{u.GetMaxHP()}";
            // 二战风 RTS 没有主动技能：技能按钮始终隐藏
            if (SkillButton && SkillButton.gameObject.activeSelf)
                SkillButton.gameObject.SetActive(false);
        }
        else if (currentSelectedBuilding != null)
        {
            RTSBuilding b = currentSelectedBuilding;
            if (UnitInfoPanel) UnitInfoPanel.SetActive(false);
            if (BuildingPanel) BuildingPanel.SetActive(true);
            if (BuildingNameText) BuildingNameText.text = b.DisplayName;
            float bhpRatio = (float)b.GetHP() / Mathf.Max(1, b.GetMaxHP());
            if (BuildingHPBar) { BuildingHPBar.value = bhpRatio; SetHPBarColor(BuildingHPBar, bhpRatio); }
            if (ProductionBar) ProductionBar.value = GetSmoothedProductionBarValue(b);
            EnsureCancelProductionButton(b);
            if (ProductionText)
            {
                bool hasProd = b.ProductionUnits != null && b.ProductionUnits.Length > 0;
                ProductionText.supportRichText = true;
                if (!hasProd)
                    ProductionText.text = b.bIsMainBase ? "修复/升级" : "";
                else if (b.ProductionQueue.Count > 0)
                {
                    // 第 1 个 = 正在生产；后续 = 队列
                    string current = "";
                    int firstIdx = b.ProductionQueue[0];
                    if (firstIdx >= 0 && firstIdx < b.ProductionUnits.Length && b.ProductionUnits[firstIdx] != null)
                    {
                        var p0 = b.ProductionUnits[firstIdx].GetComponent<RTSUnit>();
                        string n0 = p0 != null ? GetUnitDisplayNameStatic(p0) : $"单位{firstIdx+1}";
                        current = $"<color=#FFD56B>生产中: {n0}</color> ({Mathf.RoundToInt(Mathf.Clamp01(b.ProductionProgress) * 100f)}%)";
                    }
                    // 队列后续
                    string queue = "";
                    if (b.ProductionQueue.Count > 1)
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.Append("\n<color=#A8B0BD>队列: ");
                        int maxShow = Mathf.Min(5, b.ProductionQueue.Count - 1);
                        for (int qi = 1; qi <= maxShow; qi++)
                        {
                            int qIdx = b.ProductionQueue[qi];
                            if (qIdx < 0 || qIdx >= b.ProductionUnits.Length || b.ProductionUnits[qIdx] == null) continue;
                            var pq = b.ProductionUnits[qIdx].GetComponent<RTSUnit>();
                            string nq = pq != null ? GetUnitDisplayNameStatic(pq) : $"#{qIdx}";
                            if (qi > 1) sb.Append(" → ");
                            sb.Append(nq);
                        }
                        int remain = b.ProductionQueue.Count - 1 - maxShow;
                        if (remain > 0) sb.Append($" +{remain}");
                        sb.Append("</color>");
                        queue = sb.ToString();
                    }
                    ProductionText.text = current + queue;
                }
                else
                    ProductionText.text = "<color=#7E867F>空闲</color>";
            }
            // 更新生产按鈕显示（onClick 已在 OnSelectionChanged 里绑定，这里只更新文字和可见性）
            if (ProductionButtons != null && b.ProductionUnits != null)
            {
                for (int i = 0; i < ProductionButtons.Length; i++)
                {
                    if (ProductionButtons[i] == null) continue;
                    bool active = i < b.ProductionUnits.Length && b.ProductionUnits[i] != null;
                    ProductionButtons[i].gameObject.SetActive(active);
                    if (active)
                    {
                        RTSUnit proto = b.ProductionUnits[i].GetComponent<RTSUnit>();
                        int cost = (b.ProductionCosts != null && i < b.ProductionCosts.Length) ? b.ProductionCosts[i] : 0;
                        int popCost = proto != null ? Mathf.Max(1, proto.PopCost) : 1;
                        var txt = ProductionButtons[i].GetComponentInChildren<Text>();
                        if (txt)
                        {
                            string uname = proto != null ? GetUnitDisplayNameStatic(proto) : $"单位{i+1}";
                            // 同时显示金币和人口消耗，方便玩家判断
                            txt.text = popCost > 1
                                ? $"{uname}\n({cost}金 · {popCost}人)"
                                : $"{uname}\n({cost}金)";
                        }
                        // 金币或人口不足时变灰，但保持 interactable=true 以便点击触发 deny 反馈（在 BindProductionButtons 内 check）
                        bool canAfford = playerState == null || playerState.Gold >= cost;
                        bool hasRoom   = playerState == null || playerState.PopUsed + popCost <= playerState.PopCap;
                        bool ok = canAfford && hasRoom;
                        ProductionButtons[i].interactable = true;
                        var btnImg = ProductionButtons[i].GetComponent<Image>();
                        if (btnImg != null)
                        {
                            Color baseC = new Color(0.16f, 0.22f, 0.32f, 0.96f);
                            btnImg.color = ok ? baseC
                                : new Color(baseC.r * 0.55f, baseC.g * 0.55f, baseC.b * 0.55f, 0.85f);
                        }
                        // 文字颜色：金不足红、人口不足橙、可造金黄
                        if (txt != null)
                        {
                            txt.color = !canAfford ? new Color(1f, 0.4f, 0.34f)
                                       : !hasRoom ? new Color(1f, 0.78f, 0.30f)
                                       : new Color(1f, 0.92f, 0.55f);
                        }
                    }
                }
            }
        }
        else
        {
            if (UnitInfoPanel) UnitInfoPanel.SetActive(false);
            if (BuildingPanel) BuildingPanel.SetActive(false);
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
        currentSelectedUnits = units ?? new List<RTSUnit>();
        currentSelectedBuilding = building;
        if (building != _productionBarOwner)
        {
            _productionBarOwner = null;
            _productionBarDisplayValue = 0f;
        }
        BindProductionButtons(building);
    }

    void BindProductionButtons(RTSBuilding b)
    {
        if (ProductionButtons == null) return;
        for (int i = 0; i < ProductionButtons.Length; i++)
        {
            if (ProductionButtons[i] == null) continue;
            ProductionButtons[i].onClick.RemoveAllListeners();
            if (b == null || b.ProductionUnits == null || i >= b.ProductionUnits.Length || b.ProductionUnits[i] == null) continue;
            int idx = i;
            ProductionButtons[i].onClick.AddListener(() => {
                if (b == null || b.GetHP() <= 0) return;
                // 实时 affordability 检查：金币 / 人口（基于该单位的 PopCost）。不足则 deny 反馈
                int cost = (b.ProductionCosts != null && idx < b.ProductionCosts.Length) ? b.ProductionCosts[idx] : 0;
                int popCost = 1;
                if (b.ProductionUnits[idx] != null)
                {
                    var proto = b.ProductionUnits[idx].GetComponent<RTSUnit>();
                    if (proto != null) popCost = Mathf.Max(1, proto.PopCost);
                }
                if (playerState != null)
                {
                    if (playerState.Gold < cost
                        || playerState.PopUsed + popCost > playerState.PopCap)
                    {
                        _UiClickAudio.PlayDeny();
                        StartCoroutine(ShakeButton(ProductionButtons[idx].GetComponent<RectTransform>()));
                        return;
                    }
                }
                _UiClickAudio.PlayConfirm();
                b.EnqueueUnit(idx);
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
        string reportText = GameManager.Instance != null
            ? GameManager.Instance.BuildBattleReport(kills, seconds)
            : BuildFallbackBattleReport(kills, seconds);
        UpdateGameOverSummaryCards(kills, seconds);
        if (GameOverText)
        {
            GameOverText.text = win ? "胜利" : "失败";
            GameOverText.color = win ? new Color(0.9f, 0.8f, 0.1f) : new Color(0.9f, 0.3f, 0.3f);
            StartCoroutine(AnimateGameOverTitle(GameOverText, win));
        }
        if (GameOverStatsText)
        {
            // 滚动数字：先显示空，再 1 秒内滚到目标值
            StartCoroutine(AnimateStatsRoll(GameOverStatsText, reportText));
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
        if (PausePanel != null) PausePanel.SetActive(false);
        if (AlertText != null) AlertText.gameObject.SetActive(false);
        if (MinimapImage != null) MinimapImage.gameObject.SetActive(false);
        if (MinimapCameraRect != null) MinimapCameraRect.gameObject.SetActive(false);
        bBuildMenuOpen = false;
        bPaused = false;
        Time.timeScale = 1f;
    }

    string BuildFallbackBattleReport(int kills, int seconds)
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
            ? $"队伍：{(sync.IsHost ? "HOST" : "GUEST")} / 联机对战"
            : "队伍：我方部队";

        return $"{teamLine}\n" +
               $"战果：击杀 {kills}    用时 {m:00}:{s:00}    人口 {popUsed}/{popCap}\n" +
               $"经济：金币 {gold:N0}\n" +
               $"电力：{powerUsed}/{powerCap}\n" +
               "兵力：战场对象统计暂不可用";
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
            item = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();

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
            rt.offsetMin = new Vector2(24f, 16f);
            rt.offsetMax = new Vector2(-22f, -50f);
            GameOverStatsText.fontSize = 15;
            GameOverStatsText.alignment = TextAnchor.UpperLeft;
            GameOverStatsText.lineSpacing = 1.10f;
            GameOverStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            GameOverStatsText.verticalOverflow = VerticalWrapMode.Overflow;
            GameOverStatsText.color = new Color(0.84f, 0.93f, 1f);
            var statsShadow = GetOrAddUiShadow(GameOverStatsText.gameObject);
            statsShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            statsShadow.effectDistance = new Vector2(1f, -1f);
        }
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
            badge = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
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
            markGo = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Text>();
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
            deck = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
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
            report = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
        }

        var rt = report.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.410f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(640f, 205f);
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

    void EnsureGameOverReportBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        var bar = parent.Find(name);
        if (bar == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            bar = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
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
            bar = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();

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
            strip = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
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
            line = go.transform;
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>();
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

    /// <summary>结算面板出现时全屏闪光过渡（胜利金色、失败暗红），叠在 Canvas 最顶层 0.45 秒。</summary>
    void SpawnGameOverFlash(bool win)
    {
        var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
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
        Time.timeScale = bPaused ? 0f : 1f;
    }

    bool IsOnlineMatch()
    {
        return GameNetworkSync.Instance != null && GameNetworkSync.Instance.IsNetworkGame;
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
        _settingsOpen = false;
        _settingsPausedTime = false;
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

    // 按类型查表，避免依赖 prefab 序列化的 DisplayName 字段
    public static string GetUnitDisplayNameStatic(RTSUnit unit)
    {
        if (unit is Infantry)     return "步兵";
        if (unit is Artillery)    return "炮兵";
        if (unit is Tank)         return "坦克";
        if (unit is Flamethrower) return "喷火车";
        if (unit is Fighter)      return "战斗机";
        if (unit is Bomber)       return "轰炸机";
        if (unit is ScoutPlane)   return "侦察机";
        // 兜底：Awake 里设置的值（运行时实例有效）
        return string.IsNullOrEmpty(unit.DisplayName) ? unit.GetType().Name : unit.DisplayName;
    }
}
