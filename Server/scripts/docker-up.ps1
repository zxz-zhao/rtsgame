Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$appDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $appDir

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required. Install Docker Desktop or Docker Engine first."
}

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $secret = -join ($bytes | ForEach-Object { $_.ToString("x2") })

    $lines = Get-Content -LiteralPath ".env"
    $lines = $lines | ForEach-Object {
        if ($_ -match "^JWT_SECRET=") { "JWT_SECRET=$secret" }
        else { $_ }
    }
    Set-Content -LiteralPath ".env" -Value $lines -Encoding UTF8

    Write-Host "Created Server/.env."
    Write-Host "Review MYSQL_PASSWORD and MYSQL_ROOT_PASSWORD if needed, then rerun this script."
    exit 0
}

docker compose -f compose.fullstack.yaml up -d --build
Write-Host "Docker full-stack deployment started."
Write-Host "Check health with: docker compose -f compose.fullstack.yaml ps"
