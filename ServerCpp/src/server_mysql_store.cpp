#include "server_mysql_store.hpp"

#include "runtime_utils.hpp"

#include <sstream>
#include <vector>

#ifdef _WIN32
#include <Windows.h>
#else
#include <dlfcn.h>
#endif

namespace
{
struct MYSQL;
struct MYSQL_RES;
typedef char** MYSQL_ROW;

std::string QuoteIdentifier(const std::string& value)
{
    std::string escaped;
    escaped.reserve(value.size() + 2);
    escaped.push_back('`');
    for (std::size_t i = 0; i < value.size(); ++i)
    {
        if (value[i] == '`')
            escaped += "``";
        else
            escaped.push_back(value[i]);
    }
    escaped.push_back('`');
    return escaped;
}

std::string QuoteLiteral(const std::string& value)
{
    std::string escaped;
    escaped.reserve(value.size() + 2);
    escaped.push_back('\'');
    for (std::size_t i = 0; i < value.size(); ++i)
    {
        if (value[i] == '\'' || value[i] == '\\')
            escaped.push_back('\\');
        escaped.push_back(value[i]);
    }
    escaped.push_back('\'');
    return escaped;
}
}

struct ServerMySqlStore::Api
{
    MYSQL* (*mysql_init)(MYSQL*);
    MYSQL* (*mysql_real_connect)(MYSQL*, const char*, const char*, const char*, const char*, unsigned int, const char*, unsigned long);
    void (*mysql_close)(MYSQL*);
    unsigned int (*mysql_errno)(MYSQL*);
    const char* (*mysql_error)(MYSQL*);
    int (*mysql_query)(MYSQL*, const char*);
    MYSQL_RES* (*mysql_store_result)(MYSQL*);
    MYSQL_ROW (*mysql_fetch_row)(MYSQL_RES*);
    void (*mysql_free_result)(MYSQL_RES*);
    unsigned long (*mysql_real_escape_string)(MYSQL*, char*, const char*, unsigned long);
    int (*mysql_set_character_set)(MYSQL*, const char*);
};

ServerMySqlStore::ServerMySqlStore(const AppConfig& config, AsyncLogger& logger)
    : config_(config)
    , logger_(logger)
    , libraryHandle_(nullptr)
    , connection_(nullptr)
    , ready_(false)
    , api_(nullptr)
{
}

ServerMySqlStore::~ServerMySqlStore()
{
    Shutdown();
}

bool ServerMySqlStore::Initialize()
{
    if (!config_.mysqlEnabled)
    {
        logger_.Log(LogLevel::Info, "mysql storage disabled by configuration");
        return true;
    }

    std::lock_guard<std::mutex> lock(mutex_);
    if (ready_)
        return true;

    if (!LoadLibraryLocked())
    {
        if (!config_.strictStorageBackends)
            logger_.Log(LogLevel::Warn, "mysql client library unavailable, falling back to local snapshots");
        return !config_.strictStorageBackends;
    }

    if (!InitializeSchemaLocked())
    {
        CloseConnectionLocked();
        UnloadLibraryLocked();
        if (!config_.strictStorageBackends)
            logger_.Log(LogLevel::Warn, "mysql backend initialization failed, falling back to local snapshots");
        return !config_.strictStorageBackends;
    }

    ready_ = true;
    logger_.Log(
        LogLevel::Info,
        "mysql storage ready at " + config_.mysqlHost + ":" + std::to_string(config_.mysqlPort) +
            "/" + config_.mysqlDatabase + "." + config_.mysqlTable);
    return true;
}

void ServerMySqlStore::Shutdown()
{
    std::lock_guard<std::mutex> lock(mutex_);
    ready_ = false;
    CloseConnectionLocked();
    UnloadLibraryLocked();
}

bool ServerMySqlStore::IsEnabled() const
{
    return config_.mysqlEnabled;
}

bool ServerMySqlStore::IsReady() const
{
    return ready_;
}

bool ServerMySqlStore::LoadCollection(const std::string& collectionName, ServerJson::Json& value, bool& exists)
{
    std::lock_guard<std::mutex> lock(mutex_);
    exists = false;
    value = ServerJson::Json::object();

    if (!ready_ && !EnsureConnectionLocked())
        return false;

    std::string raw;
    const std::string sql =
        "SELECT data FROM " + QuoteIdentifier(config_.mysqlTable) +
        " WHERE name = " + QuoteLiteral(collectionName) +
        " LIMIT 1";
    if (!QuerySingleStringLocked(sql, raw, exists))
        return false;
    if (!exists)
        return true;

    try
    {
        ServerJson::Json parsed = ServerJson::Json::parse(raw);
        value = parsed.is_object() ? parsed : ServerJson::Json::object();
        return true;
    }
    catch (...)
    {
        logger_.Log(LogLevel::Warn, "mysql collection " + collectionName + " contained invalid JSON, treating as empty object");
        value = ServerJson::Json::object();
        return true;
    }
}

