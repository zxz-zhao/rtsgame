#requires -Version 5.1

param(
    [ValidateSet('Start', 'Stop', 'Status')]
    [string]$Action = 'Start',

    # Address RustDesk clients should use. Keep 127.0.0.1 for same-machine tests.
    [string]$HostAddress = '127.0.0.1',

    [string]$ServerRoot = (Join-Path $env:LOCALAPPDATA 'RustDeskLocalServer'),
    [string]$RustDeskExe = 'D:\RustDesk\RustDesk.exe',
    [string]$ReleaseVersion = '1.1.15',
    [string]$DownloadUrl = '',

    [switch]$ConfigureClient,
    [switch]$RestoreClientConfig,
    [switch]$ForceDownload
)

$ErrorActionPreference = 'Stop'

$ReleaseApiUrl = 'https://api.github.com/repos/rustdesk/rustdesk-server/releases/latest'
$ReleaseAssetName = 'rustdesk-server-windows-x86_64-unsigned.zip'
$BackupSuffix = '.codex-local-test.bak'
$RequiredTcpPorts = @(21115, 21116, 21117, 21118, 21119)
$RequiredUdpPorts = @(21116)

$BinDir = Join-Path $ServerRoot 'bin'
$DataDir = Join-Path $ServerRoot 'data'
$LogDir = Join-Path $ServerRoot 'logs'
$HbbsPidPath = Join-Path $ServerRoot 'hbbs.pid'
$HbbrPidPath = Join-Path $ServerRoot 'hbbr.pid'

function Write-Step {
    param([string]$Message)
    Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $Message)
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Directories {
    foreach ($dir in @($ServerRoot, $BinDir, $DataDir, $LogDir)) {
        if (-not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }
}

function Get-ServerExe {
    param([Parameter(Mandatory = $true)][string]$Name)

    $direct = Join-Path $BinDir $Name
    if (Test-Path -LiteralPath $direct) {
        return $direct
    }

    $match = Get-ChildItem -LiteralPath $BinDir -Recurse -File -Filter $Name -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($match) {
        return $match.FullName
    }

    return $null
}

function Install-ServerBinaries {
    Ensure-Directories

    $hbbs = Get-ServerExe -Name 'hbbs.exe'
    $hbbr = Get-ServerExe -Name 'hbbr.exe'
    if (-not $ForceDownload -and $hbbs -and $hbbr) {
        return
    }

    Write-Step 'Downloading RustDesk server Windows package'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $url = $DownloadUrl
    if ([string]::IsNullOrWhiteSpace($url)) {
        $url = "https://github.com/rustdesk/rustdesk-server/releases/download/$ReleaseVersion/$ReleaseAssetName"

        try {
            $release = Invoke-RestMethod -Uri $ReleaseApiUrl -Headers @{ 'User-Agent' = 'Codex-RustDeskLocalTest' } -UseBasicParsing
            $asset = $release.assets |
                Where-Object { $_.name -match '^rustdesk-server-windows-x86_64.*\.zip$' } |
                Select-Object -First 1
            if ($asset) {
                $url = $asset.browser_download_url
            }
        } catch {
            Write-Step "GitHub API lookup skipped: $($_.Exception.Message)"
            Write-Step "Using direct release URL for $ReleaseVersion instead."
        }
    }

    $zipPath = Join-Path $ServerRoot $ReleaseAssetName
    Invoke-WebRequest -Uri $url -OutFile $zipPath -Headers @{ 'User-Agent' = 'Codex-RustDeskLocalTest' } -UseBasicParsing

    if (Test-Path -LiteralPath $BinDir) {
        Remove-Item -LiteralPath $BinDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $BinDir -Force

    $hbbs = Get-ServerExe -Name 'hbbs.exe'
    $hbbr = Get-ServerExe -Name 'hbbr.exe'
    if (-not $hbbs -or -not $hbbr) {
        throw "Downloaded package did not contain hbbs.exe and hbbr.exe. Package: $zipPath"
    }
}

function Get-ManagedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$PidPath,
        [Parameter(Mandatory = $true)][string]$ExeName
    )

    if (-not (Test-Path -LiteralPath $PidPath)) {
        return $null
    }

    $pidText = (Get-Content -LiteralPath $PidPath -Raw).Trim()
    if (-not ($pidText -match '^\d+$')) {
        Remove-Item -LiteralPath $PidPath -Force -ErrorAction SilentlyContinue
        return $null
    }

    $process = Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue
    if (-not $process -or $process.ProcessName -ne [IO.Path]::GetFileNameWithoutExtension($ExeName)) {
        Remove-Item -LiteralPath $PidPath -Force -ErrorAction SilentlyContinue
        return $null
    }

    return $process
}

