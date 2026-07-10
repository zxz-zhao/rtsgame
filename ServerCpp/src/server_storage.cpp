#include "server_storage.hpp"

#include "runtime_utils.hpp"
#include "server_security.hpp"

#include <algorithm>

namespace
{
std::vector<std::string> JsonArrayToStrings(const ServerJson::Json& value)
{
    std::vector<std::string> result;
    if (!value.is_array())
        return result;

    for (ServerJson::Json::const_iterator it = value.begin(); it != value.end(); ++it)
    {
        if (it->is_string())
            result.push_back(it->get<std::string>());
    }
    return result;
}

ServerJson::Json StringsToJsonArray(const std::vector<std::string>& values)
{
    ServerJson::Json array = ServerJson::Json::array();
    for (std::size_t i = 0; i < values.size(); ++i)
        array.push_back(values[i]);
    return array;
}

bool ContainsString(const std::vector<std::string>& values, const std::string& target)
{
    return std::find(values.begin(), values.end(), target) != values.end();
}
}

ServerStorage::ServerStorage(const AppConfig& config, AsyncLogger& logger)
    : config_(config)
    , logger_(logger)
    , mysqlStore_(config, logger)
    , redisStore_(config, logger)
{
}

bool ServerStorage::Initialize()
{
    if (!RuntimeUtils::EnsureDirectoryRecursive(config_.dataDir))
        return false;

    if (!mysqlStore_.Initialize())
        return false;
    if (!redisStore_.Initialize())
        return false;

    usersFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "users.json");
    roomsFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "rooms.json");
    matchQueueFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "match_queue.json");
    pendingMatchesFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "pending_matches.json");
    sessionsFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "sessions.json");
    friendsFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "friends.json");
    invitesFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "invites.json");
    paymentOrdersFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "payment_orders.json");
    battleSessionsFilePath_ = RuntimeUtils::JoinPath(config_.dataDir, "battle_sessions.json");

    std::lock_guard<std::mutex> lock(mutex_);
    return LoadCollectionState("users", usersFilePath_, usersJson_)
        && LoadCollectionState("rooms", roomsFilePath_, roomsJson_)
        && LoadCollectionState("match_queue", matchQueueFilePath_, matchQueueJson_)
        && LoadCollectionState("pending_matches", pendingMatchesFilePath_, pendingMatchesJson_)
        && LoadCollectionState("sessions", sessionsFilePath_, sessionsJson_)
        && LoadCollectionState("friends", friendsFilePath_, friendsJson_)
        && LoadCollectionState("invites", invitesFilePath_, invitesJson_)
        && LoadCollectionState("payment_orders", paymentOrdersFilePath_, paymentOrdersJson_)
        && LoadCollectionState("battle_sessions", battleSessionsFilePath_, battleSessionsJson_);
}

bool ServerStorage::LoadOrCreateFile(const std::string& path, ServerJson::Json& target)
{
    target = ServerJson::LoadJsonFile(path);
    if (!RuntimeUtils::FileExists(path))
        return ServerJson::SaveJsonFile(path, target);
    return true;
}

bool ServerStorage::LoadCollectionState(const std::string& collectionName, const std::string& filePath, ServerJson::Json& target)
{
    ServerJson::Json snapshot;
    if (!LoadOrCreateFile(filePath, snapshot))
        return false;

    bool exists = false;
    if (ShouldUseMySqlCollection(collectionName))
    {
        if (!mysqlStore_.LoadCollection(collectionName, target, exists))
            return false;
        if (!exists)
        {
            target = snapshot;
            if (!mysqlStore_.SaveCollection(collectionName, target))
                return false;
        }
        return ServerJson::SaveJsonFile(filePath, target);
    }

    if (ShouldUseRedisCollection(collectionName))
    {
        if (!redisStore_.LoadCollection(collectionName, target, exists))
            return false;
        if (!exists)
        {
            target = snapshot;
            if (!redisStore_.SaveCollection(collectionName, target))
                return false;
        }
        return ServerJson::SaveJsonFile(filePath, target);
    }

    target = snapshot;
    return true;
}

bool ServerStorage::PersistCollectionLocked(const std::string& collectionName, const std::string& filePath, const ServerJson::Json& target)
{
    if (ShouldUseMySqlCollection(collectionName) && !mysqlStore_.SaveCollection(collectionName, target))
        return false;
    if (ShouldUseRedisCollection(collectionName) && !redisStore_.SaveCollection(collectionName, target))
        return false;
    return ServerJson::SaveJsonFile(filePath, target);
}

