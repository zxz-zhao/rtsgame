#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"
#include "server_json_utils.hpp"

#include <mutex>
#include <string>

class ServerRedisStore
{
public:
    ServerRedisStore(const AppConfig& config, AsyncLogger& logger);
    ~ServerRedisStore();

    bool Initialize();
    void Shutdown();

    bool IsEnabled() const;
    bool IsReady() const;

    bool LoadCollection(const std::string& collectionName, ServerJson::Json& value, bool& exists);
    bool SaveCollection(const std::string& collectionName, const ServerJson::Json& value);

private:
    AppConfig config_;
    AsyncLogger& logger_;
    std::mutex mutex_;
    bool ready_;
    bool networkInitialized_;
    std::string readBuffer_;

#ifdef _WIN32
    unsigned long long socketHandle_;
#else
    int socketHandle_;
#endif

    bool InitializeNetworkLocked();
    bool ConnectLocked();
    void CloseSocketLocked();
    bool EnsureConnectionLocked();
    bool SendCommandLocked(const std::string& payload);
    bool SendArrayCommandLocked(const std::string& first, const std::string& second, const std::string& third = std::string());
    bool ReadLineLocked(std::string& line);
    bool ReadBulkStringLocked(std::string& value, bool& isNil);
    bool ReadSimpleResponseLocked(std::string& value, char& prefix);
    std::string KeyFor(const std::string& collectionName) const;
};
