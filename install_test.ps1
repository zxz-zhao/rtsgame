$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$apk = "E:\code\c++\UnityRTS\Build\Android\UnityRTS.apk"
$dev = "127.0.0.1:16384"

& $adb connect $dev
Start-Sleep 2
& $adb -s $dev install -r $apk
Start-Sleep 4
& $adb -s $dev logcat -c
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Start-Sleep 8
# 点击"游客登录"（首次进入是登录界面）
& $adb -s $dev shell input tap 640 480
Start-Sleep 6
& $adb -s $dev shell screencap -p /sdcard/lobby_new.png
& $adb -s $dev pull /sdcard/lobby_new.png "E:\code\c++\UnityRTS\lobby_new.png"
Write-Host "Screenshot saved"
