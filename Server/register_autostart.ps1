Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$appDir = (Resolve-Path $PSScriptRoot).Path
$pm2 = Get-Command "pm2.cmd" -ErrorAction SilentlyContinue
if (-not $pm2) { $pm2 = Get-Command "pm2" }

$action   = New-ScheduledTaskAction -Execute $pm2.Source -Argument 'resurrect' -WorkingDirectory $appDir
$trigger  = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'UnityRTS-PM2-Resurrect' -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force | Out-Null
Write-Host 'UnityRTS-PM2-Resurrect auto-start task registered (runs at logon).'