bool ServerStorage::ShouldUseMySqlCollection(const std::string& collectionName) const
{
    if (!mysqlStore_.IsReady())
        return false;

    return collectionName == "users"
        || collectionName == "sessions"
        || collectionName == "friends"
        || collectionName == "invites"
        || collectionName == "payment_orders"
        || collectionName == "battle_sessions";
}

bool ServerStorage::ShouldUseRedisCollection(const std::string& collectionName) const
{
    if (!redisStore_.IsReady())
        return false;

    return collectionName == "rooms"
        || collectionName == "match_queue"
        || collectionName == "pending_matches";
}

bool ServerStorage::UsernameExists(const std::string& username, std::string* existingUserId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = usersJson_.begin(); it != usersJson_.end(); ++it)
    {
        UserRecord user = ServerJson::UserFromJson(it.value());
        if (user.username != username)
            continue;

        if (existingUserId != nullptr)
            *existingUserId = user.id;
        return true;
    }
    return false;
}

bool ServerStorage::FindUserByUsername(const std::string& username, UserRecord& user)
{
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = usersJson_.begin(); it != usersJson_.end(); ++it)
    {
        UserRecord candidate = ServerJson::UserFromJson(it.value());
        if (candidate.username != username)
            continue;

        user = candidate;
        return true;
    }
    return false;
}

bool ServerStorage::FindUserById(const std::string& userId, UserRecord& user)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!usersJson_.contains(userId))
        return false;

    user = ServerJson::UserFromJson(usersJson_[userId]);
    return true;
}

bool ServerStorage::IsIdCardBanned(const std::string& idCard, std::string& banReason)
{
    if (idCard.empty())
        return false;

    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = usersJson_.begin(); it != usersJson_.end(); ++it)
    {
        UserRecord u = ServerJson::UserFromJson(it.value());
        if (u.idCard == idCard && u.isBanned)
        {
            banReason = u.banReason.empty() ? "Identity card blacklisted due to account ban." : u.banReason;
            return true;
        }
    }
    return false;
}


std::vector<UserRecord> ServerStorage::ListUsers()
{
    std::vector<UserRecord> users;
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = usersJson_.begin(); it != usersJson_.end(); ++it)
        users.push_back(ServerJson::UserFromJson(it.value()));
    return users;
}

bool ServerStorage::UpsertUser(const UserRecord& user)
{
    std::lock_guard<std::mutex> lock(mutex_);
    usersJson_[user.id] = ServerJson::UserToJson(user);
    return SaveUsersLocked();
}

std::vector<RoomRecord> ServerStorage::ListRecentWaitingRooms()
{
    std::vector<RoomRecord> rooms;
    const std::int64_t cutoff = ServerJson::CurrentTimeMs() - (24LL * 60LL * 60LL * 1000LL);

    std::lock_guard<std::mutex> lock(mutex_);
    bool changed = false;
    for (ServerJson::Json::iterator it = roomsJson_.begin(); it != roomsJson_.end(); )
    {
        RoomRecord room = ServerJson::RoomFromJson(it.value());
        if (room.status != "waiting" || room.createdAt < cutoff)
        {
            it = roomsJson_.erase(it);
            changed = true;
            continue;
        }

        rooms.push_back(room);
        ++it;
    }

    if (changed)
        SaveRoomsLocked();

    std::sort(rooms.begin(), rooms.end(), [](const RoomRecord& left, const RoomRecord& right)
    {
        return left.createdAt < right.createdAt;
    });

    if (rooms.size() > 20)
        rooms.erase(rooms.begin(), rooms.end() - 20);

    return rooms;
}

bool ServerStorage::FindRoomById(const std::string& roomId, RoomRecord& room)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!roomsJson_.contains(roomId))
        return false;

    room = ServerJson::RoomFromJson(roomsJson_[roomId]);
    return true;
}

bool ServerStorage::FindActivePlayingRoomForUser(const std::string& userId, RoomRecord& room)
{
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = roomsJson_.begin(); it != roomsJson_.end(); ++it)
    {
        RoomRecord candidate = ServerJson::RoomFromJson(it.value());
        if (candidate.status != "playing")
            continue;
        if (std::find(candidate.players.begin(), candidate.players.end(), userId) == candidate.players.end())
            continue;

        room = candidate;
        return true;
    }
    return false;
}

