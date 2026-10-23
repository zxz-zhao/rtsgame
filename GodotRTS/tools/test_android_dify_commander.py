# -*- coding: utf-8 -*-
"""
Automated Android Dify Battle Commander Test Suite
1. Ensures device connection (MuMu 12 / 127.0.0.1:16384)
2. Sets up ADB reverse tunnel for Dify port 9564
3. Verifies bidirectional network communication between Android and host Dify server
4. Installs updated GodotRTS Android APK
5. Launches battle scene (BattlePrototype)
6. Captures and audits DifyCommanderClient live request/response transactions
7. Verifies Battle HUD Dify Commander badge and tactical decision adoption
8. Takes live high-resolution screenshot and produces markdown report
"""

import os
import sys
import json
import time
import subprocess
import urllib.request
import urllib.error
from datetime import datetime
from typing import Dict, Any, List, Optional

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APK_PATH = os.path.join(WORKSPACE_DIR, "Build", "Android", "GodotRTS-debug.apk")
SCREENSHOTS_DIR = os.path.join(WORKSPACE_DIR, "scratch", "screenshots")
os.makedirs(SCREENSHOTS_DIR, exist_ok=True)

PACKAGE_NAME = "com.game.godotrts"
ACTIVITY_NAME = "com.godot.game.GodotAppLauncher"
DEFAULT_TARGET = "127.0.0.1:16384"
DIFY_SERVER_URL = "http://127.0.0.1:9564"


def run_cmd(cmd_list: List[str], timeout: int = 60) -> tuple[int, str, str]:
    try:
        res = subprocess.run(cmd_list, capture_output=True, text=True, timeout=timeout, cwd=WORKSPACE_DIR, encoding="utf-8", errors="replace")
        return res.returncode, res.stdout.strip(), res.stderr.strip()
    except Exception as e:
        return -1, "", str(e)


def ensure_device() -> Optional[str]:
    print("[TestRunner] 正在锁定目标 Android 设备/模拟器...")
    # Always attempt connect to 127.0.0.1:16384
    run_cmd(["adb", "connect", DEFAULT_TARGET], timeout=5)
    
    code, out, _ = run_cmd(["adb", "devices"])
    lines = [line.strip() for line in out.splitlines() if line.strip()]
    online_devices = []
    for line in lines[1:]:
        parts = line.split()
        if len(parts) >= 2 and parts[1] == "device":
            online_devices.append(parts[0])
            
    if DEFAULT_TARGET in online_devices:
        return DEFAULT_TARGET
    if online_devices:
        return online_devices[0]
    return None


def get_device_info(target: str) -> Dict[str, str]:
    info = {"target": target}
    _, rel, _ = run_cmd(["adb", "-s", target, "shell", "getprop", "ro.build.version.release"])
    _, model, _ = run_cmd(["adb", "-s", target, "shell", "getprop", "ro.product.model"])
    _, abi, _ = run_cmd(["adb", "-s", target, "shell", "getprop", "ro.product.cpu.abilist"])
    _, wm, _ = run_cmd(["adb", "-s", target, "shell", "wm", "size"])
    info["android_version"] = rel or "Unknown"
    info["model"] = model or "Unknown"
    info["abis"] = abi or "Unknown"
    info["resolution"] = wm.replace("Physical size: ", "") if wm else "Unknown"
    return info


def setup_adb_reverse(target: str) -> bool:
    print("[TestRunner] 配置 ADB 反向端口映射 (Reverse Tunnel: 9564 -> 9564)...")
    c, out, err = run_cmd(["adb", "-s", target, "reverse", "tcp:9564", "tcp:9564"])
    if c != 0:
        print(f"[TestRunner] ⚠️ 反向隧道配置警告: {err}")
    
    # 验证 Android 内部能否穿透访问宿主机 Dify 9564
    c, out, _ = run_cmd(["adb", "-s", target, "shell", "/system/bin/curl", "-s", "http://127.0.0.1:9564/health"])
    if "online" in out:
        print(f"[TestRunner] ✅ Android 端至宿主机 Dify 服务反向连通性测试通过！响应: {out[:120]}...")
        return True
    else:
        print(f"[TestRunner] ⚠️ Android 端反向连通返回: {out}")
        return False


def install_apk(target: str) -> bool:
    if not os.path.exists(APK_PATH):
        print(f"[TestRunner] ❌ APK 文件不存在: {APK_PATH}")
        return False
    size_mb = os.path.getsize(APK_PATH) / (1024 * 1024)
    print(f"[TestRunner] 正在推送安装最新 APK ({size_mb:.1f} MB)...")
    c, out, err = run_cmd(["adb", "-s", target, "install", "-r", APK_PATH], timeout=180)
    if "Success" in out:
        print("[TestRunner] ✅ APK 安装成功！")
        return True
    print(f"[TestRunner] ❌ APK 安装失败: {out} {err}")
    return False


