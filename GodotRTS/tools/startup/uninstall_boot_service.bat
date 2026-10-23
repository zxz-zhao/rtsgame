@echo off
setlocal
echo ========================================================
echo   Rocket.Chat & RTS Multi-Agent Boot Service Uninstaller
echo ========================================================

set STARTUP_FOLDER=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
set DEST_VBS=%STARTUP_FOLDER%\StartRocketChatServices.vbs

if exist "%DEST_VBS%" (
    del /f /q "%DEST_VBS%"
    echo [OK] Removed %DEST_VBS%
)

schtasks /delete /tn "RocketChatBootSupervisor" /f >nul 2>&1
if %ERRORLEVEL% equ 0 (
    echo [OK] Deleted Scheduled Task 'RocketChatBootSupervisor'
)

echo [SUCCESS] Auto-boot service has been uninstalled.
pause
