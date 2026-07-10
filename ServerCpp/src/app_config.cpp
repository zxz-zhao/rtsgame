#include "app_config.hpp"

#include "runtime_utils.hpp"

#include <algorithm>
#include <cctype>
#include <cerrno>
#include <cstdlib>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <unordered_map>

#ifndef UNITY_RTS_SERVER_VERSION
#define UNITY_RTS_SERVER_VERSION "0.0.0"
#endif

#ifndef UNITY_RTS_SERVER_PLATFORM
#define UNITY_RTS_SERVER_PLATFORM "unknown"
#endif

namespace
{
std::string Trim(const std::string& value)
{
    const std::string whitespace = " \t\r\n";
    const std::size_t start = value.find_first_not_of(whitespace);
    if (start == std::string::npos)
        return {};

    const std::size_t end = value.find_last_not_of(whitespace);
    return value.substr(start, end - start + 1);
}

std::string Unquote(const std::string& value)
{
    if (value.size() >= 2)
    {
        const char first = value.front();
        const char last = value.back();
        if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            return value.substr(1, value.size() - 2);
    }
    return value;
}

std::unordered_map<std::string, std::string> LoadEnvMap(const std::string& filePath)
{
    std::unordered_map<std::string, std::string> values;
    std::ifstream input(filePath.c_str());
    if (!input.is_open())
        return values;

    std::string line;
    while (std::getline(input, line))
    {
        const std::string trimmed = Trim(line);
        if (trimmed.empty() || trimmed[0] == '#')
            continue;

        const std::size_t equalsPos = trimmed.find('=');
        if (equalsPos == std::string::npos || equalsPos == 0)
            continue;

        const std::string key = Trim(trimmed.substr(0, equalsPos));
        const std::string value = Unquote(Trim(trimmed.substr(equalsPos + 1)));
        values[key] = value;
    }

    return values;
}

bool TryParseUnsignedLongLong(const std::string& text, unsigned long long& value)
{
    if (text.empty())
        return false;

    errno = 0;
    char* end = nullptr;
    const unsigned long long parsed = std::strtoull(text.c_str(), &end, 10);
    if (errno != 0 || end == text.c_str() || (end != nullptr && *end != '\0'))
        return false;

    value = parsed;
    return true;
}

std::string ResolveValue(const std::unordered_map<std::string, std::string>& fileValues, const char* key, const std::string& fallback)
{
    const char* envValue = std::getenv(key);
    if (envValue != nullptr && *envValue != '\0')
        return envValue;

    const std::unordered_map<std::string, std::string>::const_iterator it = fileValues.find(key);
    if (it != fileValues.end() && !it->second.empty())
        return it->second;

    return fallback;
}

std::uint16_t ResolvePort(const std::unordered_map<std::string, std::string>& fileValues, const char* key, std::uint16_t fallback)
{
    const std::string resolved = ResolveValue(fileValues, key, "");
    if (resolved.empty())
        return fallback;

    unsigned long long parsed = 0;
    if (!TryParseUnsignedLongLong(resolved, parsed) || parsed == 0ULL || parsed > 65535ULL)
        throw std::runtime_error(std::string("Invalid port for ") + key + ": " + resolved);

    return static_cast<std::uint16_t>(parsed);
}

bool ResolveBool(const std::unordered_map<std::string, std::string>& fileValues, const char* key, bool fallback)
{
    const std::string resolved = ResolveValue(fileValues, key, "");
    if (resolved.empty())
        return fallback;

    std::string normalized = resolved;
    std::transform(normalized.begin(), normalized.end(), normalized.begin(), [](unsigned char ch)
    {
        return static_cast<char>(std::tolower(ch));
    });
    if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
        return true;
    if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
        return false;
    return fallback;
}

std::size_t ResolveSize(const std::unordered_map<std::string, std::string>& fileValues, const char* key, std::size_t fallback)
{
    const std::string resolved = ResolveValue(fileValues, key, "");
    if (resolved.empty())
        return fallback;

    unsigned long long parsed = 0;
    if (!TryParseUnsignedLongLong(resolved, parsed) || parsed == 0ULL)
        return fallback;
    return static_cast<std::size_t>(parsed);
}

std::string JsonEscape(const std::string& value)
{
    std::ostringstream out;
    for (std::string::const_iterator it = value.begin(); it != value.end(); ++it)
    {
        switch (*it)
        {
        case '\\': out << "\\\\"; break;
        case '"': out << "\\\""; break;
        case '\r': out << "\\r"; break;
        case '\n': out << "\\n"; break;
        case '\t': out << "\\t"; break;
        default: out << *it; break;
        }
    }
    return out.str();
}
}

