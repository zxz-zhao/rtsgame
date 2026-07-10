#include "battle_relay_server.hpp"

#include <array>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <sstream>
#include <vector>

#ifdef _WIN32
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

namespace
{
#ifdef _WIN32
const BattleRelaySocketHandle kInvalidBattleRelaySocket = INVALID_SOCKET;
#else
const BattleRelaySocketHandle kInvalidBattleRelaySocket = -1;
#endif

void CloseSocket(BattleRelaySocketHandle socketHandle)
{
#ifdef _WIN32
    if (socketHandle != kInvalidBattleRelaySocket)
        closesocket(socketHandle);
#else
    if (socketHandle != kInvalidBattleRelaySocket)
        close(socketHandle);
#endif
}

bool SendAll(BattleRelaySocketHandle socketHandle, const std::string& payload)
{
    const char* data = payload.c_str();
    std::size_t remaining = payload.size();
    while (remaining > 0)
    {
#ifdef _WIN32
        const int sent = send(socketHandle, data, static_cast<int>(remaining), 0);
#else
        const ssize_t sent = send(socketHandle, data, remaining, 0);
#endif
        if (sent <= 0)
            return false;

        data += sent;
        remaining -= static_cast<std::size_t>(sent);
    }
    return true;
}

bool ReceiveExact(BattleRelaySocketHandle socketHandle, char* buffer, std::size_t bytesToRead)
{
    std::size_t totalRead = 0;
    while (totalRead < bytesToRead)
    {
#ifdef _WIN32
        const int bytesRead = recv(socketHandle, buffer + totalRead, static_cast<int>(bytesToRead - totalRead), 0);
#else
        const ssize_t bytesRead = recv(socketHandle, buffer + totalRead, bytesToRead - totalRead, 0);
#endif
        if (bytesRead <= 0)
            return false;
        totalRead += static_cast<std::size_t>(bytesRead);
    }
    return true;
}

std::string ToLowerAscii(std::string value)
{
    for (std::size_t i = 0; i < value.size(); ++i)
    {
        const unsigned char ch = static_cast<unsigned char>(value[i]);
        if (ch >= 'A' && ch <= 'Z')
            value[i] = static_cast<char>(ch - 'A' + 'a');
    }
    return value;
}

std::string TrimAscii(const std::string& value)
{
    std::size_t start = 0;
    while (start < value.size() && (value[start] == ' ' || value[start] == '\t' || value[start] == '\r' || value[start] == '\n'))
        ++start;

    std::size_t end = value.size();
    while (end > start && (value[end - 1] == ' ' || value[end - 1] == '\t' || value[end - 1] == '\r' || value[end - 1] == '\n'))
        --end;

    return value.substr(start, end - start);
}

std::string Base64Encode(const std::vector<unsigned char>& bytes)
{
    static const char kAlphabet[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string out;
    for (std::size_t i = 0; i < bytes.size(); i += 3)
    {
        const unsigned int b0 = bytes[i];
        const unsigned int b1 = i + 1 < bytes.size() ? bytes[i + 1] : 0U;
        const unsigned int b2 = i + 2 < bytes.size() ? bytes[i + 2] : 0U;
        const unsigned int chunk = (b0 << 16) | (b1 << 8) | b2;

        out.push_back(kAlphabet[(chunk >> 18) & 0x3F]);
        out.push_back(kAlphabet[(chunk >> 12) & 0x3F]);
        out.push_back(i + 1 < bytes.size() ? kAlphabet[(chunk >> 6) & 0x3F] : '=');
        out.push_back(i + 2 < bytes.size() ? kAlphabet[chunk & 0x3F] : '=');
    }
    return out;
}

std::vector<unsigned char> Sha1Digest(const std::string& input)
{
    std::vector<unsigned char> data(input.begin(), input.end());
    const std::uint64_t bitLength = static_cast<std::uint64_t>(data.size()) * 8ULL;
    data.push_back(0x80);
    while ((data.size() % 64U) != 56U)
        data.push_back(0x00);

    for (int shift = 56; shift >= 0; shift -= 8)
        data.push_back(static_cast<unsigned char>((bitLength >> shift) & 0xFF));

    std::uint32_t h0 = 0x67452301U;
    std::uint32_t h1 = 0xEFCDAB89U;
    std::uint32_t h2 = 0x98BADCFEU;
    std::uint32_t h3 = 0x10325476U;
    std::uint32_t h4 = 0xC3D2E1F0U;

    auto rol = [](std::uint32_t value, unsigned int bits) -> std::uint32_t
    {
        return (value << bits) | (value >> (32U - bits));
    };

    for (std::size_t offset = 0; offset < data.size(); offset += 64U)
    {
        std::array<std::uint32_t, 80> w{};
        for (std::size_t i = 0; i < 16U; ++i)
        {
            const std::size_t base = offset + i * 4U;
            w[i] =
                (static_cast<std::uint32_t>(data[base]) << 24) |
                (static_cast<std::uint32_t>(data[base + 1]) << 16) |
                (static_cast<std::uint32_t>(data[base + 2]) << 8) |
                static_cast<std::uint32_t>(data[base + 3]);
        }

        for (std::size_t i = 16U; i < 80U; ++i)
            w[i] = rol(w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16], 1U);

        std::uint32_t a = h0;
        std::uint32_t b = h1;
        std::uint32_t c = h2;
        std::uint32_t d = h3;
        std::uint32_t e = h4;

        for (std::size_t i = 0; i < 80U; ++i)
        {
            std::uint32_t f = 0;
            std::uint32_t k = 0;
            if (i < 20U)
            {
                f = (b & c) | ((~b) & d);
                k = 0x5A827999U;
            }
            else if (i < 40U)
            {
                f = b ^ c ^ d;
                k = 0x6ED9EBA1U;
            }
            else if (i < 60U)
            {
                f = (b & c) | (b & d) | (c & d);
                k = 0x8F1BBCDCU;
            }
            else
            {
                f = b ^ c ^ d;
                k = 0xCA62C1D6U;
            }

            const std::uint32_t temp = rol(a, 5U) + f + e + k + w[i];
            e = d;
            d = c;
            c = rol(b, 30U);
            b = a;
            a = temp;
        }

        h0 += a;
        h1 += b;
        h2 += c;
        h3 += d;
        h4 += e;
    }

    std::vector<unsigned char> digest(20U);
    const std::uint32_t words[] = { h0, h1, h2, h3, h4 };
    for (std::size_t i = 0; i < 5U; ++i)
    {
        digest[i * 4U + 0U] = static_cast<unsigned char>((words[i] >> 24) & 0xFF);
        digest[i * 4U + 1U] = static_cast<unsigned char>((words[i] >> 16) & 0xFF);
        digest[i * 4U + 2U] = static_cast<unsigned char>((words[i] >> 8) & 0xFF);
        digest[i * 4U + 3U] = static_cast<unsigned char>(words[i] & 0xFF);
    }
    return digest;
}

std::string MakeWebSocketAccept(const std::string& clientKey)
{
    const std::string joined = clientKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    return Base64Encode(Sha1Digest(joined));
}

bool ParseHeaders(const std::string& requestText, std::map<std::string, std::string>& headers)
{
    std::istringstream stream(requestText);
    std::string line;
    if (!std::getline(stream, line))
        return false;

    while (std::getline(stream, line))
    {
        if (!line.empty() && line[line.size() - 1] == '\r')
            line.erase(line.size() - 1);
        if (line.empty())
            break;

        const std::size_t colon = line.find(':');
        if (colon == std::string::npos)
            continue;
        headers[ToLowerAscii(TrimAscii(line.substr(0, colon)))] = TrimAscii(line.substr(colon + 1));
    }
    return true;
}
}

