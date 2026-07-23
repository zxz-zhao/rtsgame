#include "server_service.hpp"

#include "server_crypto.hpp"
#include "server_json_utils.hpp"

#include <algorithm>
#include <cctype>
#include <cmath>
#include <cstdlib>
#include <sstream>
#include <vector>

using nlohmann::json;

namespace
{
struct GemProduct
{
    const char* id;
    const char* name;
    int amountFen;
    int gems;
    int bonusGems;
};

const GemProduct kGemProducts[] = {
    { "gem_60", "60 Gems", 600, 60, 0 },
    { "gem_300", "330 Gems", 3000, 300, 30 },
    { "gem_680", "760 Gems", 6800, 680, 80 },
    { "gem_1280", "1480 Gems", 12800, 1280, 200 }
};

std::string ToUpper(std::string value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char ch)
    {
        return static_cast<char>(std::toupper(ch));
    });
    return value;
}

int SafeIntFromJson(const json& value, const char* key, int fallback = 0)
{
    if (!value.contains(key))
        return fallback;

    if (value[key].is_number_integer())
        return value[key].get<int>();
    if (value[key].is_string())
        return std::atoi(value[key].get<std::string>().c_str());
    return fallback;
}

std::string SafeStringFromJson(const json& value, const char* key, const std::string& fallback = std::string())
{
    if (!value.contains(key))
        return fallback;
    if (value[key].is_string())
        return value[key].get<std::string>();
    return fallback;
}

int ClampInt(int value, int minValue, int maxValue)
{
    if (value < minValue)
        return minValue;
    if (value > maxValue)
        return maxValue;
    return value;
}

const GemProduct* FindGemProductById(const std::string& productId)
{
    for (std::size_t i = 0; i < sizeof(kGemProducts) / sizeof(kGemProducts[0]); ++i)
    {
        if (productId == kGemProducts[i].id)
            return &kGemProducts[i];
    }
    return nullptr;
}

json GemProductToJson(const GemProduct& product)
{
    return {
        { "productId", product.id },
        { "name", product.name },
        { "currency", "CNY" },
        { "amountFen", product.amountFen },
        { "amountYuan", static_cast<double>(product.amountFen) / 100.0 },
        { "gems", product.gems },
        { "bonusGems", product.bonusGems },
        { "totalGems", product.gems + product.bonusGems }
    };
}

std::string JoinPlayers(const std::vector<std::string>& players)
{
    std::ostringstream out;
    for (std::size_t i = 0; i < players.size(); ++i)
    {
        if (i > 0)
            out << ",";
        out << players[i];
    }
    return out.str();
}

bool TryGetFiniteNumber(const json& value, const char* key, double& result)
{
    if (!value.contains(key))
        return false;
    if (value[key].is_number())
    {
        result = value[key].get<double>();
        return std::isfinite(result);
    }
    if (value[key].is_string())
    {
        const std::string text = value[key].get<std::string>();
        char* end = nullptr;
        const double parsed = std::strtod(text.c_str(), &end);
        if (end == text.c_str() || (end != nullptr && *end != '\0') || !std::isfinite(parsed))
            return false;
        result = parsed;
        return true;
    }
    return false;
}

bool TryGetPositiveInt(const json& value, const char* key, int& result)
{
    if (!value.contains(key))
        return false;
    if (value[key].is_number_integer())
    {
        result = value[key].get<int>();
        return result > 0;
    }
    if (value[key].is_string())
    {
        result = std::atoi(value[key].get<std::string>().c_str());
        return result > 0;
    }
    return false;
}

bool IsSafeKey(const std::string& value)
{
    if (value.empty() || value.size() > 64)
        return false;

    for (std::size_t i = 0; i < value.size(); ++i)
    {
        const unsigned char ch = static_cast<unsigned char>(value[i]);
        if ((ch >= 'a' && ch <= 'z') ||
            (ch >= 'A' && ch <= 'Z') ||
            (ch >= '0' && ch <= '9') ||
            ch == '_' || ch == '-' || ch == ':' || ch == '.')
            continue;
        return false;
    }
    return true;
}

std::string TrimAscii(const std::string& value)
{
    std::size_t start = 0;
    while (start < value.size() && std::isspace(static_cast<unsigned char>(value[start])))
        ++start;

    std::size_t end = value.size();
    while (end > start && std::isspace(static_cast<unsigned char>(value[end - 1])))
        --end;
    return value.substr(start, end - start);
}

std::string SanitizeCompactText(const std::string& value, std::size_t maxBytes)
{
    std::string trimmed = TrimAscii(value);
    std::string sanitized;
    sanitized.reserve(trimmed.size());
    for (std::size_t i = 0; i < trimmed.size(); ++i)
    {
        const unsigned char ch = static_cast<unsigned char>(trimmed[i]);
        if (ch == '\r' || ch == '\n' || ch == '\t')
            sanitized.push_back(' ');
        else if (!std::iscntrl(ch))
            sanitized.push_back(trimmed[i]);
    }

    sanitized = TrimAscii(sanitized);
    if (sanitized.size() > maxBytes)
        sanitized.resize(maxBytes);
    return sanitized;
}

bool CoordinateInBounds(double value)
{
    return std::isfinite(value) && std::fabs(value) <= 100000.0;
}

int GetNameWeight(const std::string& name)
{
    int weight = 0;
    for (size_t i = 0; i < name.size(); ++i)
    {
        unsigned char c = name[i];
        if (c < 128)
        {
            weight += 1;
        }
        else if (c >= 192)
        {
            weight += 2;
        }
    }
    return weight;
}

std::string ToLowerAscii(const std::string& str)
{
    std::string result = str;
    for (size_t i = 0; i < result.size(); ++i)
    {
        if (result[i] >= 'A' && result[i] <= 'Z')
        {
            result[i] = result[i] - 'A' + 'a';
        }
    }
    return result;
}

bool ContainsBlockedName(const std::string& name)
{
    static const std::vector<std::string> blockedNames = {
        "傻逼", "煞笔", "沙比", "操你妈", "肏", "妈的", "特么的", "王八蛋", "滚蛋", "垃圾", "废柴", "混蛋", "二百五", "婊子", "贱人",
        "fuck", "bitch", "shit", "asshole", "bastard", "sb", "wocao", "caonima"
    };
    std::string lowerName = ToLowerAscii(name);
    for (const auto& word : blockedNames)
    {
        if (lowerName.find(word) != std::string::npos)
        {
            return true;
        }
    }
    return false;
}
}

ServerService::ServerService(const AppConfig& config, AsyncLogger& logger)
    : config_(config)
    , logger_(logger)
    , storage_(config, logger)
    , security_(logger)
{
}

bool ServerService::Initialize()
{
    return storage_.Initialize();
}

