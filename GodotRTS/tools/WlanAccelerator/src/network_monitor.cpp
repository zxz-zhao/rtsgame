#include "network_monitor.h"
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <iostream>

#pragma comment(lib, "ws2_32.lib")

namespace NetworkAccelerator {

    bool CheckInternetConnection(int timeoutSec) {
        WSADATA wsaData;
        if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
            return false;
        }

        struct Target {
            const char* ip;
            int port;
        };

        Target targets[] = {
            {"114.114.114.114", 53},
            {"8.8.8.8", 53},
            {"223.5.5.5", 53},
            {"1.1.1.1", 53}
        };

        bool connected = false;

        for (const auto& t : targets) {
            SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
            if (sock == INVALID_SOCKET) continue;

            sockaddr_in addr;
            addr.sin_family = AF_INET;
            addr.sin_port = htons(t.port);
            addr.sin_addr.s_addr = inet_addr(t.ip);

            // Set non-blocking mode for select timeout connection
            u_long mode = 1;
            ioctlsocket(sock, FIONBIO, &mode);

            connect(sock, (sockaddr*)&addr, sizeof(addr));

            fd_set writeSet;
            FD_ZERO(&writeSet);
            FD_SET(sock, &writeSet);

            timeval timeout;
            timeout.tv_sec = timeoutSec;
            timeout.tv_usec = 0;

            if (select(0, NULL, &writeSet, NULL, &timeout) > 0) {
                connected = true;
                closesocket(sock);
                break;
            }

            closesocket(sock);
        }

        WSACleanup();
        return connected;
    }
}