bool ServerStorage::UpsertRoom(const RoomRecord& room)
{
    std::lock_guard<std::mutex> lock(mutex_);
    roomsJson_[room.id] = ServerJson::RoomToJsonStorage(room);
    return SaveRoomsLocked();
}

bool ServerStorage::RemoveRoom(const std::string& roomId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!roomsJson_.contains(roomId))
        return true;

    roomsJson_.erase(roomId);
    return SaveRoomsLocked();
}

bool ServerStorage::FindPendingMatch(const std::string& userId, PendingMatch& match)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!pendingMatchesJson_.contains(userId))
        return false;

    match = ServerJson::PendingMatchFromJson(pendingMatchesJson_[userId]);
    return true;
}

void ServerStorage::RemovePendingMatch(const std::string& userId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    pendingMatchesJson_.erase(userId);
    SavePendingMatchesLocked();
}

void ServerStorage::UpsertPendingMatch(const std::string& userId, const PendingMatch& match)
{
    std::lock_guard<std::mutex> lock(mutex_);
    pendingMatchesJson_[userId] = ServerJson::PendingMatchToJson(match);
    SavePendingMatchesLocked();
}

bool ServerStorage::FindMatchQueueEntry(const std::string& userId, MatchQueueEntry& entry)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!matchQueueJson_.contains(userId))
        return false;

    entry = ServerJson::MatchQueueEntryFromJson(matchQueueJson_[userId]);
    return true;
}

void ServerStorage::UpsertMatchQueueEntry(const std::string& userId, const MatchQueueEntry& entry)
{
    std::lock_guard<std::mutex> lock(mutex_);
    matchQueueJson_[userId] = ServerJson::MatchQueueEntryToJson(entry);
    SaveMatchQueueLocked();
}

void ServerStorage::RemoveMatchQueueEntry(const std::string& userId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    matchQueueJson_.erase(userId);
    SaveMatchQueueLocked();
}

bool ServerStorage::FindMatchingOpponent(const std::string& userId, const std::string& mapName, MatchQueueEntry& opponent)
{
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = matchQueueJson_.begin(); it != matchQueueJson_.end(); ++it)
    {
        MatchQueueEntry candidate = ServerJson::MatchQueueEntryFromJson(it.value());
        if (candidate.userId == userId || candidate.mapName != mapName)
            continue;

        opponent = candidate;
        return true;
    }
    return false;
}

int ServerStorage::GetMatchQueueSize(const std::string& mapName)
{
    std::lock_guard<std::mutex> lock(mutex_);
    int count = 0;
    for (ServerJson::Json::const_iterator it = matchQueueJson_.begin(); it != matchQueueJson_.end(); ++it)
    {
        MatchQueueEntry candidate = ServerJson::MatchQueueEntryFromJson(it.value());
        if (candidate.mapName == mapName)
            count++;
    }
    return count;
}


bool ServerStorage::FindSession(const std::string& token, SessionRecord& session)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!sessionsJson_.contains(token))
        return false;

    const ServerJson::Json& value = sessionsJson_[token];
    session.token = token;
    session.userId = value.value("userId", "");
    session.expiresAt = value.value("expiresAt", static_cast<std::int64_t>(0));
    return !session.userId.empty();
}

void ServerStorage::UpsertSession(const SessionRecord& session)
{
    std::lock_guard<std::mutex> lock(mutex_);
    sessionsJson_[session.token] = {
        { "userId", session.userId },
        { "expiresAt", session.expiresAt }
    };
    SaveSessionsLocked();
}

void ServerStorage::RemoveSession(const std::string& token)
{
    std::lock_guard<std::mutex> lock(mutex_);
    sessionsJson_.erase(token);
    SaveSessionsLocked();
}

void ServerStorage::PruneExpiredSessions(std::int64_t nowMs)
{
    std::lock_guard<std::mutex> lock(mutex_);
    bool changed = false;
    for (ServerJson::Json::iterator it = sessionsJson_.begin(); it != sessionsJson_.end(); )
    {
        const std::int64_t expiresAt = it.value().value("expiresAt", static_cast<std::int64_t>(0));
        if (expiresAt > 0 && expiresAt < nowMs)
        {
            it = sessionsJson_.erase(it);
            changed = true;
            continue;
        }
        ++it;
    }

    if (changed)
        SaveSessionsLocked();
}

