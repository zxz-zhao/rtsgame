@echo off
chcp 65001 >nul
title Dify RTS Architecture - Telegram Bot
cd /d %~dp0
echo ========================================================
echo   Telegram Bot - Dify RTS Architecture Assistant
echo ========================================================
E:\python13\python.exe telegram_bot.py
pause
