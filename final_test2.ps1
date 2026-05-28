$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$out = "E:\code\c++\UnityRTS"

& $adb connect $dev | Out-Null
& $adb -s $dev shell am force-stop com.mystudio.unityRTS
Start-Sleep 2
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Start-Sleep 12

& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/fa.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/fa.png "$out\fa.png"
Write-Host "=LOGIN="

# 游客登录 (横屏 1920x1080)
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 960 660
Start-Sleep 12

& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/fb.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/fb.png "$out\fb.png"
Write-Host "=LOBBY="

# 立即匹配按钮 (在快速匹配卡中，大概左1/3、中高)
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 560 540
Write-Host "Tapped QuickMatch"
Start-Sleep 14

& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/fc.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/fc.png "$out\fc.png"
Write-Host "=IN-GAME="
