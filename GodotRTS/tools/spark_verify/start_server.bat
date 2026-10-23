@echo off
chcp 65001 >nul
title SparkVerify 极验网络验证服务端
echo ==============================================================
echo           SparkVerify 极验网络验证系统 (生产启动脚本)
echo ==============================================================
echo [INFO] 正在启动 FastAPI 异步核心服务...
echo [INFO] 监听地址: http://127.0.0.1:8989
echo [INFO] 管理控制台: http://127.0.0.1:8989/admin
echo [INFO] 默认管理账号: admin  密码: admin123
echo ==============================================================
cd /d "%~dp0backend"
"E:\python13\python.exe" -m uvicorn main:app --host 0.0.0.0 --port 8989
pause
