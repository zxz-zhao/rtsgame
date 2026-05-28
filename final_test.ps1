$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"

function Adb { param([string[]]$Args)
    & $adb -s $dev @Args 2>&1
}
function Reconnect {
    & $adb connect $dev | Out-Null
    Start-Sleep 1
}

Reconnect

# 重启游戏
Adb @("shell","am","force-stop","com.mystudio.unityRTS")
Start-Sleep 2
Adb @("shell","am","start","-n","com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity")
Start-Sleep 12

# 截登录页
Reconnect
Adb @("shell","screencap","-p","/data/local/tmp/fa.png")
Start-Sleep 2
Adb @("pull","/data/local/tmp/fa.png","E:\code\c++\UnityRTS\fa.png")
Write-Host "=LOGIN PAGE="

# 游客登录 (横屏 1920x1080, 按钮约 960,660)
Reconnect
Adb @("shell","input","tap","960","660")
Start-Sleep 12

# 截大厅
Reconnect
Adb @("shell","screencap","-p","/data/local/tmp/fb.png")
Start-Sleep 2
Adb @("pull","/data/local/tmp/fb.png","E:\code\c++\UnityRTS\fb.png")
Write-Host "=LOBBY="

# 大厅"立即匹配"（快速匹配卡片按钮 约 560,530）
Reconnect
Adb @("shell","input","tap","560","530")
Write-Host "Tapped match"
Start-Sleep 14

# 截游戏内（小地图区域右下角）
Reconnect
Adb @("shell","screencap","-p","/data/local/tmp/fc.png")
Start-Sleep 2
Adb @("pull","/data/local/tmp/fc.png","E:\code\c++\UnityRTS\fc.png")
Write-Host "=IN-GAME="
