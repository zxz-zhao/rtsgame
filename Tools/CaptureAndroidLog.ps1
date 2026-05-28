param(
    [string]$Serial = "",
    [int]$Tail = 500,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
. (Join-Path $root "Tools\RtsDeployCommon.ps1")

$adb = Rts_FindAdb
if (-not $adb) {
    throw "adb not found. Set ADB_PATH or install Android platform-tools."
}

if (-not $Serial) {
    $Serial = Rts_PickAdbSerial -AdbExe $adb
}

if (-not $Serial) {
    & $adb devices
    throw "No Android device or emulator detected."
}

if (-not $Output) {
    $Output = Join-Path $root "Build\Android\unityrts_logcat.txt"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Output) | Out-Null

$patterns = "Unity|AndroidRuntime|FATAL EXCEPTION|Exception|Error|com.mystudio.unityRTS|Network error|NavMesh"
$log = & $adb -s $Serial logcat -d -t $Tail 2>&1 | Select-String -Pattern $patterns
$log | ForEach-Object { $_.Line } | Set-Content -LiteralPath $Output -Encoding UTF8

Write-Host "Device: $Serial"
Write-Host "Log: $Output"
Write-Host "Matched lines: $($log.Count)"

if ($log) {
    $log | Select-Object -Last 80 | ForEach-Object { Write-Host $_.Line }
}
