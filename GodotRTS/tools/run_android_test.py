# -*- coding: utf-8 -*-
"""
Automated Android Test Engine for Godot 4 RTS (MuMu / Real Device).
Supports autonomous discovery, ADB connection, APK installation,
lifecycle launch verification, crash logcat scanning, and screen capture.
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import time
import subprocess
from datetime import datetime

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APK_PATH = os.path.join(WORKSPACE_DIR, "Build", "Android", "GodotRTS-debug.apk")
SCREENSHOTS_DIR = os.path.join(WORKSPACE_DIR, "scratch", "screenshots")
PACKAGE_NAME = "com.game.godotrts"
ACTIVITY_NAME = "com.godot.game.GodotAppLauncher"
MUMU_MANAGE = r"C:\Program Files\MuMuVMMVbox\Hypervisor\MuMuVMMManage.exe"

def run_cmd(cmd_list, timeout=30):
    try:
        res = subprocess.run(cmd_list, capture_output=True, text=True, timeout=timeout, cwd=WORKSPACE_DIR, encoding="utf-8", errors="replace")
        return res.returncode, res.stdout.strip(), res.stderr.strip()
    except Exception as e:
        return -1, "", str(e)

def find_or_connect_device():
    # 1. Always attempt connecting to standard ports first (MuMu 12 / Nox / LDPlayer)
    for port in [16384, 7555, 5555, 16416]:
        run_cmd(["adb", "connect", f"127.0.0.1:{port}"], timeout=5)

    code, out, _ = run_cmd(["adb", "devices"])
    lines = [line.strip() for line in out.splitlines() if line.strip()]
    for line in lines[1:]:
        parts = line.split()
        if len(parts) >= 2 and parts[1] == "device":
            return parts[0]

    return None

def get_device_info(target):
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

def run_android_test(install=True, launch=True, capture=True):
    os.makedirs(SCREENSHOTS_DIR, exist_ok=True)
    report = {
        "timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "success": False,
        "device": None,
        "install_success": False,
        "running_pid": None,
        "crashes_found": False,
        "screenshot_file": None,
        "screenshot_url": None,
        "logs": []
    }

    print("[AndroidTest] 正在探测目标 Android 设备/模拟器...")
    target = find_or_connect_device()
    if not target:
        msg = "未发现任何在线的 Android 设备或 MuMu 模拟器，请检查模拟器运行状态或 USB 调试连接。"
        print(f"[AndroidTest] {msg}")
        report["message"] = msg
        return report

    report["device"] = get_device_info(target)
    print(f"[AndroidTest] 成功锁定设备: {target} (型号: {report['device']['model']}, Android {report['device']['android_version']})")

    # 1. Install APK if requested
    if install:
        if not os.path.exists(APK_PATH):
            msg = f"未找到待测 APK 产物: {APK_PATH}，请先执行打包命令。"
            print(f"[AndroidTest] {msg}")
            report["message"] = msg
            return report
        
        apk_size_mb = os.path.getsize(APK_PATH) / (1024 * 1024)
        print(f"[AndroidTest] 正在安装 APK ({apk_size_mb:.1f} MB) 到设备 {target}...")
        c, out, err = run_cmd(["adb", "-s", target, "install", "-r", APK_PATH], timeout=180)
        if "Success" in out:
            print("[AndroidTest] ✅ APK 安装成功！")
            report["install_success"] = True
        else:
            print(f"[AndroidTest] ❌ 安装未完成: {out} {err}")
            report["install_success"] = False
            report["message"] = f"APK 安装失败: {out or err}"
            return report
    else:
        report["install_success"] = True

    # 2. Clear logcat and Launch app
    if launch:
        print("[AndroidTest] 清理日志缓存并启动游戏主进程...")
        run_cmd(["adb", "-s", target, "logcat", "-c"])
        launch_target = f"{PACKAGE_NAME}/{ACTIVITY_NAME}"
        c, out, err = run_cmd(["adb", "-s", target, "shell", "am", "start", "-n", launch_target])
        print(f"[AndroidTest] am start 返回: {out}")
        
        # Wait 4 seconds for Godot engine and C# runtime initialization
        time.sleep(4)

        # Check process PID
        _, pid_out, _ = run_cmd(["adb", "-s", target, "shell", "pidof", PACKAGE_NAME])
        if pid_out.strip():
            report["running_pid"] = pid_out.strip()
            print(f"[AndroidTest] ✅ 游戏进程正常运行，PID: {report['running_pid']}")
        else:
            print("[AndroidTest] ⚠️ 未能检测到运行中的游戏进程")

    # 3. Scan Logcat for fatal crashes / exceptions
    _, log_out, _ = run_cmd(["adb", "-s", target, "logcat", "-d", "-t", "200"])
    crash_keywords = ["FATAL EXCEPTION", "SIGSEGV", "Fatal signal", "backtrace:", "terminating with uncaught exception"]
    found_crashes = [line for line in log_out.splitlines() if any(k in line for k in crash_keywords)]
    
    godot_logs = [line for line in log_out.splitlines() if "godot" in line.lower() or "unityrts" in line.lower()]
    report["logs"] = godot_logs[-15:] if godot_logs else log_out.splitlines()[-15:]

    if found_crashes:
        report["crashes_found"] = True
        report["crash_sample"] = found_crashes[:5]
        print(f"[AndroidTest] ❌ 检测到崩溃信号: {found_crashes[0]}")
    else:
        report["crashes_found"] = False
        print("[AndroidTest] ✅ Logcat 扫描完毕，未发现崩溃（SIGSEGV / FATAL EXCEPTION 无触发）！")

    # 4. Take live screenshot
    if capture:
        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        shot_filename = f"android_screen_{ts}.png"
        shot_path = os.path.join(SCREENSHOTS_DIR, shot_filename)
        
        try:
            run_cmd(["adb", "-s", target, "shell", "screencap", "-p", "/sdcard/screen.png"], timeout=10)
            run_cmd(["adb", "-s", target, "pull", "/sdcard/screen.png", shot_path], timeout=15)
            
            if os.path.exists(shot_path) and os.path.getsize(shot_path) > 1024:
                lan_ip = os.environ.get("LAN_HOST", "192.168.1.176")
                report["screenshot_file"] = shot_path
                report["screenshot_url"] = f"http://{lan_ip}:9564/screenshots/{shot_filename}"
                print(f"[AndroidTest] ✅ 实机画面截取成功: {shot_path} ({os.path.getsize(shot_path)} bytes)")
        except Exception as ex:
            print(f"[AndroidTest] 截屏异常: {ex}")

    # Final verdict
    report["success"] = (report["install_success"] and not report["crashes_found"])
    
    # Generate Markdown Summary
    proc_status = f"✅ 存活 (PID: {report['running_pid']})" if report['running_pid'] else "⚠️ 未运行"
    md_lines = [
        "### 📱 Android 自动化实机测试报告",
        f"* **测试时间**：{report['timestamp']}",
        f"* **测试设备**：{report['device']['model']} (Android {report['device']['android_version']}, 分辨率 {report['device']['resolution']})",
        f"* **设备架构**：{report['device']['abis']}",
        f"* **安装状态**：{'✅ 成功' if report['install_success'] else '❌ 失败'}",
        f"* **游戏进程**：{proc_status}",
        f"* **崩溃诊断**：{'❌ 发现崩溃' if report['crashes_found'] else '✅ 无崩溃 (零 SIGSEGV/零主线程 Abort)'}",
    ]
    if report.get("screenshot_url"):
        md_lines.append(f"\n#### 📸 实机画面截图\n![Android 测试实机截图]({report['screenshot_url']})")
    
    if report["logs"]:
        md_lines.append("\n#### 📜 最新引擎日志片段")
        md_lines.append("```text")
        md_lines.extend(report["logs"][-8:])
        md_lines.append("```")

    report["markdown_report"] = "\n".join(md_lines)
    return report

if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Android Automated Test")
    parser.add_argument("--install", action="store_true", default=None, help="Force install APK")
    parser.add_argument("--no-install", action="store_true", help="Skip install APK")
    args = parser.parse_args()

    do_install = True
    if args.no_install:
        do_install = False
    elif args.install is True:
        do_install = True
    else:
        # Check if package is installed
        c, out, _ = run_cmd(["adb", "shell", "pm", "path", PACKAGE_NAME])
        if PACKAGE_NAME in out:
            print(f"[AndroidTest] 检测到 {PACKAGE_NAME} 已安装在设备上，跳过重新推送安装以节省时间。")
            do_install = False
        else:
            print(f"[AndroidTest] 设备未检测到 {PACKAGE_NAME}，将执行自动安装。")
            do_install = True

    res = run_android_test(install=do_install, launch=True, capture=True)
    print("\n--- TEST SUMMARY ---")
    print(res.get("markdown_report", ""))
