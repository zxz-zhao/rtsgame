@echo off
chcp 65001 >nul
title SparkVerify 极验网络验证客户端
cd /d "%~dp0Client_App"
"E:\python13\python.exe" main_client.py
