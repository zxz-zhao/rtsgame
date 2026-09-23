#ifndef WLAN_CONTROLLER_H
#define WLAN_CONTROLLER_H

#include <string>

namespace NetworkAccelerator {
    bool IsAdmin();
    bool RestartWlanInterface(const std::string& interfaceName);
}

#endif // WLAN_CONTROLLER_H
