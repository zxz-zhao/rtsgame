#pragma once

#include "app_config.hpp"
#include "async_logger.hpp"
#include "server_service.hpp"

#include <atomic>
#include <map>
#include <mutex>
#include <string>
#include <thread>

#ifdef _WIN32
#include <winsock2.h>
typedef SOCKET BattleRelaySocketHandle;
#else
typedef int BattleRelaySocketHandle;
#endif

class BattleRelayServer
{
public:
    BattleRelayServer(const AppConfig& config, AsyncLogger& logger, ServerService& service);
    ~BattleRelayServer();

    int Start();
    void Stop();

private:
    struct RelayRoomState
    {
        std::string roomId;
        std::string hostUserId;
        std::string guestUserId;
        BattleRelaySocketHandle hostSocket;
        BattleRelaySocketHandle guestSocket;
        int seed = 0;
    };

    struct ConnectionContext
    {
        BattleRelaySocketHandle socketHandle;
        std::string roomId;
        std::string userId;
        std::string username;
        std::string role;
        bool joined = false;
    };

    AppConfig config_;
    AsyncLogger& logger_;
    ServerService& service_;
    std::atomic<bool> stopRequested_;
    BattleRelaySocketHandle listenSocket_;
    bool networkInitialized_;
    std::thread acceptThread_;
    std::mutex roomsMutex_;
    std::map<std::string, RelayRoomState> rooms_;

    int InitializeNetwork();
    int OpenListenSocket();
    void AcceptLoop();
    void HandleClient(BattleRelaySocketHandle socketHandle);
    bool PerformHandshake(BattleRelaySocketHandle socketHandle);
    bool ReceiveHttpHeaders(BattleRelaySocketHandle socketHandle, std::string& requestText);
    bool SendTextMessage(BattleRelaySocketHandle socketHandle, const std::string& payload);
    bool SendJsonMessage(BattleRelaySocketHandle socketHandle, const nlohmann::json& payload);
    bool ReceiveFrame(BattleRelaySocketHandle socketHandle, int& opcode, std::string& payload);
    bool SendFrame(BattleRelaySocketHandle socketHandle, int opcode, const std::string& payload);
    void HandleJoinMessage(ConnectionContext& context, const nlohmann::json& message);
    void HandleCommandMessage(const ConnectionContext& context, const nlohmann::json& message);
    void CleanupConnection(const ConnectionContext& context);
};
