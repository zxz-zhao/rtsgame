param(
    [switch]$SkipStartupTask
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$appDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $appDir
New-Item -ItemType Directory -Force -Path "logs" | Out-Null

function Require-Command($Name, $Message) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw $Message
    }
}

function Set-EnvValue($Path, $Key, $Value) {
    $lines = Get-Content -LiteralPath $Path
    $pattern = "^$([regex]::Escape($Key))="
    $updated = $false
    $next = foreach ($line in $lines) {
        if ($line -match $pattern) {
            "$Key=$Value"
            $updated = $true
        } else {
            $line
        }
    }
    if (-not $updated) {
        $next += "$Key=$Value"
    }
    Set-Content -LiteralPath $Path -Value $next -Encoding UTF8
}

Require-Command "node" "Node.js LTS is required."
Require-Command "npm" "npm is required."
Require-Command "git" "git is required."

if (-not (Get-Command "pm2" -ErrorAction SilentlyContinue)) {
    Write-Host "pm2 was not found; installing it globally with npm."
    npm install -g pm2
}

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $secret = -join ($bytes | ForEach-Object { $_.ToString("x2") })
    Set-EnvValue ".env" "JWT_SECRET" $secret
    Set-EnvValue ".env" "NODE_ENV" "production"
    Write-Host "Created Server/.env."
    Write-Host "Edit MySQL settings and rerun this script."
    exit 0
}

npm ci --omit=dev
npm run check
pm2 startOrReload ecosystem.config.cjs --env production --update-env
npm run health
pm2 save

if (-not $SkipStartupTask) {
    $pm2 = (Get-Command "pm2.cmd" -ErrorAction SilentlyContinue)
    if (-not $pm2) { $pm2 = Get-Command "pm2" }
    $action = New-ScheduledTaskAction -Execute $pm2.Source -Argument "resurrect" -WorkingDirectory $appDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
    Register-ScheduledTask -TaskName "UnityRTS-PM2-Resurrect" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force | Out-Null
    Write-Host "Registered Windows startup task: UnityRTS-PM2-Resurrect."
}

Write-Host "Install complete. Open inbound TCP ports from .env: PORT and WS_PORT."
