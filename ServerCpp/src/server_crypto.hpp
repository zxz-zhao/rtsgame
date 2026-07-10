#pragma once

#include "app_config.hpp"

#include <string>

namespace ServerCrypto
{
bool VerifyPassword(const std::string& password, const std::string& storedHash);
std::string HashPassword(const std::string& password, const AppConfig& config);
std::string GenerateOpaqueToken(const AppConfig& config);
std::string GenerateUuidLike();
std::string SanitizeDisplayName(const std::string& value);
}
