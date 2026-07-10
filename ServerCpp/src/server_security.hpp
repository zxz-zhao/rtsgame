#pragma once

#include "async_logger.hpp"

#include <cstdint>
#include <map>
#include <mutex>
#include <string>
#include <vector>

// ---------------------------------------------------------------------------
// ServerSecurity
//
// Centralises security concerns that cut across all request handlers:
//   1. Login / register rate-limiting  (per-IP and per-account)
//   2. HMAC-SHA256 request signature validation
//   3. Client build-number whitelist
//   4. In-battle command frequency cap (per userId per second)
//   5. Suspicion score helpers
//
// All public methods are thread-safe.
// ---------------------------------------------------------------------------
class ServerSecurity
{
public:
    explicit ServerSecurity(AsyncLogger& logger);

    // -----------------------------------------------------------------------
    // 1. Rate limiting
    // -----------------------------------------------------------------------

    // Returns true when the caller is allowed to attempt login / register.
    // Call BEFORE any credential check.  Passing correct credentials later
    // resets the failure counter; call RecordLoginSuccess on success.
    bool CheckLoginRateLimit(const std::string& ip, const std::string& username);

    // Must be called when login succeeds so the failure window is reset.
    void RecordLoginSuccess(const std::string& ip, const std::string& username);

    // Called when login fails (wrong password, bad token, ...).
    void RecordLoginFailure(const std::string& ip, const std::string& username);

    // -----------------------------------------------------------------------
    // 2. HMAC-SHA256 request signature
    // -----------------------------------------------------------------------

    // Verify the X-Client-Sig / X-Client-Ts headers produced by the client.
    //   method     : upper-cased HTTP method ("GET", "POST", ...)
    //   path       : normalised request path ("/api/login")
    //   tsHeader   : value of the X-Client-Ts header (seconds since epoch)
    //   sigHeader  : value of the X-Client-Sig header (hex HMAC-SHA256)
    //   body       : raw request body string
    //   sharedKey  : shared HMAC key (from config)
    //   nowMs      : current time in milliseconds
    //   maxSkewMs  : maximum allowed clock skew (default 60 000 ms)
    //
    // Returns true when the signature is valid and the timestamp is fresh.
    static bool VerifyRequestSignature(
        const std::string& method,
        const std::string& path,
        const std::string& tsHeader,
        const std::string& sigHeader,
        const std::string& body,
        const std::string& sharedKey,
        std::int64_t nowMs,
        std::int64_t maxSkewMs = 60000LL);

    // -----------------------------------------------------------------------
    // 3. Build whitelist
    // -----------------------------------------------------------------------

    // Returns true when buildNum is present in the allowed-builds list.
    // An empty whitelist means "allow all".
    static bool IsBuildAllowed(int buildNum, const std::vector<int>& allowedBuilds);

    // -----------------------------------------------------------------------
    // 4. Battle command frequency cap
    // -----------------------------------------------------------------------

    // Returns true when the userId is within the allowed command-per-second
    // budget.  Call once per battle relay command.
    // maxPerSecond : maximum commands per second (recommended: 30)
    bool CheckCommandRate(const std::string& userId, int maxPerSecond = 30);

    // Generic rate limiting (sliding window, thread-safe)
    bool CheckGenericRateLimit(const std::string& key, const std::string& bucketName, int maxRequests, std::int64_t windowMs);

    // -----------------------------------------------------------------------
    // 5. Suspicion-score constants
    // -----------------------------------------------------------------------
    static constexpr int kSuspicionIncrement = 10;
    static constexpr int kSuspicionBanThreshold = 100;

private:
    // Rate-limit state for a single endpoint (IP or account key).
    struct RateBucket
    {
        int failures = 0;
        std::int64_t windowStart = 0;   // ms
        std::int64_t lockedUntil = 0;   // ms; 0 = not locked
    };

    // Command-frequency state per userId.
    struct CommandBucket
    {
        int count = 0;
        std::int64_t windowStart = 0;   // ms
    };

    AsyncLogger& logger_;

    std::mutex rateMutex_;
    std::map<std::string, RateBucket> ipBuckets_;       // keyed by IP
    std::map<std::string, RateBucket> accountBuckets_;  // keyed by username
    std::map<std::string, std::map<std::string, std::vector<std::int64_t>>> genericBuckets_; // bucketName -> {key -> timestamps}

    std::mutex cmdMutex_;
    std::map<std::string, CommandBucket> cmdBuckets_;   // keyed by userId

    // -- helpers --
    static std::int64_t NowMs();
    static std::string HmacSha256Hex(const std::string& key, const std::string& message);
    static bool ConstantTimeEqual(const std::string& a, const std::string& b);
};
