using Godot;
using System.Threading.Tasks;


public partial class NetClient : Node
{
    [Signal]
    public delegate void RequestFinishedEventHandler(string path, Godot.Collections.Dictionary data);

    [Signal]
    public delegate void RequestFailedEventHandler(string path, string error);

    public const string NetworkUnavailable = "NETWORK_UNAVAILABLE";

    [Export]
    public string ServerUrl { get; set; } = "http://127.0.0.1:8080";

    [Export]
    public double RequestTimeoutSec { get; set; } = 5.0;

    [Export]
    public string ClientSigKey { get; set; } = "dev-signature-key-change-me";

    [Export]
    public int ClientBuildNumber { get; set; } = 20260709;

    public static NetClient? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        if (OS.HasFeature("android"))
            ServerUrl = "http://10.0.2.2:8080";
    }

    public Task<Godot.Collections.Dictionary> GuestLogin(string displayName = "")
    {
        var body = new Godot.Collections.Dictionary();
        if (!string.IsNullOrWhiteSpace(displayName))
            body["displayName"] = displayName.Trim();
        return PostJson("/api/guest", body);
    }

    public Task<Godot.Collections.Dictionary> Login(string username, string password)
        => PostJson("/api/login", new Godot.Collections.Dictionary
        {
            ["username"] = username,
            ["password"] = password
        });

    public Task<Godot.Collections.Dictionary> Register(string username, string password, string idCard = "")
        => PostJson("/api/register", new Godot.Collections.Dictionary
        {
            ["username"] = username,
            ["password"] = password,
            ["idCard"] = idCard
        });

    public Task<Godot.Collections.Dictionary> GetLobby()
        => GetJson("/api/lobby");

    public Task<Godot.Collections.Dictionary> PostPresence()
        => PostJson("/api/presence", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> ClaimTask(string taskId)
        => PostJson("/api/tasks/claim", new Godot.Collections.Dictionary { ["taskId"] = taskId });

    public Task<Godot.Collections.Dictionary> ClaimMailReward(string mailId, int gold, int gems)
        => PostJson("/api/mail/claim", new Godot.Collections.Dictionary
        {
            ["mailId"] = mailId,
            ["gold"] = gold,
            ["gems"] = gems
        });

    public Task<Godot.Collections.Dictionary> StartTechResearch(string techKey = "", string mode = "match")
        => PostJson("/api/tech/start", new Godot.Collections.Dictionary { ["techKey"] = techKey, ["mode"] = mode });

    public Task<Godot.Collections.Dictionary> SpeedUpTech()
        => PostJson("/api/tech/speedup", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> GetFriends(double? timeoutSec = null)
        => GetJson("/api/friends", timeoutSec);

    public Task<Godot.Collections.Dictionary> GetGuildMembers(double? timeoutSec = null)
        => GetJson("/api/guild/members", timeoutSec);

    public Task<Godot.Collections.Dictionary> AddFriend(string friendName)
        => PostJson("/api/friends/add", new Godot.Collections.Dictionary { ["friendName"] = friendName });

    public Task<Godot.Collections.Dictionary> InviteFriend(string friendName, string roomId)
        => PostJson("/api/friends/invite", new Godot.Collections.Dictionary
        {
            ["friendName"] = friendName,
            ["roomId"] = roomId
        });

    public Task<Godot.Collections.Dictionary> GetInvites(double? timeoutSec = null)
        => GetJson("/api/invites", timeoutSec);

    public Task<Godot.Collections.Dictionary> RespondInvite(string inviteId, bool accept)
        => PostJson("/api/invites/respond", new Godot.Collections.Dictionary
        {
            ["inviteId"] = inviteId,
            ["accept"] = accept
        });

    public Task<Godot.Collections.Dictionary> GetPaymentCatalog()
        => GetJson("/api/payments/catalog");

    public Task<Godot.Collections.Dictionary> CreatePaymentOrder(string productId, string provider)
        => PostJson("/api/payments/create", new Godot.Collections.Dictionary
        {
            ["productId"] = productId,
            ["provider"] = provider
        });

    public Task<Godot.Collections.Dictionary> ConfirmPaymentOrder(string orderId)
        => PostJson("/api/payments/confirm", new Godot.Collections.Dictionary
        {
            ["orderId"] = orderId
        });

    public Task<Godot.Collections.Dictionary> ExchangeGold(int gems)
        => PostJson("/api/gold/buy", new Godot.Collections.Dictionary
        {
            ["gems"] = gems
        });

    public Task<Godot.Collections.Dictionary> GetLeaderboard(double? timeoutSec = null)
        => GetJson("/api/leaderboard", timeoutSec);

    public Task<Godot.Collections.Dictionary> JoinMatch(string mapName)
        => PostJson("/api/match/join", new Godot.Collections.Dictionary { ["mapName"] = mapName });

    public Task<Godot.Collections.Dictionary> CancelMatch()
        => PostJson("/api/match/cancel", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> CreateRoom(string mapName, string roomName = "", int maxPlayers = 2)
        => PostJson("/api/rooms/create", new Godot.Collections.Dictionary
        {
            ["mapName"] = mapName,
            ["roomName"] = roomName,
            ["maxPlayers"] = maxPlayers
        });

    public Task<Godot.Collections.Dictionary> GetRooms()
        => GetJson("/api/rooms");

    public Task<Godot.Collections.Dictionary> JoinRoom(string roomId)
        => PostJson("/api/rooms/join", new Godot.Collections.Dictionary { ["roomId"] = roomId });

    public Task<Godot.Collections.Dictionary> LeaveRoom(string roomId)
        => PostJson("/api/rooms/leave", new Godot.Collections.Dictionary { ["roomId"] = roomId });

    public Task<Godot.Collections.Dictionary> KickRoomPlayer(string roomId, string targetUserId)
        => PostJson("/api/rooms/kick", new Godot.Collections.Dictionary { ["roomId"] = roomId, ["targetUserId"] = targetUserId });

    public Task<Godot.Collections.Dictionary> UpdateRoomSettings(string roomId, string mapName)
        => PostJson("/api/rooms/update", new Godot.Collections.Dictionary { ["roomId"] = roomId, ["mapName"] = mapName });

    public Task<Godot.Collections.Dictionary> ReportMatchResult(bool win, int kills, int duration)
        => PostJson("/api/result", new Godot.Collections.Dictionary
        {
            ["win"] = win ? 1 : 0,
            ["kills"] = kills,
            ["duration"] = duration
        });

    public Task<Godot.Collections.Dictionary> GetJson(string path, double? timeoutSec = null)
        => Request(path, Godot.HttpClient.Method.Get, new Godot.Collections.Dictionary(), timeoutSec);

    public Task<Godot.Collections.Dictionary> PostJson(string path, Godot.Collections.Dictionary body, double? timeoutSec = null)
        => Request(path, Godot.HttpClient.Method.Post, body, timeoutSec);

    async Task<Godot.Collections.Dictionary> Request(string path, Godot.HttpClient.Method method, Godot.Collections.Dictionary body, double? timeoutSec = null)
    {
        var req = new HttpRequest();
        req.Timeout = timeoutSec ?? RequestTimeoutSec;
        AddChild(req);

        var headers = new Godot.Collections.Array<string> { "Content-Type: application/json" };
        if (!string.IsNullOrEmpty(GameState.Instance?.Token))
            headers.Add($"Authorization: Bearer {GameState.Instance.Token}");

        var payload = method == Godot.HttpClient.Method.Get ? "" : Json.Stringify(body);

        // Add client request HMAC-SHA256 signature and build version headers
        long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string tsStr = timestamp.ToString();
        string methodStr = method.ToString().ToUpper();
        string message = $"{methodStr}\n{path}\n{tsStr}\n{payload}";
        string signature = ComputeHmacSha256(ClientSigKey, message);

        headers.Add($"X-Client-Ts: {tsStr}");
        headers.Add($"X-Client-Sig: {signature}");
        headers.Add($"X-Client-Build: {ClientBuildNumber}");

        var err = req.Request(ServerUrl + path, headers.ToArray(), method, payload);
        if (err != Error.Ok)
        {
            req.QueueFree();
            EmitSignal(SignalName.RequestFailed, path, NetworkUnavailable);
            return Failure(NetworkUnavailable);
        }

        var signal = await ToSignal(req, HttpRequest.SignalName.RequestCompleted);
        req.QueueFree();

        var responseCode = signal[1].AsInt32();
        var bytes = signal[3].AsByteArray();
        if (responseCode <= 0 || responseCode >= 500)
        {
            EmitSignal(SignalName.RequestFailed, path, NetworkUnavailable);
            return Failure(NetworkUnavailable);
        }

        if (responseCode == 401 || responseCode == 403)
        {
            if (path == "/api/leaderboard" || path == "/api/friends" || path == "/api/rooms" || path == "/api/invites")
            {
                return Failure(responseCode == 401 ? "Unauthorized" : "Forbidden");
            }
            GameState.Instance?.ClearSession();
            GetTree().ChangeSceneToFile("res://scenes/login/LoginScene.tscn");
        }

        var parsed = Json.ParseString(bytes.GetStringFromUtf8());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            EmitSignal(SignalName.RequestFailed, path, "Invalid server JSON");
            return Failure("Invalid server JSON");
        }

        var data = parsed.AsGodotDictionary();
        EmitSignal(SignalName.RequestFinished, path, data);
        if (data.GetBool("success") && data.ContainsKey("token"))
            GameState.Instance?.SetSession(data);
        return data;
    }

    static Godot.Collections.Dictionary Failure(string error)
        => new()
        {
            ["success"] = false,
            ["error"] = error
        };

    private string ComputeHmacSha256(string key, string message)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        var messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        using (var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(messageBytes);
            var sb = new System.Text.StringBuilder();
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
