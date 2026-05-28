# ===========================================================
# UnityRTS 全自动构建 + 部署到 MuMu 脚本
# 使用方法: powershell -ExecutionPolicy Bypass -File auto_build.ps1
# ===========================================================

$UnityExe   = "E:\Tuanjie Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
$ProjectDir = "E:\code\c++\UnityRTS"
$LogFile    = "$ProjectDir\build_batch.log"
$ApkPath    = "$ProjectDir\Build\Android\UnityRTS.apk"
$AdbPath    = "G:\Program Files\Netease\MuMu\nx_device\12.0\shell\adb.exe"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "  星火RTS 全自动构建 + MuMu 部署" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# ── Step 1: 关闭现有 Unity 实例 ──────────────────────────
$unityProcs = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($unityProcs) {
    Write-Host "`n[1/5] 关闭当前 Unity Editor..." -ForegroundColor Yellow
    $unityProcs | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 3
    # 若还在运行则强制关闭
    $still = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
    if ($still) { $still | Stop-Process -Force }
    Start-Sleep -Seconds 2
    Write-Host "  Unity 已关闭" -ForegroundColor Green
} else {
    Write-Host "`n[1/5] Unity 未运行，跳过关闭步骤" -ForegroundColor Gray
}

# ── Step 2: 清理旧日志 ────────────────────────────────────
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
# 删除 BUILD_NOW 触发文件（避免重复触发）
$triggerFile = "$ProjectDir\Assets\BUILD_NOW"
if (Test-Path $triggerFile) { Remove-Item $triggerFile -Force }

# ── Step 3: Unity 批处理模式构建 ──────────────────────────
Write-Host "`n[2/5] 启动 Unity 批处理构建 (BuildAll.RunAll)..." -ForegroundColor Yellow
Write-Host "  日志: $LogFile" -ForegroundColor Gray

$buildArgs = @(
    "-batchmode",
    "-quit",
    "-projectPath", "`"$ProjectDir`"",
    "-executeMethod", "BuildAll.RunAll",
    "-logFile", "`"$LogFile`""
)

$proc = Start-Process -FilePath $UnityExe -ArgumentList $buildArgs -PassThru -NoNewWindow
Write-Host "  Unity PID: $($proc.Id)，等待构建完成..." -ForegroundColor Gray

# 实时显示日志尾部
$lastLine = 0
while (!$proc.HasExited) {
    Start-Sleep -Seconds 5
    if (Test-Path $LogFile) {
        $lines = Get-Content $LogFile -ErrorAction SilentlyContinue
        if ($lines -and $lines.Count -gt $lastLine) {
            $lines[$lastLine..($lines.Count-1)] | Where-Object {
                $_ -match "^\[|error|warning|✅|❌|Building|Packaging|SUCCESS|FAILED" -and
                $_ -notmatch "^(?:Mono|Managed|Used|Burst|Loading)" 
            } | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            $lastLine = $lines.Count
        }
    }
}

$exitCode = $proc.ExitCode
Write-Host "`n  Unity 退出码: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

# ── Step 4: 验证 APK ──────────────────────────────────────
Write-Host "`n[3/5] 验证 APK..." -ForegroundColor Yellow
if (!(Test-Path $ApkPath)) {
    Write-Host "  ❌ APK 不存在: $ApkPath" -ForegroundColor Red
    Write-Host "  查看完整日志: $LogFile" -ForegroundColor Yellow

    # 显示最后50行日志帮助诊断
    if (Test-Path $LogFile) {
        Write-Host "`n--- 日志末尾 ---" -ForegroundColor DarkGray
        Get-Content $LogFile -Tail 50 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    }
    exit 1
}

$apkItem = Get-Item $ApkPath
Write-Host "  ✅ APK: $([math]::Round($apkItem.Length/1MB,1))MB  时间: $($apkItem.LastWriteTime)" -ForegroundColor Green

# ── Step 5: 安装到 MuMu ───────────────────────────────────
Write-Host "`n[4/5] 检查 MuMu ADB 连接..." -ForegroundColor Yellow
if (!(Test-Path $AdbPath)) {
    Write-Host "  ❌ ADB 未找到: $AdbPath" -ForegroundColor Red
    exit 1
}

# 连接 MuMu
& $AdbPath connect 127.0.0.1:16384 2>&1 | Out-Null
Start-Sleep -Seconds 1

$devices = & $AdbPath devices 2>&1
Write-Host "  设备列表: $($devices -join ' | ')" -ForegroundColor Gray

if ($devices -notmatch "device") {
    Write-Host "  ❌ MuMu 未连接，请先启动模拟器" -ForegroundColor Red
    exit 1
}

Write-Host "`n[5/5] 安装 APK 到 MuMu..." -ForegroundColor Yellow
$installResult = & $AdbPath -s 127.0.0.1:16384 install -r $ApkPath 2>&1
Write-Host "  $installResult" -ForegroundColor $(if ($installResult -match "Success") { "Green" } else { "Yellow" })

# 启动 App
Write-Host "`n  启动游戏..." -ForegroundColor Yellow
& $AdbPath -s 127.0.0.1:16384 shell am start -n "com.DefaultCompany.UnityRTS/com.unity3d.player.UnityPlayerActivity" 2>&1 | Out-Null

Write-Host "`n=====================================================" -ForegroundColor Cyan
Write-Host "  ✅ 全流程完成！游戏已在 MuMu 中启动" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan

# Reopen Unity Editor
Write-Host "`nReopening Unity Editor..." -ForegroundColor Gray
Start-Process -FilePath $UnityExe -ArgumentList ('-projectPath', $ProjectDir)

