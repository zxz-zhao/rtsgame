#include "alert_reporter.hpp"
#include "app_config.hpp"
#include "async_logger.hpp"
#include "battle_relay_server.hpp"
#include "native_http_server.hpp"
#include "runtime_utils.hpp"
#include "server_service.hpp"

#include <csignal>
#include <exception>
#include <iostream>
#include <string>

namespace
{
NativeHttpServer* g_server = nullptr;
BattleRelayServer* g_relay = nullptr;
AsyncLogger* g_logger = nullptr;

void PrintUsage()
{
    std::cout
        << "UnityRTS native C++ server scaffold\n"
        << "Usage: unity_rts_server_cpp [--env path] [--host ip] [--port n] [--ws-port n] [--build-revision rev]\n";
}

void OnSignal(int)
{
    if (g_logger != nullptr)
        g_logger->Log(LogLevel::Warn, "shutdown signal received");
    if (g_relay != nullptr)
        g_relay->Stop();
    if (g_server != nullptr)
        g_server->Stop();
}
}

int main(int argc, char** argv)
{
    for (int i = 1; i < argc; ++i)
    {
        const std::string arg = argv[i];
        if (arg == "--help" || arg == "-h")
        {
            PrintUsage();
            return 0;
        }
    }

    try
    {
        const AppConfig config = AppConfig::LoadFromArgs(argc, argv);
        RuntimeUtils::EnsureDirectoryRecursive(config.logDir);
        RuntimeUtils::EnsureDirectoryRecursive(config.dataDir);
        RuntimeUtils::EnsureDirectoryRecursive(config.backupDir);

        AsyncLogger logger;
        if (!logger.Start(config.serverLogFile, config.logEchoStderr, config.logMaxQueueSize, config.logRotateBytes))
        {
            std::cerr << "[NativeServer] Failed to start logger at " << config.serverLogFile << std::endl;
            return 1;
        }

        AlertReporter alerts(config, logger);
        logger.Log(LogLevel::Info, "server process starting");
        logger.Log(LogLevel::Info, "configuration loaded from " + config.envFilePath);

        ServerService service(config, logger);
        if (!service.Initialize())
        {
            logger.Log(LogLevel::Error, "failed to initialize server storage");
            alerts.Report("server initialization failed", "storage initialization returned false");
            logger.Stop();
            return 1;
        }

        NativeHttpServer server(config, logger, service);
        BattleRelayServer relay(config, logger, service);
        g_server = &server;
        g_relay = &relay;
        g_logger = &logger;

#ifndef _WIN32
        std::signal(SIGPIPE, SIG_IGN);
#endif
        std::signal(SIGINT, OnSignal);
        std::signal(SIGTERM, OnSignal);

        if (relay.Start() != 0)
        {
            logger.Log(LogLevel::Error, "failed to start battle relay server");
            alerts.Report("server startup failed", "battle relay server failed to start");
            logger.Stop();
            return 1;
        }

        const int exitCode = server.Run();
        relay.Stop();
        logger.Log(exitCode == 0 ? LogLevel::Info : LogLevel::Error, "server process exiting with code " + std::to_string(exitCode));
        if (exitCode != 0)
            alerts.Report("server exited with error", "exit code " + std::to_string(exitCode));
        logger.Stop();
        return exitCode;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "[NativeServer] Fatal error: " << ex.what() << std::endl;
        return 1;
    }
}
