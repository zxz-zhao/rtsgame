using Godot;
using System.Collections.Generic;
using System.Linq;


public partial class GameState : Node
{
    public sealed class BattleParticipantProfile
    {
        public string ParticipantId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Role { get; init; } = "";
        public string Intro { get; init; } = "";
        public bool IsLocalPlayer { get; init; }
        public bool CanReport { get; init; } = true;
    }

    public readonly struct BattleCommunicationPreference
    {
        public bool TextBlocked { get; init; }
        public bool VoiceBlocked { get; init; }
        public bool Reported { get; init; }
        public string ReportReason { get; init; }
    }

    [Signal]
    public delegate void SelectionChangedEventHandler(Godot.Collections.Array<Node> selection);

    [Signal]
    public delegate void SessionChangedEventHandler();

    [Signal]
    public delegate void BattleCommunicationChangedEventHandler();

    public static GameState? Instance { get; private set; }

    public string Token { get; private set; } = "";
    public string UserId { get; private set; } = "";
    public string Username { get; private set; } = "";
    public bool IsGuest { get; private set; } = true;
    public int Level { get; private set; } = 1;
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int Gold { get; private set; } = 1000;
    public int Gems { get; private set; } = 100;
    public string RankTitle { get; private set; } = "列兵";
    public string GuildName { get; private set; } = "无工会";
    public int GuildLevel { get; private set; } = 1;
    public bool ShowMainBaseIdentity { get; private set; } = true;
    public string SelectedAvatarPath { get; private set; } = "res://assets/unity_migrated/Assets/Resources/LobbyGen/WW2Portraits/officer_avatar_01_field_commander.png";
    public string SelectedMapName { get; private set; } = BattleMapCatalog.DefaultMapName;
    public string SelectedMode { get; private set; } = "快速匹配";
    public string GlobalConquestStarterUnitKey { get; private set; } = "tank";
    public bool HasChosenGlobalConquestFaction { get; private set; }
    public string CurrentRoomId { get; private set; } = "";
    public bool HasBattleEntry { get; private set; }
    public string LastBattleMode { get; private set; } = "";
    public string LastBattleMapName { get; private set; } = BattleMapCatalog.DefaultMapName;
    public List<Node> Selected { get; } = new();
    readonly List<BattleParticipantProfile> battleParticipants = new();
    readonly Dictionary<string, BattleCommunicationPreference> battleCommunicationPreferences = new();

    const string SessionPath = "user://session.cfg";

    public override void _Ready()
    {
        Instance = this;
        LoadSession();
    }

    public void SetSession(Godot.Collections.Dictionary data)
    {
        Token = data.GetString("token");
        UserId = data.GetString("userId");
        Username = data.GetString("username");
        IsGuest = data.GetBool("isGuest", true);
        Level = data.GetInt("level", Level);
        Wins = data.GetInt("wins", Wins);
        Losses = data.GetInt("losses", Losses);
        Gold = data.GetInt("gold", Gold);
        Gems = data.GetInt("gems", Gems);
        RankTitle = data.GetString("rankTitle", RankTitle);
        GuildName = NormalizeGuildName(data.GetString("guildName", data.GetString("guild", GuildName)));
        GuildLevel = Mathf.Max(1, data.GetInt("guildLevel", data.GetInt("guildLv", GuildLevel)));
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
    }

    public void ApplyLobbyData(Godot.Collections.Dictionary data)
    {
        Gold = data.GetInt("gold", Gold);
        Gems = data.GetInt("gems", Gems);
        RankTitle = data.GetString("rankTitle", RankTitle);
        GuildName = NormalizeGuildName(data.GetString("guildName", data.GetString("guild", GuildName)));
        GuildLevel = Mathf.Max(1, data.GetInt("guildLevel", data.GetInt("guildLv", GuildLevel)));
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
    }

    public void SetShowMainBaseIdentity(bool show)
    {
        ShowMainBaseIdentity = true;
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
    }

    public void SetSelectedAvatarPath(string avatarPath)
    {
        var normalized = string.IsNullOrWhiteSpace(avatarPath)
            ? "res://assets/unity_migrated/Assets/Resources/LobbyGen/WW2Portraits/officer_avatar_01_field_commander.png"
            : avatarPath.Trim();
        if (normalized == SelectedAvatarPath)
            return;

        SelectedAvatarPath = normalized;
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
    }

