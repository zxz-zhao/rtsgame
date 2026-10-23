# -*- coding: utf-8 -*-
import subprocess
import time
import os

mumu_device = r"G:\Program Files\Netease\MuMu\nx_device\12.0\shell\MuMuNxDevice.exe"

print("[MuMuLauncher] 正在拉起 MuMuNxDevice 播放器窗口 (vm 0)...")
cmd = f'"{mumu_device}" -v 0'
res = subprocess.run(["wmic", "process", "call", "create", cmd], capture_output=True, text=True)
print("[MuMuLauncher] WMIC Create Result:\n", res.stdout.strip())

