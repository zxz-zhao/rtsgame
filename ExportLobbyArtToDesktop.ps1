#requires -Version 5.1
<#
.SYNOPSIS
    Copy all Assets/Resources/LobbyGen/*.png to Desktop\RTS_LobbyArt_全部PNG and zip.

.EXAMPLE
    .\ExportLobbyArtToDesktop.ps1
#>
$ErrorActionPreference = 'Stop'
$desk = [Environment]::GetFolderPath('Desktop')
$Root = $PSScriptRoot
if (-not $Root) { $Root = (Get-Location).Path }
$src = Join-Path $Root 'Assets\Resources\LobbyGen'
if (-not (Test-Path $src)) { Write-Error "Missing: $src" }

$out = Join-Path $desk 'RTS_LobbyArt_全部PNG'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Get-ChildItem $src -Filter '*.png' | Copy-Item -Destination $out -Force
$n = (Get-ChildItem $out -Filter '*.png').Count

$zip = Join-Path $desk 'RTS_LobbyArt_全部PNG.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force

Write-Host "Copied $n PNG -> $out" -ForegroundColor Green
Write-Host "Zip: $zip" -ForegroundColor Green
