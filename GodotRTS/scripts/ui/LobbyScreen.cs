using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LobbyScreen : Control
{
    const double FriendsCacheTtlSec = 43200.0;
    const double FriendsRefreshIntervalSec = 12.0;
    const double FriendsRequestTimeoutSec = 1.35;
    const double InviteRefreshIntervalSec = 10.0;
    const double SecondaryModalCacheTtlSec = 45.0;
    const double SecondaryModalRequestTimeoutSec = 1.8;
    const string LeaderboardModalKind = "leaderboard";
    const string MailModalKind = "mail";
    const string AnnouncementStorePath = "user://lobby_announcements.json";
    const string FriendsCacheStorePath = "user://lobby_friends_cache.json";
    const string LoginScenePath = "res://scenes/login/LoginScene.tscn";

    public struct SystemMail
    {
        public string Id;
        public string Time;
        public string Title;
        public string Summary;
        public string Content;
        public string Type;
        public bool HasReward;
        public int RewardGold;
        public int RewardGems;
        public bool IsRead;
        public bool IsClaimed;
    }

    private List<SystemMail> systemMails = new()
    {
        new SystemMail
        {
            Id = "mail_daily_supply",
            Time = "07-10 08:30",
            Title = "每日补给已刷新",
            Summary = "完成每日任务可领取金币和经验。",
            Content = "今日的前线后勤物资补给已经抵达！指挥官今日登录大厅可立即获得后勤配给：300 金币与 20 钻石。请点击下方领取奖励按钮将物资收入军火库！",
            Type = "补给",
            HasReward = true,
            RewardGold = 300,
            RewardGems = 20,
            IsRead = false,
            IsClaimed = false
        },
        new SystemMail
        {
            Id = "mail_recent_report",
            Time = "07-09 19:42",
            Title = "最近战报",
            Summary = "战斗报告可在结算界面查看详细统计。",
            Content = "在您最近的一场人机演练中，您的防守部队成功击退了敌方机甲集群的突袭，斩获了 15 次击杀，战役共持续 14 分钟。完整的战斗录像和兵种伤害报告已在战后结算中进行了归档，可随时查阅。",
            Type = "战报",
            HasReward = false,
            IsRead = true,
            IsClaimed = false
        }
    };

    private Dictionary<string, int> techLevels = new()
    {
        { "speed", 1 },
        { "armor", 2 },
        { "firepower", 1 },
        { "repair", 3 }
    };

    private string activeRoomHostId = "";
    private List<string> activeRoomPlayers = new();

    private Panel currentResearchDetailPanel = null!;
    private string selectedResearchTechKey = "speed";

    sealed class ModeCardUi
    {
        public Label TitleLabel { get; init; } = null!;
        public Label DescLabel { get; init; } = null!;
        public Label ActionLabel { get; init; } = null!;
        public Panel ActionDock { get; init; } = null!;
    }

    sealed class AnnouncementEntry
    {
        public string Id { get; init; } = "";
        public string DateKey { get; init; } = "";
        public string Title { get; init; } = "";
        public string Body { get; init; } = "";
        public string Kind { get; init; } = "";
    }

    sealed class ShopOfferUi
    {
        public string Name { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string Description { get; init; } = "";
        public string PriceText { get; init; } = "";
        public string ActionText { get; init; } = "";
        public string Category { get; init; } = "";
        public string Badge { get; init; } = "";
        public string IconPath { get; init; } = "";
        public string BackdropPath { get; init; } = "";
        public bool UsesGems { get; init; }
        public Color Accent { get; init; } = new(0.86f, 0.66f, 0.24f, 1f);
        public ButtonTone ActionTone { get; init; } = ButtonTone.Gold;
        public string[] Highlights { get; init; } = Array.Empty<string>();
    }

    const string BattleScenePath = "res://scenes/battle/BattlePrototype.tscn";
    const string UiRoot = "res://assets/ui/lobby_exact/";
    const string UnityLobbyRoot = "res://assets/unity_migrated/Assets/Resources/LobbyGen/";
    const string IconRoot = "res://assets/unity_migrated/Assets/Resources/icons/lobby_clean/";
    const string FinalIconRoot = "res://assets/unity_migrated/Assets/Resources/icons_final/";
    const string CurrencyIconRoot = "res://assets/unity_migrated/Assets/Resources/UI/CurrencyIcons/";
    const string TechBackgroundRoot = "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/";
    const string KenneyGameIconRoot = "res://assets/third_party/kenney/game-icons/PNG/White/2x/";
    const string KenneyGameIconExpansionRoot = "res://assets/third_party/kenney/game-icons-expansion/PNG/White/2x/";
    const string QuickMatchMode = "快速匹配";
    const string QuickMatchCardTitle = "匹配战斗";
    const string CustomRoomMode = "自定义房间";
    const string GlobalConquestMode = "全球争霸";
    const int MaxCustomRoomPlayers = 6;
    static readonly int[] CustomRoomPlayerCounts = { 2, 4, 5, 6 };

    static readonly Color PanelText = new(0.96f, 0.94f, 0.82f);
    static readonly Color MutedText = new(0.72f, 0.76f, 0.78f);
    static readonly Color GoodText = new(0.54f, 0.95f, 0.58f);
    static readonly Color WarningText = new(1f, 0.74f, 0.32f);
    static readonly Color BadText = new(1f, 0.42f, 0.36f);
    static readonly Color ToastText = new(1f, 0.95f, 0.72f, 0.98f);
    static readonly Color ToastOutline = new(0.12f, 0.08f, 0.02f, 0.92f);
    static readonly MetalUiStyle.MetalPalette GlassPanel = new(
        new Color(0.020f, 0.032f, 0.034f, 0.76f),
        new Color(0.84f, 0.67f, 0.28f, 0.78f),
        new Color(0.98f, 0.90f, 0.68f, 0.54f),
        new Color(0.02f, 0.03f, 0.03f, 0.76f),
        new Color(0.95f, 0.74f, 0.24f, 0.14f));
    static readonly MetalUiStyle.MetalPalette WarehousePanel = new(
        new Color(0.060f, 0.078f, 0.096f, 0.95f),
        new Color(0.84f, 0.72f, 0.36f, 0.84f),
        new Color(0.98f, 0.94f, 0.78f, 0.48f),
        new Color(0.015f, 0.020f, 0.026f, 0.94f),
        new Color(0.94f, 0.76f, 0.28f, 0.10f));

    Control matchSelection = null!;
    Control customSelection = null!;
    Control globalSelection = null!;
    Label playerLabel = null!;
    Label rankLabel = null!;
    Label goldLabel = null!;
    Label gemsLabel = null!;
    TextureRect commanderAvatarIcon = null!;
    TextureRect commanderRankIcon = null!;
    Button commanderPortraitButton = null!;

    Label friendStatusLabel = null!;
    VBoxContainer friendRows = null!;
    LineEdit addFriendInput = null!;
    Label roomStatusLabel = null!;
    VBoxContainer roomRows = null!;
    OptionButton roomMapPicker = null!;
    OptionButton roomPlayerCountPicker = null!;
    LineEdit roomNameInput = null!;
    Label taskStatusLabel = null!;
    VBoxContainer taskRows = null!;
    Label techTitleLabel = null!;
    Label techDescLabel = null!;
    Label techTimerLabel = null!;
    ProgressBar techProgress = null!;
    Button techStartButton = null!;
    Button techSpeedButton = null!;
    Panel modalShade = null!;
    Panel modalPanel = null!;
    Label modalTitle = null!;
    ScrollContainer modalScroll = null!;
    VBoxContainer modalBody = null!;
    Control? activeRoomActionsPanel;
    Panel? currentWarehouseDetailPanel;

    static readonly StyleBoxFlat WarehouseGridCellNormal = new()
    {
        BgColor = new Color(0.10f, 0.12f, 0.14f, 0.94f),
        BorderColor = new Color(0.38f, 0.40f, 0.44f, 0.60f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4
    };
    static readonly StyleBoxFlat WarehouseGridCellHover = new()
    {
        BgColor = new Color(0.15f, 0.18f, 0.22f, 0.96f),
        BorderColor = new Color(0.68f, 0.72f, 0.76f, 0.88f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4
    };
    static readonly StyleBoxFlat WarehouseGridCellPressed = new()
    {
        BgColor = new Color(0.06f, 0.08f, 0.10f, 0.96f),
        BorderColor = new Color(0.95f, 0.76f, 0.26f, 0.90f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4
    };
    Panel toastPanel = null!;
    Label toastLabel = null!;
    readonly Dictionary<string, ModeCardUi> modeCards = new();

    readonly List<string> loadedFriendNames = new();
    readonly HashSet<string> surfacedInviteIds = new();
    string selectedMode = QuickMatchMode;
    string selectedMap = BattleMapCatalog.DefaultMapName;
    string activeRoomId = "";
    string activeRoomMap = "";
    int activeRoomMaxPlayers = 2;
    int activeRoomPlayerCount = 1;
    long techEndAtMs;
    int techTotalSec;
    ulong toastRevision;
    double friendsPollTimer;
    double invitePollTimer;
    double presenceTimer;
    double lobbyPollTimer;
    bool quickMatchStarting;
    bool battleStarting;
    string activeFeatureModal = "";
    long friendsCacheAtMs;
    long leaderboardCacheAtMs;
    long invitesCacheAtMs;
    Godot.Collections.Dictionary cachedFriendsData = new();
    Godot.Collections.Dictionary cachedLeaderboardData = new();
    Godot.Collections.Dictionary cachedInvitesData = new();
    System.Threading.Tasks.Task? friendsRefreshTask;
    System.Threading.Tasks.Task? leaderboardRefreshTask;
    System.Threading.Tasks.Task? invitesRefreshTask;
    readonly List<AnnouncementEntry> announcementArchive = new();
    readonly HashSet<string> clearedAnnouncementIds = new();

    public override void _Ready()
    {
        if (GetNodeOrNull("Services/GameState") is null)
            AddRuntimeServices();

        selectedMap = GameState.Instance?.SelectedMapName ?? BattleMapCatalog.DefaultMapName;
        selectedMode = GameState.Instance?.SelectedMode ?? QuickMatchMode;
        LoadFriendsCache();
        LoadAnnouncementState();
        SyncAnnouncementFeed();

        BuildUi();
        PrepareFriendsPanelWarmState();
        ApplySelection(selectedMode);
        RefreshTopBar();
        _ = RefreshLobbyData();
        _ = RefreshFriends();
        _ = RefreshRooms();
        if (GameState.Instance?.IsGuest != true)
        {
            _ = NetClient.Instance?.PostPresence() ?? Task.CompletedTask;
        }
        _ = WarmSecondaryModalCaches();
    }

    public override void _Process(double delta)
    {
        UpdateTechTimer();

        if (string.IsNullOrEmpty(GameState.Instance?.Token) || GameState.Instance?.IsGuest == true)
            return;

        presenceTimer += delta;
        if (presenceTimer >= 18.0)
        {
            presenceTimer = 0.0;
            _ = NetClient.Instance?.PostPresence() ?? Task.CompletedTask;
        }

        friendsPollTimer += delta;
        if (friendsPollTimer >= FriendsRefreshIntervalSec)
        {
            friendsPollTimer = 0.0;
            _ = RefreshFriends(true);
        }

        invitePollTimer += delta;
        if (invitePollTimer >= InviteRefreshIntervalSec)
        {
            invitePollTimer = 0.0;
            _ = PollInvites();
        }

        lobbyPollTimer += delta;
        if (lobbyPollTimer >= 45.0)
        {
            lobbyPollTimer = 0.0;
            _ = RefreshLobbyData(false);
        }
    }

    void AddRuntimeServices()
    {
        var services = new Node { Name = "Services" };
        AddChild(services);
        services.AddChild(new GameState { Name = "GameState" });
        services.AddChild(new NetClient { Name = "NetClient" });
    }

    void BuildUi()
    {
        AddFullScreenTexture(UnityLobbyRoot + "user_lobby_background.png", "LobbyBackground");
        AddBackgroundWash();

        BuildTopBar();
        BuildFriendsPanel();
        BuildModeCards();
        BuildRightPanels();
        BuildBottomNav();
        BuildRoomModal();
        BuildToast();
    }

    void BuildTopBar()
    {
        var panel = new Panel
        {
            Name = "TopBar",
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(panel, new Rect2(0.030f, 0.020f, 0.940f, 0.095f));
        ApplyTopBarSurface(
            panel,
            new Color(0.035f, 0.042f, 0.046f, 0.82f),
            new Color(0.86f, 0.69f, 0.26f, 0.78f),
            6,
            new Color(0.98f, 0.76f, 0.20f, 0.10f));
        AddChild(panel);

        var contentMargin = new MarginContainer
        {
            Name = "TopBarContentMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        contentMargin.AddThemeConstantOverride("margin_left", 14);
        contentMargin.AddThemeConstantOverride("margin_top", 6);
        contentMargin.AddThemeConstantOverride("margin_right", 14);
        contentMargin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(contentMargin);

        var rowHost = new CenterContainer
        {
            Name = "TopBarRowHost",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        rowHost.SetAnchorsPreset(LayoutPreset.FullRect);
        contentMargin.AddChild(rowHost);

        var row = AddHBox(rowHost, "TopBarRow", 10);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.CustomMinimumSize = new Vector2(0, 56);

        var commanderCard = new Panel
        {
            Name = "TopBarCommanderCard",
            CustomMinimumSize = new Vector2(308, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.Fill
        };
        ApplyTopBarSurface(
            commanderCard,
            new Color(0.050f, 0.060f, 0.070f, 0.86f),
            new Color(0.92f, 0.77f, 0.36f, 0.54f),
            16,
            new Color(0.96f, 0.76f, 0.24f, 0.09f));

        row.AddChild(commanderCard);

        var commanderMargin = new MarginContainer
        {
            Name = "TopBarCommanderMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        commanderMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        commanderMargin.AddThemeConstantOverride("margin_left", 10);
        commanderMargin.AddThemeConstantOverride("margin_top", 1);
        commanderMargin.AddThemeConstantOverride("margin_right", 12);
        commanderMargin.AddThemeConstantOverride("margin_bottom", 1);
        commanderCard.AddChild(commanderMargin);

        var commanderChip = new HBoxContainer
        {
            Name = "TopBarCommanderChip",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill
        };
        commanderChip.AddThemeConstantOverride("separation", 8);
        commanderMargin.AddChild(commanderChip);

        var commanderPortrait = new Control
        {
            CustomMinimumSize = new Vector2(54, 54),
            MouseFilter = MouseFilterEnum.Stop
        };
        commanderChip.AddChild(commanderPortrait);

        commanderPortraitButton = AddButton("", ShowCommanderProfileModal, ButtonTone.Transparent, 16);
        commanderPortraitButton.TooltipText = "查看头像与军衔";

        commanderPortrait.AddChild(commanderPortraitButton);

        var portraitDisc = new Panel
        {
            Position = new Vector2(4, 3),
            Size = new Vector2(46, 48),
            MouseFilter = MouseFilterEnum.Ignore
        };
        portraitDisc.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.020f, 0.026f, 0.030f, 0.88f),
            BorderColor = new Color(1f, 0.90f, 0.62f, 0.12f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 23,
            CornerRadiusTopRight = 23,
            CornerRadiusBottomLeft = 23,
            CornerRadiusBottomRight = 23
        });
        commanderPortrait.AddChild(portraitDisc);

        commanderPortrait.AddChild(new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(54, 54),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(UnityLobbyRoot + "gen_portrait_ring.png")
        });
        if (commanderPortrait.GetChild(commanderPortrait.GetChildCount() - 1) is TextureRect commanderFrame)
        {
            PortraitFrameUtils.ApplyWrapFrame(
                commanderFrame,
                new Color(0.86f, 0.66f, 0.20f, 0.98f),
                new Color(1f, 0.96f, 0.80f, 0.92f),
                0.49f,
                0.015f,
                0.0035f,
                0.010f);
        }

        commanderAvatarIcon = new TextureRect
        {
            Position = new Vector2(3, 3),
            Size = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = LoadTexture(LocalCommanderAvatarTexturePath()),
            MouseFilter = MouseFilterEnum.Ignore,

        };
        PortraitFrameUtils.ApplyCircularPortrait(
            commanderAvatarIcon,
            new Color(0.94f, 0.74f, 0.24f, 1f),
            new Color(1f, 0.95f, 0.78f, 1f),
            0.47f,
            0.0055f,
            0.012f,
            new Vector2(1.20f, 1.20f),
            new Vector2(0.01f, -0.025f));
        commanderPortrait.AddChild(commanderAvatarIcon);

        var commanderText = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        commanderText.AddThemeConstantOverride("separation", 1);
        commanderChip.AddChild(commanderText);

        playerLabel = AddLabel("游客指挥官", 20, PanelText, HorizontalAlignment.Left);
        playerLabel.CustomMinimumSize = new Vector2(220, 0);
        commanderText.AddChild(playerLabel);

        var rankRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        rankRow.AddThemeConstantOverride("separation", 6);
        commanderText.AddChild(rankRow);

        commanderRankIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(26, 26),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = LoadTexture(LobbyRankBadgeTexturePath(GameState.Instance?.RankTitle)),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        rankRow.AddChild(commanderRankIcon);

        rankLabel = AddLabel("Lv.1  新兵", 13, WarningText, HorizontalAlignment.Left);
        rankLabel.CustomMinimumSize = new Vector2(150, 0);
        rankLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        rankRow.AddChild(rankLabel);

        row.AddChild(Spacer());

        row.AddChild(CreateCurrencyChip(
            "閲戝竵",
            CurrencyIconRoot + "currency_gold_coin.png",
            WarningText,
            out goldLabel));

        row.AddChild(CreateCurrencyChip(
            "閽荤煶",
            CurrencyIconRoot + "currency_gem_blue.png",
            new Color(0.64f, 0.90f, 1f),
            out gemsLabel));

        row.AddChild(CreateTopBarIconDock(IconRoot + "icon_bell_transparent.svg", "公告", ShowAnnouncementModal));
        row.AddChild(CreateTopBarIconDock(IconRoot + "icon_gear_transparent.svg", "设置", ShowSettingsModal));
    }

    void BuildFriendsPanel()
    {
        var panel = AddPanel("FriendsPanel", new Rect2(0.020f, 0.145f, 0.240f, 0.725f), GlassPanel, 2);
        var box = AddVBox(panel, "FriendsBox", 8, new Vector2(14, 14), new Vector2(-14, -14));

        var titleRow = new HBoxContainer { Name = "FriendsTitleRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = AddSectionTitle("👥 好友列表");
        titleRow.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleRow.AddChild(spacer);

        var refreshBtn = AddButton("🔄 刷新", () => _ = RefreshFriends(), ButtonTone.Secondary, 11);
        refreshBtn.CustomMinimumSize = new Vector2(58, 22);
        refreshBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleRow.AddChild(refreshBtn);

        var addFriendBtn = AddButton("➕ 添加", () => ShowAddFriendModal(), ButtonTone.Primary, 11);
        addFriendBtn.CustomMinimumSize = new Vector2(58, 22);
        addFriendBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleRow.AddChild(addFriendBtn);

        var inviteBtn = AddButton("📩 邀请", () => _ = ShowInvitesModal(), ButtonTone.Gold, 11);
        inviteBtn.CustomMinimumSize = new Vector2(58, 22);
        inviteBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        titleRow.AddChild(inviteBtn);

        box.AddChild(titleRow);

        friendStatusLabel = AddLabel("好友列表加载中...", 13, MutedText, HorizontalAlignment.Left);
        box.AddChild(friendStatusLabel);

        var scroll = new ScrollContainer
        {
            Name = "FriendScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        friendRows = new VBoxContainer { Name = "FriendRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        friendRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(friendRows);
        box.AddChild(scroll);
    }

    void PrepareFriendsPanelWarmState()
    {
        if (!IsLive(friendRows) || !IsLive(friendStatusLabel))
            return;

        if (HasFriendsCache())
        {
            RenderFriends(cachedFriendsData, true, "已显示上次好友缓存，正在刷新...");
            return;
        }

        ResetFriendsPanel("正在连接好友服务...");
    }

    Control CreateCurrencyChip(string label, string iconPath, Color valueColor, out Label valueLabel)
    {
        var isGemChip = IsGemCurrencyIcon(iconPath);
        var chip = new Panel
        {
            Name = "TopBarCurrency_" + label,
            CustomMinimumSize = new Vector2(isGemChip ? 104 : 118, 38),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.Fill
        };
        ApplyTopBarSurface(
            chip,
            isGemChip ? new Color(0.050f, 0.080f, 0.108f, 0.82f) : new Color(0.085f, 0.068f, 0.028f, 0.82f),
            isGemChip ? new Color(0.58f, 0.88f, 1f, 0.46f) : new Color(0.96f, 0.79f, 0.32f, 0.50f),
            15,
            isGemChip ? new Color(0.44f, 0.88f, 1f, 0.08f) : new Color(1f, 0.78f, 0.24f, 0.08f));

        var margin = new MarginContainer
        {
            Name = "TopBarCurrencyMargin_" + label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", isGemChip ? 10 : 9);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", isGemChip ? 12 : 11);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        chip.AddChild(margin);

        var row = new HBoxContainer
        {
            Name = "TopBarCurrencyRow_" + label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", isGemChip ? 8 : 6);
        margin.AddChild(row);

        row.AddChild(new TextureRect
        {
            Texture = LoadCurrencyIconTexture(iconPath),
            CustomMinimumSize = new Vector2(isGemChip ? 22 : 20, 20),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });

        valueLabel = AddLabel($"{label} --", 17, valueColor, HorizontalAlignment.Left);
        valueLabel.CustomMinimumSize = new Vector2(isGemChip ? 48 : 74, 0);
        valueLabel.AddThemeFontSizeOverride("font_size", 16);
        valueLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(valueLabel);

        return chip;
    }

    Control CreateTopBarIconDock(string iconPath, string tooltip, Action onPressed)
    {
        var shell = new Panel
        {
            Name = "TopBarIconDock_" + tooltip,
            CustomMinimumSize = new Vector2(40, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.Fill
        };
        ApplyTopBarSurface(
            shell,
            new Color(0.045f, 0.055f, 0.062f, 0.78f),
            new Color(0.92f, 0.74f, 0.28f, 0.28f),
            14,
            new Color(0.96f, 0.76f, 0.24f, 0.06f));

        var button = AddIconButton(iconPath, tooltip, onPressed);
        button.CustomMinimumSize = new Vector2(38, 38);
        button.SetAnchorsPreset(LayoutPreset.FullRect);
        button.OffsetLeft = 1;
        button.OffsetTop = 1;
        button.OffsetRight = -1;
        button.OffsetBottom = -1;
        shell.AddChild(button);
        return shell;
    }

    void BuildModeCards()
    {
        const float modeCardLeft = 0.295f;
        const float modeCardTop = 0.245f;
        const float modeCardWidth = 0.429f;
        const float modeCardHeight = 0.152f;
        const float modeCardGap = 0.058f;

        var matchAnchors = new Rect2(modeCardLeft, modeCardTop, modeCardWidth, modeCardHeight);
        var customAnchors = new Rect2(modeCardLeft, modeCardTop + modeCardHeight + modeCardGap, modeCardWidth, modeCardHeight);
        var globalAnchors = new Rect2(modeCardLeft, modeCardTop + (modeCardHeight + modeCardGap) * 2f, modeCardWidth, modeCardHeight);

        AddModeCard("ModeMatch", matchAnchors,
            "01", QuickMatchCardTitle, "标准地图快速匹配，立即前往最近一场作战。", "立即匹配", new Color(0.86f, 0.66f, 0.24f), () => _ = TriggerQuickMatchCard());
        AddModeCard("ModeCustom", customAnchors,
            "02", CustomRoomMode, "创建或加入房间，邀请好友协同部署地图与阵营。", "创建房间", new Color(0.68f, 0.83f, 0.92f), () => OpenRoomPanel());
        AddModeCard("ModeGlobal", globalAnchors,
            "03", GlobalConquestMode, "选择阵营与起始单位，配置全球争霸中的发展方向。", "当前阵营：守卫军  |  起始：中型坦克", new Color(0.80f, 0.58f, 0.28f), () => _ = StartGlobalConquest());

        matchSelection = AddModeSelection("MatchSelection", matchAnchors);
        customSelection = AddModeSelection("CustomSelection", customAnchors);
        globalSelection = AddModeSelection("GlobalSelection", globalAnchors);
        matchSelection.Visible = customSelection.Visible = globalSelection.Visible = false;
        RefreshModeCards();
    }

    void AddModeCard(string name, Rect2 anchors, string index, string title, string desc, string action, Color accent, Action pressed)
    {
        var card = new Panel
        {
            Name = name + "Card",
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true
        };
        Place(card, anchors);
        MetalUiStyle.ApplyMetalPanel(card, MakeModeCardPalette(accent), 1, 10, 6);
        AddTopAccentStripe(card, new Color(accent.R, accent.G, accent.B, 0.72f), 3);
        AddChild(card);

        var wash = new ColorRect
        {
            Name = name + "Wash",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.01f, 0.015f, 0.020f, 0.16f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        card.AddChild(wash);

        var contentMargin = new MarginContainer
        {
            Name = name + "ContentMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 12);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_bottom", 12);
        card.AddChild(contentMargin);

        var contentRow = new HBoxContainer
        {
            Name = name + "ContentRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        contentRow.AddThemeConstantOverride("separation", 12);
        contentMargin.AddChild(contentRow);

        var textDock = new Panel
        {
            Name = name + "TextDock",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyTopBarSurface(
            textDock,
            new Color(0.018f, 0.024f, 0.030f, 0.34f),
            new Color(accent.R, accent.G, accent.B, 0.18f),
            8,
            new Color(accent.R, accent.G, accent.B, 0.05f));
        contentRow.AddChild(textDock);

        var textMargin = new MarginContainer
        {
            Name = name + "TextMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        textMargin.AddThemeConstantOverride("margin_left", 14);
        textMargin.AddThemeConstantOverride("margin_top", 10);
        textMargin.AddThemeConstantOverride("margin_right", 14);
        textMargin.AddThemeConstantOverride("margin_bottom", 8);
        textDock.AddChild(textMargin);

        var textBox = new VBoxContainer
        {
            Name = name + "Text",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textBox.AddThemeConstantOverride("separation", 4);
        textMargin.AddChild(textBox);

        var titleLabel = AddLabel(title, 23, PanelText, HorizontalAlignment.Left);
        titleLabel.CustomMinimumSize = new Vector2(0, 30);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleLabel.VerticalAlignment = VerticalAlignment.Top;
        textBox.AddChild(titleLabel);
        var descLabel = AddLabel(desc, 13, MutedText, HorizontalAlignment.Left);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.VerticalAlignment = VerticalAlignment.Top;
        descLabel.CustomMinimumSize = new Vector2(0, 40);
        textBox.AddChild(descLabel);

        var spacer = new Control
        {
            Name = name + "TextSpacer",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        textBox.AddChild(spacer);

        var actionDock = new Panel
        {
            Name = name + "ActionDock",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyTopBarSurface(
            actionDock,
            new Color(0.10f, 0.08f, 0.03f, 0.54f),
            new Color(accent.R, accent.G, accent.B, 0.32f),
            7,
            new Color(accent.R, accent.G, accent.B, 0.08f));
        textBox.AddChild(actionDock);

        var actionMargin = new MarginContainer
        {
            Name = name + "ActionMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        actionMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        actionMargin.AddThemeConstantOverride("margin_left", 8);
        actionMargin.AddThemeConstantOverride("margin_top", 4);
        actionMargin.AddThemeConstantOverride("margin_right", 10);
        actionMargin.AddThemeConstantOverride("margin_bottom", 4);
        actionDock.AddChild(actionMargin);

        var actionRow = new HBoxContainer
        {
            Name = name + "ActionRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        actionRow.AddThemeConstantOverride("separation", 7);
        actionMargin.AddChild(actionRow);

        var actionMarker = new ColorRect
        {
            Name = name + "ActionMarker",
            Color = new Color(accent.R, accent.G, accent.B, 0.88f),
            CustomMinimumSize = new Vector2(4, 14),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        actionRow.AddChild(actionMarker);

        var actionLabel = AddLabel(action, 14, WarningText, HorizontalAlignment.Left);
        actionLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actionLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        actionLabel.VerticalAlignment = VerticalAlignment.Top;
        actionLabel.CustomMinimumSize = new Vector2(0, 30);
        actionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        actionRow.AddChild(actionLabel);

        var numberDock = new Panel
        {
            Name = name + "NumberDock",
            CustomMinimumSize = new Vector2(74, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyTopBarSurface(
            numberDock,
            new Color(0.030f, 0.038f, 0.044f, 0.48f),
            new Color(accent.R, accent.G, accent.B, 0.20f),
            10,
            new Color(accent.R, accent.G, accent.B, 0.05f));
        AddTopAccentStripe(numberDock, new Color(accent.R, accent.G, accent.B, 0.48f), 2);
        contentRow.AddChild(numberDock);

        var numberCenter = new CenterContainer
        {
            Name = name + "NumberCenter",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        numberCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        numberCenter.OffsetLeft = 8;
        numberCenter.OffsetTop = 6;
        numberCenter.OffsetRight = -8;
        numberCenter.OffsetBottom = -10;
        numberDock.AddChild(numberCenter);

        var indexLabel = AddLabel(index, 30, new Color(accent.R, accent.G, accent.B, 0.70f), HorizontalAlignment.Center);
        indexLabel.MouseFilter = MouseFilterEnum.Ignore;
        indexLabel.CustomMinimumSize = new Vector2(52, 0);
        numberCenter.AddChild(indexLabel);

        var numberRule = new ColorRect
        {
            Name = name + "NumberRule",
            Color = new Color(accent.R, accent.G, accent.B, 0.40f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        numberRule.AnchorLeft = 0f;
        numberRule.AnchorTop = 1f;
        numberRule.AnchorRight = 1f;
        numberRule.AnchorBottom = 1f;
        numberRule.OffsetLeft = 14;
        numberRule.OffsetTop = -8;
        numberRule.OffsetRight = -14;
        numberRule.OffsetBottom = -6;
        numberDock.AddChild(numberRule);

        var hit = AddButton("", pressed, ButtonTone.Transparent, 16);
        hit.Name = name;
        hit.SetAnchorsPreset(LayoutPreset.FullRect);
        hit.OffsetLeft = 0;
        hit.OffsetTop = 0;
        hit.OffsetRight = 0;
        hit.OffsetBottom = 0;
        card.AddChild(hit);

        modeCards[title] = new ModeCardUi
        {
            TitleLabel = titleLabel,
            DescLabel = descLabel,
            ActionLabel = actionLabel,
            ActionDock = actionDock
        };
    }

    void BuildRightPanels()
    {
        var taskPanel = AddPanel("TasksPanel", new Rect2(0.746f, 0.145f, 0.238f, 0.455f), GlassPanel, 2);
        var taskBox = AddVBox(taskPanel, "TaskBox", 8, new Vector2(14, 14), new Vector2(-14, -14));
        taskBox.AddChild(AddSectionTitle("每日任务"));
        taskStatusLabel = AddLabel("任务加载中...", 13, MutedText, HorizontalAlignment.Left);
        taskBox.AddChild(taskStatusLabel);
        var taskScroll = new ScrollContainer
        {
            Name = "TaskScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        taskRows = new VBoxContainer
        {
            Name = "TaskRows",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        taskRows.AddThemeConstantOverride("separation", 6);
        taskScroll.AddChild(taskRows);
        taskBox.AddChild(taskScroll);
        taskBox.AddChild(AddButton("查看全部任务", () => _ = ShowTaskModal(), ButtonTone.Secondary, 13));
        BuildLocalTasks("本地任务预览");

        var techPanel = AddPanel("TechPanel", new Rect2(0.746f, 0.618f, 0.238f, 0.255f), GlassPanel, 2);
        var techBox = AddVBox(techPanel, "TechBox", 7, new Vector2(14, 12), new Vector2(-14, -12));
        techBox.AddChild(AddSectionTitle("研究中心"));
        techTitleLabel = AddLabel("暂无进行中的研究", 15, PanelText, HorizontalAlignment.Left);
        techDescLabel = AddLabel("点击研究开始服务器计时。", 12, MutedText, HorizontalAlignment.Left);
        techDescLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        techTimerLabel = AddLabel("--", 12, WarningText, HorizontalAlignment.Left);
        techProgress = new ProgressBar
        {
            Name = "TechProgress",
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10)
        };
        techBox.AddChild(techTitleLabel);
        techBox.AddChild(techDescLabel);
        techBox.AddChild(techTimerLabel);
        techBox.AddChild(techProgress);

        var techButtons = new HBoxContainer { Name = "TechButtons" };
        techButtons.AddThemeConstantOverride("separation", 8);
        techStartButton = AddButton("研究列表", ShowResearchModal, ButtonTone.Primary, 13);
        techSpeedButton = AddButton("加速", () => _ = SpeedUpTech(), ButtonTone.Gold, 13);
        techButtons.AddChild(techStartButton);
        techButtons.AddChild(techSpeedButton);
        techBox.AddChild(techButtons);
    }

    void BuildBottomNav()
    {
        var bottom = AddPanel("BottomNav", new Rect2(0.028f, 0.895f, 0.944f, 0.085f), GlassPanel, 1);
        var row = AddHBox(bottom, "BottomNavRow", 10);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 18;
        row.OffsetRight = -18;
        row.OffsetTop = 8;
        row.OffsetBottom = -8;
        row.AddChild(AddButton("商店", () => ShowShopModal(), ButtonTone.Secondary, 15));
        row.AddChild(AddButton("仓库", () => ShowWarehouseModal(), ButtonTone.Secondary, 15));
        row.AddChild(AddButton("活动", () => ShowEventsModal(), ButtonTone.Gold, 15));
        row.AddChild(AddButton("战役", () => ShowCampaignModal(), ButtonTone.Primary, 15));
        row.AddChild(AddButton("排行榜", () => _ = ShowLeaderboardModal(), ButtonTone.Secondary, 15));
        row.AddChild(AddButton("邮件", () => _ = ShowMailModal(), ButtonTone.Secondary, 15));
    }

    void BuildRoomModal()
    {
        modalShade = new Panel
        {
            Name = "ModalShade",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        Place(modalShade, new Rect2(0, 0, 1, 1));
        modalShade.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) });
        AddChild(modalShade);

        modalPanel = new Panel
        {
            Name = "ModalPanel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        Place(modalPanel, new Rect2(0.245f, 0.130f, 0.510f, 0.740f));
        MetalUiStyle.ApplyMetalPanel(modalPanel, GlassPanel, 2, 18, 4);
        AddChild(modalPanel);

        var box = AddVBox(modalPanel, "ModalBox", 10, new Vector2(18, 16), new Vector2(-18, -16));
        var header = new HBoxContainer { Name = "ModalHeader" };
        modalTitle = AddLabel("自定义房间", 22, PanelText, HorizontalAlignment.Left);
        modalTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(modalTitle);
        header.AddChild(AddCloseIconButton(CloseModal));
        box.AddChild(header);

        modalScroll = new ScrollContainer
        {
            Name = "ModalScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(modalScroll);

        modalBody = new VBoxContainer
        {
            Name = "ModalBody",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        modalBody.AddThemeConstantOverride("separation", 9);
        modalScroll.AddChild(modalBody);
    }

    void OpenRoomPanel()
    {
        SelectModeInternal(CustomRoomMode, BattleMapCatalog.DefaultMapName, false);
        modalTitle.Text = "自定义房间";
        ClearChildren(modalBody);

        var createPanel = new Panel { Name = "RoomCreatePanel", CustomMinimumSize = new Vector2(0, 122) };
        MetalUiStyle.ApplyMetalPanel(createPanel, MetalUiStyle.Steel, 1, 8, 4);
        var createBox = AddVBox(createPanel, "RoomCreateBox", 7, new Vector2(12, 10), new Vector2(-12, -10));
        var row = new HBoxContainer { Name = "RoomCreateRow" };
        row.AddThemeConstantOverride("separation", 8);
        roomNameInput = new LineEdit
        {
            Name = "RoomNameInput",
            PlaceholderText = "房间名（可选）",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        roomMapPicker = new OptionButton { Name = "RoomMapPicker", CustomMinimumSize = new Vector2(150, 0) };
        foreach (var map in BattleMapCatalog.GetCustomRoomMapNames())
            roomMapPicker.AddItem(map);
        roomMapPicker.ItemSelected += (idx) =>
        {
            if (!string.IsNullOrEmpty(activeRoomId) && activeRoomHostId == GameState.Instance?.UserId)
            {
                var mapText = BattleMapCatalog.NormalizeCustomRoomMap(roomMapPicker.GetItemText((int)idx));
                _ = UpdateRoomMap(mapText);
            }
        };
        roomPlayerCountPicker = new OptionButton { Name = "RoomPlayerCountPicker", CustomMinimumSize = new Vector2(110, 0) };
        foreach (var count in CustomRoomPlayerCounts)
        {
            if (count == 5)
            {
                roomPlayerCountPicker.AddItem("5人组队团", 5);
            }
            else
            {
                var teamSize = Math.Max(1, count / 2);
                roomPlayerCountPicker.AddItem($"{teamSize}v{teamSize}", count);
            }
        }
        row.AddChild(roomNameInput);
        row.AddChild(roomMapPicker);
        row.AddChild(roomPlayerCountPicker);
        row.AddChild(AddButton("创建", () => _ = CreateRoom(), ButtonTone.Primary, 13));
        row.AddChild(AddButton("刷新", () => _ = RefreshRooms(), ButtonTone.Secondary, 13));
        createBox.AddChild(row);
        createBox.AddChild(AddLabel("自定义房间只显示小地图，最高支持 3v3 组房；全球争霸独占大地图。当前实时开战入口仍先按 1v1 接入。", 12, WarningText, HorizontalAlignment.Left));
        roomStatusLabel = AddLabel("房间加载中...", 13, MutedText, HorizontalAlignment.Left);
        createBox.AddChild(roomStatusLabel);
        modalBody.AddChild(createPanel);
        ShowCreatedRoomActions();

        var scroll = new ScrollContainer
        {
            Name = "RoomScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        roomRows = new VBoxContainer { Name = "RoomRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        roomRows.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(roomRows);
        modalBody.AddChild(scroll);

        ShowModal();
        _ = RefreshRooms();
    }

    async Task RefreshLobbyData(bool showStatus = true)
    {
        if (NetClient.Instance is null)
            return;

        if (showStatus)
            taskStatusLabel.Text = "任务同步中...";

        if (GameState.Instance?.IsGuest == true)
        {
            taskStatusLabel.Text = "离线模式：显示本地任务样例";
            BuildLocalTasks();
            UpdateTechPanel(new Godot.Collections.Dictionary());
            return;
        }

        var data = await NetClient.Instance.GetLobby();
        if (!data.GetBool("success"))
        {
            taskStatusLabel.Text = "离线模式：显示本地任务样例";
            BuildLocalTasks();
            UpdateTechPanel(new Godot.Collections.Dictionary());
            return;
        }

        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        BuildTaskRows(data.GetArray("tasks"));
        UpdateTechPanel(data);
    }

    void BuildTaskRows(Godot.Collections.Array tasks)
    {
        ClearChildren(taskRows);
        if (tasks.Count == 0)
        {
            BuildLocalTasks("暂无联网任务，显示本地任务样例");
            return;
        }

        taskStatusLabel.Text = $"已同步 {tasks.Count} 个任务";
        foreach (var item in tasks)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var task = item.AsGodotDictionary();
            var taskId = task.GetString("id");
            var title = FriendlyText(task.GetString("title"), taskId switch
            {
                "daily_login" => "每日登录",
                "win3" => "赢得 3 场战斗",
                "destroy20" => "摧毁 20 个敌方单位",
                _ => "作战任务"
            });
            var cur = task.GetInt("cur");
            var max = Math.Max(1, task.GetInt("max", 1));
            var claimed = task.GetBool("claimed");
            var canClaim = task.GetBool("canClaim");
            taskRows.AddChild(MakeTaskRow(taskId, title, cur, max, claimed, canClaim));
        }
    }

    void BuildLocalTasks(string statusText = "离线模式：显示本地任务样例")
    {
        taskStatusLabel.Text = statusText;
        ClearChildren(taskRows);
        taskRows.AddChild(MakeTaskRow("daily_login", "每日登录", 1, 1, IsLocalTaskClaimed("daily_login"), !IsLocalTaskClaimed("daily_login")));
        taskRows.AddChild(MakeTaskRow("win3", "赢得 3 场战斗", 2, 3, IsLocalTaskClaimed("win3"), false));
        taskRows.AddChild(MakeTaskRow("destroy20", "摧毁 20 个敌方单位", 12, 20, IsLocalTaskClaimed("destroy20"), false));
        taskRows.AddChild(MakeTaskRow("destroy_base", "摧毁敌军主基地", 1, 1, IsLocalTaskClaimed("destroy_base"), !IsLocalTaskClaimed("destroy_base")));
        taskRows.AddChild(MakeTaskRow("train_tanks", "生产 10 辆坦克单位", 10, 10, IsLocalTaskClaimed("train_tanks"), !IsLocalTaskClaimed("train_tanks")));
    }

    Control MakeTaskRow(string taskId, string title, int cur, int max, bool claimed, bool canClaim)
    {
        var panel = new Panel { Name = "TaskRow_" + taskId, CustomMinimumSize = new Vector2(0, 60), ClipContents = true };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);

        // 二战军事风背景底图
        var bgPath = taskId switch
        {
            "daily_login" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_radar_map.png",
            "win3" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_assault_arrows.png",
            "destroy20" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_firepower_barrage.png",
            "conquest_win" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_radar_map.png",
            "destroy_base" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_hold_bunker.png",
            "build_barracks" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_generic_plate.png",
            "train_tanks" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_armor_plate.png",
            "power_plant" => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_repair_workshop.png",
            _ => "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_generic_plate.png"
        };

        if (ResourceLoader.Exists(bgPath))
        {
            var tex = ResourceLoader.Load<Texture2D>(bgPath);
            if (tex is not null)
            {
                var bgTex = new TextureRect
                {
                    Name = "TaskRowBg",
                    Texture = tex,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    SelfModulate = new Color(1f, 1f, 1f, 0.22f), // 半透明，融入背景
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                bgTex.SetAnchorsPreset(LayoutPreset.FullRect);
                panel.AddChild(bgTex);
            }
        }

        var row = AddHBox(panel, "TaskRowBox", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 8;
        row.OffsetBottom = -8;

        var info = new VBoxContainer { Name = "TaskInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        info.AddChild(AddLabel(title, 13, PanelText, HorizontalAlignment.Left));

        var metaRow = new HBoxContainer { Name = "TaskMetaRow" };
        metaRow.AddThemeConstantOverride("separation", 8);
        metaRow.AddChild(AddLabel($"{cur}/{max}", 11, canClaim ? GoodText : MutedText, HorizontalAlignment.Left));

        var rewardText = taskId switch
        {
            "daily_login" => "奖励: 100金币",
            "win3" => "奖励: 200金币",
            "destroy20" => "奖励: 150金币 + 5钻石",
            "conquest_win" => "奖励: 300金币 + 10钻石",
            "destroy_base" => "奖励: 250金币",
            "build_barracks" => "奖励: 150金币",
            "train_tanks" => "奖励: 200金币",
            "power_plant" => "奖励: 120金币",
            _ => "奖励: 100金币"
        };
        metaRow.AddChild(AddLabel(rewardText, 11, new Color(0.95f, 0.76f, 0.24f), HorizontalAlignment.Left));

        info.AddChild(metaRow);
        row.AddChild(info);

        var button = AddButton(claimed ? "已领" : canClaim ? "领取" : "未达成", () => _ = ClaimTask(taskId), canClaim ? ButtonTone.Gold : ButtonTone.Secondary, 12);
        button.Disabled = claimed || !canClaim;
        button.CustomMinimumSize = new Vector2(66, 30);
        row.AddChild(button);
        return panel;
    }

    async Task ClaimTask(string taskId)
    {
        var isLocal = NetClient.Instance is null || GameState.Instance?.IsGuest == true;
        if (isLocal)
        {
            var goldReward = taskId switch
            {
                "daily_login" => 100,
                "win3" => 200,
                "destroy20" => 150,
                "conquest_win" => 300,
                "destroy_base" => 250,
                "build_barracks" => 150,
                "train_tanks" => 200,
                "power_plant" => 120,
                _ => 100
            };
            var gemsReward = taskId switch
            {
                "destroy20" => 5,
                "conquest_win" => 10,
                _ => 0
            };

            GameState.Instance?.AddCurrency(goldReward, gemsReward);
            RefreshTopBar();
            ShowToast($"任务奖励领取成功！金币 +{goldReward}" + (gemsReward > 0 ? $"，钻石 +{gemsReward}" : ""));
            
            MarkLocalTaskClaimed(taskId);
            
            BuildLocalTasks("本地任务预览");
            if (activeFeatureModal == "tasks" && modalPanel.Visible && string.Equals(modalTitle.Text, "全部任务", StringComparison.Ordinal))
            {
                await ShowTaskModal();
            }
            return;
        }

        if (NetClient.Instance is null)
            return;

        var data = await NetClient.Instance.ClaimTask(taskId);
        if (!data.GetBool("success"))
        {
            ShowToast("任务领取失败：" + FriendlyText(data.GetString("error"), "请稍后重试"));
            return;
        }

        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        ShowToast("任务奖励已领取");
        await RefreshLobbyData(false);
        if (activeFeatureModal == "tasks" && modalPanel.Visible && string.Equals(modalTitle.Text, "全部任务", StringComparison.Ordinal))
            await ShowTaskModal();
    }

    async Task ShowTaskModal()
    {
        activeFeatureModal = "tasks";
        OpenFeatureModal("全部任务", "查看当前账号的每日任务进度、完成状态与奖励领取入口。");
        modalBody.AddChild(AddLabel("任务同步中...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();

        Godot.Collections.Dictionary data;
        if (GameState.Instance?.IsGuest == true)
        {
            data = new Godot.Collections.Dictionary();
        }
        else if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetJson("/api/lobby", SecondaryModalRequestTimeoutSec);
        else
            data = new Godot.Collections.Dictionary();

        var tasks = data.GetArray("tasks");
        var usingLiveData = data.GetBool("success") && tasks.Count > 0;

        OpenFeatureModal("全部任务", "查看当前账号的每日任务进度、完成状态与奖励领取入口。");
        AddModalSummaryRow(
            AddStatCard("任务总数", $"{(usingLiveData ? tasks.Count : 8)}", usingLiveData ? "当前同步任务" : "离线样例任务", WarningText),
            AddStatCard("可领取", $"{CountClaimableTasks(tasks, usingLiveData)}", "已完成待领取", GoodText),
            AddStatCard("模式", usingLiveData ? "实时数据" : "离线预览", usingLiveData ? "服务器已同步" : "服务器不可用时展示", PanelText));

        var section = AddModalSectionPanel(
            "任务列表",
            usingLiveData ? $"当前共 {tasks.Count} 个任务，点击领取会立即刷新。" : "当前显示本地任务样例，联网后会替换为真实数据。",
            WarningText);

        if (usingLiveData)
        {
            foreach (var item in tasks)
            {
                if (item.VariantType != Variant.Type.Dictionary)
                    continue;

                var task = item.AsGodotDictionary();
                var taskId = task.GetString("id");
                var title = FriendlyText(task.GetString("title"), taskId switch
                {
                    "daily_login" => "每日登录",
                    "win3" => "赢得 3 场战斗",
                    "destroy20" => "摧毁 20 个敌方单位",
                    "conquest_win" => "赢得 1 场全球争霸战",
                    "destroy_base" => "摧毁敌军主基地",
                    "build_barracks" => "在一局中建造 2 个兵营",
                    "train_tanks" => "生产 10 辆坦克单位",
                    "power_plant" => "建造 3 个发电厂",
                    _ => "作战任务"
                });
                var cur = task.GetInt("cur");
                var max = Math.Max(1, task.GetInt("max", 1));
                var claimed = task.GetBool("claimed");
                var canClaim = task.GetBool("canClaim");
                section.AddChild(MakeTaskRow(taskId, title, cur, max, claimed, canClaim));
            }
        }
        else
        {
            section.AddChild(MakeTaskRow("daily_login", "每日登录", 1, 1, IsLocalTaskClaimed("daily_login"), !IsLocalTaskClaimed("daily_login")));
            section.AddChild(MakeTaskRow("win3", "赢得 3 场战斗", 2, 3, IsLocalTaskClaimed("win3"), false));
            section.AddChild(MakeTaskRow("destroy20", "摧毁 20 个敌方单位", 12, 20, IsLocalTaskClaimed("destroy20"), false));
            section.AddChild(MakeTaskRow("conquest_win", "赢得 1 场全球争霸战", 0, 1, IsLocalTaskClaimed("conquest_win"), false));
            section.AddChild(MakeTaskRow("destroy_base", "摧毁敌军主基地", 1, 1, IsLocalTaskClaimed("destroy_base"), !IsLocalTaskClaimed("destroy_base")));
            section.AddChild(MakeTaskRow("build_barracks", "在一局中建造 2 个兵营", 1, 2, IsLocalTaskClaimed("build_barracks"), false));
            section.AddChild(MakeTaskRow("train_tanks", "生产 10 辆坦克单位", 10, 10, IsLocalTaskClaimed("train_tanks"), !IsLocalTaskClaimed("train_tanks")));
            section.AddChild(MakeTaskRow("power_plant", "建造 3 个发电厂", 3, 3, true, false)); // 默认已领示范
        }

        var actionRow = new HBoxContainer { Name = "TaskModalActions" };
        actionRow.AddThemeConstantOverride("separation", 8);
        var helper = AddLabel("主面板现在用于快速预览；完整任务操作统一放在这里。", 12, MutedText, HorizontalAlignment.Left);
        helper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        actionRow.AddChild(helper);
        actionRow.AddChild(AddButton("刷新任务", () => _ = ShowTaskModal(), ButtonTone.Secondary, 12));
        modalBody.AddChild(actionRow);

        ShowModal();
    }

    int CountClaimableTasks(Godot.Collections.Array tasks, bool usingLiveData)
    {
        if (!usingLiveData)
        {
            var count = 0;
            if (!IsLocalTaskClaimed("daily_login")) count++;
            if (!IsLocalTaskClaimed("destroy_base")) count++;
            if (!IsLocalTaskClaimed("train_tanks")) count++;
            return count;
        }

        var res = 0;
        foreach (var item in tasks)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var task = item.AsGodotDictionary();
            if (task.GetBool("canClaim") && !task.GetBool("claimed"))
                res++;
        }
        return res;
    }

    void UpdateTechPanel(Godot.Collections.Dictionary data)
    {
        var techName = FriendlyText(data.GetString("techName"), "");
        techEndAtMs = data.GetLong("techEndAt");
        techTotalSec = Math.Max(1, data.GetInt("techTotalSec", 1));
        if (!string.IsNullOrEmpty(techName) && techEndAtMs > NowMs())
        {
            techTitleLabel.Text = techName;
            techDescLabel.Text = FriendlyText(data.GetString("techDesc"), "研究进行中，完成后在战斗中生效。");
            techStartButton.Visible = false;
            techSpeedButton.Visible = true;
        }
        else
        {
            techEndAtMs = 0;
            techTitleLabel.Text = "暂无进行中的研究";
            techDescLabel.Text = "点击研究开始服务器计时。";
            techTimerLabel.Text = "--";
            techProgress.Value = 0;
            techStartButton.Visible = true;
            techSpeedButton.Visible = false;
        }
        UpdateTechTimer();
    }

    void UpdateTechTimer()
    {
        if (techEndAtMs <= 0)
            return;

        var left = Math.Max(0, techEndAtMs - NowMs());
        techTimerLabel.Text = left <= 0 ? "研究完成，等待刷新" : "剩余：" + FormatDuration(left / 1000);
        techProgress.Value = 1.0 - Math.Clamp((double)left / (techTotalSec * 1000.0), 0.0, 1.0);
        if (left <= 0)
            _ = RefreshLobbyData(false);
    }

    async Task StartTechResearch()
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.StartTechResearch();
        if (!data.GetBool("success"))
        {
            ShowToast("研究开始失败：" + FriendlyText(data.GetString("error"), "请稍后重试"));
            return;
        }
        ShowToast("研究已开始");
        await RefreshLobbyData(false);
    }

    async Task SpeedUpTech()
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.SpeedUpTech();
        if (!data.GetBool("success"))
        {
            ShowToast("加速失败：" + FriendlyText(data.GetString("error"), "钻石不足或无研究"));
            return;
        }
        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        ShowToast("已消耗钻石加速 10 分钟");
        await RefreshLobbyData(false);
    }

    Task RefreshFriends(bool silent = false)
    {
        if (friendsRefreshTask is not null && !friendsRefreshTask.IsCompleted)
            return friendsRefreshTask;

        friendsRefreshTask = RefreshFriendsCore(silent);
        return friendsRefreshTask;
    }

    async Task RefreshFriendsCore(bool silent)
    {
        if (!IsLive(friendRows) || !IsLive(friendStatusLabel))
            return;

        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token) || GameState.Instance?.IsGuest == true)
        {
            ResetFriendsPanel("请先登录后使用好友系统");
            return;
        }

        if (!silent)
            friendStatusLabel.Text = HasFriendsCache() ? "已显示缓存，正在刷新好友..." : "正在同步好友...";

        var data = await NetClient.Instance.GetFriends(FriendsRequestTimeoutSec);
        if (!data.GetBool("success"))
        {
            if (HasFriendsCache())
            {
                RenderFriends(cachedFriendsData, true, silent ? "" : "网络较慢：已显示上次好友缓存");
                return;
            }

            if (!silent)
                ResetFriendsPanel("离线模式：显示好友样例");
            return;
        }

        cachedFriendsData = (Godot.Collections.Dictionary)data.Duplicate(true);
        friendsCacheAtMs = NowMs();
        SaveFriendsCache();
        RenderFriends(data, false);
    }

    void ResetFriendsPanel(string statusText)
    {
        loadedFriendNames.Clear();
        ClearChildren(friendRows);
        friendStatusLabel.Text = statusText;
        AddLocalFriendRows();
    }

    void RenderFriends(Godot.Collections.Dictionary data, bool usingCache, string statusOverride = "")
    {
        if (!IsLive(friendRows) || !IsLive(friendStatusLabel))
            return;

        loadedFriendNames.Clear();
        ClearChildren(friendRows);

        var friends = data.GetArray("friends");
        friendStatusLabel.Text = !string.IsNullOrWhiteSpace(statusOverride)
            ? statusOverride
            : friends.Count == 0
                ? "暂时还没有好友，先添加一位吧"
                : usingCache
                    ? $"已显示缓存中的 {friends.Count} 位好友"
                    : $"当前在线同步到 {friends.Count} 位好友";

        foreach (var item in friends)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var friend = item.AsGodotDictionary();
            var name = friend.GetString("username");
            if (!string.IsNullOrWhiteSpace(name))
                loadedFriendNames.Add(name);
            friendRows.AddChild(MakeFriendRow(
                name,
                FriendlyText(friend.GetString("rankTitle"), FriendlyText(friend.GetString("rank"), "好友")),
                FriendlyText(friend.GetString("status"), "在线"),
                friend.GetInt("level", 1)));
        }

        if (friends.Count == 0)
            AddLocalFriendRows();
    }

    void AddLocalFriendRows()
    {
        friendRows.AddChild(MakeFriendRow("IronWolf", "黄金指挥官", "在线", 18));
        friendRows.AddChild(MakeFriendRow("SeaHammer", "白银突击兵", "匹配中", 14));
        friendRows.AddChild(MakeFriendRow("DesertFox", "装甲先锋", "离线", 9));
    }

    Control MakeFriendRow(string username, string rank, string status, int level)
    {
        var panel = new Panel
        {
            Name = "Friend_" + username,
            CustomMinimumSize = new Vector2(0, 58),
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);

        panel.MouseEntered += () =>
        {
            MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Gold, 1, 4, 3);
        };
        panel.MouseExited += () =>
        {
            MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);
        };

        var row = AddHBox(panel, "FriendRow", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 6;
        row.OffsetBottom = -6;
        row.MouseFilter = Control.MouseFilterEnum.Ignore;

        row.AddChild(CreateFriendAvatar(username, status));

        var info = new VBoxContainer
        {
            Name = "FriendInfo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        info.AddThemeConstantOverride("separation", 1);

        var nameLabel = AddLabel(username, 13, PanelText, HorizontalAlignment.Left);
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        info.AddChild(nameLabel);

        var rankRow = new HBoxContainer
        {
            Name = "FriendRankRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rankRow.AddThemeConstantOverride("separation", 4);

        var levelLabel = AddLabel($"Lv.{level}", 11, MutedText, HorizontalAlignment.Left);
        levelLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        levelLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        rankRow.AddChild(levelLabel);

        rankRow.AddChild(CreateFriendRankBadge(rank));

        var rankLabel = AddLabel(NormalizeRankTitle(rank), 11, LobbyRankColor(rank), HorizontalAlignment.Left);
        rankLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        rankLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rankRow.AddChild(rankLabel);

        info.AddChild(rankRow);
        row.AddChild(info);

        var statusLabel = AddLabel(status, 12, StatusColor(status), HorizontalAlignment.Right);
        statusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        statusLabel.CustomMinimumSize = new Vector2(64, 0);
        row.AddChild(statusLabel);
        return panel;
    }

    TextureRect CreateFriendRankBadge(string rankTitle)
    {
        return new TextureRect
        {
            CustomMinimumSize = new Vector2(14, 14),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(LobbyRankBadgeTexturePath(rankTitle))
        };
    }

    Control CreateFriendAvatar(string username, string status)
    {
        var accent = StatusColor(status);
        var holder = new Control
        {
            Name = "FriendAvatar_" + username,
            CustomMinimumSize = new Vector2(42, 42),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };

        var disc = new Panel
        {
            Position = Vector2.Zero,
            Size = new Vector2(42, 42),
            MouseFilter = MouseFilterEnum.Ignore
        };
        disc.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.018f, 0.024f, 0.028f, 0.90f),
            BorderColor = new Color(accent.R, accent.G, accent.B, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21
        });
        holder.AddChild(disc);

        var avatar = new TextureRect
        {
            Position = new Vector2(2, 2),
            Size = new Vector2(38, 38),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(ResolveFriendAvatarTexturePath(username))
        };
        PortraitFrameUtils.ApplyCircularPortrait(
            avatar,
            new Color(0f, 0f, 0f, 0f),
            new Color(0f, 0f, 0f, 0f),
            0.49f,
            0.0f,
            0.010f,
            new Vector2(1.22f, 1.22f),
            new Vector2(0.01f, -0.025f));
        holder.AddChild(avatar);
        return holder;
    }




















    void ShowAddFriendModal()
    {
        modalTitle.Text = "添加好友";
        ClearChildren(modalBody);

        var wrap = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        wrap.AddThemeConstantOverride("separation", 16);

        wrap.AddChild(AddLabel("请输入你要添加的好友玩家名称：", 14, MutedText, HorizontalAlignment.Left));

        addFriendInput = new LineEdit
        {
            Name = "ModalAddFriendInput",
            PlaceholderText = "输入好友名称",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var editStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.10f, 0.88f),
            BorderColor = new Color(0.42f, 0.50f, 0.58f, 0.32f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        addFriendInput.AddThemeStyleboxOverride("normal", editStyle);
        addFriendInput.AddThemeStyleboxOverride("focus", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.10f, 0.88f),
            BorderColor = new Color(0.96f, 0.79f, 0.30f, 0.62f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
        addFriendInput.TextSubmitted += (val) => _ = AddFriend();
        wrap.AddChild(addFriendInput);

        var btnRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        btnRow.AddThemeConstantOverride("separation", 10);

        var cancelBtn = AddButton("取消", CloseModal, ButtonTone.Secondary, 13);
        cancelBtn.CustomMinimumSize = new Vector2(88, 30);
        btnRow.AddChild(cancelBtn);

        var confirmBtn = AddButton("发送申请", () => _ = AddFriend(), ButtonTone.Primary, 13);
        confirmBtn.CustomMinimumSize = new Vector2(88, 30);
        btnRow.AddChild(confirmBtn);

        wrap.AddChild(btnRow);

        modalBody.AddChild(wrap);
        ShowModal();

        // 弹窗显示后立即聚焦输入框
        addFriendInput.GrabFocus();
    }

    async Task AddFriend()
    {
        var name = addFriendInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowToast("请输入好友名称");
            return;
        }

        if (NetClient.Instance is null || GameState.Instance?.IsGuest == true)
        {
            ShowToast("游客模式下无法添加好友");
            return;
        }

        friendStatusLabel.Text = "正在发送好友请求...";
        var data = await NetClient.Instance.AddFriend(name);
        if (!data.GetBool("success"))
        {
            friendStatusLabel.Text = "好友添加失败";
            ShowToast(FriendlyText(data.GetString("error"), "找不到该玩家"));
            return;
        }

        CloseModal();
        ShowToast($"已发送好友申请给 {name}");
        await RefreshFriends();
    }

    async Task RefreshRooms()
    {
        if (!IsLive(roomRows))
            return;

        ClearChildren(roomRows);

        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "请先登录后再使用房间功能";
            AddRoomPlaceholder("请先登录后再使用房间功能，登录后可刷新、创建 and 邀请好友。");
            return;
        }

        if (!string.IsNullOrEmpty(activeRoomId))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = $"房间ID：{ShortRoomId(activeRoomId)} | 成员 {activeRoomPlayerCount}/{activeRoomMaxPlayers}";

            var isHost = activeRoomHostId == GameState.Instance?.UserId;

            if (roomMapPicker is not null)
                roomMapPicker.Disabled = !isHost;
            if (roomNameInput is not null)
                roomNameInput.Editable = false;
            if (roomPlayerCountPicker is not null)
                roomPlayerCountPicker.Disabled = true;

            var memberSection = AddModalSectionPanel("当前房间成员", isHost ? "您是房主，可以管理成员、修改地图或启动战斗" : "正在等待房主开始游戏...", isHost ? WarningText : GoodText);
            roomRows.AddChild(memberSection);

            foreach (var player in activeRoomPlayers)
            {
                var isMe = player == GameState.Instance?.UserId;
                var playerCard = new Panel { Name = "Member_" + player, CustomMinimumSize = new Vector2(0, 50) };
                MetalUiStyle.ApplyMetalPanel(playerCard, isMe ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 3);

                var hBox = AddHBox(playerCard, "MemberBox", 10);
                hBox.SetAnchorsPreset(LayoutPreset.FullRect);
                hBox.OffsetLeft = 10;
                hBox.OffsetRight = -10;
                hBox.OffsetTop = 6;
                hBox.OffsetBottom = -6;

                var nameStr = player == activeRoomHostId ? $"[房主] {player}" : $"[成员] {player}";
                hBox.AddChild(AddLabel(nameStr, 13, isMe ? GoodText : PanelText, HorizontalAlignment.Left));

                if (isHost && !isMe)
                {
                    var kickBtn = AddButton("踢出", () => _ = KickPlayer(player), ButtonTone.Secondary, 11);
                    kickBtn.CustomMinimumSize = new Vector2(60, 24);
                    hBox.AddChild(kickBtn);
                }

                memberSection.AddChild(playerCard);
            }
            return;
        }

        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "加载中...";
        var data = await NetClient.Instance.GetRooms();
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "房间服务不可用";
            AddRoomPlaceholder("服务器未连接，稍后重试。");
            return;
        }

        var rooms = data.GetArray("rooms");
        var visibleRooms = new List<Godot.Collections.Dictionary>();
        foreach (var item in rooms)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;

            var room = item.AsGodotDictionary();
            if (!IsSupportedCustomRoom(room))
                continue;

            visibleRooms.Add(room);
        }

        var activeRoomStillVisible = string.IsNullOrEmpty(activeRoomId) && string.IsNullOrEmpty(GameState.Instance?.CurrentRoomId);
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = visibleRooms.Count == 0 ? "暂无房间，可以创建一个" : $"找到 {visibleRooms.Count} 个房间";
        if (visibleRooms.Count == 0)
            AddRoomPlaceholder("暂无等待中的房间。");

        foreach (var room in visibleRooms)
        {
            var roomId = room.GetString("id", room.GetString("roomId"));
            if (!string.IsNullOrEmpty(roomId) && (roomId == activeRoomId || roomId == GameState.Instance?.CurrentRoomId))
                activeRoomStillVisible = true;
            AddRoomRow(room);
        }

        if (!activeRoomStillVisible)
            ClearActiveRoomState();
    }

    void AddRoomPlaceholder(string text)
    {
        if (!IsLive(roomRows))
            return;
        roomRows.AddChild(AddLabel(text, 14, MutedText, HorizontalAlignment.Center));
    }

    void AddRoomRow(Godot.Collections.Dictionary room)
    {
        if (!IsLive(roomRows))
            return;

        var roomId = room.GetString("id", room.GetString("roomId"));
        var roomName = FriendlyText(room.GetString("name"), "房间");
        var mapName = FriendlyText(room.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var players = room.GetArray("players");
        var maxPlayers = room.GetInt("maxPlayers", 2);
        var count = players.Count;
        var supportsRealtimeBattle = SupportsRealtimeRoomBattle(maxPlayers);
        var isActiveRoom = !string.IsNullOrEmpty(roomId) && (roomId == activeRoomId || roomId == GameState.Instance?.CurrentRoomId);
        if (isActiveRoom)
        {
            activeRoomId = roomId;
            activeRoomMap = mapName;
            activeRoomMaxPlayers = maxPlayers;
            activeRoomPlayerCount = count;

            activeRoomHostId = room.GetString("hostId");
            activeRoomPlayers.Clear();
            foreach (var p in players)
            {
                activeRoomPlayers.Add(p.AsString());
            }
        }

        var panel = new Panel { Name = "Room_" + roomId, CustomMinimumSize = new Vector2(0, 68) };
        MetalUiStyle.ApplyMetalPanel(panel, isActiveRoom ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 6, 4);
        var row = AddHBox(panel, "RoomRow", 10);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 10;
        row.OffsetRight = -10;
        row.OffsetTop = 8;
        row.OffsetBottom = -8;

        var info = new VBoxContainer { Name = "RoomInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddChild(AddLabel(isActiveRoom ? $"当前房间：{roomName}" : roomName, 15, isActiveRoom ? GoodText : PanelText, HorizontalAlignment.Left));
        var modeHint = DescribeRoomModeHint(maxPlayers);
        info.AddChild(AddLabel($"{mapName}  {count}/{maxPlayers}  {modeHint}  {FriendlyText(room.GetString("status"), "waiting")}", 12, isActiveRoom ? GoodText : MutedText, HorizontalAlignment.Left));
        row.AddChild(info);

        var invite = AddButton("邀请", () => ShowInvitePanel(roomId), ButtonTone.Secondary, 12);
        invite.Disabled = string.IsNullOrEmpty(roomId);
        row.AddChild(invite);
        var join = AddButton(isActiveRoom ? "进入" : "加入", () =>
        {
            if (isActiveRoom)
                _ = EnterRoomBattle(roomId, mapName, maxPlayers);
            else
                _ = JoinRoom(roomId);
        }, ButtonTone.Primary, 12);
        join.Disabled = string.IsNullOrEmpty(roomId) || (!isActiveRoom && count >= maxPlayers);
        row.AddChild(join);
        roomRows.AddChild(panel);
    }

    async Task KickPlayer(string targetUserId)
    {
        if (NetClient.Instance is null || string.IsNullOrEmpty(activeRoomId))
            return;
        var data = await NetClient.Instance.KickRoomPlayer(activeRoomId, targetUserId);
        if (data.GetBool("success"))
        {
            ShowToast("已成功踢出玩家");
            await RefreshRooms();
        }
        else
        {
            ShowToast("踢出失败");
        }
    }

    async Task UpdateRoomMap(string mapText)
    {
        if (NetClient.Instance is null || string.IsNullOrEmpty(activeRoomId))
            return;
        var data = await NetClient.Instance.UpdateRoomSettings(activeRoomId, mapText);
        if (data.GetBool("success"))
        {
            activeRoomMap = mapText;
            ShowToast($"地图已同步切换为：{mapText}");
            await RefreshRooms();
            ShowCreatedRoomActions();
        }
    }

    async Task CreateRoom()
    {
        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            ShowToast("请先登录后再创建房间");
            return;
        }

        var mapName = BattleMapCatalog.DefaultMapName;
        if (roomMapPicker is not null && roomMapPicker.ItemCount > 0)
            mapName = BattleMapCatalog.NormalizeCustomRoomMap(roomMapPicker.GetItemText(roomMapPicker.Selected));
        var roomName = roomNameInput?.Text.Trim() ?? "";
        var maxPlayers = SelectedRoomPlayerCount();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "创建中...";

        var data = await NetClient.Instance.CreateRoom(mapName, roomName, maxPlayers);
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "创建失败";
            ShowToast(FriendlyText(data.GetString("error"), "创建失败"));
            return;
        }

        activeRoomId = data.GetString("roomId");
        activeRoomMap = data.GetString("mapName", mapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", maxPlayers);
        activeRoomPlayerCount = data.GetInt("playerCount", 1);
        selectedMap = activeRoomMap;

        if (activeRoomMaxPlayers == 5 && activeRoomMap == BattleMapCatalog.GlobalConquestName)
        {
            selectedMode = GlobalConquestMode;
            GameState.Instance?.SelectMap(activeRoomMap, GlobalConquestMode);
            GameState.Instance?.SetCurrentRoom(activeRoomId);
            ShowToast("全球争霸5人组队房间已创建");
            await RefreshRooms();
            _ = OpenGlobalConquestTeamPanel();
            return;
        }

        GameState.Instance?.SelectMap(activeRoomMap, CustomRoomMode);
        GameState.Instance?.SetCurrentRoom(activeRoomId);
        ShowToast("房间已创建");
        await RefreshRooms();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = activeRoomMaxPlayers <= 2
                ? $"房间已创建：{activeRoomMap}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}。可以邀请好友，并直接进入战斗。"
                : $"房间已创建：{activeRoomMap}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}。当前房间最高支持 3v3 组队，可继续邀请成员集合。";
        ShowCreatedRoomActions();
    }

    void ShowCreatedRoomActions()
    {
        if (activeRoomActionsPanel is not null && GodotObject.IsInstanceValid(activeRoomActionsPanel))
            activeRoomActionsPanel.QueueFree();
        activeRoomActionsPanel = null;

        if (string.IsNullOrEmpty(activeRoomId) || !IsCustomRoomModalContext())
            return;

        var panel = new Panel { Name = "ActiveRoomActions", CustomMinimumSize = new Vector2(0, 70) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Green, 1, 8, 4);
        var row = AddHBox(panel, "ActiveRoomRow", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 9;
        row.OffsetBottom = -9;

        var info = new VBoxContainer { Name = "ActiveRoomInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        info.AddChild(AddLabel("已进入房间", 15, GoodText, HorizontalAlignment.Left));
        info.AddChild(AddLabel($"{FriendlyText(activeRoomMap, BattleMapCatalog.DefaultMapName)}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}  ID:{ShortRoomId(activeRoomId)}", 12, MutedText, HorizontalAlignment.Left));
        row.AddChild(info);
        row.AddChild(AddButton("邀请好友", () => ShowInvitePanel(activeRoomId), ButtonTone.Gold, 12));
        row.AddChild(AddButton("离开房间", () => _ = LeaveActiveRoom(), ButtonTone.Secondary, 12));

        var isHost = activeRoomHostId == GameState.Instance?.UserId;
        var enterButton = AddButton(
            isHost ? "开始游戏" : "等待房主开始...",
            () => _ = EnterRoomBattle(activeRoomId, activeRoomMap, activeRoomMaxPlayers),
            ButtonTone.Primary,
            12);
        enterButton.Disabled = !isHost;
        row.AddChild(enterButton);

        modalBody.AddChild(panel);
        modalBody.MoveChild(panel, Mathf.Min(1, modalBody.GetChildCount() - 1));
        activeRoomActionsPanel = panel;
    }

    async Task JoinRoom(string roomId)
    {
        if (NetClient.Instance is null || string.IsNullOrWhiteSpace(roomId))
            return;

        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "加入中...";
        var data = await NetClient.Instance.JoinRoom(roomId);
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "加入失败";
            ShowToast(FriendlyText(data.GetString("error"), "房间不可加入"));
            await RefreshRooms();
            return;
        }

        activeRoomId = data.GetString("roomId", roomId);
        activeRoomMap = data.GetString("mapName", BattleMapCatalog.DefaultMapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", 2);
        activeRoomPlayerCount = data.GetInt("playerCount", 2);
        GameState.Instance?.SetCurrentRoom(activeRoomId);

        if (activeRoomMaxPlayers == 5 && activeRoomMap == BattleMapCatalog.GlobalConquestName)
        {
            selectedMode = GlobalConquestMode;
            selectedMap = BattleMapCatalog.GlobalConquestName;
            GameState.Instance?.SelectMap(selectedMap, selectedMode);
            _ = OpenGlobalConquestTeamPanel();
            await RefreshRooms();
            ShowToast("已加入全球争霸组队房间");
            return;
        }

        selectedMode = CustomRoomMode;
        selectedMap = BattleMapCatalog.IsKnownMap(activeRoomMap) ? activeRoomMap : BattleMapCatalog.DefaultMapName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);
        if (activeRoomMaxPlayers > 2 && !IsCustomRoomModalContext())
            OpenRoomPanel();
        await RefreshRooms();
        ShowCreatedRoomActions();
        if (activeRoomMaxPlayers <= 2)
        {
            CloseModal();
            await EnterRoomBattle(activeRoomId, activeRoomMap, activeRoomMaxPlayers);
            return;
        }

        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = $"已加入 {activeRoomPlayerCount}/{activeRoomMaxPlayers} 房间，可继续邀请成员；当前最多支持 3v3 组房，实时开战入口仍先按 1v1 接入。";
        ShowToast("宸插姞鍏ユ埧闂?");
    }

    async Task LeaveActiveRoom()
    {
        if (string.IsNullOrWhiteSpace(activeRoomId))
        {
            ClearActiveRoomState();
            return;
        }

        var roomId = activeRoomId;
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "离开房间中...";
        if (NetClient.Instance is not null)
            await NetClient.Instance.LeaveRoom(roomId);

        ClearActiveRoomState();
        await RefreshRooms();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "已离开房间，可以重新选择地图和人数。";
        ShowToast("已离开房间");
    }

    void ClearActiveRoomState()
    {
        activeRoomId = "";
        activeRoomMap = "";
        activeRoomMaxPlayers = 2;
        activeRoomPlayerCount = 1;
        GameState.Instance?.ClearCurrentRoom();

        if (activeRoomActionsPanel is not null && GodotObject.IsInstanceValid(activeRoomActionsPanel))
            activeRoomActionsPanel.QueueFree();
        activeRoomActionsPanel = null;
    }

    async Task EnterRoomBattle(string roomId, string mapName, int maxPlayers = 2)
    {
        if (mapName == BattleMapCatalog.GlobalConquestName)
        {
            selectedMode = GlobalConquestMode;
            selectedMap = BattleMapCatalog.GlobalConquestName;
            GameState.Instance?.SelectMap(selectedMap, selectedMode);
            GameState.Instance?.SetCurrentRoom(roomId);
            await StartBattle(false);
            return;
        }

        if (!SupportsRealtimeRoomBattle(maxPlayers))
        {
            ShowToast("当前房间先用于 3v3 组队邀请，实时开战入口仍先按 1v1 接入。");
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = $"已加入 {Math.Max(2, maxPlayers)} 人房间，可继续邀请成员并集合；当前实时开战入口仍先按 1v1 接入。";
            return;
        }

        selectedMode = CustomRoomMode;
        selectedMap = BattleMapCatalog.NormalizeCustomRoomMap(mapName);
        GameState.Instance?.SelectMap(selectedMap, selectedMode);
        GameState.Instance?.SetCurrentRoom(roomId);
        await StartBattle(false);
    }

    void ShowInvitePanel(string roomId)
    {
        modalTitle.Text = "邀请好友";
        ClearChildren(modalBody);
        modalBody.AddChild(AddLabel(string.IsNullOrEmpty(roomId) ? "当前没有可邀请的房间。" : $"房间编号：{roomId}", 13, MutedText, HorizontalAlignment.Left));
        if (string.IsNullOrEmpty(roomId))
        {
            ShowModal();
            return;
        }

        if (loadedFriendNames.Count == 0)
        {
            modalBody.AddChild(AddLabel("暂无可邀请好友，先在左侧添加好友或刷新列表。", 14, MutedText, HorizontalAlignment.Left));
            modalBody.AddChild(AddButton("刷新好友", () => _ = RefreshFriends(), ButtonTone.Secondary, 13));
        }
        else
        {
            foreach (var friendName in loadedFriendNames)
            {
                var row = new HBoxContainer { Name = "Invite_" + friendName };
                row.AddThemeConstantOverride("separation", 8);
                var label = AddLabel(friendName, 15, PanelText, HorizontalAlignment.Left);
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(label);
                row.AddChild(AddButton("发送邀请", () => _ = SendInvite(friendName, roomId), ButtonTone.Gold, 13));
                modalBody.AddChild(row);
            }
        }
        ShowModal();
    }

    async Task SendInvite(string friendName, string roomId)
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.InviteFriend(friendName, roomId);
        ShowToast(data.GetBool("success")
            ? $"已邀请 {friendName}"
            : "邀请失败：" + FriendlyText(data.GetString("error"), "好友不在线或房间不可用"));
    }

    async Task PollInvites()
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.GetInvites();
        if (!data.GetBool("success"))
            return;
        var invites = data.GetArray("invites");
        foreach (var item in invites)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;

            var invite = item.AsGodotDictionary();
            var inviteId = invite.GetString("id");
            if (string.IsNullOrEmpty(inviteId) || surfacedInviteIds.Contains(inviteId))
                continue;

            surfacedInviteIds.Add(inviteId);
            ShowIncomingInvite(invite);
            return;
        }
    }

    void ShowIncomingInvite(Godot.Collections.Dictionary invite)
    {
        modalTitle.Text = "房间邀请";
        ClearChildren(modalBody);
        var inviteId = invite.GetString("id");
        if (!string.IsNullOrEmpty(inviteId))
            surfacedInviteIds.Add(inviteId);
        var roomId = invite.GetString("roomId");
        var mapName = FriendlyText(invite.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var maxPlayers = invite.GetInt("maxPlayers", 2);
        var playerCount = invite.GetInt("playerCount", 0);
        var roomSummary = $"{Math.Max(0, playerCount)}/{Math.Max(2, maxPlayers)}";
        modalBody.AddChild(AddLabel($"{FriendlyText(invite.GetString("from"), "好友")} 邀请你加入 {mapName}  {roomSummary}", 17, PanelText, HorizontalAlignment.Left));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(AddButton("接受", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 14));
        row.AddChild(AddButton("拒绝", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 14));
        modalBody.AddChild(row);
        ShowModal();
    }

    async Task RespondInvite(string inviteId, bool accept, string fallbackRoomId, string fallbackMap)
    {
        if (inviteId.StartsWith("mock_"))
        {
            ShowToast(accept ? $"已接受来自 IronWolf 的邀请，正在进入房间 {fallbackRoomId}..." : "已拒绝邀请");
            CloseModal();
            if (accept)
            {
                activeRoomId = fallbackRoomId;
                selectedMap = fallbackMap;
                selectedMode = CustomRoomMode;
                await StartBattle(false);
            }
            return;
        }

        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.RespondInvite(inviteId, accept);
        invitesCacheAtMs = 0;
        if (!data.GetBool("success"))
        {
            ShowToast("邀请已失效");
            CloseModal();
            return;
        }
        if (!accept)
        {
            ShowToast("已拒绝邀请");
            CloseModal();
            return;
        }

        await JoinRoom(data.GetString("roomId", fallbackRoomId));
    }

    void ShowShopModal()
    {
        OpenFeatureModal("军需商店", "补给、加速与全球争霸军备包会在这里统一购买。");
        AddModalSummaryRow(
            AddStatCard("金币", $"{GameState.Instance?.Gold ?? 0}", "常规资源", WarningText),
            AddStatCard("钻石", $"{GameState.Instance?.Gems ?? 0}", "高级货币", new Color(0.64f, 0.90f, 1f)),
            AddStatCard("今日特惠", "4 件", "可购买商品", GoodText));

        modalBody.AddChild(CreateShopHeroBanner());

        var list = AddModalSectionPanel("军需精选", "统一购买补给、加速道具和全球争霸军备包。", WarningText);

        var grid = new GridContainer
        {
            Name = "ShopGrid",
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        list.AddChild(grid);

        foreach (var offer in BuildShopOffers())
            grid.AddChild(CreateShopItemCard(offer));
        ShowModal();
    }

    Control CreateShopHeroBanner()
    {
        var panel = new Panel
        {
            Name = "ShopHeroBanner",
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(panel, MakeModeCardPalette(new Color(0.92f, 0.74f, 0.28f, 0.98f)), 1, 10, 5);
        AddTopAccentStripe(panel, new Color(0.98f, 0.82f, 0.34f, 0.90f), 4);

        var backdrop = new TextureRect
        {
            Name = "ShopHeroBackdrop",
            Texture = LoadTexture(TechBackgroundRoot + "tech_bg_firepower_barrage.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.22f)
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.AddChild(backdrop);

        var dim = new ColorRect
        {
            Name = "ShopHeroDim",
            Color = new Color(0.04f, 0.05f, 0.05f, 0.34f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        panel.AddChild(dim);

        var content = AddHBox(panel, "ShopHeroContent", 12);
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = 14;
        content.OffsetTop = 8;
        content.OffsetRight = -14;
        content.OffsetBottom = -8;

        var left = new VBoxContainer
        {
            Name = "ShopHeroLeft",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        left.AddThemeConstantOverride("separation", 4);
        content.AddChild(left);

        var titleRow = AddHBox(left, "ShopHeroTitleRow", 8);
        titleRow.AddChild(CreateTag("今日军需", new Color(0.34f, 0.24f, 0.08f, 0.92f), new Color(1f, 0.95f, 0.82f)));

        var title = AddLabel("军备补给已整理入库", 16, PanelText, HorizontalAlignment.Left);
        titleRow.AddChild(title);

        var note = AddLabel("限时特惠供应，助力指挥官快速建造与科研升级。", 12, new Color(0.86f, 0.90f, 0.92f), HorizontalAlignment.Left);
        left.AddChild(note);

        var right = new HBoxContainer
        {
            Name = "ShopHeroRight",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        right.AddThemeConstantOverride("separation", 8);
        content.AddChild(right);

        right.AddChild(CreateTag("4 项在售", new Color(0.16f, 0.34f, 0.24f, 0.92f), new Color(0.94f, 0.99f, 0.95f)));
        right.AddChild(new TextureRect
        {
            Texture = LoadTexture(KenneyGameIconRoot + "shoppingCart.png"),
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 0.93f, 0.72f, 0.84f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });

        return panel;
    }

    Panel CreateHeroCurrencyChip(string title, string value, string iconPath, Color accent)
    {
        var isGemChip = IsGemCurrencyIcon(iconPath);
        var chip = new Panel
        {
            Name = "HeroCurrency_" + title,
            CustomMinimumSize = new Vector2(isGemChip ? 116 : 118, isGemChip ? 64 : 72),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.07f, 0.52f),
            BorderColor = new Color(accent.R, accent.G, accent.B, 0.45f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });

        var row = AddHBox(chip, "HeroCurrencyRow_" + title, isGemChip ? 6 : 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 10;
        row.OffsetTop = 10;
        row.OffsetRight = -10;
        row.OffsetBottom = -10;

        row.AddChild(new TextureRect
        {
            Texture = LoadCurrencyIconTexture(iconPath),
            CustomMinimumSize = new Vector2(isGemChip ? 30 : 26, isGemChip ? 20 : 26),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = isGemChip
                ? TextureRect.StretchModeEnum.Scale
                : TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });

        var info = new VBoxContainer
        {
            Name = "HeroCurrencyInfo_" + title,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        info.AddThemeConstantOverride("separation", 2);
        row.AddChild(info);

        info.AddChild(AddLabel(title, 11, MutedText, HorizontalAlignment.Left));
        info.AddChild(AddLabel(value, 18, accent, HorizontalAlignment.Left));
        return chip;
    }

    IEnumerable<ShopOfferUi> BuildShopOffers()
    {
        yield return new ShopOfferUi
        {
            Name = "战地补给箱",
            Subtitle = "前线资源补给",
            Description = "获得 500 金币与少量指挥经验，适合快速补足开局资源。",
            PriceText = "300",
            ActionText = "购买",
            Category = "补给",
            Badge = "热销",
            IconPath = UnityLobbyRoot + "gen_icon_task_crate.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_repair_workshop.png",
            Accent = new Color(0.90f, 0.72f, 0.28f, 1f),
            UsesGems = false,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "500 金币", "指挥经验" }
        };
        yield return new ShopOfferUi
        {
            Name = "高级建造许可",
            Subtitle = "工程体系提速",
            Description = "下一场战斗建造速度提升 10%，主基地展开与前线补点都会更顺。",
            PriceText = "30",
            ActionText = "购买",
            Category = "建造",
            Badge = "加速",
            IconPath = UnityLobbyRoot + "gen_icon_helmet.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_hold_bunker.png",
            Accent = new Color(0.50f, 0.84f, 0.58f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Primary,
            Highlights = new[] { "建造 +10%", "单局生效" }
        };
        yield return new ShopOfferUi
        {
            Name = "科技速研件",
            Subtitle = "实验室即刻加速",
            Description = "科技研究立即缩短 10 分钟，适合在关键升级卡住时直接推进。",
            PriceText = "25",
            ActionText = "加速",
            Category = "科技",
            Badge = "即刻",
            IconPath = UnityLobbyRoot + "gen_icon_tech_blueprint.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_speed_wings.png",
            Accent = new Color(0.56f, 0.80f, 1f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Primary,
            Highlights = new[] { "研究 -10 分钟", "即时结算" }
        };
        yield return new ShopOfferUi
        {
            Name = "全球争霸军备包",
            Subtitle = "赛季军备整备",
            Description = "随机军备蓝图、通行证积分与稀有材料，用于全球争霸的主力补强。",
            PriceText = "80",
            ActionText = "购买",
            Category = "军备",
            Badge = "精选",
            IconPath = UnityLobbyRoot + "gen_icon_tank.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_firepower_barrage.png",
            Accent = new Color(0.98f, 0.56f, 0.30f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "蓝图", "通行证积分", "稀有材料" }
        };
        yield return new ShopOfferUi
        {
            Name = "老兵训练手册",
            Subtitle = "步兵训练档案",
            Description = "步兵单位初始经验提升，让基础兵种更快形成有效战斗力。",
            PriceText = "450",
            ActionText = "购买",
            Category = "训练",
            Badge = "步兵",
            IconPath = UnityLobbyRoot + "gen_icon_helmet.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_armor_plate.png",
            Accent = new Color(0.86f, 0.76f, 0.50f, 1f),
            UsesGems = false,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "步兵经验", "常驻加成" }
        };
    }

    Control CreateShopItemCard(ShopOfferUi offer)
    {
        var card = new Panel
        {
            Name = "ShopItem_" + offer.Name,
            CustomMinimumSize = new Vector2(280, 275), // Width 280, Height 275
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true
        };
        MetalUiStyle.ApplyMetalPanel(card, MakeModeCardPalette(offer.Accent), 1, 8, 5);
        AddTopAccentStripe(card, new Color(offer.Accent.R, offer.Accent.G, offer.Accent.B, 0.82f), 3);

        var backdrop = new TextureRect
        {
            Name = "ShopItemBackdrop_" + offer.Name,
            Texture = LoadTexture(offer.BackdropPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.24f)
        };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        card.AddChild(backdrop);

        var frame = new TextureRect
        {
            Name = "ShopItemFrame_" + offer.Name,
            Texture = LoadTexture(UnityLobbyRoot + "gen_card_frame.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.36f)
        };
        frame.SetAnchorsPreset(LayoutPreset.FullRect);
        card.AddChild(frame);

        var shade = new ColorRect
        {
            Name = "ShopItemShade_" + offer.Name,
            Color = new Color(0.03f, 0.04f, 0.04f, 0.42f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        shade.SetAnchorsPreset(LayoutPreset.FullRect);
        card.AddChild(shade);

        var margin = new MarginContainer
        {
            Name = "ShopItemMargin_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        card.AddChild(margin);

        var content = new VBoxContainer
        {
            Name = "ShopItemContent_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 6);
        margin.AddChild(content);

        // 1. 上面一个大图标 (Top: Big Icon/Preview Block)
        var preview = CreateShopPreviewBlock(offer);
        preview.CustomMinimumSize = new Vector2(256, 110);
        content.AddChild(preview);

        // 2. 下面一行介绍 (Middle: Title & Description)
        var title = AddLabel(offer.Name, 14, PanelText, HorizontalAlignment.Center);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(title);

        var detail = AddLabel(offer.Description, 10, MutedText, HorizontalAlignment.Center);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.CustomMinimumSize = new Vector2(0, 32);
        content.AddChild(detail);

        // 3. 标签就是分类 (Middle: Tags for Category and Badge)
        var meta = new HFlowContainer
        {
            Name = "ShopItemMeta_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 22)
        };
        meta.AddThemeConstantOverride("h_separation", 6);
        meta.AddChild(CreateTag(offer.Category, new Color(0.20f, 0.28f, 0.36f, 0.92f), new Color(0.94f, 0.97f, 0.99f)));
        if (!string.IsNullOrWhiteSpace(offer.Badge))
            meta.AddChild(CreateTag(offer.Badge, new Color(0.34f, 0.24f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)));
        content.AddChild(meta);

        // 4. 再下面是价格按钮，点击价格进入购买页
        var priceBtn = CreateShopPriceButton(offer);
        priceBtn.CustomMinimumSize = new Vector2(160, 32);
        priceBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        priceBtn.Pressed += () => ShowToast($"{offer.Name} 的购买接口待接入");
        content.AddChild(priceBtn);

        return card;
    }

    Control CreateShopPreviewBlock(ShopOfferUi offer)
    {
        var holder = new Panel
        {
            Name = "ShopPreview_" + offer.Name,
            CustomMinimumSize = new Vector2(256, 110),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        holder.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.02f, 0.03f, 0.03f, 0.44f),
            BorderColor = new Color(offer.Accent.R, offer.Accent.G, offer.Accent.B, 0.42f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });

        var back = new TextureRect
        {
            Name = "ShopPreviewBg_" + offer.Name,
            Texture = LoadTexture(offer.BackdropPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.90f)
        };
        back.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(back);

        var gloss = new ColorRect
        {
            Name = "ShopPreviewGloss_" + offer.Name,
            Color = new Color(0.03f, 0.04f, 0.04f, 0.16f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        gloss.SetAnchorsPreset(LayoutPreset.FullRect);
        holder.AddChild(gloss);

        var iconFrame = new Panel
        {
            Name = "ShopPreviewIconFrame_" + offer.Name,
            CustomMinimumSize = new Vector2(54, 54),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        iconFrame.SetAnchorsPreset(LayoutPreset.Center);
        iconFrame.GrowHorizontal = GrowDirection.Both;
        iconFrame.GrowVertical = GrowDirection.Both;
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.07f, 0.60f),
            BorderColor = new Color(1f, 1f, 1f, 0.18f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        });
        holder.AddChild(iconFrame);

        var icon = new TextureRect
        {
            Name = "ShopPreviewIcon_" + offer.Name,
            Texture = LoadTexture(offer.IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = new Color(1f, 1f, 1f, 0.96f)
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = 10;
        icon.OffsetTop = 10;
        icon.OffsetRight = -10;
        icon.OffsetBottom = -10;
        iconFrame.AddChild(icon);
        return holder;
    }

    Button CreateShopPriceButton(ShopOfferUi offer)
    {
        var priceBtn = new Button
        {
            Name = "ShopPriceButton_" + offer.Name,
            CustomMinimumSize = new Vector2(160, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };

        var normalStyle = new StyleBoxFlat
        {
            BgColor = offer.UsesGems
                ? new Color(0.08f, 0.18f, 0.24f, 0.90f)
                : new Color(0.30f, 0.22f, 0.08f, 0.92f),
            BorderColor = new Color(1f, 1f, 1f, 0.12f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16
        };

        var hoverStyle = (StyleBoxFlat)normalStyle.Duplicate(true);
        hoverStyle.BgColor = offer.UsesGems
            ? new Color(0.12f, 0.24f, 0.32f, 0.95f)
            : new Color(0.38f, 0.28f, 0.12f, 0.95f);
        hoverStyle.BorderColor = new Color(offer.Accent.R, offer.Accent.G, offer.Accent.B, 0.5f);

        var pressedStyle = (StyleBoxFlat)normalStyle.Duplicate(true);
        pressedStyle.BgColor = offer.UsesGems
            ? new Color(0.05f, 0.12f, 0.18f, 0.95f)
            : new Color(0.22f, 0.16f, 0.05f, 0.95f);

        priceBtn.AddThemeStyleboxOverride("normal", normalStyle);
        priceBtn.AddThemeStyleboxOverride("hover", hoverStyle);
        priceBtn.AddThemeStyleboxOverride("pressed", pressedStyle);
        priceBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        var margin = new MarginContainer
        {
            Name = "ShopPriceButtonMargin_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        priceBtn.AddChild(margin);

        var row = new HBoxContainer
        {
            Name = "ShopPriceButtonRow_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);

        row.AddChild(new TextureRect
        {
            Texture = LoadCurrencyIconTexture(offer.UsesGems ? CurrencyIconRoot + "currency_gem_blue.png" : CurrencyIconRoot + "currency_gold_coin.png"),
            CustomMinimumSize = new Vector2(offer.UsesGems ? 22 : 20, offer.UsesGems ? 16 : 20),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = offer.UsesGems
                ? TextureRect.StretchModeEnum.Scale
                : TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });

        var amount = AddLabel(offer.PriceText, 14, new Color(1f, 0.98f, 0.90f), HorizontalAlignment.Left);
        amount.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        amount.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(amount);

        return priceBtn;
    }

    void ShowResearchModal()
    {
        OpenFeatureModal("科技研究中心", "升级主基地战略科研，为战场部署获取更强的机动、火力、装甲或维修加成。");
        
        int maxLvl = 1;
        if (techLevels.Values.Count > 0)
        {
            foreach (var v in techLevels.Values)
            {
                if (v > maxLvl) maxLvl = v;
            }
        }

        AddModalSummaryRow(
            AddStatCard("科技总数", "4 项", "基地战术支援科技", WarningText),
            AddStatCard("最高等级", $"{maxLvl} 级", "当前最高科研水平", GoodText),
            AddStatCard("研究状态", techEndAtMs > NowMs() ? "进行中" : "闲置", techEndAtMs > NowMs() ? "服务器计时中" : "可开始新研究", techEndAtMs > NowMs() ? WarningText : MutedText));

        var splitBox = new HBoxContainer
        {
            Name = "ResearchSplitBox",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        splitBox.AddThemeConstantOverride("separation", 14);
        modalBody.AddChild(splitBox);

        var leftCol = new VBoxContainer
        {
            Name = "ResearchLeftCol",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        splitBox.AddChild(leftCol);

        currentResearchDetailPanel = new Panel
        {
            Name = "ResearchDetailPanel",
            CustomMinimumSize = new Vector2(230, 240),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(currentResearchDetailPanel, WarehousePanel, 1, 8, 4);
        splitBox.AddChild(currentResearchDetailPanel);

        var techList = AddWarehouseSectionPanel(leftCol, "科技列表", "点击选择科技项以查阅具体战术加成并启动升级项目", new Color(0.64f, 0.90f, 1f));
        foreach (var tech in BattleTechCatalog.GetForBuilding("main_base"))
        {
            techList.AddChild(CreateResearchGridCell(tech));
        }

        UpdateResearchDetail(selectedResearchTechKey);
        ShowModal();
    }

    Control CreateResearchGridCell(BattleTechDefinition tech)
    {
        var level = techLevels.TryGetValue(tech.Key, out var lv) ? lv : 1;
        var isSelected = tech.Key == selectedResearchTechKey;

        // 外部面板包装 (76 x 98)
        var cell = new Panel
        {
            Name = "ResearchGridCell_" + tech.Key,
            CustomMinimumSize = new Vector2(76, 98),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };

        // 垂直布局容器
        var vbox = new VBoxContainer
        {
            Name = "VBox_" + tech.Key,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        vbox.SetAnchorsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        cell.AddChild(vbox);

        // 科技图标面板 (72 x 72)
        var iconPanel = new PanelContainer
        {
            Name = "IconPanel_" + tech.Key,
            CustomMinimumSize = new Vector2(72, 72),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };

        var borderCol = isSelected ? new Color(0.95f, 0.76f, 0.24f, 0.90f) : new Color(1f, 1f, 1f, 0.12f);
        MetalUiStyle.ApplyMetalPanel(iconPanel, WarehousePanel, isSelected ? 2 : 1, 6, 4);
        if (isSelected)
        {
            AddTopAccentStripe(iconPanel, new Color(0.95f, 0.76f, 0.24f, 0.90f), 2);
        }
        vbox.AddChild(iconPanel);

        // 图标内间距与贴图
        var margin = new MarginContainer
        {
            Name = "Margin_" + tech.Key,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        iconPanel.AddChild(margin);

        if (!string.IsNullOrEmpty(tech.BackgroundTexturePath))
        {
            margin.AddChild(new TextureRect
            {
                Texture = LoadTexture(tech.BackgroundTexturePath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = tech.Tint.Lerp(Colors.White, 0.15f)
            });
        }

        // 图标下方等级文本
        var lvlLabel = new Label
        {
            Name = "LvlLabel_" + tech.Key,
            Text = $"{level}/5 级",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        lvlLabel.AddThemeFontSizeOverride("font_size", 11);
        lvlLabel.AddThemeColorOverride("font_color", isSelected ? new Color(0.95f, 0.76f, 0.24f) : MutedText);
        vbox.AddChild(lvlLabel);

        // 全覆点击按钮
        var clickBtn = new Button
        {
            Name = "Click_" + tech.Key,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Position = Vector2.Zero
        };
        clickBtn.SetAnchorsPreset(LayoutPreset.FullRect);
        clickBtn.Pressed += () =>
        {
            selectedResearchTechKey = tech.Key;
            ShowResearchModal();
        };
        cell.AddChild(clickBtn);

        return cell;
    }

    void UpdateResearchDetail(string techKey)
    {
        if (currentResearchDetailPanel is null)
            return;

        ClearChildren(currentResearchDetailPanel);

        BattleTechDefinition tech = default;
        foreach (var t in BattleTechCatalog.GetForBuilding("main_base"))
        {
            if (t.Key == techKey)
            {
                tech = t;
                break;
            }
        }
        if (tech.Key is null)
            return;

        var level = techLevels.TryGetValue(techKey, out var lv) ? lv : 1;
        var box = AddVBox(currentResearchDetailPanel, "DetailBox", 6, new Vector2(12, 10), new Vector2(-12, -10));

        box.AddChild(AddLabel(tech.DisplayName, 16, PanelText, HorizontalAlignment.Center));
        box.AddChild(CreateTag($"当前等级: Lv.{level} (最高 5)", new Color(0.18f, 0.34f, 0.40f, 0.92f), new Color(0.94f, 0.98f, 0.99f)));

        var space = new Control { CustomMinimumSize = new Vector2(0, 4) };
        box.AddChild(space);

        box.AddChild(AddLabel("科技描述", 12, MutedText, HorizontalAlignment.Left));

        var scroll = new ScrollContainer
        {
            Name = "DetailDescScroll",
            CustomMinimumSize = new Vector2(0, 100),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddChild(scroll);

        var content = new VBoxContainer
        {
            Name = "DetailContent",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(content);

        var descLabel = AddLabel(tech.Description, 12, PanelText, HorizontalAlignment.Left);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(descLabel);

        var bonusLabel = AddLabel($"战略加成: {DescribeTechBonus(tech)}", 12, WarningText, HorizontalAlignment.Left);
        bonusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bonusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(bonusLabel);

        var specLabel = AddLabel($"作用范围: {tech.Radius}m\n持续时长: {tech.Duration}s\n整备冷却: {tech.Cooldown}s", 11, MutedText, HorizontalAlignment.Left);
        specLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(specLabel);

        var space2 = new Control { CustomMinimumSize = new Vector2(0, 4) };
        box.AddChild(space2);

        string serverTechKey = techKey switch
        {
            "speed" => "speed_boost_1",
            "armor" => "tank_armor_1",
            "firepower" => "tank_attack_1",
            "repair" => "artillery_reload_1",
            _ => techKey
        };

        if (techEndAtMs > NowMs())
        {
            box.AddChild(AddLabel("其他科研正在计时中", 12, MutedText, HorizontalAlignment.Center));
        }
        else
        {
            var upgradeBtn = AddButton("启动升级项目", () => StartResearchProject(techKey, serverTechKey), ButtonTone.Gold, 13);
            upgradeBtn.CustomMinimumSize = new Vector2(0, 32);
            upgradeBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            box.AddChild(upgradeBtn);
        }
    }

    void StartResearchProject(string techKey, string serverTechKey)
    {
        if (techLevels.ContainsKey(techKey))
        {
            techLevels[techKey]++;
        }

        _ = StartTechResearch();
        ShowResearchModal();
        
        string name = "科技";
        foreach (var t in BattleTechCatalog.GetForBuilding("main_base"))
        {
            if (t.Key == techKey)
            {
                name = t.DisplayName;
                break;
            }
        }
        ShowToast($"已成功启动 {name} 升级项目！");
    }

    void ShowWarehouseModal(string activeTab = "blueprints")
    {
        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        OpenFeatureModal("仓库", "查看已拥有资源、军备蓝图、科技加成与全球争霸军备配置。");
        AddModalSummaryRow(
            AddStatCard("金币库存", $"{GameState.Instance?.Gold ?? 0}", "可用于建造与商品", WarningText),
            AddStatCard("钻石库存", $"{GameState.Instance?.Gems ?? 0}", "可用于加速与礼包", new Color(0.64f, 0.90f, 1f)),
            AddStatCard("当前全球争霸阵营", currentFaction.DisplayName, $"起始单位：{currentStarter.DisplayName}", GoodText));
        modalBody.AddChild(CreateWarehouseTabBar(activeTab));

        var splitBox = new HBoxContainer
        {
            Name = "WarehouseSplitBox",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        splitBox.AddThemeConstantOverride("separation", 14);
        modalBody.AddChild(splitBox);

        var leftCol = new VBoxContainer
        {
            Name = "WarehouseLeftCol",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        splitBox.AddChild(leftCol);

        currentWarehouseDetailPanel = new Panel
        {
            Name = "WarehouseDetailPanel",
            CustomMinimumSize = new Vector2(210, 240),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(currentWarehouseDetailPanel, WarehousePanel, 1, 8, 4);
        splitBox.AddChild(currentWarehouseDetailPanel);

        // Pre-fill details panel
        UpdateWarehouseDetail("选定项目", "在左侧网格中选择任意军备蓝图、科技加成或道具卡片，以在此处查看其详细性能规格、加成时间、激活条件或库存数量。", new Color(0.72f, 0.84f, 0.96f), "提示", "", "");

        switch (activeTab)
        {
            case "tech":
            {
                var techList = AddWarehouseSectionPanel(leftCol, "科技加成", "查看主基地科技的范围效果、持续时间和冷却信息。", new Color(0.64f, 0.90f, 1f));
                foreach (var tech in BattleTechCatalog.GetForBuilding("main_base"))
                    techList.AddChild(CreateWarehouseTechCard(tech));
                break;
            }
            case "armament":
            {
                var armamentList = AddWarehouseSectionPanel(leftCol, "全球争霸阵营", "查看可切换的开局阵营，以及对应的起始主力和定位。", GoodText);
                foreach (var faction in BattleUnitCatalog.GlobalConquestFactions)
                    armamentList.AddChild(CreateWarehouseArmamentCard(faction, currentFaction.Key == faction.Key));
                break;
            }

            default:
            {
                var list = AddWarehouseSectionPanel(leftCol, "军备蓝图", "查看已收录的可生产单位，以及当前解锁条件。", WarningText);
                foreach (var unit in BattleUnitCatalog.PlayerRoster)
                    list.AddChild(CreateWarehouseBlueprintCard(unit));
                break;
            }
        }
        ShowModal();
    }

    Control CreateWarehouseTabBar(string activeTab)
    {
        var panel = new PanelContainer
        {
            Name = "WarehouseTabBar",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, WarehousePanel, 1, 6, 4);
        AddTopAccentStripe(panel, new Color(0.92f, 0.78f, 0.34f, 0.60f), 2);

        var margin = new MarginContainer
        {
            Name = "WarehouseTabBarMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer
        {
            Name = "WarehouseTabBarBox",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 6);
        margin.AddChild(box);

        var helper = AddLabel("点击页签切换蓝图、科技、争霸军备和道具内容。", 11, new Color(0.86f, 0.90f, 0.93f, 0.96f), HorizontalAlignment.Left);
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(helper);

        var row = new HFlowContainer
        {
            Name = "WarehouseTabBarRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("h_separation", 8);
        row.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(row);

        row.AddChild(CreateWarehouseTabButton("军备蓝图", "blueprints", activeTab));
        row.AddChild(CreateWarehouseTabButton("科技加成", "tech", activeTab));
        row.AddChild(CreateWarehouseTabButton("争霸军备", "armament", activeTab));

        return panel;
    }

    Button CreateWarehouseTabButton(string text, string tabKey, string activeTab)
    {
        var button = AddButton(text, () => ShowWarehouseModal(tabKey), tabKey == activeTab ? ButtonTone.Gold : ButtonTone.Secondary, 12);
        button.CustomMinimumSize = new Vector2(136, 36);
        button.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        button.ClipText = true;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        return button;
    }

    GridContainer AddWarehouseSectionPanel(Control parent, string title, string note, Color accent)
    {
        var panel = new PanelContainer
        {
            Name = "WarehouseSection_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, WarehousePanel, 1, 8, 4);
        AddTopAccentStripe(panel, new Color(accent.R, accent.G, accent.B, 0.76f), 3);
        parent.AddChild(panel);

        var margin = new MarginContainer
        {
            Name = panel.Name + "_Margin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var box = new VBoxContainer
        {
            Name = "WarehouseSectionBox_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);
        box.AddChild(CreateWarehouseSectionHeader(title, note, accent));

        var grid = new GridContainer
        {
            Name = "WarehouseSectionGrid_" + title,
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(grid);

        return grid;
    }

    Control CreateWarehouseSectionHeader(string title, string note, Color accent)
    {
        var holder = new VBoxContainer
        {
            Name = "WarehouseSectionHeader_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        holder.AddThemeConstantOverride("separation", 2);

        var titleLabel = AddLabel(title, 16, accent, HorizontalAlignment.Left);
        holder.AddChild(titleLabel);

        var noteLabel = AddLabel(note, 12, new Color(0.84f, 0.88f, 0.90f, 0.96f), HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        holder.AddChild(noteLabel);
        return holder;
    }

    Control CreateWarehouseBlueprintCard(BattleUnitDefinition unit)
    {
        var status = WarehouseUnitStatus(unit.Key);
        var unlocked = status.StartsWith("已", StringComparison.Ordinal);
        var accent = unlocked ? new Color(0.50f, 0.90f, 0.58f, 0.88f) : WarningText;
        var iconPath = ResolveUnitIconPath(unit.Key);
        return CreateWarehouseGridCell(
            unit.DisplayName,
            iconPath,
            "",
            DescribeWarehouseBlueprint(unit.Key),
            accent,
            UnitCategory(unit.Key),
            $"费用 {unit.GoldCost} / 人口 {unit.PopCost}",
            unlocked ? "已收录" : "待解锁"
        );
    }

    Control CreateWarehouseTechCard(BattleTechDefinition tech)
    {
        return CreateWarehouseGridCell(
            tech.DisplayName,
            tech.BackgroundTexturePath,
            "",
            $"{DescribeTechBonus(tech)}",
            tech.Tint,
            "主基地",
            $"持续 {tech.Duration:0}s / 冷却 {tech.Cooldown:0}s",
            "已激活"
        );
    }

    Control CreateWarehouseArmamentCard(GlobalConquestFactionDefinition faction, bool isCurrent)
    {
        var starter = BattleUnitCatalog.Get(faction.StarterUnitKey);
        var iconPath = ResolveUnitIconPath(faction.StarterUnitKey);
        return CreateWarehouseGridCell(
            faction.DisplayName,
            iconPath,
            "",
            faction.Description,
            isCurrent ? GoodText : faction.Tint,
            UnitCategory(starter.Key),
            $"主力 {starter.DisplayName}",
            isCurrent ? "当前默认" : "可切换"
        );
    }

    Control CreateWarehouseInventoryCard(string name, string type, string count, string description)
    {
        var iconPath = "";
        return CreateWarehouseGridCell(
            name,
            iconPath,
            count,
            description,
            new Color(0.72f, 0.84f, 0.96f, 0.82f),
            type,
            $"存量 {count}",
            "常驻可用"
        );
    }

    Control CreateWarehouseGridCell(string title, string iconPath, string quantity, string description, Color accent, string category, string stats, string status)
    {
        var button = new Button
        {
            Name = "GridCell_" + title,
            CustomMinimumSize = new Vector2(76, 76),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            TooltipText = $"{title}\n{description}"
        };

        button.AddThemeStyleboxOverride("normal", WarehouseGridCellNormal);
        button.AddThemeStyleboxOverride("hover", WarehouseGridCellHover);
        button.AddThemeStyleboxOverride("pressed", WarehouseGridCellPressed);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        // Margin container to constrain content inside the grid cell
        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        button.AddChild(margin);

        // Display the item name centered inside the card
        var nameLabel = new Label
        {
            Name = "TitleLabel",
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        nameLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.93f, 0.95f));
        margin.AddChild(nameLabel);

        button.Pressed += () =>
        {
            UpdateWarehouseDetail(title, description, accent, category, stats, status);
        };

        return button;
    }

    void UpdateWarehouseDetail(string title, string description, Color accent, string category, string stats, string status)
    {
        if (currentWarehouseDetailPanel is null || !GodotObject.IsInstanceValid(currentWarehouseDetailPanel))
            return;

        ClearChildren(currentWarehouseDetailPanel);

        AddTopAccentStripe(currentWarehouseDetailPanel, new Color(accent.R, accent.G, accent.B, 0.80f), 3);

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        currentWarehouseDetailPanel.AddChild(margin);

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var titleLabel = AddLabel(title, 16, PanelText, HorizontalAlignment.Left);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(titleLabel);

        var tagRow = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        tagRow.AddThemeConstantOverride("h_separation", 6);
        tagRow.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(tagRow);

        if (!string.IsNullOrEmpty(category))
            tagRow.AddChild(CreateTag(category, new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.94f, 0.97f, 0.99f)));
        if (!string.IsNullOrEmpty(stats))
            tagRow.AddChild(CreateTag(stats, new Color(0.31f, 0.23f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)));
        if (!string.IsNullOrEmpty(status))
        {
            var isGood = status.Contains("已") || status.Contains("当前") || status.Contains("接收") || status.Contains("常驻");
            var bg = isGood ? new Color(0.16f, 0.38f, 0.24f, 0.94f) : new Color(0.44f, 0.22f, 0.10f, 0.94f);
            var fg = isGood ? new Color(0.96f, 0.98f, 0.94f) : new Color(1f, 0.92f, 0.90f);
            tagRow.AddChild(CreateTag(status, bg, fg));
        }

        var divider = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 1),
            Color = new Color(1f, 1f, 1f, 0.15f)
        };
        box.AddChild(divider);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        box.AddChild(scroll);

        var descLabel = AddLabel(description, 12, MutedText, HorizontalAlignment.Left);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(descLabel);
    }

    static string ResolveUnitIconPath(string unitKey)
    {
        const string BackdropRoot = "res://assets/unity_migrated/Assets/Resources/UI/ButtonBackdrops/";
        var filename = unitKey switch
        {
            "infantry" => "prod_infantry_icon.png",
            "infantry_artillery" => "prod_infantry_artillery_icon.png",
            "infantry_flamethrower" or "flamethrower" => "prod_infantry_flamethrower_icon.png",
            "light_tank" or "tank" or "medium_tank" or "heavy_tank" => "prod_tank_icon.png",
            "artillery" => "prod_artillery_icon.png",
            "anti_air_gun" => "prod_anti_air_icon.png",
            "scout_plane" => "prod_scout_plane_icon.png",
            "fighter" => "prod_fighter_icon.png",
            "bomber" => "prod_bomber_icon.png",
            "patrol_boat" => "prod_patrol_boat_icon.png",
            "destroyer_ship" => "prod_destroyer_ship_icon.png",
            "transport_ship" => "prod_battleship_icon.png",
            _ => "prod_infantry_icon.png"
        };
        return BackdropRoot + filename;
    }

    void AddInventoryRow(VBoxContainer list, string name, string type, string count, string description)
    {
        list.AddChild(AddTableRow(
            new[] { name, type, count, description },
            new[] { 170f, 100f, 72f, 236f },
            "",
            null,
            ButtonTone.Secondary));
    }

    void AddTechBonusRow(VBoxContainer list, BattleTechDefinition tech)
    {
        list.AddChild(AddTableRow(
            new[] { tech.DisplayName, "主基地", DescribeTechBonus(tech), $"持续 {tech.Duration:0}s / 冷却 {tech.Cooldown:0}s" },
            new[] { 144f, 92f, 222f, 128f },
            "",
            null,
            ButtonTone.Secondary));
    }

    void AddGlobalArmamentRow(VBoxContainer list, BattleUnitDefinition unit, bool isCurrent)
    {
        list.AddChild(AddTableRow(
            new[] { unit.DisplayName, UnitCategory(unit.Key), isCurrent ? "当前默认" : "可切换", DescribeGlobalConquestStarter(unit.Key) },
            new[] { 132f, 88f, 92f, 272f },
            "",
            null,
            ButtonTone.Secondary,
            isCurrent));
    }

    static string DescribeTechBonus(BattleTechDefinition tech)
    {
        var parts = new List<string>();
        if (!Mathf.IsEqualApprox(tech.MoveMultiplier, 1f))
            parts.Add($"移速 {(tech.MoveMultiplier - 1f) * 100f:+0;-0}%");
        if (!Mathf.IsEqualApprox(tech.DamageMultiplier, 1f))
            parts.Add($"火力 {(tech.DamageMultiplier - 1f) * 100f:+0;-0}%");
        if (!Mathf.IsZeroApprox(tech.AttackRangeBonus))
            parts.Add($"射程 +{tech.AttackRangeBonus:0.#}");
        if (!Mathf.IsEqualApprox(tech.AttackCooldownMultiplier, 1f))
            parts.Add($"攻速 {(1f - tech.AttackCooldownMultiplier) * 100f:+0;-0}%");
        if (!Mathf.IsZeroApprox(tech.DefenseReduction))
            parts.Add($"减伤 {tech.DefenseReduction * 100f:0}%");
        if (!Mathf.IsZeroApprox(tech.VisionBonus))
            parts.Add($"视野 +{tech.VisionBonus:0.#}");
        if (!Mathf.IsZeroApprox(tech.RegenPerSecond))
            parts.Add($"每秒维修 {tech.RegenPerSecond:0.#}");

        return parts.Count == 0 ? "提供范围支援" : string.Join("，", parts);
    }

    async Task ShowLeaderboardModal()
    {
        activeFeatureModal = LeaderboardModalKind;
        if (GameState.Instance?.IsGuest == true)
        {
            RenderLeaderboardModal(new Godot.Collections.Dictionary(), false);
            return;
        }

        if (HasLeaderboardCache())
        {
            RenderLeaderboardModal(cachedLeaderboardData, true);
            _ = RefreshLeaderboardCache();
            return;
        }

        OpenFeatureModal("排行榜", "赛季军功、胜场与军衔排名。");
        modalBody.AddChild(AddLabel("排行榜加载中...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();
        await RefreshLeaderboardCache();
    }

    void AddLeaderboardTableHeader(out VBoxContainer list)
    {
        list = AddModalTable("军功榜", new[] { "排名", "指挥官", "军衔", "战绩", "胜率" }, new[] { 80f, 160f, 116f, 102f, 80f });
    }

    void AddLeaderboardRow(VBoxContainer list, int place, string username, int level, int wins, int losses, string rankTitle, bool isCurrent)
    {
        var total = Math.Max(1, wins + losses);
        var winRate = Mathf.RoundToInt(wins * 100f / total);

        string rankStr = $"#{place}";
        if (place == 1) rankStr = "★ #1 ★";
        else if (place == 2) rankStr = "★ #2 ★";
        else if (place == 3) rankStr = "★ #3 ★";

        list.AddChild(AddTableRow(
            new[] { rankStr, $"{FriendlyText(username, "Commander")}  Lv.{level}", FriendlyText(rankTitle, "列兵"), $"{wins}胜 {losses}败", $"{winRate}%" },
            new[] { 80f, 160f, 116f, 102f, 80f },
            "",
            null,
            ButtonTone.Secondary,
            isCurrent));
    }

    async Task ShowMailModal()
    {
        activeFeatureModal = MailModalKind;

        if (HasInvitesCache())
        {
            RenderMailModal(cachedInvitesData, true);
            _ = RefreshInvitesCache();
            return;
        }

        OpenFeatureModal("邮件 / 邀请", "处理房间邀请、系统公告与战报消息。");
        modalBody.AddChild(AddLabel("正在同步邀请...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();
        await RefreshInvitesCache();
    }

    async Task WarmSecondaryModalCaches()
    {
        await Task.Delay(250);
        _ = RefreshLeaderboardCache();
        _ = RefreshInvitesCache();
    }

    bool HasFriendsCache()
        => friendsCacheAtMs > 0
            && cachedFriendsData.Count > 0
            && (NowMs() - friendsCacheAtMs) <= FriendsCacheTtlSec * 1000.0;

    bool HasLeaderboardCache()
        => leaderboardCacheAtMs > 0 && (NowMs() - leaderboardCacheAtMs) <= SecondaryModalCacheTtlSec * 1000.0;

    bool HasInvitesCache()
        => invitesCacheAtMs > 0 && (NowMs() - invitesCacheAtMs) <= SecondaryModalCacheTtlSec * 1000.0;

    Task RefreshLeaderboardCache()
    {
        if (GameState.Instance?.IsGuest == true)
            return Task.CompletedTask;

        if (leaderboardRefreshTask is not null && !leaderboardRefreshTask.IsCompleted)
            return leaderboardRefreshTask;

        leaderboardRefreshTask = RefreshLeaderboardCacheCore();
        return leaderboardRefreshTask;
    }

    Task RefreshInvitesCache()
    {

        if (invitesRefreshTask is not null && !invitesRefreshTask.IsCompleted)
            return invitesRefreshTask;

        invitesRefreshTask = RefreshInvitesCacheCore();
        return invitesRefreshTask;
    }

    void LoadFriendsCache()
    {
        cachedFriendsData = new();
        friendsCacheAtMs = 0;

        if (!Godot.FileAccess.FileExists(FriendsCacheStorePath))
            return;

        using var file = Godot.FileAccess.Open(FriendsCacheStorePath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            return;

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
            return;

        var root = parsed.AsGodotDictionary();
        var storedAt = root.ContainsKey("cachedAt") ? root["cachedAt"].AsInt64() : 0L;
        var data = root.ContainsKey("data") ? root["data"] : Variant.CreateFrom(new Godot.Collections.Dictionary());
        if (data.VariantType != Variant.Type.Dictionary)
            return;

        cachedFriendsData = (Godot.Collections.Dictionary)data.AsGodotDictionary().Duplicate(true);
        friendsCacheAtMs = storedAt;
    }

    void SaveFriendsCache()
    {
        using var file = Godot.FileAccess.Open(FriendsCacheStorePath, Godot.FileAccess.ModeFlags.Write);
        if (file is null)
            return;

        var root = new Godot.Collections.Dictionary
        {
            ["cachedAt"] = friendsCacheAtMs,
            ["data"] = cachedFriendsData
        };
        file.StoreString(Json.Stringify(root));
    }

    async Task RefreshLeaderboardCacheCore()
    {
        Godot.Collections.Dictionary data = new();
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetLeaderboard(SecondaryModalRequestTimeoutSec);

        if (data.GetBool("success"))
        {
            cachedLeaderboardData = (Godot.Collections.Dictionary)data.Duplicate(true);
            leaderboardCacheAtMs = NowMs();
        }

        if (activeFeatureModal == LeaderboardModalKind && modalPanel.Visible && modalTitle.Text == "排行榜")
            RenderLeaderboardModal(data.GetBool("success") ? data : cachedLeaderboardData, data.GetBool("success") ? false : HasLeaderboardCache());
    }

    async Task RefreshInvitesCacheCore()
    {
        Godot.Collections.Dictionary data = new();
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetInvites(SecondaryModalRequestTimeoutSec);

        if (data.GetBool("success"))
        {
            cachedInvitesData = (Godot.Collections.Dictionary)data.Duplicate(true);
            invitesCacheAtMs = NowMs();
        }

        if (activeFeatureModal == MailModalKind && modalPanel.Visible && modalTitle.Text == "邮件 / 邀请")
            RenderMailModal(data.GetBool("success") ? data : cachedInvitesData, data.GetBool("success") ? false : HasInvitesCache());
    }

    void RenderLeaderboardModal(Godot.Collections.Dictionary data, bool usingCache)
    {
        OpenFeatureModal(
            "排行榜",
            usingCache
                ? "赛季军功、胜场与军衔排名。已显示缓存，正在后台刷新。"
                : "赛季军功、胜场与军衔排名。");

        var rows = data.GetArray("leaderboard");
        if (!data.GetBool("success") || rows.Count == 0)
        {
            AddModalSummaryRow(
                AddStatCard("模式", usingCache ? "缓存数据" : "离线样例", usingCache ? "等待服务器刷新" : "服务器不可用时展示", WarningText),
                AddStatCard("我的胜场", $"{GameState.Instance?.Wins ?? 0}", "本地账号缓存", GoodText),
                AddStatCard("我的军衔", FriendlyText(GameState.Instance?.RankTitle, "列兵"), "当前档案", PanelText));
            AddLeaderboardTableHeader(out var sampleList);
            AddLeaderboardRow(sampleList, 1, GameState.Instance?.Username ?? "Commander", GameState.Instance?.Level ?? 1, GameState.Instance?.Wins ?? 0, GameState.Instance?.Losses ?? 0, GameState.Instance?.RankTitle ?? "列兵", true);
            AddLeaderboardRow(sampleList, 2, "IronWolf", 8, 12, 4, "上将", false);
            AddLeaderboardRow(sampleList, 3, "SeaHammer", 6, 9, 3, "中将", false);
            AddLeaderboardRow(sampleList, 4, "SkyLancer", 5, 7, 5, "少将", false);
            ShowModal();
            return;
        }

        AddModalSummaryRow(
            AddStatCard("赛季", FriendlyText(data.GetString("season"), "当前赛季"), FriendlyText(data.GetString("resetText"), "每周结算"), WarningText),
            AddStatCard("上榜人数", $"{rows.Count}", usingCache ? "缓存排名" : "实时排名", GoodText),
            AddStatCard("我的位置", "已标记", "绿色行为当前账号", PanelText));
        AddLeaderboardTableHeader(out var list);
        foreach (var item in rows)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var row = item.AsGodotDictionary();
            AddLeaderboardRow(list, row.GetInt("place"), row.GetString("username"), row.GetInt("level", 1), row.GetInt("wins"), row.GetInt("losses"), row.GetString("rankTitle"), row.GetBool("isCurrent"));
        }
        ShowModal();
    }

    void RenderMailModal(Godot.Collections.Dictionary data, bool usingCache)
    {
        OpenFeatureModal("系统邮件", "查看新兵补给发放与战报信息。");

        int unreadCount = 0;
        int rewardCount = 0;
        foreach (var m in systemMails)
        {
            if (!m.IsRead) unreadCount++;
            if (m.HasReward && !m.IsClaimed) rewardCount++;
        }

        AddModalSummaryRow(
            AddStatCard("收件箱", $"{systemMails.Count} 封", "系统总邮件", PanelText),
            AddStatCard("未读邮件", $"{unreadCount} 封", "请及时查阅", unreadCount > 0 ? WarningText : GoodText),
            AddStatCard("待领补给", $"{rewardCount} 项", "邮件道具附件", rewardCount > 0 ? GoodText : PanelText));

        var mailSection = AddModalSectionPanel("邮件列表", "点击邮件项可以查看正文并领取奖励附件", new Color(0.70f, 0.92f, 1f));
        foreach (var mail in systemMails)
        {
            mailSection.AddChild(CreateSystemMailCard(mail));
        }
        ShowModal();
    }

    void ShowMailDetail(SystemMail mail)
    {
        // 标记为已读
        for (int i = 0; i < systemMails.Count; i++)
        {
            if (systemMails[i].Id == mail.Id)
            {
                var updated = systemMails[i];
                updated.IsRead = true;
                systemMails[i] = updated;
                mail = updated; // 同步当前变量以便后续渲染
                break;
            }
        }

        OpenFeatureModal($"邮件正文", $"时间: {mail.Time} | 类型: {mail.Type}");

        var detailSection = AddModalSectionPanel(mail.Title, $"时间: {mail.Time}", new Color(0.70f, 0.92f, 1f));
        
        var bodyLabel = AddLabel(mail.Content, 13, PanelText, HorizontalAlignment.Left);
        bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailSection.AddChild(bodyLabel);

        if (mail.HasReward)
        {
            var rewardSec = AddModalSectionPanel("补给附件", mail.IsClaimed ? "附件奖励已成功领入您的账号" : "点击下方按钮领取金币与钻石奖励", new Color(0.92f, 0.74f, 0.28f));
            
            var row = new HBoxContainer
            {
                Name = "RewardRow",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            row.AddThemeConstantOverride("separation", 16);
            rewardSec.AddChild(row);
            
            row.AddChild(AddLabel($"🎁 包含金币 x{mail.RewardGold} | 钻石 x{mail.RewardGems}", 14, WarningText, HorizontalAlignment.Center));
            
            if (!mail.IsClaimed)
            {
                var claimBtn = AddButton("领取奖励", () => ClaimMailReward(mail.Id), ButtonTone.Gold, 13);
                row.AddChild(claimBtn);
            }
            else
            {
                row.AddChild(CreateTag("已领取", new Color(0.22f, 0.24f, 0.28f, 0.92f), new Color(1f, 0.95f, 0.82f)));
            }
        }

        var actions = AddModalSectionPanel("操作", "阅读完毕后返回邮件列表", PanelText);
        var backBtn = AddButton("返回邮件列表", () => RenderMailModal(new Godot.Collections.Dictionary(), false), ButtonTone.Secondary, 14);
        actions.AddChild(backBtn);

        ShowModal();
    }

    void ClaimMailReward(string mailId)
    {
        for (int i = 0; i < systemMails.Count; i++)
        {
            if (systemMails[i].Id == mailId)
            {
                var updated = systemMails[i];
                if (updated.IsClaimed)
                    break;
                updated.IsClaimed = true;
                systemMails[i] = updated;

                GameState.Instance?.AddCurrency(updated.RewardGold, updated.RewardGems);
                RefreshTopBar();

                ShowToast($"领取成功！金币 +{updated.RewardGold}，钻石 +{updated.RewardGems}");
                ShowMailDetail(updated);
                break;
            }
        }
    }

    async Task ShowInvitesModal()
    {
        activeFeatureModal = "invites";

        OpenFeatureModal("房间邀请", "处理来自好友的房间对局邀请。");
        modalBody.AddChild(AddLabel("正在加载对局邀请...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();
        await RefreshInvitesCacheForModal();
    }

    async Task RefreshInvitesCacheForModal()
    {
        Godot.Collections.Dictionary data = new();
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetInvites(SecondaryModalRequestTimeoutSec);

        if (data.GetBool("success"))
        {
            cachedInvitesData = (Godot.Collections.Dictionary)data.Duplicate(true);
            invitesCacheAtMs = NowMs();
        }

        if (activeFeatureModal == "invites" && modalPanel.Visible)
            RenderInvitesModal(data.GetBool("success") ? data : cachedInvitesData, data.GetBool("success") ? false : HasInvitesCache());
    }

    void RenderInvitesModal(Godot.Collections.Dictionary data, bool usingCache)
    {
        OpenFeatureModal(
            "房间邀请",
            usingCache
                ? "处理来自好友的房间对局邀请。已显示缓存，正在后台刷新。"
                : "处理来自好友的房间对局邀请。");

        var invites = data.GetArray("invites");
        if (invites.Count == 0)
        {
            var mockInvite = new Godot.Collections.Dictionary
            {
                { "id", "mock_invite_1" },
                { "roomId", "9999" },
                { "from", "IronWolf" },
                { "mapName", "沙漠绿洲" },
                { "maxPlayers", 2 },
                { "playerCount", 1 }
            };
            invites = new Godot.Collections.Array { mockInvite };
        }

        AddModalSummaryRow(
            AddStatCard("收到邀请", $"{invites.Count} 个", "未决的对局邀请", PanelText),
            AddStatCard("状态", usingCache ? "已缓存" : "最新同步", "网络同步状态", usingCache ? WarningText : GoodText));

        var inviteSection = AddModalSectionPanel("对局邀请列表", "接受邀请可以直接加入对应的房间，拒绝则会清除该条邀请。", new Color(0.70f, 0.92f, 1f));

        foreach (var item in invites)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var invite = item.AsGodotDictionary();
            inviteSection.AddChild(CreateMailInviteCard(invite));
        }

        ShowModal();
    }

    void AddMailInviteRow(VBoxContainer list, Godot.Collections.Dictionary invite)
    {
        var inviteId = invite.GetString("id");
        var roomId = invite.GetString("roomId");
        var mapName = FriendlyText(invite.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var maxPlayers = invite.GetInt("maxPlayers", 2);
        var playerCount = invite.GetInt("playerCount", 0);
        var row = AddActionTableRow(
            new[] { FriendlyText(invite.GetString("from"), "好友"), $"{mapName}  {Math.Max(0, playerCount)}/{Math.Max(2, maxPlayers)}", ShortRoomId(roomId) },
            new[] { 128f, 168f, 126f },
            new[]
            {
                AddButton("接受", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 12),
                AddButton("拒绝", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 12)
            },
            false);
        list.AddChild(row);
    }

    Panel CreateMailInviteCard(Godot.Collections.Dictionary invite)
    {
        var inviteId = invite.GetString("id");
        var roomId = invite.GetString("roomId");
        var from = FriendlyText(invite.GetString("from"), "好友");
        var mapName = FriendlyText(invite.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var maxPlayers = Math.Max(2, invite.GetInt("maxPlayers", 2));
        var playerCount = Math.Max(0, invite.GetInt("playerCount", 0));

        var card = new Panel
        {
            Name = "InviteCard_" + ShortRoomId(roomId),
            CustomMinimumSize = new Vector2(0, 108)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, new Color(0.95f, 0.76f, 0.30f, 0.86f), 3);

        var row = AddHBox(card, "InviteCardRow", 10);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 14;
        row.OffsetBottom = -12;

        var info = new VBoxContainer
        {
            Name = "InviteCardInfo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        info.AddThemeConstantOverride("separation", 4);
        info.AddChild(AddLabel(from, 16, PanelText, HorizontalAlignment.Left));

        var metaRow = new HFlowContainer
        {
            Name = "InviteMeta_" + ShortRoomId(roomId),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        metaRow.AddThemeConstantOverride("h_separation", 6);
        metaRow.AddThemeConstantOverride("v_separation", 6);
        metaRow.AddChild(CreateTag(mapName, new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.92f, 0.97f, 0.99f)));
        metaRow.AddChild(CreateTag($"{playerCount}/{maxPlayers}", new Color(0.31f, 0.23f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)));
        info.AddChild(metaRow);

        var detailLabel = AddLabel($"房间号: {ShortRoomId(roomId)}  |  点击接受后直接加入房间。", 11, MutedText, HorizontalAlignment.Left);
        detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(detailLabel);
        row.AddChild(info);

        var actions = new VBoxContainer
        {
            Name = "InviteCardActions",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        actions.AddThemeConstantOverride("separation", 6);
        var acceptButton = AddButton("接受", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 12);
        acceptButton.CustomMinimumSize = new Vector2(96, 0);
        var rejectButton = AddButton("拒绝", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 12);
        rejectButton.CustomMinimumSize = new Vector2(96, 0);
        actions.AddChild(acceptButton);
        actions.AddChild(rejectButton);
        row.AddChild(actions);

        return card;
    }

    void AddSystemMailRow(VBoxContainer list, string title, string type, string status, string note)
    {
        list.AddChild(AddTableRow(
            new[] { title, type, status, note },
            new[] { 184f, 92f, 82f, 220f },
            "",
            null,
            ButtonTone.Secondary,
            status.Contains("未读") || status.Contains("可领")));
    }

    void OpenFeatureModal(string title, string subtitle)
    {
        modalTitle.Text = title;
        ClearChildren(modalBody);
        var subtitleLabel = AddLabel(subtitle, 13, MutedText, HorizontalAlignment.Left);
        subtitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(subtitleLabel);

        if (modalTitle?.GetParent() is HBoxContainer header)
        {
            var oldTrash = header.GetNodeOrNull("ModalTrashButton");
            if (oldTrash != null)
            {
                header.RemoveChild(oldTrash);
                oldTrash.QueueFree();
            }
        }
    }
    void AddModalSummaryRow(params Control[] cards)
    {
        var row = new HFlowContainer
        {
            Name = "ModalSummaryRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("h_separation", 8);
        row.AddThemeConstantOverride("v_separation", 8);
        foreach (var card in cards)
        {
            card.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            card.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(card);
        }
        modalBody.AddChild(row);
    }

    Control AddModalSectionHeader(string title, string note, Color accent)
    {
        var holder = new VBoxContainer
        {
            Name = "SectionHeader_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        holder.AddThemeConstantOverride("separation", 2);

        var titleLabel = AddLabel(title, 16, accent, HorizontalAlignment.Left);
        holder.AddChild(titleLabel);

        var noteLabel = AddLabel(note, 12, MutedText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        holder.AddChild(noteLabel);
        return holder;
    }

    Panel AddStatCard(string title, string value, string note, Color accent)
    {
        var card = new Panel
        {
            Name = "StatCard_" + title,
            CustomMinimumSize = new Vector2(184, 96),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 7, 4);
        AddTopAccentStripe(card, new Color(accent.R, accent.G, accent.B, 0.82f), 3);

        var box = AddVBox(card, "StatCardBox_" + title, 4, new Vector2(12, 10), new Vector2(-12, -10));
        var titleLabel = AddLabel(title, 12, MutedText, HorizontalAlignment.Left);
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleLabel.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(titleLabel);

        var valueLabel = AddLabel(value, 18, accent, HorizontalAlignment.Left);
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        valueLabel.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(valueLabel);

        var noteLabel = AddLabel(note, 11, PanelText, HorizontalAlignment.Left);
        noteLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        noteLabel.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(noteLabel);
        return card;
    }

    Panel AddEmptyStateCard(string title, string note)
    {
        var card = new Panel
        {
            Name = "EmptyStateCard",
            CustomMinimumSize = new Vector2(0, 86)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, new Color(0.44f, 0.52f, 0.58f, 0.42f), 2);

        var box = AddVBox(card, "EmptyStateBox", 4, new Vector2(14, 12), new Vector2(-14, -12));
        box.AddChild(AddLabel(title, 14, PanelText, HorizontalAlignment.Left));
        var noteLabel = AddLabel(note, 12, MutedText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(noteLabel);
        return card;
    }

    Panel CreateSystemMailCard(SystemMail mail)
    {
        var isUnread = !mail.IsRead;
        var hasClaimable = mail.HasReward && !mail.IsClaimed;

        var accent = hasClaimable
            ? new Color(0.88f, 0.70f, 0.26f, 0.88f)
            : isUnread
                ? new Color(0.48f, 0.88f, 0.60f, 0.82f)
                : new Color(0.72f, 0.84f, 0.90f, 0.42f);

        var card = new Panel
        {
            Name = "SystemMail_" + mail.Id,
            CustomMinimumSize = new Vector2(0, 68)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, accent, 2);

        var clickButton = new Button
        {
            Name = "ClickBtn_" + mail.Id,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 68),
            Position = Vector2.Zero
        };
        clickButton.SetAnchorsPreset(LayoutPreset.FullRect);
        clickButton.Pressed += () => ShowMailDetail(mail);
        card.AddChild(clickButton);

        var box = AddVBox(card, "SystemMailBox_" + mail.Id, 2, new Vector2(14, 8), new Vector2(-14, -8));
        box.MouseFilter = MouseFilterEnum.Ignore;

        var titleRow = AddHBox(box, "TitleRow_" + mail.Id, 10);
        titleRow.MouseFilter = MouseFilterEnum.Ignore;

        var timeLabel = AddLabel($"[{mail.Time}]", 12, MutedText, HorizontalAlignment.Left);
        timeLabel.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(timeLabel);

        var titleLabel = AddLabel(mail.Title, 14, isUnread ? PanelText : MutedText, HorizontalAlignment.Left);
        titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(titleLabel);

        var statusText = mail.HasReward ? (mail.IsClaimed ? "已领" : "可领") : (mail.IsRead ? "已读" : "未读");
        var statusColor = mail.HasReward ? (mail.IsClaimed ? new Color(0.22f, 0.24f, 0.28f, 0.92f) : new Color(0.76f, 0.56f, 0.18f, 0.96f))
                                         : (mail.IsRead ? new Color(0.22f, 0.24f, 0.28f, 0.92f) : new Color(0.18f, 0.40f, 0.26f, 0.92f));

        var typeTag = CreateTag(mail.Type, isUnread ? new Color(0.18f, 0.34f, 0.40f, 0.92f) : new Color(0.20f, 0.28f, 0.36f, 0.92f), new Color(0.94f, 0.97f, 0.98f));
        typeTag.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(typeTag);

        var statusTag = CreateTag(statusText, statusColor, new Color(1f, 0.95f, 0.82f));
        statusTag.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(statusTag);

        var summaryText = mail.Summary;
        if (summaryText.Length > 6)
            summaryText = summaryText.Substring(0, 6);

        var noteLabel = AddLabel(summaryText + "...", 12, isUnread ? PanelText.Lerp(Colors.White, 0.15f) : MutedText, HorizontalAlignment.Left);
        noteLabel.MouseFilter = MouseFilterEnum.Ignore;
        box.AddChild(noteLabel);

        return card;
    }

    VBoxContainer AddModalSectionPanel(string title, string note, Color accent)
    {
        var panel = new PanelContainer
        {
            Name = "ModalSection_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(panel, new Color(accent.R, accent.G, accent.B, 0.70f), 3);
        modalBody.AddChild(panel);

        var margin = new MarginContainer
        {
            Name = panel.Name + "_Margin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        panel.AddChild(margin);

        var box = new VBoxContainer
        {
            Name = "ModalSectionBox_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);
        box.AddChild(AddModalSectionHeader(title, note, accent));
        return box;
    }

    VBoxContainer AddModalSectionPlain(string title, string note, Color accent)
    {
        var box = new VBoxContainer
        {
            Name = "ModalSectionBoxPlain_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 8);

        // 如果已经有子节点，增加间距以分隔不同日期的公告
        if (modalBody.GetChildCount() > 0)
        {
            var spacer = new Control { CustomMinimumSize = new Vector2(0, 14) };
            modalBody.AddChild(spacer);
        }

        modalBody.AddChild(box);
        box.AddChild(AddModalSectionHeader(title, note, accent));
        return box;
    }

    void AddTopAccentStripe(Control parent, Color color, int height)
    {
        var stripe = new ColorRect
        {
            Name = parent.Name + "_Accent",
            Color = color,
            MouseFilter = MouseFilterEnum.Ignore
        };
        stripe.SetAnchorsPreset(LayoutPreset.TopWide);
        stripe.OffsetLeft = 0;
        stripe.OffsetRight = 0;
        stripe.OffsetTop = 0;
        stripe.OffsetBottom = height;
        parent.AddChild(stripe);
    }

    Control CreateTag(string text, Color bg, Color fg)
    {
        var minWidth = Math.Max(56, 20 + text.Length * 12);
        var panel = new PanelContainer
        {
            Name = "Tag_" + text,
            CustomMinimumSize = new Vector2(minWidth, 26),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });

        var margin = new MarginContainer
        {
            Name = "TagMargin_" + text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 3);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 3);
        panel.AddChild(margin);

        var label = AddLabel(text, 11, fg, HorizontalAlignment.Center);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        margin.AddChild(label);
        return panel;
    }

    VBoxContainer AddModalTable(string title, string[] headers, float[] widths)
    {
        var panel = new PanelContainer
        {
            Name = "Table_" + title,
            CustomMinimumSize = new Vector2(0, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 8, 4);
        modalBody.AddChild(panel);

        var margin = new MarginContainer
        {
            Name = "TableMargin_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var box = new VBoxContainer
        {
            Name = "TableBox_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);
        box.AddChild(AddSectionTitle(title));
        box.AddChild(AddHeaderRow(headers, widths));

        var list = new VBoxContainer
        {
            Name = "TableRows_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        list.AddThemeConstantOverride("separation", 6);
        box.AddChild(list);
        return list;
    }

    Control AddHeaderRow(string[] headers, float[] widths)
    {
        var row = new HBoxContainer
        {
            Name = "HeaderRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < headers.Length; i++)
        {
            var label = AddLabel(headers[i], 12, WarningText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 24);
            label.SizeFlagsHorizontal = i == headers.Length - 1 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(label);
        }
        return row;
    }

    Label CreateCellLabel(int colIndex, string text, float width, bool highlight, bool isLastColumn = false, bool hasAction = false)
    {
        Color cellColor = highlight ? GoodText : PanelText;
        int fontSize = 12;

        if (colIndex == 0) // 排名列
        {
            if (text.Contains("#1"))
            {
                cellColor = new Color(1.00f, 0.84f, 0.20f); // 豪华亮金色
                fontSize = 15;
            }
            else if (text.Contains("#2"))
            {
                cellColor = new Color(0.96f, 0.79f, 0.24f); // 豪华暖金色
                fontSize = 14;
            }
            else if (text.Contains("#3"))
            {
                cellColor = new Color(0.92f, 0.74f, 0.28f); // 豪华软金色
                fontSize = 14;
            }
        }

        var label = AddLabel(text, fontSize, cellColor, HorizontalAlignment.Left);
        label.CustomMinimumSize = new Vector2(width, 36);
        label.SizeFlagsHorizontal = isLastColumn && !hasAction ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        label.VerticalAlignment = VerticalAlignment.Top;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    Control AddTableRow(string[] cells, float[] widths, string actionText, Action? onAction, ButtonTone actionTone, bool highlight = false)
    {
        var panel = new PanelContainer
        {
            Name = "TableRow_" + Guid.NewGuid().ToString("N")[..6],
            CustomMinimumSize = new Vector2(0, 56),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, highlight ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 3);

        var margin = new MarginContainer
        {
            Name = "TableRowMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            Name = "Row",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        for (var i = 0; i < cells.Length; i++)
        {
            var label = CreateCellLabel(i, cells[i], widths[i], highlight, i == cells.Length - 1, !string.IsNullOrEmpty(actionText));
            row.AddChild(label);
        }

        if (!string.IsNullOrEmpty(actionText) && onAction is not null)
        {
            var action = AddButton(actionText, onAction, actionTone, 12);
            action.CustomMinimumSize = new Vector2(84, 28);
            action.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            row.AddChild(action);
        }

        return panel;
    }

    Control AddActionTableRow(string[] cells, float[] widths, IEnumerable<Button> actions, bool highlight)
    {
        var panel = new PanelContainer
        {
            Name = "ActionTableRow_" + Guid.NewGuid().ToString("N")[..6],
            CustomMinimumSize = new Vector2(0, 56),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, highlight ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 3);

        var margin = new MarginContainer
        {
            Name = "ActionTableRowMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            Name = "ActionRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        for (var i = 0; i < cells.Length; i++)
        {
            var label = CreateCellLabel(i, cells[i], widths[i], highlight, i == cells.Length - 1, true);
            row.AddChild(label);
        }

        var actionBox = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        actionBox.AddThemeConstantOverride("separation", 6);
        foreach (var action in actions)
        {
            action.CustomMinimumSize = new Vector2(68, 28);
            action.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            actionBox.AddChild(action);
        }
        row.AddChild(actionBox);
        return panel;
    }

    Control AddEmptyTableRow(string text)
    {
        var panel = new PanelContainer
        {
            Name = "EmptyTableRow",
            CustomMinimumSize = new Vector2(0, 46),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);

        var margin = new MarginContainer
        {
            Name = "EmptyTableRowMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var label = AddLabel(text, 13, MutedText, HorizontalAlignment.Center);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        margin.AddChild(label);
        return panel;
    }

    static string UnitCategory(string unitKey) => unitKey switch
    {
        "scout_plane" or "fighter" or "bomber" => "空军",
        "patrol_boat" or "destroyer_ship" or "transport_ship" => "海军",
        "light_tank" or "tank" or "heavy_tank" or "artillery" or "anti_air_gun" => "装甲",
        _ => "陆军"
    };

    static string DescribeWarehouseBlueprint(string unitKey) => unitKey switch
    {
        "infantry" => "基础占点与前线推进单位",
        "infantry_artillery" => "远程支援步兵，擅长压制密集目标",
        "infantry_flamethrower" or "flamethrower" => "近距离清障与建筑压制单位",
        "light_tank" => "机动穿插装甲，适合侦察与骚扰",
        "tank" or "medium_tank" => "中坚装甲火力，适合正面推进",
        "heavy_tank" => "高耐久重装单位，适合攻坚与吸收伤害",
        "artillery" => "远程曲射火力，适合后排支援",
        "anti_air_gun" => "防空压制平台，兼顾反低空目标",
        "scout_plane" => "提供快速视野侦察与空中巡查",
        "fighter" => "高机动制空单位，优先拦截空军",
        "bomber" => "重型空袭单位，适合点杀高价值目标",
        "patrol_boat" => "轻型水面巡逻与护航单位",
        "destroyer_ship" => "主力海军火力单位，适合中远程压制",
        "transport_ship" => "大型运输舰船，适合海上投送",
        _ => "已编入战场军备库"
    };

    static string WarehouseUnitStatus(string unitKey) => unitKey switch
    {
        "fighter" or "bomber" or "destroyer_ship" or "transport_ship" => "需对应军备建筑解锁",
        "heavy_tank" => "需更高主基地等级",
        _ => "已收录，可在战斗内生产"
    };

    async Task StartQuickMatch()
    {
        if (quickMatchStarting || battleStarting)
            return;

        quickMatchStarting = true;
        SelectModeInternal(QuickMatchMode, BattleMapCatalog.DefaultMapName, false);
        GameState.Instance?.ClearCurrentRoom();
        ShowToast("快速匹配中...");

        try
        {
            if (NetClient.Instance is not null && !string.IsNullOrEmpty(GameState.Instance?.Token) && GameState.Instance?.IsGuest != true)
            {
                var matchTask = NetClient.Instance.JoinMatch(selectedMap);
                var completed = await Task.WhenAny(matchTask, Task.Delay(1800));
                if (completed == matchTask)
                {
                    var match = await matchTask;
                    if (match.GetBool("success") && match.GetBool("matched"))
                    {
                        var mapName = match.GetString("mapName", selectedMap);
                        selectedMap = BattleMapCatalog.IsKnownMap(mapName) ? mapName : selectedMap;
                        GameState.Instance?.SelectMap(selectedMap, selectedMode);
                        var roomId = match.GetString("roomId");
                        await ShowMatchConfirmationModal(roomId, selectedMap);
                        return;
                    }
                }
            }

            // Local simulation fallback
            ShowToast("未匹配到在线对手，开启模拟战役...");
            await Task.Delay(800);
            await ShowMatchConfirmationModal("local_practice_room", selectedMap);
        }
        finally
        {
            quickMatchStarting = false;
        }
    }

    class MatchConfState
    {
        public bool Accepted = false;
        public bool Declined = false;
        public bool Completed = false;
    }

    async Task ShowMatchConfirmationModal(string roomId, string mapName)
    {
        var confState = new MatchConfState();

        selectedMode = QuickMatchMode;
        selectedMap = mapName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);

        // 使用极其紧凑的特定对话框规格
        Place(modalPanel, new Rect2(0.260f, 0.300f, 0.480f, 0.400f));
        modalPanel.CustomMinimumSize = new Vector2(580, 252);

        ClearChildren(modalBody);
        modalTitle.Text = "匹配就绪 - 3v3战术对抗";

        // Countdown container
        var topRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var countdownLabel = AddLabel("请确认：10秒内未确认将取消匹配", 14, WarningText, HorizontalAlignment.Center);
        topRow.AddChild(countdownLabel);
        modalBody.AddChild(topRow);

        // HBox for columns
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        columns.AddThemeConstantOverride("separation", 16);
        modalBody.AddChild(columns);

        // Blue Team Column
        var blueCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        blueCol.AddThemeConstantOverride("separation", 6);
        columns.AddChild(blueCol);

        // Red Team Column
        var redCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        redCol.AddThemeConstantOverride("separation", 6);
        columns.AddChild(redCol);

        // Define players
        var myName = GameState.Instance?.Username ?? "指战员(我)";
        var bluePlayers = new[] { myName, "战鹰02", "钢铁泰坦" };
        var redPlayers = new[] { "阿尔法九号", "闪击野狼", "重装先锋" };

        var blueReady = new bool[] { false, false, false };
        var redReady = new bool[] { false, false, false };

        // Helper to rebuild rows
        Action updateSlots = () => {
            // Blue Team Rows
            ClearChildren(blueCol);
            blueCol.AddChild(AddLabel("蓝队 (己方)", 13, new Color(0.4f, 0.7f, 1.0f), HorizontalAlignment.Left));
            for (int i = 0; i < 3; i++)
            {
                var rowPanel = BuildPlayerConfirmRow(bluePlayers[i], i == 0 ? LocalCommanderAvatarTexturePath() : ResolveFriendAvatarTexturePath(bluePlayers[i]), blueReady[i]);
                blueCol.AddChild(rowPanel);
            }

            // Red Team Rows
            ClearChildren(redCol);
            redCol.AddChild(AddLabel("红队 (敌方)", 13, new Color(1.0f, 0.4f, 0.4f), HorizontalAlignment.Left));
            for (int i = 0; i < 3; i++)
            {
                var rowPanel = BuildPlayerConfirmRow(redPlayers[i], ResolveFriendAvatarTexturePath(redPlayers[i]), redReady[i]);
                redCol.AddChild(rowPanel);
            }
        };

        updateSlots();

        // Control Buttons Row
        var btnRow = new HBoxContainer 
        { 
            Name = "TaskModalActions", 
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        btnRow.AddThemeConstantOverride("separation", 16);
        
        var acceptBtn = AddButton("同意战斗", () => {
            confState.Accepted = true;
            blueReady[0] = true;
            updateSlots();
            ShowToast("您已同意，等待其他玩家确认...");
        }, ButtonTone.Primary, 13);
        acceptBtn.CustomMinimumSize = new Vector2(160, 34);
        btnRow.AddChild(acceptBtn);

        var declineBtn = AddButton("拒绝匹配", () => {
            confState.Declined = true;
        }, ButtonTone.Secondary, 13);
        declineBtn.CustomMinimumSize = new Vector2(120, 34);
        btnRow.AddChild(declineBtn);
        modalBody.AddChild(btnRow);

        ShowModal();

        // Start countdown and confirmation simulation
        int timeLeft = 10;
        var rand = new Random();

        while (timeLeft > 0 && !confState.Declined && !confState.Completed)
        {
            countdownLabel.Text = $"匹配已成功！请确认加入：{timeLeft} 秒";

            // Simulate other players accepting
            if (rand.NextDouble() < 0.35 && !blueReady[1]) { blueReady[1] = true; updateSlots(); }
            if (rand.NextDouble() < 0.35 && !blueReady[2]) { blueReady[2] = true; updateSlots(); }
            if (rand.NextDouble() < 0.35 && !redReady[0]) { redReady[0] = true; updateSlots(); }
            if (rand.NextDouble() < 0.35 && !redReady[1]) { redReady[1] = true; updateSlots(); }
            if (rand.NextDouble() < 0.35 && !redReady[2]) { redReady[2] = true; updateSlots(); }

            // Check if everyone accepted
            bool everyoneConfirmed = blueReady[0] && blueReady[1] && blueReady[2] && redReady[0] && redReady[1] && redReady[2];
            if (everyoneConfirmed)
            {
                confState.Completed = true;
                break;
            }

            await Task.Delay(1000);
            timeLeft--;
        }

        if (confState.Completed)
        {
            countdownLabel.Text = "所有玩家已确认！正在转入战场...";
            countdownLabel.AddThemeColorOverride("font_color", GoodText);
            await Task.Delay(800);
            CloseModal();
            GameState.Instance?.SetCurrentRoom(roomId);
            await StartBattle(false);
        }
        else
        {
            CloseModal();
            if (confState.Declined)
            {
                ShowToast("已取消本次匹配。");
            }
            else
            {
                ShowToast("确认超时，已退出匹配队列。");
            }

            // 游客屏蔽网络接口调用，防止401强制退登
            if (NetClient.Instance is not null && !string.IsNullOrEmpty(GameState.Instance?.Token) && GameState.Instance?.IsGuest != true)
            {
                _ = NetClient.Instance.CancelMatch();
            }
        }
    }

    Panel BuildPlayerConfirmRow(string name, string avatarPath, bool confirmed)
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(0, 34), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        MetalUiStyle.ApplyMetalPanel(panel, confirmed ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 2);

        var box = AddHBox(panel, "PlayerConfirmRowBox", 8);
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 8;
        box.OffsetRight = -8;

        var avatar = new TextureRect
        {
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Texture = LoadTexture(avatarPath)
        };
        box.AddChild(avatar);

        box.AddChild(AddLabel(name, 11, PanelText, HorizontalAlignment.Left));

        if (confirmed)
        {
            var tag = CreateTag("已同意", new Color(0.12f, 0.32f, 0.16f), new Color(0.7f, 1f, 0.7f));
            tag.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            box.AddChild(tag);
        }
        else
        {
            var tag = CreateTag("确认中...", new Color(0.32f, 0.28f, 0.12f), new Color(1f, 0.94f, 0.7f));
            tag.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            box.AddChild(tag);
        }

        return panel;
    }

    bool ShouldShowQuickMatchBattleEntry()
    {
        var state = GameState.Instance;
        return state is not null
            && state.HasBattleEntry
            && state.LastBattleMode == QuickMatchMode
            && !string.IsNullOrWhiteSpace(state.CurrentRoomId);
    }

    async Task TriggerQuickMatchCard()
    {
        if (ShouldShowQuickMatchBattleEntry())
        {
            var resumeMap = GameState.Instance?.LastBattleMapName ?? BattleMapCatalog.DefaultMapName;
            SelectModeInternal(QuickMatchMode, resumeMap, false);
            ShowToast("进入战场");
            await StartBattle(false);
            return;
        }

        await StartQuickMatch();
    }

    async Task StartGlobalConquest()
    {
        await OpenGlobalConquestTeamPanel();
    }

    async Task LeaveActiveRoomAndRefreshGc()
    {
        await LeaveActiveRoom();
        _ = OpenGlobalConquestTeamPanel();
    }

    void AddGridRow(GridContainer grid, string key, string value)
    {
        grid.AddChild(AddLabel(key, 11, WarningText, HorizontalAlignment.Left));
        grid.AddChild(AddLabel(value, 11, PanelText, HorizontalAlignment.Left));
    }

    async Task CreateGlobalConquestRoom()
    {
        var mapName = BattleMapCatalog.GlobalConquestName;
        var maxPlayers = 5;

        // 离线/游客/服务器未连接 fallback 到本地 5人战役房间
        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token) || GameState.Instance?.IsGuest == true)
        {
            activeRoomId = "local_gc_room";
            activeRoomMap = mapName;
            activeRoomMaxPlayers = maxPlayers;
            activeRoomPlayerCount = 5; // 填满 AI，让离线体验最佳
            selectedMap = activeRoomMap;
            GameState.Instance?.SelectMap(activeRoomMap, GlobalConquestMode);
            GameState.Instance?.SetCurrentRoom(activeRoomId);
            ShowToast("已进入本地全球争霸房间 (包含4名模拟盟友)");
            _ = OpenGlobalConquestTeamPanel();
            return;
        }

        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "创建中...";

        var roomName = $"{GameState.Instance?.Username ?? "玩家"}的全球争霸团";
        var data = await NetClient.Instance.CreateRoom(mapName, roomName, maxPlayers);
        if (!data.GetBool("success"))
        {
            // 服务器错误 fallback
            ShowToast("服务器连线失败，已为您创建本地争霸房间");
            activeRoomId = "local_gc_room";
            activeRoomMap = mapName;
            activeRoomMaxPlayers = maxPlayers;
            activeRoomPlayerCount = 5;
            selectedMap = activeRoomMap;
            GameState.Instance?.SelectMap(activeRoomMap, GlobalConquestMode);
            GameState.Instance?.SetCurrentRoom(activeRoomId);
            _ = OpenGlobalConquestTeamPanel();
            return;
        }

        activeRoomId = data.GetString("roomId");
        activeRoomMap = data.GetString("mapName", mapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", maxPlayers);
        activeRoomPlayerCount = data.GetInt("playerCount", 1);
        selectedMap = activeRoomMap;
        GameState.Instance?.SelectMap(activeRoomMap, GlobalConquestMode);
        GameState.Instance?.SetCurrentRoom(activeRoomId);
        ShowToast("已创建全球争霸 5人组队房间");
        
        await RefreshRooms();
        _ = OpenGlobalConquestTeamPanel();
    }

    Task OpenGlobalConquestTeamPanel()
    {
        selectedMode = GlobalConquestMode;
        selectedMap = BattleMapCatalog.GlobalConquestName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);

        ExpandModalPanel(true);
        ClearChildren(modalBody);

        bool inGcRoom = !string.IsNullOrEmpty(activeRoomId)
            && activeRoomMaxPlayers == 5
            && activeRoomMap == BattleMapCatalog.GlobalConquestName;

        if (inGcRoom)
        {
            modalTitle.Text = "全球争霸 5人组队团";

            var splitBox = new HBoxContainer
            {
                Name = "GcSplitBox",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            splitBox.AddThemeConstantOverride("separation", 16);
            modalBody.AddChild(splitBox);

            var leftCol = new VBoxContainer
            {
                Name = "GcLeftCol",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 1.1f
            };
            leftCol.AddThemeConstantOverride("separation", 10);
            splitBox.AddChild(leftCol);

            var rightCol = new VBoxContainer
            {
                Name = "GcRightCol",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.9f
            };
            rightCol.AddThemeConstantOverride("separation", 10);
            splitBox.AddChild(rightCol);

            var settingsPanel = new Panel { Name = "GcSettingsPanel", CustomMinimumSize = new Vector2(0, 160) };
            MetalUiStyle.ApplyMetalPanel(settingsPanel, MetalUiStyle.Steel, 1, 8, 4);
            var settingsBox = AddVBox(settingsPanel, "GcSettingsBox", 8, new Vector2(12, 10), new Vector2(-12, -10));

            settingsBox.AddChild(AddLabel("战术配置 (设置您的阵营)", 14, WarningText, HorizontalAlignment.Left));

            var factionRow = new HBoxContainer { Name = "GcFactionRow" };
            factionRow.AddThemeConstantOverride("separation", 8);
            settingsBox.AddChild(factionRow);

            var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
            var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);

            foreach (var fac in BattleUnitCatalog.GlobalConquestFactions)
            {
                var isSel = fac.Key == currentFaction.Key;
                var btnText = fac.DisplayName + (isSel ? " (当前)" : "");
                var btn = AddButton(btnText, () => {
                    var starterKey = fac.StarterUnitKey;
                    GameState.Instance?.SetGlobalConquestStarterUnit(starterKey);
                    GameState.Instance?.SetGlobalConquestFactionChosen(true);
                    ShowToast($"已选择阵营：{fac.DisplayName}");
                    _ = OpenGlobalConquestTeamPanel();
                }, isSel ? ButtonTone.Primary : ButtonTone.Secondary, 12);
                btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                factionRow.AddChild(btn);
            }

            var unitRow = new HBoxContainer { Name = "GcUnitRow" };
            unitRow.AddThemeConstantOverride("separation", 8);
            settingsBox.AddChild(unitRow);

            unitRow.AddChild(AddLabel("初始主力兵种：", 12, PanelText, HorizontalAlignment.Left));

            var unitPicker = new OptionButton { Name = "GcUnitPicker", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var u in BattleUnitCatalog.GlobalConquestStarterRoster)
            {
                unitPicker.AddItem(u.DisplayName + " (" + DescribeGlobalConquestStarter(u.Key) + ")", 0);
                if (u.Key == currentStarterKey)
                {
                    unitPicker.Selected = unitPicker.ItemCount - 1;
                }
            }
            unitPicker.ItemSelected += (long idx) => {
                var selectedUnit = BattleUnitCatalog.GlobalConquestStarterRoster[idx];
                GameState.Instance?.SetGlobalConquestStarterUnit(selectedUnit.Key);
                GameState.Instance?.SetGlobalConquestFactionChosen(true);
                ShowToast($"已选择初始主力：{selectedUnit.DisplayName}");
                _ = OpenGlobalConquestTeamPanel();
            };
            unitRow.AddChild(unitPicker);

            var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
            var statsLabel = AddLabel(
                $"主力属性：生命 {currentStarter.MaxHealth:0} | 攻击 {currentStarter.AttackDamage:0} | 射程 {currentStarter.AttackRange:0.#} | 速度 {currentStarter.MoveSpeed:0.#}",
                11, MutedText, HorizontalAlignment.Left
            );
            settingsBox.AddChild(statsLabel);

            leftCol.AddChild(settingsPanel);

            var teamPanel = new Panel { Name = "GcTeamPanel", SizeFlagsVertical = SizeFlags.ExpandFill };
            MetalUiStyle.ApplyMetalPanel(teamPanel, MetalUiStyle.Steel, 1, 8, 4);
            var teamBox = AddVBox(teamPanel, "GcTeamBox", 6, new Vector2(12, 10), new Vector2(-12, -10));

            teamBox.AddChild(AddLabel($"队伍成员 ({activeRoomPlayerCount}/5)", 14, WarningText, HorizontalAlignment.Left));

            for (int i = 0; i < 5; i++)
            {
                var slotRow = new Panel { CustomMinimumSize = new Vector2(0, 36) };
                MetalUiStyle.ApplyMetalPanel(slotRow, MetalUiStyle.Steel, 1, 4, 2);
                var slotBox = AddHBox(slotRow, $"GcSlot_{i}_Box", 8);
                slotBox.SetAnchorsPreset(LayoutPreset.FullRect);
                slotBox.OffsetLeft = 8;
                slotBox.OffsetRight = -8;

                if (i == 0)
                {
                    var meName = GameState.Instance?.Username ?? "我";
                    var hostLabel = AddLabel($"[队长] {meName}", 12, GoodText, HorizontalAlignment.Left);
                    slotBox.AddChild(hostLabel);
                    var factLabel = AddLabel($"{currentFaction.DisplayName} · {currentStarter.DisplayName}", 11, MutedText, HorizontalAlignment.Right);
                    slotBox.AddChild(factLabel);
                    var tag = CreateTag("已就绪", new Color(0.12f, 0.32f, 0.16f), new Color(0.7f, 1f, 0.7f));
                    slotBox.AddChild(tag);
                }
                else if (i < activeRoomPlayerCount)
                {
                    var teammateName = $"盟友 {i} 号";
                    var mockStarter = BattleUnitCatalog.GlobalConquestStarterRoster[i % BattleUnitCatalog.GlobalConquestStarterRoster.Length];
                    var mockFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(mockStarter.Key);
                    var nameLabel = AddLabel(teammateName, 12, PanelText, HorizontalAlignment.Left);
                    slotBox.AddChild(nameLabel);
                    var mockFactLabel = AddLabel($"{mockFaction.DisplayName} · {mockStarter.DisplayName}", 11, MutedText, HorizontalAlignment.Right);
                    slotBox.AddChild(mockFactLabel);
                    var tag = CreateTag("已就绪", new Color(0.12f, 0.32f, 0.16f), new Color(0.7f, 1f, 0.7f));
                    slotBox.AddChild(tag);
                }
                else
                {
                    var emptyLabel = AddLabel("(待加入空位)", 12, MutedText, HorizontalAlignment.Left);
                    slotBox.AddChild(emptyLabel);
                    var inviteBtn = AddButton("邀请好友", () => ShowInvitePanel(activeRoomId), ButtonTone.Gold, 11);
                    inviteBtn.CustomMinimumSize = new Vector2(76, 22);
                    inviteBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
                    slotBox.AddChild(inviteBtn);
                }

                teamBox.AddChild(slotRow);
            }

            leftCol.AddChild(teamPanel);

            var btnRow = new HBoxContainer { Name = "GcBtnRow" };
            btnRow.AddThemeConstantOverride("separation", 10);

            var leaveBtn = AddButton("离开队伍", () => _ = LeaveActiveRoomAndRefreshGc(), ButtonTone.Secondary, 13);
            leaveBtn.CustomMinimumSize = new Vector2(100, 36);
            btnRow.AddChild(leaveBtn);

            var startBtn = AddButton("进入战场", () => _ = EnterRoomBattle(activeRoomId, BattleMapCatalog.GlobalConquestName, 5), ButtonTone.Primary, 13);
            startBtn.CustomMinimumSize = new Vector2(150, 36);
            btnRow.AddChild(startBtn);

            leftCol.AddChild(btnRow);

            var mapIntroPanel = new Panel { Name = "GcMapIntroPanel", SizeFlagsVertical = SizeFlags.ExpandFill };
            MetalUiStyle.ApplyMetalPanel(mapIntroPanel, MetalUiStyle.Steel, 1, 8, 4);
            var mapBox = AddVBox(mapIntroPanel, "GcMapBox", 8, new Vector2(12, 10), new Vector2(-12, -10));

            mapBox.AddChild(AddLabel("地图介绍：全球争霸大地图", 15, WarningText, HorizontalAlignment.Left));

            var previewRect = new TextureRect
            {
                Name = "GcMapPreview",
                CustomMinimumSize = new Vector2(0, 150),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            try
            {
                previewRect.Texture = GD.Load<Texture2D>("res://assets/campaign/level5.jpg");
            }
            catch
            {
                // Fallback
            }
            mapBox.AddChild(previewRect);

            var statsGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            statsGrid.AddThemeConstantOverride("h_separation", 16);
            statsGrid.AddThemeConstantOverride("v_separation", 6);

            AddGridRow(statsGrid, "地图尺寸:", "200% 标准陆战地图 (沙漠绿洲)");
            AddGridRow(statsGrid, "建造半径:", "160米 (宽广前沿基地部署)");
            AddGridRow(statsGrid, "胜利条件:", "清空敌对势力的全部基地并夺取控制权");
            AddGridRow(statsGrid, "核心机制:", "主基地受损后无法自动重建, 需保障外部据点");

            mapBox.AddChild(statsGrid);

            var descText = "全球争霸战役在庞大的沙漠中心爆发。战场相比普通1v1对抗地图放大了整整两倍，长距离行军与多路进攻对玩家阵营间默契配合提出了极高要求。\n\n" +
                           "核心基地一旦陷落无法直接重建。玩家需要通过部署防线与初始主力协同扩张，夺取沙漠核心区域的控制权。此役极为考验长途调兵与侦察牵制技巧。";
            var descLabel = AddLabel(descText, 11, PanelText, HorizontalAlignment.Left);
            descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            mapBox.AddChild(descLabel);

            rightCol.AddChild(mapIntroPanel);
        }
        else
        {
            modalTitle.Text = "全球争霸组队大厅";

            var lobbyPanel = new Panel { Name = "GcLobbyPanel", CustomMinimumSize = new Vector2(0, 110) };
            MetalUiStyle.ApplyMetalPanel(lobbyPanel, MetalUiStyle.Steel, 1, 8, 4);
            var lobbyBox = AddVBox(lobbyPanel, "GcLobbyBox", 8, new Vector2(12, 10), new Vector2(-12, -10));

            var row = new HBoxContainer { Name = "GcLobbyRow" };
            row.AddThemeConstantOverride("separation", 8);

            var createBtn = AddButton("进入5人房间 (开战)", () => _ = CreateGlobalConquestRoom(), ButtonTone.Primary, 13);
            createBtn.CustomMinimumSize = new Vector2(162, 34);
            row.AddChild(createBtn);
            var refreshBtn = AddButton("刷新队伍列表", () => _ = RefreshRooms(), ButtonTone.Secondary, 13);
            refreshBtn.CustomMinimumSize = new Vector2(142, 34);
            row.AddChild(refreshBtn);
            lobbyBox.AddChild(row);

            lobbyBox.AddChild(AddLabel("全球争霸支持最多5人协作对抗敌军。您可以直接进入5人房间开始游戏或邀请好友组队。", 12, WarningText, HorizontalAlignment.Left));

            roomStatusLabel = AddLabel("正在获取队伍列表...", 13, MutedText, HorizontalAlignment.Left);
            lobbyBox.AddChild(roomStatusLabel);
            modalBody.AddChild(lobbyPanel);

            var scroll = new ScrollContainer
            {
                Name = "GcRoomScroll",
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            roomRows = new VBoxContainer { Name = "RoomRows", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            roomRows.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(roomRows);
            modalBody.AddChild(scroll);

            _ = RefreshRooms();
        }

        ShowModal();
        return Task.CompletedTask;
    }

    void ShowGlobalConquestStarterModal()
    {
        modalTitle.Text = "全球争霸阵营选择";
        ClearChildren(modalBody);

        var reminder = AddLabel("开始全球争霸前请选择开局阵营。进入本局后将锁定，本局战斗中不可更换。", 15, WarningText, HorizontalAlignment.Left);
        reminder.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(reminder);

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        var currentLabel = AddLabel($"当前阵营：{currentFaction.DisplayName}    起始单位：{currentStarter.DisplayName}    选择后会同步到全球争霸配置中。", 12, MutedText, HorizontalAlignment.Left);
        currentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(currentLabel);

        foreach (var faction in BattleUnitCatalog.GlobalConquestFactions)
            modalBody.AddChild(BuildGlobalConquestFactionOption(faction, currentFaction.Key == faction.Key));

        var footer = new HBoxContainer { Name = "GlobalConquestStarterFooter" };
        footer.AddThemeConstantOverride("separation", 10);
        footer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(Spacer());
        var cancelButton = AddButton("暂不进入", CloseModal, ButtonTone.Secondary, 13);
        cancelButton.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        cancelButton.CustomMinimumSize = new Vector2(132, 0);
        footer.AddChild(cancelButton);
        modalBody.AddChild(footer);

        ShowModal();
    }

    Panel BuildGlobalConquestFactionOption(GlobalConquestFactionDefinition faction, bool isCurrent)
    {
        var starter = BattleUnitCatalog.Get(faction.StarterUnitKey);
        var optionName = "Faction_" + faction.Key;
        var panel = new Panel
        {
            Name = optionName,
            CustomMinimumSize = new Vector2(0, 104)
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 10, 4);

        var row = AddHBox(panel, optionName + "_Row", 12);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 10;
        row.OffsetBottom = -10;

        var textBox = new VBoxContainer
        {
            Name = optionName + "_Text",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        textBox.AddThemeConstantOverride("separation", 4);

        var title = AddLabel(faction.DisplayName, 16, PanelText, HorizontalAlignment.Left);
        textBox.AddChild(title);

        var summary = AddLabel(faction.Description, 12, MutedText, HorizontalAlignment.Left);
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textBox.AddChild(summary);

        var stats = AddLabel(
            $"起始单位：{starter.DisplayName}    生命：{starter.MaxHealth:0}    攻击：{starter.AttackDamage:0}    射程：{starter.AttackRange:0.#}    速度：{starter.MoveSpeed:0.#}",
            12,
            isCurrent ? GoodText : faction.Tint,
            HorizontalAlignment.Left);
        stats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textBox.AddChild(stats);

        if (isCurrent)
            textBox.AddChild(AddLabel("当前默认阵营", 11, GoodText, HorizontalAlignment.Left));

        row.AddChild(textBox);

        var enterButton = AddButton(isCurrent ? "按当前阵营进入" : "选择阵营并进入", async () => await ConfirmGlobalConquestFaction(faction), ButtonTone.Primary, 13);
        enterButton.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        enterButton.CustomMinimumSize = new Vector2(148, 0);
        row.AddChild(enterButton);

        return panel;
    }

    async Task ConfirmGlobalConquestFaction(GlobalConquestFactionDefinition faction)
    {
        var starter = BattleUnitCatalog.Get(faction.StarterUnitKey);
        SelectModeInternal(GlobalConquestMode, BattleMapCatalog.GlobalConquestName, false);
        GameState.Instance?.SetGlobalConquestStarterUnit(starter.Key);
        GameState.Instance?.SetGlobalConquestFactionChosen(true);
        GameState.Instance?.ClearCurrentRoom();
        CloseModal();
        ShowToast($"全球争霸阵营已设为{faction.DisplayName}");
        await StartBattle(false);
    }

    static string DescribeGlobalConquestStarter(string unitKey)
        => unitKey switch
        {
            "light_tank" => "机动最快，适合侦察、抢点和快速转线。",
            "tank" => "攻防均衡，适合稳步推进和正面作战。",
            "heavy_tank" => "装甲最厚、火力最强，但推进速度最慢。",
            "artillery" => "射程最远，适合远程压制，需要注意与敌人保持距离。",
            _ => "适合作为全球争霸开局主力兵种。"
        };

    async Task StartCampaign()
    {
        SelectModeInternal("战役", selectedMap, false);
        GameState.Instance?.ClearCurrentRoom();
        await StartBattle();
    }

    struct CampaignLevelData
    {
        public string LevelNum;
        public string LevelName;
        public string MapName;
        public string Description;
        public string Reward;
        public string PreviewPath;
    }

    static readonly CampaignLevelData[] CampaignLevels = new[]
    {
        new CampaignLevelData { LevelNum = "第一关", LevelName = "战役 - 基础行动指令", MapName = "沙漠绿洲", Description = "框选并移动你的战斗单位，击毁中央的敌军哨所及基地。", Reward = "400金币", PreviewPath = "res://assets/campaign/level1.jpg" },
        new CampaignLevelData { LevelNum = "第二关", LevelName = "战役 - 基地展开与采矿", MapName = "丛林战场", Description = "建造发电厂以获取电力，展开采矿场以收集资金，训练步兵并消灭敌方基地。", Reward = "700金币", PreviewPath = "res://assets/campaign/level2.jpg" },
        new CampaignLevelData { LevelNum = "第三关", LevelName = "战役 - 坦克风暴克制协同", MapName = "冰雪要塞", Description = "训练克制兵种协同作战，摧毁敌方核心雷达站及基地。", Reward = "700金币", PreviewPath = "res://assets/campaign/level3.jpg" },
        new CampaignLevelData { LevelNum = "第四关", LevelName = "战役 - 要塞死守防御战", MapName = "城市废墟", Description = "建造机枪碉堡构筑防御线，抵御进攻并消灭所有敌军基地。", Reward = "900金币", PreviewPath = "res://assets/campaign/level4.jpg" },
        new CampaignLevelData { LevelNum = "第五关", LevelName = "战役 - 终极模拟演习", MapName = "全球争霸", Description = "研发全线科技解锁终极单位，摧毁敌方所有防线和AI基地。", Reward = "1400金币", PreviewPath = "res://assets/campaign/level5.jpg" }
    };

    async Task StartCampaignLevel(string mapName)
    {
        SelectModeInternal("战役", mapName, false);
        GameState.Instance?.ClearCurrentRoom();
        await StartBattle();
    }

    void ShowCampaignModal()
    {
        OpenFeatureModal("战役关卡选择", "指战员，选择你要部署的战役关卡进行战区推演并赢取丰厚金币奖励。");
        AddModalSummaryRow(
            AddStatCard("当前关卡", "1 关", "沙漠绿洲", WarningText),
            AddStatCard("可挑战关卡", "5 关", "全部开放", PanelText),
            AddStatCard("战役总金币", "4,100 金币", "包含所有章节任务", GoodText));

        var section = AddModalSectionPanel("战役关卡列表", "选择一条战役线路，指挥部会为你准备对应编组方案。", WarningText);
        foreach (var lvl in CampaignLevels)
        {
            section.AddChild(CreateCampaignLevelCard(lvl));
        }
        ShowModal();
    }

    Control CreateCampaignLevelCard(CampaignLevelData level)
    {
        var card = new Panel
        {
            Name = "CampaignLevelCard_" + level.MapName,
            CustomMinimumSize = new Vector2(0, 140),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true
        };
        MetalUiStyle.ApplyMetalPanel(card, MakeModeCardPalette(new Color(0.18f, 0.42f, 0.65f)), 1, 8, 4);
        AddTopAccentStripe(card, new Color(0.24f, 0.58f, 0.88f, 0.82f), 3);

        var shade = new ColorRect
        {
            Name = "Shade_" + level.MapName,
            Color = new Color(0.02f, 0.03f, 0.03f, 0.35f)
        };
        shade.SetAnchorsPreset(LayoutPreset.FullRect);
        card.AddChild(shade);

        var row = new HBoxContainer
        {
            Name = "Row_" + level.MapName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 10;
        row.OffsetBottom = -10;
        row.AddThemeConstantOverride("separation", 14);
        card.AddChild(row);

        var thumb = new TextureRect
        {
            Name = "Thumb_" + level.MapName,
            Texture = LoadTexture(level.PreviewPath),
            CustomMinimumSize = new Vector2(160, 100),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        row.AddChild(thumb);

        var info = new VBoxContainer
        {
            Name = "Info_" + level.MapName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        info.AddThemeConstantOverride("separation", 4);
        row.AddChild(info);

        var titleRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleRow.AddThemeConstantOverride("separation", 8);

        var numLabel = AddLabel(level.LevelNum, 14, WarningText, HorizontalAlignment.Left);
        numLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        titleRow.AddChild(numLabel);

        var nameLabel = AddLabel(level.LevelName, 14, PanelText, HorizontalAlignment.Left);
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(nameLabel);

        var mapLabel = AddLabel($"[{level.MapName}]", 12, new Color(0.6f, 0.8f, 0.9f), HorizontalAlignment.Right);
        mapLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        titleRow.AddChild(mapLabel);

        info.AddChild(titleRow);

        var descLabel = AddLabel(level.Description, 11, MutedText, HorizontalAlignment.Left);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.ClipText = false;
        descLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        info.AddChild(descLabel);

        var bottomRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkEnd };
        bottomRow.AddThemeConstantOverride("separation", 12);

        var rewardLabel = AddLabel($"通关奖励: {level.Reward}", 12, new Color(0.95f, 0.76f, 0.24f), HorizontalAlignment.Left);
        rewardLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottomRow.AddChild(rewardLabel);

        var deployBtn = AddButton("部署行动", () => {
            CloseModal();
            _ = StartCampaignLevel(level.MapName);
        }, ButtonTone.Gold, 12);
        deployBtn.CustomMinimumSize = new Vector2(86, 28);
        bottomRow.AddChild(deployBtn);

        info.AddChild(bottomRow);

        return card;
    }

    void SelectModeInternal(string mode, string mapName, bool showToast)
    {
        selectedMode = mode;
        selectedMap = mapName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);
        ApplySelection(mode);
        RefreshModeCards();

        if (showToast)
            ShowToast($"已选择 {mode}");
    }

    void ApplySelection(string mode)
    {
        if (matchSelection is null)
            return;
        matchSelection.Visible = mode == QuickMatchMode;
        customSelection.Visible = mode == CustomRoomMode;
        globalSelection.Visible = mode == GlobalConquestMode;
    }

    Control BuildLoadingOverlay(string mapName, string modeName)
    {
        var overlay = new Control
        {
            Name = "LoadingOverlay",
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new TextureRect
        {
            Name = "LoadingBG",
            Texture = LoadTexture(UnityLobbyRoot + "user_lobby_background.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(bg);

        var dim = new ColorRect
        {
            Name = "LoadingDim",
            Color = new Color(0.02f, 0.03f, 0.04f, 0.72f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(dim);

        var centerWrap = new CenterContainer
        {
            Name = "CenterWrap",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        centerWrap.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(centerWrap);

        var center = new VBoxContainer
        {
            Name = "CenterBox",
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(400, 300)
        };
        centerWrap.AddChild(center);

        var title = AddLabel("正在载入战场...", 20, new Color(1f, 0.84f, 0.24f), HorizontalAlignment.Center);
        title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        title.AddThemeConstantOverride("outline_size", 2);
        center.AddChild(title);

        center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });

        var subtitle = AddLabel($"地图：{mapName}  |  模式：{modeName}", 13, new Color(0.86f, 0.90f, 0.93f, 0.96f), HorizontalAlignment.Center);
        center.AddChild(subtitle);

        center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 25) });

        var progressContainer = new PanelContainer
        {
            CustomMinimumSize = new Vector2(360, 24)
        };
        MetalUiStyle.ApplyMetalPanel(progressContainer, MetalUiStyle.Steel, 1, 6, 4);
        center.AddChild(progressContainer);

        var progressBar = new ProgressBar
        {
            Name = "ProgressBar",
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(360, 24)
        };

        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.12f, 0.96f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        var fgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.96f, 0.79f, 0.30f, 0.92f),
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            ShadowColor = new Color(1f, 0.80f, 0.24f, 0.18f),
            ShadowSize = 6
        };
        progressBar.AddThemeStyleboxOverride("background", bgStyle);
        progressBar.AddThemeStyleboxOverride("fill", fgStyle);
        progressContainer.AddChild(progressBar);

        center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        var progressLabel = AddLabel("0%", 12, new Color(0.96f, 0.79f, 0.30f), HorizontalAlignment.Center);
        progressLabel.Name = "ProgressLabel";
        center.AddChild(progressLabel);

        center.AddChild(new Control { CustomMinimumSize = new Vector2(0, 20) });

        string[] tips = new[]
        {
            "提示：主基地被摧毁后，可以在废墟状态下花费金币进行重建。",
            "提示：前三名指挥官在军衔军功榜上会显示独特的多彩星徽记。",
            "提示：在地图上建造防御塔可以有效阻挡敌方战车的推进。",
            "提示：科技升级能大幅提高作战单位的伤害与护甲减伤。",
            "提示：游戏加载可能需要数秒，请耐心等待战斗加载完成。"
        };
        var randomTip = tips[new Random().Next(tips.Length)];
        var tipLabel = AddLabel(randomTip, 11, new Color(0.60f, 0.65f, 0.70f), HorizontalAlignment.Center);
        center.AddChild(tipLabel);

        return overlay;
    }

    async Task StartBattle(bool clearRoom = true)
    {
        if (battleStarting)
            return;
        battleStarting = true;

        var overlay = BuildLoadingOverlay(selectedMap, selectedMode);
        AddChild(overlay);

        // ── 等待 overlay 真正渲染到屏幕上再开始后台加载 ──────────────────
        // Godot 在 AddChild 后并不会立即绘制，需要等到下一帧（或更多帧）
        // ProcessFrame 信号在帧渲染完毕后触发，等 2 帧确保背景图也加载完成
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var progressBar   = overlay.FindChild("ProgressBar",   true, false) as ProgressBar;
        var progressLabel = overlay.FindChild("ProgressLabel", true, false) as Label;

        try
        {
            GameState.Instance?.RememberBattleEntry(selectedMode, selectedMap);
            if (clearRoom)
                GameState.Instance?.ClearCurrentRoom();

            var error = ResourceLoader.LoadThreadedRequest(BattleScenePath);
            if (error != Error.Ok)
            {
                GD.PrintErr($"[LobbyScreen] LoadThreadedRequest failed: {error}");
                GetTree().ChangeSceneToFile(BattleScenePath);
                return;
            }

            var progress = new Godot.Collections.Array { 0.0f };

            while (true)
            {
                var status = ResourceLoader.LoadThreadedGetStatus(BattleScenePath, progress);
                float progressVal = progress.Count > 0 ? progress[0].AsSingle() : 0f;

                if (progressBar is not null)
                    progressBar.Value = progressVal * 100f;
                if (progressLabel is not null)
                    progressLabel.Text = $"{Mathf.RoundToInt(progressVal * 100f)}%";

                if (status == ResourceLoader.ThreadLoadStatus.Loaded)
                    break;

                if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
                {
                    GD.PrintErr($"[LobbyScreen] Background loading failed: {status}");
                    break;
                }

                await Task.Delay(16);
            }

            // 进度满 100%，短暂停留让玩家看到满格进度条
            if (progressBar  is not null) progressBar.Value  = 100f;
            if (progressLabel is not null) progressLabel.Text = "100%";
            await Task.Delay(200);

            // ── 淡出动画：overlay 在 350ms 内渐渐变透明 ──────────────────
            // ChangeSceneToPacked 是主线程阻塞调用，会造成一帧卡顿。
            // 将这帧阻塞隐藏在动画结束点，玩家看到的是平滑淡出而非黑屏卡顿。
            var tween = overlay.CreateTween();
            tween.TweenProperty(overlay, "modulate:a", 0f, 0.35f)
                 .SetTrans(Tween.TransitionType.Quad)
                 .SetEase(Tween.EaseType.In);
            await ToSignal(tween, Tween.SignalName.Finished);

            // 再额外等一帧，确保淡出最后一帧已经提交给 GPU
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var loadedScene = (PackedScene)ResourceLoader.LoadThreadedGet(BattleScenePath);
            GetTree().ChangeSceneToPacked(loadedScene);
        }
        finally
        {
            battleStarting = false;
        }
    }

    void RefreshTopBar()
    {
        var gs = GameState.Instance;
        playerLabel.Text = string.IsNullOrWhiteSpace(gs?.Username) ? "游客指挥官" : gs!.Username;
        rankLabel.Text = $"Lv.{Math.Max(gs?.Level ?? 1, 1)}  |  {FriendlyText(gs?.RankTitle, "列兵")}";
        goldLabel.Text = $"{gs?.Gold ?? 0}";
        gemsLabel.Text = $"{gs?.Gems ?? 0}";
        if (commanderAvatarIcon is not null)
            commanderAvatarIcon.Texture = LoadTexture(LocalCommanderAvatarTexturePath());
        if (commanderRankIcon is not null)
            commanderRankIcon.Texture = LoadTexture(LobbyRankBadgeTexturePath(gs?.RankTitle));
        RefreshModeCards();
    }

    void RefreshModeCards()
    {
        if (!modeCards.TryGetValue(QuickMatchMode, out var quickMatchCard))
            return;

        if (ShouldShowQuickMatchBattleEntry())
        {
            quickMatchCard.TitleLabel.Text = "继续战斗";
            quickMatchCard.DescLabel.Text = "沿用上一次快速匹配的战场配置，直接返回当前战斗入口。";
            quickMatchCard.ActionLabel.Text = "返回战场";
        }
        else
        {
            quickMatchCard.TitleLabel.Text = QuickMatchCardTitle;
            quickMatchCard.DescLabel.Text = "标准地图快速匹配，立即前往最近一场作战。";
            quickMatchCard.ActionLabel.Text = "立即匹配";
        }

        if (!modeCards.TryGetValue(GlobalConquestMode, out var globalCard))
            return;

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var starter = BattleUnitCatalog.Get(currentStarterKey);
        var faction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        globalCard.TitleLabel.Text = GlobalConquestMode;
        globalCard.DescLabel.Text = "选择阵营与起始单位，配置全球争霸中的发展方向。";
        globalCard.ActionLabel.Text = $"当前阵营：{faction.DisplayName}  |  起始：{starter.DisplayName}";
    }
    void ShowCommanderProfileModal()
    {
        var gs = GameState.Instance;
        var rankTitle = NormalizeRankTitle(gs?.RankTitle);
        OpenFeatureModal("档案", "查看当前拥有的头像，并查看当前军衔。点击头像可立即切换。");
        AddModalSummaryRow(
            AddStatCard("账号", FriendlyText(gs?.Username, "游客"), $"Lv.{Math.Max(gs?.Level ?? 1, 1)}", WarningText),
            AddStatCard("当前军衔", rankTitle, "大厅与战场都会同步显示", LobbyRankColor(rankTitle)),
            AddStatCard("拥有头像", $"{GetCommanderAvatarOptions().Length} 款", "当前档案可用", PanelText));

        modalBody.AddChild(CreateCommanderRankShowcase(rankTitle));

        var avatarsSection = AddModalSectionPanel("头像库", "点击头像即可切换当前展示。", WarningText);
        avatarsSection.AddChild(CreateCommanderAvatarGrid());
        ShowModal();
    }

    Control CreateCommanderRankShowcase(string rankTitle)
    {
        var card = new Panel
        {
            Name = "CommanderRankShowcase",
            CustomMinimumSize = new Vector2(0, 152)
        };
        MetalUiStyle.ApplyMetalPanel(card, GlassPanel, 1, 8, 4);

        var row = AddHBox(card, "CommanderRankShowcaseRow", 16);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 16;
        row.OffsetTop = 16;
        row.OffsetRight = -16;
        row.OffsetBottom = -16;

        var badgeDock = new Panel
        {
            Name = "CommanderRankBadgeDock",
            CustomMinimumSize = new Vector2(136, 120),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        MetalUiStyle.ApplyMetalPanel(badgeDock, GlassPanel, 1, 8, 4);
        row.AddChild(badgeDock);

        var badge = new TextureRect
        {
            Name = "CommanderRankBadgeLarge",
            Texture = LoadTexture(LobbyRankBadgeTexturePath(rankTitle)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(104, 104),
            MouseFilter = MouseFilterEnum.Ignore
        };
        badge.SetAnchorsPreset(LayoutPreset.Center);
        badge.Position = new Vector2(16, 8);
        badgeDock.AddChild(badge);

        var info = new VBoxContainer
        {
            Name = "CommanderRankInfo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        info.AddThemeConstantOverride("separation", 6);
        row.AddChild(info);

        info.AddChild(AddLabel("当前军衔", 14, MutedText, HorizontalAlignment.Left));
        info.AddChild(AddLabel(rankTitle, 28, LobbyRankColor(rankTitle), HorizontalAlignment.Left));
        var note = AddLabel("军衔徽章已放大展示，方便直接看清当前段位。", 12, PanelText, HorizontalAlignment.Left);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(note);
        return card;
    }

    Control CreateCommanderAvatarGrid()
    {
        var grid = new GridContainer
        {
            Name = "CommanderAvatarGrid",
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);

        var selected = LocalCommanderAvatarTexturePath();
        foreach (var avatarPath in GetCommanderAvatarOptions())
            grid.AddChild(CreateCommanderAvatarOption(avatarPath, avatarPath == selected));
        return grid;
    }

    Control CreateCommanderAvatarOption(string avatarPath, bool selected)
    {
        var shell = new VBoxContainer
        {
            Name = "CommanderAvatarOption_" + StableVisualIndex(avatarPath, 1000),
            CustomMinimumSize = new Vector2(0, 126),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        shell.AddThemeConstantOverride("separation", 6);

        var button = new Button
        {
            Text = "",
            CustomMinimumSize = new Vector2(0, 92),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            TooltipText = selected ? "当前使用中" : "切换为这个头像"
        };
        button.Pressed += () => SelectCommanderAvatar(avatarPath);
        var border = selected ? new Color(1f, 0.82f, 0.34f, 0.98f) : new Color(0.72f, 0.64f, 0.36f, 0.36f);
        button.AddThemeStyleboxOverride("normal", TransparentButtonStyle(new Color(0.05f, 0.07f, 0.08f, 0.82f), 8, border));
        button.AddThemeStyleboxOverride("hover", TransparentButtonStyle(new Color(0.10f, 0.12f, 0.10f, 0.90f), 8, new Color(1f, 0.86f, 0.42f, 0.96f)));
        button.AddThemeStyleboxOverride("pressed", TransparentButtonStyle(new Color(0.12f, 0.10f, 0.06f, 0.94f), 8, new Color(1f, 0.78f, 0.28f, 1f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        var holder = new Control
        {
            Name = "AvatarOptionHolder",
            CustomMinimumSize = new Vector2(84, 84),
            MouseFilter = MouseFilterEnum.Ignore
        };
        holder.SetAnchorsPreset(LayoutPreset.Center);
        holder.Position = new Vector2(10, 4);
        button.AddChild(holder);

        var frame = new TextureRect
        {
            Name = "AvatarOptionFrame",
            Position = Vector2.Zero,
            Size = new Vector2(84, 84),
            Texture = LoadTexture(UnityLobbyRoot + "gen_portrait_ring.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        PortraitFrameUtils.ApplyWrapFrame(
            frame,
            new Color(0.86f, 0.66f, 0.20f, 0.98f),
            new Color(1f, 0.96f, 0.80f, 0.92f),
            0.49f,
            0.015f,
            0.0035f,
            0.010f);
        holder.AddChild(frame);

        var avatar = new TextureRect
        {
            Name = "AvatarOptionTexture",
            Position = new Vector2(7, 7),
            Size = new Vector2(70, 70),
            Texture = LoadTexture(avatarPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore
        };
        PortraitFrameUtils.ApplyCircularPortrait(
            avatar,
            new Color(0.94f, 0.74f, 0.24f, 1f),
            new Color(1f, 0.95f, 0.78f, 1f),
            0.47f,
            0.0055f,
            0.012f,
            new Vector2(1.20f, 1.20f),
            new Vector2(0.01f, -0.025f));
        holder.AddChild(avatar);

        shell.AddChild(button);
        shell.AddChild(AddLabel(selected ? "使用中" : AvatarDisplayName(avatarPath), 12, selected ? WarningText : PanelText, HorizontalAlignment.Center));
        return shell;
    }

    void SelectCommanderAvatar(string avatarPath)
    {
        GameState.Instance?.SetSelectedAvatarPath(avatarPath);
        RefreshTopBar();
        ShowCommanderProfileModal();
    }

    void ShowSettingsModal()
    {
        OpenFeatureModal("设置", "管理本地公告、战场屏蔽记录与账号会话。");
        AddModalSummaryRow(
            AddStatCard("账号", FriendlyText(GameState.Instance?.Username, "游客"), FriendlyText(GameState.Instance?.RankTitle, "列兵"), WarningText),
            AddStatCard("房间", string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId) ? "未加入" : ShortRoomId(GameState.Instance?.CurrentRoomId ?? ""), "当前会话", PanelText),
            AddStatCard("公告", $"{GetVisibleAnnouncements().Count} 条", "本地保留", GoodText));

        var section = AddModalSectionPanel("本地操作", "下面的动作会立即作用到当前本地档案。", WarningText);
        section.AddChild(CreateSettingsActionRow(
            "重置战场屏蔽与举报",
            "清除战斗设置里已保存的文字、语音屏蔽与举报标记。",
            "重置",
            ResetBattlePreferencesFromSettings,
            ButtonTone.Secondary));
        section.AddChild(CreateSettingsActionRow(
            "退出登录",
            "清除当前账号会话并返回登录界面。",
            "退出",
            LogoutToLogin,
            ButtonTone.Primary));
        ShowModal();
    }

    void ShowInfoModal(string title, string body)
    {
        activeFeatureModal = "";
        modalTitle.Text = title;
        ClearChildren(modalBody);
        var label = AddLabel(body, 15, PanelText, HorizontalAlignment.Left);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(label);
        ShowModal();
    }

    Control CreateSettingsActionRow(string title, string note, string actionText, Action onPressed, ButtonTone tone)
    {
        var panel = new Panel
        {
            Name = "SettingsAction_" + title,
            CustomMinimumSize = new Vector2(0, 84)
        };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(panel, new Color(0.76f, 0.70f, 0.46f, 0.46f), 2);

        var row = AddHBox(panel, "SettingsActionRow_" + title, 10);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 12;
        row.OffsetRight = -12;
        row.OffsetTop = 12;
        row.OffsetBottom = -12;

        var info = new VBoxContainer
        {
            Name = "SettingsActionInfo_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        info.AddThemeConstantOverride("separation", 4);
        info.AddChild(AddLabel(title, 15, PanelText, HorizontalAlignment.Left));
        var noteLabel = AddLabel(note, 12, MutedText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(noteLabel);
        row.AddChild(info);

        var button = AddButton(actionText, onPressed, tone, 12);
        button.CustomMinimumSize = new Vector2(88, 32);
        row.AddChild(button);
        return panel;
    }


    void ResetBattlePreferencesFromSettings()
    {
        GameState.Instance?.ClearBattleCommunicationPreferences();
        ShowSettingsModal();
        ShowToast("战场屏蔽与举报记录已重置");
    }

    void LogoutToLogin()
    {
        GameState.Instance?.ClearSession();
        CloseModal();
        GetTree().ChangeSceneToFile(LoginScenePath);
    }

    void ShowAnnouncementModal()
    {
        SyncAnnouncementFeed();
        RenderAnnouncementModal();
    }

    void RenderAnnouncementModal()
    {
        OpenFeatureModal("公告", "大厅更新会按日期保留在本地，已读后可手动清空。");

        var visible = GetVisibleAnnouncements();
        var latestDate = visible.Count > 0 ? visible[0].DateKey : "--";
        var dayCount = CountAnnouncementDays(visible);
        AddModalSummaryRow(
            AddStatCard("未清空公告", $"{visible.Count} 条", "当前本地保留", visible.Count > 0 ? WarningText : GoodText),
            AddStatCard("日期", $"{dayCount} 天", "按天归档", PanelText),
            AddStatCard("最近更新", latestDate, "本地记录日期", new Color(0.64f, 0.90f, 1f)));

        if (visible.Count == 0)
        {
            modalBody.AddChild(AddEmptyStateCard("当前没有待查看公告。", "新的大厅更新会按日期出现在这里。"));
            ShowModal();
            return;
        }

        if (modalTitle?.GetParent() is HBoxContainer header)
        {
            var trashBtn = new Button
            {
                Name = "ModalTrashButton",
                Icon = LoadTexture(KenneyGameIconRoot + "trashcan.png"),
                ExpandIcon = true,
                TooltipText = "清空公告",
                MouseDefaultCursorShape = CursorShape.PointingHand,
                CustomMinimumSize = new Vector2(40, 38),
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd
            };
            trashBtn.Pressed += ClearAnnouncements;
            ApplyTransparentIconStyle(trashBtn, 10);
            header.AddChild(trashBtn);
            header.MoveChild(trashBtn, header.GetChildCount() - 2);
        }

        var helper = AddLabel("公告会保存在本地；清空后当前列表不会再次自动弹回。", 12, MutedText, HorizontalAlignment.Left);
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(helper);

        string currentDate = "";
        VBoxContainer? section = null;
        foreach (var entry in visible)
        {
            if (!string.Equals(currentDate, entry.DateKey, StringComparison.Ordinal))
            {
                currentDate = entry.DateKey;
                section = AddModalSectionPlain(currentDate, $"{CountAnnouncementsForDate(visible, currentDate)} 条公告", WarningText);
            }

            section?.AddChild(CreateAnnouncementCard(entry));
        }

        ShowModal();
    }

    void ClearAnnouncements()
    {
        var visible = GetVisibleAnnouncements();
        foreach (var entry in visible)
            clearedAnnouncementIds.Add(entry.Id);
        SaveAnnouncementState();
        RenderAnnouncementModal();
        ShowToast("公告已清空");
    }

    Panel CreateAnnouncementCard(AnnouncementEntry entry)
    {
        var card = new Panel
        {
            Name = "Announcement_" + entry.Id,
            CustomMinimumSize = new Vector2(0, 68)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, new Color(0.84f, 0.67f, 0.28f, 0.74f), 2);

        var clickButton = new Button
        {
            Name = "ClickBtn_" + entry.Id,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 68),
            Position = Vector2.Zero
        };
        clickButton.SetAnchorsPreset(LayoutPreset.FullRect);
        clickButton.Pressed += () => ShowAnnouncementDetail(entry);
        card.AddChild(clickButton);

        var box = AddVBox(card, "AnnouncementBox_" + entry.Id, 2, new Vector2(14, 8), new Vector2(-14, -8));
        box.MouseFilter = MouseFilterEnum.Ignore;

        var titleRow = AddHBox(box, "TitleRow_" + entry.Id, 10);
        titleRow.MouseFilter = MouseFilterEnum.Ignore;

        var timeLabel = AddLabel($"[{entry.DateKey}]", 12, MutedText, HorizontalAlignment.Left);
        timeLabel.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(timeLabel);

        var titleLabel = AddLabel(entry.Title, 14, PanelText, HorizontalAlignment.Left);
        titleLabel.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(titleLabel);

        var typeTag = CreateTag(entry.Kind, new Color(0.18f, 0.34f, 0.40f, 0.92f), new Color(0.94f, 0.97f, 0.98f));
        typeTag.MouseFilter = MouseFilterEnum.Ignore;
        titleRow.AddChild(typeTag);

        var summaryText = entry.Body;
        if (summaryText.Length > 15)
            summaryText = summaryText.Substring(0, 15);

        var noteLabel = AddLabel(summaryText + "...", 12, MutedText, HorizontalAlignment.Left);
        noteLabel.MouseFilter = MouseFilterEnum.Ignore;
        box.AddChild(noteLabel);

        return card;
    }

    void ShowAnnouncementDetail(AnnouncementEntry entry)
    {
        OpenFeatureModal("公告详情", $"发布时间: {entry.DateKey} | 类型: {entry.Kind}");

        var detailSection = AddModalSectionPanel(entry.Title, $"发布日期: {entry.DateKey}", new Color(0.70f, 0.92f, 1f));
        
        var bodyLabel = AddLabel(entry.Body, 13, PanelText, HorizontalAlignment.Left);
        bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailSection.AddChild(bodyLabel);

        var actions = AddModalSectionPanel("操作", "阅读完毕后返回公告列表", PanelText);
        var backBtn = AddButton("返回公告列表", RenderAnnouncementModal, ButtonTone.Secondary, 14);
        actions.AddChild(backBtn);

        ShowModal();
    }

    void LoadAnnouncementState()
    {
        announcementArchive.Clear();
        clearedAnnouncementIds.Clear();

        if (!Godot.FileAccess.FileExists(AnnouncementStorePath))
            return;

        using var file = Godot.FileAccess.Open(AnnouncementStorePath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            return;

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
            return;

        var root = parsed.AsGodotDictionary();
        foreach (var item in root.GetArray("archive"))
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var data = item.AsGodotDictionary();
            var id = data.GetString("id");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            announcementArchive.Add(new AnnouncementEntry
            {
                Id = id,
                DateKey = FriendlyText(data.GetString("date"), DateTime.Now.ToString("yyyy-MM-dd")),
                Title = FriendlyText(data.GetString("title"), "大厅公告"),
                Body = FriendlyText(data.GetString("body"), ""),
                Kind = FriendlyText(data.GetString("kind"), "公告")
            });
        }

        foreach (var item in root.GetArray("cleared"))
        {
            var id = item.AsString();
            if (!string.IsNullOrWhiteSpace(id))
                clearedAnnouncementIds.Add(id);
        }

        SortAnnouncementArchive();
    }

    void SaveAnnouncementState()
    {
        SortAnnouncementArchive();

        var archive = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var entry in announcementArchive)
        {
            archive.Add(new Godot.Collections.Dictionary
            {
                ["id"] = entry.Id,
                ["date"] = entry.DateKey,
                ["title"] = entry.Title,
                ["body"] = entry.Body,
                ["kind"] = entry.Kind
            });
        }

        var cleared = new Godot.Collections.Array<string>();
        foreach (var id in clearedAnnouncementIds)
            cleared.Add(id);

        var root = new Godot.Collections.Dictionary
        {
            ["archive"] = archive,
            ["cleared"] = cleared
        };

        using var file = Godot.FileAccess.Open(AnnouncementStorePath, Godot.FileAccess.ModeFlags.Write);
        if (file is null)
            return;
        file.StoreString(Json.Stringify(root));
    }

    void SyncAnnouncementFeed()
    {
        var changed = false;
        foreach (var entry in BuildAnnouncementSeed())
        {
            int existingIdx = -1;
            for (int i = 0; i < announcementArchive.Count; i++)
            {
                if (announcementArchive[i].Id == entry.Id)
                {
                    existingIdx = i;
                    break;
                }
            }

            if (existingIdx >= 0)
            {
                var existing = announcementArchive[existingIdx];
                if (existing.Title != entry.Title || existing.Body != entry.Body || existing.Kind != entry.Kind || existing.DateKey != entry.DateKey)
                {
                    announcementArchive[existingIdx] = entry;
                    changed = true;
                }
            }
            else
            {
                announcementArchive.Add(entry);
                changed = true;
            }
        }

        if (changed)
            SaveAnnouncementState();
        else
            SortAnnouncementArchive();
    }

    List<AnnouncementEntry> BuildAnnouncementSeed()
    {
        var today = DateTime.Now.Date;
        return new List<AnnouncementEntry>
        {
            new()
            {
                Id = $"{today.AddDays(-2):yyyyMMdd}_lobby-connected",
                DateKey = today.AddDays(-2).ToString("yyyy-MM-dd"),
                Title = "大厅基础面板接入完成",
                Body = "房间、好友、任务、科技与排行榜入口已经接入真实面板，后续继续补服务端数据。",
                Kind = "大厅"
            },
            new()
            {
                Id = $"{today.AddDays(-1):yyyyMMdd}_secondary-cache",
                DateKey = today.AddDays(-1).ToString("yyyy-MM-dd"),
                Title = "排行榜与邀请面板提速",
                Body = "排行榜和邀请增加了 45 秒本地缓存，首次打开先显示上次结果，再后台刷新。",
                Kind = "优化"
            },
            new()
            {
                Id = $"{today:yyyyMMdd}_ui-polish",
                DateKey = today.ToString("yyyy-MM-dd"),
                Title = "邮件与军需商店界面重排",
                Body = "邮件邀请改成分组卡片，军需商店改成商品卡片，避免长文案把按钮挤出面板。",
                Kind = "界面"
            }
        };
    }

    List<AnnouncementEntry> GetVisibleAnnouncements()
    {
        var visible = new List<AnnouncementEntry>();
        foreach (var entry in announcementArchive)
        {
            if (!clearedAnnouncementIds.Contains(entry.Id))
                visible.Add(entry);
        }
        visible.Sort((a, b) =>
        {
            var dateCompare = string.CompareOrdinal(b.DateKey, a.DateKey);
            return dateCompare != 0 ? dateCompare : string.CompareOrdinal(b.Id, a.Id);
        });
        return visible;
    }

    bool HasAnnouncementEntry(string id)
    {
        foreach (var entry in announcementArchive)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    void SortAnnouncementArchive()
    {
        announcementArchive.Sort((a, b) =>
        {
            var dateCompare = string.CompareOrdinal(b.DateKey, a.DateKey);
            return dateCompare != 0 ? dateCompare : string.CompareOrdinal(b.Id, a.Id);
        });
    }

    int CountAnnouncementDays(List<AnnouncementEntry> entries)
    {
        var seen = new HashSet<string>();
        foreach (var entry in entries)
            seen.Add(entry.DateKey);
        return seen.Count;
    }

    int CountAnnouncementsForDate(List<AnnouncementEntry> entries, string dateKey)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (string.Equals(entry.DateKey, dateKey, StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    void ShowModal()
    {
        modalShade.Modulate = new Color(1f, 1f, 1f, 0f);
        modalPanel.Modulate = new Color(1f, 1f, 1f, 0f);
        modalShade.Visible = true;
        modalPanel.Visible = true;
        modalPanel.MoveToFront();

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(modalShade, "modulate:a", 1f, 0.12);
        tween.TweenProperty(modalPanel, "modulate:a", 1f, 0.16).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
    }

    void ExpandModalPanel(bool expand)
    {
        if (modalPanel is null || !GodotObject.IsInstanceValid(modalPanel))
            return;
        if (expand)
        {
            Place(modalPanel, new Rect2(0.120f, 0.100f, 0.760f, 0.800f));
            modalPanel.CustomMinimumSize = new Vector2(920, 520);
        }
        else
        {
            Place(modalPanel, new Rect2(0.245f, 0.130f, 0.510f, 0.740f));
            modalPanel.CustomMinimumSize = new Vector2(620, 500);
        }
    }

    void CloseModal()
    {
        activeFeatureModal = "";
        modalShade.Visible = false;
        modalPanel.Visible = false;
        ClearChildren(modalBody);
        ExpandModalPanel(false);
    }

    async void ShowToast(string message)
    {
        toastRevision++;
        var revision = toastRevision;

        toastLabel.Text = message;
        toastPanel.Visible = true;
        toastPanel.Modulate = new Color(1f, 1f, 1f, 0f);

        var tween = CreateTween();
        tween.TweenProperty(toastPanel, "modulate:a", 1f, 0.10);

        await ToSignal(GetTree().CreateTimer(1.8f), SceneTreeTimer.SignalName.Timeout);
        if (revision == toastRevision)
            toastPanel.Visible = false;
    }

    Panel AddPanel(string name, Rect2 anchors, MetalUiStyle.MetalPalette palette, int borderWidth)
    {
        var panel = new Panel { Name = name };
        Place(panel, anchors);
        MetalUiStyle.ApplyMetalPanel(panel, palette, borderWidth, 12, 4);
        AddChild(panel);
        return panel;
    }

    void AddBackgroundWash()
    {
        var wash = new ColorRect
        {
            Name = "BackgroundWash",
            Color = new Color(0.015f, 0.020f, 0.018f, 0.34f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(wash, new Rect2(0, 0, 1, 1));
        AddChild(wash);
    }

    static void ApplyTopBarSurface(Control panel, Color fill, Color border, int cornerRadius, Color glow)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ShadowColor = glow,
            ShadowSize = 8,
            ShadowOffset = Vector2.Zero
        });
    }

    Panel AddModeSelection(string name, Rect2 anchors)
    {
        var panel = new Panel
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(panel, anchors);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.98f, 0.80f, 0.28f, 0.10f),
            BorderColor = new Color(1f, 0.88f, 0.38f, 0.96f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ShadowColor = new Color(1f, 0.76f, 0.22f, 0.24f),
            ShadowSize = 12,
            ShadowOffset = Vector2.Zero
        });
        AddChild(panel);
        return panel;
    }

    static MetalUiStyle.MetalPalette MakeModeCardPalette(Color accent)
        => new(
            new Color(0.025f, 0.034f, 0.030f, 0.72f),
            new Color(accent.R, accent.G, accent.B, 0.72f),
            new Color(1f, 0.94f, 0.74f, 0.50f),
            new Color(0.02f, 0.025f, 0.020f, 0.74f),
            new Color(accent.R, accent.G, accent.B, 0.16f));

    VBoxContainer AddVBox(Control parent, string name, int separation, Vector2 marginLeftTop, Vector2 marginRightBottom)
    {
        var box = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", separation);
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = marginLeftTop.X;
        box.OffsetTop = marginLeftTop.Y;
        box.OffsetRight = marginRightBottom.X;
        box.OffsetBottom = marginRightBottom.Y;
        parent.AddChild(box);
        return box;
    }

    HBoxContainer AddHBox(Control parent, string name, int separation)
    {
        var box = new HBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", separation);
        parent.AddChild(box);
        return box;
    }

    Label AddSectionTitle(string text)
    {
        var label = AddLabel(text, 18, WarningText, HorizontalAlignment.Left);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        label.AddThemeConstantOverride("outline_size", 2);
        return label;
    }

    Label AddLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = alignment,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.78f));
        label.AddThemeConstantOverride("outline_size", 1);
        return label;
    }

    Button AddButton(string text, Action onPressed, ButtonTone tone, int fontSize)
    {
        var button = new Button
        {
            Text = text,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.Pressed += onPressed;

        if (tone == ButtonTone.Transparent)
        {
            button.Flat = true;
            button.Modulate = new Color(1f, 1f, 1f, 0.01f);
            return button;
        }

        MetalUiStyle.ApplyMetalButton(button, tone switch
        {
            ButtonTone.Primary => MetalUiStyle.Green,
            ButtonTone.Gold => MetalUiStyle.Gold,
            _ => MetalUiStyle.Steel
        }, fontSize, tone is ButtonTone.Primary or ButtonTone.Gold);
        return button;
    }

    Button AddIconButton(string iconPath, string tooltip, Action onPressed)
    {
        var button = new Button
        {
            Text = "",
            TooltipText = tooltip,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(40, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        button.Pressed += onPressed;
        ApplyTransparentIconStyle(button, 9);

        var iconCenter = new CenterContainer
        {
            Name = "IconCenter",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        button.AddChild(iconCenter);

        var icon = new TextureRect
        {
            Name = "Icon",
            Texture = LoadTrimmedTexture(iconPath, 0),
            CustomMinimumSize = new Vector2(18, 18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        iconCenter.AddChild(icon);
        return button;
    }

    Button AddCloseIconButton(Action onPressed)
    {
        var button = new Button
        {
            Icon = LoadTexture(IconRoot + "icon_close.svg"),
            ExpandIcon = true,
            TooltipText = "鍏抽棴",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(40, 38),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        button.Pressed += onPressed;
        ApplyTransparentIconStyle(button, 10);
        return button;
    }

    static void ApplyTransparentIconStyle(Button button, int cornerRadius)
    {
        button.Flat = true;
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", TransparentButtonStyle(new Color(0.95f, 0.75f, 0.28f, 0.08f), cornerRadius));
        button.AddThemeStyleboxOverride("pressed", TransparentButtonStyle(new Color(0.95f, 0.66f, 0.18f, 0.12f), cornerRadius));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
        button.AddThemeColorOverride("icon_normal_color", new Color(1f, 0.93f, 0.70f, 0.82f));
        button.AddThemeColorOverride("icon_hover_color", new Color(1f, 0.98f, 0.84f, 1f));
        button.AddThemeColorOverride("icon_pressed_color", new Color(1f, 0.76f, 0.34f, 1f));
    }

    static StyleBoxFlat TransparentButtonStyle(Color bg, int cornerRadius, Color? border = null)
        => new()
        {
            BgColor = bg,
            BorderColor = border ?? new Color(1f, 0.78f, 0.26f, 0.0f),
            BorderWidthLeft = border.HasValue ? 0 : 0,
            BorderWidthTop = border.HasValue ? 0 : 0,
            BorderWidthRight = border.HasValue ? 0 : 0,
            BorderWidthBottom = border.HasValue ? 0 : 0,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ContentMarginLeft = 0,
            ContentMarginTop = 0,
            ContentMarginRight = 0,
            ContentMarginBottom = 0
        };

    Control Spacer()
        => new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    TextureRect AddFullScreenTexture(string path, string name)
    {
        var rect = new TextureRect
        {
            Name = name,
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            Texture = LoadTexture(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(rect);
        return rect;
    }

    TextureRect AddAnchoredTexture(string path, string name, Rect2 anchors, Color modulate)
    {
        var rect = new TextureRect
        {
            Name = name,
            Texture = LoadTexture(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = modulate
        };
        Place(rect, anchors);
        AddChild(rect);
        return rect;
    }

    void BuildToast()
    {
        toastPanel = new Panel
        {
            Name = "ToastPanel",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(toastPanel, new Rect2(0.292f, 0.156f, 0.438f, 0.060f));
        MetalUiStyle.ApplyMetalPanel(toastPanel, GlassPanel, 1, 8, 10);
        AddChild(toastPanel);

        toastLabel = new Label
        {
            Name = "ToastLabel",
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        toastLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        toastLabel.OffsetLeft = 14;
        toastLabel.OffsetRight = -14;
        toastLabel.OffsetTop = 2;
        toastLabel.OffsetBottom = -2;
        toastLabel.AddThemeFontSizeOverride("font_size", 18);
        toastLabel.AddThemeColorOverride("font_color", ToastText);
        toastLabel.AddThemeColorOverride("font_outline_color", ToastOutline);
        toastLabel.AddThemeConstantOverride("outline_size", 4);
        toastPanel.AddChild(toastLabel);
    }

    static string LocalCommanderAvatarTexturePath()
        => string.IsNullOrWhiteSpace(GameState.Instance?.SelectedAvatarPath)
            ? UnityLobbyRoot + "WW2Portraits/officer_avatar_01_field_commander.png"
            : GameState.Instance!.SelectedAvatarPath;

    static string[] GetCommanderAvatarOptions()
        => new[]
        {
            UnityLobbyRoot + "WW2Portraits/officer_avatar_01_field_commander.png",
            UnityLobbyRoot + "WW2Portraits/officer_avatar_02_tank_commander.png",
            UnityLobbyRoot + "WW2Portraits/officer_avatar_03_air_wing.png",
            UnityLobbyRoot + "WW2Portraits/officer_avatar_04_naval_command.png",
            UnityLobbyRoot + "gen_avatar_friend_a.png",
            UnityLobbyRoot + "gen_avatar_friend_b.png"
        };

    static string AvatarDisplayName(string avatarPath)
    {
        if (avatarPath.Contains("field_commander", StringComparison.OrdinalIgnoreCase))
            return "野战指挥官";
        if (avatarPath.Contains("tank_commander", StringComparison.OrdinalIgnoreCase))
            return "装甲指挥官";
        if (avatarPath.Contains("air_wing", StringComparison.OrdinalIgnoreCase))
            return "空军指挥官";
        if (avatarPath.Contains("naval_command", StringComparison.OrdinalIgnoreCase))
            return "海军指挥官";
        if (avatarPath.Contains("friend_a", StringComparison.OrdinalIgnoreCase))
            return "侦察兵头像";
        if (avatarPath.Contains("friend_b", StringComparison.OrdinalIgnoreCase))
            return "突击兵头像";
        return "标准头像";
    }

    static bool IsGemCurrencyIcon(string iconPath)
        => iconPath.Contains("currency_gem_blue", StringComparison.OrdinalIgnoreCase);

    static string ResolveFriendAvatarTexturePath(string username)
    {
        var variants = new[]
        {
            UnityLobbyRoot + "WW2Portraits/officer_avatar_02_tank_commander.png",
            UnityLobbyRoot + "WW2Portraits/officer_avatar_03_air_wing.png",
            UnityLobbyRoot + "WW2Portraits/officer_avatar_04_naval_command.png",
            UnityLobbyRoot + "gen_avatar_friend_a.png",
            UnityLobbyRoot + "gen_avatar_friend_b.png"
        };
        return variants[StableVisualIndex(username, variants.Length)];
    }

    static int StableVisualIndex(string seed, int count)
    {
        if (count <= 0)
            return 0;

        var checksum = 0;
        foreach (var ch in seed ?? "")
            checksum += ch;
        return Mathf.Abs(checksum) % count;
    }

    static Texture2D LoadCurrencyIconTexture(string iconPath)
    {
        if (!IsGemCurrencyIcon(iconPath))
            return LoadTrimmedTexture(iconPath, 0);

        return LoadTrimmedTexture(UnityLobbyRoot + "gen_icon_gem.png", 0);
    }

    static Texture2D LoadTexture(string resourcePath)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is null || image.IsEmpty())
        {
            GD.PushError($"Failed to load lobby image: {resourcePath}");
            var fallback = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            fallback.Fill(new Color(1f, 0f, 1f, 1f));
            return ImageTexture.CreateFromImage(fallback);
        }

        return ImageTexture.CreateFromImage(image);
    }

    static Texture2D LoadTrimmedTexture(string resourcePath, int padding)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is null || image.IsEmpty())
            return LoadTexture(resourcePath);

        var bounds = FindOpaqueBounds(image);
        if (bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            return ImageTexture.CreateFromImage(image);

        var left = Math.Max(0, bounds.Position.X - padding);
        var top = Math.Max(0, bounds.Position.Y - padding);
        var right = Math.Min(image.GetWidth(), bounds.End.X + padding);
        var bottom = Math.Min(image.GetHeight(), bounds.End.Y + padding);
        var region = new Rect2I(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        return ImageTexture.CreateFromImage(image.GetRegion(region));
    }

    static Rect2I FindOpaqueBounds(Image image)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (image.GetPixel(x, y).A <= 0.02f)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
            return new Rect2I(0, 0, width, height);

        return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    static void Place(Control control, Rect2 anchors)
    {
        control.LayoutMode = 1;
        control.AnchorLeft = anchors.Position.X;
        control.AnchorTop = anchors.Position.Y;
        control.AnchorRight = anchors.Position.X + anchors.Size.X;
        control.AnchorBottom = anchors.Position.Y + anchors.Size.Y;
        control.OffsetLeft = 0f;
        control.OffsetTop = 0f;
        control.OffsetRight = 0f;
        control.OffsetBottom = 0f;
        control.GrowHorizontal = GrowDirection.Both;
        control.GrowVertical = GrowDirection.Both;
    }

    static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
            child.QueueFree();
    }

    static bool IsLive(GodotObject? obj)
        => obj is not null && GodotObject.IsInstanceValid(obj);

    bool IsCustomRoomModalContext()
        => IsLive(modalBody)
        && IsLive(modalTitle)
        && (string.Equals(modalTitle.Text, "自定义房间", StringComparison.Ordinal)
            || string.Equals(modalTitle.Text, "全球争霸组队", StringComparison.Ordinal)
            || string.Equals(modalTitle.Text, "全球争霸 5人组队团", StringComparison.Ordinal)
            || string.Equals(modalTitle.Text, "全球争霸组队大厅", StringComparison.Ordinal));

    static Color StatusColor(string status)
    {
        var lower = status.ToLowerInvariant();
        if (lower.Contains("离线") || lower.Contains("off"))
            return MutedText;
        if (lower.Contains("游戏") || lower.Contains("匹配") || lower.Contains("busy") || lower.Contains("match"))
            return WarningText;
        return GoodText;
    }

    static long NowMs()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static string FormatDuration(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";
    }

    static string FriendlyText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("閿?")
            || value.Contains("闁?")
            || value.Contains("闁?")
            || value.Contains("闁?"))
            return fallback;
        return value;
    }

    static string LobbyRankBadgeTexturePath(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        var badge = "rank_badge_01_bronze_shield.png";
        if (ContainsAny(normalized, "元帅", "王者", "将军", "marshal"))
            badge = "rank_badge_05_marshal_eagle.png";
        else if (ContainsAny(normalized, "上校", "少校", "major", "diamond", "钻石"))
            badge = "rank_badge_04_major_crossed_sabers.png";
        else if (ContainsAny(normalized, "黄金", "中将", "wing", "gold"))
            badge = "rank_badge_03_gold_wing_star.png";
        else if (ContainsAny(normalized, "白银", "少将", "silver"))
            badge = "rank_badge_02_silver_double_star.png";
        return UnityLobbyRoot + "WW2RankBadges/" + badge;
    }

    static Color LobbyRankColor(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        if (ContainsAny(normalized, "元帅", "王者", "将军", "marshal"))
            return new Color(1f, 0.92f, 0.66f);
        if (ContainsAny(normalized, "上校", "少校", "major"))
            return new Color(0.96f, 0.84f, 0.60f);
        if (ContainsAny(normalized, "钻石", "diamond"))
            return new Color(0.72f, 0.90f, 1f);
        if (ContainsAny(normalized, "黄金", "中将", "wing", "gold"))
            return new Color(1f, 0.84f, 0.34f);
        if (ContainsAny(normalized, "白银", "少将", "silver"))
            return new Color(0.84f, 0.90f, 0.98f);
        return new Color(0.86f, 0.76f, 0.62f);
    }

    static string NormalizeRankTitle(string? rankTitle)
        => string.IsNullOrWhiteSpace(rankTitle) ? "列兵" : rankTitle.Trim();

    static bool ContainsAny(string value, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (!string.IsNullOrWhiteSpace(pattern) && value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string ShortRoomId(string value)
        => value.Length <= 6 ? value : value[..6];

    int SelectedRoomPlayerCount()
    {
        if (roomPlayerCountPicker is null || roomPlayerCountPicker.ItemCount <= 0)
            return 2;

        var selected = Mathf.Clamp(roomPlayerCountPicker.Selected, 0, roomPlayerCountPicker.ItemCount - 1);
        var itemId = roomPlayerCountPicker.GetItemId(selected);
        return NormalizeCustomRoomPlayerCount(itemId);
    }

    static bool IsSupportedCustomRoom(Godot.Collections.Dictionary room)
    {
        var mapName = room.GetString("mapName", BattleMapCatalog.DefaultMapName);
        var maxPlayers = room.GetInt("maxPlayers", 2);
        return BattleMapCatalog.IsCustomRoomMap(mapName) && IsSupportedCustomRoomSize(maxPlayers);
    }

    static int NormalizeCustomRoomPlayerCount(int maxPlayers)
    {
        if (maxPlayers <= 2)
            return 2;
        if (maxPlayers <= 4)
            return 4;
        if (maxPlayers <= 5)
            return 5;
        return MaxCustomRoomPlayers;
    }

    static bool IsSupportedCustomRoomSize(int maxPlayers)
        => maxPlayers == 2 || maxPlayers == 4 || maxPlayers == 5 || maxPlayers == MaxCustomRoomPlayers;

    static string DescribeRoomModeHint(int maxPlayers) => maxPlayers switch
    {
        <= 2 => "1v1实时战斗",
        <= 4 => "2v2组房",
        5 => "5人战术协同",
        _ => "3v3组房"
    };

    static bool SupportsRealtimeRoomBattle(int maxPlayers)
        => maxPlayers <= 2;

    enum ButtonTone
    {
        Primary,
        Secondary,
        Gold,
        Transparent
    }

    private readonly System.Collections.Generic.HashSet<string> claimedLocalTaskIds = new();
    private readonly System.Collections.Generic.HashSet<int> claimedRecruitDays = new();
    private string activeEventTab = "recruit";

    void MarkLocalTaskClaimed(string taskId) => claimedLocalTaskIds.Add(taskId);
    bool IsLocalTaskClaimed(string taskId) => claimedLocalTaskIds.Contains(taskId);
    void MarkRecruitDayClaimed(int day) => claimedRecruitDays.Add(day);
    bool IsRecruitDayClaimed(int day) => claimedRecruitDays.Contains(day);

    void ShowEventsModal()
    {
        activeFeatureModal = "events";
        OpenFeatureModal("活动中心", "指战员，参与限时战场活动，完成战术演练，即可赢取海量金币与珍贵科研图纸。");
        
        AddModalSummaryRow(
            AddStatCard("进行中活动", "3 个", "参与即可领取奖励", WarningText),
            AddStatCard("可领取礼包", $"{((IsRecruitDayClaimed(3) ? 0 : 1) + (IsLocalTaskClaimed("EventTask_全歼敌方巡逻艇部队") ? 0 : 1) + (IsLocalTaskClaimed("EventTask_单局无损建造 3 个发电厂") ? 0 : 1))} 个", "完成活动解锁", GoodText),
            AddStatCard("赛季剩余时间", "24 天", "终极大奖：重型虎式坦克图纸", PanelText)
        );

        var tabs = new HBoxContainer { Name = "EventTabs" };
        tabs.AddThemeConstantOverride("separation", 10);
        
        var recruitBtn = AddButton(activeEventTab == "recruit" ? "★ 新兵七日礼" : "新兵七日礼", () => {
            activeEventTab = "recruit";
            ShowEventsModal();
        }, activeEventTab == "recruit" ? ButtonTone.Gold : ButtonTone.Secondary, 13);
        
        var campaignBtn = AddButton(activeEventTab == "campaign" ? "★ 闪击战役" : "闪击战役", () => {
            activeEventTab = "campaign";
            ShowEventsModal();
        }, activeEventTab == "campaign" ? ButtonTone.Gold : ButtonTone.Secondary, 13);

        var supplyBtn = AddButton(activeEventTab == "supply" ? "★ 军需集结" : "军需集结", () => {
            activeEventTab = "supply";
            ShowEventsModal();
        }, activeEventTab == "supply" ? ButtonTone.Gold : ButtonTone.Secondary, 13);

        tabs.AddChild(recruitBtn);
        tabs.AddChild(campaignBtn);
        tabs.AddChild(supplyBtn);
        modalBody.AddChild(tabs);

        if (activeEventTab == "recruit")
        {
            var section = AddModalSectionPanel("新兵签到", "每日登录即可解锁二战经典军备，金币直接到账！", WarningText);
            
            var flow = new HFlowContainer { Name = "RecruitFlow" };
            flow.AddThemeConstantOverride("h_separation", 8);
            flow.AddThemeConstantOverride("v_separation", 8);
            section.AddChild(flow);
            
            flow.AddChild(CreateRecruitDayCard(1, "100金币", true, false));
            flow.AddChild(CreateRecruitDayCard(2, "200金币", true, false));
            flow.AddChild(CreateRecruitDayCard(3, "300金币", IsRecruitDayClaimed(3), !IsRecruitDayClaimed(3)));
            flow.AddChild(CreateRecruitDayCard(4, "500金币", false, false));
            flow.AddChild(CreateRecruitDayCard(5, "谢尔曼图纸", false, false));
            flow.AddChild(CreateRecruitDayCard(6, "800金币", false, false));
            flow.AddChild(CreateRecruitDayCard(7, "重装虎式坦克", false, false));
        }
        else if (activeEventTab == "campaign")
        {
            var section = AddModalSectionPanel("闪击战役挑战", "完成特定闪击战术演练，获取重火力武器支援！", WarningText);
            
            section.AddChild(CreateEventTaskRow("在 10 分钟内赢得 1 场遭遇战", "奖励: 500金币 + 谢尔曼图纸", "0/1", false, false, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_assault_arrows.png"));
            section.AddChild(CreateEventTaskRow("全歼敌方巡逻艇部队", "奖励: 300金币", "1/1", false, true, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_firepower_barrage.png"));
            section.AddChild(CreateEventTaskRow("单局无损建造 3 个发电厂", "奖励: 250金币", "3/3", false, true, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_repair_workshop.png"));
        }
        else
        {
            var section = AddModalSectionPanel("限时物资筹备", "筹备基础工业物资，保障钢铁洪流的电力和黄金供应！", WarningText);
            section.AddChild(CreateEventTaskRow("累计开采金矿满 5,000 黄金", "奖励: 400金币 + 10钻石", "3,200/5,000", false, false, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_radar_map.png"));
            section.AddChild(CreateEventTaskRow("建造 5 个金矿与 5 个发电厂", "奖励: 600金币", "6/10", false, false, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_generic_plate.png"));
            section.AddChild(CreateEventTaskRow("单局战斗生产超过 30 辆坦克", "奖励: 800金币 + 20钻石", "30/30", false, true, "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_armor_plate.png"));
        }

        ShowModal();
    }

    Control CreateRecruitDayCard(int day, string reward, bool claimed, bool canClaim)
    {
        var card = new Panel
        {
            Name = $"RecruitDayCard_{day}",
            CustomMinimumSize = new Vector2(106, 92),
            ClipContents = true
        };
        
        var accent = claimed 
            ? new Color(0.5f, 0.5f, 0.5f, 0.4f) 
            : canClaim 
                ? new Color(0.95f, 0.72f, 0.28f, 0.95f) 
                : new Color(0.24f, 0.58f, 0.88f, 0.65f);
                
        MetalUiStyle.ApplyMetalPanel(card, new MetalUiStyle.MetalPalette(
            claimed ? new Color(0.04f, 0.05f, 0.06f, 0.94f) : new Color(0.02f, 0.03f, 0.05f, 0.96f),
            accent,
            new Color(1f, 0.95f, 0.84f, 0.15f),
            new Color(0.02f, 0.02f, 0.03f, 0.92f),
            new Color(accent.R, accent.G, accent.B, 0.08f)),
            1,
            4,
            3);

        const string radarMapPath = "res://assets/unity_migrated/Assets/Resources/BattleHud/TechBackgrounds/tech_bg_radar_map.png";
        if (ResourceLoader.Exists(radarMapPath))
        {
            var bgTex = new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(radarMapPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SelfModulate = new Color(1f, 1f, 1f, claimed ? 0.06f : 0.18f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            bgTex.SetAnchorsPreset(LayoutPreset.FullRect);
            card.AddChild(bgTex);
        }

        var box = AddVBox(card, "Box", 4, new Vector2(4, 6), new Vector2(-4, -6));
        box.AddChild(AddLabel($"第 {day} 天", 12, claimed ? MutedText : PanelText, HorizontalAlignment.Center));
        
        var rewardLabel = AddLabel(reward, 10, new Color(0.95f, 0.76f, 0.24f), HorizontalAlignment.Center);
        rewardLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rewardLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(rewardLabel);

        var statusText = claimed ? "已领取" : canClaim ? "可领取" : "未解锁";
        var button = AddButton(statusText, () => {
            if (canClaim && !claimed)
            {
                GameState.Instance?.AddCurrency(300, 0); // 签到获取300金币
                RefreshTopBar();
                ShowToast($"新兵签到成功！金币 +300");
                MarkRecruitDayClaimed(day);
                ShowEventsModal();
            }
        }, canClaim ? ButtonTone.Gold : ButtonTone.Secondary, 9);
        button.Disabled = !canClaim || claimed;
        button.CustomMinimumSize = new Vector2(0, 18);
        box.AddChild(button);

        return card;
    }

    Control CreateEventTaskRow(string title, string reward, string progressText, bool claimed, bool canClaim, string bgPath)
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(0, 60), ClipContents = true };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);

        if (!string.IsNullOrEmpty(bgPath) && ResourceLoader.Exists(bgPath))
        {
            var bgTex = new TextureRect
            {
                Texture = ResourceLoader.Load<Texture2D>(bgPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SelfModulate = new Color(1f, 1f, 1f, 0.22f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            bgTex.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.AddChild(bgTex);
        }

        var row = AddHBox(panel, "EventTaskRowBox", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 8;
        row.OffsetBottom = -8;

        var info = new VBoxContainer { Name = "EventTaskInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        info.AddChild(AddLabel(title, 13, PanelText, HorizontalAlignment.Left));

        var metaRow = new HBoxContainer { Name = "EventTaskMetaRow" };
        metaRow.AddThemeConstantOverride("separation", 8);
        metaRow.AddChild(AddLabel(progressText, 11, canClaim ? GoodText : MutedText, HorizontalAlignment.Left));
        metaRow.AddChild(AddLabel(reward, 11, new Color(0.95f, 0.76f, 0.24f), HorizontalAlignment.Left));
        info.AddChild(metaRow);
        row.AddChild(info);

        var eventKey = "EventTask_" + title;
        var isClaimedLocally = IsLocalTaskClaimed(eventKey) || claimed;

        var button = AddButton(isClaimedLocally ? "已领" : canClaim ? "领取" : "未达成", () => {
            if (canClaim && !isClaimedLocally)
            {
                GameState.Instance?.AddCurrency(500, 0); // 活动领取奖励500金币
                RefreshTopBar();
                ShowToast($"活动挑战成功！金币 +500");
                MarkLocalTaskClaimed(eventKey);
                ShowEventsModal();
            }
        }, canClaim && !isClaimedLocally ? ButtonTone.Gold : ButtonTone.Secondary, 12);
        
        button.Disabled = isClaimedLocally || !canClaim;
        button.CustomMinimumSize = new Vector2(66, 30);
        row.AddChild(button);

        return panel;
    }
}
