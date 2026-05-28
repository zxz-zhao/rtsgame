#requires -Version 5.1
param(
    [switch]$FullPipeline,
    [switch]$SkipServer,
    [switch]$BuildOnly
)
<#
.SYNOPSIS
    Ensure backend, Unity Android APK build, adb install, launch (MuMu).

.PARAMETER FullPipeline
    BuildAll.RunAll (slower).

.PARAMETER SkipServer
    Do not start or check Server.

.PARAMETER BuildOnly
    Unity Android build only; skip adb (faster CI / no emulator).

.EXAMPLE
    .\PackInstallTest.ps1
    .\PackInstallTest.ps1 -FullPipeline
    .\PackInstallTest.ps1 -SkipServer
    .\PackInstallTest.ps1 -BuildOnly -SkipServer
#>
$ErrorActionPreference = 'Stop'
chcp 65001 | Out-Null

$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }

$Common = Join-Path $Root 'Tools\RtsDeployCommon.ps1'
if (-not (Test-Path $Common)) { Write-Error ('Missing common script: ' + $Common) }
. $Common

$Unity = Rts_GetDefaultUnityPath
if (-not (Test-Path $Unity)) {
    Write-Error ('Unity not found: ' + $Unity + ' Set UNITY_EDITOR.')
}

$Adb = $null
if (-not $BuildOnly) {
    $Adb = Rts_FindAdb
    if (-not $Adb) {
        Write-Error 'adb not found. Install platform-tools or set ADB_PATH. (Or use -BuildOnly to skip install.)'
    }
}

$Apk      = Join-Path $Root 'Build\Android\UnityRTS.apk'
$BuildDir = Join-Path $Root 'Build'
if (-not (Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir | Out-Null }
$Log      = Join-Path $BuildDir 'unity_pack_install.log'

if (-not $SkipServer) {
    Write-Host '=== [1/4] Backend (8080) ===' -ForegroundColor Cyan
    [void](Rts_EnsureBackendServer -ProjectRoot $Root)
} else {
    Write-Host '=== [1/4] Backend skipped (-SkipServer) ===' -ForegroundColor DarkGray
}

$method = if ($FullPipeline) { 'BuildAll.RunAll' } else { 'AndroidBuildSetup.BuildAndroid' }
Write-Host ('=== [2/4] Unity: ' + $method + ' ===') -ForegroundColor Cyan
$unityArgs = @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $Root,
    '-executeMethod', $method,
    '-logFile', $Log
)
$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -Wait -NoNewWindow
if ($proc.ExitCode -ne 0) {
    Write-Host ('Unity exit code: ' + $proc.ExitCode) -ForegroundColor Red
    if (Test-Path $Log) { Get-Content $Log -Tail 50 }
    exit $proc.ExitCode
}
if (-not (Test-Path $Apk)) {
    Write-Error ('APK not built: ' + $Apk + ' See ' + $Log)
}
$i = Get-Item $Apk
Write-Host ('  APK: ' + [math]::Round($i.Length / 1MB, 1) + ' MB  ' + $i.LastWriteTime) -ForegroundColor Green

if ($BuildOnly) {
    Write-Host '=== [3-4/4] Skipped (-BuildOnly, no adb) ===' -ForegroundColor DarkGray
    Write-Host ('Build-only OK. Log: ' + $Log) -ForegroundColor Green
    exit 0
}

Write-Host '=== [3/4] adb connect + install ===' -ForegroundColor Cyan
Rts_ConnectAdbEmulators -AdbExe $Adb
$serial = Rts_PickAdbSerial -AdbExe $Adb
if (-not $serial) {
    $devs = (& $Adb devices 2>&1 | Out-String)
    Write-Error ('No adb device. Start MuMu.' + [Environment]::NewLine + $devs)
}
Write-Host ('  device: ' + $serial) -ForegroundColor Gray

Write-Host '=== [4/4] Launch ===' -ForegroundColor Cyan
if (-not (Rts_InstallAndLaunch -AdbExe $Adb -Serial $serial -ApkPath $Apk)) { exit 1 }
Write-Host ('Done. Log: ' + $Log) -ForegroundColor Green