    public bool TrySpendCurrency(int goldCost, int gemCost)
    {
        var normalizedGold = Mathf.Max(0, goldCost);
        var normalizedGems = Mathf.Max(0, gemCost);
        if (Gold < normalizedGold || Gems < normalizedGems)
            return false;

        Gold -= normalizedGold;
        Gems -= normalizedGems;
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
        return true;
    }
    public void AddCurrency(int goldAmount, int gemAmount)
    {
        var normalizedGold = Mathf.Max(0, goldAmount);
        var normalizedGems = Mathf.Max(0, gemAmount);
        if (normalizedGold == 0 && normalizedGems == 0)
            return;

        Gold += normalizedGold;
        Gems += normalizedGems;
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
    }

    public void ClearSession()
    {
        Token = "";
        UserId = "";
        Username = "";
        IsGuest = true;
        Level = 1;
        Wins = 0;
        Losses = 0;
        RankTitle = "列兵";
        GuildName = "无工会";
        GuildLevel = 1;
        ShowMainBaseIdentity = true;
        SelectedAvatarPath = "res://assets/unity_migrated/Assets/Resources/LobbyGen/WW2Portraits/officer_avatar_01_field_commander.png";
        CurrentRoomId = "";
        GlobalConquestStarterUnitKey = "tank";
        HasBattleEntry = false;
        LastBattleMode = "";
        LastBattleMapName = BattleMapCatalog.DefaultMapName;
        battleParticipants.Clear();
        battleCommunicationPreferences.Clear();
        SaveSession();
        EmitSignal(SignalName.SessionChanged);
        EmitSignal(SignalName.BattleCommunicationChanged);
    }

    public void SelectMap(string mapName, string mode = "快速匹配")
    {
        SelectedMapName = BattleMapCatalog.IsKnownMap(mapName)
            ? mapName
            : BattleMapCatalog.DefaultMapName;
        SelectedMode = string.IsNullOrWhiteSpace(mode) ? "快速匹配" : mode;
        SaveSession();
    }

    public void SetCurrentRoom(string roomId)
    {
        var normalized = string.IsNullOrWhiteSpace(roomId) ? "" : roomId.Trim();
        if (normalized != CurrentRoomId)
            ClearBattleParticipants();
        CurrentRoomId = normalized;
        SaveSession();
    }

    public void ClearCurrentRoom()
    {
        CurrentRoomId = "";
        ClearBattleParticipants();
        SaveSession();
    }

    public void SetGlobalConquestStarterUnit(string unitKey)
    {
        var normalized = string.IsNullOrWhiteSpace(unitKey) ? "tank" : unitKey.Trim();
        GlobalConquestStarterUnitKey = BattleUnitCatalog.IsGlobalConquestStarter(normalized)
            ? normalized
            : "tank";
        SaveSession();
    }

    public void SetGlobalConquestFactionChosen(bool chosen)
    {
        HasChosenGlobalConquestFaction = chosen;
        SaveSession();
    }

    public void RememberBattleEntry(string mode, string mapName)
    {
        HasBattleEntry = true;
        LastBattleMode = string.IsNullOrWhiteSpace(mode) ? SelectedMode : mode.Trim();
        LastBattleMapName = BattleMapCatalog.IsKnownMap(mapName)
            ? mapName
            : SelectedMapName;
        SaveSession();
    }

    public void ClearBattleEntry()
    {
        HasBattleEntry = false;
        LastBattleMode = "";
        LastBattleMapName = SelectedMapName;
        SaveSession();
    }

    public BattleParticipantProfile[] GetBattleParticipants()
        => battleParticipants
            .Select(profile => new BattleParticipantProfile
            {
                ParticipantId = profile.ParticipantId,
                DisplayName = profile.DisplayName,
                Role = profile.Role,
                Intro = profile.Intro,
                IsLocalPlayer = profile.IsLocalPlayer,
                CanReport = profile.CanReport
            })
            .ToArray();

    public void SetBattleParticipants(IEnumerable<BattleParticipantProfile> participants)
    {
        battleParticipants.Clear();
        foreach (var participant in participants ?? Enumerable.Empty<BattleParticipantProfile>())
        {
            if (participant is null)
                continue;

            var participantId = NormalizeBattleParticipantKey(participant.ParticipantId, participant.DisplayName);
            if (string.IsNullOrEmpty(participantId) || battleParticipants.Any(existing => existing.ParticipantId == participantId))
                continue;

            battleParticipants.Add(new BattleParticipantProfile
            {
                ParticipantId = participantId,
                DisplayName = string.IsNullOrWhiteSpace(participant.DisplayName) ? participantId : participant.DisplayName.Trim(),
                Role = string.IsNullOrWhiteSpace(participant.Role) ? "战场成员" : participant.Role.Trim(),
                Intro = string.IsNullOrWhiteSpace(participant.Intro) ? "暂无简介" : participant.Intro.Trim(),
                IsLocalPlayer = participant.IsLocalPlayer,
                CanReport = participant.CanReport
            });
        }

        EmitSignal(SignalName.BattleCommunicationChanged);
    }

