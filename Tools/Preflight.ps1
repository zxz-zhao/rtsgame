param(
    [switch]$SkipServerCheck
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Assert-Path {
    param([string]$RelativePath)

    $path = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing required path: $RelativePath"
    }
    Write-Host "OK  $RelativePath"
}

Write-Host "UnityRTS preflight"
Write-Host "Project: $root"

Assert-Path "Assets\Scenes\LoginScene.unity"
Assert-Path "Assets\Scenes\LobbyScene.unity"
Assert-Path "Assets\Scenes\GameScene.unity"
Assert-Path "Assets\Editor\AndroidBuildSetup.cs"
Assert-Path "Packages\manifest.json"
Assert-Path "ProjectSettings\ProjectVersion.txt"
Assert-Path "Server\server.js"
Assert-Path "Server\package.json"
Assert-Path "Server\.env.example"
Assert-Path "Tools\BuildReleaseAndroid.ps1"
Assert-Path "Tools\InstallAndLaunchAndroid.ps1"
Assert-Path "Tools\CheckBuildLog.ps1"
Assert-Path "Tools\CaptureAndroidLog.ps1"

if (-not $SkipServerCheck) {
    Write-Host ""
    Write-Host "Checking server syntax..."
    Push-Location (Join-Path $root "Server")
    try {
        npm run check
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Checking cleanup dry-run..."
& (Join-Path $root "Tools\CleanLocalArtifacts.ps1") | Select-Object -First 20

Write-Host ""
Write-Host "Preflight completed."
