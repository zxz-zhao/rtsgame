using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;

// HTTP 瀹㈡埛绔細灏佽鎵€鏈夋湇鍔＄ API 璋冪敤
public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance { get; private set; }
    const string NetworkUnavailableError = "NETWORK_UNAVAILABLE";

    // 鏈嶅姟鍣ㄥ湴鍧€锛歁uMu 妯℃嫙鍣ㄨ闂涓绘満鐢?10.0.2.2锛屽眬鍩熺綉鐢ㄥ疄闄?IP
    public string ServerUrl = "http://10.0.2.2:8080";

    // 褰撳墠鐧诲綍淇℃伅
    public string Token    { get; private set; }
    public string UserId   { get; private set; }
    public string UserName { get; private set; }
    public int    Level    { get; private set; } = 1;
    public int    Wins     { get; private set; } = 0;
    public int    Losses   { get; private set; } = 0;
    public bool   IsGuest  { get; private set; }
    public int    Gold     { get; private set; } = 0;
    public int    Gems     { get; private set; } = 0;
    public string RankTitle { get; private set; } = "";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 浠?PlayerPrefs 鎭㈠浼氳瘽
        Token    = PlayerPrefs.GetString("net_token", "");
        UserId   = PlayerPrefs.GetString("net_userid", "");
        UserName = PlayerPrefs.GetString("net_username", "");
        Level    = PlayerPrefs.GetInt("net_level", 1);
        Wins     = PlayerPrefs.GetInt("net_wins",   0);
        Losses   = PlayerPrefs.GetInt("net_losses", 0);
        IsGuest  = PlayerPrefs.GetInt("net_isguest", 1) == 1;
        Gold     = PlayerPrefs.GetInt("net_gold", 0);
        Gems     = PlayerPrefs.GetInt("net_gems", 0);
        RankTitle = PlayerPrefs.GetString("net_rank_title", "");

#if UNITY_ANDROID && !UNITY_EDITOR
        ServerUrl = "http://10.0.2.2:8080";
#else
        ServerUrl = "http://127.0.0.1:8080";
