$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$apk = "E:\code\c++\UnityRTS\Build\Android\UnityRTS.apk"

& $adb connect $dev
Start-Sleep 1
& $adb -s $dev install -r $apk
Start-Sleep 3

# 启动游戏
& $adb -s $dev shell am force-stop com.mystudio.unityRTS
Start-Sleep 2
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Write-Host "Launching..."
Start-Sleep 8

# 游客登录（横屏 1920x1080，游客按钮约 960,670）
& $adb -s $dev shell input tap 960 670
Write-Host "Guest login tapped"
Start-Sleep 8

# 截登录后大厅（验证先）
& $adb -s $dev shell screencap -p /data/local/tmp/s1.png
& $adb -s $dev pull /data/local/tmp/s1.png "E:\code\c++\UnityRTS\sc_after_login.png"

# 点击"立即匹配"进入游戏（约 480,390）
& $adb -s $dev shell input tap 480 390
Write-Host "Quick match tapped"
Start-Sleep 10

# 截游戏内小地图区域
& $adb -s $dev shell screencap -p /data/local/tmp/s2.png
& $adb -s $dev pull /data/local/tmp/s2.png "E:\code\c++\UnityRTS\sc_ingame.png"
Write-Host "Done"
