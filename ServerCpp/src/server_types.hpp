#pragma once

#include <cstdint>
#include <string>
#include <vector>

struct LobbyState
{
    std::string day;
    bool dailyLoginClaimed = false;
    bool win3Claimed = false;
    bool destroyClaimed = false;
    int winsToday = 0;
    int killsToday = 0;
    std::vector<std::string> claimedMailIds;
};

struct ActiveTech
{
    std::string id;
    std::string name;
    std::string desc;
    std::int64_t endAt = 0;
    int totalSec = 0;
};

struct UserStats
{
    int wins = 0;
    int losses = 0;
    int kills = 0;
};

struct UserRecord
{
    std::string id;
    std::string username;
    std::string passwordHash;
    bool isGuest = false;
    int level = 1;
    int wins = 0;
    int losses = 0;
    int gold = 1000;
    int gems = 100;
    std::int64_t createdAt = 0;
    LobbyState lobbyState;
    bool hasActiveTech = false;
    ActiveTech activeTech;
    bool hasStats = false;
    UserStats stats;
    std::vector<std::string> creditedOrderIds;
    std::vector<std::string> settledBattleIds;

    // Ban / security fields
    bool isBanned = false;
    std::string banReason;
    std::int64_t bannedUntil = 0;  // 0 = permanent; epoch ms otherwise
    int suspicionScore = 0;        // accumulates on anomalous behaviour; >= 100 flags for review
    std::string idCard;
    std::string realName;
};

struct RoomRecord
{
    std::string id;
    std::string name;
    std::string mapName;
    std::string hostId;
    std::string status;
    std::vector<std::string> players;
    int maxPlayers = 2;
    std::int64_t createdAt = 0;
};

struct MatchQueueEntry
{
    std::string userId;
    std::string mapName;
    std::int64_t joinedAt = 0;
};

struct PendingMatch
{
    std::string roomId;
    std::string mapName;
};

struct InviteRecord
{
    std::string id;
    std::string from;
    std::string fromId;
    std::string roomId;
    std::string mapName;
    int maxPlayers = 2;
    int playerCount = 0;
    std::int64_t createdAt = 0;
};

struct PaymentOrderRecord
{
    std::string id;
    std::string userId;
    std::string productId;
    std::string productName;
    std::string currency = "CNY";
    std::string status = "pending";
    std::string provider = "manual";
    std::string providerOrderId;
    int amountFen = 0;
    int gems = 0;
    int bonusGems = 0;
    bool creditsApplied = false;
    std::int64_t createdAt = 0;
    std::int64_t paidAt = 0;
    std::int64_t creditedAt = 0;
};

struct BattleParticipantReport
{
    std::string userId;
    int win = 0;
    int kills = 0;
    int durationSec = 0;
    std::int64_t submittedAt = 0;
};

struct BattleSessionRecord
{
    std::string id;
    std::string roomId;
    std::string mapName;
    std::string createdByUserId;
    std::string status = "active";
    std::vector<std::string> players;
    std::vector<BattleParticipantReport> reports;
    std::string winnerUserId;
    bool rewardsApplied = false;
    std::int64_t createdAt = 0;
    std::int64_t startedAt = 0;
    std::int64_t expiresAt = 0;
    std::int64_t settledAt = 0;
};

struct SessionRecord
{
    std::string token;
    std::string userId;
    std::int64_t expiresAt = 0;
};
