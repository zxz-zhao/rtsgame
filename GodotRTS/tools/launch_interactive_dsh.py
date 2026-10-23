import subprocess
import time
import os
import sys
import socket

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

def is_port_listening(port: int) -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.settimeout(1.0)
        return s.connect_ex(("127.0.0.1", port)) == 0

print("========================================")
print("[DSH Launcher] Starting DeepSeek Harness via ChatOS & New-API Bridge")
print("========================================")

# 1. 确保 ChatOS 8000 端口服务在线
if not is_port_listening(8000):
    print("[1/3] Starting ChatOS reverse proxy on port 8000...")
    subprocess.Popen(
        ["E:\\python13\\python.exe", "H:\\api\\server.py"],
        cwd="H:\\api",
        creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
    )
    for _ in range(10):
        time.sleep(0.5)
        if is_port_listening(8000):
            break
    print("  -> ChatOS reverse proxy ready (port 8000)")
else:
    print("[1/3] ChatOS reverse proxy already running (port 8000)")

# 2. 确保 New-API 3000 端口网关在线
if not is_port_listening(3000):
    print("[2/3] Starting New-API protocol gateway on port 3000...")
    new_api_path = r"E:\code\c++\UnityRTS\GodotRTS\tools\new-api\new-api.exe"
    subprocess.Popen(
        [new_api_path, "--port", "3000"],
        cwd=os.path.dirname(new_api_path),
        creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
    )
    for _ in range(10):
        time.sleep(0.5)
        if is_port_listening(3000):
            break
    print("  -> New-API gateway ready (port 3000)")
else:
    print("[2/3] New-API gateway already running (port 3000)")

# 3. 清理残留僵尸进程，释放单实例锁
print("[3/3] Terminating any stale/hung DeepSeek processes to release singleton lock...")
subprocess.run(
    ["powershell", "-Command", "Stop-Process -Name '*DeepSeek*' -Force -ErrorAction SilentlyContinue"],
    capture_output=True
)
time.sleep(1)

# 4. 组装环境变量并启动客户端
env = os.environ.copy()
env["DSH_HOME"] = r"C:\Users\Administrator\.dsh"
env["DEEPSEEK_API_KEY"] = "pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q"
env["DEEPSEEK_BASE_URL"] = "http://127.0.0.1:3000"

si = subprocess.STARTUPINFO()
si.lpDesktop = r"WinSta0\Default"

exe_path = r"H:\deepseek\DeepSeek Harness.exe"
print(f"Launching DeepSeek Harness on WinSta0\\Default: {exe_path} ...")

p = subprocess.Popen(
    [exe_path],
    env=env,
    startupinfo=si,
    creationflags=subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP
)

print(f"DeepSeek Harness launched with PID: {p.pid}")
time.sleep(4)

subprocess.run(
    ["powershell", "-Command", "Get-Process | Where-Object { $_.ProcessName -like '*DeepSeek*' } | Select-Object Id, ProcessName, MainWindowTitle, MainWindowHandle, Responding"],
    check=False
)
