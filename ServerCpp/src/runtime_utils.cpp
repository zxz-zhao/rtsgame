#include "runtime_utils.hpp"

#include <cerrno>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <ctime>
#include <sstream>
#include <thread>
#include <vector>

#ifdef _WIN32
#include <direct.h>
#include <Windows.h>
#include <sys/stat.h>
#define UNITY_RTS_MKDIR(path) _mkdir(path)
#else
#include <limits.h>
#include <unistd.h>
#include <sys/stat.h>
#include <sys/types.h>
#define UNITY_RTS_MKDIR(path) mkdir(path, 0755)
#endif

namespace
{
bool DirectoryExists(const std::string& path)
{
    struct stat info;
    return stat(path.c_str(), &info) == 0 && (info.st_mode & S_IFDIR) != 0;
}
}

namespace RuntimeUtils
{
bool EnsureDirectoryRecursive(const std::string& path)
{
    if (path.empty() || DirectoryExists(path))
        return true;

    std::string normalized = ReplaceAll(path, "\\", "/");
    if (normalized.empty())
        return true;

    std::vector<std::string> parts;
    std::stringstream input(normalized);
    std::string item;
    while (std::getline(input, item, '/'))
    {
        if (!item.empty())
            parts.push_back(item);
    }

    if (parts.empty())
        return true;

    std::string current;
    if (normalized.size() >= 2 && normalized[1] == ':')
        current = normalized.substr(0, 2);
    else if (!normalized.empty() && normalized[0] == '/')
        current = "/";

    for (std::size_t i = 0; i < parts.size(); ++i)
    {
        if (!current.empty() && current != "/" && current[current.size() - 1] != '/')
            current += "/";
        current += parts[i];

        if (DirectoryExists(current))
            continue;

        if (UNITY_RTS_MKDIR(current.c_str()) != 0 && errno != EEXIST)
            return false;
    }

    return true;
}

bool FileExists(const std::string& path)
{
    struct stat info;
    return stat(path.c_str(), &info) == 0;
}

std::string JoinPath(const std::string& left, const std::string& right)
{
    if (left.empty())
        return right;
    if (right.empty())
        return left;

    if (left[left.size() - 1] == '/' || left[left.size() - 1] == '\\')
        return left + right;

    return left + "/" + right;
}

std::string GetExecutableDirectory()
{
#ifdef _WIN32
    char buffer[MAX_PATH];
    const DWORD length = GetModuleFileNameA(nullptr, buffer, static_cast<DWORD>(sizeof(buffer)));
    if (length == 0 || length >= sizeof(buffer))
        return ".";

    std::string path(buffer, buffer + length);
#else
    char buffer[PATH_MAX];
    const ssize_t length = readlink("/proc/self/exe", buffer, sizeof(buffer) - 1);
    if (length <= 0)
        return ".";

    buffer[length] = '\0';
    std::string path(buffer);
#endif

    path = ReplaceAll(path, "\\", "/");
    const std::size_t slash = path.find_last_of('/');
    return slash == std::string::npos ? "." : path.substr(0, slash);
}

std::string CurrentTimestampLocal()
{
    const std::time_t now = std::time(nullptr);
    std::tm localTime;
#ifdef _WIN32
    localtime_s(&localTime, &now);
#else
    localtime_r(&now, &localTime);
#endif

    char buffer[64];
    std::strftime(buffer, sizeof(buffer), "%Y-%m-%d %H:%M:%S", &localTime);
    return buffer;
}

std::string CurrentTimestampForFileName()
{
    const std::time_t now = std::time(nullptr);
    std::tm localTime;
#ifdef _WIN32
    localtime_s(&localTime, &now);
#else
    localtime_r(&now, &localTime);
#endif

    char buffer[64];
    std::strftime(buffer, sizeof(buffer), "%Y%m%d-%H%M%S", &localTime);
    return buffer;
}

void SleepMs(unsigned int milliseconds)
{
    std::this_thread::sleep_for(std::chrono::milliseconds(milliseconds));
}

std::string ReplaceAll(std::string value, const std::string& from, const std::string& to)
{
    if (from.empty())
        return value;

    std::size_t position = 0;
    while ((position = value.find(from, position)) != std::string::npos)
    {
        value.replace(position, from.size(), to);
        position += to.size();
    }
    return value;
}
}
