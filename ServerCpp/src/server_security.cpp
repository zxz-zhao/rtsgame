#include "server_security.hpp"

#include "server_json_utils.hpp"

#include <algorithm>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <sstream>

// ---------------------------------------------------------------------------
// HMAC-SHA256 — pure C++11 implementation (no OpenSSL dependency).
// Uses the standard FIPS 198-1 construction with the SHA-256 compression
// function below.
// ---------------------------------------------------------------------------
namespace
{
// ---- SHA-256 ---------------------------------------------------------------
static const std::uint32_t kK[64] = {
    0x428a2f98u, 0x71374491u, 0xb5c0fbcfu, 0xe9b5dba5u,
    0x3956c25bu, 0x59f111f1u, 0x923f82a4u, 0xab1c5ed5u,
    0xd807aa98u, 0x12835b01u, 0x243185beu, 0x550c7dc3u,
    0x72be5d74u, 0x80deb1feu, 0x9bdc06a7u, 0xc19bf174u,
    0xe49b69c1u, 0xefbe4786u, 0x0fc19dc6u, 0x240ca1ccu,
    0x2de92c6fu, 0x4a7484aau, 0x5cb0a9dcu, 0x76f988dau,
    0x983e5152u, 0xa831c66du, 0xb00327c8u, 0xbf597fc7u,
    0xc6e00bf3u, 0xd5a79147u, 0x06ca6351u, 0x14292967u,
    0x27b70a85u, 0x2e1b2138u, 0x4d2c6dfcu, 0x53380d13u,
    0x650a7354u, 0x766a0abbu, 0x81c2c92eu, 0x92722c85u,
    0xa2bfe8a1u, 0xa81a664bu, 0xc24b8b70u, 0xc76c51a3u,
    0xd192e819u, 0xd6990624u, 0xf40e3585u, 0x106aa070u,
    0x19a4c116u, 0x1e376c08u, 0x2748774cu, 0x34b0bcb5u,
    0x391c0cb3u, 0x4ed8aa4au, 0x5b9cca4fu, 0x682e6ff3u,
    0x748f82eeu, 0x78a5636fu, 0x84c87814u, 0x8cc70208u,
    0x90befffau, 0xa4506cebu, 0xbef9a3f7u, 0xc67178f2u
};

static inline std::uint32_t Rotr32(std::uint32_t x, int n)
{
    return (x >> n) | (x << (32 - n));
}

struct Sha256State
{
    std::uint32_t h[8];
    std::uint64_t bits = 0;
    unsigned char buf[64];
    std::size_t bufLen = 0;

    Sha256State()
    {
        h[0] = 0x6a09e667u; h[1] = 0xbb67ae85u;
        h[2] = 0x3c6ef372u; h[3] = 0xa54ff53au;
        h[4] = 0x510e527fu; h[5] = 0x9b05688cu;
        h[6] = 0x1f83d9abu; h[7] = 0x5be0cd19u;
    }

    void ProcessBlock(const unsigned char* block)
    {
        std::uint32_t w[64];
        for (int i = 0; i < 16; ++i)
        {
            w[i] = (static_cast<std::uint32_t>(block[i * 4])     << 24) |
                   (static_cast<std::uint32_t>(block[i * 4 + 1]) << 16) |
                   (static_cast<std::uint32_t>(block[i * 4 + 2]) <<  8) |
                    static_cast<std::uint32_t>(block[i * 4 + 3]);
        }
        for (int i = 16; i < 64; ++i)
        {
            std::uint32_t s0 = Rotr32(w[i-15],  7) ^ Rotr32(w[i-15], 18) ^ (w[i-15] >>  3);
            std::uint32_t s1 = Rotr32(w[i- 2], 17) ^ Rotr32(w[i- 2], 19) ^ (w[i- 2] >> 10);
            w[i] = w[i-16] + s0 + w[i-7] + s1;
        }

        std::uint32_t a = h[0], b = h[1], c = h[2], d = h[3];
        std::uint32_t e = h[4], f = h[5], g = h[6], hh = h[7];

        for (int i = 0; i < 64; ++i)
        {
            std::uint32_t S1   = Rotr32(e, 6)  ^ Rotr32(e, 11) ^ Rotr32(e, 25);
            std::uint32_t ch   = (e & f) ^ (~e & g);
            std::uint32_t temp1 = hh + S1 + ch + kK[i] + w[i];
            std::uint32_t S0   = Rotr32(a, 2)  ^ Rotr32(a, 13) ^ Rotr32(a, 22);
            std::uint32_t maj  = (a & b) ^ (a & c) ^ (b & c);
            std::uint32_t temp2 = S0 + maj;
            hh = g; g = f; f = e; e = d + temp1;
            d  = c; c = b; b = a; a = temp1 + temp2;
        }

        h[0] += a; h[1] += b; h[2] += c; h[3] += d;
        h[4] += e; h[5] += f; h[6] += g; h[7] += hh;
    }

