import subprocess
import time
import os
import sys

# Ensure clean slate
subprocess.run(["powershell", "-Command", "Stop-Process -Name '*DeepSeek*' -Force -ErrorAction SilentlyContinue"], capture_output=True)

si = subprocess.STARTUPINFO()
si.lpDesktop = r"WinSta0\Default"

exe_path = r"H:\deepseek\DeepSeek Harness.exe"
print(f"Starting {exe_path} on WinSta0\\Default as daemon...")

p = subprocess.Popen(
    [exe_path],
    startupinfo=si
)

print(f"DeepSeek Harness main process started with PID: {p.pid}")
sys.stdout.flush()

try:
    code = p.wait()
    print(f"DeepSeek Harness exited with code: {code}")
except KeyboardInterrupt:
    print("Daemon stopped by interrupt")
