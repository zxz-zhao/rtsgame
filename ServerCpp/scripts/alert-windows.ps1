param(
    [string]$Title = "UnityRTS alert",
    [string]$Message = "No message provided"
)

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
Write-Output "$timestamp [ALERT-HOOK] $Title :: $Message"
