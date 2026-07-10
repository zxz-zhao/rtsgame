#include "server_crypto.hpp"

#include "runtime_utils.hpp"

extern "C" {
#include "crypt_blowfish.h"
}

#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <random>
#include <sstream>
#include <vector>

namespace
{
std::mt19937_64& RandomEngine()
{
    static std::mt19937_64 engine(static_cast<unsigned long long>(std::random_device()()) ^
        (static_cast<unsigned long long>(std::time(nullptr)) << 16));
    return engine;
}

std::string RandomBytes(std::size_t size)
{
    std::string output(size, '\0');
    std::uniform_int_distribution<int> dist(0, 255);
    for (std::size_t i = 0; i < size; ++i)
        output[i] = static_cast<char>(dist(RandomEngine()));
    return output;
}

std::string ToHex(const std::string& bytes)
{
    static const char* kHex = "0123456789abcdef";
    std::string out;
    out.reserve(bytes.size() * 2);
    for (std::size_t i = 0; i < bytes.size(); ++i)
    {
        const unsigned char ch = static_cast<unsigned char>(bytes[i]);
        out.push_back(kHex[ch >> 4]);
        out.push_back(kHex[ch & 0x0F]);
    }
    return out;
}
}

namespace ServerCrypto
{
bool VerifyPassword(const std::string& password, const std::string& storedHash)
{
    if (storedHash.empty())
        return false;

    if (storedHash.size() >= 4 && storedHash[0] == '$' && storedHash[1] == '2')
    {
        char buffer[128];
        std::memset(buffer, 0, sizeof(buffer));
        char* result = _crypt_blowfish_rn(password.c_str(), storedHash.c_str(), buffer, static_cast<int>(sizeof(buffer)));
        return result != nullptr && storedHash == result;
    }

    return storedHash == password;
}

std::string HashPassword(const std::string& password, const AppConfig& config)
{
    std::string saltInput = RandomBytes(16);
    char salt[64];
    std::memset(salt, 0, sizeof(salt));
    if (_crypt_gensalt_blowfish_rn("$2b$", config.bcryptCost, saltInput.c_str(), static_cast<int>(saltInput.size()), salt, static_cast<int>(sizeof(salt))) == nullptr)
        return password;

    char output[128];
    std::memset(output, 0, sizeof(output));
    char* result = _crypt_blowfish_rn(password.c_str(), salt, output, static_cast<int>(sizeof(output)));
    return result != nullptr ? result : password;
}

std::string GenerateOpaqueToken(const AppConfig& config)
{
    std::ostringstream out;
    out << ToHex(RandomBytes(config.authTokenBytes > 0 ? config.authTokenBytes : 32));
    return out.str();
}

std::string GenerateUuidLike()
{
    std::string bytes = RandomBytes(16);
    unsigned char* data = reinterpret_cast<unsigned char*>(&bytes[0]);
    data[6] = static_cast<unsigned char>((data[6] & 0x0F) | 0x40);
    data[8] = static_cast<unsigned char>((data[8] & 0x3F) | 0x80);

    std::ostringstream out;
    out << ToHex(bytes.substr(0, 4)) << "-"
        << ToHex(bytes.substr(4, 2)) << "-"
        << ToHex(bytes.substr(6, 2)) << "-"
        << ToHex(bytes.substr(8, 2)) << "-"
        << ToHex(bytes.substr(10, 6));
    return out.str();
}

std::string SanitizeDisplayName(const std::string& value)
{
    std::string output;
    output.reserve(value.size());
    for (std::string::const_iterator it = value.begin(); it != value.end(); ++it)
    {
        if (*it == '<' || *it == '>' || *it == '\'' || *it == '"')
            continue;
        output.push_back(*it);
    }

    while (!output.empty() && std::isspace(static_cast<unsigned char>(output.front())))
        output.erase(output.begin());
    while (!output.empty() && std::isspace(static_cast<unsigned char>(output.back())))
        output.erase(output.end() - 1);
    return output;
}
}