BattleRelayServer::BattleRelayServer(const AppConfig& config, AsyncLogger& logger, ServerService& service)
    : config_(config)
    , logger_(logger)
    , service_(service)
    , stopRequested_(false)
    , listenSocket_(kInvalidBattleRelaySocket)
    , networkInitialized_(false)
{
}

BattleRelayServer::~BattleRelayServer()
{
    Stop();
}

int BattleRelayServer::Start()
{
    if (InitializeNetwork() != 0)
        return 1;
    if (OpenListenSocket() != 0)
        return 1;

    acceptThread_ = std::thread(&BattleRelayServer::AcceptLoop, this);
    logger_.Log(LogLevel::Info, "battle relay listening on " + config_.bindHost + ":" + std::to_string(config_.wsPort));
    return 0;
}

void BattleRelayServer::Stop()
{
    const bool alreadyStopping = stopRequested_.exchange(true);
    if (alreadyStopping)
        return;

    if (listenSocket_ != kInvalidBattleRelaySocket)
    {
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidBattleRelaySocket;
    }

    std::vector<BattleRelaySocketHandle> sockets;
    {
        std::lock_guard<std::mutex> lock(roomsMutex_);
        for (std::map<std::string, RelayRoomState>::const_iterator it = rooms_.begin(); it != rooms_.end(); ++it)
        {
            if (it->second.hostSocket != kInvalidBattleRelaySocket)
                sockets.push_back(it->second.hostSocket);
            if (it->second.guestSocket != kInvalidBattleRelaySocket)
                sockets.push_back(it->second.guestSocket);
        }
    }

    for (std::size_t i = 0; i < sockets.size(); ++i)
        CloseSocket(sockets[i]);

    if (acceptThread_.joinable())
        acceptThread_.join();

#ifdef _WIN32
    if (networkInitialized_)
    {
        WSACleanup();
        networkInitialized_ = false;
    }
#endif
}