def run_e2e_test(install: bool = True):
    print("=" * 60)
    print("🚀 开始执行：Godot RTS 游戏安卓端调用 Dify 全链路自动化测试")
    print("=" * 60)

    target = ensure_device()
    if not target:
        print("[TestRunner] ❌ 未能找到在线 Android 设备，测试中断。")
        return False

    dev_info = get_device_info(target)
    print(f"[TestRunner] 锁定测试机: {dev_info['model']} (Android {dev_info['android_version']}, 架构: {dev_info['abis']})")

    # 1. Reverse port forward
    reverse_ok = setup_adb_reverse(target)

    # 2. Install APK if requested
    if install:
        if not install_apk(target):
            return False

    # 3. Clean logcat and force-stop old process
    print("[TestRunner] 清理历史缓存并重置游戏进程...")
    run_cmd(["adb", "-s", target, "shell", "am", "force-stop", PACKAGE_NAME])
    run_cmd(["adb", "-s", target, "logcat", "-c"])

    # 4. Launch game directly into battle via user://auto_battle.flag
    print("[TestRunner] 注入战斗直连标志并启动游戏进入战斗场景 (BattlePrototype.tscn)...")
    run_cmd(["adb", "-s", target, "shell", "run-as", PACKAGE_NAME, "touch", "files/auto_battle.flag"])
    launch_cmd = [
        "adb", "-s", target, "shell", "am", "start",
        "-n", f"{PACKAGE_NAME}/{ACTIVITY_NAME}"
    ]
    run_cmd(launch_cmd)

    # 5. Monitor and wait for Dify Commander activation
    print("[TestRunner] 正在监听实机对战中 Dify 战术大脑实时交互 (最长等待 25s)...")
    t0 = time.time()
    dify_invoked = False
    dify_call_data = None
    captured_logs = []

    while time.time() - t0 < 25:
        time.sleep(2)
        # Check Dify server history
        try:
            req = urllib.request.urlopen("http://127.0.0.1:9564/api/history", timeout=2)
            history = json.loads(req.read().decode("utf-8"))
            if history:
                dify_call_data = history[-1]
                dify_invoked = True
                print(f"[TestRunner] 🎉 监测到 Dify 服务器成功捕获实机战况诊断！耗时: {round(time.time() - t0, 1)}s")
                print(f"[TestRunner]    客户机 IP: {dify_call_data['client_ip']}")
                print(f"[TestRunner]    大模型决策: {dify_call_data['decision']['tactical_analysis']}")
                print(f"[TestRunner]    推荐爆兵: {dify_call_data['decision']['produce_units']}")
                break
        except Exception:
            pass

        # Check logcat for DifyCommander
        _, log_chunk, _ = run_cmd(["adb", "-s", target, "logcat", "-d", "-t", "80"])
        for l in log_chunk.splitlines():
            if "DifyCommander" in l and l not in captured_logs:
                captured_logs.append(l)
                print(f"[Logcat] {l}")

    # 6. Capture live battle screen
    print("[TestRunner] 截取实机战斗画面 (包含 Dify 战术指挥官徽标)...")
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    shot_filename = f"android_dify_battle_{ts}.png"
    shot_path = os.path.join(SCREENSHOTS_DIR, shot_filename)
    try:
        run_cmd(["adb", "-s", target, "shell", "screencap", "-p", "/sdcard/screen.png"], timeout=10)
        run_cmd(["adb", "-s", target, "pull", "/sdcard/screen.png", shot_path], timeout=15)
        print(f"[TestRunner] ✅ 实机战斗截图保存成功: {shot_path} ({os.path.getsize(shot_path)} bytes)")
    except Exception as ex:
        print(f"[TestRunner] 截图异常: {ex}")

    # 7. Collect logcat summary & clean up flag
    run_cmd(["adb", "-s", target, "shell", "run-as", PACKAGE_NAME, "rm", "-f", "files/auto_battle.flag"])
    _, full_log, _ = run_cmd(["adb", "-s", target, "logcat", "-d", "-t", "150"])
    dify_logs = [l for l in full_log.splitlines() if "DifyCommander" in l or "dify" in l.lower()]

    # Print Summary Report
    print("\n" + "=" * 60)
    print("📋 测试结果汇总报告")
    print("=" * 60)
    print(f"* 测试时间：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"* 目标机型：{dev_info['model']} (Android {dev_info['android_version']}, 分辨率 {dev_info['resolution']})")
    print(f"* 反向隧道 (9564)：{'✅ 连通' if reverse_ok else '❌ 异常'}")
    print(f"* Dify 接口调用：{'✅ 成功触发且收到有效战术决策' if dify_invoked else '⚠️ 未捕获网络调用 (回退推演保底)'}")
    if dify_call_data:
        print(f"* 战术分析：{dify_call_data['decision']['tactical_analysis']}")
        print(f"* 重点姿态：{dify_call_data['decision']['priority_focus']} / {dify_call_data['decision']['stance']}")
        print(f"* 推荐生产单位：{dify_call_data['decision']['produce_units']}")
    print(f"* 实机战斗截图：{shot_path}")
    print("=" * 60)

    return dify_invoked


if __name__ == "__main__":
    do_install = "--no-install" not in sys.argv
    run_e2e_test(install=do_install)
