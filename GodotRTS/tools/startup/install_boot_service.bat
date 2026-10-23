@echo off
setlocal
echo ========================================================
echo   Rocket.Chat & RTS Multi-Agent Boot Service Installer
echo ========================================================

set SCRIPT_DIR=%~dp0
set VBS_PATH=%SCRIPT_DIR%start_rocketchat_services.vbs
set STARTUP_FOLDER=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
set DEST_VBS=%STARTUP_FOLDER%\StartRocketChatServices.vbs

echo [1/2] Copying silent launcher to Windows Startup folder...
copy /Y "%VBS_PATH%" "%DEST_VBS%" >nul
if %ERRORLEVEL% equ 0 (
    echo   [OK] Placed %DEST_VBS%
) else (
    echo   [WARN] Could not copy to startup folder.
)

echo [2/2] Registering Windows Scheduled Task 'RocketChatBootSupervisor'...
schtasks /create /f /tn "RocketChatBootSupervisor" /tr "wscript.exe \"%VBS_PATH%\"" /sc onlogon /rl highest >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo   [OK] Task 'RocketChatBootSupervisor' registered successfully (Trigger: OnLogon, Highest Privileges)
) else (
    echo   [INFO] User Startup shortcut is active.
)

echo.
echo ========================================================
echo [SUCCESS] Rocket.Chat, 8.8.1 Auto-Patch and AI Bridge
echo           are now configured to start automatically on boot!
echo ========================================================
pause
