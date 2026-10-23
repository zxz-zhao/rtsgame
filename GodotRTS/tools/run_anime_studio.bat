@echo off
chcp 65001 >nul
title Anime Studio 3D - 次世代 3D 动漫漫剧一键制片厂
echo ========================================================
echo   Anime Studio 3D - 电影级 3D 动漫漫剧一键生成系统
echo   服务地址: http://127.0.0.1:8088
echo   Dify 插件端点: http://host.docker.internal:8088/openapi.json
echo ========================================================
echo.

start http://127.0.0.1:8088
"E:\python13\python.exe" server.py
pause