json ServerService::HandleRequest(
    const std::string& method,
    const std::string& path,
    const std::map<std::string, std::string>& headers,
    const std::string& body,
    const std::string& clientIp,
    int& statusCode)
{
    ParsedRequest request;
    request.method   = ToUpper(method);
    request.path     = NormalizePath(path);
    request.headers  = NormalizeHeaders(headers);
    request.body     = ParseJsonBody(body);
    request.clientIp = clientIp;

    // -----------------------------------------------------------------------
    // Security Integrity Checks (Request Signatures and Build Whitelist)
    // Only applied to paths starting with "/api/" to bypass health checks
    // -----------------------------------------------------------------------
    if (request.path.find("/api/") == 0)
    {
        // 1. Check Build Number Whitelist
        const std::string buildStr = HeaderLookup(request.headers, "x-client-build");
        int buildNum = buildStr.empty() ? 0 : std::atoi(buildStr.c_str());
        if (!ServerSecurity::IsBuildAllowed(buildNum, config_.allowedBuilds))
        {
            statusCode = 400;
            return {
                { "success", false },
                { "error", "Your client version is outdated. Please update to continue." },
                { "errorCode", "CLIENT_OUTDATED" }
            };
        }

        // 2. Verify HMAC request signature
        const std::string tsHeader = HeaderLookup(request.headers, "x-client-ts");
        const std::string sigHeader = HeaderLookup(request.headers, "x-client-sig");
        if (!ServerSecurity::VerifyRequestSignature(
                request.method,
                request.path,
                tsHeader,
                sigHeader,
                body,
                config_.clientSigKey,
                ServerJson::CurrentTimeMs()))
        {
            statusCode = 403;
            return {
                { "success", false },
                { "error", "Request signature verification failed." },
                { "errorCode", "SIGNATURE_INVALID" }
            };
        }
    }

    if (request.path == "/health" && request.method == "GET")
        return HandleHealth(statusCode);
    if (request.path == "/api/register" && request.method == "POST")
        return HandleRegister(request, statusCode);
    if (request.path == "/api/login" && request.method == "POST")
        return HandleLogin(request, statusCode);
    if (request.path == "/api/guest" && request.method == "POST")
        return HandleGuest(request, statusCode);
    if (request.path == "/api/profile" && request.method == "GET")
        return HandleProfile(request, statusCode);
    if (request.path == "/api/profile/realname" && request.method == "POST")
        return HandleRealNameBind(request, statusCode);
    if (request.path == "/api/lobby" && request.method == "GET")
        return HandleLobby(request, statusCode);
    if (request.path == "/api/presence" && request.method == "POST")
        return HandlePresence(request, statusCode);
    if (request.path == "/api/tasks/claim" && request.method == "POST")
        return HandleTaskClaim(request, statusCode);
    if (request.path == "/api/mail/claim" && request.method == "POST")
        return HandleMailClaim(request, statusCode);
    if (request.path == "/api/tech/start" && request.method == "POST")
        return HandleTechStart(request, statusCode);
    if (request.path == "/api/tech/speedup" && request.method == "POST")
        return HandleTechSpeedup(request, statusCode);
    if (request.path == "/api/friends" && request.method == "GET")
        return HandleFriends(request, statusCode);
    if (request.path == "/api/leaderboard" && request.method == "GET")
        return HandleLeaderboard(request, statusCode);
    if (request.path == "/api/friends/add" && request.method == "POST")
        return HandleFriendAdd(request, statusCode);
    if (request.path == "/api/friends/invite" && request.method == "POST")
        return HandleFriendInvite(request, statusCode);
    if (request.path == "/api/invites" && request.method == "GET")
        return HandleInvites(request, statusCode);
    if (request.path == "/api/invites/respond" && request.method == "POST")
        return HandleInviteRespond(request, statusCode);
    if (request.path == "/api/rooms" && request.method == "GET")
        return HandleRoomsList(request, statusCode);
    if (request.path == "/api/rooms/create" && request.method == "POST")
        return HandleRoomCreate(request, statusCode);
    if (request.path == "/api/rooms/join" && request.method == "POST")
        return HandleRoomJoin(request, statusCode);
    if (request.path == "/api/rooms/leave" && request.method == "POST")
        return HandleRoomLeave(request, statusCode);
    if (request.path == "/api/rooms/kick" && request.method == "POST")
        return HandleRoomKick(request, statusCode);
    if (request.path == "/api/rooms/update" && request.method == "POST")
        return HandleRoomUpdate(request, statusCode);
    if (request.path == "/api/match/join" && request.method == "POST")
        return HandleMatchJoin(request, statusCode);
    if (request.path == "/api/match/cancel" && request.method == "POST")
        return HandleMatchCancel(request, statusCode);
    if (request.path == "/api/payments/catalog" && request.method == "GET")
        return HandlePaymentsCatalog(statusCode);
    if (request.path == "/api/payments" && request.method == "GET")
        return HandlePaymentsList(request, statusCode);
    if (request.path == "/api/payments/create" && request.method == "POST")
        return HandlePaymentCreate(request, statusCode);
    if (request.path == "/api/payments/confirm" && request.method == "POST")
        return HandlePaymentConfirm(request, statusCode);
    if (request.path == "/api/battle/session/start" && request.method == "POST")
        return HandleBattleSessionStart(request, statusCode);
    if (request.path == "/api/result" && request.method == "POST")
        return HandleResult(request, statusCode);
    if (request.path == "/api/admin/ban" && request.method == "POST")
        return HandleAdminBan(request, statusCode);

    statusCode = 404;
    return {
        { "success", false },
        { "error", "Not Found" }
    };
}

bool ServerService::AuthorizeBattleRelayJoin(
    const std::string& token,
    const std::string& roomId,
    BattleRelayJoinContext& context,
    std::string& error)
{
    error.clear();
    context = BattleRelayJoinContext();

    SessionRecord session;
    if (!ResolveSession(token, session))
    {
        error = "Invalid token.";
        return false;
    }

    if (!storage_.FindUserById(session.userId, context.user))
    {
        error = "User not found.";
        return false;
    }

    if (!storage_.FindRoomById(Trim(roomId), context.room))
    {
        error = "Room not found.";
        return false;
    }

    if (context.room.status == "closed")
    {
        error = "Room already closed.";
        return false;
    }

    if (context.room.players.empty())
    {
        error = "Room has no participants.";
        return false;
    }

    if (std::find(context.room.players.begin(), context.room.players.end(), context.user.id) == context.room.players.end())
    {
        error = "User is not part of this room.";
        return false;
    }

    context.hostUserId = !context.room.hostId.empty() ? context.room.hostId : context.room.players.front();
    for (std::size_t i = 0; i < context.room.players.size(); ++i)
    {
        if (context.room.players[i] != context.hostUserId)
        {
            context.guestUserId = context.room.players[i];
            break;
        }
    }

    if (context.user.id == context.hostUserId)
        context.role = "host";
    else if (!context.guestUserId.empty() && context.user.id == context.guestUserId)
        context.role = "guest";
    else
    {
        error = "Battle relay currently supports two combatants only.";
        return false;
    }

    if (context.room.status != "playing")
    {
        context.room.status = "playing";
        storage_.UpsertRoom(context.room);
    }

    if (!context.guestUserId.empty())
    {
        BattleSessionRecord activeSession;
        if (!storage_.FindActiveBattleSessionForRoom(context.room.id, activeSession))
        {
            BattleSessionRecord created;
            created.id = ServerCrypto::GenerateUuidLike();
            created.roomId = context.room.id;
            created.mapName = context.room.mapName;
            created.createdByUserId = context.user.id;
            created.status = "active";
            created.players = context.room.players;
            created.createdAt = ServerJson::CurrentTimeMs();
            created.startedAt = created.createdAt;
            created.expiresAt = created.createdAt + 2LL * 60LL * 60LL * 1000LL;
            storage_.UpsertBattleSession(created);
        }
    }

    TrackPresence(context.user.id);
    return true;
}

