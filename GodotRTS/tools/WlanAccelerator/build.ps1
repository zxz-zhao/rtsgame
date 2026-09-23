# Build script for WlanAccelerator C++ application
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BinDir = Join-Path $ScriptDir "bin"
if (!(Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir | Out-Null
}

$Compiler = "C:\Strawberry\c\bin\g++.exe"
$OutputExe = Join-Path $BinDir "WlanAccelerator.exe"

$SrcFiles = @(
    (Join-Path $ScriptDir "src\main.cpp"),
    (Join-Path $ScriptDir "src\network_monitor.cpp"),
    (Join-Path $ScriptDir "src\wlan_controller.cpp"),
    (Join-Path $ScriptDir "src\task_installer.cpp"),
    (Join-Path $ScriptDir "src\logger.cpp")
)

Write-Host "Compiling C++ WlanAccelerator.exe..." -ForegroundColor Cyan

$CompileArgs = @(
    "-std=c++11",
    "-O2",
    "-I$($ScriptDir)\src"
) + $SrcFiles + @(
    "-o", $OutputExe,
    "-lws2_32",
    "-lshlwapi"
)

& $Compiler $CompileArgs

if (Test-Path $OutputExe) {
    Write-Host "Compilation successful! Output: $OutputExe" -ForegroundColor Green
} else {
    Write-Host "Compilation failed! Binary not found." -ForegroundColor Red
    exit 1
}
