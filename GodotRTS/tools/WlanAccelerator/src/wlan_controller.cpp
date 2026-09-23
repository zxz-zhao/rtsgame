#include "wlan_controller.h"
#include <windows.h>
#include <shlobj.h>
#include <iostream>
#include <sstream>
#include <thread>
#include <chrono>

namespace NetworkAccelerator {

    bool IsAdmin() {
        BOOL fIsRunAsAdmin = FALSE;
        PSID pAdministratorsGroup = NULL;
        SID_IDENTIFIER_AUTHORITY NtAuthority = SECURITY_NT_AUTHORITY;

        if (AllocateAndInitializeSid(
            &NtAuthority, 2,
            SECURITY_BUILTIN_DOMAIN_RID,
            DOMAIN_ALIAS_RID_ADMINS,
            0, 0, 0, 0, 0, 0,
            &pAdministratorsGroup)) {

            CheckTokenMembership(NULL, pAdministratorsGroup, &fIsRunAsAdmin);
            FreeSid(pAdministratorsGroup);
        }

        return fIsRunAsAdmin != FALSE;
    }

    static std::string ExecuteCommand(const std::string& cmd) {
        std::string result = "";
        char buffer[256];
        FILE* pipe = _popen(cmd.c_str(), "r");
        if (!pipe) return result;

        while (fgets(buffer, sizeof(buffer), pipe) != NULL) {
            result += buffer;
        }
        _pclose(pipe);
        return result;
    }

    bool RestartWlanInterface(const std::string& interfaceName) {
        if (!IsAdmin()) {
            std::cerr << "[ERROR] Administrator privileges required to restart network interface!" << std::endl;
            return false;
        }

        std::cout << "[INFO] Disabling interface '" << interfaceName << "'..." << std::endl;
        std::string disableCmd = "netsh interface set interface name=\"" + interfaceName + "\" admin=disabled";
        ExecuteCommand(disableCmd);

        std::this_thread::sleep_for(std::chrono::seconds(5));

        std::cout << "[INFO] Enabling interface '" << interfaceName << "'..." << std::endl;
        std::string enableCmd = "netsh interface set interface name=\"" + interfaceName + "\" admin=enabled";
        ExecuteCommand(enableCmd);

        std::cout << "[INFO] Interface '" << interfaceName << "' restart sequence completed." << std::endl;
        return true;
    }
}