int BattleRelayServer::InitializeNetwork()
{
#ifdef _WIN32
    WSADATA wsaData;
    const int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0)
    {
        logger_.Log(LogLevel::Error, "battle relay WSAStartup failed with code " + std::to_string(result));
        return 1;
    }
    networkInitialized_ = true;
#endif
    return 0;
}

int BattleRelayServer::OpenListenSocket()
{
    listenSocket_ = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSocket_ == kInvalidBattleRelaySocket)
    {
        logger_.Log(LogLevel::Error, "battle relay failed to create listen socket");
        return 1;
    }

    int reuseAddr = 1;
    setsockopt(listenSocket_, SOL_SOCKET, SO_REUSEADDR, reinterpret_cast<const char*>(&reuseAddr), sizeof(reuseAddr));

    sockaddr_in address;
    std::memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    address.sin_port = htons(config_.wsPort);
    if (config_.bindHost == "0.0.0.0")
    {
        address.sin_addr.s_addr = htonl(INADDR_ANY);
    }
    else
    {
        const unsigned long parsedAddress = inet_addr(config_.bindHost.c_str());
        if (parsedAddress == INADDR_NONE)
        {
            logger_.Log(LogLevel::Error, "battle relay invalid bind host " + config_.bindHost);
            CloseSocket(listenSocket_);
            listenSocket_ = kInvalidBattleRelaySocket;
            return 1;
        }
        address.sin_addr.s_addr = parsedAddress;
    }

    if (bind(listenSocket_, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0)
    {
        logger_.Log(LogLevel::Error, "battle relay bind failed on " + config_.bindHost + ":" + std::to_string(config_.wsPort));
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidBattleRelaySocket;
        return 1;
    }

    if (listen(listenSocket_, 64) != 0)
    {
        logger_.Log(LogLevel::Error, "battle relay failed to enter listen state");
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidBattleRelaySocket;
        return 1;
    }

    return 0;
}

