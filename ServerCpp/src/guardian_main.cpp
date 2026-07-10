#include "alert_reporter.hpp"
#include "app_config.hpp"
#include "async_logger.hpp"
#include "runtime_utils.hpp"

#include <cerrno>
#include <csignal>
#include <ctime>
#include <deque>
#include <iostream>
#include <string>
#include <vector>

#ifdef _WIN32
#include <windows.h>
#else
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace
{
volatile std::sig_atomic_t g_stopRequested = 0;

void OnSignal(int)
{
    g_stopRequested = 1;
}

std::string GetExecutableSuffix()
{
#ifdef _WIN32
    return ".exe";
#else
    return "";
#endif
}

std::string ResolveServerPath(int argc, char** argv)
{
    for (int i = 1; i < argc; ++i)
    {
        const std::string arg = argv[i];
        if (arg.find("--server=") == 0)
            return arg.substr(9);
        if (arg == "--server" && i + 1 < argc)
            return argv[i + 1];
    }
    return RuntimeUtils::JoinPath("bin", "unity_rts_server_cpp" + GetExecutableSuffix());
}

struct ProcessExit
{
    int exitCode;
    bool exitedCleanly;
};

#ifdef _WIN32
ProcessExit RunChildProcess(const std::string& serverPath, const std::string& envFilePath)
{
    STARTUPINFOA startupInfo;
    PROCESS_INFORMATION processInfo;
    ZeroMemory(&startupInfo, sizeof(startupInfo));
    ZeroMemory(&processInfo, sizeof(processInfo));
    startupInfo.cb = sizeof(startupInfo);

    std::string commandLine = "\"" + serverPath + "\" --env \"" + envFilePath + "\"";
    if (!CreateProcessA(
            nullptr,
            &commandLine[0],
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            nullptr,
            &startupInfo,
            &processInfo))
    {
        return ProcessExit{ static_cast<int>(GetLastError()), false };
    }

    while (!g_stopRequested)
    {
        const DWORD waitResult = WaitForSingleObject(processInfo.hProcess, 500);
        if (waitResult == WAIT_OBJECT_0)
            break;
    }

    if (g_stopRequested)
    {
        TerminateProcess(processInfo.hProcess, 0);
    }

    DWORD exitCode = 0;
    GetExitCodeProcess(processInfo.hProcess, &exitCode);
    CloseHandle(processInfo.hThread);
    CloseHandle(processInfo.hProcess);
    return ProcessExit{ static_cast<int>(exitCode), true };
}
#else
ProcessExit RunChildProcess(const std::string& serverPath, const std::string& envFilePath)
{
    const pid_t pid = fork();
    if (pid == 0)
    {
        execl(serverPath.c_str(), serverPath.c_str(), "--env", envFilePath.c_str(), static_cast<char*>(nullptr));
        _exit(127);
    }

    if (pid < 0)
        return ProcessExit{ errno, false };

    int status = 0;
    while (!g_stopRequested)
    {
        const pid_t waitResult = waitpid(pid, &status, WNOHANG);
        if (waitResult == pid)
            break;
        RuntimeUtils::SleepMs(500);
    }

    if (g_stopRequested)
    {
        kill(pid, SIGTERM);
        waitpid(pid, &status, 0);
    }

    if (WIFEXITED(status))
        return ProcessExit{ WEXITSTATUS(status), true };
    if (WIFSIGNALED(status))
        return ProcessExit{ 128 + WTERMSIG(status), true };
    return ProcessExit{ 1, false };
}
#endif
}

int main(int argc, char** argv)
{
    try
    {
        const AppConfig config = AppConfig::LoadFromArgs(argc, argv);
        RuntimeUtils::EnsureDirectoryRecursive(config.logDir);
        RuntimeUtils::EnsureDirectoryRecursive(config.backupDir);

        AsyncLogger logger;
        if (!logger.Start(config.guardianLogFile, config.logEchoStderr, config.logMaxQueueSize, config.logRotateBytes))
        {
            std::cerr << "[Guardian] Failed to start logger: " << config.guardianLogFile << std::endl;
            return 1;
        }

        AlertReporter alerts(config, logger);
        const std::string serverPath = ResolveServerPath(argc, argv);

        std::signal(SIGINT, OnSignal);
        std::signal(SIGTERM, OnSignal);

        logger.Log(LogLevel::Info, "guardian starting for server path " + serverPath);

        std::deque<std::time_t> restartHistory;
        while (!g_stopRequested)
        {
            const std::time_t now = std::time(nullptr);
            restartHistory.push_back(now);

            while (!restartHistory.empty() && static_cast<unsigned int>(now - restartHistory.front()) > config.guardianCrashWindowSec)
                restartHistory.pop_front();

            if (restartHistory.size() > config.guardianMaxRestartsInWindow)
            {
                alerts.Report(
                    "guardian crash loop",
                    "restart count exceeded threshold; entering cooldown for " + std::to_string(config.guardianCooldownMs) + "ms"
                );
                RuntimeUtils::SleepMs(config.guardianCooldownMs);
                restartHistory.clear();
            }

            logger.Log(LogLevel::Info, "starting child server process");
            const ProcessExit result = RunChildProcess(serverPath, config.envFilePath);

            if (!result.exitedCleanly)
            {
                alerts.Report("guardian failed to launch child", "process creation failed with code " + std::to_string(result.exitCode));
                RuntimeUtils::SleepMs(config.guardianRestartDelayMs);
                continue;
            }

            logger.Log(LogLevel::Warn, "child process exited with code " + std::to_string(result.exitCode));

            if (g_stopRequested)
                break;

            if (result.exitCode == 0 && !config.guardianRestartOnZeroExit)
            {
                logger.Log(LogLevel::Info, "child exited cleanly; guardian will stop");
                break;
            }

            alerts.Report("server process stopped", "child exited with code " + std::to_string(result.exitCode) + ", restarting");
            RuntimeUtils::SleepMs(config.guardianRestartDelayMs);
        }

        logger.Log(LogLevel::Info, "guardian shutting down");
        logger.Stop();
        return 0;
    }
    catch (const std::exception& ex)
    {
        std::cerr << "[Guardian] Fatal error: " << ex.what() << std::endl;
        return 1;
    }
}
