using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleHud : CanvasLayer
{
    const ulong CommandPanelLiveRefreshMs = 250UL;

    sealed class BattleTechUiState
    {
        public string Key { get; init; } = "";
        public Button Button { get; init; } = null!;
        public TextureProgressBar? CooldownFill { get; init; }
        public Label? CooldownLabel { get; init; }
        public Panel Card { get; init; } = null!;
    }

    const float HudWidth = 1280f;
    const float HudHeight = 720f;
    const float CommandPanelContentWidth = 356f;
    const float BattleHudMinimapLeft = 10f;
    const float BattleHudMinimapBottom = 68f;
    const float BattleHudMinimapWidth = 260f;
    const float BattleHudMinimapHeight = 195f;
    const float BattleHudActionGap = 10f;
    const float BattleHudActionButtonWidth = 136f;
    const float BattleHudActionButtonHeight = 42f;
    const float BattleHudTopBarHeight = 58f;
    const float BattleHudTechButtonBottom = BattleHudMinimapBottom + BattleHudMinimapHeight + BattleHudActionGap;
    const float BattleHudBuildButtonBottom = BattleHudTechButtonBottom + BattleHudActionButtonHeight + BattleHudActionGap;
    const string UnityFrameRoot = "res://assets/unity_migrated/Assets/Resources/frames/";
    const string UnityIconRoot = "res://assets/unity_migrated/Assets/Resources/icons_final/";
    const string UnityBattleHudRoot = "res://assets/unity_migrated/Assets/Resources/BattleHud/";
    const string UnityLobbyGenRoot = "res://assets/unity_migrated/Assets/Resources/LobbyGen/";
    const string CurrencyIconRoot = "res://assets/unity_migrated/Assets/Resources/UI/CurrencyIcons/";
    const string ButtonBackdropRoot = "res://assets/unity_migrated/Assets/Resources/UI/ButtonBackdrops/";

    Panel topPanel = null!;
    Control hudRoot = null!;
    Panel commandPanel = null!;
    Control commandPanelLockOverlay = null!;
    Label commandPanelTitle = null!;
    Label commandPanelLockLabel = null!;
    Label economyLabel = null!;
    Label goldValueLabel = null!;
    Label goldIncomeRateLabel = null!;
    Label powerValueLabel = null!;
    Label selectionLabel = null!;
    Label alertLabel = null!;
    VBoxContainer commandList = null!;
    Panel gameOverPanel = null!;
    Label gameOverTitle = null!;
    Label gameOverBody = null!;
    Control gameOverOverlay = null!;
    Panel gameOverResultBadge = null!;
    Label gameOverResultMark = null!;
    Control gameOverSummaryRow = null!;
    Panel[] gameOverSummaryCards = System.Array.Empty<Panel>();
    Label[] gameOverSummaryValueLabels = System.Array.Empty<Label>();
    Label gameOverReportMeta = null!;
    Label gameOverReportPlayerHeader = null!;
    Label gameOverReportCenterHeader = null!;
    Label gameOverReportEnemyHeader = null!;
    Label gameOverCountdownLabel = null!;
    GridContainer gameOverReportGrid = null!;
    readonly List<Label> gameOverReportPlayerValues = new();
    readonly List<Label> gameOverReportMetricLabels = new();
    readonly List<Label> gameOverReportEnemyValues = new();
    Panel battleCommunicationPanel = null!;
    Button battleCommunicationButton = null!;
    Panel battleCommunicationLogPlate = null!;
    Panel battleCommunicationComposerPlate = null!;
    Panel battleCommunicationMemberBadge = null!;
    Panel battleCommunicationUnreadDot = null!;
    ScrollContainer battleCommunicationScroll = null!;
    VBoxContainer battleCommunicationMessages = null!;
    Label battleCommunicationEmptyLabel = null!;
    Label battleCommunicationVoiceLabel = null!;
    Label battleCommunicationMemberCountLabel = null!;
    Label battleCommunicationTitleLabel = null!;
    Label battleCommunicationDetailLabel = null!;
    Label battleCommunicationLatestLabel = null!;
    LineEdit battleCommunicationInput = null!;
    Button battleCommunicationSendButton = null!;
    Button battleCommunicationVoiceButton = null!;
    ColorRect battleCommunicationDockTail = null!;
    readonly List<Label> battleCommunicationLines = new();
    BattleMinimap minimap = null!;
    Control minimapPanel = null!;
    Control bottomCommandBar = null!;
    Panel inputHintPanel = null!;
    Label inputHintLabel = null!;
    Button selectAllCommandButton = null!;
    Button stopCommandButton = null!;
    Button patrolCommandButton = null!;
    Button specialCommandButton = null!;
    Button parkAircraftCommandButton = null!;
    Button settingsButton = null!;
    Panel objectivePanel = null!;
    VBoxContainer objectiveList = null!;
    float objectiveRefreshTimer = 0f;
    Button objectiveRestoreBtn = null!;
    Control objectiveDialogRoot = null!;
    Panel objectiveDialogPanel = null!;
    VBoxContainer objectiveDialogList = null!;
    readonly HashSet<string> claimedObjectiveKeys = new();
    Button buildActionButton = null!;
    Button techActionButton = null!;
    Button commandPanelLockCancelButton = null!;
    readonly List<CanvasItem> gameplayHudItems = new();
    Control upgradeDialogRoot = null!;
    Panel upgradeDialogPanel = null!;
    Label upgradeDialogTitle = null!;
    Label upgradeDialogSummary = null!;
    Label upgradeDialogHint = null!;
    VBoxContainer upgradeRequirementList = null!;
    Button upgradeConfirmButton = null!;
    Button upgradeCancelButton = null!;
    Control settingsDialogRoot = null!;
    Panel settingsDialogPanel = null!;
    Label settingsDialogSubtitle = null!;
    Label settingsRosterStatusLabel = null!;
    VBoxContainer settingsParticipantList = null!;
    Label settingsParticipantTitle = null!;
    Label settingsParticipantRole = null!;
    Label settingsParticipantReceive = null!;
    Label settingsParticipantIntro = null!;
    Label settingsParticipantReportHint = null!;
    TextureRect settingsParticipantAvatar = null!;
    TextureRect settingsParticipantRankBadgeIcon = null!;
    Label settingsParticipantBadge = null!;
    Button settingsTextMuteButton = null!;
    Button settingsVoiceMuteButton = null!;
    Button settingsMuteAllButton = null!;
    Button settingsReportButton = null!;
    Button settingsReturnLobbyButton = null!;
    CheckButton settingsMainBaseIdentityToggle = null!;
    Label settingsMainBaseIdentityHint = null!;
    Control settingsReportDialogRoot = null!;
    Label settingsReportDialogTitle = null!;
    Node3D? techTargetRing;
    string activeTechTargetKey = "";
    readonly List<BattleTechUiState> techButtons = new();
    GameState.BattleParticipantProfile[] settingsParticipants = System.Array.Empty<GameState.BattleParticipantProfile>();
    string selectedBattleParticipantId = "";
    string lastKnownPeerId = "";
    string lastKnownPeerName = "";
    string pendingReportParticipantId = "";
    string pendingReportParticipantName = "";
    bool refreshingSettingsDisplayOptions;
    double lastInvasionAlertTime = -99.0;

    RtsBuilding? selectedBuilding;
    RtsUnit? selectedRepairUnit;
    RtsBuilding? pendingUpgradeBuilding;
    bool buildMenuOpen;
    bool techMenuOpen;
    int activeBuildMenuSectionIndex;
    int activeMainBaseInfoSectionIndex;
    int activeTechCategoryIndex;
    float gameOverCountdownRemaining;
    bool gameOverCountdownActive;
    bool gameOverTransitionTriggered;
    float battleCommunicationVoiceRemaining;
    bool battleCommunicationVoiceHolding;
    bool battleCommunicationExpanded;
    bool battleCommunicationUnreadWhileCollapsed;
    ulong nextBattleCommunicationVoicePulseAtMs;
    ulong nextCommandPanelLiveRefreshMs;
    string lastMapInteractionLockToken = "";
    string activeBattleVoiceParticipantId = "";
    string activeBattleVoiceSpeakerName = "";
    TextureRect commanderAvatarTexture = null!;
    TextureRect commanderRankBadgeTexture = null!;
    Label commanderRankLabel = null!;

    public override void _Ready()
    {
        AddToGroup("battle_hud");

        hudRoot = new Control
        {
            Name = "HudRoot",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(hudRoot);

        BuildTopPanel(hudRoot);
        BuildInputHintPanel(hudRoot);
        BuildBattleCommunicationPanel(hudRoot);
        BuildCommandPanel(hudRoot);
        BuildGameOverPanel(hudRoot);
        BuildMinimap(hudRoot);
        hudRoot.AddChild(new SelectionBoxOverlay
        {
            Name = "SelectionBoxOverlay",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        BuildUpgradeDialog(hudRoot);
        BuildSettingsDialog(hudRoot);

        if (GameState.Instance is not null)
        {
            GameState.Instance.SelectionChanged += OnSelectionChanged;
            GameState.Instance.SessionChanged += OnBattleSessionChanged;
            GameState.Instance.BattleCommunicationChanged += OnBattleCommunicationChanged;
            EnsureBattleCommunicationParticipants();
            OnSelectionChanged(new Godot.Collections.Array<Node>(GameState.Instance.Selected));
        }
        else
        {
            EnsureBattleCommunicationParticipants();
        }

        if (GameRelay.Instance is not null)
        {
            GameRelay.Instance.PeerJoined += OnNetworkPeerJoined;
            GameRelay.Instance.PeerLeft += OnNetworkPeerLeft;
        }

        RefreshBattleCommunicationState();
        BuildObjectivePanel(hudRoot);

        if (BattleGameManager.Instance is not null)
            BindManager(BattleGameManager.Instance);
        else
            CallDeferred(MethodName.BindDeferred);
    }

    public override void _ExitTree()
    {
        if (GameState.Instance is not null)
        {
            GameState.Instance.SelectionChanged -= OnSelectionChanged;
            GameState.Instance.SessionChanged -= OnBattleSessionChanged;
            GameState.Instance.BattleCommunicationChanged -= OnBattleCommunicationChanged;
        }

        if (GameRelay.Instance is not null)
        {
            GameRelay.Instance.PeerJoined -= OnNetworkPeerJoined;
            GameRelay.Instance.PeerLeft -= OnNetworkPeerLeft;
        }
    }

    public override void _Process(double delta)
    {
        if (gameOverOverlay.Visible)
        {
            UpdateGameOverCountdown((float)delta);
            return;
        }

        RefreshEconomy();
        RefreshSelectionLabel();
        RefreshCommandPanelLive();
        RefreshMapInteractionLockState();
        RefreshBottomCommandBar();
        RefreshInputHint();
        UpdateTechTargetingVisual();
        UpdateTechCooldownVisuals();
        UpdateBattleCommunicationPanel((float)delta);

        objectiveRefreshTimer += (float)delta;
        if (objectiveRefreshTimer >= 0.5f)
        {
            objectiveRefreshTimer = 0f;
            RefreshObjectives();
        }
    }

    void BuildTopPanel(Control root)
    {
        topPanel = TransparentPanel(Vector2.Zero, new Vector2(HudWidth, BattleHudTopBarHeight));
        topPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(topPanel);

        var avatarGroup = CreateCommanderHeader(topPanel);
        avatarGroup.Position = new Vector2(12f, 2f);

        var goldChip = new Control
        {
            Name = "GoldChip",
            Position = new Vector2(172f, 12f),
            Size = new Vector2(156f, 28f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topPanel.AddChild(goldChip);

        goldChip.AddChild(new TextureRect
        {
            Position = new Vector2(0f, 2f),
            Size = new Vector2(24f, 24f),
            Texture = LoadHudTexture(CurrencyIconRoot + "currency_gold_coin.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        goldValueLabel = HudLabel("--", 16, new Color(1f, 0.86f, 0.42f));
        goldValueLabel.Position = new Vector2(28f, 0f);
        goldValueLabel.Size = new Vector2(72f, 28f);
        goldValueLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        goldValueLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        goldValueLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        goldValueLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        goldValueLabel.AddThemeConstantOverride("outline_size", 2);
        goldChip.AddChild(goldValueLabel);

        goldIncomeRateLabel = HudLabel("+0/s", 11, new Color(0.90f, 1f, 0.78f));
        goldIncomeRateLabel.Position = new Vector2(104f, 5f);
        goldIncomeRateLabel.Size = new Vector2(62f, 18f);
        goldIncomeRateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        goldIncomeRateLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        goldIncomeRateLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        goldIncomeRateLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        goldIncomeRateLabel.AddThemeConstantOverride("outline_size", 2);
        goldChip.AddChild(goldIncomeRateLabel);

        var powerChip = new Control
        {
            Name = "PowerChip",
            Position = new Vector2(328f, 12f),
            Size = new Vector2(102f, 24f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topPanel.AddChild(powerChip);

        powerChip.AddChild(new TextureRect
        {
            Position = new Vector2(1f, 2f),
            Size = new Vector2(20f, 20f),
            Texture = LoadTrimmedHudTexture("res://assets/unity_migrated/Assets/Resources/UI/Icons/power_lightning.png", 1),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.74f, 0.96f, 0.82f, 0.96f)
        });

        powerValueLabel = HudLabel("--/--", 15, new Color(0.74f, 0.96f, 0.82f));
        powerValueLabel.Position = new Vector2(24f, -1f);
        powerValueLabel.Size = new Vector2(82f, 24f);
        powerValueLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        powerValueLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        powerValueLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        powerValueLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        powerValueLabel.AddThemeConstantOverride("outline_size", 2);
        powerChip.AddChild(powerValueLabel);

        economyLabel = HudLabel("", 17, new Color(0.82f, 0.96f, 1f));
        economyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        economyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        economyLabel.Position = new Vector2(430, 14);
        economyLabel.Size = new Vector2(402, 30);
        economyLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        economyLabel.AddThemeConstantOverride("outline_size", 3);
        topPanel.AddChild(economyLabel);

        selectionLabel = HudLabel("当前选择：未选择单位", 15, new Color(0.94f, 0.96f, 0.88f));
        selectionLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        selectionLabel.HorizontalAlignment = HorizontalAlignment.Right;
        selectionLabel.Position = new Vector2(800, 14);
        selectionLabel.Size = new Vector2(360, 30);
        selectionLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        selectionLabel.ClipText = true;
        selectionLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        selectionLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        selectionLabel.AddThemeConstantOverride("outline_size", 3);
        topPanel.AddChild(selectionLabel);

        alertLabel = HudLabel("", 15, new Color(1f, 0.72f, 0.34f));
        alertLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        alertLabel.HorizontalAlignment = HorizontalAlignment.Center;
        alertLabel.Position = new Vector2(310, BattleHudTopBarHeight + 2f);
        alertLabel.Size = new Vector2(660, 30);
        alertLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        alertLabel.AddThemeConstantOverride("outline_size", 3);
        root.AddChild(alertLabel);

        settingsButton = new Button
        {
            Position = new Vector2(1180, 10),
            Size = new Vector2(40, 36)
        };
        settingsButton.Icon = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/icons3/gear.png");
        settingsButton.ExpandIcon = true;
        ApplyButtonStyle(settingsButton, new Color(0.16f, 0.18f, 0.20f, 0.72f), new Color(1f, 0.84f, 0.30f, 0.88f), 18);
        settingsButton.Pressed += ToggleSettingsDialog;
        settingsButton.TooltipText = "设置";
        root.AddChild(settingsButton);
        gameplayHudItems.Add(topPanel);
        gameplayHudItems.Add(alertLabel);
        gameplayHudItems.Add(settingsButton);
        RefreshCommanderHeaderVisuals();
    }

    void BuildCommandPanel(Control root)
    {
        AddBottomCommandBar(root);
        buildActionButton = AddLeftActionButton(root, "建造", "建", BattleHudBuildButtonBottom, new Color(0.20f, 0.34f, 0.14f, 0.96f), () =>
        {
            buildMenuOpen = true;
            techMenuOpen = false;
            GameState.Instance?.SetSelection(System.Array.Empty<Node>());
            ShowAlert("打开建造面板");
            RefreshCommandPanel();
        });
        techActionButton = AddLeftActionButton(root, "科技", "技", BattleHudTechButtonBottom, new Color(0.16f, 0.30f, 0.54f, 0.96f), () =>
        {
            buildMenuOpen = false;
            techMenuOpen = true;
            ShowAlert(selectedBuilding switch
            {
                null => "选择兵工厂、坦克厂等建筑后研究科技",
                { IsMainBase: true } => "主基地显示信息面板，可在右侧查看升级条件",
                _ => "已打开当前建筑科技"
            });
            RefreshCommandPanel();
        });

        commandPanel = Panel(new Vector2(852, 126), new Vector2(414, 372), new Color(0.035f, 0.045f, 0.025f, 0.92f));
        commandPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        commandPanel.ClipContents = true;
        commandPanel.GuiInput += ConsumeHudPointerInput;
        root.AddChild(commandPanel);
        AddCommandPanelChrome(commandPanel);

        commandPanelTitle = HudLabel("作战指令", 18, new Color(1f, 0.88f, 0.58f));
        commandPanelTitle.HorizontalAlignment = HorizontalAlignment.Center;
        commandPanelTitle.Position = new Vector2(18, 8);
        commandPanelTitle.Size = new Vector2(332, 28);
        commandPanelTitle.AutowrapMode = TextServer.AutowrapMode.Off;
        commandPanelTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        commandPanel.AddChild(commandPanelTitle);

        var commandPanelCloseButton = new Button
        {
            Text = "×",
            Position = new Vector2(368, 8),
            Size = new Vector2(28, 28),
            Flat = true,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand
        };
        commandPanelCloseButton.AddThemeFontSizeOverride("font_size", 20);
        commandPanelCloseButton.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        
        var hoverBox = new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.12f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var pressedBox = new StyleBoxFlat
        {
            BgColor = new Color(1f, 1f, 1f, 0.20f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        
        commandPanelCloseButton.AddThemeStyleboxOverride("hover", hoverBox);
        commandPanelCloseButton.AddThemeStyleboxOverride("pressed", pressedBox);
        commandPanelCloseButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        
        commandPanelCloseButton.AddThemeColorOverride("font_color", new Color(0.96f, 0.94f, 0.82f, 0.80f));
        commandPanelCloseButton.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f, 1f));
        commandPanelCloseButton.AddThemeColorOverride("font_pressed_color", new Color(0.95f, 0.74f, 0.24f, 1f));
        
        commandPanelCloseButton.Pressed += CloseCommandPanelView;
        commandPanel.AddChild(commandPanelCloseButton);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(18, 40),
            Size = new Vector2(378, 312),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        scroll.GuiInput += ConsumeHudPointerInput;
        commandPanel.AddChild(scroll);

        commandList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(CommandPanelContentWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        commandList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(commandList);
        BuildCommandPanelLockOverlay(commandPanel);
        commandPanel.Visible = false;
        gameplayHudItems.Add(commandPanel);
    }

    void BuildCommandPanelLockOverlay(Control parent)
    {
        commandPanelLockOverlay = new Control
        {
            Name = "CommandPanelLockOverlay",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        parent.AddChild(commandPanelLockOverlay);

        var dim = new ColorRect
        {
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0.01f, 0.02f, 0.02f, 0.58f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        commandPanelLockOverlay.AddChild(dim);

        var lockPanel = Panel(new Vector2(34f, 78f), new Vector2(346f, 146f), new Color(0.028f, 0.040f, 0.044f, 0.97f));
        commandPanelLockOverlay.AddChild(lockPanel);

        commandPanelLockLabel = HudLabel("", 15, new Color(0.92f, 0.96f, 0.94f));
        commandPanelLockLabel.HorizontalAlignment = HorizontalAlignment.Center;
        commandPanelLockLabel.Position = new Vector2(16f, 18f);
        commandPanelLockLabel.Size = new Vector2(314f, 66f);
        commandPanelLockLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        commandPanelLockLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        commandPanelLockLabel.AddThemeConstantOverride("outline_size", 2);
        lockPanel.AddChild(commandPanelLockLabel);

        commandPanelLockCancelButton = new Button
        {
            Text = "取消当前操作",
            Position = new Vector2(92f, 96f),
            Size = new Vector2(162f, 34f)
        };
        ApplyButtonStyle(commandPanelLockCancelButton, new Color(0.32f, 0.18f, 0.12f, 0.96f), new Color(1f, 0.74f, 0.36f, 0.84f), 13);
        commandPanelLockCancelButton.Pressed += CancelActiveMapInteractionFromPanel;
        lockPanel.AddChild(commandPanelLockCancelButton);
    }

    void BuildInputHintPanel(Control root)
    {
        inputHintPanel = Panel(new Vector2(356, 614), new Vector2(568, 30), new Color(0.018f, 0.028f, 0.032f, 0.58f));
        inputHintPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        inputHintPanel.Visible = false;
        root.AddChild(inputHintPanel);

        inputHintLabel = HudLabel("", 12, new Color(0.82f, 0.92f, 0.94f, 0.94f));
        inputHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        inputHintLabel.VerticalAlignment = VerticalAlignment.Center;
        inputHintLabel.Position = new Vector2(12, 3);
        inputHintLabel.Size = new Vector2(544, 24);
        inputHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        inputHintLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        inputHintLabel.AddThemeConstantOverride("outline_size", 2);
        inputHintPanel.AddChild(inputHintLabel);

        gameplayHudItems.Add(inputHintPanel);
        RefreshInputHint();
    }

    void BuildBattleCommunicationPanel(Control root)
    {
        battleCommunicationPanel = Panel(new Vector2(12, 474), new Vector2(76, 78), new Color(0.018f, 0.028f, 0.034f, 0.82f));
        battleCommunicationPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        battleCommunicationPanel.ClipContents = true;
        root.AddChild(battleCommunicationPanel);
        AddTextureFrame(battleCommunicationPanel, UnityFrameRoot + "panel_task_frame.png", new Color(1f, 1f, 1f, 0.14f));

        battleCommunicationDockTail = new ColorRect
        {
            Position = new Vector2(18, 54),
            Size = new Vector2(14, 14),
            Color = new Color(0.10f, 0.16f, 0.22f, 0.96f),
            RotationDegrees = 45f,
            PivotOffset = new Vector2(7f, 7f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        battleCommunicationPanel.AddChild(battleCommunicationDockTail);

        battleCommunicationButton = new Button
        {
            Position = new Vector2(10, 8),
            Size = new Vector2(56, 48),
            Icon = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png"),
            ExpandIcon = true
        };
        ApplyButtonStyle(battleCommunicationButton, new Color(0.10f, 0.16f, 0.22f, 0.96f), new Color(0.78f, 0.62f, 0.18f, 0.82f), 10);
        battleCommunicationButton.Pressed += ToggleBattleCommunicationPanel;
        battleCommunicationPanel.AddChild(battleCommunicationButton);

        battleCommunicationMemberBadge = new Panel
        {
            Position = new Vector2(48, 6),
            Size = new Vector2(18, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        battleCommunicationMemberBadge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.22f, 0.24f, 0.96f),
            BorderColor = new Color(1f, 0.80f, 0.38f, 0.82f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        battleCommunicationPanel.AddChild(battleCommunicationMemberBadge);

        battleCommunicationUnreadDot = new Panel
        {
            Position = new Vector2(56, 24),
            Size = new Vector2(12, 12),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        battleCommunicationUnreadDot.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.80f, 0.24f, 0.98f),
            BorderColor = new Color(0.10f, 0.08f, 0.02f, 0.92f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        });
        battleCommunicationPanel.AddChild(battleCommunicationUnreadDot);

        battleCommunicationMemberCountLabel = HudLabel("", 10, new Color(0.96f, 0.98f, 0.92f));
        battleCommunicationMemberCountLabel.HorizontalAlignment = HorizontalAlignment.Center;
        battleCommunicationMemberCountLabel.VerticalAlignment = VerticalAlignment.Center;
        battleCommunicationMemberCountLabel.Size = new Vector2(18, 18);
        battleCommunicationMemberCountLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.94f));
        battleCommunicationMemberCountLabel.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationMemberBadge.AddChild(battleCommunicationMemberCountLabel);

        battleCommunicationVoiceLabel = HudLabel("", 11, new Color(0.76f, 0.92f, 0.96f, 0.96f));
        battleCommunicationVoiceLabel.HorizontalAlignment = HorizontalAlignment.Center;
        battleCommunicationVoiceLabel.VerticalAlignment = VerticalAlignment.Center;
        battleCommunicationVoiceLabel.Position = new Vector2(8, 59);
        battleCommunicationVoiceLabel.Size = new Vector2(60, 14);
        battleCommunicationVoiceLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.94f));
        battleCommunicationVoiceLabel.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationPanel.AddChild(battleCommunicationVoiceLabel);

        battleCommunicationTitleLabel = HudLabel("战地通讯", 14, new Color(1f, 0.88f, 0.58f));
        battleCommunicationTitleLabel.Position = new Vector2(82, 10);
        battleCommunicationTitleLabel.Size = new Vector2(146, 20);
        battleCommunicationTitleLabel.Visible = false;
        battleCommunicationPanel.AddChild(battleCommunicationTitleLabel);

        battleCommunicationDetailLabel = HudLabel("", 12, new Color(0.76f, 0.90f, 0.96f));
        battleCommunicationDetailLabel.Position = new Vector2(82, 30);
        battleCommunicationDetailLabel.Size = new Vector2(254, 18);
        battleCommunicationDetailLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        battleCommunicationDetailLabel.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationDetailLabel.Visible = false;
        battleCommunicationPanel.AddChild(battleCommunicationDetailLabel);

        battleCommunicationLatestLabel = HudLabel("", 11, new Color(0.76f, 0.84f, 0.88f, 0.96f));
        battleCommunicationLatestLabel.Position = new Vector2(82, 50);
        battleCommunicationLatestLabel.Size = new Vector2(282, 18);
        battleCommunicationLatestLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        battleCommunicationLatestLabel.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationLatestLabel.Visible = false;
        battleCommunicationPanel.AddChild(battleCommunicationLatestLabel);

        battleCommunicationLogPlate = Panel(new Vector2(12, 74), new Vector2(392, 108), new Color(0.020f, 0.032f, 0.038f, 0.86f));
        battleCommunicationLogPlate.MouseFilter = Control.MouseFilterEnum.Ignore;
        battleCommunicationLogPlate.Visible = false;
        battleCommunicationPanel.AddChild(battleCommunicationLogPlate);

        battleCommunicationMessages = new VBoxContainer
        {
            Position = Vector2.Zero,
            Size = new Vector2(366, 0),
            CustomMinimumSize = new Vector2(366, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        battleCommunicationMessages.AddThemeConstantOverride("separation", 6);
        battleCommunicationScroll = new ScrollContainer
        {
            Position = new Vector2(18, 80),
            Size = new Vector2(380, 96),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        battleCommunicationScroll.AddChild(battleCommunicationMessages);
        battleCommunicationPanel.AddChild(battleCommunicationScroll);

        battleCommunicationEmptyLabel = HudLabel("", 12, new Color(0.78f, 0.86f, 0.90f));
        battleCommunicationEmptyLabel.Position = new Vector2(28, 96);
        battleCommunicationEmptyLabel.Size = new Vector2(360, 58);
        battleCommunicationEmptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        battleCommunicationEmptyLabel.VerticalAlignment = VerticalAlignment.Center;
        battleCommunicationEmptyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        battleCommunicationEmptyLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        battleCommunicationEmptyLabel.AddThemeConstantOverride("outline_size", 2);
        battleCommunicationEmptyLabel.Visible = false;
        battleCommunicationEmptyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        battleCommunicationPanel.AddChild(battleCommunicationEmptyLabel);

        battleCommunicationComposerPlate = Panel(new Vector2(12, 188), new Vector2(392, 34), new Color(0.018f, 0.030f, 0.036f, 0.88f));
        battleCommunicationComposerPlate.MouseFilter = Control.MouseFilterEnum.Ignore;
        battleCommunicationComposerPlate.Visible = false;
        battleCommunicationPanel.AddChild(battleCommunicationComposerPlate);

        battleCommunicationVoiceButton = new Button
        {
            Text = "按住语音",
            Position = new Vector2(18, 191),
            Size = new Vector2(60, 28),
            Visible = false
        };
        ApplyButtonStyle(battleCommunicationVoiceButton, new Color(0.16f, 0.30f, 0.54f, 0.96f), new Color(0.70f, 0.90f, 1f, 0.86f), 10);
        battleCommunicationVoiceButton.ButtonDown += BeginBattleVoiceTransmit;
        battleCommunicationVoiceButton.ButtonUp += EndBattleVoiceTransmit;
        battleCommunicationPanel.AddChild(battleCommunicationVoiceButton);

        battleCommunicationInput = new LineEdit
        {
            Position = new Vector2(86, 191),
            Size = new Vector2(236, 28),
            Visible = false,
            PlaceholderText = "输入战场消息，回车发送",
            ClearButtonEnabled = true,
            MaxLength = 60
        };
        battleCommunicationInput.TextChanged += OnBattleCommunicationInputChanged;
        battleCommunicationInput.TextSubmitted += OnBattleCommunicationTextSubmitted;
        battleCommunicationPanel.AddChild(battleCommunicationInput);

        battleCommunicationSendButton = new Button
        {
            Text = "发送",
            Position = new Vector2(330, 191),
            Size = new Vector2(68, 28),
            Visible = false
        };
        ApplyButtonStyle(battleCommunicationSendButton, new Color(0.18f, 0.30f, 0.42f, 0.96f), new Color(0.72f, 0.92f, 1f, 0.84f), 11);
        battleCommunicationSendButton.Pressed += SendBattleCommunicationText;
        battleCommunicationPanel.AddChild(battleCommunicationSendButton);

        gameplayHudItems.Add(battleCommunicationPanel);

        RefreshBattleCommunicationPanelLayout();
    }

    void BindDeferred()
    {
        if (BattleGameManager.Instance is not null)
            BindManager(BattleGameManager.Instance);
    }

    void BindManager(BattleGameManager manager)
    {
        manager.EconomyChanged += OnBattleEconomyChanged;
        manager.GameEnded += ShowGameOver;
        OnBattleEconomyChanged();
    }

    void OnBattleEconomyChanged()
    {
        RefreshEconomy();

        if (commandPanel is not null && (commandPanel.Visible
            || selectedBuilding is not null
            || buildMenuOpen
            || techMenuOpen
            || !string.IsNullOrEmpty(BattleGameManager.Instance?.PendingBuildKey)))
        {
            RefreshCommandPanel();
            RefreshInputHint();
        }
    }

    void OnBattleCommunicationChanged()
    {
        RefreshBattleCommunicationState();
        if (settingsDialogRoot is not null && settingsDialogRoot.Visible)
        {
            RefreshSettingsParticipantList();
            RefreshSettingsParticipantDetails();
        }
    }

    void OnBattleSessionChanged()
    {
        RefreshCommanderHeaderVisuals();
        RefreshEconomy();
        RefreshSettingsDisplayOptions();
        RefreshMainBaseIdentityLabels();
        if (settingsDialogRoot is not null && settingsDialogRoot.Visible)
            RefreshSettingsParticipantDetails();
    }

    void OnNetworkPeerJoined(string peerId)
    {
        lastKnownPeerId = string.IsNullOrWhiteSpace(peerId) ? lastKnownPeerId : peerId.Trim();
        EnsureBattleCommunicationParticipants();
        ShowAlert("队友已加入战场通信");
    }

    void OnNetworkPeerLeft()
    {
        battleCommunicationVoiceHolding = false;
        battleCommunicationVoiceRemaining = 0f;
        activeBattleVoiceParticipantId = "";
        activeBattleVoiceSpeakerName = "";
        lastKnownPeerId = "";
        lastKnownPeerName = "";
        EnsureBattleCommunicationParticipants();
        RefreshBattleCommunicationHeader();
        RefreshBattleCommunicationComposerState();
        ShowAlert("队友已离开或断开连接");
    }

    void EnsureBattleCommunicationParticipants()
    {
        if (GameState.Instance is null)
            return;

        var participants = new List<GameState.BattleParticipantProfile>();
        var localDisplayName = string.IsNullOrWhiteSpace(GameState.Instance.Username) ? "我方指挥官" : GameState.Instance.Username.Trim();
        participants.Add(new GameState.BattleParticipantProfile
        {
            ParticipantId = string.IsNullOrWhiteSpace(GameState.Instance.UserId) ? "local-player" : GameState.Instance.UserId,
            DisplayName = localDisplayName,
            Role = "我方",
            Intro = $"当前操作者：{localDisplayName}",
            IsLocalPlayer = true,
            CanReport = false
        });

        if (!string.IsNullOrWhiteSpace(GameState.Instance.CurrentRoomId))
        {
            var relay = GameRelay.Instance;
            if (!string.IsNullOrWhiteSpace(lastKnownPeerId) || relay?.PeerConnected == true)
            {
                var teammateName = string.IsNullOrWhiteSpace(lastKnownPeerName) ? "队友" : lastKnownPeerName.Trim();
                var teammateId = string.IsNullOrWhiteSpace(lastKnownPeerId)
                    ? $"room-peer:{GameState.Instance.CurrentRoomId}"
                    : lastKnownPeerId;
                participants.Add(new GameState.BattleParticipantProfile
                {
                    ParticipantId = teammateId,
                    DisplayName = teammateName,
                    Role = relay?.IsHost == true ? "友军支援" : "联机队友",
                    Intro = $"{teammateName} 来自当前联机房间，可在这里屏蔽其文字、语音，或提交举报。",
                    CanReport = true
                });
            }
            else
            {
                participants.Add(new GameState.BattleParticipantProfile
                {
                    ParticipantId = $"room-waiting:{GameState.Instance.CurrentRoomId}",
                    DisplayName = "等待队友",
                    Role = "房间成员",
                    Intro = "当前房间尚未检测到队友连接，队友加入后会自动显示在这里。",
                    CanReport = false
                });
            }
        }
        else
        {
            participants.Add(new GameState.BattleParticipantProfile
            {
                ParticipantId = "local-ai",
                DisplayName = "本地演练",
                Role = "离线模式",
                Intro = "当前战斗未连接联机房间，因此没有真实队友聊天/语音流可以屏蔽。",
                CanReport = false
            });
        }

        GameState.Instance.SetBattleParticipants(participants);
    }

    void RefreshBattleCommunicationState()
    {
        settingsParticipants = GameState.Instance?.GetBattleParticipants() ?? System.Array.Empty<GameState.BattleParticipantProfile>();
        if (string.IsNullOrEmpty(selectedBattleParticipantId) || settingsParticipants.All(profile => profile.ParticipantId != selectedBattleParticipantId))
            selectedBattleParticipantId = settingsParticipants.FirstOrDefault(profile => !profile.IsLocalPlayer)?.ParticipantId
                ?? settingsParticipants.FirstOrDefault()?.ParticipantId
                ?? "";

        if (settingsDialogSubtitle is not null)
        {
            settingsDialogSubtitle.Text = string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId)
                ? "本地演练暂无真实队友，联机房间中可在此屏蔽文字/语音并举报队友。"
                : $"房间 {ShortRoomId(GameState.Instance?.CurrentRoomId ?? "")} 的队友通信控制";
        }
        RefreshSettingsDisplayOptions();
        RefreshBattleCommunicationComposerState();
        RefreshBattleCommunicationHeader();
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void RefreshBattleCommunicationHeader()
    {
        if (battleCommunicationVoiceLabel is null || battleCommunicationMemberCountLabel is null)
            return;

        var teammateCount = ConnectedBattleTeammateCount();
        var canTransmit = CanTransmitBattleCommunication();
        var localVoiceActive = IsLocalBattleVoiceActive();
        var remoteVoiceActive = IsRemoteBattleVoiceActive();

        battleCommunicationMemberCountLabel.Text = teammateCount.ToString();
        battleCommunicationMemberCountLabel.Visible = teammateCount > 0;
        if (battleCommunicationMemberBadge is not null)
        {
            battleCommunicationMemberBadge.Visible = teammateCount > 0;
            battleCommunicationMemberBadge.Position = battleCommunicationExpanded
                ? new Vector2(382, 12)
                : new Vector2(48, 6);
        }

        string shortStatus;
        string detail;
        Color shortColor;
        Color detailColor;
        if (localVoiceActive)
        {
            shortStatus = "发送";
            detail = "正在发送语音状态";
            shortColor = new Color(1f, 0.78f, 0.70f, 0.98f);
            detailColor = new Color(1f, 0.84f, 0.78f, 0.96f);
        }
        else if (remoteVoiceActive)
        {
            shortStatus = "接收";
            detail = $"{ResolveActiveBattleVoiceSpeaker()} 正在通话";
            shortColor = new Color(0.82f, 0.94f, 1f, 0.98f);
            detailColor = new Color(0.76f, 0.90f, 1f, 0.96f);
        }
        else if (canTransmit)
        {
            shortStatus = "联机";
            detail = teammateCount > 0
                ? $"{teammateCount} 名成员在线，可随时联络"
                : "战地通讯链路已就绪";
            shortColor = new Color(0.78f, 0.92f, 1f, 0.96f);
            detailColor = new Color(0.76f, 0.90f, 0.98f, 0.96f);
        }
        else if (!string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId))
        {
            shortStatus = "待命";
            detail = "等待队友连接";
            shortColor = new Color(0.96f, 0.84f, 0.56f, 0.96f);
            detailColor = new Color(0.96f, 0.88f, 0.70f, 0.96f);
        }
        else
        {
            shortStatus = "离线";
            detail = "离线演练";
            shortColor = new Color(0.82f, 0.84f, 0.86f, 0.92f);
            detailColor = new Color(0.76f, 0.82f, 0.86f, 0.92f);
        }

        battleCommunicationVoiceLabel.Text = shortStatus;
        battleCommunicationVoiceLabel.AddThemeColorOverride("font_color", shortColor);
        if (battleCommunicationDetailLabel is not null)
        {
            battleCommunicationDetailLabel.Text = detail;
            battleCommunicationDetailLabel.AddThemeColorOverride("font_color", detailColor);
        }

        if (battleCommunicationLatestLabel is not null)
        {
            battleCommunicationLatestLabel.Text = BuildBattleCommunicationLatestHint(canTransmit);
            battleCommunicationLatestLabel.AddThemeColorOverride("font_color",
                battleCommunicationUnreadWhileCollapsed && !battleCommunicationExpanded
                    ? new Color(1f, 0.88f, 0.54f, 0.98f)
                    : new Color(0.76f, 0.84f, 0.88f, 0.96f));
        }

        RefreshBattleCommunicationDockButtonState();
    }

    void RefreshBattleCommunicationDockButtonState()
    {
        if (battleCommunicationButton is null)
            return;

        Color buttonBg;
        Color accent;
        if (IsLocalBattleVoiceActive())
        {
            buttonBg = new Color(0.65f, 0.14f, 0.12f, 0.98f);
            accent = new Color(1f, 0.76f, 0.66f, 0.90f);
        }
        else if (IsRemoteBattleVoiceActive())
        {
            buttonBg = new Color(0.24f, 0.46f, 0.72f, 0.98f);
            accent = new Color(0.74f, 0.92f, 1f, 0.90f);
        }
        else if (battleCommunicationExpanded)
        {
            buttonBg = new Color(0.16f, 0.30f, 0.54f, 0.96f);
            accent = new Color(0.72f, 0.90f, 1f, 0.86f);
        }
        else if (battleCommunicationUnreadWhileCollapsed)
        {
            buttonBg = new Color(0.30f, 0.24f, 0.08f, 0.98f);
            accent = new Color(1f, 0.84f, 0.28f, 0.92f);
        }
        else
        {
            buttonBg = new Color(0.10f, 0.16f, 0.22f, 0.96f);
            accent = new Color(0.78f, 0.62f, 0.18f, 0.82f);
        }

        ApplyButtonStyle(battleCommunicationButton, buttonBg, accent, 10);
        if (battleCommunicationDockTail is not null)
        {
            battleCommunicationDockTail.Color = buttonBg;
            battleCommunicationDockTail.Visible = !battleCommunicationExpanded;
        }

        if (battleCommunicationUnreadDot is not null)
            battleCommunicationUnreadDot.Visible = !battleCommunicationExpanded && battleCommunicationUnreadWhileCollapsed;
    }

    bool IsLocalBattleVoiceActive()
        => battleCommunicationVoiceHolding
            || (battleCommunicationVoiceRemaining > 0f
                && string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal));

    bool IsRemoteBattleVoiceActive()
        => battleCommunicationVoiceRemaining > 0f
            && !string.IsNullOrWhiteSpace(activeBattleVoiceParticipantId)
            && !string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal);

    string ResolveActiveBattleVoiceSpeaker()
    {
        if (!string.IsNullOrWhiteSpace(activeBattleVoiceSpeakerName))
            return activeBattleVoiceSpeakerName.Trim();

        if (!string.IsNullOrWhiteSpace(activeBattleVoiceParticipantId))
        {
            var participant = settingsParticipants.FirstOrDefault(profile =>
                string.Equals(profile.ParticipantId, activeBattleVoiceParticipantId, StringComparison.Ordinal));
            if (participant is not null && !string.IsNullOrWhiteSpace(participant.DisplayName))
                return participant.DisplayName.Trim();
        }

        return string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal)
            ? LocalBattleSpeakerName()
            : "队友";
    }

    string BuildBattleCommunicationLatestHint(bool canTransmit)
    {
        var latest = LatestBattleCommunicationPreview(48);
        if (!string.IsNullOrWhiteSpace(latest))
            return $"{(battleCommunicationUnreadWhileCollapsed ? "新消息" : "最新")}: {latest}";

        if (IsLocalBattleVoiceActive())
            return "松开语音键后会结束当前通话状态";
        if (IsRemoteBattleVoiceActive())
            return $"{ResolveActiveBattleVoiceSpeaker()} 的语音链路活动中";
        if (canTransmit)
            return "回车发送文字，按住左侧按钮发送语音";
        if (!string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId))
            return "队友接入后会自动开放文字与语音发送";
        return "进入联机房间后会启用完整战地通讯";
    }

    string LatestBattleCommunicationPreview(int maxLength)
    {
        var latest = battleCommunicationLines.LastOrDefault(line =>
            line is not null
            && GodotObject.IsInstanceValid(line)
            && !string.IsNullOrWhiteSpace(line.Text));
        if (latest is null || string.IsNullOrWhiteSpace(latest.Text))
            return "";

        var normalized = latest.Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalized.Length <= maxLength)
            return normalized;
        return normalized[..Math.Max(0, maxLength - 3)] + "...";
    }

    void UpdateBattleCommunicationPanel(float delta)
    {
        if (battleCommunicationVoiceHolding && Time.GetTicksMsec() >= nextBattleCommunicationVoicePulseAtMs)
        {
            SendBattleCommunicationVoiceSignal(false);
            nextBattleCommunicationVoicePulseAtMs = Time.GetTicksMsec() + 900UL;
        }

        if (battleCommunicationVoiceRemaining > 0f)
        {
            battleCommunicationVoiceRemaining = Mathf.Max(0f, battleCommunicationVoiceRemaining - delta);
            if (battleCommunicationVoiceRemaining <= 0f)
            {
                activeBattleVoiceParticipantId = "";
                activeBattleVoiceSpeakerName = "";
                RefreshBattleCommunicationHeader();
        RefreshBattleCommunicationComposerState();
            }
        }
    }

    void ToggleBattleCommunicationPanel()
    {
        if (battleCommunicationScroll is null)
            return;

        battleCommunicationExpanded = !battleCommunicationExpanded;
        if (battleCommunicationExpanded)
        {
            battleCommunicationUnreadWhileCollapsed = false;
            battleCommunicationPanel.MoveToFront();
        }
        else if (battleCommunicationVoiceHolding)
        {
            battleCommunicationVoiceHolding = false;
            if (string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal))
            {
                activeBattleVoiceParticipantId = "";
                activeBattleVoiceSpeakerName = "";
                battleCommunicationVoiceRemaining = 0f;
            }
        }

        RefreshBattleCommunicationPanelLayout();
        RefreshBattleCommunicationHeader();
        RefreshBattleCommunicationComposerState();
        if (battleCommunicationExpanded && CanTransmitBattleCommunication())
            battleCommunicationInput?.GrabFocus();
    }

    void RefreshBattleCommunicationPanelLayout()
    {
        if (battleCommunicationPanel is null
            || battleCommunicationButton is null
            || battleCommunicationScroll is null
            || battleCommunicationEmptyLabel is null
            || battleCommunicationLogPlate is null
            || battleCommunicationComposerPlate is null
            || battleCommunicationTitleLabel is null
            || battleCommunicationDetailLabel is null
            || battleCommunicationLatestLabel is null)
            return;

        var showLog = battleCommunicationExpanded;
        var canTransmit = CanTransmitBattleCommunication();
        var hasMessages = HasBattleCommunicationMessages();
        // 允许在本地离线或等待队友时显示输入框和发送按钮，方便测试消息和冒泡提示
        var allowComposer = true;

        battleCommunicationPanel.Size = showLog
            ? allowComposer ? new Vector2(416, 228) : new Vector2(416, 196)
            : new Vector2(76, 78);
        battleCommunicationButton.Position = new Vector2(10, 8);
        battleCommunicationButton.Size = new Vector2(56, 48);
        battleCommunicationTitleLabel.Visible = showLog;
        battleCommunicationDetailLabel.Visible = showLog;
        battleCommunicationLatestLabel.Visible = showLog;

        battleCommunicationLogPlate.Position = new Vector2(12, 74);
        battleCommunicationLogPlate.Size = new Vector2(392, allowComposer ? 108 : 116);
        battleCommunicationLogPlate.Visible = showLog;
        battleCommunicationScroll.Position = new Vector2(18, 80);
        battleCommunicationScroll.Size = new Vector2(380, allowComposer ? 96 : 104);
        battleCommunicationScroll.Visible = showLog && hasMessages;
        battleCommunicationMessages.Visible = showLog && hasMessages;
        battleCommunicationComposerPlate.Position = new Vector2(12, 188);
        battleCommunicationComposerPlate.Size = new Vector2(392, 34);
        battleCommunicationComposerPlate.Visible = showLog && allowComposer;
        battleCommunicationInput.Visible = showLog && allowComposer;
        battleCommunicationSendButton.Visible = showLog && allowComposer;
        battleCommunicationVoiceButton.Visible = showLog && allowComposer;
        battleCommunicationInput.Position = new Vector2(86, 191);
        battleCommunicationInput.Size = new Vector2(236, 28);
        battleCommunicationSendButton.Position = new Vector2(330, 191);
        battleCommunicationSendButton.Size = new Vector2(68, 28);
        battleCommunicationVoiceButton.Position = new Vector2(18, 191);
        battleCommunicationVoiceButton.Size = new Vector2(60, 28);
        battleCommunicationEmptyLabel.Visible = showLog && !hasMessages;
        if (showLog && !hasMessages)
            battleCommunicationEmptyLabel.Text = BattleCommunicationEmptyStateText(canTransmit);
        battleCommunicationEmptyLabel.Position = new Vector2(28, allowComposer ? 96 : 100);
        battleCommunicationEmptyLabel.Size = new Vector2(360, allowComposer ? 60 : 70);

        UpdateBattleCommunicationMessageWidths(Mathf.Max(220f, battleCommunicationScroll.Size.X - 28f));
        if (showLog)
        RefreshBattleCommunicationComposerState();
    }

    void RefreshBattleCommunicationComposerState()
    {
        if (battleCommunicationInput is null || battleCommunicationSendButton is null || battleCommunicationVoiceButton is null)
            return;

        var canTransmit = CanTransmitBattleCommunication();
        if (!canTransmit)
        {
            battleCommunicationVoiceHolding = false;
            if (string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal))
            {
                activeBattleVoiceParticipantId = "";
                activeBattleVoiceSpeakerName = "";
                battleCommunicationVoiceRemaining = 0f;
            }
        }
        // Always editable so offline mode players can input locally
        battleCommunicationInput.Editable = true;
        battleCommunicationInput.PlaceholderText = "输入战场消息，回车发送";
        var hasText = !string.IsNullOrWhiteSpace(battleCommunicationInput.Text);
        battleCommunicationSendButton.Disabled = !hasText;
        battleCommunicationVoiceButton.Disabled = !canTransmit;

        if (!canTransmit)
        {
            battleCommunicationVoiceButton.Text = "语音";
            ApplyButtonStyle(battleCommunicationVoiceButton, new Color(0.20f, 0.22f, 0.24f, 0.92f), new Color(0.62f, 0.68f, 0.72f, 0.42f), 10);
            return;
        }

        if (battleCommunicationVoiceHolding)
        {
            battleCommunicationVoiceButton.Text = "发送中";
            ApplyButtonStyle(battleCommunicationVoiceButton, new Color(0.65f, 0.14f, 0.12f, 0.98f), new Color(1f, 0.76f, 0.66f, 0.90f), 10);
        }
        else if (IsRemoteBattleVoiceActive())
        {
            battleCommunicationVoiceButton.Text = "队友通话";
            ApplyButtonStyle(battleCommunicationVoiceButton, new Color(0.24f, 0.46f, 0.72f, 0.98f), new Color(0.74f, 0.92f, 1f, 0.90f), 10);
        }
        else
        {
            battleCommunicationVoiceButton.Text = "按住语音";
            ApplyButtonStyle(battleCommunicationVoiceButton, new Color(0.16f, 0.30f, 0.54f, 0.96f), new Color(0.70f, 0.90f, 1f, 0.86f), 10);
        }
    }

    bool CanTransmitBattleCommunication()
        => GameRelay.Instance?.IsNetworkGame == true && GameRelay.Instance.PeerConnected;

    int ConnectedBattleTeammateCount()
        => settingsParticipants.Count(IsConnectedBattleTeammate);

    static bool IsConnectedBattleTeammate(GameState.BattleParticipantProfile participant)
        => !participant.IsLocalPlayer
            && participant.ParticipantId != "local-ai"
            && !participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal);

    bool HasBattleCommunicationMessages()
        => battleCommunicationLines.Any(line => line is not null && GodotObject.IsInstanceValid(line) && !string.IsNullOrWhiteSpace(line.Text));

    void UpdateBattleCommunicationMessageWidths(float width)
    {
        if (battleCommunicationMessages is null)
            return;

        battleCommunicationMessages.CustomMinimumSize = new Vector2(width, 0f);
        foreach (var line in battleCommunicationLines.Where(GodotObject.IsInstanceValid))
        {
            line.CustomMinimumSize = new Vector2(width, 22f);
            line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
    }

    void ScrollBattleCommunicationToBottom()
    {
        if (battleCommunicationScroll?.GetVScrollBar() is null)
            return;

        battleCommunicationScroll.ScrollVertical = Mathf.RoundToInt(battleCommunicationScroll.GetVScrollBar().MaxValue);
    }

    string BattleCommunicationEmptyStateText(bool canTransmit)
    {
        if (canTransmit)
            return "通信频道已接通。\n可以发送文字，或按住左侧语音键联络队友。";
        if (!string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId))
            return "房间已建立，正在等待队友接入。\n您可以在下方输入消息并回车发送以测试冒泡通知。";
        return "当前为离线演练。\n已接通本地战地通讯，可以直接发送测试消息指令。";
    }

    public void TriggerInvasionAlert(string message, bool isBaseAttack)
    {
        var now = Godot.Time.GetTicksMsec() / 1000.0;
        if (now - lastInvasionAlertTime < 12.0)
            return;
        lastInvasionAlertTime = now;
        string speaker = isBaseAttack ? "基地警报" : "雷达哨所";
        Color color = isBaseAttack ? new Color(1f, 0.32f, 0.28f) : new Color(1f, 0.64f, 0.30f);
        AppendBattleCommunicationMessage(speaker, message, color);
    }

    void OnBattleCommunicationTextSubmitted(string text)
    {
        SendBattleCommunicationText();
    }

    void OnBattleCommunicationInputChanged(string _)
    {
        RefreshBattleCommunicationComposerState();
    }

    void SendBattleCommunicationText()
    {
        if (battleCommunicationInput is null)
            return;

        var message = battleCommunicationInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return;

        var senderId = LocalBattleParticipantId();
        var speaker = LocalBattleSpeakerName();

        if (GameRelay.Instance?.IsNetworkGame == true && GameRelay.Instance.PeerConnected)
        {
            GameRelay.Instance.SendBattleChat(senderId, speaker, message);
        }
        else
        {
            // 离线或等待联机时，支持本地消息输入并触发AI队友模拟回复
            SimulateLocalTeammateReply(message);
        }

        AppendBattleCommunicationMessage(speaker, message, new Color(0.74f, 1f, 0.82f));
        battleCommunicationInput.Text = "";
        RefreshBattleCommunicationPanelLayout();
        RefreshBattleCommunicationComposerState();
        battleCommunicationInput.GrabFocus();
    }

    async void SimulateLocalTeammateReply(string playerMessage)
    {
        await System.Threading.Tasks.Task.Delay(1000);
        if (!GodotObject.IsInstanceValid(this) || !Visible)
            return;

        string reply = "收到，指挥官！无线电通信测试正常。";
        var lower = playerMessage.ToLower();
        if (lower.Contains("攻击") || lower.Contains("打") || lower.Contains("attack"))
            reply = "收到！先锋部队已经锁定目标，正全力开火！";
        else if (lower.Contains("撤") || lower.Contains("退") || lower.Contains("retreat"))
            reply = "收到指令！后卫部队已布设阻滞火力，掩护撤退中。";
        else if (lower.Contains("防守") || lower.Contains("守") || lower.Contains("defend"))
            reply = "阵地防御系统已超载激活，全力顶住攻势！";
        else if (lower.Contains("金币") || lower.Contains("钱") || lower.Contains("gold") || lower.Contains("money"))
            reply = "前线金矿资源采集速度平稳，请指示主力升级方向！";
        else if (lower.Contains("树") || lower.Contains("草") || lower.Contains("tree") || lower.Contains("grass"))
            reply = "河畔树木茂盛，适合隐藏自行火炮打伏击！";

        AppendBattleCommunicationMessage("队友(AI)", reply, new Color(0.86f, 0.96f, 1f));
    }

    void BeginBattleVoiceTransmit()
    {
        if (GameRelay.Instance?.IsNetworkGame != true || !GameRelay.Instance.PeerConnected)
        {
            ShowAlert("当前未连接队友，无法发送语音");
        RefreshBattleCommunicationComposerState();
            return;
        }

        battleCommunicationVoiceHolding = true;
        nextBattleCommunicationVoicePulseAtMs = 0UL;
        SendBattleCommunicationVoiceSignal(true);
        RefreshBattleCommunicationComposerState();
    }

    void EndBattleVoiceTransmit()
    {
        if (!battleCommunicationVoiceHolding)
            return;

        battleCommunicationVoiceHolding = false;
        if (string.Equals(activeBattleVoiceParticipantId, LocalBattleParticipantId(), StringComparison.Ordinal))
        {
            activeBattleVoiceParticipantId = "";
            activeBattleVoiceSpeakerName = "";
            battleCommunicationVoiceRemaining = 0f;
        }
        AppendBattleCommunicationMessage("语音", $"{LocalBattleSpeakerName()} 语音结束", new Color(0.70f, 0.98f, 0.94f));
        RefreshBattleCommunicationHeader();
        RefreshBattleCommunicationComposerState();
    }

    void SendBattleCommunicationVoiceSignal(bool showLocalMessage)
    {
        if (GameRelay.Instance?.IsNetworkGame != true || !GameRelay.Instance.PeerConnected)
            return;

        var senderId = LocalBattleParticipantId();
        var speaker = LocalBattleSpeakerName();
        GameRelay.Instance.SendBattleVoice(senderId, speaker);
        selectedBattleParticipantId = senderId;
        activeBattleVoiceParticipantId = senderId;
        activeBattleVoiceSpeakerName = speaker;
        battleCommunicationVoiceRemaining = 0.9f;
        if (showLocalMessage)
            AppendBattleCommunicationMessage("语音", $"{speaker} 正在语音", new Color(0.70f, 0.98f, 0.94f));
        RefreshBattleCommunicationHeader();
    }

    static string LocalBattleParticipantId()
    {
        var userId = GameState.Instance?.UserId ?? "";
        return string.IsNullOrWhiteSpace(userId) ? "local-player" : userId.Trim();
    }

    static string LocalBattleSpeakerName()
    {
        var username = GameState.Instance?.Username ?? "";
        return string.IsNullOrWhiteSpace(username) ? "我方指挥官" : username.Trim();
    }

    void OnSelectionChanged(Godot.Collections.Array<Node> selection)
    {
        selectedBuilding = selection.OfType<RtsBuilding>().FirstOrDefault(b => b.PlayerOwned || (b.IsMainBase && b.IsRuined));
        selectedRepairUnit = selection.OfType<RtsUnit>()
            .Where(u => u.PlayerOwned && !u.IsDead && u.Health < u.MaxHealth - 1f)
            .OrderBy(u => u.Health / u.MaxHealth)
            .FirstOrDefault();
        if (selectedBuilding is not null)
        {
            buildMenuOpen = false;
            techMenuOpen = false;
        }
        else if (selection.Count > 0)
        {
            buildMenuOpen = false;
            techMenuOpen = false;
        }

        if (selection.Count == 0)
        {
            selectionLabel.Text = "当前选择：未选择单位";
            RefreshCommandPanel();
            RefreshInputHint();
            return;
        }

        RefreshSelectionLabel(selection);
        RefreshCommandPanel();
        RefreshInputHint();
    }

    void RefreshSelectionLabel(Godot.Collections.Array<Node>? selection = null)
    {
        var selectedNodes = selection is not null
            ? selection.Where(GodotObject.IsInstanceValid).ToArray()
            : (GameState.Instance?.Selected ?? Enumerable.Empty<Node>()).Where(GodotObject.IsInstanceValid).ToArray();
        if (selectedNodes.Length == 0)
        {
            selectionLabel.Text = "当前选择：未选择单位";
            return;
        }

        var names = selectedNodes.Select(node => node switch
        {
            RtsUnit unit => FormatSelectedUnitText(unit),
            RtsBuilding building => $"{building.DisplayName} 耐久 {building.Health:0}/{building.MaxHealth:0}",
            _ => node.Name.ToString()
        });
        selectionLabel.Text = "当前选择：" + string.Join("，", names);
    }

    static string FormatSelectedUnitText(RtsUnit unit)
    {
        var text = $"{unit.DisplayName} 耐久 {unit.Health:0}/{unit.MaxHealth:0}";
        if (!BattleUnitCatalog.IsAirUnit(unit.UnitKey))
            return text;

        var fuel = Mathf.RoundToInt(unit.FuelRatio * 100f);
        var state = unit.IsParkedAtAirfield
            ? "停机加油"
            : unit.IsReturningToRefuel
                ? "返场加油"
                : "飞行";
        return $"{text} 燃油 {fuel}% {state}";
    }

    void RefreshEconomy()
    {
        var manager = BattleGameManager.Instance;
        if (manager is null)
        {
            if (goldValueLabel is not null)
                goldValueLabel.Text = "--";
            if (goldIncomeRateLabel is not null)
                goldIncomeRateLabel.Text = "+0/s";
            if (powerValueLabel is not null)
            {
                powerValueLabel.Text = "--/--";
                powerValueLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.96f, 0.82f));
            }
            economyLabel.Text = "经济系统初始化中...";
            return;
        }

        var pending = string.IsNullOrEmpty(manager.PendingBuildKey)
            ? ""
            : $"   建造：{BattleBuildingCatalog.Get(manager.PendingBuildKey).DisplayName}";
        var power = manager.PlayerPowerOnline
            ? $"电力 {manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}"
            : $"电力不足 {manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}";
        if (goldValueLabel is not null)
            goldValueLabel.Text = manager.PlayerGold.ToString();
        if (goldIncomeRateLabel is not null)
            goldIncomeRateLabel.Text = FormatGoldIncomePerSecond(manager);
        if (powerValueLabel is not null)
        {
            powerValueLabel.Text = $"{manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}";
            powerValueLabel.AddThemeColorOverride("font_color", manager.PlayerPowerOnline
                ? new Color(0.74f, 0.96f, 0.82f)
                : new Color(1f, 0.30f, 0.30f));
        }
        economyLabel.Text = $"人口 {manager.PlayerPopUsed}/{manager.PlayerPopCap}   {power}   时间 {FormatTime(manager.GameTime)}{pending}";
    }

    static string FormatGoldIncomePerSecond(BattleGameManager manager)
    {
        var incomePerSecond = manager
            .GetBuildings(true)
            .Where(building =>
                !building.UnderConstruction
                && !building.IsRuined
                && !building.IsRebuilding
                && building.GoldIncomeAmount > 0
                && building.GoldIncomeInterval > 0f
                && (!building.RequiresPower() || building.Powered))
            .Sum(building => building.GoldIncomeAmount / building.GoldIncomeInterval);

        return $"+{incomePerSecond:0.#}/s";
    }

    void RefreshCommandPanel()
    {
        if (commandList is null)
            return;

        FreeChildNodes(commandList);

        var manager = BattleGameManager.Instance;
        if (!string.IsNullOrEmpty(manager?.PendingBuildKey) || !string.IsNullOrEmpty(activeTechTargetKey))
        {
            commandPanel.Visible = false;
            return;
        }

        if (selectedBuilding is not null && GodotObject.IsInstanceValid(selectedBuilding))
        {
            commandPanel.Visible = true;
            if (selectedBuilding.IsMainBase)
            {
                commandPanelTitle.Text = "主基地信息";
                AddMainBaseInfoMenu(selectedBuilding);
            }
            else if (techMenuOpen)
            {
                commandPanelTitle.Text = $"{selectedBuilding.DisplayName} · 科技";
                AddBattleTechMenu(manager, selectedBuilding);
            }
            else
            {
                commandPanelTitle.Text = selectedBuilding.DisplayName;
                AddProductionMenu(selectedBuilding);
            }
        }
        else if (techMenuOpen)
        {
            commandPanel.Visible = true;
            commandPanelTitle.Text = "战场科技";
            AddBattleTechMenu(manager, null);
        }
        else if (buildMenuOpen || !string.IsNullOrEmpty(manager?.PendingBuildKey))
        {
            commandPanel.Visible = true;
            commandPanelTitle.Text = "建造部署";
            AddBuildMenu(manager);
        }
        else
        {
            commandPanel.Visible = false;
            return;
        }

        StyleCommandListControls(commandList);
        nextCommandPanelLiveRefreshMs = Time.GetTicksMsec() + CommandPanelLiveRefreshMs;
    }

    void RefreshCommandPanelLive()
    {
        if (commandPanel is null || !commandPanel.Visible)
            return;

        if (!CommandPanelNeedsLiveRefresh())
            return;

        if (Time.GetTicksMsec() < nextCommandPanelLiveRefreshMs)
            return;

        RefreshCommandPanel();
    }

    void RefreshMapInteractionLockState()
    {
        var locked = TryGetMapInteractionLock(out var token, out var reason);
        var stateChanged = token != lastMapInteractionLockToken;
        if (stateChanged)
        {
            var hadLock = !string.IsNullOrEmpty(lastMapInteractionLockToken);
            lastMapInteractionLockToken = token;
            if (commandList is not null && (locked || hadLock || (commandPanel is not null && commandPanel.Visible)))
                RefreshCommandPanel();
        }

        if (buildActionButton is not null)
            buildActionButton.Disabled = locked;
        if (techActionButton is not null)
            techActionButton.Disabled = locked;

        if (commandPanelLockOverlay is null)
            return;

        commandPanelLockOverlay.Visible = locked && commandPanel is not null && commandPanel.Visible;
        if (commandPanelLockLabel is not null)
            commandPanelLockLabel.Text = reason;
        if (commandPanelLockCancelButton is not null)
            commandPanelLockCancelButton.Disabled = !locked;
    }

    bool TryGetMapInteractionLock(out string token, out string reason)
    {
        if (!string.IsNullOrEmpty(activeTechTargetKey))
        {
            var tech = BattleTechCatalog.Get(activeTechTargetKey);
            token = "tech:" + activeTechTargetKey;
            reason = $"科技释放中：{tech.DisplayName}\n请先在地图上确认释放位置，取消后再操作建造、科技等面板。";
            return true;
        }

        if (BattleGameManager.Instance is { PendingBuildKey.Length: > 0 } manager)
        {
            var building = BattleBuildingCatalog.Get(manager.PendingBuildKey);
            token = "build:" + manager.PendingBuildKey;
            reason = $"建造放置中：{building.DisplayName}\n请先在地图上确认落点，取消后再操作建造、科技等面板。";
            return true;
        }

        var controller = FindPlayerController();
        if (controller?.IsBombingRunAwaitingDirection == true)
        {
            token = "cmd:bombrun_direction";
            reason = "区域轰炸定向中\n请先在地图上确认终点方向，取消后再操作其他面板。";
            return true;
        }

        if (controller?.IsBombingRunModeActive == true)
        {
            token = "cmd:bombrun_start";
            reason = "区域轰炸选点中\n请先在地图上确认起点，取消后再操作其他面板。";
            return true;
        }

        if (controller?.IsAttackGroundModeActive == true)
        {
            token = "cmd:attack_ground";
            reason = "炮击命令选点中\n请先在地图上指定攻击点，取消后再操作其他面板。";
            return true;
        }

        if (controller?.IsPatrolModeActive == true)
        {
            token = "cmd:patrol";
            reason = "巡逻命令选点中\n请先在地图上指定巡逻终点，取消后再操作其他面板。";
            return true;
        }

        token = "";
        reason = "";
        return false;
    }

    void CancelActiveMapInteractionFromPanel()
    {
        if (CancelActiveTechTargeting())
            return;

        if (BattleGameManager.Instance is { PendingBuildKey.Length: > 0 } manager)
        {
            manager.CancelBuildPlacement();
            BattleFeedback.UiCancel(this);
            ShowAlert("已取消建造放置");
            RefreshEconomy();
            RefreshCommandPanel();
            return;
        }

        if (FindPlayerController()?.CancelCommandMode(false) == true)
        {
            ShowAlert("已取消当前命令");
            RefreshCommandPanel();
            RefreshBottomCommandBar();
            RefreshInputHint();
        }
    }

    void CloseCommandPanelView()
    {
        if (selectedBuilding is not null && GodotObject.IsInstanceValid(selectedBuilding))
        {
            GameState.Instance?.SetSelection(System.Array.Empty<Node>());
            return;
        }

        buildMenuOpen = false;
        techMenuOpen = false;
        selectedRepairUnit = null;
        RefreshCommandPanel();
        RefreshInputHint();
    }

    bool CommandPanelNeedsLiveRefresh()
    {
        if (techMenuOpen || buildMenuOpen)
            return false;

        if (selectedBuilding is null || !GodotObject.IsInstanceValid(selectedBuilding))
            return false;

        return selectedBuilding.UnderConstruction
            || selectedBuilding.IsRebuilding
            || !string.IsNullOrEmpty(selectedBuilding.CurrentProduction);
    }

    void AddUnitCommandMenu(RtsUnit unit, BattleGameManager? manager)
    {
        if (unit.Health < unit.MaxHealth - 1f)
            AddUnitRepairButton(unit);
    }

    void AddStopUnitsButton()
    {
        var units = GameState.Instance?.Selected
            .OfType<RtsUnit>()
            .Where(unit => GodotObject.IsInstanceValid(unit) && unit.PlayerOwned && !unit.IsDead)
            .ToList();
        if (units is null || units.Count == 0)
            return;

        var button = new Button
        {
            Text = units.Count == 1 ? "停止单位" : $"停止单位（{units.Count}）",
            CustomMinimumSize = new Vector2(150, 36)
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += () =>
        {
            foreach (var selectedUnit in units.Where(GodotObject.IsInstanceValid))
            {
                selectedUnit.Stop();
                GameRelay.Instance?.SendStop(selectedUnit.NetId);
            }
            ShowAlert("已下达停止命令");
        };
        commandList.AddChild(button);
    }

    void AddTacticalOrderHint()
    {
        commandList.AddChild(HudLabel("单位命令请使用底部按钮操作", 12, new Color(0.70f, 0.84f, 0.86f)));
    }

    void AddProductionMenu(RtsBuilding building)
    {
        var roster = building.GetProductionRoster();
        var powerBlocked = building.RequiresPower() && !building.Powered;
        AddBuildingStatusSummary(building);
        if (building.IsRuined)
        {
            AddMainBaseRebuildMenu(building);
            return;
        }
        if (building.IsRebuilding)
        {
            commandList.AddChild(HudLabel($"{building.DisplayName} 重建中：{building.RebuildProgress:P0}，剩余 {building.RebuildTimeLeft:0.0}s", 14, new Color(1f, 0.84f, 0.48f)));
            return;
        }
        if (building.UnderConstruction)
        {
            commandList.AddChild(HudLabel($"{building.DisplayName} 建造中：{building.ConstructionProgress:P0}，剩余 {building.ConstructionTimeLeft:0.0}s", 14, new Color(1f, 0.84f, 0.48f)));
            var cancel = new Button
            {
                Text = $"取消建造 返还 {RefundAmount(building)}",
                CustomMinimumSize = new Vector2(180, 36)
            };
            cancel.AddThemeFontSizeOverride("font_size", 13);
            cancel.Pressed += () => CancelConstruction(building);
            commandList.AddChild(cancel);
            commandList.AddChild(HudLabel("完成建造后会开放该建筑自身的生产或功能面板。", 12, new Color(0.74f, 0.88f, 0.92f)));
            return;
        }
        if (powerBlocked)
            commandList.AddChild(HudLabel($"{building.DisplayName} 电力不足：当前生产暂停，恢复供电后继续。", 14, new Color(1f, 0.62f, 0.36f)));

        if (building.Health < building.MaxHealth - 1f)
            AddRepairButton(building);

        AddBuildingUpgradeButton(building);
        AddBuildingTechSummary(building);

        if (roster.Length == 0)
        {
            commandList.AddChild(HudLabel($"{building.DisplayName}：该建筑提供战场功能，不直接生产单位。", 14, new Color(0.78f, 0.86f, 0.88f)));
            return;
        }

        commandList.AddChild(HudLabel("生产", 14, new Color(1f, 0.88f, 0.58f)));

        const int productionColumns = 3;
        const float productionCardWidth = 110f;
        const float productionCardHeight = 96f;
        const float productionGap = 6f;
        var productionRows = Mathf.Max(1, Mathf.CeilToInt(roster.Length / (float)productionColumns));
        var row = new GridContainer
        {
            Name = "ProductionRow",
            Columns = productionColumns,
            CustomMinimumSize = new Vector2(
                productionColumns * productionCardWidth + (productionColumns - 1) * productionGap,
                productionRows * productionCardHeight + (productionRows - 1) * productionGap),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        row.AddThemeConstantOverride("h_separation", (int)productionGap);
        row.AddThemeConstantOverride("v_separation", (int)productionGap);
        commandList.AddChild(row);
        foreach (var unitKey in roster)
        {
            var def = BattleUnitCatalog.Get(unitKey);
            row.AddChild(CreateProductionCard(building, def));
        }

        commandList.AddChild(HudLabel("生产队列", 14, new Color(1f, 0.88f, 0.58f)));
        commandList.AddChild(CreateProductionQueueStrip(building));
        commandList.AddChild(HudLabel(
            building.CanSetRallyPoint()
                ? "右键地面可设置集结点，出兵后会朝集结点移动。"
                : "当前建筑暂无可设置的集结点。",
            12,
            new Color(0.74f, 0.88f, 0.92f)));
        commandList.AddChild(HudLabel("科技是范围施放效果：机动只给可移动部队，火力/射程/攻速只给武装单位或建筑，不是全军永久共享。", 12, new Color(0.74f, 0.88f, 0.92f)));
    }

    void AddMainBaseInfoMenu(RtsBuilding building)
    {
        var manager = BattleGameManager.Instance;
        var sections = BuildMainBaseInfoSections(IsGlobalConquestMode());
        var forceOverview = building.IsRuined || building.IsRebuilding || building.UnderConstruction || manager is null;
        if (activeMainBaseInfoSectionIndex < 0 || activeMainBaseInfoSectionIndex >= sections.Length)
            activeMainBaseInfoSectionIndex = 0;
        if (forceOverview)
            activeMainBaseInfoSectionIndex = 0;

        commandList.AddChild(HudLabel("信息分类", 14, new Color(1f, 0.88f, 0.58f)));
        AddMainBaseInfoTabs(sections);

        var activeSection = sections[activeMainBaseInfoSectionIndex];
        commandList.AddChild(HudLabel(activeSection.Title, 13, new Color(0.80f, 0.94f, 1f)));

        if (building.IsRuined)
        {
            AddMainBaseOverviewSummary(building, false, true);
            commandList.AddChild(HudLabel("状态：主基地已被摧毁，当前处于废墟状态。", 13, new Color(1f, 0.62f, 0.42f)));
            AddMainBaseRebuildMenu(building);
            return;
        }

        if (building.IsRebuilding)
        {
            AddMainBaseOverviewSummary(building, false, true);
            commandList.AddChild(HudLabel($"状态：重建中 {building.RebuildProgress:P0}，剩余 {building.RebuildTimeLeft:0.0}s", 13, new Color(1f, 0.84f, 0.48f)));
            commandList.AddChild(HudLabel("重建完成后会恢复主基地功能，并重新允许建造与升级。", 12, new Color(0.78f, 0.88f, 0.92f)));
            return;
        }

        if (building.UnderConstruction)
        {
            AddMainBaseOverviewSummary(building, false, true);
            commandList.AddChild(HudLabel($"状态：建造中 {building.ConstructionProgress:P0}，剩余 {building.ConstructionTimeLeft:0.0}s", 13, new Color(1f, 0.84f, 0.48f)));
            commandList.AddChild(HudLabel("主基地建成后才能继续扩张和解锁后续等级。", 12, new Color(0.78f, 0.88f, 0.92f)));
            return;
        }

        if (manager is null)
        {
            AddMainBaseOverviewSummary(building, false, true);
            commandList.AddChild(HudLabel("战斗管理器尚未就绪，升级与科技信息稍后刷新。", 12, new Color(0.82f, 0.88f, 0.92f)));
            return;
        }

        commandList.AddChild(HudLabel(MainBaseStatusText(building), 12, MainBaseStatusColor(building)));

        switch (activeSection.Key)
        {
            case "overview":
                AddMainBaseOverviewSummary(building, false, true);
                break;
            case "upgrade":
                if (BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel) &&
                    manager.TryGetBuildingUpgradePreview(building, out var nextLevel, out var goldCost, out var requirements, out _))
                {
                    AddMainBaseUpgradeSummary(building, nextLevel, goldCost, requirements, manager.CanUpgradeBuilding(building, out _), false);
                }
                else
                {
                    commandList.AddChild(HudLabel("当前主基地已满级。", 13, new Color(0.74f, 1f, 0.78f)));
                }
                break;
            case "tech":
                AddMainBaseTechSummary(manager, false);
                break;
            case "armament":
                AddGlobalConquestArmamentSummary(manager, false);
                break;
        }

        if (building.Health < building.MaxHealth - 1f)
            AddRepairButton(building);
    }

        void AddMainBaseRebuildMenu(RtsBuilding building)
    {
        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        if (building.PlayerOwned)
        {
            commandList.AddChild(HudLabel(
                IsGlobalConquestMode()
                    ? "全球争霸主基地不能重建，只能在清空周边基础设施后重新占领。"
                    : "当前地图主基地被摧毁后不可重建。",
                13,
                new Color(1f, 0.78f, 0.54f)));
            return;
        }

        if (!IsGlobalConquestMode())
            return;

        var canOccupy = manager.CanOccupyGlobalConquestMainBase(true, out var message);
        commandList.AddChild(HudLabel(
            canOccupy ? "该敌方主基地废墟已满足占领条件。" : message,
            13,
            canOccupy ? new Color(0.74f, 1f, 0.78f) : new Color(1f, 0.78f, 0.54f)));

        var button = new Button
        {
            Text = "占领主基地",
            CustomMinimumSize = new Vector2(200, 40),
            Disabled = !canOccupy
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += () => OccupyGlobalConquestMainBase(building);
        commandList.AddChild(button);
    }

    void AddMainBaseOverviewSummary(RtsBuilding building, bool includeHeading = true, bool compactHorizontal = false)
    {
        if (includeHeading)
            commandList.AddChild(HudLabel("主基地概览", 14, new Color(1f, 0.88f, 0.58f)));

        var row = new GridContainer { Columns = compactHorizontal ? 4 : 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        row.AddChild(CreateSummaryTile(
            "主基地等级",
            $"Lv.{Math.Max(1, building.BuildingLevel)}",
            compactHorizontal ? "解锁内容" : "核心等级决定可解锁内容",
            new Color(1f, 0.90f, 0.62f),
            compactHorizontal ? new Vector2(84f, 78f) : new Vector2(174f, 66f),
            compactHorizontal ? 11 : 12,
            compactHorizontal ? 13 : 16,
            compactHorizontal ? 10 : 11));

        row.AddChild(CreateSummaryTile(
            "当前血量",
            $"{Mathf.RoundToInt(building.Health):0}/{Mathf.RoundToInt(building.MaxHealth):0}",
            compactHorizontal
                ? $"{(building.MaxHealth > 0f ? building.Health / building.MaxHealth : 0f):P0}耐久"
                : $"{(building.MaxHealth > 0f ? building.Health / building.MaxHealth : 0f):P0} 耐久",
            new Color(0.72f, 0.92f, 1f),
            compactHorizontal ? new Vector2(84f, 78f) : new Vector2(174f, 66f),
            compactHorizontal ? 11 : 12,
            compactHorizontal ? 11 : 16,
            compactHorizontal ? 10 : 11));

        row.AddChild(CreateSummaryTile(
            "资源收入",
            compactHorizontal
                ? $"+{building.GoldIncomeAmount}/{building.GoldIncomeInterval:0.#}s"
                : $"+{building.GoldIncomeAmount} / {building.GoldIncomeInterval:0.#}s",
            compactHorizontal ? "固定产金" : "固定周期自动产出金币",
            new Color(0.90f, 0.78f, 0.38f),
            compactHorizontal ? new Vector2(84f, 78f) : new Vector2(174f, 66f),
            compactHorizontal ? 11 : 12,
            compactHorizontal ? 11 : 15,
            compactHorizontal ? 10 : 11));

        row.AddChild(CreateSummaryTile(
            "人口上限",
            $"+{building.PopCapBonus}",
            compactHorizontal ? "可驻更多" : "可容纳更多作战单位",
            new Color(0.74f, 1f, 0.78f),
            compactHorizontal ? new Vector2(84f, 78f) : new Vector2(174f, 66f),
            compactHorizontal ? 11 : 12,
            compactHorizontal ? 13 : 16,
            compactHorizontal ? 10 : 11));
    }

    void AddMainBaseUpgradeSummary(
        RtsBuilding building,
        int nextLevel,
        int goldCost,
        BattleBuildingRequirementStatus[] requirements,
        bool canUpgrade,
        bool includeHeading = true)
    {
        var upgrade = BattleBuildingUpgradeCatalog.Get(building.BuildKey, nextLevel);
        var baseDef = BattleBuildingCatalog.Get(building.BuildKey);
        var nextMaxHealth = Mathf.RoundToInt(upgrade.MaxHealthOverride ?? baseDef.MaxHealth * upgrade.HealthMultiplier);
        var nextPopCap = upgrade.PopCapBonusOverride ?? (baseDef.PopCapBonus + upgrade.PopCapBonusAdd);
        var nextIncome = upgrade.GoldIncomeOverride ?? Mathf.RoundToInt(baseDef.GoldIncomeAmount * upgrade.IncomeMultiplier);
        var currentHealth = Mathf.RoundToInt(building.MaxHealth);
        var currentPopCap = building.PopCapBonus;
        var currentIncome = building.GoldIncomeAmount;

        if (includeHeading)
            commandList.AddChild(HudLabel("升级说明", 14, new Color(1f, 0.88f, 0.58f)));

        commandList.AddChild(CreateSummaryTile(
            "升级收益",
            $"Lv.{nextLevel}",
            $"血量 {currentHealth:0} → {nextMaxHealth:0}\n人口 +{currentPopCap} → +{nextPopCap}\n收入 +{currentIncome} → +{nextIncome} / {building.GoldIncomeInterval:0.#}s",
            canUpgrade ? new Color(0.74f, 1f, 0.78f) : new Color(0.96f, 0.78f, 0.56f),
            new Vector2(356f, 116f),
            12,
            16,
            11));

        commandList.AddChild(HudLabel(
            canUpgrade
                ? "升级条件已满足，点击下方按钮即可升级主基地。"
                : "升级方式：先满足下面的建筑数量/等级条件，再点击“升级主基地”。",
            12,
            canUpgrade ? new Color(0.74f, 1f, 0.78f) : new Color(0.96f, 0.78f, 0.56f)));

        AddRequirementStatusSummary(requirements);

        var upgradeButton = new Button
        {
            Text = $"升级主基地\nLv.{nextLevel}  ${goldCost}",
            CustomMinimumSize = new Vector2(180, 46)
        };
        upgradeButton.AddThemeFontSizeOverride("font_size", 13);
        upgradeButton.Pressed += () => OpenUpgradeDialog(building);
        commandList.AddChild(upgradeButton);
    }

    void AddMainBaseTechSummary(BattleGameManager manager, bool includeHeading = true)
    {
        if (includeHeading)
            commandList.AddChild(HudLabel("挂载科技", 14, new Color(1f, 0.88f, 0.58f)));

        var techs = BattleTechCatalog.GetForBuilding("main_base");
        if (techs.Count == 0)
        {
            commandList.AddChild(HudLabel("当前没有可用的主基地科技。", 12, new Color(0.76f, 0.86f, 0.90f)));
            return;
        }

        var readyCount = techs.Count(tech => manager.GetBattleTechCooldownRemaining(tech.Key) <= 0f);
        commandList.AddChild(HudLabel(
            $"当前主基地挂载 {techs.Count} 项战场科技，{readyCount} 项可立即释放。机动只给部队，火力类只给武装单位或建筑。",
            12,
            new Color(0.80f, 0.92f, 0.96f)));

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        foreach (var tech in techs)
        {
            var cooldown = manager.GetBattleTechCooldownRemaining(tech.Key);
            var cooldownText = cooldown > 0f
                ? $"冷却 {Mathf.CeilToInt(cooldown)}s"
                : "待释放";
            row.AddChild(CreateMainBaseTechSummaryTile(tech, cooldownText));
        }
    }

    void AddGlobalConquestArmamentSummary(BattleGameManager manager, bool includeHeading = true)
    {
        if (includeHeading)
            commandList.AddChild(HudLabel("全球争霸军备", 14, new Color(1f, 0.88f, 0.58f)));

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentFaction = BattleUnitCatalog.GetGlobalConquestFactionByStarter(currentStarterKey);
        var buildings = manager.GetBuildings(true)
            .Where(candidate => GodotObject.IsInstanceValid(candidate) && !candidate.UnderConstruction && !candidate.IsRuined && !candidate.IsRebuilding)
            .ToArray();
        var buildingNames = buildings
            .Where(candidate => !candidate.IsMainBase)
            .Select(candidate => candidate.DisplayName)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        var ownedUnits = manager.GetUnits(true)
            .Where(unit => GodotObject.IsInstanceValid(unit) && !unit.IsDead)
            .GroupBy(unit => unit.UnitKey)
            .Select(group => $"{BattleUnitCatalog.Get(group.Key).DisplayName} x{group.Count()}")
            .OrderBy(text => text)
            .ToArray();

        var producibleUnits = buildings
            .SelectMany(candidate => candidate.GetProductionRoster())
            .Distinct()
            .Select(key => BattleUnitCatalog.Get(key).DisplayName)
            .OrderBy(name => name)
            .ToArray();

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        row.AddChild(CreateSummaryTile(
            "本局阵营",
            currentFaction.DisplayName,
            $"开局主力：{currentStarter.DisplayName}",
            new Color(0.78f, 0.90f, 0.98f),
            new Vector2(174f, 82f),
            12,
            15,
            11));

        row.AddChild(CreateSummaryTile(
            "军备建筑",
            FormatJoinedNames(buildingNames, "仅有主基地"),
            "可生产军备的建筑",
            new Color(0.74f, 0.92f, 0.94f),
            new Vector2(174f, 88f),
            12,
            12,
            11));

        row.AddChild(CreateSummaryTile(
            "已部署军备",
            FormatJoinedNames(ownedUnits, "暂无出战单位"),
            "当前场上单位统计",
            new Color(0.74f, 1f, 0.78f),
            new Vector2(174f, 96f),
            12,
            12,
            11));

        row.AddChild(CreateSummaryTile(
            "可调用军备",
            FormatJoinedNames(producibleUnits, "暂无可调用单位"),
            "由现有军备建筑汇总",
            new Color(1f, 0.86f, 0.42f),
            new Vector2(174f, 96f),
            12,
            12,
            11));

        commandList.AddChild(HudLabel("后续如果接入仓库/蓝图数据，这里还能继续显示你的专属军备配置。", 12, new Color(0.88f, 0.80f, 0.56f)));
    }

    void AddTechMenu(RtsBuilding building)
    {
        AddBattleTechMenu(BattleGameManager.Instance, building);
    }

    void AddStandaloneTechMenu(BattleGameManager? manager, RtsBuilding? building)
    {
        AddBattleTechMenu(manager, building);
    }

    void AddTechButtons(BattleGameManager manager, IEnumerable<BattleTechDefinition> techs)
    {
        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);
        foreach (var tech in techs)
        {
            var buildingName = BattleBuildingCatalog.Get(tech.RequiredBuildingKey).DisplayName;
            var button = new Button
            {
                Text = $"{tech.DisplayName}\n{buildingName}  主动施放",
                CustomMinimumSize = new Vector2(174, 52),
                Disabled = false
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            button.Pressed += () => BeginTechTargeting(tech.Key);
            row.AddChild(button);
        }
    }

    void AddBuildingUpgradeButton(RtsBuilding building)
    {
        if (!building.PlayerOwned || building.UnderConstruction || !BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel))
            return;

        var manager = BattleGameManager.Instance;
        if (manager is null || !manager.TryGetBuildingUpgradePreview(building, out var nextLevel, out var goldCost, out _, out var previewMessage))
            return;

        var canUpgrade = manager.CanUpgradeBuilding(building, out var eligibilityMessage);
        var detailText = canUpgrade ? $"可升级至 Lv.{nextLevel}" : eligibilityMessage;
        const float rowHeight = 58f;
        var panel = Panel(Vector2.Zero, new Vector2(CommandPanelContentWidth, rowHeight), new Color(0.035f, 0.045f, 0.040f, 0.96f));
        panel.Name = "BuildingUpgradeRow";
        panel.CustomMinimumSize = new Vector2(CommandPanelContentWidth, rowHeight);
        panel.ClipContents = true;
        commandList.AddChild(panel);

        var layout = new HBoxContainer
        {
            Position = new Vector2(10f, 6f),
            Size = new Vector2(CommandPanelContentWidth - 20f, rowHeight - 12f),
            CustomMinimumSize = new Vector2(CommandPanelContentWidth - 20f, rowHeight - 12f)
        };
        layout.AddThemeConstantOverride("separation", 10);
        panel.AddChild(layout);

        var label = HudLabel(detailText, 12, canUpgrade ? new Color(0.74f, 1f, 0.78f) : new Color(1f, 0.78f, 0.54f));
        label.CustomMinimumSize = new Vector2(0f, rowHeight - 12f);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        layout.AddChild(label);

        var button = new Button
        {
            Text = $"升级\n${goldCost}",
            CustomMinimumSize = new Vector2(90f, 40f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            Disabled = !canUpgrade,
            TooltipText = canUpgrade ? previewMessage : eligibilityMessage
        };
        ApplyButtonStyle(
            button,
            canUpgrade ? new Color(0.18f, 0.34f, 0.20f, 0.96f) : new Color(0.16f, 0.16f, 0.15f, 0.78f),
            canUpgrade ? new Color(0.72f, 1f, 0.78f, 0.86f) : new Color(0.60f, 0.56f, 0.48f, 0.50f),
            11);
        button.SetMeta("preserve_command_style", true);
        button.Pressed += () => OpenUpgradeDialog(building);
        layout.AddChild(button);
    }
    void AddBuildMenu(BattleGameManager? manager)
    {
        if (manager is not null && !string.IsNullOrEmpty(manager.PendingBuildKey))
        {
            var pending = BattleBuildingCatalog.Get(manager.PendingBuildKey);
            commandList.AddChild(HudLabel($"放置中：{pending.DisplayName}，点地图落点", 14, new Color(1f, 0.84f, 0.48f)));
            return;
        }

        var sections = BuildMenuSections();
        if (activeBuildMenuSectionIndex < 0 || activeBuildMenuSectionIndex >= sections.Length)
            activeBuildMenuSectionIndex = 0;

        commandList.AddChild(HudLabel("建造分类", 14, new Color(1f, 0.88f, 0.58f)));
        AddBuildCategoryTabs(sections);

        var activeSection = sections[activeBuildMenuSectionIndex];
        var sectionTitle = HudLabel(activeSection.Title, 13, new Color(0.80f, 0.94f, 1f));
        commandList.AddChild(sectionTitle);

        var row = new GridContainer { Columns = 3 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);
        foreach (var def in activeSection.Buildings)
            row.AddChild(CreateBuildCard(def));
    }

    void AddBuildCategoryTabs((string Title, BattleBuildingDefinition[] Buildings)[] sections)
    {
        var tabRow = new HBoxContainer
        {
            Name = "BuildCategoryTabs",
            CustomMinimumSize = new Vector2(356f, 32f)
        };
        tabRow.AddThemeConstantOverride("separation", 5);
        commandList.AddChild(tabRow);

        for (var i = 0; i < sections.Length; i++)
        {
            var isActive = i == activeBuildMenuSectionIndex;
            var button = new Button
            {
                Text = sections[i].Title,
                CustomMinimumSize = new Vector2(82f, 30f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            ApplyButtonStyle(
                button,
                isActive ? new Color(0.20f, 0.42f, 0.34f, 0.98f) : new Color(0.08f, 0.12f, 0.12f, 0.96f),
                isActive ? new Color(0.72f, 1f, 0.78f, 0.92f) : new Color(0.95f, 0.72f, 0.28f, 0.72f),
                11);
            button.SetMeta("preserve_command_style", true);

            var sectionIndex = i;
            button.Pressed += () =>
            {
                activeBuildMenuSectionIndex = sectionIndex;
                RefreshCommandPanel();
            };
            tabRow.AddChild(button);
        }
    }

    void AddMainBaseInfoTabs((string Key, string Title)[] sections)
    {
        var tabRow = new HBoxContainer
        {
            Name = "MainBaseInfoTabs",
            CustomMinimumSize = new Vector2(356f, 32f)
        };
        tabRow.AddThemeConstantOverride("separation", 5);
        commandList.AddChild(tabRow);

        for (var i = 0; i < sections.Length; i++)
        {
            var isActive = i == activeMainBaseInfoSectionIndex;
            var button = new Button
            {
                Text = sections[i].Title,
                CustomMinimumSize = new Vector2(82f, 30f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            ApplyButtonStyle(
                button,
                isActive ? new Color(0.20f, 0.42f, 0.34f, 0.98f) : new Color(0.08f, 0.12f, 0.12f, 0.96f),
                isActive ? new Color(0.72f, 1f, 0.78f, 0.92f) : new Color(0.95f, 0.72f, 0.28f, 0.72f),
                11);
            button.SetMeta("preserve_command_style", true);

            var sectionIndex = i;
            button.Pressed += () =>
            {
                activeMainBaseInfoSectionIndex = sectionIndex;
                RefreshCommandPanel();
            };
            tabRow.AddChild(button);
        }
    }

    static string MainBaseStatusText(RtsBuilding building)
    {
        return building.Health < building.MaxHealth - 1f
            ? "状态：可维修，升级后会继续提升血量、人口上限和收入。"
            : "状态：运行中，升级后会继续提升血量、人口上限和收入。";
    }

    static Color MainBaseStatusColor(RtsBuilding building)
    {
        return building.Health < building.MaxHealth - 1f
            ? new Color(1f, 0.84f, 0.48f)
            : new Color(0.78f, 0.92f, 0.94f);
    }

    static (string Key, string Title)[] BuildMainBaseInfoSections(bool includeArmament)
    {
        var sections = new List<(string Key, string Title)>
        {
            ("overview", "概览"),
            ("upgrade", "升级"),
            ("tech", "科技")
        };
        if (includeArmament)
            sections.Add(("armament", "军备"));
        return sections.ToArray();
    }

    static (string Title, BattleBuildingDefinition[] Buildings)[] BuildMenuSections()
    {
        return
        [
            ("陆军军备",
            [
                BattleBuildingCatalog.Barracks,
                BattleBuildingCatalog.TankFactory,
                BattleBuildingCatalog.ArmorFactory
            ]),
            ("空军设施",
            [
                BattleBuildingCatalog.Airfield,
                BattleBuildingCatalog.AirFactory
            ]),
            ("海军设施",
            [
                BattleBuildingCatalog.NavalYard
            ]),
            ("经济与防御",
            [
                BattleBuildingCatalog.GoldMine,
                BattleBuildingCatalog.PowerPlant,
                BattleBuildingCatalog.Turret
            ])
        ];
    }

    Control CreateBuildCard(BattleBuildingDefinition def)
    {
        var manager = BattleGameManager.Instance;
        var mainBaseLevel = manager is not null ? Mathf.Max(1, manager.GetMainBaseLevel(true)) : 1;
        var isLocked = mainBaseLevel == 1 && (def.Key == "tank_factory" || def.Key == "armor_factory" || def.Key == "airfield" || def.Key == "air_factory" || def.Key == "naval_yard");

        var card = new Panel
        {
            CustomMinimumSize = new Vector2(116, 82),
            Size = new Vector2(116, 82),
            MouseFilter = Control.MouseFilterEnum.Stop,
            ClipContents = true
        };
        if (isLocked)
        {
            card.Modulate = new Color(0.65f, 0.65f, 0.65f, 0.85f);
        }
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(0.88f, 0.70f, 0.28f, 0.76f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });

        var background = new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(116f, 82f),
            Texture = LoadHudTexture(BuildCardBackgroundPath(def.Key)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(1f, 1f, 1f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(background);

        var wash = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(116f, 82f),
            Color = new Color(0.06f, 0.10f, 0.14f, 0.28f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(wash);

        var stripe = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(116f, 3f),
            Color = BuildAccentColor(def),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var iconFrame = new Panel
        {
            Position = new Vector2(8f, 8f),
            Size = new Vector2(26f, 26f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.10f, 0.72f),
            BorderColor = new Color(0.92f, 0.80f, 0.46f, 0.72f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
        card.AddChild(iconFrame);

        var iconTexture = new TextureRect
        {
            Position = new Vector2(3f, 3f),
            Size = new Vector2(20f, 20f),
            Texture = LoadHudTexture(BuildCardIconPath(def.Key)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconFrame.AddChild(iconTexture);

        var name = HudLabel(def.DisplayName, 13, new Color(0.95f, 0.97f, 0.98f));
        name.Position = new Vector2(36f, 8f);
        name.Size = new Vector2(72f, 22f);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        name.AddThemeConstantOverride("outline_size", 2);
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(name);

        var cost = HudLabel($"${def.GoldCost}", 12, new Color(1f, 0.86f, 0.42f));
        cost.Position = new Vector2(8f, 36f);
        cost.Size = new Vector2(48f, 18f);
        cost.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        cost.AddThemeConstantOverride("outline_size", 2);
        cost.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(cost);

        var meta = HudLabel(BuildCardMeta(def), 11, new Color(0.76f, 0.88f, 0.90f));
        meta.Position = new Vector2(56f, 36f);
        meta.Size = new Vector2(52f, 18f);
        meta.HorizontalAlignment = HorizontalAlignment.Right;
        meta.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.84f));
        meta.AddThemeConstantOverride("outline_size", 2);
        meta.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(meta);

        var hint = HudLabel(isLocked ? "需要主基地 Lv.2" : "点击建造", 11, isLocked ? new Color(0.96f, 0.40f, 0.40f) : new Color(0.82f, 0.88f, 0.92f));
        hint.Position = new Vector2(8f, 58f);
        hint.Size = new Vector2(100f, 16f);
        hint.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        hint.AddThemeConstantOverride("outline_size", 2);
        hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(hint);

        var button = new Button
        {
            Text = "",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Flat = true,
            Disabled = isLocked
        };
        button.Pressed += () => BeginBuild(def.Key);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.84f, 0.32f, 0.08f),
            BorderColor = new Color(1f, 0.84f, 0.32f, 0.70f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });
        button.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.74f, 0.22f, 0.12f),
            BorderColor = new Color(1f, 0.84f, 0.32f, 0.78f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });
        card.AddChild(button);
        return card;
    }

    static string BuildCardBackgroundPath(string key) => ButtonBackdropRoot + (key switch
    {
        "barracks" => "build_barracks.png",
        "tank_factory" => "build_special_factory.png",
        "armor_factory" => "build_tank_factory.png",
        "airfield" => "build_airfield.png",
        "air_factory" => "build_air_factory.png",
        "naval_yard" => "build_naval_yard.png",
        "turret" => "build_turret.png",
        "gold_mine" => "build_gold_mine.png",
        "power_plant" => "build_power_plant.png",
        _ => "build_barracks.png"
    });

    static string BuildCardIconPath(string key) => ButtonBackdropRoot + (key switch
    {
        "barracks" => "build_barracks_icon.png",
        "tank_factory" => "build_special_factory_icon.png",
        "armor_factory" => "build_tank_factory_icon.png",
        "airfield" => "build_airfield_icon.png",
        "air_factory" => "build_air_factory_icon.png",
        "naval_yard" => "build_naval_yard_icon.png",
        "turret" => "build_turret_icon.png",
        "gold_mine" => "build_gold_mine_icon.png",
        "power_plant" => "build_power_plant_icon.png",
        _ => "build_barracks_icon.png"
    });

    static string BuildGlyph(string key) => key switch
    {
        "barracks" => "兵",
        "tank_factory" => "炮",
        "armor_factory" => "坦",
        "airfield" => "停",
        "air_factory" => "机",
        "naval_yard" => "港",
        "turret" => "塔",
        "gold_mine" => "矿",
        "power_plant" => "电",
        _ => "建"
    };

    static string BuildCardMeta(BattleBuildingDefinition def)
    {
        if (def.PowerProvided > 0)
            return $"+{def.PowerProvided}电";
        if (def.PowerUsed > 0)
            return $"-{def.PowerUsed}电";
        if (def.PopCapBonus > 0)
            return $"+{def.PopCapBonus}人";
        return "部署";
    }

    static Color BuildAccentColor(BattleBuildingDefinition def)
    {
        if (def.PowerProvided > 0)
            return new Color(0.28f, 0.86f, 0.94f, 0.92f);
        if (def.GoldIncomeAmount > 0)
            return new Color(1f, 0.82f, 0.24f, 0.92f);
        if (def.IsDefenseTurret)
            return new Color(1f, 0.56f, 0.34f, 0.92f);
        return new Color(0.74f, 0.88f, 0.42f, 0.92f);
    }

    static string BuildButtonText(BattleBuildingDefinition def)
    {
        var power = def.PowerProvided > 0
            ? $" +{def.PowerProvided}电"
            : def.PowerUsed > 0
                ? $" -{def.PowerUsed}电"
                : "";
        return $"{def.DisplayName}\n${def.GoldCost}{power}";
    }

    void QueueUnit(string unitKey)
    {
        if (selectedBuilding is null || BattleGameManager.Instance is not { } manager)
            return;

        var queued = manager.TryQueueProduction(selectedBuilding, unitKey, out var message);
        if (queued)
            GameRelay.Instance?.SendProduce(selectedBuilding.NetId, unitKey);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

    void ResearchTech(string techKey)
    {
        BeginTechTargeting(techKey);
    }

    void OpenUpgradeDialog(RtsBuilding building)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        if (!manager.TryGetBuildingUpgradePreview(building, out var nextLevel, out var goldCost, out var requirements, out var message))
        {
            ShowAlert(message);
            return;
        }

        pendingUpgradeBuilding = building;
        upgradeDialogRoot.Visible = true;
        upgradeDialogRoot.MoveToFront();
        upgradeDialogTitle.Text = $"{building.DisplayName} 升级预览";
        upgradeDialogSummary.Text = $"升级到 Lv.{nextLevel}，需要金币 {goldCost}";
        var canUpgrade = manager.CanUpgradeBuilding(building, out var eligibilityMessage);
        upgradeDialogHint.Text = canUpgrade ? "条件已满足，点击确认升级" : eligibilityMessage;
        upgradeDialogHint.Modulate = canUpgrade
            ? new Color(0.78f, 0.98f, 0.82f)
            : new Color(1f, 0.56f, 0.48f);
        upgradeConfirmButton.Disabled = !canUpgrade;
        upgradeConfirmButton.Text = canUpgrade ? "确认升级" : "不符合条件";
        RefreshUpgradeRequirementList(requirements);
    }

    void ConfirmUpgradeBuilding()
    {
        if (pendingUpgradeBuilding is null || BattleGameManager.Instance is not { } manager)
        {
            CloseUpgradeDialog();
            return;
        }

        if (!GodotObject.IsInstanceValid(pendingUpgradeBuilding))
        {
            CloseUpgradeDialog();
            return;
        }

        var upgraded = manager.TryUpgradeBuilding(pendingUpgradeBuilding, out var message);
        if (upgraded)
            GameRelay.Instance?.SendUpgradeBuilding(pendingUpgradeBuilding.NetId);
        ShowAlert(message);
        CloseUpgradeDialog();
        RefreshEconomy();
        RefreshCommandPanel();
    }

    void CloseUpgradeDialog()
    {
        pendingUpgradeBuilding = null;
        upgradeDialogRoot.Visible = false;
    }

    void CancelConstruction(RtsBuilding building)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        var cancelled = manager.TryCancelConstruction(building, out var message);
        if (cancelled)
            GameRelay.Instance?.SendCancelConstruction(building.NetId);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

    void AddRepairButton(RtsBuilding building)
    {
        var button = new Button
        {
            Text = $"维修 +{RepairAmount(building):0}耐久  ${RepairCost(building)}",
            CustomMinimumSize = new Vector2(200, 36)
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += () => RepairBuilding(building);
        commandList.AddChild(button);
    }

    void RepairBuilding(RtsBuilding building)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        var repaired = manager.TryRepairBuilding(building, out var message);
        if (repaired)
            GameRelay.Instance?.SendRepairBuilding(building.NetId);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

        void StartMainBaseRebuild(RtsBuilding building)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        var started = manager.TryStartMainBaseRebuild(building, out var message);
        if (started)
            GameRelay.Instance?.SendRebuildMainBase(building.NetId);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

    void OccupyGlobalConquestMainBase(RtsBuilding building)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        var occupied = manager.TryOccupyGlobalConquestMainBase(out var message);
        if (occupied)
            GameRelay.Instance?.SendOccupyMainBase(building.NetId);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

    void AddUnitRepairButton(RtsUnit unit)
    {
        var button = new Button
        {
            Text = $"维修 {unit.DisplayName} +{RepairAmount(unit):0}耐久  ${RepairCost(unit)}",
            CustomMinimumSize = new Vector2(240, 36)
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += () => RepairUnit(unit);
        commandList.AddChild(button);
    }

    void RepairUnit(RtsUnit unit)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        var repaired = manager.TryRepairUnit(unit, out var message);
        if (repaired)
            GameRelay.Instance?.SendRepairUnit(unit.NetId);
        ShowAlert(message);
        RefreshEconomy();
        RefreshCommandPanel();
    }

    static int RefundAmount(RtsBuilding building)
        => Mathf.RoundToInt(BattleBuildingCatalog.Get(building.BuildKey).GoldCost * 0.75f);

    static float RepairAmount(RtsBuilding building)
        => Mathf.Min(building.MaxHealth - building.Health, building.MaxHealth * 0.30f);

    static int RepairCost(RtsBuilding building)
        => Mathf.Max(1, Mathf.CeilToInt(RepairAmount(building) * 0.18f));

    static float RepairAmount(RtsUnit unit)
        => Mathf.Min(unit.MaxHealth - unit.Health, unit.MaxHealth * 0.30f);

    static int RepairCost(RtsUnit unit)
        => Mathf.Max(1, Mathf.CeilToInt(RepairAmount(unit) * 0.20f));

    void BeginBuild(string buildKey)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;
        techMenuOpen = false;
        if (!manager.BeginBuildPlacement(buildKey, out var message))
        {
            ShowAlert(message);
            return;
        }
        var def = BattleBuildingCatalog.Get(buildKey);
        ShowAlert($"选择位置建造：{def.DisplayName}");
        RefreshEconomy();
        RefreshCommandPanel();
        RefreshInputHint();
    }

    void AddBattleTechMenu(BattleGameManager? manager, RtsBuilding? building)
    {
        techButtons.Clear();
        commandList.AddChild(HudLabel("战场科技", 14, new Color(1f, 0.88f, 0.58f)));
        if (manager is null)
        {
            commandList.AddChild(HudLabel("科技系统初始化中...", 13, new Color(0.82f, 0.90f, 0.94f)));
            return;
        }

        if (building is not null && GodotObject.IsInstanceValid(building))
            commandList.AddChild(HudLabel($"当前建筑：{building.DisplayName}", 13, new Color(0.82f, 0.90f, 0.94f)));
        else
            commandList.AddChild(HudLabel("选择科技后，点击地面释放范围加成。", 13, new Color(0.82f, 0.90f, 0.94f)));

        commandList.AddChild(HudLabel("科技为范围施放：点击地图后，仅当时处在框内的我方目标会获得临时状态；机动只给部队，火力类只给武装单位或建筑。", 12, new Color(0.74f, 0.88f, 0.92f)));

        var allTechs = manager.GetBuildings(true)
            .Where(candidate => GodotObject.IsInstanceValid(candidate) && !candidate.UnderConstruction)
            .SelectMany(candidate => BattleTechCatalog.GetForBuilding(candidate.BuildKey))
            .GroupBy(tech => tech.Key)
            .Select(group => group.First())
            .OrderBy(tech => tech.DisplayName)
            .ToArray();

        if (allTechs.Length == 0)
        {
            commandList.AddChild(HudLabel("当前没有可用科技。", 13, new Color(0.82f, 0.90f, 0.94f)));
            return;
        }

        var sections = BuildBattleTechSections(allTechs);
        if (activeTechCategoryIndex < 0 || activeTechCategoryIndex >= sections.Length)
            activeTechCategoryIndex = 0;

        commandList.AddChild(HudLabel("科技分类", 14, new Color(1f, 0.88f, 0.58f)));
        AddBattleTechCategoryTabs(sections);

        var activeSection = sections[activeTechCategoryIndex];
        commandList.AddChild(HudLabel(activeSection.Title, 13, new Color(0.80f, 0.94f, 1f)));

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);
        foreach (var tech in activeSection.Techs)
            row.AddChild(CreateTechCard(manager, tech));

        if (!string.IsNullOrEmpty(activeTechTargetKey))
            commandList.AddChild(HudLabel("左键地面释放，右键取消。", 12, new Color(1f, 0.84f, 0.48f)));
    }

    void AddBattleTechCategoryTabs((string Key, string Title, BattleTechDefinition[] Techs)[] sections)
    {
        var tabRow = new HBoxContainer
        {
            Name = "BattleTechTabs",
            CustomMinimumSize = new Vector2(356f, 32f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        tabRow.AddThemeConstantOverride("separation", 5);
        commandList.AddChild(tabRow);

        for (var i = 0; i < sections.Length; i++)
        {
            var isActive = i == activeTechCategoryIndex;
            var button = new Button
            {
                Text = sections[i].Title,
                CustomMinimumSize = new Vector2(82f, 30f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            ApplyButtonStyle(
                button,
                isActive ? new Color(0.20f, 0.42f, 0.34f, 0.98f) : new Color(0.08f, 0.12f, 0.12f, 0.96f),
                isActive ? new Color(0.72f, 1f, 0.78f, 0.92f) : new Color(0.95f, 0.72f, 0.28f, 0.72f),
                11);
            button.AddThemeColorOverride("font_color", isActive ? new Color(0.92f, 1f, 0.94f) : new Color(0.96f, 0.88f, 0.72f));
            button.AddThemeColorOverride("font_hover_color", Colors.White);
            button.AddThemeColorOverride("font_pressed_color", isActive ? new Color(0.92f, 1f, 0.94f) : new Color(1f, 0.94f, 0.82f));
            button.SetMeta("preserve_command_style", true);

            var sectionIndex = i;
            button.Pressed += () =>
            {
                activeTechCategoryIndex = sectionIndex;
                RefreshCommandPanel();
            };
            tabRow.AddChild(button);
        }
    }
    static (string Key, string Title, BattleTechDefinition[] Techs)[] BuildBattleTechSections(IEnumerable<BattleTechDefinition> techs)
    {
        var available = techs
            .GroupBy(tech => tech.Key)
            .ToDictionary(group => group.Key, group => group.First());
        var assigned = new HashSet<string>();
        var sections = new List<(string Key, string Title, BattleTechDefinition[] Techs)>();

        AddBattleTechSection(sections, assigned, available, "mobility", "机动", "speed", "radar");
        AddBattleTechSection(sections, assigned, available, "offense", "进攻", "firepower", "rapid", "assault");
        AddBattleTechSection(sections, assigned, available, "defense", "防御", "armor", "repair", "hold");

        var uncategorized = available.Values
            .Where(tech => !assigned.Contains(tech.Key))
            .OrderBy(tech => tech.DisplayName)
            .ToArray();
        if (uncategorized.Length > 0)
            sections.Add(("other", "其他", uncategorized));

        return sections.ToArray();
    }

    static void AddBattleTechSection(
        ICollection<(string Key, string Title, BattleTechDefinition[] Techs)> sections,
        ISet<string> assigned,
        IReadOnlyDictionary<string, BattleTechDefinition> available,
        string key,
        string title,
        params string[] techKeys)
    {
        var matched = techKeys
            .Where(available.ContainsKey)
            .Select(techKey =>
            {
                assigned.Add(techKey);
                return available[techKey];
            })
            .ToArray();
        if (matched.Length > 0)
            sections.Add((key, title, matched));
    }

    Control CreateTechCard(BattleGameManager manager, BattleTechDefinition tech)
    {
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(172, 84),
            Size = new Vector2(172, 84),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        card.ClipContents = true;
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(tech.Tint.R, tech.Tint.G, tech.Tint.B, 0.86f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });

        var stripe = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(172f, 3f),
            Color = tech.Tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var iconRect = new TextureRect
        {
            Name = "TechIcon",
            Position = new Vector2(8f, 6f),
            Size = new Vector2(28f, 28f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            SelfModulate = new Color(tech.Tint.R, tech.Tint.G, tech.Tint.B, 1.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconRect.Texture = LoadHudTexture(tech.BackgroundTexturePath);
        card.AddChild(iconRect);

        var title = HudLabel(tech.DisplayName, 15, new Color(1f, 0.90f, 0.62f));
        title.Position = new Vector2(40f, 8f);
        title.Size = new Vector2(122f, 20f);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(title);

        var meta = HudLabel($"半径 {tech.Radius:0}  持续 {tech.Duration:0}s", 11, new Color(0.84f, 0.92f, 0.98f));
        meta.Position = new Vector2(8f, 34f);
        meta.Size = new Vector2(152f, 18f);
        meta.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(meta);

        var desc = HudLabel(tech.Description, 11, new Color(0.80f, 0.90f, 0.94f));
        desc.Position = new Vector2(8f, 52f);
        desc.Size = new Vector2(152f, 24f);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(desc);

        var cooldownFill = new TextureProgressBar
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(172f, 84f),
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            TintProgress = new Color(0f, 0f, 0f, 0.45f),
            TintUnder = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        cooldownFill.FillMode = (int)TextureProgressBar.FillModeEnum.BottomToTop;
        card.AddChild(cooldownFill);

        var cooldownLabel = HudLabel("", 22, Colors.White);
        cooldownLabel.Position = new Vector2(0f, 0f);
        cooldownLabel.Size = new Vector2(172f, 84f);
        cooldownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        cooldownLabel.VerticalAlignment = VerticalAlignment.Center;
        cooldownLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        cooldownLabel.AddThemeConstantOverride("outline_size", 2);
        cooldownLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(cooldownLabel);

        var button = new Button
        {
            Text = "",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Flat = true
        };
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.84f, 0.30f, 0.08f),
            BorderColor = new Color(1f, 0.84f, 0.30f, 0.72f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });
        button.Pressed += () => BeginTechTargeting(tech.Key);
        card.AddChild(button);

        techButtons.Add(new BattleTechUiState
        {
            Key = tech.Key,
            Button = button,
            CooldownFill = cooldownFill,
            CooldownLabel = cooldownLabel,
            Card = card
        });
        return card;
    }

    void BeginTechTargeting(string techKey)
    {
        if (BattleGameManager.Instance is not { } manager)
            return;

        if (!manager.CanCastBattleTech(techKey, out var message))
        {
            ShowAlert(message);
            return;
        }

        buildMenuOpen = false;
        techMenuOpen = true;
        activeTechTargetKey = techKey;
        EnsureTechTargetRing(techKey);
        UpdateTechCooldownVisuals();
        ShowAlert($"选择 {BattleTechCatalog.Get(techKey).DisplayName} 释放位置");
    }

    public bool IsTechTargetingActive => !string.IsNullOrEmpty(activeTechTargetKey);

    public bool TryCastActiveTech(Vector2 screenPos)
    {
        if (string.IsNullOrEmpty(activeTechTargetKey) || BattleGameManager.Instance is not { } manager)
            return false;

        if (!TryProjectGroundPoint(screenPos, out var point))
        {
            ShowAlert("请选择地面位置释放科技");
            return true;
        }

        var techKey = activeTechTargetKey;
        var cast = manager.TryCastBattleTech(techKey, point, out var message, out _);
        if (cast)
            GameRelay.Instance?.SendBattleTech(techKey, point);
        ShowAlert(message);
        activeTechTargetKey = "";
        RemoveTechTargetRing();
        RefreshCommandPanel();
        return true;
    }

    public bool CancelActiveTechTargeting(bool notify = true)
    {
        if (string.IsNullOrEmpty(activeTechTargetKey))
            return false;

        activeTechTargetKey = "";
        RemoveTechTargetRing();
        BattleFeedback.UiCancel(this);
        if (notify)
            ShowAlert("已取消科技释放");
        RefreshCommandPanel();
        return true;
    }

    void UpdateTechCooldownVisuals()
    {
        if (BattleGameManager.Instance is not { } manager || techButtons.Count == 0)
            return;

        foreach (var state in techButtons)
        {
            var tech = BattleTechCatalog.Get(state.Key);
            var remain = manager.GetBattleTechCooldownRemaining(state.Key);
            var isTargeting = activeTechTargetKey == state.Key;
            state.Button.Disabled = remain > 0f || (IsTechTargetingActive && !isTargeting);
            if (state.CooldownFill is not null)
                state.CooldownFill.Value = tech.Cooldown > 0.01f ? remain / tech.Cooldown * 100f : 0f;
            if (state.CooldownLabel is not null)
                state.CooldownLabel.Text = remain > 0f ? Mathf.CeilToInt(remain).ToString() : "";

            if (state.Card.GetThemeStylebox("panel") is StyleBoxFlat box)
                box.BorderColor = isTargeting ? Colors.White : tech.Tint;
        }
    }

    void UpdateTechTargetingVisual()
    {
        if (string.IsNullOrEmpty(activeTechTargetKey) || techTargetRing is null)
            return;

        var viewport = GetViewport();
        if (viewport?.GetCamera3D() is not Camera3D camera)
            return;

        var mousePos = viewport.GetMousePosition();
        if (!TryProjectGroundPoint(camera, mousePos, out var point))
            return;

        techTargetRing.GlobalPosition = point + new Vector3(0f, 0.06f, 0f);
        var radius = BattleTechCatalog.Get(activeTechTargetKey).Radius;
        var pulse = 1f + Mathf.Sin(Time.GetTicksMsec() / 1000.0f * 7f) * 0.035f;
        techTargetRing.Scale = new Vector3(radius * 2f * pulse, 1f, radius * 2f * pulse);
    }

    void EnsureTechTargetRing(string techKey)
    {
        RemoveTechTargetRing();
        var tech = BattleTechCatalog.Get(techKey);
        var mesh = new CylinderMesh
        {
            TopRadius = 1f,
            BottomRadius = 1f,
            Height = 0.03f,
            RadialSegments = 48
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(tech.Tint.R, tech.Tint.G, tech.Tint.B, 0.28f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        techTargetRing = new MeshInstance3D
        {
            Name = "TechTargetRing",
            Mesh = mesh,
            MaterialOverride = material,
            Scale = new Vector3(tech.Radius * 2f, 1f, tech.Radius * 2f)
        };
        GetParent()?.AddChild(techTargetRing);
    }

    void RemoveTechTargetRing()
    {
        if (techTargetRing is null)
            return;
        techTargetRing.QueueFree();
        techTargetRing = null;
    }

    bool TryProjectGroundPoint(Vector2 screenPos, out Vector3 point)
    {
        if (GetViewport()?.GetCamera3D() is not Camera3D camera)
        {
            point = Vector3.Zero;
            return false;
        }

        // 优先使用物理投射获取地面实际高度的水平交点，消除地形起伏导致的投影偏差
        var origin = camera.ProjectRayOrigin(screenPos);
        var end = origin + camera.ProjectRayNormal(screenPos) * 2000f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        var hit = GetViewport().World3D.DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
        {
            var hitPos = hit["position"].AsVector3();
            point = hitPos;
            return true;
        }

        return TryProjectGroundPoint(camera, screenPos, out point);
    }

    static bool TryProjectGroundPoint(Camera3D camera, Vector2 screenPos, out Vector3 point)
    {
        var origin = camera.ProjectRayOrigin(screenPos);
        var direction = camera.ProjectRayNormal(screenPos);
        if (Mathf.Abs(direction.Y) <= 0.0001f)
        {
            point = Vector3.Zero;
            return false;
        }

        var distance = -origin.Y / direction.Y;
        if (distance <= 0f)
        {
            point = Vector3.Zero;
            return false;
        }

        point = origin + direction * distance;
        point.Y = 0f;
        return true;
    }

    public void ShowAlert(string message)
    {
        alertLabel.Text = message;
        alertLabel.Modulate = Colors.White;
        var tween = CreateTween();
        tween.TweenInterval(2.0);
        tween.TweenProperty(alertLabel, "modulate:a", 0.0, 0.6);
    }

    void ShowGameOver(bool playerWon, string reason)
    {
        var manager = BattleGameManager.Instance;

        var dimRect = gameOverOverlay.FindChild("DimRect", true, false) as ColorRect;
        if (dimRect is not null)
        {
            dimRect.Color = playerWon
                ? new Color(0.04f, 0.05f, 0.06f, 0.76f)
                : new Color(0.18f, 0.04f, 0.04f, 0.82f);
        }

        gameOverTitle.Text = playerWon ? "★ 凯 旋 归 来 ★" : "☠ 战 局 失 利 ☠";
        gameOverTitle.Modulate = playerWon
            ? new Color(1f, 0.84f, 0.24f)
            : new Color(0.96f, 0.26f, 0.26f);

        string encouragement = playerWon
            ? "指挥官，您精湛的即时战略指挥艺术让敌军闻风丧胆！"
            : "胜败乃兵家常事。建议多建造防空履带车以御敌，或优先升级科技中心。";

        gameOverBody.Text = $"{reason}\n{encouragement}";

        RefreshGameOverSummary(playerWon, manager);
        SetGameplayHudVisible(false);
        StartGameOverCountdown(30f);
        gameOverOverlay.Visible = true;
        gameOverOverlay.MoveToFront();

        gameOverPanel.PivotOffset = new Vector2(360, 296);
        gameOverPanel.Scale = new Vector2(0.85f, 0.85f);
        gameOverPanel.Modulate = new Color(1f, 1f, 1f, 0f);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(gameOverPanel, "scale", new Vector2(1f, 1f), 0.38)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(gameOverPanel, "modulate:a", 1.0, 0.26);
    }

    public void DebugShowGameOver(bool playerWon, string reason)
        => ShowGameOver(playerWon, reason);

    public void DebugOpenBuildMenu()
    {
        buildMenuOpen = true;
        techMenuOpen = false;
        selectedBuilding = null;
        RefreshCommandPanel();
    }

    void BuildGameOverPanel(Control root)
    {
        gameOverOverlay = new Control
        {
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.AddChild(gameOverOverlay);

        var dim = new ColorRect
        {
            Name = "DimRect",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.74f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        gameOverOverlay.AddChild(dim);

        gameOverPanel = Panel(new Vector2(280, 60), new Vector2(720, 592), new Color(0.015f, 0.025f, 0.03f, 0.97f));
        gameOverOverlay.AddChild(gameOverPanel);
        AddTextureFrame(gameOverPanel, UnityFrameRoot + "panel_task_frame.png", new Color(1f, 1f, 1f, 0.18f));
        AddGameOverChrome(gameOverPanel);

        gameOverResultBadge = Panel(new Vector2(62, 38), new Vector2(58, 58), new Color(0.16f, 0.12f, 0.05f, 0.94f));
        gameOverResultBadge.MouseFilter = Control.MouseFilterEnum.Stop;
        gameOverResultBadge.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        gameOverResultBadge.GuiInput += (InputEvent @event) =>
        {
            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
            {
                ReturnToLobbyFromGameOver();
            }
        };
        gameOverPanel.AddChild(gameOverResultBadge);

        gameOverResultMark = HudLabel("", 28, new Color(1f, 0.86f, 0.42f));
        gameOverResultMark.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverResultMark.VerticalAlignment = VerticalAlignment.Center;
        gameOverResultMark.Position = new Vector2(0, 5);
        gameOverResultMark.Size = new Vector2(58, 46);
        gameOverResultMark.MouseFilter = Control.MouseFilterEnum.Ignore;
        gameOverResultBadge.AddChild(gameOverResultMark);

        gameOverTitle = HudLabel("", 42, new Color(1f, 0.86f, 0.42f));
        gameOverTitle.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverTitle.Position = new Vector2(140, 36);
        gameOverTitle.Size = new Vector2(440, 58);
        gameOverPanel.AddChild(gameOverTitle);

        gameOverBody = HudLabel("", 14, new Color(0.88f, 0.92f, 0.95f));
        gameOverBody.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverBody.VerticalAlignment = VerticalAlignment.Center;
        gameOverBody.Position = new Vector2(60, 96);
        gameOverBody.Size = new Vector2(600, 72);
        gameOverBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        gameOverPanel.AddChild(gameOverBody);

        BuildGameOverSummaryCards();
        BuildGameOverReportSection();

        var buttonDeck = Panel(new Vector2(146, 486), new Vector2(428, 82), new Color(0.012f, 0.018f, 0.020f, 0.48f));
        gameOverPanel.AddChild(buttonDeck);

        gameOverCountdownLabel = HudLabel("", 13, new Color(0.76f, 0.88f, 0.92f));
        gameOverCountdownLabel.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverCountdownLabel.Position = new Vector2(168, 496);
        gameOverCountdownLabel.Size = new Vector2(384, 18);
        gameOverPanel.AddChild(gameOverCountdownLabel);

        var lobbyButton = new Button
        {
            Text = "返回大厅",
            Position = new Vector2(190, 520),
            Size = new Vector2(144, 46)
        };
        lobbyButton.Pressed += ReturnToLobbyFromGameOver;
        gameOverPanel.AddChild(lobbyButton);

        var retryButton = new Button
        {
            Text = "再来一局",
            Position = new Vector2(386, 520),
            Size = new Vector2(144, 46)
        };
        retryButton.Pressed += ReloadBattleFromGameOver;
        gameOverPanel.AddChild(retryButton);

        ApplyButtonStyle(lobbyButton, new Color(0.16f, 0.33f, 0.66f, 0.98f), new Color(0.64f, 0.82f, 1f, 0.82f), 17);
        ApplyButtonStyle(retryButton, new Color(0.12f, 0.48f, 0.22f, 0.98f), new Color(0.62f, 1f, 0.72f, 0.78f), 17);
    }

    void BuildGameOverSummaryCards()
    {
        gameOverSummaryRow = new Control
        {
            Position = new Vector2(58, 176),
            Size = new Vector2(604, 66)
        };
        gameOverPanel.AddChild(gameOverSummaryRow);

        gameOverSummaryCards = new Panel[4];
        gameOverSummaryValueLabels = new Label[4];
        var labels = new[] { "金币", "电力", "主基地", "战果" };
        var accents = new[]
        {
            new Color(0.98f, 0.78f, 0.22f, 1f),
            new Color(0.42f, 0.94f, 0.76f, 1f),
            new Color(0.54f, 0.76f, 1f, 1f),
            new Color(1f, 0.50f, 0.42f, 1f)
        };

        for (var i = 0; i < labels.Length; i++)
        {
            var card = Panel(new Vector2(i * 152, 0), new Vector2(142, 62), new Color(0.030f, 0.048f, 0.050f, 0.98f));
            gameOverSummaryRow.AddChild(card);
            gameOverSummaryCards[i] = card;

            var accent = new ColorRect
            {
                Position = new Vector2(0, 0),
                Size = new Vector2(142, 3),
                Color = accents[i],
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            card.AddChild(accent);

            var label = HudLabel(labels[i], 13, new Color(0.64f, 0.72f, 0.76f));
            label.Position = new Vector2(12, 10);
            label.Size = new Vector2(116, 18);
            card.AddChild(label);

            var value = HudLabel("--", 18, Colors.White);
            value.Position = new Vector2(12, 30);
            value.Size = new Vector2(118, 22);
            card.AddChild(value);
            gameOverSummaryValueLabels[i] = value;
        }
    }

    void RefreshGameOverSummary(bool playerWon, BattleGameManager? manager)
    {
        if (gameOverResultBadge is not null)
        {
            gameOverResultBadge.Modulate = playerWon
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 0.86f, 0.86f, 1f);
        }

        if (gameOverResultMark is not null)
        {
            gameOverResultMark.Text = playerWon ? "★" : "✖";
            gameOverResultMark.Modulate = playerWon
                ? new Color(1f, 0.84f, 0.24f)
                : new Color(0.96f, 0.26f, 0.26f);
        }

        if (manager is null || gameOverSummaryValueLabels.Length < 4)
            return;

        gameOverSummaryValueLabels[0].Text = manager.PlayerGold.ToString("N0");
        gameOverSummaryValueLabels[1].Text = $"{manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}";
        gameOverSummaryValueLabels[2].Text = manager.GetPlayerBaseSummaryText();
        gameOverSummaryValueLabels[3].Text = $"{manager.PlayerUnitKills + manager.PlayerBuildingKills} 击杀";
        RefreshGameOverReport(manager);
    }

    void BuildGameOverReportSection()
    {
        var reportPanel = Panel(new Vector2(58, 254), new Vector2(604, 220), new Color(0.018f, 0.027f, 0.030f, 0.90f));
        gameOverPanel.AddChild(reportPanel);

        var title = HudLabel("战斗报告", 15, new Color(1f, 0.86f, 0.42f));
        title.Position = new Vector2(18, 10);
        title.Size = new Vector2(150, 20);
        reportPanel.AddChild(title);

        gameOverReportMeta = HudLabel("", 13, new Color(0.72f, 0.86f, 0.92f, 0.95f));
        gameOverReportMeta.HorizontalAlignment = HorizontalAlignment.Right;
        gameOverReportMeta.Position = new Vector2(210, 10);
        gameOverReportMeta.Size = new Vector2(374, 20);
        reportPanel.AddChild(gameOverReportMeta);

        var rule = new ColorRect
        {
            Position = new Vector2(0, 38),
            Size = new Vector2(604, 2),
            Color = new Color(0.94f, 0.72f, 0.22f, 0.32f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        reportPanel.AddChild(rule);

        var accent = new ColorRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(4, 220),
            Color = new Color(0.42f, 0.94f, 0.76f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        reportPanel.AddChild(accent);

        gameOverReportGrid = new GridContainer
        {
            Columns = 6,
            Position = new Vector2(16, 44),
            Size = new Vector2(572, 166),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        gameOverReportGrid.AddThemeConstantOverride("h_separation", 6);
        gameOverReportGrid.AddThemeConstantOverride("v_separation", 4);
        reportPanel.AddChild(gameOverReportGrid);
    }

    void RefreshGameOverReport(BattleGameManager? manager)
    {
        if (gameOverReportGrid is null || manager is null)
            return;

        // Clear all previous cells (if any)
        foreach (var child in gameOverReportGrid.GetChildren())
        {
            gameOverReportGrid.RemoveChild(child);
            child.QueueFree();
        }

        // Header Row (6 cells)
        var headerColor = new Color(0.98f, 0.85f, 0.35f); // Gold
        string[] headers = { "玩家", "击杀", "损失", "出兵", "伤害", "金币/收入" };
        float[] colWidths = { 132f, 70f, 70f, 80f, 110f, 110f };

        for (int i = 0; i < 6; i++)
        {
            var alignment = (i == 0) ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            var cell = CreateReportCell(colWidths[i], 18, alignment, 12, headerColor, true);
            cell.Text = headers[i];
            gameOverReportGrid.AddChild(cell);
        }

        var myName = GameState.Instance?.Username ?? "指战员(我)";
        var bluePlayers = new[] { myName, "战鹰02", "钢铁泰坦" };
        var redPlayers = new[] { "阿尔法九号", "闪击野狼", "重装先锋" };

        System.Random rand = new System.Random(12345); // stable seed for teammates' details

        // 1. Blue Team (Ally) - 3 players
        for (int i = 0; i < 3; i++)
        {
            string name = $"[我方] {bluePlayers[i]}";
            int kills, losses, produced;
            float damage, gold;

            if (i == 0) // Local Player
            {
                kills = manager.PlayerUnitKills + manager.PlayerBuildingKills;
                losses = manager.PlayerUnitsLost + manager.PlayerBuildingsLost;
                produced = manager.PlayerUnitsProduced;
                damage = manager.PlayerDamageDealt;
                gold = manager.PlayerGoldIncome;
            }
            else // Teammates (simulated proportional to local player)
            {
                kills = System.Math.Max(0, (manager.PlayerUnitKills + manager.PlayerBuildingKills) * (10 - i * 3) / 10 + rand.Next(-1, 2));
                losses = System.Math.Max(0, (manager.PlayerUnitsLost + manager.PlayerBuildingsLost) * (10 - i * 2) / 10 + rand.Next(-1, 2));
                produced = System.Math.Max(0, manager.PlayerUnitsProduced * (10 - i * 3) / 10 + rand.Next(-1, 2));
                damage = System.Math.Max(0f, manager.PlayerDamageDealt * (10f - i * 3f) / 10f + rand.Next(-200, 200));
                gold = System.Math.Max(0f, manager.PlayerGoldIncome * (10f - i * 2f) / 10f + rand.Next(-100, 100));
            }

            AddPlayerRow(name, kills, losses, produced, damage, gold, new Color(0.42f, 0.94f, 0.76f)); // Light green/cyan for allies
        }

        // 2. Red Team (Enemy) - 3 players
        for (int i = 0; i < 3; i++)
        {
            string name = $"[敌方] {redPlayers[i]}";
            int kills, losses, produced;
            float damage, gold;

            // Enemies (simulated proportional to aggregate enemy stats)
            // Total enemy kills = player losses. Total enemy losses = player kills.
            int totalEnemyKills = manager.PlayerUnitsLost + manager.PlayerBuildingsLost;
            int totalEnemyLosses = manager.PlayerUnitKills + manager.PlayerBuildingKills;

            kills = System.Math.Max(0, totalEnemyKills * (10 - i * 2) / 10 + rand.Next(-1, 2));
            losses = System.Math.Max(0, totalEnemyLosses * (10 - i * 3) / 10 + rand.Next(-1, 2));
            produced = System.Math.Max(0, manager.EnemyUnitsProduced * (10 - i * 3) / 10 + rand.Next(-1, 2));
            damage = System.Math.Max(0f, manager.EnemyDamageDealt * (10f - i * 3f) / 10f + rand.Next(-200, 200));
            gold = System.Math.Max(0f, manager.EnemyGoldIncome * (10f - i * 2f) / 10f + rand.Next(-100, 100));

            AddPlayerRow(name, kills, losses, produced, damage, gold, new Color(1.0f, 0.50f, 0.42f)); // Light red for enemies
        }

        var durationText = $"{Mathf.FloorToInt(manager.GameTime) / 60:00}:{Mathf.FloorToInt(manager.GameTime) % 60:00}";
        gameOverReportMeta.Text = $"战役模式 (3v3战术对抗)    用时 {durationText}";
    }

    void AddPlayerRow(string name, int kills, int losses, int produced, float damage, float gold, Color nameColor)
    {
        float[] colWidths = { 132f, 70f, 70f, 80f, 110f, 110f };
        var cellColor = new Color(0.9f, 0.9f, 0.9f); // Off-white for general values

        // Cell 0: Player name
        var nameCell = CreateReportCell(colWidths[0], 16, HorizontalAlignment.Left, 11, nameColor, false);
        nameCell.Text = name;
        gameOverReportGrid.AddChild(nameCell);

        // Cell 1: Kills
        var killsCell = CreateReportCell(colWidths[1], 16, HorizontalAlignment.Center, 11, cellColor, false);
        killsCell.Text = kills.ToString();
        gameOverReportGrid.AddChild(killsCell);

        // Cell 2: Losses
        var lossesCell = CreateReportCell(colWidths[2], 16, HorizontalAlignment.Center, 11, cellColor, false);
        lossesCell.Text = losses.ToString();
        gameOverReportGrid.AddChild(lossesCell);

        // Cell 3: Produced
        var prodCell = CreateReportCell(colWidths[3], 16, HorizontalAlignment.Center, 11, cellColor, false);
        prodCell.Text = produced.ToString();
        gameOverReportGrid.AddChild(prodCell);

        // Cell 4: Damage
        var dmgCell = CreateReportCell(colWidths[4], 16, HorizontalAlignment.Center, 11, cellColor, false);
        dmgCell.Text = damage.ToString("N0");
        gameOverReportGrid.AddChild(dmgCell);

        // Cell 5: Gold
        var goldCell = CreateReportCell(colWidths[5], 16, HorizontalAlignment.Center, 11, cellColor, false);
        goldCell.Text = gold.ToString("N0");
        gameOverReportGrid.AddChild(goldCell);
    }

    void StartGameOverCountdown(float seconds)
    {
        gameOverCountdownRemaining = seconds;
        gameOverCountdownActive = true;
        gameOverTransitionTriggered = false;
        UpdateGameOverCountdownLabel();
    }

    void UpdateGameOverCountdown(float delta)
    {
        if (!gameOverCountdownActive || gameOverTransitionTriggered || !gameOverOverlay.Visible)
            return;

        gameOverCountdownRemaining = Mathf.Max(0f, gameOverCountdownRemaining - delta);
        UpdateGameOverCountdownLabel();
        if (gameOverCountdownRemaining > 0f)
            return;

        gameOverCountdownActive = false;
        ReturnToLobbyFromGameOver();
    }

    void UpdateGameOverCountdownLabel()
    {
        if (gameOverCountdownLabel is null)
            return;

        gameOverCountdownLabel.Text = gameOverTransitionTriggered
            ? "正在返回大厅..."
            : $"{Mathf.CeilToInt(gameOverCountdownRemaining)}秒后自动返回大厅";
    }

    void ReturnToLobbyFromGameOver()
    {
        if (gameOverTransitionTriggered)
            return;

        gameOverTransitionTriggered = true;
        gameOverCountdownActive = false;
        UpdateGameOverCountdownLabel();
        
        // 战斗正式结束，清除大厅的“继续战斗”记录
        GameState.Instance?.ClearBattleEntry();
        
        GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn");
    }

    void OnSurrenderPressed()
    {
        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        if (!manager.TrySurrender(out var msg))
        {
            ShowAlert(msg);
            return;
        }
        // 关闭设置面板，让结算界面正常弹出
        if (settingsDialogRoot is not null)
            settingsDialogRoot.Visible = false;
    }

    void ReloadBattleFromGameOver()
    {
        if (gameOverTransitionTriggered)
            return;

        gameOverTransitionTriggered = true;
        gameOverCountdownActive = false;
        GetTree().ReloadCurrentScene();
    }

    void AddGameOverChrome(Control parent)
    {
        var topGlow = new ColorRect
        {
            Position = new Vector2(14f, 10f),
            Size = new Vector2(parent.Size.X - 28f, 2f),
            Color = new Color(0.92f, 0.76f, 0.34f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(topGlow);
        parent.MoveChild(topGlow, 0);

        var bottomGlow = new ColorRect
        {
            Position = new Vector2(14f, parent.Size.Y - 12f),
            Size = new Vector2(parent.Size.X - 28f, 1.5f),
            Color = new Color(0.42f, 0.72f, 0.70f, 0.28f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(bottomGlow);
        parent.MoveChild(bottomGlow, 0);
    }

    void BuildMinimap(Control root)
    {
        minimapPanel = Panel(new Vector2(1040, 494), new Vector2(220, 206), new Color(0.02f, 0.04f, 0.05f, 0.82f));
        minimapPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        minimapPanel.GuiInput += ConsumeHudPointerInput;
        root.AddChild(minimapPanel);

        var title = HudLabel("战术地图", 15, new Color(1f, 0.88f, 0.58f));
        title.Position = new Vector2(12, 8);
        title.Size = new Vector2(140, 22);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        minimapPanel.AddChild(title);

        minimap = new BattleMinimap
        {
            Name = "BattleMinimap",
            Position = new Vector2(10, 34),
            Size = new Vector2(200, 160)
        };
        minimapPanel.AddChild(minimap);
        gameplayHudItems.Add(minimapPanel);
    }

    void ConsumeHudPointerInput(InputEvent evt)
    {
        if (evt is not InputEventMouseButton
            && evt is not InputEventMouseMotion
            && evt is not InputEventScreenTouch
            && evt is not InputEventScreenDrag)
            return;

        GetViewport().SetInputAsHandled();
    }

    public bool IsPointerOverBlockingHud(Vector2 screenPosition)
    {
        return (commandPanel is not null
                && commandPanel.Visible
                && commandPanel.GetGlobalRect().HasPoint(screenPosition))
            || (minimapPanel is not null
                && minimapPanel.Visible
                && minimapPanel.GetGlobalRect().HasPoint(screenPosition))
            || (battleCommunicationPanel is not null
                && battleCommunicationPanel.Visible
                && battleCommunicationPanel.GetGlobalRect().HasPoint(screenPosition));
    }

    void BuildUpgradeDialog(Control root)
    {
        upgradeDialogRoot = new Control
        {
            Name = "UpgradeDialogRoot",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.AddChild(upgradeDialogRoot);

        var dim = new ColorRect
        {
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.58f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        upgradeDialogRoot.AddChild(dim);

        upgradeDialogPanel = Panel(new Vector2(370, 124), new Vector2(540, 430), new Color(0.03f, 0.05f, 0.06f, 0.96f));
        upgradeDialogRoot.AddChild(upgradeDialogPanel);

        var content = new MarginContainer
        {
            Position = new Vector2(18, 18),
            Size = new Vector2(504, 394)
        };
        content.AddThemeConstantOverride("margin_left", 8);
        content.AddThemeConstantOverride("margin_top", 8);
        content.AddThemeConstantOverride("margin_right", 8);
        content.AddThemeConstantOverride("margin_bottom", 8);
        upgradeDialogPanel.AddChild(content);

        var stack = new VBoxContainer
        {
            Size = new Vector2(488, 378)
        };
        stack.AddThemeConstantOverride("separation", 8);
        content.AddChild(stack);

        upgradeDialogTitle = HudLabel("升级预览", 24, new Color(1f, 0.90f, 0.62f));
        upgradeDialogTitle.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(upgradeDialogTitle);

        upgradeDialogSummary = HudLabel("", 16, new Color(0.82f, 0.92f, 0.97f));
        upgradeDialogSummary.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(upgradeDialogSummary);

        upgradeDialogHint = HudLabel("", 15, new Color(0.78f, 0.98f, 0.82f));
        upgradeDialogHint.HorizontalAlignment = HorizontalAlignment.Center;
        upgradeDialogHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        upgradeDialogHint.CustomMinimumSize = new Vector2(488, 38);
        stack.AddChild(upgradeDialogHint);

        var requirementTitle = HudLabel("升级条件", 15, new Color(0.72f, 1f, 0.78f));
        requirementTitle.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(requirementTitle);

        var requirementScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(488, 192)
        };
        stack.AddChild(requirementScroll);

        upgradeRequirementList = new VBoxContainer
        {
            Size = new Vector2(468, 176)
        };
        upgradeRequirementList.AddThemeConstantOverride("separation", 3);
        requirementScroll.AddChild(upgradeRequirementList);

        var buttonRow = new HBoxContainer
        {
            Name = "UpgradeDialogButtons",
            Size = new Vector2(488, 48)
        };
        buttonRow.AddThemeConstantOverride("separation", 12);
        stack.AddChild(buttonRow);

        upgradeConfirmButton = new Button
        {
            Text = "确认升级",
            CustomMinimumSize = new Vector2(170, 44)
        };
        upgradeConfirmButton.AddThemeFontSizeOverride("font_size", 15);
        upgradeConfirmButton.AddThemeColorOverride("font_color", new Color(0.93f, 1f, 0.94f));
        upgradeConfirmButton.Pressed += ConfirmUpgradeBuilding;
        buttonRow.AddChild(upgradeConfirmButton);

        upgradeCancelButton = new Button
        {
            Text = "取消",
            CustomMinimumSize = new Vector2(130, 44)
        };
        upgradeCancelButton.AddThemeFontSizeOverride("font_size", 15);
        upgradeCancelButton.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.90f));
        upgradeCancelButton.Pressed += CloseUpgradeDialog;
        buttonRow.AddChild(upgradeCancelButton);
    }

    void BuildSettingsDialog(Control root)
    {
        settingsDialogRoot = new Control
        {
            Name = "SettingsDialogRoot",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.AddChild(settingsDialogRoot);

        var dim = new ColorRect
        {
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.34f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        settingsDialogRoot.AddChild(dim);

        settingsDialogPanel = Panel(new Vector2(260, 70), new Vector2(760, 580), new Color(0.030f, 0.046f, 0.058f, 0.96f));
        settingsDialogRoot.AddChild(settingsDialogPanel);
        AddTextureFrame(settingsDialogPanel, UnityFrameRoot + "panel_task_frame.png", new Color(1f, 1f, 1f, 0.16f));

        var title = HudLabel("战斗设置", 25, new Color(1f, 0.90f, 0.62f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Position = new Vector2(28, 18);
        title.Size = new Vector2(704, 32);
        settingsDialogPanel.AddChild(title);

        var closeButton = new Button
        {
            Text = "×",
            Position = new Vector2(710, 14),
            Size = new Vector2(32, 30)
        };
        ApplyButtonStyle(closeButton, new Color(0.12f, 0.14f, 0.16f, 0.96f), new Color(0.85f, 0.62f, 0.18f, 0.82f), 18);
        closeButton.Pressed += ToggleSettingsDialog;
        settingsDialogPanel.AddChild(closeButton);

        settingsDialogSubtitle = HudLabel("队友介绍与通信接收控制", 13, new Color(0.74f, 0.88f, 0.92f));
        settingsDialogSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        settingsDialogSubtitle.Position = new Vector2(80, 48);
        settingsDialogSubtitle.Size = new Vector2(600, 22);
        settingsDialogPanel.AddChild(settingsDialogSubtitle);

        var rosterPanel = Panel(new Vector2(24, 82), new Vector2(286, 398), new Color(0.020f, 0.032f, 0.038f, 0.84f));
        settingsDialogPanel.AddChild(rosterPanel);

        var rosterTitle = HudLabel("战场成员", 16, new Color(1f, 0.86f, 0.42f));
        rosterTitle.Position = new Vector2(14, 12);
        rosterTitle.Size = new Vector2(130, 22);
        rosterPanel.AddChild(rosterTitle);

        settingsRosterStatusLabel = HudLabel("", 12, new Color(0.68f, 0.84f, 0.88f));
        settingsRosterStatusLabel.HorizontalAlignment = HorizontalAlignment.Right;
        settingsRosterStatusLabel.Position = new Vector2(142, 14);
        settingsRosterStatusLabel.Size = new Vector2(128, 18);
        rosterPanel.AddChild(settingsRosterStatusLabel);

        var rosterScroll = new ScrollContainer
        {
            Position = new Vector2(12, 48),
            Size = new Vector2(262, 334),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        rosterPanel.AddChild(rosterScroll);

        settingsParticipantList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(244, 0)
        };
        settingsParticipantList.AddThemeConstantOverride("separation", 8);
        rosterScroll.AddChild(settingsParticipantList);

        var detailPanel = Panel(new Vector2(326, 82), new Vector2(410, 398), new Color(0.018f, 0.030f, 0.036f, 0.86f));
        settingsDialogPanel.AddChild(detailPanel);

        settingsParticipantTitle = HudLabel("选择队友", 20, new Color(0.86f, 1f, 0.90f));
        settingsParticipantTitle.Position = new Vector2(18, 18);
        settingsParticipantTitle.Size = new Vector2(254, 30);
        detailPanel.AddChild(settingsParticipantTitle);

        settingsParticipantRole = HudLabel("", 13, new Color(1f, 0.86f, 0.48f));
        settingsParticipantRole.Position = new Vector2(18, 52);
        settingsParticipantRole.Size = new Vector2(254, 24);
        detailPanel.AddChild(settingsParticipantRole);

        settingsParticipantReceive = HudLabel("", 13, new Color(0.76f, 0.92f, 1f));
        settingsParticipantReceive.Position = new Vector2(18, 84);
        settingsParticipantReceive.Size = new Vector2(254, 42);
        settingsParticipantReceive.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantReceive);

        var avatarFrame = new TextureRect
        {
            Position = new Vector2(302, 24),
            Size = new Vector2(68, 68),
            Texture = LoadHudTexture(UnityLobbyGenRoot + "gen_portrait_ring.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        PortraitFrameUtils.ApplyWrapFrame(
            avatarFrame,
            new Color(0.86f, 0.66f, 0.20f, 0.98f),
            new Color(1f, 0.96f, 0.80f, 0.92f),
            0.43f,
            0.015f,
            0.0035f,
            0.010f);
        detailPanel.AddChild(avatarFrame);

        settingsParticipantAvatar = new TextureRect
        {
            Position = new Vector2(306, 28),
            Size = new Vector2(60, 60),
            Texture = LoadHudTexture(UnityBattleHudRoot + "battle_avatar_placeholder.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        PortraitFrameUtils.ApplyCircularPortrait(
            settingsParticipantAvatar,
            new Color(0.94f, 0.76f, 0.28f, 1f),
            new Color(1f, 0.96f, 0.80f, 1f),
            0.47f,
            0.0055f,
            0.012f,
            new Vector2(1.24f, 1.24f),
            new Vector2(0.01f, -0.025f));
        detailPanel.AddChild(settingsParticipantAvatar);

        settingsParticipantRankBadgeIcon = new TextureRect
        {
            Position = new Vector2(308, 84),
            Size = new Vector2(38, 38),
            Texture = LoadHudTexture(UnityBattleHudRoot + "battle_rank_badge_placeholder.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        detailPanel.AddChild(settingsParticipantRankBadgeIcon);

        settingsParticipantBadge = HudLabel("", 15, new Color(1f, 0.90f, 0.55f));
        settingsParticipantBadge.Position = new Vector2(350, 92);
        settingsParticipantBadge.Size = new Vector2(48, 20);
        settingsParticipantBadge.HorizontalAlignment = HorizontalAlignment.Left;
        detailPanel.AddChild(settingsParticipantBadge);

        var infoTitle = HudLabel("队友介绍", 15, new Color(1f, 0.86f, 0.42f));
        infoTitle.Position = new Vector2(18, 162);
        infoTitle.Size = new Vector2(160, 24);
        detailPanel.AddChild(infoTitle);

        settingsParticipantIntro = HudLabel("", 14, new Color(0.86f, 0.94f, 0.96f));
        settingsParticipantIntro.Position = new Vector2(18, 192);
        settingsParticipantIntro.Size = new Vector2(372, 80);
        settingsParticipantIntro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantIntro);

        settingsParticipantReportHint = HudLabel("", 12, new Color(0.92f, 0.72f, 0.52f));
        settingsParticipantReportHint.Position = new Vector2(18, 282);
        settingsParticipantReportHint.Size = new Vector2(372, 46);
        settingsParticipantReportHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantReportHint);

        settingsTextMuteButton = new Button
        {
            Name = "SettingsTextMuteButton",
            Text = "禁文字",
            Position = new Vector2(18, 338),
            Size = new Vector2(116, 38),
            Icon = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png"),
            ExpandIcon = true
        };
        settingsTextMuteButton.Pressed += ToggleSelectedBattleTextBlock;
        detailPanel.AddChild(settingsTextMuteButton);

        settingsVoiceMuteButton = new Button
        {
            Name = "SettingsVoiceMuteButton",
            Text = "禁语音",
            Position = new Vector2(142, 338),
            Size = new Vector2(116, 38),
            Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOff.png"),
            ExpandIcon = true
        };
        settingsVoiceMuteButton.Pressed += ToggleSelectedBattleVoiceBlock;
        detailPanel.AddChild(settingsVoiceMuteButton);

        settingsReportButton = new Button
        {
            Name = "SettingsReportButton",
            Text = "投诉",
            Position = new Vector2(266, 338),
            Size = new Vector2(116, 38),
            Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/exclamation.png"),
            ExpandIcon = true
        };
        settingsReportButton.Pressed += ReportSelectedBattleParticipant;
        detailPanel.AddChild(settingsReportButton);

        settingsMuteAllButton = new Button { Name = "SettingsMuteAllButton", Visible = false };


        settingsReturnLobbyButton = AddSettingsActionButton("返回大厅", new Vector2(24, 500), new Vector2(132, 42), new Color(0.42f, 0.17f, 0.12f, 0.96f), () => GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn"));

        // 投降按钮：主动认输，立即触发失败结算
        var surrenderButton = AddSettingsActionButton("投降", new Vector2(168, 500), new Vector2(100, 42), new Color(0.38f, 0.12f, 0.12f, 0.96f), OnSurrenderPressed);
        surrenderButton.TooltipText = "主动投降，立即触发失败结算（开局 30 秒内不可投降）";

        settingsReportDialogRoot = new Control
        {
            Name = "SettingsReportDialog",
            Position = new Vector2(150, 122),
            Size = new Vector2(460, 286),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        settingsDialogPanel.AddChild(settingsReportDialogRoot);

        var reportDim = new ColorRect
        {
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        settingsReportDialogRoot.AddChild(reportDim);

        var reportPanel = Panel(new Vector2(14, 14), new Vector2(432, 258), new Color(0.028f, 0.038f, 0.044f, 0.98f));
        settingsReportDialogRoot.AddChild(reportPanel);

        settingsReportDialogTitle = HudLabel("举报队友", 20, new Color(1f, 0.86f, 0.42f));
        settingsReportDialogTitle.HorizontalAlignment = HorizontalAlignment.Center;
        settingsReportDialogTitle.Position = new Vector2(20, 16);
        settingsReportDialogTitle.Size = new Vector2(392, 28);
        reportPanel.AddChild(settingsReportDialogTitle);

        var reportHint = HudLabel("选择原因后会立即记录到当前本地战斗档案，并在成员卡片中标记。", 13, new Color(0.78f, 0.90f, 0.94f));
        reportHint.HorizontalAlignment = HorizontalAlignment.Center;
        reportHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reportHint.Position = new Vector2(22, 50);
        reportHint.Size = new Vector2(388, 42);
        reportPanel.AddChild(reportHint);

        var reasons = new (string Text, Color Bg)[]
        {
            ("恶意辱骂", new Color(0.34f, 0.18f, 0.16f, 0.96f)),
            ("消极挂机", new Color(0.34f, 0.24f, 0.12f, 0.96f)),
            ("恶意送人头", new Color(0.28f, 0.18f, 0.34f, 0.96f)),
            ("疑似作弊", new Color(0.38f, 0.14f, 0.14f, 0.98f))
        };
        for (var i = 0; i < reasons.Length; i++)
        {
            var row = i / 2;
            var col = i % 2;
            var reasonButton = new Button
            {
                Text = reasons[i].Text,
                Position = new Vector2(24 + col * 194, 108 + row * 52),
                Size = new Vector2(182, 40)
            };
            ApplyButtonStyle(reasonButton, reasons[i].Bg, new Color(1f, 0.78f, 0.34f, 0.82f), 14);
            var reasonText = reasons[i].Text;
            reasonButton.Pressed += () => SubmitBattleReport(reasonText);
            reportPanel.AddChild(reasonButton);
        }

        var cancelReportButton = new Button
        {
            Text = "取消",
            Position = new Vector2(146, 214),
            Size = new Vector2(140, 32)
        };
        ApplyButtonStyle(cancelReportButton, new Color(0.16f, 0.18f, 0.20f, 0.96f), new Color(0.78f, 0.84f, 0.90f, 0.76f), 13);
        cancelReportButton.Pressed += CloseBattleReportDialog;
        reportPanel.AddChild(cancelReportButton);
    }

    Button AddSettingsActionButton(string text, Vector2 position, Vector2 size, Color bg, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Position = position,
            Size = size
        };
        ApplyButtonStyle(button, bg, new Color(1f, 0.76f, 0.30f, 0.84f), 14);
        button.Pressed += onPressed;
        settingsDialogPanel.AddChild(button);
        return button;
    }


    void RefreshSettingsDisplayOptions()
    {
        if (settingsMainBaseIdentityToggle is null)
            return;

        var showIdentity = GameState.Instance?.ShowMainBaseIdentity ?? true;
        refreshingSettingsDisplayOptions = true;
        settingsMainBaseIdentityToggle.ButtonPressed = showIdentity;
        settingsMainBaseIdentityToggle.Text = showIdentity ? "显示主基地身份" : "隐藏主基地身份";
        if (settingsMainBaseIdentityHint is not null)
        {
            settingsMainBaseIdentityHint.Text = showIdentity
                ? "玩家名、工会与等级会显示在主基地上方。"
                : "主基地头顶身份信息已隐藏。";
        }
        refreshingSettingsDisplayOptions = false;
    }

    void ToggleMainBaseIdentityDisplay(bool showIdentity)
    {
        if (refreshingSettingsDisplayOptions)
            return;

        GameState.Instance?.SetShowMainBaseIdentity(showIdentity);
        RefreshSettingsDisplayOptions();
        RefreshMainBaseIdentityLabels();
        ShowAlert(showIdentity ? "已显示主基地身份标识" : "已隐藏主基地身份标识");
    }

    void RefreshMainBaseIdentityLabels()
    {
        foreach (var node in GetTree().GetNodesInGroup("rts_buildings"))
        {
            if (node is RtsBuilding building && GodotObject.IsInstanceValid(building))
                building.RefreshMainBaseIdentity();
        }
    }
    void RefreshSettingsParticipantList()
    {
        if (settingsParticipantList is null)
            return;

        FreeChildNodes(settingsParticipantList);

        if (settingsParticipants.Length == 0)
        {
            var label = HudLabel("暂无战场成员", 14, new Color(0.78f, 0.88f, 0.92f));
            label.CustomMinimumSize = new Vector2(244, 32);
            settingsParticipantList.AddChild(label);
            if (settingsRosterStatusLabel is not null)
                settingsRosterStatusLabel.Text = "0 人";
            return;
        }

        if (settingsRosterStatusLabel is not null)
            settingsRosterStatusLabel.Text = $"{settingsParticipants.Length} 人";

        foreach (var participant in settingsParticipants)
        {
            var pref = GameState.Instance?.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName) ?? default;
            var isSelected = participant.ParticipantId == selectedBattleParticipantId;
            var canControl = CanControlBattleParticipant(participant);
            var canReport = participant.CanReport && !participant.IsLocalPlayer;
            var tag = ParticipantRosterTag(participant, pref);

            var card = new Panel
            {
                CustomMinimumSize = new Vector2(238, 84),
                ClipContents = true
            };
            var bg = isSelected
                ? new Color(0.18f, 0.34f, 0.40f, 0.96f)
                : participant.IsLocalPlayer
                    ? new Color(0.18f, 0.26f, 0.20f, 0.92f)
                    : new Color(0.08f, 0.11f, 0.12f, 0.94f);
            var accent = isSelected
                ? new Color(0.42f, 0.96f, 0.90f, 0.92f)
                : participant.IsLocalPlayer
                    ? new Color(0.62f, 0.92f, 0.66f, 0.80f)
                    : new Color(1f, 0.74f, 0.30f, 0.72f);
            MetalUiStyle.ApplyMetalPanel(card, new MetalUiStyle.MetalPalette(
                bg,
                accent,
                new Color(1f, 0.95f, 0.84f, 0.22f),
                new Color(0.02f, 0.02f, 0.03f, 0.92f),
                new Color(accent.R, accent.G, accent.B, 0.08f)),
                1,
                6,
                3);

            var button = new Button
            {
                Text = $"{participant.DisplayName}    {tag}",
                Position = new Vector2(6, 6),
                Size = new Vector2(226, 30),
                Flat = true
            };
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(1f, 1f, 1f, 0.05f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            });
            button.Pressed += () =>
            {
                selectedBattleParticipantId = participant.ParticipantId;
                RefreshSettingsParticipantList();
                RefreshSettingsParticipantDetails();
            };
            card.AddChild(button);

            var actionRow = new HBoxContainer
            {
                Name = $"ParticipantActions_{participant.ParticipantId}",
                Position = new Vector2(6, 40),
                Size = new Vector2(226, 36)
            };
            actionRow.AddThemeConstantOverride("separation", 6);
            card.AddChild(actionRow);

            var textBtn = new Button
            {
                Name = $"TextMute_{participant.ParticipantId}",
                CustomMinimumSize = new Vector2(71f, 32f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ExpandIcon = true
            };
            
            var voiceBtn = new Button
            {
                Name = $"VoiceMute_{participant.ParticipantId}",
                CustomMinimumSize = new Vector2(71f, 32f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ExpandIcon = true
            };

            var reportBtn = new Button
            {
                Name = $"Report_{participant.ParticipantId}",
                CustomMinimumSize = new Vector2(71f, 32f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ExpandIcon = true
            };

            if (canControl)
            {
                textBtn.Text = "";
                textBtn.Icon = LoadHudTexture(pref.TextBlocked 
                    ? "res://assets/third_party/kenney/game-icons/PNG/White/2x/cross.png" 
                    : "res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png");
                ApplyButtonStyle(textBtn,
                    pref.TextBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.18f, 0.30f, 0.42f, 0.96f),
                    pref.TextBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.72f, 0.92f, 1f, 0.84f),
                    10);
                textBtn.Pressed += () => {
                    ToggleBattleTextBlockFor(participant.ParticipantId, participant.DisplayName);
                    RefreshSettingsParticipantList();
                    RefreshSettingsParticipantDetails();
                };

                voiceBtn.Text = "";
                voiceBtn.Icon = LoadHudTexture(pref.VoiceBlocked 
                    ? "res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOff.png" 
                    : "res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOn.png");
                ApplyButtonStyle(voiceBtn,
                    pref.VoiceBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.16f, 0.36f, 0.36f, 0.96f),
                    pref.VoiceBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.70f, 0.98f, 0.94f, 0.84f),
                    10);
                voiceBtn.Pressed += () => {
                    ToggleBattleVoiceBlockFor(participant.ParticipantId, participant.DisplayName);
                    RefreshSettingsParticipantList();
                    RefreshSettingsParticipantDetails();
                };
            }
            else
            {
                textBtn.Text = "";
                textBtn.Icon = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png");
                ApplyButtonStyle(textBtn, new Color(0.20f, 0.22f, 0.24f, 0.92f), new Color(0.62f, 0.68f, 0.72f, 0.42f), 10);
                textBtn.Disabled = true;

                voiceBtn.Text = "";
                voiceBtn.Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOff.png");
                ApplyButtonStyle(voiceBtn, new Color(0.20f, 0.22f, 0.24f, 0.92f), new Color(0.62f, 0.68f, 0.72f, 0.42f), 10);
                voiceBtn.Disabled = true;
            }

            reportBtn.Text = "";
            reportBtn.Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/exclamation.png");
            ApplyButtonStyle(
                reportBtn,
                canReport && !pref.Reported ? new Color(0.44f, 0.16f, 0.13f, 0.96f) : new Color(0.20f, 0.22f, 0.24f, 0.92f),
                canReport && !pref.Reported ? new Color(1f, 0.74f, 0.56f, 0.84f) : new Color(0.62f, 0.68f, 0.72f, 0.42f),
                10);
            if (canReport && !pref.Reported)
            {
                reportBtn.Pressed += () => {
                    OpenBattleReportDialog(participant.ParticipantId, participant.DisplayName);
                };
            }
            else
            {
                reportBtn.Disabled = true;
            }

            actionRow.AddChild(textBtn);
            actionRow.AddChild(voiceBtn);
            actionRow.AddChild(reportBtn);

            settingsParticipantList.AddChild(card);
        }
    }

    Button CreateSettingsInlineIconButton(string glyph, string tooltip, Color bg, Action onPressed)
    {
        var button = new Button
        {
            Text = glyph,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        ApplyButtonStyle(button, bg, new Color(1f, 0.76f, 0.30f, 0.72f), 16);
        button.Pressed += onPressed;
        return button;
    }

    void RefreshSettingsParticipantDetails()
    {
        if (settingsParticipantTitle is null)
            return;

        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null)
        {
            settingsParticipantTitle.Text = "暂无成员";
            settingsParticipantRole.Text = "身份：未连接";
            settingsParticipantReceive.Text = "当前没有可配置的队友通信对象。";
            settingsParticipantIntro.Text = "离线演练或房间中尚未检测到队友。";
            settingsParticipantReportHint.Text = "当联机队友加入后，这里会展示其介绍并开放屏蔽/举报操作。";
            settingsParticipantAvatar.Texture = LoadHudTexture(UnityBattleHudRoot + "battle_avatar_placeholder.png");
            settingsParticipantAvatar.Modulate = new Color(0.70f, 0.74f, 0.78f, 0.88f);
            settingsParticipantRankBadgeIcon.Texture = LoadHudTexture(UnityBattleHudRoot + "battle_rank_badge_placeholder.png");
            settingsParticipantRankBadgeIcon.Modulate = new Color(0.82f, 0.84f, 0.88f, 0.92f);
            settingsParticipantBadge.Text = "未连接";
            settingsParticipantBadge.Modulate = new Color(0.76f, 0.82f, 0.88f, 0.92f);
            settingsTextMuteButton.Visible = false;
            settingsVoiceMuteButton.Visible = false;
            settingsReportButton.Visible = false;
            SetSettingsActionButtonsEnabled(false, false, false, false);
            return;
        }

        var pref = GameState.Instance?.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName) ?? default;
        var canControl = CanControlBattleParticipant(participant);
        var canReport = participant.CanReport && !participant.IsLocalPlayer;

        settingsTextMuteButton.Visible = canControl;
        settingsVoiceMuteButton.Visible = canControl;
        settingsReportButton.Visible = canReport;

        settingsParticipantTitle.Text = participant.IsLocalPlayer ? $"{participant.DisplayName}（自己）" : participant.DisplayName;
        settingsParticipantRole.Text = $"身份：{participant.Role}";
        settingsParticipantReceive.Text = ParticipantReceiveSummary(participant, pref);
        settingsParticipantIntro.Text = participant.Intro;
        settingsParticipantAvatar.Texture = LoadHudTexture(ResolveParticipantAvatarTexturePath(participant));
        settingsParticipantAvatar.Modulate = ParticipantAvatarTint(participant, pref);
        settingsParticipantRankBadgeIcon.Texture = LoadHudTexture(ResolveParticipantRankBadgeTexturePath(participant));
        settingsParticipantRankBadgeIcon.Modulate = Colors.White;
        settingsParticipantBadge.Text = ParticipantRankTitle(participant);
        settingsParticipantBadge.Modulate = ParticipantBadgeColor(participant, pref);
        settingsParticipantReportHint.Text = pref.Reported
            ? $"举报状态：已记录。{(string.IsNullOrWhiteSpace(pref.ReportReason) ? "原因未填写。" : $"原因：{pref.ReportReason}")}"
            : ParticipantReportHint(participant, canReport);

        if (canControl)
        {
            settingsTextMuteButton.Text = pref.TextBlocked ? "接收文字" : "禁文字";
            settingsVoiceMuteButton.Text = pref.VoiceBlocked ? "接收语音" : "禁语音";
            
            settingsTextMuteButton.Icon = LoadHudTexture(pref.TextBlocked 
                ? "res://assets/third_party/kenney/game-icons/PNG/White/2x/cross.png" 
                : "res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png");
            settingsVoiceMuteButton.Icon = LoadHudTexture(pref.VoiceBlocked 
                ? "res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOff.png" 
                : "res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOn.png");

            ApplyButtonStyle(settingsTextMuteButton,
                pref.TextBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.18f, 0.30f, 0.42f, 0.96f),
                pref.TextBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.72f, 0.92f, 1f, 0.84f),
                13);
            ApplyButtonStyle(settingsVoiceMuteButton,
                pref.VoiceBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.16f, 0.36f, 0.36f, 0.96f),
                pref.VoiceBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.70f, 0.98f, 0.94f, 0.84f),
                13);
        }
        else
        {
            settingsTextMuteButton.Text = participant.IsLocalPlayer ? "自己" : participant.ParticipantId == "local-ai" ? "离线" : "等待队友";
            settingsVoiceMuteButton.Text = "不可操作";
            
            settingsTextMuteButton.Icon = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/UI/Icons/chat_icon.png");
            settingsVoiceMuteButton.Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/audioOff.png");

            ApplyButtonStyle(settingsTextMuteButton, new Color(0.20f, 0.22f, 0.24f, 0.92f), new Color(0.62f, 0.68f, 0.72f, 0.42f), 13);
            ApplyButtonStyle(settingsVoiceMuteButton, new Color(0.20f, 0.22f, 0.24f, 0.92f), new Color(0.62f, 0.68f, 0.72f, 0.42f), 13);
        }

        settingsReportButton.Text = pref.Reported ? "已举报" : canReport ? "投诉" : "不可投诉";
        settingsReportButton.Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/exclamation.png");
        ApplyButtonStyle(
            settingsReportButton,
            canReport && !pref.Reported ? new Color(0.44f, 0.16f, 0.13f, 0.96f) : new Color(0.20f, 0.22f, 0.24f, 0.92f),
            canReport && !pref.Reported ? new Color(1f, 0.74f, 0.56f, 0.84f) : new Color(0.62f, 0.68f, 0.72f, 0.42f),
            13);

        SetSettingsActionButtonsEnabled(canControl, canControl, false, canReport && !pref.Reported);
    }

    static string ParticipantRosterTag(GameState.BattleParticipantProfile participant, GameState.BattleCommunicationPreference pref)
    {
        if (participant.IsLocalPlayer)
            return "自己";
        if (participant.ParticipantId == "local-ai")
            return "离线";
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return "待连接";
        if (pref.Reported)
            return "已举报";
        if (pref.TextBlocked && pref.VoiceBlocked)
            return "禁文/禁语";
        if (pref.TextBlocked)
            return "禁文";
        if (pref.VoiceBlocked)
            return "禁语";
        return "接收中";
    }

    static string ParticipantActionHint(GameState.BattleParticipantProfile participant)
    {
        if (participant.IsLocalPlayer)
            return "当前账号，不能屏蔽自己。";
        if (participant.ParticipantId == "local-ai")
            return "本地演练没有真实队友通信。";
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return "队友加入后开放屏蔽和举报。";
        return "当前对象暂无可执行的通信控制。";
    }

    static string ParticipantReceiveSummary(GameState.BattleParticipantProfile participant, GameState.BattleCommunicationPreference pref)
    {
        if (participant.IsLocalPlayer)
            return "当前账号：不能对自己设置屏蔽。";
        if (participant.ParticipantId == "local-ai")
            return "离线演练：没有真实队友通信可供屏蔽。";
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return "房间暂无队友连接，加入后开放通信控制。";
        return $"文字：{(pref.TextBlocked ? "已屏蔽" : "接收中")}    语音：{(pref.VoiceBlocked ? "已屏蔽" : "接收中")}";
    }

    static string ParticipantReportHint(GameState.BattleParticipantProfile participant, bool canReport)
    {
        if (canReport)
            return "可在这里举报该队友；确认后会立即记录到当前本地战斗档案。";
        if (participant.IsLocalPlayer)
            return "当前账号不支持举报自己。";
        if (participant.ParticipantId == "local-ai")
            return "离线演练对象不支持举报。";
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return "等待真实队友连接后，才会开放举报功能。";
        return "当前对象不支持举报。";
    }

    static Color ParticipantAvatarTint(GameState.BattleParticipantProfile participant, GameState.BattleCommunicationPreference pref)
    {
        if (participant.IsLocalPlayer)
            return new Color(0.76f, 0.94f, 1f, 1f);
        if (pref.Reported)
            return new Color(1f, 0.72f, 0.62f, 1f);
        if (pref.TextBlocked && pref.VoiceBlocked)
            return new Color(0.86f, 0.78f, 0.64f, 1f);
        return new Color(0.92f, 0.92f, 0.92f, 1f);
    }

    static string ParticipantBadgeText(GameState.BattleParticipantProfile participant, GameState.BattleCommunicationPreference pref)
    {
        if (participant.IsLocalPlayer)
            return NormalizeRankTitle(GameState.Instance?.RankTitle);
        if (participant.ParticipantId == "local-ai")
            return "离线演练";
        if (pref.Reported)
            return "战地成员";
        if (pref.TextBlocked && pref.VoiceBlocked)
            return "战地成员";
        return string.IsNullOrWhiteSpace(participant.Role) ? "战地成员" : participant.Role.Trim();
    }

    static Color ParticipantBadgeColor(GameState.BattleParticipantProfile participant, GameState.BattleCommunicationPreference pref)
    {
        if (participant.IsLocalPlayer)
            return new Color(1f, 0.90f, 0.55f);
        if (participant.ParticipantId == "local-ai")
            return new Color(0.82f, 0.84f, 0.88f);
        if (pref.Reported)
            return new Color(1f, 0.74f, 0.56f);
        if (pref.TextBlocked && pref.VoiceBlocked)
            return new Color(1f, 0.88f, 0.62f);
        return new Color(1f, 0.90f, 0.55f);
    }

    void SetSettingsActionButtonsEnabled(bool textEnabled, bool voiceEnabled, bool muteAllEnabled, bool reportEnabled)
    {
        if (settingsTextMuteButton is not null)
            settingsTextMuteButton.Disabled = !textEnabled;
        if (settingsVoiceMuteButton is not null)
            settingsVoiceMuteButton.Disabled = !voiceEnabled;
        if (settingsMuteAllButton is not null)
            settingsMuteAllButton.Disabled = !muteAllEnabled;
        if (settingsReportButton is not null)
            settingsReportButton.Disabled = !reportEnabled;
    }

    bool CanControlBattleParticipant(GameState.BattleParticipantProfile participant)
    {
        if (participant.IsLocalPlayer)
            return false;
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal) || participant.ParticipantId == "local-ai")
            return false;
        return !string.IsNullOrWhiteSpace(GameState.Instance?.CurrentRoomId);
    }

    GameState.BattleParticipantProfile? ResolveParticipantProfile(string participantId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(participantId))
        {
            var exact = settingsParticipants.FirstOrDefault(profile => profile.ParticipantId == participantId);
            if (exact is not null)
                return exact;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var byName = settingsParticipants.FirstOrDefault(profile => profile.DisplayName == displayName);
            if (byName is not null)
                return byName;
        }

        return settingsParticipants.FirstOrDefault();
    }

    void ToggleSelectedBattleTextBlock()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || !CanControlBattleParticipant(participant))
            return;
        ToggleBattleTextBlockFor(participant.ParticipantId, participant.DisplayName);
    }

    void ToggleBattleTextBlockFor(string participantId, string displayName)
    {
        if (GameState.Instance is null)
            return;
        selectedBattleParticipantId = participantId;
        var pref = GameState.Instance.GetBattleCommunicationPreference(participantId, displayName);
        var blocked = !pref.TextBlocked;
        GameState.Instance.SetBattleTextBlocked(participantId, displayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{displayName}{(blocked ? " 已屏蔽文字消息" : " 已恢复文字接收")}", new Color(0.72f, 0.92f, 1f));
        ShowAlert(blocked ? "已屏蔽该队友文字消息" : "已恢复该队友文字消息");
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void ToggleSelectedBattleVoiceBlock()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || !CanControlBattleParticipant(participant))
            return;
        ToggleBattleVoiceBlockFor(participant.ParticipantId, participant.DisplayName);
    }

    void ToggleBattleVoiceBlockFor(string participantId, string displayName)
    {
        if (GameState.Instance is null)
            return;
        selectedBattleParticipantId = participantId;
        var pref = GameState.Instance.GetBattleCommunicationPreference(participantId, displayName);
        var blocked = !pref.VoiceBlocked;
        GameState.Instance.SetBattleVoiceBlocked(participantId, displayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{displayName}{(blocked ? " 已屏蔽语音" : " 已恢复语音接收")}", new Color(0.72f, 1f, 0.94f));
        ShowAlert(blocked ? "已屏蔽该队友语音" : "已恢复该队友语音");
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void ToggleSelectedBattleMuteAll()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || !CanControlBattleParticipant(participant))
            return;
        ToggleBattleMuteAllFor(participant.ParticipantId, participant.DisplayName);
    }

    void ToggleBattleMuteAllFor(string participantId, string displayName)
    {
        if (GameState.Instance is null)
            return;
        selectedBattleParticipantId = participantId;
        var pref = GameState.Instance.GetBattleCommunicationPreference(participantId, displayName);
        var blocked = !(pref.TextBlocked && pref.VoiceBlocked);
        GameState.Instance.SetBattleMuteAll(participantId, displayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{displayName}{(blocked ? " 已全部屏蔽" : " 已取消全部屏蔽")}", new Color(1f, 0.84f, 0.58f));
        ShowAlert(blocked ? "已全部屏蔽该队友" : "已取消全部屏蔽");
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void ReportSelectedBattleParticipant()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || participant.IsLocalPlayer || !participant.CanReport)
            return;
        var pref = GameState.Instance.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName);
        if (pref.Reported)
        {
            ShowAlert("该队友已经记录举报");
            return;
        }

        OpenBattleReportDialog(participant.ParticipantId, participant.DisplayName);
    }

    void OpenBattleReportDialog(string participantId, string displayName)
    {
        if (settingsReportDialogRoot is null || GameState.Instance is null)
            return;

        var participant = ResolveParticipantProfile(participantId, displayName);
        if (participant is null || participant.IsLocalPlayer || !participant.CanReport)
            return;

        var pref = GameState.Instance.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName);
        if (pref.Reported)
        {
            ShowAlert("该队友已经记录举报");
            return;
        }

        selectedBattleParticipantId = participant.ParticipantId;
        pendingReportParticipantId = participant.ParticipantId;
        pendingReportParticipantName = participant.DisplayName;
        settingsReportDialogTitle.Text = $"举报 {participant.DisplayName}";
        settingsReportDialogRoot.Visible = true;
        settingsReportDialogRoot.MoveToFront();
    }

    void CloseBattleReportDialog()
    {
        pendingReportParticipantId = "";
        pendingReportParticipantName = "";
        if (settingsReportDialogRoot is not null)
            settingsReportDialogRoot.Visible = false;
    }

    void SubmitBattleReport(string reason)
    {
        if (GameState.Instance is null || string.IsNullOrWhiteSpace(pendingReportParticipantId))
            return;

        ReportBattleParticipant(pendingReportParticipantId, pendingReportParticipantName, reason);
        CloseBattleReportDialog();
    }

    void ReportBattleParticipant(string participantId, string displayName, string reason)
    {
        if (GameState.Instance is null)
            return;
        selectedBattleParticipantId = participantId;
        var reportReason = string.IsNullOrWhiteSpace(reason) ? "未选择原因" : reason.Trim();
        GameState.Instance.RecordBattleReport(participantId, displayName, reportReason);
        AppendBattleCommunicationMessage("设置", $"已记录对 {displayName} 的举报：{reportReason}", new Color(1f, 0.72f, 0.54f));
        ShowAlert("举报已记录");
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void AppendBattleCommunicationMessage(string speaker, string message, Color color)
    {
        if (battleCommunicationMessages is null)
            return;

        while (battleCommunicationLines.Count >= 10)
        {
            var first = battleCommunicationLines[0];
            battleCommunicationLines.RemoveAt(0);
            if (GodotObject.IsInstanceValid(first))
                first.QueueFree();
        }

        var line = HudLabel($"[{speaker}] {message}", 12, color);
        line.CustomMinimumSize = new Vector2(352, 22);
        line.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        line.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationMessages.AddChild(line);
        battleCommunicationLines.Add(line);
        RefreshBattleCommunicationPanelLayout();
        RefreshBattleCommunicationHeader();
        CallDeferred(nameof(ScrollBattleCommunicationToBottom));

        // 每次收到或发送消息时，在主屏上方进行可淡出的广播冒泡通知
        if (speaker != "设置")
        {
            ShowAlert($"📻 [{speaker}]: {message}");
        }
    }

    public void ReceiveBattleTextMessage(string participantId, string speaker, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var resolvedParticipantId = ResolveInboundBattleParticipantId(participantId, speaker);
        RememberInboundBattleParticipant(resolvedParticipantId, speaker);

        if (GameState.Instance is not null && !GameState.Instance.ShouldReceiveBattleText(resolvedParticipantId, speaker))
            return;

        if (!battleCommunicationExpanded)
            battleCommunicationUnreadWhileCollapsed = true;
        AppendBattleCommunicationMessage(string.IsNullOrWhiteSpace(speaker) ? "队友" : speaker.Trim(), message.Trim(), new Color(0.86f, 0.96f, 1f));
    }

    public void ReceiveBattleVoiceSignal(string participantId, string speaker)
    {
        var resolvedParticipantId = ResolveInboundBattleParticipantId(participantId, speaker);
        RememberInboundBattleParticipant(resolvedParticipantId, speaker);

        if (GameState.Instance is not null && !GameState.Instance.ShouldReceiveBattleVoice(resolvedParticipantId, speaker))
            return;

        var resolvedSpeaker = string.IsNullOrWhiteSpace(speaker) ? "队友" : speaker.Trim();
        var wasSameRemoteVoiceActive = IsRemoteBattleVoiceActive()
            && string.Equals(activeBattleVoiceParticipantId, resolvedParticipantId, StringComparison.Ordinal)
            && string.Equals(activeBattleVoiceSpeakerName, resolvedSpeaker, StringComparison.Ordinal);
        selectedBattleParticipantId = string.IsNullOrWhiteSpace(resolvedParticipantId) ? selectedBattleParticipantId : resolvedParticipantId;
        activeBattleVoiceParticipantId = string.IsNullOrWhiteSpace(resolvedParticipantId) ? selectedBattleParticipantId : resolvedParticipantId;
        activeBattleVoiceSpeakerName = resolvedSpeaker;
        battleCommunicationVoiceRemaining = 0.9f;
        if (!battleCommunicationExpanded)
            battleCommunicationUnreadWhileCollapsed = true;
        if (!wasSameRemoteVoiceActive)
        {
            AppendBattleCommunicationMessage("语音", $"{resolvedSpeaker} 正在通话", new Color(0.72f, 0.92f, 1f, 0.96f));
        }
        else
        {
            RefreshBattleCommunicationHeader();
        RefreshBattleCommunicationComposerState();
        }
    }

    string ResolveInboundBattleParticipantId(string participantId, string speaker)
    {
        if (!string.IsNullOrWhiteSpace(participantId))
            return participantId.Trim();

        if (!string.IsNullOrWhiteSpace(speaker))
        {
            var byName = settingsParticipants.FirstOrDefault(profile =>
                string.Equals(profile.DisplayName, speaker.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
                return byName.ParticipantId;
        }

        var teammate = settingsParticipants.FirstOrDefault(CanControlBattleParticipant);
        return teammate?.ParticipantId
            ?? (string.IsNullOrWhiteSpace(speaker) ? "" : speaker.Trim());
    }

    void RememberInboundBattleParticipant(string participantId, string speaker)
    {
        if (string.IsNullOrWhiteSpace(speaker))
            return;

        var normalizedSpeaker = speaker.Trim();
        if (!string.IsNullOrWhiteSpace(participantId))
        {
            selectedBattleParticipantId = participantId.Trim();
            if (!participantId.StartsWith("room-waiting", StringComparison.Ordinal) && participantId != "local-ai")
                lastKnownPeerId = participantId.Trim();
        }

        if (!string.Equals(normalizedSpeaker, "队友", StringComparison.Ordinal) &&
            !string.Equals(normalizedSpeaker, "等待队友", StringComparison.Ordinal))
            lastKnownPeerName = normalizedSpeaker;

        if (!string.IsNullOrWhiteSpace(lastKnownPeerId) || !string.IsNullOrWhiteSpace(lastKnownPeerName))
            EnsureBattleCommunicationParticipants();
    }

    void RefreshUpgradeRequirementList(BattleBuildingRequirementStatus[] requirements)
    {
        FreeChildNodes(upgradeRequirementList);

        if (requirements.Length == 0)
        {
            var label = HudLabel("无需额外条件", 15, new Color(0.78f, 0.93f, 0.82f));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.CustomMinimumSize = new Vector2(460, 30);
            upgradeRequirementList.AddChild(label);
            return;
        }

        foreach (var requirement in requirements)
        {
            var row = new HBoxContainer
            {
                Name = $"UpgradeRequirementRow_{requirement.DisplayName}",
                CustomMinimumSize = new Vector2(460, 32)
            };
            row.AddThemeConstantOverride("separation", 10);

            var icon = HudLabel(requirement.IsMet ? "✓" : "×", 22, requirement.IsMet
                ? new Color(0.54f, 1f, 0.58f)
                : new Color(1f, 0.46f, 0.38f));
            icon.CustomMinimumSize = new Vector2(24, 30);
            row.AddChild(icon);

            var name = HudLabel(requirement.DisplayName, 16, new Color(0.92f, 0.95f, 0.98f));
            name.CustomMinimumSize = new Vector2(220, 30);
            row.AddChild(name);

            var progressText = requirement.RequiredLevel > 1
                ? $"等级 {requirement.HighestLevel}/{requirement.RequiredLevel}"
                : $"数量 {requirement.CurrentCount}/{requirement.RequiredCount}";
            var progress = HudLabel(progressText, 15, requirement.IsMet
                ? new Color(0.72f, 1f, 0.76f)
                : new Color(1f, 0.58f, 0.48f));
            progress.HorizontalAlignment = HorizontalAlignment.Right;
            progress.CustomMinimumSize = new Vector2(180, 30);
            row.AddChild(progress);

            upgradeRequirementList.AddChild(row);
        }
    }

    void AddRequirementStatusSummary(IEnumerable<BattleBuildingRequirementStatus> requirements)
    {
        var requirementArray = requirements.ToArray();
        var rowHeight = requirementArray.Length == 0 ? 62f : 38f + requirementArray.Length * 26f;
        var card = Panel(Vector2.Zero, new Vector2(356f, rowHeight), new Color(0.030f, 0.044f, 0.050f, 0.96f));
        card.CustomMinimumSize = new Vector2(356f, rowHeight);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;

        var stripe = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(356f, 3f),
            Color = new Color(0.96f, 0.80f, 0.36f, 0.92f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var title = HudLabel("前置条件", 13, new Color(1f, 0.88f, 0.58f));
        title.Position = new Vector2(10f, 8f);
        title.Size = new Vector2(160f, 18f);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(title);

        if (requirementArray.Length == 0)
        {
            var label = HudLabel("当前升级没有额外前置条件。", 12, new Color(0.74f, 1f, 0.78f));
            label.Position = new Vector2(12f, 28f);
            label.Size = new Vector2(332f, 24f);
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.AddChild(label);
            commandList.AddChild(card);
            return;
        }

        var body = new VBoxContainer
        {
            Position = new Vector2(10f, 28f),
            Size = new Vector2(336f, rowHeight - 34f),
            CustomMinimumSize = new Vector2(336f, rowHeight - 34f)
        };
        body.AddThemeConstantOverride("separation", 4);
        card.AddChild(body);

        foreach (var requirement in requirementArray)
        {
            var row = new HBoxContainer
            {
                Name = $"RequirementSummaryRow_{requirement.DisplayName}",
                CustomMinimumSize = new Vector2(334f, 24f)
            };
            row.AddThemeConstantOverride("separation", 8);
            body.AddChild(row);

            var icon = HudLabel(requirement.IsMet ? "✓" : "×", 18, requirement.IsMet
                ? new Color(0.54f, 1f, 0.58f)
                : new Color(1f, 0.46f, 0.38f));
            icon.CustomMinimumSize = new Vector2(20f, 20f);
            icon.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(icon);

            var name = HudLabel(requirement.DisplayName, 12, new Color(0.92f, 0.95f, 0.98f));
            name.CustomMinimumSize = new Vector2(174f, 22f);
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(name);

            var progressText = requirement.RequiredLevel > 1
                ? $"等级 {requirement.HighestLevel}/{requirement.RequiredLevel}"
                : $"数量 {requirement.CurrentCount}/{requirement.RequiredCount}";
            var progress = HudLabel(progressText, 12, requirement.IsMet
                ? new Color(0.72f, 1f, 0.76f)
                : new Color(1f, 0.58f, 0.48f));
            progress.HorizontalAlignment = HorizontalAlignment.Right;
            progress.CustomMinimumSize = new Vector2(118f, 22f);
            progress.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(progress);
        }

        commandList.AddChild(card);
    }

    static string FormatJoinedNames(IEnumerable<string> values, string emptyText)
    {
        var array = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (array.Length == 0)
            return emptyText;

        if (array.Length <= 2)
            return string.Join("、", array);

        return $"{array[0]}、{array[1]} 等{array.Length}项";
    }

    Control CreateSummaryTile(
        string title,
        string value,
        string note,
        Color accent,
        Vector2 size,
        int titleFontSize = 12,
        int valueFontSize = 15,
        int noteFontSize = 11)
    {
        var card = Panel(Vector2.Zero, size, new Color(0.030f, 0.044f, 0.050f, 0.96f));
        card.CustomMinimumSize = size;
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.ClipContents = true;

        var stripe = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(size.X, 3f),
            Color = accent,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var titleLabel = HudLabel(title, titleFontSize, accent);
        titleLabel.Position = new Vector2(10f, 7f);
        titleLabel.Size = new Vector2(size.X - 20f, 18f);
        titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(titleLabel);

        var noteLineCount = string.IsNullOrEmpty(note)
            ? 0
            : note.Split('\n').Length;
        var singleLineNoteHeight = size.Y >= 88f ? 28f : 18f;
        var maxNoteHeight = Mathf.Max(singleLineNoteHeight, size.Y - 49f);
        var noteHeight = noteLineCount switch
        {
            <= 0 => 0f,
            1 => singleLineNoteHeight,
            _ => Mathf.Min(maxNoteHeight, noteLineCount * (noteFontSize + 4f) + 4f)
        };

        var valueLabel = HudLabel(value, valueFontSize, new Color(0.95f, 0.97f, 0.98f));
        valueLabel.Position = new Vector2(10f, 23f);
        valueLabel.Size = new Vector2(size.X - 20f, Mathf.Max(20f, size.Y - 35f - noteHeight));
        valueLabel.VerticalAlignment = VerticalAlignment.Top;
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        valueLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(valueLabel);

        if (!string.IsNullOrEmpty(note))
        {
            var noteLabel = HudLabel(note, noteFontSize, new Color(0.74f, 0.88f, 0.92f));
            noteLabel.Position = new Vector2(10f, size.Y - noteHeight - 6f);
            noteLabel.Size = new Vector2(size.X - 20f, noteHeight);
            noteLabel.VerticalAlignment = VerticalAlignment.Top;
            noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            noteLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.AddChild(noteLabel);
        }

        return card;
    }

    void AddBuildingStatusSummary(RtsBuilding building)
    {
        var statusText = building.RequiresPower()
            ? building.Powered ? "供电正常" : "电力不足"
            : "常驻运作";
        var statusColor = building.RequiresPower() && !building.Powered
            ? new Color(1f, 0.58f, 0.44f)
            : new Color(0.74f, 1f, 0.78f);
        var queueText = string.IsNullOrEmpty(building.CurrentProduction)
            ? "生产空闲"
            : $"队列 {building.QueueCount} 项";

        var row = new GridContainer { Columns = 3 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        row.AddChild(CreateSummaryTile(
            "建筑状态",
            statusText,
            $"耐久 {Mathf.RoundToInt(building.Health)}/{Mathf.RoundToInt(building.MaxHealth)}",
            statusColor,
            new Vector2(114f, 72f),
            12,
            13,
            10));
        row.AddChild(CreateSummaryTile(
            "建筑等级",
            $"Lv.{Math.Max(1, building.BuildingLevel)}",
            building.BuildKey == "barracks" ? "步兵训练" : "作战设施",
            new Color(1f, 0.86f, 0.42f),
            new Vector2(114f, 72f),
            12,
            14,
            10));
        row.AddChild(CreateSummaryTile(
            "生产状态",
            queueText,
            building.CanSetRallyPoint() ? "可设置集结点" : "无集结点",
            new Color(0.72f, 0.92f, 1f),
            new Vector2(114f, 72f),
            12,
            13,
            10));
    }

    void AddBuildingTechSummary(RtsBuilding building)
    {
        var techs = BattleTechCatalog.GetForBuilding(building.BuildKey);
        if (techs.Count == 0)
            return;

        commandList.AddChild(HudLabel("科技", 14, new Color(1f, 0.88f, 0.58f)));
        var row = new HBoxContainer
        {
            Name = "BuildingTechSummaryRow",
            CustomMinimumSize = new Vector2(356f, 84f)
        };
        row.AddThemeConstantOverride("separation", 6);
        commandList.AddChild(row);

        foreach (var tech in techs.Take(2))
        {
            var remain = BattleGameManager.Instance?.GetBattleTechCooldownRemaining(tech.Key) ?? 0f;
            row.AddChild(CreateSummaryTile(
                tech.DisplayName,
                remain > 0f ? $"冷却 {Mathf.CeilToInt(remain)}s" : "可施放",
                $"范围友军目标获得状态，持续 {tech.Duration:0}s，半径 {tech.Radius:0}",
                tech.Tint,
                new Vector2(174f, 82f),
                12,
                13,
                10));
        }
    }

    Control CreateProductionCard(RtsBuilding building, BattleUnitDefinition def)
    {
        const float cardWidth = 110f;
        const float cardHeight = 96f;
        var disabledByPower = building.RequiresPower() && !building.Powered;
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(cardWidth, cardHeight),
            Size = new Vector2(cardWidth, cardHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ClipContents = true
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(0.88f, 0.70f, 0.28f, 0.76f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });

        var background = new TextureRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(cardWidth, cardHeight),
            Texture = LoadHudTexture(ProductionCardBackgroundPath(def.Key)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Modulate = new Color(1f, 1f, 1f, 0.76f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(background);

        card.AddChild(new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(cardWidth, cardHeight),
            Color = new Color(0.05f, 0.08f, 0.10f, 0.34f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        card.AddChild(new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(cardWidth, 3f),
            Color = ProductionAccentColor(def),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        var iconFrame = new Panel
        {
            Position = new Vector2(8f, 8f),
            Size = new Vector2(28f, 28f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.08f, 0.10f, 0.72f),
            BorderColor = new Color(0.92f, 0.80f, 0.46f, 0.72f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });
        card.AddChild(iconFrame);

        iconFrame.AddChild(new TextureRect
        {
            Position = new Vector2(4f, 4f),
            Size = new Vector2(20f, 20f),
            Texture = LoadHudTexture(ProductionCardIconPath(def.Key)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        var name = HudLabel(def.DisplayName, 13, new Color(0.95f, 0.97f, 0.98f));
        name.Position = new Vector2(40f, 9f);
        name.Size = new Vector2(cardWidth - 46f, 20f);
        name.AutowrapMode = TextServer.AutowrapMode.Off;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        name.AddThemeConstantOverride("outline_size", 2);
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(name);

        var cost = HudLabel($"${def.GoldCost}", 12, new Color(1f, 0.86f, 0.42f));
        cost.Position = new Vector2(8f, 42f);
        cost.Size = new Vector2(48f, 18f);
        cost.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
        cost.AddThemeConstantOverride("outline_size", 2);
        cost.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(cost);

        var pop = HudLabel($"人口{def.PopCost}", 11, new Color(0.76f, 0.88f, 0.90f));
        pop.Position = new Vector2(58f, 42f);
        pop.Size = new Vector2(44f, 18f);
        pop.HorizontalAlignment = HorizontalAlignment.Right;
        pop.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.84f));
        pop.AddThemeConstantOverride("outline_size", 2);
        pop.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(pop);

        var meta = HudLabel($"耗时 {building.ProductionDuration(def.Key):0.#}s", 10, new Color(0.78f, 0.90f, 0.94f));
        meta.Position = new Vector2(8f, 61f);
        meta.Size = new Vector2(cardWidth - 16f, 14f);
        meta.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        meta.AddThemeConstantOverride("outline_size", 2);
        meta.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(meta);

        var hint = HudLabel(ProductionHint(def), 10, new Color(0.84f, 0.90f, 0.94f));
        hint.Position = new Vector2(8f, 76f);
        hint.Size = new Vector2(cardWidth - 16f, 14f);
        hint.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        hint.AddThemeConstantOverride("outline_size", 2);
        hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(hint);

        var button = new Button
        {
            Text = "",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Flat = true,
            Disabled = disabledByPower,
            TooltipText = disabledByPower ? $"{building.DisplayName} 电力不足，恢复供电后可生产 {def.DisplayName}" : ""
        };
        button.Pressed += () => QueueUnit(def.Key);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.84f, 0.32f, 0.08f),
            BorderColor = new Color(1f, 0.84f, 0.32f, 0.70f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });
        button.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.74f, 0.22f, 0.12f),
            BorderColor = new Color(1f, 0.84f, 0.32f, 0.78f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        });
        card.AddChild(button);

        if (disabledByPower)
        {
            card.AddChild(new ColorRect
            {
                Position = Vector2.Zero,
                Size = new Vector2(cardWidth, cardHeight),
                Color = new Color(0.04f, 0.06f, 0.08f, 0.42f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });

            var disabledLabel = HudLabel("断电", 12, new Color(1f, 0.74f, 0.52f));
            disabledLabel.Position = new Vector2(8f, 38f);
            disabledLabel.Size = new Vector2(cardWidth - 16f, 18f);
            disabledLabel.HorizontalAlignment = HorizontalAlignment.Center;
            disabledLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
            disabledLabel.AddThemeConstantOverride("outline_size", 2);
            disabledLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.AddChild(disabledLabel);
        }

        return card;
    }

    Control CreateProductionQueueStrip(RtsBuilding building)
    {
        var queue = building.GetProductionQueueSnapshot();
        var section = new VBoxContainer
        {
            Name = "ProductionQueueSection",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        section.AddThemeConstantOverride("separation", 4);

        var summaryText = queue.Length == 0
            ? "当前没有排队单位"
            : $"当前生产：{BattleUnitCatalog.Get(queue[0]).DisplayName} · 队列 {queue.Length} 项";
        var summary = HudLabel(summaryText, 11, new Color(0.80f, 0.92f, 0.98f));
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        section.AddChild(summary);

        var grid = new GridContainer
        {
            Name = "ProductionQueueGrid",
            Columns = 4,
            CustomMinimumSize = new Vector2(316f, 116f), // 56 * 2 + 4
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        grid.GuiInput += ConsumeHudPointerInput;
        section.AddChild(grid);

        const int maxQueueSlots = 8;
        for (var i = 0; i < maxQueueSlots; i++)
        {
            if (i < queue.Length)
            {
                var def = BattleUnitCatalog.Get(queue[i]);
                var isActive = i == 0;
                var detail = isActive
                    ? $"剩余 {building.ProductionTimeLeft:0.0}s"
                    : $"排队 {i + 1}";
                grid.AddChild(CreateProductionQueueEntry(
                    def.DisplayName,
                    detail,
                    ProductionAccentColor(def),
                    ProductionCardIconPath(def.Key),
                    isActive));
            }
            else
            {
                // 空置的槽位
                grid.AddChild(CreateProductionQueueEntry(
                    "空闲",
                    "等待生产",
                    new Color(0.24f, 0.34f, 0.38f, 0.22f),
                    null,
                    false));
            }
        }

        return section;
    }

    Control CreateProductionQueueEntry(string title, string detail, Color accent, string? iconPath, bool active)
    {
        const float cardWidth = 76f;
        const float cardHeight = 56f;

        var card = new Panel
        {
            Name = "ProductionQueueEntry_" + title,
            CustomMinimumSize = new Vector2(cardWidth, cardHeight),
            Size = new Vector2(cardWidth, cardHeight),
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = active
                ? new Color(0.08f, 0.12f, 0.14f, 0.96f)
                : new Color(0.06f, 0.09f, 0.10f, 0.90f),
            BorderColor = new Color(accent.R, accent.G, accent.B, active ? 0.88f : 0.68f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        });

        card.AddChild(new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(cardWidth, 3f),
            Color = new Color(accent.R, accent.G, accent.B, active ? 0.98f : 0.84f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        if (!string.IsNullOrEmpty(iconPath))
        {
            var iconFrame = new Panel
            {
                Position = new Vector2(6f, 7f),
                Size = new Vector2(18f, 18f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            iconFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.05f, 0.08f, 0.10f, 0.82f),
                BorderColor = new Color(accent.R, accent.G, accent.B, 0.72f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3
            });
            card.AddChild(iconFrame);

            iconFrame.AddChild(new TextureRect
            {
                Position = new Vector2(2f, 2f),
                Size = new Vector2(14f, 14f),
                Texture = LoadHudTexture(iconPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }

        var titleLabel = HudLabel(title, 10, new Color(0.96f, 0.97f, 0.98f));
        titleLabel.Position = new Vector2(iconPath is null ? 6f : 28f, 7f);
        titleLabel.Size = new Vector2(iconPath is null ? 64f : 42f, 14f);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        titleLabel.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(titleLabel);

        var stateLabel = HudLabel(active ? "生产中" : "排队中", 9, active ? new Color(1f, 0.86f, 0.42f) : new Color(0.76f, 0.88f, 0.92f));
        stateLabel.Position = new Vector2(6f, 27f);
        stateLabel.Size = new Vector2(64f, 12f);
        stateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        stateLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.84f));
        stateLabel.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(stateLabel);

        var detailLabel = HudLabel(detail, 9, new Color(0.84f, 0.90f, 0.94f));
        detailLabel.Position = new Vector2(6f, 39f);
        detailLabel.Size = new Vector2(64f, 12f);
        detailLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        detailLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.84f));
        detailLabel.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(detailLabel);

        return card;
    }

    static string ProductionCardBackgroundPath(string key) => ButtonBackdropRoot + (key switch
    {
        "infantry" => "prod_infantry.png",
        "infantry_artillery" => "prod_infantry_artillery.png",
        "infantry_flamethrower" or "flamethrower" => "prod_infantry_flamethrower.png",
        "light_tank" or "tank" or "medium_tank" or "heavy_tank" => "prod_tank.png",
        "artillery" => "prod_artillery.png",
        "anti_air_gun" => "prod_anti_air.png",
        "scout_plane" => "prod_scout_plane.png",
        "fighter" => "prod_fighter.png",
        "bomber" => "prod_bomber.png",
        "patrol_boat" => "prod_patrol_boat.png",
        "destroyer_ship" => "prod_destroyer_ship.png",
        "transport_ship" => "prod_battleship.png",
        "aircraft_carrier" => "prod_battleship.png",   // 暂复用战舰图
        _ => "prod_infantry.png"
    });

    static string ProductionCardIconPath(string key) => ButtonBackdropRoot + (key switch
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
        "aircraft_carrier" => "prod_battleship_icon.png",   // 暂复用战舰图标
        _ => "prod_infantry_icon.png"
    });

    static Color ProductionAccentColor(BattleUnitDefinition def)
    {
        if (BattleUnitCatalog.IsAirUnit(def.Key))
            return new Color(0.54f, 0.86f, 1f, 0.92f);
        if (BattleUnitCatalog.IsNavalUnit(def.Key))
            return new Color(0.42f, 0.90f, 0.94f, 0.92f);
        if (BattleUnitCatalog.IsInfantryLike(def.Key))
            return new Color(0.72f, 0.88f, 0.42f, 0.92f);
        return new Color(1f, 0.76f, 0.34f, 0.92f);
    }

    static string ProductionHint(BattleUnitDefinition def)
        => BattleUnitCatalog.IsInfantryLike(def.Key)
            ? "步兵编成"
            : BattleUnitCatalog.IsAirUnit(def.Key)
                ? "空中支援"
                : BattleUnitCatalog.IsNavalUnit(def.Key)
                    ? "水面作战"
                    : "地面突击";

    Control CreateMainBaseTechSummaryTile(BattleTechDefinition tech, string cooldownText)
    {
        var size = new Vector2(174f, 112f);
        var card = Panel(Vector2.Zero, size, new Color(0.030f, 0.044f, 0.050f, 0.96f));
        card.CustomMinimumSize = size;
        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.ClipContents = true;

        var stripe = new ColorRect
        {
            Position = Vector2.Zero,
            Size = new Vector2(size.X, 3f),
            Color = tech.Tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var titleLabel = HudLabel(tech.DisplayName, 12, tech.Tint);
        titleLabel.Position = new Vector2(10f, 7f);
        titleLabel.Size = new Vector2(size.X - 20f, 18f);
        titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(titleLabel);

        var effectLabel = HudLabel(BuildTechEffectSummary(tech), 10, new Color(0.95f, 0.97f, 0.98f));
        effectLabel.Position = new Vector2(10f, 24f);
        effectLabel.Size = new Vector2(size.X - 20f, 34f);
        effectLabel.VerticalAlignment = VerticalAlignment.Top;
        effectLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        effectLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(effectLabel);

        var metaLabel = HudLabel(BuildTechDurationRadiusSummary(tech), 10, new Color(0.74f, 0.88f, 0.92f));
        metaLabel.Position = new Vector2(10f, 59f);
        metaLabel.Size = new Vector2(size.X - 20f, 14f);
        metaLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(metaLabel);

        var statusLabel = HudLabel(BuildMainBaseTechStatusText(cooldownText), 11, new Color(0.92f, 0.95f, 0.98f));
        statusLabel.Position = new Vector2(10f, 77f);
        statusLabel.Size = new Vector2(size.X - 20f, 26f);
        statusLabel.VerticalAlignment = VerticalAlignment.Top;
        statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        statusLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(statusLabel);

        return card;
    }

    static string BuildTechSummary(BattleTechDefinition tech)
    {
        var statText = string.Join("，", BuildTechEffectParts(tech, true, true));
        if (string.IsNullOrEmpty(statText))
            statText = "提供范围支援";
        return $"{statText}，{BuildTechDurationRadiusSummary(tech)}";
    }

    static string BuildTechEffectSummary(BattleTechDefinition tech, bool includeMoveEffects = true, bool includeAttackEffects = true)
    {
        var parts = BuildTechEffectParts(tech, includeMoveEffects, includeAttackEffects);
        if (parts.Count == 0)
            return "提供范围支援";

        var lines = new List<string>();
        for (var i = 0; i < parts.Count; i += 2)
            lines.Add(string.Join("，", parts.Skip(i).Take(2)));
        return string.Join("\n", lines);
    }

    static string BuildTechDurationRadiusSummary(BattleTechDefinition tech)
        => $"持续 {tech.Duration:0}s，半径 {tech.Radius:0}";

    static string BuildMainBaseTechStatusText(string cooldownText)
        => cooldownText == "待释放"
            ? "主基地挂载中，可随时释放"
            : $"主基地挂载中，{cooldownText}";

    static List<string> BuildTechEffectParts(BattleTechDefinition tech, bool includeMoveEffects = true, bool includeAttackEffects = true)
    {
        var parts = new List<string>();
        if (includeMoveEffects && !Mathf.IsEqualApprox(tech.MoveMultiplier, 1f))
            parts.Add($"部队移速 {(tech.MoveMultiplier - 1f) * 100f:+0;-0}%");
        if (includeAttackEffects && !Mathf.IsEqualApprox(tech.DamageMultiplier, 1f))
            parts.Add($"武装目标火力 {(tech.DamageMultiplier - 1f) * 100f:+0;-0}%");
        if (includeAttackEffects && !Mathf.IsZeroApprox(tech.AttackRangeBonus))
            parts.Add($"武装目标射程 +{tech.AttackRangeBonus:0.#}");
        if (includeAttackEffects && !Mathf.IsEqualApprox(tech.AttackCooldownMultiplier, 1f))
            parts.Add($"武装目标攻速 {(1f - tech.AttackCooldownMultiplier) * 100f:+0;-0}%");
        if (!Mathf.IsZeroApprox(tech.DefenseReduction))
            parts.Add($"范围减伤 {tech.DefenseReduction * 100f:0}%");
        if (!Mathf.IsZeroApprox(tech.VisionBonus))
            parts.Add($"范围视野 +{tech.VisionBonus:0.#}");
        if (!Mathf.IsZeroApprox(tech.RegenPerSecond))
            parts.Add($"范围维修 +{tech.RegenPerSecond:0.#}/s");
        return parts;
    }

    static bool IsGlobalConquestMode()
    {
        var selectedMode = GameState.Instance?.SelectedMode ?? "";
        var selectedMap = GameState.Instance?.SelectedMapName ?? "";
        return string.Equals(selectedMode, "全球争霸", StringComparison.Ordinal)
            || string.Equals(selectedMap, BattleMapCatalog.GlobalConquestName, StringComparison.Ordinal);
    }

    void AddTextureFrame(Control parent, string path, Color modulate)
    {
        var frame = new TextureRect
        {
            Name = "UnityFrame",
            LayoutMode = 1,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            Texture = LoadHudTexture(path),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SelfModulate = modulate
        };
        parent.AddChild(frame);
        parent.MoveChild(frame, 0);
    }

    void AddCommandPanelChrome(Control parent)
    {
        var outline = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(0.76f, 0.62f, 0.22f, 0.82f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2
        };
        parent.AddThemeStyleboxOverride("panel", outline);

        var topGlow = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(parent.Size.X, 2f),
            Color = new Color(0.86f, 0.68f, 0.18f, 0.60f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(topGlow);

        var innerFrame = new Panel
        {
            Position = new Vector2(10f, 38f),
            Size = new Vector2(394f, 276f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        innerFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color(0.32f, 0.26f, 0.10f, 0.62f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 1,
            CornerRadiusTopRight = 1,
            CornerRadiusBottomLeft = 1,
            CornerRadiusBottomRight = 1
        });
        parent.AddChild(innerFrame);
        parent.MoveChild(innerFrame, 0);
        parent.MoveChild(topGlow, 0);
    }

    Control CreateCommanderHeader(Control parent)
    {
        var holder = new Control
        {
            Name = "CommanderHeader",
            CustomMinimumSize = new Vector2(166f, 54f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(holder);

        var frame = new TextureRect
        {
            Name = "AvatarFrame",
            Position = new Vector2(0f, 0f),
            Size = new Vector2(52f, 52f),
            Texture = LoadHudTexture(UnityLobbyGenRoot + "gen_portrait_ring.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        PortraitFrameUtils.ApplyWrapFrame(
            frame,
            new Color(0.86f, 0.66f, 0.20f, 0.98f),
            new Color(1f, 0.96f, 0.80f, 0.92f),
            0.43f,
            0.015f,
            0.0035f,
            0.010f);
        holder.AddChild(frame);

        commanderAvatarTexture = new TextureRect
        {
            Name = "Avatar",
            Position = new Vector2(6f, 6f),
            Size = new Vector2(40f, 40f),
            Texture = LoadHudTexture(LocalCommanderAvatarTexturePath()),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White
        };
        PortraitFrameUtils.ApplyCircularPortrait(
            commanderAvatarTexture,
            new Color(0.94f, 0.74f, 0.24f, 1f),
            new Color(1f, 0.95f, 0.78f, 1f),
            0.47f,
            0.0055f,
            0.012f,
            new Vector2(1.24f, 1.24f),
            new Vector2(0.01f, -0.025f));
        holder.AddChild(commanderAvatarTexture);

        commanderRankBadgeTexture = new TextureRect
        {
            Name = "RankBadge",
            Position = new Vector2(56f, 2f),
            Size = new Vector2(44f, 44f),
            Texture = LoadHudTexture(RankBadgeTexturePath(GameState.Instance?.RankTitle)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        holder.AddChild(commanderRankBadgeTexture);

        commanderRankLabel = HudLabel(NormalizeRankTitle(GameState.Instance?.RankTitle), 14, new Color(1f, 0.90f, 0.55f));
        commanderRankLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        commanderRankLabel.Position = new Vector2(106f, 12f);
        commanderRankLabel.Size = new Vector2(58f, 18f);
        holder.AddChild(commanderRankLabel);

        return holder;
    }

    void RefreshCommanderHeaderVisuals()
    {
        if (commanderAvatarTexture is null || commanderRankBadgeTexture is null || commanderRankLabel is null)
            return;

        commanderAvatarTexture.Texture = LoadHudTexture(LocalCommanderAvatarTexturePath());
        commanderRankBadgeTexture.Texture = LoadHudTexture(RankBadgeTexturePath(GameState.Instance?.RankTitle));
        commanderRankLabel.Text = NormalizeRankTitle(GameState.Instance?.RankTitle);
    }

    static string LocalCommanderAvatarTexturePath()
        => string.IsNullOrWhiteSpace(GameState.Instance?.SelectedAvatarPath)
            ? UnityLobbyGenRoot + "WW2Portraits/officer_avatar_01_field_commander.png"
            : GameState.Instance!.SelectedAvatarPath;

    static string ResolveParticipantAvatarTexturePath(GameState.BattleParticipantProfile participant)
    {
        if (participant.IsLocalPlayer)
            return LocalCommanderAvatarTexturePath();

        if (participant.ParticipantId == "local-ai")
            return UnityLobbyGenRoot + "gen_avatar_friend_b.png";

        var variants = new[]
        {
            UnityLobbyGenRoot + "WW2Portraits/officer_avatar_02_tank_commander.png",
            UnityLobbyGenRoot + "WW2Portraits/officer_avatar_03_air_wing.png",
            UnityLobbyGenRoot + "WW2Portraits/officer_avatar_04_naval_command.png",
            UnityLobbyGenRoot + "gen_avatar_friend_a.png"
        };
        return variants[StableParticipantVisualIndex(participant.ParticipantId, variants.Length)];
    }

    static string ResolveParticipantRankBadgeTexturePath(GameState.BattleParticipantProfile participant)
    {
        if (participant.IsLocalPlayer)
            return RankBadgeTexturePath(GameState.Instance?.RankTitle);
        if (participant.ParticipantId == "local-ai")
            return RankBadgeTexturePath("少校");
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return UnityBattleHudRoot + "battle_rank_badge_placeholder.png";
        return RankBadgeTexturePath(participant.Role);
    }

    static string ParticipantRankTitle(GameState.BattleParticipantProfile participant)
    {
        if (participant.IsLocalPlayer)
            return RankBadgeDisplayTitle(GameState.Instance?.RankTitle);
        if (participant.ParticipantId == "local-ai")
            return "少校";
        if (participant.ParticipantId.StartsWith("room-waiting", StringComparison.Ordinal))
            return "待命";
        return RankBadgeDisplayTitle(participant.Role);
    }

    static string RankBadgeTexturePath(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        var badge = "rank_badge_01_bronze_shield.png";
        if (ContainsAny(normalized, "元帅", "王者", "将", "marshal"))
            badge = "rank_badge_05_marshal_eagle.png";
        else if (ContainsAny(normalized, "上校", "少校", "major", "diamond", "钻石"))
            badge = "rank_badge_04_major_crossed_sabers.png";
        else if (ContainsAny(normalized, "黄金", "中尉", "wing", "gold"))
            badge = "rank_badge_03_gold_wing_star.png";
        else if (ContainsAny(normalized, "白银", "少尉", "silver"))
            badge = "rank_badge_02_silver_double_star.png";
        return UnityLobbyGenRoot + "WW2RankBadges/" + badge;
    }

    static string RankBadgeDisplayTitle(string? rankTitle)
    {
        var normalized = NormalizeRankTitle(rankTitle);
        if (ContainsAny(normalized, "元帅"))
            return "元帅";
        if (ContainsAny(normalized, "王者", "将", "marshal"))
            return "将官";
        if (ContainsAny(normalized, "上校"))
            return "上校";
        if (ContainsAny(normalized, "少校", "major", "diamond", "钻石"))
            return "少校";
        if (ContainsAny(normalized, "中尉", "wing", "gold", "黄金"))
            return "中尉";
        if (ContainsAny(normalized, "少尉", "silver", "白银"))
            return "少尉";
        return "列兵";
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

    static int StableParticipantVisualIndex(string participantId, int count)
    {
        if (count <= 0)
            return 0;

        var checksum = 0;
        foreach (var ch in participantId ?? "")
            checksum += ch;
        return Mathf.Abs(checksum) % count;
    }

    void OpenSettingsDialog()
    {
        if (settingsDialogRoot is null)
            return;

        settingsDialogRoot.Visible = true;
        EnsureBattleCommunicationParticipants();
        RefreshBattleCommunicationState();
        settingsDialogRoot.MoveToFront();
    }

    void ToggleSettingsDialog()
    {
        if (settingsDialogRoot is null)
            return;

        if (!settingsDialogRoot.Visible)
        {
            OpenSettingsDialog();
        }
        else
        {
            settingsDialogRoot.Visible = false;
            CloseBattleReportDialog();
        }
    }

    static string ShortRoomId(string value)
        => value.Length <= 6 ? value : value[..6];

    void AddBottomCommandBar(Control root)
    {
        const float width = 338f;
        const float height = 62f;
        bottomCommandBar = Panel(new Vector2((HudWidth - width) * 0.5f, HudHeight - height - 8f), new Vector2(width, height), new Color(0.015f, 0.020f, 0.020f, 0.82f));
        root.AddChild(bottomCommandBar);
        AddTextureFrame(bottomCommandBar, UnityFrameRoot + "bottombar_frame.png", new Color(1f, 1f, 1f, 0.52f));

        selectAllCommandButton = AddCommandIconButton(bottomCommandBar, 8f, "全选", "全", new Color(0.16f, 0.34f, 0.44f, 0.96f), SelectAllOwnedUnits);
        stopCommandButton = AddCommandIconButton(bottomCommandBar, 74f, "停止", "停", new Color(0.40f, 0.18f, 0.14f, 0.96f), StopSelectedUnits);
        patrolCommandButton = AddCommandIconButton(bottomCommandBar, 140f, "巡逻", "巡", new Color(0.20f, 0.34f, 0.20f, 0.96f), TogglePatrolCommand);
        specialCommandButton = AddCommandIconButton(bottomCommandBar, 206f, "炮击", "炮", new Color(0.42f, 0.18f, 0.10f, 0.96f), TriggerSpecialCommand);
        parkAircraftCommandButton = AddCommandIconButton(bottomCommandBar, 272f, "停机", "停", new Color(0.16f, 0.38f, 0.36f, 0.96f), ParkSelectedAircraft);
        gameplayHudItems.Add(bottomCommandBar);
    }

    Button AddCommandIconButton(Control parent, float x, string caption, string glyph, Color bg, System.Action onPressed)
    {
        var button = new Button
        {
            Text = $"{glyph}\n{caption}",
            Position = new Vector2(x, 6f),
            Size = new Vector2(58f, 50f),
            TooltipText = caption
        };
        ApplyButtonStyle(button, bg, new Color(1f, 0.76f, 0.32f, 0.78f), 12);
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }

    void RefreshBottomCommandBar()
    {
        if (bottomCommandBar is null)
            return;

        var controller = FindPlayerController();
        var selectedUnits = GetSelectedOwnedUnits();
        var hasUnits = selectedUnits.Count > 0;
        var hasAttackGroundUnits = selectedUnits.Any(unit => unit.CanAttackGroundPoint);
        var hasBombers = selectedUnits.Any(unit => unit.SupportsBombingRun);
        var hasParkableAircraft = selectedUnits.Any(unit => unit.SupportsAirfieldParking && !unit.IsParkedAtAirfield && !unit.IsReturningToPark);

        ConfigureBottomCommandButton(selectAllCommandButton, "全选", "全", new Color(0.16f, 0.34f, 0.44f, 0.96f), true, false);
        ConfigureBottomCommandButton(stopCommandButton, "停止", "停", new Color(0.40f, 0.18f, 0.14f, 0.96f), hasUnits, false);
        ConfigureBottomCommandButton(
            patrolCommandButton,
            "巡逻",
            "巡",
            new Color(0.20f, 0.34f, 0.20f, 0.96f),
            hasUnits,
            controller?.IsPatrolModeActive == true);

        var specialCaption = hasBombers ? "轰炸" : "炮击";
        var specialGlyph = hasBombers ? "轰" : "炮";
        var specialEnabled = hasBombers || hasAttackGroundUnits;
        var specialActive = controller?.IsBombingRunModeActive == true || controller?.IsAttackGroundModeActive == true;
        ConfigureBottomCommandButton(
            specialCommandButton,
            specialCaption,
            specialGlyph,
            hasBombers ? new Color(0.48f, 0.22f, 0.10f, 0.96f) : new Color(0.42f, 0.18f, 0.10f, 0.96f),
            specialEnabled,
            specialActive);

        ConfigureBottomCommandButton(
            parkAircraftCommandButton,
            "停机",
            "停",
            new Color(0.16f, 0.38f, 0.36f, 0.96f),
            hasParkableAircraft,
            false);
    }

    void RefreshInputHint()
    {
        if (inputHintLabel is null || inputHintPanel is null)
            return;

        var manager = BattleGameManager.Instance;
        var controller = FindPlayerController();
        var color = new Color(0.82f, 0.92f, 0.94f, 0.94f);
        string? text = null;

        if (!string.IsNullOrEmpty(activeTechTargetKey))
        {
            text = "科技释放中：点击地面确认范围，右键取消。";
            color = new Color(0.72f, 1f, 0.82f);
        }
        else if (!string.IsNullOrEmpty(manager?.PendingBuildKey))
        {
            var err = controller?.BuildPlacementError;
            if (!string.IsNullOrWhiteSpace(err))
            {
                text = $"不能放置：{err}！\n(点击地面放置 {BattleBuildingCatalog.Get(manager.PendingBuildKey).DisplayName}，右键取消)";
                color = new Color(1f, 0.38f, 0.28f);
            }
            else
            {
                text = $"建造模式：点击地面放置 {BattleBuildingCatalog.Get(manager.PendingBuildKey).DisplayName}，右键取消。";
                color = new Color(1f, 0.86f, 0.46f);
            }
        }
        else if (controller?.IsBombingRunAwaitingDirection == true)
        {
            text = "区域轰炸：已锁定起点，再点终点确定轰炸方向。";
            color = new Color(1f, 0.72f, 0.42f);
        }
        else if (controller?.IsBombingRunModeActive == true)
        {
            text = "区域轰炸：先点击起点，再点击终点；右键取消。";
            color = new Color(1f, 0.72f, 0.42f);
        }
        else if (controller?.IsAttackGroundModeActive == true)
        {
            text = "炮击命令：点击地面指定持续攻击点；右键取消。";
            color = new Color(1f, 0.72f, 0.42f);
        }
        else if (controller?.IsPatrolModeActive == true)
        {
            text = "巡逻命令：点击地面设置巡逻终点；右键取消。";
            color = new Color(0.76f, 1f, 0.78f);
        }
        else if (buildMenuOpen)
        {
            text = "建造面板：选择建筑后点击地图落点；资源不足的按钮会置灰。";
            color = new Color(0.86f, 0.92f, 0.94f, 0.90f);
        }
        else if (techMenuOpen)
        {
            text = "科技面板：选择科技后点击战场位置释放，只有当时框内单位获得临时状态。";
            color = new Color(0.80f, 0.92f, 1f, 0.92f);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            inputHintPanel.Visible = false;
            inputHintLabel.Text = "";
            return;
        }

        inputHintPanel.Visible = true;
        inputHintLabel.Text = text;
        inputHintLabel.AddThemeColorOverride("font_color", color);
    }

    void ConfigureBottomCommandButton(Button button, string caption, string glyph, Color bg, bool enabled, bool active)
    {
        if (button is null)
            return;

        button.Text = $"{glyph}\n{caption}";
        button.TooltipText = caption;
        button.Disabled = !enabled;
        button.Modulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.55f);
        var fill = active ? bg.Lightened(0.18f) : enabled ? bg : bg.Darkened(0.42f);
        var accent = active
            ? new Color(0.44f, 0.98f, 0.86f, 0.92f)
            : enabled
                ? new Color(1f, 0.76f, 0.32f, 0.78f)
                : new Color(0.52f, 0.56f, 0.60f, 0.36f);
        ApplyButtonStyle(button, fill, accent, 12);
    }

    Button AddLeftActionButton(Control root, string label, string glyph, float bottom, Color bg, System.Action onPressed)
    {
        var button = new Button
        {
            Text = $"{glyph}  {label}",
            Position = new Vector2(BattleHudMinimapLeft, HudHeight - bottom - BattleHudActionButtonHeight),
            Size = new Vector2(BattleHudActionButtonWidth, BattleHudActionButtonHeight),
            TooltipText = label
        };
        ApplyButtonStyle(button, bg, new Color(1f, 0.84f, 0.30f, 0.82f), 15);
        button.Pressed += onPressed;
        root.AddChild(button);
        gameplayHudItems.Add(button);
        return button;
    }

    void StyleCommandListControls(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Button button:
                    if (!button.HasMeta("preserve_command_style"))
                        ApplyButtonStyle(button, new Color(0.07f, 0.10f, 0.10f, 0.96f), new Color(0.95f, 0.72f, 0.28f, 0.72f), 12);
                    ConstrainCommandButton(button);
                    break;
                case Label label:
                    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
                    label.AddThemeConstantOverride("outline_size", 2);
                    ConstrainCommandFlowLabel(label);
                    break;
            }

            StyleCommandListControls(child);
        }
    }

    void SelectAllOwnedUnits()
    {
        var units = BattleGameManager.Instance?.GetUnits(true)
            .Where(unit => GodotObject.IsInstanceValid(unit) && !unit.IsDead)
            .Cast<Node>()
            .ToArray() ?? System.Array.Empty<Node>();
        GameState.Instance?.SetSelection(units);
        ShowAlert(units.Length == 0 ? "当前没有可选单位" : $"已选择全部单位：{units.Length}");
    }

    void StopSelectedUnits()
    {
        var units = GameState.Instance?.Selected
            .OfType<RtsUnit>()
            .Where(unit => GodotObject.IsInstanceValid(unit) && unit.PlayerOwned && !unit.IsDead)
            .ToArray() ?? System.Array.Empty<RtsUnit>();

        foreach (var unit in units)
        {
            unit.Stop();
            GameRelay.Instance?.SendStop(unit.NetId);
        }
        ShowAlert(units.Length == 0 ? "没有选中可停止的单位" : "已下达停止命令");
    }

    void TogglePatrolCommand()
    {
        var controller = FindPlayerController();
        if (controller is null)
            return;

        string message;
        if (controller.IsPatrolModeActive)
        {
            controller.CancelCommandMode(false);
            message = "已取消巡逻命令";
        }
        else
        {
            controller.BeginPatrolMode(out message);
        }

        ShowAlert(message);
        RefreshBottomCommandBar();
    }

    void TriggerSpecialCommand()
    {
        var controller = FindPlayerController();
        if (controller is null)
            return;

        var selectedUnits = GetSelectedOwnedUnits();
        var hasBombers = selectedUnits.Any(unit => unit.SupportsBombingRun);
        var hasAttackGroundUnits = selectedUnits.Any(unit => unit.CanAttackGroundPoint);
        if (!hasBombers && !hasAttackGroundUnits)
        {
            ShowAlert("请选择可执行炮击或轰炸的单位");
            return;
        }

        string message;
        if (hasBombers)
        {
            if (controller.IsBombingRunModeActive)
            {
                controller.CancelCommandMode(false);
                message = "已取消区域轰炸";
            }
            else
            {
                controller.BeginBombingRunMode(out message);
            }
        }
        else
        {
            if (controller.IsAttackGroundModeActive)
            {
                controller.CancelCommandMode(false);
                message = "已取消炮击命令";
            }
            else
            {
                controller.BeginAttackGroundMode(out message);
            }
        }

        ShowAlert(message);
        RefreshBottomCommandBar();
    }

    void ParkSelectedAircraft()
    {
        var controller = FindPlayerController();
        if (controller is null)
            return;

        controller.ParkSelectedAircraft(out var message);
        ShowAlert(message);
        RefreshBottomCommandBar();
    }

    PlayerController? FindPlayerController()
        => GetParent()?.GetNodeOrNull<PlayerController>("PlayerController");

    List<RtsUnit> GetSelectedOwnedUnits()
    {
        return GameState.Instance?.Selected
            .OfType<RtsUnit>()
            .Where(unit => GodotObject.IsInstanceValid(unit) && unit.PlayerOwned && !unit.IsDead)
            .ToList() ?? new List<RtsUnit>();
    }

    static void ConstrainCommandButton(Button button)
    {
        button.ClipText = true;
        button.Alignment = HorizontalAlignment.Center;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    static void ConstrainCommandFlowLabel(Label label)
    {
        if (label.GetParent() is not VBoxContainer)
            return;

        label.CustomMinimumSize = new Vector2(CommandPanelContentWidth, 0f);
        label.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        label.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    static void ApplyButtonStyle(Button button, Color bg, Color accent, int fontSize)
    {
        button.ClipText = true;
        button.Alignment = HorizontalAlignment.Center;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        var palette = new MetalUiStyle.MetalPalette(
            bg,
            accent,
            new Color(1f, 0.95f, 0.78f, 0.94f),
            new Color(0.04f, 0.04f, 0.03f, 0.92f),
            new Color(accent.R, accent.G, accent.B, 0.18f));
        MetalUiStyle.ApplyMetalButton(button, palette, fontSize, true);
    }

    static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
        => new()
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            ContentMarginLeft = 8,
            ContentMarginTop = 5,
            ContentMarginRight = 8,
            ContentMarginBottom = 5
        };

    static Texture2D LoadHudTexture(string resourcePath)
    {
        if (ResourceLoader.Exists(resourcePath))
        {
            var texture = GD.Load<Texture2D>(resourcePath);
            if (texture is not null)
                return texture;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is not null && !image.IsEmpty())
            return ImageTexture.CreateFromImage(image);

        GD.PushError($"Failed to load HUD image: {resourcePath}");
        var fallback = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        fallback.Fill(new Color(1f, 0f, 1f, 1f));
        return ImageTexture.CreateFromImage(fallback);
    }

    static Texture2D LoadTrimmedHudTexture(string resourcePath, int padding)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is null || image.IsEmpty())
            return LoadHudTexture(resourcePath);

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

    static Panel TransparentPanel(Vector2 position, Vector2 size)
    {
        var panel = new Panel
        {
            Position = position,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        return panel;
    }

    static Panel Panel(Vector2 position, Vector2 size, Color color)
    {
        var panel = new Panel
        {
            Position = position,
            Size = size
        };
        MetalUiStyle.ApplyMetalPanel(panel, new MetalUiStyle.MetalPalette(
            color,
            new Color(0.80f, 0.65f, 0.36f, 0.75f),
            new Color(1f, 0.92f, 0.74f, 0.55f),
            new Color(0.02f, 0.02f, 0.02f, 0.92f),
            new Color(0.95f, 0.76f, 0.26f, 0.10f)),
            1,
            8,
            4);
        return panel;
    }

    static Label HudLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    static Label CreateReportCell(float width, float height, HorizontalAlignment alignment, int fontSize, Color color, bool bold)
    {
        var label = HudLabel("", fontSize, color);
        label.CustomMinimumSize = new Vector2(width, height);
        label.Size = new Vector2(width, height);
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        label.AddThemeConstantOverride("outline_size", bold ? 2 : 1);
        if (bold)
            label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    void BuildObjectivePanel(Control root)
    {
        // 1. Create Task Icon Button (always visible on HUD, click to open pop-up)
        objectiveRestoreBtn = new Button
        {
            Name = "ObjectiveIconBtn",
            Position = new Vector2(12f, 64f),
            Size = new Vector2(36f, 36f),
            Visible = true,
            Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/menuList.png"),
            ExpandIcon = true,
            FocusMode = Control.FocusModeEnum.None
        };
        MetalUiStyle.ApplyMetalButton(objectiveRestoreBtn, MetalUiStyle.Steel, 16, false);
        objectiveRestoreBtn.Pressed += OpenObjectiveDialog;
        root.AddChild(objectiveRestoreBtn);
        gameplayHudItems.Add(objectiveRestoreBtn);

        // Dummy objects to prevent null references elsewhere in existing code structure
        objectivePanel = new Panel { Visible = false };
        objectiveList = new VBoxContainer();

        RefreshObjectives();

        BuildObjectiveDialog(root);
    }

    void ToggleObjectivePanel(bool show)
    {
        objectivePanel.Visible = show;
        objectiveRestoreBtn.Visible = !show;
    }

    void RefreshObjectives()
    {
        if (objectiveList is null || !GodotObject.IsInstanceValid(objectiveList))
            return;

        FreeChildNodes(objectiveList);
        if (objectiveDialogList is not null && GodotObject.IsInstanceValid(objectiveDialogList))
            FreeChildNodes(objectiveDialogList);

        var mode = GameState.Instance?.SelectedMode ?? "";
        var mapName = GameState.Instance?.SelectedMapName ?? "";

        var enemyBases = BattleGameManager.Instance?.GetMainBases(false) ?? System.Array.Empty<RtsBuilding>();
        var enemyBasesDestroyed = enemyBases.Count == 0;

        var myBuildings = BattleGameManager.Instance?.GetBuildings(true) ?? System.Array.Empty<RtsBuilding>();
        var hasPowerPlant = myBuildings.Any(b => b.BuildKey == "power_plant");
        var hasRefinery = myBuildings.Any(b => b.BuildKey == "gold_mine");
        var hasBarracks = myBuildings.Any(b => b.BuildKey == "barracks");
        var hasTurrets = myBuildings.Any(b => b.BuildKey == "turret");

        var researchedTechCount = BattleGameManager.Instance?.ResearchedTechs?.Count ?? 0;

        if (mode.Contains("战役 - 基础行动指令") || mode.Contains("Level 1") || mapName == "沙漠绿洲")
        {
            AddObjectiveRow($"{mode}_task1", "1. 框选并移动你的战斗单位", true, "100 金币", 100);
            AddObjectiveRow($"{mode}_task2", "2. 击毁中央的敌军哨所及基地", enemyBasesDestroyed, "300 金币", 300);
        }
        else if (mode.Contains("战役 - 基地展开与采矿") || mode.Contains("Level 2") || mapName == "丛林战场")
        {
            AddObjectiveRow($"{mode}_task1", "1. 建造发电厂以获取电力", hasPowerPlant, "150 金币", 150);
            AddObjectiveRow($"{mode}_task2", "2. 展开采矿场以收集资金", hasRefinery, "150 金币", 150);
            AddObjectiveRow($"{mode}_task3", "3. 训练步兵并消灭敌方基地", enemyBasesDestroyed, "400 金币", 400);
        }
        else if (mode.Contains("战役 - 坦克风暴克制协同") || mode.Contains("Level 3") || mapName == "冰雪要塞")
        {
            AddObjectiveRow($"{mode}_task1", "1. 训练克制兵种协同作战", hasBarracks, "200 金币", 200);
            AddObjectiveRow($"{mode}_task2", "2. 摧毁敌方核心雷达站及基地", enemyBasesDestroyed, "500 金币", 500);
        }
        else if (mode.Contains("战役 - 要塞死守防御战") || mode.Contains("Level 4") || mapName == "城市废墟")
        {
            AddObjectiveRow($"{mode}_task1", "1. 建造机枪碉堡构筑防御线", hasTurrets, "300 金币", 300);
            AddObjectiveRow($"{mode}_task2", "2. 抵御进攻并消灭所有敌军基地", enemyBasesDestroyed, "600 金币", 600);
        }
        else if (mode.Contains("战役 - 终极模拟演习") || mode.Contains("Level 5") || mapName == "全球争霸")
        {
            AddObjectiveRow($"{mode}_task1", "1. 研发全线科技解锁终极单位", researchedTechCount > 0, "400 金币", 400);
            AddObjectiveRow($"{mode}_task2", "2. 摧毁敌方所有防线和AI基地", enemyBasesDestroyed, "1000 金币", 1000);
        }
        else
        {
            AddObjectiveRow($"{mode}_task1", "1. 发展基地生产战斗单位", myBuildings.Count > 1, "200 金币", 200);
            AddObjectiveRow($"{mode}_task2", "2. 彻底摧毁所有敌对阵营基地", enemyBasesDestroyed, "500 金币", 500);
        }
    }

    void AddObjectiveRow(string key, string text, bool completed, string reward = "", int goldReward = 0)
    {
        // 1. Add to small HUD panel (limit to first 2 tasks to avoid overflow)
        if (objectiveList.GetChildCount() < 2)
        {
            var rowHUD = CreateObjectiveRowWidget(key, text, completed, reward, goldReward, false);
            objectiveList.AddChild(rowHUD);
        }

        // 2. Add to large popup dialog (show all tasks)
        if (objectiveDialogList is not null && GodotObject.IsInstanceValid(objectiveDialogList))
        {
            var rowDialog = CreateObjectiveRowWidget(key, text, completed, reward, goldReward, true);
            objectiveDialogList.AddChild(rowDialog);
        }
    }

    Control CreateObjectiveRowWidget(string key, string text, bool completed, string reward, int goldReward, bool isDialog)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        // 1. Status Indicator / Claim Button
        if (completed)
        {
            if (claimedObjectiveKeys.Contains(key))
            {
                var statusIcon = new TextureRect
                {
                    Texture = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/checkmark.png"),
                    CustomMinimumSize = new Vector2(isDialog ? 18f : 14f, isDialog ? 18f : 14f),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    SelfModulate = new Color(0.2f, 0.85f, 0.3f), // Bright green
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
                };
                row.AddChild(statusIcon);
            }
            else
            {
                var claimBtn = new Button
                {
                    Name = "ClaimBtn_" + key,
                    Text = "领取",
                    CustomMinimumSize = new Vector2(isDialog ? 56f : 40f, isDialog ? 26f : 22f),
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                    FocusMode = Control.FocusModeEnum.None
                };
                MetalUiStyle.ApplyMetalButton(claimBtn, MetalUiStyle.Steel, isDialog ? 12 : 10, false);
                claimBtn.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));

                claimBtn.Pressed += () =>
                {
                    claimedObjectiveKeys.Add(key);
                    if (BattleGameManager.Instance is not null)
                    {
                        BattleGameManager.Instance.AddPlayerGold(goldReward);
                    }
                    ShowAlert($"【战场任务】完成！获得 {goldReward} 金币奖励！");
                    RefreshObjectives();
                };
                row.AddChild(claimBtn);
            }
        }
        else
        {
            var statusIcon = new Panel
            {
                CustomMinimumSize = new Vector2(isDialog ? 18f : 14f, isDialog ? 18f : 14f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
            };
            var outline = new StyleBoxFlat
            {
                BgColor = Colors.Transparent,
                BorderColor = new Color(0.6f, 0.7f, 0.8f, 0.6f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3
            };
            statusIcon.AddThemeStyleboxOverride("panel", outline);
            row.AddChild(statusIcon);
        }

        // 2. Text description
        var isClaimed = completed && claimedObjectiveKeys.Contains(key);
        var textColor = isClaimed ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.92f, 0.94f, 0.96f);
        var descLabel = HudLabel(text, isDialog ? 14 : 12, textColor);
        descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLabel.ClipText = false;
        row.AddChild(descLabel);

        // 3. Reward Display with gold coin icon
        if (!string.IsNullOrEmpty(reward))
        {
            var rewardContainer = new HBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
                Alignment = BoxContainer.AlignmentMode.End
            };
            rewardContainer.AddThemeConstantOverride("separation", 4);

            var coinIcon = new TextureRect
            {
                Texture = LoadHudTexture("res://assets/unity_migrated/Assets/Resources/UI/CurrencyIcons/currency_gold_coin.png"),
                CustomMinimumSize = new Vector2(isDialog ? 18f : 14f, isDialog ? 18f : 14f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered
            };
            if (isClaimed)
            {
                coinIcon.SelfModulate = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
            rewardContainer.AddChild(coinIcon);

            string cleanReward = reward.Replace("金币", "").Trim();
            var rewardLabel = HudLabel(
                isClaimed ? $"{cleanReward} (已领)" : cleanReward,
                isDialog ? 13 : 11,
                isClaimed ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.95f, 0.76f, 0.24f)
            );
            rewardContainer.AddChild(rewardLabel);

            row.AddChild(rewardContainer);
        }

        return row;
    }

    void BuildObjectiveDialog(Control root)
    {
        objectiveDialogRoot = new Control
        {
            Name = "ObjectiveDialogRoot",
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        root.AddChild(objectiveDialogRoot);

        var dim = new ColorRect
        {
            LayoutMode = 3,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        objectiveDialogRoot.AddChild(dim);

        objectiveDialogPanel = Panel(new Vector2(320, 120), new Vector2(640, 480), new Color(0.030f, 0.046f, 0.058f, 0.96f));
        objectiveDialogRoot.AddChild(objectiveDialogPanel);
        AddTextureFrame(objectiveDialogPanel, UnityFrameRoot + "panel_task_frame.png", new Color(1f, 1f, 1f, 0.16f));

        var titleContainer = new HBoxContainer
        {
            Position = new Vector2(20, 18),
            Size = new Vector2(600, 36),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        titleContainer.AddThemeConstantOverride("separation", 8);
        objectiveDialogPanel.AddChild(titleContainer);

        var titleIcon = new TextureRect
        {
            Texture = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/target.png"),
            CustomMinimumSize = new Vector2(24, 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            SelfModulate = new Color(1f, 0.90f, 0.62f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        titleContainer.AddChild(titleIcon);

        var title = HudLabel("战场任务列表", 22, new Color(1f, 0.90f, 0.62f));
        title.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        titleContainer.AddChild(title);

        var subtitle = HudLabel("完成以下作战任务可获得丰厚的奖励", 13, new Color(0.74f, 0.88f, 0.92f));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.Position = new Vector2(20, 52);
        subtitle.Size = new Vector2(600, 22);
        objectiveDialogPanel.AddChild(subtitle);

        var closeButton = new Button
        {
            Name = "CloseButton",
            Position = new Vector2(590, 16),
            Size = new Vector2(34, 32),
            Icon = LoadHudTexture("res://assets/third_party/kenney/game-icons/PNG/White/2x/cross.png"),
            ExpandIcon = true,
            FocusMode = Control.FocusModeEnum.None
        };
        ApplyButtonStyle(closeButton, new Color(0.12f, 0.14f, 0.16f, 0.96f), new Color(0.85f, 0.62f, 0.18f, 0.82f), 18);
        closeButton.Pressed += () => objectiveDialogRoot.Visible = false;
        objectiveDialogPanel.AddChild(closeButton);

        var scroll = new ScrollContainer
        {
            Name = "ObjectiveScroll",
            Position = new Vector2(32, 90),
            Size = new Vector2(576, 350),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        objectiveDialogPanel.AddChild(scroll);

        objectiveDialogList = new VBoxContainer
        {
            Name = "ObjectiveDialogList",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        objectiveDialogList.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(objectiveDialogList);
    }

    void OpenObjectiveDialog()
    {
        if (objectiveDialogRoot is null)
            return;
        objectiveDialogRoot.Visible = true;
        objectiveDialogRoot.MoveToFront();
        RefreshObjectives();
    }

    static string FormatTime(float seconds)
    {
        var total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    static void FreeChildNodes(Node parent)
    {
        foreach (var child in parent.GetChildren().ToArray())
        {
            if (child is Node node && GodotObject.IsInstanceValid(node))
                node.QueueFree();
        }
    }

    void SetGameplayHudVisible(bool visible)
    {
        if (hudRoot is not null && GodotObject.IsInstanceValid(hudRoot))
        {
            foreach (var child in hudRoot.GetChildren())
            {
                if (child == gameOverOverlay || child == upgradeDialogRoot || child == settingsDialogRoot || child == objectiveDialogRoot)
                    continue;
                if (child is CanvasItem canvasItem && GodotObject.IsInstanceValid(canvasItem))
                    canvasItem.Visible = visible;
            }
        }

        foreach (var item in gameplayHudItems)
        {
            if (item is null || !GodotObject.IsInstanceValid(item))
                continue;
            item.Visible = visible;
        }
    }
}
