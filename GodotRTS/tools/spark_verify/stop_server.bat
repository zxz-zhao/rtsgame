@echo off
chcp 65001 >nul
echo 正在停止 SparkVerify 服务 (端口 8989)...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":8989" ^| findstr "LISTENING"') do (
    taskkill /F /PID %%a
    echo 已终止进程 PID: %%a
)
echo 停止完成。
pause