#endif
    }

    // 鈹€鈹€ 淇濆瓨浼氳瘽 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
    public void SetTestSession(string username, string userId)
        => SaveSession("test-token-local", userId, username, 1, false, 0, 0, 0, 0, "");

    void SaveSession(string token, string userId, string username, int level, bool isGuest,
        int wins = 0, int losses = 0, int gold = 0, int gems = 0, string rankTitle = "")
    {
        Token = token; UserId = userId; UserName = username; Level = level; IsGuest = isGuest;
        Wins = wins; Losses = losses;
        Gold = gold; Gems = gems; RankTitle = rankTitle ?? "";
        PlayerPrefs.SetString("net_token",    token);
        PlayerPrefs.SetString("net_userid",   userId);
        PlayerPrefs.SetString("net_username", username);
        PlayerPrefs.SetInt   ("net_level",    level);
        PlayerPrefs.SetInt   ("net_wins",     wins);
        PlayerPrefs.SetInt   ("net_losses",   losses);
        PlayerPrefs.SetInt   ("net_isguest",  isGuest ? 1 : 0);
        PlayerPrefs.SetInt   ("net_gold",     Gold);
        PlayerPrefs.SetInt   ("net_gems",     Gems);
        PlayerPrefs.SetString("net_rank_title", RankTitle);
        PlayerPrefs.SetString("current_user", username);
        PlayerPrefs.Save();
    }

    public void SetEconomy(int gold, int gems)
    {
        Gold = gold;
        Gems = gems;
        PlayerPrefs.SetInt("net_gold", Gold);
        PlayerPrefs.SetInt("net_gems", Gems);
        PlayerPrefs.Save();
    }

    public void SetRankTitle(string r)
    {
        RankTitle = r ?? "";
        PlayerPrefs.SetString("net_rank_title", RankTitle);
        PlayerPrefs.Save();
    }

    public void ClearSession()
    {
        Token = UserId = UserName = "";
        Level = 1; Wins = 0; Losses = 0; IsGuest = true; Gold = 0; Gems = 0; RankTitle = "";
        PlayerPrefs.DeleteKey("net_token");
        PlayerPrefs.DeleteKey("net_userid");
        PlayerPrefs.DeleteKey("net_username");
        PlayerPrefs.DeleteKey("net_level");
        PlayerPrefs.DeleteKey("net_wins");
        PlayerPrefs.DeleteKey("net_losses");
        PlayerPrefs.DeleteKey("net_isguest");
        PlayerPrefs.DeleteKey("net_gold");
        PlayerPrefs.DeleteKey("net_gems");
        PlayerPrefs.DeleteKey("net_rank_title");
        PlayerPrefs.DeleteKey("current_user");
        PlayerPrefs.Save();
    }

    // ============================================================
    //  AUTH
    // ============================================================
    public void Register(string username, string password, Action<bool, string> cb)
        => StartCoroutine(Post("/api/register",
            $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}",
            json => {
                if (json.success)
                {
                    SaveSession(json.token, json.userId, json.username, 1, false, 0, 0,
                        json.gold ?? 1000, json.gems ?? 100, json.rankTitle ?? "列兵");
                    cb(true, "");
                    return;
                }

                if (IsNetworkUnavailable(json))
                {
                    SaveLocalSession(username, false);
                    cb(true, "本地注册模式");
                    return;
                }

                cb(false, json.error);
            }));

    public void Login(string username, string password, Action<bool, string> cb)
        => StartCoroutine(Post("/api/login",
            $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}",
            json => {
                if (json.success)
                {
                    SaveSession(json.token, json.userId, json.username,
                        json.level ?? 1, false, json.wins ?? 0, json.losses ?? 0,
                        json.gold ?? 0, json.gems ?? 0, json.rankTitle ?? "");
                    cb(true, "");
                    return;
                }

                if (IsNetworkUnavailable(json))
                {
                    SaveLocalSession(username, false);
                    cb(true, "本地登录模式");
                    return;
                }

                cb(false, json.error);
            }));

    public void GuestLogin(Action<bool, string> cb, string preferredDisplayName = null)
    {
        string body = "{}";
        if (!string.IsNullOrEmpty(preferredDisplayName))
        {
            string safe = preferredDisplayName.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
            body = "{\"displayName\":\"" + safe + "\"}";
        }
        StartCoroutine(Post("/api/guest", body,
            json => {
                if (json.success)
                {
                    SaveSession(json.token, json.userId, json.username, 1, true, 0, 0,
                        json.gold ?? 0, json.gems ?? 0, json.rankTitle ?? "列兵");
                    cb(true, json.username);
                    return;
                }

                if (IsNetworkUnavailable(json))
                {
                    string localName = string.IsNullOrWhiteSpace(preferredDisplayName) ? "游客" : preferredDisplayName.Trim();
                    SaveLocalSession(localName, true);
                    cb(true, localName);
                    return;
                }

                cb(false, json.error);
            }));
    }

    public void GuestLoginLocal(Action<bool, string> cb, string preferredDisplayName = null)
    {
        string localName = string.IsNullOrWhiteSpace(preferredDisplayName) ? "游客" : preferredDisplayName.Trim();
        SaveLocalSession(localName, true);
        cb?.Invoke(true, localName);
    }

    static bool IsNetworkUnavailable(SimpleJson json)
    {
        return json != null && json.error == NetworkUnavailableError;
    }

    void SaveLocalSession(string username, bool isGuest)
    {
        string safeName = string.IsNullOrWhiteSpace(username) ? (isGuest ? "游客" : "Commander") : username.Trim();
        string idPrefix = isGuest ? "guest-local-" : "user-local-";
        SaveSession("test-token-local", idPrefix + Mathf.Abs(safeName.GetHashCode()), safeName, 1, isGuest, 0, 0, 1000, 100, "列兵");
    }

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    //  濂藉弸
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    public void GetFriends(Action<string> cb)
        => StartCoroutine(Get("/api/friends", json => {
            if (!json.success) { cb("Failed to load friends"); return; }
            var sb = new StringBuilder();
            if (json.friends == null || json.friends.Count == 0) { cb("-- No Friends --"); return; }
            foreach (var f in json.friends)
            {
                string st = f.status ?? "Offline";
                string lower = st.ToLowerInvariant();
                string col = lower.Contains("off") ? "#8899aa" : ((lower.Contains("game") || lower.Contains("busy") || lower.Contains("match")) ? "#ffb347" : "#56f078");
                string rk = string.IsNullOrEmpty(f.rank) ? "" : ("  " + f.rank);
                sb.Append("<b>").Append(f.username ?? "").Append("</b>  Lv.").Append(f.level).Append(rk)
                    .Append("\n  <color=").Append(col).Append(">").Append(st).Append("</color>\n\n");
            }
            cb(sb.ToString());
        }));

    public void GetFriendsData(Action<System.Collections.Generic.List<SimpleJson>> cb)
        => StartCoroutine(Get("/api/friends", json => {
            if (!json.success) { cb(new System.Collections.Generic.List<SimpleJson>()); return; }
            cb(json.friends ?? new System.Collections.Generic.List<SimpleJson>());
        }));

    public void GetFriendNames(Action<string[]> cb)
        => StartCoroutine(Get("/api/friends", json => {
            if (!json.success || json.friends == null || json.friends.Count == 0) { cb(new string[0]); return; }
            var names = new string[json.friends.Count];
            for (int i = 0; i < json.friends.Count; i++)
                names[i] = json.friends[i].username ?? "";
            cb(names);
        }));

    public void AddFriend(string friendName, Action<bool, string> cb)
        => StartCoroutine(Post("/api/friends/add",
            $"{{\"friendName\":\"{friendName}\"}}",
            json => cb(json.success, json.success ? "Friend added" : json.error)));

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    //  鍖归厤
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    public void JoinMatch(string mapName, Action<bool, string> cb)
        => StartCoroutine(Post("/api/match/join",
            $"{{\"mapName\":\"{mapName}\"}}",
            json => {
                bool matched = json.matched == true;
                cb(matched, matched ? (json.roomId ?? "") : "");
            }));

    public void CancelMatch(Action cb = null)
        => StartCoroutine(Post("/api/match/cancel", "{}", _ => cb?.Invoke()));

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    //  鑷畾涔夋埧闂?    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    public void GetRooms(Action<System.Collections.Generic.List<SimpleJson>> cb)
        => StartCoroutine(Get("/api/rooms",
            json => cb(json.rooms ?? new System.Collections.Generic.List<SimpleJson>())));

    public void CreateRoom(string mapName, Action<bool, string> cb, string roomName = "")
        => StartCoroutine(Post("/api/rooms/create",
            $"{{\"mapName\":\"{mapName}\",\"roomName\":\"{roomName}\"}}",
            json => cb(json.success, json.success ? (json.roomId ?? "") : (json.error ?? "鍒涘缓澶辫触"))));

    public void JoinRoom(string roomId, Action<bool, string> cb)
        => StartCoroutine(Post("/api/rooms/join",
            $"{{\"roomId\":\"{roomId}\"}}",
            json => cb(json.success, json.success ? (json.mapName ?? "") : (json.error ?? "Join failed"))));

    public void LeaveRoom(string roomId, Action cb = null)
        => StartCoroutine(Post("/api/rooms/leave",
            $"{{\"roomId\":\"{roomId}\"}}",
            _ => cb?.Invoke()));

    public void InviteFriend(string friendName, string roomId, Action<bool, string> cb)
        => StartCoroutine(Post("/api/friends/invite",
            $"{{\"friendName\":\"{friendName}\",\"roomId\":\"{roomId}\"}}",
            json => cb(json.success, json.success ? "" : (json.error ?? "Invite failed"))));

    public void GetInvites(Action<InviteInfo[]> cb)
        => StartCoroutine(Get("/api/invites", json => {
            if (!json.success || json.invites == null) { cb(new InviteInfo[0]); return; }
            var list = new InviteInfo[json.invites.Count];
            for (int i = 0; i < json.invites.Count; i++)
            {
                var v = json.invites[i];
                list[i] = new InviteInfo { id = v.id ?? "", from = v.from ?? "",
                                           roomId = v.roomId ?? "", mapName = v.mapName ?? "" };
            }
            cb(list);
        }));

    public void RespondInvite(string inviteId, bool accept, Action<bool, string> cb)
        => StartCoroutine(Post("/api/invites/respond",
            $"{{\"inviteId\":\"{inviteId}\",\"accept\":{(accept ? "true" : "false")}}}",
            json => cb(json.success, json.success ? (json.roomId ?? "") : (json.error ?? ""))));

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    //  澶у巺锛堣揣甯?/ 浠诲姟 / 绉戞妧锛?    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    public void GetLobby(Action<SimpleJson> cb) => StartCoroutine(GetLobbyCo(cb));

    public void GetLeaderboard(Action<SimpleJson> cb)
        => StartCoroutine(Get("/api/leaderboard", cb));

    IEnumerator GetLobbyCo(Action<SimpleJson> cb)
    {
        var req = UnityWebRequest.Get(ServerUrl + "/api/lobby");
        req.timeout = 12;
        if (!string.IsNullOrEmpty(Token))
            req.SetRequestHeader("Authorization", "Bearer " + Token);
        yield return req.SendWebRequest();
        HandleResponse(req, cb);
    }

    public void PostPresence(Action cb = null)
        => StartCoroutine(Post("/api/presence", "{}", _ => cb?.Invoke()));

    public void ClaimTask(string taskId, Action<SimpleJson> cb)
        => StartCoroutine(Post("/api/tasks/claim", "{\"taskId\":\"" + taskId + "\"}", cb));

    public void StartTechResearch(Action<SimpleJson> cb)
        => StartCoroutine(Post("/api/tech/start", "{}", cb));

    public void SpeedUpTech(Action<SimpleJson> cb)
        => StartCoroutine(Post("/api/tech/speedup", "{}", cb));

    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    //  HTTP 鍩虹鏂规硶
    // 鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲鈺愨晲
    IEnumerator Get(string path, Action<SimpleJson> cb)
    {
        var req = UnityWebRequest.Get(ServerUrl + path);
        req.timeout = 3;
        if (!string.IsNullOrEmpty(Token))
            req.SetRequestHeader("Authorization", "Bearer " + Token);
        yield return req.SendWebRequest();
        HandleResponse(req, cb);
    }

    IEnumerator Post(string path, string body, Action<SimpleJson> cb)
    {
        var req = new UnityWebRequest(ServerUrl + path, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 3;
        req.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(Token))
            req.SetRequestHeader("Authorization", "Bearer " + Token);
        yield return req.SendWebRequest();
        HandleResponse(req, cb);
    }

    void HandleResponse(UnityWebRequest req, Action<SimpleJson> cb)
    {
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Network error {req.url}: {req.error}");
            if (req.responseCode == 401) { Token = ""; PlayerPrefs.DeleteKey("net_token"); }
            bool connectionFailed = req.result == UnityWebRequest.Result.ConnectionError || req.responseCode == 0;
            cb(new SimpleJson { success = false, error = connectionFailed ? NetworkUnavailableError : req.error });
            return;
        }
        SimpleJson json;
        try {
            json = SimpleJson.Parse(req.downloadHandler.text);
        } catch (Exception e) {
            Debug.LogError($"JSON parse error: {e.Message}\n{req.downloadHandler.text}");
            cb(new SimpleJson { success = false, error = "Failed to parse server response" });
            return;
        }
        cb(json);
    }

    public void ReportMatchResult(bool win, int kills, int duration)
    {
        if (string.IsNullOrEmpty(Token)) return;
        string body = "{\"win\":" + (win ? "1" : "0") + ",\"kills\":" + kills + ",\"duration\":" + duration + "}";
        StartCoroutine(Post("/api/result", body, _ => {}));
    }
}