std::vector<std::string> ServerStorage::GetFriendIds(const std::string& userId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!friendsJson_.contains(userId))
        return std::vector<std::string>();

    return JsonArrayToStrings(friendsJson_[userId]);
}

bool ServerStorage::AddFriendPair(const std::string& userId, const std::string& friendId)
{
    std::lock_guard<std::mutex> lock(mutex_);
    std::vector<std::string> left = friendsJson_.contains(userId)
        ? JsonArrayToStrings(friendsJson_[userId])
        : std::vector<std::string>();
    std::vector<std::string> right = friendsJson_.contains(friendId)
        ? JsonArrayToStrings(friendsJson_[friendId])
        : std::vector<std::string>();

    if (std::find(left.begin(), left.end(), friendId) == left.end())
        left.push_back(friendId);
    if (std::find(right.begin(), right.end(), userId) == right.end())
        right.push_back(userId);

    friendsJson_[userId] = StringsToJsonArray(left);
    friendsJson_[friendId] = StringsToJsonArray(right);
    return SaveFriendsLocked();
}

std::vector<InviteRecord> ServerStorage::ListInvitesForUser(const std::string& userId)
{
    std::vector<InviteRecord> invites;
    std::lock_guard<std::mutex> lock(mutex_);
    if (!invitesJson_.contains(userId) || !invitesJson_[userId].is_array())
        return invites;

    for (ServerJson::Json::const_iterator it = invitesJson_[userId].begin(); it != invitesJson_[userId].end(); ++it)
        invites.push_back(ServerJson::InviteFromJson(*it));
    return invites;
}

bool ServerStorage::UpsertInvite(const std::string& userId, const InviteRecord& invite, std::size_t maxPerUser)
{
    std::lock_guard<std::mutex> lock(mutex_);
    ServerJson::Json invites = invitesJson_.contains(userId) && invitesJson_[userId].is_array()
        ? invitesJson_[userId]
        : ServerJson::Json::array();

    for (ServerJson::Json::iterator it = invites.begin(); it != invites.end(); )
    {
        InviteRecord existing = ServerJson::InviteFromJson(*it);
        if (existing.fromId == invite.fromId && existing.roomId == invite.roomId)
        {
            it = invites.erase(it);
            continue;
        }
        ++it;
    }

    invites.push_back(ServerJson::InviteToJson(invite));
    while (invites.size() > maxPerUser)
        invites.erase(invites.begin());

    invitesJson_[userId] = invites;
    return SaveInvitesLocked();
}

bool ServerStorage::RemoveInviteById(const std::string& userId, const std::string& inviteId, InviteRecord& invite)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!invitesJson_.contains(userId) || !invitesJson_[userId].is_array())
        return false;

    ServerJson::Json invites = invitesJson_[userId];
    for (ServerJson::Json::iterator it = invites.begin(); it != invites.end(); ++it)
    {
        InviteRecord candidate = ServerJson::InviteFromJson(*it);
        if (candidate.id != inviteId)
            continue;

        invite = candidate;
        invites.erase(it);
        invitesJson_[userId] = invites;
        SaveInvitesLocked();
        return true;
    }
    return false;
}

std::vector<PaymentOrderRecord> ServerStorage::ListPaymentOrdersForUser(const std::string& userId)
{
    std::vector<PaymentOrderRecord> orders;
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = paymentOrdersJson_.begin(); it != paymentOrdersJson_.end(); ++it)
    {
        PaymentOrderRecord order = ServerJson::PaymentOrderFromJson(it.value());
        if (order.userId == userId)
            orders.push_back(order);
    }

    std::sort(orders.begin(), orders.end(), [](const PaymentOrderRecord& left, const PaymentOrderRecord& right)
    {
        return left.createdAt > right.createdAt;
    });
    return orders;
}

bool ServerStorage::FindPaymentOrder(const std::string& orderId, PaymentOrderRecord& order)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!paymentOrdersJson_.contains(orderId))
        return false;

    order = ServerJson::PaymentOrderFromJson(paymentOrdersJson_[orderId]);
    return true;
}

