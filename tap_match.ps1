$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$out = "E:\code\c++\UnityRTS"

& $adb connect $dev | Out-Null

# 在大厅界面点 "立即匹配" (576, 702 = 1280x720 canvas mapped to 1920x1080)
& $adb -s $dev shell input tap 576 702
Write-Host "Tapped 立即匹配"
Start-Sleep 14   # 5s 匹配 + 9s 场景加载

& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/fc2.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/fc2.png "$out\fc2.png"
Write-Host "Done"
