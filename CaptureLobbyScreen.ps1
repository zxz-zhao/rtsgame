#requires -Version 5.1
<#
.SYNOPSIS
    Pull current emulator screen to PNG (no auto-clicks). Use after you enter lobby manually.

.EXAMPLE
    .\CaptureLobbyScreen.ps1
    .\CaptureLobbyScreen.ps1 -Serial "127.0.0.1:16384"
#>
param([string]$Serial = "127.0.0.1:7555")

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }

$Common = Join-Path $Root 'Tools\RtsDeployCommon.ps1'
if (Test-Path $Common) { . $Common; $adb = Rts_FindAdb } else { $adb = $null }
if (-not $adb -or -not (Test-Path $adb)) { $adb = 'F:\AndroidSdk\platform-tools\adb.exe' }
if (-not (Test-Path $adb)) { Write-Error 'adb not found.' }

$out = Join-Path $Root 'LobbyScreenNow.png'
& $adb connect $Serial 2>&1 | Out-Null
Start-Sleep -Milliseconds 400
& $adb -s $Serial shell screencap -p /sdcard/lobby_cap_now.png
& $adb -s $Serial pull /sdcard/lobby_cap_now.png $out
Write-Host ('Saved: ' + $out) -ForegroundColor Green