bool ServerStorage::UpsertPaymentOrder(const PaymentOrderRecord& order)
{
    std::lock_guard<std::mutex> lock(mutex_);
    paymentOrdersJson_[order.id] = ServerJson::PaymentOrderToJson(order);
    return SavePaymentOrdersLocked();
}

bool ServerStorage::ConfirmPaymentOrder(const std::string& orderId, const std::string& providerOrderId, std::int64_t paidAt, PaymentOrderRecord& order, UserRecord& user, bool& creditedNow, std::string& error)
{
    std::lock_guard<std::mutex> lock(mutex_);
    creditedNow = false;

    if (!paymentOrdersJson_.contains(orderId))
    {
        error = "Order not found.";
        return false;
    }

    order = ServerJson::PaymentOrderFromJson(paymentOrdersJson_[orderId]);
    if (order.userId.empty())
    {
        error = "Order user is invalid.";
        return false;
    }

    if (!usersJson_.contains(order.userId))
    {
        error = "Order user does not exist.";
        return false;
    }

    user = ServerJson::UserFromJson(usersJson_[order.userId]);

    if (!order.creditsApplied && !ContainsString(user.creditedOrderIds, order.id))
    {
        const int totalGems = std::max(0, order.gems) + std::max(0, order.bonusGems);
        user.gems += totalGems;
        user.creditedOrderIds.push_back(order.id);
        order.creditsApplied = true;
        order.creditedAt = paidAt;
        creditedNow = true;
    }

    order.status = "paid";
    order.providerOrderId = providerOrderId.empty() ? order.providerOrderId : providerOrderId;
    order.paidAt = paidAt;

    usersJson_[user.id] = ServerJson::UserToJson(user);
    paymentOrdersJson_[order.id] = ServerJson::PaymentOrderToJson(order);
    return SaveUsersLocked() && SavePaymentOrdersLocked();
}

bool ServerStorage::FindBattleSessionById(const std::string& battleId, BattleSessionRecord& session)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!battleSessionsJson_.contains(battleId))
        return false;

    session = ServerJson::BattleSessionFromJson(battleSessionsJson_[battleId]);
    return true;
}

bool ServerStorage::FindActiveBattleSessionForRoom(const std::string& roomId, BattleSessionRecord& session)
{
    std::lock_guard<std::mutex> lock(mutex_);
    for (ServerJson::Json::const_iterator it = battleSessionsJson_.begin(); it != battleSessionsJson_.end(); ++it)
    {
        BattleSessionRecord candidate = ServerJson::BattleSessionFromJson(it.value());
        if (candidate.roomId != roomId || candidate.status != "active")
            continue;

        session = candidate;
        return true;
    }
    return false;
}

bool ServerStorage::UpsertBattleSession(const BattleSessionRecord& session)
{
    std::lock_guard<std::mutex> lock(mutex_);
    battleSessionsJson_[session.id] = ServerJson::BattleSessionToJson(session);
    return SaveBattleSessionsLocked();
}

