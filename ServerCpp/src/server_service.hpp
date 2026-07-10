#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"
#include "server_security.hpp"
#include "server_storage.hpp"
#include "server_types.hpp"

#include <nlohmann/json.hpp>

#include <cstdint>
#include <map>
#include <mutex>
#include <string>

class ServerService
{
public:
    struct BattleRelayJoinContext
    {
        UserRecord user;
        RoomRecord room;
        std::string role;
        std::string hostUserId;
        std::string guestUserId;
    };

    ServerService(const AppConfig& config, AsyncLogger& logger);

    bool Initialize();

    nlohmann::json HandleRequest(
        const std::string& method,
        const std::string& path,
        const std::map<std::string, std::string>& headers,
        const std::string& body,
        const std::string& clientIp,
        int& statusCode);

    bool AuthorizeBattleRelayJoin(
        const std::string& token,
        const std::string& roomId,
        BattleRelayJoinContext& context,
        std::string& error);

    bool ValidateBattleRelayCommand(
        const std::string& roomId,
        const std::string& userId,
        const std::string& username,
        const nlohmann::json& payload,
        nlohmann::json& normalizedPayload,
        std::string& error);

private:
    struct ParsedRequest
    {
        std::string method;
        std::string path;
        std::map<std::string, std::string> headers;
        nlohmann::json body;
        std::string clientIp;  // remote address, used for rate-limiting
    };

    AppConfig config_;
    AsyncLogger& logger_;
    ServerStorage storage_;
    ServerSecurity security_;

    bool TryAuthenticate(const ParsedRequest& request, UserRecord& user, nlohmann::json& errorJson, int& statusCode);
    static std::string HeaderLookup(const std::map<std::string, std::string>& headers, const std::string& key);
    static std::string Trim(const std::string& value);
    static std::string ToLower(std::string value);
    static std::map<std::string, std::string> NormalizeHeaders(const std::map<std::string, std::string>& headers);
    static nlohmann::json ParseJsonBody(const std::string& body);
    bool HasAdminSecret(const ParsedRequest& request) const;

    nlohmann::json HandleHealth(int& statusCode) const;
    nlohmann::json HandleRegister(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleLogin(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleGuest(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleProfile(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRealNameBind(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleLobby(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandlePresence(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleTaskClaim(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleTechStart(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleTechSpeedup(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleFriends(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleLeaderboard(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleFriendAdd(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleFriendInvite(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleInvites(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleInviteRespond(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomsList(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomCreate(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomJoin(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomLeave(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomKick(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleRoomUpdate(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleMatchJoin(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleMatchCancel(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandlePaymentsCatalog(int& statusCode) const;
    nlohmann::json HandlePaymentsList(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandlePaymentCreate(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandlePaymentConfirm(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleBattleSessionStart(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleResult(const ParsedRequest& request, int& statusCode);
    nlohmann::json HandleAdminBan(const ParsedRequest& request, int& statusCode);

    // Returns a ready-made 403 ban response with reason and unban time.
    static nlohmann::json BannedResponse(const UserRecord& user);
    // Returns true if user is currently banned (checks bannedUntil vs now).
    static bool IsEffectivelyBanned(const UserRecord& user, std::int64_t nowMs);

    std::string IssueSession(const std::string& userId);
    bool ResolveSession(const std::string& token, SessionRecord& session);

    void TrackPresence(const std::string& userId);
    std::string FriendStatusFor(const std::string& userId);
    static int NormalizeRoomMaxPlayers(const nlohmann::json& body);
    static std::string NormalizePath(const std::string& path);

    std::mutex presenceMutex_;
    std::map<std::string, std::int64_t> presenceByUserId_;
};