bool ServerService::ValidateBattleRelayCommand(
    const std::string& roomId,
    const std::string& userId,
    const std::string& username,
    const nlohmann::json& payload,
    nlohmann::json& normalizedPayload,
    std::string& error)
{
    normalizedPayload = json::object();
    error.clear();

    // --- Command frequency cap ---
    // Reject if this userId is sending more than 30 commands per second.
    // This is the first line of defence against macro/script cheating.
    if (!security_.CheckCommandRate(userId))
    {
        // Raise suspicion score for the offending user
        UserRecord offender;
        if (storage_.FindUserById(userId, offender))
        {
            offender.suspicionScore += ServerSecurity::kSuspicionIncrement;
            if (offender.suspicionScore >= ServerSecurity::kSuspicionBanThreshold)
            {
                offender.isBanned    = true;
                offender.banReason   = "Automatic ban: command flood detected";
                offender.bannedUntil = 0;  // permanent
                logger_.Log(LogLevel::Warn,
                    "[Security] Auto-banned userId=" + userId +
                    " reason=command_flood score=" + std::to_string(offender.suspicionScore));
            }
            storage_.UpsertUser(offender);
        }
        error = "Command rate limit exceeded.";
        return false;
    }

    RoomRecord room;
    if (!storage_.FindRoomById(roomId, room))
    {
        error = "Room not found.";
        return false;
    }
    if (std::find(room.players.begin(), room.players.end(), userId) == room.players.end())
    {
        error = "User is not part of the room.";
        return false;
    }

    const std::string action = SafeStringFromJson(payload, "action");
    if (action.empty())
    {
        error = "Battle command is missing action.";
        return false;
    }

    normalizedPayload["action"] = action;
    normalizedPayload["userId"] = userId;
    normalizedPayload["senderId"] = userId;

    auto requireCoordinate = [&](const char* key) -> bool
    {
        double value = 0.0;
        if (!TryGetFiniteNumber(payload, key, value) || !CoordinateInBounds(value))
        {
            error = std::string("Invalid coordinate: ") + key;
            return false;
        }
        normalizedPayload[key] = value;
        return true;
    };

    auto requireId = [&](const char* key) -> bool
    {
        int value = 0;
        if (!TryGetPositiveInt(payload, key, value))
        {
            error = std::string("Invalid id field: ") + key;
            return false;
        }
        normalizedPayload[key] = value;
        return true;
    };

    if (action == "move" || action == "amove" || action == "aground")
        return requireId("id") && requireCoordinate("x") && requireCoordinate("z");
    if (action == "patrol")
        return requireId("id") && requireCoordinate("ax") && requireCoordinate("az") && requireCoordinate("bx") && requireCoordinate("bz");
    if (action == "attack")
    {
        if (!requireId("id") || !requireId("tid"))
            return false;
        normalizedPayload["bldg"] = SafeIntFromJson(payload, "bldg", 0) != 0 ? 1 : 0;
        return true;
    }
    if (action == "guard")
        return requireId("id") && requireId("tid");
    if (action == "rally")
        return requireId("id") && requireCoordinate("x") && requireCoordinate("z");
    if (action == "park" || action == "stop" || action == "upgrade" || action == "cancel_build" || action == "repair_b" || action == "repair_u" || action == "rebuild")
        return requireId("id");
    if (action == "bombrun")
        return requireId("id") && requireCoordinate("sx") && requireCoordinate("sz") && requireCoordinate("ex") && requireCoordinate("ez");
    if (action == "build")
    {
        const std::string key = SafeStringFromJson(payload, "key");
        if (!requireCoordinate("x") || !requireCoordinate("z"))
            return false;
        int netId = 0;
        if (!TryGetPositiveInt(payload, "nid", netId) || !IsSafeKey(key))
        {
            error = "Invalid build payload.";
            return false;
        }
        normalizedPayload["key"] = key;
        normalizedPayload["nid"] = netId;
        return true;
    }
    if (action == "produce" || action == "tech")
    {
        const std::string key = SafeStringFromJson(payload, "key");
        if (!IsSafeKey(key))
        {
            error = "Invalid command key.";
            return false;
        }
        normalizedPayload["key"] = key;
        if (action == "produce")
            return requireId("id");
        return requireCoordinate("x") && requireCoordinate("z");
    }
    if (action == "chat")
    {
        const std::string participantId = SanitizeCompactText(SafeStringFromJson(payload, "participantId", userId), 64);
        const std::string speaker = SanitizeCompactText(SafeStringFromJson(payload, "speaker", username), 32);
        const std::string message = SanitizeCompactText(SafeStringFromJson(payload, "message"), 256);
        if (message.empty())
        {
            error = "Chat message is empty.";
            return false;
        }
        normalizedPayload["participantId"] = participantId.empty() ? userId : participantId;
        normalizedPayload["speaker"] = speaker.empty() ? username : speaker;
        normalizedPayload["message"] = message;
        return true;
    }
    if (action == "voice")
    {
        const std::string participantId = SanitizeCompactText(SafeStringFromJson(payload, "participantId", userId), 64);
        const std::string speaker = SanitizeCompactText(SafeStringFromJson(payload, "speaker", username), 32);
        normalizedPayload["participantId"] = participantId.empty() ? userId : participantId;
        normalizedPayload["speaker"] = speaker.empty() ? username : speaker;
        return true;
    }

    error = "Unsupported battle action: " + action;
    return false;
}

bool ServerService::TryAuthenticate(const ParsedRequest& request, UserRecord& user, json& errorJson, int& statusCode)
{
    const std::string authorization = HeaderLookup(request.headers, "authorization");
    if (authorization.size() < 8 || authorization.substr(0, 7) != "Bearer ")
    {
        statusCode = 401;
        errorJson = {
            { "success", false },
            { "error", "Unauthorized" }
        };
        return false;
    }

    SessionRecord session;
    if (!ResolveSession(authorization.substr(7), session))
    {
        statusCode = 401;
        errorJson = {
            { "success", false },
            { "error", "Invalid token" }
        };
        return false;
    }

    if (!storage_.FindUserById(session.userId, user))
    {
        statusCode = 401;
        errorJson = {
            { "success", false },
            { "error", "Invalid token" }
        };
        return false;
    }

    // Ban check — performed on every authenticated request
    std::string idCardBanReason;
    if (IsEffectivelyBanned(user, ServerJson::CurrentTimeMs()) || 
        (!user.idCard.empty() && storage_.IsIdCardBanned(user.idCard, idCardBanReason)))
    {
        if (idCardBanReason.empty())
        {
            idCardBanReason = user.banReason;
        }
        else
        {
            if (!user.isBanned)
            {
                user.isBanned = true;
                user.banReason = idCardBanReason;
                storage_.UpsertUser(user);
            }
        }
        statusCode = 403;
        errorJson = BannedResponse(user);
        logger_.Log(LogLevel::Warn,
            "[Security] Banned ID card/user attempted access userId=" + user.id +
            " reason=" + idCardBanReason);
        return false;
    }

    TrackPresence(user.id);
    return true;
}

std::string ServerService::HeaderLookup(const std::map<std::string, std::string>& headers, const std::string& key)
{
    const std::map<std::string, std::string>::const_iterator it = headers.find(key);
    return it == headers.end() ? std::string() : it->second;
}

std::string ServerService::Trim(const std::string& value)
{
    std::size_t start = 0;
    while (start < value.size() && std::isspace(static_cast<unsigned char>(value[start])))
        ++start;

    std::size_t end = value.size();
    while (end > start && std::isspace(static_cast<unsigned char>(value[end - 1])))
        --end;
    return value.substr(start, end - start);
}

std::string ServerService::ToLower(std::string value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char ch)
    {
        return static_cast<char>(std::tolower(ch));
    });
    return value;
}

std::map<std::string, std::string> ServerService::NormalizeHeaders(const std::map<std::string, std::string>& headers)
{
    std::map<std::string, std::string> normalized;
    for (std::map<std::string, std::string>::const_iterator it = headers.begin(); it != headers.end(); ++it)
        normalized[ToLower(it->first)] = Trim(it->second);
    return normalized;
}

json ServerService::ParseJsonBody(const std::string& body)
{
    if (body.empty())
        return json::object();

    try
    {
        json parsed = json::parse(body);
        return parsed.is_object() ? parsed : json::object();
    }
    catch (...)
    {
        return json::object();
    }
}

bool ServerService::HasAdminSecret(const ParsedRequest& request) const
{
    if (config_.adminApiSecret.empty())
        return false;

    const std::string headerSecret = HeaderLookup(request.headers, "x-admin-secret");
    return !headerSecret.empty() && headerSecret == config_.adminApiSecret;
}

json ServerService::HandleHealth(int& statusCode) const
{
    statusCode = 200;
    json response = json::parse(config_.ToHealthJson());
    response["success"] = true;
    response["scope"] = "http-json-port";
    return response;
}

json ServerService::HandleRegister(const ParsedRequest& request, int& statusCode)
{
    // Sliding window IP rate-limiting: max 3 registrations per IP per 10 minutes (bypassed for local loopback / host)
    bool isLocalIp = (request.clientIp == "127.0.0.1" || request.clientIp == "::1" || request.clientIp == "10.0.2.2");
    if (!isLocalIp && !security_.CheckGenericRateLimit(request.clientIp, "register", 3, 10LL * 60LL * 1000LL))
    {
        statusCode = 429;
        return { { "success", false }, { "error", "Too many registration attempts from this IP. Please try again later." } };
    }

    statusCode = 200;
    const std::string username = Trim(SafeStringFromJson(request.body, "username"));
    const std::string password = SafeStringFromJson(request.body, "password");
    int weight = GetNameWeight(username);
    if (weight < 4 || weight > 14)
        return { { "success", false }, { "error", "账号长度不符合要求（中文字符算2，英文算1，要求4-14）" } };
    if (ContainsBlockedName(username))
        return { { "success", false }, { "error", "账号包含敏感词或不当言论" } };
    if (password.size() < 6)
        return { { "success", false }, { "error", "Password must be at least 6 characters." } };
    if (storage_.UsernameExists(username))
        return { { "success", false }, { "error", "Username already exists." } };

    const std::string idCard = Trim(SafeStringFromJson(request.body, "idCard"));
    const std::string realName = Trim(SafeStringFromJson(request.body, "realName"));
    if (!idCard.empty())
    {
        std::string banReason;
        if (storage_.IsIdCardBanned(idCard, banReason))
        {
            return { { "success", false }, { "error", "This identity card is blacklisted. Registration rejected." } };
        }
    }

    UserRecord user;
    user.id = ServerCrypto::GenerateUuidLike();
    user.username = username;
    user.passwordHash = ServerCrypto::HashPassword(password, config_);
    user.isGuest = false;
    user.level = 1;
    user.wins = 0;
    user.losses = 0;
    user.gold = 1000;
    user.gems = 100;
    user.createdAt = ServerJson::CurrentTimeMs();
    user.lobbyState.day = ServerJson::TodayStringUtc();
    user.idCard = idCard;
    user.realName = realName;
    storage_.UpsertUser(user);

    return {
        { "success", true },
        { "token", IssueSession(user.id) },
        { "userId", user.id },
        { "username", user.username },
        { "level", user.level },
        { "gold", user.gold },
        { "gems", user.gems },
        { "rankTitle", ServerJson::RankTitleFrom(user) }
    };
}