void BattleRelayServer::AcceptLoop()
{
    while (!stopRequested_.load())
    {
        sockaddr_in clientAddress;
#ifdef _WIN32
        int clientLength = sizeof(clientAddress);
#else
        socklen_t clientLength = sizeof(clientAddress);
#endif
        BattleRelaySocketHandle clientSocket = accept(listenSocket_, reinterpret_cast<sockaddr*>(&clientAddress), &clientLength);
        if (clientSocket == kInvalidBattleRelaySocket)
        {
            if (stopRequested_.load())
                break;
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            continue;
        }

        std::thread(&BattleRelayServer::HandleClient, this, clientSocket).detach();
    }
}

void BattleRelayServer::HandleClient(BattleRelaySocketHandle socketHandle)
{
    ConnectionContext context;
    context.socketHandle = socketHandle;

    if (!PerformHandshake(socketHandle))
    {
        CloseSocket(socketHandle);
        return;
    }

    while (!stopRequested_.load())
    {
        int opcode = 0;
        std::string payload;
        if (!ReceiveFrame(socketHandle, opcode, payload))
            break;

        if (opcode == 0x8)
            break;
        if (opcode != 0x1)
            continue;

        nlohmann::json message;
        try
        {
            message = nlohmann::json::parse(payload);
        }
        catch (...)
        {
            continue;
        }

        const std::string type = message.value("type", "");
        if (type == "join")
            HandleJoinMessage(context, message);
        else if (type == "cmd")
            HandleCommandMessage(context, message);
        else if (type == "ping")
            SendJsonMessage(socketHandle, { { "type", "pong" } });
    }

    CleanupConnection(context);
    CloseSocket(socketHandle);
}

bool BattleRelayServer::PerformHandshake(BattleRelaySocketHandle socketHandle)
{
    std::string requestText;
    if (!ReceiveHttpHeaders(socketHandle, requestText))
        return false;

    std::map<std::string, std::string> headers;
    if (!ParseHeaders(requestText, headers))
        return false;

    const std::string upgrade = ToLowerAscii(headers["upgrade"]);
    const std::string connection = ToLowerAscii(headers["connection"]);
    const std::string clientKey = headers["sec-websocket-key"];
    if (upgrade != "websocket" || connection.find("upgrade") == std::string::npos || clientKey.empty())
        return false;

    std::ostringstream response;
    response
        << "HTTP/1.1 101 Switching Protocols\r\n"
        << "Upgrade: websocket\r\n"
        << "Connection: Upgrade\r\n"
        << "Sec-WebSocket-Accept: " << MakeWebSocketAccept(clientKey) << "\r\n"
        << "\r\n";
    return SendAll(socketHandle, response.str());
}

bool BattleRelayServer::ReceiveHttpHeaders(BattleRelaySocketHandle socketHandle, std::string& requestText)
{
    requestText.clear();
    char buffer[4096];
    while (requestText.find("\r\n\r\n") == std::string::npos && requestText.size() < 32768U)
    {
#ifdef _WIN32
        const int bytesRead = recv(socketHandle, buffer, static_cast<int>(sizeof(buffer)), 0);
#else
        const ssize_t bytesRead = recv(socketHandle, buffer, sizeof(buffer), 0);
#endif
        if (bytesRead <= 0)
            return false;
        requestText.append(buffer, static_cast<std::size_t>(bytesRead));
    }
    return requestText.find("\r\n\r\n") != std::string::npos;
}

bool BattleRelayServer::SendTextMessage(BattleRelaySocketHandle socketHandle, const std::string& payload)
{
    return SendFrame(socketHandle, 0x1, payload);
}

bool BattleRelayServer::SendJsonMessage(BattleRelaySocketHandle socketHandle, const nlohmann::json& payload)
{
    return SendTextMessage(socketHandle, payload.dump());
}

