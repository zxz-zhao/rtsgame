$adb = "F:\AndroidSdk\platform-tools\adb.exe"
$dev = "127.0.0.1:16384"
$apk = "E:\code\c++\UnityRTS\Build\Android\UnityRTS.apk"

& $adb connect $dev | Out-Null
& $adb -s $dev install -r $apk
Start-Sleep 3

# 完全重启游戏
& $adb connect $dev | Out-Null
& $adb -s $dev shell am force-stop com.mystudio.unityRTS
Start-Sleep 3
& $adb -s $dev shell am start -n com.mystudio.unityRTS/com.unity3d.player.UnityPlayerActivity
Start-Sleep 12   # 等候完整加载

# 截登录页
& $adb -s $dev shell screencap -p /data/local/tmp/sc_a.png
Start-Sleep 3
& $adb -s $dev pull /data/local/tmp/sc_a.png "E:\code\c++\UnityRTS\sc_a.png"
Write-Host "=A= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_a.png'){'OK'}else{'FAIL'})"

# 横屏1920x1080 "游客登录"约在 (960, 650)
& $adb -s $dev shell input tap 960 650
Start-Sleep 10

& $adb -s $dev shell screencap -p /data/local/tmp/sc_b.png
Start-Sleep 3
& $adb -s $dev pull /data/local/tmp/sc_b.png "E:\code\c++\UnityRTS\sc_b.png"
Write-Host "=B= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_b.png'){'OK'}else{'FAIL'})"

# 大厅"立即匹配"按钮
# QuickCard anchor(0.3,0.46) in HallPanel(1280x652), btn anchor(0.5,0.18) in card(300x150)
# device x = 0.3*1280*1.5=576, device y from top = 1080-252*1.5=702
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 576 702
Write-Host "Tapped Quick Match card"
Start-Sleep 3   # 等地图面板弹出

# 截图地图选择面板
& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/sc_c.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/sc_c.png "E:\code\c++\UnityRTS\sc_c.png"
Write-Host "=C= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_c.png'){'OK'}else{'FAIL'})"

# 点"开始匹配"按钮
# StartMatchButton anchor(0.5,0.2) in MapPanel(1280x652)
# device x=960, device y from top = 1080-0.2*652*1.5=885
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 960 885
Write-Host "Tapped Start Match"
Start-Sleep 9   # 5s单机兜底+4s场景加载

# 截游戏场景（初始）
& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/sc_d.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/sc_d.png "E:\code\c++\UnityRTS\sc_d.png"
Write-Host "=D= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_d.png'){'OK'}else{'FAIL'})"

# 等待 AI 第一波（30s），截图看单位颜色
Write-Host "Waiting 30s for AI first wave..."
Start-Sleep 30
& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/sc_f.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/sc_f.png "E:\code\c++\UnityRTS\sc_f.png"
Write-Host "=F= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_f.png'){'OK'}else{'FAIL'})"

# 点击兵营（绿色建筑右侧，约 device (1367, 553)）
& $adb connect $dev | Out-Null
& $adb -s $dev shell input tap 1367 553
Write-Host "Tapped Barracks"
Start-Sleep 2

# 截图建筑面板（确认生产按钮名称）
& $adb connect $dev | Out-Null
& $adb -s $dev shell screencap -p /data/local/tmp/sc_e.png
Start-Sleep 1
& $adb -s $dev pull /data/local/tmp/sc_e.png "E:\code\c++\UnityRTS\sc_e.png"
Write-Host "=E= $(if(Test-Path 'E:\code\c++\UnityRTS\sc_e.png'){'OK'}else{'FAIL'})"
