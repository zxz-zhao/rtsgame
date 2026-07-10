#include "server_json_utils.hpp"

#include <cstdio>
#include <ctime>
#include <fstream>
#include <sstream>

namespace
{
void ReadStringArray(const ServerJson::Json& value, const char* key, std::vector<std::string>& output)
{
    if (!value.contains(key) || !value[key].is_array())
        return;

    for (ServerJson::Json::const_iterator it = value[key].begin(); it != value[key].end(); ++it)
    {
        if (it->is_string())
            output.push_back(it->get<std::string>());
    }
}
}

namespace ServerJson
{
Json LoadJsonFile(const std::string& path)
{
    std::ifstream input(path.c_str(), std::ios::in | std::ios::binary);
    if (!input.is_open())
        return Json::object();

    try
    {
        Json value;
        input >> value;
        return value.is_object() ? value : Json::object();
    }
    catch (...)
    {
        return Json::object();
    }
}

bool SaveJsonFile(const std::string& path, const Json& jsonValue)
{
    const std::string tempPath = path + ".tmp";
    std::ofstream output(tempPath.c_str(), std::ios::out | std::ios::binary | std::ios::trunc);
    if (!output.is_open())
        return false;

    output << jsonValue.dump(2);
    output.close();

    std::remove(path.c_str());
    return std::rename(tempPath.c_str(), path.c_str()) == 0;
}

std::string TodayStringUtc()
{
    const std::time_t now = std::time(nullptr);
    std::tm utcTime;
#ifdef _WIN32
    gmtime_s(&utcTime, &now);
#else
    gmtime_r(&now, &utcTime);
#endif

    char buffer[32];
    std::strftime(buffer, sizeof(buffer), "%Y-%m-%d", &utcTime);
    return buffer;
}

std::int64_t CurrentTimeMs()
{
    std::time_t now = std::time(nullptr);
    return static_cast<std::int64_t>(now) * 1000LL;
}

void EnsureUserDefaults(UserRecord& user)
{
    if (user.level <= 0)
        user.level = 1;
    if (user.gold < 0)
        user.gold = 0;
    if (user.gems < 0)
        user.gems = 0;
    if (user.lobbyState.day.empty())
        user.lobbyState.day = TodayStringUtc();
}

void EnsureLobbyDay(UserRecord& user)
{
    EnsureUserDefaults(user);
    const std::string today = TodayStringUtc();
    if (user.lobbyState.day == today)
        return;

    user.lobbyState.day = today;
    user.lobbyState.dailyLoginClaimed = false;
    user.lobbyState.win3Claimed = false;
    user.lobbyState.destroyClaimed = false;
    user.lobbyState.winsToday = 0;
    user.lobbyState.killsToday = 0;
}

std::string RankTitleFrom(const UserRecord& user)
{
    const int wins = user.wins;
    const int losses = user.losses;
    const int total = wins + losses;
    const double winRate = total == 0 ? 0.0 : static_cast<double>(wins) / static_cast<double>(total);

    if (wins >= 50 && winRate >= 0.55)
        return "Commander Ace";
    if (wins >= 30)
        return "Diamond I";
    if (wins >= 15)
        return "Gold II";
    if (wins >= 5)
        return "Silver I";
    if (user.level >= 25)
        return "Colonel I";
    if (user.level >= 15)
        return "Major III";
    if (user.level >= 8)
        return "Captain II";
    return "Recruit";
}

Json UserToSafeJson(const UserRecord& user)
{
    Json value = Json::object();
    value["id"] = user.id;
    value["username"] = user.username;
    value["isGuest"] = user.isGuest;
    value["level"] = user.level;
    value["wins"] = user.wins;
    value["losses"] = user.losses;
    value["gold"] = user.gold;
    value["gems"] = user.gems;
    value["createdAt"] = user.createdAt;
    value["rankTitle"] = RankTitleFrom(user);
    
    // Mask idCard for client-side privacy
    if (!user.idCard.empty())
    {
        std::string masked = user.idCard;
        if (masked.length() > 6)
        {
            masked = masked.substr(0, 4) + std::string(masked.length() - 6, '*') + masked.substr(masked.length() - 2);
        }
        value["idCard"] = masked;
    }
    else
    {
        value["idCard"] = "";
    }
    value["realName"] = user.realName;

    value["lobbyState"] = {
        { "day", user.lobbyState.day },
        { "dailyLoginClaimed", user.lobbyState.dailyLoginClaimed },
        { "win3Claimed", user.lobbyState.win3Claimed },
        { "destroyClaimed", user.lobbyState.destroyClaimed },
        { "winsToday", user.lobbyState.winsToday },
        { "killsToday", user.lobbyState.killsToday }
    };

    if (user.hasStats)
    {
        value["stats"] = {
            { "wins", user.stats.wins },
            { "losses", user.stats.losses },
            { "kills", user.stats.kills }
        };
    }

    if (user.hasActiveTech)
    {
        value["activeTech"] = {
            { "id", user.activeTech.id },
            { "name", user.activeTech.name },
            { "desc", user.activeTech.desc },
            { "endAt", user.activeTech.endAt },
            { "totalSec", user.activeTech.totalSec }
        };
    }

    return value;
}

Json LobbyTasksToJson(const UserRecord& user)
{
    const int winsToday = user.lobbyState.winsToday;
    const int killsToday = user.lobbyState.killsToday;

    Json tasks = Json::array();
    tasks.push_back({
        { "id", "daily_login" },
        { "title", "Daily Login" },
        { "cur", 1 },
        { "max", 1 },
        { "claimed", user.lobbyState.dailyLoginClaimed },
        { "canClaim", !user.lobbyState.dailyLoginClaimed }
    });
    tasks.push_back({
        { "id", "win3" },
        { "title", "Win 3 Battles" },
        { "cur", winsToday < 3 ? winsToday : 3 },
        { "max", 3 },
        { "claimed", user.lobbyState.win3Claimed },
        { "canClaim", winsToday >= 3 && !user.lobbyState.win3Claimed }
    });
    tasks.push_back({
        { "id", "destroy20" },
        { "title", "Destroy 20 Units" },
        { "cur", killsToday < 20 ? killsToday : 20 },
        { "max", 20 },
        { "claimed", user.lobbyState.destroyClaimed },
        { "canClaim", killsToday >= 20 && !user.lobbyState.destroyClaimed }
    });
    return tasks;
}

Json RoomToJson(const RoomRecord& room)
{
    Json value = Json::object();
    value["id"] = room.id;
    value["name"] = room.name;
    value["mapName"] = room.mapName;
    value["hostId"] = room.hostId;
    value["status"] = room.status;
    value["players"] = room.players;
    value["maxPlayers"] = room.maxPlayers;
    value["playerCount"] = static_cast<int>(room.players.size());
    value["createdAt"] = room.createdAt;
    return value;
}

UserRecord UserFromJson(const Json& value)
{
    UserRecord user;
    user.id = value.value("id", "");
    user.username = value.value("username", "");
    user.passwordHash = value.value("passwordHash", "");
    if (user.passwordHash.empty())
        user.passwordHash = value.value("password", "");
    user.isGuest = value.value("isGuest", false);
    user.level = value.value("level", 1);
    user.wins = value.value("wins", 0);
    user.losses = value.value("losses", 0);
    user.gold = value.value("gold", 1000);
    user.gems = value.value("gems", 100);
    user.createdAt = value.value("createdAt", static_cast<std::int64_t>(0));

    if (value.contains("lobbyState") && value["lobbyState"].is_object())
    {
        const Json& lobby = value["lobbyState"];
        user.lobbyState.day = lobby.value("day", "");
        user.lobbyState.dailyLoginClaimed = lobby.value("dailyLoginClaimed", false);
        user.lobbyState.win3Claimed = lobby.value("win3Claimed", false);
        user.lobbyState.destroyClaimed = lobby.value("destroyClaimed", false);
        user.lobbyState.winsToday = lobby.value("winsToday", 0);
        user.lobbyState.killsToday = lobby.value("killsToday", 0);
    }

    if (value.contains("stats") && value["stats"].is_object())
    {
        const Json& stats = value["stats"];
        user.hasStats = true;
        user.stats.wins = stats.value("wins", 0);
        user.stats.losses = stats.value("losses", 0);
        user.stats.kills = stats.value("kills", 0);
    }

    if (value.contains("activeTech") && value["activeTech"].is_object())
    {
        const Json& tech = value["activeTech"];
        user.hasActiveTech = true;
        user.activeTech.id = tech.value("id", "");
        user.activeTech.name = tech.value("name", "");
        user.activeTech.desc = tech.value("desc", "");
        user.activeTech.endAt = tech.value("endAt", static_cast<std::int64_t>(0));
        user.activeTech.totalSec = tech.value("totalSec", 0);
    }

    ReadStringArray(value, "creditedOrderIds", user.creditedOrderIds);
    ReadStringArray(value, "settledBattleIds", user.settledBattleIds);

    // Ban / security fields
    user.isBanned      = value.value("isBanned",      false);
    user.banReason     = value.value("banReason",     std::string());
    user.bannedUntil   = value.value("bannedUntil",   static_cast<std::int64_t>(0));
    user.suspicionScore = value.value("suspicionScore", 0);
    user.idCard        = value.value("idCard",        "");
    user.realName      = value.value("realName",      "");

    EnsureUserDefaults(user);
    return user;
}

Json UserToJson(const UserRecord& user)
{
    Json value = UserToSafeJson(user);
    value["passwordHash"]   = user.passwordHash;
    value["password"]       = user.passwordHash;
    value["creditedOrderIds"] = user.creditedOrderIds;
    value["settledBattleIds"] = user.settledBattleIds;
    // Ban / security fields
    value["isBanned"]       = user.isBanned;
    value["banReason"]      = user.banReason;
    value["bannedUntil"]    = user.bannedUntil;
    value["suspicionScore"] = user.suspicionScore;
    value["idCard"]         = user.idCard;
    value["realName"]       = user.realName;
    return value;
}

RoomRecord RoomFromJson(const Json& value)
{
    RoomRecord room;
    room.id = value.value("id", "");
    room.name = value.value("name", "");
    room.mapName = value.value("mapName", "");
    room.hostId = value.value("hostId", "");
    room.status = value.value("status", "");
    room.maxPlayers = value.value("maxPlayers", 2);
    room.createdAt = value.value("createdAt", static_cast<std::int64_t>(0));
    ReadStringArray(value, "players", room.players);
    return room;
}

Json RoomToJsonStorage(const RoomRecord& room)
{
    return RoomToJson(room);
}

MatchQueueEntry MatchQueueEntryFromJson(const Json& value)
{
    MatchQueueEntry entry;
    entry.userId = value.value("userId", "");
    entry.mapName = value.value("mapName", "");
    entry.joinedAt = value.value("joinedAt", static_cast<std::int64_t>(0));
    return entry;
}

Json MatchQueueEntryToJson(const MatchQueueEntry& entry)
{
    return {
        { "userId", entry.userId },
        { "mapName", entry.mapName },
        { "joinedAt", entry.joinedAt }
    };
}

PendingMatch PendingMatchFromJson(const Json& value)
{
    PendingMatch match;
    match.roomId = value.value("roomId", "");
    match.mapName = value.value("mapName", "");
    return match;
}

Json PendingMatchToJson(const PendingMatch& entry)
{
    return {
        { "roomId", entry.roomId },
        { "mapName", entry.mapName }
    };
}

InviteRecord InviteFromJson(const Json& value)
{
    InviteRecord invite;
    invite.id = value.value("id", "");
    invite.from = value.value("from", "");
    invite.fromId = value.value("fromId", "");
    invite.roomId = value.value("roomId", "");
    invite.mapName = value.value("mapName", "");
    invite.maxPlayers = value.value("maxPlayers", 2);
    invite.playerCount = value.value("playerCount", 0);
    invite.createdAt = value.value("createdAt", static_cast<std::int64_t>(0));
    return invite;
}

Json InviteToJson(const InviteRecord& invite)
{
    return {
        { "id", invite.id },
        { "from", invite.from },
        { "fromId", invite.fromId },
        { "roomId", invite.roomId },
        { "mapName", invite.mapName },
        { "maxPlayers", invite.maxPlayers },
        { "playerCount", invite.playerCount },
        { "createdAt", invite.createdAt }
    };
}

PaymentOrderRecord PaymentOrderFromJson(const Json& value)
{
    PaymentOrderRecord order;
    order.id = value.value("id", "");
    order.userId = value.value("userId", "");
    order.productId = value.value("productId", "");
    order.productName = value.value("productName", "");
    order.currency = value.value("currency", "CNY");
    order.status = value.value("status", "pending");
    order.provider = value.value("provider", "manual");
    order.providerOrderId = value.value("providerOrderId", "");
    order.amountFen = value.value("amountFen", 0);
    order.gems = value.value("gems", 0);
    order.bonusGems = value.value("bonusGems", 0);
    order.creditsApplied = value.value("creditsApplied", false);
    order.createdAt = value.value("createdAt", static_cast<std::int64_t>(0));
    order.paidAt = value.value("paidAt", static_cast<std::int64_t>(0));
    order.creditedAt = value.value("creditedAt", static_cast<std::int64_t>(0));
    return order;
}

Json PaymentOrderToJson(const PaymentOrderRecord& order)
{
    return {
        { "id", order.id },
        { "userId", order.userId },
        { "productId", order.productId },
        { "productName", order.productName },
        { "currency", order.currency },
        { "status", order.status },
        { "provider", order.provider },
        { "providerOrderId", order.providerOrderId },
        { "amountFen", order.amountFen },
        { "gems", order.gems },
        { "bonusGems", order.bonusGems },
        { "creditsApplied", order.creditsApplied },
        { "createdAt", order.createdAt },
        { "paidAt", order.paidAt },
        { "creditedAt", order.creditedAt }
    };
}

BattleParticipantReport BattleParticipantReportFromJson(const Json& value)
{
    BattleParticipantReport report;
    report.userId = value.value("userId", "");
    report.win = value.value("win", 0);
    report.kills = value.value("kills", 0);
    report.durationSec = value.value("durationSec", 0);
    report.submittedAt = value.value("submittedAt", static_cast<std::int64_t>(0));
    return report;
}

Json BattleParticipantReportToJson(const BattleParticipantReport& report)
{
    return {
        { "userId", report.userId },
        { "win", report.win },
        { "kills", report.kills },
        { "durationSec", report.durationSec },
        { "submittedAt", report.submittedAt }
    };
}

BattleSessionRecord BattleSessionFromJson(const Json& value)
{
    BattleSessionRecord session;
    session.id = value.value("id", "");
    session.roomId = value.value("roomId", "");
    session.mapName = value.value("mapName", "");
    session.createdByUserId = value.value("createdByUserId", "");
    session.status = value.value("status", "active");
    session.winnerUserId = value.value("winnerUserId", "");
    session.rewardsApplied = value.value("rewardsApplied", false);
    session.createdAt = value.value("createdAt", static_cast<std::int64_t>(0));
    session.startedAt = value.value("startedAt", static_cast<std::int64_t>(0));
    session.expiresAt = value.value("expiresAt", static_cast<std::int64_t>(0));
    session.settledAt = value.value("settledAt", static_cast<std::int64_t>(0));

    ReadStringArray(value, "players", session.players);
    if (value.contains("reports") && value["reports"].is_array())
    {
        for (Json::const_iterator it = value["reports"].begin(); it != value["reports"].end(); ++it)
        {
            if (!it->is_object())
                continue;
            session.reports.push_back(BattleParticipantReportFromJson(*it));
        }
    }
    return session;
}

Json BattleSessionToJson(const BattleSessionRecord& session)
{
    Json reports = Json::array();
    for (std::size_t i = 0; i < session.reports.size(); ++i)
        reports.push_back(BattleParticipantReportToJson(session.reports[i]));

    return {
        { "id", session.id },
        { "roomId", session.roomId },
        { "mapName", session.mapName },
        { "createdByUserId", session.createdByUserId },
        { "status", session.status },
        { "players", session.players },
        { "reports", reports },
        { "winnerUserId", session.winnerUserId },
        { "rewardsApplied", session.rewardsApplied },
        { "createdAt", session.createdAt },
        { "startedAt", session.startedAt },
        { "expiresAt", session.expiresAt },
        { "settledAt", session.settledAt }
    };
}
}
