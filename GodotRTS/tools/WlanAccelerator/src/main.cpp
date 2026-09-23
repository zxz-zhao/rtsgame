#include "network_monitor.h"
#include "wlan_controller.h"
#include "task_installer.h"
#include "logger.h"

#include <windows.h>
#include <iostream>
#include <string>
#include <thread>
#include <chrono>

void PrintUsage() {
    std::cout << "WlanAccelerator v1.0 - C++ Native Network Accelerator & Reconnect Tool\n\n"
              << "Usage:\n"
              << "  WlanAccelerator.exe [options]\n\n"
              << "Options:\n"
              << "  --install, -i        Register tool to run automatically at system boot\n"
              << "  --uninstall, -u      Unregister system boot task\n"
              << "  --interface <name>   WLAN adapter name (default: WLAN)\n"
              << "  --retries <count>    Max monitoring retry count (default: 5)\n"
              << "  --interval <mins>    Interval between checks in minutes (default: 3)\n"
              << "  --help, -h           Show this help message\n";
}

std::string GetExePath() {
    char buffer[MAX_PATH];
    GetModuleFileNameA(NULL, buffer, MAX_PATH);
    return std::string(buffer);
}

std::string GetExeDirectory() {
    std::string exePath = GetExePath();
    size_t pos = exePath.find_last_of("\\/");
    return (pos == std::string::npos) ? "" : exePath.substr(0, pos);
}

int main(int argc, char* argv[]) {
    std::string exeDir = GetExeDirectory();
    std::string logPath = exeDir + "\\WlanAccelerator.log";
    NetworkAccelerator::Logger::Instance().Init(logPath);

    std::string interfaceName = "WLAN";
    int maxRetries = 5;
    int intervalMins = 3;

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--install" || arg == "-i") {
            return NetworkAccelerator::InstallBootTask(GetExePath()) ? 0 : 1;
        } else if (arg == "--uninstall" || arg == "-u") {
            return NetworkAccelerator::UninstallBootTask() ? 0 : 1;
        } else if (arg == "--interface" && i + 1 < argc) {
            interfaceName = argv[++i];
        } else if (arg == "--retries" && i + 1 < argc) {
            maxRetries = std::stoi(argv[++i]);
        } else if (arg == "--interval" && i + 1 < argc) {
            intervalMins = std::stoi(argv[++i]);
        } else if (arg == "--help" || arg == "-h") {
            PrintUsage();
            return 0;
        }
    }

    NetworkAccelerator::Logger::Instance().Log("INFO", "Starting WlanAccelerator C++ native monitor...");
    std::string configMsg = "Config: Interface='" + interfaceName + "', Retries=" + std::to_string(maxRetries) + ", Interval=" + std::to_string(intervalMins) + "min";
    NetworkAccelerator::Logger::Instance().Log("INFO", configMsg);

    for (int attempt = 1; attempt <= maxRetries; ++attempt) {
        std::string waitMsg = "Waiting " + std::to_string(intervalMins) + " minute(s) before check " + std::to_string(attempt) + "/" + std::to_string(maxRetries) + "...";
        NetworkAccelerator::Logger::Instance().Log("INFO", waitMsg);
        
        std::this_thread::sleep_for(std::chrono::minutes(intervalMins));

        NetworkAccelerator::Logger::Instance().Log("INFO", "Testing internet connectivity...");
        if (NetworkAccelerator::CheckInternetConnection(3)) {
            NetworkAccelerator::Logger::Instance().Log("INFO", "Internet connection verified online! Exiting monitor.");
            return 0;
        }

        NetworkAccelerator::Logger::Instance().Log("WARNING", "No internet connection detected! Restarting WLAN adapter...");
        if (NetworkAccelerator::RestartWlanInterface(interfaceName)) {
            NetworkAccelerator::Logger::Instance().Log("INFO", "WLAN restart completed. Waiting 10s for IP assignment...");
            std::this_thread::sleep_for(std::chrono::seconds(10));

            if (NetworkAccelerator::CheckInternetConnection(5)) {
                NetworkAccelerator::Logger::Instance().Log("INFO", "Internet connection successfully restored after WLAN restart! Exiting monitor.");
                return 0;
            } else {
                NetworkAccelerator::Logger::Instance().Log("WARNING", "Internet still offline after WLAN restart.");
            }
        } else {
            NetworkAccelerator::Logger::Instance().Log("ERROR", "Failed to restart WLAN adapter.");
        }
    }

    NetworkAccelerator::Logger::Instance().Log("ERROR", "Completed all retry attempts. Internet connection could not be restored.");
    return 1;
}