    public void ClearBattleParticipants()
    {
        var hadParticipants = battleParticipants.Count > 0;
        battleParticipants.Clear();
        if (hadParticipants)
            EmitSignal(SignalName.BattleCommunicationChanged);
    }

    public void ClearBattleCommunicationPreferences()
    {
        if (battleCommunicationPreferences.Count == 0)
            return;

        battleCommunicationPreferences.Clear();
        EmitSignal(SignalName.BattleCommunicationChanged);
    }

    public BattleCommunicationPreference GetBattleCommunicationPreference(string participantId, string displayName = "")
    {
        var key = NormalizeBattleParticipantKey(participantId, displayName);
        return !string.IsNullOrEmpty(key) && battleCommunicationPreferences.TryGetValue(key, out var pref)
            ? pref
            : default;
    }

    public bool ShouldReceiveBattleText(string participantId, string displayName = "")
        => !GetBattleCommunicationPreference(participantId, displayName).TextBlocked;

    public bool ShouldReceiveBattleVoice(string participantId, string displayName = "")
        => !GetBattleCommunicationPreference(participantId, displayName).VoiceBlocked;

    public void SetBattleTextBlocked(string participantId, string displayName, bool blocked)
        => UpdateBattleCommunicationPreference(participantId, displayName, pref => new BattleCommunicationPreference
        {
            TextBlocked = blocked,
            VoiceBlocked = pref.VoiceBlocked,
            Reported = pref.Reported,
            ReportReason = pref.ReportReason
        });

    public void SetBattleVoiceBlocked(string participantId, string displayName, bool blocked)
        => UpdateBattleCommunicationPreference(participantId, displayName, pref => new BattleCommunicationPreference
        {
            TextBlocked = pref.TextBlocked,
            VoiceBlocked = blocked,
            Reported = pref.Reported,
            ReportReason = pref.ReportReason
        });

    public void SetBattleMuteAll(string participantId, string displayName, bool blocked)
        => UpdateBattleCommunicationPreference(participantId, displayName, pref => new BattleCommunicationPreference
        {
            TextBlocked = blocked,
            VoiceBlocked = blocked,
            Reported = pref.Reported,
            ReportReason = pref.ReportReason
        });

    public void RecordBattleReport(string participantId, string displayName, string reason)
        => UpdateBattleCommunicationPreference(participantId, displayName, pref => new BattleCommunicationPreference
        {
            TextBlocked = pref.TextBlocked,
            VoiceBlocked = pref.VoiceBlocked,
            Reported = true,
            ReportReason = string.IsNullOrWhiteSpace(reason) ? pref.ReportReason : reason.Trim()
        });

    public void SetSelection(IEnumerable<Node> nodes)
    {
        var hadSameSelection = Selected.Where(GodotObject.IsInstanceValid).SequenceEqual(nodes.Where(GodotObject.IsInstanceValid));

        foreach (var old in Selected.Where(GodotObject.IsInstanceValid))
        {
            if (old.HasMethod("SetSelected"))
                old.Call("SetSelected", false);
        }

        Selected.Clear();
        Selected.AddRange(nodes.Where(GodotObject.IsInstanceValid));

        foreach (var item in Selected)
        {
            if (item.HasMethod("SetSelected"))
                item.Call("SetSelected", true);
        }

        if (!hadSameSelection && Selected.Count > 0)
        {
            var containsPlayerOwned = Selected.Any(item =>
                item switch
                {
                    RtsUnit unit => unit.PlayerOwned,
                    RtsBuilding building => building.PlayerOwned,
                    _ => false
                });
            var containsEnemy = Selected.Any(item =>
                item switch
                {
                    RtsUnit unit => !unit.PlayerOwned,
                    RtsBuilding building => !building.PlayerOwned,
                    _ => false
                });
            BattleFeedback.Selection(this, Selected.Count, containsPlayerOwned, containsEnemy);
        }

        EmitSignal(SignalName.SelectionChanged, new Godot.Collections.Array<Node>(Selected));
    }

