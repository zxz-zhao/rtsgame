import os
import sys
import time
import socket
import subprocess
import argparse
import ctypes
from ctypes import wintypes

kernel32 = ctypes.windll.kernel32
user32 = ctypes.windll.user32

TH32CS_SNAPPROCESS = 0x00000002
PROCESS_TERMINATE = 0x0001
SW_RESTORE = 9

class PROCESSENTRY32(ctypes.Structure):
    _fields_ = [
        ('dwSize', wintypes.DWORD),
        ('cntUsage', wintypes.DWORD),
        ('th32ProcessID', wintypes.DWORD),
        ('th32DefaultHeapID', ctypes.POINTER(ctypes.c_ulong)),
        ('th32ModuleID', wintypes.DWORD),
        ('cntThreads', wintypes.DWORD),
        ('th32ParentProcessID', wintypes.DWORD),
        ('pcPriClassBase', wintypes.LONG),
        ('dwFlags', wintypes.DWORD),
        ('szExeFile', ctypes.c_char * 260)
    ]

def log(msg: str):
    timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{timestamp}] {msg}"
    print(line)
    try:
        log_dir = r"C:\Users\Administrator\.dsh"
        os.makedirs(log_dir, exist_ok=True)
        with open(os.path.join(log_dir, "supervisor.log"), "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except Exception:
        pass

def is_port_listening(port: int, host: str = "127.0.0.1") -> bool:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.settimeout(1.0)
        return s.connect_ex((host, port)) == 0

def ensure_services():
    # 1. ChatOS Server (Port 8000)
    if not is_port_listening(8000):
        log("ChatOS server (port 8000) is down. Launching H:\\api\\server.py...")
        subprocess.Popen(
            [r"E:\python13\python.exe", r"H:\api\server.py"],
            cwd=r"H:\api",
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP
        )
        for _ in range(15):
            time.sleep(0.4)
            if is_port_listening(8000):
                log("ChatOS server is now UP on port 8000.")
                break
        else:
            log("WARNING: ChatOS server did not respond within timeout.")
    else:
        log("ChatOS server is healthy (port 8000).")

    # 2. New-API Gateway (Port 3000)
    if not is_port_listening(3000):
        log("New-API gateway (port 3000) is down. Launching new-api.exe...")
        new_api_path = r"E:\code\c++\UnityRTS\GodotRTS\tools\new-api\new-api.exe"
        subprocess.Popen(
            [new_api_path, "--port", "3000"],
            cwd=os.path.dirname(new_api_path),
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP
        )
        for _ in range(15):
            time.sleep(0.4)
            if is_port_listening(3000):
                log("New-API gateway is now UP on port 3000.")
                break
        else:
            log("WARNING: New-API gateway did not respond within timeout.")
    else:
        log("New-API gateway is healthy (port 3000).")

def get_deepseek_pids():
    pids = []
    hSnap = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    if hSnap == -1:
        return pids
    pe = PROCESSENTRY32()
    pe.dwSize = ctypes.sizeof(PROCESSENTRY32)
    if kernel32.Process32First(hSnap, ctypes.byref(pe)):
        while True:
            exe = pe.szExeFile.decode('utf-8', errors='ignore')
            if "deepseek" in exe.lower():
                pids.append(pe.th32ProcessID)
            if not kernel32.Process32Next(hSnap, ctypes.byref(pe)):
                break
    kernel32.CloseHandle(hSnap)
    return pids

def get_visible_windows(pids):
    if not pids:
        return []
    pid_set = set(pids)
    windows = []
    WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    def enum_proc(hwnd, lparam):
        pid = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        if pid.value in pid_set:
            if user32.IsWindowVisible(hwnd):
                rect = wintypes.RECT()
                user32.GetWindowRect(hwnd, ctypes.byref(rect))
                width = rect.right - rect.left
                height = rect.bottom - rect.top
                if width > 100 and height > 100:
                    length = user32.GetWindowTextLengthW(hwnd)
                    buf = ctypes.create_unicode_buffer(length + 1)
                    user32.GetWindowTextW(hwnd, buf, length + 1)
                    windows.append((hwnd, pid.value, buf.value))
        return True

    cb = WNDENUMPROC(enum_proc)
    user32.EnumWindows(cb, 0)
    return windows

def kill_pids(pids):
    for pid in pids:
        hProc = kernel32.OpenProcess(PROCESS_TERMINATE, False, pid)
        if hProc:
            kernel32.TerminateProcess(hProc, 1)
            kernel32.CloseHandle(hProc)
            log(f"Terminated zombie process PID {pid}")

def launch_dsh(profile="default"):
    ensure_services()

    # Check existing DeepSeek processes
    pids = get_deepseek_pids()
    if pids:
        vis_windows = get_visible_windows(pids)
        if vis_windows:
            log(f"Found existing active window ({len(vis_windows)} visible). Bringing to foreground...")
            for hwnd, pid, title in vis_windows:
                user32.ShowWindow(hwnd, SW_RESTORE)
                user32.SetForegroundWindow(hwnd)
            log("Focused existing active window. Done.")
            return
        else:
            log(f"Found {len(pids)} stale/zombie DeepSeek processes without windows. Cleaning up...")
            kill_pids(pids)
            time.sleep(1)

    # Prepare environment
    env = os.environ.copy()
    env["DEEPSEEK_BASE_URL"] = "http://127.0.0.1:3000"
    env["DEEPSEEK_API_KEY"] = "pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q"

    exe_path = r"H:\deepseek\DeepSeek Harness.exe"
    args = [exe_path]

    if profile == "acc2":
        env["DSH_HOME"] = r"C:\Users\Administrator\.dsh-acc2"
        args.extend([
            r'--user-data-dir=C:\Users\Administrator\.dsh-acc2\userData',
            r'--dsh-home=C:\Users\Administrator\.dsh-acc2'
        ])
    else:
        env["DSH_HOME"] = r"C:\Users\Administrator\.dsh"

    log(f"Spawning DeepSeek Harness [{profile}]: {exe_path} ...")
    proc = subprocess.Popen(
        args,
        env=env,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=subprocess.CREATE_NEW_PROCESS_GROUP
    )
    log(f"DeepSeek Harness launched successfully with PID {proc.pid}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="DeepSeek Harness Supervisor")
    parser.add_argument("--profile", default="default", choices=["default", "acc2"], help="Profile to launch")
    parser.add_argument("--kill", action="store_true", help="Force kill all DeepSeek processes")
    args = parser.parse_args()

    if args.kill:
        pids = get_deepseek_pids()
        log(f"Force killing {len(pids)} DeepSeek processes...")
        kill_pids(pids)
    else:
        launch_dsh(args.profile)
