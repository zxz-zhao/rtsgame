#pragma once

#include <cstdint>
#include <string>
#include <vector>

struct AppConfig
{
    std::string bindHost = "0.0.0.0";
    std::uint16_t httpPort = 8080;
    std::uint16_t wsPort = 8081;
    std::string jwtSecret = "dev-only-change-me";
    std::string buildRevision = "local";
    std::string envFilePath = ".env";
    std::size_t authTokenBytes = 32;
    unsigned int sessionTtlSec = 7 * 24 * 60 * 60;
    unsigned int bcryptCost = 10;
    std::string logDir = "logs";
    std::string dataDir = "data";
    std::string backupDir = "backups";
    std::string serverLogFile = "logs/server.log";
    std::string guardianLogFile = "logs/guardian.log";
    std::string alertLogFile = "logs/alerts.log";
    std::string alertCommand;
    bool logEchoStderr = true;
    std::size_t logMaxQueueSize = 4096;
    std::size_t logRotateBytes = 8 * 1024 * 1024;
    unsigned int guardianRestartDelayMs = 2000;
    unsigned int guardianCrashWindowSec = 120;
    unsigned int guardianMaxRestartsInWindow = 8;
    unsigned int guardianCooldownMs = 30000;
    bool guardianRestartOnZeroExit = false;

    bool mysqlEnabled = true;
    std::string mysqlHost = "127.0.0.1";
    std::uint16_t mysqlPort = 3306;
    std::string mysqlUser = "unityrts";
    std::string mysqlPassword;
    std::string mysqlDatabase = "unity_rts";
    std::string mysqlTable = "rts_kv_store";
    std::string mysqlClientLibrary;

    bool redisEnabled = true;
    std::string redisHost = "127.0.0.1";
    std::uint16_t redisPort = 6379;
    std::string redisPassword;
    std::string redisKeyPrefix = "unity_rts";
    bool strictStorageBackends = true;
    std::string adminApiSecret;
    std::string clientSigKey = "dev-signature-key-change-me";
    std::vector<int> allowedBuilds;

    static AppConfig LoadFromArgs(int argc, char** argv);
    std::string ToHealthJson() const;
};