bool ServerMySqlStore::SaveCollection(const std::string& collectionName, const ServerJson::Json& value)
{
    std::lock_guard<std::mutex> lock(mutex_);
    if (!ready_ && !EnsureConnectionLocked())
        return false;

    const std::string escapedName = EscapeSqlLocked(collectionName);
    const std::string escapedJson = EscapeSqlLocked(value.dump());
    const std::string table = QuoteIdentifier(config_.mysqlTable);
    const std::string sql =
        "INSERT INTO " + table + " (name, data) VALUES ('" + escapedName + "', CAST('" + escapedJson + "' AS JSON)) "
        "ON DUPLICATE KEY UPDATE data = VALUES(data)";
    return ExecuteNonQueryLocked(sql);
}

bool ServerMySqlStore::LoadLibraryLocked()
{
    if (api_ != nullptr && libraryHandle_ != nullptr)
        return true;

    std::vector<std::string> candidates;
    if (!config_.mysqlClientLibrary.empty())
        candidates.push_back(config_.mysqlClientLibrary);

    const std::string executableDir = RuntimeUtils::GetExecutableDirectory();
#ifdef _WIN32
    candidates.push_back(RuntimeUtils::JoinPath(executableDir, "libmysql.dll"));
    candidates.push_back(RuntimeUtils::JoinPath(executableDir, "libmariadb.dll"));
    candidates.push_back("libmysql.dll");
    candidates.push_back("libmariadb.dll");
#else
    candidates.push_back(RuntimeUtils::JoinPath(executableDir, "libmysqlclient.so"));
    candidates.push_back(RuntimeUtils::JoinPath(executableDir, "libmariadb.so"));
    candidates.push_back("libmysqlclient.so");
    candidates.push_back("libmysqlclient.so.21");
    candidates.push_back("libmariadb.so");
    candidates.push_back("libmariadb.so.3");
#endif

    for (std::size_t i = 0; i < candidates.size(); ++i)
    {
        if (candidates[i].empty())
            continue;

#ifdef _WIN32
        HMODULE handle = LoadLibraryA(candidates[i].c_str());
        if (handle == nullptr)
            continue;
#define UNITY_RTS_LOAD_SYMBOL(name) reinterpret_cast<decltype(api->name)>(GetProcAddress(handle, #name))
        void* rawHandle = reinterpret_cast<void*>(handle);
#else
        void* rawHandle = dlopen(candidates[i].c_str(), RTLD_NOW);
        if (rawHandle == nullptr)
            continue;
#define UNITY_RTS_LOAD_SYMBOL(name) reinterpret_cast<decltype(api->name)>(dlsym(rawHandle, #name))
#endif

        Api* api = new Api();
        api->mysql_init = UNITY_RTS_LOAD_SYMBOL(mysql_init);
        api->mysql_real_connect = UNITY_RTS_LOAD_SYMBOL(mysql_real_connect);
        api->mysql_close = UNITY_RTS_LOAD_SYMBOL(mysql_close);
        api->mysql_errno = UNITY_RTS_LOAD_SYMBOL(mysql_errno);
        api->mysql_error = UNITY_RTS_LOAD_SYMBOL(mysql_error);
        api->mysql_query = UNITY_RTS_LOAD_SYMBOL(mysql_query);
        api->mysql_store_result = UNITY_RTS_LOAD_SYMBOL(mysql_store_result);
        api->mysql_fetch_row = UNITY_RTS_LOAD_SYMBOL(mysql_fetch_row);
        api->mysql_free_result = UNITY_RTS_LOAD_SYMBOL(mysql_free_result);
        api->mysql_real_escape_string = UNITY_RTS_LOAD_SYMBOL(mysql_real_escape_string);
        api->mysql_set_character_set = UNITY_RTS_LOAD_SYMBOL(mysql_set_character_set);
#undef UNITY_RTS_LOAD_SYMBOL

        if (api->mysql_init == nullptr ||
            api->mysql_real_connect == nullptr ||
            api->mysql_close == nullptr ||
            api->mysql_errno == nullptr ||
            api->mysql_error == nullptr ||
            api->mysql_query == nullptr ||
            api->mysql_store_result == nullptr ||
            api->mysql_fetch_row == nullptr ||
            api->mysql_free_result == nullptr ||
            api->mysql_real_escape_string == nullptr)
        {
#ifdef _WIN32
            FreeLibrary(reinterpret_cast<HMODULE>(rawHandle));
#else
            dlclose(rawHandle);
#endif
            delete api;
            continue;
        }

        api_ = api;
        libraryHandle_ = rawHandle;
        logger_.Log(LogLevel::Info, "mysql client library loaded from " + candidates[i]);
        return true;
    }

    logger_.Log(LogLevel::Error, "unable to load mysql client library");
    return false;
}

bool ServerMySqlStore::EnsureConnectionLocked()
{
    if (connection_ != nullptr)
        return true;
    if (api_ == nullptr && !LoadLibraryLocked())
        return false;
    return InitializeSchemaLocked();
}

