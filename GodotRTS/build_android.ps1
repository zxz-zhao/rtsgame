# Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$ProjectDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$BuildDir = Join-Path $ProjectDir "Build\Android"
$ApkPath = Join-Path $BuildDir "GodotRTS.apk"
$GodotExe = "G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Godot RTS Android Build Script" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Compile .NET project
Write-Host "[1/3] Compiling C# project..." -ForegroundColor Yellow
dotnet build (Join-Path $ProjectDir "GodotRTS.csproj")
if ($LASTEXITCODE -ne 0) {
    Write-Error "C# Build Failed!"
    exit $LASTEXITCODE
}
Write-Host "  C# Build Success!" -ForegroundColor Green

# 2. Prepare output directory
if (-not (Test-Path $BuildDir)) {
    New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
}
if (Test-Path $ApkPath) {
    Remove-Item $ApkPath -Force
}

# 3. Run Godot export
Write-Host "`n[2/3] Exporting Android APK..." -ForegroundColor Yellow
$GodotArgs = @(
    "--headless",
    "--path", $ProjectDir,
    "--export-debug", "Android",
    $ApkPath,
    "--verbose"
)

Write-Host "  Running: $GodotExe $($GodotArgs -join ' ')" -ForegroundColor Gray
$proc = Start-Process -FilePath $GodotExe -ArgumentList $GodotArgs -PassThru -Wait -NoNewWindow
$exitCode = $proc.ExitCode
Write-Host "  Godot Export Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

# 4. Verify APK
Write-Host "`n[3/3] Verifying generated APK..." -ForegroundColor Yellow
if (Test-Path $ApkPath) {
    $apk = Get-Item $ApkPath
    $sizeMB = [math]::Round($apk.Length / 1MB, 2)
    Write-Host "  [OK] Export succeeded!" -ForegroundColor Green
    Write-Host "  APK Path: $ApkPath" -ForegroundColor Green
    Write-Host "  APK Size: $sizeMB MB" -ForegroundColor Green
    Write-Host "  Modified Time: $($apk.LastWriteTime)" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] APK file not found!" -ForegroundColor Red
    exit 1
}
