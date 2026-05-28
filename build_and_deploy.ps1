#requires -Version 5.1
chcp 65001 | Out-Null
# Unity 已打开 → BUILD_NOW 触发构建；否则批处理 BuildAll。安装逻辑与 PackInstallTest 共用 RtsDeployCommon。

$ErrorActionPreference = 'Stop'
$ProjectDir  = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Common      = Join-Path $ProjectDir 'Tools\RtsDeployCommon.ps1'
if (-not (Test-Path $Common)) { Write-Error "缺少: $Common"; exit 1 }
. $Common

$TriggerFile = Join-Path $ProjectDir 'Assets\BUILD_NOW'
$ApkPath     = Join-Path $ProjectDir 'Build\Android\UnityRTS.apk'
$UnityExe    = Rts_GetDefaultUnityPath
$AdbPath     = Rts_FindAdb
if (-not $AdbPath) { Write-Error 'adb 未找到，请设置 ADB_PATH 或安装 SDK platform-tools'; exit 1 }
if (-not (Test-Path $UnityExe)) { Write-Error "Unity 未找到: $UnityExe"; exit 1 }

Write-Host '=== [0/3] 后端服务（8080）===' -ForegroundColor Cyan
[void](Rts_EnsureBackendServer -ProjectRoot $ProjectDir)

$TimeoutSec = 1800
Write-Host '=== UnityRTS Build + Deploy ===' -ForegroundColor Cyan

$unityProc = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue

if (-not $unityProc) {
    Write-Host '[WARN] Unity 未运行 — 使用批处理 BuildAll.RunAll' -ForegroundColor Yellow
    $LogFile = Join-Path $ProjectDir 'build_batch.log'
    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    $bArgs = @('-batchmode', '-quit', '-nographics', '-projectPath', $ProjectDir, '-executeMethod', 'BuildAll.RunAll', '-logFile', $LogFile)
    $proc = Start-Process -FilePath $UnityExe -ArgumentList $bArgs -PassThru -Wait -NoNewWindow
    Write-Host ('  Exit: ' + $proc.ExitCode) -ForegroundColor $(if ($proc.ExitCode -eq 0) { 'Green' } else { 'Red' })
    if ($proc.ExitCode -ne 0) {
        if (Test-Path $LogFile) { Get-Content $LogFile -Tail 40 }
        exit $proc.ExitCode
    }
} else {
    Write-Host ('[1/3] Unity PID=' + $unityProc.Id + ' — 写入 BUILD_NOW ...') -ForegroundColor Yellow
    $apkBefore = if (Test-Path $ApkPath) { (Get-Item $ApkPath).LastWriteTime } else { [datetime]::MinValue }
    New-Item -Path $TriggerFile -ItemType File -Force | Out-Null
    Write-Host ('  最长等待 ' + $TimeoutSec + 's ...') -ForegroundColor Gray

    Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices; using System.Text;
public class BDHelper {
    [DllImport("user32")] public static extern bool EnumWindows(EnumWindowsProc c, IntPtr l);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    [DllImport("user32")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int m);
    [DllImport("user32")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32")] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
    [DllImport("user32")] public static extern bool SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
}
'@ -ErrorAction SilentlyContinue

    $elapsed = 0
    $built = $false
    while ($elapsed -lt $TimeoutSec) {
        Start-Sleep -Seconds 10
        $elapsed += 10

        $wins = @{}
        [BDHelper]::EnumWindows({ param($h,$l)
            if ([BDHelper]::IsWindowVisible($h)) {
                $s = New-Object Text.StringBuilder 100
                [BDHelper]::GetWindowText($h,$s,100) | Out-Null
                $t = $s.ToString(); if ($t -eq "配置完成") { $wins[$h.ToInt32()] = $t }
            }; $true
        }, [IntPtr]::Zero) | Out-Null
        foreach ($kv in $wins.GetEnumerator()) {
            $btn = [BDHelper]::FindWindowEx([IntPtr]$kv.Key, [IntPtr]::Zero, "Button", "确定")
            if ($btn -ne [IntPtr]::Zero) {
                [BDHelper]::SendMessage($btn, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
                Write-Host ('  [' + $elapsed + 's] 已尝试关闭「配置完成」') -ForegroundColor DarkYellow
            }
        }

        if (Test-Path $ApkPath) {
            if ((Get-Item $ApkPath).LastWriteTime -gt $apkBefore) {
                Write-Host ('  构建完成，耗时 ' + $elapsed + 's') -ForegroundColor Green
                $built = $true
                break
            }
        }
        if (($elapsed % 60) -eq 0) {
            $logT = (Get-Item "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -ErrorAction SilentlyContinue).LastWriteTime
            Write-Host ('  [' + $elapsed + 's] 等待中... Editor.log=' + $logT) -ForegroundColor DarkGray
        }
    }
    if (-not $built) {
        Write-Host 'TIMEOUT: APK 未更新' -ForegroundColor Red
        exit 1
    }
}

Write-Host '[2/3] 校验 APK ...' -ForegroundColor Yellow
if (-not (Test-Path $ApkPath)) { Write-Host '未找到 APK!' -ForegroundColor Red; exit 1 }
$apk = Get-Item $ApkPath
Write-Host ('  OK: ' + [math]::Round($apk.Length/1MB,1) + ' MB  ' + $apk.LastWriteTime) -ForegroundColor Green

Write-Host '[3/3] 安装到模拟器 ...' -ForegroundColor Yellow
Rts_ConnectAdbEmulators -AdbExe $AdbPath
$serial = Rts_PickAdbSerial -AdbExe $AdbPath
if (-not $serial) {
    Write-Host '未检测到 adb 设备（请先打开 MuMu）。' -ForegroundColor Red
    & $AdbPath devices
    exit 1
}
Write-Host "  使用设备: $serial" -ForegroundColor Gray
if (-not (Rts_InstallAndLaunch -AdbExe $AdbPath -Serial $serial -ApkPath $ApkPath)) { exit 1 }
Write-Host '=== Done ===' -ForegroundColor Cyan
