using Godot;


public partial class GameRelay : Node
{
    [Signal]
    public delegate void RoleAssignedEventHandler(bool isHost, int seed);

    [Signal]
    public delegate void PeerJoinedEventHandler(string peerId);

    [Signal]
    public delegate void PeerLeftEventHandler();

    [Signal]
    public delegate void CommandReceivedEventHandler(Godot.Collections.Dictionary data);

    public static GameRelay? Instance { get; private set; }

    public bool IsNetworkGame { get; private set; }
    public bool IsHost { get; private set; }
    public bool PeerConnected { get; private set; }
    public int GameSeed { get; private set; } = 12345;

    readonly WebSocketPeer socket = new();
    string roomId = "";
    bool connected;

    public override void _Ready()
    {
        Instance = this;
    }

    public void StartNetworkGame(string room)
    {
        roomId = room;
        var serverUrl = NetClient.Instance?.ServerUrl ?? "http://127.0.0.1:8080";
        var host = serverUrl.Replace("http://", "").Replace("https://", "").Split(':')[0];
        var err = socket.ConnectToUrl($"ws://{host}:8081");
        IsNetworkGame = err == Error.Ok;
    }

    public override void _Process(double delta)
    {
        socket.Poll();
        var state = socket.GetReadyState();
        if (state == WebSocketPeer.State.Open && !connected)
        {
            connected = true;
            Send(new Godot.Collections.Dictionary
            {
                ["type"] = "join",
                ["token"] = GameState.Instance?.Token ?? "",
                ["roomId"] = roomId
            });
        }
        else if (state == WebSocketPeer.State.Closed)
        {
            connected = false;
            IsNetworkGame = false;
        }

        while (socket.GetAvailablePacketCount() > 0)
        {
            var parsed = Json.ParseString(socket.GetPacket().GetStringFromUtf8());
            if (parsed.VariantType == Variant.Type.Dictionary)
                HandleMessage(parsed.AsGodotDictionary());
        }
    }

    public void SendCommand(Godot.Collections.Dictionary data)
    {
        if (!IsNetworkGame || !connected)
            return;
        Send(new Godot.Collections.Dictionary
        {
            ["type"] = "cmd",
            ["data"] = data
        });
    }

    public void SendMove(int netId, Vector3 dest)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "move",
            ["id"] = netId,
            ["x"] = dest.X,
            ["z"] = dest.Z
        });

    public void SendAttack(int attackerId, int targetId, bool targetIsBuilding)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "attack",
            ["id"] = attackerId,
            ["tid"] = targetId,
            ["bldg"] = targetIsBuilding ? 1 : 0
        });

    public void SendStop(int netId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "stop",
            ["id"] = netId
        });

    public void SendAttackMove(int netId, Vector3 dest)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "amove",
            ["id"] = netId,
            ["x"] = dest.X,
            ["z"] = dest.Z
        });

    public void SendAttackGround(int netId, Vector3 point)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "aground",
            ["id"] = netId,
            ["x"] = point.X,
            ["z"] = point.Z
        });

    public void SendPatrol(int netId, Vector3 pointA, Vector3 pointB)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "patrol",
            ["id"] = netId,
            ["ax"] = pointA.X,
            ["az"] = pointA.Z,
            ["bx"] = pointB.X,
            ["bz"] = pointB.Z
        });

    public void SendGuard(int netId, int allyNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "guard",
            ["id"] = netId,
            ["tid"] = allyNetId
        });

    public void SendRally(int buildingNetId, Vector3 point)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "rally",
            ["id"] = buildingNetId,
            ["x"] = point.X,
            ["z"] = point.Z
        });

    public void SendPark(int netId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "park",
            ["id"] = netId
        });

    public void SendBombingRun(int netId, Vector3 start, Vector3 end)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "bombrun",
            ["id"] = netId,
            ["sx"] = start.X,
            ["sz"] = start.Z,
            ["ex"] = end.X,
            ["ez"] = end.Z
        });

    public void SendBuild(string buildKey, Vector3 position, int buildingNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "build",
            ["key"] = buildKey,
            ["x"] = position.X,
            ["z"] = position.Z,
            ["nid"] = buildingNetId
        });

    public void SendProduce(int buildingNetId, string unitKey)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "produce",
            ["id"] = buildingNetId,
            ["key"] = unitKey
        });

    public void SendUpgradeBuilding(int buildingNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "upgrade",
            ["id"] = buildingNetId
        });

    public void SendCancelConstruction(int buildingNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "cancel_build",
            ["id"] = buildingNetId
        });

    public void SendRepairBuilding(int buildingNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "repair_b",
            ["id"] = buildingNetId
        });

    public void SendRepairUnit(int unitNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "repair_u",
            ["id"] = unitNetId
        });

    public void SendRebuildMainBase(int buildingNetId)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "rebuild",
            ["id"] = buildingNetId
        });

    public void SendBattleTech(string techKey, Vector3 point)
        => SendCommand(new Godot.Collections.Dictionary
        {
            ["action"] = "tech",
            ["key"] = techKey,
            ["x"] = point.X,
            ["z"] = point.Z
        });

    void Send(Godot.Collections.Dictionary data)
    {
        socket.SendText(Json.Stringify(data));
    }

    void HandleMessage(Godot.Collections.Dictionary msg)
    {
        switch (msg.GetString("type"))
        {
            case "role":
                IsHost = msg.GetString("role") == "host";
                GameSeed = msg.GetInt("seed", 12345);
                EmitSignal(SignalName.RoleAssigned, IsHost, GameSeed);
                break;
            case "peer_joined":
                PeerConnected = true;
                EmitSignal(SignalName.PeerJoined, msg.GetString("peerId"));
                break;
            case "peer_left":
                PeerConnected = false;
                EmitSignal(SignalName.PeerLeft);
                break;
            case "cmd":
                if (msg.TryGetValue("data", out var data) && data.VariantType == Variant.Type.Dictionary)
                    EmitSignal(SignalName.CommandReceived, data.AsGodotDictionary());
                break;
        }
    }
}
