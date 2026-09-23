#include "task_installer.h"
#include "wlan_controller.h"
#include <windows.h>
#include <iostream>
#include <cstdlib>

namespace NetworkAccelerator {

    bool InstallBootTask(const std::string& exePath, const std::string& taskName) {
        if (!IsAdmin()) {
            std::cerr << "[ERROR] Administrator privileges are required to install boot task!" << std::endl;
            return false;
        }

        std::string cmd = "schtasks /create /f /tn \"" + taskName + "\" /tr \"\\\"" + exePath + "\\\"\" /sc onstart /ru \"SYSTEM\"";
        int ret = std::system(cmd.c_str());
        if (ret == 0) {
            std::cout << "[SUCCESS] Registered startup task '" << taskName << "'." << std::endl;
            return true;
        } else {
            std::cerr << "[ERROR] Failed to register startup task. Exit code: " << ret << std::endl;
            return false;
        }
    }

    bool UninstallBootTask(const std::string& taskName) {
        if (!IsAdmin()) {
            std::cerr << "[ERROR] Administrator privileges are required to uninstall boot task!" << std::endl;
            return false;
        }

        std::string cmd = "schtasks /delete /tn \"" + taskName + "\" /f";
        int ret = std::system(cmd.c_str());
        if (ret == 0) {
            std::cout << "[SUCCESS] Unregistered startup task '" << taskName << "'." << std::endl;
            return true;
        } else {
            std::cerr << "[ERROR] Failed to unregister startup task. Exit code: " << ret << std::endl;
            return false;
        }
    }
}