function Stop-ManagedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$PidPath,
        [Parameter(Mandatory = $true)][string]$ExeName
    )

    $process = Get-ManagedProcess -PidPath $PidPath -ExeName $ExeName
    if (-not $process) {
        Write-Step "$Name is not running from this script."
        return
    }

    Write-Step "Stopping $Name (PID $($process.Id))"
    Stop-Process -Id $process.Id -Force
    Remove-Item -LiteralPath $PidPath -Force -ErrorAction SilentlyContinue
}

function Assert-PortsAvailable {
    $managedPids = @()
    foreach ($item in @(
        @{ Path = $HbbsPidPath; Exe = 'hbbs.exe' },
        @{ Path = $HbbrPidPath; Exe = 'hbbr.exe' }
    )) {
        $process = Get-ManagedProcess -PidPath $item.Path -ExeName $item.Exe
        if ($process) {
            $managedPids += $process.Id
        }
    }

    foreach ($port in $RequiredTcpPorts) {
        $listeners = @(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)
        $otherListeners = @($listeners | Where-Object { $managedPids -notcontains $_.OwningProcess })
        if ($otherListeners.Count -gt 0) {
            $owners = ($otherListeners | Select-Object -ExpandProperty OwningProcess -Unique) -join ', '
            throw "TCP port $port is already in use by process id(s): $owners"
        }
    }

    foreach ($port in $RequiredUdpPorts) {
        $listeners = @(Get-NetUDPEndpoint -LocalPort $port -ErrorAction SilentlyContinue)
        $otherListeners = @($listeners | Where-Object { $managedPids -notcontains $_.OwningProcess })
        if ($otherListeners.Count -gt 0) {
            $owners = ($otherListeners | Select-Object -ExpandProperty OwningProcess -Unique) -join ', '
            throw "UDP port $port is already in use by process id(s): $owners"
        }
    }
}

function Start-ManagedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ExePath,
        [Parameter(Mandatory = $true)][string]$PidPath,
        [string[]]$Arguments = @()
    )

    $existing = Get-ManagedProcess -PidPath $PidPath -ExeName ([IO.Path]::GetFileName($ExePath))
    if ($existing) {
        Write-Step "$Name is already running (PID $($existing.Id))"
        return
    }

    $stdout = Join-Path $LogDir "$Name.out.log"
    $stderr = Join-Path $LogDir "$Name.err.log"
    Write-Step "Starting $Name"
    $startArgs = @{
        FilePath = $ExePath
        WorkingDirectory = $DataDir
        RedirectStandardOutput = $stdout
        RedirectStandardError = $stderr
        WindowStyle = 'Hidden'
        PassThru = $true
    }

    if ($Arguments.Count -gt 0) {
        $startArgs.ArgumentList = $Arguments
    }

    $process = Start-Process @startArgs

    Set-Content -LiteralPath $PidPath -Value $process.Id -Encoding ASCII
    Start-Sleep -Seconds 1

    if ($process.HasExited) {
        $err = ''
        if (Test-Path -LiteralPath $stderr) {
            $err = Get-Content -LiteralPath $stderr -Raw
        }
        throw "$Name exited immediately. Log: $stderr $err"
    }
}

function Get-PublicKey {
    $keyPath = Join-Path $DataDir 'id_ed25519.pub'
    if (-not (Test-Path -LiteralPath $keyPath)) {
        return $null
    }

    return (Get-Content -LiteralPath $keyPath -Raw).Trim()
}

function Wait-PublicKey {
    for ($i = 0; $i -lt 30; $i++) {
        $key = Get-PublicKey
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            return $key
        }

        Start-Sleep -Seconds 1
    }

    throw "Server public key was not generated in $DataDir. Check $LogDir."
}

