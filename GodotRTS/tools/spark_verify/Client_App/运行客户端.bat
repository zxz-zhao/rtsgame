@echo off
chcp 65001 >nul
title SparkVerify 极验网络验证客户端
cd /d "%~dp0"
"E:\python13\python.exe" main_client.py
