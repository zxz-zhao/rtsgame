using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LobbyUIToolkitController : MonoBehaviour
{
    [SerializeField] UIDocument document;

    VisualElement root;
    VisualElement backdrop;
    VisualElement currentPopup;
    bool bound;
    string selectedMatchMap = BattleMapCatalog.DefaultMapName;
    ScrollView friendsPopupList;
    Label friendsPopupStatusLabel;
    Label friendsPopupDetailTitle;
    Label friendsPopupDetailBody;
    ScrollView rankPopupList;
    Label rankSeasonLabel;
    Label rankResetLabel;
    Label rankDetailTitle;
    Label rankDetailBody;
    ScrollView notifyPopupList;
    Label notifyDetailTitle;
    Label notifyDetailBody;
    string[] warehouseItemNames = new string[0];
    string[] warehouseItemDescs = new string[0];
    int[] warehouseItemCounts = new int[0];
    int warehouseSelectedIndex = -1;
    readonly List<VisualElement> friendPopupRows = new List<VisualElement>();
    readonly List<SimpleJson> friendPopupData = new List<SimpleJson>();
    readonly List<VisualElement> rankPopupRows = new List<VisualElement>();
    readonly List<SimpleJson> rankPopupData = new List<SimpleJson>();
    readonly List<VisualElement> notifyPopupRows = new List<VisualElement>();
    readonly List<SimpleJson> notifyPopupData = new List<SimpleJson>();
    int friendPopupSelectedIndex = -1;
    int rankPopupSelectedIndex = -1;
    int notifyPopupSelectedIndex = -1;

    const int MatchMapButtonCount = 5;
    const int WarehouseItemCount = 6;
    const string SelectedClass = "selected";

    void Awake()
    {
        BindIfNeeded();
    }

    void OnEnable()
    {
        BindIfNeeded();
    }

    void BindIfNeeded()
    {
        if (document == null)
            document = GetComponent<UIDocument>();
        if (document == null || document.rootVisualElement == null)
            return;

        root = document.rootVisualElement;
        backdrop = root.Q<VisualElement>("modal-backdrop-button");

        if (!bound)
            HideAllPopups();
        RefreshMapButtons();
        RefreshSettingsRuntimeState();
        CacheFriendsPopupElements();
        CacheRankPopupElements();
        CacheNotifyPopupElements();

        if (bound)
            return;
        bound = true;

        RegisterClose("modal-backdrop-button");
        RegisterClose("notify-popup-close");
        RegisterClose("notify-popup-action");
        RegisterClose("tech-popup-close");
        RegisterClose("match-popup-close");
        RegisterClose("warehouse-popup-close");
        RegisterClose("rank-popup-close");
        RegisterClose("room-popup-close");
        RegisterClose("invite-popup-close");
        RegisterClose("friends-popup-close");
        RegisterClose("settings-popup-close");

        RegisterOpen("settings-button", OpenSettingsPopup);
        RegisterOpen("quick-match-button", OpenMatchPopup);
        RegisterOpen("notify-button", OpenToolkitNotifyPanel);
        RegisterOpen("gold-plus-button", OpenToolkitShopPanel);
        RegisterOpen("gem-plus-button", OpenToolkitGemStorePanel);
        RegisterOpen("shop-nav-button", OpenToolkitShopPanel);
        RegisterOpen("warehouse-nav-button", OpenToolkitWarehousePanel);
        RegisterOpen("campaign-nav-button", OpenMatchPopup);
        RegisterOpen("rank-nav-button", OpenToolkitRankPanel);
        RegisterOpen("mail-nav-button", OpenToolkitMailPanel);
        RegisterOpen("global-conquest-button", OpenToolkitGlobalConquestPanel);
        RegisterOpen("friends-button", OpenFriendsPopup);
        RegisterOpen("tech-panel-button", OpenTechPopup);
        RegisterOpen("tech-gear-button", OpenTechPopup);
        RegisterOpen("tech-tree-button", OpenTechPopup);

        var techPopupStart = root.Q<Button>("tech-popup-start");
        if (techPopupStart != null)
            techPopupStart.clicked += StartTechResearchFromPopup;

        var techSpeedButton = root.Q<Button>("tech-speed-button");
        if (techSpeedButton != null)
            techSpeedButton.clicked += SpeedUpTechFromHall;

        for (int i = 0; i < MatchMapButtonCount; i++)
        {
            int mapIndex = i;
            var mapButton = root.Q<Button>("match-map-button-" + i);
            if (mapButton != null)
                mapButton.clicked += () => SelectMatchMap(mapIndex);
        }

        for (int i = 0; i < WarehouseItemCount; i++)
        {
            int warehouseIndex = i;
            var detailButton = root.Q<Button>("warehouse-detail-button-" + i);
            if (detailButton != null)
                detailButton.clicked += () => SelectWarehouseEntry(warehouseIndex);
        }

        var startButton = root.Q<Button>("match-start-button");
        if (startButton != null)
            startButton.clicked += StartSelectedMatch;

        var cancelButton = root.Q<Button>("match-cancel-button");
        if (cancelButton != null)
            cancelButton.clicked += CancelOrReturnFromMatch;

        var returnBattleButton = root.Q<Button>("settings-return-battle-button");
        if (returnBattleButton != null)
            returnBattleButton.clicked += ReturnToBattle;

        var notifyAction = root.Q<Button>("notify-popup-action");
        if (notifyAction != null)
            notifyAction.clicked += HandleNotifyPopupAction;
    }

    void Update()
    {
        if (root != null && currentPopup != null && currentPopup.name == "match-popup")
            RefreshMatchRuntimeState();
        if (root != null && currentPopup != null && currentPopup.name == "settings-popup")
            RefreshSettingsRuntimeState();
    }

    void RegisterOpen(string name, System.Action action)
    {
        var button = root?.Q<Button>(name);
        if (button != null)
            button.clicked += action;
    }

    void RegisterClose(string name)
    {
        var button = root?.Q<Button>(name);
        if (button != null)
            button.clicked += CloseCurrentPopup;
    }

    void HideAllPopups()
    {
        if (root == null)
            return;

        var popups = root.Query<VisualElement>(className: "modal-popup").ToList();
        for (int i = 0; i < popups.Count; i++)
            SetVisible(popups[i], false);

        SetVisible(backdrop, false);
        currentPopup = null;
    }

    void CloseCurrentPopup()
    {
        if (currentPopup != null)
            SetVisible(currentPopup, false);
        SetVisible(backdrop, false);
        currentPopup = null;
    }

    void OpenPopup(string popupName)
    {
        BindIfNeeded();
        if (root == null)
            return;

        if (currentPopup != null)
            SetVisible(currentPopup, false);

        currentPopup = root.Q<VisualElement>(popupName);
        SetVisible(backdrop, currentPopup != null);
        SetVisible(currentPopup, currentPopup != null);
    }

    static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    void SetLabel(string name, string value)
    {
        var label = root?.Q<Label>(name);
        if (label != null)
            label.text = value ?? "";
    }

    void SetButtonText(string name, string text)
    {
        var button = root?.Q<Button>(name);
        if (button != null)
            button.text = text ?? "";
    }

    void SetProgressFill(string name, float progress)
    {
        var fill = root?.Q<VisualElement>(name);
        if (fill != null)
            fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
    }

    void OpenInfoPopup(string title, string subtitle, string body, string actionText = "OK")
    {
        BindIfNeeded();
        if (root == null)
            return;

        SetLabel("notify-popup-title", title);
        SetLabel("notify-popup-subtitle", subtitle);
        SetLabel("notify-detail-title", string.IsNullOrEmpty(subtitle) ? title : subtitle);
        SetLabel("notify-popup-body", body);
        ShowNotifyList(false);
        notifyPopupRows.Clear();
        notifyPopupData.Clear();
        notifyPopupSelectedIndex = -1;

        var action = root.Q<Button>("notify-popup-action");
        if (action != null)
            action.text = actionText;

        OpenPopup("notify-popup");
    }

    void OpenNotificationPopup(string subtitle, string body)
    {
        OpenInfoPopup("通知中心", subtitle, body, "知道了");
    }

    void OpenTechPopup()
    {
        BindIfNeeded();
        if (root == null)
            return;

        SetLabel("tech-popup-title", "科技中心");
        SetLabel("tech-popup-subtitle", "研究队列");
        OpenPopup("tech-popup");
    }

    public void OpenToolkitTechPanel()
    {
        OpenTechPopup();
    }

    void StartTechResearchFromPopup()
    {
        bool started = LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitTechStart();
        SetLabel("tech-popup-subtitle", started ? "研究指令已发送" : "需要先登录指挥账号");
    }

    void SpeedUpTechFromHall()
    {
        bool started = LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitTechSpeedUp();
        OpenTechPopup();
        SetLabel("tech-popup-subtitle", started ? "加速指令已发送" : "需要先登录指挥账号");
    }

    void OpenSettingsPopup()
    {
        BindIfNeeded();
        if (root == null)
            return;

        RefreshSettingsRuntimeState();
        OpenPopup("settings-popup");
    }

    void RefreshSettingsRuntimeState()
    {
        if (root == null)
            return;

        var returnBattleButton = root.Q<Button>("settings-return-battle-button");
        if (returnBattleButton != null)
            returnBattleButton.style.display = PlayerPrefs.GetInt("battle_resume_available", 0) == 1
                ? DisplayStyle.Flex
                : DisplayStyle.None;
    }

    void CacheFriendsPopupElements()
    {
        if (root == null)
            return;

        friendsPopupList = root.Q<ScrollView>("friends-popup-list");
        friendsPopupStatusLabel = root.Q<Label>("friends-status-label");
        friendsPopupDetailTitle = root.Q<Label>("friend-detail-title");
        friendsPopupDetailBody = root.Q<Label>("friend-detail-body");

        if (friendsPopupList != null)
        {
            friendsPopupList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            friendsPopupList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            friendsPopupList.contentContainer.style.flexDirection = FlexDirection.Column;
            friendsPopupList.contentContainer.style.alignItems = Align.Stretch;
        }
    }

    void ReturnToBattle()
    {
        PlayerPrefs.SetInt("battle_resume_available", 0);
        PlayerPrefs.Save();
        SceneManager.LoadScene("GameScene");
    }

    void RefreshMapButtons()
    {
        if (root == null)
            return;

        string[] maps = BattleMapCatalog.GetPlayableMapNames();
        for (int i = 0; i < MatchMapButtonCount; i++)
        {
            bool hasMap = maps != null && i < maps.Length;
            string mapName = hasMap ? maps[i] : "";

            var matchButton = root.Q<Button>("match-map-button-" + i);
            if (matchButton != null)
            {
                matchButton.text = mapName;
                matchButton.style.display = hasMap ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var roomButton = root.Q<Button>("room-map-button-" + i);
            if (roomButton != null)
            {
                roomButton.text = mapName;
                roomButton.style.display = hasMap ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        int selectedIndex = FindMapIndex(selectedMatchMap, maps);
        SelectMatchMap(selectedIndex >= 0 ? selectedIndex : 0);
    }

    static int FindMapIndex(string mapName, string[] maps)
    {
        if (maps == null || maps.Length == 0)
            return -1;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] == mapName)
                return i;
        }

        return -1;
    }

    void SelectMatchMap(int index)
    {
        if (root == null)
            return;

        string[] maps = BattleMapCatalog.GetPlayableMapNames();
        if (maps == null || maps.Length == 0)
        {
            selectedMatchMap = BattleMapCatalog.DefaultMapName;
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, maps.Length - 1);
        selectedMatchMap = maps[safeIndex];

        for (int i = 0; i < MatchMapButtonCount; i++)
        {
            var button = root.Q<Button>("match-map-button-" + i);
            if (button == null)
                continue;

            if (i == safeIndex)
                button.AddToClassList(SelectedClass);
            else
                button.RemoveFromClassList(SelectedClass);
        }

        SetLabel("match-selected-map", "当前战区：" + selectedMatchMap);
        SetButtonText("match-start-button", "开始匹配");
        RefreshMatchRuntimeState();
    }

    void OpenMatchPopup()
    {
        BindIfNeeded();
        if (root == null)
            return;

        LobbyManager.Instance?.UiToolkitQuickMatch();
        RefreshMapButtons();
        SetLabel("match-hint-label", "将按当前高亮战区寻找对手，匹配成功后自动进入战场。");
        OpenPopup("match-popup");
        RefreshMatchRuntimeState();
    }

    void StartSelectedMatch()
    {
        if (string.IsNullOrEmpty(selectedMatchMap))
            SelectMatchMap(0);

        LobbyManager.Instance?.UiToolkitStartMatch(selectedMatchMap);
        SetLabel("match-hint-label", "正在连接对手，匹配成功后自动进入战场。");
        SetButtonText("match-start-button", "开始匹配");
        SetButtonText("match-cancel-button", "取消匹配");
        RefreshMatchRuntimeState();
    }

    void CancelOrReturnFromMatch()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitIsMatching())
            LobbyManager.Instance.UiToolkitCancelMatch();

        SetLabel("match-timer-label", "--");
        SetLabel("match-hint-label", "将按当前高亮战区寻找对手，匹配成功后自动进入战场。");
        SetButtonText("match-start-button", "开始匹配");
        SetButtonText("match-cancel-button", "返回大厅");
        CloseCurrentPopup();
    }

    void RefreshMatchRuntimeState()
    {
        bool matching = LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitIsMatching();
        var startButton = root?.Q<Button>("match-start-button");
        if (startButton != null)
        {
            startButton.text = "开始匹配";
            startButton.SetEnabled(!matching);
        }

        SetButtonText("match-cancel-button", matching ? "取消匹配" : "返回大厅");
        SetLabel("match-timer-label", matching && LobbyManager.Instance != null
            ? LobbyManager.Instance.UiToolkitMatchElapsedSeconds() + "s"
            : "--");
    }

    public void OpenToolkitNotifyPanel()
    {
        OpenToolkitNotifyPanel("");
    }

    void OpenToolkitNotifyPanel(string subtitleOverride)
    {
        BindIfNeeded();
        if (root == null)
            return;

        CacheNotifyPopupElements();
        OpenPopup("notify-popup");
        SetLabel("notify-popup-title", "通知中心");
        SetLabel("notify-popup-subtitle", string.IsNullOrEmpty(subtitleOverride) ? "战场快报" : subtitleOverride);
        ShowNotifyList(true);

        var rows = LobbyManager.Instance != null
            ? LobbyManager.Instance.UiToolkitBuildNotifications()
            : new List<SimpleJson>
            {
                new SimpleJson
                {
                    title = "指挥部",
                    body = "暂无新消息，指挥部一切正常。",
                    status = "正常",
                    action = "知道了"
                }
            };

        PopulateNotifyPopup(rows);
    }

    public void OpenToolkitShopPanel()
    {
        OpenInfoPopup("Supply Shop", "Coin supplies", "The supply shop is being connected. For now, earn resources through battles.");
    }

    public void OpenToolkitWarehousePanel()
    {
        BindIfNeeded();
        if (root == null)
            return;

        LobbyManager.Instance?.UiToolkitOpenWarehouse();
        OpenPopup("warehouse-popup");
        SetLabel("warehouse-status-label", "Syncing supplies...");
        SetWarehouseDetail("Supply Details", "Select an item to inspect.");

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UiToolkitLoadWarehouse((status, names, descs, counts) =>
            {
                if (root == null)
                    return;

                PopulateWarehousePopup(status, names, descs, counts);
            });
            return;
        }

        PopulateWarehousePopup(
            "Supplies loaded",
            new[] { "Armor Crate", "Energy Core", "Vehicle Parts", "Command Chip", "Alloy Steel", "Boost Module" },
            new[]
            {
                "Rapid resupply for armored units on the front line.",
                "Powers base facilities and advanced research.",
                "Keeps tanks, artillery, and vehicle lines operational.",
                "Improves command routing and tactical response.",
                "Used for fortifications and armor upgrades.",
                "Shortens some research and training timers."
            },
            new[] { 12, 19, 26, 33, 40, 47 });
    }

    public void OpenToolkitGemStorePanel()
    {
        OpenInfoPopup("Gem Store", "Premium supplies", "Premium exchange is not open yet. We will wire this in after the battlefield pass.");
    }

    public void OpenToolkitRankPanel()
    {
        BindIfNeeded();
        if (root == null)
            return;

        CacheRankPopupElements();
        OpenPopup("rank-popup");
        SetRankHeader("S3 东线军演赛季", "排行榜同步中...");
        SetRankDetail("指挥官情报", "正在同步赛季军功榜...");
        PopulateRankLoadingState("正在加载排行榜...");

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.UiToolkitLoadLeaderboard((season, resetText, rows, current, status) =>
            {
                if (root == null)
                    return;

                PopulateRankPopup(season, resetText, rows, current, status);
            });
            return;
        }

        PopulateRankPopup(
            "S3 东线军演赛季",
            "离线模式榜单",
            new List<SimpleJson>(),
            null,
            "排行榜服务暂不可用");
    }

    public void OpenToolkitMailPanel()
    {
        LobbyManager.Instance?.UiToolkitRememberInboxSyncNotification();
        OpenToolkitNotifyPanel("邮件同步");
    }

    public void OpenToolkitGlobalConquestPanel()
    {
        OpenInfoPopup("Global Conquest", "Strategic map", "Global conquest is under construction and will use sea charts and island zones later.");
    }

    void CacheRankPopupElements()
    {
        if (root == null)
            return;

        rankPopupList = root.Q<ScrollView>("rank-popup-list");
        rankSeasonLabel = root.Q<Label>("rank-season-label");
        rankResetLabel = root.Q<Label>("rank-reset-label");
        rankDetailTitle = root.Q<Label>("rank-detail-title");
        rankDetailBody = root.Q<Label>("rank-detail-body");

        if (rankPopupList != null)
        {
            rankPopupList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            rankPopupList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rankPopupList.contentContainer.style.flexDirection = FlexDirection.Column;
            rankPopupList.contentContainer.style.alignItems = Align.Stretch;
        }
    }

    void CacheNotifyPopupElements()
    {
        if (root == null)
            return;

        notifyPopupList = root.Q<ScrollView>("notify-popup-list");
        notifyDetailTitle = root.Q<Label>("notify-detail-title");
        notifyDetailBody = root.Q<Label>("notify-popup-body");

        if (notifyPopupList != null)
        {
            notifyPopupList.verticalScrollerVisibility = ScrollerVisibility.Auto;
            notifyPopupList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            notifyPopupList.contentContainer.style.flexDirection = FlexDirection.Column;
            notifyPopupList.contentContainer.style.alignItems = Align.Stretch;
        }
    }

    void ShowNotifyList(bool visible)
    {
        CacheNotifyPopupElements();
        SetVisible(notifyPopupList, visible);
    }

    void PopulateRankLoadingState(string text)
    {
        CacheRankPopupElements();
        rankPopupRows.Clear();
        rankPopupData.Clear();
        rankPopupSelectedIndex = -1;
        if (rankPopupList == null)
            return;

        rankPopupList.contentContainer.Clear();
        AddScrollEmptyState(rankPopupList, text);
    }

    void PopulateRankPopup(string season, string resetText, List<SimpleJson> rows, SimpleJson current, string status)
    {
        CacheRankPopupElements();
        SetRankHeader(season, string.IsNullOrEmpty(status) ? resetText : resetText + " · " + status);
        SetRankDetail("指挥官情报", "选择一名指挥官查看赛季战绩。");

        rankPopupRows.Clear();
        rankPopupData.Clear();
        rankPopupSelectedIndex = -1;

        if (rankPopupList == null)
            return;

        rankPopupList.contentContainer.Clear();
        if (rows == null || rows.Count == 0)
        {
            AddScrollEmptyState(rankPopupList, string.IsNullOrEmpty(status) ? "暂无排行数据" : status);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var rowData = rows[i];
            rankPopupData.Add(rowData);
            var row = CreateRankPopupRow(rowData, i);
            rankPopupRows.Add(row);
            rankPopupList.contentContainer.Add(row);
        }

        int selected = FindRankSelectionIndex(rows, current);
        SelectRankPopupEntry(selected >= 0 ? selected : 0);
    }

    int FindRankSelectionIndex(List<SimpleJson> rows, SimpleJson current)
    {
        if (rows == null)
            return -1;

        string currentId = current != null ? (current.id ?? current.userId) : "";
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null)
                continue;
            if (row.isCurrentBool)
                return i;
            string rowId = row.id ?? row.userId;
            if (!string.IsNullOrEmpty(currentId) && rowId == currentId)
                return i;
        }

        return -1;
    }

    VisualElement CreateRankPopupRow(SimpleJson rowData, int index)
    {
        var row = new VisualElement();
        row.AddToClassList("rank-row");
        row.style.width = Length.Percent(100f);
        row.style.flexShrink = 0f;
        row.RegisterCallback<ClickEvent>(_ => SelectRankPopupEntry(index));

        var place = new Label("#" + Mathf.Max(1, rowData != null && rowData.place.HasValue ? rowData.place.Value : index + 1));
        place.AddToClassList("rank-row-place");

        var copy = new VisualElement();
        copy.AddToClassList("rank-row-copy");

        string name = GetLeaderboardName(rowData);
        var nameLabel = new Label(rowData != null && rowData.isCurrentBool ? name + "  ·  我" : name);
        nameLabel.AddToClassList("rank-row-name");

        int level = rowData != null && rowData.level.HasValue ? Mathf.Max(1, rowData.level.Value) : 1;
        string meta = "Lv." + level + " · " + GetLeaderboardRank(rowData)
            + " · " + Mathf.Max(0, rowData != null && rowData.wins.HasValue ? rowData.wins.Value : 0) + "胜/"
            + Mathf.Max(0, rowData != null && rowData.losses.HasValue ? rowData.losses.Value : 0) + "负";
        var metaLabel = new Label(meta);
        metaLabel.AddToClassList("rank-row-meta");

        copy.Add(nameLabel);
        copy.Add(metaLabel);

        var score = new Label(Mathf.Max(0, rowData != null && rowData.score.HasValue ? rowData.score.Value : 0).ToString());
        score.AddToClassList("rank-row-score");

        row.Add(place);
        row.Add(copy);
        row.Add(score);
        return row;
    }

    void SelectRankPopupEntry(int index)
    {
        if (index < 0 || index >= rankPopupData.Count)
            return;

        rankPopupSelectedIndex = index;
        for (int i = 0; i < rankPopupRows.Count; i++)
        {
            if (rankPopupRows[i] != null)
                rankPopupRows[i].EnableInClassList(SelectedClass, i == index);
        }

        var row = rankPopupData[index];
        SetRankDetail(GetLeaderboardName(row), BuildRankDetailBody(row));
    }

    void SetRankHeader(string season, string resetText)
    {
        if (rankSeasonLabel != null)
            rankSeasonLabel.text = string.IsNullOrEmpty(season) ? "S3 东线军演赛季" : season;
        if (rankResetLabel != null)
            rankResetLabel.text = resetText ?? "";
    }

    void SetRankDetail(string title, string body)
    {
        if (rankDetailTitle != null)
            rankDetailTitle.text = title ?? "";
        if (rankDetailBody != null)
            rankDetailBody.text = body ?? "";
    }

    static string BuildRankDetailBody(SimpleJson row)
    {
        int place = Mathf.Max(1, row != null && row.place.HasValue ? row.place.Value : 1);
        int wins = Mathf.Max(0, row != null && row.wins.HasValue ? row.wins.Value : 0);
        int losses = Mathf.Max(0, row != null && row.losses.HasValue ? row.losses.Value : 0);
        int score = Mathf.Max(0, row != null && row.score.HasValue ? row.score.Value : 0);
        string status = row != null && !string.IsNullOrWhiteSpace(row.status) ? row.status : "赛季活跃";
        return "第 " + place + " 名 · " + GetLeaderboardRank(row)
            + "\n战绩：" + wins + " 胜 / " + losses + " 负"
            + "\n军功：" + score + " · " + status;
    }

    static string GetLeaderboardName(SimpleJson row)
    {
        return row != null && !string.IsNullOrWhiteSpace(row.username) ? row.username : "未知指挥官";
    }

    static string GetLeaderboardRank(SimpleJson row)
    {
        if (row == null)
            return "列兵";
        if (!string.IsNullOrWhiteSpace(row.rankTitle))
            return row.rankTitle;
        if (!string.IsNullOrWhiteSpace(row.rank))
            return row.rank;
        return "列兵";
    }

    void PopulateNotifyPopup(List<SimpleJson> rows)
    {
        CacheNotifyPopupElements();
        ShowNotifyList(true);

        notifyPopupRows.Clear();
        notifyPopupData.Clear();
        notifyPopupSelectedIndex = -1;

        if (notifyPopupList == null)
            return;

        notifyPopupList.contentContainer.Clear();
        if (rows == null || rows.Count == 0)
        {
            AddScrollEmptyState(notifyPopupList, "暂无新消息，指挥部一切正常。");
            SetNotifyDetail("指挥部", "暂无新消息，指挥部一切正常。", "知道了");
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            notifyPopupData.Add(item);
            var row = CreateNotifyPopupRow(item, i);
            notifyPopupRows.Add(row);
            notifyPopupList.contentContainer.Add(row);
        }

        SelectNotifyPopupEntry(0);
    }

    VisualElement CreateNotifyPopupRow(SimpleJson item, int index)
    {
        var row = new VisualElement();
        row.AddToClassList("notify-row");
        row.style.width = Length.Percent(100f);
        row.style.flexShrink = 0f;
        row.RegisterCallback<ClickEvent>(_ => SelectNotifyPopupEntry(index));

        var copy = new VisualElement();
        copy.AddToClassList("notify-row-copy");

        var title = new Label(!string.IsNullOrWhiteSpace(item?.title) ? item.title : "通知");
        title.AddToClassList("notify-row-title");

        var body = new Label(!string.IsNullOrWhiteSpace(item?.body) ? item.body : "暂无详情");
        body.AddToClassList("notify-row-body");

        copy.Add(title);
        copy.Add(body);

        var status = new Label(!string.IsNullOrWhiteSpace(item?.status) ? item.status : "未读");
        status.AddToClassList("notify-row-status");

        row.Add(copy);
        row.Add(status);
        return row;
    }

    void SelectNotifyPopupEntry(int index)
    {
        if (index < 0 || index >= notifyPopupData.Count)
            return;

        notifyPopupSelectedIndex = index;
        for (int i = 0; i < notifyPopupRows.Count; i++)
        {
            if (notifyPopupRows[i] != null)
                notifyPopupRows[i].EnableInClassList(SelectedClass, i == index);
        }

        var item = notifyPopupData[index];
        SetNotifyDetail(
            !string.IsNullOrWhiteSpace(item?.title) ? item.title : "通知",
            !string.IsNullOrWhiteSpace(item?.body) ? item.body : "暂无详情",
            !string.IsNullOrWhiteSpace(item?.action) ? item.action : "知道了");
    }

    void SetNotifyDetail(string title, string body, string actionText)
    {
        if (notifyDetailTitle != null)
            notifyDetailTitle.text = title ?? "";
        if (notifyDetailBody != null)
            notifyDetailBody.text = body ?? "";
        var action = root?.Q<Button>("notify-popup-action");
        if (action != null)
            action.text = string.IsNullOrEmpty(actionText) ? "知道了" : actionText;
    }

    void HandleNotifyPopupAction()
    {
        if (notifyPopupSelectedIndex < 0 || notifyPopupSelectedIndex >= notifyPopupData.Count)
            return;

        var item = notifyPopupData[notifyPopupSelectedIndex];
        string action = item != null ? item.action ?? "" : "";
        string title = item != null ? item.title ?? "" : "";
        if ((action.Contains("房间") || title.Contains("邀请")) && LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitHasPendingInvite())
            LobbyManager.Instance.UiToolkitOpenPendingInviteRoomList();
    }

    void AddScrollEmptyState(ScrollView list, string text)
    {
        if (list == null)
            return;

        var empty = new Label(text ?? "");
        empty.AddToClassList("friends-popup-empty");
        empty.style.marginTop = 12f;
        empty.style.flexGrow = 1f;
        empty.style.minHeight = 56f;
        empty.style.unityTextAlign = TextAnchor.MiddleCenter;
        empty.style.whiteSpace = WhiteSpace.Normal;
        list.contentContainer.Add(empty);
    }

    public void OpenFriendsPopup()
    {
        BindIfNeeded();
        if (root == null)
            return;

        CacheFriendsPopupElements();
        OpenPopup("friends-popup");
        SetFriendsPopupStatus("加载好友中...");
        SetFriendsPopupDetail("好友名册", "正在同步完整好友列表...");

        if (LobbyManager.Instance != null && LobbyManager.Instance.MatchStatusText != null)
            LobbyManager.Instance.MatchStatusText.text = "好友列表已打开";

        var net = NetworkClient.Instance;
        if (net == null)
        {
            PopulateFriendsPopup(new List<SimpleJson>(), "好友服务暂不可用", "网络客户端尚未就绪。");
            return;
        }

        if (net.IsGuest || string.IsNullOrEmpty(net.Token))
        {
            PopulateFriendsPopup(new List<SimpleJson>(), "游客模式暂无好友数据", "登录后可以查看完整好友列表。");
            return;
        }

        net.GetFriendsData(friends =>
        {
            if (root == null)
                return;

            PopulateFriendsPopup(friends ?? new List<SimpleJson>(),
                friends != null && friends.Count > 0
                    ? $"已加载 {friends.Count} 位好友"
                    : "暂无好友，可以添加一位",
                friends != null && friends.Count > 0
                    ? "点击任意好友查看详情。"
                    : "还没有好友，先添加一位吧。");
        });
    }

    void PopulateFriendsPopup(List<SimpleJson> friends, string statusText, string detailText)
    {
        CacheFriendsPopupElements();

        SetFriendsPopupStatus(statusText);
        SetFriendsPopupDetail("好友名册", detailText);

        friendPopupRows.Clear();
        friendPopupData.Clear();
        friendPopupSelectedIndex = -1;

        if (friendsPopupList == null)
            return;

        friendsPopupList.contentContainer.Clear();

        if (friends == null || friends.Count == 0)
        {
            AddFriendsPopupEmptyState(statusText);
            return;
        }

        for (int i = 0; i < friends.Count; i++)
        {
            var friend = friends[i];
            friendPopupData.Add(friend);
            var row = CreateFriendPopupRow(friend, i);
            friendPopupRows.Add(row);
            friendsPopupList.contentContainer.Add(row);
        }

        SelectFriendPopupEntry(0);
    }

    void AddFriendsPopupEmptyState(string text)
    {
        if (friendsPopupList == null)
            return;

        var empty = new Label(text ?? "");
        empty.AddToClassList("friends-popup-empty");
        empty.style.marginTop = 12f;
        empty.style.flexGrow = 1f;
        empty.style.minHeight = 56f;
        empty.style.unityTextAlign = TextAnchor.MiddleCenter;
        empty.style.whiteSpace = WhiteSpace.Normal;
        friendsPopupList.contentContainer.Add(empty);
    }

    VisualElement CreateFriendPopupRow(SimpleJson friend, int index)
    {
        string name = GetFriendName(friend);
        string meta = GetFriendMeta(friend);
        string status = GetFriendStatus(friend);

        var row = new VisualElement();
        row.AddToClassList("friend-popup-row");
        row.style.width = Length.Percent(100f);
        row.style.height = 67f;
        row.style.marginBottom = 5f;
        row.style.flexShrink = 0f;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.RegisterCallback<ClickEvent>(_ => SelectFriendPopupEntry(index));

        var copy = new VisualElement();
        copy.AddToClassList("friend-popup-copy");
        copy.style.flexGrow = 1f;
        copy.style.flexShrink = 1f;
        copy.style.flexDirection = FlexDirection.Column;
        copy.style.minWidth = 0f;

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("friend-popup-name");

        var metaLabel = new Label("Lv." + Mathf.Max(1, friend != null && friend.level.HasValue ? friend.level.Value : 1) + " · " + meta);
        metaLabel.AddToClassList("friend-popup-meta");

        var statusLabel = new Label(status);
        statusLabel.AddToClassList("friend-popup-status");
        statusLabel.style.color = new StyleColor(GetFriendStatusColor(status));

        copy.Add(nameLabel);
        copy.Add(metaLabel);
        row.Add(copy);
        row.Add(statusLabel);
        return row;
    }

    void SelectFriendPopupEntry(int index)
    {
        if (index < 0 || index >= friendPopupData.Count)
            return;

        friendPopupSelectedIndex = index;
        for (int i = 0; i < friendPopupRows.Count; i++)
        {
            if (friendPopupRows[i] == null)
                continue;
            friendPopupRows[i].EnableInClassList(SelectedClass, i == index);
        }

        var friend = friendPopupData[index];
        SetFriendsPopupDetail(GetFriendName(friend), BuildFriendDetailBody(friend));
    }

    void SetFriendsPopupStatus(string text)
    {
        if (friendsPopupStatusLabel != null)
            friendsPopupStatusLabel.text = text ?? "";
    }

    void SetFriendsPopupDetail(string title, string body)
    {
        if (friendsPopupDetailTitle != null)
            friendsPopupDetailTitle.text = title ?? "";
        if (friendsPopupDetailBody != null)
            friendsPopupDetailBody.text = body ?? "";
    }

    static string BuildFriendDetailBody(SimpleJson friend)
    {
        string meta = GetFriendMeta(friend);
        string status = GetFriendStatus(friend);
        int level = friend != null && friend.level.HasValue ? Mathf.Max(1, friend.level.Value) : 1;
        return "Lv." + level + " · " + meta + "\n" + status;
    }

    static string GetFriendName(SimpleJson friend)
    {
        return friend != null && !string.IsNullOrWhiteSpace(friend.username) ? friend.username : "未知好友";
    }

    static string GetFriendMeta(SimpleJson friend)
    {
        if (friend == null)
            return "列兵";

        if (!string.IsNullOrWhiteSpace(friend.rank))
            return friend.rank;

        if (!string.IsNullOrWhiteSpace(friend.rankTitle))
            return friend.rankTitle;

        return "列兵";
    }

    static string GetFriendStatus(SimpleJson friend)
    {
        return friend != null && !string.IsNullOrWhiteSpace(friend.status) ? friend.status : "离线";
    }

    static Color GetFriendStatusColor(string status)
    {
        string lower = string.IsNullOrEmpty(status) ? "" : status.ToLowerInvariant();
        if (lower.Contains("off") || lower.Contains("离线"))
            return new Color(0.62f, 0.67f, 0.72f);
        if (lower.Contains("game") || lower.Contains("match") || lower.Contains("busy") || lower.Contains("游戏"))
            return new Color(1f, 0.76f, 0.32f);
        return new Color(0.44f, 0.92f, 0.58f);
    }

    public void SetFriendSlot(int index, string name, string meta, string status, int level)
    {
        BindIfNeeded();
        if (root == null || index < 0)
            return;

        bool hasFriend = !string.IsNullOrEmpty(name);
        SetLabel("friend-name-" + index, name);
        SetLabel("friend-meta-" + index, meta);
        SetLabel("friend-status-" + index, status);
        SetLabel("friend-level-" + index, hasFriend && level > 0 ? "Lv." + Mathf.Max(1, level) : "");
    }

    public void SetTaskSlot(int index, string title, int current, int max, bool claimed, bool canClaim)
    {
        BindIfNeeded();
        if (root == null || index < 0)
            return;

        int safeMax = Mathf.Max(1, max);
        int safeCurrent = Mathf.Clamp(current, 0, safeMax);
        bool hasTask = !string.IsNullOrEmpty(title);

        SetLabel("task-title-" + index, title);
        SetLabel("task-progress-" + index, hasTask ? safeCurrent + "/" + safeMax : "");
        SetProgressFill("task-fill-" + index, hasTask ? safeCurrent / (float)safeMax : 0f);
        SetTaskClaimState(index, claimed, canClaim);
    }

    public void SetTaskClaimState(int index, bool claimed, bool canClaim)
    {
        BindIfNeeded();
        if (root == null || index < 0)
            return;

        string text = claimed ? "Claimed" : (canClaim ? "Claim" : "Go");
        SetLabel("task-claim-label-" + index, text);

        var label = root.Q<Label>("task-claim-label-" + index);
        if (label != null)
        {
            label.style.color = claimed
                ? new StyleColor(new Color(0.68f, 0.72f, 0.66f, 1f))
                : new StyleColor(canClaim ? new Color(1f, 0.84f, 0.30f, 1f) : new Color(0.72f, 0.86f, 1f, 1f));
        }
    }

    public void SetTechState(string title, string desc, string timerText, float progress)
    {
        BindIfNeeded();
        if (root == null)
            return;

        SetLabel("tech-name", title);
        SetLabel("tech-desc", desc);
        SetLabel("tech-timer", timerText);
        SetLabel("tech-popup-body", string.IsNullOrEmpty(desc) ? title : title + "\n" + desc + "\n" + timerText);
        SetProgressFill("tech-fill", progress);
    }

    void PopulateWarehousePopup(string status, string[] names, string[] descs, int[] counts)
    {
        warehouseItemNames = names ?? new string[0];
        warehouseItemDescs = descs ?? new string[0];
        warehouseItemCounts = counts ?? new int[0];
        warehouseSelectedIndex = -1;

        SetLabel("warehouse-status-label", string.IsNullOrEmpty(status) ? "Supplies synced" : status);

        for (int i = 0; i < WarehouseItemCount; i++)
        {
            bool hasItem = HasWarehouseItem(i);
            SetVisible(root?.Q<VisualElement>("warehouse-item-" + i), hasItem);
            SetLabel("warehouse-item-name-" + i, hasItem ? warehouseItemNames[i] : "");
            SetLabel("warehouse-item-desc-" + i, hasItem ? GetWarehouseDesc(i) : "");
            SetLabel("warehouse-item-count-" + i, hasItem ? "x" + GetWarehouseCount(i) : "");

            var detailButton = root?.Q<Button>("warehouse-detail-button-" + i);
            if (detailButton != null)
                detailButton.SetEnabled(hasItem);
        }

        if (HasWarehouseItem(0))
            SelectWarehouseEntry(0);
        else
            SetWarehouseDetail("Supply Details", "No supply items are available right now.");
    }

    void SelectWarehouseEntry(int index)
    {
        if (!HasWarehouseItem(index))
            return;

        warehouseSelectedIndex = index;
        for (int i = 0; i < WarehouseItemCount; i++)
        {
            var row = root?.Q<VisualElement>("warehouse-item-" + i);
            if (row != null)
                row.EnableInClassList(SelectedClass, i == warehouseSelectedIndex);
        }

        string desc = GetWarehouseDesc(index);
        int count = GetWarehouseCount(index);
        SetWarehouseDetail(warehouseItemNames[index], desc + "\nStock: " + count);
    }

    void SetWarehouseDetail(string title, string body)
    {
        SetLabel("warehouse-detail-title", title);
        SetLabel("warehouse-detail-body", body);
    }

    bool HasWarehouseItem(int index)
    {
        return warehouseItemNames != null
            && index >= 0
            && index < warehouseItemNames.Length
            && !string.IsNullOrEmpty(warehouseItemNames[index]);
    }

    string GetWarehouseDesc(int index)
    {
        return warehouseItemDescs != null && index >= 0 && index < warehouseItemDescs.Length
            ? warehouseItemDescs[index] ?? ""
            : "";
    }

    int GetWarehouseCount(int index)
    {
        return warehouseItemCounts != null && index >= 0 && index < warehouseItemCounts.Length
            ? Mathf.Max(0, warehouseItemCounts[index])
            : 0;
    }
}
