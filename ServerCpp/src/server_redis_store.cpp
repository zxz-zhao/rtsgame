#include "server_redis_store.hpp"

#include "runtime_utils.hpp"

#include <cstdlib>
#include <cstring>
#include <sstream>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <netdb.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

namespace
{
#ifdef _WIN32
const unsigned long long kInvalidRedisSocket = static_cast<unsigned long long>(INVALID_SOCKET);
#else
const int kInvalidRedisSocket = -1;
#endif

std::string BuildBulk(const std::string& value)
{
    std::ostringstream out;
    out << "$" << value.size() << "\r\n" << value << "\r\n";
    return out.str();
}

std::string BuildCommand(const std::string& first, const std::string& second, const std::string& third)
{
    const bool hasThird = !third.empty();
    std::ostringstream out;
    out << "*" << (hasThird ? 3 : 2) << "\r\n";
    out << BuildBulk(first);
    out << BuildBulk(second);
    if (hasThird)
        out << BuildBulk(third);
    return out.str();
}
}

ServerRedisStore::ServerRedisStore(const AppConfig& config, AsyncLogger& logger)
    : config_(config)
    , logger_(logger)
    , ready_(false)
    , networkInitialized_(false)
    , socketHandle_(kInvalidRedisSocket)
{
}

ServerRedisStore::~ServerRedisStore()
{
    Shutdown();
}

bool ServerRedisStore::Initialize()
{
    if (!config_.redisEnabled)
    {
        logger_.Log(LogLevel::Info, "redis storage disabled by configuration");
        return true;
    }

    std::lock_guard<std::mutex> lock(mutex_);
    if (ready_)
        return true;

    if (!InitializeNetworkLocked() || !ConnectLocked())
    {
        if (!config_.strictStorageBackends)
            logger_.Log(LogLevel::Warn, "redis backend unavailable, falling back to local snapshots");
        return !config_.strictStorageBackends;
    }

    ready_ = true;
    logger_.Log(LogLevel::Info, "redis storage ready at " + config_.redisHost + ":" + std::to_string(config_.redisPort));
    return true;
}

void ServerRedisStore::Shutdown()
{
    std::lock_guard<std::mutex> lock(mutex_);
    ready_ = false;
    CloseSocketLocked();
#ifdef _WIN32
    if (networkInitialized_)
    {
        WSACleanup();
        networkInitialized_ = false;
    }
#endif
}

bool ServerRedisStore::IsEnabled() const
{
    return config_.redisEnabled;
}

bool ServerRedisStore::IsReady() const
{
    return ready_;
}

bool ServerRedisStore::LoadCollection(const std::string& collectionName, ServerJson::Json& value, bool& exists)
{
    std::lock_guard<std::mutex> lock(mutex_);
    value = ServerJson::Json::object();
    exists = false;

    if (!ready_ && !EnsureConnectionLocked())
        return false;

    if (!SendArrayCommandLocked("GET", KeyFor(collectionName)))
        return false;

    std::string raw;
    bool isNil = false;
    if (!ReadBulkStringLocked(raw, isNil))
        return false;

    if (isNil)
        return true;

    exists = true;
    try
    {
        ServerJson::Json parsed = ServerJson::Json::parse(raw);
        value = parsed.is_object() ? parsed : ServerJson::Json::object();
        return true;
    }
    catch (...)
    {
        logger_.Log(LogLevel::Warn, "redis collection " + collectionName + " contained invalid JSON, treating as empty object");
        return true;
    }
}

bool ServerRedisStore::SaveCollection(const std::string& collectionName, const ServerJson::Json& value)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!ready_ && !EnsureConnectionLocked())
        return false;

    if (!SendArrayCommandLocked("SET", KeyFor(collectionName), value.dump()))
        return false;

    std::string response;
    char prefix = '\0';
    if (!ReadSimpleResponseLocked(response, prefix))
        return false;
    return prefix == '+';
}

bool ServerRedisStore::InitializeNetworkLocked()
{
#ifdef _WIN32
    if (networkInitialized_)
        return true;

    WSADATA wsaData;
    const int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0)
    {
        logger_.Log(LogLevel::Error, "redis store WSAStartup failed with code " + std::to_string(result));
        return false;
    }
    networkInitialized_ = true;
#endif
    return true;
}

