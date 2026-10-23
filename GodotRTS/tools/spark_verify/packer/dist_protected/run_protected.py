"""
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

HOST = "http://127.0.0.1:8989"
APP_ID = "APP_DEMO_001"
APP_SECRET = "sv_secret_88889999_k9"
PACK_KEY = "2d21f27b41afe52c975939e2592b210f89f56bde52e0ecdd326545d0835acf98"
IS_PYTHON_TARGET = True

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
    print(f"[*] 启动受保护的目标主程序: {tmp_path}")
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
                print(f"[-] 心跳异常中断: {msg}，强制终止保护进程！")
                proc.terminate()
                try:
                    proc.kill()
                except Exception:
                    pass
                tkinter.messagebox.showerror("授权终止", f"与验证服务器失去连接或会话已被吊销:\n{msg}\n程序已强制关闭！")
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