bool ServerMySqlStore::ConnectLocked(bool withDatabase)
{
    CloseConnectionLocked();
    MYSQL* handle = api_->mysql_init(nullptr);
    if (handle == nullptr)
    {
        logger_.Log(LogLevel::Error, "mysql_init returned null");
        return false;
    }

    const char* database = withDatabase ? config_.mysqlDatabase.c_str() : nullptr;
    MYSQL* connected = api_->mysql_real_connect(
        handle,
        config_.mysqlHost.c_str(),
        config_.mysqlUser.c_str(),
        config_.mysqlPassword.empty() ? nullptr : config_.mysqlPassword.c_str(),
        database,
        static_cast<unsigned int>(config_.mysqlPort),
        nullptr,
        0UL);
    if (connected == nullptr)
    {
        const std::string error = api_->mysql_error(handle) != nullptr ? api_->mysql_error(handle) : "unknown mysql connection error";
        logger_.Log(LogLevel::Error, "mysql_real_connect failed: " + error);
        api_->mysql_close(handle);
        return false;
    }

    if (api_->mysql_set_character_set != nullptr)
        api_->mysql_set_character_set(connected, "utf8mb4");

    connection_ = connected;
    return true;
}

bool ServerMySqlStore::ExecuteNonQueryLocked(const std::string& sql)
{
    if (connection_ == nullptr && !EnsureConnectionLocked())
        return false;

    if (api_->mysql_query(static_cast<MYSQL*>(connection_), sql.c_str()) == 0)
        return true;

    const unsigned int firstError = api_->mysql_errno(static_cast<MYSQL*>(connection_));
    const std::string firstMessage = api_->mysql_error(static_cast<MYSQL*>(connection_)) != nullptr
        ? api_->mysql_error(static_cast<MYSQL*>(connection_))
        : "unknown mysql query error";
    logger_.Log(LogLevel::Warn, "mysql query failed, retrying once: " + firstMessage);

    if (!ConnectLocked(true))
        return false;

    if (api_->mysql_query(static_cast<MYSQL*>(connection_), sql.c_str()) == 0)
        return true;

    const std::string error = api_->mysql_error(static_cast<MYSQL*>(connection_)) != nullptr
        ? api_->mysql_error(static_cast<MYSQL*>(connection_))
        : "unknown mysql query error";
    logger_.Log(LogLevel::Error, "mysql query failed after retry code=" + std::to_string(firstError) + " error=" + error);
    return false;
}

bool ServerMySqlStore::QuerySingleStringLocked(const std::string& sql, std::string& value, bool& exists)
{
    exists = false;
    value.clear();
    if (connection_ == nullptr && !EnsureConnectionLocked())
        return false;

    if (api_->mysql_query(static_cast<MYSQL*>(connection_), sql.c_str()) != 0)
    {
        const std::string error = api_->mysql_error(static_cast<MYSQL*>(connection_)) != nullptr
            ? api_->mysql_error(static_cast<MYSQL*>(connection_))
            : "unknown mysql query error";
        logger_.Log(LogLevel::Error, "mysql select failed: " + error);
        return false;
    }

    MYSQL_RES* result = api_->mysql_store_result(static_cast<MYSQL*>(connection_));
    if (result == nullptr)
    {
        exists = false;
        return true;
    }

    MYSQL_ROW row = api_->mysql_fetch_row(result);
    if (row != nullptr && row[0] != nullptr)
    {
        exists = true;
        value = row[0];
    }

    api_->mysql_free_result(result);
    return true;
}

std::string ServerMySqlStore::EscapeSqlLocked(const std::string& value)
{
    if (value.empty())
        return std::string();

    std::vector<char> buffer(value.size() * 2 + 1, '\0');
    const unsigned long escapedLength = api_->mysql_real_escape_string(
        static_cast<MYSQL*>(connection_),
        &buffer[0],
        value.c_str(),
        static_cast<unsigned long>(value.size()));
    return std::string(&buffer[0], &buffer[0] + escapedLength);
}

void ServerMySqlStore::CloseConnectionLocked()
{
    if (connection_ != nullptr && api_ != nullptr && api_->mysql_close != nullptr)
        api_->mysql_close(static_cast<MYSQL*>(connection_));
    connection_ = nullptr;
}

void ServerMySqlStore::UnloadLibraryLocked()
{
    if (libraryHandle_ != nullptr)
    {
#ifdef _WIN32
        FreeLibrary(reinterpret_cast<HMODULE>(libraryHandle_));
#else
        dlclose(libraryHandle_);
#endif
    }
    libraryHandle_ = nullptr;
    delete api_;
    api_ = nullptr;
}

bool ServerMySqlStore::InitializeSchemaLocked()
{
    if (api_ == nullptr && !LoadLibraryLocked())
        return false;

    if (!ConnectLocked(false))
        return false;

    const std::string createDatabase =
        "CREATE DATABASE IF NOT EXISTS " + QuoteIdentifier(config_.mysqlDatabase) +
        " CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
    if (!ExecuteNonQueryLocked(createDatabase))
        return false;

    if (!ConnectLocked(true))
        return false;

    const std::string createTable =
        "CREATE TABLE IF NOT EXISTS " + QuoteIdentifier(config_.mysqlTable) + " ("
        "name VARCHAR(64) NOT NULL PRIMARY KEY,"
        "data JSON NOT NULL,"
        "updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP"
        ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
    return ExecuteNonQueryLocked(createTable);
}