    void LoadSession()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SessionPath) != Error.Ok)
            return;
        Token = cfg.GetValue("session", "token", "").AsString();
        UserId = cfg.GetValue("session", "user_id", "").AsString();
        Username = cfg.GetValue("session", "username", "").AsString();
        IsGuest = cfg.GetValue("session", "is_guest", true).AsBool();
        Level = cfg.GetValue("session", "level", Level).AsInt32();
        Wins = cfg.GetValue("session", "wins", Wins).AsInt32();
        Losses = cfg.GetValue("session", "losses", Losses).AsInt32();
        Gold = cfg.GetValue("session", "gold", Gold).AsInt32();
        Gems = cfg.GetValue("session", "gems", Gems).AsInt32();
        RankTitle = cfg.GetValue("session", "rank_title", RankTitle).AsString();
        GuildName = NormalizeGuildName(cfg.GetValue("session", "guild_name", GuildName).AsString());
        GuildLevel = Mathf.Max(1, cfg.GetValue("session", "guild_level", GuildLevel).AsInt32());
        ShowMainBaseIdentity = true;
        SelectedAvatarPath = cfg.GetValue("session", "selected_avatar_path", SelectedAvatarPath).AsString();
        SelectedMapName = cfg.GetValue("battle", "selected_map", SelectedMapName).AsString();
        if (!BattleMapCatalog.IsKnownMap(SelectedMapName))
            SelectedMapName = BattleMapCatalog.DefaultMapName;
        SelectedMode = cfg.GetValue("battle", "selected_mode", SelectedMode).AsString();
        GlobalConquestStarterUnitKey = cfg.GetValue("battle", "global_conquest_starter", GlobalConquestStarterUnitKey).AsString();
        if (!BattleUnitCatalog.IsGlobalConquestStarter(GlobalConquestStarterUnitKey))
            GlobalConquestStarterUnitKey = "tank";
        HasChosenGlobalConquestFaction = cfg.GetValue("battle", "has_chosen_faction", false).AsBool();
        CurrentRoomId = cfg.GetValue("battle", "current_room", CurrentRoomId).AsString();
        HasBattleEntry = cfg.GetValue("battle", "has_battle_entry", false).AsBool();
        LastBattleMode = cfg.GetValue("battle", "last_battle_mode", LastBattleMode).AsString();
        LastBattleMapName = cfg.GetValue("battle", "last_battle_map", LastBattleMapName).AsString();
        if (!BattleMapCatalog.IsKnownMap(LastBattleMapName))
            LastBattleMapName = BattleMapCatalog.DefaultMapName;
    }

    void SaveSession()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("session", "token", Token);
        cfg.SetValue("session", "user_id", UserId);
        cfg.SetValue("session", "username", Username);
        cfg.SetValue("session", "is_guest", IsGuest);
        cfg.SetValue("session", "level", Level);
        cfg.SetValue("session", "wins", Wins);
        cfg.SetValue("session", "losses", Losses);
        cfg.SetValue("session", "gold", Gold);
        cfg.SetValue("session", "gems", Gems);
        cfg.SetValue("session", "rank_title", RankTitle);
        cfg.SetValue("session", "guild_name", GuildName);
        cfg.SetValue("session", "guild_level", GuildLevel);
        cfg.SetValue("session", "selected_avatar_path", SelectedAvatarPath);
        cfg.SetValue("settings", "show_main_base_identity", ShowMainBaseIdentity);
        cfg.SetValue("battle", "selected_map", SelectedMapName);
        cfg.SetValue("battle", "selected_mode", SelectedMode);
        cfg.SetValue("battle", "global_conquest_starter", GlobalConquestStarterUnitKey);
        cfg.SetValue("battle", "has_chosen_faction", HasChosenGlobalConquestFaction);
        cfg.SetValue("battle", "current_room", CurrentRoomId);
        cfg.SetValue("battle", "has_battle_entry", HasBattleEntry);
        cfg.SetValue("battle", "last_battle_mode", LastBattleMode);
        cfg.SetValue("battle", "last_battle_map", LastBattleMapName);
        cfg.Save(SessionPath);
    }

    static string NormalizeGuildName(string value)
        => string.IsNullOrWhiteSpace(value) ? "无工会" : value.Trim();

    void UpdateBattleCommunicationPreference(string participantId, string displayName, System.Func<BattleCommunicationPreference, BattleCommunicationPreference> updater)
    {
        var key = NormalizeBattleParticipantKey(participantId, displayName);
        if (string.IsNullOrEmpty(key))
            return;

        var current = battleCommunicationPreferences.TryGetValue(key, out var pref) ? pref : default;
        battleCommunicationPreferences[key] = updater(current);
        EmitSignal(SignalName.BattleCommunicationChanged);
    }

    static string NormalizeBattleParticipantKey(string participantId, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(participantId))
            return participantId.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();
        return "";
    }
}