bool BattleRelayServer::ReceiveFrame(BattleRelaySocketHandle socketHandle, int& opcode, std::string& payload)
{
    payload.clear();
    std::array<unsigned char, 2> header{};
    if (!ReceiveExact(socketHandle, reinterpret_cast<char*>(header.data()), header.size()))
        return false;

    opcode = header[0] & 0x0F;
    const bool masked = (header[1] & 0x80U) != 0;
    std::uint64_t payloadLength = header[1] & 0x7FU;
    if (payloadLength == 126U)
    {
        std::array<unsigned char, 2> extended{};
        if (!ReceiveExact(socketHandle, reinterpret_cast<char*>(extended.data()), extended.size()))
            return false;
        payloadLength = (static_cast<std::uint64_t>(extended[0]) << 8) | static_cast<std::uint64_t>(extended[1]);
    }
    else if (payloadLength == 127U)
    {
        std::array<unsigned char, 8> extended{};
        if (!ReceiveExact(socketHandle, reinterpret_cast<char*>(extended.data()), extended.size()))
            return false;
        payloadLength = 0;
        for (std::size_t i = 0; i < extended.size(); ++i)
            payloadLength = (payloadLength << 8) | extended[i];
    }

    std::array<unsigned char, 4> mask{};
    if (masked && !ReceiveExact(socketHandle, reinterpret_cast<char*>(mask.data()), mask.size()))
        return false;

    if (payloadLength > 1024ULL * 1024ULL)
        return false;

    payload.resize(static_cast<std::size_t>(payloadLength));
    if (payloadLength > 0 && !ReceiveExact(socketHandle, &payload[0], static_cast<std::size_t>(payloadLength)))
        return false;

    if (masked)
    {
        for (std::size_t i = 0; i < payload.size(); ++i)
            payload[i] = static_cast<char>(static_cast<unsigned char>(payload[i]) ^ mask[i % 4U]);
    }

    if (opcode == 0x9)
    {
        SendFrame(socketHandle, 0xA, payload);
        payload.clear();
    }
    return true;
}

bool BattleRelayServer::SendFrame(BattleRelaySocketHandle socketHandle, int opcode, const std::string& payload)
{
    std::string frame;
    frame.push_back(static_cast<char>(0x80 | (opcode & 0x0F)));
    if (payload.size() < 126U)
    {
        frame.push_back(static_cast<char>(payload.size()));
    }
    else if (payload.size() <= 0xFFFFU)
    {
        frame.push_back(126);
        frame.push_back(static_cast<char>((payload.size() >> 8) & 0xFF));
        frame.push_back(static_cast<char>(payload.size() & 0xFF));
    }
    else
    {
        frame.push_back(127);
        const std::uint64_t size = static_cast<std::uint64_t>(payload.size());
        for (int shift = 56; shift >= 0; shift -= 8)
            frame.push_back(static_cast<char>((size >> shift) & 0xFF));
    }
    frame += payload;
    return SendAll(socketHandle, frame);
}

