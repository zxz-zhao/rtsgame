using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }
    public const string InboxSyncNotificationMessage = "邮件：收件箱已同步，暂无未读邮件";
    public static readonly Vector2 ReturnBattleButtonAnchor = new Vector2(0.5f, 0.810f);
    public static readonly Vector2 ReturnBattleButtonSize = new Vector2(190f, 38f);

    [Header("Legacy Lobby Scaffold")]
    public bool EnableLegacyLobbyScaffolding = false;

    [Header("Top Bar")]
    public Text   PlayerNameText;
    public Text   PlayerLevelText;
    public Text   PlayerRankText;
    public Image  PlayerAvatarImage;
    public Text   GoldText;
    public Text   GemText;
    public Button GoldPlusBtn;
    public Button GemPlusBtn;
    public Button TechButton;
    public Button SettingsIconBtn;
    public Button NotifyBellBtn;
    public Button AddFriendIconBtn;

    [Header("Main Lobby")]
    public GameObject HallPanel;
    public Button QuickMatchButton;
    public Button GlobalConquestButton;
    public Button CustomRoomButton;
    public Text   MatchStatusText;

    [Header("好友（左侧）")]
    public GameObject FriendsDock;
    public GameObject FriendsSidebar;
    public Text       FriendListText;
    public Button[]   FriendClickButtons;
    public Button     ViewAllFriendsBtn;
    [System.NonSerialized] public InputField AddFriendInput;
    [System.NonSerialized] public Button     AddFriendButton;
    [System.NonSerialized] public Text       AddFriendStatus;

    [Header("右侧任务与科技")]
    public Text[]   TaskTitleTexts;
    public Text[]   TaskProgTexts;
    public Slider[] TaskProgSliders;
    public Button[] TaskClaimButtons;
    public Button   MoreTasksBtn;
    public Text     TechResearchNameText;
    public Text     TechResearchDescText;
    public Text     TechTimerBarText;
    public Slider   TechProgressSlider;
    public Button   TechSpeedBtn;
    public Button   TechStartBtn;
    public Button   TechTreeBtn;

    [Header("底部导航")]
    public Button NavShopBtn;
    public Button NavWarehouseBtn;
    public Button NavCampaignBtn;
    public Button NavRankBtn;
    public Button NavMailBtn;
    [Header("Runtime Button Sprites")]
    public Sprite RuntimeButtonPrimarySprite;
    public Sprite RuntimeButtonSecondarySprite;
    public Sprite RuntimeButtonNeutralSprite;
    public Sprite RuntimeButtonDangerSprite;
    public Sprite CampaignMatchRouteSprite;
    public Sprite CampaignCustomRouteSprite;
    public Sprite CampaignGlobalRouteSprite;
    [System.NonSerialized] public GameObject ShopPanel;
    [System.NonSerialized] public Button ShopBackBtn;
    public GameObject WarehousePanel;
    public Button WarehouseBackBtn;
    [System.NonSerialized] public GameObject CampaignPanel;
    [System.NonSerialized] public Button CampaignBackBtn;
    [System.NonSerialized] public GameObject RankPanel;
    [System.NonSerialized] public Button RankBackBtn;

    [Header("科技面板（模态）")]
    public GameObject TechPanel;
    public Text   TechPlayerName;
    public Text   TechWinsText;
    public Text   TechLossText;
    public Text   TechRateText;
    public Button TechCloseBtn;

    [Header("设置面板（右侧小模态）")]
    public GameObject SettingsPanel;
    public Slider MusicSlider;
    public Slider SFXSlider;
    public Button SettingsReturnBattleButton;
    public Button LogoutButton;
    public Button SettingsCloseBtn;

    [Header("地图匹配面板")]
    public GameObject MapPanel;
    public Button[]   MapButtons;
    public Button     StartMatchButton;
    public Text       MatchTimerText;
    public Button     BackToHallButton;

    [Header("匹配中覆盖层")]
    public GameObject MatchingOverlay;
    public Text   MatchingMapText;
    public Text   MatchingCountText;
    public Button CancelMatchButton;

    [Header("房间面板")]
    public GameObject RoomPanel;
    public Button   CreateRoomButton;
    public Button   RefreshRoomsButton;
    public Button   RoomBackButton;
    public Text     RoomStatusText;
    public Text[]   RoomRowTexts;
    public Button[] RoomJoinButtons;
    public Button[] RoomInviteButtons;

    [Header("邀请面板")]
    public GameObject InvitePanel;
    public Text[]     InviteFriendNames;
    public Button[]   InviteFriendButtons;
    public Button     InviteCloseBtn;

    [Header("创建房间弹窗")]
    public GameObject CreateRoomPanel;
    public InputField CRNameInput;
    public Text       CRSelectedMapText;
    public Button[]   CRMapButtons;
    public Button     CRConfirmBtn;
    public Button     CRCancelBtn;

    // 运行时状态
    bool   bMatching    = false;
    float  matchTimer   = 0f;
    float  pollTimer    = 0f;
    float  invitePollTimer = 0f;
    string selectedMap  = BattleMapCatalog.DefaultMapName;
    string _inviteRoomId = "";
    string[] _loadedFriends = new string[0];
    string _pendingInviteId  = "";
    string _pendingInviteRoomId = "";
    string _pendingInviteFrom = "";
    string _pendingInviteMapName = "";
    string _latestInboxNotification = "";
    string _crSelectedMap = BattleMapCatalog.DefaultMapName;
    Coroutine _waitRoomCoroutine = null;

    string[] _taskIds = new string[3] { "daily_login", "win3", "destroy20" };
    bool[] _taskClaimedToday = new bool[3];
    bool[] _taskCanClaimNow = new bool[3];
    long _techEndMs;
    int _techTotalSec;
    bool _hasActiveTech;
    float _presenceTimer;
    float _lobbyPollTimer;
    Sprite _runtimeLobbyDeskSprite;
    AudioSource _lobbyMusicSource;
    AudioClip _lobbyMusicClip;
    LobbyUIToolkitController _uiToolkit;
    GameObject[] _friendRows;
    Text[] _friendNameTexts;
    Text[] _friendMetaTexts;
    Text[] _friendStatusTexts;
    Text[] _friendLevelTexts;
    Image[] _friendAvatarImages;
    Button[] _friendClickButtons;
    string[] _friendNames;
    string[] _friendMetas;
    string[] _friendStatuses;
    int[] _friendLevels;
    RawImage _sceneRankBadgeRawImage;
    Image _sceneRankBadgeImage;
    Text _sceneCommanderNameText;
    Text _sceneCommanderLevelText;
    Text _sceneRankBadgeNameText;
    readonly Dictionary<string, Sprite> _runtimeRankBadgeSprites = new Dictionary<string, Sprite>(12);
    Button _returnBattleButton;
    GameObject _friendView;
    RectTransform _friendViewViewport;
    RectTransform _friendViewContent;
    ScrollRect _friendViewScrollRect;
    Scrollbar _friendViewScrollbar;
    readonly List<GameObject> _friendViewRows = new List<GameObject>();
    const float FriendViewRowHeight = 64f;
    const float FriendViewRowSpacing = 7f;
    const float FriendViewRowPadding = 4f;
    static readonly Vector2 LobbyLayoutReferenceResolution = new Vector2(1280f, 720f);
    int _lastLayoutScreenWidth = -1;
    int _lastLayoutScreenHeight = -1;
    Rect _lastLayoutSafeArea = new Rect(-1f, -1f, -1f, -1f);
    static readonly Dictionary<string, Sprite> RuntimeResourceSpriteCache = new Dictionary<string, Sprite>(64);
    static readonly (string name, string rank, string status, int level)[] FriendListTestData =
    {
        ("IronWolf", "黄金指挥官", "在线", 18),
        ("BlueHawk", "白银突击队", "匹配中", 14),
        ("RedFox", "装甲先锋", "在线", 12),
        ("NightOwl", "侦察队长", "离线", 9),
        ("SteelRain", "炮兵专家", "游戏中", 21),
        ("FalconNine", "补给官", "在线", 16),
        ("TigerAce", "王牌车长", "离线", 24),
        ("SnowBear", "防线军士", "在线", 11),
    };
    static readonly (string resource, string displayName, int minLevel, string[] tokens)[] RankBadgeCatalog =
    {
        ("rank_badge_10_marksman_rifle", "神射军徽", 50, new[] { "最强", "王者", "王牌", "神射" }),
        ("rank_badge_05_marshal_eagle", "鹰徽元帅", 35, new[] { "元帅" }),
        ("rank_badge_09_heavy_mg", "重机枪军徽", 30, new[] { "钻石" }),
        ("rank_badge_08_crossed_pistols", "双枪军徽", 25, new[] { "上校" }),
        ("rank_badge_07_crossed_smg", "冲锋军徽", 20, new[] { "黄金" }),
        ("rank_badge_06_crossed_rifles", "步枪军徽", 15, new[] { "白银" }),
        ("rank_badge_04_major_crossed_sabers", "少校军徽", 10, new[] { "少校" }),
        ("rank_badge_03_gold_wing_star", "金翼星徽", 8, new[] { "中尉", "尉" }),
        ("rank_badge_02_silver_double_star", "银星军徽", 4, new[] { "下士", "中士", "士官" }),
        ("rank_badge_01_bronze_shield", "青铜盾徽", 1, new[] { "列兵", "新兵", "青铜" }),
    };
    const float TwoPi = 6.283185307179586f;

    /// <summary>从 Resources/LobbyGen 取精灵（与 LobbySceneBuilder 生成的 PNG 同名）。如果场景里误绑了其他贴图，这里会统一纠正。</summary>
    static class LobbyGenRes
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(32);

        public static Sprite Get(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return null;
            baseName = baseName.Trim().Replace('\\', '/');
            if (baseName.StartsWith("LobbyGen/", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring("LobbyGen/".Length);

            if (Cache.TryGetValue(baseName, out var cached) && cached != null) return cached;

            string path = "LobbyGen/" + baseName;
            Sprite s = Resources.Load<Sprite>(path);
            if (s == null)
            {
                foreach (var o in Resources.LoadAll(path))
                {
                    if (o is Sprite sp) { s = sp; break; }
                }
            }
            if (s != null) Cache[baseName] = s;
            return s;
        }
    }

    /// <summary>运行时读取下载的 Kenney 科幻 UI 贴图，避免匹配面板继续使用手绘按钮。</summary>
    static class KenneyUiRes
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(16);
        static readonly Vector4 DefaultBorder = new Vector4(14f, 14f, 14f, 14f);

        public static Sprite Get(string baseName) => Get(baseName, DefaultBorder);

        public static Sprite Get(string baseName, Vector4 border)
        {
            if (string.IsNullOrEmpty(baseName)) return null;

            string key = baseName + "_" + border;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            string path = "UI/KenneySpace/" + baseName;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                foreach (var o in Resources.LoadAll(path))
                {
                    if (o is Sprite sp) { sprite = sp; break; }
                }
            }

            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    sprite = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect,
                        border);
                    sprite.name = "kenney_" + baseName;
                }
            }

            if (sprite != null) Cache[key] = sprite;
            return sprite;
        }
    }

    static class GeneratedButtonRes
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(8);

        public static Sprite Get(string baseName, Vector4 border)
        {
            if (string.IsNullOrEmpty(baseName)) return null;
            string key = baseName + "_" + border;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            string path = "UI/GeneratedButtons/" + baseName;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    sprite = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect,
                        border);
                    sprite.name = baseName;
                }
            }

            if (sprite != null) Cache[key] = sprite;
            return sprite;
        }
    }

    static readonly string[] LobbyBackgroundResourceCandidates = new[]
    {
        "LobbyGen/user_lobby_background",
        "LobbyGen/custom_lobby_background",
        "LobbyGen/ref_lobby_desk_no_bottom_map"
    };

    static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static Transform FindFirstNamedChild(Transform root, params string[] names)
    {
        if (root == null || names == null) return null;
        for (int i = 0; i < names.Length; i++)
        {
            var child = FindDeepChild(root, names[i]);
            if (child != null) return child;
        }
        return null;
    }

    static bool HasSceneAuthoredTaskPanel(Transform root)
    {
        var taskPanel = FindDeepChild(root, "renwu");
        if (taskPanel == null) return false;

        return FindDeepChild(taskPanel, "TaskSlotPanel0") is RectTransform
            && FindDeepChild(taskPanel, "TaskSlotPanel1") is RectTransform
            && FindDeepChild(taskPanel, "TaskSlotPanel2") is RectTransform;
    }

    static bool HasSceneAuthoredTechPanel(Transform root)
    {
        var techPanel = FindDeepChild(root, "keji");
        if (techPanel == null) return false;

        return FindDeepChild(techPanel, "TechSlotPanel0") is RectTransform
            || FindDeepChild(techPanel, "TechResearchName") is RectTransform
            || FindDeepChild(techPanel, "TechProgressSlider") is RectTransform;
    }

    static bool HasSceneAuthoredMainLobbyLayout(Transform root)
    {
        if (root == null) return false;

        bool hasModeButtons =
            FindDeepChild(root, "QuickMatchButton")?.GetComponent<Button>() != null
            && FindDeepChild(root, "CustomRoomButton")?.GetComponent<Button>() != null
            && FindDeepChild(root, "GlobalConquestButton")?.GetComponent<Button>() != null;
        if (hasModeButtons) return true;

        bool hasModeCards =
            FindDeepChild(root, "CardMatch") is RectTransform
            && FindDeepChild(root, "CardCustom") is RectTransform
            && FindDeepChild(root, "CardGlobal") is RectTransform;
        return hasModeCards;
    }

    static void SetImageSpriteIfLoaded(Transform root, string nodeName, string lobbyGenBaseName)
    {
        var tr = FindDeepChild(root, nodeName);
        if (tr == null) return;
        var img = tr.GetComponent<Image>();
        if (img == null) return;
        var sp = LobbyGenRes.Get(lobbyGenBaseName);
        if (sp == null) return;
        img.sprite = sp;
        img.preserveAspect = true;
        img.color = Color.white;
    }

    static void SetChildIconSprite(Transform root, string parentName, string childName, string lobbyGenBaseName)
    {
        var p = FindDeepChild(root, parentName);
        if (p == null) return;
        var ch = p.Find(childName);
        if (ch == null) return;
        var img = ch.GetComponent<Image>();
        if (img == null) return;
        var sp = LobbyGenRes.Get(lobbyGenBaseName);
        if (sp == null) return;
        img.sprite = sp;
        img.preserveAspect = true;
        if (parentName != null && parentName.StartsWith("Nav", StringComparison.Ordinal))
            img.color = new Color(1f, 0.95f, 0.78f, 1f);
        else
            img.color = Color.white;
    }

    static void SetDeepText(Transform root, string nodeName, string value)
    {
        var tr = FindDeepChild(root, nodeName);
        if (tr == null) return;
        var txt = tr.GetComponent<Text>();
        if (txt != null) txt.text = value;
    }

    static void SetButtonLabel(Transform root, string buttonName, string value)
    {
        var tr = FindDeepChild(root, buttonName);
        if (tr == null) return;
        var txt = tr.GetComponentInChildren<Text>(true);
        if (txt != null) txt.text = value;
    }

    static Color GetLobbyFriendStatusColor(string status)
    {
        string lower = string.IsNullOrEmpty(status) ? "" : status.ToLowerInvariant();
        if (lower.Contains("off") || lower.Contains("离线"))
            return new Color(0.66f, 0.70f, 0.74f, 1f);
        if (lower.Contains("game") || lower.Contains("match") || lower.Contains("busy") || lower.Contains("游戏"))
            return new Color(1f, 0.76f, 0.32f, 1f);
        return new Color(0.44f, 0.92f, 0.58f, 1f);
    }

    static void ApplyMissionSliderSkin(Transform root, string sliderName)
    {
        var tr = FindDeepChild(root, sliderName);
        if (tr == null) return;
        var slider = tr.GetComponent<Slider>();
        if (slider != null) slider.interactable = false;

        var bg = tr.Find("Background")?.GetComponent<Image>();
        var bgSp = LobbyGenRes.Get("gen_progress_track");
        if (bg != null && bgSp != null)
        {
            bg.sprite = bgSp;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;
        }

        var fill = tr.Find("Fill Area/Fill")?.GetComponent<Image>();
        var fillSp = LobbyGenRes.Get("gen_progress_fill");
        if (fill != null && fillSp != null)
        {
            fill.sprite = fillSp;
            fill.type = Image.Type.Sliced;
            fill.color = Color.white;
            fill.raycastTarget = false;
        }

        var fillArea = tr.Find("Fill Area") as RectTransform;
        if (fillArea != null)
        {
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(3f, 3f);
            fillArea.offsetMax = new Vector2(-3f, -3f);
        }
    }

    static void ApplyMissionCardSkin(Transform root)
    {
        MakeNodeTransparent(root, "TaskMissionCard");
        MakeNodeTransparent(root, "TechMissionCard");
        for (int i = 0; i < 3; i++)
        {
            MakeNodeTransparent(root, "TaskRow" + i);
            SkinMissionButton(root, "TaskClaim" + i, "gen_exact_claim_button_soft",
                Color.white, new Color(1f, 0.94f, 0.66f, 1f));
        }
        MakeNodeTransparent(root, "TechSummaryBox");
        SkinMissionButton(root, "TechButton", "gen_button_dark",
            new Color(0f, 0f, 0f, 0.004f), new Color(1f, 0.86f, 0.42f, 1f));
        SkinMissionButton(root, "MoreTasksBtn", "gen_button_dark",
            new Color(0f, 0f, 0f, 0.004f), new Color(0.98f, 0.96f, 0.78f, 1f));
        SkinMissionButton(root, "TechTreeBtn", "gen_button_dark",
            new Color(0f, 0f, 0f, 0.004f), new Color(0.98f, 0.96f, 0.78f, 1f));
        SkinMissionButton(root, "TechSpeedBtn", "gen_button_gold",
            new Color(0f, 0f, 0f, 0.004f), new Color(1f, 0.94f, 0.66f, 1f));
        SkinMissionButton(root, "TechStartBtn", "gen_button_dark",
            new Color(0f, 0f, 0f, 0.004f), new Color(0.82f, 1f, 0.86f, 1f));
        SetImageSpriteIfLoaded(root, "TaskIcon0", "gen_icon_task_crate");
        SetImageSpriteIfLoaded(root, "TechBlueprintIcon", "gen_icon_tech_blueprint");
        for (int i = 0; i < 3; i++)
            ApplyMissionSliderSkin(root, "TaskSlider" + i);
        ApplyMissionSliderSkin(root, "TechProgressSlider");

        SetDeepText(root, "TaskSectionTitle", "任务");
        SetDeepText(root, "TechSectionTitle", "科技");
        SetButtonLabel(root, "MoreTasksBtn", "查看全部任务");
        SetButtonLabel(root, "TechTreeBtn", "查看科技树");
        SetButtonLabel(root, "TechSpeedBtn", "加速");
        SetButtonLabel(root, "TechStartBtn", "研究");
    }

    static void SkinMissionButton(Transform root, string nodeName, string spriteName, Color tint, Color labelColor)
    {
        var tr = FindDeepChild(root, nodeName);
        if (tr == null) return;

        var img = tr.GetComponent<Image>();
        var sp = LobbyGenRes.Get(spriteName);
        bool useExactClaimArt = spriteName == "gen_exact_claim_button_soft";
        if (img != null && tint.a <= 0.01f)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = tint;
        }
        else if (img != null && sp != null)
        {
            img.sprite = sp;
            img.type = useExactClaimArt ? Image.Type.Simple : Image.Type.Sliced;
            img.preserveAspect = false;
            img.color = tint;
        }

        if (useExactClaimArt && tr is RectTransform rt)
            rt.sizeDelta = new Vector2(68f, 28f);

        var btn = tr.GetComponent<Button>();
        if (btn != null)
        {
            var colors = btn.colors;
            colors.normalColor = tint;
            colors.highlightedColor = Color.Lerp(tint, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(tint, Color.black, 0.30f);
            colors.selectedColor = Color.Lerp(tint, Color.white, 0.08f);
            colors.disabledColor = new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f, tint.a * 0.62f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
        }

        var label = tr.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.fontStyle = FontStyle.Bold;
            label.color = labelColor;
        }
    }

    static void MakeNodeTransparent(Transform root, string nodeName)
    {
        var tr = FindDeepChild(root, nodeName);
        if (tr == null) return;
        var img = tr.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
        }
    }

    static void ApplySlicedSprite(Transform root, string nodeName, string lobbyGenBaseName)
    {
        ApplySlicedSprite(root, nodeName, lobbyGenBaseName, Color.white);
    }

    static void ApplySlicedSprite(Transform root, string nodeName, string lobbyGenBaseName, Color tint)
    {
        var tr = FindDeepChild(root, nodeName);
        if (tr == null) return;
        var img = tr.GetComponent<Image>();
        var sp = LobbyGenRes.Get(lobbyGenBaseName);
        if (img == null || sp == null) return;
        img.sprite = sp;
        img.type = Image.Type.Sliced;
        img.preserveAspect = false;
        img.color = tint;
    }

    static void ApplyPremiumNavButton(Transform root, string buttonName, string labelText, string iconRes)
    {
        var p = FindDeepChild(root, buttonName);
        if (p == null) return;

        var bg = p.GetComponent<Image>();
        if (IsExactNavSprite(bg))
            return;
        var bgSprite = LobbyGenRes.Get("gen_nav_wh_button");
        if (bg != null && bgSprite != null)
        {
            bg.sprite = bgSprite;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }

        var rt = p.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(172f, 54f);
            if (buttonName != "NavCampaignBtn")
                rt.anchoredPosition = Vector2.zero;
        }

        var icon = p.Find("NavIcon") as RectTransform;
        if (icon != null)
        {
            icon.anchorMin = new Vector2(0.07f, 0.10f);
            icon.anchorMax = new Vector2(0.42f, 0.92f);
            icon.offsetMin = Vector2.zero;
            icon.offsetMax = Vector2.zero;
            var img = icon.GetComponent<Image>();
            var sp = LobbyGenRes.Get(iconRes);
            if (img != null && sp != null)
            {
                img.sprite = sp;
                img.preserveAspect = true;
                img.color = new Color(1f, 0.95f, 0.78f, 1f);
            }
        }

        var label = p.Find("Text")?.GetComponent<Text>();
        if (label != null)
        {
            var lrt = label.GetComponent<RectTransform>();
            if (lrt != null)
            {
                lrt.anchorMin = new Vector2(0.38f, 0.04f);
                lrt.anchorMax = new Vector2(0.98f, 0.96f);
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
            }
            label.text = labelText;
            label.fontSize = labelText.Length >= 3 ? 21 : 24;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.84f, 0.46f, 1f);
            if (label.GetComponent<Outline>() == null)
            {
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.18f, 0.1f, 0.02f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }
    }

    static bool IsExactNavSprite(Image image)
    {
        return image != null
            && image.sprite != null
            && image.sprite.name.StartsWith("gen_exact_nav_", StringComparison.Ordinal);
    }

    static void ApplyWarehouseNavButton(Transform root)
    {
        ApplyPremiumNavButton(root, "NavWarehouseBtn", "仓库", "gen_nav_wh");
    }

    static void RepairVisibleLobbyText(Transform root)
    {
        SetDeepText(root, "MapTitle", "选择地图");
        SetButtonLabel(root, "BackToHallButton", "返回");
        SetButtonLabel(root, "StartMatchButton", "开始匹配");
        string[] maps = { "A\n沙漠绿洲", "B\n冰雪要塞", "C\n丛林战场", "D\n城市废墟" };
        for (int i = 0; i < maps.Length; i++)
            SetButtonLabel(root, "MapButton" + i, maps[i]);

        SetButtonLabel(root, "RoomBackButton", "返回大厅");
        SetDeepText(root, "RoomPanelTitle", "自定义房间");
        SetButtonLabel(root, "RefreshRoomsButton", "刷新");
        SetButtonLabel(root, "CreateRoomButton", "创建");
        SetDeepText(root, "HeaderName", "房间名称 / 地图");
        SetDeepText(root, "HeaderPlayers", "人数");
        SetDeepText(root, "HeaderStatus", "状态");
        for (int i = 0; i < 4; i++)
        {
            SetDeepText(root, "RoomIcon" + i, "房");
            SetDeepText(root, "RoomStatusText" + i, "等待中");
            SetButtonLabel(root, "RoomJoinButton" + i, "加入");
            SetButtonLabel(root, "RoomInviteButton" + i, "邀请");
        }

        SetDeepText(root, "InvitePanelTitle", "邀请好友");
        for (int i = 0; i < 6; i++)
            SetButtonLabel(root, "InviteFriendBtn" + i, "邀请");
        SetButtonLabel(root, "InviteCloseBtn", "关闭");

        SetDeepText(root, "CreateRoomTitle", "创建自定义房间");
        SetDeepText(root, "CRNameLabel", "房间名称");
        SetDeepText(root, "CRMapLabel", "选择地图");
        string[] crMaps = { "沙漠绿洲", "冰雪要塞", "丛林战场", "城市废墟" };
        for (int i = 0; i < crMaps.Length; i++)
            SetButtonLabel(root, "CRMapBtn" + i, crMaps[i]);
        SetDeepText(root, "CRSelectedMap", "已选：沙漠绿洲");
        SetButtonLabel(root, "CRConfirmBtn", "确认创建");
        SetButtonLabel(root, "CRCancelBtn", "取消");

        SetButtonLabel(root, "TechCloseBtn", "关闭");
        SetDeepText(root, "TechIntroText", "勇者无惧，星火燎原 · 战略大师");
        SetDeepText(root, "TechWinsBoxLabel", "胜场");
        SetDeepText(root, "TechLossBoxLabel", "负场");
        SetDeepText(root, "TechRateBoxLabel", "胜率");
        SetDeepText(root, "TechDetailTitle", "科技使用详情");
        SetDeepText(root, "TechDetailText", "【初级兵营】训练步兵 x 12     【高级防御】部署炮台 x 5\n【快速增援】使用 x 3          【战场侦察】使用 x 8\n科技解锁记录会显示在这里。");

        SetDeepText(root, "SettingsTitle", "设置");
        SetButtonLabel(root, "SettingsCloseBtn", "关闭");
        SetDeepText(root, "MusicLabel", "音乐");
        SetDeepText(root, "SFXLabel", "音效");
        SetButtonLabel(root, "SettingsReturnBattleButton", "返回战场");
        SetButtonLabel(root, "ReturnBattleButton", "返回战场");
    }

    static Text CreateRuntimeText(Transform parent, string name, string text, Vector2 anchor, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = Mathf.Max(8, fontSize - 6);
        t.resizeTextMaxSize = fontSize;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Image CreateRuntimeImage(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size, Color color, bool sliced = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = !sliced;
        img.color = color;
        return img;
    }

    static Image EnsureRuntimeImageNode(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 size, Color color, bool sliced = false)
    {
        var tr = parent != null ? parent.Find(name) : null;
        GameObject go;
        RectTransform rt;
        if (tr == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            rt = go.AddComponent<RectTransform>();
        }
        else
        {
            go = tr.gameObject;
            rt = tr as RectTransform;
            if (rt == null) rt = go.AddComponent<RectTransform>();
            tr.SetParent(parent, false);
        }

        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = !sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text EnsureRuntimeTextNode(Transform parent, string name, string text, Vector2 anchor, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        var tr = parent != null ? parent.Find(name) : null;
        GameObject go;
        RectTransform rt;
        if (tr == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            rt = go.AddComponent<RectTransform>();
        }
        else
        {
            go = tr.gameObject;
            rt = tr as RectTransform;
            if (rt == null) rt = go.AddComponent<RectTransform>();
            tr.SetParent(parent, false);
        }

        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var label = go.GetComponent<Text>();
        if (label == null) label = go.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = alignment;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(8, fontSize - 8);
        label.resizeTextMaxSize = fontSize;
        label.raycastTarget = false;

        var shadow = go.GetComponent<Shadow>();
        if (shadow == null) shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.6f, -1.6f);
        return label;
    }

    Button CreateRuntimeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 size)
    {
        EnsureRuntimeButtonSprites();
        var sprite = RuntimeButtonNeutralSprite
            ?? RuntimeButtonSecondarySprite;
        var img = CreateRuntimeImage(parent, name, sprite, anchor, size, sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.004f), false);
        var btn = img.gameObject.AddComponent<Button>();
        var label = CreateRuntimeText(img.transform, "Text", text, new Vector2(0.5f, 0.5f), size, 18, new Color(0.96f, 0.92f, 0.74f, 1f));
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return btn;
    }

    static bool HasSpriteBorder(Sprite sprite)
    {
        return sprite != null && sprite.border.sqrMagnitude > 0.01f;
    }

    Sprite ResolveRuntimeButtonSkinSprite(string spriteName)
    {
        EnsureRuntimeButtonSprites();
        Sprite assignedSprite = null;
        switch (spriteName)
        {
            case "button_yellow_header":
                assignedSprite = RuntimeButtonPrimarySprite;
                break;
            case "button_blue_header":
            case "button_neutral_depth":
                assignedSprite = RuntimeButtonSecondarySprite;
                break;
            case "button_red_header":
                assignedSprite = RuntimeButtonDangerSprite;
                break;
            default:
                assignedSprite = RuntimeButtonNeutralSprite;
                break;
        }

        if (assignedSprite != null) return assignedSprite;
        var kenneySprite = KenneyUiRes.Get(spriteName, new Vector4(14f, 14f, 14f, 14f));
        return kenneySprite
            ?? RuntimeButtonNeutralSprite
            ?? RuntimeButtonSecondarySprite
            ?? RuntimeButtonPrimarySprite;
    }

    void ApplyRuntimeButtonSkin(Button button, string spriteName, Color tint, Color labelColor)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        Sprite sprite = null;
        if (image != null)
        {
            sprite = ResolveRuntimeButtonSkinSprite(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
            }
            image.type = HasSpriteBorder(image.sprite) ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.color = image.sprite != null ? tint : new Color(tint.r, tint.g, tint.b, Mathf.Min(tint.a, 0.004f));
        }

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            if (sprite != null && sprite.name == "gen_button_dark")
                label.color = new Color(0.96f, 0.92f, 0.74f, 1f);
            else
                label.color = labelColor;
            label.fontStyle = FontStyle.Bold;
        }
    }

    void StartLobbyMusic()
    {
        if (_lobbyMusicSource != null && _lobbyMusicSource.isPlaying) return;

        var go = GameObject.Find("LobbyBGM");
        if (go == null)
        {
            go = new GameObject("LobbyBGM");
        }

        _lobbyMusicSource = go.GetComponent<AudioSource>();
        if (_lobbyMusicSource == null)
            _lobbyMusicSource = go.AddComponent<AudioSource>();

        _lobbyMusicSource.playOnAwake = false;
        _lobbyMusicSource.loop = true;
        _lobbyMusicSource.spatialBlend = 0f;
        _lobbyMusicSource.volume = 0.42f;
        _lobbyMusicSource.priority = 96;

        if (_lobbyMusicClip == null)
            _lobbyMusicClip = CreateLobbyMarchClip();
        _lobbyMusicSource.clip = _lobbyMusicClip;
        if (!_lobbyMusicSource.isPlaying)
            _lobbyMusicSource.Play();
    }

    AudioClip CreateLobbyMarchClip()
    {
        const int sampleRate = 44100;
        const int channels = 2;
        const float seconds = 16f;
        const float tempo = 116f;
        int frames = Mathf.RoundToInt(sampleRate * seconds);
        float[] data = new float[frames * channels];
        float beatDur = 60f / tempo;

        int[] melody = { 50, 55, 57, 62, 60, 57, 55, 53, 50, 53, 55, 57, 62, 60, 57, 55 };
        int[] bass = { 38, 38, 43, 43, 45, 45, 41, 41 };

        for (int i = 0; i < frames; i++)
        {
            float t = i / (float)sampleRate;
            float beat = t / beatDur;
            int beatIndex = Mathf.FloorToInt(beat);
            float beatPhase = beat - beatIndex;
            int eighth = Mathf.FloorToInt(beat * 2f);
            float eighthPhase = beat * 2f - eighth;

            float brass = MarchNote(t, beat, melody[beatIndex % melody.Length], 0.74f, 0.18f);
            brass += 0.42f * MarchNote(t, beat, melody[(beatIndex + 4) % melody.Length] - 12, 0.72f, 0.10f);
            brass += 0.24f * MarchNote(t, beat, melody[(beatIndex + 8) % melody.Length] - 7, 0.70f, 0.10f);

            float bassTone = 0.38f * MarchBass(t, beat, bass[(beatIndex / 2) % bass.Length]);
            float kick = MarchKick(beatPhase, beatIndex % 2 == 0);
            float snare = MarchSnare(beatPhase, beatIndex % 2 == 1, i);
            float hat = MarchHat(eighthPhase, i);
            float roll = MarchRoll(beat, i);

            float sample = brass * 0.28f + bassTone * 0.32f + kick * 0.42f + snare * 0.30f + hat * 0.055f + roll * 0.08f;
            sample = Mathf.Clamp(sample, -0.82f, 0.82f);

            float pan = Mathf.Sin(t * 0.55f) * 0.10f;
            data[i * channels] = sample * (0.92f - pan);
            data[i * channels + 1] = sample * (0.92f + pan);
        }

        var clip = AudioClip.Create("Original_Wartime_March_Lobby_Loop", frames, channels, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float MarchNote(float t, float beat, int midi, float length, float detune)
    {
        float local = beat - Mathf.Floor(beat);
        if (local > length) return 0f;
        float freq = MidiToFreq(midi);
        float env = Mathf.Clamp01(local / 0.055f) * Mathf.Exp(-local * 2.2f);
        float vibrato = 1f + Mathf.Sin(t * 18f) * 0.003f;
        float sawA = Saw(t * freq * vibrato);
        float sawB = Saw(t * freq * (1f + detune * 0.01f));
        float square = Mathf.Sign(Mathf.Sin(TwoPi * freq * 0.5f * t)) * 0.22f;
        return (sawA * 0.55f + sawB * 0.35f + square) * env;
    }

    static float MarchBass(float t, float beat, int midi)
    {
        float local = beat - Mathf.Floor(beat);
        if (local > 0.45f) return 0f;
        float freq = MidiToFreq(midi);
        float env = Mathf.Clamp01(local / 0.035f) * Mathf.Exp(-local * 4.4f);
        return Mathf.Sin(TwoPi * freq * t) * env;
    }

    static float MarchKick(float phase, bool active)
    {
        if (!active || phase > 0.22f) return 0f;
        float env = Mathf.Exp(-phase * 18f);
        float freq = Mathf.Lerp(92f, 42f, Mathf.Clamp01(phase / 0.22f));
        return Mathf.Sin(TwoPi * freq * phase * 0.22f) * env;
    }

    static float MarchSnare(float phase, bool active, int sampleIndex)
    {
        if (!active || phase > 0.28f) return 0f;
        float env = Mathf.Exp(-phase * 14f);
        return HashNoise(sampleIndex) * env;
    }

    static float MarchHat(float phase, int sampleIndex)
    {
        if (phase > 0.12f) return 0f;
        float env = Mathf.Exp(-phase * 36f);
        return HashNoise(sampleIndex * 7 + 19) * env;
    }

    static float MarchRoll(float beat, int sampleIndex)
    {
        float barBeat = beat % 8f;
        if (barBeat < 7.25f) return 0f;
        float phase = (barBeat - 7.25f) * 8f;
        float hit = phase - Mathf.Floor(phase);
        float env = Mathf.Exp(-hit * 9f) * Mathf.InverseLerp(7.25f, 8f, barBeat);
        return HashNoise(sampleIndex * 11 + 5) * env;
    }

    static float MidiToFreq(int midi)
        => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static float Saw(float x)
        => 2f * (x - Mathf.Floor(x + 0.5f));

    static float HashNoise(int x)
    {
        unchecked
        {
            x = (x << 13) ^ x;
            int n = (x * (x * x * 15731 + 789221) + 1376312589);
            return 1f - ((n & 0x7fffffff) / 1073741824f);
        }
    }

    void EnsureReferenceDynamicOverlay()
    {
        Transform canvas = HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
        if (canvas == null) return;

        if (HasRealHallChrome(canvas))
        {
            var existingRefSkin = FindDeepChild(canvas, "ReferenceLobbySkin");
            if (existingRefSkin != null)
                Destroy(existingRefSkin.gameObject);
            CleanupLegacyReferenceOverlay(canvas);
            return;
        }

        bool hasRealAvatar = FindDeepChild(canvas, "PortraitFace")?.GetComponent<Image>() != null;
        if (hasRealAvatar)
        {
            var staleAvatar = FindDeepChild(canvas, "DynamicPlayerAvatar");
            if (staleAvatar != null)
                Destroy(staleAvatar.gameObject);
        }

        var oldOverlay = FindDeepChild(canvas, "ReferenceDynamicOverlay");
        if (oldOverlay != null)
            Destroy(oldOverlay.gameObject);

        if (!NeedsReferenceDynamicOverlay())
        {
            CleanupLegacyReferenceOverlay(canvas);
            return;
        }

        var refSkin = FindDeepChild(canvas, "ReferenceLobbySkin");
        if (refSkin == null) return;

        if (FindDeepChild(refSkin, "DynamicGoldText") != null)
        {
            RetuneReferenceOverlayLayout(refSkin);
            BindReferenceDynamicOverlay(canvas);
            return;
        }

        var overlay = refSkin;

        var gold = CreateOverlayRuntimeText(overlay.transform, "DynamicGoldText", "0",
            new Vector2(0.260f, 0.944f), new Vector2(116f, 28f), 22, new Color(1f, 0.92f, 0.56f, 1f));
        gold.alignment = TextAnchor.MiddleCenter;
        var gem = CreateOverlayRuntimeText(overlay.transform, "DynamicGemText", "0",
            new Vector2(0.824f, 0.944f), new Vector2(116f, 28f), 22, new Color(0.74f, 0.96f, 1f, 1f));
        gem.alignment = TextAnchor.MiddleCenter;
        if (!hasRealAvatar)
        {
            var avatar = CreateRuntimeImage(overlay.transform, "DynamicPlayerAvatar", LobbyGenRes.Get("gen_avatar_player"),
                new Vector2(0.448f, 0.938f), new Vector2(46f, 46f), Color.white);
            avatar.raycastTarget = false;
        }

        CreateRuntimeHitButton(overlay.transform, "DynamicQuickMatchButton", new Vector2(0.500f, 0.711f), new Vector2(430f, 150f));
        CreateRuntimeHitButton(overlay.transform, "DynamicCustomRoomButton", new Vector2(0.500f, 0.473f), new Vector2(430f, 132f));
        CreateRuntimeHitButton(overlay.transform, "DynamicGlobalConquestButton", new Vector2(0.500f, 0.234f), new Vector2(430f, 132f));
        CreateOverlayRuntimeText(overlay.transform, "DynamicMatchStatusText", "",
            new Vector2(0.500f, 0.145f), new Vector2(520f, 28f), 15, new Color(1f, 0.78f, 0.28f, 1f));

        float[] taskY = { 0.748f, 0.647f, 0.546f };
        for (int i = 0; i < taskY.Length; i++)
        {
            var title = CreateOverlayRuntimeText(overlay.transform, "DynamicTaskTitle" + i, "",
                new Vector2(0.812f, taskY[i] + 0.018f), new Vector2(118f, 20f), 12, new Color(0.92f, 0.89f, 0.72f, 1f));
            title.alignment = TextAnchor.MiddleLeft;
            var prog = CreateOverlayRuntimeText(overlay.transform, "DynamicTaskProg" + i, "",
                new Vector2(0.865f, taskY[i] + 0.018f), new Vector2(44f, 20f), 12, new Color(0.50f, 0.95f, 0.62f, 1f));
            prog.alignment = TextAnchor.MiddleRight;
            CreateOverlayRuntimeSlider(overlay.transform, "DynamicTaskSlider" + i,
                new Vector2(0.846f, taskY[i] - 0.020f), new Vector2(112f, 8f), new Color(0.34f, 0.92f, 0.42f, 1f));
            CreateRuntimeHitButton(overlay.transform, "DynamicTaskClaim" + i,
                new Vector2(0.925f, taskY[i] - 0.002f), new Vector2(66f, 40f));
        }
        CreateRuntimeHitButton(overlay.transform, "DynamicMoreTasksBtn", new Vector2(0.852f, 0.438f), new Vector2(178f, 34f));

        var techName = CreateOverlayRuntimeText(overlay.transform, "DynamicTechResearchName", "",
            new Vector2(0.850f, 0.332f), new Vector2(150f, 22f), 13, new Color(0.92f, 0.89f, 0.72f, 1f));
        techName.alignment = TextAnchor.MiddleLeft;
        var techDesc = CreateOverlayRuntimeText(overlay.transform, "DynamicTechResearchDesc", "",
            new Vector2(0.850f, 0.300f), new Vector2(150f, 22f), 11, new Color(0.82f, 0.84f, 0.66f, 1f));
        techDesc.alignment = TextAnchor.MiddleLeft;
        var timer = CreateOverlayRuntimeText(overlay.transform, "DynamicTechTimerText", "",
            new Vector2(0.842f, 0.256f), new Vector2(82f, 20f), 12, new Color(0.50f, 0.95f, 0.62f, 1f));
        timer.alignment = TextAnchor.MiddleLeft;
        CreateOverlayRuntimeSlider(overlay.transform, "DynamicTechProgressSlider",
            new Vector2(0.846f, 0.239f), new Vector2(112f, 8f), new Color(0.34f, 0.92f, 0.42f, 1f));

        CreateRuntimeHitButton(overlay.transform, "DynamicTechButton", new Vector2(0.934f, 0.820f), new Vector2(84f, 42f));
        CreateRuntimeHitButton(overlay.transform, "DynamicTechSpeedBtn", new Vector2(0.895f, 0.257f), new Vector2(60f, 38f));
        CreateRuntimeHitButton(overlay.transform, "DynamicTechStartBtn", new Vector2(0.958f, 0.257f), new Vector2(60f, 38f));
        CreateRuntimeHitButton(overlay.transform, "DynamicTechTreeBtn", new Vector2(0.830f, 0.154f), new Vector2(226f, 42f));

        CreateRuntimeHitButton(overlay.transform, "DynamicNavShopBtn", new Vector2(0.095f, 0.046f), new Vector2(190f, 70f));
        CreateRuntimeHitButton(overlay.transform, "DynamicNavWarehouseBtn", new Vector2(0.303f, 0.046f), new Vector2(240f, 70f));
        CreateRuntimeHitButton(overlay.transform, "DynamicNavCampaignBtn", new Vector2(0.500f, 0.052f), new Vector2(164f, 82f));
        CreateRuntimeHitButton(overlay.transform, "DynamicNavRankBtn", new Vector2(0.704f, 0.046f), new Vector2(254f, 70f));
        CreateRuntimeHitButton(overlay.transform, "DynamicNavMailBtn", new Vector2(0.914f, 0.046f), new Vector2(176f, 70f));

        RetuneReferenceOverlayLayout(overlay);
        BindReferenceDynamicOverlay(canvas);
    }

    void RetuneReferenceOverlayLayout(Transform overlay)
    {
        if (overlay == null) return;

        ConfigureOverlayText(FindDeepChild(overlay, "DynamicGoldText")?.GetComponent<Text>(),
            new Vector2(0.260f, 0.944f), new Vector2(116f, 28f), 22, TextAnchor.MiddleCenter);
        ConfigureOverlayText(FindDeepChild(overlay, "DynamicGemText")?.GetComponent<Text>(),
            new Vector2(0.824f, 0.944f), new Vector2(116f, 28f), 22, TextAnchor.MiddleCenter);

        float[] taskY = { 0.748f, 0.647f, 0.546f };
        for (int i = 0; i < taskY.Length; i++)
        {
            ConfigureOverlayText(FindDeepChild(overlay, "DynamicTaskTitle" + i)?.GetComponent<Text>(),
                new Vector2(0.812f, taskY[i] + 0.018f), new Vector2(118f, 20f), 12, TextAnchor.MiddleLeft);
            ConfigureOverlayText(FindDeepChild(overlay, "DynamicTaskProg" + i)?.GetComponent<Text>(),
                new Vector2(0.865f, taskY[i] + 0.018f), new Vector2(44f, 20f), 12, TextAnchor.MiddleRight);
            ConfigureOverlayRect(FindDeepChild(overlay, "DynamicTaskSlider" + i) as RectTransform,
                new Vector2(0.846f, taskY[i] - 0.020f), new Vector2(112f, 8f));
            ConfigureOverlayRect(FindDeepChild(overlay, "DynamicTaskClaim" + i) as RectTransform,
                new Vector2(0.925f, taskY[i] - 0.002f), new Vector2(66f, 40f));
        }

        ConfigureOverlayRect(FindDeepChild(overlay, "DynamicMoreTasksBtn") as RectTransform,
            new Vector2(0.852f, 0.438f), new Vector2(178f, 34f));
        ConfigureOverlayText(FindDeepChild(overlay, "DynamicTechResearchName")?.GetComponent<Text>(),
            new Vector2(0.850f, 0.332f), new Vector2(150f, 22f), 13, TextAnchor.MiddleLeft);
        ConfigureOverlayText(FindDeepChild(overlay, "DynamicTechResearchDesc")?.GetComponent<Text>(),
            new Vector2(0.850f, 0.300f), new Vector2(150f, 22f), 11, TextAnchor.MiddleLeft);
        ConfigureOverlayText(FindDeepChild(overlay, "DynamicTechTimerText")?.GetComponent<Text>(),
            new Vector2(0.842f, 0.256f), new Vector2(82f, 20f), 12, TextAnchor.MiddleLeft);
        ConfigureOverlayRect(FindDeepChild(overlay, "DynamicTechProgressSlider") as RectTransform,
            new Vector2(0.846f, 0.239f), new Vector2(112f, 8f));
        ConfigureOverlayRect(FindDeepChild(overlay, "DynamicTechSpeedBtn") as RectTransform,
            new Vector2(0.895f, 0.257f), new Vector2(60f, 38f));
        ConfigureOverlayRect(FindDeepChild(overlay, "DynamicTechStartBtn") as RectTransform,
            new Vector2(0.958f, 0.257f), new Vector2(60f, 38f));
    }

    void RetuneRealHallDataLayout(Transform root)
    {
        if (root == null) return;

        // 旧大厅场景里右侧任务/科技控件有些框过宽或仍允许 Overflow。
        // 这里运行时统一收进参考图右侧栏，避免服务端返回长中文、长数字后压到中央卡片。
        ConfigureOverlayText(FindDeepChild(root, "GoldCountText")?.GetComponent<Text>(),
            new Vector2(0.380f, 1.110f), new Vector2(132f, 30f), 22, TextAnchor.MiddleCenter);
        ConfigureOverlayText(FindDeepChild(root, "GemCountText")?.GetComponent<Text>(),
            new Vector2(0.350f, 1.110f), new Vector2(112f, 30f), 22, TextAnchor.MiddleCenter);

        if (!HasSceneAuthoredTaskPanel(root))
        {
            float[] taskY = { 0.700f, 0.460f, 0.220f };
            float[] fullTaskY = { 0.682f, 0.575f, 0.468f };
            for (int i = 0; i < taskY.Length; i++)
            {
                Transform row = FindDeepChild(root, "TaskRow" + i);
                Transform scope = row != null ? row : root;
                bool rowOnFullHall = row != null && row.parent != null
                    && (row.parent == root || row.parent.name == "HallPanel");
                if (row != null)
                {
                    var rowRt = row as RectTransform;
                    if (rowRt != null)
                    {
                        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = rowOnFullHall
                            ? new Vector2(0.852f, fullTaskY[i])
                            : new Vector2(0.5f, taskY[i]);
                        rowRt.anchoredPosition = Vector2.zero;
                        rowRt.sizeDelta = rowOnFullHall ? new Vector2(250f, 52f) : new Vector2(230f, 62f);
                    }
                }

                ConfigureOverlayText(FindDeepChild(scope, "TaskTitle" + i)?.GetComponent<Text>(),
                    rowOnFullHall ? new Vector2(0.350f, 0.760f) : new Vector2(0.480f, 0.660f),
                    rowOnFullHall ? new Vector2(116f, 18f) : new Vector2(100f, 18f), 12, TextAnchor.MiddleLeft);
                ConfigureOverlayText(FindDeepChild(scope, "TaskProg" + i)?.GetComponent<Text>(),
                    rowOnFullHall ? new Vector2(0.900f, 0.760f) : new Vector2(0.940f, 0.660f),
                    new Vector2(42f, 18f), 12, TextAnchor.MiddleRight);
                ConfigureOverlayRect(FindDeepChild(scope, "TaskSlider" + i) as RectTransform,
                    rowOnFullHall ? new Vector2(0.420f, 0.250f) : new Vector2(0.500f, 0.330f),
                    rowOnFullHall ? new Vector2(112f, 8f) : new Vector2(96f, 8f));
                ConfigureOverlayRect(FindDeepChild(scope, "TaskClaim" + i) as RectTransform,
                    rowOnFullHall ? new Vector2(0.960f, 0.120f) : new Vector2(0.940f, 0.120f),
                    rowOnFullHall ? new Vector2(56f, 24f) : new Vector2(56f, 24f));
            }

            RetuneRectWithFullHallFallback(root, "MoreTasksBtn",
                new Vector2(0.500f, 0.070f), new Vector2(196f, 30f),
                new Vector2(0.852f, 0.395f), new Vector2(180f, 34f));
        }

        if (!HasSceneAuthoredTechPanel(root))
        {
            RetuneTextWithFullHallFallback(root, "TechResearchName",
                new Vector2(0.595f, 0.700f), new Vector2(110f, 18f),
                new Vector2(0.852f, 0.385f), new Vector2(150f, 22f), 13, TextAnchor.MiddleLeft);
            RetuneTextWithFullHallFallback(root, "TechResearchDesc",
                new Vector2(0.595f, 0.490f), new Vector2(112f, 18f),
                new Vector2(0.852f, 0.358f), new Vector2(150f, 18f), 11, TextAnchor.MiddleLeft);
            RetuneTextWithFullHallFallback(root, "TechTimerText",
                new Vector2(0.485f, 0.200f), new Vector2(76f, 18f),
                new Vector2(0.835f, 0.328f), new Vector2(82f, 18f), 12, TextAnchor.MiddleLeft);
            RetuneTextWithFullHallFallback(root, "TechTimerBar",
                new Vector2(0.485f, 0.200f), new Vector2(76f, 18f),
                new Vector2(0.835f, 0.328f), new Vector2(82f, 18f), 12, TextAnchor.MiddleLeft);
            RetuneRectWithFullHallFallback(root, "TechProgressSlider",
                new Vector2(0.472f, 0.160f), new Vector2(76f, 8f),
                new Vector2(0.846f, 0.300f), new Vector2(112f, 6f));
            RetuneRectWithFullHallFallback(root, "TechSpeedBtn",
                new Vector2(0.800f, 0.380f), new Vector2(56f, 28f),
                new Vector2(0.895f, 0.292f), new Vector2(60f, 30f));
            RetuneRectWithFullHallFallback(root, "TechStartBtn",
                new Vector2(0.800f, 0.380f), new Vector2(56f, 28f),
                new Vector2(0.958f, 0.292f), new Vector2(60f, 30f));
            RetuneRectWithFullHallFallback(root, "TechTreeBtn",
                new Vector2(0.500f, 0.080f), new Vector2(196f, 30f),
                new Vector2(0.860f, 0.488f), new Vector2(210f, 44f));
        }
    }

    void EnsureCommanderProfileChrome(Transform root)
    {
        if (root == null) return;

        bool needsProfile = PlayerNameText == null || PlayerLevelText == null || PlayerRankText == null || PlayerAvatarImage == null;
        if (!needsProfile)
            return;

        var plateSprite = KenneyUiRes.Get("button_neutral_depth", new Vector4(18f, 18f, 18f, 18f))
            ?? LobbyGenRes.Get("gen_card_frame")
            ?? LobbyGenRes.Get("gen_panel_frame");
        var fadeSprite = LobbyGenRes.Get("gen_card_fade");
        var ringSprite = KenneyUiRes.Get("button_blue_header", new Vector4(14f, 14f, 14f, 14f))
            ?? LobbyGenRes.Get("gen_portrait_ring");
        var badgeSprite = KenneyUiRes.Get("button_yellow_header", new Vector4(14f, 14f, 14f, 14f))
            ?? LobbyGenRes.Get("gen_button_gold")
            ?? LobbyGenRes.Get("gen_button_dark");
        var rankSprite = LobbyGenRes.Get("gen_icon_star");
        var avatarSprite = LobbyGenRes.Get("gen_avatar_player");

        var plate = EnsureRuntimeImageNode(root, "RuntimeCommanderPlate",
            plateSprite, new Vector2(0.565f, 1.085f), new Vector2(372f, 92f), new Color(1f, 1f, 1f, 0.97f), true);
        plate.rectTransform.SetSiblingIndex(0);

        if (fadeSprite != null)
        {
            var fade = EnsureRuntimeImageNode(plate.transform, "RuntimeCommanderPlateFade",
                fadeSprite, new Vector2(0.5f, 0.5f), new Vector2(344f, 72f), new Color(1f, 1f, 1f, 0.34f), true);
            fade.rectTransform.SetAsFirstSibling();
        }

        EnsureRuntimeTextNode(plate.transform, "RuntimeCommanderTitle", "指挥官档案",
            new Vector2(0.48f, 0.84f), new Vector2(180f, 18f), 12,
            new Color(0.95f, 0.84f, 0.56f, 0.96f), TextAnchor.MiddleLeft);

        EnsureRuntimeImageNode(plate.transform, "RuntimeCommanderPortraitRing",
            ringSprite, new Vector2(0.14f, 0.50f), new Vector2(92f, 92f), Color.white);
        var avatar = EnsureRuntimeImageNode(plate.transform, "RuntimeCommanderAvatar",
            avatarSprite, new Vector2(0.14f, 0.50f), new Vector2(72f, 72f), Color.white);
        avatar.preserveAspect = true;

        EnsureRuntimeImageNode(plate.transform, "RuntimeCommanderRankIcon",
            rankSprite, new Vector2(0.40f, 0.34f), new Vector2(18f, 18f), Color.white);
        EnsureRuntimeImageNode(plate.transform, "RuntimeCommanderLevelBadge",
            badgeSprite, new Vector2(0.84f, 0.33f), new Vector2(72f, 24f), new Color(1f, 1f, 1f, 0.96f), true);

        PlayerNameText = EnsureRuntimeTextNode(plate.transform, "RuntimeCommanderName", "Commander",
            new Vector2(0.56f, 0.60f), new Vector2(210f, 28f), 23,
            new Color(1f, 0.96f, 0.86f, 1f), TextAnchor.MiddleLeft);
        PlayerRankText = EnsureRuntimeTextNode(plate.transform, "RuntimeCommanderRank", "--",
            new Vector2(0.58f, 0.33f), new Vector2(176f, 20f), 14,
            new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft);
        PlayerLevelText = EnsureRuntimeTextNode(plate.transform, "RuntimeCommanderLevel", "1",
            new Vector2(0.84f, 0.33f), new Vector2(56f, 20f), 15,
            new Color(0.18f, 0.12f, 0.04f, 1f), TextAnchor.MiddleCenter);
        PlayerAvatarImage = avatar;
    }

    void EnsureFriendPanelChrome(Transform root)
    {
        if (root == null) return;

        var panel = EnsureRuntimeImageNode(root, "RuntimeFriendsPanelCard",
            KenneyUiRes.Get("button_neutral_depth", new Vector4(18f, 18f, 18f, 18f)) ?? LobbyGenRes.Get("gen_panel_frame"), new Vector2(0.125f, 0.530f),
            new Vector2(256f, 560f), new Color(1f, 1f, 1f, 0.98f), true);
        panel.rectTransform.SetSiblingIndex(0);

        EnsureRuntimeImageNode(panel.transform, "RuntimeFriendsPanelIcon",
            LobbyGenRes.Get("gen_icon_helmet"), new Vector2(0.13f, 0.93f),
            new Vector2(28f, 28f), Color.white);
        EnsureRuntimeTextNode(panel.transform, "RuntimeFriendsPanelTitle", "好友通讯",
            new Vector2(0.44f, 0.93f), new Vector2(140f, 24f), 19,
            new Color(1f, 0.94f, 0.74f, 1f), TextAnchor.MiddleLeft);
        EnsureRuntimeTextNode(panel.transform, "RuntimeFriendsPanelSubtitle", "在线小队 / 盟友动态",
            new Vector2(0.50f, 0.88f), new Vector2(176f, 18f), 11,
            new Color(0.82f, 0.88f, 0.80f, 0.94f), TextAnchor.MiddleCenter);

        BindFriendRowViews();
        if (_friendRows == null) return;

        for (int i = 0; i < _friendRows.Length; i++)
        {
            if (_friendRows[i] == null) continue;

            var rowRt = _friendRows[i].GetComponent<RectTransform>();
            if (rowRt != null)
            {
                rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.125f, 0.700f - i * 0.104f);
                rowRt.anchoredPosition = Vector2.zero;
                rowRt.sizeDelta = new Vector2(190f, 64f);
            }

            var rowImage = _friendRows[i].GetComponent<Image>();
            var rowSprite = LobbyGenRes.Get("gen_friend_row")
                ?? KenneyUiRes.Get("button_neutral_depth", new Vector4(16f, 16f, 16f, 16f));
            if (rowImage != null && rowSprite != null)
            {
                rowImage.sprite = rowSprite;
                rowImage.type = Image.Type.Sliced;
                rowImage.color = Color.white;
            }

            EnsureRuntimeImageNode(_friendRows[i].transform, "RuntimeFriendAvatarRing" + i,
                KenneyUiRes.Get("button_blue_header", new Vector4(14f, 14f, 14f, 14f)) ?? LobbyGenRes.Get("gen_portrait_ring"), new Vector2(0f, 0.5f),
                new Vector2(56f, 56f), new Color(1f, 1f, 1f, 0.96f)).rectTransform.anchoredPosition = new Vector2(34f, 0f);

            if (_friendAvatarImages?[i] != null)
            {
                var avatarRt = _friendAvatarImages[i].rectTransform;
                avatarRt.anchorMin = avatarRt.anchorMax = avatarRt.pivot = new Vector2(0f, 0.5f);
                avatarRt.anchoredPosition = new Vector2(10f, 0f);
                avatarRt.sizeDelta = new Vector2(48f, 48f);
                _friendAvatarImages[i].preserveAspect = true;
            }

            if (_friendLevelTexts?[i] != null)
                ConfigureOverlayText(_friendLevelTexts[i], new Vector2(0f, 0.5f), new Vector2(44f, 16f), 11, TextAnchor.MiddleCenter);
            if (_friendNameTexts?[i] != null)
                ConfigureOverlayText(_friendNameTexts[i], new Vector2(0f, 1f), new Vector2(96f, 22f), 15, TextAnchor.MiddleLeft);
            if (_friendMetaTexts?[i] != null)
                ConfigureOverlayText(_friendMetaTexts[i], new Vector2(0f, 0f), new Vector2(84f, 18f), 11, TextAnchor.MiddleLeft);
            if (_friendStatusTexts?[i] != null)
                ConfigureOverlayText(_friendStatusTexts[i], new Vector2(1f, 0.5f), new Vector2(58f, 18f), 11, TextAnchor.MiddleRight);

            if (_friendNameTexts?[i] != null)
            {
                _friendNameTexts[i].rectTransform.anchoredPosition = new Vector2(66f, -8f);
                _friendNameTexts[i].rectTransform.pivot = new Vector2(0f, 1f);
                _friendNameTexts[i].rectTransform.anchorMin = new Vector2(0f, 1f);
                _friendNameTexts[i].rectTransform.anchorMax = new Vector2(0f, 1f);
            }

            if (_friendMetaTexts?[i] != null)
            {
                _friendMetaTexts[i].rectTransform.anchoredPosition = new Vector2(66f, 10f);
                _friendMetaTexts[i].rectTransform.pivot = new Vector2(0f, 0f);
                _friendMetaTexts[i].rectTransform.anchorMin = new Vector2(0f, 0f);
                _friendMetaTexts[i].rectTransform.anchorMax = new Vector2(0f, 0f);
            }

            if (_friendStatusTexts?[i] != null)
            {
                _friendStatusTexts[i].rectTransform.anchoredPosition = new Vector2(-8f, -10f);
                _friendStatusTexts[i].rectTransform.pivot = new Vector2(1f, 0.5f);
                _friendStatusTexts[i].rectTransform.anchorMin = new Vector2(1f, 0.5f);
                _friendStatusTexts[i].rectTransform.anchorMax = new Vector2(1f, 0.5f);
            }

            if (_friendLevelTexts?[i] != null)
            {
                _friendLevelTexts[i].rectTransform.anchoredPosition = new Vector2(34f, 20f);
                _friendLevelTexts[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _friendLevelTexts[i].rectTransform.anchorMin = new Vector2(0f, 0.5f);
                _friendLevelTexts[i].rectTransform.anchorMax = new Vector2(0f, 0.5f);
            }
        }

        if (ViewAllFriendsBtn != null)
        {
            var rt = ViewAllFriendsBtn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.125f, 0.108f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(208f, 30f);
            }
            ApplyRuntimeButtonSkin(ViewAllFriendsBtn, "button_blue_header", Color.white, new Color(0.08f, 0.12f, 0.13f, 1f));
        }
    }

    void EnsureTaskAndTechChrome(Transform root)
    {
        if (root == null) return;

        var taskCard = EnsureRuntimeImageNode(root, "RuntimeTaskPanelCard",
            KenneyUiRes.Get("button_neutral_depth", new Vector4(18f, 18f, 18f, 18f))
                ?? LobbyGenRes.Get("gen_right_tasks_panel")
                ?? LobbyGenRes.Get("gen_panel_frame"),
            new Vector2(0.852f, 0.575f), new Vector2(252f, 256f), new Color(1f, 1f, 1f, 0.98f), true);
        taskCard.rectTransform.SetSiblingIndex(0);
        EnsureRuntimeImageNode(taskCard.transform, "RuntimeTaskPanelIcon",
            LobbyGenRes.Get("gen_icon_task_crate"), new Vector2(0.12f, 0.90f), new Vector2(28f, 28f), Color.white);
        EnsureRuntimeTextNode(taskCard.transform, "RuntimeTaskPanelTitle", "任务中心",
            new Vector2(0.46f, 0.90f), new Vector2(136f, 24f), 19,
            new Color(1f, 0.94f, 0.74f, 1f), TextAnchor.MiddleLeft);
        EnsureRuntimeTextNode(taskCard.transform, "RuntimeTaskPanelSubtitle", "每日军务 / 补给奖励",
            new Vector2(0.52f, 0.84f), new Vector2(182f, 18f), 11,
            new Color(0.84f, 0.88f, 0.82f, 0.94f), TextAnchor.MiddleCenter);

        var taskRowIcons = new[] { "gen_icon_task_crate", "gen_icon_helmet", "gen_icon_tank" };
        for (int i = 0; i < 3; i++)
        {
            var row = FindDeepChild(root, "TaskRow" + i);
            if (row == null) continue;

            var rowRt = row as RectTransform;
            if (rowRt != null)
            {
                rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0.852f, 0.682f - i * 0.107f);
                rowRt.anchoredPosition = Vector2.zero;
                rowRt.sizeDelta = new Vector2(232f, 62f);
            }

            var rowImage = row.GetComponent<Image>();
            var rowSprite = KenneyUiRes.Get("button_blue_header", new Vector4(16f, 16f, 16f, 16f))
                ?? LobbyGenRes.Get("gen_task_card")
                ?? LobbyGenRes.Get("gen_mission_row");
            if (rowImage != null && rowSprite != null)
            {
                rowImage.sprite = rowSprite;
                rowImage.type = Image.Type.Sliced;
                rowImage.color = Color.white;
            }

            EnsureRuntimeImageNode(row, "TaskIcon" + i, LobbyGenRes.Get(taskRowIcons[i]),
                new Vector2(0f, 0.5f), new Vector2(34f, 34f), Color.white).rectTransform.anchoredPosition = new Vector2(18f, 0f);

            var title = FindDeepChild(row, "TaskTitle" + i)?.GetComponent<Text>();
            var prog = FindDeepChild(row, "TaskProg" + i)?.GetComponent<Text>();
            var slider = FindDeepChild(row, "TaskSlider" + i) as RectTransform;
            var claim = FindDeepChild(row, "TaskClaim" + i) as RectTransform;

            if (title != null)
            {
                ConfigureOverlayText(title, new Vector2(0f, 1f), new Vector2(108f, 18f), 12, TextAnchor.MiddleLeft);
                title.rectTransform.anchoredPosition = new Vector2(40f, -8f);
            }
            if (prog != null)
            {
                ConfigureOverlayText(prog, new Vector2(1f, 1f), new Vector2(48f, 18f), 12, TextAnchor.MiddleRight);
                prog.rectTransform.anchoredPosition = new Vector2(-44f, -8f);
            }
            if (slider != null)
            {
                slider.anchorMin = slider.anchorMax = slider.pivot = new Vector2(0f, 0.5f);
                slider.anchoredPosition = new Vector2(40f, -10f);
                slider.sizeDelta = new Vector2(120f, 10f);
                ApplyMissionSliderSkin(root, "TaskSlider" + i);
            }
            if (claim != null)
            {
                claim.anchorMin = claim.anchorMax = claim.pivot = new Vector2(1f, 0.5f);
                claim.anchoredPosition = new Vector2(-14f, -2f);
                claim.sizeDelta = new Vector2(62f, 28f);
            }
        }

        var techCard = EnsureRuntimeImageNode(root, "RuntimeTechPanelCard",
            KenneyUiRes.Get("button_neutral_depth", new Vector4(18f, 18f, 18f, 18f))
                ?? LobbyGenRes.Get("gen_tech_card")
                ?? LobbyGenRes.Get("gen_panel_frame"),
            new Vector2(0.852f, 0.310f), new Vector2(252f, 176f), new Color(1f, 1f, 1f, 0.98f), true);
        techCard.rectTransform.SetSiblingIndex(0);
        EnsureRuntimeImageNode(techCard.transform, "RuntimeTechPanelIcon",
            LobbyGenRes.Get("gen_icon_tech_blueprint"), new Vector2(0.12f, 0.86f), new Vector2(28f, 28f), Color.white);
        EnsureRuntimeTextNode(techCard.transform, "RuntimeTechPanelTitle", "科技研究",
            new Vector2(0.46f, 0.86f), new Vector2(136f, 24f), 19,
            new Color(1f, 0.94f, 0.74f, 1f), TextAnchor.MiddleLeft);
        EnsureRuntimeTextNode(techCard.transform, "RuntimeTechPanelSubtitle", "基地升级 / 战术加成",
            new Vector2(0.52f, 0.79f), new Vector2(182f, 18f), 11,
            new Color(0.84f, 0.88f, 0.82f, 0.94f), TextAnchor.MiddleCenter);

        ApplyMissionSliderSkin(root, "TechProgressSlider");
        if (MoreTasksBtn != null)
            ApplyRuntimeButtonSkin(MoreTasksBtn, "button_blue_header", Color.white, new Color(0.08f, 0.12f, 0.13f, 1f));
        if (TechTreeBtn != null)
            ApplyRuntimeButtonSkin(TechTreeBtn, "button_neutral_depth", Color.white, new Color(0.08f, 0.12f, 0.13f, 1f));
        if (TechSpeedBtn != null)
            ApplyRuntimeButtonSkin(TechSpeedBtn, "button_yellow_header", Color.white, new Color(0.10f, 0.11f, 0.07f, 1f));
        if (TechStartBtn != null)
            ApplyRuntimeButtonSkin(TechStartBtn, "button_blue_header", Color.white, new Color(0.08f, 0.12f, 0.13f, 1f));
    }

    void EnsureLobbyVisualChrome(Transform root)
    {
        if (root == null) return;
        if (!EnableLegacyLobbyScaffolding)
        {
            var canvasRoot = root.root != null ? root.root : root;
            CleanupRuntimeLobbyChrome(canvasRoot);
            BindExistingHallDataNodes(canvasRoot);
            return;
        }

        ApplyLobbyDeskBackgroundSprite();
        ApplyLobbyGenUiSprites();
        EnsureCommanderProfileChrome(root);
        EnsureFriendPanelChrome(root);
        EnsureTaskAndTechChrome(root);
    }

    void BindExistingHallDataNodes(Transform root)
    {
        if (root == null) return;

        if (GoldText == null)
            GoldText = FindDeepChild(root, "GoldCountText")?.GetComponent<Text>()
                ?? FindDeepChild(root, "GoldText")?.GetComponent<Text>();
        if (GemText == null)
            GemText = FindDeepChild(root, "GemCountText")?.GetComponent<Text>()
                ?? FindDeepChild(root, "GemText")?.GetComponent<Text>();
        if (MatchStatusText == null)
            MatchStatusText = FindDeepChild(root, "MatchStatusText")?.GetComponent<Text>();

        BindExistingCommanderBadgeNodes(root);

        if (QuickMatchButton == null)
            QuickMatchButton = FindDeepChild(root, "QuickMatchButton")?.GetComponent<Button>();
        if (CustomRoomButton == null)
            CustomRoomButton = FindDeepChild(root, "CustomRoomButton")?.GetComponent<Button>();
        if (GlobalConquestButton == null)
            GlobalConquestButton = FindDeepChild(root, "GlobalConquestButton")?.GetComponent<Button>();

        if (AddFriendIconBtn == null)
            AddFriendIconBtn = FindDeepChild(root, "AddFriendIconBtn")?.GetComponent<Button>();
        if (ViewAllFriendsBtn == null)
            ViewAllFriendsBtn = FindDeepChild(root, "ViewAllFriendsBtn")?.GetComponent<Button>();

        BindExistingTaskNodes(root);
        RetuneSceneTaskListLayout(root);
        BindExistingTechNodes(root);
        BindExistingNavNodes(root);
        BindExistingMapNodes(root);
        BindExistingRoomNodes(root);
    }

    void BindExistingCommanderBadgeNodes(Transform root)
    {
        if (root == null) return;

        _sceneCommanderLevelText = FindDeepChild(root, "dengji")?.GetComponent<Text>();

        var plinth = FindDeepChild(root, "LobbyModelPreviewPlinth");
        if (plinth != null)
        {
            _sceneCommanderNameText =
                FindDeepChild(plinth, "name")?.GetComponent<Text>()
                ?? plinth.GetComponentInChildren<Text>(true);
        }

        var glow = FindDeepChild(root, "LobbyModelPreviewGlow");
        if (glow != null)
            _sceneRankBadgeNameText = glow.GetComponentInChildren<Text>(true);

        var preview = FindDeepChild(root, "LobbyModelPreview");
        if (preview == null) return;

        var oldModelPreview = preview.GetComponent<LobbyModelPreview>();
        if (oldModelPreview != null && oldModelPreview.enabled)
            oldModelPreview.enabled = false;

        _sceneRankBadgeRawImage = preview.GetComponent<RawImage>();
        _sceneRankBadgeImage = preview.GetComponent<Image>();

        if (_sceneRankBadgeRawImage != null)
        {
            _sceneRankBadgeRawImage.color = Color.white;
            _sceneRankBadgeRawImage.raycastTarget = false;
            _sceneRankBadgeRawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        if (_sceneRankBadgeImage != null)
        {
            _sceneRankBadgeImage.color = Color.white;
            _sceneRankBadgeImage.raycastTarget = false;
            _sceneRankBadgeImage.preserveAspect = true;
        }
    }

    void ApplySceneCommanderBadgeData(string commanderName, int level, string rankTitle)
    {
        if (_sceneRankBadgeRawImage == null && _sceneRankBadgeImage == null)
        {
            var root = HallPanel != null ? HallPanel.transform : GameObject.Find("LobbyCanvas")?.transform;
            BindExistingCommanderBadgeNodes(root);
        }

        level = Mathf.Max(1, level);
        commanderName = string.IsNullOrWhiteSpace(commanderName) ? "游客" : commanderName.Trim();

        var badge = ResolveRankBadge(rankTitle, level);
        string shownRankTitle = string.IsNullOrWhiteSpace(rankTitle) ? badge.displayName : rankTitle.Trim();

        if (_sceneCommanderNameText != null)
            _sceneCommanderNameText.text = commanderName;
        if (_sceneCommanderLevelText != null)
            _sceneCommanderLevelText.text = level.ToString();
        if (_sceneRankBadgeNameText != null)
            _sceneRankBadgeNameText.text = shownRankTitle;

        var texture = LoadRankBadgeTexture(badge.resource);
        if (texture == null)
            return;

        var oldModelPreview = _sceneRankBadgeRawImage != null
            ? _sceneRankBadgeRawImage.GetComponent<LobbyModelPreview>()
            : _sceneRankBadgeImage != null ? _sceneRankBadgeImage.GetComponent<LobbyModelPreview>() : null;
        if (oldModelPreview != null && oldModelPreview.enabled)
            oldModelPreview.enabled = false;

        if (_sceneRankBadgeRawImage != null)
        {
            _sceneRankBadgeRawImage.texture = texture;
            _sceneRankBadgeRawImage.color = Color.white;
            _sceneRankBadgeRawImage.raycastTarget = false;
            if (_sceneRankBadgeImage != null)
                _sceneRankBadgeImage.enabled = false;
            return;
        }

        if (_sceneRankBadgeImage != null)
        {
            _sceneRankBadgeImage.sprite = GetOrCreateRankBadgeSprite(badge.resource, texture);
            _sceneRankBadgeImage.color = Color.white;
            _sceneRankBadgeImage.preserveAspect = true;
            _sceneRankBadgeImage.raycastTarget = false;
        }
    }

    static (string resource, string displayName) ResolveRankBadge(string rankTitle, int level)
    {
        string text = rankTitle ?? "";
        for (int i = 0; i < RankBadgeCatalog.Length; i++)
        {
            var entry = RankBadgeCatalog[i];
            for (int j = 0; j < entry.tokens.Length; j++)
            {
                if (text.IndexOf(entry.tokens[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    return (entry.resource, entry.displayName);
            }
        }

        int safeLevel = Mathf.Max(1, level);
        for (int i = 0; i < RankBadgeCatalog.Length; i++)
        {
            var entry = RankBadgeCatalog[i];
            if (safeLevel >= entry.minLevel)
                return (entry.resource, entry.displayName);
        }

        var fallback = RankBadgeCatalog[RankBadgeCatalog.Length - 1];
        return (fallback.resource, fallback.displayName);
    }

    static Texture2D LoadRankBadgeTexture(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
            return null;

        string path = "LobbyGen/WW2RankBadges/" + resourceName;
        var texture = Resources.Load<Texture2D>(path);
        if (texture != null)
            return texture;

        return Resources.Load<Texture2D>("LobbyGen/WW2RankBadges/rank_badge_01_bronze_shield");
    }

    Sprite GetOrCreateRankBadgeSprite(string resourceName, Texture2D texture)
    {
        if (string.IsNullOrEmpty(resourceName))
            return null;

        string path = "LobbyGen/WW2RankBadges/" + resourceName;
        var importedSprite = Resources.Load<Sprite>(path);
        if (importedSprite != null)
            return importedSprite;
        if (texture == null)
            return null;

        if (_runtimeRankBadgeSprites.TryGetValue(resourceName, out var cached) && cached != null)
            return cached;

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = resourceName;
        _runtimeRankBadgeSprites[resourceName] = sprite;
        return sprite;
    }

    string GetCommanderDisplayName()
    {
        var net = NetworkClient.Instance;
        string name = net != null && !string.IsNullOrEmpty(net.UserName)
            ? net.UserName
            : PlayerPrefs.GetString("current_user", "游客");
        return string.IsNullOrWhiteSpace(name) ? "游客" : name.Trim();
    }

    int GetCommanderLevel()
    {
        var net = NetworkClient.Instance;
        int level = net != null ? net.Level : PlayerPrefs.GetInt("net_level", 1);
        return Mathf.Max(1, level);
    }

    string GetCommanderRankTitle(int level)
    {
        var net = NetworkClient.Instance;
        string rank = net != null ? net.RankTitle : "";
        if (string.IsNullOrWhiteSpace(rank))
            rank = PlayerPrefs.GetString("net_rank_title", "");
        if (string.IsNullOrWhiteSpace(rank))
            rank = PlayerPrefs.GetString("player_rank_title", "");
        if (!string.IsNullOrWhiteSpace(rank))
            return rank.Trim();

        return ResolveRankBadge("", level).displayName;
    }

    void RetuneSceneTaskListLayout(Transform root)
    {
        if (root == null) return;

        var taskPanel = FindDeepChild(root, "renwu");
        if (taskPanel == null) return;

        // 当前大厅的任务栏由场景手工摆放；运行时只绑定数据，不再覆盖 RectTransform。
        if (HasSceneAuthoredTaskPanel(root))
            return;

        var taskSlots = new[]
        {
            FindDeepChild(taskPanel, "TaskSlotPanel0") as RectTransform,
            FindDeepChild(taskPanel, "TaskSlotPanel1") as RectTransform,
            FindDeepChild(taskPanel, "TaskSlotPanel2") as RectTransform
        };

        float buttonBand = 0.12f;
        float slotHeight = (1f - buttonBand) / taskSlots.Length;
        for (int i = 0; i < taskSlots.Length; i++)
        {
            var slot = taskSlots[i];
            if (slot == null) continue;

            float top = 1f - i * slotHeight;
            float bottom = top - slotHeight;
            slot.anchorMin = new Vector2(0f, bottom);
            slot.anchorMax = new Vector2(1f, top);
            slot.anchoredPosition = Vector2.zero;
            slot.sizeDelta = new Vector2(-16f, -2f);
        }

        var moreTasks = FindDeepChild(taskPanel, "MoreTasksBtn") as RectTransform
            ?? FindDeepChild(root, "MoreTasksBtn") as RectTransform;
        if (moreTasks == null) return;

        moreTasks.SetParent(taskPanel, false);
        moreTasks.anchorMin = moreTasks.anchorMax = moreTasks.pivot = new Vector2(0.5f, buttonBand * 0.5f);
        moreTasks.anchoredPosition = Vector2.zero;
        moreTasks.sizeDelta = new Vector2(106f, 24f);
    }

    static bool ApproximatelyRect(Rect a, Rect b)
    {
        return Mathf.Abs(a.x - b.x) < 0.5f
            && Mathf.Abs(a.y - b.y) < 0.5f
            && Mathf.Abs(a.width - b.width) < 0.5f
            && Mathf.Abs(a.height - b.height) < 0.5f;
    }

    static Rect GetScreenSafeAreaFallback()
    {
        try
        {
            var prop = typeof(Screen).GetProperty(
                "safeArea",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null && prop.PropertyType == typeof(Rect))
            {
                object value = prop.GetValue(null, null);
                if (value is Rect)
                    return (Rect)value;
            }
        }
        catch (Exception)
        {
            // Older Unity runtimes do not expose Screen.safeArea.
        }

        return new Rect(0f, 0f, Screen.width, Screen.height);
    }

    static void ApplyReferenceStageTransform(RectTransform rt, Vector2 anchoredPosition, float uniformScale)
    {
        if (rt == null) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = LobbyLayoutReferenceResolution;
        rt.anchoredPosition = anchoredPosition;
        rt.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    static void MoveCanvasChildIntoHall(Transform canvas, Transform hall, string childName)
    {
        if (canvas == null || hall == null || string.IsNullOrEmpty(childName)) return;

        var child = canvas.Find(childName);
        if (child == null || child == hall) return;
        child.SetParent(hall, false);
    }

    void EnsureHandmadeHallHierarchy(Transform canvas, Transform hall)
    {
        if (EnableLegacyLobbyScaffolding) return;

        MoveCanvasChildIntoHall(canvas, hall, "dengji");
    }

    void ApplyResponsiveHallLayout(bool force = false)
    {
        if (!EnableLegacyLobbyScaffolding && HasSceneAuthoredMainLobbyLayout(GetLobbyCanvasRoot()))
            return;

        RectTransform hallRect = HallPanel != null ? HallPanel.GetComponent<RectTransform>() : null;
        if (hallRect == null) return;

        Transform canvasTransform = hallRect.parent;

        RectTransform canvasRect = null;
        Canvas canvas = hallRect.root != null ? hallRect.root.GetComponent<Canvas>() : null;
        if (canvas != null)
            canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
            canvasRect = hallRect.parent as RectTransform;
        if (canvasRect == null) return;

        Rect safeArea = GetScreenSafeAreaFallback();
        if (safeArea.width <= 0f || safeArea.height <= 0f)
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);

        if (!force
            && _lastLayoutScreenWidth == Screen.width
            && _lastLayoutScreenHeight == Screen.height
            && ApproximatelyRect(_lastLayoutSafeArea, safeArea))
            return;

        float scaleFactor = canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
        float usableWidth = safeArea.width / scaleFactor;
        float usableHeight = safeArea.height / scaleFactor;
        float uniformScale = Mathf.Min(
            usableWidth / LobbyLayoutReferenceResolution.x,
            usableHeight / LobbyLayoutReferenceResolution.y);
        uniformScale = Mathf.Max(0.01f, uniformScale);

        float safeCenterX = (safeArea.x + safeArea.width * 0.5f) / Mathf.Max(1f, Screen.width);
        float safeCenterY = (safeArea.y + safeArea.height * 0.5f) / Mathf.Max(1f, Screen.height);
        Vector2 anchoredPosition = new Vector2(
            (safeCenterX - 0.5f) * canvasRect.rect.width,
            (safeCenterY - 0.5f) * canvasRect.rect.height);

        EnsureHandmadeHallHierarchy(canvasTransform, hallRect.transform);

        if (!EnableLegacyLobbyScaffolding && canvasTransform != null)
        {
            ApplyReferenceStageTransform(canvasTransform.Find("Background") as RectTransform, anchoredPosition, uniformScale);
            ApplyReferenceStageTransform(canvasTransform.Find("BackgroundOverlay") as RectTransform, anchoredPosition, uniformScale);
        }
        ApplyReferenceStageTransform(hallRect, anchoredPosition, uniformScale);

        _lastLayoutScreenWidth = Screen.width;
        _lastLayoutScreenHeight = Screen.height;
        _lastLayoutSafeArea = safeArea;
    }

    void BindExistingTaskNodes(Transform root)
    {
        if (HasNullEntries(TaskTitleTexts, 3))
        {
            var titles = new Text[3];
            for (int i = 0; i < titles.Length; i++)
                titles[i] = FindDeepChild(root, "TaskTitle" + i)?.GetComponent<Text>();
            TaskTitleTexts = titles;
        }

        if (HasNullEntries(TaskProgTexts, 3))
        {
            var progs = new Text[3];
            for (int i = 0; i < progs.Length; i++)
                progs[i] = FindDeepChild(root, "TaskProg" + i)?.GetComponent<Text>();
            TaskProgTexts = progs;
        }

        if (HasNullEntries(TaskProgSliders, 3))
        {
            var sliders = new Slider[3];
            for (int i = 0; i < sliders.Length; i++)
                sliders[i] = FindDeepChild(root, "TaskSlider" + i)?.GetComponent<Slider>();
            TaskProgSliders = sliders;
        }

        if (HasNullEntries(TaskClaimButtons, 3))
        {
            var claims = new Button[3];
            for (int i = 0; i < claims.Length; i++)
                claims[i] = FindDeepChild(root, "TaskClaim" + i)?.GetComponent<Button>();
            TaskClaimButtons = claims;
        }
    }

    void BindExistingTechNodes(Transform root)
    {
        if (TechResearchNameText == null)
            TechResearchNameText = FindDeepChild(root, "TechResearchName")?.GetComponent<Text>();
        if (TechResearchDescText == null)
            TechResearchDescText = FindDeepChild(root, "TechResearchDesc")?.GetComponent<Text>();
        if (TechTimerBarText == null)
            TechTimerBarText = FindDeepChild(root, "TechTimerBar")?.GetComponent<Text>()
                ?? FindDeepChild(root, "TechTimerText")?.GetComponent<Text>();
        if (TechProgressSlider == null)
            TechProgressSlider = FindDeepChild(root, "TechProgressSlider")?.GetComponent<Slider>();
        if (TechSpeedBtn == null)
            TechSpeedBtn = FindDeepChild(root, "TechSpeedBtn")?.GetComponent<Button>();
        if (TechStartBtn == null)
            TechStartBtn = FindDeepChild(root, "TechStartBtn")?.GetComponent<Button>();
        if (TechTreeBtn == null)
            TechTreeBtn = FindDeepChild(root, "TechTreeBtn")?.GetComponent<Button>();
        if (MoreTasksBtn == null)
            MoreTasksBtn = FindDeepChild(root, "MoreTasksBtn")?.GetComponent<Button>();
    }

    void BindExistingNavNodes(Transform root)
    {
        if (SettingsIconBtn == null)
            SettingsIconBtn = FindDeepChild(root, "SettingsIconBtn")?.GetComponent<Button>();
        if (NotifyBellBtn == null)
            NotifyBellBtn = FindDeepChild(root, "NotifyBellBtn")?.GetComponent<Button>();
        if (GoldPlusBtn == null)
            GoldPlusBtn = FindDeepChild(root, "GoldPlusBtn")?.GetComponent<Button>();
        if (GemPlusBtn == null)
            GemPlusBtn = FindDeepChild(root, "GemPlusBtn")?.GetComponent<Button>();
        if (NavShopBtn == null)
            NavShopBtn = FindDeepChild(root, "NavShopBtn")?.GetComponent<Button>();
        if (NavWarehouseBtn == null)
            NavWarehouseBtn = FindDeepChild(root, "NavWarehouseBtn")?.GetComponent<Button>();
        if (NavCampaignBtn == null)
            NavCampaignBtn = FindDeepChild(root, "NavCampaignBtn")?.GetComponent<Button>();
        if (NavRankBtn == null)
            NavRankBtn = FindDeepChild(root, "NavRankBtn")?.GetComponent<Button>();
        if (NavMailBtn == null)
            NavMailBtn = FindDeepChild(root, "NavMailBtn")?.GetComponent<Button>();
    }

    void BindExistingRoomNodes(Transform root)
    {
        if (RoomPanel == null)
            RoomPanel = FindDeepChild(root, "RoomPanel")?.gameObject;
        if (CreateRoomButton == null)
            CreateRoomButton = FindDeepChild(root, "CreateRoomButton")?.GetComponent<Button>();
        if (RefreshRoomsButton == null)
            RefreshRoomsButton = FindDeepChild(root, "RefreshRoomsButton")?.GetComponent<Button>();
        if (RoomBackButton == null)
            RoomBackButton = FindDeepChild(root, "RoomBackButton")?.GetComponent<Button>();
        if (RoomStatusText == null)
            RoomStatusText = FindDeepChild(root, "RoomStatusText")?.GetComponent<Text>();

        if (HasNullEntries(RoomRowTexts, 4))
        {
            var rows = new Text[4];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = FindDeepChild(root, "RoomRowText" + i)?.GetComponent<Text>();
            RoomRowTexts = rows;
        }

        if (HasNullEntries(RoomJoinButtons, 4))
        {
            var joins = new Button[4];
            for (int i = 0; i < joins.Length; i++)
                joins[i] = FindDeepChild(root, "RoomJoinButton" + i)?.GetComponent<Button>();
            RoomJoinButtons = joins;
        }

        if (HasNullEntries(RoomInviteButtons, 4))
        {
            var invites = new Button[4];
            for (int i = 0; i < invites.Length; i++)
                invites[i] = FindDeepChild(root, "RoomInviteButton" + i)?.GetComponent<Button>();
            RoomInviteButtons = invites;
        }

        if (InvitePanel == null)
            InvitePanel = FindDeepChild(root, "InvitePanel")?.gameObject;
        if (InviteCloseBtn == null)
            InviteCloseBtn = FindDeepChild(root, "InviteCloseBtn")?.GetComponent<Button>();
        if (HasNullEntries(InviteFriendNames, 6))
        {
            var names = new Text[6];
            for (int i = 0; i < names.Length; i++)
                names[i] = FindDeepChild(root, "InviteFriendName" + i)?.GetComponent<Text>();
            InviteFriendNames = names;
        }
        if (HasNullEntries(InviteFriendButtons, 6))
        {
            var buttons = new Button[6];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = FindDeepChild(root, "InviteFriendBtn" + i)?.GetComponent<Button>();
            InviteFriendButtons = buttons;
        }

        if (CreateRoomPanel == null)
            CreateRoomPanel = FindDeepChild(root, "CreateRoomPanel")?.gameObject;
        if (CRNameInput == null)
            CRNameInput = FindDeepChild(root, "CRNameInput")?.GetComponent<InputField>();
        if (CRSelectedMapText == null)
            CRSelectedMapText = FindDeepChild(root, "CRSelectedMap")?.GetComponent<Text>();
        if (CRConfirmBtn == null)
            CRConfirmBtn = FindDeepChild(root, "CRConfirmBtn")?.GetComponent<Button>();
        if (CRCancelBtn == null)
            CRCancelBtn = FindDeepChild(root, "CRCancelBtn")?.GetComponent<Button>();
        if (HasNullEntries(CRMapButtons, 4))
        {
            var maps = new Button[4];
            for (int i = 0; i < maps.Length; i++)
                maps[i] = FindDeepChild(root, "CRMapBtn" + i)?.GetComponent<Button>();
            CRMapButtons = maps;
        }
    }

    void BindExistingMapNodes(Transform root)
    {
        if (MapPanel == null)
            MapPanel = FindDeepChild(root, "MapPanel")?.gameObject;
        if (StartMatchButton == null)
            StartMatchButton = FindDeepChild(root, "StartMatchButton")?.GetComponent<Button>();
        if (BackToHallButton == null)
            BackToHallButton = FindDeepChild(root, "BackToHallButton")?.GetComponent<Button>();
        if (MatchTimerText == null)
            MatchTimerText = FindDeepChild(root, "MatchTimerText")?.GetComponent<Text>();

        if (HasNullEntries(MapButtons, 5))
        {
            var buttons = new Button[5];
            for (int i = 0; i < buttons.Length; i++)
                buttons[i] = FindDeepChild(root, "MapButton" + i)?.GetComponent<Button>();
            MapButtons = buttons;
        }

        if (MatchingOverlay == null)
            MatchingOverlay = FindDeepChild(root, "MatchingOverlay")?.gameObject;
        if (MatchingMapText == null)
            MatchingMapText = FindDeepChild(root, "MatchingMapText")?.GetComponent<Text>();
        if (MatchingCountText == null)
            MatchingCountText = FindDeepChild(root, "MatchingCountText")?.GetComponent<Text>();
        if (CancelMatchButton == null)
            CancelMatchButton = FindDeepChild(root, "CancelMatchButton")?.GetComponent<Button>();
    }

    void CleanupRuntimeLobbyChrome(Transform root)
    {
        string[] runtimeNodes =
        {
            "RuntimeCommanderPlate", "RuntimeFriendsPanelCard", "RuntimeTaskPanelCard",
            "RuntimeTechPanelCard", "ReferenceDynamicOverlay"
        };

        foreach (var nodeName in runtimeNodes)
        {
            var tr = FindDeepChild(root, nodeName);
            if (tr != null) Destroy(tr.gameObject);
        }

        CleanupLegacyReferenceOverlay(root);
    }

    static void RetuneTextWithFullHallFallback(Transform root, string name,
        Vector2 panelAnchor, Vector2 panelSize, Vector2 fullAnchor, Vector2 fullSize,
        int maxFontSize, TextAnchor alignment)
    {
        var tr = FindDeepChild(root, name);
        var text = tr != null ? tr.GetComponent<Text>() : null;
        if (text == null) return;
        bool onFullHall = tr.parent != null && (tr.parent == root || tr.parent.name == "HallPanel");
        ConfigureOverlayText(text, onFullHall ? fullAnchor : panelAnchor, onFullHall ? fullSize : panelSize,
            maxFontSize, alignment);
    }

    static void RetuneRectWithFullHallFallback(Transform root, string name,
        Vector2 panelAnchor, Vector2 panelSize, Vector2 fullAnchor, Vector2 fullSize)
    {
        var tr = FindDeepChild(root, name);
        var rt = tr as RectTransform;
        if (rt == null) return;
        bool onFullHall = tr.parent != null && (tr.parent == root || tr.parent.name == "HallPanel");
        ConfigureOverlayRect(rt, onFullHall ? fullAnchor : panelAnchor, onFullHall ? fullSize : panelSize);
    }

    void ConstrainRealHallDataTexts()
    {
        ConstrainOverlayText(GoldText, 22, TextAnchor.MiddleCenter);
        ConstrainOverlayText(GemText, 22, TextAnchor.MiddleCenter);

        if (TaskTitleTexts != null)
            for (int i = 0; i < TaskTitleTexts.Length; i++)
                ConstrainOverlayText(TaskTitleTexts[i], 12, TextAnchor.MiddleLeft);

        if (TaskProgTexts != null)
            for (int i = 0; i < TaskProgTexts.Length; i++)
                ConstrainOverlayText(TaskProgTexts[i], 12, TextAnchor.MiddleRight);

        ConstrainOverlayText(TechResearchNameText, 13, TextAnchor.MiddleLeft);
        ConstrainOverlayText(TechResearchDescText, 11, TextAnchor.MiddleLeft);
        ConstrainOverlayText(TechTimerBarText, 12, TextAnchor.MiddleLeft);
    }

    static void ConfigureOverlayText(Text text, Vector2 anchor, Vector2 size, int maxFontSize, TextAnchor alignment)
    {
        if (text == null) return;
        ConfigureOverlayRect(text.rectTransform, anchor, size);
        ConstrainOverlayText(text, maxFontSize, alignment);
    }

    static void ConstrainOverlayText(Text text, int maxFontSize, TextAnchor alignment)
    {
        if (text == null) return;
        text.alignment = alignment;
        text.fontSize = maxFontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = maxFontSize;
    }

    static void ConfigureOverlayRect(RectTransform rt, Vector2 anchor, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    static bool HasRealHallChrome(Transform root)
    {
        return FindDeepChild(root, "PortraitFace")?.GetComponent<Image>() != null
            && (FindDeepChild(root, "GoldText")?.GetComponent<Text>() != null
                || FindDeepChild(root, "GoldCountText")?.GetComponent<Text>() != null)
            && (FindDeepChild(root, "GemText")?.GetComponent<Text>() != null
                || FindDeepChild(root, "GemCountText")?.GetComponent<Text>() != null)
            && FindDeepChild(root, "QuickMatchButton")?.GetComponent<Button>() != null
            && FindDeepChild(root, "CustomRoomButton")?.GetComponent<Button>() != null
            && FindDeepChild(root, "GlobalConquestButton")?.GetComponent<Button>() != null
            && FindDeepChild(root, "TaskTitle0")?.GetComponent<Text>() != null
            && FindDeepChild(root, "TaskProg0")?.GetComponent<Text>() != null
            && FindDeepChild(root, "TaskSlider0")?.GetComponent<Slider>() != null
            && FindDeepChild(root, "TaskClaim0")?.GetComponent<Button>() != null
            && FindDeepChild(root, "MoreTasksBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "TechResearchName")?.GetComponent<Text>() != null
            && FindDeepChild(root, "TechResearchDesc")?.GetComponent<Text>() != null
            && FindDeepChild(root, "TechTimerText")?.GetComponent<Text>() != null
            && FindDeepChild(root, "TechProgressSlider")?.GetComponent<Slider>() != null
            && FindDeepChild(root, "TechTreeBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "NavShopBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "NavWarehouseBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "NavCampaignBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "NavRankBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "NavMailBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "AddFriendIconBtn")?.GetComponent<Button>() != null
            && FindDeepChild(root, "ViewAllFriendsBtn")?.GetComponent<Button>() != null;
    }

    void BindReferenceDynamicOverlay(Transform root)
    {
        var oldOverlay = FindDeepChild(root, "ReferenceDynamicOverlay");
        if (oldOverlay != null)
            Destroy(oldOverlay.gameObject);

        CleanupLegacyFriendOverlay(root);

        var gold = FindDeepChild(root, "DynamicGoldText")?.GetComponent<Text>();
        var gem = FindDeepChild(root, "DynamicGemText")?.GetComponent<Text>();
        var avatar = FindDeepChild(root, "DynamicPlayerAvatar")?.GetComponent<Image>();
        var realAvatar = FindDeepChild(root, "PortraitFace")?.GetComponent<Image>();
        if (realAvatar != null && avatar != null)
        {
            Destroy(avatar.gameObject);
            avatar = null;
        }
        if (gold != null && GoldText == null) GoldText = gold;
        if (gem != null && GemText == null) GemText = gem;
        if (avatar != null && PlayerAvatarImage == null) PlayerAvatarImage = avatar;

        var techButton = FindDeepChild(root, "DynamicTechButton")?.GetComponent<Button>();
        if (techButton != null && TechButton == null) TechButton = techButton;
        var quickMatch = FindDeepChild(root, "DynamicQuickMatchButton")?.GetComponent<Button>();
        var customRoom = FindDeepChild(root, "DynamicCustomRoomButton")?.GetComponent<Button>();
        var globalConquest = FindDeepChild(root, "DynamicGlobalConquestButton")?.GetComponent<Button>();
        if (quickMatch != null && QuickMatchButton == null) QuickMatchButton = quickMatch;
        if (customRoom != null && CustomRoomButton == null) CustomRoomButton = customRoom;
        if (globalConquest != null && GlobalConquestButton == null) GlobalConquestButton = globalConquest;
        var matchStatus = FindDeepChild(root, "DynamicMatchStatusText")?.GetComponent<Text>();
        if (matchStatus != null && MatchStatusText == null) MatchStatusText = matchStatus;

        var titles = new List<Text>();
        var progs = new List<Text>();
        var sliders = new List<Slider>();
        var claims = new List<Button>();
        for (int i = 0; i < 3; i++)
        {
            titles.Add(FindDeepChild(root, "DynamicTaskTitle" + i)?.GetComponent<Text>());
            progs.Add(FindDeepChild(root, "DynamicTaskProg" + i)?.GetComponent<Text>());
            sliders.Add(FindDeepChild(root, "DynamicTaskSlider" + i)?.GetComponent<Slider>());
            claims.Add(FindDeepChild(root, "DynamicTaskClaim" + i)?.GetComponent<Button>());
        }
        if (NeedsTaskTitleOverlay() && (titles[0] != null || titles[1] != null || titles[2] != null)) TaskTitleTexts = titles.ToArray();
        if (NeedsTaskProgOverlay() && (progs[0] != null || progs[1] != null || progs[2] != null)) TaskProgTexts = progs.ToArray();
        if (NeedsTaskSliderOverlay() && (sliders[0] != null || sliders[1] != null || sliders[2] != null)) TaskProgSliders = sliders.ToArray();
        if (NeedsTaskClaimOverlay() && (claims[0] != null || claims[1] != null || claims[2] != null)) TaskClaimButtons = claims.ToArray();

        var techName = FindDeepChild(root, "DynamicTechResearchName")?.GetComponent<Text>();
        var techDesc = FindDeepChild(root, "DynamicTechResearchDesc")?.GetComponent<Text>();
        var techTimer = FindDeepChild(root, "DynamicTechTimerText")?.GetComponent<Text>();
        var techSlider = FindDeepChild(root, "DynamicTechProgressSlider")?.GetComponent<Slider>();
        var techSpeed = FindDeepChild(root, "DynamicTechSpeedBtn")?.GetComponent<Button>();
        var techStart = FindDeepChild(root, "DynamicTechStartBtn")?.GetComponent<Button>();
        var techTree = FindDeepChild(root, "DynamicTechTreeBtn")?.GetComponent<Button>();
        var moreTasks = FindDeepChild(root, "DynamicMoreTasksBtn")?.GetComponent<Button>();
        if (techName != null && TechResearchNameText == null) TechResearchNameText = techName;
        if (techDesc != null && TechResearchDescText == null) TechResearchDescText = techDesc;
        if (techTimer != null && TechTimerBarText == null) TechTimerBarText = techTimer;
        if (techSlider != null && TechProgressSlider == null) TechProgressSlider = techSlider;
        if (techSpeed != null && TechSpeedBtn == null) TechSpeedBtn = techSpeed;
        if (techStart != null && TechStartBtn == null) TechStartBtn = techStart;
        if (techTree != null && TechTreeBtn == null) TechTreeBtn = techTree;
        if (moreTasks != null && MoreTasksBtn == null) MoreTasksBtn = moreTasks;

        var navShop = FindDeepChild(root, "DynamicNavShopBtn")?.GetComponent<Button>();
        var navWarehouse = FindDeepChild(root, "DynamicNavWarehouseBtn")?.GetComponent<Button>();
        var navCampaign = FindDeepChild(root, "DynamicNavCampaignBtn")?.GetComponent<Button>();
        var navRank = FindDeepChild(root, "DynamicNavRankBtn")?.GetComponent<Button>();
        var navMail = FindDeepChild(root, "DynamicNavMailBtn")?.GetComponent<Button>();
        if (navShop != null && NavShopBtn == null) NavShopBtn = navShop;
        if (navWarehouse != null && NavWarehouseBtn == null) NavWarehouseBtn = navWarehouse;
        if (navCampaign != null && NavCampaignBtn == null) NavCampaignBtn = navCampaign;
        if (navRank != null && NavRankBtn == null) NavRankBtn = navRank;
        if (navMail != null && NavMailBtn == null) NavMailBtn = navMail;
    }

    bool NeedsReferenceDynamicOverlay()
    {
        return GoldText == null
            || GemText == null
            || PlayerAvatarImage == null
            || TechButton == null
            || QuickMatchButton == null
            || GlobalConquestButton == null
            || CustomRoomButton == null
            || MatchStatusText == null
            || NeedsTaskTitleOverlay()
            || NeedsTaskProgOverlay()
            || NeedsTaskSliderOverlay()
            || NeedsTaskClaimOverlay()
            || MoreTasksBtn == null
            || TechResearchNameText == null
            || TechResearchDescText == null
            || TechTimerBarText == null
            || TechProgressSlider == null
            || TechSpeedBtn == null
            || TechStartBtn == null
            || TechTreeBtn == null
            || NavShopBtn == null
            || NavWarehouseBtn == null
            || NavCampaignBtn == null
            || NavRankBtn == null
            || NavMailBtn == null;
    }

    bool NeedsTaskTitleOverlay() => HasNullEntries(TaskTitleTexts, 3);
    bool NeedsTaskProgOverlay() => HasNullEntries(TaskProgTexts, 3);
    bool NeedsTaskSliderOverlay() => HasNullEntries(TaskProgSliders, 3);
    bool NeedsTaskClaimOverlay() => HasNullEntries(TaskClaimButtons, 3);

    static bool HasNullEntries<T>(T[] arr, int minLength) where T : class
    {
        if (arr == null || arr.Length < minLength) return true;
        for (int i = 0; i < minLength; i++)
            if (arr[i] == null) return true;
        return false;
    }

    void CleanupLegacyReferenceOverlay(Transform root)
    {
        if (root == null) return;

        string[] singleNodes = {
            "DynamicGoldText", "DynamicGemText", "DynamicPlayerAvatar",
            "DynamicQuickMatchButton", "DynamicCustomRoomButton", "DynamicGlobalConquestButton",
            "DynamicMatchStatusText", "DynamicMoreTasksBtn",
            "DynamicTechResearchName", "DynamicTechResearchDesc", "DynamicTechTimerText",
            "DynamicTechProgressSlider", "DynamicTechButton", "DynamicTechSpeedBtn",
            "DynamicTechStartBtn", "DynamicTechTreeBtn",
            "DynamicNavShopBtn", "DynamicNavWarehouseBtn", "DynamicNavCampaignBtn",
            "DynamicNavRankBtn", "DynamicNavMailBtn"
        };

        foreach (var nodeName in singleNodes)
        {
            var tr = FindDeepChild(root, nodeName);
            if (tr != null) Destroy(tr.gameObject);
        }

        for (int i = 0; i < 3; i++)
        {
            var title = FindDeepChild(root, "DynamicTaskTitle" + i);
            if (title != null) Destroy(title.gameObject);
            var prog = FindDeepChild(root, "DynamicTaskProg" + i);
            if (prog != null) Destroy(prog.gameObject);
            var slider = FindDeepChild(root, "DynamicTaskSlider" + i);
            if (slider != null) Destroy(slider.gameObject);
            var claim = FindDeepChild(root, "DynamicTaskClaim" + i);
            if (claim != null) Destroy(claim.gameObject);
        }
    }

    void CleanupLegacyFriendOverlay(Transform root)
    {
        if (root == null) return;

        for (int i = 0; i < 5; i++)
        {
            var row = FindDeepChild(root, "DynamicFriendRow" + i);
            if (row != null) Destroy(row.gameObject);
        }

        var addFriend = FindDeepChild(root, "DynamicAddFriendIconBtn");
        if (addFriend != null) Destroy(addFriend.gameObject);

        var viewAllFriends = FindDeepChild(root, "DynamicViewAllFriendsBtn");
        if (viewAllFriends != null) Destroy(viewAllFriends.gameObject);
    }

    static Text CreateOverlayRuntimeText(Transform parent, string name, string text, Vector2 anchor, Vector2 size, int fontSize, Color color)
    {
        var t = CreateRuntimeText(parent, name, text, anchor, size, fontSize, color);
        t.raycastTarget = false;
        return t;
    }

    static Button CreateRuntimeHitButton(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var img = CreateRuntimeImage(parent, name, null, anchor, size, new Color(0f, 0f, 0f, 0.004f));
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    static Button EnsureRuntimeHitButton(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        var tr = FindDeepChild(parent, name);
        if (tr == null)
            return CreateRuntimeHitButton(parent, name, anchor, size);

        tr.SetParent(parent, false);
        tr.gameObject.SetActive(true);

        var rt = tr.GetComponent<RectTransform>();
        if (rt == null) rt = tr.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var img = tr.GetComponent<Image>();
        if (img == null) img = tr.gameObject.AddComponent<Image>();
        img.sprite = null;
        img.color = new Color(0f, 0f, 0f, 0.004f);
        img.raycastTarget = true;

        var btn = tr.GetComponent<Button>();
        if (btn == null) btn = tr.gameObject.AddComponent<Button>();
        btn.interactable = true;
        btn.targetGraphic = img;
        return btn;
    }

    static void CreateRuntimeFriendRow(Transform parent, int index, Vector2 anchor)
    {
        var rowBtn = CreateRuntimeHitButton(parent, "DynamicFriendRow" + index, anchor, new Vector2(250f, 70f));
        var row = rowBtn.transform;
        CreateRuntimeImage(row, "DynamicFriendAvatar" + index,
            LobbyGenRes.Get("gen_avatar_friend_a") ?? LobbyGenRes.Get("gen_avatar_player"),
            new Vector2(0.17f, 0.54f), new Vector2(48f, 48f), Color.white);
        var level = CreateOverlayRuntimeText(row, "DynamicFriendLevelText" + index, "",
            new Vector2(0.17f, 0.18f), new Vector2(42f, 18f), 12, new Color(1f, 0.90f, 0.46f, 1f));
        level.alignment = TextAnchor.MiddleCenter;
        var name = CreateOverlayRuntimeText(row, "DynamicFriendName" + index, "",
            new Vector2(0.51f, 0.66f), new Vector2(130f, 22f), 15, new Color(0.96f, 0.91f, 0.72f, 1f));
        name.alignment = TextAnchor.MiddleLeft;
        var meta = CreateOverlayRuntimeText(row, "DynamicFriendMeta" + index, "",
            new Vector2(0.51f, 0.42f), new Vector2(130f, 20f), 12, new Color(0.76f, 0.81f, 0.76f, 1f));
        meta.alignment = TextAnchor.MiddleLeft;
        var status = CreateOverlayRuntimeText(row, "DynamicFriendStatus" + index, "",
            new Vector2(0.82f, 0.26f), new Vector2(72f, 20f), 12, new Color(0.44f, 0.92f, 0.58f, 1f));
        status.alignment = TextAnchor.MiddleRight;
    }

    static void CreateOverlayRuntimeSlider(Transform parent, string name, Vector2 anchor, Vector2 size, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        var slider = go.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.interactable = false;

        var trackSprite = KenneyUiRes.Get("bar_blue_gloss") ?? LobbyGenRes.Get("gen_progress_track");
        var bg = CreateRuntimeImage(go.transform, "Background", trackSprite, new Vector2(0.5f, 0.5f), size, Color.white, HasSpriteBorder(trackSprite));
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = Vector2.zero;
        bg.rectTransform.offsetMax = Vector2.zero;
        bg.raycastTarget = false;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var fa = fillArea.AddComponent<RectTransform>();
        fa.anchorMin = Vector2.zero;
        fa.anchorMax = Vector2.one;
        fa.offsetMin = Vector2.zero;
        fa.offsetMax = Vector2.zero;

        var fillSprite = KenneyUiRes.Get("bar_yellow_gloss") ?? LobbyGenRes.Get("gen_progress_fill");
        var fill = CreateRuntimeImage(fillArea.transform, "Fill", fillSprite, new Vector2(0.5f, 0.5f), size, fillColor, HasSpriteBorder(fillSprite));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        fill.raycastTarget = false;
        slider.fillRect = fill.rectTransform;
    }

    Transform GetLobbyCanvasRoot()
    {
        return HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
    }

    static Sprite GetBestLobbySprite(params string[] resourceNames)
    {
        if (resourceNames == null) return null;
        for (int i = 0; i < resourceNames.Length; i++)
        {
            var sprite = LobbyGenRes.Get(resourceNames[i]);
            if (sprite != null)
                return sprite;
        }
        return null;
    }

    static Sprite GetRuntimeResourceSprite(string resourcePath, Vector4 border = default(Vector4))
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        string key = resourcePath + "_" + border;
        if (RuntimeResourceSpriteCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var importedSprite = Resources.Load<Sprite>(resourcePath);
        if (importedSprite != null && border.sqrMagnitude <= 0.01f)
        {
            RuntimeResourceSpriteCache[key] = importedSprite;
            return importedSprite;
        }

        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null) return importedSprite;

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = resourcePath.Replace('/', '_');
        RuntimeResourceSpriteCache[key] = sprite;
        return sprite;
    }

    void SetBottomNavSubPanelActive(GameObject activePanel)
    {
        SafeSetActive(ShopPanel, activePanel == ShopPanel);
        SafeSetActive(WarehousePanel, activePanel == WarehousePanel);
        SafeSetActive(CampaignPanel, activePanel == CampaignPanel);
        SafeSetActive(RankPanel, activePanel == RankPanel);
    }

    bool CreateRuntimeFeaturePanelBase(
        string panelName,
        string headerSpriteName,
        string title,
        string subtitle,
        Vector2 shellSize,
        Color backdropTint,
        out GameObject panel,
        out Transform shell,
        out Button backButton)
    {
        panel = null;
        shell = null;
        backButton = null;

        Transform canvas = GetLobbyCanvasRoot();
        if (canvas == null) return false;

        panel = new GameObject(panelName);
        panel.transform.SetParent(canvas, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(0f, 60f);
        prt.offsetMax = new Vector2(0f, -88f);

        var bg = panel.AddComponent<Image>();
        bg.sprite = GetBestLobbySprite("ref_lobby_desk_no_bottom_map", "_ref_lobby_master", "gen_bg_desk");
        bg.color = backdropTint;

        shell = CreateRuntimeImage(panel.transform, panelName + "Shell",
            GetBestLobbySprite("gen_panel_frame", "gen_card_frame"),
            new Vector2(0.5f, 0.52f), shellSize, Color.white, true).transform;

        var headerPlateSprite = GetBestLobbySprite("gen_friend_row", "gen_card_frame", "gen_task_card");
        var header = CreateRuntimeImage(shell, panelName + "Header", headerPlateSprite,
            new Vector2(0.5f, 0.85f), new Vector2(Mathf.Min(shellSize.x - 170f, 650f), 82f), Color.white, true);
        header.raycastTarget = false;

        var headerIconSprite = GetBestLobbySprite(headerSpriteName);
        bool hasExactHeaderArt = headerIconSprite != null
            && headerIconSprite.name.StartsWith("gen_exact_nav_", StringComparison.Ordinal);
        if (headerIconSprite != null)
        {
            var iconSize = hasExactHeaderArt
                ? new Vector2(headerIconSprite.rect.width, headerIconSprite.rect.height)
                : new Vector2(58f, 58f);
            if (hasExactHeaderArt)
            {
                float scale = Mathf.Min(1f, Mathf.Min(270f / Mathf.Max(1f, iconSize.x), 74f / Mathf.Max(1f, iconSize.y)));
                iconSize *= scale;
            }
            var icon = CreateRuntimeImage(header.transform, panelName + "HeaderIcon", headerIconSprite,
                hasExactHeaderArt ? new Vector2(0.23f, 0.50f) : new Vector2(0.18f, 0.50f),
                iconSize, Color.white, false);
            icon.raycastTarget = false;
            UseCenterPivot(icon);
        }

        if (!hasExactHeaderArt)
        {
            var titleText = CreateRuntimeText(header.transform, panelName + "HeaderTitle", title,
                new Vector2(0.48f, 0.62f), new Vector2(260f, 32f), 28, new Color(1f, 0.90f, 0.56f, 1f));
            titleText.alignment = TextAnchor.MiddleLeft;
            UseCenterPivot(titleText);
        }

        var headerSubText = CreateRuntimeText(header.transform, panelName + "HeaderSubText", subtitle,
            hasExactHeaderArt ? new Vector2(0.68f, 0.50f) : new Vector2(0.50f, 0.30f),
            hasExactHeaderArt ? new Vector2(300f, 24f) : new Vector2(360f, 20f),
            14, new Color(0.91f, 0.84f, 0.66f, 1f));
        headerSubText.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(headerSubText);

        if (!hasExactHeaderArt)
        {
            var section = CreateRuntimeText(shell, panelName + "SectionTitle", title + "指挥台",
                new Vector2(0.18f, 0.92f), new Vector2(240f, 28f), 16, new Color(0.98f, 0.91f, 0.74f, 1f));
            section.alignment = TextAnchor.MiddleLeft;
            UseCenterPivot(section);
        }

        var dividerSprite = GetBestLobbySprite("gen_progress_fill") ?? KenneyUiRes.Get("bar_yellow_gloss");
        var divider = CreateRuntimeImage(shell, panelName + "Divider", dividerSprite,
            new Vector2(0.5f, 0.690f), new Vector2(shellSize.x - 300f, 6f), new Color(1f, 0.82f, 0.32f, 0.72f),
            HasSpriteBorder(dividerSprite));
        divider.raycastTarget = false;

        backButton = CreateRuntimeButton(shell, panelName + "BackBtn", "返回大厅",
            new Vector2(0.5f, 0.060f), new Vector2(220f, 40f));
        ApplyRuntimeButtonSkin(backButton, "button_blue_header", Color.white, new Color(0.06f, 0.14f, 0.18f, 1f));
        backButton.onClick.AddListener(ShowHall);
        panel.SetActive(false);
        return true;
    }

    Button CreateRuntimeInfoActionCard(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 size,
        Sprite icon,
        string title,
        string desc,
        string footer,
        string actionText,
        bool primaryAction,
        Action onClick)
    {
        var card = CreateRuntimeImage(parent, name,
            GetBestLobbySprite("gen_task_card", "gen_friend_row", "gen_panel_frame"),
            anchor, size, Color.white, true).transform;

        if (icon != null)
            CreateRuntimeImage(card, name + "Icon", icon,
                new Vector2(0.13f, 0.56f), new Vector2(52f, 52f), Color.white);

        var titleText = CreateRuntimeText(card, name + "Title", title,
            new Vector2(0.43f, 0.67f), new Vector2(size.x * 0.44f, 24f), 18, new Color(0.98f, 0.92f, 0.76f, 1f));
        titleText.alignment = TextAnchor.MiddleLeft;

        var descText = CreateRuntimeText(card, name + "Desc", desc,
            new Vector2(0.43f, 0.44f), new Vector2(size.x * 0.44f, 34f), 12, new Color(0.76f, 0.82f, 0.74f, 1f));
        descText.alignment = TextAnchor.MiddleLeft;

        var footerText = CreateRuntimeText(card, name + "Footer", footer,
            new Vector2(0.43f, 0.20f), new Vector2(size.x * 0.44f, 20f), 13,
            primaryAction ? new Color(1f, 0.84f, 0.34f, 1f) : new Color(0.62f, 0.90f, 1f, 1f));
        footerText.alignment = TextAnchor.MiddleLeft;

        var actionButton = CreateRuntimeButton(card, name + "Action", actionText,
            new Vector2(0.84f, 0.50f), new Vector2(92f, 36f));
        ApplyRuntimeButtonSkin(actionButton, primaryAction ? "button_yellow_header" : "button_blue_header",
            Color.white, primaryAction ? new Color(0.10f, 0.11f, 0.07f, 1f) : new Color(0.06f, 0.14f, 0.18f, 1f));

        var actionLabel = actionButton.GetComponentInChildren<Text>(true);
        if (actionLabel != null)
        {
            actionLabel.resizeTextForBestFit = true;
            actionLabel.resizeTextMinSize = 10;
            actionLabel.resizeTextMaxSize = 16;
        }

        if (onClick != null)
            actionButton.onClick.AddListener(() => onClick());
        return actionButton;
    }

    Button CreateRuntimeShopOfferRow(
        Transform parent,
        string name,
        Vector2 anchor,
        Sprite rowSprite,
        Sprite icon,
        string title,
        string desc,
        string price,
        string actionText,
        bool primaryAction,
        Action onClick)
    {
        var resolvedRowSprite = rowSprite ?? GetBestLobbySprite("gen_friend_row", "gen_task_card");
        var rowImage = CreateRuntimeImage(parent, name,
            resolvedRowSprite,
            anchor, new Vector2(760f, 72f), Color.white, HasSpriteBorder(resolvedRowSprite));
        rowImage.preserveAspect = false;
        var row = rowImage.transform;

        if (icon != null)
        {
            var iconImage = CreateRuntimeImage(row, name + "Icon", icon,
                new Vector2(0.08f, 0.52f), new Vector2(38f, 38f), Color.white);
            UseCenterPivot(iconImage);
        }

        var titleText = CreateRuntimeText(row, name + "Title", title,
            new Vector2(0.27f, 0.72f), new Vector2(260f, 22f), 17, new Color(0.98f, 0.92f, 0.76f, 1f));
        titleText.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(titleText);

        var descText = CreateRuntimeText(row, name + "Desc", desc,
            new Vector2(0.35f, 0.28f), new Vector2(390f, 20f), 12, new Color(0.78f, 0.84f, 0.76f, 1f));
        descText.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(descText);

        var priceText = CreateRuntimeText(row, name + "Price", price,
            new Vector2(0.71f, 0.72f), new Vector2(142f, 24f), 14,
            primaryAction ? new Color(1f, 0.84f, 0.34f, 1f) : new Color(0.62f, 0.90f, 1f, 1f));
        priceText.alignment = TextAnchor.MiddleRight;
        UseCenterPivot(priceText);

        var actionButton = CreateRuntimeButton(row, name + "Action", actionText,
            new Vector2(0.90f, 0.50f), new Vector2(104f, 30f));
        UseCenterPivot(actionButton);
        ApplyRuntimeButtonSkin(actionButton, primaryAction ? "button_yellow_header" : "button_blue_header",
            Color.white, primaryAction ? new Color(0.10f, 0.11f, 0.07f, 1f) : new Color(0.06f, 0.14f, 0.18f, 1f));

        var actionLabel = actionButton.GetComponentInChildren<Text>(true);
        if (actionLabel != null)
        {
            actionLabel.fontSize = 15;
            actionLabel.resizeTextMinSize = 10;
            actionLabel.resizeTextMaxSize = 15;
        }

        if (onClick != null)
            actionButton.onClick.AddListener(() => onClick());
        return actionButton;
    }

    static void UseCenterPivot(Component component)
    {
        if (component == null) return;
        var rt = component.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    void CreateRuntimeRankRow(
        Transform parent,
        string name,
        Vector2 anchor,
        int place,
        string commander,
        string title,
        int level,
        int score,
        int wins,
        string badgeResource,
        bool highlight)
    {
        var row = CreateRuntimeImage(parent, name,
            GetBestLobbySprite("gen_friend_row", "gen_task_card", "gen_panel_frame"),
            anchor, new Vector2(820f, 50f),
            highlight ? new Color(1f, 0.98f, 0.90f, 1f) : Color.white, true).transform;

        var placeText = CreateRuntimeText(row, name + "Place", "#" + place,
            new Vector2(0.08f, 0.50f), new Vector2(82f, 28f), 18,
            highlight ? new Color(1f, 0.82f, 0.38f, 1f) : new Color(0.93f, 0.91f, 0.82f, 1f));
        placeText.alignment = TextAnchor.MiddleCenter;

        string resolvedBadge = string.IsNullOrEmpty(badgeResource)
            ? ResolveRankBadge(title, level).resource
            : badgeResource;
        var badgeTexture = LoadRankBadgeTexture(resolvedBadge);
        var badgeSprite = GetOrCreateRankBadgeSprite(resolvedBadge, badgeTexture);
        if (badgeSprite != null)
            CreateRuntimeImage(row, name + "Badge", badgeSprite,
                new Vector2(0.19f, 0.50f), new Vector2(38f, 38f), Color.white);

        var nameText = CreateRuntimeText(row, name + "Commander", commander,
            new Vector2(0.42f, 0.63f), new Vector2(280f, 22f), 16, new Color(0.97f, 0.92f, 0.76f, 1f));
        nameText.alignment = TextAnchor.MiddleLeft;

        var metaText = CreateRuntimeText(row, name + "Meta", title + "  Lv." + Mathf.Max(1, level),
            new Vector2(0.42f, 0.34f), new Vector2(280f, 20f), 12, new Color(0.74f, 0.82f, 0.76f, 1f));
        metaText.alignment = TextAnchor.MiddleLeft;

        var scoreText = CreateRuntimeText(row, name + "Score", "功勋 " + score,
            new Vector2(0.82f, 0.63f), new Vector2(152f, 20f), 13, new Color(1f, 0.84f, 0.36f, 1f));
        scoreText.alignment = TextAnchor.MiddleRight;

        var winsText = CreateRuntimeText(row, name + "Wins", "胜场 " + wins,
            new Vector2(0.82f, 0.34f), new Vector2(152f, 18f), 12, new Color(0.72f, 0.90f, 1f, 1f));
        winsText.alignment = TextAnchor.MiddleRight;
    }

    bool CreateRuntimeShopPanel()
    {
        if (!CreateRuntimeFeaturePanelBase(
            "ShopPanel",
            "gen_exact_nav_shop",
            "商店",
            "军需补给 / 稀有兑换 / 每日特供",
            new Vector2(980f, 530f),
            new Color(0.28f, 0.24f, 0.18f, 1f),
            out var panel,
            out var shell,
            out var backButton))
            return false;

        ShopPanel = panel;
        ShopBackBtn = backButton;

        string goldNow = GoldText != null ? GoldText.text : "10,000";
        string gemNow = GemText != null ? GemText.text : "888";

        var stockCard = CreateRuntimeImage(shell, "ShopStockCard",
            GetBestLobbySprite("gen_friend_row", "gen_task_card"),
            new Vector2(0.5f, 0.625f), new Vector2(760f, 52f), Color.white, true).transform;
        var stockTitle = CreateRuntimeText(stockCard, "ShopStockTitle", "当前军需储备",
            new Vector2(0.16f, 0.50f), new Vector2(190f, 22f), 16, new Color(0.97f, 0.91f, 0.74f, 1f));
        stockTitle.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(stockTitle);
        var stockGoldIcon = CreateRuntimeImage(stockCard, "ShopGoldIcon", GetBestLobbySprite("gen_icon_star"),
            new Vector2(0.41f, 0.50f), new Vector2(28f, 28f), Color.white);
        UseCenterPivot(stockGoldIcon);
        var goldCount = CreateRuntimeText(stockCard, "ShopGoldCount", goldNow + " 金币",
            new Vector2(0.52f, 0.50f), new Vector2(160f, 22f), 14, new Color(1f, 0.84f, 0.36f, 1f));
        goldCount.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(goldCount);
        var stockGemIcon = CreateRuntimeImage(stockCard, "ShopGemIcon", GetBestLobbySprite("gen_icon_gem"),
            new Vector2(0.72f, 0.50f), new Vector2(28f, 28f), Color.white);
        UseCenterPivot(stockGemIcon);
        var gemCount = CreateRuntimeText(stockCard, "ShopGemCount", gemNow + " 晶石",
            new Vector2(0.84f, 0.50f), new Vector2(160f, 22f), 14, new Color(0.70f, 0.93f, 1f, 1f));
        gemCount.alignment = TextAnchor.MiddleLeft;
        UseCenterPivot(gemCount);

        var hint = CreateRuntimeText(shell, "ShopHint",
            "点击任意补给可查看报价，军需官会在每日 05:00 刷新特供。",
            new Vector2(0.5f, 0.128f), new Vector2(760f, 20f), 12, new Color(0.82f, 0.84f, 0.78f, 1f));
        hint.alignment = TextAnchor.MiddleCenter;
        UseCenterPivot(hint);

        string[] names = { "基础补给箱", "精锐装备包", "重装蓝图", "赛季通行令" };
        string[] descs =
        {
            "常规军需补给，适合前线轮换整备。",
            "钻石兑换稀有模组与车组外观。",
            "提升重型坦克与火炮的研发进度。",
            "开启额外任务链与赛季奖励。"
        };
        string[] footers = { "2,400 金币", "180 晶石", "6,200 金币", "480 晶石" };
        string[] actions = { "补给", "兑换", "研发", "激活" };
        Sprite[] icons =
        {
            GetBestLobbySprite("gen_icon_star"),
            GetBestLobbySprite("gen_icon_gem"),
            GetBestLobbySprite("gen_icon_tank"),
            GetBestLobbySprite("gen_icon_helmet")
        };
        Sprite rowBaseSprite = GetBestLobbySprite("gen_friend_row", "gen_task_card");
        Sprite[] rowSprites = { rowBaseSprite, rowBaseSprite, rowBaseSprite, rowBaseSprite };

        for (int i = 0; i < names.Length; i++)
        {
            string itemName = names[i];
            string footer = footers[i];
            bool premium = footer.Contains("晶石");
            CreateRuntimeShopOfferRow(shell, "ShopOffer" + i,
                new Vector2(0.5f, 0.515f - i * 0.098f),
                rowSprites[i], icons[i], itemName, descs[i], footer, actions[i], !premium,
                () =>
                {
                    hint.text = "商店：" + itemName + " 已加入军需清单";
                    if (MatchStatusText) MatchStatusText.text = "商店：正在查看 " + itemName;
                });
        }

        panel.SetActive(false);
        return true;
    }

    bool CreateRuntimeWarehousePanel()
    {
        if (!CreateRuntimeFeaturePanelBase(
            "WarehousePanel",
            "gen_exact_nav_warehouse",
            "仓库",
            "物资补给 / 装备库存 / 战备箱",
            new Vector2(960f, 520f),
            new Color(0.28f, 0.24f, 0.18f, 1f),
            out var panel,
            out var shell,
            out var backButton))
            return false;

        WarehousePanel = panel;
        WarehouseBackBtn = backButton;

        string[] names = { "装甲补给箱", "能源核心", "战车零件", "指挥芯片", "合金钢材", "加速模块" };
        string[] categories = { "装备补给", "战略资源", "载具维护", "战术模组", "装甲材料", "效率增幅" };
        string[] descs =
        {
            "标准前线军需箱，内含履带修复件、弹药补给与应急整备包。",
            "用于驱动基地设施与高阶科技研究，是后勤部每日重点保管物资。",
            "覆盖主战坦克、突击炮与自行火炮的常用维护套件。",
            "用于提升编队指令传输与战场部署效率的核心数据模块。",
            "强化建筑包层与战车防护时使用的高强度军工材料。",
            "可缩短研究、训练与物资调度时间的即时加速组件。"
        };
        string[] sourceTexts =
        {
            "获取：匹配奖励 / 日常任务 / 军需商店",
            "获取：战役章节 / 占领奖励 / 每周补给",
            "获取：装甲工坊 / 战损回收 / 竞技场",
            "获取：科技中心 / 赛季通行令 / 精锐补给",
            "获取：联盟捐献 / 基地生产 / 军团补给",
            "获取：限时活动 / 军需兑换 / 高级补给箱"
        };
        int[] counts = { 12, 19, 26, 33, 40, 47 };
        float[] readiness = { 0.78f, 0.92f, 0.66f, 0.81f, 0.74f, 0.88f };
        float[] rarity = { 0.42f, 0.76f, 0.58f, 0.81f, 0.63f, 0.94f };
        float[] demand = { 0.69f, 0.82f, 0.73f, 0.57f, 0.79f, 0.91f };
        Sprite[] icons =
        {
            GetBestLobbySprite("gen_nav_wh"),
            GetBestLobbySprite("gen_icon_gem"),
            GetBestLobbySprite("gen_icon_tank"),
            GetBestLobbySprite("gen_icon_tech_blueprint"),
            GetBestLobbySprite("gen_icon_star"),
            GetBestLobbySprite("gen_icon_helmet")
        };

        var overview = CreateRuntimeImage(shell, "WarehouseOverviewCard",
            GetBestLobbySprite("gen_task_card", "gen_friend_row"),
            new Vector2(0.5f, 0.60f), new Vector2(820f, 50f), Color.white, true).transform;
        var capText = CreateRuntimeText(overview, "WarehouseCapacityText", "库存容量  68 / 120",
            new Vector2(0.15f, 0.50f), new Vector2(190f, 22f), 16, new Color(0.97f, 0.91f, 0.74f, 1f));
        capText.alignment = TextAnchor.MiddleLeft;
        CreateOverlayRuntimeSlider(overview, "WarehouseCapacitySlider",
            new Vector2(0.48f, 0.50f), new Vector2(210f, 14f), new Color(0.76f, 0.66f, 0.24f, 1f));
        var capSlider = overview.Find("WarehouseCapacitySlider")?.GetComponent<Slider>();
        if (capSlider != null) capSlider.value = 68f / 120f;
        var reserveText = CreateRuntimeText(overview, "WarehouseReserveText", "战备评分  4860",
            new Vector2(0.82f, 0.50f), new Vector2(170f, 22f), 15, new Color(0.68f, 0.92f, 1f, 1f));
        reserveText.alignment = TextAnchor.MiddleRight;

        var hint = CreateRuntimeText(shell, "WarehouseHint", "仓库军需已同步，可在战斗前快速查看核心资源存量。",
            new Vector2(0.5f, 0.14f), new Vector2(760f, 22f), 14, new Color(0.82f, 0.84f, 0.78f, 1f));
        hint.alignment = TextAnchor.MiddleCenter;

        var tabBar = CreateRuntimeImage(shell, "WarehouseTabBar",
            GetBestLobbySprite("gen_friend_row", "gen_task_card"),
            new Vector2(0.47f, 0.52f), new Vector2(596f, 40f), Color.white, true);
        tabBar.raycastTarget = false;

        var listPanel = CreateRuntimeImage(shell, "WarehouseInventoryPanel",
            GetBestLobbySprite("gen_task_card", "gen_panel_frame", "gen_card_frame"),
            new Vector2(0.28f, 0.34f), new Vector2(360f, 262f), Color.white, true).transform;
        var listTitle = CreateRuntimeText(listPanel, "WarehouseListTitle", "战备清单",
            new Vector2(0.18f, 0.92f), new Vector2(120f, 20f), 16, new Color(1f, 0.90f, 0.56f, 1f));
        listTitle.alignment = TextAnchor.MiddleLeft;
        var listMeta = CreateRuntimeText(listPanel, "WarehouseListMeta", "共 6 类核心物资",
            new Vector2(0.82f, 0.92f), new Vector2(140f, 18f), 12, new Color(0.72f, 0.86f, 1f, 1f));
        listMeta.alignment = TextAnchor.MiddleRight;

        var detailPanel = CreateRuntimeImage(shell, "WarehouseDetailPanel",
            GetBestLobbySprite("gen_tech_card", "gen_panel_frame", "gen_card_frame"),
            new Vector2(0.73f, 0.34f), new Vector2(440f, 262f), Color.white, true).transform;
        var detailSection = CreateRuntimeText(detailPanel, "WarehouseDetailSection", "物资详情",
            new Vector2(0.18f, 0.92f), new Vector2(120f, 20f), 16, new Color(1f, 0.90f, 0.56f, 1f));
        detailSection.alignment = TextAnchor.MiddleLeft;

        var detailIconPlate = CreateRuntimeImage(detailPanel, "WarehouseDetailIconPlate",
            GetBestLobbySprite("gen_card_frame", "gen_task_card", "gen_friend_row"),
            new Vector2(0.18f, 0.69f), new Vector2(96f, 96f), Color.white, true).transform;
        var detailIcon = CreateRuntimeImage(detailIconPlate, "WarehouseDetailIcon", icons[0],
            new Vector2(0.50f, 0.50f), new Vector2(58f, 58f), Color.white);

        var detailTitle = CreateRuntimeText(detailPanel, "WarehouseDetailTitle", names[0],
            new Vector2(0.58f, 0.79f), new Vector2(190f, 24f), 18, new Color(0.98f, 0.92f, 0.76f, 1f));
        detailTitle.alignment = TextAnchor.MiddleLeft;
        var detailCategory = CreateRuntimeText(detailPanel, "WarehouseDetailCategory", categories[0],
            new Vector2(0.58f, 0.69f), new Vector2(190f, 18f), 12, new Color(0.72f, 0.86f, 1f, 1f));
        detailCategory.alignment = TextAnchor.MiddleLeft;
        var detailCount = CreateRuntimeText(detailPanel, "WarehouseDetailCount", "库存 x" + counts[0],
            new Vector2(0.84f, 0.69f), new Vector2(90f, 18f), 13, new Color(0.52f, 0.92f, 0.58f, 1f));
        detailCount.alignment = TextAnchor.MiddleRight;

        var detailDesc = CreateRuntimeText(detailPanel, "WarehouseDetailDesc", descs[0],
            new Vector2(0.50f, 0.51f), new Vector2(350f, 56f), 13, new Color(0.84f, 0.86f, 0.80f, 1f));
        detailDesc.alignment = TextAnchor.UpperLeft;

        string[] statLabels = { "前线储备", "稀有等级", "调度需求" };
        Color[] statColors =
        {
            new Color(0.64f, 0.86f, 0.58f, 1f),
            new Color(1f, 0.82f, 0.36f, 1f),
            new Color(0.72f, 0.86f, 1f, 1f)
        };
        Slider[] statSliders = new Slider[3];
        for (int i = 0; i < statLabels.Length; i++)
        {
            var statRow = CreateRuntimeImage(detailPanel, "WarehouseDetailStatRow" + i,
                GetBestLobbySprite("gen_friend_row", "gen_task_card"),
                new Vector2(0.50f, 0.30f - i * 0.09f), new Vector2(350f, 32f), Color.white, true).transform;
            var statLabel = CreateRuntimeText(statRow, "WarehouseDetailStatLabel" + i, statLabels[i],
                new Vector2(0.18f, 0.50f), new Vector2(90f, 18f), 12, new Color(0.94f, 0.90f, 0.82f, 1f));
            statLabel.alignment = TextAnchor.MiddleLeft;
            CreateOverlayRuntimeSlider(statRow, "WarehouseDetailStatSlider" + i,
                new Vector2(0.66f, 0.50f), new Vector2(150f, 10f), statColors[i]);
            statSliders[i] = statRow.Find("WarehouseDetailStatSlider" + i)?.GetComponent<Slider>();
        }

        var detailSource = CreateRuntimeText(detailPanel, "WarehouseDetailSource", sourceTexts[0],
            new Vector2(0.50f, 0.07f), new Vector2(350f, 34f), 11, new Color(0.80f, 0.82f, 0.76f, 1f));
        detailSource.alignment = TextAnchor.UpperLeft;

        var actionPrimary = CreateRuntimeButton(shell, "WarehouseActionPrimary", "快速装备",
            new Vector2(0.58f, 0.19f), new Vector2(126f, 36f));
        ApplyRuntimeButtonSkin(actionPrimary, "button_yellow_header", Color.white, new Color(0.10f, 0.11f, 0.07f, 1f));
        var actionSecondary = CreateRuntimeButton(shell, "WarehouseActionSecondary", "整理仓库",
            new Vector2(0.73f, 0.19f), new Vector2(126f, 36f));
        ApplyRuntimeButtonSkin(actionSecondary, "button_blue_header", Color.white, new Color(0.06f, 0.14f, 0.18f, 1f));
        var actionTertiary = CreateRuntimeButton(shell, "WarehouseActionTertiary", "军需兑换",
            new Vector2(0.88f, 0.19f), new Vector2(126f, 36f));
        ApplyRuntimeButtonSkin(actionTertiary, "button_neutral_depth", Color.white, new Color(0.08f, 0.12f, 0.13f, 1f));

        Button[] tabButtons = new Button[4];
        string[] tabNames = { "全部", "装备", "资源", "模组" };
        Action<int> setTabSelection = null;
        setTabSelection = selectedTab =>
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var button = tabButtons[i];
                if (button == null) continue;
                ApplyRuntimeButtonSkin(button,
                    i == selectedTab ? "gen_nav_tab_active" : "gen_nav_tab",
                    Color.white,
                    i == selectedTab ? new Color(0.20f, 0.16f, 0.08f, 1f) : new Color(0.88f, 0.90f, 0.86f, 1f));
            }
            hint.text = "仓库：当前筛选 " + tabNames[selectedTab] + " 分类";
            if (MatchStatusText) MatchStatusText.text = "仓库：切换到 " + tabNames[selectedTab] + " 分类";
        };

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tabIndex = i;
            var chip = CreateRuntimeButton(shell, "WarehouseTab" + i, tabNames[i],
                new Vector2(0.26f + i * 0.14f, 0.52f), new Vector2(104f, 34f));
            chip.onClick.AddListener(() => setTabSelection(tabIndex));
            tabButtons[i] = chip;
        }

        Button[] slotButtons = new Button[names.Length];
        Image[] slotImages = new Image[names.Length];
        Text[] slotNameTexts = new Text[names.Length];
        Text[] slotCountTexts = new Text[names.Length];
        Text[] slotMetaTexts = new Text[names.Length];
        Action<int> selectItem = null;
        selectItem = selectedIndex =>
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, names.Length - 1);
            for (int i = 0; i < names.Length; i++)
            {
                bool isSelected = i == selectedIndex;
                if (slotImages[i] != null)
                    slotImages[i].color = isSelected ? new Color(1f, 0.97f, 0.88f, 1f) : Color.white;
                if (slotNameTexts[i] != null)
                    slotNameTexts[i].color = isSelected ? new Color(1f, 0.88f, 0.46f, 1f) : new Color(0.98f, 0.92f, 0.76f, 1f);
                if (slotMetaTexts[i] != null)
                    slotMetaTexts[i].color = isSelected ? new Color(0.82f, 0.92f, 1f, 1f) : new Color(0.70f, 0.78f, 0.76f, 1f);
                if (slotCountTexts[i] != null)
                    slotCountTexts[i].color = isSelected ? new Color(0.58f, 0.96f, 0.64f, 1f) : new Color(0.52f, 0.92f, 0.58f, 1f);
            }

            detailIcon.sprite = icons[selectedIndex];
            detailTitle.text = names[selectedIndex];
            detailCategory.text = categories[selectedIndex];
            detailCount.text = "库存 x" + counts[selectedIndex];
            detailDesc.text = descs[selectedIndex];
            detailSource.text = sourceTexts[selectedIndex];

            if (statSliders[0] != null) statSliders[0].value = readiness[selectedIndex];
            if (statSliders[1] != null) statSliders[1].value = rarity[selectedIndex];
            if (statSliders[2] != null) statSliders[2].value = demand[selectedIndex];

            hint.text = "仓库：已选中 " + names[selectedIndex];
            if (MatchStatusText) MatchStatusText.text = "仓库：查看 " + names[selectedIndex] + " 详情";
        };

        for (int i = 0; i < names.Length; i++)
        {
            int itemIndex = i;
            int col = i % 2;
            int row = i / 2;
            var slotImage = CreateRuntimeImage(listPanel, "WarehouseSlot" + i,
                GetBestLobbySprite("gen_friend_row", "gen_task_card"),
                new Vector2(0.29f + col * 0.42f, 0.69f - row * 0.22f), new Vector2(146f, 54f), Color.white, true);
            var slotButton = slotImage.gameObject.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;
            slotImages[i] = slotImage;
            slotButtons[i] = slotButton;

            CreateRuntimeImage(slotImage.transform, "WarehouseSlotIcon" + i, icons[i],
                new Vector2(0.16f, 0.50f), new Vector2(32f, 32f), Color.white);
            var item = CreateRuntimeText(slotImage.transform, "WarehouseSlotName" + i, names[i],
                new Vector2(0.54f, 0.64f), new Vector2(92f, 16f), 12, new Color(0.98f, 0.92f, 0.76f, 1f));
            item.alignment = TextAnchor.MiddleLeft;
            slotNameTexts[i] = item;
            var meta = CreateRuntimeText(slotImage.transform, "WarehouseSlotMeta" + i, categories[i],
                new Vector2(0.54f, 0.34f), new Vector2(92f, 14f), 10, new Color(0.70f, 0.78f, 0.76f, 1f));
            meta.alignment = TextAnchor.MiddleLeft;
            slotMetaTexts[i] = meta;
            var count = CreateRuntimeText(slotImage.transform, "WarehouseSlotCount" + i, "x" + counts[i],
                new Vector2(0.84f, 0.50f), new Vector2(42f, 14f), 11, new Color(0.52f, 0.92f, 0.58f, 1f));
            count.alignment = TextAnchor.MiddleRight;
            slotCountTexts[i] = count;

            slotButton.onClick.AddListener(() => selectItem(itemIndex));
        }

        actionPrimary.onClick.AddListener(() =>
        {
            hint.text = "仓库：已将 " + detailTitle.text + " 装入当前编组";
            if (MatchStatusText) MatchStatusText.text = "仓库：快速装备完成";
        });
        actionSecondary.onClick.AddListener(() =>
        {
            hint.text = "仓库：后勤官正在整理库存与补给序列";
            if (MatchStatusText) MatchStatusText.text = "仓库：整理指令已下达";
        });
        actionTertiary.onClick.AddListener(() =>
        {
            hint.text = "仓库：已打开 " + detailTitle.text + " 的军需兑换方案";
            if (MatchStatusText) MatchStatusText.text = "仓库：准备进入军需兑换";
        });

        setTabSelection(0);
        selectItem(0);

        panel.SetActive(false);
        return true;
    }

    bool CreateRuntimeCampaignPanel()
    {
        if (!CreateRuntimeFeaturePanelBase(
            "CampaignPanel",
            "gen_exact_nav_campaign",
            "战役",
            "战区演练 / 行动档案 / 荣耀征程",
            new Vector2(990f, 540f),
            new Color(0.24f, 0.20f, 0.15f, 1f),
            out var panel,
            out var shell,
            out var backButton))
            return false;

        CampaignPanel = panel;
        CampaignBackBtn = backButton;

        var briefing = CreateRuntimeImage(shell, "CampaignBriefing",
            GetBestLobbySprite("gen_tech_card", "gen_task_card", "gen_panel_frame"),
            new Vector2(0.80f, 0.42f), new Vector2(240f, 300f), Color.white, true).transform;
        var briefingTitle = CreateRuntimeText(briefing, "CampaignBriefingTitle", "战役简报",
            new Vector2(0.50f, 0.86f), new Vector2(180f, 24f), 18, new Color(1f, 0.90f, 0.56f, 1f));
        briefingTitle.alignment = TextAnchor.MiddleCenter;
        var briefingBody = CreateRuntimeText(briefing, "CampaignBriefingBody",
            "今日推荐\n北境登陆  Lv.6\n钢铁峡谷  Lv.12\n海岛封锁  Lv.20\n\n完成章节可获得军功、蓝图与战备箱。",
            new Vector2(0.50f, 0.48f), new Vector2(190f, 180f), 14, new Color(0.84f, 0.86f, 0.80f, 1f));
        briefingBody.alignment = TextAnchor.UpperLeft;

        var hint = CreateRuntimeText(shell, "CampaignHint",
            "选择一条战役线路，指挥部会为你准备对应编组方案。",
            new Vector2(0.40f, 0.12f), new Vector2(520f, 22f), 14, new Color(0.82f, 0.84f, 0.78f, 1f));
        hint.alignment = TextAnchor.MiddleCenter;

        var routeConfigs = new[]
        {
            new { Sprite = CampaignMatchRouteSprite, Anchor = new Vector2(0.39f, 0.56f), Size = new Vector2(412f, 145f), Message = "战役：已切换到战区演练，准备选择匹配地图", Action = (Action)(() => OnQuickMatch()) },
            new { Sprite = CampaignCustomRouteSprite, Anchor = new Vector2(0.39f, 0.36f), Size = new Vector2(412f, 132f), Message = "战役：联合推演正在整编，即将开放多人协同关卡", Action = (Action)(() => { if (MatchStatusText) MatchStatusText.text = "战役：联合推演即将开放"; hint.text = "联合推演需要更多战役节点，当前版本先保留为预告位。"; }) },
            new { Sprite = CampaignGlobalRouteSprite, Anchor = new Vector2(0.39f, 0.16f), Size = new Vector2(412f, 132f), Message = "战役：已进入全球争霸部署阶段", Action = (Action)(() => OnGlobalConquest()) }
        };

        for (int i = 0; i < routeConfigs.Length; i++)
        {
            var cfg = routeConfigs[i];
            var banner = CreateRuntimeImage(shell, "CampaignRoute" + i,
                cfg.Sprite,
                cfg.Anchor, cfg.Size, cfg.Sprite != null ? Color.white : new Color(0f, 0f, 0f, 0.004f), false);
            var bannerButton = banner.gameObject.AddComponent<Button>();
            bannerButton.targetGraphic = banner;
            bannerButton.onClick.AddListener(() =>
            {
                if (MatchStatusText) MatchStatusText.text = cfg.Message;
                cfg.Action?.Invoke();
            });
        }

        panel.SetActive(false);
        return true;
    }

    bool CreateRuntimeRankPanel()
    {
        if (!CreateRuntimeFeaturePanelBase(
            "RankPanel",
            "gen_exact_nav_rank",
            "排行榜",
            "赛季军衔 / 功勋排行 / 指挥官战绩",
            new Vector2(980f, 530f),
            new Color(0.18f, 0.20f, 0.26f, 1f),
            out var panel,
            out var shell,
            out var backButton))
            return false;

        RankPanel = panel;
        RankBackBtn = backButton;

        string commander = GetCommanderDisplayName();
        int level = GetCommanderLevel();
        string title = GetCommanderRankTitle(level);

        var seasonCard = CreateRuntimeImage(shell, "RankSeasonCard",
            GetBestLobbySprite("gen_task_card", "gen_friend_row"),
            new Vector2(0.50f, 0.60f), new Vector2(840f, 58f), Color.white, true).transform;
        var seasonTitle = CreateRuntimeText(seasonCard, "RankSeasonTitle", "S3 东线军演赛季",
            new Vector2(0.22f, 0.50f), new Vector2(220f, 22f), 17, new Color(1f, 0.90f, 0.56f, 1f));
        seasonTitle.alignment = TextAnchor.MiddleLeft;
        var seasonMeta = CreateRuntimeText(seasonCard, "RankSeasonMeta", "每周一 05:00 结算军功与战绩",
            new Vector2(0.68f, 0.50f), new Vector2(330f, 20f), 13, new Color(0.74f, 0.86f, 1f, 1f));
        seasonMeta.alignment = TextAnchor.MiddleRight;

        var topRows = new[]
        {
            new { Place = 1, Name = "IronWolf", Rank = "鹰徽元帅", Level = 37, Score = 2190, Wins = 128, Badge = "rank_badge_05_marshal_eagle" },
            new { Place = 2, Name = "BlueHawk", Rank = "重机枪军徽", Level = 33, Score = 2044, Wins = 116, Badge = "rank_badge_09_heavy_mg" },
            new { Place = 3, Name = "TigerAce", Rank = "双枪军徽", Level = 28, Score = 1896, Wins = 104, Badge = "rank_badge_08_crossed_pistols" },
            new { Place = 4, Name = "SteelRain", Rank = "冲锋军徽", Level = 22, Score = 1710, Wins = 93, Badge = "rank_badge_07_crossed_smg" }
        };

        for (int i = 0; i < topRows.Length; i++)
        {
            var row = topRows[i];
            CreateRuntimeRankRow(shell, "RankTopRow" + i,
                new Vector2(0.50f, 0.49f - i * 0.11f),
                row.Place, row.Name, row.Rank, row.Level, row.Score, row.Wins, row.Badge, false);
        }

        CreateRuntimeRankRow(shell, "RankPlayerRow",
            new Vector2(0.50f, 0.16f),
            27, commander, title, level, 1280 + level * 7, 42 + level, null, true);

        var hint = CreateRuntimeText(shell, "RankHint", "排行榜每场战斗后自动刷新，你的个人战绩已在下方高亮。",
            new Vector2(0.50f, 0.10f), new Vector2(760f, 20f), 13, new Color(0.82f, 0.84f, 0.78f, 1f));
        hint.alignment = TextAnchor.MiddleCenter;

        panel.SetActive(false);
        return true;
    }

    bool CreateRuntimeMapPanel()
    {
        Transform canvas = HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
        if (canvas == null) return false;

        // 有些大厅场景使用整张参考图做主界面，旧的 MapPanel 没有序列化绑定。
        // 这里运行时补一个真正可点击的地图面板，避免把英文调试提示露给玩家。
        var panel = new GameObject("MapPanel");
        panel.transform.SetParent(canvas, false);
        var prt = panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(0f, 60f);
        prt.offsetMax = new Vector2(0f, -88f);
        var bg = panel.AddComponent<Image>();
        bg.sprite = LobbyGenRes.Get("ref_lobby_desk_no_bottom_map") ?? LobbyGenRes.Get("_ref_lobby_master");
        bg.color = new Color(0.12f, 0.13f, 0.09f, 0.92f);

        var shell = CreateRuntimeImage(panel.transform, "MapShell",
            KenneyUiRes.Get("button_neutral_depth", new Vector4(18f, 18f, 18f, 18f)) ?? LobbyGenRes.Get("gen_panel_frame"),
            new Vector2(0.5f, 0.52f), new Vector2(960f, 540f), new Color(0.22f, 0.46f, 0.56f, 0.96f), true).transform;
        var shade = CreateRuntimeImage(shell, "MapShellInnerShade", null,
            new Vector2(0.5f, 0.46f), new Vector2(900f, 390f), new Color(0.02f, 0.08f, 0.12f, 0.30f), true);
        shade.raycastTarget = false;
        CreateRuntimeText(shell, "MapTitle", "选择战场",
            new Vector2(0.5f, 0.89f), new Vector2(420f, 64f), 42, new Color(1f, 0.88f, 0.42f, 1f));
        var subtitle = CreateRuntimeText(shell, "MapSubtitle", "选择地图后开始联网匹配",
            new Vector2(0.5f, 0.80f), new Vector2(560f, 30f), 18, new Color(0.92f, 0.96f, 1f, 1f));
        subtitle.alignment = TextAnchor.MiddleCenter;
        CreateRuntimeImage(shell, "MapPanelKenneyLine", KenneyUiRes.Get("bar_blue_gloss"),
            new Vector2(0.5f, 0.745f), new Vector2(620f, 12f), new Color(0.85f, 1f, 1f, 0.95f), true);

        string[] maps = BattleMapCatalog.GetPlayableMapNames();
        int count = Mathf.Max(1, maps != null ? maps.Length : 0);
        MapButtons = new Button[count];
        for (int i = 0; i < count; i++)
        {
            int idx = i;
            string mapName = maps != null && i < maps.Length ? maps[i] : BattleMapCatalog.DefaultMapName;
            int col = i % 2;
            int row = i / 2;
            var btn = CreateRuntimeButton(shell, "MapButton" + i, mapName,
                new Vector2(0.34f + col * 0.32f, 0.60f - row * 0.16f), new Vector2(260f, 66f));
            ApplyRuntimeButtonSkin(btn, "button_blue_header", Color.white, new Color(0.06f, 0.14f, 0.18f, 1f));
            btn.onClick.AddListener(() => SelectMap(idx));
            MapButtons[i] = btn;
        }

        StartMatchButton = CreateRuntimeButton(shell, "StartMatchButton", "开始匹配",
            new Vector2(0.70f, 0.105f), new Vector2(220f, 46f));
        ApplyRuntimeButtonSkin(StartMatchButton, "button_yellow_header", Color.white, new Color(0.10f, 0.11f, 0.07f, 1f));
        StartMatchButton.onClick.AddListener(OnStartMatch);
        BackToHallButton = CreateRuntimeButton(shell, "BackToHallButton", "返回大厅",
            new Vector2(0.30f, 0.105f), new Vector2(220f, 46f));
        ApplyRuntimeButtonSkin(BackToHallButton, "button_blue_header", Color.white, new Color(0.06f, 0.14f, 0.18f, 1f));
        BackToHallButton.onClick.AddListener(ShowHall);

        MapPanel = panel;
        panel.SetActive(false);
        SelectMap(0);
        return true;
    }

    void ApplyLobbyGenUiSprites()
    {
        Transform root = HallPanel != null ? HallPanel.transform.root : null;
        if (root == null)
        {
            var go = GameObject.Find("LobbyCanvas");
            if (go != null) root = go.transform;
        }
        if (root == null) return;

        SetImageSpriteIfLoaded(root, "GoldIcon", "gen_icon_star");
        SetImageSpriteIfLoaded(root, "GemIcon", "gen_icon_gem");
        SetImageSpriteIfLoaded(root, "FriendsHeaderIcon", "gen_icon_clipboard");
        SetImageSpriteIfLoaded(root, "PortraitRing", "gen_portrait_ring");
        SetImageSpriteIfLoaded(root, "PortraitFace", "gen_avatar_player");
        SetImageSpriteIfLoaded(root, "TaskSectionIcon", "gen_icon_clipboard");
        SetImageSpriteIfLoaded(root, "TechSectionIcon", "gen_icon_gear");
        SetImageSpriteIfLoaded(root, "TechGearIcon", "gen_icon_gear");
        SetImageSpriteIfLoaded(root, "TechBlueprintIcon", "gen_icon_tech_blueprint");
        SetImageSpriteIfLoaded(root, "NotifyDot", "gen_dot");
        ApplyMissionCardSkin(root);

        SetChildIconSprite(root, "SettingsIconBtn", "Icon", "gen_icon_gear");
        SetChildIconSprite(root, "NotifyBellBtn", "Icon", "gen_icon_bell");

        var cardMatch = FindDeepChild(root, "CardMatch");
        if (cardMatch != null)
        {
            var ci = cardMatch.Find("CardIcon");
            if (ci != null)
            {
                var img = ci.GetComponent<Image>();
                var sp = LobbyGenRes.Get("gen_icon_match");
                if (img != null && sp != null) { img.sprite = sp; img.preserveAspect = true; img.color = new Color(1f, 0.96f, 0.88f, 0.92f); }
            }
        }
        var cardCustom = FindDeepChild(root, "CardCustom");
        if (cardCustom != null)
        {
            var ci = cardCustom.Find("CardIcon");
            if (ci != null)
            {
                var img = ci.GetComponent<Image>();
                var sp = LobbyGenRes.Get("gen_icon_custom");
                if (img != null && sp != null) { img.sprite = sp; img.preserveAspect = true; img.color = new Color(1f, 0.96f, 0.88f, 0.92f); }
            }
        }
        var cardGlobal = FindDeepChild(root, "CardGlobal");
        if (cardGlobal != null)
        {
            var ci = cardGlobal.Find("CardIcon");
            if (ci != null)
            {
                var img = ci.GetComponent<Image>();
                var sp = LobbyGenRes.Get("gen_icon_war");
                if (img != null && sp != null) { img.sprite = sp; img.preserveAspect = true; img.color = new Color(1f, 0.96f, 0.88f, 0.92f); }
            }
        }

        for (int i = 0; i < 3; i++)
        {
            var row = FindDeepChild(root, "TaskRow" + i);
            if (row == null) continue;
            var ic = row.Find("TaskIcon" + i);
            if (ic == null) continue;
            var img = ic.GetComponent<Image>();
            if (img == null) continue;
            string res = i == 0 ? "gen_icon_task_crate" : (i == 1 ? "gen_nav_wh" : "gen_icon_tank");
            var sp = LobbyGenRes.Get(res);
            if (sp != null) { img.sprite = sp; img.preserveAspect = true; img.color = Color.white; }
        }

        SetChildIconSprite(root, "NavShopBtn", "NavIcon", "gen_nav_shop");
        SetChildIconSprite(root, "NavWarehouseBtn", "NavIcon", "gen_nav_wh");
        SetChildIconSprite(root, "NavCampaignBtn", "NavIcon", "gen_nav_map");
        SetChildIconSprite(root, "NavRankBtn", "NavIcon", "gen_nav_rank");
        SetChildIconSprite(root, "NavMailBtn", "NavIcon", "gen_nav_mail");
        ApplyPremiumNavButton(root, "NavShopBtn", "商店", "gen_nav_shop");
        ApplyWarehouseNavButton(root);
        ApplyPremiumNavButton(root, "NavRankBtn", "排行榜", "gen_nav_rank");
        RepairVisibleLobbyText(root);
    }

    void ApplyLobbyFriendsLayout()
    {
        if (FriendListText == null) return;
        FriendListText.alignment = TextAnchor.UpperLeft;
        FriendListText.horizontalOverflow = HorizontalWrapMode.Wrap;
        FriendListText.verticalOverflow = VerticalWrapMode.Overflow;
        FriendListText.supportRichText = true;
        if (FriendListText.fontSize < 13) FriendListText.fontSize = 13;
        var rt = FriendListText.rectTransform;
        rt.offsetMin = new Vector2(10f, 10f);
        rt.offsetMax = new Vector2(-10f, -12f);
    }

    bool EnsureSceneFriendView()
    {
        if (_friendView != null && _friendViewScrollRect != null && _friendViewContent != null)
            return true;

        Transform root = HallPanel != null ? HallPanel.transform.root : GameObject.Find("LobbyCanvas")?.transform;
        if (root == null) return false;

        var friendView = FindFirstNamedChild(root, "friendview", "FriendView", "Scroll View");
        if (friendView == null)
        {
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll == null) continue;
                var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform.Find("Viewport") as RectTransform;
                var content = scroll.content != null
                    ? scroll.content
                    : (viewport != null ? viewport.Find("Content") as RectTransform : scroll.transform.Find("Content") as RectTransform);
                if (viewport != null && content != null)
                {
                    friendView = scroll.transform;
                    break;
                }
            }
        }
        if (friendView == null) return false;

        _friendView = friendView.gameObject;
        _friendViewScrollRect = friendView.GetComponent<ScrollRect>();
        _friendViewViewport = friendView.Find("Viewport") as RectTransform;
        _friendViewContent = _friendViewViewport != null
            ? _friendViewViewport.Find("Content") as RectTransform
            : friendView.Find("Content") as RectTransform;
        _friendViewScrollbar = friendView.Find("Scrollbar Vertical")?.GetComponent<Scrollbar>();

        if (_friendViewScrollRect == null || _friendViewViewport == null || _friendViewContent == null)
            return false;

        _friendViewScrollRect.horizontal = false;
        _friendViewScrollRect.vertical = true;
        _friendViewScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _friendViewScrollRect.scrollSensitivity = 24f;

        _friendViewViewport.anchorMin = Vector2.zero;
        _friendViewViewport.anchorMax = Vector2.one;
        _friendViewViewport.pivot = new Vector2(0.5f, 0.5f);
        _friendViewViewport.anchoredPosition = Vector2.zero;
        _friendViewViewport.offsetMin = new Vector2(8f, 8f);
        _friendViewViewport.offsetMax = new Vector2(_friendViewScrollbar != null ? -24f : -8f, -8f);

        var viewportMask = _friendViewViewport.GetComponent<RectMask2D>();
        if (viewportMask == null) viewportMask = _friendViewViewport.gameObject.AddComponent<RectMask2D>();

        var viewportImage = _friendViewViewport.GetComponent<Image>();
        if (viewportImage == null) viewportImage = _friendViewViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.02f, 0.04f, 0.02f, 0.12f);
        viewportImage.raycastTarget = true;

        _friendViewContent.anchorMin = new Vector2(0f, 1f);
        _friendViewContent.anchorMax = new Vector2(1f, 1f);
        _friendViewContent.pivot = new Vector2(0.5f, 1f);
        _friendViewContent.anchoredPosition = Vector2.zero;
        _friendViewContent.sizeDelta = new Vector2(0f, 0f);

        var layout = _friendViewContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null) layout = _friendViewContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = FriendViewRowSpacing;
        int pad = (int)FriendViewRowPadding;
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = _friendViewContent.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = _friendViewContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var horizontalScrollbar = friendView.Find("Scrollbar Horizontal");
        if (horizontalScrollbar != null)
            horizontalScrollbar.gameObject.SetActive(false);

        if (_friendViewScrollbar != null)
        {
            var barRt = _friendViewScrollbar.GetComponent<RectTransform>();
            if (barRt != null)
            {
                barRt.anchorMin = new Vector2(1f, 0f);
                barRt.anchorMax = new Vector2(1f, 1f);
                barRt.pivot = new Vector2(1f, 1f);
                barRt.anchoredPosition = Vector2.zero;
                barRt.sizeDelta = new Vector2(18f, 0f);
            }
            _friendViewScrollbar.direction = Scrollbar.Direction.BottomToTop;
        }

        _friendViewScrollRect.viewport = _friendViewViewport;
        _friendViewScrollRect.content = _friendViewContent;
        _friendViewScrollRect.horizontalScrollbar = null;
        _friendViewScrollRect.verticalScrollbar = _friendViewScrollbar;
        _friendViewScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        return true;
    }

    void SetSceneFriendViewVisible(bool visible)
    {
        if (!EnsureSceneFriendView()) return;

        _friendView.SetActive(visible);
        if (_friendViewScrollbar != null)
            _friendViewScrollbar.gameObject.SetActive(visible);

        SetCompactFriendRowsVisible(!visible);

        Transform root = HallPanel != null ? HallPanel.transform.root : ViewAllFriendsBtn?.transform.root;
        if (root != null)
            SetButtonLabel(root, "ViewAllFriendsBtn", visible ? "收起好友" : "查看全部好友");

        if (visible && _friendViewScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _friendViewScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void SetCompactFriendRowsVisible(bool visible)
    {
        Transform root = HallPanel != null ? HallPanel.transform.root : GameObject.Find("LobbyCanvas")?.transform;
        if (root == null) return;

        var rowsRoot = FindDeepChild(root, "FriendRows");
        if (rowsRoot != null)
            rowsRoot.gameObject.SetActive(visible);
    }

    void ToggleFriendsView()
    {
        if (EnsureSceneFriendView())
        {
            if (_friendView != null && _friendView.activeSelf) CloseFriends();
            else OpenFriends();
            return;
        }

        OpenFriends();
    }

    GameObject EnsureFriendViewRow(int index)
    {
        while (_friendViewRows.Count <= index)
            _friendViewRows.Add(null);

        if (_friendViewRows[index] != null)
            return _friendViewRows[index];

        var row = new GameObject("FriendViewRow" + index);
        row.transform.SetParent(_friendViewContent, false);

        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, FriendViewRowHeight);

        var layout = row.AddComponent<LayoutElement>();
        layout.minHeight = FriendViewRowHeight;
        layout.preferredHeight = FriendViewRowHeight;
        layout.flexibleWidth = 1f;

        var image = row.AddComponent<Image>();
        var rowSprite = LobbyGenRes.Get("gen_friend_row")
            ?? KenneyUiRes.Get("button_neutral_depth", new Vector4(16f, 16f, 16f, 16f));
        image.sprite = rowSprite;
        image.type = rowSprite != null && HasSpriteBorder(rowSprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = rowSprite != null ? Color.white : new Color(0.06f, 0.07f, 0.04f, 0.92f);

        var button = row.AddComponent<Button>();
        button.targetGraphic = image;

        var avatarRingSprite = KenneyUiRes.Get("button_blue_header", new Vector4(14f, 14f, 14f, 14f))
            ?? LobbyGenRes.Get("gen_portrait_ring");
        var avatarRing = CreateRuntimeImage(row.transform, "AvatarRing",
            avatarRingSprite, new Vector2(0f, 0.5f), new Vector2(52f, 52f), Color.white, HasSpriteBorder(avatarRingSprite));
        avatarRing.rectTransform.anchoredPosition = new Vector2(32f, 0f);
        avatarRing.raycastTarget = false;

        var avatar = CreateRuntimeImage(row.transform, "Avatar",
            LobbyGenRes.Get("gen_avatar_player"), new Vector2(0f, 0.5f), new Vector2(44f, 44f), Color.white);
        avatar.rectTransform.anchoredPosition = new Vector2(32f, 0f);
        avatar.preserveAspect = true;
        avatar.raycastTarget = false;

        var level = CreateRuntimeText(row.transform, "Level", "",
            new Vector2(0f, 0f), new Vector2(42f, 16f), 11, new Color(1f, 0.90f, 0.46f, 1f));
        level.alignment = TextAnchor.MiddleCenter;
        level.rectTransform.anchoredPosition = new Vector2(32f, 4f);
        level.raycastTarget = false;

        var name = CreateRuntimeText(row.transform, "Name", "",
            new Vector2(0f, 1f), new Vector2(122f, 21f), 15, new Color(0.96f, 0.91f, 0.72f, 1f));
        name.alignment = TextAnchor.MiddleLeft;
        name.rectTransform.anchoredPosition = new Vector2(66f, -9f);
        name.raycastTarget = false;

        var meta = CreateRuntimeText(row.transform, "Meta", "",
            new Vector2(0f, 0f), new Vector2(122f, 18f), 11, new Color(0.76f, 0.81f, 0.76f, 1f));
        meta.alignment = TextAnchor.MiddleLeft;
        meta.rectTransform.anchoredPosition = new Vector2(66f, 9f);
        meta.raycastTarget = false;

        var status = CreateRuntimeText(row.transform, "Status", "",
            new Vector2(1f, 0.5f), new Vector2(62f, 18f), 11, new Color(0.44f, 0.92f, 0.58f, 1f));
        status.alignment = TextAnchor.MiddleRight;
        status.rectTransform.anchoredPosition = new Vector2(-11f, -1f);
        status.raycastTarget = false;

        var empty = CreateRuntimeText(row.transform, "Empty", "",
            new Vector2(0.5f, 0.5f), new Vector2(214f, 22f), 13, new Color(0.82f, 0.82f, 0.66f, 1f));
        empty.alignment = TextAnchor.MiddleCenter;
        empty.raycastTarget = false;
        empty.gameObject.SetActive(false);

        _friendViewRows[index] = row;
        return row;
    }

    void PopulateFriendViewRow(GameObject row, string name, string meta, string status, int level, Sprite avatar, string emptyText)
    {
        if (row == null) return;

        bool isEmpty = !string.IsNullOrEmpty(emptyText);
        var avatarImage = row.transform.Find("Avatar")?.GetComponent<Image>();
        var avatarRingImage = row.transform.Find("AvatarRing")?.GetComponent<Image>();
        var levelText = row.transform.Find("Level")?.GetComponent<Text>();
        var nameText = row.transform.Find("Name")?.GetComponent<Text>();
        var metaText = row.transform.Find("Meta")?.GetComponent<Text>();
        var statusText = row.transform.Find("Status")?.GetComponent<Text>();
        var emptyLabel = row.transform.Find("Empty")?.GetComponent<Text>();
        var button = row.GetComponent<Button>();

        if (avatarImage != null) avatarImage.gameObject.SetActive(!isEmpty);
        if (avatarRingImage != null) avatarRingImage.gameObject.SetActive(!isEmpty);
        if (levelText != null) levelText.gameObject.SetActive(!isEmpty);
        if (nameText != null) nameText.gameObject.SetActive(!isEmpty);
        if (metaText != null) metaText.gameObject.SetActive(!isEmpty);
        if (statusText != null) statusText.gameObject.SetActive(!isEmpty);
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(isEmpty);

        if (isEmpty)
        {
            if (emptyLabel != null) emptyLabel.text = emptyText;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
            row.SetActive(true);
            return;
        }

        if (avatarImage != null)
        {
            avatarImage.sprite = avatar != null ? avatar : LobbyGenRes.Get("gen_avatar_player");
            avatarImage.preserveAspect = true;
            avatarImage.color = Color.white;
        }

        if (levelText != null) levelText.text = "Lv." + Mathf.Max(1, level);
        if (nameText != null) nameText.text = string.IsNullOrEmpty(name) ? "未知玩家" : name;
        if (metaText != null) metaText.text = string.IsNullOrEmpty(meta) ? "列兵" : meta;
        if (statusText != null)
        {
            statusText.text = string.IsNullOrEmpty(status) ? "离线" : status;
            statusText.color = GetLobbyFriendStatusColor(statusText.text);
        }

        if (button != null)
        {
            string clickName = string.IsNullOrEmpty(name) ? "未知玩家" : name;
            string clickMeta = string.IsNullOrEmpty(meta) ? "" : meta;
            string clickStatus = string.IsNullOrEmpty(status) ? "离线" : status;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (MatchStatusText)
                    MatchStatusText.text = string.IsNullOrEmpty(clickMeta)
                        ? $"好友：{clickName}  {clickStatus}"
                        : $"好友：{clickName}  {clickMeta}  {clickStatus}";
            });
            button.interactable = true;
        }

        row.SetActive(true);
    }

    void RefreshSceneFriendView(List<SimpleJson> friends, bool isGuest)
    {
        if (!EnsureSceneFriendView()) return;
        friends = NormalizeFriendList(friends);

        Sprite[] avatars = {
            LobbyGenRes.Get("gen_avatar_player"),
            LobbyGenRes.Get("gen_avatar_friend_a"),
            LobbyGenRes.Get("gen_avatar_friend_b")
        };

        int visibleCount = friends == null || friends.Count == 0 ? 1 : friends.Count;
        for (int i = 0; i < visibleCount; i++)
        {
            var row = EnsureFriendViewRow(i);
            if (friends == null || friends.Count == 0)
            {
                PopulateFriendViewRow(row, null, null, null, 1, null,
                    isGuest ? "游客模式暂无好友数据" : "暂无好友");
                continue;
            }

            var f = friends[i];
            string uname = string.IsNullOrEmpty(f.username) ? "未知玩家" : f.username;
            string meta = !string.IsNullOrEmpty(f.rank) ? f.rank : "列兵";
            string status = !string.IsNullOrEmpty(f.status) ? f.status : "离线";
            int level = f.level ?? 1;
            PopulateFriendViewRow(row, uname, meta, status, level, avatars[i % avatars.Length], null);
        }

        for (int i = visibleCount; i < _friendViewRows.Count; i++)
        {
            if (_friendViewRows[i] != null)
                _friendViewRows[i].SetActive(false);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_friendViewContent);
        if (_friendView.activeSelf && _friendViewScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _friendViewScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void EnsureCompactFriendListOverlay(Transform root)
    {
        if (root == null) return;

        DisableCompactFriendScrollbars(root);

        var rowsRoot = FindDeepChild(root, "FriendRows");
        if (rowsRoot == null)
        {
            var rowsGo = new GameObject("FriendRows");
            rowsGo.transform.SetParent(root, false);
            rowsRoot = rowsGo.transform;
        }
        else
        {
            rowsRoot.SetParent(root, false);
            rowsRoot.gameObject.SetActive(true);
        }

        var rowsRt = rowsRoot as RectTransform;
        if (rowsRt == null) rowsRt = rowsRoot.gameObject.AddComponent<RectTransform>();
        rowsRt.anchorMin = Vector2.zero;
        rowsRt.anchorMax = Vector2.one;
        rowsRt.pivot = new Vector2(0.5f, 0.5f);
        rowsRt.anchoredPosition = Vector2.zero;
        rowsRt.offsetMin = Vector2.zero;
        rowsRt.offsetMax = Vector2.zero;

        float[] rowY = { 0.681f, 0.575f, 0.469f, 0.363f, 0.257f };
        for (int i = 0; i < 5; i++)
            EnsureCompactFriendRow(rowsRoot, root, i, new Vector2(0.125f, rowY[i]), new Vector2(190f, 64f));

        _friendRows = null;
        _friendNameTexts = null;
        _friendMetaTexts = null;
        _friendStatusTexts = null;
        _friendLevelTexts = null;
        _friendAvatarImages = null;
        _friendClickButtons = null;
    }


    void DisableCompactFriendScrollbars(Transform root)
    {
        Transform panel = FindDeepChild(root, "friend")
            ?? FindDeepChild(root, "FriendPanel")
            ?? FindDeepChild(root, "RuntimeFriendsPanelCard");
        if (panel == null) return;

        foreach (var scroll in panel.GetComponentsInChildren<ScrollRect>(true))
        {
            scroll.horizontal = false;
            scroll.vertical = false;
            if (scroll.horizontalScrollbar != null)
                scroll.horizontalScrollbar.gameObject.SetActive(false);
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbar.gameObject.SetActive(false);
        }

        foreach (var scrollbar in panel.GetComponentsInChildren<Scrollbar>(true))
            scrollbar.gameObject.SetActive(false);
    }

    static void EnsureCompactFriendRow(Transform parent, Transform searchRoot, int index, Vector2 anchor, Vector2 size)
    {
        var rowName = "FriendRow" + index;
        var dynamicRowName = "DynamicFriendRow" + index;
        var existing = parent != null ? parent.Find(rowName) : null;
        if (existing == null && parent != null)
            existing = parent.Find(dynamicRowName);
        if (existing == null && searchRoot != null)
            existing = FindDeepChild(searchRoot, rowName) ?? FindDeepChild(searchRoot, dynamicRowName);

        Transform row;
        bool created = false;
        if (existing != null)
        {
            row = existing;
            row.SetParent(parent, false);
            row.gameObject.SetActive(true);
            var existingRt = row as RectTransform;
            if (existingRt == null) existingRt = row.gameObject.AddComponent<RectTransform>();
            ConfigureOverlayRect(existingRt, anchor, size);
        }
        else
        {
            var rowButton = CreateRuntimeHitButton(parent, rowName, anchor, size);
            row = rowButton.transform;
            created = true;
        }

        var rowImage = row.GetComponent<Image>();
        if (rowImage == null) rowImage = row.gameObject.AddComponent<Image>();
        var rowSprite = LobbyGenRes.Get("gen_friend_row")
            ?? KenneyUiRes.Get("button_neutral_depth", new Vector4(16f, 16f, 16f, 16f));
        rowImage.sprite = rowSprite;
        rowImage.type = rowSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        rowImage.color = rowSprite != null ? Color.white : new Color(0.04f, 0.05f, 0.03f, 0.92f);
        rowImage.raycastTarget = true;

        var button = row.GetComponent<Button>();
        if (button == null) button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = rowImage;

        if (!created)
        {
            RetuneCompactFriendRowChildren(row, index);
            return;
        }

        CreateRuntimeImage(row, "FriendAvatar" + index,
            LobbyGenRes.Get("gen_avatar_friend_a") ?? LobbyGenRes.Get("gen_avatar_player"),
            new Vector2(0.17f, 0.54f), new Vector2(44f, 44f), Color.white);

        var level = CreateOverlayRuntimeText(row, "FriendLevelText" + index, "",
            new Vector2(0.17f, 0.19f), new Vector2(40f, 16f), 11, new Color(1f, 0.90f, 0.46f, 1f));
        level.alignment = TextAnchor.MiddleCenter;

        var name = CreateOverlayRuntimeText(row, "FriendName" + index, "",
            new Vector2(0.53f, 0.66f), new Vector2(108f, 20f), 14, new Color(0.96f, 0.91f, 0.72f, 1f));
        name.alignment = TextAnchor.MiddleLeft;

        var meta = CreateOverlayRuntimeText(row, "FriendMeta" + index, "",
            new Vector2(0.53f, 0.39f), new Vector2(108f, 18f), 11, new Color(0.76f, 0.81f, 0.76f, 1f));
        meta.alignment = TextAnchor.MiddleLeft;

        var status = CreateOverlayRuntimeText(row, "FriendStatus" + index, "",
            new Vector2(0.91f, 0.31f), new Vector2(54f, 18f), 11, new Color(0.44f, 0.92f, 0.58f, 1f));
        status.alignment = TextAnchor.MiddleRight;
        RetuneCompactFriendRowChildren(row, index);
    }

    static void RetuneCompactFriendRowChildren(Transform row, int index)
    {
        RetuneCompactFriendImage(row, "FriendAvatar" + index, "DynamicFriendAvatar" + index,
            new Vector2(0.17f, 0.54f), new Vector2(44f, 44f));
        RetuneCompactFriendText(row, "FriendLevelText" + index, "DynamicFriendLevelText" + index,
            new Vector2(0.17f, 0.19f), new Vector2(40f, 16f), 11, TextAnchor.MiddleCenter);
        RetuneCompactFriendText(row, "FriendName" + index, "DynamicFriendName" + index,
            new Vector2(0.57f, 0.66f), new Vector2(96f, 20f), 14, TextAnchor.MiddleLeft);
        RetuneCompactFriendText(row, "FriendMeta" + index, "DynamicFriendMeta" + index,
            new Vector2(0.57f, 0.39f), new Vector2(96f, 18f), 11, TextAnchor.MiddleLeft);
        RetuneCompactFriendText(row, "FriendStatus" + index, "DynamicFriendStatus" + index,
            new Vector2(0.93f, 0.31f), new Vector2(54f, 18f), 11, TextAnchor.MiddleRight);
    }

    static void RetuneCompactFriendImage(Transform row, string primaryName, string fallbackName, Vector2 anchor, Vector2 size)
    {
        var tr = FindDeepChild(row, primaryName) ?? FindDeepChild(row, fallbackName);
        var image = tr != null ? tr.GetComponent<Image>() : null;
        if (image == null) return;
        ConfigureOverlayRect(image.rectTransform, anchor, size);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    static void RetuneCompactFriendText(Transform row, string primaryName, string fallbackName,
        Vector2 anchor, Vector2 size, int maxFontSize, TextAnchor alignment)
    {
        var tr = FindDeepChild(row, primaryName) ?? FindDeepChild(row, fallbackName);
        var text = tr != null ? tr.GetComponent<Text>() : null;
        if (text == null) return;
        ConfigureOverlayText(text, anchor, size, maxFontSize, alignment);
        text.raycastTarget = false;
    }


    void BindFriendRowViews()
    {
        if (_friendRows != null && _friendRows.Length > 0) return;

        _friendRows = new GameObject[5];
        _friendNameTexts = new Text[5];
        _friendMetaTexts = new Text[5];
        _friendStatusTexts = new Text[5];
        _friendLevelTexts = new Text[5];
        _friendAvatarImages = new Image[5];
        _friendClickButtons = new Button[5];
        _friendNames = new string[5];
        _friendMetas = new string[5];
        _friendStatuses = new string[5];
        _friendLevels = new int[5];

        Transform root = HallPanel != null ? HallPanel.transform.root : transform;
        for (int i = 0; i < 5; i++)
        {
            _friendRows[i] = (FindDeepChild(root, "FriendRow" + i) ?? FindDeepChild(root, "DynamicFriendRow" + i))?.gameObject;
            _friendNameTexts[i] = (FindDeepChild(root, "FriendName" + i) ?? FindDeepChild(root, "DynamicFriendName" + i))?.GetComponent<Text>();
            _friendMetaTexts[i] = (FindDeepChild(root, "FriendMeta" + i) ?? FindDeepChild(root, "DynamicFriendMeta" + i))?.GetComponent<Text>();
            _friendStatusTexts[i] = (FindDeepChild(root, "FriendStatus" + i) ?? FindDeepChild(root, "DynamicFriendStatus" + i))?.GetComponent<Text>();
            _friendLevelTexts[i] = (FindDeepChild(root, "FriendLevelText" + i) ?? FindDeepChild(root, "DynamicFriendLevelText" + i))?.GetComponent<Text>();
            _friendAvatarImages[i] = (FindDeepChild(root, "FriendAvatar" + i) ?? FindDeepChild(root, "DynamicFriendAvatar" + i))?.GetComponent<Image>();
            _friendClickButtons[i] = _friendRows[i]?.GetComponent<Button>();
        }
        FriendClickButtons = _friendClickButtons;
    }

    void SetFriendRow(int index, string name, string meta, string status, int level, Sprite avatar)
    {
        if (_friendRows == null || index < 0 || index >= _friendRows.Length) return;
        bool active = !string.IsNullOrEmpty(name);
        if (_friendRows[index] != null) _friendRows[index].SetActive(active);
        if (_friendNames != null) _friendNames[index] = active ? name : "";
        if (_friendMetas != null) _friendMetas[index] = active ? meta : "";
        if (_friendStatuses != null) _friendStatuses[index] = active ? status : "";
        bool showLevel = active && level > 0;
        if (_friendLevels != null) _friendLevels[index] = showLevel ? Mathf.Max(1, level) : 0;
        if (_friendClickButtons?[index] != null) _friendClickButtons[index].interactable = active;
        var toolkit = UiToolkit;
        if (!active)
        {
            toolkit?.SetFriendSlot(index, "", "", "", 1);
            return;
        }

        if (_friendNameTexts?[index] != null) _friendNameTexts[index].text = name;
        if (_friendMetaTexts?[index] != null) _friendMetaTexts[index].text = meta;
        if (_friendLevelTexts?[index] != null) _friendLevelTexts[index].text = showLevel ? Mathf.Max(1, level).ToString() : "";
        if (_friendStatusTexts?[index] != null)
        {
            _friendStatusTexts[index].text = status;
            string statusLower = string.IsNullOrEmpty(status) ? "" : status.ToLowerInvariant();
            _friendStatusTexts[index].color =
                (statusLower.Contains("off") || statusLower.Contains("离线")) ? new Color(0.62f, 0.67f, 0.72f) : ((statusLower.Contains("game") || statusLower.Contains("match") || statusLower.Contains("busy") || statusLower.Contains("游戏")) ? new Color(1f, 0.76f, 0.32f) : new Color(0.44f, 0.92f, 0.58f));
        }
        if (_friendAvatarImages?[index] != null && avatar != null)
        {
            _friendAvatarImages[index].sprite = avatar;
            _friendAvatarImages[index].color = Color.white;
            _friendAvatarImages[index].preserveAspect = true;
        }
        toolkit?.SetFriendSlot(index, name, meta, status, showLevel ? level : 0);
    }

    void BindFriendRowClicks()
    {
        BindFriendRowViews();
        for (int i = 0; i < (_friendClickButtons?.Length ?? 0); i++)
        {
            if (_friendClickButtons[i] == null) continue;
            int idx = i;
            _friendClickButtons[i].onClick.RemoveAllListeners();
            _friendClickButtons[i].onClick.AddListener(() => OnFriendRowClicked(idx));
        }
    }

    void OnFriendRowClicked(int index)
    {
        if (_friendNames == null || index < 0 || index >= _friendNames.Length) return;
        string name = _friendNames[index];
        if (string.IsNullOrEmpty(name)) return;
        string meta = _friendMetas != null ? _friendMetas[index] : "";
        string status = _friendStatuses != null ? _friendStatuses[index] : "";
        if (MatchStatusText)
            MatchStatusText.text = string.IsNullOrEmpty(meta) ? $"好友：{name}  {status}" : $"好友：{name}  {meta}  {status}";
    }

    void RenderFriendRows(List<SimpleJson> friends, bool isGuest)
    {
        friends = NormalizeFriendList(friends);
        RefreshSceneFriendView(friends, isGuest);
        BindFriendRowViews();
        if (_friendRows == null || _friendRows.Length == 0) return;

        if (friends == null || friends.Count == 0)
        {
            for (int i = 0; i < _friendRows.Length; i++)
            {
                if (isGuest && i == 0)
                {
                    SetFriendRow(i, "游客模式", "登录后查看好友列表", "不可用", 0, LobbyGenRes.Get("gen_avatar_player"));
                }
                else
                {
                    SetFriendRow(i, null, null, null, 1, null);
                }
            }
            return;
        }

        Sprite[] avatars = {
            LobbyGenRes.Get("gen_avatar_player"),
            LobbyGenRes.Get("gen_avatar_friend_a"),
            LobbyGenRes.Get("gen_avatar_friend_b")
        };

        for (int i = 0; i < _friendRows.Length; i++)
        {
            if (i < friends.Count)
            {
                var f = friends[i];
                string uname = string.IsNullOrEmpty(f.username) ? "未知玩家" : f.username;
                string meta = !string.IsNullOrEmpty(f.rank) ? f.rank : "列兵";
                string status = !string.IsNullOrEmpty(f.status) ? f.status : "离线";
                int level = f.level ?? 1;
                SetFriendRow(i, uname, meta, status, level, avatars[i % avatars.Length]);
            }
            else
            {
                SetFriendRow(i, null, null, null, 1, null);
            }
        }
    }

    void RefreshFriendViews(NetworkClient net, bool isGuest)
    {
        if (FriendListText)
            FriendListText.text = isGuest ? "游客模式暂无好友数据" : "加载中...";

        if (net == null || isGuest || string.IsNullOrEmpty(net.Token))
        {
            RenderFriendRows(GetFriendListTestData(), isGuest);
            return;
        }

        net.GetFriendsData(friends =>
        {
            friends = NormalizeFriendList(friends);
            RenderFriendRows(friends, false);
            UpdateLegacyFriendListText(friends);
        });
    }

    static List<SimpleJson> GetFriendListTestData()
    {
        var friends = new List<SimpleJson>(FriendListTestData.Length);
        foreach (var item in FriendListTestData)
        {
            friends.Add(new SimpleJson
            {
                username = item.name,
                rank = item.rank,
                status = item.status,
                level = item.level
            });
        }
        return friends;
    }

    static List<SimpleJson> NormalizeFriendList(List<SimpleJson> friends)
    {
        return friends == null || friends.Count == 0 ? GetFriendListTestData() : friends;
    }

    void UpdateLegacyFriendListText(List<SimpleJson> friends)
    {
        if (FriendListText == null) return;
        if (friends == null || friends.Count == 0)
        {
            FriendListText.text = "-- 暂无好友 --";
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var f in friends)
        {
            string status = string.IsNullOrEmpty(f.status) ? "离线" : f.status;
            string rank = string.IsNullOrEmpty(f.rank) ? "" : ("  " + f.rank);
            sb.Append("<b>").Append(f.username ?? "").Append("</b>  Lv.").Append(f.level ?? 1).Append(rank)
                .Append("\n  ").Append(status).Append("\n\n");
        }
        FriendListText.text = sb.ToString();
    }

    void CopyFriendSnapshot(out string[] names, out string[] metas, out string[] statuses, out int[] levels)
    {
        BindFriendRowViews();
        names = new string[5];
        metas = new string[5];
        statuses = new string[5];
        levels = new int[5];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = _friendNames != null && i < _friendNames.Length ? (_friendNames[i] ?? "") : "";
            metas[i] = _friendMetas != null && i < _friendMetas.Length ? (_friendMetas[i] ?? "") : "";
            statuses[i] = _friendStatuses != null && i < _friendStatuses.Length ? (_friendStatuses[i] ?? "") : "";
            levels[i] = _friendLevels != null && i < _friendLevels.Length ? Mathf.Max(1, _friendLevels[i]) : 1;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _uiToolkit = FindObjectOfType<LobbyUIToolkitController>();
        EnsureRuntimeButtonSprites();

        Transform root = GameObject.Find("LobbyCanvas")?.transform;
        if (root != null)
        {
            var legacyReturnBattle = FindDeepChild(root, "ReturnBattleButton");
            if (legacyReturnBattle != null) legacyReturnBattle.gameObject.SetActive(false);
        }
    }

    void EnsureRuntimeButtonSprites()
    {
        Vector4 border = new Vector4(16f, 16f, 16f, 16f);
        if (RuntimeButtonPrimarySprite == null)
            RuntimeButtonPrimarySprite = LobbyGenRes.Get("gen_button_gold")
                ?? GeneratedButtonRes.Get("generated_button_primary", border);
        if (RuntimeButtonSecondarySprite == null)
            RuntimeButtonSecondarySprite = LobbyGenRes.Get("gen_button_dark")
                ?? GeneratedButtonRes.Get("generated_button_secondary", border);
        if (RuntimeButtonNeutralSprite == null)
            RuntimeButtonNeutralSprite = LobbyGenRes.Get("gen_button_tech_metal")
                ?? LobbyGenRes.Get("gen_button_dark")
                ?? GeneratedButtonRes.Get("generated_button_neutral", border);
        if (RuntimeButtonDangerSprite == null)
            RuntimeButtonDangerSprite = LobbyGenRes.Get("gen_button_dark")
                ?? GeneratedButtonRes.Get("generated_button_danger", border);
    }

    LobbyUIToolkitController UiToolkit
    {
        get
        {
            if (_uiToolkit == null)
                _uiToolkit = FindObjectOfType<LobbyUIToolkitController>();
            return _uiToolkit;
        }
    }

    void OpenLobbyNotifyInfo()
    {
        var toolkit = UiToolkit;
        if (toolkit != null) { toolkit.OpenToolkitNotifyPanel(); return; }
        if (MatchStatusText) MatchStatusText.text = "通知：暂无新消息";
    }

    void OpenLobbyShopInfo()
    {
        if (ShopPanel == null)
        {
            CreateRuntimeShopPanel();
            PolishPanel(ShopPanel, new Color(0.22f, 0.18f, 0.10f, 0.97f), new Color(1f, 0.72f, 0.12f, 0.95f));
            PolishButton(ShopBackBtn);
        }
        if (ShopPanel != null)
        {
            SafeSetActive(HallPanel, false);
            SafeSetActive(MapPanel, false);
            SafeSetActive(RoomPanel, false);
            SafeSetActive(InvitePanel, false);
            SafeSetActive(CreateRoomPanel, false);
            SetBottomNavSubPanelActive(ShopPanel);
            SetMainLobbyHitButtonsActive(false);
            if (MatchStatusText) MatchStatusText.text = "商店：浏览军需补给与稀有兑换";
            return;
        }

        var toolkit = UiToolkit;
        if (toolkit != null) { toolkit.OpenToolkitShopPanel(); return; }
        if (MatchStatusText) MatchStatusText.text = "商店：补给商城正在接入";
    }

    void OpenLobbyGemStoreInfo()
    {
        if (ShopPanel == null)
        {
            CreateRuntimeShopPanel();
            PolishPanel(ShopPanel, new Color(0.22f, 0.18f, 0.10f, 0.97f), new Color(1f, 0.72f, 0.12f, 0.95f));
            PolishButton(ShopBackBtn);
        }
        if (ShopPanel != null)
        {
            SafeSetActive(HallPanel, false);
            SafeSetActive(MapPanel, false);
            SafeSetActive(RoomPanel, false);
            SafeSetActive(InvitePanel, false);
            SafeSetActive(CreateRoomPanel, false);
            SetBottomNavSubPanelActive(ShopPanel);
            SetMainLobbyHitButtonsActive(false);
            if (MatchStatusText) MatchStatusText.text = "商店：已切换到高级物资与晶石兑换";
            return;
        }

        var toolkit = UiToolkit;
        if (toolkit != null) { toolkit.OpenToolkitGemStorePanel(); return; }
        if (MatchStatusText) MatchStatusText.text = "钻石商店：高级物资兑换待开放";
    }

    void OpenLobbyRankInfo()
    {
        if (RankPanel == null)
        {
            CreateRuntimeRankPanel();
            PolishPanel(RankPanel, new Color(0.08f, 0.11f, 0.18f, 0.97f), new Color(0.78f, 0.88f, 1f, 0.95f));
            PolishButton(RankBackBtn);
        }
        if (RankPanel != null)
        {
            SafeSetActive(HallPanel, false);
            SafeSetActive(MapPanel, false);
            SafeSetActive(RoomPanel, false);
            SafeSetActive(InvitePanel, false);
            SafeSetActive(CreateRoomPanel, false);
            SetBottomNavSubPanelActive(RankPanel);
            SetMainLobbyHitButtonsActive(false);
            if (MatchStatusText) MatchStatusText.text = "排行榜：赛季功勋与军衔榜单";
            return;
        }

        var toolkit = UiToolkit;
        if (toolkit != null) { toolkit.OpenToolkitRankPanel(); return; }
        if (MatchStatusText) MatchStatusText.text = "排行榜：赛季榜单正在接入";
    }

    void OpenLobbyMailInfo()
    {
        var toolkit = UiToolkit;
        if (toolkit != null)
        {
            UiToolkitRememberInboxSyncNotification();
            toolkit.OpenToolkitMailPanel();
            return;
        }
        if (MatchStatusText) MatchStatusText.text = InboxSyncNotificationMessage;
    }

    void OpenLobbyGlobalConquestInfo()
    {
        var toolkit = UiToolkit;
        if (toolkit != null) { toolkit.OpenToolkitGlobalConquestPanel(); return; }
        OnGlobalConquest();
    }

    void OpenCampaignPanel()
    {
        if (CampaignPanel == null)
        {
            CreateRuntimeCampaignPanel();
            PolishPanel(CampaignPanel, new Color(0.20f, 0.16f, 0.11f, 0.97f), new Color(1f, 0.72f, 0.12f, 0.95f));
            PolishButton(CampaignBackBtn);
        }
        if (CampaignPanel == null)
        {
            if (MatchStatusText) MatchStatusText.text = "战役：指挥台初始化失败";
            return;
        }

        SafeSetActive(HallPanel, false);
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SafeSetActive(InvitePanel, false);
        SafeSetActive(CreateRoomPanel, false);
        SetBottomNavSubPanelActive(CampaignPanel);
        SetMainLobbyHitButtonsActive(false);
        if (MatchStatusText) MatchStatusText.text = "战役：已进入战区行动档案";
    }

    void Start()
    {
        var net = NetworkClient.Instance;
        string pname = GetCommanderDisplayName();
        bool isGuest = net != null ? net.IsGuest : (PlayerPrefs.GetInt("is_guest", 1) == 1);
        int level = GetCommanderLevel();
        string rankTitle = GetCommanderRankTitle(level);
        int wins = net != null ? net.Wins : 0;
        int losses = net != null ? net.Losses : 0;
        LoadTaskClaimedToday();

        if (EnableLegacyLobbyScaffolding)
            EnsureReferenceDynamicOverlay();

        Transform lobbyRoot = HallPanel != null ? HallPanel.transform : GameObject.Find("LobbyCanvas")?.transform;
        ApplyResponsiveHallLayout(true);
        EnsureLobbyVisualChrome(lobbyRoot);
        EnsureCompactFriendListOverlay(lobbyRoot);
        EnsureSceneFriendView();
        SetSceneFriendViewVisible(false);
        EnsureTopEconomyTexts();
        if (EnableLegacyLobbyScaffolding)
        {
            RetuneRealHallDataLayout(lobbyRoot);
            ConstrainRealHallDataTexts();
            EnsureBackImageMainButtons();
            EnsureReturnBattleButton();
            ConstrainRealHallDataTexts();
        }

        if (PlayerNameText) PlayerNameText.text = pname + (isGuest ? " [游客]" : "");
        if (PlayerLevelText) PlayerLevelText.text = level.ToString();
        if (PlayerRankText) PlayerRankText.text = rankTitle;
        ApplySceneCommanderBadgeData(pname, level, rankTitle);
        RefreshPlayerAvatar();
        RefreshTopEconomy();

        TechButton?.onClick.AddListener(OpenTech);
        SettingsIconBtn?.onClick.AddListener(ToggleSettings);
        NotifyBellBtn?.onClick.AddListener(OpenLobbyNotifyInfo);
        AddFriendIconBtn?.onClick.AddListener(() => { if (AddFriendInput) { AddFriendInput.ActivateInputField(); AddFriendInput.Select(); } });
        ViewAllFriendsBtn?.onClick.AddListener(ToggleFriendsView);
        GoldPlusBtn?.onClick.AddListener(OpenLobbyShopInfo);
        GemPlusBtn?.onClick.AddListener(OpenLobbyGemStoreInfo);
        var logoutTop = GameObject.Find("LogoutBtnTop");
        if (logoutTop != null) logoutTop.SetActive(false);

        QuickMatchButton?.onClick.AddListener(OnQuickMatch);
        GlobalConquestButton?.onClick.AddListener(OpenLobbyGlobalConquestInfo);
        CustomRoomButton?.onClick.AddListener(OnCustomRoom);

        Button dockBtn = null;
        if (FriendsDock != null)
            dockBtn = FriendsDock.transform.Find("FriendsDockBtn")?.GetComponent<Button>();
        dockBtn?.onClick.AddListener(OpenFriends);
        Button closeBtn = null;
        if (FriendsSidebar != null)
            closeBtn = FriendsSidebar.transform.Find("FriendsCloseBtn")?.GetComponent<Button>();
        closeBtn?.onClick.AddListener(CloseFriends);

        var friendRoot = FriendsSidebar != null ? FriendsSidebar : HallPanel;
        if (friendRoot != null)
        {
            AddFriendInput = FindInChildren<InputField>(friendRoot, "AddFriendInput");
            AddFriendButton = FindInChildren<Button>(friendRoot, "AddFriendButton");
            AddFriendStatus = FindInChildren<Text>(friendRoot, "AddFriendStatus");
        }
        AddFriendButton?.onClick.AddListener(OnAddFriend);
        BindFriendRowViews();
        BindFriendRowClicks();
        RefreshFriendViews(net, isGuest);

        // Friend rows are rendered by BindFriendRowViews/RefreshFriendViews above.





        TechCloseBtn?.onClick.AddListener(CloseTech);
        UpdateTechPanel(pname, wins, losses);

        SettingsCloseBtn?.onClick.AddListener(() => SettingsPanel?.SetActive(false));
        HideUselessLogoutEntry();
        if (MusicSlider) MusicSlider.value = PlayerPrefs.GetFloat("music_vol", 0.8f);
        if (SFXSlider) SFXSlider.value = PlayerPrefs.GetFloat("sfx_vol", 1f);
        AudioListener.volume = PlayerPrefs.GetFloat("music_vol", 0.8f);
        if (EnableLegacyLobbyScaffolding)
            StartLobbyMusic();
        MusicSlider?.onValueChanged.AddListener(v => { AudioListener.volume = v; PlayerPrefs.SetFloat("music_vol", v); PlayerPrefs.Save(); });
        SFXSlider?.onValueChanged.AddListener(v => { _UiClickAudio.SetVolumeScale(v); _CombatSfx.SetVolumeScale(v); PlayerPrefs.Save(); });

        for (int i = 0; i < MapButtons?.Length; i++) { int idx = i; MapButtons[i]?.onClick.AddListener(() => SelectMap(idx)); }
        StartMatchButton?.onClick.AddListener(OnStartMatch);
        BackToHallButton?.onClick.AddListener(ShowHall);
        CancelMatchButton?.onClick.AddListener(CancelMatch);

        RoomBackButton?.onClick.AddListener(ShowHall);
        CreateRoomButton?.onClick.AddListener(OpenCreateRoomPanel);
        RefreshRoomsButton?.onClick.AddListener(RefreshRooms);
        for (int i = 0; i < RoomInviteButtons?.Length; i++)
            RoomInviteButtons[i]?.onClick.AddListener(() => OpenInvitePanel(""));

        InviteCloseBtn?.onClick.AddListener(() => InvitePanel?.SetActive(false));

        string[] crMaps = BattleMapCatalog.GetPlayableMapNames();
        int crButtonCount = Mathf.Min(CRMapButtons?.Length ?? 0, crMaps.Length);
        for (int i = 0; i < crButtonCount; i++)
        { int idx = i; string m = crMaps[idx]; CRMapButtons[i]?.onClick.AddListener(() => SelectCRMap(m)); }
        CRConfirmBtn?.onClick.AddListener(DoCreateRoom);
        CRCancelBtn?.onClick.AddListener(() => CreateRoomPanel?.SetActive(false));

        for (int i = 0; i < (TaskClaimButtons?.Length ?? 0); i++)
        {
            int ti = i;
            TaskClaimButtons[i]?.onClick.AddListener(() => OnClaimTask(ti));
        }
        TechSpeedBtn?.onClick.AddListener(OnTechSpeedUp);
        TechStartBtn?.onClick.AddListener(OnTechStart);
        TechTreeBtn?.onClick.AddListener(OpenTech);
        MoreTasksBtn?.onClick.AddListener(() => { RefreshLobbyFromServer(); if (MatchStatusText) MatchStatusText.text = "任务已刷新"; });

        NavShopBtn?.onClick.AddListener(OpenLobbyShopInfo);
        NavWarehouseBtn?.onClick.AddListener(OpenWarehouse);
        WarehouseBackBtn?.onClick.AddListener(ShowHall);
        NavCampaignBtn?.onClick.AddListener(OpenCampaignPanel);
        NavRankBtn?.onClick.AddListener(OpenLobbyRankInfo);
        NavMailBtn?.onClick.AddListener(OpenLobbyMailInfo);

        if (TaskProgSliders != null)
            foreach (var s in TaskProgSliders)
                if (s) { s.interactable = false; }
        if (TechProgressSlider) TechProgressSlider.interactable = false;

        ShowHall();
        if (EnableLegacyLobbyScaffolding)
            EnsureReturnBattleButton();
        if (EnableLegacyLobbyScaffolding)
        {
            RuntimeLobbyPolish();
            ApplyLobbyDeskBackgroundSprite();
            ApplyLobbyGenUiSprites();
            ApplyLobbyFriendsLayout();
            StartCoroutine(ReapplyLobbyBackgroundEndOfFrame());
        }

        if (net != null && !string.IsNullOrEmpty(net.Token))
        {
            net.PostPresence();
            RefreshLobbyFromServer();
        }
    }

    void EnsureTopEconomyTexts()
    {
        Transform canvas = HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
        if (canvas == null) return;

        if (GoldText == null)
            GoldText = FindDeepChild(canvas, "GoldCountText")?.GetComponent<Text>()
                ?? FindDeepChild(canvas, "GoldText")?.GetComponent<Text>();

        if (GemText == null)
            GemText = FindDeepChild(canvas, "GemCountText")?.GetComponent<Text>()
                ?? FindDeepChild(canvas, "GemText")?.GetComponent<Text>();

        if (EnableLegacyLobbyScaffolding && GoldText == null)
            GoldText = FindDeepChild(canvas, "DynamicGoldText")?.GetComponent<Text>();

        if (EnableLegacyLobbyScaffolding && GemText == null)
            GemText = FindDeepChild(canvas, "DynamicGemText")?.GetComponent<Text>();

        if (GoldText != null)
        {
            if (EnableLegacyLobbyScaffolding)
            {
                ConstrainOverlayText(GoldText, 22, TextAnchor.MiddleCenter);
                GoldText.transform.SetAsLastSibling();
            }
        }

        if (GemText != null)
        {
            if (EnableLegacyLobbyScaffolding)
            {
                ConstrainOverlayText(GemText, 22, TextAnchor.MiddleCenter);
                GemText.transform.SetAsLastSibling();
            }
        }
    }

    void EnsureBackImageMainButtons()
    {
        Transform canvas = HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
        if (canvas == null) return;

        // 场景里可能存在只负责显示的按钮/图片，层级被盖住时会导致主入口点不动。
        // 这里强制在 Canvas 顶层补透明热区，并在离开大厅时关闭，保证“匹配/自定义/争霸”入口稳定可点。
        QuickMatchButton = EnsureRuntimeHitButton(canvas, "BackImageQuickMatchButton",
            new Vector2(0.500f, 0.660f), new Vector2(620f, 155f));
        CustomRoomButton = EnsureRuntimeHitButton(canvas, "BackImageCustomRoomButton",
            new Vector2(0.500f, 0.438f), new Vector2(620f, 155f));
        GlobalConquestButton = EnsureRuntimeHitButton(canvas, "BackImageGlobalConquestButton",
            new Vector2(0.500f, 0.235f), new Vector2(620f, 155f));

        QuickMatchButton.transform.SetAsLastSibling();
        CustomRoomButton.transform.SetAsLastSibling();
        GlobalConquestButton.transform.SetAsLastSibling();
    }

    void EnsureReturnBattleButton()
    {
        Transform canvas = HallPanel != null && HallPanel.transform.parent != null
            ? HallPanel.transform.parent
            : GameObject.Find("LobbyCanvas")?.transform;
        if (canvas == null) return;

        bool available = PlayerPrefs.GetInt("battle_resume_available", 0) == 1;
        var settingsRoot = SettingsPanel != null ? SettingsPanel.transform : FindDeepChild(canvas, "SettingsPanel");
        var settingsBox = settingsRoot != null ? settingsRoot.Find("SettingsPanelBox") : null;
        var buttonParent = settingsBox != null ? settingsBox : settingsRoot;
        var existingLegacy = FindDeepChild(canvas, "ReturnBattleButton")?.GetComponent<Button>();
        var existingSettings = FindDeepChild(canvas, "SettingsReturnBattleButton")?.GetComponent<Button>();

        if (!EnableLegacyLobbyScaffolding)
        {
            SettingsReturnBattleButton = existingSettings;
            if (existingLegacy != null) existingLegacy.gameObject.SetActive(false);
            if (SettingsReturnBattleButton != null)
            {
                SettingsReturnBattleButton.gameObject.SetActive(available);
                SettingsReturnBattleButton.onClick.RemoveAllListeners();
                SettingsReturnBattleButton.onClick.AddListener(ReturnToBattle);
            }
            return;
        }

        if (existingLegacy != null)
            existingLegacy.gameObject.SetActive(false);

        if (!available)
        {
            if (existingSettings != null) existingSettings.gameObject.SetActive(false);
            if (_returnBattleButton != null) _returnBattleButton.gameObject.SetActive(false);
            if (SettingsReturnBattleButton != null) SettingsReturnBattleButton.gameObject.SetActive(false);
            return;
        }

        if (buttonParent == null)
        {
            if (existingSettings != null) existingSettings.gameObject.SetActive(false);
            if (_returnBattleButton != null) _returnBattleButton.gameObject.SetActive(false);
            if (SettingsReturnBattleButton != null) SettingsReturnBattleButton.gameObject.SetActive(false);
            return;
        }

        if (SettingsReturnBattleButton != null && !SettingsReturnBattleButton.transform.IsChildOf(buttonParent))
            SettingsReturnBattleButton = null;

        if (SettingsReturnBattleButton == null)
        {
            SettingsReturnBattleButton = existingSettings;
            if (SettingsReturnBattleButton == null)
            {
                SettingsReturnBattleButton = CreateRuntimeButton(buttonParent, "SettingsReturnBattleButton", "返回战场",
                    new Vector2(0.5f, 0.18f), new Vector2(220f, 42f));
                ApplyRuntimeButtonSkin(SettingsReturnBattleButton, "button_yellow_header", Color.white, new Color(0.10f, 0.11f, 0.07f, 1f));
            }
        }

        if (SettingsReturnBattleButton == null) return;

        _returnBattleButton = SettingsReturnBattleButton;
        _returnBattleButton.onClick.RemoveAllListeners();
        _returnBattleButton.onClick.AddListener(ReturnToBattle);
        var rt = _returnBattleButton.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.18f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(220f, 42f);
        }
        _returnBattleButton.gameObject.SetActive(true);
        _returnBattleButton.transform.SetAsLastSibling();
        if (existingLegacy != null && existingLegacy.gameObject != _returnBattleButton.gameObject)
            existingLegacy.gameObject.SetActive(false);
    }

    void HideUselessLogoutEntry()
    {
        if (LogoutButton != null)
        {
            LogoutButton.onClick.RemoveAllListeners();
            LogoutButton.gameObject.SetActive(false);
        }

        Transform root = HallPanel != null ? HallPanel.transform.root : GameObject.Find("LobbyCanvas")?.transform;
        var logoutTop = FindDeepChild(root, "LogoutBtnTop");
        if (logoutTop != null) logoutTop.gameObject.SetActive(false);
    }

    void ReturnToBattle()
    {
        PlayerPrefs.SetInt("battle_resume_available", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("GameScene");
    }

    void RefreshTopEconomy()
    {
        var net = NetworkClient.Instance;
        if (net == null) return;
        if (GoldText) GoldText.text = FormatNumber(net.Gold);
        if (GemText) GemText.text = FormatNumber(net.Gems);
        if (EnableLegacyLobbyScaffolding)
            ConstrainRealHallDataTexts();
    }

    void RefreshPlayerAvatar()
    {
        if (PlayerAvatarImage == null) return;
        string avatarRes = PlayerPrefs.GetString("player_avatar_resource", "gen_avatar_player");
        var sprite = LobbyGenRes.Get(avatarRes) ?? LobbyGenRes.Get("gen_avatar_player");
        if (sprite == null) return;
        PlayerAvatarImage.sprite = sprite;
        PlayerAvatarImage.color = Color.white;
        PlayerAvatarImage.preserveAspect = true;
    }

    public void SetPlayerAvatarResource(string lobbyGenResourceName)
    {
        if (string.IsNullOrEmpty(lobbyGenResourceName)) return;
        PlayerPrefs.SetString("player_avatar_resource", lobbyGenResourceName);
        PlayerPrefs.Save();
        RefreshPlayerAvatar();
    }

    static string FormatNumber(int n)
        => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    static string TodayKey()
        => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    string TaskClaimPrefKey(string taskId)
    {
        var net = NetworkClient.Instance;
        string user = net != null && !string.IsNullOrEmpty(net.UserId)
            ? net.UserId
            : PlayerPrefs.GetString("current_user", "guest");
        return "lobby_task_claimed_" + user + "_" + TodayKey() + "_" + taskId;
    }

    void LoadTaskClaimedToday()
    {
        for (int i = 0; i < _taskClaimedToday.Length && i < _taskIds.Length; i++)
            _taskClaimedToday[i] = PlayerPrefs.GetInt(TaskClaimPrefKey(_taskIds[i]), 0) == 1;
    }

    bool IsTaskClaimedToday(int index, string taskId = null)
    {
        if (index < 0 || index >= _taskClaimedToday.Length) return false;
        if (_taskClaimedToday[index]) return true;
        string id = string.IsNullOrEmpty(taskId) && index < _taskIds.Length ? _taskIds[index] : taskId;
        if (string.IsNullOrEmpty(id)) return false;
        bool claimed = PlayerPrefs.GetInt(TaskClaimPrefKey(id), 0) == 1;
        if (claimed) _taskClaimedToday[index] = true;
        return claimed;
    }

    void MarkTaskClaimedToday(int index, string taskId = null)
    {
        if (index < 0 || index >= _taskClaimedToday.Length) return;
        string id = string.IsNullOrEmpty(taskId) && index < _taskIds.Length ? _taskIds[index] : taskId;
        if (string.IsNullOrEmpty(id)) return;
        _taskClaimedToday[index] = true;
        PlayerPrefs.SetInt(TaskClaimPrefKey(id), 1);
        PlayerPrefs.Save();
    }

    void SetTaskClaimVisual(int index, bool claimed, bool canClaim)
    {
        if (index < 0 || index >= _taskIds.Length)
            return;

        if (TaskClaimButtons != null && index < TaskClaimButtons.Length && TaskClaimButtons[index] != null)
        {
            var button = TaskClaimButtons[index];
            button.interactable = !claimed;
            var tx = EnsureTaskClaimLabel(button, index);
            if (tx != null)
            {
                tx.text = claimed ? "已领取" : (canClaim ? "领取" : "前往");
                tx.color = claimed ? new Color(0.72f, 0.76f, 0.68f, 1f) : new Color(1f, 0.86f, 0.45f, 1f);
            }
        }

        UiToolkit?.SetTaskClaimState(index, claimed, canClaim);
    }

    Text EnsureTaskClaimLabel(Button button, int index)
    {
        if (button == null) return null;
        var existing = button.GetComponentInChildren<Text>(true);
        if (existing != null) return existing;
        if (!EnableLegacyLobbyScaffolding) return null;

        var go = new GameObject("DynamicTaskClaimLabel" + index);
        go.transform.SetParent(button.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        return text;
    }

    void RefreshLobbyFromServer()
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return;
        net.GetLobby(j =>
        {
            if (!j.success) return;
            ApplyLobbyJson(j);
        });
    }

    void ApplyLobbyJson(SimpleJson j)
    {
        var net = NetworkClient.Instance;
        if (net == null) return;
        if (j.gold.HasValue && j.gems.HasValue)
            net.SetEconomy(j.gold.Value, j.gems.Value);
        if (!string.IsNullOrEmpty(j.rankTitle))
            net.SetRankTitle(j.rankTitle);
        RefreshTopEconomy();
        int level = GetCommanderLevel();
        string rankTitle = GetCommanderRankTitle(level);
        if (PlayerRankText) PlayerRankText.text = rankTitle;
        ApplySceneCommanderBadgeData(GetCommanderDisplayName(), level, rankTitle);
        var toolkit = UiToolkit;

        int taskSlotCount = Mathf.Max(TaskTitleTexts?.Length ?? 0, 3);
        if (j.tasks != null && taskSlotCount > 0)
        {
            int filledCount = Mathf.Min(taskSlotCount, j.tasks.Count);
            for (int i = 0; i < filledCount; i++)
            {
                var t = j.tasks[i];
                if (!string.IsNullOrEmpty(t.id)) _taskIds[i] = t.id;
                if (TaskTitleTexts != null && i < TaskTitleTexts.Length && TaskTitleTexts[i]) TaskTitleTexts[i].text = t.title ?? "";
                int cur = t.cur ?? 0, mx = t.max ?? 1;
                if (TaskProgTexts != null && i < TaskProgTexts.Length && TaskProgTexts[i])
                    TaskProgTexts[i].text = $"{cur}/{mx}";
                if (TaskProgSliders != null && i < TaskProgSliders.Length && TaskProgSliders[i])
                    TaskProgSliders[i].value = mx > 0 ? Mathf.Clamp01((float)cur / mx) : 0f;
                bool claimed = t.claimedBool || IsTaskClaimedToday(i, _taskIds[i]);
                if (t.claimedBool) MarkTaskClaimedToday(i, _taskIds[i]);
                bool can = t.canClaimBool && !claimed;
                _taskCanClaimNow[i] = can;
                if (TaskClaimButtons != null && i < TaskClaimButtons.Length && TaskClaimButtons[i])
                    SetTaskClaimVisual(i, claimed, can);
                toolkit?.SetTaskSlot(i, t.title ?? "", cur, mx, claimed, can);
            }

            for (int i = filledCount; i < taskSlotCount; i++)
            {
                if (TaskTitleTexts != null && i < TaskTitleTexts.Length && TaskTitleTexts[i]) TaskTitleTexts[i].text = "";
                if (TaskProgTexts != null && i < TaskProgTexts.Length && TaskProgTexts[i]) TaskProgTexts[i].text = "";
                if (TaskProgSliders != null && i < TaskProgSliders.Length && TaskProgSliders[i]) TaskProgSliders[i].value = 0f;
                if (i < _taskCanClaimNow.Length) _taskCanClaimNow[i] = false;
                if (TaskClaimButtons != null && i < TaskClaimButtons.Length && TaskClaimButtons[i])
                    SetTaskClaimVisual(i, false, false);
                toolkit?.SetTaskSlot(i, "", 0, 1, false, false);
            }
        }
        else
        {
            for (int i = 0; i < taskSlotCount; i++)
            {
                if (TaskTitleTexts != null && i < TaskTitleTexts.Length && TaskTitleTexts[i]) TaskTitleTexts[i].text = "";
                if (TaskProgTexts != null && i < TaskProgTexts.Length && TaskProgTexts[i]) TaskProgTexts[i].text = "";
                if (TaskProgSliders != null && i < TaskProgSliders.Length && TaskProgSliders[i]) TaskProgSliders[i].value = 0f;
                if (i < _taskCanClaimNow.Length) _taskCanClaimNow[i] = false;
                if (TaskClaimButtons != null && i < TaskClaimButtons.Length && TaskClaimButtons[i])
                    SetTaskClaimVisual(i, false, false);
                toolkit?.SetTaskSlot(i, "", 0, 1, false, false);
            }
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool hasTech = j.techEndAtMs.HasValue && j.techEndAtMs.Value > nowMs
            && !string.IsNullOrEmpty(j.techName) && j.techName != "null";
        _hasActiveTech = hasTech;
        if (hasTech)
        {
            _techEndMs = j.techEndAtMs.Value;
            _techTotalSec = j.techTotalSec ?? 1;
            if (TechResearchNameText) TechResearchNameText.text = j.techName ?? "";
            if (TechResearchDescText) TechResearchDescText.text = j.techDesc ?? "";
            if (TechStartBtn) TechStartBtn.gameObject.SetActive(false);
            if (TechSpeedBtn) TechSpeedBtn.gameObject.SetActive(true);
        }
        else
        {
            _techEndMs = 0;
            if (TechResearchNameText) TechResearchNameText.text = "暂无进行中的研究";
            if (TechResearchDescText) TechResearchDescText.text = "点击下方开始研究，耗时由服务器计时";
            if (TechTimerBarText) TechTimerBarText.text = "--";
            if (TechProgressSlider) TechProgressSlider.value = 0f;
            if (TechStartBtn) TechStartBtn.gameObject.SetActive(true);
            if (TechSpeedBtn) TechSpeedBtn.gameObject.SetActive(false);
        }
        if (EnableLegacyLobbyScaffolding)
        {
            RetuneRealHallDataLayout(HallPanel != null ? HallPanel.transform : GameObject.Find("LobbyCanvas")?.transform);
            ConstrainRealHallDataTexts();
        }
        SyncTechStateToUiToolkit();
    }

    void OnClaimTask(int idx)
    {
        var net = NetworkClient.Instance;
        if (idx < 0 || idx >= _taskIds.Length)
        {
            if (MatchStatusText) MatchStatusText.text = "任务数据异常";
            return;
        }
        if (IsTaskClaimedToday(idx, _taskIds[idx]))
        {
            SetTaskClaimVisual(idx, true, false);
            if (MatchStatusText) MatchStatusText.text = "该任务今日已领取";
            return;
        }
        if (idx < _taskCanClaimNow.Length && !_taskCanClaimNow[idx])
        {
            if (MatchStatusText) MatchStatusText.text = "任务条件未达成，暂时不能领取";
            return;
        }
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            if (MatchStatusText) MatchStatusText.text = "请先登录后领取任务奖励";
            return;
        }

        net.ClaimTask(_taskIds[idx], j =>
        {
            if (j.success)
            {
                MarkTaskClaimedToday(idx, _taskIds[idx]);
                SetTaskClaimVisual(idx, true, false);
                if (j.gold.HasValue && j.gems.HasValue) net.SetEconomy(j.gold.Value, j.gems.Value);
                RefreshTopEconomy();
                if (MatchStatusText) MatchStatusText.text = "任务奖励已领取";
                RefreshLobbyFromServer();
            }
            else
            {
                string err = j.error ?? "领取失败";
                if (err.Contains("已领取") || err.ToLowerInvariant().Contains("claimed"))
                {
                    MarkTaskClaimedToday(idx, _taskIds[idx]);
                    SetTaskClaimVisual(idx, true, false);
                }
                if (MatchStatusText) MatchStatusText.text = err;
            }
        });
    }

    void OnTechStart()
    {
        TryStartTechResearch();
    }

    bool TryStartTechResearch()
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return false;
        net.StartTechResearch(j =>
        {
            if (j.success) RefreshLobbyFromServer();
            else if (MatchStatusText) MatchStatusText.text = j.error ?? "无法开始研究";
        });
        return true;
    }

    void OnTechSpeedUp()
    {
        TrySpeedUpTech();
    }

    bool TrySpeedUpTech()
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return false;
        net.SpeedUpTech(j =>
        {
            if (j.success)
            {
                if (j.gold.HasValue && j.gems.HasValue) net.SetEconomy(j.gold.Value, j.gems.Value);
                RefreshTopEconomy();
                RefreshLobbyFromServer();
            }
            else if (MatchStatusText) MatchStatusText.text = j.error ?? "加速失败";
        });
        return true;
    }

    public void UiToolkitClaimTask(int index)
    {
        OnClaimTask(index);
    }

    public void UiToolkitRefreshTasks()
    {
        RefreshLobbyFromServer();
        if (MatchStatusText) MatchStatusText.text = "任务已刷新";
    }

    public bool UiToolkitTechStart()
    {
        return TryStartTechResearch();
    }

    public bool UiToolkitTechSpeedUp()
    {
        return TrySpeedUpTech();
    }

    public void UiToolkitOpenFriends()
    {
        OpenFriends();
        if (MatchStatusText) MatchStatusText.text = "好友列表已打开";
    }

    public void UiToolkitRefreshFriends(Action<string, string[], string[], string[], int[]> callback)
    {
        BindFriendRowViews();
        var net = NetworkClient.Instance;
        if (net == null || net.IsGuest || string.IsNullOrEmpty(net.Token))
        {
            RenderFriendRows(new List<SimpleJson>(), true);
            CopyFriendSnapshot(out var guestNames, out var guestMetas, out var guestStatuses, out var guestLevels);
            string status = "游客模式暂无好友数据";
            if (MatchStatusText) MatchStatusText.text = status;
            callback?.Invoke(status, guestNames, guestMetas, guestStatuses, guestLevels);
            return;
        }

        if (FriendListText) FriendListText.text = "加载中...";
        net.GetFriendsData(friends =>
        {
            RenderFriendRows(friends, false);
            UpdateLegacyFriendListText(friends);
            CopyFriendSnapshot(out var names, out var metas, out var statuses, out var levels);
            string status = friends == null || friends.Count == 0
                ? "暂无好友，可以添加一位"
                : $"已加载 {Mathf.Min(friends.Count, names.Length)} 位好友";
            if (MatchStatusText) MatchStatusText.text = status;
            callback?.Invoke(status, names, metas, statuses, levels);
        });
    }

    public void UiToolkitAddFriend(string friendName, Action<bool, string, string[], string[], string[], int[]> callback)
    {
        string name = friendName == null ? "" : friendName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            CopyFriendSnapshot(out var emptyNames, out var emptyMetas, out var emptyStatuses, out var emptyLevels);
            callback?.Invoke(false, "请输入好友昵称", emptyNames, emptyMetas, emptyStatuses, emptyLevels);
            return;
        }

        var net = NetworkClient.Instance;
        if (net == null || net.IsGuest || string.IsNullOrEmpty(net.Token))
        {
            CopyFriendSnapshot(out var guestNames, out var guestMetas, out var guestStatuses, out var guestLevels);
            callback?.Invoke(false, "游客模式不支持添加好友", guestNames, guestMetas, guestStatuses, guestLevels);
            return;
        }

        SetFriendStatus("添加中...", true);
        net.AddFriend(name, (ok, msg) =>
        {
            string message = ok ? $"已添加 {name}" : (string.IsNullOrEmpty(msg) ? "添加失败" : msg);
            SetFriendStatus(message, ok);
            if (!ok)
            {
                CopyFriendSnapshot(out var failNames, out var failMetas, out var failStatuses, out var failLevels);
                callback?.Invoke(false, message, failNames, failMetas, failStatuses, failLevels);
                return;
            }

            _loadedFriends = new string[0];
            UiToolkitRefreshFriends((_, names, metas, statuses, levels) =>
            {
                callback?.Invoke(true, message, names, metas, statuses, levels);
            });
        });
    }

    public bool UiToolkitHasActiveTech()
    {
        return _hasActiveTech;
    }

    public string UiToolkitBuildTechSummary()
    {
        string title = TechResearchNameText != null && !string.IsNullOrWhiteSpace(TechResearchNameText.text)
            ? TechResearchNameText.text
            : (_hasActiveTech ? "科技研究中" : "暂无进行中的研究");
        string desc = TechResearchDescText != null && !string.IsNullOrWhiteSpace(TechResearchDescText.text)
            ? TechResearchDescText.text
            : (_hasActiveTech ? "研究正在进行，指挥部可消耗资源立即完成。" : "点击下方开始研究，耗时由服务器计时。");
        string timer = TechTimerBarText != null && !string.IsNullOrWhiteSpace(TechTimerBarText.text)
            ? TechTimerBarText.text
            : (_hasActiveTech ? FormatRemainMs(Math.Max(0, _techEndMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) : "--");
        return _hasActiveTech
            ? $"{title}\n{desc}\n剩余时间：{timer}"
            : $"{title}\n{desc}";
    }

    // ── Update（匹配计时 + 邀请轮询）────────────────────────────
    void Update()
    {
        ApplyResponsiveHallLayout();

        var net2 = NetworkClient.Instance;
        if (net2 != null && !string.IsNullOrEmpty(net2.Token))
        {
            invitePollTimer += Time.deltaTime;
            if (invitePollTimer >= 30f)
            {
                invitePollTimer = 0f;
                net2.GetInvites(invites => {
                    if (invites != null && invites.Length > 0)
                        ShowInviteNotify(invites[0].from, invites[0].id, invites[0].roomId, invites[0].mapName);
                });
            }
        }

        if (net2 != null && !string.IsNullOrEmpty(net2.Token) && !bMatching)
        {
            _presenceTimer += Time.deltaTime;
            if (_presenceTimer >= 18f)
            {
                _presenceTimer = 0f;
                net2.PostPresence();
            }
            _lobbyPollTimer += Time.deltaTime;
            if (_lobbyPollTimer >= 45f)
            {
                _lobbyPollTimer = 0f;
                RefreshLobbyFromServer();
            }
        }

        if (_hasActiveTech && TechTimerBarText != null)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long left = Math.Max(0, _techEndMs - now);
            int ts = Mathf.Max(1, _techTotalSec);
            TechTimerBarText.text = FormatRemainMs(left);
            if (TechProgressSlider)
                TechProgressSlider.value = 1f - Mathf.Clamp01((float)left / (ts * 1000f));
            SyncTechStateToUiToolkit();
            if (left <= 0)
            {
                _hasActiveTech = false;
                RefreshLobbyFromServer();
            }
        }

        if (!bMatching) return;
        matchTimer += Time.deltaTime;
        int sec = (int)matchTimer;
        if (MatchingCountText) MatchingCountText.text = $"{sec}s";
        if (MatchTimerText) MatchTimerText.text = $"匹配中... {sec}s";
        if (ShouldEnterBattleWithoutServer(NetworkClient.Instance))
            return;

        pollTimer += Time.deltaTime;
        if (pollTimer >= 10f)
        {
            pollTimer = 0f;
            var net = NetworkClient.Instance;
            if (net != null && !string.IsNullOrEmpty(net.Token))
                net.JoinMatch(selectedMap, (matched, rid) => { if (matched) EnterGame(rid, selectedMap); });
        }
    }

    static string FormatRemainMs(long msLeft)
    {
        msLeft /= 1000;
        int h = (int)(msLeft / 3600), m = (int)((msLeft % 3600) / 60), s = (int)(msLeft % 60);
        return $"{h:00}:{m:00}:{s:00}";
    }

    void ShowInviteNotify(string from, string inviteId, string roomId, string mapName)
    {
        _pendingInviteId     = inviteId;
        _pendingInviteRoomId = roomId;
        _pendingInviteFrom   = from ?? "";
        _pendingInviteMapName = mapName ?? "";
        if (RoomStatusText) RoomStatusText.text = $"{from} 邀请你加入房间（{mapName}）";
        // 房间面板可见时，下一次刷新会展示最新列表。
    }
    // 鈹€鈹€ 闈㈡澘鎺у埗 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void ShowHall()
    {
        SafeSetActive(HallPanel, true);
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SafeSetActive(InvitePanel, false);
        SafeSetActive(CreateRoomPanel, false);
        SafeSetActive(MatchingOverlay, false);
        SetBottomNavSubPanelActive(null);
        SetMainLobbyHitButtonsActive(true);
    }

    void SetMainLobbyHitButtonsActive(bool active)
    {
        SetTopLevelHitActive(QuickMatchButton, active);
        SetTopLevelHitActive(CustomRoomButton, active);
        SetTopLevelHitActive(GlobalConquestButton, active);
    }

    static void SetTopLevelHitActive(Button button, bool active)
    {
        if (button != null && button.name.StartsWith("BackImage", StringComparison.Ordinal))
            button.gameObject.SetActive(active);
    }

    void OpenWarehouse()
    {
        if (WarehousePanel == null)
        {
            CreateRuntimeWarehousePanel();
            PolishPanel(WarehousePanel, new Color(0.06f, 0.09f, 0.18f, 0.97f), new Color(1f, 0.72f, 0.12f, 0.95f));
            PolishButton(WarehouseBackBtn);
        }
        if (WarehousePanel == null) return;
        SafeSetActive(HallPanel, false);
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SafeSetActive(InvitePanel, false);
        SafeSetActive(CreateRoomPanel, false);
        SetBottomNavSubPanelActive(WarehousePanel);
        SetMainLobbyHitButtonsActive(false);
        if (MatchStatusText) MatchStatusText.text = "仓库：查看物资、装备与补给箱";
    }

    void OpenFriends()
    {
        if (EnsureSceneFriendView())
        {
            SetSceneFriendViewVisible(true);
            var net = NetworkClient.Instance;
            bool isGuest = net != null ? net.IsGuest : (PlayerPrefs.GetInt("is_guest", 1) == 1);
            RefreshFriendViews(net, isGuest);
            if (MatchStatusText) MatchStatusText.text = "好友列表已打开";
            return;
        }

        var toolkit = UiToolkit;
        if (toolkit != null)
        {
            toolkit.OpenFriendsPopup();
            if (MatchStatusText) MatchStatusText.text = "好友列表已打开";
            return;
        }

        if (FriendsDock != null) FriendsDock.SetActive(false);
        if (FriendsSidebar != null) FriendsSidebar.SetActive(true);
    }

    void CloseFriends()
    {
        if (EnsureSceneFriendView())
            SetSceneFriendViewVisible(false);
        if (FriendsSidebar != null) FriendsSidebar.SetActive(false);
        if (FriendsDock != null) FriendsDock.SetActive(true);
    }

    void OpenTech()
    {
        SafeSetActive(TechPanel, true);
    }

    void CloseTech()
    {
        SafeSetActive(TechPanel, false);
    }

    void ToggleSettings()
    {
        if (SettingsPanel == null) return;
        EnsureReturnBattleButton();
        SettingsPanel.SetActive(!SettingsPanel.activeSelf);
    }

    void SyncTechStateToUiToolkit()
    {
        var toolkit = UiToolkit;
        if (toolkit == null) return;

        string title = TechResearchNameText != null ? TechResearchNameText.text : "";
        string desc = TechResearchDescText != null ? TechResearchDescText.text : "";
        string timerText = TechTimerBarText != null ? TechTimerBarText.text : (_hasActiveTech ? FormatRemainMs(Math.Max(0, _techEndMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())) : "--");
        float progress = TechProgressSlider != null
            ? TechProgressSlider.value
            : (_hasActiveTech && _techTotalSec > 0
                ? 1f - Mathf.Clamp01((float)Math.Max(0, _techEndMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / (_techTotalSec * 1000f))
                : 0f);
        toolkit.SetTechState(title, desc, timerText, progress);
    }

    // 鈹€鈹€ 绉戞妧闈㈡澘鏁版嵁濉厖 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void UpdateTechPanel(string name, int wins, int losses)
    {
        if (TechPlayerName) TechPlayerName.text = name;
        if (TechWinsText)   TechWinsText.text   = wins.ToString();
        if (TechLossText)   TechLossText.text   = losses.ToString();
        int total = wins + losses;
        float rate = total > 0 ? wins * 100f / total : 0f;
        if (TechRateText) TechRateText.text = $"{rate:F0}%";
    }

    // 鈹€鈹€ 鍖归厤鎸夐挳 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void OnQuickMatch()
    {
        selectedMap = BattleMapCatalog.DefaultMapName;
        if (MapPanel == null)
        {
            CreateRuntimeMapPanel();
            PolishPanel(MapPanel, new Color(0.05f, 0.08f, 0.16f, 0.96f), new Color(0.20f, 0.55f, 1f, 0.95f));
            PolishButtonArray(MapButtons);
            PolishButton(StartMatchButton);
            PolishButton(BackToHallButton);
        }

        if (MapPanel == null)
        {
            // 极端情况下连运行时面板都创建失败，就不要显示英文错误，直接进入默认匹配流程。
            if (MatchStatusText) MatchStatusText.text = "地图面板初始化失败，已使用默认地图匹配";
            StartMatchWithMap(selectedMap);
            return;
        }

        SafeSetActive(HallPanel, false);
        SafeSetActive(RoomPanel, false);
        SetBottomNavSubPanelActive(null);
        SafeSetActive(MapPanel, true);
        SetMainLobbyHitButtonsActive(false);
        if (MatchStatusText) MatchStatusText.text = "请选择地图后开始匹配";
    }

    void OnGlobalConquest()
    {
        selectedMap = BattleMapCatalog.GlobalConquestName;
        StartMatchWithMap(selectedMap);
    }

    void OnCustomRoom()
    {
        if (RoomPanel == null)
        {
            if (MatchStatusText) MatchStatusText.text = "房间面板未配置";
            return;
        }

        SafeSetActive(HallPanel, false);
        SafeSetActive(MapPanel, false);
        SetBottomNavSubPanelActive(null);
        SafeSetActive(RoomPanel, true);
        SetMainLobbyHitButtonsActive(false);
        RefreshRooms();
    }

    public void UiToolkitQuickMatch()
    {
        selectedMap = BattleMapCatalog.DefaultMapName;
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SafeSetActive(InvitePanel, false);
        SafeSetActive(CreateRoomPanel, false);
        SafeSetActive(MatchingOverlay, false);
        SetBottomNavSubPanelActive(null);
        if (MatchStatusText) MatchStatusText.text = "请选择匹配地图";
    }

    public void UiToolkitStartMatch(string mapName)
    {
        string map = string.IsNullOrEmpty(mapName) ? BattleMapCatalog.DefaultMapName : mapName;
        selectedMap = map;
        bMatching = true;
        matchTimer = 0f;
        pollTimer = 0f;

        SafeSetActive(HallPanel, true);
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SafeSetActive(InvitePanel, false);
        SafeSetActive(CreateRoomPanel, false);
        SafeSetActive(MatchingOverlay, false);
        SetBottomNavSubPanelActive(null);

        if (MatchStatusText) MatchStatusText.text = $"匹配中：{map}";
        var net = NetworkClient.Instance;
        if (ShouldEnterBattleWithoutServer(net))
        {
            EnterBattleAfterLocalMatch(map);
            return;
        }

        net.JoinMatch(map, (matched, rid) => { if (matched) EnterGame(rid, map); });
    }

    public void UiToolkitCancelMatch()
    {
        bMatching = false;
        matchTimer = 0f;
        pollTimer = 0f;
        if (_waitRoomCoroutine != null) { StopCoroutine(_waitRoomCoroutine); _waitRoomCoroutine = null; }
        SafeSetActive(MatchingOverlay, false);
        NetworkClient.Instance?.CancelMatch();
        ShowHall();
        if (MatchStatusText) MatchStatusText.text = "匹配已取消";
    }

    public bool UiToolkitIsMatching()
    {
        return bMatching;
    }

    public int UiToolkitMatchElapsedSeconds()
    {
        return Mathf.Max(0, (int)matchTimer);
    }

    public void UiToolkitCustomRoom()
    {
        OnCustomRoom();
    }

    public void UiToolkitRefreshRooms(Action<string, List<SimpleJson>> callback)
    {
        if (RoomStatusText) RoomStatusText.text = "加载中...";
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            string status = "请先登录后再使用房间功能";
            if (RoomStatusText) RoomStatusText.text = status;
            callback?.Invoke(status, new List<SimpleJson>());
            return;
        }

        net.GetRooms(rooms =>
        {
            string status = rooms == null || rooms.Count == 0
                ? "暂无房间，可以创建一个"
                : $"找到 {rooms.Count} 个房间";
            if (RoomStatusText) RoomStatusText.text = status;
            callback?.Invoke(status, rooms ?? new List<SimpleJson>());
        });
    }

    public void UiToolkitCreateRoom(string mapName, string roomName, Action<bool, string, string> callback)
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            string status = "请先登录后再创建房间";
            if (RoomStatusText) RoomStatusText.text = status;
            callback?.Invoke(false, status, mapName);
            return;
        }

        string selected = string.IsNullOrEmpty(mapName) ? BattleMapCatalog.DefaultMapName : mapName;
        string displayName = string.IsNullOrEmpty(roomName) ? "" : roomName.Trim();
        if (RoomStatusText) RoomStatusText.text = "创建中...";
        if (CreateRoomButton) CreateRoomButton.interactable = false;
        net.CreateRoom(selected, (ok, roomId) =>
        {
            if (CreateRoomButton) CreateRoomButton.interactable = true;
            if (ok)
            {
                if (RoomStatusText) RoomStatusText.text = $"房间已创建（{selected}），等待对手加入...";
                RefreshRooms();
                if (_waitRoomCoroutine != null) StopCoroutine(_waitRoomCoroutine);
                selectedMap = selected;
                _waitRoomCoroutine = StartCoroutine(WaitAndEnterGame(roomId, selected));
                callback?.Invoke(true, "房间已创建，等待对手加入...", selected);
            }
            else
            {
                string status = "创建失败：" + roomId;
                if (RoomStatusText) RoomStatusText.text = status;
                callback?.Invoke(false, status, selected);
            }
        }, displayName);
    }

    public void UiToolkitJoinRoom(string roomId)
    {
        OnJoinRoom(roomId);
    }

    public void UiToolkitOpenInvite(string roomId)
    {
        _inviteRoomId = roomId;
        if (RoomStatusText) RoomStatusText.text = "请选择要邀请的好友";
    }

    public void UiToolkitLoadInviteFriends(Action<string, string[]> callback)
    {
        var net = NetworkClient.Instance;
        if (net == null || net.IsGuest || string.IsNullOrEmpty(net.Token))
        {
            string status = "游客模式暂无好友数据";
            if (RoomStatusText) RoomStatusText.text = status;
            callback?.Invoke(status, new string[0]);
            return;
        }

        if (_loadedFriends != null && _loadedFriends.Length > 0)
        {
            callback?.Invoke("请选择要邀请的好友", _loadedFriends);
            return;
        }

        net.GetFriendNames(names =>
        {
            _loadedFriends = names ?? new string[0];
            string status = _loadedFriends.Length > 0 ? "请选择要邀请的好友" : "暂无可邀请好友";
            callback?.Invoke(status, _loadedFriends);
        });
    }

    public void UiToolkitSendInvite(string friendName, string roomId, Action<bool, string> callback)
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        {
            callback?.Invoke(false, "请先登录后再邀请好友");
            return;
        }
        if (string.IsNullOrEmpty(roomId))
        {
            callback?.Invoke(false, "请先选择有效房间");
            return;
        }
        if (string.IsNullOrEmpty(friendName) || friendName == "--")
        {
            callback?.Invoke(false, "请选择有效好友");
            return;
        }

        if (RoomStatusText) RoomStatusText.text = $"正在邀请 {friendName}...";
        net.InviteFriend(friendName, roomId, (ok, msg) =>
        {
            string status = ok ? $"已邀请 {friendName}" : $"邀请失败：{msg}";
            if (RoomStatusText) RoomStatusText.text = status;
            callback?.Invoke(ok, status);
        });
    }

    public void UiToolkitGlobalConquest()
    {
        OnGlobalConquest();
    }

    public void UiToolkitOpenWarehouse()
    {
        OpenWarehouse();
    }

    public void UiToolkitLoadWarehouse(Action<string, string[], string[], int[]> callback)
    {
        string[] names = { "装甲补给箱", "能源核心", "战车零件", "指挥芯片", "合金钢材", "加速模块" };
        string[] descs =
        {
            "用于前线装甲单位的快速整备。",
            "驱动基地设施与高级科技研究。",
            "维护坦克、火炮与载具生产线。",
            "提升指挥链路与战术调度效率。",
            "建造防御工事与升级建筑外壳。",
            "可用于缩短部分研究与训练时间。"
        };
        int[] counts = { 12, 19, 26, 33, 40, 47 };
        if (MatchStatusText) MatchStatusText.text = "仓库：物资清单已同步";
        callback?.Invoke("战备库存已加载", names, descs, counts);
    }

    public void UiToolkitLoadLeaderboard(Action<string, string, List<SimpleJson>, SimpleJson, string> callback)
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token) || net.IsGuest)
        {
            var fallback = BuildLocalLeaderboard(net);
            var current = FindCurrentLeaderboardEntry(fallback, net != null ? net.UserId : null);
            if (MatchStatusText) MatchStatusText.text = "排行榜：本地赛季榜单已加载";
            callback?.Invoke("S3 东线军演赛季", "每周一 05:00 结算军功与战绩", fallback, current, "本地榜单");
            return;
        }

        net.GetLeaderboard(json =>
        {
            if (json != null && json.success)
            {
                var rows = json.leaderboard ?? new List<SimpleJson>();
                SimpleJson currentRow = json.current ?? FindCurrentLeaderboardEntry(rows, net.UserId);
                if (MatchStatusText) MatchStatusText.text = "排行榜：赛季功勋榜已同步";
                callback?.Invoke(
                    string.IsNullOrEmpty(json.season) ? "S3 东线军演赛季" : json.season,
                    string.IsNullOrEmpty(json.resetText) ? "每周一 05:00 结算军功与战绩" : json.resetText,
                    rows,
                    currentRow,
                    rows.Count > 0 ? $"已加载 {rows.Count} 名指挥官" : "暂无排行数据");
                return;
            }

            var fallback = BuildLocalLeaderboard(net);
            var fallbackCurrent = FindCurrentLeaderboardEntry(fallback, net != null ? net.UserId : null);
            if (MatchStatusText) MatchStatusText.text = "排行榜：服务器未连接，显示本地榜单";
            callback?.Invoke("S3 东线军演赛季", "服务器未连接时显示本地演示榜", fallback, fallbackCurrent, "本地榜单");
        });
    }

    static SimpleJson FindCurrentLeaderboardEntry(List<SimpleJson> rows, string userId)
    {
        if (rows == null || string.IsNullOrEmpty(userId)) return null;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row != null && row.userId == userId)
                return row;
            if (row != null && row.id == userId)
                return row;
            if (row != null && row.isCurrentBool)
                return row;
        }
        return null;
    }

    static List<SimpleJson> BuildLocalLeaderboard(NetworkClient net)
    {
        string currentName = net != null && !string.IsNullOrWhiteSpace(net.UserName) ? net.UserName : "Commander";
        int currentLevel = net != null ? Mathf.Max(1, net.Level) : 1;
        int currentWins = net != null ? Mathf.Max(0, net.Wins) : 0;
        int currentLosses = net != null ? Mathf.Max(0, net.Losses) : 0;
        string currentRank = net != null && !string.IsNullOrWhiteSpace(net.RankTitle) ? net.RankTitle : "列兵";

        string currentUserId = net != null && !string.IsNullOrWhiteSpace(net.UserId) ? net.UserId : "local-current";
        var currentRow = NewLeaderboardRow(1, currentName, currentLevel, currentWins, currentLosses, currentWins * 30 + currentLevel * 8 - currentLosses * 6, currentRank, true);
        currentRow.id = currentUserId;
        currentRow.userId = currentUserId;

        var rows = new List<SimpleJson>
        {
            currentRow,
            NewLeaderboardRow(2, "IronWolf", 8, 12, 4, 402, "上尉", false),
            NewLeaderboardRow(3, "SeaHammer", 6, 9, 3, 318, "中尉", false),
            NewLeaderboardRow(4, "SkyLancer", 5, 7, 5, 245, "少尉", false),
            NewLeaderboardRow(5, "DesertFox", 4, 5, 4, 188, "军士", false)
        };
        rows.Sort((a, b) =>
        {
            int scoreCompare = (b.score ?? 0).CompareTo(a.score ?? 0);
            if (scoreCompare != 0) return scoreCompare;
            return (b.wins ?? 0).CompareTo(a.wins ?? 0);
        });
        for (int i = 0; i < rows.Count; i++)
            rows[i].place = i + 1;
        return rows;
    }

    static SimpleJson NewLeaderboardRow(int place, string username, int level, int wins, int losses, int score, string rankTitle, bool current)
    {
        return new SimpleJson
        {
            place = place,
            username = username,
            level = level,
            wins = wins,
            losses = losses,
            score = score,
            rankTitle = rankTitle,
            status = current ? "我方指挥官" : "赛季活跃",
            isCurrentBool = current
        };
    }

    public void UiToolkitOpenTech()
    {
        OpenTech();
    }

    public void UiToolkitToggleSettings()
    {
        ToggleSettings();
    }

    public void UiToolkitRememberInboxSyncNotification()
    {
        _latestInboxNotification = InboxSyncNotificationMessage;
    }

    public string UiToolkitBuildNotificationSummary()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(_pendingInviteRoomId))
        {
            string from = string.IsNullOrEmpty(_pendingInviteFrom) ? "队友" : _pendingInviteFrom;
            string mapName = string.IsNullOrEmpty(_pendingInviteMapName) ? "未知地图" : _pendingInviteMapName;
            sb.Append(from).Append(" 邀请你加入房间（").Append(mapName).Append("）");
            sb.Append("\n可点击下方按钮前往房间列表查看。");
        }

        if (!string.IsNullOrWhiteSpace(_latestInboxNotification))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(_latestInboxNotification);
        }

        string hallStatus = MatchStatusText != null ? MatchStatusText.text : "";
        if (!string.IsNullOrWhiteSpace(hallStatus) && !string.Equals(hallStatus, _latestInboxNotification, StringComparison.Ordinal))
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("大厅状态：").Append(hallStatus);
        }

        if (sb.Length == 0)
            sb.Append("暂无新消息，指挥部一切正常。");
        return sb.ToString();
    }

    public List<SimpleJson> UiToolkitBuildNotifications()
    {
        var rows = new List<SimpleJson>();
        if (!string.IsNullOrEmpty(_pendingInviteRoomId))
        {
            string from = string.IsNullOrEmpty(_pendingInviteFrom) ? "队友" : _pendingInviteFrom;
            string mapName = string.IsNullOrEmpty(_pendingInviteMapName) ? "未知地图" : _pendingInviteMapName;
            rows.Add(new SimpleJson
            {
                title = "房间邀请",
                body = from + " 邀请你加入房间（" + mapName + "）",
                status = "待处理",
                action = "查看房间"
            });
        }

        if (!string.IsNullOrWhiteSpace(_latestInboxNotification))
        {
            rows.Add(new SimpleJson
            {
                title = "邮件同步",
                body = _latestInboxNotification,
                status = "已同步",
                action = "知道了"
            });
        }

        string hallStatus = MatchStatusText != null ? MatchStatusText.text : "";
        if (!string.IsNullOrWhiteSpace(hallStatus) && !string.Equals(hallStatus, _latestInboxNotification, StringComparison.Ordinal))
        {
            rows.Add(new SimpleJson
            {
                title = "大厅状态",
                body = hallStatus,
                status = "实时",
                action = "查看"
            });
        }

        if (rows.Count == 0)
        {
            rows.Add(new SimpleJson
            {
                title = "指挥部",
                body = "暂无新消息，指挥部一切正常。",
                status = "正常",
                action = "知道了"
            });
        }

        return rows;
    }

    public bool UiToolkitHasPendingInvite()
    {
        return !string.IsNullOrEmpty(_pendingInviteRoomId);
    }

    public void UiToolkitOpenPendingInviteRoomList()
    {
        OnCustomRoom();
    }

    public void UiToolkitLogout()
    {
        HideUselessLogoutEntry();
        if (SettingsPanel != null) SettingsPanel.SetActive(false);
    }

    void SelectMap(int idx)
    {
        string[] maps = BattleMapCatalog.GetPlayableMapNames();
        selectedMap = idx >= 0 && idx < maps.Length ? maps[idx] : BattleMapCatalog.DefaultMapName;
        for (int i = 0; i < MapButtons?.Length; i++)
        {
            var img = MapButtons[i]?.GetComponent<Image>();
            bool selected = i == idx;
            if (img)
            {
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = img.sprite != null
                    ? (selected ? Color.white : new Color(0.78f, 0.92f, 1f, 0.95f))
                    : new Color(0f, 0f, 0f, 0.004f);
            }

            var label = MapButtons[i]?.GetComponentInChildren<Text>(true);
            if (label)
                label.color = selected ? new Color(1f, 0.96f, 0.76f, 1f) : new Color(0.94f, 0.98f, 1f, 1f);
        }
        if (StartMatchButton)
        {
            var t = StartMatchButton.GetComponentInChildren<Text>();
            // 地图名只通过卡片选中态表达，主操作按钮保持短文本，避免小屏幕重叠。
            if (t) t.text = "开始匹配";
        }
    }

    void OnStartMatch() => StartMatchWithMap(selectedMap);

    static bool ShouldEnterBattleWithoutServer(NetworkClient net)
    {
        // 游客/本地测试登录会使用 test-token-local；这种 token 不会在服务器产生真实匹配房间。
        // MuMu 打包测试经常没有后端服务，游客模式也直接进战场，保证地图和镜头流程可验证。
        return net == null
            || string.IsNullOrEmpty(net.Token)
            || net.IsGuest
            || net.Token == "test-token-local";
    }

    void EnterBattleAfterLocalMatch(string map)
    {
        if (MatchStatusText) MatchStatusText.text = "匹配成功，进入战场";
        if (MatchingMapText) MatchingMapText.text = "地图：" + map;
        if (MatchingCountText) MatchingCountText.text = "0s";
        EnterGame("", map);
    }

    void StartMatchWithMap(string map)
    {
        selectedMap  = map;
        bMatching    = true;
        matchTimer   = 0f;
        SafeSetActive(HallPanel, false);
        SafeSetActive(MapPanel, false);
        SafeSetActive(RoomPanel, false);
        SetBottomNavSubPanelActive(null);
        SafeSetActive(MatchingOverlay, true);
        if (MatchingMapText)   MatchingMapText.text   = "地图：" + map;
        if (MatchingCountText) MatchingCountText.text = "0s";

        var net = NetworkClient.Instance;
        if (ShouldEnterBattleWithoutServer(net))
        {
            EnterBattleAfterLocalMatch(map);
            return;
        }

        net.JoinMatch(map, (matched, rid) => { if (matched) EnterGame(rid, map); });
    }

    void CancelMatch()
    {
        bMatching  = false;
        matchTimer = 0f;
        pollTimer  = 0f;
        if (_waitRoomCoroutine != null) { StopCoroutine(_waitRoomCoroutine); _waitRoomCoroutine = null; }
        SafeSetActive(MatchingOverlay, false);
        NetworkClient.Instance?.CancelMatch();
        if (selectedMap == BattleMapCatalog.GlobalConquestName || MapPanel == null) ShowHall();
        else
        {
            SafeSetActive(HallPanel, false);
            SetBottomNavSubPanelActive(null);
            SafeSetActive(MapPanel, true);
        }
    }

    // 鈹€鈹€ 濂藉弸鎿嶄綔 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void OnAddFriend()
    {
        string name = AddFriendInput?.text?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        var net = NetworkClient.Instance;
        if (net == null || net.IsGuest) { SetFriendStatus("游客模式不支持添加好友", false); return; }
        SetFriendStatus("添加中...", true);
        net.AddFriend(name, (ok, msg) => {
            SetFriendStatus(ok ? $"已添加 {name}" : msg, ok);
            if (ok)
            {
                _loadedFriends = new string[0];
                RefreshFriendViews(net, false);
            }
        });
    }

    void SetFriendStatus(string msg, bool ok)
    {
        if (AddFriendStatus == null) return;
        AddFriendStatus.text  = msg;
        AddFriendStatus.color = ok ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.4f);
    }

    // 鈹€鈹€ 鑷畾涔夋埧闂?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void RefreshRooms()
    {
        if (RoomStatusText) RoomStatusText.text = "加载中...";
        for (int i = 0; i < RoomRowTexts?.Length; i++)
        {
            if (RoomRowTexts[i])    RoomRowTexts[i].text = "--";
            if (RoomJoinButtons?[i])   RoomJoinButtons[i].interactable   = false;
            if (RoomInviteButtons?[i]) RoomInviteButtons[i].interactable = false;
        }
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        { if (RoomStatusText) RoomStatusText.text = "请先登录后再使用房间功能"; return; }
        net.GetRooms(rooms => {
            if (RoomStatusText) RoomStatusText.text = rooms.Count == 0 ? "暂无房间，可以创建一个" : $"找到 {rooms.Count} 个房间";
            for (int i = 0; i < (RoomRowTexts?.Length ?? 0); i++)
            {
                if (i < rooms.Count)
                {
                    var r = rooms[i];
                    string rId = r.id ?? r.roomId ?? "";
                    int pc = r.playerCount ?? 0, mc = r.maxPlayers ?? 2;
                    if (RoomRowTexts[i]) RoomRowTexts[i].text = $"{r.name ?? "房间"}  /  {r.mapName ?? "-"}  ({pc}/{mc})";
                    if (RoomJoinButtons?[i])
                    {
                        RoomJoinButtons[i].interactable = pc < mc;
                        string cid = rId;
                        RoomJoinButtons[i].onClick.RemoveAllListeners();
                        RoomJoinButtons[i].onClick.AddListener(() => OnJoinRoom(cid));
                    }
                    if (RoomInviteButtons?[i])
                    {
                        RoomInviteButtons[i].interactable = true;
                        string cid = rId;
                        RoomInviteButtons[i].onClick.RemoveAllListeners();
                        RoomInviteButtons[i].onClick.AddListener(() => OpenInvitePanel(cid));
                    }
                }
                else
                {
                    if (RoomRowTexts?[i])       RoomRowTexts[i].text      = "--";
                    if (RoomJoinButtons?[i])    RoomJoinButtons[i].interactable   = false;
                    if (RoomInviteButtons?[i])  RoomInviteButtons[i].interactable = false;
                }
            }
        });
    }

    // ── 邀请好友 ─────────────────────────────────────────────
    void OpenInvitePanel(string roomId)
    {
        _inviteRoomId = roomId;
        if (InvitePanel == null) return;
        InvitePanel.SetActive(true);

        // 娓呯┖鍒楄〃
        for (int i = 0; i < InviteFriendNames?.Length; i++)
        {
            if (InviteFriendNames[i]) InviteFriendNames[i].text = "--";
            if (InviteFriendButtons?[i]) InviteFriendButtons[i].interactable = false;
        }

        var net = NetworkClient.Instance;
        bool isGuest = net == null || net.IsGuest;
        if (isGuest)
        {
            if (InviteFriendNames?.Length > 0 && InviteFriendNames[0])
                InviteFriendNames[0].text = "游客模式暂无好友数据";
            return;
        }

        if (_loadedFriends.Length > 0) { FillInviteList(_loadedFriends); return; }
        net.GetFriendNames(names => {
            _loadedFriends = names;
            FillInviteList(names);
        });
    }

    void FillInviteList(string[] friends)
    {
        for (int i = 0; i < (InviteFriendNames?.Length ?? 0); i++)
        {
            if (i < friends.Length)
            {
                string fname = friends[i].Trim();
                if (InviteFriendNames[i]) InviteFriendNames[i].text = fname;
                if (InviteFriendButtons?[i])
                {
                    InviteFriendButtons[i].interactable = !string.IsNullOrEmpty(fname) && fname != "--";
                    string fn = fname; string rid = _inviteRoomId;
                    InviteFriendButtons[i].onClick.RemoveAllListeners();
                    InviteFriendButtons[i].onClick.AddListener(() => SendInvite(fn, rid));
                }
            }
            else
            {
                if (InviteFriendNames?[i])    InviteFriendNames[i].text        = "--";
                if (InviteFriendButtons?[i])  InviteFriendButtons[i].interactable = false;
            }
        }
    }

    void SendInvite(string friendName, string roomId)
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return;
        if (string.IsNullOrEmpty(friendName) || friendName == "--") return;
        if (RoomStatusText) RoomStatusText.text = $"正在邀请 {friendName}...";
        InvitePanel?.SetActive(false);
        net.InviteFriend(friendName, roomId, (ok, msg) => {
            if (RoomStatusText) RoomStatusText.text = ok
                ? $"已邀请 {friendName}"
                : $"邀请失败：{msg}";
        });
    }

    void OpenCreateRoomPanel()
    {
        _crSelectedMap = BattleMapCatalog.DefaultMapName;
        if (CRSelectedMapText) CRSelectedMapText.text = "已选：" + BattleMapCatalog.DefaultMapName;
        if (CRNameInput)       CRNameInput.text = "";
        // 楂樹寒榛樿鎸夐挳
        HighlightCRMapBtn(0);
        CreateRoomPanel?.SetActive(true);
    }

    void SelectCRMap(string map)
    {
        _crSelectedMap = map;
        if (CRSelectedMapText) CRSelectedMapText.text = "已选：" + map;
        string[] crMaps = BattleMapCatalog.GetPlayableMapNames();
        HighlightCRMapBtn(System.Array.IndexOf(crMaps, map));
    }

    void HighlightCRMapBtn(int selIdx)
    {
        for (int i = 0; i < CRMapButtons?.Length; i++)
        {
            if (CRMapButtons[i] == null) continue;
            var img = CRMapButtons[i].GetComponent<UnityEngine.UI.Image>();
            if (img) img.color = (i == selIdx)
                ? new Color(0.15f, 0.55f, 0.95f)
                : new Color(0.15f, 0.25f, 0.45f);
        }
    }

    void DoCreateRoom()
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token))
        { if (RoomStatusText) RoomStatusText.text = "请先登录后再创建房间"; return; }
        CreateRoomPanel?.SetActive(false);
        if (RoomStatusText) RoomStatusText.text = "创建中...";
        if (CreateRoomButton) CreateRoomButton.interactable = false;
        string mapName  = string.IsNullOrEmpty(_crSelectedMap) ? BattleMapCatalog.DefaultMapName : _crSelectedMap;
        string roomName = CRNameInput != null ? CRNameInput.text.Trim() : "";
        net.CreateRoom(mapName, (ok, roomId) => {
            if (CreateRoomButton) CreateRoomButton.interactable = true;
            if (ok)
            {
                if (RoomStatusText) RoomStatusText.text = $"房间已创建（{mapName}），等待对手加入...";
                RefreshRooms();
                if (_waitRoomCoroutine != null) StopCoroutine(_waitRoomCoroutine);
                selectedMap = mapName;
                _waitRoomCoroutine = StartCoroutine(WaitAndEnterGame(roomId, mapName));
            }
            else { if (RoomStatusText) RoomStatusText.text = "创建失败：" + roomId; }
        }, roomName);
    }

    void OnJoinRoom(string roomId)
    {
        var net = NetworkClient.Instance;
        if (net == null || string.IsNullOrEmpty(net.Token)) return;
        if (RoomStatusText) RoomStatusText.text = "加入中...";
        net.JoinRoom(roomId, (ok, mapName) => { if (ok) EnterGame(roomId, mapName); else if (RoomStatusText) RoomStatusText.text = "加入失败：" + mapName; });
    }

    IEnumerator WaitAndEnterGame(string roomId, string mapOverride = "")
    {
        yield return new UnityEngine.WaitForSeconds(30f);
        NetworkClient.Instance?.LeaveRoom(roomId);
        if (RoomStatusText) RoomStatusText.text = "等待对手超时，已离开房间";
        RefreshRooms();
    }

    // 鈹€鈹€ 閫氱敤 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    void OnLogout()
    {
        NetworkClient.Instance?.ClearSession();
        PlayerPrefs.DeleteKey("current_user");
        PlayerPrefs.DeleteKey("is_guest");
        PlayerPrefs.SetInt("battle_resume_available", 0);
        SceneManager.LoadScene("LoginScene");
    }

    void EnterGame(string roomId = "", string mapOverride = "")
    {
        bMatching = false;
        string mapName = string.IsNullOrEmpty(mapOverride) ? selectedMap : mapOverride;
        if (string.IsNullOrEmpty(mapName)) mapName = BattleMapCatalog.DefaultMapName;
        selectedMap = mapName;
        PlayerPrefs.SetInt("battle_resume_available", 0);
        PlayerPrefs.SetString("current_map", mapName);
        PlayerPrefs.Save();

        // 有真实 roomId 且已登录时，走联机模式。
        var net = NetworkClient.Instance;
        bool canNet = !string.IsNullOrEmpty(roomId)
                   && net != null && !string.IsNullOrEmpty(net.Token)
                   && net.Token != "test-token-local";
        if (canNet)
        {
            if (GameNetworkSync.Instance == null)
                new UnityEngine.GameObject("GameNetworkSync").AddComponent<GameNetworkSync>();
            GameNetworkSync.Instance.StartNetworkGame(roomId);
        }

        SceneManager.LoadScene("GameScene");
    }

    void RuntimeLobbyPolish()
    {
        Color panelColor = new Color(0.05f, 0.08f, 0.16f, 0.96f);
        // HallPanel 是全屏垫层：必须保持较低 alpha，否则会把 LobbyCanvas 下的 Background 底图完全盖住。
        Color hallPanelTint = new Color(0.05f, 0.08f, 0.16f, 0.14f);
        Color modalColor = new Color(0.06f, 0.09f, 0.18f, 0.97f);
        Color accentBlue = new Color(0.20f, 0.55f, 1f, 0.95f);
        Color accentGold = new Color(1f, 0.72f, 0.12f, 0.95f);

        PolishPanel(HallPanel, hallPanelTint, accentGold);
        PolishPanel(MapPanel, panelColor, accentBlue);
        PolishPanel(RoomPanel, panelColor, accentBlue);
        PolishPanel(WarehousePanel, modalColor, accentGold);
        PolishPanel(InvitePanel, modalColor, accentGold);
        PolishPanel(CreateRoomPanel, modalColor, accentGold);
        PolishPanel(TechPanel, modalColor, accentBlue);
        PolishPanel(SettingsPanel, modalColor, accentBlue);
        if (FriendsSidebar != null)
            PolishPanel(FriendsSidebar, modalColor, accentGold);

        foreach (var text in new[] { PlayerNameText, PlayerLevelText, PlayerRankText, GoldText, GemText,
            MatchStatusText, MatchTimerText, RoomStatusText, CRSelectedMapText, TechResearchNameText, TechResearchDescText, TechTimerBarText })
        {
            if (text == null) continue;
            if (text.color.a <= 0.01f) continue;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.Lerp(text.color, Color.white, 0.25f);
        }

        PolishButton(NotifyBellBtn);
        PolishButton(AddFriendIconBtn);
        PolishButton(ViewAllFriendsBtn);
        PolishButton(GoldPlusBtn);
        PolishButton(GemPlusBtn);
        PolishButton(TechButton);
        PolishButton(SettingsIconBtn);
        PolishButton(QuickMatchButton);
        PolishButton(GlobalConquestButton);
        PolishButton(CustomRoomButton);
        PolishButton(StartMatchButton);
        PolishButton(BackToHallButton);
        PolishButton(CancelMatchButton);
        PolishButton(CreateRoomButton);
        PolishButton(RefreshRoomsButton);
        PolishButton(RoomBackButton);
        PolishButton(InviteCloseBtn);
        PolishButton(CRConfirmBtn);
        PolishButton(CRCancelBtn);
        PolishButton(SettingsCloseBtn);
        PolishButton(SettingsReturnBattleButton);
        PolishButton(LogoutButton);
        PolishButton(TechCloseBtn);
        PolishButton(TechSpeedBtn);
        PolishButton(TechStartBtn);
        PolishButton(TechTreeBtn);
        PolishButton(MoreTasksBtn);
        PolishButton(NavShopBtn);
        PolishButton(NavWarehouseBtn);
        PolishButton(NavCampaignBtn);
        PolishButton(NavRankBtn);
        PolishButton(NavMailBtn);
        PolishButton(WarehouseBackBtn);
        PolishButtonArray(MapButtons);
        PolishButtonArray(CRMapButtons);
        PolishButtonArray(RoomJoinButtons);
        PolishButtonArray(RoomInviteButtons);
        PolishButtonArray(InviteFriendButtons);
        PolishButtonArray(TaskClaimButtons);
    }

    void ApplyLobbyDeskBackgroundSprite()
    {
        Image lobbyBg = null;
        if (HallPanel != null && HallPanel.transform.parent != null)
            lobbyBg = HallPanel.transform.parent.Find("Background")?.GetComponent<Image>();
        if (lobbyBg == null)
            lobbyBg = GameObject.Find("LobbyCanvas")?.transform.Find("Background")?.GetComponent<Image>();
        if (lobbyBg == null) return;
        bool usesCustomOverride = false;
        Sprite deskSpr = LoadLobbyBackgroundSprite(out usesCustomOverride);
        if (deskSpr == null) return;
        if (_runtimeLobbyDeskSprite != null && deskSpr != _runtimeLobbyDeskSprite)
        {
            Destroy(_runtimeLobbyDeskSprite);
            _runtimeLobbyDeskSprite = null;
        }
        lobbyBg.sprite = deskSpr;
        lobbyBg.color = Color.white;
        ConfigureLobbyBackgroundLayout(lobbyBg, deskSpr, usesCustomOverride);
    }

    Sprite LoadLobbyBackgroundSprite(out bool usesCustomOverride)
    {
        usesCustomOverride = false;

        for (int i = 0; i < LobbyBackgroundResourceCandidates.Length; i++)
        {
            string path = LobbyBackgroundResourceCandidates[i];
            bool isCustomPath = i < LobbyBackgroundResourceCandidates.Length - 1;

            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                foreach (var o in Resources.LoadAll(path))
                {
                    if (o is Sprite sp)
                    {
                        sprite = sp;
                        break;
                    }
                }
            }

            if (sprite != null)
            {
                usesCustomOverride = isCustomPath;
                return sprite;
            }

            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
            {
                if (_runtimeLobbyDeskSprite != null)
                    Destroy(_runtimeLobbyDeskSprite);
                _runtimeLobbyDeskSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                usesCustomOverride = isCustomPath;
                return _runtimeLobbyDeskSprite;
            }
        }

        return null;
    }

    static void ConfigureLobbyBackgroundLayout(Image lobbyBg, Sprite sprite, bool usesCustomOverride)
    {
        if (lobbyBg == null) return;

        var rect = lobbyBg.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var fitter = lobbyBg.GetComponent<AspectRatioFitter>();
        if (!usesCustomOverride || sprite == null || sprite.rect.height <= 0.01f)
        {
            if (fitter != null)
                Destroy(fitter);
            lobbyBg.preserveAspect = false;
            return;
        }

        if (fitter == null)
            fitter = lobbyBg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        lobbyBg.preserveAspect = true;
    }

    IEnumerator ReapplyLobbyBackgroundEndOfFrame()
    {
        yield return null;
        ApplyLobbyDeskBackgroundSprite();
        ApplyLobbyGenUiSprites();
    }

    void OnDestroy()
    {
        if (_lobbyMusicSource != null)
        {
            _lobbyMusicSource.Stop();
            _lobbyMusicSource = null;
        }
        if (_lobbyMusicClip != null)
        {
            Destroy(_lobbyMusicClip);
            _lobbyMusicClip = null;
        }
        if (_runtimeLobbyDeskSprite != null)
        {
            Destroy(_runtimeLobbyDeskSprite);
            _runtimeLobbyDeskSprite = null;
        }
    }

    void PolishPanel(GameObject panel, Color color, Color accent)
    {
        if (panel == null) return;

        var image = panel.GetComponent<Image>();
        if (IsExactLobbySprite(image))
            return;
        if (image != null) image.color = color;

        if (panel.transform.Find("_RuntimeAccent") != null) return;

        var line = new GameObject("_RuntimeAccent");
        line.transform.SetParent(panel.transform, false);
        line.transform.SetAsFirstSibling();
        var rt = line.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, -3f);
        rt.offsetMax = Vector2.zero;
        line.AddComponent<Image>().color = accent;
    }

    void PolishButtonArray(Button[] buttons)
    {
        if (buttons == null) return;
        foreach (var button in buttons) PolishButton(button);
    }

    static bool ShouldKeepButtonTransparent(Button button, Text label)
    {
        if (button == null) return true;
        string name = button.name ?? "";
        if (name.StartsWith("BackImage", StringComparison.Ordinal))
            return true;
        return label == null || string.IsNullOrEmpty(label.text) || label.text.Trim().Length == 0;
    }

    static string ChoosePolishButtonSkinName(Button button)
    {
        string name = button != null && button.name != null ? button.name : "";
        if (name.IndexOf("Logout", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Surrender", StringComparison.OrdinalIgnoreCase) >= 0)
            return "button_red_header";

        if (name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Claim", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Create", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Speed", StringComparison.OrdinalIgnoreCase) >= 0)
            return "button_yellow_header";

        return "button_blue_header";
    }

    static bool IsButtonBackgroundSprite(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrEmpty(sprite.name)) return false;
        return sprite.name.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void PolishButton(Button button)
    {
        if (button == null) return;

        var image = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<Text>(true);
        if (image == null)
        {
            image = button.gameObject.AddComponent<Image>();
            button.targetGraphic = image;
        }

        Color baseColor = image != null ? image.color : new Color(0.16f, 0.32f, 0.58f, 1f);
        bool transparentImage = image != null && image.color.a <= 0.02f;
        if (transparentImage && ShouldKeepButtonTransparent(button, label))
        {
            var transparent = button.colors;
            transparent.normalColor = new Color(1f, 1f, 1f, 0f);
            transparent.highlightedColor = new Color(1f, 1f, 1f, 0.04f);
            transparent.pressedColor = new Color(1f, 1f, 1f, 0.08f);
            transparent.selectedColor = new Color(1f, 1f, 1f, 0f);
            transparent.disabledColor = new Color(1f, 1f, 1f, 0f);
            transparent.colorMultiplier = 1f;
            transparent.fadeDuration = 0.08f;
            button.colors = transparent;
            return;
        }

        string skinName = ChoosePolishButtonSkinName(button);
        Sprite skinSprite = null;
        bool canReplaceSprite = image != null
            && !IsExactLobbySprite(image)
            && (image.sprite == null || transparentImage || IsButtonBackgroundSprite(image.sprite));
        if (canReplaceSprite)
        {
            skinSprite = ResolveRuntimeButtonSkinSprite(skinName);
            if (skinSprite != null)
            {
                image.sprite = skinSprite;
                image.type = HasSpriteBorder(skinSprite) ? Image.Type.Sliced : Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
                baseColor = Color.white;
            }
        }

        if (image != null && transparentImage && skinSprite == null)
        {
            baseColor = new Color(0.14f, 0.33f, 0.52f, 0.95f);
            image.color = baseColor;
        }

        if (image != null && image.color.a < 0.9f)
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f);

        var colors = button.colors;
        Color transitionBase = image != null && image.sprite != null ? Color.white : baseColor;
        colors.normalColor = transitionBase;
        colors.highlightedColor = Color.Lerp(transitionBase, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(transitionBase, Color.black, 0.28f);
        colors.selectedColor = Color.Lerp(transitionBase, Color.white, 0.10f);
        colors.disabledColor = new Color(transitionBase.r * 0.55f, transitionBase.g * 0.55f, transitionBase.b * 0.55f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (label != null)
        {
            label.fontStyle = FontStyle.Bold;
            label.color = skinName == "button_yellow_header"
                ? new Color(0.10f, 0.11f, 0.07f, 1f)
                : button == NavWarehouseBtn
                ? new Color(1f, 0.84f, 0.46f, 1f)
                : new Color(0.94f, 0.97f, 1f, 1f);
        }
    }

    static bool IsExactLobbySprite(Image image)
    {
        return image != null
            && image.sprite != null
            && image.sprite.name.StartsWith("gen_exact_", StringComparison.Ordinal);
    }

    static void SafeSetActive(GameObject go, bool active)
    {
        if (go != null)
            go.SetActive(active);
    }

    static T FindInChildren<T>(GameObject root, string name) where T : Component
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) { var c = t.GetComponent<T>(); if (c) return c; }
        return null;
    }
}
