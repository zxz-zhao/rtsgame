#ifndef NETWORK_MONITOR_H
#define NETWORK_MONITOR_H

#include <string>

namespace NetworkAccelerator {
    // Check internet connection using sockets & probes
    bool CheckInternetConnection(int timeoutSec = 3);
}

#endif // NETWORK_MONITOR_H
