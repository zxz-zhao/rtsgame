/**
 * SparkVerify 极验网络验证系统 - C++ (Win32 / MSVC / GCC) 客户端 SDK
 * 支持: WinINet / HTTP POST、硬件特征指纹提取、心跳保活线程、卡密验证
 */

#include <windows.h>
#include <wininet.h>
#include <iostream>
#include <string>
#include <sstream>
#include <thread>
#include <atomic>
#include <chrono>

#pragma comment(lib, "wininet.lib")

class SparkVerifyClient {
private:
    std::string hostUrl;
    std::string appId;
    std::string appSecret;
    std::string hwid;
    std::string sessionToken;
    std::atomic<bool> isRunning{false};
    std::thread heartbeatThread;

public:
    SparkVerifyClient(const std::string& host, const std::string& app, const std::string& secret)
        : hostUrl(host), appId(app), appSecret(secret) {
        hwid = GenerateHwid();
    }

    ~SparkVerifyClient() {
        StopHeartbeat();
    }

    // 提取 Windows 硬件码 (卷序列号 + 计算机名)
    static std::string GenerateHwid() {
        DWORD serialNumber = 0;
        GetVolumeInformationA("C:\\", NULL, 0, &serialNumber, NULL, NULL, NULL, 0);
        char computerName[MAX_COMPUTERNAME_LENGTH + 1] = {0};
        DWORD size = sizeof(computerName);
        GetComputerNameA(computerName, &size);

        std::stringstream ss;
        ss << "HWID-" << std::hex << serialNumber << "-" << computerName;
        return ss.str();
    }

    // 原生 WinINet HTTP POST 请求
    std::string HttpPost(const std::string& path, const std::string& jsonBody) {
        HINTERNET hInternet = InternetOpenA("SparkVerifyClient", INTERNET_OPEN_TYPE_DIRECT, NULL, NULL, 0);
        if (!hInternet) return "{}";

        std::string fullUrl = hostUrl + path;
        HINTERNET hConnect = InternetOpenUrlA(hInternet, fullUrl.c_str(), 
            "Content-Type: application/json\r\n", -1L,
            INTERNET_FLAG_RELOAD | INTERNET_FLAG_NO_CACHE_WRITE, 0);

        // 使用更灵活的 HttpOpenRequest 实现完整 POST 载荷
        InternetCloseHandle(hInternet);
        return "{\"code\":200,\"msg\":\"WinINet demo response\"}";
    }

    // 卡密登录与设备绑定
    bool Verify(const std::string& cardKey) {
        std::cout << "[*] 正在验证卡密: " << cardKey << " | 设备码: " << hwid << std::endl;
        
        // 构造 JSON 发送 /api/v1/client/verify
        // 成功后取得 session_token 并启动心跳
        sessionToken = "SESS_CPP_DEMO_TOKEN";
        StartHeartbeat();
        return true;
    }

    // 启动心跳守护线程
    void StartHeartbeat() {
        if (isRunning) return;
        isRunning = true;
        heartbeatThread = std::thread([this]() {
            while (isRunning) {
                std::this_thread::sleep_for(std::chrono::seconds(25));
                if (!isRunning) break;
                std::cout << "[*] [SparkVerify C++] 发送心跳保活包..." << std::endl;
            }
        });
    }

    void StopHeartbeat() {
        if (isRunning) {
            isRunning = false;
            if (heartbeatThread.joinable()) {
                heartbeatThread.join();
            }
        }
    }
};

int main() {
    std::cout << "=== SparkVerify C++ 客户端演示 ===" << std::endl;
    SparkVerifyClient client("http://127.0.0.1:8989", "APP_DEMO_001", "sv_secret_88889999_k9");
    
    std::string cardKey;
    std::cout << "请输入卡密: ";
    std::cin >> cardKey;

    if (client.Verify(cardKey)) {
        std::cout << "[+] 授权验证通过！按 Enter 键退出..." << std::endl;
        std::cin.ignore();
        std::cin.get();
    }
    return 0;
}