json ServerService::HandleLogin(const ParsedRequest& request, int& statusCode)
{
    statusCode = 200;
    const std::string username = Trim(SafeStringFromJson(request.body, "username"));
    const std::string password = SafeStringFromJson(request.body, "password");

    // Rate-limit check before touching the DB
    if (!security_.CheckLoginRateLimit(request.clientIp, username))
    {
        statusCode = 429;
        return { { "success", false }, { "error", "Too many login attempts. Please wait and try again." } };
    }

    UserRecord user;
    if (!storage_.FindUserByUsername(username, user) || user.isGuest)
    {
        security_.RecordLoginFailure(request.clientIp, username);
        return { { "success", false }, { "error", "Account does not exist." } };
    }

    // Ban check before verifying credentials to avoid timing side-channel
    std::string idCardBanReason;
    if (IsEffectivelyBanned(user, ServerJson::CurrentTimeMs()) || 
        (!user.idCard.empty() && storage_.IsIdCardBanned(user.idCard, idCardBanReason)))
    {
        if (idCardBanReason.empty())
        {
            idCardBanReason = user.banReason;
        }
        else
        {
            if (!user.isBanned)
            {
                user.isBanned = true;
                user.banReason = idCardBanReason;
                storage_.UpsertUser(user);
            }
        }
        statusCode = 403;
        logger_.Log(LogLevel::Warn,
            "[Security] Banned ID card/user login attempt userId=" + user.id +
            " reason=" + idCardBanReason);
        return BannedResponse(user);
    }

    if (!ServerCrypto::VerifyPassword(password, user.passwordHash))
    {
        security_.RecordLoginFailure(request.clientIp, username);
        return { { "success", false }, { "error", "Password is incorrect." } };
    }

    security_.RecordLoginSuccess(request.clientIp, username);
    ServerJson::EnsureUserDefaults(user);
    storage_.UpsertUser(user);

    return {
        { "success", true },
        { "token", IssueSession(user.id) },
        { "userId", user.id },
        { "username", user.username },
        { "level", user.level },
        { "wins", user.wins },
        { "losses", user.losses },
        { "gold", user.gold },
        { "gems", user.gems },
        { "rankTitle", ServerJson::RankTitleFrom(user) },
        { "isGuest", false }
    };
}

json ServerService::HandleGuest(const ParsedRequest& request, int& statusCode)
{
    statusCode = 200;
    std::string guestName = ServerCrypto::SanitizeDisplayName(SafeStringFromJson(request.body, "displayName"));
    int weight = GetNameWeight(guestName);
    if (guestName.empty() || ContainsBlockedName(guestName) || weight > 14 || weight < 4)
        guestName = std::string("Guest") + std::to_string((std::rand() % 9000) + 1000);

    UserRecord user;
    user.id = ServerCrypto::GenerateUuidLike();
    user.username = guestName;
    user.isGuest = true;
    user.level = 1;
    user.wins = 0;
    user.losses = 0;
    user.gold = 500;
    user.gems = 50;
    user.createdAt = ServerJson::CurrentTimeMs();
    user.lobbyState.day = ServerJson::TodayStringUtc();
    storage_.UpsertUser(user);

    return {
        { "success", true },
        { "token", IssueSession(user.id) },
        { "userId", user.id },
        { "username", user.username },
        { "level", user.level },
        { "isGuest", true },
        { "gold", user.gold },
        { "gems", user.gems },
        { "rankTitle", ServerJson::RankTitleFrom(user) }
    };
}

json ServerService::HandleProfile(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    ServerJson::EnsureUserDefaults(user);
    json response = ServerJson::UserToSafeJson(user);
    response["success"] = true;
    statusCode = 200;
    return response;
}

json ServerService::HandleRealNameBind(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string realName = Trim(SafeStringFromJson(request.body, "realName"));
    const std::string idCard = Trim(SafeStringFromJson(request.body, "idCard"));

    if (realName.empty())
    {
        statusCode = 400;
        return { { "success", false }, { "error", "Real name cannot be empty." } };
    }
    if (idCard.empty() || idCard.length() < 15)
    {
        statusCode = 400;
        return { { "success", false }, { "error", "Invalid identity card format." } };
    }

    // Check if ID card is banned
    std::string banReason;
    if (storage_.IsIdCardBanned(idCard, banReason))
    {
        statusCode = 403;
        return { { "success", false }, { "error", "This identity card is blacklisted due to safety policy." }, { "errorCode", "IDCARD_BANNED" } };
    }

    // Save ID card and realName to the user record
    user.realName = realName;
    user.idCard = idCard;
    storage_.UpsertUser(user);

    json response = ServerJson::UserToSafeJson(user);
    response["success"] = true;
    statusCode = 200;
    return response;
}

json ServerService::HandleLobby(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    ServerJson::EnsureUserDefaults(user);
    ServerJson::EnsureLobbyDay(user);
    if (user.hasActiveTech && user.activeTech.endAt <= ServerJson::CurrentTimeMs())
    {
        user.hasActiveTech = false;
        user.activeTech = ActiveTech();
    }
    storage_.UpsertUser(user);

    json response = {
        { "success", true },
        { "gold", user.gold },
        { "gems", user.gems },
        { "rankTitle", ServerJson::RankTitleFrom(user) },
        { "tasks", ServerJson::LobbyTasksToJson(user) },
        { "techId", user.hasActiveTech ? user.activeTech.id : "" },
        { "techName", user.hasActiveTech ? user.activeTech.name : "" },
        { "techDesc", user.hasActiveTech ? user.activeTech.desc : "" },
        { "techEndAt", user.hasActiveTech ? user.activeTech.endAt : 0 },
        { "techTotalSec", user.hasActiveTech ? user.activeTech.totalSec : 0 },
        { "claimedMailIds", user.lobbyState.claimedMailIds }
    };
    statusCode = 200;
    return response;
}

json ServerService::HandlePresence(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    TrackPresence(user.id);
    statusCode = 200;
    return { { "success", true } };
}

json ServerService::HandleTaskClaim(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    ServerJson::EnsureUserDefaults(user);
    ServerJson::EnsureLobbyDay(user);
    const std::string taskId = SafeStringFromJson(request.body, "taskId");
    if (taskId == "daily_login")
    {
        if (user.lobbyState.dailyLoginClaimed)
            return { { "success", false }, { "error", "Task already claimed." } };
        user.lobbyState.dailyLoginClaimed = true;
        user.gold += 100;
    }
    else if (taskId == "win3")
    {
        if (user.lobbyState.win3Claimed)
            return { { "success", false }, { "error", "Task already claimed." } };
        if (user.lobbyState.winsToday < 3)
            return { { "success", false }, { "error", "Task progress is not enough yet." } };
        user.lobbyState.win3Claimed = true;
        user.gold += 200;
    }
    else if (taskId == "destroy20")
    {
        if (user.lobbyState.destroyClaimed)
            return { { "success", false }, { "error", "Task already claimed." } };
        if (user.lobbyState.killsToday < 20)
            return { { "success", false }, { "error", "Task progress is not enough yet." } };
        user.lobbyState.destroyClaimed = true;
        user.gold += 150;
        user.gems += 5;
    }
    else
    {
        return { { "success", false }, { "error", "Unknown task id." } };
    }

    storage_.UpsertUser(user);
    return {
        { "success", true },
        { "gold", user.gold },
        { "gems", user.gems }
    };
}

