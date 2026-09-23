@echo off
chcp 65001 >nul
title 企业微信 Webhook 播报服务
cd /d "%~dp0"
echo ======================================================
echo 正在启动企业微信 Webhook 本地网关服务...
echo ======================================================
E:\python13\python.exe server.py
pause
