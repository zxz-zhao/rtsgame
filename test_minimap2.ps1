$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"

& $adb connect $dev

# 先确认设备可以截图
& $adb -s $dev shell screencap -p /data/local/tmp/sc3.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/sc3.png "E:\code\c++\UnityRTS\sc_current.png"
Write-Host "Current screen pulled"

# 查看游戏进程状态
$proc = & $adb -s $dev shell "ps | grep unityRTS"
Write-Host "Process: $proc"

# 点游客登录（先试竖屏坐标 540,1280，然后等）
& $adb -s $dev shell input tap 540 1280
Start-Sleep 1
& $adb -s $dev shell input tap 540 1190
Start-Sleep 8

& $adb -s $dev shell screencap -p /data/local/tmp/sc4.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/sc4.png "E:\code\c++\UnityRTS\sc_after_tap.png"
Write-Host "After tap pulled"
