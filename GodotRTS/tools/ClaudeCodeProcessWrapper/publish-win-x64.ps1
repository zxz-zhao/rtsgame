$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "ClaudeCodeProcessWrapper.csproj"
$dist = Join-Path $PSScriptRoot "dist"

dotnet publish $project `
  -c Release `
  -r win-x64 `
  -p:PublishSingleFile=true `
  --self-contained true

$publishDir = Join-Path $PSScriptRoot "bin\Release\net9.0-windows\win-x64\publish"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Copy-Item -LiteralPath (Join-Path $publishDir "ClaudeCodeProcessWrapper.exe") -Destination (Join-Path $dist "ClaudeCodeProcessWrapper.exe") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "OpenClaudeConfig.cmd") -Destination (Join-Path $dist "OpenClaudeConfig.cmd") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ClaudeCodeProcessWrapper.sample.json") -Destination (Join-Path $dist "ClaudeCodeProcessWrapper.sample.json") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README.zh-CN.md") -Destination (Join-Path $dist "README.zh-CN.md") -Force

Write-Host "Published GUI build to $dist"
