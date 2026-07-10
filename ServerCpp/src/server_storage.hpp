#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"
#include "server_json_utils.hpp"
#include "server_mysql_store.hpp"
#include "server_redis_store.hpp"
#include "server_types.hpp"

#include <map>
#include <mutex>
#include <string>
#include <vector>

class ServerStorage
{
public:
    ServerStorage(const AppConfig& config, AsyncLogger& logger);

    bool Initialize();

    bool UsernameExists(const std::string& username, std::string* existingUserId = nullptr);
    bool FindUserByUsername(const std::string& username, UserRecord& user);
    bool FindUserById(const std::string& userId, UserRecord& user);
    bool IsIdCardBanned(const std::string& idCard, std::string& banReason);
    std::vector<UserRecord> ListUsers();
    bool UpsertUser(const UserRecord& user);

    std::vector<RoomRecord> ListRecentWaitingRooms();
    bool FindRoomById(const std::string& roomId, RoomRecord& room);
    bool FindActivePlayingRoomForUser(const std::string& userId, RoomRecord& room);
    bool UpsertRoom(const RoomRecord& room);
    bool RemoveRoom(const std::string& roomId);

    bool FindPendingMatch(const std::string& userId, PendingMatch& match);
    void RemovePendingMatch(const std::string& userId);
    void UpsertPendingMatch(const std::string& userId, const PendingMatch& match);

    bool FindMatchQueueEntry(const std::string& userId, MatchQueueEntry& entry);
    void UpsertMatchQueueEntry(const std::string& userId, const MatchQueueEntry& entry);
    void RemoveMatchQueueEntry(const std::string& userId);
    bool FindMatchingOpponent(const std::string& userId, const std::string& mapName, MatchQueueEntry& opponent);
    int GetMatchQueueSize(const std::string& mapName);

    bool FindSession(const std::string& token, SessionRecord& session);
    void UpsertSession(const SessionRecord& session);
    void RemoveSession(const std::string& token);
    void PruneExpiredSessions(std::int64_t nowMs);

    std::vector<std::string> GetFriendIds(const std::string& userId);
    bool AddFriendPair(const std::string& userId, const std::string& friendId);

    std::vector<InviteRecord> ListInvitesForUser(const std::string& userId);
    bool UpsertInvite(const std::string& userId, const InviteRecord& invite, std::size_t maxPerUser = 20);
    bool RemoveInviteById(const std::string& userId, const std::string& inviteId, InviteRecord& invite);

    std::vector<PaymentOrderRecord> ListPaymentOrdersForUser(const std::string& userId);
    bool FindPaymentOrder(const std::string& orderId, PaymentOrderRecord& order);
    bool UpsertPaymentOrder(const PaymentOrderRecord& order);
    bool ConfirmPaymentOrder(const std::string& orderId, const std::string& providerOrderId, std::int64_t paidAt, PaymentOrderRecord& order, UserRecord& user, bool& creditedNow, std::string& error);

    bool FindBattleSessionById(const std::string& battleId, BattleSessionRecord& session);
    bool FindActiveBattleSessionForRoom(const std::string& roomId, BattleSessionRecord& session);
    bool UpsertBattleSession(const BattleSessionRecord& session);
    bool SubmitBattleReport(const std::string& battleId, const BattleParticipantReport& report, std::int64_t nowMs, BattleSessionRecord& session, std::string& resultCode, std::string& error);

private:
    AppConfig config_;
    AsyncLogger& logger_;
    ServerMySqlStore mysqlStore_;
    ServerRedisStore redisStore_;
    std::mutex mutex_;
    std::string usersFilePath_;
    std::string roomsFilePath_;
    std::string matchQueueFilePath_;
    std::string pendingMatchesFilePath_;
    std::string sessionsFilePath_;
    std::string friendsFilePath_;
    std::string invitesFilePath_;
    std::string paymentOrdersFilePath_;
    std::string battleSessionsFilePath_;

    ServerJson::Json usersJson_;
    ServerJson::Json roomsJson_;
    ServerJson::Json matchQueueJson_;
    ServerJson::Json pendingMatchesJson_;
    ServerJson::Json sessionsJson_;
    ServerJson::Json friendsJson_;
    ServerJson::Json invitesJson_;
    ServerJson::Json paymentOrdersJson_;
    ServerJson::Json battleSessionsJson_;

    bool LoadOrCreateFile(const std::string& path, ServerJson::Json& target);
    bool LoadCollectionState(const std::string& collectionName, const std::string& filePath, ServerJson::Json& target);
    bool PersistCollectionLocked(const std::string& collectionName, const std::string& filePath, const ServerJson::Json& target);
    bool ShouldUseMySqlCollection(const std::string& collectionName) const;
    bool ShouldUseRedisCollection(const std::string& collectionName) const;
    bool SaveUsersLocked();
    bool SaveRoomsLocked();
    bool SaveMatchQueueLocked();
    bool SavePendingMatchesLocked();
    bool SaveSessionsLocked();
    bool SaveFriendsLocked();
    bool SaveInvitesLocked();
    bool SavePaymentOrdersLocked();
    bool SaveBattleSessionsLocked();
};
