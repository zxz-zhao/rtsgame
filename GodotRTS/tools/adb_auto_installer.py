import subprocess
import time
import sys

APK_PATH = "Build/Android/GodotRTS-debug.apk"
PACKAGE_NAME = "com.game.godotrts"
ACTIVITY_NAME = "com.godot.game.GodotApp"

print("[ADB-AutoInstaller] 监听 Android 设备/模拟器连接中 (每 3 秒检测一次)...")

while True:
    try:
        res = subprocess.run(["adb", "devices"], capture_output=True, text=True, timeout=10)
        lines = [line.strip() for line in res.stdout.splitlines() if line.strip()]
        devices = []
        for line in lines[1:]: # Skip "List of devices attached"
            parts = line.split()
            if len(parts) >= 2 and parts[1] == "device":
                devices.append(parts[0])
        
        if devices:
            target = devices[0]
            print(f"[ADB-AutoInstaller] 发现在线 Android 设备: {target}！正在开始自动化安装测试...")
            
            install_cmd = ["adb", "-s", target, "install", "-r", APK_PATH]
            print(f"[ADB-AutoInstaller] 执行: {' '.join(install_cmd)}")
            install_res = subprocess.run(install_cmd, capture_output=True, text=True, timeout=300)
            print("[ADB-AutoInstaller] 安装输出:\n", install_res.stdout)
            if install_res.stderr:
                print("[ADB-AutoInstaller] 警告/错误:\n", install_res.stderr)
            
            if "Success" in install_res.stdout:
                print(f"[ADB-AutoInstaller] ✅ 安装成功！正在拉起游戏主进程: {PACKAGE_NAME}/{ACTIVITY_NAME}...")
                launch_cmd = ["adb", "-s", target, "shell", "am", "start", "-n", f"{PACKAGE_NAME}/{ACTIVITY_NAME}"]
                launch_res = subprocess.run(launch_cmd, capture_output=True, text=True, timeout=30)
                print("[ADB-AutoInstaller] 启动输出:\n", launch_res.stdout)
                print("[ADB-AutoInstaller] 🚀 游戏已成功在设备上启动运行！")
                break
            else:
                print("[ADB-AutoInstaller] 安装未能确认 Success，5 秒后重试...")
                time.sleep(5)
    except Exception as ex:
        print(f"[ADB-AutoInstaller] 检测异常: {ex}")
    
    time.sleep(3)
