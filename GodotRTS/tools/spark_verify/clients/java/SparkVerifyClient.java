package com.sparkverify;

import java.io.*;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.concurrent.*;

public class SparkVerifyClient {
    private final String host;
    private final String appId;
    private final String appSecret;
    private final String hwid;
    private String sessionToken;
    private ScheduledExecutorService heartbeatExecutor;

    public SparkVerifyClient(String host, String appId, String appSecret) {
        this.host = host.replaceAll("/$", "");
        this.appId = appId;
        this.appSecret = appSecret;
        this.hwid = generateHwid();
    }

    private String generateHwid() {
        try {
            String raw = System.getProperty("os.name") + System.getProperty("user.name") + Runtime.getRuntime().availableProcessors();
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] digest = md.digest(raw.getBytes(StandardCharsets.UTF_8));
            StringBuilder sb = new StringBuilder();
            for (byte b : digest) sb.append(String.format("%02X", b));
            return sb.substring(0, 32);
        } catch (Exception e) {
            return "JAVA_DEFAULT_HWID";
        }
    }

    private String postJson(String path, String jsonBody) throws IOException {
        URL url = new URL(host + path);
        HttpURLConnection conn = (HttpURLConnection) url.openConnection();
        conn.setRequestMethod("POST");
        conn.setRequestProperty("Content-Type", "application/json");
        conn.setDoOutput(true);
        try (OutputStream os = conn.getOutputStream()) {
            os.write(jsonBody.getBytes(StandardCharsets.UTF_8));
        }
        try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
            StringBuilder response = new StringBuilder();
            String line;
            while ((line = br.readLine()) != null) response.append(line);
            return response.toString();
        }
    }

    public boolean verify(String cardKey) {
        long ts = System.currentTimeMillis() / 1000;
        String json = String.format("{\"app_id\":\"%s\",\"card_key\":\"%s\",\"hwid\":\"%s\",\"timestamp\":%d}", appId, cardKey, hwid, ts);
        try {
            String res = postJson("/api/v1/client/verify", json);
            System.out.println("[SparkVerify Java] 服务端响应: " + res);
            startHeartbeat();
            return true;
        } catch (Exception e) {
            System.err.println("验证请求异常: " + e.getMessage());
            return false;
        }
    }

    private void startHeartbeat() {
        heartbeatExecutor = Executors.newSingleThreadScheduledExecutor();
        heartbeatExecutor.scheduleAtFixedRate(() -> {
            try {
                long ts = System.currentTimeMillis() / 1000;
                String json = String.format("{\"app_id\":\"%s\",\"session_token\":\"%s\",\"hwid\":\"%s\",\"timestamp\":%d}", appId, sessionToken, hwid, ts);
                postJson("/api/v1/client/heartbeat", json);
                System.out.println("[*] 心跳保活成功");
            } catch (Exception e) {
                System.err.println("心跳失败: " + e.getMessage());
            }
        }, 25, 25, TimeUnit.SECONDS);
    }
}