json ServerService::HandleMailClaim(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    ServerJson::EnsureUserDefaults(user);
    ServerJson::EnsureLobbyDay(user);
    const std::string mailId = SafeStringFromJson(request.body, "mailId");
    int gold = SafeIntFromJson(request.body, "gold", 0);
    int gems = SafeIntFromJson(request.body, "gems", 0);

    if (mailId.empty())
    {
        return { { "success", false }, { "error", "mailId is required." } };
    }

    for (const auto& id : user.lobbyState.claimedMailIds)
    {
        if (id == mailId)
        {
            return { { "success", false }, { "error", "Mail reward already claimed." } };
        }
    }

    user.gold += std::max(0, gold);
    user.gems += std::max(0, gems);
    user.lobbyState.claimedMailIds.push_back(mailId);

    storage_.UpsertUser(user);
    return {
        { "success", true },
        { "gold", user.gold },
        { "gems", user.gems },
        { "claimedMailIds", user.lobbyState.claimedMailIds }
    };
}

json ServerService::HandleTechStart(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    ServerJson::EnsureUserDefaults(user);
    if (user.hasActiveTech && user.activeTech.endAt > ServerJson::CurrentTimeMs())
        return { { "success", false }, { "error", "A research project is already in progress." } };

    user.hasActiveTech = true;
    std::string techKey = SafeStringFromJson(request.body, "techKey");
    std::string mode    = SafeStringFromJson(request.body, "mode");
    bool isConquest = (mode == "conquest");

    // Conquest: hours.  Match: short seconds (<=3 min).
    if (techKey == "tank_attack_1")
    {
        user.activeTech.id       = "tank_attack_1";
        user.activeTech.name     = "Tank Attack I";
        user.activeTech.desc     = "Increase tank attack damage by 10%.";
        user.activeTech.totalSec = isConquest ? 2 * 60 * 60 : 90;
    }
    else if (techKey == "artillery_reload_1")
    {
        user.activeTech.id       = "artillery_reload_1";
        user.activeTech.name     = "Artillery Speed I";
        user.activeTech.desc     = "Reduce artillery reload time by 10%.";
        user.activeTech.totalSec = isConquest ? 3 * 60 * 60 : 120;
    }
    else if (techKey == "speed_boost_1")
    {
        user.activeTech.id       = "speed_boost_1";
        user.activeTech.name     = "Tactical Speed I";
        user.activeTech.desc     = "Increase all unit movement speed by 10%.";
        user.activeTech.totalSec = isConquest ? 4 * 60 * 60 : 180;
    }
    else  // tank_armor_1 or unknown
    {
        user.activeTech.id       = "tank_armor_1";
        user.activeTech.name     = "Tank Armor I";
        user.activeTech.desc     = "Increase tank health by 10%.";
        user.activeTech.totalSec = isConquest ? 1 * 60 * 60 : 60;
    }
    user.activeTech.endAt = ServerJson::CurrentTimeMs() + static_cast<std::int64_t>(user.activeTech.totalSec) * 1000LL;
    storage_.UpsertUser(user);

    return {
        { "success", true },
        { "tech", {
            { "id", user.activeTech.id },
            { "name", user.activeTech.name },
            { "desc", user.activeTech.desc },
            { "endAt", user.activeTech.endAt },
            { "totalSec", user.activeTech.totalSec }
        } }
    };
}

json ServerService::HandleTechSpeedup(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    if (!user.hasActiveTech || user.activeTech.endAt <= ServerJson::CurrentTimeMs())
        return { { "success", false }, { "error", "No research is currently in progress." } };
    if (user.gems < 10)
        return { { "success", false }, { "error", "Not enough gems." } };

    user.gems -= 10;
    user.activeTech.endAt = std::max<std::int64_t>(ServerJson::CurrentTimeMs(), user.activeTech.endAt - 10LL * 60LL * 1000LL);
    storage_.UpsertUser(user);

    return {
        { "success", true },
        { "gold", user.gold },
        { "gems", user.gems },
        { "endAt", user.activeTech.endAt }
    };
}

json ServerService::HandleFriends(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    std::vector<std::string> friendIds = storage_.GetFriendIds(user.id);
    json friends = json::array();
    for (std::size_t i = 0; i < friendIds.size(); ++i)
    {
        UserRecord friendUser;
        if (!storage_.FindUserById(friendIds[i], friendUser))
            continue;

        ServerJson::EnsureUserDefaults(friendUser);
        const std::string rankTitle = ServerJson::RankTitleFrom(friendUser);
        friends.push_back({
            { "id", friendUser.id },
            { "userId", friendUser.id },
            { "username", friendUser.username },
            { "level", friendUser.level },
            { "rank", rankTitle },
            { "rankTitle", rankTitle },
            { "status", FriendStatusFor(friendUser.id) }
        });
    }

    statusCode = 200;
    return {
        { "success", true },
        { "friends", friends }
    };
}

json ServerService::HandleLeaderboard(const ParsedRequest& request, int& statusCode)
{
    UserRecord currentUser;
    json errorJson;
    if (!TryAuthenticate(request, currentUser, errorJson, statusCode))
        return errorJson;

    struct RankedUser
    {
        UserRecord user;
        int score = 0;
    };

    std::vector<UserRecord> users = storage_.ListUsers();
    std::vector<RankedUser> ranked;
    ranked.reserve(users.size());
    for (std::size_t i = 0; i < users.size(); ++i)
    {
        UserRecord user = users[i];
        if (user.id.empty() || user.username.empty())
            continue;

        ServerJson::EnsureUserDefaults(user);
        const int kills = user.hasStats ? user.stats.kills : 0;
        RankedUser row;
        row.user = user;
        row.score = user.wins * 30 + std::max(0, user.level) * 8 + kills - user.losses * 6;
        ranked.push_back(row);
    }

    std::sort(ranked.begin(), ranked.end(), [](const RankedUser& left, const RankedUser& right)
    {
        if (left.score != right.score)
            return left.score > right.score;
        if (left.user.wins != right.user.wins)
            return left.user.wins > right.user.wins;
        return left.user.username < right.user.username;
    });

    json leaderboard = json::array();
    json current = json();
    for (std::size_t i = 0; i < ranked.size(); ++i)
    {
        const RankedUser& row = ranked[i];
        json entry = {
            { "id", row.user.id },
            { "userId", row.user.id },
            { "username", row.user.username },
            { "level", row.user.level },
            { "wins", row.user.wins },
            { "losses", row.user.losses },
            { "score", row.score },
            { "place", static_cast<int>(i) + 1 },
            { "rankTitle", ServerJson::RankTitleFrom(row.user) },
            { "status", row.user.id == currentUser.id ? "Current Commander" : "Season Active" },
            { "isCurrent", row.user.id == currentUser.id }
        };

        if (row.user.id == currentUser.id)
            current = entry;
        if (i < 20)
            leaderboard.push_back(entry);
    }

    if (!current.is_null())
    {
        bool alreadyIncluded = false;
        for (json::const_iterator it = leaderboard.begin(); it != leaderboard.end(); ++it)
        {
            if (it->value("id", "") == current.value("id", ""))
            {
                alreadyIncluded = true;
                break;
            }
        }

        if (!alreadyIncluded)
        {
            if (!leaderboard.empty())
                leaderboard.erase(leaderboard.end() - 1);
            leaderboard.push_back(current);
        }
    }

    statusCode = 200;
    return {
        { "success", true },
        { "season", "S3 Eastern Front Season" },
        { "resetText", "Weekly reset Monday 05:00" },
        { "leaderboard", leaderboard },
        { "current", current.is_null() ? json(nullptr) : current }
    };
}

json ServerService::HandleFriendAdd(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    const std::string friendName = Trim(SafeStringFromJson(request.body, "friendName"));
    if (friendName.empty())
        return { { "success", false }, { "error", "friendName is required." } };

    UserRecord friendUser;
    if (!storage_.FindUserByUsername(friendName, friendUser))
        return { { "success", false }, { "error", "User not found." } };
    if (friendUser.id == user.id)
        return { { "success", false }, { "error", "You cannot add yourself." } };

    storage_.AddFriendPair(user.id, friendUser.id);
    return { { "success", true } };
}

