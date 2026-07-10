param(
    [string]$InstallDir = "C:\UnityRTS\ServerCpp",
    [string]$TaskName = "UnityRTSGuardian"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$guardianExe = Join-Path $InstallDir "bin\unity_rts_guardian_cpp.exe"
$serverExe = Join-Path $InstallDir "bin\unity_rts_server_cpp.exe"
$envFile = Join-Path $InstallDir ".env"

if (-not (Test-Path $guardianExe)) {
    throw "Guardian executable not found: $guardianExe"
}

if (-not (Test-Path $serverExe)) {
    throw "Server executable not found: $serverExe"
}

if (-not (Test-Path $envFile)) {
    throw ".env not found: $envFile"
}

$escapedInstallDir = $InstallDir.Replace("'", "''")
$escapedGuardianExe = $guardianExe.Replace("'", "''")
$escapedServerExe = $serverExe.Replace("'", "''")
$escapedEnvFile = $envFile.Replace("'", "''")

$taskCommand = @"
Set-Location '$escapedInstallDir'
& '$escapedGuardianExe' --env '$escapedEnvFile' --server '$escapedServerExe'
"@

$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($taskCommand))
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand $encoded"
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1)
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null
Start-ScheduledTask -TaskName $TaskName

Write-Host "Installed scheduled task: $TaskName"
