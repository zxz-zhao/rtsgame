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

    Panel topPanel = null!;
    Control hudRoot = null!;
    Panel commandPanel = null!;
    Label commandPanelTitle = null!;
    Label economyLabel = null!;
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
    VBoxContainer battleCommunicationMessages = null!;
    Label battleCommunicationVoiceLabel = null!;
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
    Button settingsTextMuteButton = null!;
    Button settingsVoiceMuteButton = null!;
    Button settingsMuteAllButton = null!;
    Button settingsReportButton = null!;
    Button settingsReturnLobbyButton = null!;
    Node3D? techTargetRing;
    string activeTechTargetKey = "";
    readonly List<BattleTechUiState> techButtons = new();
    GameState.BattleParticipantProfile[] settingsParticipants = System.Array.Empty<GameState.BattleParticipantProfile>();
    string selectedBattleParticipantId = "";
    string lastKnownPeerId = "";
    string lastKnownPeerName = "";

    RtsBuilding? selectedBuilding;
    RtsUnit? selectedRepairUnit;
    RtsBuilding? pendingUpgradeBuilding;
    bool buildMenuOpen;
    bool techMenuOpen;
    float gameOverCountdownRemaining;
    bool gameOverCountdownActive;
    bool gameOverTransitionTriggered;
    float battleCommunicationVoiceRemaining;
    ulong nextCommandPanelLiveRefreshMs;

    public override void _Ready()
    {
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
        RefreshBottomCommandBar();
        RefreshInputHint();
        UpdateTechTargetingVisual();
        UpdateTechCooldownVisuals();
        UpdateBattleCommunicationPanel((float)delta);
    }

    void BuildTopPanel(Control root)
    {
        topPanel = TransparentPanel(Vector2.Zero, new Vector2(HudWidth, BattleHudTopBarHeight));
        topPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(topPanel);

        var avatarGroup = CreateCommanderHeader(topPanel);
        avatarGroup.Position = new Vector2(12f, 2f);

        economyLabel = HudLabel("", 17, new Color(0.82f, 0.96f, 1f));
        economyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        economyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        economyLabel.Position = new Vector2(210, 14);
        economyLabel.Size = new Vector2(640, 30);
        economyLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        economyLabel.AddThemeConstantOverride("outline_size", 3);
        topPanel.AddChild(economyLabel);

        selectionLabel = HudLabel("当前选择：未选择单位", 15, new Color(0.94f, 0.96f, 0.88f));
        selectionLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        selectionLabel.HorizontalAlignment = HorizontalAlignment.Right;
        selectionLabel.Position = new Vector2(850, 14);
        selectionLabel.Size = new Vector2(300, 30);
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
    }

    void BuildCommandPanel(Control root)
    {
        AddBottomCommandBar(root);
        AddLeftActionButton(root, "建造", "建", BattleHudBuildButtonBottom, new Color(0.20f, 0.34f, 0.14f, 0.96f), () =>
        {
            buildMenuOpen = true;
            techMenuOpen = false;
            GameState.Instance?.SetSelection(System.Array.Empty<Node>());
            ShowAlert("打开建造面板");
            RefreshCommandPanel();
        });
        AddLeftActionButton(root, "科技", "技", BattleHudTechButtonBottom, new Color(0.16f, 0.30f, 0.54f, 0.96f), () =>
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

        commandPanel = Panel(new Vector2(852, 156), new Vector2(414, 300), new Color(0.035f, 0.045f, 0.025f, 0.92f));
        root.AddChild(commandPanel);
        AddCommandPanelChrome(commandPanel);

        commandPanelTitle = HudLabel("作战指令", 18, new Color(1f, 0.88f, 0.58f));
        commandPanelTitle.HorizontalAlignment = HorizontalAlignment.Center;
        commandPanelTitle.Position = new Vector2(18, 8);
        commandPanelTitle.Size = new Vector2(378, 28);
        commandPanel.AddChild(commandPanelTitle);

        var scroll = new ScrollContainer
        {
            Position = new Vector2(18, 40),
            Size = new Vector2(378, 240),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        commandPanel.AddChild(scroll);

        commandList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(356, 0)
        };
        commandList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(commandList);
        commandPanel.Visible = false;
        gameplayHudItems.Add(commandPanel);
    }

    void BuildInputHintPanel(Control root)
    {
        inputHintPanel = Panel(new Vector2(294, 606), new Vector2(692, 34), new Color(0.020f, 0.035f, 0.038f, 0.78f));
        inputHintPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(inputHintPanel);

        inputHintLabel = HudLabel("", 13, new Color(0.78f, 0.92f, 0.94f));
        inputHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
        inputHintLabel.VerticalAlignment = VerticalAlignment.Center;
        inputHintLabel.Position = new Vector2(12, 4);
        inputHintLabel.Size = new Vector2(668, 25);
        inputHintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        inputHintLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        inputHintLabel.AddThemeConstantOverride("outline_size", 2);
        inputHintPanel.AddChild(inputHintLabel);

        gameplayHudItems.Add(inputHintPanel);
        RefreshInputHint();
    }

    void BuildBattleCommunicationPanel(Control root)
    {
        battleCommunicationPanel = Panel(new Vector2(12, 474), new Vector2(274, 170), new Color(0.018f, 0.028f, 0.034f, 0.76f));
        root.AddChild(battleCommunicationPanel);
        AddTextureFrame(battleCommunicationPanel, UnityFrameRoot + "panel_task_frame.png", new Color(1f, 1f, 1f, 0.16f));

        var title = HudLabel("战地通讯", 14, new Color(1f, 0.88f, 0.58f));
        title.Position = new Vector2(12, 8);
        title.Size = new Vector2(120, 20);
        battleCommunicationPanel.AddChild(title);

        battleCommunicationVoiceLabel = HudLabel("", 12, new Color(0.70f, 0.94f, 1f, 0.94f));
        battleCommunicationVoiceLabel.HorizontalAlignment = HorizontalAlignment.Right;
        battleCommunicationVoiceLabel.Position = new Vector2(114, 8);
        battleCommunicationVoiceLabel.Size = new Vector2(148, 20);
        battleCommunicationPanel.AddChild(battleCommunicationVoiceLabel);

        var body = new ScrollContainer
        {
            Position = new Vector2(10, 34),
            Size = new Vector2(254, 124),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        battleCommunicationPanel.AddChild(body);

        battleCommunicationMessages = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(244, 0)
        };
        battleCommunicationMessages.AddThemeConstantOverride("separation", 4);
        body.AddChild(battleCommunicationMessages);
        gameplayHudItems.Add(battleCommunicationPanel);
    }

    void BindDeferred()
    {
        if (BattleGameManager.Instance is not null)
            BindManager(BattleGameManager.Instance);
    }

    void BindManager(BattleGameManager manager)
    {
        manager.EconomyChanged += RefreshEconomy;
        manager.GameEnded += ShowGameOver;
        RefreshEconomy();
    }

    void OnBattleCommunicationChanged()
    {
        RefreshBattleCommunicationState();
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
        lastKnownPeerId = "";
        lastKnownPeerName = "";
        EnsureBattleCommunicationParticipants();
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

        RefreshBattleCommunicationHeader();
        RefreshSettingsParticipantList();
        RefreshSettingsParticipantDetails();
    }

    void RefreshBattleCommunicationHeader()
    {
        if (battleCommunicationVoiceLabel is null)
            return;

        if (battleCommunicationVoiceRemaining > 0f)
        {
            var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
            var speaker = participant?.DisplayName ?? "队友";
            battleCommunicationVoiceLabel.Text = $"{speaker} 语音中";
        }
        else if (settingsParticipants.Length > 1)
        {
            battleCommunicationVoiceLabel.Text = $"成员 {Math.Max(0, settingsParticipants.Length - 1)}";
        }
        else
        {
            battleCommunicationVoiceLabel.Text = "离线演练";
        }
    }

    void UpdateBattleCommunicationPanel(float delta)
    {
        if (battleCommunicationVoiceRemaining > 0f)
        {
            battleCommunicationVoiceRemaining = Mathf.Max(0f, battleCommunicationVoiceRemaining - delta);
            if (battleCommunicationVoiceRemaining <= 0f)
                RefreshBattleCommunicationHeader();
        }
    }

    void OnSelectionChanged(Godot.Collections.Array<Node> selection)
    {
        selectedBuilding = selection.OfType<RtsBuilding>().FirstOrDefault(b => b.PlayerOwned);
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
            economyLabel.Text = "经济系统初始化中...";
            return;
        }

        var pending = string.IsNullOrEmpty(manager.PendingBuildKey)
            ? ""
            : $"   建造：{BattleBuildingCatalog.Get(manager.PendingBuildKey).DisplayName}";
        var power = manager.PlayerPowerOnline
            ? $"电力 {manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}"
            : $"电力不足 {manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}";
        economyLabel.Text = $"金币 {manager.PlayerGold}   人口 {manager.PlayerPopUsed}/{manager.PlayerPopCap}   {power}   时间 {FormatTime(manager.GameTime)}{pending}";
    }

    void RefreshCommandPanel()
    {
        if (commandList is null)
            return;

        foreach (var child in commandList.GetChildren())
            child.QueueFree();

        var manager = BattleGameManager.Instance;
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
                commandPanelTitle.Text = "战场科技";
                AddBattleTechMenu(manager, selectedBuilding);
            }
            else
            {
                commandPanelTitle.Text = "作战指令";
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
            AddBuildMenu(BattleGameManager.Instance);
            return;
        }
        if (building.RequiresPower() && !building.Powered)
        {
            commandList.AddChild(HudLabel($"{building.DisplayName} 电力不足：生产、收入或防御暂停", 14, new Color(1f, 0.62f, 0.36f)));
            AddBuildMenu(BattleGameManager.Instance);
            return;
        }

        if (building.Health < building.MaxHealth - 1f)
            AddRepairButton(building);

        AddBuildingUpgradeButton(building);
        AddTechMenu(building);

        if (roster.Length == 0)
        {
            commandList.AddChild(HudLabel($"{building.DisplayName}：不可生产单位", 14, new Color(0.78f, 0.86f, 0.88f)));
            AddBuildMenu(BattleGameManager.Instance);
            return;
        }

        var row = new GridContainer { Columns = 3 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);
        foreach (var unitKey in roster)
        {
            var def = BattleUnitCatalog.Get(unitKey);
            var button = new Button
            {
                Text = $"{def.DisplayName}\n${def.GoldCost} 人口{def.PopCost}",
                CustomMinimumSize = new Vector2(116, 54)
            };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.Pressed += () => QueueUnit(def.Key);
            row.AddChild(button);
        }

        var queueText = string.IsNullOrEmpty(building.CurrentProduction)
            ? "当前队列：空"
            : $"当前队列：{BattleUnitCatalog.Get(building.CurrentProduction).DisplayName}，剩余 {building.ProductionTimeLeft:0.0}s，队列 {building.QueueCount}";
        commandList.AddChild(HudLabel(queueText, 13, new Color(0.84f, 0.91f, 0.94f)));
        commandList.AddChild(HudLabel("点选目标攻击，点选地面移动。需要建造时点左侧建造。", 12, new Color(0.65f, 0.76f, 0.78f)));
    }

    void AddMainBaseInfoMenu(RtsBuilding building)
    {
        var manager = BattleGameManager.Instance;
        AddMainBaseOverviewSummary(building);

        if (building.IsRuined)
        {
            commandList.AddChild(HudLabel("状态：主基地已被摧毁，当前处于废墟状态。", 13, new Color(1f, 0.62f, 0.42f)));
            AddMainBaseRebuildMenu(building);
            return;
        }

        if (building.IsRebuilding)
        {
            commandList.AddChild(HudLabel($"状态：重建中 {building.RebuildProgress:P0}，剩余 {building.RebuildTimeLeft:0.0}s", 13, new Color(1f, 0.84f, 0.48f)));
            commandList.AddChild(HudLabel("重建完成后会恢复主基地功能，并重新允许建造与升级。", 12, new Color(0.78f, 0.88f, 0.92f)));
            return;
        }

        if (building.UnderConstruction)
        {
            commandList.AddChild(HudLabel($"状态：建造中 {building.ConstructionProgress:P0}，剩余 {building.ConstructionTimeLeft:0.0}s", 13, new Color(1f, 0.84f, 0.48f)));
            commandList.AddChild(HudLabel("主基地建成后才能继续扩张和解锁后续等级。", 12, new Color(0.78f, 0.88f, 0.92f)));
            return;
        }

        if (manager is null)
        {
            commandList.AddChild(HudLabel("战斗管理器尚未就绪，升级与科技信息稍后刷新。", 12, new Color(0.82f, 0.88f, 0.92f)));
            return;
        }

        if (building.Health < building.MaxHealth - 1f)
            AddRepairButton(building);

        if (BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel) &&
            manager.TryGetBuildingUpgradePreview(building, out var nextLevel, out var goldCost, out var requirements, out _))
        {
            AddMainBaseUpgradeSummary(building, nextLevel, goldCost, requirements, manager.CanUpgradeBuilding(building, out _));
        }
        else
        {
            commandList.AddChild(HudLabel("当前主基地已满级。", 13, new Color(0.74f, 1f, 0.78f)));
        }

        AddMainBaseTechSummary(manager);

        if (IsGlobalConquestMode())
            AddGlobalConquestArmamentSummary(manager);
    }

    void AddMainBaseRebuildMenu(RtsBuilding building)
    {
        var manager = BattleGameManager.Instance;
        if (manager is null)
            return;

        var button = new Button
        {
            Text = $"重建主基地  ${manager.MainBaseRebuildCost}",
            CustomMinimumSize = new Vector2(200, 40)
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += () => StartMainBaseRebuild(building);
        commandList.AddChild(button);
    }

    void AddMainBaseOverviewSummary(RtsBuilding building)
    {
        commandList.AddChild(HudLabel("主基地概览", 14, new Color(1f, 0.88f, 0.58f)));
        commandList.AddChild(HudLabel(
            building.Health < building.MaxHealth - 1f
                ? "状态：可维修，升级后会继续提升血量、人口上限和收入。"
                : "状态：运行中，升级后会继续提升血量、人口上限和收入。",
            12,
            building.Health < building.MaxHealth - 1f
                ? new Color(1f, 0.84f, 0.48f)
                : new Color(0.78f, 0.92f, 0.94f)));

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        row.AddChild(CreateSummaryTile(
            "主基地等级",
            $"Lv.{Math.Max(1, building.BuildingLevel)}",
            "核心等级决定可解锁内容",
            new Color(1f, 0.90f, 0.62f),
            new Vector2(174f, 66f),
            12,
            16,
            11));

        row.AddChild(CreateSummaryTile(
            "当前血量",
            $"{Mathf.RoundToInt(building.Health):0}/{Mathf.RoundToInt(building.MaxHealth):0}",
            $"{(building.MaxHealth > 0f ? building.Health / building.MaxHealth : 0f):P0} 耐久",
            new Color(0.72f, 0.92f, 1f),
            new Vector2(174f, 66f),
            12,
            16,
            11));

        row.AddChild(CreateSummaryTile(
            "资源收入",
            $"+{building.GoldIncomeAmount} / {building.GoldIncomeInterval:0.#}s",
            "固定周期自动产出金币",
            new Color(0.90f, 0.78f, 0.38f),
            new Vector2(174f, 66f),
            12,
            15,
            11));

        row.AddChild(CreateSummaryTile(
            "人口上限",
            $"+{building.PopCapBonus}",
            "可容纳更多作战单位",
            new Color(0.74f, 1f, 0.78f),
            new Vector2(174f, 66f),
            12,
            16,
            11));
    }

    void AddMainBaseUpgradeSummary(
        RtsBuilding building,
        int nextLevel,
        int goldCost,
        BattleBuildingRequirementStatus[] requirements,
        bool canUpgrade)
    {
        var upgrade = BattleBuildingUpgradeCatalog.Get(building.BuildKey, nextLevel);
        var baseDef = BattleBuildingCatalog.Get(building.BuildKey);
        var nextMaxHealth = Mathf.RoundToInt(upgrade.MaxHealthOverride ?? baseDef.MaxHealth * upgrade.HealthMultiplier);
        var nextPopCap = upgrade.PopCapBonusOverride ?? (baseDef.PopCapBonus + upgrade.PopCapBonusAdd);
        var nextIncome = upgrade.GoldIncomeOverride ?? Mathf.RoundToInt(baseDef.GoldIncomeAmount * upgrade.IncomeMultiplier);
        var currentHealth = Mathf.RoundToInt(building.MaxHealth);
        var currentPopCap = building.PopCapBonus;
        var currentIncome = building.GoldIncomeAmount;

        commandList.AddChild(HudLabel("升级说明", 14, new Color(1f, 0.88f, 0.58f)));

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        row.AddChild(CreateSummaryTile(
            "升级费用",
            $"${goldCost}",
            canUpgrade ? "条件已满足，点击按钮即可升级" : "先满足前置建筑与等级要求",
            new Color(1f, 0.84f, 0.48f),
            new Vector2(174f, 74f),
            12,
            18,
            11));

        row.AddChild(CreateSummaryTile(
            "升级收益",
            $"Lv.{nextLevel}",
            $"血量 {currentHealth:0} → {nextMaxHealth:0}\n人口 +{currentPopCap} → +{nextPopCap}\n收入 +{currentIncome} → +{nextIncome} / {building.GoldIncomeInterval:0.#}s",
            canUpgrade ? new Color(0.74f, 1f, 0.78f) : new Color(0.96f, 0.78f, 0.56f),
            new Vector2(174f, 92f),
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

    void AddMainBaseTechSummary(BattleGameManager manager)
    {
        commandList.AddChild(HudLabel("科技加成", 14, new Color(1f, 0.88f, 0.58f)));

        var techs = BattleTechCatalog.GetForBuilding("main_base");
        if (techs.Count == 0)
        {
            commandList.AddChild(HudLabel("当前没有可用的主基地科技。", 12, new Color(0.76f, 0.86f, 0.90f)));
            return;
        }

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);

        foreach (var tech in techs)
        {
            var cooldown = manager.GetBattleTechCooldownRemaining(tech.Key);
            var cooldownText = cooldown > 0f ? $"冷却 {Mathf.CeilToInt(cooldown)}s" : "已就绪";
            row.AddChild(CreateSummaryTile(
                tech.DisplayName,
                BuildTechSummary(tech),
                cooldownText,
                tech.Tint,
                new Vector2(174f, 88f),
                12,
                11,
                11));
        }
    }

    void AddGlobalConquestArmamentSummary(BattleGameManager manager)
    {
        commandList.AddChild(HudLabel("全球争霸军备", 14, new Color(1f, 0.88f, 0.58f)));

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
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
            "本局主力",
            currentStarter.DisplayName,
            "全球争霸开局锁定的主力兵种",
            new Color(0.78f, 0.90f, 0.98f),
            new Vector2(174f, 74f),
            12,
            15,
            11));

        row.AddChild(CreateSummaryTile(
            "军备建筑",
            FormatJoinedNames(buildingNames, "仅有主基地"),
            "可生产军备的建筑",
            new Color(0.74f, 0.92f, 0.94f),
            new Vector2(174f, 74f),
            12,
            12,
            11));

        row.AddChild(CreateSummaryTile(
            "已部署军备",
            FormatJoinedNames(ownedUnits, "暂无出战单位"),
            "当前场上单位统计",
            new Color(0.74f, 1f, 0.78f),
            new Vector2(174f, 82f),
            12,
            12,
            11));

        row.AddChild(CreateSummaryTile(
            "可调用军备",
            FormatJoinedNames(producibleUnits, "暂无可调用单位"),
            "由现有军备建筑汇总",
            new Color(1f, 0.86f, 0.42f),
            new Vector2(174f, 82f),
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
            button.TooltipText = tech.Description;
            button.Pressed += () => BeginTechTargeting(tech.Key);
            row.AddChild(button);
        }
    }

    void AddBuildingUpgradeButton(RtsBuilding building)
    {
        if (!building.PlayerOwned || building.UnderConstruction || !BattleBuildingUpgradeCatalog.HasNextLevel(building.BuildKey, building.BuildingLevel))
            return;

        var nextLevel = building.BuildingLevel + 1;
        var button = new Button
        {
            Text = $"升级建筑\nLv.{nextLevel}  ${building.NextUpgradeCost()}",
            CustomMinimumSize = new Vector2(160, 42)
        };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += () => OpenUpgradeDialog(building);
        commandList.AddChild(button);
    }

    void AddBuildMenu(BattleGameManager? manager)
    {
        if (manager is not null && !string.IsNullOrEmpty(manager.PendingBuildKey))
        {
            var pending = BattleBuildingCatalog.Get(manager.PendingBuildKey);
            commandList.AddChild(HudLabel($"放置中：{pending.DisplayName}，点地图落点", 14, new Color(1f, 0.84f, 0.48f)));
            return;
        }

        commandList.AddChild(HudLabel("建造", 14, new Color(1f, 0.88f, 0.58f)));
        foreach (var section in BuildMenuSections())
        {
            var sectionTitle = HudLabel(section.Title, 13, new Color(0.80f, 0.94f, 1f));
            commandList.AddChild(sectionTitle);

            var row = new GridContainer { Columns = 3 };
            row.AddThemeConstantOverride("h_separation", 6);
            row.AddThemeConstantOverride("v_separation", 6);
            commandList.AddChild(row);
            foreach (var def in section.Buildings)
                row.AddChild(CreateBuildCard(def));
        }
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
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(116, 82),
            Size = new Vector2(116, 82),
            MouseFilter = Control.MouseFilterEnum.Stop
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

        var stripe = new ColorRect
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(116f, 3f),
            Color = BuildAccentColor(def),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var icon = new Label
        {
            Text = BuildGlyph(def.Key),
            Position = new Vector2(8f, 8f),
            Size = new Vector2(24f, 24f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        icon.AddThemeFontSizeOverride("font_size", 18);
        icon.AddThemeColorOverride("font_color", new Color(0.98f, 0.92f, 0.78f));
        icon.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        icon.AddThemeConstantOverride("outline_size", 2);
        card.AddChild(icon);

        var name = HudLabel(def.DisplayName, 13, new Color(0.95f, 0.97f, 0.98f));
        name.Position = new Vector2(36f, 8f);
        name.Size = new Vector2(72f, 22f);
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(name);

        var cost = HudLabel($"${def.GoldCost}", 12, new Color(1f, 0.86f, 0.42f));
        cost.Position = new Vector2(8f, 36f);
        cost.Size = new Vector2(48f, 18f);
        cost.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(cost);

        var meta = HudLabel(BuildCardMeta(def), 11, new Color(0.76f, 0.88f, 0.90f));
        meta.Position = new Vector2(56f, 36f);
        meta.Size = new Vector2(52f, 18f);
        meta.HorizontalAlignment = HorizontalAlignment.Right;
        meta.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(meta);

        var hint = HudLabel("点击建造", 11, new Color(0.82f, 0.88f, 0.92f));
        hint.Position = new Vector2(8f, 58f);
        hint.Size = new Vector2(100f, 16f);
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
            TooltipText = BuildButtonText(def)
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

        var row = new GridContainer { Columns = 2 };
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        commandList.AddChild(row);
        foreach (var tech in allTechs)
            row.AddChild(CreateTechCard(manager, tech));

        if (!string.IsNullOrEmpty(activeTechTargetKey))
            commandList.AddChild(HudLabel("左键地面释放，右键取消。", 12, new Color(1f, 0.84f, 0.48f)));
    }

    Control CreateTechCard(BattleGameManager manager, BattleTechDefinition tech)
    {
        var card = new Panel
        {
            CustomMinimumSize = new Vector2(174, 88),
            Size = new Vector2(174, 88),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
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
            Size = new Vector2(174f, 3f),
            Color = tech.Tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var glyph = HudLabel(tech.Glyph, 24, new Color(1f, 0.96f, 0.88f));
        glyph.Position = new Vector2(8f, 6f);
        glyph.Size = new Vector2(30f, 28f);
        glyph.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        glyph.AddThemeConstantOverride("outline_size", 2);
        glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(glyph);

        var title = HudLabel(tech.DisplayName, 15, new Color(1f, 0.90f, 0.62f));
        title.Position = new Vector2(40f, 8f);
        title.Size = new Vector2(126f, 20f);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(title);

        var meta = HudLabel($"半径 {tech.Radius:0}  持续 {tech.Duration:0}s", 11, new Color(0.84f, 0.92f, 0.98f));
        meta.Position = new Vector2(8f, 34f);
        meta.Size = new Vector2(156f, 18f);
        meta.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(meta);

        var desc = HudLabel(tech.Description, 11, new Color(0.80f, 0.90f, 0.94f));
        desc.Position = new Vector2(8f, 52f);
        desc.Size = new Vector2(156f, 28f);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(desc);

        var cooldownFill = new TextureProgressBar
        {
            Position = new Vector2(0f, 0f),
            Size = new Vector2(174f, 88f),
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
        cooldownLabel.Size = new Vector2(174f, 88f);
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
            Flat = true,
            TooltipText = tech.Description
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
        gameOverTitle.Text = playerWon ? "胜利" : "失败";
        gameOverTitle.Modulate = playerWon
            ? new Color(1f, 0.86f, 0.42f)
            : new Color(0.96f, 0.42f, 0.34f);
        gameOverBody.Text = $"{reason}\n\n本局用时：{FormatTime(manager?.GameTime ?? 0f)}";
        RefreshGameOverSummary(playerWon, manager);
        SetGameplayHudVisible(false);
        StartGameOverCountdown(30f);
        gameOverOverlay.Visible = true;
        gameOverOverlay.MoveToFront();
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
        gameOverPanel.AddChild(gameOverResultBadge);
        gameOverResultMark = HudLabel("", 28, new Color(1f, 0.86f, 0.42f));
        gameOverResultMark.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverResultMark.VerticalAlignment = VerticalAlignment.Center;
        gameOverResultMark.Position = new Vector2(0, 5);
        gameOverResultMark.Size = new Vector2(58, 46);
        gameOverResultBadge.AddChild(gameOverResultMark);

        gameOverTitle = HudLabel("", 42, new Color(1f, 0.86f, 0.42f));
        gameOverTitle.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverTitle.Position = new Vector2(140, 36);
        gameOverTitle.Size = new Vector2(440, 58);
        gameOverPanel.AddChild(gameOverTitle);

        gameOverBody = HudLabel("", 18, Colors.White);
        gameOverBody.HorizontalAlignment = HorizontalAlignment.Center;
        gameOverBody.VerticalAlignment = VerticalAlignment.Center;
        gameOverBody.Position = new Vector2(98, 100);
        gameOverBody.Size = new Vector2(524, 62);
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
            gameOverResultMark.Text = playerWon ? "V" : "X";
            gameOverResultMark.Modulate = playerWon
                ? new Color(1f, 0.86f, 0.42f)
                : new Color(0.96f, 0.42f, 0.34f);
        }

        if (manager is null || gameOverSummaryValueLabels.Length < 4)
            return;

        gameOverSummaryValueLabels[0].Text = manager.PlayerGold.ToString("N0");
        gameOverSummaryValueLabels[1].Text = $"{manager.PlayerPowerUsed}/{manager.PlayerPowerProvided}";
        gameOverSummaryValueLabels[2].Text = manager.GetPlayerBaseSummaryText();
        gameOverSummaryValueLabels[3].Text = $"{manager.PlayerUnitKills + manager.PlayerBuildingKills}鏉€  {FormatTime(manager.GameTime)}";
        RefreshGameOverReport(manager.BuildBattleReportData());
    }

    void BuildGameOverReportSection()
    {
        var reportPanel = Panel(new Vector2(58, 254), new Vector2(604, 194), new Color(0.018f, 0.027f, 0.030f, 0.90f));
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
            Size = new Vector2(4, 194),
            Color = new Color(0.42f, 0.94f, 0.76f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        reportPanel.AddChild(accent);

        gameOverReportGrid = new GridContainer
        {
            Columns = 3,
            Position = new Vector2(16, 48),
            Size = new Vector2(572, 136),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        gameOverReportGrid.AddThemeConstantOverride("h_separation", 12);
        gameOverReportGrid.AddThemeConstantOverride("v_separation", 2);
        reportPanel.AddChild(gameOverReportGrid);

        gameOverReportPlayerHeader = CreateReportCell(250, 16, HorizontalAlignment.Left, 12, new Color(0.42f, 0.94f, 0.76f), true);
        gameOverReportCenterHeader = CreateReportCell(72, 16, HorizontalAlignment.Center, 12, new Color(1f, 0.86f, 0.42f), true);
        gameOverReportEnemyHeader = CreateReportCell(250, 16, HorizontalAlignment.Right, 12, new Color(1f, 0.50f, 0.42f), true);
        gameOverReportGrid.AddChild(gameOverReportPlayerHeader);
        gameOverReportGrid.AddChild(gameOverReportCenterHeader);
        gameOverReportGrid.AddChild(gameOverReportEnemyHeader);
    }

    void RefreshGameOverReport(BattleGameManager.BattleReportData report)
    {
        if (gameOverReportGrid is null)
            return;

        gameOverReportMeta.Text = $"{report.TeamLine}    用时 {report.DurationText}";
        gameOverReportPlayerHeader.Text = string.IsNullOrWhiteSpace(report.PlayerHeader) ? "我方" : report.PlayerHeader;
        gameOverReportCenterHeader.Text = "项目";
        gameOverReportEnemyHeader.Text = string.IsNullOrWhiteSpace(report.EnemyHeader) ? "敌方" : report.EnemyHeader;

        while (gameOverReportPlayerValues.Count < report.Rows.Length)
        {
            var player = CreateReportCell(250, 14, HorizontalAlignment.Left, 11, new Color(0.78f, 0.96f, 0.90f), false);
            var metric = CreateReportCell(72, 14, HorizontalAlignment.Center, 11, new Color(0.68f, 0.75f, 0.78f), false);
            var enemy = CreateReportCell(250, 14, HorizontalAlignment.Right, 11, new Color(1f, 0.78f, 0.74f), false);
            gameOverReportPlayerValues.Add(player);
            gameOverReportMetricLabels.Add(metric);
            gameOverReportEnemyValues.Add(enemy);
            gameOverReportGrid.AddChild(player);
            gameOverReportGrid.AddChild(metric);
            gameOverReportGrid.AddChild(enemy);
        }

        for (var i = 0; i < gameOverReportPlayerValues.Count; i++)
        {
            var visible = i < report.Rows.Length;
            gameOverReportPlayerValues[i].Visible = visible;
            gameOverReportMetricLabels[i].Visible = visible;
            gameOverReportEnemyValues[i].Visible = visible;
            if (!visible)
                continue;

            var row = report.Rows[i];
            gameOverReportPlayerValues[i].Text = row.PlayerValue;
            gameOverReportMetricLabels[i].Text = row.Label;
            gameOverReportEnemyValues[i].Text = row.EnemyValue;
        }
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
        GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn");
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
        return minimapPanel is not null
            && minimapPanel.Visible
            && minimapPanel.GetGlobalRect().HasPoint(screenPosition);
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
        settingsParticipantTitle.Size = new Vector2(372, 30);
        detailPanel.AddChild(settingsParticipantTitle);

        settingsParticipantRole = HudLabel("", 13, new Color(1f, 0.86f, 0.48f));
        settingsParticipantRole.Position = new Vector2(18, 52);
        settingsParticipantRole.Size = new Vector2(372, 24);
        detailPanel.AddChild(settingsParticipantRole);

        settingsParticipantReceive = HudLabel("", 13, new Color(0.76f, 0.92f, 1f));
        settingsParticipantReceive.Position = new Vector2(18, 84);
        settingsParticipantReceive.Size = new Vector2(372, 42);
        settingsParticipantReceive.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantReceive);

        var infoTitle = HudLabel("队友介绍", 15, new Color(1f, 0.86f, 0.42f));
        infoTitle.Position = new Vector2(18, 144);
        infoTitle.Size = new Vector2(160, 24);
        detailPanel.AddChild(infoTitle);

        settingsParticipantIntro = HudLabel("", 14, new Color(0.86f, 0.94f, 0.96f));
        settingsParticipantIntro.Position = new Vector2(18, 174);
        settingsParticipantIntro.Size = new Vector2(372, 122);
        settingsParticipantIntro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantIntro);

        settingsParticipantReportHint = HudLabel("", 12, new Color(0.92f, 0.72f, 0.52f));
        settingsParticipantReportHint.Position = new Vector2(18, 320);
        settingsParticipantReportHint.Size = new Vector2(372, 52);
        settingsParticipantReportHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detailPanel.AddChild(settingsParticipantReportHint);

        settingsTextMuteButton = AddSettingsActionButton("屏蔽文字", new Vector2(326, 500), new Vector2(132, 42), new Color(0.18f, 0.30f, 0.42f, 0.96f), ToggleSelectedBattleTextBlock);
        settingsVoiceMuteButton = AddSettingsActionButton("屏蔽语音", new Vector2(470, 500), new Vector2(132, 42), new Color(0.16f, 0.36f, 0.36f, 0.96f), ToggleSelectedBattleVoiceBlock);
        settingsMuteAllButton = AddSettingsActionButton("全部屏蔽", new Vector2(614, 500), new Vector2(122, 42), new Color(0.38f, 0.22f, 0.16f, 0.96f), ToggleSelectedBattleMuteAll);
        settingsReportButton = AddSettingsActionButton("举报", new Vector2(24, 500), new Vector2(126, 42), new Color(0.44f, 0.16f, 0.13f, 0.96f), ReportSelectedBattleParticipant);

        settingsReturnLobbyButton = AddSettingsActionButton("返回大厅", new Vector2(162, 500), new Vector2(132, 42), new Color(0.42f, 0.17f, 0.12f, 0.96f), () => GetTree().ChangeSceneToFile("res://scenes/lobby/LobbyScene.tscn"));
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

    void RefreshSettingsParticipantList()
    {
        if (settingsParticipantList is null)
            return;

        foreach (var child in settingsParticipantList.GetChildren())
            child.QueueFree();

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
            var tag = participant.IsLocalPlayer
                ? "自己"
                : pref.TextBlocked && pref.VoiceBlocked
                    ? "全屏蔽"
                    : pref.TextBlocked
                        ? "禁文"
                        : pref.VoiceBlocked
                            ? "禁语"
                            : "接收中";

            var button = new Button
            {
                Text = $"{participant.DisplayName}    {tag}",
                CustomMinimumSize = new Vector2(238, 42)
            };
            button.AddThemeFontSizeOverride("font_size", 13);
            var isSelected = participant.ParticipantId == selectedBattleParticipantId;
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
            ApplyButtonStyle(button, bg, accent, 13);
            button.Pressed += () =>
            {
                selectedBattleParticipantId = participant.ParticipantId;
                RefreshSettingsParticipantList();
                RefreshSettingsParticipantDetails();
            };
            settingsParticipantList.AddChild(button);
        }
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
            SetSettingsActionButtonsEnabled(false, false, false, false);
            return;
        }

        var pref = GameState.Instance?.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName) ?? default;
        var canControl = CanControlBattleParticipant(participant);
        var canReport = participant.CanReport && !participant.IsLocalPlayer;

        settingsParticipantTitle.Text = participant.IsLocalPlayer ? $"{participant.DisplayName}（自己）" : participant.DisplayName;
        settingsParticipantRole.Text = $"身份：{participant.Role}";
        settingsParticipantReceive.Text = $"文字：{(pref.TextBlocked ? "已屏蔽" : "接收中")}    语音：{(pref.VoiceBlocked ? "已屏蔽" : "接收中")}";
        settingsParticipantIntro.Text = participant.Intro;
        settingsParticipantReportHint.Text = pref.Reported
            ? $"举报状态：已记录。{(string.IsNullOrWhiteSpace(pref.ReportReason) ? "后续可接真实服务端接口提交。" : pref.ReportReason)}"
            : canReport
                ? "可在这里举报该队友。当前先记录到本地运行态，后续可以直接接服务器举报接口。"
                : "当前对象不支持举报。";

        settingsTextMuteButton.Text = pref.TextBlocked ? "接收文字" : "屏蔽文字";
        settingsVoiceMuteButton.Text = pref.VoiceBlocked ? "接收语音" : "屏蔽语音";
        settingsMuteAllButton.Text = pref.TextBlocked && pref.VoiceBlocked ? "取消全屏蔽" : "全部屏蔽";
        settingsReportButton.Text = pref.Reported ? "已举报" : "举报";

        ApplyButtonStyle(settingsTextMuteButton,
            pref.TextBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.18f, 0.30f, 0.42f, 0.96f),
            pref.TextBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.72f, 0.92f, 1f, 0.84f),
            14);
        ApplyButtonStyle(settingsVoiceMuteButton,
            pref.VoiceBlocked ? new Color(0.34f, 0.18f, 0.14f, 0.96f) : new Color(0.16f, 0.36f, 0.36f, 0.96f),
            pref.VoiceBlocked ? new Color(1f, 0.68f, 0.48f, 0.88f) : new Color(0.70f, 0.98f, 0.94f, 0.84f),
            14);
        ApplyButtonStyle(settingsMuteAllButton,
            pref.TextBlocked && pref.VoiceBlocked ? new Color(0.20f, 0.40f, 0.24f, 0.96f) : new Color(0.38f, 0.22f, 0.16f, 0.96f),
            pref.TextBlocked && pref.VoiceBlocked ? new Color(0.74f, 1f, 0.78f, 0.84f) : new Color(1f, 0.80f, 0.50f, 0.84f),
            14);

        SetSettingsActionButtonsEnabled(canControl, canControl, canControl, canReport && !pref.Reported);
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

        var pref = GameState.Instance.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName);
        var blocked = !pref.TextBlocked;
        GameState.Instance.SetBattleTextBlocked(participant.ParticipantId, participant.DisplayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{participant.DisplayName}{(blocked ? " 已屏蔽文字消息" : " 已恢复文字接收")}", new Color(0.72f, 0.92f, 1f));
        ShowAlert(blocked ? "已屏蔽该队友文字消息" : "已恢复该队友文字消息");
    }

    void ToggleSelectedBattleVoiceBlock()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || !CanControlBattleParticipant(participant))
            return;

        var pref = GameState.Instance.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName);
        var blocked = !pref.VoiceBlocked;
        GameState.Instance.SetBattleVoiceBlocked(participant.ParticipantId, participant.DisplayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{participant.DisplayName}{(blocked ? " 已屏蔽语音" : " 已恢复语音接收")}", new Color(0.72f, 1f, 0.94f));
        ShowAlert(blocked ? "已屏蔽该队友语音" : "已恢复该队友语音");
    }

    void ToggleSelectedBattleMuteAll()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || !CanControlBattleParticipant(participant))
            return;

        var pref = GameState.Instance.GetBattleCommunicationPreference(participant.ParticipantId, participant.DisplayName);
        var blocked = !(pref.TextBlocked && pref.VoiceBlocked);
        GameState.Instance.SetBattleMuteAll(participant.ParticipantId, participant.DisplayName, blocked);
        AppendBattleCommunicationMessage("设置", $"{participant.DisplayName}{(blocked ? " 已全部屏蔽" : " 已取消全部屏蔽")}", new Color(1f, 0.84f, 0.58f));
        ShowAlert(blocked ? "已全部屏蔽该队友" : "已取消全部屏蔽");
    }

    void ReportSelectedBattleParticipant()
    {
        var participant = ResolveParticipantProfile(selectedBattleParticipantId, "");
        if (participant is null || GameState.Instance is null || participant.IsLocalPlayer || !participant.CanReport)
            return;

        GameState.Instance.RecordBattleReport(participant.ParticipantId, participant.DisplayName, "举报原因待接入服务端表单");
        AppendBattleCommunicationMessage("设置", $"已记录对 {participant.DisplayName} 的举报", new Color(1f, 0.72f, 0.54f));
        ShowAlert("举报已记录");
    }

    void AppendBattleCommunicationMessage(string speaker, string message, Color color)
    {
        if (battleCommunicationMessages is null)
            return;

        while (battleCommunicationLines.Count >= 6)
        {
            var first = battleCommunicationLines[0];
            battleCommunicationLines.RemoveAt(0);
            if (GodotObject.IsInstanceValid(first))
                first.QueueFree();
        }

        var line = HudLabel($"[{speaker}] {message}", 12, color);
        line.CustomMinimumSize = new Vector2(240, 18);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.92f));
        line.AddThemeConstantOverride("outline_size", 1);
        battleCommunicationMessages.AddChild(line);
        battleCommunicationLines.Add(line);
    }

    public void ReceiveBattleTextMessage(string participantId, string speaker, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var resolvedParticipantId = ResolveInboundBattleParticipantId(participantId, speaker);
        RememberInboundBattleParticipant(resolvedParticipantId, speaker);

        if (GameState.Instance is not null && !GameState.Instance.ShouldReceiveBattleText(resolvedParticipantId, speaker))
            return;

        AppendBattleCommunicationMessage(string.IsNullOrWhiteSpace(speaker) ? "队友" : speaker.Trim(), message.Trim(), new Color(0.86f, 0.96f, 1f));
    }

    public void ReceiveBattleVoiceSignal(string participantId, string speaker)
    {
        var resolvedParticipantId = ResolveInboundBattleParticipantId(participantId, speaker);
        RememberInboundBattleParticipant(resolvedParticipantId, speaker);

        if (GameState.Instance is not null && !GameState.Instance.ShouldReceiveBattleVoice(resolvedParticipantId, speaker))
            return;

        selectedBattleParticipantId = string.IsNullOrWhiteSpace(resolvedParticipantId) ? selectedBattleParticipantId : resolvedParticipantId;
        battleCommunicationVoiceRemaining = 0.9f;
        battleCommunicationVoiceLabel.Text = $"{(string.IsNullOrWhiteSpace(speaker) ? "队友" : speaker.Trim())} 语音中";
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
        foreach (var child in upgradeRequirementList.GetChildren())
            child.QueueFree();

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
        var rowHeight = requirementArray.Length == 0 ? 62f : 34f + requirementArray.Length * 24f;
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
                CustomMinimumSize = new Vector2(334f, 22f)
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
            name.CustomMinimumSize = new Vector2(174f, 20f);
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(name);

            var progressText = requirement.RequiredLevel > 1
                ? $"等级 {requirement.HighestLevel}/{requirement.RequiredLevel}"
                : $"数量 {requirement.CurrentCount}/{requirement.RequiredCount}";
            var progress = HudLabel(progressText, 12, requirement.IsMet
                ? new Color(0.72f, 1f, 0.76f)
                : new Color(1f, 0.58f, 0.48f));
            progress.HorizontalAlignment = HorizontalAlignment.Right;
            progress.CustomMinimumSize = new Vector2(118f, 20f);
            progress.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(progress);
        }

        commandList.AddChild(card);
    }

    static string FormatJoinedNames(IEnumerable<string> values, string emptyText)
    {
        var array = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return array.Length == 0 ? emptyText : string.Join("、", array);
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
        titleLabel.Size = new Vector2(size.X - 20f, 17f);
        titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(titleLabel);

        var valueLabel = HudLabel(value, valueFontSize, new Color(0.95f, 0.97f, 0.98f));
        valueLabel.Position = new Vector2(10f, 23f);
        valueLabel.Size = new Vector2(size.X - 20f, Mathf.Max(20f, size.Y - 44f));
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        valueLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(valueLabel);

        if (!string.IsNullOrEmpty(note))
        {
            var noteLabel = HudLabel(note, noteFontSize, new Color(0.74f, 0.88f, 0.92f));
            noteLabel.Position = new Vector2(10f, size.Y - 18f);
            noteLabel.Size = new Vector2(size.X - 20f, 14f);
            noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            noteLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            card.AddChild(noteLabel);
        }

        return card;
    }

    static string BuildTechSummary(BattleTechDefinition tech)
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

        var statText = parts.Count == 0 ? "提供范围支援" : string.Join("，", parts);
        return $"{statText}，持续 {tech.Duration:0}s，半径 {tech.Radius:0}";
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
            Size = new Vector2(394f, 250f),
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
        holder.AddChild(frame);

        var avatar = new TextureRect
        {
            Name = "Avatar",
            Position = new Vector2(5f, 5f),
            Size = new Vector2(42f, 42f),
            Texture = LoadHudTexture(UnityBattleHudRoot + "battle_avatar_placeholder.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = Colors.White
        };
        holder.AddChild(avatar);

        var badge = new TextureRect
        {
            Name = "RankBadge",
            Position = new Vector2(56f, 2f),
            Size = new Vector2(44f, 44f),
            Texture = LoadHudTexture(UnityLobbyGenRoot + "WW2RankBadges/rank_badge_01_bronze_shield.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        holder.AddChild(badge);

        var rank = HudLabel("列兵", 14, new Color(1f, 0.90f, 0.55f));
        rank.MouseFilter = Control.MouseFilterEnum.Ignore;
        rank.Position = new Vector2(106f, 12f);
        rank.Size = new Vector2(58f, 18f);
        holder.AddChild(rank);

        return holder;
    }

    void ToggleSettingsDialog()
    {
        if (settingsDialogRoot is null)
            return;

        var willOpen = !settingsDialogRoot.Visible;
        settingsDialogRoot.Visible = willOpen;
        if (willOpen)
        {
            EnsureBattleCommunicationParticipants();
            RefreshBattleCommunicationState();
            settingsDialogRoot.MoveToFront();
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
        if (inputHintLabel is null)
            return;

        var manager = BattleGameManager.Instance;
        var controller = FindPlayerController();
        var selectedUnits = GetSelectedOwnedUnits();
        var color = new Color(0.78f, 0.92f, 0.94f);
        string text;

        if (!string.IsNullOrEmpty(activeTechTargetKey))
        {
            text = "科技释放中：点击地面确认范围，右键取消。";
            color = new Color(0.72f, 1f, 0.82f);
        }
        else if (!string.IsNullOrEmpty(manager?.PendingBuildKey))
        {
            text = $"建造模式：点击地面放置 {BattleBuildingCatalog.Get(manager.PendingBuildKey).DisplayName}，右键取消。";
            color = new Color(1f, 0.86f, 0.46f);
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
        else if (selectedBuilding is not null && GodotObject.IsInstanceValid(selectedBuilding))
        {
            text = selectedBuilding.IsMainBase
                ? "主基地已选中：右侧面板会显示等级、耐久和升级条件。"
                : selectedBuilding.CanSetRallyPoint()
                    ? "建筑已选中：右键地面设置集结点，右侧面板生产/升级/科技。"
                    : "建筑已选中：在右侧面板处理生产、维修、升级或重建。";
        }
        else if (selectedUnits.Count > 0)
        {
            text = $"已选择 {selectedUnits.Count} 个单位：右键移动/攻击，中键拖动地图，滚轮缩放；手机单指点地面下令，双指拖动或捏合镜头。";
        }
        else if (buildMenuOpen)
        {
            text = "建造面板：选择建筑后点击地图落点；资源不足的按钮会置灰。";
        }
        else if (techMenuOpen)
        {
            text = "科技面板：选择科技后点击战场位置释放范围效果。";
        }
        else
        {
            text = "左键点选/框选，右键移动或攻击，中键拖动地图，滚轮缩放；手机单指选择/下令，双指平移/缩放。";
        }

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

    void AddLeftActionButton(Control root, string label, string glyph, float bottom, Color bg, System.Action onPressed)
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
    }

    void StyleCommandListControls(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Button button:
                    ApplyButtonStyle(button, new Color(0.07f, 0.10f, 0.10f, 0.96f), new Color(0.95f, 0.72f, 0.28f, 0.72f), 12);
                    break;
                case Label label:
                    label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.90f));
                    label.AddThemeConstantOverride("outline_size", 2);
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

    static void ApplyButtonStyle(Button button, Color bg, Color accent, int fontSize)
    {
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
        var texture = GD.Load<Texture2D>(resourcePath);
        if (texture is not null)
            return texture;

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image is not null && !image.IsEmpty())
            return ImageTexture.CreateFromImage(image);

        GD.PushError($"Failed to load HUD image: {resourcePath}");
        var fallback = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        fallback.Fill(new Color(1f, 0f, 1f, 1f));
        return ImageTexture.CreateFromImage(fallback);
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

    static string FormatTime(float seconds)
    {
        var total = Mathf.FloorToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }

    void SetGameplayHudVisible(bool visible)
    {
        if (hudRoot is not null && GodotObject.IsInstanceValid(hudRoot))
        {
            foreach (var child in hudRoot.GetChildren())
            {
                if (child == gameOverOverlay || child == upgradeDialogRoot || child == settingsDialogRoot)
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
