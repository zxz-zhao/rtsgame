$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$out = "E:\code\c++\UnityRTS"

& $adb connect $dev | Out-Null

# 点"⚔ 开始匹配" (图中约 960, 863)
& $adb -s $dev shell input tap 960 863
Write-Host "Tapped StartMatch"
Start-Sleep 14

& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/game1.png
Start-Sleep 2
& $adb -s $dev pull /data/local/tmp/game1.png "$out\game1.png"
Write-Host "In-game screenshot done"
