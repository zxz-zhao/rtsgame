import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

targets = [
    ("用户临时文件 (AppData\\Local\\Temp)", r"C:\Users\Administrator\AppData\Local\Temp"),
    ("系统临时文件 (Windows\\Temp)", r"C:\Windows\Temp"),
    ("Gradle构建缓存 (.gradle\\caches)", r"C:\Users\Administrator\.gradle\caches"),
    ("Gradle版本下载包 (.gradle\\wrapper)", r"C:\Users\Administrator\.gradle\wrapper"),
    ("NuGet包缓存 (.nuget\\packages)", r"C:\Users\Administrator\.nuget\packages"),
    ("Pip下载缓存 (pip\\cache)", r"C:\Users\Administrator\AppData\Local\pip\cache"),
    ("NPM缓存 (npm-cache)", r"C:\Users\Administrator\AppData\Local\npm-cache"),
    ("Yarn缓存 (Yarn\\Cache)", r"C:\Users\Administrator\AppData\Local\Yarn\Cache"),
    ("通用用户缓存 (.cache)", r"C:\Users\Administrator\.cache"),
    ("系统崩溃转储 (CrashDumps)", r"C:\Users\Administrator\AppData\Local\CrashDumps"),
    ("Windows更新下载包 (SoftwareDistribution\\Download)", r"C:\Windows\SoftwareDistribution\Download"),
    ("回收站 ($Recycle.Bin)", r"C:\$Recycle.Bin"),
]

print("=== 正在快速统计 C 盘各安全可清理缓存项 ===", flush=True)

results = []
grand_total = 0

for name, path in targets:
    size = 0
    count = 0
    if os.path.exists(path):
        try:
            for root, dirs, files in os.walk(path):
                for f in files:
                    try:
                        fp = os.path.join(root, f)
                        if not os.path.islink(fp):
                            size += os.path.getsize(fp)
                            count += 1
                    except Exception:
                        pass
        except Exception:
            pass
        grand_total += size
        mb = size / (1024 * 1024)
        print(f"[{mb:8.1f} MB] ({count:5d} 个文件) : {name}", flush=True)
        results.append((name, path, size, count))
    else:
        print(f"[    0.0 MB] (不存在)       : {name}", flush=True)

print(f"\n==========================================", flush=True)
print(f"👉 预计可直接安全释放空间: {grand_total / (1024*1024*1024):.2f} GB ({grand_total / (1024*1024):.1f} MB)", flush=True)
print(f"==========================================", flush=True)
