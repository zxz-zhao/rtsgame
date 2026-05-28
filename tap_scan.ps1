$adb = "G:\Program Files\Netease\MuMu\nx_main\adb.exe"
$prevSize = 123276
$found = $false
$xs = @(620, 660, 700, 740)
$ys = @(680, 700, 720, 740, 760)
foreach ($y in $ys) {
    foreach ($x in $xs) {
        & $adb -s emulator-5554 shell input tap $x $y
        Start-Sleep -Milliseconds 800
        & $adb -s emulator-5554 shell screencap -p /sdcard/t.png | Out-Null
        & $adb -s emulator-5554 pull /sdcard/t.png "E:\code\c++\UnityRTS\t.png" | Out-Null
        $sz = (Get-Item "E:\code\c++\UnityRTS\t.png").Length
        Write-Host "tap($x,$y) size=$sz"
        if ($sz -ne $prevSize) {
            Write-Host "*** Screen changed! Hit ($x,$y) ***"
            $found = $true
            break
        }
    }
    if ($found) { break }
}
if (-not $found) { Write-Host "No hit found" }
