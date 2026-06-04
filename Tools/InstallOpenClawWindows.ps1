param(
    [string]$OpenClawPackage = "openclaw@latest",
    [string]$NodeWingetId = "OpenJS.NodeJS.LTS",
    [string]$LogFile = "",
    [switch]$SkipOnboard,
    [switch]$NoDaemon,
    [switch]$ForceNodeInstall
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)

    Write-Host "OK  $Message" -ForegroundColor Green
}

function Refresh-Path {
    $pathParts = @(
        [Environment]::GetEnvironmentVariable("Path", "Machine"),
        [Environment]::GetEnvironmentVariable("Path", "User"),
        $env:Path,
        (Join-Path $env:ProgramFiles "nodejs"),
        (Join-Path $env:APPDATA "npm")
    )

    $env:Path = (($pathParts -join ";").Split(";") |
        Where-Object { $_ -and $_.Trim() } |
        Select-Object -Unique) -join ";"
}

function Get-Tool {
    param([string[]]$Names)

    foreach ($name in $Names) {
        $tool = Get-Command $name -ErrorAction SilentlyContinue
        if ($tool) {
            return $tool
        }
    }

    return $null
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        if (-not $FailureMessage) {
            $FailureMessage = "$FilePath failed with exit code $LASTEXITCODE."
        }
        throw $FailureMessage
    }
}

function Get-NodeMajorVersion {
    Refresh-Path
    $node = Get-Tool @("node.exe", "node")
    if (-not $node) {
        return 0
    }

    $version = & $node.Source "--version" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $version) {
        return 0
    }

    if ($version -match "^v?(\d+)\.") {
        return [int]$Matches[1]
    }

    return 0
}

function Ensure-Node {
    $major = Get-NodeMajorVersion
    if ($major -ge 22 -and -not $ForceNodeInstall) {
        Write-Ok "Node.js major version $major detected."
        return
    }

    Write-Step "Installing Node.js LTS with winget"
    $winget = Get-Tool @("winget.exe", "winget")
    if (-not $winget) {
        throw "winget was not found. Install Node.js 22+ from https://nodejs.org/, then rerun this script."
    }

    $args = @(
        "install",
        "--id", $NodeWingetId,
        "-e",
        "--accept-source-agreements",
        "--accept-package-agreements"
    )

    Invoke-Tool -FilePath $winget.Source -Arguments $args -FailureMessage "Node.js installation failed."
    Refresh-Path

    $major = Get-NodeMajorVersion
    if ($major -lt 22) {
        throw "Node.js 22+ is still not available in PATH after install. Open a new terminal and rerun this script."
    }

    Write-Ok "Node.js major version $major is ready."
}

function Ensure-Npm {
    Refresh-Path
    $npm = Get-Tool @("npm.cmd", "npm.exe", "npm")
    if (-not $npm) {
        throw "npm was not found. Reinstall Node.js 22+ and rerun this script."
    }

    $version = & $npm.Source "--version"
    if ($LASTEXITCODE -ne 0) {
        throw "npm is installed but did not run successfully."
    }

    Write-Ok "npm $version detected."
    return $npm.Source
}

function Install-OpenClaw {
    param([string]$NpmPath)

    Write-Step "Installing $OpenClawPackage globally"
    Invoke-Tool -FilePath $NpmPath -Arguments @("install", "-g", $OpenClawPackage) -FailureMessage "OpenClaw npm install failed."
    Refresh-Path

    $openclaw = Get-Tool @("openclaw.cmd", "openclaw.exe", "openclaw")
    if (-not $openclaw) {
        throw "OpenClaw was installed, but openclaw was not found in PATH. Open a new terminal and run: openclaw --version"
    }

    $version = & $openclaw.Source "--version"
    if ($LASTEXITCODE -ne 0) {
        throw "OpenClaw was installed, but version verification failed."
    }

    Write-Ok "OpenClaw installed: $version"
    return $openclaw.Source
}

function Run-Onboarding {
    param([string]$OpenClawPath)

    if ($SkipOnboard) {
        Write-Host ""
        Write-Host "Skipping onboarding. Run it later with:" -ForegroundColor Yellow
        Write-Host "  openclaw onboard --install-daemon"
        return
    }

    Write-Step "Starting OpenClaw onboarding"
    Write-Host "You will be asked for an LLM provider/API key or local model settings."
    Write-Host "For safer local use, keep the gateway bound to 127.0.0.1 when prompted/configuring."

    $args = @("onboard")
    if (-not $NoDaemon) {
        $args += "--install-daemon"
    }

    Invoke-Tool -FilePath $OpenClawPath -Arguments $args -FailureMessage "OpenClaw onboarding failed."
}

if (-not $LogFile) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $LogFile = Join-Path $env:TEMP "openclaw_windows_install_$timestamp.log"
}

$logDir = Split-Path -Parent $LogFile
if ($logDir) {
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
}

$transcriptStarted = $false
try {
    Start-Transcript -Path $LogFile -Force | Out-Null
    $transcriptStarted = $true
}
catch {
    Write-Warning "Could not start transcript logging: $($_.Exception.Message)"
}

try {
    Write-Host "OpenClaw Windows one-click installer"
    Write-Host "Log: $LogFile"

    Write-Step "Checking prerequisites"
    Ensure-Node
    $npmPath = Ensure-Npm

    $openClawPath = Install-OpenClaw -NpmPath $npmPath
    Run-Onboarding -OpenClawPath $openClawPath

    Write-Step "Done"
    Write-Host "Useful commands:"
    Write-Host "  openclaw --version"
    Write-Host "  openclaw status"
    Write-Host "  openclaw chat `"Hello, what can you do?`""
    Write-Host ""
    Write-Host "Security note: OpenClaw can access local files and shell tools. Start with the narrowest permissions you need."
}
finally {
    if ($transcriptStarted) {
        Stop-Transcript | Out-Null
    }
}
