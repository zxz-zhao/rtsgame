param(
    [string]$Manifest = "migration/unity_asset_manifest.json",
    [string]$AssetOut = "assets_migrated",
    [switch]$IncludeFbx,
    [int]$BatchSize = 0
)

$ErrorActionPreference = "Stop"

$manifestPath = Resolve-Path $Manifest
$manifestData = Get-Content $manifestPath -Raw | ConvertFrom-Json
$rootPath = $manifestData.unityRoot
$targetRoot = Join-Path (Get-Location) $AssetOut
New-Item -ItemType Directory -Force $targetRoot | Out-Null

$items = $manifestData.assets
if (-not $IncludeFbx) {
    $items = $items | Where-Object { $_.migration -ne "model-convert-to-gltf" }
}

$copied = 0
$skipped = 0
$failed = 0

foreach ($item in $items) {
    $src = Join-Path $rootPath $item.path
    $dst = Join-Path $targetRoot $item.path
    if ((Test-Path -LiteralPath $dst) -and ((Get-Item -LiteralPath $dst).Length -eq [int64]$item.bytes)) {
        $skipped++
        continue
    }

    try {
        New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
        $copied++
        if ($BatchSize -gt 0 -and $copied -ge $BatchSize) {
            break
        }
    } catch {
        Write-Warning "Failed to copy $($item.path): $($_.Exception.Message)"
        $failed++
    }
}

[pscustomobject]@{
    copied = $copied
    skipped = $skipped
    failed = $failed
    includeFbx = [bool]$IncludeFbx
    target = $targetRoot
}