// 鈹€鈹€ 鏋佺畝 JSON 瑙ｆ瀽鍣紙閬垮厤寮曞叆绗笁鏂瑰簱锛夆攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
public class SimpleJson
{
    public bool   success;
    public string error;
    public string token;
    public string userId;
    public string username;
    public int?   level;
    public int?   wins;
    public int?   losses;
    public bool?  matched;
    public bool?  isGuest;
    public string roomId;
    public string mapName;
    public string id;
    public string name;
    public int?   maxPlayers;
    public int?   playerCount;
    public System.Collections.Generic.List<SimpleJson> friends;
    public System.Collections.Generic.List<SimpleJson> rooms;
    public System.Collections.Generic.List<SimpleJson> invites;
    public string from;
    public string fromId;
    public string inviteId;
    public int?   gold;
    public int?   gems;
    public string rankTitle;
    public string title;
    public string rank;
    public string status;
    public string season;
    public string resetText;
    public string body;
    public string action;
    public int?   cur;
    public int?   max;
    public int?   place;
    public int?   score;
    public int?   kills;
    public bool   isCurrentBool;
    public bool   claimedBool;
    public bool   canClaimBool;
    public string techId;
    public string techName;
    public string techDesc;
    public long?  techEndAtMs;
    public int?   techTotalSec;
    public System.Collections.Generic.List<SimpleJson> tasks;
    public System.Collections.Generic.List<SimpleJson> leaderboard;
    public SimpleJson current;

