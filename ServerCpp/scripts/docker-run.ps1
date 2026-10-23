<#
.SYNOPSIS
Starts the UnityRTS C++ Server container stack using Docker Compose.
#>
param(
    [ValidateSet("fullstack", "standalone")]
    [string]$Mode = "fullstack"
)

$RootDir = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $RootDir

@("logs", "data", "backups") | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
}

if (-not (Test-Path ".env")) {
    Write-Host "==> Creating .env from config/server.env.example..." -ForegroundColor Yellow
    Copy-Item "config\server.env.example" ".env"
}

if ($Mode -eq "standalone") {
    Write-Host "==> Starting UnityRTS C++ Server in standalone (zero-dependency) mode..." -ForegroundColor Cyan
    docker compose -f compose.standalone.yaml up -d --build
} else {
    Write-Host "==> Starting UnityRTS C++ Server with MySQL and Redis..." -ForegroundColor Cyan
    docker compose -f compose.yaml up -d --build
}

Start-Sleep -Seconds 3
docker compose ps
