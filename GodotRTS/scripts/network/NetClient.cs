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

    public Task<Godot.Collections.Dictionary> Register(string username, string password)
        => PostJson("/api/register", new Godot.Collections.Dictionary
        {
            ["username"] = username,
            ["password"] = password
        });

    public Task<Godot.Collections.Dictionary> GetLobby()
        => GetJson("/api/lobby");

    public Task<Godot.Collections.Dictionary> PostPresence()
        => PostJson("/api/presence", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> ClaimTask(string taskId)
        => PostJson("/api/tasks/claim", new Godot.Collections.Dictionary { ["taskId"] = taskId });

    public Task<Godot.Collections.Dictionary> StartTechResearch()
        => PostJson("/api/tech/start", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> SpeedUpTech()
        => PostJson("/api/tech/speedup", new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> GetFriends()
        => GetJson("/api/friends");

    public Task<Godot.Collections.Dictionary> AddFriend(string friendName)
        => PostJson("/api/friends/add", new Godot.Collections.Dictionary { ["friendName"] = friendName });

    public Task<Godot.Collections.Dictionary> InviteFriend(string friendName, string roomId)
        => PostJson("/api/friends/invite", new Godot.Collections.Dictionary
        {
            ["friendName"] = friendName,
            ["roomId"] = roomId
        });

    public Task<Godot.Collections.Dictionary> GetInvites()
        => GetJson("/api/invites");

    public Task<Godot.Collections.Dictionary> RespondInvite(string inviteId, bool accept)
        => PostJson("/api/invites/respond", new Godot.Collections.Dictionary
        {
            ["inviteId"] = inviteId,
            ["accept"] = accept
        });

    public Task<Godot.Collections.Dictionary> GetLeaderboard()
        => GetJson("/api/leaderboard");

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

    public Task<Godot.Collections.Dictionary> ReportMatchResult(bool win, int kills, int duration)
        => PostJson("/api/result", new Godot.Collections.Dictionary
        {
            ["win"] = win ? 1 : 0,
            ["kills"] = kills,
            ["duration"] = duration
        });

    public Task<Godot.Collections.Dictionary> GetJson(string path)
        => Request(path, Godot.HttpClient.Method.Get, new Godot.Collections.Dictionary());

    public Task<Godot.Collections.Dictionary> PostJson(string path, Godot.Collections.Dictionary body)
        => Request(path, Godot.HttpClient.Method.Post, body);

    async Task<Godot.Collections.Dictionary> Request(string path, Godot.HttpClient.Method method, Godot.Collections.Dictionary body)
    {
        var req = new HttpRequest();
        req.Timeout = RequestTimeoutSec;
        AddChild(req);

        var headers = new Godot.Collections.Array<string> { "Content-Type: application/json" };
        if (!string.IsNullOrEmpty(GameState.Instance?.Token))
            headers.Add($"Authorization: Bearer {GameState.Instance.Token}");

        var payload = method == Godot.HttpClient.Method.Get ? "" : Json.Stringify(body);
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

        if (responseCode == 401)
            GameState.Instance?.ClearSession();

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
}