    public static SimpleJson Parse(string json)
    {
        var obj = new SimpleJson();
        obj.success  = GetBool(json, "success");
        obj.error    = GetStr(json, "error");
        obj.token    = GetStr(json, "token");
        obj.userId   = GetStr(json, "userId");
        obj.username = GetStr(json, "username");
        obj.roomId     = GetStr(json, "roomId");
        obj.mapName    = GetStr(json, "mapName");
        obj.id         = GetStr(json, "id");
        obj.name       = GetStr(json, "name");
        obj.maxPlayers = GetInt(json, "maxPlayers");
        obj.playerCount = CountArrayItems(json, "players");
        obj.level    = GetInt(json, "level");
        obj.wins     = GetInt(json, "wins");
        obj.losses   = GetInt(json, "losses");
        obj.gold     = GetInt(json, "gold");
        obj.gems     = GetInt(json, "gems");
        obj.rankTitle = GetStr(json, "rankTitle");
        obj.title    = GetStr(json, "title");
        obj.rank     = GetStr(json, "rank");
        obj.status   = GetStr(json, "status");
        obj.season   = GetStr(json, "season");
        obj.resetText = GetStr(json, "resetText");
        obj.body     = GetStr(json, "body");
        obj.action   = GetStr(json, "action");
        obj.cur      = GetInt(json, "cur");
        obj.max      = GetInt(json, "max");
        obj.place    = GetInt(json, "place");
        obj.score    = GetInt(json, "score");
        obj.kills    = GetInt(json, "kills");
        obj.isCurrentBool = GetStr(json, "isCurrent") == "true";
        obj.claimedBool = GetStr(json, "claimed") == "true";
        obj.canClaimBool = GetStr(json, "canClaim") == "true";
        obj.techId   = GetStr(json, "techId");
        obj.techName = GetStr(json, "techName");
        obj.techDesc = GetStr(json, "techDesc");
        obj.techTotalSec = GetInt(json, "techTotalSec");
        var techEndStr = GetStr(json, "techEndAt");
        if (!string.IsNullOrEmpty(techEndStr) && long.TryParse(techEndStr, out long te))
            obj.techEndAtMs = te;
        string matchedStr = GetStr(json, "matched");
        if (matchedStr == "true") obj.matched = true;
        else if (matchedStr == "false") obj.matched = false;
        string guestStr = GetStr(json, "isGuest");
        if (guestStr == "true") obj.isGuest = true;
        obj.friends  = ParseArray(json, "friends");
        obj.rooms    = ParseArray(json, "rooms");
        obj.invites  = ParseArray(json, "invites");
        obj.tasks    = ParseArray(json, "tasks");
        obj.leaderboard = ParseArray(json, "leaderboard");
        obj.current  = ParseObject(json, "current");
        obj.from     = GetStr(json, "from");
        obj.fromId   = GetStr(json, "fromId");
        obj.inviteId = GetStr(json, "inviteId");
        return obj;
    }

