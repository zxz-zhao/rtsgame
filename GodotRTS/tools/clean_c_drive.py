import os
import sys
import shutil
import subprocess

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

def get_c_free_bytes():
    try:
        import ctypes
        free_bytes = ctypes.c_ulonglong(0)
        total_bytes = ctypes.c_ulonglong(0)
        total_free_bytes = ctypes.c_ulonglong(0)
        ctypes.windll.kernel32.GetDiskFreeSpaceExW(
            ctypes.c_wchar_p("C:\\"),
            ctypes.byref(free_bytes),
            ctypes.byref(total_bytes),
            ctypes.byref(total_free_bytes)
        )
        return free_bytes.value, total_bytes.value
    except Exception:
        return 0, 0

def clean_dir_contents(dir_path: str) -> tuple[int, int]:
    """安全清理目录下的文件和子文件夹，跳过正在被占用锁定的文件"""
    freed = 0
    cleaned_count = 0
    if not os.path.exists(dir_path):
        return 0, 0

    try:
        entries = os.listdir(dir_path)
    except Exception:
        return 0, 0

    for item in entries:
        item_path = os.path.join(dir_path, item)
        # 保护 IDE 自身和当前会话相关关键临时目录（如果有）
        if "antigravity" in item.lower():
            continue
        try:
            if os.path.isfile(item_path) or os.path.islink(item_path):
                sz = os.path.getsize(item_path)
                os.remove(item_path)
                freed += sz
                cleaned_count += 1
            elif os.path.isdir(item_path):
                # 统计子文件夹大小
                for r, d, fs in os.walk(item_path):
                    for f in fs:
                        try:
                            fp = os.path.join(r, f)
                            if not os.path.islink(fp):
                                freed += os.path.getsize(fp)
                                cleaned_count += 1
                        except Exception:
                            pass
                shutil.rmtree(item_path, ignore_errors=True)
        except Exception:
            # 文件正在被进程占用，安全跳过
            pass

    return freed, cleaned_count

def run_cmd(cmd_list, desc=""):
    print(f"-> 正在执行: {desc or ' '.join(cmd_list)} ...", flush=True)
    try:
        res = subprocess.run(cmd_list, capture_output=True, text=True, timeout=60, shell=True)
        return res.returncode == 0
    except Exception as e:
        print(f"   执行异常: {e}", flush=True)
        return False

def main():
    print("==================================================", flush=True)
    print("🧹 C 盘极速深度安全清理程序", flush=True)
    print("==================================================", flush=True)

    free_start, total = get_c_free_bytes()
    print(f"清理前 C 盘可用空间: {free_start / (1024**3):.2f} GB / 总计: {total / (1024**3):.2f} GB\n", flush=True)

    total_freed = 0

    # 1. 清理 AppData\Local\Temp
    temp_dir = os.path.expandvars(r"%LOCALAPPDATA%\Temp")
    print(f"[1/7] 清理用户临时文件夹: {temp_dir} ...", flush=True)
    f1, c1 = clean_dir_contents(temp_dir)
    total_freed += f1
    print(f"      已清理 {c1} 个临时文件，释放 {f1 / (1024*1024):.1f} MB\n", flush=True)

    # 2. 清理 Windows\Temp
    win_temp = r"C:\Windows\Temp"
    print(f"[2/7] 清理系统临时文件夹: {win_temp} ...", flush=True)
    f2, c2 = clean_dir_contents(win_temp)
    total_freed += f2
    print(f"      已清理 {c2} 个系统临时文件，释放 {f2 / (1024*1024):.1f} MB\n", flush=True)

    # 3. 清理系统崩溃转储 CrashDumps
    crash_dir = os.path.expandvars(r"%LOCALAPPDATA%\CrashDumps")
    print(f"[3/7] 清理崩溃转储文件: {crash_dir} ...", flush=True)
    f3, c3 = clean_dir_contents(crash_dir)
    total_freed += f3
    print(f"      已清理 {c3} 个转储文件，释放 {f3 / (1024*1024):.1f} MB\n", flush=True)

    # 4. 清理 Pip 下载缓存
    pip_cache = os.path.expandvars(r"%LOCALAPPDATA%\pip\cache")
    print(f"[4/7] 清理 Python Pip 缓存: {pip_cache} ...", flush=True)
    f4, c4 = clean_dir_contents(pip_cache)
    total_freed += f4
    print(f"      释放 {f4 / (1024*1024):.1f} MB\n", flush=True)

    # 5. 清理 NuGet 全局历史缓存 (使用官方 dotnet 命令)
    print(f"[5/7] 清理 .NET NuGet 历史包缓存 ...", flush=True)
    run_cmd("dotnet nuget locals http-cache --clear", "清空 NuGet HTTP 缓存")
    run_cmd("dotnet nuget locals temp --clear", "清空 NuGet 临时缓存")
    print("      NuGet 缓存清理完成\n", flush=True)

    # 6. 清理 NPM 缓存
    npm_cache = os.path.expandvars(r"%LOCALAPPDATA%\npm-cache")
    print(f"[6/7] 清理 NPM 缓存: {npm_cache} ...", flush=True)
    f6, c6 = clean_dir_contents(npm_cache)
    total_freed += f6
    print(f"      释放 {f6 / (1024*1024):.1f} MB\n", flush=True)

    # 7. 清空 Windows 回收站
    print(f"[7/7] 清空 Windows 回收站 ...", flush=True)
    run_cmd("powershell -Command \"Clear-RecycleBin -Force -ErrorAction SilentlyContinue\"", "清空回收站")
    print("      回收站已清空\n", flush=True)

    free_end, _ = get_c_free_bytes()
    net_gain = free_end - free_start
    print("==================================================", flush=True)
    print("🎉 C 盘清理完成！", flush=True)
    print(f"清理前可用: {free_start / (1024**3):.2f} GB", flush=True)
    print(f"清理后可用: {free_end / (1024**3):.2f} GB", flush=True)
    print(f"🚀 实际净释放空间: {net_gain / (1024**3):.2f} GB ({net_gain / (1024*1024):.1f} MB)", flush=True)
    print("==================================================", flush=True)

if __name__ == "__main__":
    main()