function Get-RustDesk2ConfigFiles {
    $candidates = New-Object System.Collections.Generic.List[string]
    $candidates.Add('C:\Windows\ServiceProfiles\LocalService\AppData\Roaming\RustDesk\config\RustDesk2.toml')
    $candidates.Add('C:\Windows\System32\config\systemprofile\AppData\Roaming\RustDesk\config\RustDesk2.toml')

    if ($env:APPDATA) {
        $candidates.Add((Join-Path $env:APPDATA 'RustDesk\config\RustDesk2.toml'))
    }

    $usersRoot = 'C:\Users'
    if (Test-Path -LiteralPath $usersRoot) {
        Get-ChildItem -LiteralPath $usersRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $candidates.Add((Join-Path $_.FullName 'AppData\Roaming\RustDesk\config\RustDesk2.toml'))
        }
    }

    $seen = @{}
    foreach ($path in $candidates) {
        if (-not $seen.ContainsKey($path) -and (Test-Path -LiteralPath $path)) {
            $seen[$path] = $true
            $path
        }
    }
}

function Set-TomlTopLevel {
    param(
        [Parameter(Mandatory = $true)][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $newLine = "{0} = '{1}'" -f $Name, ($Value -replace "'", "''")
    $namePattern = [regex]::Escape($Name)
    $insertIndex = $Lines.Count

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match '^\s*\[') {
            $insertIndex = $i
            break
        }

        if ($Lines[$i] -match "^\s*$namePattern\s*=") {
            $Lines[$i] = $newLine
            return $Lines
        }
    }

    if ($insertIndex -lt $Lines.Count) {
        $before = @()
        if ($insertIndex -gt 0) {
            $before = $Lines[0..($insertIndex - 1)]
        }
        $after = $Lines[$insertIndex..($Lines.Count - 1)]
        return @($before + $newLine + $after)
    }

    return @($Lines + $newLine)
}

function Set-TomlOption {
    param(
        [Parameter(Mandatory = $true)][string[]]$Lines,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $newLine = "{0} = '{1}'" -f $Name, ($Value -replace "'", "''")
    $namePattern = [regex]::Escape($Name)
    $inOptions = $false
    $foundOptions = $false
    $insertIndex = $Lines.Count

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]

        if ($line -match '^\s*\[([^\]]+)\]\s*$') {
            if ($inOptions) {
                $insertIndex = $i
                break
            }

            $inOptions = ($Matches[1] -eq 'options')
            if ($inOptions) {
                $foundOptions = $true
            }
            continue
        }

        if ($inOptions -and $line -match "^\s*$namePattern\s*=") {
            $Lines[$i] = $newLine
            return $Lines
        }
    }

    if ($foundOptions) {
        if ($insertIndex -lt $Lines.Count) {
            $before = @()
            if ($insertIndex -gt 0) {
                $before = $Lines[0..($insertIndex - 1)]
            }
            $after = $Lines[$insertIndex..($Lines.Count - 1)]
            return @($before + $newLine + $after)
        }

        return @($Lines + $newLine)
    }

    return @($Lines + '' + '[options]' + $newLine)
}

function Set-ClientConfig {
    param([Parameter(Mandatory = $true)][string]$Key)

    $idServer = "${HostAddress}:21116"
    $relayServer = "${HostAddress}:21117"
    $files = @(Get-RustDesk2ConfigFiles)

    if ($files.Count -eq 0) {
        throw 'No RustDesk2.toml config files were found. Open RustDesk once, then rerun with -ConfigureClient.'
    }

    foreach ($file in $files) {
        try {
            $backup = "$file$BackupSuffix"
            if (-not (Test-Path -LiteralPath $backup)) {
                Copy-Item -LiteralPath $file -Destination $backup -Force
                Write-Step "Backed up $file"
            }

            $lines = @(Get-Content -LiteralPath $file -Encoding UTF8)
            $lines = Set-TomlTopLevel -Lines $lines -Name 'rendezvous_server' -Value $idServer
            $lines = Set-TomlOption -Lines $lines -Name 'custom-rendezvous-server' -Value $idServer
            $lines = Set-TomlOption -Lines $lines -Name 'relay-server' -Value $relayServer
            $lines = Set-TomlOption -Lines $lines -Name 'key' -Value $Key
            Set-Content -LiteralPath $file -Encoding UTF8 -Value $lines
            Write-Step "Configured RustDesk client file: $file"
        } catch {
            Write-Step "Skipped ${file}: $($_.Exception.Message)"
        }
    }

    Restart-RustDeskService
}

