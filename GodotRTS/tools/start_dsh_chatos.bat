@echo off
title 启动 DeepSeek Harness (ChatOS 反向代理接入)

echo [1/4] 清理可能的残留僵尸进程...
taskkill /f /im "DeepSeek Harness.exe" 2>nul

echo [2/4] 正在检查 ChatOS 反代服务 (端口 8000)...
netstat -ano | findstr :8000 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    echo 正在启动 ChatOS 反代服务器...
    start "" /b "E:\python13\python.exe" "H:\api\server.py"
    ping 127.0.0.1 -n 3 >nul
) else (
    echo ChatOS 反代服务已在运行 (端口 8000).
)

echo [3/4] 正在检查 New-API 协议网关 (端口 3000)...
netstat -ano | findstr :3000 | findstr LISTENING >nul
if %errorlevel% neq 0 (
    echo 正在启动 New-API 协议网关...
    start "" /b "E:\code\c++\UnityRTS\GodotRTS\tools\new-api\new-api.exe" --port 3000
    ping 127.0.0.1 -n 3 >nul
) else (
    echo New-API 协议网关已在运行 (端口 3000).
)

echo [4/4] 正在注入环境变量并启动 DeepSeek Harness 客户端...
set "DSH_HOME=C:\Users\Administrator\.dsh"
set "DEEPSEEK_API_KEY=pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q"
set "DEEPSEEK_BASE_URL=http://127.0.0.1:3000"

start "" "H:\deepseek\DeepSeek Harness.exe"

echo 启动成功！
ping 127.0.0.1 -n 2 >nul
exit
