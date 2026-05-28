$action   = New-ScheduledTaskAction -Execute 'node' -Argument 'server.js' -WorkingDirectory 'E:\code\c++\UnityRTS\Server'
$trigger  = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 0) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'UnityRTS-Server' -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest -Force | Out-Null
Write-Host 'UnityRTS-Server auto-start task registered (runs at logon).'
