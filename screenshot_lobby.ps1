#requires -Version 5.1
<#
.SYNOPSIS
    Launch game, auto guest login (1920x1080 landscape MuMu), capture lobby to LobbyAutoCap.png.

.NOTES
    LoginPanel layout from SceneBuilder: GuestLoginButton anchor (0.27,0.155), size 168x46 on panel 460x390 at canvas center (0,-20).
#>
param([string]$Serial = "127.0.0.1:7555")

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }

$Common = Join-Path $Root 'Tools\RtsDeployCommon.ps1'
if (Test-Path $Common) { . $Common; $adb = Rts_FindAdb } else { $adb = $null }
if (-not $adb -or -not (Test-Path $adb)) { $adb = 'F:\AndroidSdk\platform-tools\adb.exe' }
if (-not (Test-Path $adb)) { Write-Error 'adb not found.' }

$pkg = 'com.mystudio.unityRTS'
$out = Join-Path $Root 'LobbyAutoCap.png'

& $adb connect $Serial 2>&1 | Out-Null
Start-Sleep -Milliseconds 500
$sizeRaw = & $adb -s $Serial shell wm size
Write-Host "Screen: $sizeRaw"

& $adb -s $Serial shell am force-stop $pkg
Start-Sleep -Seconds 1
& $adb -s $Serial shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
Start-Sleep -Seconds 5

# 1920x1080: guest ~ (854,385), guest confirm ~ (872,423) — 若未进大厅，请用手动进大厅后运行 CaptureLobbyScreen.ps1
& $adb -s $Serial shell input tap 854 385
Start-Sleep -Seconds 2
& $adb -s $Serial shell input tap 872 423
Start-Sleep -Seconds 14

& $adb -s $Serial shell screencap -p /sdcard/lobby_auto.png
& $adb -s $Serial pull /sdcard/lobby_auto.png $out
Write-Host "Saved: $out"
