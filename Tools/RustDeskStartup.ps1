#requires -Version 5.1

param(
    [switch]$InstallTask
)

$ErrorActionPreference = 'Stop'

# Change this if RustDesk is installed somewhere else.
$RustDeskExe = 'D:\RustDesk\RustDesk.exe'

# Requested ID.
$TargetId = 'a999999'

# Put the unattended permanent password here.
# Leave it empty to keep the password already saved in RustDesk.
$RustDeskPassword = ''

# Set to $true if you want the script to exit with an error when RustDesk
# rejects the requested ID. The unattended-access fixes still run either way.
$StrictIdVerification = $false

$TaskName = 'RustDeskStartupConfig'
$LogDir = Join-Path $env:ProgramData 'RustDeskStartup'
$LogPath = Join-Path $LogDir 'startup.log'

function Write-Log {
    param([string]$Message)

    if (-not (Test-Path -LiteralPath $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }

    $line = '{0} {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    Add-Content -LiteralPath $LogPath -Value $line
    Write-Host $line
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-TomlOption {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    $newLine = "{0} = '{1}'" -f $Name, ($Value -replace "'", "''")
    $namePattern = [regex]::Escape($Name)
    $inOptions = $false
    $foundOptions = $false
    $replaced = $false
    $insertIndex = $lines.Count

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match '^\s*\[([^\]]+)\]\s*$') {
            if ($inOptions -and -not $replaced) {
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
            $lines[$i] = $newLine
            $replaced = $true
            break
        }
    }

    if (-not $replaced) {
        if ($foundOptions) {
            if ($insertIndex -lt $lines.Count) {
                $before = @()
                $after = @()
                if ($insertIndex -gt 0) {
                    $before = $lines[0..($insertIndex - 1)]
                }
                $after = $lines[$insertIndex..($lines.Count - 1)]
                $lines = @($before + $newLine + $after)
            } else {
                $lines = @($lines + $newLine)
            }
        } else {
            $lines = @($lines + '' + '[options]' + $newLine)
        }
    }

    Set-Content -LiteralPath $Path -Encoding UTF8 -Value $lines
    Write-Log "Updated $Name in $Path"
}

function Get-RustDeskConfigFiles {
    $candidates = New-Object System.Collections.Generic.List[string]

    $serviceFiles = @(
        'C:\Windows\ServiceProfiles\LocalService\AppData\Roaming\RustDesk\config\RustDesk.toml',
        'C:\Windows\System32\config\systemprofile\AppData\Roaming\RustDesk\config\RustDesk.toml'
    )

    foreach ($path in $serviceFiles) {
        $candidates.Add($path)
    }

    $usersRoot = 'C:\Users'
    if (Test-Path -LiteralPath $usersRoot) {
        Get-ChildItem -LiteralPath $usersRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $candidates.Add((Join-Path $_.FullName 'AppData\Roaming\RustDesk\config\RustDesk.toml'))
        }
    }

    if ($env:APPDATA) {
        $candidates.Add((Join-Path $env:APPDATA 'RustDesk\config\RustDesk.toml'))
    }

    $seen = @{}
    foreach ($path in $candidates) {
        if (-not $seen.ContainsKey($path) -and (Test-Path -LiteralPath $path)) {
            $seen[$path] = $true
            $path
        }
    }
}

function Invoke-RustDesk {
    param([string[]]$Arguments)

    if (-not (Test-Path -LiteralPath $RustDeskExe)) {
        throw "RustDesk executable not found: $RustDeskExe"
    }

    Write-Log "Running: $RustDeskExe $($Arguments -join ' ')"
    $output = & $RustDeskExe @Arguments 2>&1 | Out-String
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "RustDesk command failed with exit code $LASTEXITCODE. Output: $output"
    }

    if (-not [string]::IsNullOrWhiteSpace($output)) {
        return ($output -split "\r?\n" | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        })
    }
}

