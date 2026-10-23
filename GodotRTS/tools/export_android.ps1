param(
    [ValidateSet("debug", "release")]
    [string]$Configuration = "debug",
    [string]$GodotExe = "G:\soft\godot\Godot_v4.6.3-stable_mono_win64\Godot_v4.6.3-stable_mono_win64_console.exe"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$env:ANDROID_SDK_ROOT = "F:\AndroidSDK"
$env:ANDROID_HOME = "F:\AndroidSDK"
$env:JAVA_HOME = "C:\Program Files\Java\jdk-17"

$OutputDir = Join-Path $ProjectRoot "Build\Android"
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$ApkName = if ($Configuration -eq "debug") { "GodotRTS-debug.apk" } else { "GodotRTS-release.apk" }
$OutputPath = Join-Path $OutputDir $ApkName

Write-Host "Verifying .NET build..."
& dotnet build (Join-Path $ProjectRoot "GodotRTS.csproj")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed"
}

Write-Host "Running Godot export ($Configuration) to $OutputPath..."
$flag = if ($Configuration -eq "debug") { "--export-debug" } else { "--export-release" }

& $GodotExe --headless --verbose --path $ProjectRoot --install-android-build-template $flag "Android" $OutputPath
if ($LASTEXITCODE -ne 0) {
    throw "Godot export failed with exit code $LASTEXITCODE"
}

Write-Host "Export successfully completed! Output: $OutputPath"