    void Update(const unsigned char* data, std::size_t len)
    {
        bits += static_cast<std::uint64_t>(len) * 8u;
        while (len > 0)
        {
            std::size_t space = 64 - bufLen;
            std::size_t copy  = len < space ? len : space;
            std::memcpy(buf + bufLen, data, copy);
            bufLen += copy;
            data   += copy;
            len    -= copy;
            if (bufLen == 64)
            {
                ProcessBlock(buf);
                bufLen = 0;
            }
        }
    }

    std::string Final()
    {
        buf[bufLen++] = 0x80;
        if (bufLen > 56)
        {
            while (bufLen < 64)
                buf[bufLen++] = 0;
            ProcessBlock(buf);
            bufLen = 0;
        }
        while (bufLen < 56)
            buf[bufLen++] = 0;

        for (int i = 7; i >= 0; --i)
            buf[bufLen++] = static_cast<unsigned char>((bits >> (i * 8)) & 0xFF);

        ProcessBlock(buf);

        static const char* kHex = "0123456789abcdef";
        std::string out;
        out.reserve(64);
        for (int i = 0; i < 8; ++i)
        {
            out.push_back(kHex[(h[i] >> 28) & 0xF]);
            out.push_back(kHex[(h[i] >> 24) & 0xF]);
            out.push_back(kHex[(h[i] >> 20) & 0xF]);
            out.push_back(kHex[(h[i] >> 16) & 0xF]);
            out.push_back(kHex[(h[i] >> 12) & 0xF]);
            out.push_back(kHex[(h[i] >>  8) & 0xF]);
            out.push_back(kHex[(h[i] >>  4) & 0xF]);
            out.push_back(kHex[(h[i]      ) & 0xF]);
        }
        return out;
    }
};

// HMAC-SHA256
std::string HmacSha256(const std::string& key, const std::string& message)
{
    // Key preparation: if key > 64 bytes, hash it; pad to 64 bytes
    unsigned char k[64];
    std::memset(k, 0, sizeof(k));
    if (key.size() > 64)
    {
        Sha256State ks;
        ks.Update(reinterpret_cast<const unsigned char*>(key.data()), key.size());
        std::string hk = ks.Final();
        // hk is hex; convert to raw bytes for key material
        std::size_t rawLen = hk.size() / 2;
        if (rawLen > 64) rawLen = 64;
        for (std::size_t i = 0; i < rawLen; ++i)
        {
            int hi = (hk[i * 2]     >= 'a') ? (hk[i * 2]     - 'a' + 10) : (hk[i * 2]     - '0');
            int lo = (hk[i * 2 + 1] >= 'a') ? (hk[i * 2 + 1] - 'a' + 10) : (hk[i * 2 + 1] - '0');
            k[i] = static_cast<unsigned char>((hi << 4) | lo);
        }
    }
    else
    {
        std::memcpy(k, key.data(), key.size());
    }

    unsigned char opad[64], ipad[64];
    for (int i = 0; i < 64; ++i)
    {
        opad[i] = k[i] ^ 0x5Cu;
        ipad[i] = k[i] ^ 0x36u;
    }

    // Inner hash: H(ipad || message)
    Sha256State inner;
    inner.Update(ipad, 64);
    inner.Update(reinterpret_cast<const unsigned char*>(message.data()), message.size());
    std::string innerHex = inner.Final();

    // Convert innerHex back to raw bytes
    unsigned char innerRaw[32];
    for (int i = 0; i < 32; ++i)
    {
        int hi = (innerHex[i * 2]     >= 'a') ? (innerHex[i * 2]     - 'a' + 10) : (innerHex[i * 2]     - '0');
        int lo = (innerHex[i * 2 + 1] >= 'a') ? (innerHex[i * 2 + 1] - 'a' + 10) : (innerHex[i * 2 + 1] - '0');
        innerRaw[i] = static_cast<unsigned char>((hi << 4) | lo);
    }

    // Outer hash: H(opad || inner)
    Sha256State outer;
    outer.Update(opad, 64);
    outer.Update(innerRaw, 32);
    return outer.Final();
}
} // anonymous namespace