json ServerService::HandleFriendInvite(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    const std::string friendName = Trim(SafeStringFromJson(request.body, "friendName"));
    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    if (friendName.empty())
        return { { "success", false }, { "error", "friendName is required." } };

    UserRecord target;
    if (!storage_.FindUserByUsername(friendName, target))
        return { { "success", false }, { "error", "Target user was not found." } };

    if (FriendStatusFor(target.id) == "Offline")
        return { { "success", false }, { "error", "User is offline and cannot accept invitations." } };

    RoomRecord room;
    bool roomFound = false;
    if (!roomId.empty())
    {
        roomFound = storage_.FindRoomById(roomId, room);
        if (!roomFound)
            return { { "success", false }, { "error", "Room not found." } };
        if (room.status != "waiting")
            return { { "success", false }, { "error", "This room can no longer be joined." } };
    }

    InviteRecord invite;
    invite.id = ServerCrypto::GenerateUuidLike();
    invite.from = user.username;
    invite.fromId = user.id;
    invite.roomId = roomId;
    invite.mapName = roomFound ? room.mapName : "";
    invite.maxPlayers = roomFound ? room.maxPlayers : 2;
    invite.playerCount = roomFound ? static_cast<int>(room.players.size()) : 0;
    invite.createdAt = ServerJson::CurrentTimeMs();
    storage_.UpsertInvite(target.id, invite);

    return {
        { "success", true },
        { "inviteId", invite.id }
    };
}

json ServerService::HandleInvites(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    std::vector<InviteRecord> invites = storage_.ListInvitesForUser(user.id);
    json list = json::array();
    const std::int64_t now = ServerJson::CurrentTimeMs();
    for (std::size_t i = 0; i < invites.size(); ++i)
    {
        // 过了10分钟邀请链接失效
        if (now - invites[i].createdAt >= 10LL * 60LL * 1000LL)
            continue;

        // 房主退出了，房间自动失效
        if (!invites[i].roomId.empty())
        {
            RoomRecord room;
            if (!storage_.FindRoomById(invites[i].roomId, room) || room.status != "waiting")
                continue;
            if (room.hostId != invites[i].fromId)
                continue;
            if (std::find(room.players.begin(), room.players.end(), invites[i].fromId) == room.players.end())
                continue;
        }

        // 获取发送者等级与军衔
        UserRecord sender;
        int level = 1;
        std::string rank = "Recruit";
        if (storage_.FindUserById(invites[i].fromId, sender))
        {
            level = sender.level;
            rank = ServerJson::RankTitleFrom(sender);
        }

        json inviteJson = ServerJson::InviteToJson(invites[i]);
        inviteJson["level"] = level;
        inviteJson["rank"] = rank;
        list.push_back(inviteJson);
    }

    statusCode = 200;
    return {
        { "success", true },
        { "invites", list }
    };
}

json ServerService::HandleInviteRespond(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    const std::string inviteId = SafeStringFromJson(request.body, "inviteId");
    const bool accept = request.body.value("accept", false);

    InviteRecord invite;
    if (!storage_.RemoveInviteById(user.id, inviteId, invite))
        return { { "success", false }, { "error", "Invite not found." } };

    if (accept && !invite.roomId.empty())
    {
        const std::int64_t now = ServerJson::CurrentTimeMs();
        // 过了10分钟邀请链接失效
        if (now - invite.createdAt > 10LL * 60LL * 1000LL)
            return { { "success", false }, { "error", "Invite has expired." } };

        RoomRecord room;
        // 房主退出了，房间自动失效
        if (!storage_.FindRoomById(invite.roomId, room) || room.status != "waiting" ||
            room.hostId != invite.fromId ||
            std::find(room.players.begin(), room.players.end(), invite.fromId) == room.players.end())
        {
            return { { "success", false }, { "error", "Room is invalid or host has left." } };
        }

        return {
            { "success", true },
            { "roomId", invite.roomId },
            { "mapName", room.mapName },
            { "maxPlayers", room.maxPlayers },
            { "playerCount", static_cast<int>(room.players.size()) },
            { "players", room.players }
        };
    }

    return { { "success", true } };
}

json ServerService::HandleRoomsList(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::vector<RoomRecord> rooms = storage_.ListRecentWaitingRooms();
    json list = json::array();
    for (std::size_t i = 0; i < rooms.size(); ++i)
        list.push_back(ServerJson::RoomToJson(rooms[i]));

    statusCode = 200;
    return {
        { "success", true },
        { "rooms", list }
    };
}

json ServerService::HandleRoomCreate(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    // Sliding window User rate-limiting: max 5 room creations per user per 5 minutes
    if (!security_.CheckGenericRateLimit(user.id, "room_create", 5, 5LL * 60LL * 1000LL))
    {
        statusCode = 429;
        return { { "success", false }, { "error", "Too many room creations. Please wait a few minutes." } };
    }

    statusCode = 200;
    std::string mapName = SafeStringFromJson(request.body, "mapName", "Desert Oasis");
    if (Trim(mapName).empty())
        mapName = "Desert Oasis";
    std::string roomName = SafeStringFromJson(request.body, "roomName");
    if (Trim(roomName).empty())
        roomName = user.username + "'s Room";

    RoomRecord room;
    room.id = ServerCrypto::GenerateUuidLike();
    room.name = Trim(roomName);
    room.mapName = mapName;
    room.hostId = user.id;
    room.status = "waiting";
    room.players.push_back(user.id);
    room.maxPlayers = NormalizeRoomMaxPlayers(request.body);
    room.createdAt = ServerJson::CurrentTimeMs();
    storage_.UpsertRoom(room);

    return {
        { "success", true },
        { "roomId", room.id },
        { "mapName", room.mapName },
        { "maxPlayers", room.maxPlayers },
        { "playerCount", 1 },
        { "players", room.players }
    };
}

json ServerService::HandleRoomJoin(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    RoomRecord room;
    if (!storage_.FindRoomById(roomId, room) || room.status != "waiting")
        return { { "success", false }, { "error", "Room does not exist or has already started." } };
    if (static_cast<int>(room.players.size()) >= room.maxPlayers)
        return { { "success", false }, { "error", "Room is full." } };

    if (std::find(room.players.begin(), room.players.end(), user.id) == room.players.end())
        room.players.push_back(user.id);
    storage_.UpsertRoom(room);

    return {
        { "success", true },
        { "roomId", room.id },
        { "mapName", room.mapName },
        { "maxPlayers", room.maxPlayers },
        { "playerCount", static_cast<int>(room.players.size()) },
        { "players", room.players }
    };
}

json ServerService::HandleRoomLeave(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    RoomRecord room;
    if (storage_.FindRoomById(roomId, room))
    {
        if (room.hostId == user.id)
        {
            // 房主退出了，房间自动失效
            storage_.RemoveRoom(roomId);
        }
        else
        {
            room.players.erase(std::remove(room.players.begin(), room.players.end(), user.id), room.players.end());
            if (room.players.empty())
                storage_.RemoveRoom(roomId);
            else
                storage_.UpsertRoom(room);
        }
    }

    statusCode = 200;
    return { { "success", true } };
}

json ServerService::HandleRoomKick(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    const std::string targetUserId = SafeStringFromJson(request.body, "targetUserId");

    RoomRecord room;
    if (!storage_.FindRoomById(roomId, room))
    {
        statusCode = 404;
        return { { "success", false }, { "error", "Room not found." } };
    }

    if (room.hostId != user.id)
    {
        statusCode = 403;
        return { { "success", false }, { "error", "Only the host can kick players." } };
    }

    room.players.erase(std::remove(room.players.begin(), room.players.end(), targetUserId), room.players.end());
    storage_.UpsertRoom(room);

    statusCode = 200;
    return { { "success", true } };
}

json ServerService::HandleRoomUpdate(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    const std::string mapName = SafeStringFromJson(request.body, "mapName");

    RoomRecord room;
    if (!storage_.FindRoomById(roomId, room))
    {
        statusCode = 404;
        return { { "success", false }, { "error", "Room not found." } };
    }

    if (room.hostId != user.id)
    {
        statusCode = 403;
        return { { "success", false }, { "error", "Only the host can update room settings." } };
    }

    if (!mapName.empty())
    {
        room.mapName = mapName;
    }

    storage_.UpsertRoom(room);

    statusCode = 200;
    return { { "success", true } };
}

