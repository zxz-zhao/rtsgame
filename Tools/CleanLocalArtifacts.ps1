param(
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$topLevelPaths = @(
    "Library",
    "Temp",
    "Obj",
    "Build",
    "Logs",
    "UserSettings",
    "Server/node_modules",
    "LobbyExport"
)

$filePatterns = @(
    "Server/*.log",
    "Server/data/*.json",
    "*.log",
    "*.apk",
    "*.aab",
    "hs_err_pid*.log",
    "mumu_*.png",
    "build_*.png",
    "after*.png",
    "game*.png",
    "lobby*.png",
    "hud*.png",
    "fc*.png",
    "fa.png",
    "fb.png",
    "g*.png",
    "sfinal.png",
    "state.png",
    "sm.png",
    "*.zip"
)

$candidates = New-Object System.Collections.Generic.List[object]

foreach ($relativePath in $topLevelPaths) {
    $candidate = Join-Path $root $relativePath
    if (Test-Path -LiteralPath $candidate) {
        $candidates.Add((Get-Item -LiteralPath $candidate -Force))
    }
}

foreach ($pattern in $filePatterns) {
    $base = $root
    $leaf = $pattern
    if ($pattern.Contains("/")) {
        $base = Join-Path $root ($pattern.Substring(0, $pattern.LastIndexOf("/")))
        $leaf = $pattern.Substring($pattern.LastIndexOf("/") + 1)
    }
    if (Test-Path -LiteralPath $base) {
        Get-ChildItem -LiteralPath $base -Filter $leaf -Force -ErrorAction SilentlyContinue |
            ForEach-Object { $candidates.Add($_) }
    }
}

$unique = $candidates |
    Where-Object { $_ -ne $null } |
    Sort-Object FullName -Unique

if ($unique.Count -eq 0) {
    Write-Host "No local artifacts matched."
    exit 0
}

if (-not $Apply) {
    Write-Host "Dry run. The following local artifacts would be removed:"
    $unique | ForEach-Object { Write-Host ("  " + $_.FullName) }
    Write-Host ""
    Write-Host "Run with -Apply to delete these files and directories."
    exit 0
}

foreach ($item in $unique) {
    $resolved = Resolve-Path -LiteralPath $item.FullName
    if (-not $resolved.Path.StartsWith($root.Path, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove outside project root: $($resolved.Path)"
    }
}

foreach ($item in $unique) {
    Remove-Item -LiteralPath $item.FullName -Recurse -Force
}

Write-Host "Removed $($unique.Count) local artifact paths."
