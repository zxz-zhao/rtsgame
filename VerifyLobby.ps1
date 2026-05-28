#requires -Version 5.1
<#
.SYNOPSIS
    Batch-regenerate Assets/Scenes/LobbyScene.unity only (no APK, no adb).

.DESCRIPTION
    Runs Unity -batchmode with executeMethod LobbySceneBuilder.BuildLobbyScene.
    Close the Unity editor for this project first, or use menu RTS -> ③ in editor.

.EXAMPLE
    .\VerifyLobby.ps1
#>
$ErrorActionPreference = 'Stop'
chcp 65001 | Out-Null

$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }

$Common = Join-Path $Root 'Tools\RtsDeployCommon.ps1'
if (-not (Test-Path $Common)) { Write-Error ('Missing: ' + $Common) }
. $Common

$Unity = Rts_GetDefaultUnityPath
if (-not (Test-Path $Unity)) {
    Write-Error ('Unity not found: ' + $Unity + ' Set UNITY_EDITOR.')
}

$BuildDir = Join-Path $Root 'Build'
if (-not (Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
$Log = Join-Path $BuildDir 'verify_lobby.log'

Write-Host '=== Lobby only: LobbySceneBuilder.BuildLobbyScene ===' -ForegroundColor Cyan
Write-Host ('Unity: ' + $Unity) -ForegroundColor Gray
Write-Host ('Log:   ' + $Log) -ForegroundColor Gray

$unityArgs = @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $Root,
    '-executeMethod', 'LobbySceneBuilder.BuildLobbyScene',
    '-logFile', $Log
)
$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -Wait -NoNewWindow
if ($proc.ExitCode -ne 0) {
    Write-Host ('Unity exit code: ' + $proc.ExitCode) -ForegroundColor Red
    if (Test-Path $Log) { Get-Content $Log -Tail 80 }
    exit $proc.ExitCode
}

$scene = Join-Path $Root 'Assets\Scenes\LobbyScene.unity'
if (-not (Test-Path $scene)) {
    Write-Error ('Lobby scene not saved: ' + $scene + ' See ' + $Log)
}

$i = Get-Item $scene
Write-Host ('OK: ' + $scene) -ForegroundColor Green
Write-Host ('  Size: ' + [math]::Round($i.Length / 1KB, 1) + ' KB  ' + $i.LastWriteTime) -ForegroundColor Green
Write-Host ('Log: ' + $Log) -ForegroundColor DarkGray
exit 0