json ServerService::HandleMatchJoin(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    statusCode = 200;
    std::string mapName = SafeStringFromJson(request.body, "mapName", "娌欐紶缁挎床");
    if (Trim(mapName).empty())
        mapName = "娌欐紶缁挎床";

    PendingMatch pending;
    if (storage_.FindPendingMatch(user.id, pending))
    {
        storage_.RemovePendingMatch(user.id);
        return {
            { "success", true },
            { "matched", true },
            { "roomId", pending.roomId },
            { "mapName", pending.mapName }
        };
    }

    MatchQueueEntry self;
    self.userId = user.id;
    self.mapName = mapName;
    self.joinedAt = ServerJson::CurrentTimeMs();
    storage_.UpsertMatchQueueEntry(user.id, self);

    MatchQueueEntry other;
    if (storage_.FindMatchingOpponent(user.id, mapName, other))
    {
        RoomRecord room;
        room.id = ServerCrypto::GenerateUuidLike();
        room.name = "鍖归厤鎴块棿";
        room.mapName = mapName;
        room.hostId = user.id;
        room.status = "playing";
        room.players.push_back(user.id);
        room.players.push_back(other.userId);
        room.maxPlayers = 2;
        room.createdAt = ServerJson::CurrentTimeMs();
        storage_.UpsertRoom(room);
        storage_.RemoveMatchQueueEntry(user.id);
        storage_.RemoveMatchQueueEntry(other.userId);
        storage_.UpsertPendingMatch(other.userId, PendingMatch{ room.id, mapName });

        BattleSessionRecord session;
        session.id = ServerCrypto::GenerateUuidLike();
        session.roomId = room.id;
        session.mapName = room.mapName;
        session.createdByUserId = user.id;
        session.status = "active";
        session.players = room.players;
        session.createdAt = room.createdAt;
        session.startedAt = room.createdAt;
        session.expiresAt = room.createdAt + 2LL * 60LL * 60LL * 1000LL;
        storage_.UpsertBattleSession(session);
        logger_.Log(
            LogLevel::Info,
            std::string("match battle session created battleId=") + session.id +
            " roomId=" + room.id +
            " players=" + JoinPlayers(session.players));

        return {
            { "success", true },
            { "matched", true },
            { "roomId", room.id },
            { "mapName", room.mapName }
        };
    }

    int queueSize = storage_.GetMatchQueueSize(mapName);
    return {
        { "success", true },
        { "matched", false },
        { "queueSize", queueSize }
    };
}

json ServerService::HandleMatchCancel(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    storage_.RemoveMatchQueueEntry(user.id);
    statusCode = 200;
    return { { "success", true } };
}

json ServerService::HandlePaymentsCatalog(int& statusCode) const
{
    json products = json::array();
    for (std::size_t i = 0; i < sizeof(kGemProducts) / sizeof(kGemProducts[0]); ++i)
        products.push_back(GemProductToJson(kGemProducts[i]));

    statusCode = 200;
    return {
        { "success", true },
        { "products", products }
    };
}

json ServerService::HandlePaymentsList(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::vector<PaymentOrderRecord> orders = storage_.ListPaymentOrdersForUser(user.id);
    json list = json::array();
    for (std::size_t i = 0; i < orders.size(); ++i)
    {
        list.push_back({
            { "id", orders[i].id },
            { "productId", orders[i].productId },
            { "productName", orders[i].productName },
            { "currency", orders[i].currency },
            { "status", orders[i].status },
            { "provider", orders[i].provider },
            { "providerOrderId", orders[i].providerOrderId },
            { "amountFen", orders[i].amountFen },
            { "gems", orders[i].gems },
            { "bonusGems", orders[i].bonusGems },
            { "creditsApplied", orders[i].creditsApplied },
            { "createdAt", orders[i].createdAt },
            { "paidAt", orders[i].paidAt }
        });
    }

    statusCode = 200;
    return {
        { "success", true },
        { "orders", list }
    };
}

json ServerService::HandlePaymentCreate(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string productId = SafeStringFromJson(request.body, "productId");
    const GemProduct* product = FindGemProductById(productId);
    if (product == nullptr)
    {
        statusCode = 400;
        return { { "success", false }, { "error", "Unknown productId." } };
    }

    PaymentOrderRecord order;
    order.id = ServerCrypto::GenerateUuidLike();
    order.userId = user.id;
    order.productId = product->id;
    order.productName = product->name;
    order.currency = "CNY";
    order.status = "pending";
    order.provider = SafeStringFromJson(request.body, "provider", "manual");
    order.amountFen = product->amountFen;
    order.gems = product->gems;
    order.bonusGems = product->bonusGems;
    order.createdAt = ServerJson::CurrentTimeMs();
    storage_.UpsertPaymentOrder(order);

    logger_.Log(LogLevel::Info, "payment order created orderId=" + order.id + " userId=" + user.id + " productId=" + order.productId);

    statusCode = 201;
    return {
        { "success", true },
        { "order", {
            { "id", order.id },
            { "productId", order.productId },
            { "productName", order.productName },
            { "currency", order.currency },
            { "amountFen", order.amountFen },
            { "gems", order.gems },
            { "bonusGems", order.bonusGems },
            { "status", order.status },
            { "provider", order.provider },
            { "createdAt", order.createdAt }
        } }
    };
}

json ServerService::HandlePaymentConfirm(const ParsedRequest& request, int& statusCode)
{
    if (!HasAdminSecret(request))
    {
        statusCode = 401;
        return {
            { "success", false },
            { "error", "Admin secret is required." }
        };
    }

    const std::string orderId = SafeStringFromJson(request.body, "orderId");
    const std::string providerOrderId = SafeStringFromJson(request.body, "providerOrderId");
    if (orderId.empty())
    {
        statusCode = 400;
        return { { "success", false }, { "error", "orderId is required." } };
    }

    PaymentOrderRecord order;
    UserRecord user;
    bool creditedNow = false;
    std::string error;
    const std::int64_t paidAt = ServerJson::CurrentTimeMs();
    if (!storage_.ConfirmPaymentOrder(orderId, providerOrderId, paidAt, order, user, creditedNow, error))
    {
        statusCode = 400;
        return { { "success", false }, { "error", error } };
    }

    logger_.Log(
        LogLevel::Info,
        std::string("payment order confirmed orderId=") + order.id +
        " userId=" + user.id +
        " productId=" + order.productId +
        " creditedNow=" + (creditedNow ? "true" : "false"));

    statusCode = 200;
    return {
        { "success", true },
        { "creditedNow", creditedNow },
        { "orderId", order.id },
        { "status", order.status },
        { "gemsAwarded", std::max(0, order.gems) + std::max(0, order.bonusGems) },
        { "userId", user.id },
        { "userGems", user.gems }
    };
}

json ServerService::HandleBattleSessionStart(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    const std::string roomId = SafeStringFromJson(request.body, "roomId");
    const std::string mapName = SafeStringFromJson(request.body, "mapName", "unknown");

    RoomRecord room;
    if (!roomId.empty())
    {
        if (!storage_.FindRoomById(roomId, room))
        {
            statusCode = 404;
            return { { "success", false }, { "error", "Room not found." } };
        }

        if (std::find(room.players.begin(), room.players.end(), user.id) == room.players.end())
        {
            statusCode = 403;
            return { { "success", false }, { "error", "User is not part of this room." } };
        }

        BattleSessionRecord existing;
        if (storage_.FindActiveBattleSessionForRoom(roomId, existing))
        {
            statusCode = 200;
            return {
                { "success", true },
                { "battleId", existing.id },
                { "roomId", existing.roomId },
                { "mapName", existing.mapName },
                { "status", existing.status },
                { "players", existing.players },
                { "expiresAt", existing.expiresAt }
            };
        }
    }

    BattleSessionRecord session;
    session.id = ServerCrypto::GenerateUuidLike();
    session.roomId = roomId;
    session.mapName = !roomId.empty() ? room.mapName : mapName;
    session.createdByUserId = user.id;
    session.status = "active";
    session.players = !roomId.empty() ? room.players : std::vector<std::string>(1, user.id);
    session.createdAt = ServerJson::CurrentTimeMs();
    session.startedAt = session.createdAt;
    session.expiresAt = session.createdAt + 2LL * 60LL * 60LL * 1000LL;
    storage_.UpsertBattleSession(session);

    logger_.Log(
        LogLevel::Info,
        std::string("battle session created battleId=") + session.id +
        " roomId=" + session.roomId +
        " players=" + JoinPlayers(session.players));

    statusCode = 201;
    return {
        { "success", true },
        { "battleId", session.id },
        { "roomId", session.roomId },
        { "mapName", session.mapName },
        { "status", session.status },
        { "players", session.players },
        { "expiresAt", session.expiresAt }
    };
}