    static string GetStr(string json, string key)
    {
        var pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return null;
        int colon = json.IndexOf(':', idx);
        if (colon < 0) return null;
        // 璺宠繃鍐掑彿鍚庣殑绌虹櫧
        int vs = colon + 1;
        while (vs < json.Length && (json[vs] == ' ' || json[vs] == '\n' || json[vs] == '\r')) vs++;
        if (vs >= json.Length) return null;
        // 鍒ゆ柇鍊肩被鍨嬶細鑻ラ瀛楃鏄?'"' 鍒欒瀛楃涓诧紝鍚﹀垯璇?bool/鏁板瓧/null
        if (json[vs] == '"') {
            int start = vs;
            int end = json.IndexOf('"', start + 1);
            return end > start ? json.Substring(start + 1, end - start - 1) : null;
        } else {
            int ve = vs;
            while (ve < json.Length && json[ve] != ',' && json[ve] != '}' && json[ve] != ']') ve++;
            return json.Substring(vs, ve - vs).Trim();
        }
    }

    static bool GetBool(string json, string key)
        => GetStr(json, key) == "true";

    static int? CountArrayItems(string json, string key)
    {
        var pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return null;
        int start = json.IndexOf('[', idx);
        if (start < 0) return null;
        int end = start + 1;
        int depth = 1;
        while (end < json.Length && depth > 0) {
            if (json[end] == '[') depth++;
            else if (json[end] == ']') depth--;
            end++;
        }
        string inner = json.Substring(start + 1, end - start - 2).Trim();
        if (string.IsNullOrEmpty(inner)) return 0;
        int count = 1, d = 0;
        foreach (char c in inner) {
            if (c == '[' || c == '{') d++;
            else if (c == ']' || c == '}') d--;
            else if (c == ',' && d == 0) count++;
        }
        return count;
    }