function Ensure-RustDeskService {
    $service = Get-Service -Name RustDesk -ErrorAction SilentlyContinue

    if ($null -eq $service) {
        Write-Log 'RustDesk service not found. Installing service.'
        Invoke-RustDesk -Arguments @('--install-service') | Out-Null
        Start-Sleep -Seconds 2
        $service = Get-Service -Name RustDesk -ErrorAction SilentlyContinue
    }

    if ($null -eq $service) {
        throw 'RustDesk service still not found after install attempt.'
    }

    Set-Service -Name RustDesk -StartupType Automatic

    $service = Get-Service -Name RustDesk
    if ($service.Status -eq 'Running') {
        Write-Log 'Restarting RustDesk service.'
        Restart-Service -Name RustDesk -Force
    } else {
        Write-Log 'Starting RustDesk service.'
        Start-Service -Name RustDesk
    }
}

function Get-RustDeskId {
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        $id = @(Invoke-RustDesk -Arguments @('--get-id') | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_)
        } | Select-Object -First 1)

        if ($id.Count -gt 0) {
            return $id[0].ToString().Trim()
        }

        Write-Log "RustDesk did not return an ID yet. Retry $attempt/10."
        Start-Sleep -Seconds 2
    }

    return $null
}

function Install-StartupTask {
    if (-not (Test-Admin)) {
        throw 'Please run PowerShell as Administrator before installing the startup task.'
    }

    $scriptPath = $PSCommandPath
    if (-not $scriptPath) {
        throw 'Cannot determine script path for scheduled task.'
    }

    $action = New-ScheduledTaskAction `
        -Execute 'powershell.exe' `
        -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $scriptPath)
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 5) `
        -StartWhenAvailable

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Configures RustDesk ID, unattended access settings, and service startup.' `
        -Force | Out-Null

    Write-Log "Installed startup task: $TaskName"
}

if ($InstallTask) {
    Install-StartupTask
    return
}

if (-not (Test-Admin)) {
    Write-Log 'Warning: not running as Administrator. Service/config updates may fail.'
}

if ($TargetId -notmatch '^[A-Za-z]') {
    Write-Log "Warning: RustDesk may reject custom IDs that do not start with a letter. Trying requested ID anyway: $TargetId"
}

$setIdOutput = @(Invoke-RustDesk -Arguments @('--set-id', $TargetId))
if ($setIdOutput.Count -gt 0) {
    Write-Log "RustDesk set-id output: $($setIdOutput -join ' ')"
}

if (-not [string]::IsNullOrWhiteSpace($RustDeskPassword)) {
    Invoke-RustDesk -Arguments @('--password', $RustDeskPassword) | Out-Null
    Write-Log 'Permanent password was set from this script.'
} else {
    Write-Log 'RustDeskPassword is empty. Keeping the existing saved password.'
}

$configFiles = @(Get-RustDeskConfigFiles)
foreach ($configFile in $configFiles) {
    Set-TomlOption -Path $configFile -Name 'allow-only-conn-window-open' -Value 'N'
    Set-TomlOption -Path $configFile -Name 'verification-method' -Value 'use-permanent-password'
    Set-TomlOption -Path $configFile -Name 'access-mode' -Value 'full'
}

Ensure-RustDeskService
Start-Sleep -Seconds 3

$currentId = Get-RustDeskId
if ([string]::IsNullOrWhiteSpace($currentId)) {
    throw 'RustDesk ID verification failed. RustDesk did not return an ID after service restart.'
}

if ($currentId -ne $TargetId) {
    $message = "RustDesk ID verification failed. Expected $TargetId, got $currentId. RustDesk rejected this ID format; use an ID that starts with a letter, for example r999999999."
    if ($StrictIdVerification) {
        throw $message
    }

    Write-Log "Warning: $message"
    Write-Log 'RustDesk startup configuration completed with ID warning.'
    return
}

Write-Log "RustDesk startup configuration completed. Current ID: $currentId"