json ServerService::HandleResult(const ParsedRequest& request, int& statusCode)
{
    UserRecord user;
    json errorJson;
    if (!TryAuthenticate(request, user, errorJson, statusCode))
        return errorJson;

    // Check for anomalous battle results parameters (kills > 500 or duration < 30s or > 7200s)
    int rawKills = SafeIntFromJson(request.body, "kills", 0);
    int rawDuration = SafeIntFromJson(request.body, "duration", 0);
    if (rawDuration < 30 || rawDuration > 7200 || rawKills > 500 || rawKills < 0)
    {
        user.suspicionScore += 15;
        if (user.suspicionScore >= ServerSecurity::kSuspicionBanThreshold)
        {
            user.isBanned = true;
            user.banReason = "Automatic ban: anomalous battle result parameters";
            user.bannedUntil = 0; // permanent
        }
        storage_.UpsertUser(user);
        logger_.Log(LogLevel::Warn,
            "[Security] Anomalous battle result parameters reported by userId=" + user.id +
            " kills=" + std::to_string(rawKills) +
            " duration=" + std::to_string(rawDuration) +
            " suspicionScore=" + std::to_string(user.suspicionScore));
    }

    std::string battleId = SafeStringFromJson(request.body, "battleId");
    if (battleId.empty())
    {
        RoomRecord room;
        BattleSessionRecord activeSession;
        if (storage_.FindActivePlayingRoomForUser(user.id, room) && storage_.FindActiveBattleSessionForRoom(room.id, activeSession))
            battleId = activeSession.id;
    }
    if (battleId.empty())
    {
        statusCode = 400;
        return { { "success", false }, { "error", "battleId is required." } };
    }

    BattleParticipantReport report;
    report.userId = user.id;
    report.win = SafeIntFromJson(request.body, "win", 0) != 0 ? 1 : 0;
    report.kills = ClampInt(SafeIntFromJson(request.body, "kills", 0), 0, 500);
    report.durationSec = ClampInt(SafeIntFromJson(request.body, "duration", 0), 30, 6 * 60 * 60);
    report.submittedAt = ServerJson::CurrentTimeMs();

    BattleSessionRecord session;
    std::string resultCode;
    std::string error;
    if (!storage_.SubmitBattleReport(battleId, report, report.submittedAt, session, resultCode, error))
    {
        statusCode = resultCode == "already_reported" || resultCode == "already_settled" ? 409 : 400;
        return {
            { "success", false },
            { "error", error },
            { "resultCode", resultCode }
        };
    }

    logger_.Log(
        LogLevel::Info,
        std::string("battle report accepted battleId=") + battleId +
        " userId=" + user.id +
        " resultCode=" + resultCode +
        " reports=" + std::to_string(session.reports.size()));

    statusCode = 200;
    return {
        { "success", true },
        { "battleId", session.id },
        { "status", session.status },
        { "resultCode", resultCode },
        { "reportsReceived", static_cast<int>(session.reports.size()) },
        { "expectedReports", static_cast<int>(session.players.size()) },
        { "winnerUserId", session.winnerUserId }
    };
}

std::string ServerService::IssueSession(const std::string& userId)
{
    storage_.PruneExpiredSessions(ServerJson::CurrentTimeMs());

    SessionRecord session;
    session.token = ServerCrypto::GenerateOpaqueToken(config_);
    session.userId = userId;
    session.expiresAt = ServerJson::CurrentTimeMs() + static_cast<std::int64_t>(config_.sessionTtlSec) * 1000LL;
    storage_.UpsertSession(session);
    return session.token;
}

bool ServerService::ResolveSession(const std::string& token, SessionRecord& session)
{
    storage_.PruneExpiredSessions(ServerJson::CurrentTimeMs());
    if (!storage_.FindSession(token, session))
        return false;

    if (session.expiresAt < ServerJson::CurrentTimeMs())
    {
        storage_.RemoveSession(token);
        return false;
    }
    return true;
}

void ServerService::TrackPresence(const std::string& userId)
{
    std::lock_guard<std::mutex> lock(presenceMutex_);
    presenceByUserId_[userId] = ServerJson::CurrentTimeMs();
}

std::string ServerService::FriendStatusFor(const std::string& userId)
{
    MatchQueueEntry queueEntry;
    if (storage_.FindMatchQueueEntry(userId, queueEntry))
        return "Matchmaking";

    RoomRecord room;
    if (storage_.FindActivePlayingRoomForUser(userId, room))
        return "In Game";

    std::lock_guard<std::mutex> lock(presenceMutex_);
    const std::map<std::string, std::int64_t>::const_iterator it = presenceByUserId_.find(userId);
    if (it != presenceByUserId_.end() && ServerJson::CurrentTimeMs() - it->second < 45000LL)
        return "Online";
    return "Offline";
}

int ServerService::NormalizeRoomMaxPlayers(const nlohmann::json& body)
{
    int parsed = 2;
    if (body.contains("maxPlayers"))
    {
        if (body["maxPlayers"].is_number_integer())
            parsed = body["maxPlayers"].get<int>();
        else if (body["maxPlayers"].is_string())
            parsed = std::atoi(body["maxPlayers"].get<std::string>().c_str());
    }

    if (parsed < 2)
        parsed = 2;
    if (parsed > 100)
        parsed = 100;
    return parsed;
}

std::string ServerService::NormalizePath(const std::string& path)
{
    const std::size_t query = path.find('?');
    return query == std::string::npos ? path : path.substr(0, query);
}

// ---------------------------------------------------------------------------
// Security helpers
// ---------------------------------------------------------------------------

bool ServerService::IsEffectivelyBanned(const UserRecord& user, std::int64_t nowMs)
{
    if (!user.isBanned)
        return false;
    // bannedUntil == 0 means permanent ban
    if (user.bannedUntil == 0)
        return true;
    return nowMs < user.bannedUntil;
}

json ServerService::BannedResponse(const UserRecord& user)
{
    return {
        { "success",     false },
        { "error",       "Your account has been suspended." },
        { "errorCode",   "ACCOUNT_BANNED" },
        { "banReason",   user.banReason },
        { "bannedUntil", user.bannedUntil }
    };
}

// ---------------------------------------------------------------------------
// Admin ban endpoint
// POST /api/admin/ban
// Requires header:  X-Admin-Secret: <adminApiSecret>
// Body:
//   { "userId": "...",     // required
//     "reason": "...",     // required
//     "durationHours": 24  // 0 = permanent; -1 = unban
//   }
// ---------------------------------------------------------------------------
json ServerService::HandleAdminBan(const ParsedRequest& request, int& statusCode)
{
    if (!HasAdminSecret(request))
    {
        statusCode = 401;
        return { { "success", false }, { "error", "Admin secret is required." } };
    }

    const std::string targetUserId = SafeStringFromJson(request.body, "userId");
    const std::string reason       = SafeStringFromJson(request.body, "reason");
    const int durationHours        = SafeIntFromJson(request.body, "durationHours", 0);

    if (targetUserId.empty())
    {
        statusCode = 400;
        return { { "success", false }, { "error", "userId is required." } };
    }

    UserRecord user;
    if (!storage_.FindUserById(targetUserId, user))
    {
        statusCode = 404;
        return { { "success", false }, { "error", "User not found." } };
    }

    if (durationHours < 0)
    {
        // Unban
        user.isBanned      = false;
        user.banReason     = "";
        user.bannedUntil   = 0;
        user.suspicionScore = 0;
        storage_.UpsertUser(user);

        logger_.Log(LogLevel::Info,
            "[Security] Admin unbanned userId=" + targetUserId);

        statusCode = 200;
        return {
            { "success",  true },
            { "action",   "unbanned" },
            { "userId",   targetUserId }
        };
    }

    // Ban
    if (reason.empty())
    {
        statusCode = 400;
        return { { "success", false }, { "error", "reason is required when banning." } };
    }

    user.isBanned    = true;
    user.banReason   = reason;
    user.bannedUntil = durationHours == 0
        ? 0  // permanent
        : ServerJson::CurrentTimeMs() + static_cast<std::int64_t>(durationHours) * 3600LL * 1000LL;

    storage_.UpsertUser(user);

    logger_.Log(LogLevel::Warn,
        "[Security] Admin banned userId=" + targetUserId +
        " reason=" + reason +
        " durationHours=" + std::to_string(durationHours));

    statusCode = 200;
    return {
        { "success",     true },
        { "action",      "banned" },
        { "userId",      targetUserId },
        { "banReason",   user.banReason },
        { "bannedUntil", user.bannedUntil }
    };
}