bool ServerStorage::SubmitBattleReport(const std::string& battleId, const BattleParticipantReport& report, std::int64_t nowMs, BattleSessionRecord& session, std::string& resultCode, std::string& error)
{
    std::lock_guard<std::mutex> lock(mutex_);
    resultCode.clear();
    error.clear();

    if (!battleSessionsJson_.contains(battleId))
    {
        error = "Battle session not found.";
        return false;
    }

    session = ServerJson::BattleSessionFromJson(battleSessionsJson_[battleId]);
    if (session.status != "active")
    {
        if (session.status == "settled")
            resultCode = "already_settled";
        error = session.status == "settled" ? "Battle session already settled." : "Battle session is not active.";
        return false;
    }

    if (session.expiresAt > 0 && nowMs > session.expiresAt)
    {
        session.status = "expired";
        battleSessionsJson_[session.id] = ServerJson::BattleSessionToJson(session);
        SaveBattleSessionsLocked();
        error = "Battle session expired.";
        return false;
    }

    if (!ContainsString(session.players, report.userId))
    {
        error = "User is not part of this battle session.";
        return false;
    }

    for (std::size_t i = 0; i < session.reports.size(); ++i)
    {
        if (session.reports[i].userId == report.userId)
        {
            resultCode = "already_reported";
            error = "Battle result already submitted for this user.";
            return false;
        }
    }

    session.reports.push_back(report);
    const bool everyoneReported = !session.players.empty() && session.reports.size() >= session.players.size();
    if (!everyoneReported)
    {
        battleSessionsJson_[session.id] = ServerJson::BattleSessionToJson(session);
        resultCode = "pending";
        return SaveBattleSessionsLocked();
    }

    int winCount = 0;
    std::string winnerUserId;
    for (std::size_t i = 0; i < session.reports.size(); ++i)
    {
        if (session.reports[i].win == 0)
            continue;
        ++winCount;
        winnerUserId = session.reports[i].userId;
    }

    if ((session.players.size() > 1 && winCount != 1) || winnerUserId.empty())
    {
        session.status = "review";
        session.settledAt = nowMs;
        session.winnerUserId.clear();
        battleSessionsJson_[session.id] = ServerJson::BattleSessionToJson(session);

        // Raise suspicion score for all participants on conflict
        for (const auto& pid : session.players)
        {
            if (usersJson_.contains(pid))
            {
                UserRecord u = ServerJson::UserFromJson(usersJson_[pid]);
                u.suspicionScore += 20; // Conflict raises suspicion score
                if (u.suspicionScore >= ServerSecurity::kSuspicionBanThreshold)
                {
                    u.isBanned = true;
                    u.banReason = "Automatic ban: battle result reporting anomaly (multiple conflicts)";
                    u.bannedUntil = 0; // permanent
                }
                usersJson_[pid] = ServerJson::UserToJson(u);
            }
        }

        resultCode = "review";
        return SaveBattleSessionsLocked() && SaveUsersLocked();
    }

    session.status = "settled";
    session.settledAt = nowMs;
    session.winnerUserId = winnerUserId;
    session.rewardsApplied = true;

    for (std::size_t i = 0; i < session.reports.size(); ++i)
    {
        const BattleParticipantReport& participant = session.reports[i];
        if (!usersJson_.contains(participant.userId))
            continue;

        UserRecord user = ServerJson::UserFromJson(usersJson_[participant.userId]);
        ServerJson::EnsureUserDefaults(user);
        ServerJson::EnsureLobbyDay(user);
        if (!user.hasStats)
            user.hasStats = true;

        if (ContainsString(user.settledBattleIds, battleId))
            continue;

        const int kills = std::max(0, participant.kills);
        user.stats.kills += kills;
        user.lobbyState.killsToday += kills;
        if (participant.userId == winnerUserId)
        {
            user.stats.wins += 1;
            user.wins += 1;
            user.lobbyState.winsToday += 1;
            user.gold += 80;
        }
        else
        {
            user.stats.losses += 1;
            user.losses += 1;
            user.gold += 25;
        }

        user.settledBattleIds.push_back(battleId);
        usersJson_[user.id] = ServerJson::UserToJson(user);
    }

    battleSessionsJson_[session.id] = ServerJson::BattleSessionToJson(session);
    resultCode = "settled";
    return SaveUsersLocked() && SaveBattleSessionsLocked();
}

bool ServerStorage::SaveUsersLocked()
{
    return PersistCollectionLocked("users", usersFilePath_, usersJson_);
}

bool ServerStorage::SaveRoomsLocked()
{
    return PersistCollectionLocked("rooms", roomsFilePath_, roomsJson_);
}

bool ServerStorage::SaveMatchQueueLocked()
{
    return PersistCollectionLocked("match_queue", matchQueueFilePath_, matchQueueJson_);
}

bool ServerStorage::SavePendingMatchesLocked()
{
    return PersistCollectionLocked("pending_matches", pendingMatchesFilePath_, pendingMatchesJson_);
}

bool ServerStorage::SaveSessionsLocked()
{
    return PersistCollectionLocked("sessions", sessionsFilePath_, sessionsJson_);
}

bool ServerStorage::SaveFriendsLocked()
{
    return PersistCollectionLocked("friends", friendsFilePath_, friendsJson_);
}

bool ServerStorage::SaveInvitesLocked()
{
    return PersistCollectionLocked("invites", invitesFilePath_, invitesJson_);
}

bool ServerStorage::SavePaymentOrdersLocked()
{
    return PersistCollectionLocked("payment_orders", paymentOrdersFilePath_, paymentOrdersJson_);
}

bool ServerStorage::SaveBattleSessionsLocked()
{
    return PersistCollectionLocked("battle_sessions", battleSessionsFilePath_, battleSessionsJson_);
}
