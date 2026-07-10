using Godot;


public partial class BattleBootstrap : Node3D
{
    int nextNetId = 1;

    public override void _Ready()
    {
        foreach (var node in GetTree().GetNodesInGroup("rts_selectable"))
        {
            switch (node)
            {
                case RtsUnit unit:
                    unit.NetId = nextNetId++;
                    break;
                case RtsBuilding building:
                    building.NetId = nextNetId++;
                    break;
            }
        }

        if (GameRelay.Instance is not null)
        {
            var roomId = GameState.Instance?.CurrentRoomId ?? "";
            if (!string.IsNullOrWhiteSpace(roomId))
                GameRelay.Instance.StartNetworkGame(roomId);
            GameRelay.Instance.CommandReceived += OnRemoteCommand;
        }
    }

    public override void _ExitTree()
    {
        if (GameRelay.Instance is not null)
            GameRelay.Instance.CommandReceived -= OnRemoteCommand;
    }

    void OnRemoteCommand(Godot.Collections.Dictionary data)
    {
        switch (data.GetString("action"))
        {
            case "move":
                if (FindByNetId(data.GetInt("id")) is RtsUnit unit)
                    unit.MoveTo(new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z")));
                break;
            case "amove":
                if (FindByNetId(data.GetInt("id")) is RtsUnit attackMoveUnit)
                    attackMoveUnit.AttackMoveTo(new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z")));
                break;
            case "attack":
                if (FindByNetId(data.GetInt("id")) is RtsUnit attacker &&
                    FindByNetId(data.GetInt("tid")) is Node3D target)
                    attacker.Attack(target);
                break;
            case "aground":
                if (FindByNetId(data.GetInt("id")) is RtsUnit attackGroundUnit)
                    attackGroundUnit.AttackGround(new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z")));
                break;
            case "patrol":
                if (FindByNetId(data.GetInt("id")) is RtsUnit patrolUnit)
                    patrolUnit.PatrolBetween(
                        new Vector3(data.GetFloat("ax"), 0f, data.GetFloat("az")),
                        new Vector3(data.GetFloat("bx"), 0f, data.GetFloat("bz")));
                break;
            case "guard":
                if (FindByNetId(data.GetInt("id")) is RtsUnit guardUnit &&
                    FindByNetId(data.GetInt("tid")) is RtsUnit ally)
                    guardUnit.Guard(ally);
                break;
            case "rally":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding rallyBuilding)
                    rallyBuilding.SetRallyPoint(new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z")));
                break;
            case "park":
                if (FindByNetId(data.GetInt("id")) is RtsUnit parkUnit)
                    BattleGameManager.Instance?.TrySendUnitToAirfieldParking(parkUnit, out _);
                break;
            case "bombrun":
                if (FindByNetId(data.GetInt("id")) is RtsUnit bomberUnit)
                    bomberUnit.ApplyBombingRunCommand(
                        new Vector3(data.GetFloat("sx"), 0f, data.GetFloat("sz")),
                        new Vector3(data.GetFloat("ex"), 0f, data.GetFloat("ez")));
                break;
            case "stop":
                if (FindByNetId(data.GetInt("id")) is RtsUnit stopUnit)
                    stopUnit.Stop();
                break;
            case "build":
                if (BattleGameManager.Instance is { } buildManager)
                {
                    var pos = new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z"));
                    if (buildManager.TryConstructBuilding(data.GetString("key"), pos, true, out _, out var constructed) && constructed is not null)
                    {
                        var netId = data.GetInt("nid", constructed.NetId);
                        constructed.NetId = netId;
                        buildManager.EnsureNextNetIdAbove(netId);
                    }
                }
                break;
            case "produce":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding producer)
                    BattleGameManager.Instance?.TryQueueProductionForFaction(producer, data.GetString("key"), producer.PlayerOwned, out _);
                break;
            case "upgrade":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding upgradeBuilding)
                    BattleGameManager.Instance?.TryUpgradeBuildingForFaction(upgradeBuilding, upgradeBuilding.PlayerOwned, out _);
                break;
            case "cancel_build":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding cancelBuilding)
                    BattleGameManager.Instance?.TryCancelConstructionForFaction(cancelBuilding, cancelBuilding.PlayerOwned, out _);
                break;
            case "repair_b":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding repairBuilding)
                    BattleGameManager.Instance?.TryRepairBuildingForFaction(repairBuilding, repairBuilding.PlayerOwned, out _);
                break;
            case "repair_u":
                if (FindByNetId(data.GetInt("id")) is RtsUnit repairUnit)
                    BattleGameManager.Instance?.TryRepairUnitForFaction(repairUnit, repairUnit.PlayerOwned, out _);
                break;
            case "rebuild":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding rebuildBuilding)
                    BattleGameManager.Instance?.TryStartFactionMainBaseRebuild(rebuildBuilding.PlayerOwned, out _);
                break;
            case "occupy_base":
                if (FindByNetId(data.GetInt("id")) is RtsBuilding occupiedBase)
                    BattleGameManager.Instance?.TryOccupyGlobalConquestMainBaseForFaction(true, out _);
                break;
            case "tech":
                BattleGameManager.Instance?.TryCastBattleTechForFaction(
                    data.GetString("key"),
                    new Vector3(data.GetFloat("x"), 0f, data.GetFloat("z")),
                    true,
                    out _,
                    out _);
                break;
            case "chat":
                FindHud()?.ReceiveBattleTextMessage(
                    ResolveRemoteParticipantId(data),
                    ResolveRemoteSpeaker(data),
                    ResolveRemoteMessage(data));
                break;
            case "voice":
                FindHud()?.ReceiveBattleVoiceSignal(
                    ResolveRemoteParticipantId(data),
                    ResolveRemoteSpeaker(data));
                break;
        }
    }

    BattleHud? FindHud()
        => GetNodeOrNull<BattleHud>("HUD");

    static string ResolveRemoteParticipantId(Godot.Collections.Dictionary data)
    {
        var participantId = data.GetString("participantId");
        if (!string.IsNullOrWhiteSpace(participantId))
            return participantId.Trim();

        var userId = data.GetString("userId");
        if (!string.IsNullOrWhiteSpace(userId))
            return userId.Trim();

        var senderId = data.GetString("senderId");
        if (!string.IsNullOrWhiteSpace(senderId))
            return senderId.Trim();

        var peerId = data.GetString("peerId");
        if (!string.IsNullOrWhiteSpace(peerId))
            return peerId.Trim();

        var fromId = data.GetString("fromId");
        return string.IsNullOrWhiteSpace(fromId) ? "" : fromId.Trim();
    }

    static string ResolveRemoteSpeaker(Godot.Collections.Dictionary data)
    {
        var speaker = data.GetString("speaker");
        if (!string.IsNullOrWhiteSpace(speaker))
            return speaker.Trim();

        var displayName = data.GetString("displayName");
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();

        var username = data.GetString("username");
        if (!string.IsNullOrWhiteSpace(username))
            return username.Trim();

        var name = data.GetString("name");
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        var from = data.GetString("from");
        return string.IsNullOrWhiteSpace(from) ? "队友" : from.Trim();
    }

    static string ResolveRemoteMessage(Godot.Collections.Dictionary data)
    {
        var message = data.GetString("msg");
        if (!string.IsNullOrWhiteSpace(message))
            return message.Trim();

        message = data.GetString("message");
        if (!string.IsNullOrWhiteSpace(message))
            return message.Trim();

        message = data.GetString("text");
        if (!string.IsNullOrWhiteSpace(message))
            return message.Trim();

        var content = data.GetString("content");
        return string.IsNullOrWhiteSpace(content) ? "" : content.Trim();
    }

    Node? FindByNetId(int netId)
    {
        foreach (var node in GetTree().GetNodesInGroup("rts_selectable"))
        {
            switch (node)
            {
                case RtsUnit unit when unit.NetId == netId:
                    return unit;
                case RtsBuilding building when building.NetId == netId:
                    return building;
            }
        }
        return null;
    }
}