function Restore-ClientConfig {
    $files = @(Get-RustDesk2ConfigFiles)
    $restored = 0

    foreach ($file in $files) {
        $backup = "$file$BackupSuffix"
        if (Test-Path -LiteralPath $backup) {
            try {
                Copy-Item -LiteralPath $backup -Destination $file -Force
                $restored++
                Write-Step "Restored $file"
            } catch {
                Write-Step "Skipped restore for ${file}: $($_.Exception.Message)"
            }
        }
    }

    if ($restored -eq 0) {
        Write-Step "No backups ending in $BackupSuffix were found."
    } else {
        Restart-RustDeskService
    }
}

function Restart-RustDeskService {
    $service = Get-Service -Name RustDesk -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        Write-Step 'RustDesk service is not installed; skipped service restart.'
        return
    }

    try {
        Write-Step 'Restarting RustDesk service'
        Restart-Service -Name RustDesk -Force -ErrorAction Stop
    } catch {
        Write-Step "Could not restart RustDesk service automatically: $($_.Exception.Message)"
    }
}

function Show-CurrentRustDeskId {
    if (-not (Test-Path -LiteralPath $RustDeskExe)) {
        Write-Step "RustDesk executable not found at $RustDeskExe; skipped --get-id."
        return
    }

    try {
        $id = (& $RustDeskExe --get-id 2>&1 | Out-String).Trim()
        if (-not [string]::IsNullOrWhiteSpace($id)) {
            Write-Step "Current RustDesk ID: $id"
        }
    } catch {
        Write-Step "Could not read RustDesk ID: $($_.Exception.Message)"
    }
}

function Write-Status {
    param([string]$Key = (Get-PublicKey))

    $hbbs = Get-ManagedProcess -PidPath $HbbsPidPath -ExeName 'hbbs.exe'
    $hbbr = Get-ManagedProcess -PidPath $HbbrPidPath -ExeName 'hbbr.exe'

    Write-Host ''
    Write-Host 'RustDesk local OSS server'
    Write-Host "  hbbs:         $(if ($hbbs) { "running PID $($hbbs.Id)" } else { 'stopped' })"
    Write-Host "  hbbr:         $(if ($hbbr) { "running PID $($hbbr.Id)" } else { 'stopped' })"
    Write-Host "  ID Server:    ${HostAddress}:21116"
    Write-Host "  Relay Server: ${HostAddress}:21117"
    Write-Host "  API Server:   <blank for OSS>"
    Write-Host "  Key:          $Key"
    Write-Host "  Server Root:  $ServerRoot"
    Write-Host ''
}

function Start-LocalServer {
    Install-ServerBinaries
    Ensure-Directories
    Assert-PortsAvailable

    $hbbrExe = Get-ServerExe -Name 'hbbr.exe'
    $hbbsExe = Get-ServerExe -Name 'hbbs.exe'
    Start-ManagedProcess -Name 'hbbr' -ExePath $hbbrExe -PidPath $HbbrPidPath
    Start-ManagedProcess -Name 'hbbs' -ExePath $hbbsExe -PidPath $HbbsPidPath -Arguments @('-r', "${HostAddress}:21117")

    $key = Wait-PublicKey
    Write-Status -Key $key

    if ($ConfigureClient) {
        if (-not (Test-Admin)) {
            Write-Step 'Not running as Administrator; service-profile RustDesk config may be skipped.'
        }
        Set-ClientConfig -Key $key
        Show-CurrentRustDeskId
    }
}

if ($RestoreClientConfig) {
    Restore-ClientConfig
    return
}

switch ($Action) {
    'Start' {
        Start-LocalServer
    }
    'Stop' {
        Stop-ManagedProcess -Name 'hbbs' -PidPath $HbbsPidPath -ExeName 'hbbs.exe'
        Stop-ManagedProcess -Name 'hbbr' -PidPath $HbbrPidPath -ExeName 'hbbr.exe'
    }
    'Status' {
        Install-ServerBinaries
        Write-Status
        Show-CurrentRustDeskId
    }
}
