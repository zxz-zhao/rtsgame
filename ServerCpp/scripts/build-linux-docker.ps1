<#
.SYNOPSIS
Builds the Linux C++ server binaries inside a Docker container and extracts the release tarball.
#>
param(
    [string]$DistDir = "$PSScriptRoot\..\dist"
)

$ErrorActionPreference = "Stop"
$RootDir = (Resolve-Path "$PSScriptRoot\..").Path

Write-Host "==> Building UnityRTS C++ Server Linux binary via Docker builder..." -ForegroundColor Cyan
Set-Location $RootDir

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

$imageTag = "unity-rts-cpp-builder:temp"
docker build --target builder -t $imageTag -f Dockerfile .
if ($LASTEXITCODE -ne 0) {
    throw "Docker build failed."
}

$containerId = (docker create $imageTag).Trim()
Write-Host "==> Extracting compiled Linux binaries from container $containerId..." -ForegroundColor Cyan

$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    docker cp "${containerId}:/src/build/linux/unity_rts_server_cpp" "$tempDir\unity_rts_server_cpp"
    docker cp "${containerId}:/src/build/linux/unity_rts_guardian_cpp" "$tempDir\unity_rts_guardian_cpp"
    docker rm $containerId | Out-Null

    $pkgName = "unity-rts-server-cpp-0.1.0-Linux-x86_64"
    $pkgDir = Join-Path $tempDir $pkgName
    New-Item -ItemType Directory -Path "$pkgDir\bin" -Force | Out-Null
    New-Item -ItemType Directory -Path "$pkgDir\scripts" -Force | Out-Null
    New-Item -ItemType Directory -Path "$pkgDir\logs" -Force | Out-Null
    New-Item -ItemType Directory -Path "$pkgDir\data" -Force | Out-Null
    New-Item -ItemType Directory -Path "$pkgDir\backups" -Force | Out-Null

    Move-Item "$tempDir\unity_rts_server_cpp" "$pkgDir\bin\"
    Move-Item "$tempDir\unity_rts_guardian_cpp" "$pkgDir\bin\"

    Copy-Item "$RootDir\config\server.env.example" "$pkgDir\.env.example"
    Copy-Item "$RootDir\config\server.env.example" "$pkgDir\.env"
    Copy-Item "$RootDir\README.md" "$pkgDir\"
    Copy-Item "$RootDir\DEPLOY.md" "$pkgDir\"
    Copy-Item "$RootDir\MIGRATION.md" "$pkgDir\"
    Copy-Item "$RootDir\scripts\*.sh" "$pkgDir\scripts\"

    $tarOutput = Join-Path $DistDir "$pkgName.tar.gz"
    tar -czf $tarOutput -C $tempDir $pkgName

    Write-Host "==> Linux package successfully generated: $tarOutput" -ForegroundColor Green
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
