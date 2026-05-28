param(
    [string]$LogFile = ""
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if (-not $LogFile) {
    $LogFile = Join-Path $root "Build\Logs\release_android_build.log"
}

if (-not (Test-Path -LiteralPath $LogFile)) {
    throw "Build log not found: $LogFile"
}

$lines = Get-Content -LiteralPath $LogFile
$errors = $lines | Select-String -Pattern "error CS|Build failed|Exception:|UnityException|Gradle build failed|FAILURE: Build failed|Fatal Error|ProjectAlreadyOpen|another Unity instance" -CaseSensitive:$false
$warnings = $lines | Select-String -Pattern "warning CS|Build completed with a result of|BuildReport" -CaseSensitive:$false
$success = $lines | Select-String -Pattern "APK build succeeded|Build Successful|Exiting batchmode successfully" -CaseSensitive:$false

Write-Host "Build log: $LogFile"

if ($success) {
    Write-Host "Success markers:"
    $success | Select-Object -Last 5 | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
}

if ($warnings) {
    Write-Host ""
    Write-Host "Warnings:"
    $warnings | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
}

if ($errors) {
    Write-Host ""
    Write-Host "Errors:"
    $errors | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
    exit 1
}

Write-Host ""
Write-Host "No build errors found."
