using System.Collections.Generic;
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
    readonly List<VisualElement> friendPopupRows = new List<VisualElement>();
    readonly List<SimpleJson> friendPopupData = new List<SimpleJson>();
    int friendPopupSelectedIndex = -1;

    const int MatchMapButtonCount = 5;
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

        HideAllPopups();
        RefreshMapButtons();
        RefreshSettingsRuntimeState();
        CacheFriendsPopupElements();

        if (bound)
            return;
        bound = true;

        RegisterClose("modal-backdrop-button");
        RegisterClose("notify-popup-close");
        RegisterClose("notify-popup-action");
        RegisterClose("tech-popup-close");
        RegisterClose("match-popup-close");
        RegisterClose("warehouse-popup-close");
        RegisterClose("room-popup-close");
        RegisterClose("invite-popup-close");
        RegisterClose("friends-popup-close");
        RegisterClose("settings-popup-close");

        RegisterOpen("settings-button", OpenSettingsPopup);
        RegisterOpen("quick-match-button", OpenMatchPopup);
        RegisterOpen("notify-button", OpenToolkitNotifyPanel);
        RegisterOpen("gold-plus-button", OpenToolkitShopPanel);
        RegisterOpen("gem-plus-button", OpenToolkitGemStorePanel);
        RegisterOpen("rank-nav-button", OpenToolkitRankPanel);
        RegisterOpen("mail-nav-button", OpenToolkitMailPanel);
        RegisterOpen("global-conquest-button", OpenToolkitGlobalConquestPanel);
        RegisterOpen("friends-button", OpenFriendsPopup);
        RegisterOpen("tech-gear-button", OpenTechPopup);
        RegisterOpen("tech-tree-button", OpenTechPopup);

        for (int i = 0; i < MatchMapButtonCount; i++)
        {
            int mapIndex = i;
            var mapButton = root.Q<Button>("match-map-button-" + i);
            if (mapButton != null)
                mapButton.clicked += () => SelectMatchMap(mapIndex);
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
        SetLabel("notify-popup-body", body);

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

        SetLabel("tech-popup-title", "Tech Center");
        SetLabel("tech-popup-subtitle", "Research queue");
        OpenPopup("tech-popup");
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
        SetLabel("match-status-label", "已锁定战区，点击开始匹配");
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
        SetLabel("match-status-label", "匹配中：正在搜索对手");
        SetLabel("match-hint-label", "正在连接对手，匹配成功后自动进入战场。");
        SetButtonText("match-start-button", "开始匹配");
        SetButtonText("match-cancel-button", "取消匹配");
        RefreshMatchRuntimeState();
    }

    void CancelOrReturnFromMatch()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.UiToolkitIsMatching())
            LobbyManager.Instance.UiToolkitCancelMatch();

        SetLabel("match-status-label", "选择战区后开始匹配");
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

        if (matching)
            SetLabel("match-status-label", "匹配中：正在搜索对手");
    }

    public void OpenToolkitNotifyPanel()
    {
        string body = LobbyManager.Instance != null
            ? LobbyManager.Instance.UiToolkitBuildNotificationSummary()
            : "暂无新消息，指挥部一切正常。";
        OpenNotificationPopup("战场快报", body);
    }

    public void OpenToolkitShopPanel()
    {
        OpenInfoPopup("Supply Shop", "Coin supplies", "The supply shop is being connected. For now, earn resources through battles.");
    }

    public void OpenToolkitGemStorePanel()
    {
        OpenInfoPopup("Gem Store", "Premium supplies", "Premium exchange is not open yet. We will wire this in after the battlefield pass.");
    }

    public void OpenToolkitRankPanel()
    {
        OpenInfoPopup("Rankings", "Season records", "Season rankings are syncing.");
    }

    public void OpenToolkitMailPanel()
    {
        LobbyManager.Instance?.UiToolkitRememberInboxSyncNotification();
        OpenNotificationPopup("邮件同步", LobbyManager.InboxSyncNotificationMessage);
    }

    public void OpenToolkitGlobalConquestPanel()
    {
        OpenInfoPopup("Global Conquest", "Strategic map", "Global conquest is under construction and will use sea charts and island zones later.");
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
}
