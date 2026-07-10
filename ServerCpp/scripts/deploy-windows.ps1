param(
    [string]$InstallDir = "C:\UnityRTS\ServerCpp",
    [string]$TaskName = "UnityRTSGuardian"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sourceDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$envExampleSource = Join-Path $sourceDir ".env.example"
if (-not (Test-Path $envExampleSource)) {
    $envExampleSource = Join-Path $sourceDir "config\server.env.example"
}

if (-not (Test-Path (Join-Path $sourceDir "bin"))) {
    throw "Packaged layout not found. Expected directory: $(Join-Path $sourceDir 'bin')"
}

if (-not (Test-Path (Join-Path $sourceDir ".env")) -and -not (Test-Path (Join-Path $InstallDir ".env"))) {
    throw "No .env found. Create one in the package root before deployment."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "bin") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "scripts") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "logs") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "data") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $InstallDir "backups") | Out-Null

Copy-Item -Path (Join-Path $sourceDir "bin\*") -Destination (Join-Path $InstallDir "bin") -Recurse -Force
Copy-Item -Path (Join-Path $sourceDir "scripts\*") -Destination (Join-Path $InstallDir "scripts") -Recurse -Force
Copy-Item -Path (Join-Path $sourceDir "README.md") -Destination (Join-Path $InstallDir "README.md") -Force
Copy-Item -Path (Join-Path $sourceDir "DEPLOY.md") -Destination (Join-Path $InstallDir "DEPLOY.md") -Force
Copy-Item -Path (Join-Path $sourceDir "MIGRATION.md") -Destination (Join-Path $InstallDir "MIGRATION.md") -Force
Copy-Item -Path $envExampleSource -Destination (Join-Path $InstallDir ".env.example") -Force

foreach ($dirName in @("logs", "data", "backups")) {
    $from = Join-Path $sourceDir $dirName
    $to = Join-Path $InstallDir $dirName
    if (Test-Path $from) {
        Copy-Item -Path (Join-Path $from "*") -Destination $to -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$packageEnv = Join-Path $sourceDir ".env"
$targetEnv = Join-Path $InstallDir ".env"
if ((Test-Path $packageEnv) -and -not (Test-Path $targetEnv)) {
    Copy-Item -Path $packageEnv -Destination $targetEnv -Force
}

& (Join-Path $InstallDir "scripts\install-windows-service.ps1") -InstallDir $InstallDir -TaskName $TaskName

Write-Host "Deployment completed."
Write-Host "Task name: $TaskName"
Write-Host "Install dir: $InstallDir"
