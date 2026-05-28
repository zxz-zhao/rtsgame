param(
    [string]$UnityExe = "",
    [string]$ProjectPath = "",
    [string]$LogFile = ""
)

$ErrorActionPreference = "Stop"

if (-not $ProjectPath) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

if (-not $LogFile) {
    $LogFile = Join-Path $ProjectPath "Build\Logs\release_android_build.log"
}

if (-not $UnityExe) {
    $candidates = @(
        "E:\Tuanjie Hub\Editor\2022.3.62f3c1\Editor\Unity.exe",
        "E:\Unity Hub\Editor\2022.3.62f3c1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
    )

    $UnityExe = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $UnityExe -or -not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity executable not found. Pass -UnityExe with the full path to Unity.exe."
}

$openUnity = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and ($_.Path -ieq $UnityExe)
}
if ($openUnity) {
    $ids = ($openUnity | Select-Object -ExpandProperty Id) -join ", "
    throw "Unity is already running (PID: $ids). Close the editor for this project before running batchmode build."
}

$logDir = Split-Path -Parent $LogFile
if ($logDir) {
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
}

$apkPath = Join-Path $ProjectPath "Build\Android\UnityRTS.apk"
Write-Host "Building Android release APK..."
Write-Host "Unity: $UnityExe"
Write-Host "Project: $ProjectPath"
Write-Host "Log: $LogFile"

$args = @(
    "-batchmode",
    "-quit",
    "-nographics",
    "-projectPath", $ProjectPath,
    "-executeMethod", "AndroidBuildSetup.BuildAndroid",
    "-logFile", $LogFile
)

$proc = Start-Process -FilePath $UnityExe -ArgumentList $args -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $LogFile) {
        Get-Content -LiteralPath $LogFile -Tail 120
    }
    throw "Unity build failed with exit code $($proc.ExitCode)."
}

if (-not (Test-Path -LiteralPath $apkPath)) {
    throw "Unity finished successfully, but APK was not found at $apkPath."
}

$apk = Get-Item -LiteralPath $apkPath
Write-Host ("Build complete: {0} MB  {1}" -f ([math]::Round($apk.Length / 1MB, 1)), $apk.FullName)