// ---------------------------------------------------------------------------
// ServerSecurity implementation
// ---------------------------------------------------------------------------

ServerSecurity::ServerSecurity(AsyncLogger& logger)
    : logger_(logger)
{
}

// ---------------------------------------------------------------------------
// 1. Rate limiting
// ---------------------------------------------------------------------------

// Configuration constants
static constexpr int     kMaxFailuresPerWindow = 10;
static constexpr std::int64_t kWindowMs        = 5LL * 60LL * 1000LL;   // 5 min
static constexpr std::int64_t kIpLockMs        = 5LL * 60LL * 1000LL;   // 5 min
static constexpr int     kMaxAccountFailures   = 5;
static constexpr std::int64_t kAccountLockMs   = 15LL * 60LL * 1000LL;  // 15 min

bool ServerSecurity::CheckLoginRateLimit(const std::string& ip, const std::string& username)
{
    std::int64_t now = NowMs();
    std::lock_guard<std::mutex> lock(rateMutex_);

    // Check IP bucket
    {
        RateBucket& bucket = ipBuckets_[ip];
        if (bucket.lockedUntil > now)
        {
            logger_.Log(LogLevel::Warn,
                "[Security] IP rate-limited ip=" + ip +
                " lockedUntilMs=" + std::to_string(bucket.lockedUntil));
            return false;
        }
        // Reset window if expired
        if (now - bucket.windowStart > kWindowMs)
        {
            bucket.failures    = 0;
            bucket.windowStart = now;
        }
    }

    // Check account bucket
    {
        RateBucket& bucket = accountBuckets_[username];
        if (bucket.lockedUntil > now)
        {
            logger_.Log(LogLevel::Warn,
                "[Security] Account rate-limited username=" + username +
                " lockedUntilMs=" + std::to_string(bucket.lockedUntil));
            return false;
        }
        if (now - bucket.windowStart > kWindowMs)
        {
            bucket.failures    = 0;
            bucket.windowStart = now;
        }
    }

    return true;
}

void ServerSecurity::RecordLoginSuccess(const std::string& ip, const std::string& username)
{
    std::lock_guard<std::mutex> lock(rateMutex_);
    ipBuckets_[ip].failures       = 0;
    accountBuckets_[username].failures = 0;
}

void ServerSecurity::RecordLoginFailure(const std::string& ip, const std::string& username)
{
    std::int64_t now = NowMs();
    std::lock_guard<std::mutex> lock(rateMutex_);

    {
        RateBucket& bucket = ipBuckets_[ip];
        if (now - bucket.windowStart > kWindowMs)
        {
            bucket.failures    = 0;
            bucket.windowStart = now;
        }
        ++bucket.failures;
        if (bucket.failures >= kMaxFailuresPerWindow)
        {
            bucket.lockedUntil = now + kIpLockMs;
            logger_.Log(LogLevel::Warn,
                "[Security] IP locked after " + std::to_string(bucket.failures) +
                " failures ip=" + ip);
        }
    }

    {
        RateBucket& bucket = accountBuckets_[username];
        if (now - bucket.windowStart > kWindowMs)
        {
            bucket.failures    = 0;
            bucket.windowStart = now;
        }
        ++bucket.failures;
        if (bucket.failures >= kMaxAccountFailures)
        {
            bucket.lockedUntil = now + kAccountLockMs;
            logger_.Log(LogLevel::Warn,
                "[Security] Account locked after " + std::to_string(bucket.failures) +
                " failures username=" + username);
        }
    }
}

// ---------------------------------------------------------------------------
// 2. HMAC-SHA256 request signature
// ---------------------------------------------------------------------------