AppConfig AppConfig::LoadFromArgs(int argc, char** argv)
{
    AppConfig config;

    for (int i = 1; i < argc; ++i)
    {
        const std::string arg = argv[i];
        if (arg.find("--env=") == 0)
        {
            config.envFilePath = arg.substr(6);
        }
        else if (arg == "--env" && i + 1 < argc)
        {
            config.envFilePath = argv[++i];
        }
    }

    const std::unordered_map<std::string, std::string> fileValues = LoadEnvMap(config.envFilePath);

    config.bindHost = ResolveValue(fileValues, "BIND_HOST", config.bindHost);
    config.httpPort = ResolvePort(fileValues, "PORT", config.httpPort);
    config.wsPort = ResolvePort(fileValues, "WS_PORT", config.wsPort);
    config.jwtSecret = ResolveValue(fileValues, "JWT_SECRET", config.jwtSecret);
    config.buildRevision = ResolveValue(fileValues, "BUILD_REVISION", config.buildRevision);
    config.authTokenBytes = ResolveSize(fileValues, "AUTH_TOKEN_BYTES", config.authTokenBytes);
    config.sessionTtlSec = static_cast<unsigned int>(ResolveSize(fileValues, "SESSION_TTL_SEC", config.sessionTtlSec));
    config.bcryptCost = static_cast<unsigned int>(ResolveSize(fileValues, "BCRYPT_COST", config.bcryptCost));
    if (config.bcryptCost < 4U) config.bcryptCost = 10U;
    if (config.bcryptCost > 31U) config.bcryptCost = 31U;

    config.logDir = ResolveValue(fileValues, "LOG_DIR", config.logDir);
    config.dataDir = ResolveValue(fileValues, "DATA_DIR", config.dataDir);
    config.backupDir = ResolveValue(fileValues, "BACKUP_DIR", config.backupDir);
    config.serverLogFile = ResolveValue(fileValues, "SERVER_LOG_FILE", RuntimeUtils::JoinPath(config.logDir, "server.log"));
    config.guardianLogFile = ResolveValue(fileValues, "GUARDIAN_LOG_FILE", RuntimeUtils::JoinPath(config.logDir, "guardian.log"));
    config.alertLogFile = ResolveValue(fileValues, "ALERT_LOG_FILE", RuntimeUtils::JoinPath(config.logDir, "alerts.log"));
    config.alertCommand = ResolveValue(fileValues, "ALERT_COMMAND", config.alertCommand);
    config.logEchoStderr = ResolveBool(fileValues, "LOG_ECHO_STDERR", config.logEchoStderr);
    config.logMaxQueueSize = ResolveSize(fileValues, "LOG_MAX_QUEUE_SIZE", config.logMaxQueueSize);
    config.logRotateBytes = ResolveSize(fileValues, "LOG_ROTATE_BYTES", config.logRotateBytes);
    config.guardianRestartDelayMs = static_cast<unsigned int>(ResolveSize(fileValues, "GUARDIAN_RESTART_DELAY_MS", config.guardianRestartDelayMs));
    config.guardianCrashWindowSec = static_cast<unsigned int>(ResolveSize(fileValues, "GUARDIAN_CRASH_WINDOW_SEC", config.guardianCrashWindowSec));
    config.guardianMaxRestartsInWindow = static_cast<unsigned int>(ResolveSize(fileValues, "GUARDIAN_MAX_RESTARTS_IN_WINDOW", config.guardianMaxRestartsInWindow));
    config.guardianCooldownMs = static_cast<unsigned int>(ResolveSize(fileValues, "GUARDIAN_COOLDOWN_MS", config.guardianCooldownMs));
    config.guardianRestartOnZeroExit = ResolveBool(fileValues, "GUARDIAN_RESTART_ON_ZERO_EXIT", config.guardianRestartOnZeroExit);

    config.mysqlEnabled = ResolveBool(fileValues, "MYSQL_ENABLED", config.mysqlEnabled);
    config.mysqlHost = ResolveValue(fileValues, "MYSQL_HOST", config.mysqlHost);
    config.mysqlPort = ResolvePort(fileValues, "MYSQL_PORT", config.mysqlPort);
    config.mysqlUser = ResolveValue(fileValues, "MYSQL_USER", config.mysqlUser);
    config.mysqlPassword = ResolveValue(fileValues, "MYSQL_PASSWORD", config.mysqlPassword);
    config.mysqlDatabase = ResolveValue(fileValues, "MYSQL_DATABASE", config.mysqlDatabase);
    config.mysqlTable = ResolveValue(fileValues, "MYSQL_TABLE", config.mysqlTable);
    config.mysqlClientLibrary = ResolveValue(fileValues, "MYSQL_CLIENT_LIBRARY", config.mysqlClientLibrary);

    config.redisEnabled = ResolveBool(fileValues, "REDIS_ENABLED", config.redisEnabled);
    config.redisHost = ResolveValue(fileValues, "REDIS_HOST", config.redisHost);
    config.redisPort = ResolvePort(fileValues, "REDIS_PORT", config.redisPort);
    config.redisPassword = ResolveValue(fileValues, "REDIS_PASSWORD", config.redisPassword);
    config.redisKeyPrefix = ResolveValue(fileValues, "REDIS_KEY_PREFIX", config.redisKeyPrefix);
    config.strictStorageBackends = ResolveBool(fileValues, "STRICT_STORAGE_BACKENDS", config.strictStorageBackends);
    config.adminApiSecret = ResolveValue(fileValues, "ADMIN_API_SECRET", config.adminApiSecret);
    config.clientSigKey = ResolveValue(fileValues, "CLIENT_SIG_KEY", config.clientSigKey);
    const std::string buildsStr = ResolveValue(fileValues, "ALLOWED_BUILDS", "");
    if (!buildsStr.empty())
    {
        std::istringstream iss(buildsStr);
        std::string token;
        while (std::getline(iss, token, ','))
        {
            if (!token.empty())
            {
                config.allowedBuilds.push_back(std::atoi(token.c_str()));
            }
        }
    }

    for (int i = 1; i < argc; ++i)
    {
        const std::string arg = argv[i];
        if (arg.find("--host=") == 0)
            config.bindHost = arg.substr(7);
        else if (arg.find("--port=") == 0)
            config.httpPort = static_cast<std::uint16_t>(std::strtoul(arg.substr(7).c_str(), nullptr, 10));
        else if (arg.find("--ws-port=") == 0)
            config.wsPort = static_cast<std::uint16_t>(std::strtoul(arg.substr(10).c_str(), nullptr, 10));
        else if (arg.find("--build-revision=") == 0)
            config.buildRevision = arg.substr(17);
    }

    return config;
}

