@echo off
chcp 65001 >nul
title VoceChat - Dify Multi-Agent Bridge (Port 5006)

echo ======================================================================
echo 🚀 启动 VoceChat ^<^-^> Dify 超轻量级研发与审批协同网关 (Port 5006)
echo 💡 VoceChat 内存仅 12MB，相比 Rocket.Chat (近 1GB) 节省 98%% 物理资源！
echo ======================================================================

set VOCE_API_URL=http://45.205.31.65:3009
set VOCE_BOT_KEY=ec5a07a119de44ccc685337ac00ea90abeda8e3902c7c4ed859d84f003ebe8527b22756964223a332c226e6f6e6365223a226a65594748494932316d6f41414141416765366e4b30647a52424574555a7031227d
set VOCE_DEFAULT_GROUP_ID=1
set BRIDGE_PORT=5006

set PYTHON_EXE=E:\python13\python.exe
if not exist "%PYTHON_EXE%" set PYTHON_EXE=python

"%PYTHON_EXE%" "%~dp0vocechat_dify_bridge.py"
pause