bool ServerRedisStore::ConnectLocked()
{
    CloseSocketLocked();
    readBuffer_.clear();

    addrinfo hints;
    std::memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    hints.ai_protocol = IPPROTO_TCP;

    addrinfo* results = nullptr;
    const std::string portText = std::to_string(config_.redisPort);
    if (getaddrinfo(config_.redisHost.c_str(), portText.c_str(), &hints, &results) != 0)
    {
        logger_.Log(LogLevel::Error, "redis getaddrinfo failed for " + config_.redisHost + ":" + portText);
        return false;
    }

    bool connected = false;
    for (addrinfo* current = results; current != nullptr; current = current->ai_next)
    {
#ifdef _WIN32
        SOCKET candidate = socket(current->ai_family, current->ai_socktype, current->ai_protocol);
        if (candidate == INVALID_SOCKET)
            continue;
        if (connect(candidate, current->ai_addr, static_cast<int>(current->ai_addrlen)) == 0)
        {
            socketHandle_ = static_cast<unsigned long long>(candidate);
            connected = true;
            break;
        }
        closesocket(candidate);
#else
        int candidate = socket(current->ai_family, current->ai_socktype, current->ai_protocol);
        if (candidate < 0)
            continue;
        if (connect(candidate, current->ai_addr, current->ai_addrlen) == 0)
        {
            socketHandle_ = candidate;
            connected = true;
            break;
        }
        close(candidate);
#endif
    }

    freeaddrinfo(results);

    if (!connected)
    {
        logger_.Log(LogLevel::Error, "redis connect failed to " + config_.redisHost + ":" + portText);
        return false;
    }

    if (!config_.redisPassword.empty())
    {
        if (!SendArrayCommandLocked("AUTH", config_.redisPassword))
            return false;

        std::string response;
        char prefix = '\0';
        if (!ReadSimpleResponseLocked(response, prefix) || prefix == '-')
        {
            logger_.Log(LogLevel::Error, "redis AUTH failed: " + response);
            return false;
        }
    }

    if (!SendArrayCommandLocked("PING", "codex"))
        return false;

    std::string response;
    char prefix = '\0';
    if (!ReadSimpleResponseLocked(response, prefix) || prefix == '-')
    {
        logger_.Log(LogLevel::Error, "redis PING failed: " + response);
        return false;
    }

    return true;
}

void ServerRedisStore::CloseSocketLocked()
{
#ifdef _WIN32
    if (socketHandle_ != kInvalidRedisSocket)
        closesocket(static_cast<SOCKET>(socketHandle_));
#else
    if (socketHandle_ != kInvalidRedisSocket)
        close(socketHandle_);
#endif
    socketHandle_ = kInvalidRedisSocket;
    readBuffer_.clear();
}

bool ServerRedisStore::EnsureConnectionLocked()
{
    if (!InitializeNetworkLocked())
        return false;
    if (socketHandle_ != kInvalidRedisSocket)
        return true;
    return ConnectLocked();
}

bool ServerRedisStore::SendCommandLocked(const std::string& payload)
{
    if (socketHandle_ == kInvalidRedisSocket && !EnsureConnectionLocked())
        return false;

    const char* data = payload.c_str();
    std::size_t remaining = payload.size();
    while (remaining > 0)
    {
#ifdef _WIN32
        const int sent = send(static_cast<SOCKET>(socketHandle_), data, static_cast<int>(remaining), 0);
#else
        const ssize_t sent = send(socketHandle_, data, remaining, 0);
#endif
        if (sent <= 0)
        {
            CloseSocketLocked();
            return false;
        }

        data += sent;
        remaining -= static_cast<std::size_t>(sent);
    }
    return true;
}

bool ServerRedisStore::SendArrayCommandLocked(const std::string& first, const std::string& second, const std::string& third)
{
    return SendCommandLocked(BuildCommand(first, second, third));
}

bool ServerRedisStore::ReadLineLocked(std::string& line)
{
    for (;;)
    {
        const std::size_t newline = readBuffer_.find("\r\n");
        if (newline != std::string::npos)
        {
            line = readBuffer_.substr(0, newline);
            readBuffer_.erase(0, newline + 2);
            return true;
        }

        char buffer[4096];
#ifdef _WIN32
        const int bytesRead = recv(static_cast<SOCKET>(socketHandle_), buffer, static_cast<int>(sizeof(buffer)), 0);
#else
        const ssize_t bytesRead = recv(socketHandle_, buffer, sizeof(buffer), 0);
#endif
        if (bytesRead <= 0)
        {
            CloseSocketLocked();
            return false;
        }

        readBuffer_.append(buffer, static_cast<std::size_t>(bytesRead));
    }
}

bool ServerRedisStore::ReadBulkStringLocked(std::string& value, bool& isNil)
{
    std::string line;
    if (!ReadLineLocked(line))
        return false;
    if (line.empty())
        return false;

    if (line[0] == '-')
    {
        logger_.Log(LogLevel::Error, "redis error response: " + line.substr(1));
        return false;
    }
    if (line[0] != '$')
        return false;

    const long long length = std::strtoll(line.c_str() + 1, nullptr, 10);
    if (length < 0)
    {
        isNil = true;
        value.clear();
        return true;
    }

    isNil = false;
    while (readBuffer_.size() < static_cast<std::size_t>(length) + 2U)
    {
        char buffer[4096];
#ifdef _WIN32
        const int bytesRead = recv(static_cast<SOCKET>(socketHandle_), buffer, static_cast<int>(sizeof(buffer)), 0);
#else
        const ssize_t bytesRead = recv(socketHandle_, buffer, sizeof(buffer), 0);
#endif
        if (bytesRead <= 0)
        {
            CloseSocketLocked();
            return false;
        }

        readBuffer_.append(buffer, static_cast<std::size_t>(bytesRead));
    }

    value = readBuffer_.substr(0, static_cast<std::size_t>(length));
    readBuffer_.erase(0, static_cast<std::size_t>(length) + 2U);
    return true;
}

bool ServerRedisStore::ReadSimpleResponseLocked(std::string& value, char& prefix)
{
    std::string line;
    if (!ReadLineLocked(line) || line.empty())
        return false;

    prefix = line[0];
    value = line.substr(1);
    if (prefix == '-')
        logger_.Log(LogLevel::Error, "redis command failed: " + value);
    return true;
}

std::string ServerRedisStore::KeyFor(const std::string& collectionName) const
{
    return config_.redisKeyPrefix.empty()
        ? collectionName
        : config_.redisKeyPrefix + ":" + collectionName;
}
