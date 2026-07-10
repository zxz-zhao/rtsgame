param(
    [string]$Preset = "windows-nmake-release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ToolPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installPath = & $vswhere -latest -products * -property installationPath
        if ($installPath) {
            $candidate = Join-Path $installPath "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\$Name.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "$Name is required to package the native server."
}

function Resolve-MySqlRuntimeDll {
    $envCandidate = $env:MYSQL_CLIENT_RUNTIME_DLL
    if ($envCandidate -and (Test-Path $envCandidate)) {
        return (Resolve-Path $envCandidate).Path
    }

    $candidates = @(
        "C:\Program Files\MySQL\MySQL Server 8.0\lib\libmysql.dll",
        "C:\Program Files\MySQL\MySQL Router 8.0\lib\libmysql.dll",
        "C:\Program Files\MariaDB 11.4\lib\libmariadb.dll",
        "C:\Program Files\MariaDB 11.3\lib\libmariadb.dll"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Resolve-VsDevCmd {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe is required to locate VsDevCmd.bat."
    }

    $installPath = & $vswhere -latest -products * -property installationPath
    if (-not $installPath) {
        throw "Visual Studio installation was not found."
    }

    $candidate = Join-Path $installPath "Common7\Tools\VsDevCmd.bat"
    if (-not (Test-Path $candidate)) {
        throw "VsDevCmd.bat was not found under $installPath"
    }

    return $candidate
}

function Invoke-InVsDevShell {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VsDevCmd,
        [Parameter(Mandatory = $true)]
        [string]$Command
    )

    $cmdLine = "`"$VsDevCmd`" -arch=x64 >nul && $Command"
    cmd /c $cmdLine
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed inside VsDevCmd: $Command"
    }
}

$projectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $projectDir

$cmake = Resolve-ToolPath -Name "cmake"
$cpack = Resolve-ToolPath -Name "cpack"
$mysqlDll = Resolve-MySqlRuntimeDll
$vsDevCmd = Resolve-VsDevCmd

$configureArgs = @("--preset", $Preset)
if ($mysqlDll) {
    $normalizedMysqlDll = $mysqlDll.Replace('\', '/')
    $configureArgs += "-DMYSQL_CLIENT_RUNTIME_DLL=`"$normalizedMysqlDll`""
    Write-Host "Including MySQL runtime DLL: $mysqlDll"
} else {
    Write-Warning "MySQL runtime DLL not found automatically. Set MYSQL_CLIENT_RUNTIME_DLL to include it in the package."
}

Invoke-InVsDevShell -VsDevCmd $vsDevCmd -Command "`"$cmake`" $($configureArgs -join ' ')"
Invoke-InVsDevShell -VsDevCmd $vsDevCmd -Command "`"$cmake`" --build --preset $Preset"

$buildDir = Join-Path $projectDir "build\$Preset"
$cpackConfig = Join-Path $buildDir "CPackConfig.cmake"

if (-not (Test-Path $cpackConfig)) {
    throw "CPackConfig.cmake was not generated at $cpackConfig"
}

if ($Preset -eq "windows-msvc-release") {
    Invoke-InVsDevShell -VsDevCmd $vsDevCmd -Command "`"$cpack`" --config `"$cpackConfig`" -C Release"
} else {
    Invoke-InVsDevShell -VsDevCmd $vsDevCmd -Command "`"$cpack`" --config `"$cpackConfig`""
}

Write-Host "Windows package completed. Check dist/ under $projectDir."