bool ServerSecurity::VerifyRequestSignature(
    const std::string& method,
    const std::string& path,
    const std::string& tsHeader,
    const std::string& sigHeader,
    const std::string& body,
    const std::string& sharedKey,
    std::int64_t nowMs,
    std::int64_t maxSkewMs)
{
    if (sharedKey.empty())
        return true;  // signature check disabled when no key configured

    if (tsHeader.empty() || sigHeader.empty())
        return false;

    // Parse timestamp (seconds)
    const std::int64_t ts = static_cast<std::int64_t>(std::atoll(tsHeader.c_str()));
    const std::int64_t tsMs = ts * 1000LL;
    const std::int64_t diff = tsMs - nowMs;
    // Accept slight future skew too (< 5s) to be tolerant of client clock
    if (diff > 5000LL || diff < -maxSkewMs)
        return false;  // replay or too stale

    // Message = METHOD\nPATH\nTIMESTAMP\nBODY
    std::string message;
    message.reserve(method.size() + path.size() + tsHeader.size() + body.size() + 4);
    message += method;
    message += '\n';
    message += path;
    message += '\n';
    message += tsHeader;
    message += '\n';
    message += body;

    const std::string expected = HmacSha256(sharedKey, message);
    return ConstantTimeEqual(expected, sigHeader);
}

// ---------------------------------------------------------------------------
// 3. Build whitelist
// ---------------------------------------------------------------------------

bool ServerSecurity::IsBuildAllowed(int buildNum, const std::vector<int>& allowedBuilds)
{
    if (allowedBuilds.empty())
        return true;  // no whitelist configured — allow all
    return std::find(allowedBuilds.begin(), allowedBuilds.end(), buildNum) != allowedBuilds.end();
}

// ---------------------------------------------------------------------------
// 4. Battle command frequency cap
// ---------------------------------------------------------------------------

bool ServerSecurity::CheckCommandRate(const std::string& userId, int maxPerSecond)
{
    const std::int64_t now = NowMs();
    std::lock_guard<std::mutex> lock(cmdMutex_);

    CommandBucket& bucket = cmdBuckets_[userId];
    if (now - bucket.windowStart >= 1000LL)
    {
        bucket.count       = 0;
        bucket.windowStart = now;
    }

    ++bucket.count;
    if (bucket.count > maxPerSecond)
    {
        // Log only on first excess per window to avoid log spam
        if (bucket.count == maxPerSecond + 1)
        {
            logger_.Log(LogLevel::Warn,
                "[Security] Battle command rate exceeded userId=" + userId +
                " count=" + std::to_string(bucket.count) +
                " max=" + std::to_string(maxPerSecond));
        }
        return false;
    }
    return true;
}

bool ServerSecurity::CheckGenericRateLimit(
    const std::string& key,
    const std::string& bucketName,
    int maxRequests,
    std::int64_t windowMs)
{
    const std::int64_t now = NowMs();
    std::lock_guard<std::mutex> lock(rateMutex_);

    std::vector<std::int64_t>& timestamps = genericBuckets_[bucketName][key];

    // Remove expired timestamps
    timestamps.erase(
        std::remove_if(timestamps.begin(), timestamps.end(), [now, windowMs](std::int64_t ts) {
            return now - ts > windowMs;
        }),
        timestamps.end()
    );

    if (static_cast<int>(timestamps.size()) >= maxRequests)
    {
        logger_.Log(LogLevel::Warn,
            "[Security] Generic rate limit exceeded bucket=" + bucketName +
            " key=" + key +
            " count=" + std::to_string(timestamps.size()) +
            " max=" + std::to_string(maxRequests));
        return false;
    }

    timestamps.push_back(now);
    return true;
}

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

std::int64_t ServerSecurity::NowMs()
{
    return ServerJson::CurrentTimeMs();
}

std::string ServerSecurity::HmacSha256Hex(const std::string& key, const std::string& message)
{
    return HmacSha256(key, message);
}

bool ServerSecurity::ConstantTimeEqual(const std::string& a, const std::string& b)
{
    if (a.size() != b.size())
        return false;
    unsigned char diff = 0;
    for (std::size_t i = 0; i < a.size(); ++i)
        diff |= static_cast<unsigned char>(a[i]) ^ static_cast<unsigned char>(b[i]);
    return diff == 0;
}
