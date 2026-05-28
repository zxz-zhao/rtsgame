param(
    [string]$ApkPath = "",
    [string]$Serial = "",
    [string]$PackageId = "com.mystudio.unityRTS",
    [string]$DBBackend = "json",
    [switch]$SkipServer,
    [switch]$Screenshot
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$common = Join-Path $root "Tools\RtsDeployCommon.ps1"
. $common

if (-not $ApkPath) {
    $ApkPath = Join-Path $root "Build\Android\UnityRTS.apk"
}

if (-not (Test-Path -LiteralPath $ApkPath)) {
    throw "APK not found: $ApkPath"
}

if (-not $SkipServer) {
    Write-Host "Ensuring backend server is running..."
    if (-not (Rts_EnsureBackendServer -ProjectRoot $root -DBBackend $DBBackend)) {
        Write-Warning "Backend server is not reachable. The app may show offline/network errors."
    }
}

$adb = Rts_FindAdb
if (-not $adb) {
    throw "adb not found. Set ADB_PATH or install Android platform-tools."
}

Rts_ConnectAdbEmulators -AdbExe $adb

if (-not $Serial) {
    $Serial = Rts_PickAdbSerial -AdbExe $adb
}

if (-not $Serial) {
    & $adb devices
    throw "No Android device or emulator detected."
}

Write-Host "Installing APK..."
Write-Host "ADB: $adb"
Write-Host "Device: $Serial"
Write-Host "APK: $ApkPath"

if (-not (Rts_InstallAndLaunch -AdbExe $adb -Serial $Serial -ApkPath $ApkPath -PackageId $PackageId)) {
    throw "Install failed."
}

Write-Host "App launched: $PackageId"

if ($Screenshot) {
    Start-Sleep -Seconds 8
    $remote = "/sdcard/unityrts_launch.png"
    $local = Join-Path $root "Build\Android\unityrts_launch.png"
    & $adb -s $Serial shell screencap -p $remote | Out-Null
    & $adb -s $Serial pull $remote $local | Out-Null
    Write-Host "Screenshot: $local"
}
