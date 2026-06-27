param(
    [string]$Remote = "origin",
    [string]$Branch = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$appDir = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $appDir
New-Item -ItemType Directory -Force -Path "logs" | Out-Null

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git is required for hot-update.ps1."
}

if (-not (Get-Command pm2 -ErrorAction SilentlyContinue)) {
    throw "pm2 is required. Install it first: npm install -g pm2"
}

if (-not (Test-Path ".env")) {
    throw "Server/.env is missing. Copy .env.example to .env and fill production values first."
}

if ([string]::IsNullOrWhiteSpace($Branch)) {
    $Branch = (git rev-parse --abbrev-ref HEAD).Trim()
}

if ($Branch -eq "HEAD") {
    throw "Repository is in detached HEAD. Pass -Branch <branch-name> and retry."
}

$oldRev = (git rev-parse --short HEAD).Trim()
git fetch $Remote $Branch
git pull --ff-only $Remote $Branch
$newRev = (git rev-parse --short HEAD).Trim()

npm ci --omit=dev
npm run check

$env:BUILD_REVISION = $newRev
pm2 startOrReload ecosystem.config.cjs --env production --update-env
npm run health
pm2 save

Write-Host "Hot update complete: $oldRev -> $newRev"
