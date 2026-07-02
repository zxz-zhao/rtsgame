param(
    [string]$UnityRoot = "..",
    [string]$Output = "migration/unity_asset_manifest.json",
    [switch]$Copy,
    [switch]$IncludeFbx,
    [string]$AssetOut = "assets_migrated"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path $UnityRoot
$rootPath = $root.Path
$outPath = Join-Path (Get-Location) $Output
$outDir = Split-Path $outPath -Parent
New-Item -ItemType Directory -Force $outDir | Out-Null

$assetRoot = Join-Path $rootPath "Assets"
$extensions = @(".glb", ".gltf", ".obj", ".mtl", ".fbx", ".png", ".jpg", ".jpeg", ".wav", ".ogg", ".mp3")
$files = Get-ChildItem -Path $assetRoot -Recurse -File |
    Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() }

$items = foreach ($file in $files) {
    if ($file.FullName.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $file.FullName.Substring($rootPath.Length).TrimStart([char[]]@("\", "/")).Replace("\", "/")
    } else {
        $relative = $file.FullName.Replace("\", "/")
    }
    $kind = switch ($file.Extension.ToLowerInvariant()) {
        ".glb"  { "model-ready"; break }
        ".gltf" { "model-ready"; break }
        ".obj"  { "model-ready"; break }
        ".mtl"  { "material-sidecar-ready"; break }
        ".fbx"  { "model-convert-to-gltf"; break }
        ".png"  { "texture-ready"; break }
        ".jpg"  { "texture-ready"; break }
        ".jpeg" { "texture-ready"; break }
        ".wav"  { "audio-ready"; break }
        ".ogg"  { "audio-ready"; break }
        ".mp3"  { "audio-ready"; break }
        default { "unknown" }
    }
    [pscustomobject]@{
        path = $relative
        extension = $file.Extension.ToLowerInvariant()
        bytes = $file.Length
        migration = $kind
    }
}

$summary = $items | Group-Object migration | ForEach-Object {
    [pscustomobject]@{ migration = $_.Name; count = $_.Count; bytes = ($_.Group | Measure-Object bytes -Sum).Sum }
}

$manifest = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("o")
    unityRoot = $rootPath
    totalCount = $items.Count
    totalBytes = ($items | Measure-Object bytes -Sum).Sum
    summary = $summary
    assets = $items
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $outPath
Write-Host "Wrote $outPath"

if ($Copy) {
    $targetRoot = Join-Path (Get-Location) $AssetOut
    New-Item -ItemType Directory -Force $targetRoot | Out-Null
    $copyable = if ($IncludeFbx) {
        $items
    } else {
        $items | Where-Object { $_.migration -ne "model-convert-to-gltf" }
    }
    foreach ($item in $copyable) {
        $src = Join-Path $rootPath $item.path
        $dst = Join-Path $targetRoot $item.path
        New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
    Write-Host "Copied $($copyable.Count) ready assets to $targetRoot"
    if (-not $IncludeFbx) {
        Write-Host "Skipped FBX originals. Add -IncludeFbx when you want a full-fidelity source asset mirror."
    }
}
