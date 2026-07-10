#pragma once

#include <string>

namespace RuntimeUtils
{
bool EnsureDirectoryRecursive(const std::string& path);
bool FileExists(const std::string& path);
std::string JoinPath(const std::string& left, const std::string& right);
std::string GetExecutableDirectory();
std::string CurrentTimestampLocal();
std::string CurrentTimestampForFileName();
void SleepMs(unsigned int milliseconds);
std::string ReplaceAll(std::string value, const std::string& from, const std::string& to);
}
