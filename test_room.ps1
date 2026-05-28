$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$apk = "E:\code\c++\UnityRTS\Build\Android\UnityRTS.apk"
$out = "E:\code\c++\UnityRTS"

& $adb connect $dev | Out-Null
& $adb -s $dev install -r $apk
Start-Sleep 3

& $adb connect $dev | Out-Null
& $adb -s $dev shell am force-stop com.mystudio.unityRTS
Start-Sleep 2
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Start-Sleep 12

# 游客登录 (960, 660)
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 960 660
Start-Sleep 10

# 在大厅点"自定义房间"按钮
# Canvas 1280x720: CustomRoomBtn anchor (0.5, 0.2) -> x=640, y=144 from bottom -> y_top=576
# Device: x=640*1.5=960, y=576*1.5=864... wait let me recalc
# CustomRoom button: anchor (0.5, 0.26) of HallPanel (652h)
# y_from_bottom = 0.26*652=169, y_from_top = 720-169=551, device_y = 551*1.5=827
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 960 827
Start-Sleep 4

# 截自定义房间面板
& $adb -s $dev shell screencap -p /data/local/tmp/room1.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/room1.png "$out\room1.png"
Write-Host "Room panel screenshot saved"

# 点 "+ 创建" 按钮
# CreateRoomButton anchor (0.84, 0.91) in RoomPanel (full: 1280x652 canvas)
# canvas x=0.84*1280=1075, device x=1075*1.5=1613 (clamp to ~1600)
# RoomPanel top = TOP_H*1.5 = 102, height = (720-68)*1.5 = 978
# canvas y from bottom = 0.91*652=593, canvas y from top = 720-68-593=59
# device y = 102 + 59*1.5 = 102+89 = 191
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 1600 191
Start-Sleep 3

# 截创建房间弹窗
& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/room2.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/room2.png "$out\room2.png"
Write-Host "Create room dialog screenshot saved"
