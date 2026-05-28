$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"

& $adb connect $dev
Start-Sleep 1

# 重启游戏
& $adb -s $dev shell am force-stop com.mystudio.unityRTS
Start-Sleep 2
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Write-Host "Game starting..."
Start-Sleep 8

# 查当前屏幕方向
$size = & $adb -s $dev shell wm size
Write-Host "Size: $size"

# 游客登录：横屏1920x1080下约 (960, 670)，竖屏1080x1920下约 (540, 1190)
& $adb -s $dev shell input tap 960 670
Write-Host "Tapped guest login"
Start-Sleep 8

# 截图
& $adb -s $dev shell screencap -p /data/local/tmp/sc2.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/sc2.png "E:\code\c++\UnityRTS\lobby_hall.png"
Write-Host "Done"
