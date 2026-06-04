@echo off
setlocal

cd /d "%~dp0"

where powershell.exe >nul 2>nul
if errorlevel 1 (
    echo PowerShell was not found on this Windows installation.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\InstallOpenClawWindows.ps1" %*
set "OPENCLAW_INSTALL_EXIT=%ERRORLEVEL%"

echo.
if "%OPENCLAW_INSTALL_EXIT%"=="0" (
    echo OpenClaw installer finished.
) else (
    echo OpenClaw installer failed with exit code %OPENCLAW_INSTALL_EXIT%.
)

if not defined OPENCLAW_NO_PAUSE pause
exit /b %OPENCLAW_INSTALL_EXIT%
