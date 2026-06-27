param(
    [switch]$Execute,
    [switch]$AllowUnityRunning,
    [string]$ArchiveRoot = "ExternalRawAssets"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ProjectPrefix = $ProjectRoot.TrimEnd("\") + "\"
$ExternalRoot = Join-Path $ProjectRoot "Assets\External"

function To-UnityPath([string]$Path) {
    return $Path.Replace("\", "/")
}

function Get-ProjectRelativePath([string]$FullPath) {
    $absolute = [IO.Path]::GetFullPath($FullPath)
    if (-not $absolute.StartsWith($ProjectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside project: $absolute"
    }
    return $absolute.Substring($ProjectPrefix.Length)
}

function Add-RegexGuids($Lines, $Set) {
    foreach ($line in $Lines) {
        foreach ($match in [regex]::Matches($line, "guid: ([0-9a-f]{32})")) {
            [void]$Set.Add($match.Groups[1].Value)
        }
    }
}

function Add-HardcodedExternalPaths($Lines, $Set) {
    foreach ($line in $Lines) {
        foreach ($match in [regex]::Matches($line, "Assets/External/[^`"`']+?\.(?:fbx|obj|png|jpg|jpeg|mat|asset|controller)")) {
            [void]$Set.Add((To-UnityPath $match.Value))
        }
    }
}

if (-not (Test-Path -LiteralPath $ExternalRoot)) {
    throw "Assets\External was not found."
}

if (-not $AllowUnityRunning -and (Get-Process Unity -ErrorAction SilentlyContinue)) {
    throw "Unity is running. Close Unity before moving assets, or pass -AllowUnityRunning if you know the AssetDatabase is idle."
}

Set-Location $ProjectRoot

$externalByGuid = @{}
Get-ChildItem -Recurse -File -LiteralPath $ExternalRoot -Filter *.meta | ForEach-Object {
    $line = Select-String -Path $_.FullName -Pattern "^guid: ([0-9a-f]{32})" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($line -and $line.Matches.Count -gt 0) {
        $guid = $line.Matches[0].Groups[1].Value
        $relativeMeta = To-UnityPath (Get-ProjectRelativePath $_.FullName)
        $assetPath = $relativeMeta.Substring(0, $relativeMeta.Length - 5)
        $externalByGuid[$guid] = $assetPath
    }
}

$usedGuids = New-Object "System.Collections.Generic.HashSet[string]"
$searchRoots = @("Assets", "ProjectSettings", "Packages") | Where-Object { Test-Path -LiteralPath $_ }
$rg = Get-Command rg -ErrorAction SilentlyContinue
if ($rg) {
    $guidLines = & rg --no-heading --glob "!Assets/External/**" --glob "!**/*.meta" -o "guid: [0-9a-f]{32}" $searchRoots 2>$null
    Add-RegexGuids $guidLines $usedGuids
} else {
    $textFiles = Get-ChildItem $searchRoots -Recurse -File -Include *.prefab,*.unity,*.mat,*.asset,*.controller,*.anim,*.uxml,*.uss -ErrorAction SilentlyContinue |
        Where-Object { (To-UnityPath (Get-ProjectRelativePath $_.FullName)) -notlike "Assets/External/*" -and $_.Extension -ne ".meta" }
    $guidLines = $textFiles | Select-String -Pattern "guid: [0-9a-f]{32}" -ErrorAction SilentlyContinue
    Add-RegexGuids $guidLines $usedGuids
}

$keepPaths = New-Object "System.Collections.Generic.HashSet[string]" ([StringComparer]::OrdinalIgnoreCase)
foreach ($guid in $usedGuids) {
    if ($externalByGuid.ContainsKey($guid)) {
        [void]$keepPaths.Add($externalByGuid[$guid])
    }
}

$codeRoots = @("Assets\Editor", "Assets\Scripts") | Where-Object { Test-Path -LiteralPath $_ }
if ($rg -and $codeRoots.Count -gt 0) {
    $pathLines = & rg --no-heading "Assets/External/" $codeRoots -g "*.cs" 2>$null
    Add-HardcodedExternalPaths $pathLines $keepPaths
} elseif ($codeRoots.Count -gt 0) {
    $pathLines = Get-ChildItem $codeRoots -Recurse -File -Filter *.cs | Select-String -Pattern "Assets/External/" -ErrorAction SilentlyContinue
    Add-HardcodedExternalPaths $pathLines $keepPaths
}

$keepPrefixes = @(
    "Assets/External/Kenney/BlockyCharacters/Models/FBX format/",
    "Assets/External/Kenney/BlasterKit/Models/FBX format/",
    "Assets/External/Kenney/CarKit/Models/FBX format/",
    "Assets/External/Kenney/NatureKit/Models/FBX format/",
    "Assets/External/Kenney/ModularBuildings/Models/FBX format/",
    "Assets/External/Kenney/TowerDefenseKit/Models/FBX format/",
    "Assets/External/Kenney/SurvivalKit/Models/FBX format/",
    "Assets/External/Kenney/RetroUrbanKit/Models/FBX format/",
    "Assets/External/Kenney/CastleKit/Models/FBX format/",
    "Assets/External/Kenney/TrainKit/Models/FBX format/",
    "Assets/External/MilitaryModels/Kenney/Extracted/Models/FBX format/",
    "Assets/External/MilitaryModels/Kenney/Extracted/WatercraftKit/Models/FBX format/",
    "Assets/External/Kenney/SpaceKit/Models/FBX format/",
    "Assets/External/Downloads/city-industrial/Models/FBX format/",
    "Assets/External/Downloads/factory-kit/Models/FBX format/",
    "Assets/External/MilitaryModels/Kenney/Extracted/SurvivalKit/Models/FBX format/",
    "Assets/External/MilitaryModels/Kenney/Extracted/CarKit/Models/FBX format/",
    "Assets/External/Kenney/NatureKit/Models/OBJ format/",
    "Assets/External/Kenney/SurvivalKit/Models/OBJ format/",
    "Assets/External/Kenney/CastleKit/Models/OBJ format/",
    "Assets/External/MilitaryModels/Kenney/Extracted/Models/OBJ format/",
    "Assets/External/Mixamo/BasicShooter/",
    "Assets/External/ambientCG/SciFiVehicleMaterials/"
)

function Test-KeepAsset([string]$UnityPath) {
    if ($keepPaths.Contains($UnityPath)) {
        return $true
    }

    foreach ($prefix in $keepPrefixes) {
        if ($UnityPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

$assetFiles = Get-ChildItem -Recurse -File -LiteralPath $ExternalRoot | Where-Object { $_.Extension -ne ".meta" }
$moveFiles = foreach ($file in $assetFiles) {
    $unityPath = To-UnityPath (Get-ProjectRelativePath $file.FullName)
    if (-not (Test-KeepAsset $unityPath)) {
        $file
    }
}

$keepFiles = foreach ($file in $assetFiles) {
    $unityPath = To-UnityPath (Get-ProjectRelativePath $file.FullName)
    if (Test-KeepAsset $unityPath) {
        $file
    }
}

$moveSummary = $moveFiles | Measure-Object Length -Sum
$keepSummary = $keepFiles | Measure-Object Length -Sum

Write-Host ("External assets to move: {0} files, {1:N2} MB" -f $moveSummary.Count, ($moveSummary.Sum / 1MB))
Write-Host ("External assets to keep: {0} files, {1:N2} MB" -f $keepSummary.Count, ($keepSummary.Sum / 1MB))

if (-not $Execute) {
    Write-Host "Dry run only. Re-run with -Execute after closing Unity."
    return
}

$targetRoot = Join-Path $ProjectRoot (Join-Path $ArchiveRoot ("ExternalAssets_" + (Get-Date -Format "yyyyMMdd_HHmmss")))
$targetFull = [IO.Path]::GetFullPath($targetRoot).TrimEnd("\") + "\"
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null

$moved = 0
foreach ($file in $moveFiles) {
    $sourceFull = [IO.Path]::GetFullPath($file.FullName)
    if (-not $sourceFull.StartsWith((Join-Path $ProjectRoot "Assets\External").TrimEnd("\") + "\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to move path outside Assets\External: $sourceFull"
    }

    $relative = Get-ProjectRelativePath $sourceFull
    $destination = Join-Path $targetRoot $relative
    $destinationFull = [IO.Path]::GetFullPath($destination)
    if (-not $destinationFull.StartsWith($targetFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to move outside archive root: $destinationFull"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destinationFull) | Out-Null
    Move-Item -LiteralPath $sourceFull -Destination $destinationFull

    $sourceMeta = $sourceFull + ".meta"
    if (Test-Path -LiteralPath $sourceMeta) {
        Move-Item -LiteralPath $sourceMeta -Destination ($destinationFull + ".meta")
    }

    $moved++
}

$dirs = Get-ChildItem -Recurse -Directory -LiteralPath $ExternalRoot | Sort-Object FullName -Descending
foreach ($dir in $dirs) {
    $remaining = Get-ChildItem -Force -LiteralPath $dir.FullName -ErrorAction SilentlyContinue
    if ($remaining.Count -eq 0) {
        $dirMeta = $dir.FullName + ".meta"
        if (Test-Path -LiteralPath $dirMeta) {
            $relativeMeta = Get-ProjectRelativePath $dirMeta
            $destMeta = Join-Path $targetRoot $relativeMeta
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destMeta) | Out-Null
            Move-Item -LiteralPath $dirMeta -Destination $destMeta
        }
        Remove-Item -LiteralPath $dir.FullName
    }
}

Write-Host "Moved $moved external asset files to:"
Write-Host $targetRoot
Write-Host "To restore, move that folder's Assets\\External contents back into the project root."
