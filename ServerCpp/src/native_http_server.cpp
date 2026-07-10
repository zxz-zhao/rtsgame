#include "native_http_server.hpp"

#include <cerrno>
#include <chrono>
#include <cstring>
#include <iostream>
#include <sstream>
#include <thread>

#ifdef _WIN32
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

namespace
{
#ifdef _WIN32
const NativeSocketHandle kInvalidSocket = INVALID_SOCKET;
#else
const NativeSocketHandle kInvalidSocket = -1;
#endif

void CloseSocket(NativeSocketHandle socketHandle)
{
#ifdef _WIN32
    if (socketHandle != kInvalidSocket)
        closesocket(socketHandle);
#else
    if (socketHandle != kInvalidSocket)
        close(socketHandle);
#endif
}

bool SendAll(NativeSocketHandle socketHandle, const std::string& payload)
{
    const char* data = payload.c_str();
    std::size_t remaining = payload.size();
    while (remaining > 0)
    {
#ifdef _WIN32
        const int sent = send(socketHandle, data, static_cast<int>(remaining), 0);
#else
        const ssize_t sent = send(socketHandle, data, remaining, 0);
#endif
        if (sent <= 0)
            return false;

        data += sent;
        remaining -= static_cast<std::size_t>(sent);
    }
    return true;
}

std::string StatusTextFor(int statusCode)
{
    switch (statusCode)
    {
    case 200: return "OK";
    case 201: return "Created";
    case 204: return "No Content";
    case 400: return "Bad Request";
    case 401: return "Unauthorized";
    case 404: return "Not Found";
    case 405: return "Method Not Allowed";
    case 500: return "Internal Server Error";
    case 501: return "Not Implemented";
    default: return "OK";
    }
}

std::string Trim(const std::string& value)
{
    std::size_t start = 0;
    while (start < value.size() && (value[start] == ' ' || value[start] == '\t' || value[start] == '\r' || value[start] == '\n'))
        ++start;

    std::size_t end = value.size();
    while (end > start && (value[end - 1] == ' ' || value[end - 1] == '\t' || value[end - 1] == '\r' || value[end - 1] == '\n'))
        --end;

    return value.substr(start, end - start);
}

std::string ToLower(std::string value)
{
    for (std::size_t i = 0; i < value.size(); ++i)
    {
        unsigned char ch = static_cast<unsigned char>(value[i]);
        if (ch >= 'A' && ch <= 'Z')
            value[i] = static_cast<char>(ch - 'A' + 'a');
    }
    return value;
}

std::string BuildHttpResponse(int statusCode, const std::string& contentType, const std::string& body)
{
    std::ostringstream response;
    response
        << "HTTP/1.1 " << statusCode << " " << StatusTextFor(statusCode) << "\r\n"
        << "Content-Type: " << contentType << "\r\n"
        << "Content-Length: " << body.size() << "\r\n"
        << "Connection: close\r\n"
        << "Access-Control-Allow-Origin: *\r\n"
        << "Access-Control-Allow-Headers: Content-Type, Authorization\r\n"
        << "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n"
        << "\r\n"
        << body;
    return response.str();
}

bool TryParseRequest(
    const std::string& rawRequest,
    std::string& method,
    std::string& path,
    std::map<std::string, std::string>& headers,
    std::string& body)
{
    const std::size_t headerEnd = rawRequest.find("\r\n\r\n");
    if (headerEnd == std::string::npos)
        return false;

    const std::string headerText = rawRequest.substr(0, headerEnd);
    body = rawRequest.substr(headerEnd + 4);

    std::istringstream stream(headerText);
    std::string line;
    if (!std::getline(stream, line))
        return false;

    if (!line.empty() && line[line.size() - 1] == '\r')
        line.erase(line.size() - 1);

    std::istringstream requestLine(line);
    std::string version;
    if (!(requestLine >> method >> path >> version))
        return false;

    while (std::getline(stream, line))
    {
        if (!line.empty() && line[line.size() - 1] == '\r')
            line.erase(line.size() - 1);
        if (line.empty())
            continue;

        const std::size_t colon = line.find(':');
        if (colon == std::string::npos)
            continue;

        const std::string key = ToLower(Trim(line.substr(0, colon)));
        const std::string value = Trim(line.substr(colon + 1));
        headers[key] = value;
    }

    return true;
}

bool ReceiveHttpRequest(NativeSocketHandle clientSocket, std::string& request)
{
    request.clear();
    char buffer[4096];
    const std::size_t maxBytes = 1024U * 1024U;
    std::size_t expectedTotal = 0;

    while (request.size() < maxBytes)
    {
#ifdef _WIN32
        const int bytesRead = recv(clientSocket, buffer, static_cast<int>(sizeof(buffer)), 0);
#else
        const ssize_t bytesRead = recv(clientSocket, buffer, sizeof(buffer), 0);
#endif
        if (bytesRead <= 0)
            break;

        request.append(buffer, static_cast<std::size_t>(bytesRead));

        const std::size_t headerEnd = request.find("\r\n\r\n");
        if (headerEnd == std::string::npos)
            continue;

        if (expectedTotal == 0)
        {
            const std::string headers = request.substr(0, headerEnd);
            std::size_t contentLength = 0;
            std::istringstream headerStream(headers);
            std::string line;
            std::getline(headerStream, line);
            while (std::getline(headerStream, line))
            {
                if (!line.empty() && line[line.size() - 1] == '\r')
                    line.erase(line.size() - 1);
                const std::size_t colon = line.find(':');
                if (colon == std::string::npos)
                    continue;
                const std::string key = ToLower(Trim(line.substr(0, colon)));
                if (key != "content-length")
                    continue;
                contentLength = static_cast<std::size_t>(std::strtoul(Trim(line.substr(colon + 1)).c_str(), nullptr, 10));
                break;
            }
            expectedTotal = headerEnd + 4 + contentLength;
        }

        if (request.size() >= expectedTotal)
            return true;
    }

    return !request.empty();
}
}

