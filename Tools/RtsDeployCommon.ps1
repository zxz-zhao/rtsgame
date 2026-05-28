# Shared deploy helpers (dot-source from PackInstallTest.ps1 / build_and_deploy.ps1). Do not run alone.

function Rts_GetDefaultUnityPath {
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) { return $env:UNITY_EDITOR }
    return 'E:\Tuanjie Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
}

function Rts_FindAdb {
    if ($env:ADB_PATH -and (Test-Path $env:ADB_PATH)) { return $env:ADB_PATH }
    $candidates = @(
        'F:\AndroidSdk\platform-tools\adb.exe',
        'G:\Program Files\Netease\MuMu\nx_main\adb.exe',
        "${env:LOCALAPPDATA}\Android\Sdk\platform-tools\adb.exe",
        "${env:ANDROID_HOME}\platform-tools\adb.exe"
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }
    return $null
}

function Rts_TestPortListening {
    param([int]$Port)
    $hit = netstat -ano 2>$null | Select-String (":$Port\s+.*LISTENING")
    return [bool]$hit
}

function Rts_TestBackendHealth {
    param([int]$Port = 8080)
    try {
        $response = Invoke-WebRequest -Uri ("http://127.0.0.1:" + $Port + "/health") -UseBasicParsing -TimeoutSec 3
        return $response.StatusCode -eq 200 -and $response.Content -match '"ok"\s*:\s*true'
    } catch {
        return $false
    }
}

function Rts_EnsureBackendServer {
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [int]$Port = 8080,
        [string]$DBBackend = 'json',
        [int]$StartupWaitSec = 3,
        [int]$MaxWaitSec = 12
    )
    $serverDir = Join-Path $ProjectRoot 'Server'
    $scriptJs  = Join-Path $serverDir 'server.js'
    if (-not (Test-Path $scriptJs)) {
        Write-Host '[Server] No Server\server.js, skip.' -ForegroundColor Yellow
        return $true
    }
    if (Rts_TestPortListening -Port $Port) {
        if (Rts_TestBackendHealth -Port $Port) {
            Write-Host ('[Server] Port ' + $Port + ' already listening and healthy.') -ForegroundColor Gray
            return $true
        }
        Write-Warning ('[Server] Port ' + $Port + ' is listening, but /health did not respond. Stop the stale process or use a different port.')
        return $false
    }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Warning '[Server] node not in PATH; cannot start backend.'
        return $false
    }
    Write-Host ('[Server] Starting node server.js (port ' + $Port + ', DB_BACKEND=' + $DBBackend + ')...') -ForegroundColor Yellow
    $startCommand = "`$env:DB_BACKEND='$DBBackend'; node server.js"
    Start-Process -FilePath 'powershell' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $startCommand) `
        -WorkingDirectory $serverDir `
        -WindowStyle Hidden
    $deadline = [datetime]::UtcNow.AddSeconds($MaxWaitSec)
    while ([datetime]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $StartupWaitSec
        if (Rts_TestPortListening -Port $Port) {
            Write-Host ('[Server] Ready (' + $Port + ' LISTEN).') -ForegroundColor Green
            return $true
        }
    }
    Write-Warning ('[Server] Port ' + $Port + ' not listening after ' + $MaxWaitSec + 's.')
    return $false
}

function Rts_ConnectAdbEmulators {
    param(
        [Parameter(Mandatory)][string]$AdbExe,
        [string]$AdbHost = '127.0.0.1',
        [int[]]$Ports = @(7555, 16384, 5555)
    )
    $oldEa = $ErrorActionPreference
    $ErrorActionPreference = 'SilentlyContinue'
    try {
        & $AdbExe kill-server 2>&1 | Out-Null
        Start-Sleep -Milliseconds 400
        & $AdbExe start-server 2>&1 | Out-Null
        foreach ($p in $Ports) {
            & $AdbExe connect ($AdbHost + ':' + $p) 2>&1 | Out-Null
        }
        Start-Sleep -Milliseconds 600
    } finally {
        $ErrorActionPreference = $oldEa
    }
}

function Rts_PickAdbSerial {
    param([Parameter(Mandatory)][string]$AdbExe)
    $raw = & $AdbExe devices 2>&1 | Out-String
    $serials = @()
    foreach ($line in ($raw -split "`r?`n")) {
        if ($line -match '^([\w.\-:]+)\s+device\s*$') { $serials += $Matches[1] }
    }
    foreach ($pref in @('127.0.0.1:7555', '127.0.0.1:16384', '127.0.0.1:5555')) {
        if ($serials -contains $pref) { return $pref }
    }
    foreach ($s in $serials) {
        if ($s -like 'emulator-*') { return $s }
    }
    return $serials | Select-Object -First 1
}

function Rts_InstallApk {
    param(
        [Parameter(Mandatory)][string]$AdbExe,
        [Parameter(Mandatory)][string]$Serial,
        [Parameter(Mandatory)][string]$ApkPath
    )
    Write-Host ('  adb install -r -> ' + $Serial) -ForegroundColor Gray
    $r = & $AdbExe -s $Serial install -r $ApkPath 2>&1
    Write-Host ('  ' + $r)
    return ("$r" -match 'Success')
}

function Rts_LaunchUnityPlayer {
    param(
        [Parameter(Mandatory)][string]$AdbExe,
        [Parameter(Mandatory)][string]$Serial,
        [string]$PackageId = 'com.mystudio.unityRTS'
    )
    $oldEa = $ErrorActionPreference
    $ErrorActionPreference = 'SilentlyContinue'
    try {
        & $AdbExe -s $Serial shell monkey -p $PackageId -c android.intent.category.LAUNCHER 1 2>&1 | Out-Null
    } finally {
        $ErrorActionPreference = $oldEa
    }
}

function Rts_InstallAndLaunch {
    param(
        [Parameter(Mandatory)][string]$AdbExe,
        [Parameter(Mandatory)][string]$Serial,
        [Parameter(Mandatory)][string]$ApkPath,
        [string]$PackageId = 'com.mystudio.unityRTS'
    )
    if (-not (Rts_InstallApk -AdbExe $AdbExe -Serial $Serial -ApkPath $ApkPath)) { return $false }
    Rts_LaunchUnityPlayer -AdbExe $AdbExe -Serial $Serial -PackageId $PackageId
    return $true
}
