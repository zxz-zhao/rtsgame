"""
SparkPacker - 软件一键加壳植入与加密打包器
将目标业务程序加密打包，嵌入 SparkVerify 卡密验证与心跳看门狗
"""

import os
import sys
import json
import base64
import secrets
import shutil
import subprocess

# 引入加解密
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "backend")))
import crypto_utils

def pack_application(
    target_file: str,
    output_dir: str,
    host: str = "http://127.0.0.1:8989",
    app_id: str = "APP_DEMO_001",
    app_secret: str = "sv_secret_88889999_k9",
    build_exe: bool = False
):
    print("==========================================================")
    print("         SparkPacker 软件加密与卡密验证植入工具")
    print("==========================================================")
    
    if not os.path.exists(target_file):
        raise FileNotFoundError(f"目标程序不存在: {target_file}")
        
    os.makedirs(output_dir, exist_ok=True)
    
    # 1. 读取原程序二进制
    print(f"[*] 正在读取待保护程序: {target_file}")
    with open(target_file, "rb") as f:
        raw_bytes = f.read()
    raw_b64 = base64.b64encode(raw_bytes).decode("utf-8")
    
    # 2. 生成随机加壳加密密钥 (AES-256)
    pack_key = secrets.token_hex(32)
    print(f"[*] 派生高强度封包加密密钥: {pack_key[:12]}...")
    
    # 3. 加密目标 Payload
    enc_payload = crypto_utils.encrypt_aes_gcm(raw_b64, pack_key)
    payload_file = os.path.join(output_dir, "payload.enc")
    with open(payload_file, "w", encoding="utf-8") as f:
        f.write(enc_payload)
    print(f"[+] 目标程序已成功加密并保存至: {payload_file}")
    
    # 4. 生成引导装载器 Loader
    loader_script = f'''"""
SparkVerify 动态安全引导装载器 (Protected Loader)
自动执行卡密授权、解密载入、心跳保活与反破解守护
"""
import os
import sys
import time
import base64
import tempfile
import threading
import subprocess
import tkinter.messagebox

# 导入 SDK 与图形界面
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "clients", "python")))
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "backend")))
import crypto_utils
from verify_gui import SparkVerifyLoginDialog

HOST = "{host}"
APP_ID = "{app_id}"
APP_SECRET = "{app_secret}"
PACK_KEY = "{pack_key}"
IS_PYTHON_TARGET = {str(target_file.endswith(".py"))}

def run_protected_process(client, session_data):
    # 1. 读取并解密 Payload
    enc_file = os.path.join(os.path.dirname(__file__), "payload.enc")
    if not os.path.exists(enc_file):
        tkinter.messagebox.showerror("错误", "加密载荷缺失，程序无法运行！")
        return
        
    with open(enc_file, "r", encoding="utf-8") as f:
        enc_payload = f.read()
        
    decrypted_b64 = crypto_utils.decrypt_aes_gcm(enc_payload, PACK_KEY)
    if not decrypted_b64:
        tkinter.messagebox.showerror("错误", "解密核心载荷失败，校验和损坏！")
        return
        
    raw_bytes = base64.b64decode(decrypted_b64.encode("utf-8"))
    
    # 2. 在安全沙箱临时释放并启动
    suffix = ".py" if IS_PYTHON_TARGET else ".exe"
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
        tmp.write(raw_bytes)
        tmp_path = tmp.name
        
    cmd = [sys.executable, tmp_path] if IS_PYTHON_TARGET else [tmp_path]
    print(f"[*] 启动受保护的目标主程序: {{tmp_path}}")
    proc = subprocess.Popen(cmd)
    
    # 3. 启动心跳看门狗监控
    def _watchdog():
        interval = session_data.get("heartbeat_interval", 25)
        while proc.poll() is None:
            time.sleep(interval)
            if proc.poll() is not None:
                break
            ok, msg = client.heartbeat_once()
            if not ok:
                print(f"[-] 心跳异常中断: {{msg}}，强制终止保护进程！")
                proc.terminate()
                try:
                    proc.kill()
                except Exception:
                    pass
                tkinter.messagebox.showerror("授权终止", f"与验证服务器失去连接或会话已被吊销:\\n{{msg}}\\n程序已强制关闭！")
                break
                
        # 进程结束后清理临时文件
        client.logout()
        try:
            if os.path.exists(tmp_path):
                os.remove(tmp_path)
        except Exception:
            pass

    threading.Thread(target=_watchdog, daemon=True).start()
    proc.wait()

def main():
    dialog = SparkVerifyLoginDialog(
        host=HOST,
        app_id=APP_ID,
        app_secret=APP_SECRET,
        on_success=run_protected_process
    )
    dialog.run()

if __name__ == "__main__":
    main()
'''
    loader_path = os.path.join(output_dir, "run_protected.py")
    with open(loader_path, "w", encoding="utf-8") as f:
        f.write(loader_script)
    print(f"[+] 动态保护引导器生成成功: {loader_path}")

    # 5. 复制必需的 GUI 模块
    gui_src = os.path.join(os.path.dirname(__file__), "verify_gui.py")
    shutil.copy(gui_src, os.path.join(output_dir, "verify_gui.py"))

    # 6. 生成一键编译为单文件 EXE 的脚本
    build_bat = os.path.join(output_dir, "build_standalone_exe.bat")
    with open(build_bat, "w", encoding="utf-8") as f:
        f.write(f'''@echo off
chcp 65001 >nul
echo 正在使用 PyInstaller 编译为单文件加壳 EXE...
"E:\\python13\\python.exe" -m PyInstaller --onefile --noconsole --name "ProtectedApp" --add-data "payload.enc;." run_protected.py
pause
''')
    print(f"[+] 单文件 EXE 编译脚本已就绪: {build_bat}")

    print("\n==========================================================")
    print("      [SUCCESS] 软件外壳加密植入成功完成！")
    print(f"      输出目录: {output_dir}")
    print("==========================================================")
    return loader_path

if __name__ == "__main__":
    sample_file = os.path.join(os.path.dirname(__file__), "sample_target.py")
    out_dir = os.path.join(os.path.dirname(__file__), "dist_protected")
    pack_application(sample_file, out_dir)
