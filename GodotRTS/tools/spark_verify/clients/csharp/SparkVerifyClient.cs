// ==============================================================================
// SparkVerify 极验网络验证系统 - C# (.NET / Unity / Godot) 客户端 SDK
// 支持: 一机一码硬件指纹、HMAC防抓包签名、卡密登录、异步心跳保活、云变量下发
// ==============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SparkVerify
{
    public class SparkVerifyClient : IDisposable
    {
        public string Host { get; }
        public string AppId { get; }
        public string AppSecret { get; }
        public string Version { get; }
        public string Hwid { get; private set; }

        public string SessionToken { get; private set; }
        public string ExpiresAt { get; private set; }
        public Dictionary<string, string> CloudVars { get; private set; } = new();

        private readonly HttpClient _http = new();
        private CancellationTokenSource _heartbeatCts;
        private int _heartbeatInterval = 25;

        public SparkVerifyClient(string host, string appId, string appSecret, string version = "1.0.0")
        {
            Host = host.TrimEnd('/');
            AppId = appId;
            AppSecret = appSecret;
            Version = version;
            Hwid = GenerateHwid();
        }

        /// <summary>
        /// 采集 Windows 硬件特征码并哈希（CPU ID + 主板 UUID + 硬盘序列号）
        /// </summary>
        public static string GenerateHwid()
        {
            var sb = new StringBuilder();
            try
            {
                // 1. 主板 UUID
                using var mosMb = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                foreach (var mo in mosMb.Get())
                {
                    sb.Append(mo["UUID"]?.ToString() ?? "");
                    break;
                }

                // 2. CPU ID
                using var mosCpu = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var mo in mosCpu.Get())
                {
                    sb.Append(mo["ProcessorId"]?.ToString() ?? "");
                    break;
                }

                // 3. 硬盘序列号
                using var mosDisk = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (var mo in mosDisk.Get())
                {
                    sb.Append(mo["SerialNumber"]?.ToString() ?? "");
                    break;
                }
            }
            catch
            {
                sb.Append(Environment.MachineName);
                sb.Append(Environment.UserName);
            }

            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(hash).Substring(0, 32);
        }

        private string ComputeHmacSha256(string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AppSecret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task<JsonDocument> PostJsonAsync(string path, object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync($"{Host}{path}", content);
            string responseString = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(responseString);
        }

        /// <summary>
        /// 初始化应用公告与版本校验
        /// </summary>
        public async Task<(bool success, string appName, string notice, string latestVersion, bool forceUpdate)> InitAppAsync()
        {
            var req = new { app_id = AppId, version = Version, hwid = Hwid };
            var doc = await PostJsonAsync("/api/v1/client/init", req);
            var root = doc.RootElement;
            if (root.GetProperty("code").GetInt32() == 200)
            {
                var data = root.GetProperty("data");
                return (
                    true,
                    data.GetProperty("app_name").GetString(),
                    data.GetProperty("notice").GetString(),
                    data.GetProperty("latest_version").GetString(),
                    data.GetProperty("force_update").GetBoolean()
                );
            }
            return (false, "", "", "", false);
        }

        /// <summary>
        /// 卡密授权验证与设备绑定
        /// </summary>
        public async Task<(bool success, string message)> VerifyCardAsync(string cardKey, bool startHeartbeat = true)
        {
            long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string signStr = $"app_id={AppId}&card_key={cardKey}&hwid={Hwid}&timestamp={ts}";
            string sign = ComputeHmacSha256(signStr);

            var req = new
            {
                app_id = AppId,
                card_key = cardKey,
                hwid = Hwid,
                timestamp = ts,
                sign = sign
            };

            var doc = await PostJsonAsync("/api/v1/client/verify", req);
            var root = doc.RootElement;
            int code = root.GetProperty("code").GetInt32();
            string msg = root.GetProperty("msg").GetString();

            if (code == 200)
            {
                var data = root.GetProperty("data");
                SessionToken = data.GetProperty("session_token").GetString();
                ExpiresAt = data.GetProperty("expires_at").GetString();
                _heartbeatInterval = data.GetProperty("heartbeat_interval").GetInt32();

                if (data.TryGetProperty("cloud_vars", out var cvElem))
                {
                    CloudVars.Clear();
                    foreach (var prop in cvElem.EnumerateObject())
                    {
                        CloudVars[prop.Name] = prop.Value.GetString();
                    }
                }

                if (startHeartbeat)
                {
                    StartHeartbeatLoop();
                }
                return (true, msg);
            }

            return (false, msg);
        }

        /// <summary>
        /// 启动后台心跳循环
        /// </summary>
        private void StartHeartbeatLoop()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts = new CancellationTokenSource();
            var token = _heartbeatCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_heartbeatInterval * 1000, token);
                    if (token.IsCancellationRequested) break;

                    long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var req = new
                    {
                        app_id = AppId,
                        session_token = SessionToken,
                        hwid = Hwid,
                        timestamp = ts
                    };

                    try
                    {
                        var doc = await PostJsonAsync("/api/v1/client/heartbeat", req);
                        int code = doc.RootElement.GetProperty("code").GetInt32();
                        if (code != 200)
                        {
                            Console.WriteLine($"[SparkVerify] 心跳离线或被踢下线: {doc.RootElement.GetProperty("msg").GetString()}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SparkVerify] 心跳网络异常: {ex.Message}");
                    }
                }
            }, token);
        }

        public void Dispose()
        {
            _heartbeatCts?.Cancel();
            _http?.Dispose();
        }
    }
}
