using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class LobbyScreen : Control
{
    sealed class ModeCardUi
    {
        public Label TitleLabel { get; init; } = null!;
        public Label DescLabel { get; init; } = null!;
        public Label ActionLabel { get; init; } = null!;
    }

    const string BattleScenePath = "res://scenes/battle/BattlePrototype.tscn";
    const string UiRoot = "res://assets/ui/lobby_exact/";
    const string UnityLobbyRoot = "res://assets/unity_migrated/Assets/Resources/LobbyGen/";
    const string IconRoot = "res://assets/unity_migrated/Assets/Resources/icons/lobby_clean/";
    const string QuickMatchMode = "快速匹配";
    const string CustomRoomMode = "自定义房间";
    const string GlobalConquestMode = "全球争霸";

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

    Control matchSelection = null!;
    Control customSelection = null!;
    Control globalSelection = null!;
    Label playerLabel = null!;
    Label rankLabel = null!;
    Label goldLabel = null!;
    Label gemsLabel = null!;
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
    string selectedMode = QuickMatchMode;
    string selectedMap = BattleMapCatalog.DefaultMapName;
    string activeRoomId = "";
    string activeRoomMap = "";
    int activeRoomMaxPlayers = 2;
    int activeRoomPlayerCount = 1;
    long techEndAtMs;
    int techTotalSec;
    ulong toastRevision;
    double invitePollTimer;
    double presenceTimer;
    double lobbyPollTimer;
    bool quickMatchStarting;
    bool battleStarting;

    public override void _Ready()
    {
        if (GetNodeOrNull("Services/GameState") is null)
            AddRuntimeServices();

        selectedMap = GameState.Instance?.SelectedMapName ?? BattleMapCatalog.DefaultMapName;
        selectedMode = GameState.Instance?.SelectedMode ?? QuickMatchMode;

        BuildUi();
        ApplySelection(selectedMode);
        RefreshTopBar();
        _ = RefreshLobbyData();
        _ = RefreshFriends();
        _ = RefreshRooms();
        _ = NetClient.Instance?.PostPresence() ?? Task.CompletedTask;
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

        invitePollTimer += delta;
        if (invitePollTimer >= 30.0)
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
        var panel = AddPanel("TopBar", new Rect2(0.030f, 0.020f, 0.940f, 0.095f), GlassPanel, 2);
        var row = AddHBox(panel, "TopBarRow", 12);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 16;
        row.OffsetRight = -16;
        row.OffsetTop = 10;
        row.OffsetBottom = -10;

        playerLabel = AddLabel("指挥官", 18, PanelText, HorizontalAlignment.Left);
        playerLabel.CustomMinimumSize = new Vector2(250, 0);
        row.AddChild(playerLabel);

        rankLabel = AddLabel("军衔：--", 14, WarningText, HorizontalAlignment.Left);
        rankLabel.CustomMinimumSize = new Vector2(180, 0);
        row.AddChild(rankLabel);

        row.AddChild(Spacer());

        goldLabel = AddLabel("金币 --", 17, WarningText, HorizontalAlignment.Right);
        goldLabel.CustomMinimumSize = new Vector2(140, 0);
        row.AddChild(goldLabel);

        gemsLabel = AddLabel("钻石 --", 17, new Color(0.64f, 0.90f, 1f), HorizontalAlignment.Right);
        gemsLabel.CustomMinimumSize = new Vector2(140, 0);
        row.AddChild(gemsLabel);

        row.AddChild(AddIconButton(IconRoot + "icon_bell_transparent.svg", "公告", () => ShowInfoModal("公告", "大厅已接入服务器：房间、好友、邀请、任务、科技与排行榜都已接入真实面板，后续可继续接服务端数据。")));
        row.AddChild(AddIconButton(IconRoot + "icon_gear_transparent.svg", "设置", () => ShowInfoModal("设置", "设置面板后续会接音量、画质、账号退出。当前先保留入口与真实弹窗。")));
    }

    void BuildFriendsPanel()
    {
        var panel = AddPanel("FriendsPanel", new Rect2(0.020f, 0.145f, 0.240f, 0.725f), GlassPanel, 2);
        var box = AddVBox(panel, "FriendsBox", 8, new Vector2(14, 14), new Vector2(-14, -14));

        box.AddChild(AddSectionTitle("好友"));
        friendStatusLabel = AddLabel("好友加载中...", 13, MutedText, HorizontalAlignment.Left);
        box.AddChild(friendStatusLabel);

        var addRow = new HBoxContainer { Name = "AddFriendRow" };
        addRow.AddThemeConstantOverride("separation", 6);
        addFriendInput = new LineEdit
        {
            Name = "AddFriendInput",
            PlaceholderText = "输入昵称",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        addRow.AddChild(addFriendInput);
        addRow.AddChild(AddButton("添加", () => _ = AddFriend(), ButtonTone.Primary, 13));
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

        box.AddChild(AddButton("刷新好友 / 邀请", () => _ = RefreshFriends(), ButtonTone.Secondary, 13));
    }

    void BuildModeCards()
    {
        AddModeCard("ModeMatch", new Rect2(0.295f, 0.245f, 0.429f, 0.152f),
            "01", QuickMatchMode, "标准地图快速匹配", "立即匹配", new Color(0.86f, 0.66f, 0.24f), () => _ = TriggerQuickMatchCard());
        AddModeCard("ModeCustom", new Rect2(0.295f, 0.482f, 0.429f, 0.146f),
            "02", CustomRoomMode, "可选地图与人数，创建、加入并邀请好友", "打开房间", new Color(0.68f, 0.83f, 0.92f), () => OpenRoomPanel());
        AddModeCard("ModeGlobal", new Rect2(0.295f, 0.676f, 0.429f, 0.146f),
            "03", GlobalConquestMode, "先选本局主力兵种，进入后本局不可更改", "选择兵种再进入", new Color(0.80f, 0.58f, 0.28f), () => _ = StartGlobalConquest());

        matchSelection = AddModeSelection("MatchSelection", new Rect2(0.295f, 0.245f, 0.429f, 0.152f));
        customSelection = AddModeSelection("CustomSelection", new Rect2(0.295f, 0.482f, 0.429f, 0.146f));
        globalSelection = AddModeSelection("GlobalSelection", new Rect2(0.295f, 0.676f, 0.429f, 0.146f));
        matchSelection.Visible = customSelection.Visible = globalSelection.Visible = false;
        RefreshModeCards();
    }

    void AddModeCard(string name, Rect2 anchors, string index, string title, string desc, string action, Color accent, Action pressed)
    {
        var card = new Panel { Name = name + "Card", MouseFilter = MouseFilterEnum.Ignore };
        Place(card, anchors);
        MetalUiStyle.ApplyMetalPanel(card, MakeModeCardPalette(accent), 1, 10, 6);
        AddChild(card);

        var stripe = new ColorRect
        {
            Name = name + "Accent",
            Color = new Color(accent.R, accent.G, accent.B, 0.72f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        Place(stripe, new Rect2(anchors.Position.X, anchors.Position.Y, 0.006f, anchors.Size.Y));
        AddChild(stripe);

        var indexLabel = AddLabel(index, 30, new Color(accent.R, accent.G, accent.B, 0.58f), HorizontalAlignment.Right);
        indexLabel.MouseFilter = MouseFilterEnum.Ignore;
        Place(indexLabel, new Rect2(anchors.Position.X + anchors.Size.X - 0.120f, anchors.Position.Y + 0.020f, 0.090f, 0.060f));
        AddChild(indexLabel);

        var hit = AddButton("", pressed, ButtonTone.Transparent, 16);
        hit.Name = name;
        Place(hit, anchors);
        AddChild(hit);

        var textBox = new VBoxContainer { Name = name + "Text", MouseFilter = MouseFilterEnum.Ignore };
        textBox.AddThemeConstantOverride("separation", 3);
        Place(textBox, new Rect2(anchors.Position.X + 0.036f, anchors.Position.Y + 0.030f, anchors.Size.X - 0.090f, anchors.Size.Y - 0.050f));
        AddChild(textBox);
        var titleLabel = AddLabel(title, 23, PanelText, HorizontalAlignment.Left);
        textBox.AddChild(titleLabel);
        var descLabel = AddLabel(desc, 13, MutedText, HorizontalAlignment.Left);
        textBox.AddChild(descLabel);
        var actionLabel = AddLabel(action, 14, WarningText, HorizontalAlignment.Left);
        textBox.AddChild(actionLabel);

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
        taskBox.AddChild(AddSectionTitle("每日任务"));
        taskStatusLabel = AddLabel("任务加载中...", 13, MutedText, HorizontalAlignment.Left);
        taskBox.AddChild(taskStatusLabel);
        taskRows = new VBoxContainer { Name = "TaskRows", SizeFlagsVertical = SizeFlags.ExpandFill };
        taskRows.AddThemeConstantOverride("separation", 6);
        taskBox.AddChild(taskRows);
        taskBox.AddChild(AddButton("刷新任务", () => _ = RefreshLobbyData(), ButtonTone.Secondary, 13));

        var techPanel = AddPanel("TechPanel", new Rect2(0.746f, 0.618f, 0.238f, 0.255f), GlassPanel, 2);
        var techBox = AddVBox(techPanel, "TechBox", 7, new Vector2(14, 12), new Vector2(-14, -12));
        techBox.AddChild(AddSectionTitle("科技研究"));
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
        techStartButton = AddButton("研究", () => _ = StartTechResearch(), ButtonTone.Primary, 13);
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
        row.AddChild(AddButton("战役", () => _ = StartCampaign(), ButtonTone.Primary, 15));
        row.AddChild(AddButton("排行榜", () => _ = ShowLeaderboardModal(), ButtonTone.Gold, 15));
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
        foreach (var map in BattleMapCatalog.GetPlayableMapNames())
            roomMapPicker.AddItem(map);
        roomPlayerCountPicker = new OptionButton { Name = "RoomPlayerCountPicker", CustomMinimumSize = new Vector2(110, 0) };
        foreach (var count in new[] { 2, 4, 8, 16, 32, 64, 100 })
            roomPlayerCountPicker.AddItem($"{count}人", count);
        row.AddChild(roomNameInput);
        row.AddChild(roomMapPicker);
        row.AddChild(roomPlayerCountPicker);
        row.AddChild(AddButton("创建", () => _ = CreateRoom(), ButtonTone.Primary, 13));
        row.AddChild(AddButton("刷新", () => _ = RefreshRooms(), ButtonTone.Secondary, 13));
        createBox.AddChild(row);
        createBox.AddChild(AddLabel("自定义房间可选择地图与人数。当前联机战斗实时同步仍为双人对战，2 人以上房间先用于组房与邀请展示。", 12, WarningText, HorizontalAlignment.Left));
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
            taskStatusLabel.Text = "暂无任务";
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

    void BuildLocalTasks()
    {
        ClearChildren(taskRows);
        taskRows.AddChild(MakeTaskRow("daily_login", "每日登录", 1, 1, false, false));
        taskRows.AddChild(MakeTaskRow("win3", "赢得 3 场战斗", 0, 3, false, false));
        taskRows.AddChild(MakeTaskRow("destroy20", "摧毁 20 个敌方单位", 0, 20, false, false));
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

        var button = AddButton(claimed ? "已领" : canClaim ? "领取" : "未达成", () => _ = ClaimTask(taskId), canClaim ? ButtonTone.Gold : ButtonTone.Secondary, 12);
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
            ShowToast("任务领取失败：" + FriendlyText(data.GetString("error"), "进度不足或已领取"));
            return;
        }

        GameState.Instance?.ApplyLobbyData(data);
        RefreshTopBar();
        ShowToast("奖励已领取");
        await RefreshLobbyData(false);
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
            ShowToast("研究失败：" + FriendlyText(data.GetString("error"), "已有研究或网络不可用"));
            return;
        }
        ShowToast("科技研究已开始");
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

    async Task RefreshFriends()
    {
        loadedFriendNames.Clear();
        ClearChildren(friendRows);

        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            friendStatusLabel.Text = "请先登录后使用好友系统";
            AddLocalFriendRows();
            return;
        }

        var data = await NetClient.Instance.GetFriends();
        if (!data.GetBool("success"))
        {
            friendStatusLabel.Text = "离线模式：显示好友样例";
            AddLocalFriendRows();
            return;
        }

        var friends = data.GetArray("friends");
        friendStatusLabel.Text = friends.Count == 0 ? "暂无好友，可以添加一位" : $"已加载 {friends.Count} 位好友";
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
                FriendlyText(friend.GetString("rank"), friend.GetString("rankTitle", "好友")),
                FriendlyText(friend.GetString("status"), "在线"),
                friend.GetInt("level", 1)));
        }

        if (friends.Count == 0)
            AddLocalFriendRows();
    }

    void AddLocalFriendRows()
    {
        friendRows.AddChild(MakeFriendRow("IronWolf", "黄金指挥官", "在线", 18));
        friendRows.AddChild(MakeFriendRow("SeaHammer", "白银突击队", "匹配中", 14));
        friendRows.AddChild(MakeFriendRow("DesertFox", "装甲先锋", "离线", 9));
    }

    Control MakeFriendRow(string username, string rank, string status, int level)
    {
        var panel = new Panel { Name = "Friend_" + username, CustomMinimumSize = new Vector2(0, 50) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);
        var row = AddHBox(panel, "FriendRow", 8);
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 8;
        row.OffsetRight = -8;
        row.OffsetTop = 6;
        row.OffsetBottom = -6;

        var info = new VBoxContainer { Name = "FriendInfo", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddChild(AddLabel(username, 13, PanelText, HorizontalAlignment.Left));
        info.AddChild(AddLabel($"Lv.{level}  {rank}", 11, MutedText, HorizontalAlignment.Left));
        row.AddChild(info);
        row.AddChild(AddLabel(status, 12, StatusColor(status), HorizontalAlignment.Right));
        return panel;
    }

    async Task AddFriend()
    {
        var name = addFriendInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowToast("请输入好友昵称");
            return;
        }

        if (NetClient.Instance is null || GameState.Instance?.IsGuest == true)
        {
            ShowToast("游客模式暂不支持添加好友");
            return;
        }

        friendStatusLabel.Text = "添加中...";
        var data = await NetClient.Instance.AddFriend(name);
        if (!data.GetBool("success"))
        {
            friendStatusLabel.Text = "添加失败";
            ShowToast(FriendlyText(data.GetString("error"), "找不到该玩家"));
            return;
        }

        addFriendInput.Text = "";
        ShowToast($"已添加 {name}");
        await RefreshFriends();
    }

    async Task RefreshRooms()
    {
        if (roomRows is not null)
            ClearChildren(roomRows);

        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            if (roomStatusLabel is not null)
                roomStatusLabel.Text = "请先登录后再使用房间功能";
            AddRoomPlaceholder("请先登录后再使用房间功能，登录后可刷新、创建和邀请好友。");
            return;
        }

        if (roomStatusLabel is not null)
            roomStatusLabel.Text = "加载中...";
        var data = await NetClient.Instance.GetRooms();
        if (!data.GetBool("success"))
        {
            if (roomStatusLabel is not null)
                roomStatusLabel.Text = "房间服务不可用";
            AddRoomPlaceholder("服务器未连接，稍后重试。");
            return;
        }

        var rooms = data.GetArray("rooms");
        if (roomStatusLabel is not null)
            roomStatusLabel.Text = rooms.Count == 0 ? "暂无房间，可以创建一个" : $"找到 {rooms.Count} 个房间";
        if (rooms.Count == 0)
            AddRoomPlaceholder("暂无等待中的房间。");

        foreach (var item in rooms)
        {
            if (item.VariantType == Variant.Type.Dictionary)
                AddRoomRow(item.AsGodotDictionary());
        }
    }

    void AddRoomPlaceholder(string text)
    {
        if (roomRows is null)
            return;
        roomRows.AddChild(AddLabel(text, 14, MutedText, HorizontalAlignment.Center));
    }

    void AddRoomRow(Godot.Collections.Dictionary room)
    {
        if (roomRows is null)
            return;

        var roomId = room.GetString("id", room.GetString("roomId"));
        var roomName = FriendlyText(room.GetString("name"), "房间");
        var mapName = FriendlyText(room.GetString("mapName"), BattleMapCatalog.DefaultMapName);
        var players = room.GetArray("players");
        var maxPlayers = room.GetInt("maxPlayers", 2);
        var count = players.Count;
        var supportsRealtimeBattle = maxPlayers <= 2;
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
        info.AddChild(AddLabel(isActiveRoom ? $"当前房间：{roomName}" : roomName, 15, isActiveRoom ? GoodText : PanelText, HorizontalAlignment.Left));
        var modeHint = supportsRealtimeBattle ? "双人实时战斗" : "多人组房";
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

    async Task CreateRoom()
    {
        if (NetClient.Instance is null || string.IsNullOrEmpty(GameState.Instance?.Token))
        {
            ShowToast("请先登录后再创建房间");
            return;
        }

        var mapName = BattleMapCatalog.DefaultMapName;
        if (roomMapPicker is not null && roomMapPicker.ItemCount > 0)
            mapName = roomMapPicker.GetItemText(roomMapPicker.Selected);
        var roomName = roomNameInput?.Text.Trim() ?? "";
        var maxPlayers = SelectedRoomPlayerCount();
        if (roomStatusLabel is not null)
            roomStatusLabel.Text = "创建中...";

        var data = await NetClient.Instance.CreateRoom(mapName, roomName, maxPlayers);
        if (!data.GetBool("success"))
        {
            if (roomStatusLabel is not null)
                roomStatusLabel.Text = "创建失败";
            ShowToast(FriendlyText(data.GetString("error"), "创建失败"));
            return;
        }

        activeRoomId = data.GetString("roomId");
        activeRoomMap = data.GetString("mapName", mapName);
        activeRoomMaxPlayers = data.GetInt("maxPlayers", maxPlayers);
        activeRoomPlayerCount = data.GetInt("playerCount", 1);
        selectedMap = activeRoomMap;
        GameState.Instance?.SelectMap(activeRoomMap, CustomRoomMode);
        GameState.Instance?.SetCurrentRoom(activeRoomId);
        ShowToast("房间已创建");
        await RefreshRooms();
        if (roomStatusLabel is not null)
            roomStatusLabel.Text = $"房间已创建：{activeRoomMap}  {activeRoomPlayerCount}/{activeRoomMaxPlayers}。可以邀请好友，双人房可直接进入战斗。";
        ShowCreatedRoomActions();
    }

    void ShowCreatedRoomActions()
    {
        if (activeRoomActionsPanel is not null && GodotObject.IsInstanceValid(activeRoomActionsPanel))
            activeRoomActionsPanel.QueueFree();
        activeRoomActionsPanel = null;

        if (string.IsNullOrEmpty(activeRoomId))
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
        var enterButton = AddButton(
            activeRoomMaxPlayers <= 2 ? "进入战斗" : "多人战斗待接入",
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

        if (roomStatusLabel is not null)
            roomStatusLabel.Text = "加入中...";
        var data = await NetClient.Instance.JoinRoom(roomId);
        if (!data.GetBool("success"))
        {
            if (roomStatusLabel is not null)
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
        await RefreshRooms();
        ShowCreatedRoomActions();
        await EnterRoomBattle(activeRoomId, activeRoomMap, activeRoomMaxPlayers);
    }

    async Task EnterRoomBattle(string roomId, string mapName, int maxPlayers = 2)
    {
        if (!SupportsRealtimeRoomBattle(maxPlayers))
        {
            ShowToast("该房间已支持自定义人数，但当前实时联机战斗仍限 2 人。");
            if (roomStatusLabel is not null)
                roomStatusLabel.Text = $"已加入 {Math.Max(2, maxPlayers)} 人房间，可继续邀请成员；多人实时战斗同步仍在接入中。";
            return;
        }

        selectedMode = CustomRoomMode;
        selectedMap = BattleMapCatalog.IsKnownMap(mapName) ? mapName : BattleMapCatalog.DefaultMapName;
        GameState.Instance?.SelectMap(selectedMap, selectedMode);
        GameState.Instance?.SetCurrentRoom(roomId);
        await StartBattle(false);
    }

    void ShowInvitePanel(string roomId)
    {
        modalTitle.Text = "邀请好友";
        ClearChildren(modalBody);
        modalBody.AddChild(AddLabel(string.IsNullOrEmpty(roomId) ? "请先创建或选择房间。" : $"房间：{roomId}", 13, MutedText, HorizontalAlignment.Left));
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
        if (invites.Count == 0 || invites[0].VariantType != Variant.Type.Dictionary)
            return;
        ShowIncomingInvite(invites[0].AsGodotDictionary());
    }

    void ShowIncomingInvite(Godot.Collections.Dictionary invite)
    {
        modalTitle.Text = "房间邀请";
        ClearChildren(modalBody);
        var inviteId = invite.GetString("id");
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
        if (NetClient.Instance is null)
            return;
        var data = await NetClient.Instance.RespondInvite(inviteId, accept);
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

        var list = AddModalTable("商品", new[] { "商品", "价格", "内容", "操作" }, new[] { 190f, 92f, 214f, 92f });
        AddShopRow(list, "战地补给箱", "300 金币", "获得 500 金币与少量指挥经验", "购买");
        AddShopRow(list, "高级建造许可", "30 钻石", "下一场战斗建造速度提升 10%", "购买");
        AddShopRow(list, "科技速研件", "25 钻石", "科技研究立即缩短 10 分钟", "加速");
        AddShopRow(list, "全球争霸军备包", "80 钻石", "随机军备蓝图、通行证积分与稀有材料", "购买");
        AddShopRow(list, "老兵训练手册", "450 金币", "步兵单位初始经验提升", "购买");
        ShowModal();
    }

    void AddShopRow(VBoxContainer list, string name, string price, string content, string action)
    {
        list.AddChild(AddTableRow(
            new[] { name, price, content },
            new[] { 190f, 92f, 214f },
            action,
            () => ShowToast($"{name} 的购买接口待接入"),
            ButtonTone.Gold));
    }

    void ShowWarehouseModal()
    {
        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        OpenFeatureModal("仓库", "查看已有资源、军备蓝图、科技加成与全球争霸军备配置。");
        AddModalSummaryRow(
            AddStatCard("金币库存", $"{GameState.Instance?.Gold ?? 0}", "可用于建造与商品", WarningText),
            AddStatCard("钻石库存", $"{GameState.Instance?.Gems ?? 0}", "可用于加速与礼包", new Color(0.64f, 0.90f, 1f)),
            AddStatCard("当前开局主力", currentStarter.DisplayName, "全球争霸默认军备", GoodText));

        var list = AddModalTable("军备蓝图", new[] { "蓝图", "类别", "费用/人口", "状态" }, new[] { 180f, 96f, 118f, 184f });
        foreach (var unit in BattleUnitCatalog.PlayerRoster)
        {
            var type = UnitCategory(unit.Key);
            var status = WarehouseUnitStatus(unit.Key);
            list.AddChild(AddTableRow(
                new[] { unit.DisplayName, type, $"{unit.GoldCost} / {unit.PopCost}", status },
                new[] { 180f, 96f, 118f, 184f },
                "",
                null,
                ButtonTone.Secondary,
                status.StartsWith("已", StringComparison.Ordinal)));
        }

        var techList = AddModalTable("科技加成", new[] { "科技", "作用建筑", "效果", "备注" }, new[] { 144f, 92f, 222f, 128f });
        foreach (var tech in BattleTechCatalog.GetForBuilding("main_base"))
            AddTechBonusRow(techList, tech);

        var armamentList = AddModalTable("全球争霸军备", new[] { "军备", "类别", "状态", "说明" }, new[] { 132f, 88f, 92f, 272f });
        foreach (var starter in BattleUnitCatalog.GlobalConquestStarterRoster)
            AddGlobalArmamentRow(armamentList, starter, currentStarterKey == starter.Key);

        var itemList = AddModalTable("道具与科技卡", new[] { "物品", "类型", "数量", "说明" }, new[] { 170f, 100f, 72f, 236f });
        AddInventoryRow(itemList, "科技卡：机动强化", "科技卡", "1", "主基地范围科技，提升友军移动能力。");
        AddInventoryRow(itemList, "全球争霸通行证", "战役道具", "1", "进入全球争霸玩法并记录赛季进度。");
        AddInventoryRow(itemList, "战地维修包", "消耗品", "3", "战斗中用于紧急维修单位或建筑。");
        ShowModal();
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
        OpenFeatureModal("排行榜", "赛季军功、胜场与军衔排名。");
        modalBody.AddChild(AddLabel("排行榜加载中...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();

        Godot.Collections.Dictionary data = new();
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetLeaderboard();

        OpenFeatureModal("排行榜", "赛季军功、胜场与军衔排名。");
        var rows = data.GetArray("leaderboard");
        if (!data.GetBool("success") || rows.Count == 0)
        {
            AddModalSummaryRow(
                AddStatCard("模式", "离线样例", "服务器不可用时展示", WarningText),
                AddStatCard("我的胜场", $"{GameState.Instance?.Wins ?? 0}", "本地账号缓存", GoodText),
                AddStatCard("我的军衔", FriendlyText(GameState.Instance?.RankTitle, "列兵"), "当前档案", PanelText));
            AddLeaderboardTableHeader(out var sampleList);
            AddLeaderboardRow(sampleList, 1, GameState.Instance?.Username ?? "Commander", GameState.Instance?.Level ?? 1, GameState.Instance?.Wins ?? 0, GameState.Instance?.Losses ?? 0, GameState.Instance?.RankTitle ?? "列兵", true);
            AddLeaderboardRow(sampleList, 2, "IronWolf", 8, 12, 4, "上尉", false);
            AddLeaderboardRow(sampleList, 3, "SeaHammer", 6, 9, 3, "中尉", false);
            AddLeaderboardRow(sampleList, 4, "SkyLancer", 5, 7, 5, "少尉", false);
            ShowModal();
            return;
        }

        AddModalSummaryRow(
            AddStatCard("赛季", FriendlyText(data.GetString("season"), "当前赛季"), FriendlyText(data.GetString("resetText"), "每周结算"), WarningText),
            AddStatCard("上榜人数", $"{rows.Count}", "实时排名", GoodText),
            AddStatCard("我的位置", "已标亮", "绿色行为当前账号", PanelText));
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

    void AddLeaderboardTableHeader(out VBoxContainer list)
    {
        list = AddModalTable("军功榜", new[] { "排名", "指挥官", "军衔", "战绩", "胜率" }, new[] { 64f, 176f, 116f, 102f, 80f });
    }

    void AddLeaderboardRow(VBoxContainer list, int place, string username, int level, int wins, int losses, string rankTitle, bool isCurrent)
    {
        var total = Math.Max(1, wins + losses);
        var winRate = Mathf.RoundToInt(wins * 100f / total);
        list.AddChild(AddTableRow(
            new[] { $"#{place}", $"{FriendlyText(username, "Commander")}  Lv.{level}", FriendlyText(rankTitle, "列兵"), $"{wins}胜/{losses}负", $"{winRate}%" },
            new[] { 64f, 176f, 116f, 102f, 80f },
            "",
            null,
            ButtonTone.Secondary,
            isCurrent));
    }

    async Task ShowMailModal()
    {
        OpenFeatureModal("邮件 / 邀请", "处理房间邀请、系统公告与战报消息。");
        modalBody.AddChild(AddLabel("正在同步邀请...", 14, MutedText, HorizontalAlignment.Left));
        ShowModal();

        Godot.Collections.Dictionary data = new();
        if (NetClient.Instance is not null)
            data = await NetClient.Instance.GetInvites();

        OpenFeatureModal("邮件 / 邀请", "处理房间邀请、系统公告与战报消息。");
        var invites = data.GetArray("invites");
        AddModalSummaryRow(
            AddStatCard("未处理邀请", $"{(data.GetBool("success") ? invites.Count : 0)}", "好友房间邀请", invites.Count > 0 ? WarningText : GoodText),
            AddStatCard("系统邮件", "2", "公告与补偿", PanelText),
            AddStatCard("战报", "1", "最近一场战斗", GoodText));

        var inviteList = AddModalTable("房间邀请", new[] { "发件人", "地图", "房间", "操作" }, new[] { 128f, 168f, 126f, 154f });
        if (!data.GetBool("success") || invites.Count == 0)
            inviteList.AddChild(AddEmptyTableRow("暂无未处理邀请。"));
        else
        {
            foreach (var item in invites)
            {
                if (item.VariantType == Variant.Type.Dictionary)
                    AddMailInviteRow(inviteList, item.AsGodotDictionary());
            }
        }

        var mailList = AddModalTable("系统邮件", new[] { "标题", "类型", "状态", "说明" }, new[] { 184f, 92f, 82f, 220f });
        AddSystemMailRow(mailList, "全球争霸赛季开放", "公告", "未读", "新增全球争霸入口与军备展示。");
        AddSystemMailRow(mailList, "每日补给已刷新", "补给", "可领", "完成每日任务可领取金币和经验。");
        AddSystemMailRow(mailList, "最近战报", "战报", "已读", "战斗报告可在结算界面查看详细统计。");
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
    }

    void AddModalSummaryRow(params Control[] cards)
    {
        var row = new HBoxContainer { Name = "ModalSummaryRow" };
        row.AddThemeConstantOverride("separation", 8);
        foreach (var card in cards)
            row.AddChild(card);
        modalBody.AddChild(row);
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

        var stripe = new ColorRect
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(260, 3),
            Color = new Color(accent.R, accent.G, accent.B, 0.82f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        card.AddChild(stripe);

        var box = AddVBox(card, "StatCardBox_" + title, 2, new Vector2(12, 10), new Vector2(-12, -10));
        box.AddChild(AddLabel(title, 12, MutedText, HorizontalAlignment.Left));
        box.AddChild(AddLabel(value, 18, accent, HorizontalAlignment.Left));
        var noteLabel = AddLabel(note, 11, PanelText, HorizontalAlignment.Left);
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(noteLabel);
        return card;
    }

    VBoxContainer AddModalTable(string title, string[] headers, float[] widths)
    {
        var panel = new Panel { Name = "Table_" + title, CustomMinimumSize = new Vector2(0, 0) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 8, 4);
        modalBody.AddChild(panel);

        var box = AddVBox(panel, "TableBox_" + title, 8, new Vector2(12, 10), new Vector2(-12, -10));
        box.AddChild(AddSectionTitle(title));
        box.AddChild(AddHeaderRow(headers, widths));

        var list = new VBoxContainer { Name = "TableRows_" + title };
        list.AddThemeConstantOverride("separation", 6);
        box.AddChild(list);
        return list;
    }

    Control AddHeaderRow(string[] headers, float[] widths)
    {
        var row = new HBoxContainer { Name = "HeaderRow" };
        row.AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < headers.Length; i++)
        {
            var label = AddLabel(headers[i], 12, WarningText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 24);
            label.SizeFlagsHorizontal = i == headers.Length - 1 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
            row.AddChild(label);
        }
        return row;
    }

    Control AddTableRow(string[] cells, float[] widths, string actionText, Action? onAction, ButtonTone actionTone, bool highlight = false)
    {
        var panel = new Panel { Name = "TableRow_" + Guid.NewGuid().ToString("N")[..6], CustomMinimumSize = new Vector2(0, 42) };
        MetalUiStyle.ApplyMetalPanel(panel, highlight ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 3);

        var row = new HBoxContainer { Name = "Row" };
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 10;
        row.OffsetRight = -10;
        row.OffsetTop = 7;
        row.OffsetBottom = -7;
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        for (var i = 0; i < cells.Length; i++)
        {
            var label = AddLabel(cells[i], 12, highlight ? GoodText : PanelText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 24);
            label.SizeFlagsHorizontal = i == cells.Length - 1 && string.IsNullOrEmpty(actionText) ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
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
        var panel = new Panel { Name = "ActionTableRow_" + Guid.NewGuid().ToString("N")[..6], CustomMinimumSize = new Vector2(0, 46) };
        MetalUiStyle.ApplyMetalPanel(panel, highlight ? MetalUiStyle.Green : MetalUiStyle.Steel, 1, 4, 3);

        var row = new HBoxContainer { Name = "ActionRow" };
        row.SetAnchorsPreset(LayoutPreset.FullRect);
        row.OffsetLeft = 10;
        row.OffsetRight = -10;
        row.OffsetTop = 7;
        row.OffsetBottom = -7;
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        for (var i = 0; i < cells.Length; i++)
        {
            var label = AddLabel(cells[i], 12, highlight ? GoodText : PanelText, HorizontalAlignment.Left);
            label.CustomMinimumSize = new Vector2(widths[i], 24);
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
        var panel = new Panel { Name = "EmptyTableRow", CustomMinimumSize = new Vector2(0, 42) };
        MetalUiStyle.ApplyMetalPanel(panel, MetalUiStyle.Steel, 1, 4, 3);
        var label = AddLabel(text, 13, MutedText, HorizontalAlignment.Center);
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.OffsetLeft = 12;
        label.OffsetRight = -12;
        label.OffsetTop = 8;
        label.OffsetBottom = -8;
        panel.AddChild(label);
        return panel;
    }

    static string UnitCategory(string unitKey) => unitKey switch
    {
        "scout_plane" or "fighter" or "bomber" => "空军",
        "patrol_boat" or "destroyer_ship" or "transport_ship" => "海军",
        "light_tank" or "tank" or "heavy_tank" or "artillery" or "anti_air_gun" => "装甲",
        _ => "陆军"
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

                    ShowToast(match.GetBool("success") ? "暂无对手，进入本地演练" : "匹配服务不可用，进入本地演练");
                }
                else
                {
                    ShowToast("匹配等待超时，进入本地演练");
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
            ShowToast("进入战场");
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
        modalTitle.Text = "全球争霸兵种选择";
        ClearChildren(modalBody);

        var reminder = AddLabel("开始全球争霸前请选择开局主力兵种。进入本局后将锁定，战斗中不可更换。", 15, WarningText, HorizontalAlignment.Left);
        reminder.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(reminder);

        var currentStarterKey = GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank";
        var currentStarter = BattleUnitCatalog.Get(currentStarterKey);
        var currentLabel = AddLabel($"当前默认：{currentStarter.DisplayName}    提示：这里只锁定本局，下次进入可重新选择。", 12, MutedText, HorizontalAlignment.Left);
        currentLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(currentLabel);

        foreach (var starter in BattleUnitCatalog.GlobalConquestStarterRoster)
            modalBody.AddChild(BuildGlobalConquestStarterOption(starter, currentStarterKey == starter.Key));

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

    Panel BuildGlobalConquestStarterOption(BattleUnitDefinition starter, bool isCurrent)
    {
        var optionName = "Starter_" + starter.Key;
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

        var title = AddLabel(starter.DisplayName, 16, PanelText, HorizontalAlignment.Left);
        textBox.AddChild(title);

        var summary = AddLabel(DescribeGlobalConquestStarter(starter.Key), 12, MutedText, HorizontalAlignment.Left);
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textBox.AddChild(summary);

        var stats = AddLabel(
            $"耐久 {starter.MaxHealth:0}    火力 {starter.AttackDamage:0}    射程 {starter.AttackRange:0.#}    机动 {starter.MoveSpeed:0.#}",
            12,
            isCurrent ? GoodText : WarningText,
            HorizontalAlignment.Left);
        textBox.AddChild(stats);

        if (isCurrent)
            textBox.AddChild(AddLabel("当前默认开局兵种", 11, GoodText, HorizontalAlignment.Left));

        row.AddChild(textBox);

        var enterButton = AddButton(isCurrent ? "按当前选择进入" : "选择并进入", async () => await ConfirmGlobalConquestStarter(starter.Key), ButtonTone.Primary, 13);
        enterButton.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        enterButton.CustomMinimumSize = new Vector2(148, 0);
        row.AddChild(enterButton);

        return panel;
    }

    async Task ConfirmGlobalConquestStarter(string unitKey)
    {
        var starter = BattleUnitCatalog.Get(unitKey);
        SelectModeInternal(GlobalConquestMode, BattleMapCatalog.GlobalConquestName, false);
        GameState.Instance?.SetGlobalConquestStarterUnit(starter.Key);
        GameState.Instance?.ClearCurrentRoom();
        CloseModal();
        ShowToast($"全球争霸开局主力已设为 {starter.DisplayName}");
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

    async Task StartBattle(bool clearRoom = true)
    {
        if (battleStarting)
            return;
        battleStarting = true;
        ShowToast($"进入 {selectedMode}");
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
        var gs = GameState.Instance;
        playerLabel.Text = string.IsNullOrEmpty(gs?.Username) ? "指挥官：游客" : $"指挥官：{gs.Username}  Lv.{gs.Level}";
        rankLabel.Text = $"军衔：{FriendlyText(gs?.RankTitle, "列兵")}";
        goldLabel.Text = $"金币 {gs?.Gold ?? 0}";
        gemsLabel.Text = $"钻石 {gs?.Gems ?? 0}";
        RefreshModeCards();
    }

    void RefreshModeCards()
    {
        if (!modeCards.TryGetValue(QuickMatchMode, out var quickMatchCard))
            return;

        if (ShouldShowQuickMatchBattleEntry())
        {
            quickMatchCard.TitleLabel.Text = "进入战场";
            quickMatchCard.DescLabel.Text = "从大厅继续当前快速匹配战局";
            quickMatchCard.ActionLabel.Text = "进入战场";
        }
        else
        {
            quickMatchCard.TitleLabel.Text = QuickMatchMode;
            quickMatchCard.DescLabel.Text = "标准地图快速匹配";
            quickMatchCard.ActionLabel.Text = "立即匹配";
        }

        if (!modeCards.TryGetValue(GlobalConquestMode, out var globalCard))
            return;

        var starter = BattleUnitCatalog.Get(GameState.Instance?.GlobalConquestStarterUnitKey ?? "tank");
        globalCard.TitleLabel.Text = GlobalConquestMode;
        globalCard.DescLabel.Text = "进入前选择开局主力兵种，进入后本局不可更改";
        globalCard.ActionLabel.Text = $"当前默认：{starter.DisplayName}";
    }

    void ShowInfoModal(string title, string body)
    {
        modalTitle.Text = title;
        ClearChildren(modalBody);
        var label = AddLabel(body, 15, PanelText, HorizontalAlignment.Left);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        modalBody.AddChild(label);
        ShowModal();
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
            Icon = LoadTexture(iconPath),
            ExpandIcon = true,
            TooltipText = tooltip,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(48, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        button.Pressed += onPressed;
        ApplyTransparentIconStyle(button, 9);
        return button;
    }

    Button AddCloseIconButton(Action onPressed)
    {
        var button = new Button
        {
            Icon = LoadTexture(IconRoot + "icon_close.svg"),
            ExpandIcon = true,
            TooltipText = "关闭",
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
            || value.Contains("锟")
            || value.Contains("閳")
            || value.Contains("閹")
            || value.Contains("閸"))
            return fallback;
        return value;
    }

    static string ShortRoomId(string value)
        => value.Length <= 6 ? value : value[..6];

    int SelectedRoomPlayerCount()
    {
        if (roomPlayerCountPicker is null || roomPlayerCountPicker.ItemCount <= 0)
            return 2;

        var selected = Mathf.Clamp(roomPlayerCountPicker.Selected, 0, roomPlayerCountPicker.ItemCount - 1);
        var itemId = roomPlayerCountPicker.GetItemId(selected);
        return itemId >= 2 ? itemId : 2;
    }

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
