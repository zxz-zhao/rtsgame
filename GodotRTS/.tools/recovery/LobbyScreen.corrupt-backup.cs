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

    sealed class ModeCardUi
    {
        public Label TitleLabel { get; init; } = null!;
        public Label DescLabel { get; init; } = null!;
        public Label ActionLabel { get; init; } = null!;
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
    const string CustomRoomMode = "自定义房间";
    const string GlobalConquestMode = "全球争霸";
    const int MaxCustomRoomPlayers = 6;
    static readonly int[] CustomRoomPlayerCounts = { 2, 4, 6 };

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
        _ = NetClient.Instance?.PostPresence() ?? Task.CompletedTask;
        _ = WarmSecondaryModalCaches();
    }

    public override void _Process(double delta)
    {
        UpdateTechTimer();

        if (string.IsNullOrEmpty(GameState.Instance?.Token))
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
        commanderPortraitButton.TooltipText = "鏌ョ湅澶村儚涓庡啗琛?;
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
            Texture = LoadTexture(LocalCommanderAvatarTexturePath())
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(UnityLobbyRoot + "WW2Portraits/officer_avatar_01_field_commander.png")
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

        playerLabel = AddLabel("娓稿鎸囨尌瀹?, 20, PanelText, HorizontalAlignment.Left);
        playerLabel.CustomMinimumSize = new Vector2(220, 0);
        commanderText.AddChild(playerLabel);

        var rankRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin
        };
        rankRow.AddThemeConstantOverride("separation", 6);
        commanderText.AddChild(rankRow);

        commanderRankIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(18, 18),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = LoadTexture(LobbyRankBadgeTexturePath(GameState.Instance?.RankTitle))
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(UnityLobbyRoot + "WW2RankBadges/rank_badge_01_bronze_shield.png")
        };
        rankRow.AddChild(commanderRankIcon);

        rankLabel = AddLabel("Lv.1  路  鍒楀叺", 13, WarningText, HorizontalAlignment.Left);
        rankLabel.CustomMinimumSize = new Vector2(150, 0);
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

        row.AddChild(CreateTopBarIconDock(IconRoot + "icon_bell_transparent.svg", "鍏憡", ShowAnnouncementModal));
        row.AddChild(CreateTopBarIconDock(IconRoot + "icon_gear_transparent.svg", "璁剧疆", ShowSettingsModal));
    }

    void BuildFriendsPanel()
    {
        var panel = AddPanel("FriendsPanel", new Rect2(0.020f, 0.145f, 0.240f, 0.725f), GlassPanel, 2);
        var box = AddVBox(panel, "FriendsBox", 8, new Vector2(14, 14), new Vector2(-14, -14));

        box.AddChild(AddSectionTitle("濂藉弸"));
        friendStatusLabel = AddLabel("濂藉弸鍔犺浇涓?..", 13, MutedText, HorizontalAlignment.Left);
        box.AddChild(friendStatusLabel);

        var addRow = new HBoxContainer { Name = "AddFriendRow" };
        addRow.AddThemeConstantOverride("separation", 6);
        addFriendInput = new LineEdit
        {
            Name = "AddFriendInput",
            PlaceholderText = "杈撳叆鏄电О",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        addRow.AddChild(addFriendInput);
        addRow.AddChild(AddButton("娣诲姞", () => _ = AddFriend(), ButtonTone.Primary, 13));
        box.AddChild(addRow);

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

        box.AddChild(AddButton("鍒锋柊濂藉弸 / 閭€璇?, () => _ = RefreshFriends(), ButtonTone.Secondary, 13));
    }

    void PrepareFriendsPanelWarmState()
    {
        if (!IsLive(friendRows) || !IsLive(friendStatusLabel))
            return;

        if (HasFriendsCache())
        {
            RenderFriends(cachedFriendsData, true, "宸叉樉绀轰笂娆″ソ鍙嬬紦瀛橈紝姝ｅ湪鍒锋柊...");
            return;
        }

        ResetFriendsPanel("姝ｅ湪鍚屾濂藉弸...");
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
        AddModeCard("ModeMatch", new Rect2(0.295f, 0.245f, 0.429f, 0.152f),
            "01", QuickMatchMode, "鏍囧噯鍦板浘蹇€熷尮閰?, "绔嬪嵆鍖归厤", new Color(0.86f, 0.66f, 0.24f), () => _ = TriggerQuickMatchCard());
        AddModeCard("ModeCustom", new Rect2(0.295f, 0.482f, 0.429f, 0.146f),
            "02", CustomRoomMode, "鍙€夊湴鍥句笌浜烘暟锛屽垱寤恒€佸姞鍏ュ苟閭€璇峰ソ鍙?, "鎵撳紑鎴块棿", new Color(0.68f, 0.83f, 0.92f), () => OpenRoomPanel());
        AddModeCard("ModeGlobal", new Rect2(0.295f, 0.676f, 0.429f, 0.146f),
            "03", GlobalConquestMode, "鍏堥€夋湰灞€涓诲姏鍏电锛岃繘鍏ュ悗鏈眬涓嶅彲鏇存敼", "閫夋嫨鍏电鍐嶈繘鍏?, new Color(0.80f, 0.58f, 0.28f), () => _ = StartGlobalConquest());

        matchSelection = AddModeSelection("MatchSelection", new Rect2(0.295f, 0.245f, 0.429f, 0.152f));
        customSelection = AddModeSelection("CustomSelection", new Rect2(0.295f, 0.482f, 0.429f, 0.146f));
        globalSelection = AddModeSelection("GlobalSelection", new Rect2(0.295f, 0.676f, 0.429f, 0.146f));
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
        textBox.AddThemeConstantOverride("separation", 2);
        textMargin.AddChild(textBox);

        var titleLabel = AddLabel(title, 23, PanelText, HorizontalAlignment.Left);
        textBox.AddChild(titleLabel);
        var descLabel = AddLabel(desc, 13, MutedText, HorizontalAlignment.Left);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
            CustomMinimumSize = new Vector2(188, 30),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
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
        actionMargin.AddThemeConstantOverride("margin_top", 3);
        actionMargin.AddThemeConstantOverride("margin_right", 10);
        actionMargin.AddThemeConstantOverride("margin_bottom", 3);
        actionDock.AddChild(actionMargin);

        var actionRow = new HBoxContainer
        {
            Name = name + "ActionRow",
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
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
        actionLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        actionRow.AddChild(actionLabel);

        var numberDock = new Panel
        {
            Name = name + "NumberDock",
            CustomMinimumSize = new Vector2(78, 0),
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
            ActionLabel = actionLabel
        };
    }

    void BuildRightPanels()
    {
        var taskPanel = AddPanel("TasksPanel", new Rect2(0.746f, 0.145f, 0.238f, 0.455f), GlassPanel, 2);
        var taskBox = AddVBox(taskPanel, "TaskBox", 8, new Vector2(14, 14), new Vector2(-14, -14));
        taskBox.AddChild(AddSectionTitle("姣忔棩浠诲姟"));
        taskStatusLabel = AddLabel("浠诲姟鍔犺浇涓?..", 13, MutedText, HorizontalAlignment.Left);
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
        taskBox.AddChild(AddButton("鏌ョ湅鍏ㄩ儴浠诲姟", () => _ = ShowTaskModal(), ButtonTone.Secondary, 13));
        BuildLocalTasks("鏈湴浠诲姟棰勮");

        var techPanel = AddPanel("TechPanel", new Rect2(0.746f, 0.618f, 0.238f, 0.255f), GlassPanel, 2);
        var techBox = AddVBox(techPanel, "TechBox", 7, new Vector2(14, 12), new Vector2(-14, -12));
        techBox.AddChild(AddSectionTitle("绉戞妧鐮旂┒"));
        techTitleLabel = AddLabel("鏆傛棤杩涜涓殑鐮旂┒", 15, PanelText, HorizontalAlignment.Left);
        techDescLabel = AddLabel("鐐瑰嚮鐮旂┒寮€濮嬫湇鍔″櫒璁℃椂銆?, 12, MutedText, HorizontalAlignment.Left);
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
        techStartButton = AddButton("鐮旂┒", () => _ = StartTechResearch(), ButtonTone.Primary, 13);
        techSpeedButton = AddButton("鍔犻€?, () => _ = SpeedUpTech(), ButtonTone.Gold, 13);
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
        row.AddChild(AddButton("鍟嗗簵", () => ShowShopModal(), ButtonTone.Secondary, 15));
        row.AddChild(AddButton("浠撳簱", () => ShowWarehouseModal(), ButtonTone.Secondary, 15));
        row.AddChild(AddButton("鎴樺焦", () => _ = StartCampaign(), ButtonTone.Primary, 15));
        row.AddChild(AddButton("鎺掕姒?, () => _ = ShowLeaderboardModal(), ButtonTone.Gold, 15));
        row.AddChild(AddButton("閭欢", () => _ = ShowMailModal(), ButtonTone.Secondary, 15));
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
        modalTitle = AddLabel("鑷畾涔夋埧闂?, 22, PanelText, HorizontalAlignment.Left);
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
        modalTitle.Text = "鑷畾涔夋埧闂?;
        ClearChildren(modalBody);

        var createPanel = new Panel { Name = "RoomCreatePanel", CustomMinimumSize = new Vector2(0, 122) };
        MetalUiStyle.ApplyMetalPanel(createPanel, MetalUiStyle.Steel, 1, 8, 4);
        var createBox = AddVBox(createPanel, "RoomCreateBox", 7, new Vector2(12, 10), new Vector2(-12, -10));
        var row = new HBoxContainer { Name = "RoomCreateRow" };
        row.AddThemeConstantOverride("separation", 8);
        roomNameInput = new LineEdit
        {
            Name = "RoomNameInput",
            PlaceholderText = "鎴块棿鍚嶏紙鍙€夛級",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        roomMapPicker = new OptionButton { Name = "RoomMapPicker", CustomMinimumSize = new Vector2(150, 0) };
        foreach (var map in BattleMapCatalog.GetCustomRoomMapNames())
            roomMapPicker.AddItem(map);
        roomPlayerCountPicker = new OptionButton { Name = "RoomPlayerCountPicker", CustomMinimumSize = new Vector2(110, 0) };
        foreach (var count in CustomRoomPlayerCounts)
        {
            var teamSize = Math.Max(1, count / 2);
            roomPlayerCountPicker.AddItem($"{teamSize}v{teamSize}", count);
        }
        row.AddChild(roomNameInput);
        row.AddChild(roomMapPicker);
        row.AddChild(roomPlayerCountPicker);
        row.AddChild(AddButton("鍒涘缓", () => _ = CreateRoom(), ButtonTone.Primary, 13));
        row.AddChild(AddButton("鍒锋柊", () => _ = RefreshRooms(), ButtonTone.Secondary, 13));
        createBox.AddChild(row);
        createBox.AddChild(AddLabel("鑷畾涔夋埧闂村彧鏄剧ず灏忓湴鍥撅紝鏈€楂樻敮鎸?3v3 缁勬埧锛涘叏鐞冧簤闇哥嫭鍗犲ぇ鍦板浘銆傚綋鍓嶅疄鏃跺紑鎴樺叆鍙ｄ粛鍏堟寜 1v1 鎺ュ叆銆?, 12, WarningText, HorizontalAlignment.Left));
        roomStatusLabel = AddLabel("鎴块棿鍔犺浇涓?..", 13, MutedText, HorizontalAlignment.Left);
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
            taskStatusLabel.Text = "浠诲姟鍚屾涓?..";

        var data = await NetClient.Instance.GetLobby();
        if (!data.GetBool("success"))
        {
            taskStatusLabel.Text = "绂荤嚎妯″紡锛氭樉绀烘湰鍦颁换鍔℃牱渚?;
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
            BuildLocalTasks("鏆傛棤鑱旂綉浠诲姟锛屾樉绀烘湰鍦颁换鍔℃牱渚?);
            return;
        }

        taskStatusLabel.Text = $"宸插悓姝?{tasks.Count} 涓换鍔?;
        foreach (var item in tasks)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var task = item.AsGodotDictionary();
            var taskId = task.GetString("id");
            var title = FriendlyText(task.GetString("title"), taskId switch
            {
                "daily_login" => "姣忔棩鐧诲綍",
                "win3" => "璧㈠緱 3 鍦烘垬鏂?,
                "destroy20" => "鎽ф瘉 20 涓晫鏂瑰崟浣?,
                _ => "浣滄垬浠诲姟"
            });
            var cur = task.GetInt("cur");
            var max = Math.Max(1, task.GetInt("max", 1));
            var claimed = task.GetBool("claimed");
            var canClaim = task.GetBool("canClaim");
            taskRows.AddChild(MakeTaskRow(taskId, title, cur, max, claimed, canClaim));
        }
    }

    void BuildLocalTasks(string statusText = "绂荤嚎妯″紡锛氭樉绀烘湰鍦颁换鍔℃牱渚?)
    {
        taskStatusLabel.Text = statusText;
        ClearChildren(taskRows);
        taskRows.AddChild(MakeTaskRow("daily_login", "姣忔棩鐧诲綍", 1, 1, false, false));
        taskRows.AddChild(MakeTaskRow("win3", "璧㈠緱 3 鍦烘垬鏂?, 0, 3, false, false));
        taskRows.AddChild(MakeTaskRow("destroy20", "鎽ф瘉 20 涓晫鏂瑰崟浣?, 0, 20, false, false));
    }

    Control MakeTaskRow(string taskId, string title, int cur, int max, bool claimed, bool canClaim)
    {
        var panel = new Panel { Name = "TaskRow_" + taskId, CustomMinimumSize = new Vector2(0, 54) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);
        var row = AddHBox(panel, "TaskRowBox", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 7;
        row.OffsetBottom = -7;

        var info = new VBoxContainer { Name = "TaskInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        info.AddChild(AddLabel(title, 13, PanelText, HorizontalAlignment.Left));
        info.AddChild(AddLabel($"{cur}/{max}", 11, canClaim ? GoodText : MutedText, HorizontalAlignment.Left));
        row.AddChild(info);

        var button = AddButton(claimed ? "宸查" : canClaim ? "棰嗗彇" : "鏈揪鎴?, () => _ = ClaimTask(taskId), canClaim ? ButtonTone.Gold : ButtonTone.Secondary, 12);
        button.Disabled = claimed || !canClaim;
        button.CustomMinimumSize = new Vector2(66, 30);
        row.AddChild(button);
        return panel;
    }

    async Task ClaimTask(string taskId)
    {
        if (NetClient.Instance is null)
            return;

        var data = await NetClient.Instance.ClaimTask(taskId);
        if (!data.GetBool("success"))
        {
            ShowToast("浠诲姟棰嗗彇澶辫触锛? + FriendlyText(data.GetString("error"), "杩涘害涓嶈冻鎴栧凡棰嗗彇"));
            return;
        }

        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        ShowToast("濂栧姳宸查鍙?);
        await RefreshLobbyData(false);
        if (activeFeatureModal == "tasks" && modalPanel.Visible && string.Equals(modalTitle.Text, "鍏ㄩ儴浠诲姟", StringComparison.Ordinal))
            await ShowTaskModal();
    }

    async Task ShowTaskModal()
    {
        activeFeatureModal = "tasks";
        OpenFeatureModal("鍏ㄩ儴浠诲姟", "鏌ョ湅褰撳墠璐﹀彿鐨勬瘡鏃ヤ换鍔¤繘搴︺€佸畬鎴愮姸鎬佷笌濂栧姳棰嗗彇鍏ュ彛銆?);
        modalBody.AddChild(AddLabel("浠诲姟鍚屾涓?..", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();

        Godot.Collections.Dictionary data;
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetJson("/api/lobby", SecondaryModalRequestTimeoutSec);
        else
            data = new Godot.Collections.Dictionary();

        var tasks = data.GetArray("tasks");
        var usingLiveData = data.GetBool("success") && tasks.Count > 0;

        OpenFeatureModal("鍏ㄩ儴浠诲姟", "鏌ョ湅褰撳墠璐﹀彿鐨勬瘡鏃ヤ换鍔¤繘搴︺€佸畬鎴愮姸鎬佷笌濂栧姳棰嗗彇鍏ュ彛銆?);
        AddModalSummaryRow(
            AddStatCard("浠诲姟鎬绘暟", $"{(usingLiveData ? tasks.Count : 3)}", usingLiveData ? "褰撳墠鍚屾浠诲姟" : "绂荤嚎鏍蜂緥浠诲姟", WarningText),
            AddStatCard("鍙鍙?, $"{CountClaimableTasks(tasks, usingLiveData)}", "宸插畬鎴愬緟棰嗗彇", GoodText),
            AddStatCard("妯″紡", usingLiveData ? "瀹炴椂鏁版嵁" : "绂荤嚎棰勮", usingLiveData ? "鏈嶅姟鍣ㄥ凡鍚屾" : "鏈嶅姟鍣ㄤ笉鍙敤鏃跺睍绀?, PanelText));

        var section = AddModalSectionPanel(
            "浠诲姟鍒楄〃",
            usingLiveData ? $"褰撳墠鍏?{tasks.Count} 涓换鍔★紝鐐瑰嚮棰嗗彇浼氱珛鍗冲埛鏂般€? : "褰撳墠鏄剧ず鏈湴浠诲姟鏍蜂緥锛岃仈缃戝悗浼氭浛鎹负鐪熷疄鏁版嵁銆?,
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
                    "daily_login" => "姣忔棩鐧诲綍",
                    "win3" => "璧㈠緱 3 鍦烘垬鏂?,
                    "destroy20" => "鎽ф瘉 20 涓晫鏂瑰崟浣?,
                    _ => "浣滄垬浠诲姟"
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
            section.AddChild(MakeTaskRow("daily_login", "姣忔棩鐧诲綍", 1, 1, false, false));
            section.AddChild(MakeTaskRow("win3", "璧㈠緱 3 鍦烘垬鏂?, 0, 3, false, false));
            section.AddChild(MakeTaskRow("destroy20", "鎽ф瘉 20 涓晫鏂瑰崟浣?, 0, 20, false, false));
        }

        var actionRow = new HBoxContainer { Name = "TaskModalActions" };
        actionRow.AddThemeConstantOverride("separation", 8);
        var helper = AddLabel("涓婚潰鏉跨幇鍦ㄧ敤浜庡揩閫熼瑙堬紱瀹屾暣浠诲姟鎿嶄綔缁熶竴鏀惧湪杩欓噷銆?, 12, MutedText, HorizontalAlignment.Left);
        helper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        actionRow.AddChild(helper);
        actionRow.AddChild(AddButton("鍒锋柊浠诲姟", () => _ = ShowTaskModal(), ButtonTone.Secondary, 12));
        modalBody.AddChild(actionRow);

        ShowModal();
    }

    static int CountClaimableTasks(Godot.Collections.Array tasks, bool usingLiveData)
    {
        if (!usingLiveData)
            return 0;

        var count = 0;
        foreach (var item in tasks)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            if (item.AsGodotDictionary().GetBool("canClaim"))
                count++;
        }
        return count;
    }

    void UpdateTechPanel(Godot.Collections.Dictionary data)
    {
        var techName = FriendlyText(data.GetString("techName"), "");
        techEndAtMs = data.GetLong("techEndAt");
        techTotalSec = Math.Max(1, data.GetInt("techTotalSec", 1));
        if (!string.IsNullOrEmpty(techName) && techEndAtMs > NowMs())
        {
            techTitleLabel.Text = techName;
            techDescLabel.Text = FriendlyText(data.GetString("techDesc"), "鐮旂┒杩涜涓紝瀹屾垚鍚庡湪鎴樻枟涓敓鏁堛€?);
            techStartButton.Visible = false;
            techSpeedButton.Visible = true;
        }
        else
        {
            techEndAtMs = 0;
            techTitleLabel.Text = "鏆傛棤杩涜涓殑鐮旂┒";
            techDescLabel.Text = "鐐瑰嚮鐮旂┒寮€濮嬫湇鍔″櫒璁℃椂銆?;
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
        techTimerLabel.Text = left <= 0 ? "鐮旂┒瀹屾垚锛岀瓑寰呭埛鏂? : "鍓╀綑锛? + FormatDuration(left / 1000);
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
            ShowToast("鐮旂┒澶辫触锛? + FriendlyText(data.GetString("error"), "宸叉湁鐮旂┒鎴栫綉缁滀笉鍙敤"));
            return;
        }
        ShowToast("绉戞妧鐮旂┒宸插紑濮?);
        await RefreshLobbyData(false);
    }

    async Task SpeedUpTech()
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.SpeedUpTech();
        if (!data.GetBool("success"))
        {
            ShowToast("鍔犻€熷け璐ワ細" + FriendlyText(data.GetString("error"), "閽荤煶涓嶈冻鎴栨棤鐮旂┒"));
            return;
        }
        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        ShowToast("宸叉秷鑰楅捇鐭冲姞閫?10 鍒嗛挓");
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

        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            ResetFriendsPanel("璇峰厛鐧诲綍鍚庝娇鐢ㄥソ鍙嬬郴缁?);
            return;
        }

        if (!silent)
            friendStatusLabel.Text = HasFriendsCache() ? "宸叉樉绀虹紦瀛橈紝姝ｅ湪鍒锋柊濂藉弸..." : "姝ｅ湪鍚屾濂藉弸...";

        var data = await NetClient.Instance.GetFriends(FriendsRequestTimeoutSec);
        if (!data.GetBool("success"))
        {
            if (HasFriendsCache())
            {
                RenderFriends(cachedFriendsData, true, silent ? "" : "缃戠粶杈冩參锛氬凡鏄剧ず涓婃濂藉弸缂撳瓨");
                return;
            }

            if (!silent)
                ResetFriendsPanel("绂荤嚎妯″紡锛氭樉绀哄ソ鍙嬫牱渚?);
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
                ? "鏆傛棤濂藉弸锛屽彲浠ユ坊鍔犱竴浣?
                : usingCache
                    ? $"宸叉樉绀?{friends.Count} 浣嶅ソ鍙嬬紦瀛?
                    : $"宸插姞杞?{friends.Count} 浣嶅ソ鍙?;

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
                FriendlyText(friend.GetString("rankTitle"), FriendlyText(friend.GetString("rank"), "濂藉弸")),
                FriendlyText(friend.GetString("status"), "鍦ㄧ嚎"),
                friend.GetInt("level", 1)));
        }

        if (friends.Count == 0)
            AddLocalFriendRows();
    }

    void AddLocalFriendRows()
    {
        friendRows.AddChild(MakeFriendRow("IronWolf", "榛勯噾鎸囨尌瀹?, "鍦ㄧ嚎", 18));
        friendRows.AddChild(MakeFriendRow("SeaHammer", "鐧介摱绐佸嚮闃?, "鍖归厤涓?, 14));
        friendRows.AddChild(MakeFriendRow("DesertFox", "瑁呯敳鍏堥攱", "绂荤嚎", 9));
    }

    Control MakeFriendRow(string username, string rank, string status, int level)
    {
        var panel = new Panel { Name = "Friend_" + username, CustomMinimumSize = new Vector2(0, 58) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);
        var row = AddHBox(panel, "FriendRow", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 6;
        row.OffsetBottom = -6;

        row.AddChild(CreateFriendAvatar(username, status));

        var info = new VBoxContainer
        {
            Name = "FriendInfo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        info.AddThemeConstantOverride("separation", 1);
        info.AddChild(AddLabel(username, 13, PanelText, HorizontalAlignment.Left));

        var rankRow = new HBoxContainer
        {
            Name = "FriendRankRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        rankRow.AddThemeConstantOverride("separation", 4);

        var levelLabel = AddLabel($"Lv.{level}", 11, MutedText, HorizontalAlignment.Left);
        levelLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        rankRow.AddChild(levelLabel);

        rankRow.AddChild(CreateFriendRankBadge(rank));

        var rankLabel = AddLabel(NormalizeRankTitle(rank), 11, LobbyRankColor(rank), HorizontalAlignment.Left);
        rankLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rankRow.AddChild(rankLabel);

        info.AddChild(rankRow);
        row.AddChild(info);

        var statusLabel = AddLabel(status, 12, StatusColor(status), HorizontalAlignment.Right);
        statusLabel.CustomMinimumSize = new Vector2(52, 0);
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
            CustomMinimumSize = new Vector2(40, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };

        var disc = new Panel
        {
            Position = new Vector2(4, 3),
            Size = new Vector2(32, 32),
            MouseFilter = MouseFilterEnum.Ignore
        };
        disc.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.018f, 0.024f, 0.028f, 0.90f),
            BorderColor = new Color(1f, 1f, 1f, 0.10f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16
        });
        holder.AddChild(disc);

        var frame = new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(46, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(UnityLobbyRoot + "gen_portrait_ring.png")
        };
        PortraitFrameUtils.ApplyWrapFrame(
            frame,
            new Color(accent.R, accent.G, accent.B, 0.96f),
            new Color(1f, 0.96f, 0.84f, 0.90f),
            0.49f,
            0.018f,
            0.0055f,
            0.012f);
        holder.AddChild(frame);

        var avatar = new TextureRect
        {
            Position = new Vector2(3, 3),
            Size = new Vector2(36, 36),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = LoadTexture(ResolveFriendAvatarTexturePath(username))
        };
        PortraitFrameUtils.ApplyCircularPortrait(
            avatar,
            new Color(accent.R, accent.G, accent.B, 1f),
            new Color(1f, 0.95f, 0.80f, 1f),
            0.47f,
            0.008f,
            0.012f,
            new Vector2(1.30f, 1.30f),
            new Vector2(0.01f, -0.025f));
        holder.AddChild(avatar);
        return holder;
    }

    async Task AddFriend()
    {
        var name = addFriendInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowToast("璇疯緭鍏ュソ鍙嬫樀绉?);
            return;
        }

        if (NetClient.Instance is null || GameState.Instance?.IsGuest == true)
        {
            ShowToast("娓稿妯″紡鏆備笉鏀寔娣诲姞濂藉弸");
            return;
        }

        friendStatusLabel.Text = "娣诲姞涓?..";
        var data = await NetClient.Instance.AddFriend(name);
        if (!data.GetBool("success"))
        {
            friendStatusLabel.Text = "娣诲姞澶辫触";
            ShowToast(FriendlyText(data.GetString("error"), "鎵句笉鍒拌鐜╁"));
            return;
        }

        addFriendInput.Text = "";
        ShowToast($"宸叉坊鍔?{name}");
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
                roomStatusLabel.Text = "璇峰厛鐧诲綍鍚庡啀浣跨敤鎴块棿鍔熻兘";
            AddRoomPlaceholder("璇峰厛鐧诲綍鍚庡啀浣跨敤鎴块棿鍔熻兘锛岀櫥褰曞悗鍙埛鏂般€佸垱寤哄拰閭€璇峰ソ鍙嬨€?);
            return;
        }

        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "鍔犺浇涓?..";
        var data = await NetClient.Instance.GetRooms();
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "鎴块棿鏈嶅姟涓嶅彲鐢?;
            AddRoomPlaceholder("鏈嶅姟鍣ㄦ湭杩炴帴锛岀◢鍚庨噸璇曘€?);
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
            roomStatusLabel.Text = visibleRooms.Count == 0 ? "鏆傛棤鎴块棿锛屽彲浠ュ垱寤轰竴涓? : $"鎵惧埌 {visibleRooms.Count} 涓埧闂?;
        if (visibleRooms.Count == 0)
            AddRoomPlaceholder("鏆傛棤绛夊緟涓殑鎴块棿銆?);

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
        var roomName = FriendlyText(room.GetString("name"), "鎴块棿");
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
        info.AddChild(AddLabel(isActiveRoom ? $"褰撳墠鎴块棿锛歿roomName}" : roomName, 15, isActiveRoom ? GoodText : PanelText, HorizontalAlignment.Left));
        var modeHint = DescribeRoomModeHint(maxPlayers);
        info.AddChild(AddLabel($"{mapName}  {count}/{maxPlayers}  {modeHint}  {FriendlyText(room.GetString("status"), "waiting")}", 12, isActiveRoom ? GoodText : MutedText, HorizontalAlignment.Left));
        row.AddChild(info);

        var invite = AddButton("閭€璇?, () => ShowInvitePanel(roomId), ButtonTone.Secondary, 12);
        invite.Disabled = string.IsNullOrEmpty(roomId);
        row.AddChild(invite);
        var join = AddButton(isActiveRoom ? "杩涘叆" : "鍔犲叆", () =>
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

    async Task CreateRoom()
    {
        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            ShowToast("璇峰厛鐧诲綍鍚庡啀鍒涘缓鎴块棿");
            return;
        }

        var mapName = BattleMapCatalog.DefaultMapName;
        if (roomMapPicker is not null && roomMapPicker.ItemCount > 0)
            mapName = BattleMapCatalog.NormalizeCustomRoomMap(roomMapPicker.GetItemText(roomMapPicker.Selected));
        var roomName = roomNameInput?.Text.Trim() ?? "";
        var maxPlayers = SelectedRoomPlayerCount();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "鍒涘缓涓?..";

        var data = await NetClient.Instance.CreateRoom(mapName, roomName, maxPlayers);
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "鍒涘缓澶辫触";
            ShowToast(FriendlyText(data.GetString("error"), "鍒涘缓澶辫触"));
            return;
        }

        activeRoomId = data.GetString("roomId");
        activeRoomMap = data.GetString("mapName", mapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", maxPlayers);
        activeRoomPlayerCount = data.GetInt("playerCount", 1);
        selectedMap = activeRoomMap;
        GameState.Instance?.SelectMap(activeRoomMap, CustomRoomMode);
        GameState.Instance?.SetCurrentRoom(activeRoomId);
        ShowToast("鎴块棿宸插垱寤?);
        await RefreshRooms();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = activeRoomMaxPlayers <= 2
                ? $"鎴块棿宸插垱寤猴細{activeRoomMap}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}銆傚彲浠ラ個璇峰ソ鍙嬶紝骞剁洿鎺ヨ繘鍏ユ垬鏂椼€?
                : $"鎴块棿宸插垱寤猴細{activeRoomMap}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}銆傚綋鍓嶆埧闂存渶楂樻敮鎸?3v3 缁勯槦锛屽彲缁х画閭€璇锋垚鍛橀泦鍚堛€?;
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
        info.AddChild(AddLabel("宸茶繘鍏ユ埧闂?, 15, GoodText, HorizontalAlignment.Left));
        info.AddChild(AddLabel($"{FriendlyText(activeRoomMap, BattleMapCatalog.DefaultMapName)}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}  ID:{ShortRoomId(activeRoomId)}", 12, MutedText, HorizontalAlignment.Left));
        row.AddChild(info);
        row.AddChild(AddButton("閭€璇峰ソ鍙?, () => ShowInvitePanel(activeRoomId), ButtonTone.Gold, 12));
        row.AddChild(AddButton("绂诲紑鎴块棿", () => _ = LeaveActiveRoom(), ButtonTone.Secondary, 12));
        var enterButton = AddButton(
            activeRoomMaxPlayers <= 2 ? "杩涘叆鎴樻枟" : "3v3缁勬埧涓?,
            () => _ = EnterRoomBattle(activeRoomId, activeRoomMap, activeRoomMaxPlayers),
            ButtonTone.Primary,
            12);
        enterButton.Disabled = activeRoomMaxPlayers > 2;
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
            roomStatusLabel.Text = "鍔犲叆涓?..";
        var data = await NetClient.Instance.JoinRoom(roomId);
        if (!data.GetBool("success"))
        {
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = "鍔犲叆澶辫触";
            ShowToast(FriendlyText(data.GetString("error"), "鎴块棿涓嶅彲鍔犲叆"));
            await RefreshRooms();
            return;
        }

        activeRoomId = data.GetString("roomId", roomId);
        activeRoomMap = data.GetString("mapName", BattleMapCatalog.DefaultMapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", 2);
        activeRoomPlayerCount = data.GetInt("playerCount", 2);
        GameState.Instance?.SetCurrentRoom(activeRoomId);
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
            roomStatusLabel.Text = $"宸插姞鍏?{activeRoomPlayerCount}/{activeRoomMaxPlayers} 鎴块棿锛屽彲缁х画閭€璇锋垚鍛橈紱褰撳墠鏈€澶氭敮鎸?3v3 缁勬埧锛屽疄鏃跺紑鎴樺叆鍙ｄ粛鍏堟寜 1v1 鎺ュ叆銆?;
        ShowToast("宸插姞鍏ユ埧闂?);
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
            roomStatusLabel.Text = "绂诲紑鎴块棿涓?..";
        if (NetClient.Instance is not null)
            await NetClient.Instance.LeaveRoom(roomId);

        ClearActiveRoomState();
        await RefreshRooms();
        if (IsLive(roomStatusLabel))
            roomStatusLabel.Text = "宸茬寮€鎴块棿锛屽彲浠ラ噸鏂伴€夋嫨鍦板浘鍜屼汉鏁般€?;
        ShowToast("宸茬寮€鎴块棿");
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
        if (!SupportsRealtimeRoomBattle(maxPlayers))
        {
            ShowToast("褰撳墠鎴块棿鍏堢敤浜?3v3 缁勯槦閭€璇凤紝瀹炴椂寮€鎴樺叆鍙ｄ粛鍏堟寜 1v1 鎺ュ叆銆?);
            if (IsLive(roomStatusLabel))
                roomStatusLabel.Text = $"宸插姞鍏?{Math.Max(2, maxPlayers)} 浜烘埧闂达紝鍙户缁個璇锋垚鍛樺苟闆嗗悎锛涘綋鍓嶅疄鏃跺紑鎴樺叆鍙ｄ粛鍏堟寜 1v1 鎺ュ叆銆?;
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
        modalTitle.Text = "閭€璇峰ソ鍙?;
        ClearChildren(modalBody);
        modalBody.AddChild(AddLabel(string.IsNullOrEmpty(roomId) ? "璇峰厛鍒涘缓鎴栭€夋嫨鎴块棿銆? : $"鎴块棿锛歿roomId}", 13, MutedText, HorizontalAlignment.Left));
        if (string.IsNullOrEmpty(roomId))
        {
            ShowModal();
            return;
        }

        if (loadedFriendNames.Count == 0)
        {
            modalBody.AddChild(AddLabel("鏆傛棤鍙個璇峰ソ鍙嬶紝鍏堝湪宸︿晶娣诲姞濂藉弸鎴栧埛鏂板垪琛ㄣ€?, 14, MutedText, HorizontalAlignment.Left));
            modalBody.AddChild(AddButton("鍒锋柊濂藉弸", () => _ = RefreshFriends(), ButtonTone.Secondary, 13));
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
                row.AddChild(AddButton("鍙戦€侀個璇?, () => _ = SendInvite(friendName, roomId), ButtonTone.Gold, 13));
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
            ? $"宸查個璇?{friendName}"
            : "閭€璇峰け璐ワ細" + FriendlyText(data.GetString("error"), "濂藉弸涓嶅湪绾挎垨鎴块棿涓嶅彲鐢?));
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
        modalTitle.Text = "鎴块棿閭€璇?;
        ClearChildren(modalBody);
        var inviteId = invite.GetString("id");
        if (!string.IsNullOrEmpty(inviteId))
            surfacedInviteIds.Add(inviteId);
        var roomId = invite.GetString("roomId");
        var mapName = FriendlyText(invite.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var maxPlayers = invite.GetInt("maxPlayers", 2);
        var playerCount = invite.GetInt("playerCount", 0);
        var roomSummary = $"{Math.Max(0, playerCount)}/{Math.Max(2, maxPlayers)}";
        modalBody.AddChild(AddLabel($"{FriendlyText(invite.GetString("from"), "濂藉弸")} 閭€璇蜂綘鍔犲叆 {mapName}  {roomSummary}", 17, PanelText, HorizontalAlignment.Left));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(AddButton("鎺ュ彈", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 14));
        row.AddChild(AddButton("鎷掔粷", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 14));
        modalBody.AddChild(row);
        ShowModal();
    }

    async Task RespondInvite(string inviteId, bool accept, string fallbackRoomId, string fallbackMap)
    {
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.RespondInvite(inviteId, accept);
        invitesCacheAtMs = 0;
        if (!data.GetBool("success"))
        {
            ShowToast("閭€璇峰凡澶辨晥");
            CloseModal();
            return;
        }
        if (!accept)
        {
            ShowToast("宸叉嫆缁濋個璇?);
            CloseModal();
            return;
        }

        await JoinRoom(data.GetString("roomId", fallbackRoomId));
    }

    void ShowShopModal()
    {
        OpenFeatureModal("鍐涢渶鍟嗗簵", "琛ョ粰銆佸姞閫熶笌鍏ㄧ悆浜夐湼鍐涘鍖呬細鍦ㄨ繖閲岀粺涓€璐拱銆?);
        AddModalSummaryRow(
            AddStatCard("閲戝竵", $"{GameState.Instance?.Gold ?? 0}", "甯歌璧勬簮", WarningText),
            AddStatCard("閽荤煶", $"{GameState.Instance?.Gems ?? 0}", "楂樼骇璐у竵", new Color(0.64f, 0.90f, 1f)),
            AddStatCard("浠婃棩鐗规儬", "4 浠?, "鍙喘涔板晢鍝?, GoodText));

        modalBody.AddChild(CreateShopHeroBanner());

        var list = AddModalSectionPanel("鍐涢渶绮鹃€?, "缁熶竴璐拱琛ョ粰銆佸姞閫熼亾鍏峰拰鍏ㄧ悆浜夐湼鍐涘鍖呫€?, WarningText);
        foreach (var offer in BuildShopOffers())
            list.AddChild(CreateShopItemCard(offer));
        ShowModal();
    }

    Control CreateShopHeroBanner()
    {
        var panel = new Panel
        {
            Name = "ShopHeroBanner",
            CustomMinimumSize = new Vector2(0, 146),
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
            Modulate = new Color(1f, 1f, 1f, 0.32f)
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
        content.OffsetLeft = 16;
        content.OffsetTop = 14;
        content.OffsetRight = -16;
        content.OffsetBottom = -14;

        var left = new VBoxContainer
        {
            Name = "ShopHeroLeft",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        left.AddThemeConstantOverride("separation", 6);
        content.AddChild(left);

        left.AddChild(CreateTag("浠婃棩鍐涢渶", new Color(0.34f, 0.24f, 0.08f, 0.92f), new Color(1f, 0.95f, 0.82f)));

        var title = AddLabel("鍐涘琛ョ粰宸叉暣鐞嗗叆搴?, 22, PanelText, HorizontalAlignment.Left);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        left.AddChild(title);

        var note = AddLabel("浣跨敤鐜版垚璧勬簮鍥炬爣涓庡啗姝﹀簳鍥撅紝琛ョ粰銆佸缓閫犲拰绉戞妧鍟嗗搧鍒嗗眰灞曠ず锛屼笉鍐嶆槸绠€鍗曟枃瀛楀爢鍙犮€?, 13, new Color(0.92f, 0.95f, 0.96f), HorizontalAlignment.Left);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        left.AddChild(note);

        var heroMeta = new HFlowContainer
        {
            Name = "ShopHeroMeta",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        heroMeta.AddThemeConstantOverride("h_separation", 8);
        heroMeta.AddThemeConstantOverride("v_separation", 8);
        heroMeta.AddChild(CreateTag("4 椤瑰湪鍞?, new Color(0.16f, 0.34f, 0.24f, 0.92f), new Color(0.94f, 0.99f, 0.95f)));
        heroMeta.AddChild(CreateTag("琛ョ粰 / 鍔犻€?/ 鍐涘", new Color(0.22f, 0.28f, 0.36f, 0.94f), new Color(0.94f, 0.97f, 0.99f)));
        left.AddChild(heroMeta);

        var right = new HBoxContainer
        {
            Name = "ShopHeroRight",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        right.AddThemeConstantOverride("separation", 8);
        content.AddChild(right);

        right.AddChild(new TextureRect
        {
            Texture = LoadTexture(KenneyGameIconRoot + "shoppingCart.png"),
            CustomMinimumSize = new Vector2(34, 34),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 0.93f, 0.72f, 0.84f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        });
        right.AddChild(CreateHeroCurrencyChip("閲戝竵", $"{GameState.Instance?.Gold ?? 0}", CurrencyIconRoot + "currency_gold_coin.png", WarningText));
        right.AddChild(CreateHeroCurrencyChip("閽荤煶", $"{GameState.Instance?.Gems ?? 0}", CurrencyIconRoot + "currency_gem_blue.png", new Color(0.64f, 0.90f, 1f)));
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
            Name = "鎴樺湴琛ョ粰绠?,
            Subtitle = "鍓嶇嚎璧勬簮琛ョ粰",
            Description = "鑾峰緱 500 閲戝竵涓庡皯閲忔寚鎸ョ粡楠岋紝閫傚悎蹇€熻ˉ瓒冲紑灞€璧勬簮銆?,
            PriceText = "300",
            ActionText = "璐拱",
            Category = "琛ョ粰",
            Badge = "鐑攢",
            IconPath = UnityLobbyRoot + "gen_icon_task_crate.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_repair_workshop.png",
            Accent = new Color(0.90f, 0.72f, 0.28f, 1f),
            UsesGems = false,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "500 閲戝竵", "鎸囨尌缁忛獙" }
        };
        yield return new ShopOfferUi
        {
            Name = "楂樼骇寤洪€犺鍙?,
            Subtitle = "宸ョ▼浣撶郴鎻愰€?,
            Description = "涓嬩竴鍦烘垬鏂楀缓閫犻€熷害鎻愬崌 10%锛屼富鍩哄湴灞曞紑涓庡墠绾胯ˉ鐐归兘浼氭洿椤恒€?,
            PriceText = "30",
            ActionText = "璐拱",
            Category = "寤洪€?,
            Badge = "鍔犻€?,
            IconPath = UnityLobbyRoot + "gen_icon_helmet.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_hold_bunker.png",
            Accent = new Color(0.50f, 0.84f, 0.58f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Primary,
            Highlights = new[] { "寤洪€?+10%", "鍗曞眬鐢熸晥" }
        };
        yield return new ShopOfferUi
        {
            Name = "绉戞妧閫熺爺浠?,
            Subtitle = "瀹為獙瀹ゅ嵆鍒诲姞閫?,
            Description = "绉戞妧鐮旂┒绔嬪嵆缂╃煭 10 鍒嗛挓锛岄€傚悎鍦ㄥ叧閿崌绾у崱浣忔椂鐩存帴鎺ㄨ繘銆?,
            PriceText = "25",
            ActionText = "鍔犻€?,
            Category = "绉戞妧",
            Badge = "鍗冲埢",
            IconPath = UnityLobbyRoot + "gen_icon_tech_blueprint.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_speed_wings.png",
            Accent = new Color(0.56f, 0.80f, 1f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Primary,
            Highlights = new[] { "鐮旂┒ -10 鍒嗛挓", "鍗虫椂缁撶畻" }
        };
        yield return new ShopOfferUi
        {
            Name = "鍏ㄧ悆浜夐湼鍐涘鍖?,
            Subtitle = "璧涘鍐涘鏁村",
            Description = "闅忔満鍐涘钃濆浘銆侀€氳璇佺Н鍒嗕笌绋€鏈夋潗鏂欙紝鐢ㄤ簬鍏ㄧ悆浜夐湼鐨勪富鍔涜ˉ寮恒€?,
            PriceText = "80",
            ActionText = "璐拱",
            Category = "鍐涘",
            Badge = "绮鹃€?,
            IconPath = UnityLobbyRoot + "gen_icon_tank.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_firepower_barrage.png",
            Accent = new Color(0.98f, 0.56f, 0.30f, 1f),
            UsesGems = true,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "钃濆浘", "閫氳璇佺Н鍒?, "绋€鏈夋潗鏂? }
        };
        yield return new ShopOfferUi
        {
            Name = "鑰佸叺璁粌鎵嬪唽",
            Subtitle = "姝ュ叺璁粌妗ｆ",
            Description = "姝ュ叺鍗曚綅鍒濆缁忛獙鎻愬崌锛岃鍩虹鍏电鏇村揩褰㈡垚鏈夋晥鎴樻枟鍔涖€?,
            PriceText = "450",
            ActionText = "璐拱",
            Category = "璁粌",
            Badge = "姝ュ叺",
            IconPath = UnityLobbyRoot + "gen_icon_helmet.png",
            BackdropPath = TechBackgroundRoot + "tech_bg_armor_plate.png",
            Accent = new Color(0.86f, 0.76f, 0.50f, 1f),
            UsesGems = false,
            ActionTone = ButtonTone.Gold,
            Highlights = new[] { "姝ュ叺缁忛獙", "甯搁┗鍔犳垚" }
        };
    }

    Control CreateShopItemCard(ShopOfferUi offer)
    {
        var card = new Panel
        {
            Name = "ShopItem_" + offer.Name,
            CustomMinimumSize = new Vector2(0, offer.Highlights.Length > 2 ? 214 : 188),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
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
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        card.AddChild(margin);

        var content = new VBoxContainer
        {
            Name = "ShopItemContent_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);

        var box = new HBoxContainer
        {
            Name = "ShopItemBox_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        box.AddThemeConstantOverride("separation", 14);
        content.AddChild(box);

        var preview = CreateShopPreviewBlock(offer);
        preview.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        box.AddChild(preview);

        var info = new VBoxContainer
        {
            Name = "ShopItemInfo_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        info.AddThemeConstantOverride("separation", 6);
        box.AddChild(info);

        var topRow = new HBoxContainer
        {
            Name = "ShopItemTopRow_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        topRow.AddThemeConstantOverride("separation", 8);
        info.AddChild(topRow);

        var titleStack = new VBoxContainer
        {
            Name = "ShopItemTitleStack_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 40)
        };
        titleStack.AddThemeConstantOverride("separation", 3);
        topRow.AddChild(titleStack);

        var title = AddLabel(offer.Name, 16, PanelText, HorizontalAlignment.Left);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.VerticalAlignment = VerticalAlignment.Top;
        titleStack.AddChild(title);

        var subtitle = AddLabel(offer.Subtitle, 12, new Color(0.86f, 0.91f, 0.94f), HorizontalAlignment.Left);
        subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        subtitle.VerticalAlignment = VerticalAlignment.Top;
        titleStack.AddChild(subtitle);

        var meta = new HFlowContainer
        {
            Name = "ShopItemMeta_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 26)
        };
        meta.AddThemeConstantOverride("h_separation", 6);
        meta.AddThemeConstantOverride("v_separation", 6);
        meta.AddChild(CreateTag(offer.Category, new Color(0.20f, 0.28f, 0.36f, 0.92f), new Color(0.94f, 0.97f, 0.99f)));
        if (!string.IsNullOrWhiteSpace(offer.Badge))
            meta.AddChild(CreateTag(offer.Badge, new Color(0.34f, 0.24f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)));
        info.AddChild(meta);

        var detail = AddLabel(offer.Description, 12, MutedText, HorizontalAlignment.Left);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.VerticalAlignment = VerticalAlignment.Top;
        detail.CustomMinimumSize = new Vector2(0, 34);
        info.AddChild(detail);

        var buyColumn = new VBoxContainer
        {
            Name = "ShopItemBuyColumn_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(104, 0)
        };
        buyColumn.AddThemeConstantOverride("separation", 8);
        box.AddChild(buyColumn);

        buyColumn.AddChild(CreateShopPriceChip(offer));

        var buyButton = AddButton(offer.ActionText, () => ShowToast($"{offer.Name} 鐨勮喘涔版帴鍙ｅ緟鎺ュ叆"), offer.ActionTone, 12);
        buyButton.CustomMinimumSize = new Vector2(84, 32);
        buyButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        buyColumn.AddChild(buyButton);

        if (offer.Highlights.Length > 0)
        {
            var highlights = new HFlowContainer
            {
                Name = "ShopItemHighlights_" + offer.Name,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
                CustomMinimumSize = new Vector2(0, offer.Highlights.Length > 2 ? 52 : 26)
            };
            highlights.AddThemeConstantOverride("h_separation", 6);
            highlights.AddThemeConstantOverride("v_separation", 6);
            foreach (var highlight in offer.Highlights)
                highlights.AddChild(CreateTag(highlight, new Color(0.14f, 0.18f, 0.20f, 0.88f), new Color(0.92f, 0.94f, 0.90f)));
            content.AddChild(highlights);
        }
        return card;
    }

    Control CreateShopPreviewBlock(ShopOfferUi offer)
    {
        var holder = new Panel
        {
            Name = "ShopPreview_" + offer.Name,
            CustomMinimumSize = new Vector2(124, 104),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
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
        iconFrame.SetAnchorsPreset(LayoutPreset.FullRect);
        iconFrame.OffsetLeft = 39;
        iconFrame.OffsetTop = 29;
        iconFrame.OffsetRight = -39;
        iconFrame.OffsetBottom = -29;
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

    Control CreateShopPriceChip(ShopOfferUi offer)
    {
        var chip = new PanelContainer
        {
            Name = "ShopPriceChip_" + offer.Name,
            CustomMinimumSize = new Vector2(104, 42),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = offer.UsesGems
                ? new Color(0.08f, 0.18f, 0.24f, 0.90f)
                : new Color(0.30f, 0.22f, 0.08f, 0.92f),
            BorderColor = new Color(1f, 1f, 1f, 0.12f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21
        });

        var margin = new MarginContainer
        {
            Name = "ShopPriceChipMargin_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        chip.AddChild(margin);

        var row = new HBoxContainer
        {
            Name = "ShopPriceChipRow_" + offer.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
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

        var amount = AddLabel(offer.PriceText, 15, new Color(1f, 0.98f, 0.90f), HorizontalAlignment.Left);
        amount.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        row.AddChild(amount);
        return chip;
    }

    void ShowWarehouseModal(string activeTab = "blueprints")
    {
        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        OpenFeatureModal("浠撳簱", "鏌ョ湅宸叉湁璧勬簮銆佸啗澶囪摑鍥俱€佺鎶€鍔犳垚涓庡叏鐞冧簤闇稿啗澶囬厤缃€?);
        AddModalSummaryRow(
            AddStatCard("閲戝竵搴撳瓨", $"{GameState.Instance?.Gold ?? 0}", "鍙敤浜庡缓閫犱笌鍟嗗搧", WarningText),
            AddStatCard("閽荤煶搴撳瓨", $"{GameState.Instance?.Gems ?? 0}", "鍙敤浜庡姞閫熶笌绀煎寘", new Color(0.64f, 0.90f, 1f)),
            AddStatCard("褰撳墠寮€灞€闃佃惀", currentFaction.DisplayName, $"寮€灞€涓诲姏锛歿currentStarter.DisplayName}", GoodText));
        modalBody.AddChild(CreateWarehouseTabBar(activeTab));

        switch (activeTab)
        {
            case "tech":
            {
                var techList = AddWarehouseSectionPanel("绉戞妧鍔犳垚", "涓诲熀鍦拌寖鍥寸鎶€涓庢寔缁€佸喎鍗翠俊鎭€?, new Color(0.64f, 0.90f, 1f));
                foreach (var tech in BattleTechCatalog.GetForBuilding("main_base"))
                    techList.AddChild(CreateWarehouseTechCard(tech));
                break;
            }
            case "armament":
            {
                var armamentList = AddWarehouseSectionPanel("鍏ㄧ悆浜夐湼闃佃惀", "鍙垏鎹㈠紑灞€闃佃惀锛屽苟鏌ョ湅瀵瑰簲鐨勫紑灞€涓诲姏涓庡畾浣嶃€?, GoodText);
                foreach (var faction in BattleUnitCatalog.GlobalConquestFactions)
                    armamentList.AddChild(CreateWarehouseArmamentCard(faction, currentFaction.Key == faction.Key));
                break;
            }
            case "items":
            {
                var itemList = AddWarehouseSectionPanel("閬撳叿涓庣鎶€鍗?, "甯搁┗鍗＄墖銆侀€氳璇佸拰鎴樻枟娑堣€楀搧銆?, new Color(0.82f, 0.92f, 1f));
                itemList.AddChild(CreateWarehouseInventoryCard("绉戞妧鍗★細鏈哄姩寮哄寲", "绉戞妧鍗?, "1", "涓诲熀鍦拌寖鍥寸鎶€锛屾彁鍗囧弸鍐涚Щ鍔ㄨ兘鍔涖€?));
                itemList.AddChild(CreateWarehouseInventoryCard("鍏ㄧ悆浜夐湼閫氳璇?, "鎴樺焦閬撳叿", "1", "杩涘叆鍏ㄧ悆浜夐湼鐜╂硶骞惰褰曡禌瀛ｈ繘搴︺€?));
                itemList.AddChild(CreateWarehouseInventoryCard("鎴樺湴缁翠慨鍖?, "娑堣€楀搧", "3", "鎴樻枟涓敤浜庣揣鎬ョ淮淇崟浣嶆垨寤虹瓚銆?));
                break;
            }
            default:
            {
                var list = AddWarehouseSectionPanel("鍐涘钃濆浘", "宸叉敹褰曞彲鐢熶骇鍗曚綅涓庡綋鍓嶈В閿佹潯浠躲€?, WarningText);
                foreach (var unit in BattleUnitCatalog.PlayerRoster)
                    list.AddChild(CreateWarehouseBlueprintCard(unit));
                break;
            }
        }
        ShowModal();
    }

    Control CreateWarehouseTabBar(string activeTab)
    {
        var panel = new Panel
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
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
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

        var helper = AddLabel("妯悜鍒嗙被鍒囨崲锛岀偣鏍囩鏌ョ湅瀵瑰簲浠撳簱鍐呭銆?, 11, new Color(0.86f, 0.90f, 0.93f, 0.96f), HorizontalAlignment.Left);
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(helper);

        var row = new HBoxContainer
        {
            Name = "WarehouseTabBarRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("separation", 8);
        box.AddChild(row);

        row.AddChild(CreateWarehouseTabButton("鍐涘钃濆浘", "blueprints", activeTab));
        row.AddChild(CreateWarehouseTabButton("绉戞妧鍔犳垚", "tech", activeTab));
        row.AddChild(CreateWarehouseTabButton("浜夐湼鍐涘", "armament", activeTab));
        row.AddChild(CreateWarehouseTabButton("閬撳叿鍗?, "items", activeTab));
        return panel;
    }

    Button CreateWarehouseTabButton(string text, string tabKey, string activeTab)
    {
        var button = AddButton(text, () => ShowWarehouseModal(tabKey), tabKey == activeTab ? ButtonTone.Gold : ButtonTone.Secondary, 12);
        button.CustomMinimumSize = new Vector2(0, 34);
        return button;
    }

    VBoxContainer AddWarehouseSectionPanel(string title, string note, Color accent)
    {
        var panel = new Panel
        {
            Name = "WarehouseSection_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(panel, WarehousePanel, 1, 8, 4);
        AddTopAccentStripe(panel, new Color(accent.R, accent.G, accent.B, 0.76f), 3);
        modalBody.AddChild(panel);

        var margin = new MarginContainer
        {
            Name = panel.Name + "_Margin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
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
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);
        box.AddChild(CreateWarehouseSectionHeader(title, note, accent));
        return box;
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

    Panel CreateWarehouseBlueprintCard(BattleUnitDefinition unit)
    {
        var status = WarehouseUnitStatus(unit.Key);
        var unlocked = status.StartsWith("宸?, StringComparison.Ordinal);
        var accent = unlocked ? new Color(0.50f, 0.90f, 0.58f, 0.88f) : WarningText;
        return CreateWarehouseCard(
            unit.DisplayName,
            $"{DescribeWarehouseBlueprint(unit.Key)}銆倇status}",
            accent,
            CreateTag(UnitCategory(unit.Key), new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.94f, 0.97f, 0.99f)),
            CreateTag($"璐圭敤 {unit.GoldCost} / 浜哄彛 {unit.PopCost}", new Color(0.31f, 0.23f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)),
            CreateTag(unlocked ? "宸叉敹褰? : "寰呰В閿?, unlocked ? new Color(0.16f, 0.38f, 0.24f, 0.94f) : new Color(0.44f, 0.22f, 0.10f, 0.94f), new Color(0.96f, 0.98f, 0.94f)));
    }

    Panel CreateWarehouseTechCard(BattleTechDefinition tech)
    {
        return CreateWarehouseCard(
            tech.DisplayName,
            $"{DescribeTechBonus(tech)}銆傛寔缁?{tech.Duration:0}s锛屽喎鍗?{tech.Cooldown:0}s锛岃寖鍥?{tech.Radius:0.#}銆?,
            tech.Tint,
            CreateTag("涓诲熀鍦?, new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.94f, 0.97f, 0.99f)),
            CreateTag($"鎸佺画 {tech.Duration:0}s", new Color(0.20f, 0.34f, 0.24f, 0.92f), new Color(0.94f, 0.99f, 0.95f)),
            CreateTag($"鍐峰嵈 {tech.Cooldown:0}s", new Color(0.22f, 0.24f, 0.28f, 0.92f), new Color(1f, 0.95f, 0.82f)));
    }

    Panel CreateWarehouseArmamentCard(GlobalConquestFactionDefinition faction, bool isCurrent)
    {
        var starter = BattleUnitCatalog.Get(faction.StarterUnitKey);
        return CreateWarehouseCard(
            faction.DisplayName,
            $"{faction.Description} 寮€灞€涓诲姏锛歿starter.DisplayName}銆?,
            isCurrent ? GoodText : faction.Tint,
            CreateTag($"涓诲姏 {starter.DisplayName}", new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.94f, 0.97f, 0.99f)),
            CreateTag(isCurrent ? "褰撳墠榛樿" : "鍙垏鎹?, isCurrent ? new Color(0.16f, 0.38f, 0.24f, 0.94f) : new Color(0.31f, 0.23f, 0.08f, 0.94f), isCurrent ? new Color(0.96f, 0.99f, 0.96f) : new Color(1f, 0.94f, 0.78f)),
            CreateTag(UnitCategory(starter.Key), new Color(0.22f, 0.24f, 0.28f, 0.92f), new Color(0.92f, 0.95f, 0.98f)));
    }

    Panel CreateWarehouseInventoryCard(string name, string type, string count, string description)
    {
        return CreateWarehouseCard(
            name,
            description,
            new Color(0.72f, 0.84f, 0.96f, 0.82f),
            CreateTag(type, new Color(0.18f, 0.28f, 0.35f, 0.92f), new Color(0.94f, 0.97f, 0.99f)),
            CreateTag($"鏁伴噺 {count}", new Color(0.31f, 0.23f, 0.08f, 0.94f), new Color(1f, 0.94f, 0.78f)));
    }

    Panel CreateWarehouseCard(string title, string description, Color accent, params Control[] tags)
    {
        var card = new Panel
        {
            Name = "WarehouseCard_" + title,
            CustomMinimumSize = new Vector2(0, 92),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 7, 4);
        AddTopAccentStripe(card, new Color(accent.R, accent.G, accent.B, 0.80f), 3);

        var box = AddVBox(card, "WarehouseCardBox_" + title, 6, new Vector2(14, 12), new Vector2(-14, -12));

        var header = new HBoxContainer
        {
            Name = "WarehouseHeader_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        header.AddThemeConstantOverride("separation", 10);
        box.AddChild(header);

        var titleLabel = AddLabel(title, 15, PanelText, HorizontalAlignment.Left);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleLabel.VerticalAlignment = VerticalAlignment.Top;
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);

        if (tags.Length > 0)
        {
            var tagRow = new HBoxContainer
            {
                Name = "WarehouseTags_" + title,
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            tagRow.AddThemeConstantOverride("separation", 6);
            foreach (var tag in tags)
                tagRow.AddChild(tag);
            header.AddChild(tagRow);
        }

        var body = AddLabel(description, 12, MutedText, HorizontalAlignment.Left);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(body);
        return card;
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
            new[] { tech.DisplayName, "涓诲熀鍦?, DescribeTechBonus(tech), $"鎸佺画 {tech.Duration:0}s / 鍐峰嵈 {tech.Cooldown:0}s" },
            new[] { 144f, 92f, 222f, 128f },
            "",
            null,
            ButtonTone.Secondary));
    }

    void AddGlobalArmamentRow(VBoxContainer list, BattleUnitDefinition unit, bool isCurrent)
    {
        list.AddChild(AddTableRow(
            new[] { unit.DisplayName, UnitCategory(unit.Key), isCurrent ? "褰撳墠榛樿" : "鍙垏鎹?, DescribeGlobalConquestStarter(unit.Key) },
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
            parts.Add($"绉婚€?{(tech.MoveMultiplier - 1f) * 100f:+0;-0}%");
        if (!Mathf.IsEqualApprox(tech.DamageMultiplier, 1f))
            parts.Add($"鐏姏 {(tech.DamageMultiplier - 1f) * 100f:+0;-0}%");
        if (!Mathf.IsZeroApprox(tech.AttackRangeBonus))
            parts.Add($"灏勭▼ +{tech.AttackRangeBonus:0.#}");
        if (!Mathf.IsEqualApprox(tech.AttackCooldownMultiplier, 1f))
            parts.Add($"鏀婚€?{(1f - tech.AttackCooldownMultiplier) * 100f:+0;-0}%");
        if (!Mathf.IsZeroApprox(tech.DefenseReduction))
            parts.Add($"鍑忎激 {tech.DefenseReduction * 100f:0}%");
        if (!Mathf.IsZeroApprox(tech.VisionBonus))
            parts.Add($"瑙嗛噹 +{tech.VisionBonus:0.#}");
        if (!Mathf.IsZeroApprox(tech.RegenPerSecond))
            parts.Add($"姣忕缁翠慨 {tech.RegenPerSecond:0.#}");

        return parts.Count == 0 ? "鎻愪緵鑼冨洿鏀彺" : string.Join("锛?, parts);
    }

    async Task ShowLeaderboardModal()
    {
        activeFeatureModal = LeaderboardModalKind;
        if (HasLeaderboardCache())
        {
            RenderLeaderboardModal(cachedLeaderboardData, true);
            _ = RefreshLeaderboardCache();
            return;
        }

        OpenFeatureModal("鎺掕姒?, "璧涘鍐涘姛銆佽儨鍦轰笌鍐涜鎺掑悕銆?);
        modalBody.AddChild(AddLabel("鎺掕姒滃姞杞戒腑...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();
        await RefreshLeaderboardCache();
    }

    void AddLeaderboardTableHeader(out VBoxContainer list)
    {
        list = AddModalTable("鍐涘姛姒?, new[] { "鎺掑悕", "鎸囨尌瀹?, "鍐涜", "鎴樼哗", "鑳滅巼" }, new[] { 64f, 176f, 116f, 102f, 80f });
    }

    void AddLeaderboardRow(VBoxContainer list, int place, string username, int level, int wins, int losses, string rankTitle, bool isCurrent)
    {
        var total = Math.Max(1, wins + losses);
        var winRate = Mathf.RoundToInt(wins * 100f / total);
        list.AddChild(AddTableRow(
            new[] { $"#{place}", $"{FriendlyText(username, "Commander")}  Lv.{level}", FriendlyText(rankTitle, "鍒楀叺"), $"{wins}鑳?{losses}璐?, $"{winRate}%" },
            new[] { 64f, 176f, 116f, 102f, 80f },
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

        OpenFeatureModal("閭欢 / 閭€璇?, "澶勭悊鎴块棿閭€璇枫€佺郴缁熷叕鍛婁笌鎴樻姤娑堟伅銆?);
        modalBody.AddChild(AddLabel("姝ｅ湪鍚屾閭€璇?..", 14, MutedText, HorizontalAlignment.Left));
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

        if (activeFeatureModal == LeaderboardModalKind && modalPanel.Visible && modalTitle.Text == "鎺掕姒?)
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

        if (activeFeatureModal == MailModalKind && modalPanel.Visible && modalTitle.Text == "閭欢 / 閭€璇?)
            RenderMailModal(data.GetBool("success") ? data : cachedInvitesData, data.GetBool("success") ? false : HasInvitesCache());
    }

    void RenderLeaderboardModal(Godot.Collections.Dictionary data, bool usingCache)
    {
        OpenFeatureModal(
            "鎺掕姒?,
            usingCache
                ? "璧涘鍐涘姛銆佽儨鍦轰笌鍐涜鎺掑悕銆傚凡鏄剧ず缂撳瓨锛屾鍦ㄥ悗鍙板埛鏂般€?
                : "璧涘鍐涘姛銆佽儨鍦轰笌鍐涜鎺掑悕銆?);

        var rows = data.GetArray("leaderboard");
        if (!data.GetBool("success") || rows.Count == 0)
        {
            AddModalSummaryRow(
                AddStatCard("妯″紡", usingCache ? "缂撳瓨鏁版嵁" : "绂荤嚎鏍蜂緥", usingCache ? "绛夊緟鏈嶅姟鍣ㄥ埛鏂? : "鏈嶅姟鍣ㄤ笉鍙敤鏃跺睍绀?, WarningText),
                AddStatCard("鎴戠殑鑳滃満", $"{GameState.Instance?.Wins ?? 0}", "鏈湴璐﹀彿缂撳瓨", GoodText),
                AddStatCard("鎴戠殑鍐涜", FriendlyText(GameState.Instance?.RankTitle, "鍒楀叺"), "褰撳墠妗ｆ", PanelText));
            AddLeaderboardTableHeader(out var sampleList);
            AddLeaderboardRow(sampleList, 1, GameState.Instance?.Username ?? "Commander", GameState.Instance?.Level ?? 1, GameState.Instance?.Wins ?? 0, GameState.Instance?.Losses ?? 0, GameState.Instance?.RankTitle ?? "鍒楀叺", true);
            AddLeaderboardRow(sampleList, 2, "IronWolf", 8, 12, 4, "涓婂皦", false);
            AddLeaderboardRow(sampleList, 3, "SeaHammer", 6, 9, 3, "涓皦", false);
            AddLeaderboardRow(sampleList, 4, "SkyLancer", 5, 7, 5, "灏戝皦", false);
            ShowModal();
            return;
        }

        AddModalSummaryRow(
            AddStatCard("璧涘", FriendlyText(data.GetString("season"), "褰撳墠璧涘"), FriendlyText(data.GetString("resetText"), "姣忓懆缁撶畻"), WarningText),
            AddStatCard("涓婃浜烘暟", $"{rows.Count}", usingCache ? "缂撳瓨鎺掑悕" : "瀹炴椂鎺掑悕", GoodText),
            AddStatCard("鎴戠殑浣嶇疆", "宸叉爣浜?, "缁胯壊琛屼负褰撳墠璐﹀彿", PanelText));
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
        OpenFeatureModal(
            "閭欢 / 閭€璇?,
            usingCache
                ? "澶勭悊鎴块棿閭€璇枫€佺郴缁熷叕鍛婁笌鎴樻姤娑堟伅銆傚凡鏄剧ず缂撳瓨锛屾鍦ㄥ悗鍙板埛鏂般€?
                : "澶勭悊鎴块棿閭€璇枫€佺郴缁熷叕鍛婁笌鎴樻姤娑堟伅銆?);

        var invites = data.GetArray("invites");
        AddModalSummaryRow(
            AddStatCard("鏈鐞嗛個璇?, $"{(data.GetBool("success") ? invites.Count : 0)}", usingCache ? "缂撳瓨涓殑濂藉弸鎴块棿閭€璇? : "濂藉弸鎴块棿閭€璇?, invites.Count > 0 ? WarningText : GoodText),
            AddStatCard("绯荤粺閭欢", "3", "鍏憡銆佽ˉ缁欎笌鎴樻姤", PanelText),
            AddStatCard("鎴樻姤", "1", "鏈€杩戜竴鍦烘垬鏂?, GoodText));

        var inviteSection = AddModalSectionPanel("濂藉弸鎴块棿閭€璇?, invites.Count > 0 ? $"褰撳墠鏈?{invites.Count} 鏉″緟澶勭悊閭€璇? : "褰撳墠娌℃湁寰呭鐞嗙殑鎴块棿閭€璇?, WarningText);
        if (!data.GetBool("success") || invites.Count == 0)
            inviteSection.AddChild(AddEmptyStateCard("鏆傛棤鏈鐞嗛個璇枫€?, "鏂扮殑濂藉弸閭€璇峰埌杈惧悗浼氭樉绀哄湪杩欓噷銆?));
        else
        {
            foreach (var item in invites)
            {
                if (item.VariantType == Variant.Type.Dictionary)
                    inviteSection.AddChild(CreateMailInviteCard(item.AsGodotDictionary()));
            }
        }

        var mailSection = AddModalSectionPanel("绯荤粺閭欢", "鍏憡銆佽ˉ缁欏拰鏈€杩戞垬鎶?, new Color(0.70f, 0.92f, 1f));
        mailSection.AddChild(CreateSystemMailCard("鍏ㄧ悆浜夐湼璧涘寮€鏀?, "鍏憡", "鏈", "鏂板鍏ㄧ悆浜夐湼鍏ュ彛涓庡啗澶囧睍绀恒€?));
        mailSection.AddChild(CreateSystemMailCard("姣忔棩琛ョ粰宸插埛鏂?, "琛ョ粰", "鍙", "瀹屾垚姣忔棩浠诲姟鍙鍙栭噾甯佸拰缁忛獙銆?));
        mailSection.AddChild(CreateSystemMailCard("鏈€杩戞垬鎶?, "鎴樻姤", "宸茶", "鎴樻枟鎶ュ憡鍙湪缁撶畻鐣岄潰鏌ョ湅璇︾粏缁熻銆?));
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
            new[] { FriendlyText(invite.GetString("from"), "濂藉弸"), $"{mapName}  {Math.Max(0, playerCount)}/{Math.Max(2, maxPlayers)}", ShortRoomId(roomId) },
            new[] { 128f, 168f, 126f },
            new[]
            {
                AddButton("鎺ュ彈", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 12),
                AddButton("鎷掔粷", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 12)
            },
            false);
        list.AddChild(row);
    }

    Panel CreateMailInviteCard(Godot.Collections.Dictionary invite)
    {
        var inviteId = invite.GetString("id");
        var roomId = invite.GetString("roomId");
        var from = FriendlyText(invite.GetString("from"), "濂藉弸");
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

        var detailLabel = AddLabel($"鎴块棿鍙?{ShortRoomId(roomId)}  |  鐐瑰嚮鎺ュ彈鍚庣洿鎺ュ姞鍏ユ埧闂淬€?, 11, MutedText, HorizontalAlignment.Left);
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
        var acceptButton = AddButton("鎺ュ彈", () => _ = RespondInvite(inviteId, true, roomId, mapName), ButtonTone.Primary, 12);
        acceptButton.CustomMinimumSize = new Vector2(96, 0);
        var rejectButton = AddButton("鎷掔粷", () => _ = RespondInvite(inviteId, false, roomId, mapName), ButtonTone.Secondary, 12);
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
            status.Contains("鏈") || status.Contains("鍙")));
    }

    void OpenFeatureModal(string title, string subtitle)
    {
        modalTitle.Text = title;
        ClearChildren(modalBody);
        var subtitleLabel = AddLabel(subtitle, 13, MutedText, HorizontalAlignment.Left);
        subtitleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(subtitleLabel);
    }

    void AddModalSummaryRow(params Control[] cards)
    {
        var row = new HBoxContainer { Name = "ModalSummaryRow" };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var card in cards)
            row.AddChild(card);
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
            CustomMinimumSize = new Vector2(0, 78),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 7, 4);
        AddTopAccentStripe(card, new Color(accent.R, accent.G, accent.B, 0.82f), 3);

        var box = AddVBox(card, "StatCardBox_" + title, 2, new Vector2(12, 10), new Vector2(-12, -10));
        box.AddChild(AddLabel(title, 12, MutedText, HorizontalAlignment.Left));
        box.AddChild(AddLabel(value, 18, accent, HorizontalAlignment.Left));
        var noteLabel = AddLabel(note, 11, PanelText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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

    Panel CreateSystemMailCard(string title, string type, string status, string note)
    {
        var highlight = status.Contains("鏈") || status.Contains("鍙");
        var accent = status.Contains("鍙")
            ? new Color(0.88f, 0.70f, 0.26f, 0.88f)
            : highlight
                ? new Color(0.48f, 0.88f, 0.60f, 0.82f)
                : new Color(0.72f, 0.84f, 0.90f, 0.42f);
        var card = new Panel
        {
            Name = "SystemMail_" + title,
            CustomMinimumSize = new Vector2(0, 88)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, accent, 2);

        var box = AddVBox(card, "SystemMailBox_" + title, 4, new Vector2(14, 10), new Vector2(-14, -10));
        var header = new VBoxContainer
        {
            Name = "SystemMailHeader_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddThemeConstantOverride("separation", 6);
        box.AddChild(header);

        var titleLabel = AddLabel(title, 15, PanelText, HorizontalAlignment.Left);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(titleLabel);

        var tags = new HFlowContainer
        {
            Name = "SystemMailTags_" + title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        tags.AddThemeConstantOverride("h_separation", 6);
        tags.AddThemeConstantOverride("v_separation", 6);
        tags.AddChild(CreateTag(type, highlight ? new Color(0.18f, 0.40f, 0.26f, 0.92f) : new Color(0.20f, 0.28f, 0.36f, 0.92f), new Color(0.94f, 0.97f, 0.98f)));
        tags.AddChild(CreateTag(status, highlight ? new Color(0.76f, 0.56f, 0.18f, 0.96f) : new Color(0.22f, 0.24f, 0.28f, 0.92f), new Color(1f, 0.95f, 0.82f)));
        header.AddChild(tags);

        var noteLabel = AddLabel(note, 12, highlight ? PanelText : MutedText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
            var label = AddLabel(cells[i], 12, highlight ? GoodText : PanelText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 36);
            label.SizeFlagsHorizontal = i == cells.Length - 1 && string.IsNullOrEmpty(actionText) ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
            var label = AddLabel(cells[i], 12, highlight ? GoodText : PanelText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 36);
            label.VerticalAlignment = VerticalAlignment.Top;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
        "scout_plane" or "fighter" or "bomber" => "绌哄啗",
        "patrol_boat" or "destroyer_ship" or "transport_ship" => "娴峰啗",
        "light_tank" or "tank" or "heavy_tank" or "artillery" or "anti_air_gun" => "瑁呯敳",
        _ => "闄嗗啗"
    };

    static string DescribeWarehouseBlueprint(string unitKey) => unitKey switch
    {
        "infantry" => "鍩虹鍗犵偣涓庡墠绾挎帹杩涘崟浣?,
        "infantry_artillery" => "杩滅▼鏀彺姝ュ叺锛屾搮闀垮帇鍒跺瘑闆嗙洰鏍?,
        "infantry_flamethrower" or "flamethrower" => "杩戣窛绂绘竻闅滀笌寤虹瓚鍘嬪埗鍗曚綅",
        "light_tank" => "鏈哄姩绌挎彃瑁呯敳锛岄€傚悎渚﹀療涓庨獨鎵?,
        "tank" or "medium_tank" => "涓潥瑁呯敳鐏姏锛岄€傚悎姝ｉ潰鎺ㄨ繘",
        "heavy_tank" => "楂樿€愪箙閲嶈鍗曚綅锛岄€傚悎鏀诲潥涓庡惛鏀朵激瀹?,
        "artillery" => "杩滅▼鏇插皠鐏姏锛岄€傚悎鍚庢帓鏀彺",
        "anti_air_gun" => "闃茬┖鍘嬪埗骞冲彴锛屽吋椤惧弽浣庣┖鐩爣",
        "scout_plane" => "鎻愪緵蹇€熻閲庝睛瀵熶笌绌轰腑宸℃煡",
        "fighter" => "楂樻満鍔ㄥ埗绌哄崟浣嶏紝浼樺厛鎷︽埅绌哄啗",
        "bomber" => "閲嶅瀷绌鸿鍗曚綅锛岄€傚悎鐐规潃楂樹环鍊肩洰鏍?,
        "patrol_boat" => "杞诲瀷姘撮潰宸￠€讳笌鎶よ埅鍗曚綅",
        "destroyer_ship" => "涓诲姏娴峰啗鐐嚮鍗曚綅锛岄€傚悎涓繙绋嬪帇鍒?,
        "transport_ship" => "澶у瀷杩愯浇鑸拌埞锛岄€傚悎娴蜂笂鎶曢€?,
        _ => "宸茬紪鍏ユ垬鍦哄啗澶囧簱"
    };

    static string WarehouseUnitStatus(string unitKey) => unitKey switch
    {
        "fighter" or "bomber" or "destroyer_ship" or "transport_ship" => "闇€瀵瑰簲鍐涘寤虹瓚瑙ｉ攣",
        "heavy_tank" => "闇€鏇撮珮涓诲熀鍦扮瓑绾?,
        _ => "宸叉敹褰曪紝鍙湪鎴樻枟鍐呯敓浜?
    };

    async Task StartQuickMatch()
    {
        if (quickMatchStarting || battleStarting)
            return;

        quickMatchStarting = true;
        SelectModeInternal(QuickMatchMode, BattleMapCatalog.DefaultMapName, false);
        GameState.Instance?.ClearCurrentRoom();
        ShowToast("蹇€熷尮閰嶄腑...");

        try
        {
            if (NetClient.Instance is not null && !string.IsNullOrEmpty(GameState.Instance?.Token))
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
                        GameState.Instance?.SetCurrentRoom(match.GetString("roomId"));
                        await StartBattle(false);
                        return;
                    }

                    ShowToast(match.GetBool("success") ? "鏆傛棤瀵规墜锛岃繘鍏ユ湰鍦版紨缁? : "鍖归厤鏈嶅姟涓嶅彲鐢紝杩涘叆鏈湴婕旂粌");
                }
                else
                {
                    ShowToast("鍖归厤绛夊緟瓒呮椂锛岃繘鍏ユ湰鍦版紨缁?);
                }
            }

            await StartBattle();
        }
        finally
        {
            quickMatchStarting = false;
        }
    }

    bool ShouldShowQuickMatchBattleEntry()
    {
        var state = GameState.Instance;
        return state is not null
            && state.HasBattleEntry
            && state.LastBattleMode == QuickMatchMode;
    }

    async Task TriggerQuickMatchCard()
    {
        if (ShouldShowQuickMatchBattleEntry())
        {
            var resumeMap = GameState.Instance?.LastBattleMapName ?? BattleMapCatalog.DefaultMapName;
            SelectModeInternal(QuickMatchMode, resumeMap, false);
            ShowToast("杩涘叆鎴樺満");
            await StartBattle(false);
            return;
        }

        await StartQuickMatch();
    }

    Task StartGlobalConquest()
    {
        SelectModeInternal(GlobalConquestMode, BattleMapCatalog.GlobalConquestName, false);
        ShowGlobalConquestStarterModal();
        return Task.CompletedTask;
    }

    void ShowGlobalConquestStarterModal()
    {
        modalTitle.Text = "鍏ㄧ悆浜夐湼闃佃惀閫夋嫨";
        ClearChildren(modalBody);

        var reminder = AddLabel("寮€濮嬪叏鐞冧簤闇稿墠璇烽€夋嫨寮€灞€闃佃惀銆傝繘鍏ユ湰灞€鍚庡皢閿佸畾锛屾湰灞€鎴樻枟涓笉鍙洿鎹€?, 15, WarningText, HorizontalAlignment.Left);
        reminder.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(reminder);

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        var currentLabel = AddLabel($"褰撳墠榛樿锛歿currentFaction.DisplayName}    寮€灞€涓诲姏锛歿currentStarter.DisplayName}    鎻愮ず锛氳繖閲屽彧閿佸畾鏈眬锛屼笅娆¤繘鍏ュ彲閲嶆柊閫夋嫨銆?, 12, MutedText, HorizontalAlignment.Left);
        currentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(currentLabel);

        foreach (var faction in BattleUnitCatalog.GlobalConquestFactions)
            modalBody.AddChild(BuildGlobalConquestFactionOption(faction, currentFaction.Key == faction.Key));

        var footer = new HBoxContainer { Name = "GlobalConquestStarterFooter" };
        footer.AddThemeConstantOverride("separation", 10);
        footer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(Spacer());
        var cancelButton = AddButton("鏆備笉杩涘叆", CloseModal, ButtonTone.Secondary, 13);
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
            $"寮€灞€涓诲姏锛歿starter.DisplayName}    鑰愪箙 {starter.MaxHealth:0}    鐏姏 {starter.AttackDamage:0}    灏勭▼ {starter.AttackRange:0.#}    鏈哄姩 {starter.MoveSpeed:0.#}",
            12,
            isCurrent ? GoodText : faction.Tint,
            HorizontalAlignment.Left);
        stats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textBox.AddChild(stats);

        if (isCurrent)
            textBox.AddChild(AddLabel("褰撳墠榛樿闃佃惀", 11, GoodText, HorizontalAlignment.Left));

        row.AddChild(textBox);

        var enterButton = AddButton(isCurrent ? "鎸夊綋鍓嶉樀钀ヨ繘鍏? : "閫夋嫨闃佃惀骞惰繘鍏?, async () => await ConfirmGlobalConquestFaction(faction), ButtonTone.Primary, 13);
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
        GameState.Instance?.ClearCurrentRoom();
        CloseModal();
        ShowToast($"鍏ㄧ悆浜夐湼闃佃惀宸茶涓?{faction.DisplayName}");
        await StartBattle(false);
    }

    static string DescribeGlobalConquestStarter(string unitKey)
        => unitKey switch
        {
            "light_tank" => "鏈哄姩鏈€蹇紝閫傚悎渚﹀療銆佹姠鐐瑰拰蹇€熻浆绾裤€?,
            "tank" => "鏀婚槻鍧囪　锛岄€傚悎绋虫鎺ㄨ繘鍜屾闈綔鎴樸€?,
            "heavy_tank" => "瑁呯敳鏈€鍘氥€佺伀鍔涙渶寮猴紝浣嗘帹杩涢€熷害鏈€鎱€?,
            "artillery" => "灏勭▼鏈€杩滐紝閫傚悎杩滅▼鍘嬪埗锛岄渶瑕佹敞鎰忎笌鏁屼汉淇濇寔璺濈銆?,
            _ => "閫傚悎浣滀负鍏ㄧ悆浜夐湼寮€灞€涓诲姏鍏电銆?
        };

    async Task StartCampaign()
    {
        SelectModeInternal("鎴樺焦", selectedMap, false);
        GameState.Instance?.ClearCurrentRoom();
        await StartBattle();
    }

    void SelectModeInternal(string mode, string mapName, bool showToast)
    {
        selectedMode = mode;
        selectedMap = mapName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);
        ApplySelection(mode);
        RefreshModeCards();

        if (showToast)
            ShowToast($"宸查€夋嫨 {mode}");
    }

    void ApplySelection(string mode)
    {
        if (matchSelection is null)
            return;
        matchSelection.Visible = mode == QuickMatchMode;
        customSelection.Visible = mode == CustomRoomMode;
        globalSelection.Visible = mode == GlobalConquestMode;
    }

    async Task StartBattle(bool clearRoom = true)
    {
        if (battleStarting)
            return;
        battleStarting = true;
        ShowToast($"杩涘叆 {selectedMode}");
        try
        {
            GameState.Instance?.RememberBattleEntry(selectedMode, selectedMap);
            if (clearRoom)
                GameState.Instance?.ClearCurrentRoom();
            await Task.Yield();
            GetTree().ChangeSceneToFile(BattleScenePath);
        }
        finally
        {
            battleStarting = false;
        }
    }

    void RefreshTopBar()
    {
        playerLabel.Text = string.IsNullOrWhiteSpace(gs?.Username) ? "游客指挥官" : gs!.Username;
        rankLabel.Text = $"Lv.{Math.Max(gs?.Level ?? 1, 1)}  ·  {FriendlyText(gs?.RankTitle, "列兵")}";
        rankLabel.Text = $"Lv.{Math.Max(gs?.Level ?? 1, 1)}  路  {FriendlyText(gs?.RankTitle, "鍒楀叺")}";
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
            quickMatchCard.TitleLabel.Text = "杩涘叆鎴樺満";
            quickMatchCard.DescLabel.Text = "浠庡ぇ鍘呯户缁綋鍓嶅揩閫熷尮閰嶆垬灞€";
            quickMatchCard.ActionLabel.Text = "杩涘叆鎴樺満";
        }
        else
        {
            quickMatchCard.TitleLabel.Text = QuickMatchMode;
            quickMatchCard.DescLabel.Text = "鏍囧噯鍦板浘蹇€熷尮閰?;
            quickMatchCard.ActionLabel.Text = "绔嬪嵆鍖归厤";
        }

        if (!modeCards.TryGetValue(GlobalConquestMode, out var globalCard))
            return;

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var starter = BattleUnitCatalog.Get(currentStarterKey);
        var faction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        globalCard.TitleLabel.Text = GlobalConquestMode;
        globalCard.DescLabel.Text = "杩涘叆鍓嶉€夋嫨寮€灞€闃佃惀锛岃繘鍏ュ悗鏈眬涓嶅彲鏇存敼";
        globalCard.ActionLabel.Text = $"褰撳墠闃佃惀锛歿faction.DisplayName} 路 {starter.DisplayName}";
    }

    void ShowCommanderProfileModal()
    {
        var gs = GameState.Instance;
        var rankTitle = NormalizeRankTitle(gs?.RankTitle);
        OpenFeatureModal("档案", "查看当前拥有头像，并查看当前军衔。点击头像可立即切换。");
        AddModalSummaryRow(
            AddStatCard("账号", FriendlyText(gs?.Username, "游客"), $"Lv.{Math.Max(gs?.Level ?? 1, 1)}", WarningText),
            AddStatCard("当前军衔", rankTitle, "大厅与战场会同步显示", LobbyRankColor(rankTitle)),
            AddStatCard("拥有头像", $"{GetCommanderAvatarOptions().Length} 款", "当前档案可用", PanelText));

        modalBody.AddChild(CreateCommanderRankShowcase(rankTitle));

        var avatarsSection = AddModalSectionPanel("头像库", "点击头像即可切换当前展示。", WarningText);
        avatarsSection.AddChild(CreateCommanderAvatarGrid());
        ShowModal();
    }

    Panel CreateCommanderRankShowcase(string rankTitle)
    {
        var card = new Panel
        {
            Name = "CommanderRankShowcase",
            CustomMinimumSize = new Vector2(0, 156),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 9, 4);
        AddTopAccentStripe(card, new Color(0.96f, 0.78f, 0.30f, 0.84f), 3);

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
        OpenFeatureModal("璁剧疆", "绠＄悊鏈湴鍏憡銆佹垬鍦哄睆钄借褰曚笌璐﹀彿浼氳瘽銆?);
        AddModalSummaryRow(
            AddStatCard("璐﹀彿", FriendlyText(GameState.Instance?.Username, "娓稿"), FriendlyText(GameState.Instance?.RankTitle, "鍒楀叺"), WarningText),
            AddStatCard("鎴块棿", string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId) ? "鏈姞鍏? : ShortRoomId(GameState.Instance?.CurrentRoomId ?? ""), "褰撳墠浼氳瘽", PanelText),
            AddStatCard("鍏憡", $"{GetVisibleAnnouncements().Count} 鏉?, "鏈湴淇濈暀", GoodText));

        var section = AddModalSectionPanel("鏈湴鎿嶄綔", "涓嬮潰鐨勫姩浣滀細绔嬪嵆浣滅敤鍒板綋鍓嶆湰鍦版。妗堛€?, WarningText);
        section.AddChild(CreateSettingsActionRow(
            "娓呯┖鍏憡",
            "绉婚櫎褰撳墠澶у巺閲屼繚鐣欑殑鍏憡璁板綍銆?,
            "娓呯┖",
            ClearAnnouncementsFromSettings,
            ButtonTone.Secondary));
        section.AddChild(CreateSettingsActionRow(
            "閲嶇疆鎴樺満灞忚斀涓庝妇鎶?,
            "娓呴櫎鎴樻枟璁剧疆閲屽凡淇濆瓨鐨勬枃瀛椼€佽闊冲睆钄戒笌涓炬姤鏍囪銆?,
            "閲嶇疆",
            ResetBattlePreferencesFromSettings,
            ButtonTone.Secondary));
        section.AddChild(CreateSettingsActionRow(
            "閫€鍑虹櫥褰?,
            "娓呴櫎褰撳墠璐﹀彿浼氳瘽骞惰繑鍥炵櫥褰曠晫闈€?,
            "閫€鍑?,
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

    void ClearAnnouncementsFromSettings()
    {
        foreach (var entry in GetVisibleAnnouncements())
            clearedAnnouncementIds.Add(entry.Id);
        SaveAnnouncementState();
        ShowSettingsModal();
        ShowToast("鍏憡宸叉竻绌?);
    }

    void ResetBattlePreferencesFromSettings()
    {
        GameState.Instance?.ClearBattleCommunicationPreferences();
        ShowSettingsModal();
        ShowToast("鎴樺満灞忚斀涓庝妇鎶ヨ褰曞凡閲嶇疆");
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
        OpenFeatureModal("鍏憡", "澶у巺鏇存柊浼氭寜鏃ユ湡淇濈暀鍦ㄦ湰鍦帮紝宸茶鍚庡彲鎵嬪姩娓呯┖銆?);

        var visible = GetVisibleAnnouncements();
        var latestDate = visible.Count > 0 ? visible[0].DateKey : "--";
        var dayCount = CountAnnouncementDays(visible);
        AddModalSummaryRow(
            AddStatCard("鏈竻绌哄叕鍛?, $"{visible.Count} 鏉?, "褰撳墠鏈湴淇濈暀", visible.Count > 0 ? WarningText : GoodText),
            AddStatCard("鏃ユ湡", $"{dayCount} 澶?, "鎸夊ぉ褰掓。", PanelText),
            AddStatCard("鏈€杩戞洿鏂?, latestDate, "鏈湴璁板綍鏃ユ湡", new Color(0.64f, 0.90f, 1f)));

        var actionRow = new HBoxContainer { Name = "AnnouncementActionRow" };
        actionRow.AddThemeConstantOverride("separation", 8);
        var helper = AddLabel("鍏憡浼氫繚瀛樺湪鏈湴锛涙竻绌哄悗褰撳墠鍒楄〃涓嶄細鍐嶆鑷姩寮瑰洖銆?, 12, MutedText, HorizontalAlignment.Left);
        helper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        helper.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        actionRow.AddChild(helper);
        var clearButton = AddButton("娓呯┖鍏憡", ClearAnnouncements, ButtonTone.Secondary, 12);
        clearButton.Disabled = visible.Count == 0;
        actionRow.AddChild(clearButton);
        modalBody.AddChild(actionRow);

        if (visible.Count == 0)
        {
            modalBody.AddChild(AddEmptyStateCard("褰撳墠娌℃湁寰呮煡鐪嬪叕鍛娿€?, "鏂扮殑澶у巺鏇存柊浼氭寜鏃ユ湡鍑虹幇鍦ㄨ繖閲屻€?));
            ShowModal();
            return;
        }

        string currentDate = "";
        VBoxContainer? section = null;
        foreach (var entry in visible)
        {
            if (!string.Equals(currentDate, entry.DateKey, StringComparison.Ordinal))
            {
                currentDate = entry.DateKey;
                section = AddModalSectionPanel(currentDate, $"{CountAnnouncementsForDate(visible, currentDate)} 鏉″叕鍛?, WarningText);
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
        ShowToast("鍏憡宸叉竻绌?);
    }

    Panel CreateAnnouncementCard(AnnouncementEntry entry)
    {
        var card = new Panel
        {
            Name = "Announcement_" + entry.Id,
            CustomMinimumSize = new Vector2(0, 86)
        };
        MetalUiStyle.ApplyMetalPanel(card, MetalUiStyle.Steel, 1, 8, 4);
        AddTopAccentStripe(card, new Color(0.84f, 0.67f, 0.28f, 0.74f), 2);

        var box = AddVBox(card, "AnnouncementBox_" + entry.Id, 6, new Vector2(14, 12), new Vector2(-14, -12));
        var header = new HBoxContainer
        {
            Name = "AnnouncementHeader_" + entry.Id,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        header.AddThemeConstantOverride("separation", 8);
        box.AddChild(header);

        var title = AddLabel(entry.Title, 15, PanelText, HorizontalAlignment.Left);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(title);
        header.AddChild(CreateTag(entry.Kind, new Color(0.22f, 0.29f, 0.35f, 0.94f), new Color(0.94f, 0.97f, 0.99f)));

        var body = AddLabel(entry.Body, 12, MutedText, HorizontalAlignment.Left);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(body);
        return card;
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
                Title = FriendlyText(data.GetString("title"), "澶у巺鍏憡"),
                Body = FriendlyText(data.GetString("body"), ""),
                Kind = FriendlyText(data.GetString("kind"), "鍏憡")
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
            if (HasAnnouncementEntry(entry.Id))
                continue;
            announcementArchive.Add(entry);
            changed = true;
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
                Title = "澶у巺鍩虹闈㈡澘鎺ュ叆瀹屾垚",
                Body = "鎴块棿銆佸ソ鍙嬨€佷换鍔°€佺鎶€涓庢帓琛屾鍏ュ彛宸茬粡鎺ュ叆鐪熷疄闈㈡澘锛屽悗缁户缁ˉ鏈嶅姟绔暟鎹€?,
                Kind = "澶у巺"
            },
            new()
            {
                Id = $"{today.AddDays(-1):yyyyMMdd}_secondary-cache",
                DateKey = today.AddDays(-1).ToString("yyyy-MM-dd"),
                Title = "鎺掕姒滀笌閭€璇烽潰鏉挎彁閫?,
                Body = "鎺掕姒滃拰閭€璇峰鍔犱簡 45 绉掓湰鍦扮紦瀛橈紝棣栨鎵撳紑鍏堟樉绀轰笂娆＄粨鏋滐紝鍐嶅悗鍙板埛鏂般€?,
                Kind = "浼樺寲"
            },
            new()
            {
                Id = $"{today:yyyyMMdd}_ui-polish",
                DateKey = today.ToString("yyyy-MM-dd"),
                Title = "閭欢涓庡啗闇€鍟嗗簵鐣岄潰閲嶆帓",
                Body = "閭欢閭€璇锋敼鎴愬垎缁勫崱鐗囷紝鍐涢渶鍟嗗簵鏀规垚鍟嗗搧鍗＄墖锛岄伩鍏嶉暱鏂囨鎶婃寜閽尋鍑洪潰鏉裤€?,
                Kind = "鐣岄潰"
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

    void CloseModal()
    {
        activeFeatureModal = "";
        modalShade.Visible = false;
        modalPanel.Visible = false;
        ClearChildren(modalBody);
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
        && string.Equals(modalTitle.Text, "鑷畾涔夋埧闂?, StringComparison.Ordinal);

    static Color StatusColor(string status)
    {
        var lower = status.ToLowerInvariant();
        if (lower.Contains("绂荤嚎") || lower.Contains("off"))
            return MutedText;
        if (lower.Contains("娓告垙") || lower.Contains("鍖归厤") || lower.Contains("busy") || lower.Contains("match"))
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
            || value.Contains("閿?)
            || value.Contains("闁?)
            || value.Contains("闁?)
            || value.Contains("闁?))
            return fallback;
        return value;
    }

    static string LobbyRankBadgeTexturePath(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        var badge = "rank_badge_01_bronze_shield.png";
        if (ContainsAny(normalized, "鍏冨竻", "鐜嬭€?, "灏?, "marshal"))
            badge = "rank_badge_05_marshal_eagle.png";
        else if (ContainsAny(normalized, "涓婃牎", "灏戞牎", "major", "diamond", "閽荤煶"))
            badge = "rank_badge_04_major_crossed_sabers.png";
        else if (ContainsAny(normalized, "榛勯噾", "涓皦", "wing", "gold"))
            badge = "rank_badge_03_gold_wing_star.png";
        else if (ContainsAny(normalized, "鐧介摱", "灏戝皦", "silver"))
            badge = "rank_badge_02_silver_double_star.png";
        return UnityLobbyRoot + "WW2RankBadges/" + badge;
    }

    static Color LobbyRankColor(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        if (ContainsAny(normalized, "鍏冨竻", "鐜嬭€?, "灏?, "marshal"))
            return new Color(1f, 0.92f, 0.66f);
        if (ContainsAny(normalized, "涓婃牎", "灏戞牎", "major"))
            return new Color(0.96f, 0.84f, 0.60f);
        if (ContainsAny(normalized, "閽荤煶", "diamond"))
            return new Color(0.72f, 0.90f, 1f);
        if (ContainsAny(normalized, "榛勯噾", "涓皦", "wing", "gold"))
            return new Color(1f, 0.84f, 0.34f);
        if (ContainsAny(normalized, "鐧介摱", "灏戝皦", "silver"))
            return new Color(0.84f, 0.90f, 0.98f);
        return new Color(0.86f, 0.76f, 0.62f);
    }

    static string NormalizeRankTitle(string? rankTitle)
        => string.IsNullOrWhiteSpace(rankTitle) ? "鍒楀叺" : rankTitle.Trim();

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
        return MaxCustomRoomPlayers;
    }

    static bool IsSupportedCustomRoomSize(int maxPlayers)
        => maxPlayers == 2 || maxPlayers == 4 || maxPlayers == MaxCustomRoomPlayers;

    static string DescribeRoomModeHint(int maxPlayers) => maxPlayers switch
    {
        <= 2 => "1v1瀹炴椂鎴樻枟",
        <= 4 => "2v2缁勬埧",
        _ => "3v3缁勬埧"
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
}