std::string AppConfig::ToHealthJson() const
{
    const bool mysqlConfigured = mysqlEnabled && !mysqlHost.empty() && !mysqlUser.empty() && !mysqlDatabase.empty();
    const bool redisConfigured = redisEnabled && !redisHost.empty();
    const bool adminApiConfigured = !adminApiSecret.empty();

    std::ostringstream out;
    out
        << "{"
        << "\"ok\":true,"
        << "\"version\":\"" << JsonEscape(UNITY_RTS_SERVER_VERSION) << "\","
        << "\"revision\":\"" << JsonEscape(buildRevision) << "\","
        << "\"platform\":\"" << JsonEscape(UNITY_RTS_SERVER_PLATFORM) << "\","
        << "\"httpPort\":" << httpPort << ","
        << "\"wsPort\":" << wsPort << ","
        << "\"native\":true,"
        << "\"scope\":\"scaffold\","
        << "\"storage\":{"
        << "\"strict\":" << (strictStorageBackends ? "true" : "false") << ","
        << "\"mysqlConfigured\":" << (mysqlConfigured ? "true" : "false") << ","
        << "\"redisConfigured\":" << (redisConfigured ? "true" : "false")
        << "},"
        << "\"security\":{"
        << "\"adminApiConfigured\":" << (adminApiConfigured ? "true" : "false")
        << "},"
        << "\"logging\":{"
        << "\"serverLogFile\":\"" << JsonEscape(serverLogFile) << "\","
        << "\"guardianLogFile\":\"" << JsonEscape(guardianLogFile) << "\","
        << "\"alertLogFile\":\"" << JsonEscape(alertLogFile) << "\""
        << "}"
        << "}";
    return out.str();
}
