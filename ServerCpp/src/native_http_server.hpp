#pragma once

#include "async_logger.hpp"
#include "app_config.hpp"
#include "server_service.hpp"

#include <atomic>

#ifdef _WIN32
#include <winsock2.h>
typedef SOCKET NativeSocketHandle;
#else
typedef int NativeSocketHandle;
#endif

class NativeHttpServer
{
public:
    NativeHttpServer(const AppConfig& config, AsyncLogger& logger, ServerService& service);
    ~NativeHttpServer();

    int Run();
    void Stop();

private:
    AppConfig config_;
    AsyncLogger& logger_;
    ServerService& service_;
    std::atomic<bool> stopRequested_;
    NativeSocketHandle listenSocket_;
    bool networkInitialized_;

    int InitializeNetwork();
    int OpenListenSocket();
    void HandleClient(NativeSocketHandle clientSocket, const std::string& clientIp);
};