void BattleRelayServer::HandleJoinMessage(ConnectionContext& context, const nlohmann::json& message)
{
    if (context.joined)
        return;

    ServerService::BattleRelayJoinContext joinContext;
    std::string error;
    if (!service_.AuthorizeBattleRelayJoin(message.value("token", ""), message.value("roomId", ""), joinContext, error))
    {
        SendJsonMessage(context.socketHandle, { { "type", "error" }, { "msg", error } });
        CloseSocket(context.socketHandle);
        return;
    }

    context.joined = true;
    context.roomId = joinContext.room.id;
    context.userId = joinContext.user.id;
    context.username = joinContext.user.username;
    context.role = joinContext.role;

    BattleRelaySocketHandle replacedSocket = kInvalidBattleRelaySocket;
    BattleRelaySocketHandle peerSocket = kInvalidBattleRelaySocket;
    std::string peerUserId;
    int seed = 0;
    {
        std::lock_guard<std::mutex> lock(roomsMutex_);
        RelayRoomState& roomState = rooms_[context.roomId];
        if (roomState.roomId.empty())
        {
            roomState.roomId = context.roomId;
            roomState.hostUserId = joinContext.hostUserId;
            roomState.guestUserId = joinContext.guestUserId;
            roomState.hostSocket = kInvalidBattleRelaySocket;
            roomState.guestSocket = kInvalidBattleRelaySocket;
            roomState.seed = static_cast<int>(ServerJson::CurrentTimeMs() % 900000LL) + 10000;
        }

        seed = roomState.seed;
        if (context.role == "host")
        {
            replacedSocket = roomState.hostSocket;
            roomState.hostSocket = context.socketHandle;
            peerSocket = roomState.guestSocket;
            peerUserId = roomState.guestUserId;
        }
        else
        {
            replacedSocket = roomState.guestSocket;
            roomState.guestSocket = context.socketHandle;
            peerSocket = roomState.hostSocket;
            peerUserId = roomState.hostUserId;
        }
    }

    if (replacedSocket != kInvalidBattleRelaySocket && replacedSocket != context.socketHandle)
        CloseSocket(replacedSocket);

    SendJsonMessage(context.socketHandle, {
        { "type", "role" },
        { "role", context.role },
        { "seed", seed }
    });

    if (peerSocket != kInvalidBattleRelaySocket)
    {
        SendJsonMessage(peerSocket, {
            { "type", "peer_joined" },
            { "peerId", context.userId }
        });
        SendJsonMessage(context.socketHandle, {
            { "type", "peer_joined" },
            { "peerId", peerUserId }
        });
    }

    logger_.Log(LogLevel::Info, "battle relay join roomId=" + context.roomId + " userId=" + context.userId + " role=" + context.role);
}

void BattleRelayServer::HandleCommandMessage(const ConnectionContext& context, const nlohmann::json& message)
{
    if (!context.joined || !message.contains("data") || !message["data"].is_object())
        return;

    nlohmann::json normalizedPayload;
    std::string error;
    if (!service_.ValidateBattleRelayCommand(context.roomId, context.userId, context.username, message["data"], normalizedPayload, error))
    {
        SendJsonMessage(context.socketHandle, {
            { "type", "error" },
            { "msg", error }
        });
        return;
    }

    BattleRelaySocketHandle peerSocket = kInvalidBattleRelaySocket;
    {
        std::lock_guard<std::mutex> lock(roomsMutex_);
        const std::map<std::string, RelayRoomState>::const_iterator it = rooms_.find(context.roomId);
        if (it != rooms_.end())
            peerSocket = context.role == "host" ? it->second.guestSocket : it->second.hostSocket;
    }

    if (peerSocket == kInvalidBattleRelaySocket)
        return;

    SendJsonMessage(peerSocket, {
        { "type", "cmd" },
        { "data", normalizedPayload },
        { "from", context.role }
    });
}

void BattleRelayServer::CleanupConnection(const ConnectionContext& context)
{
    if (!context.joined || context.roomId.empty())
        return;

    BattleRelaySocketHandle peerSocket = kInvalidBattleRelaySocket;
    bool eraseRoom = false;
    {
        std::lock_guard<std::mutex> lock(roomsMutex_);
        std::map<std::string, RelayRoomState>::iterator it = rooms_.find(context.roomId);
        if (it == rooms_.end())
            return;

        RelayRoomState& roomState = it->second;
        if (context.role == "host" && roomState.hostSocket == context.socketHandle)
        {
            roomState.hostSocket = kInvalidBattleRelaySocket;
            peerSocket = roomState.guestSocket;
        }
        else if (context.role == "guest" && roomState.guestSocket == context.socketHandle)
        {
            roomState.guestSocket = kInvalidBattleRelaySocket;
            peerSocket = roomState.hostSocket;
        }

        eraseRoom = roomState.hostSocket == kInvalidBattleRelaySocket && roomState.guestSocket == kInvalidBattleRelaySocket;
        if (eraseRoom)
            rooms_.erase(it);
    }

    if (peerSocket != kInvalidBattleRelaySocket)
        SendJsonMessage(peerSocket, { { "type", "peer_left" } });

    logger_.Log(LogLevel::Info, "battle relay leave roomId=" + context.roomId + " userId=" + context.userId + " role=" + context.role);
}