NativeHttpServer::NativeHttpServer(const AppConfig& config, AsyncLogger& logger, ServerService& service)
    : config_(config)
    , logger_(logger)
    , service_(service)
    , stopRequested_(false)
    , listenSocket_(kInvalidSocket)
    , networkInitialized_(false)
{
}

NativeHttpServer::~NativeHttpServer()
{
    Stop();
}

int NativeHttpServer::InitializeNetwork()
{
#ifdef _WIN32
    WSADATA wsaData;
    const int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0)
    {
        std::cerr << "[NativeServer] WSAStartup failed: " << result << std::endl;
        logger_.Log(LogLevel::Error, "WSAStartup failed with code " + std::to_string(result));
        return 1;
    }
    networkInitialized_ = true;
#endif
    return 0;
}

int NativeHttpServer::OpenListenSocket()
{
    listenSocket_ = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (listenSocket_ == kInvalidSocket)
    {
        std::cerr << "[NativeServer] Failed to create TCP socket." << std::endl;
        logger_.Log(LogLevel::Error, "failed to create TCP socket");
        return 1;
    }

    int reuseAddr = 1;
    setsockopt(listenSocket_, SOL_SOCKET, SO_REUSEADDR, reinterpret_cast<const char*>(&reuseAddr), sizeof(reuseAddr));

    sockaddr_in address;
    std::memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    address.sin_port = htons(config_.httpPort);

    if (config_.bindHost == "0.0.0.0")
    {
        address.sin_addr.s_addr = htonl(INADDR_ANY);
    }
    else
    {
        const unsigned long parsedAddress = inet_addr(config_.bindHost.c_str());
        if (parsedAddress == INADDR_NONE)
        {
            std::cerr << "[NativeServer] Invalid bind host " << config_.bindHost << std::endl;
            logger_.Log(LogLevel::Error, "invalid bind host " + config_.bindHost);
            CloseSocket(listenSocket_);
            listenSocket_ = kInvalidSocket;
            return 1;
        }
        address.sin_addr.s_addr = parsedAddress;
    }

    if (bind(listenSocket_, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0)
    {
        std::cerr << "[NativeServer] Failed to bind on " << config_.bindHost << ":" << config_.httpPort << std::endl;
        logger_.Log(LogLevel::Error, "failed to bind on " + config_.bindHost + ":" + std::to_string(config_.httpPort));
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidSocket;
        return 1;
    }

    if (listen(listenSocket_, 64) != 0)
    {
        std::cerr << "[NativeServer] Failed to listen on TCP socket." << std::endl;
        logger_.Log(LogLevel::Error, "failed to enter listen state on TCP socket");
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidSocket;
        return 1;
    }

    return 0;
}

void NativeHttpServer::HandleClient(NativeSocketHandle clientSocket, const std::string& clientIp)
{
    std::string rawRequest;
    if (!ReceiveHttpRequest(clientSocket, rawRequest))
    {
        CloseSocket(clientSocket);
        return;
    }

    std::string method;
    std::string path;
    std::string body;
    std::map<std::string, std::string> headers;
    if (!TryParseRequest(rawRequest, method, path, headers, body))
    {
        const std::string response = BuildHttpResponse(400, "application/json; charset=utf-8", "{\"success\":false,\"error\":\"Bad Request\"}");
        SendAll(clientSocket, response);
        CloseSocket(clientSocket);
        return;
    }

    if (method == "OPTIONS")
    {
        const std::string response = BuildHttpResponse(204, "text/plain; charset=utf-8", "");
        SendAll(clientSocket, response);
        CloseSocket(clientSocket);
        return;
    }

    int statusCode = 200;
    std::string responseBody;
    std::string contentType = "application/json; charset=utf-8";

    if (path == "/" || path.empty())
    {
        contentType = "text/plain; charset=utf-8";
        responseBody = "UnityRTS native C++ server is running.\n";
    }
    else
    {
        try
        {
            const nlohmann::json jsonResponse = service_.HandleRequest(method, path, headers, body, clientIp, statusCode);
            responseBody = jsonResponse.dump();
        }
        catch (const std::exception& ex)
        {
            statusCode = 500;
            nlohmann::json error = {
                { "success", false },
                { "error", "Internal Server Error" },
                { "details", ex.what() }
            };
            responseBody = error.dump();
            logger_.Log(LogLevel::Error, std::string("request handling failed for ") + path + ": " + ex.what());
        }
    }

    const std::string response = BuildHttpResponse(statusCode, contentType, responseBody);
    SendAll(clientSocket, response);
    CloseSocket(clientSocket);
}

int NativeHttpServer::Run()
{
    if (InitializeNetwork() != 0)
        return 1;

    if (OpenListenSocket() != 0)
        return 1;

    std::cout
        << "[NativeServer] HTTP listening on " << config_.bindHost << ":" << config_.httpPort
        << " (native API)" << std::endl;
    std::cout
        << "[NativeServer] Battle WebSocket relay port: " << config_.wsPort
        << std::endl;
    logger_.Log(LogLevel::Info, "native server HTTP listening on " + config_.bindHost + ":" + std::to_string(config_.httpPort));
    logger_.Log(LogLevel::Info, "battle WebSocket relay port is " + std::to_string(config_.wsPort));

    while (!stopRequested_.load())
    {
        sockaddr_in clientAddress;
#ifdef _WIN32
        int clientLength = sizeof(clientAddress);
#else
        socklen_t clientLength = sizeof(clientAddress);
#endif
        NativeSocketHandle clientSocket = accept(
            listenSocket_,
            reinterpret_cast<sockaddr*>(&clientAddress),
            &clientLength
        );

        if (clientSocket == kInvalidSocket)
        {
            if (stopRequested_.load())
                break;
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            continue;
        }

        // Extract client IP string for rate-limiting
        char ipBuf[INET_ADDRSTRLEN] = {};
#ifdef _WIN32
        const char* ipStr = inet_ntoa(clientAddress.sin_addr);
        if (ipStr)
            std::strncpy(ipBuf, ipStr, sizeof(ipBuf) - 1);
#else
        inet_ntop(AF_INET, &clientAddress.sin_addr, ipBuf, sizeof(ipBuf));
#endif
        const std::string clientIp(ipBuf[0] ? ipBuf : "unknown");

        std::thread(&NativeHttpServer::HandleClient, this, clientSocket, clientIp).detach();
    }

#ifdef _WIN32
    if (networkInitialized_)
    {
        WSACleanup();
        networkInitialized_ = false;
    }
#endif

    return 0;
}

void NativeHttpServer::Stop()
{
    const bool alreadyStopping = stopRequested_.exchange(true);
    if (alreadyStopping)
        return;

    logger_.Log(LogLevel::Info, "stop requested");

    if (listenSocket_ != kInvalidSocket)
    {
        CloseSocket(listenSocket_);
        listenSocket_ = kInvalidSocket;
    }
}
