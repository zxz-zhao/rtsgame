#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"
#include "server_json_utils.hpp"

#include <mutex>
#include <string>

class ServerMySqlStore
{
public:
    ServerMySqlStore(const AppConfig& config, AsyncLogger& logger);
    ~ServerMySqlStore();

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
    void* libraryHandle_;
    void* connection_;
    bool ready_;

    struct Api;
    Api* api_;

    bool LoadLibraryLocked();
    bool EnsureConnectionLocked();
    bool ConnectLocked(bool withDatabase);
    bool ExecuteNonQueryLocked(const std::string& sql);
    bool QuerySingleStringLocked(const std::string& sql, std::string& value, bool& exists);
    std::string EscapeSqlLocked(const std::string& value);
    void CloseConnectionLocked();
    void UnloadLibraryLocked();
    bool InitializeSchemaLocked();
};