    static int? GetInt(string json, string key)
    {
        string v = GetStr(json, key);
        return int.TryParse(v, out int r) ? r : (int?)null;
    }

    static System.Collections.Generic.List<SimpleJson> ParseArray(string json, string key)
    {
        var list = new System.Collections.Generic.List<SimpleJson>();
        var pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return list;
        int start = json.IndexOf('[', idx);
        if (start < 0) return list;
        int depth = 0; int objStart = -1;
        for (int i = start; i < json.Length; i++) {
            if (json[i] == '{') { if (depth == 1) objStart = i; depth++; }
            else if (json[i] == '}') {
                depth--;
                if (depth == 1 && objStart >= 0) {
                    list.Add(Parse(json.Substring(objStart, i - objStart + 1)));
                    objStart = -1;
                }
            } else if (json[i] == '[') depth++;
            else if (json[i] == ']') { if (depth == 1) break; depth--; }
        }
        return list;
    }

    static SimpleJson ParseObject(string json, string key)
    {
        var pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return null;
        int start = json.IndexOf('{', idx);
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escaping = false;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (escaping) escaping = false;
                else if (c == '\\') escaping = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return Parse(json.Substring(start, i - start + 1));
            }
        }

        return null;
    }
}

public struct InviteInfo
{
    public string id;
    public string from;
    public string roomId;
    public string mapName;
}
