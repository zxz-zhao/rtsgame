@echo off
chcp 65001 >nul
title 测试企业微信发送
cd /d "%~dp0"
echo 正在执行测试发送（附带当前屏幕截图）...
E:\python13\python.exe send_report.py
pause
