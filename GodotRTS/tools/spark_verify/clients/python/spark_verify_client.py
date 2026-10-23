"""
SparkVerify 极验网络验证系统 - Python 客户端 SDK
支持: 一机一码提取、HMAC防抓包签名、卡密激活、后台心跳保活线程、动态云变量解析
"""

import os
import sys
import time
import json
import hmac
import hashlib
import platform
import threading
import subprocess
from typing import Dict, Any, Optional
import urllib.request
import urllib.error

class SparkVerifyClient:
    def __init__(self, host: str, app_id: str, app_secret: str, version: str = "1.0.0"):
        self.host = host.rstrip("/")
        self.app_id = app_id
        self.app_secret = app_secret
        self.version = version
        self.hwid = self._get_hwid()
        
        self.session_token: Optional[str] = None
        self.expires_at: Optional[str] = None
        self.cloud_vars: Dict[str, str] = {}
        self.heartbeat_interval = 25
        self._heartbeat_thread: Optional[threading.Thread] = None
        self._running = False
        self._on_kick_callback = None

    def _get_hwid(self) -> str:
        """提取机器唯一硬件指纹 (Windows: CPU + BIOS/主板 + 硬盘)"""
        fingerprints = []
        try:
            if platform.system() == "Windows":
                # 1. 主板 UUID
                cmd_uuid = 'powershell -NoProfile -Command "(Get-CimInstance Win32_ComputerSystemProduct).UUID"'
                uuid_res = subprocess.check_output(cmd_uuid, shell=True, text=True, stderr=subprocess.DEVNULL).strip()
                fingerprints.append(uuid_res)

                # 2. 处理器 ID
                cmd_cpu = 'powershell -NoProfile -Command "(Get-CimInstance Win32_Processor).ProcessorId"'
                cpu_res = subprocess.check_output(cmd_cpu, shell=True, text=True, stderr=subprocess.DEVNULL).strip()
                fingerprints.append(cpu_res)

                # 3. 硬盘序列号
                cmd_disk = 'powershell -NoProfile -Command "(Get-CimInstance Win32_DiskDrive | Select-Object -First 1).SerialNumber"'
                disk_res = subprocess.check_output(cmd_disk, shell=True, text=True, stderr=subprocess.DEVNULL).strip()
                fingerprints.append(disk_res)
            else:
                fingerprints.append(platform.node())
                fingerprints.append(platform.machine())
        except Exception:
            fingerprints.append("FALLBACK_HARDWARE_FINGERPRINT")

        raw_str = "|".join(fingerprints)
        return hashlib.sha256(raw_str.encode("utf-8")).hexdigest()[:32].upper()

    def _sign(self, param_str: str) -> str:
        mac = hmac.new(self.app_secret.encode("utf-8"), param_str.encode("utf-8"), hashlib.sha256)
        return mac.hexdigest()

    def _post(self, path: str, payload: dict) -> dict:
        url = f"{self.host}{path}"
        data_bytes = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data_bytes, headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=10) as resp:
                res_body = resp.read().decode("utf-8")
                return json.loads(res_body)
        except urllib.error.HTTPError as e:
            try:
                return json.loads(e.read().decode("utf-8"))
            except Exception:
                return {"code": e.code, "msg": f"HTTP Error {e.code}"}
        except Exception as e:
            return {"code": 500, "msg": f"网络通信失败: {str(e)}"}

    def init_app(self) -> dict:
        """初始化应用、获取公告、检查更新"""
        payload = {
            "app_id": self.app_id,
            "version": self.version,
            "hwid": self.hwid
        }
        res = self._post("/api/v1/client/init", payload)
        return res

    def verify(self, card_key: str, auto_heartbeat: bool = True) -> (bool, str, dict):
        """卡密验证、一机一码绑定与登录"""
        ts = int(time.time())
        sign_str = f"app_id={self.app_id}&card_key={card_key}&hwid={self.hwid}&timestamp={ts}"
        sign = self._sign(sign_str)

        payload = {
            "app_id": self.app_id,
            "card_key": card_key,
            "hwid": self.hwid,
            "timestamp": ts,
            "sign": sign
        }

        res = self._post("/api/v1/client/verify", payload)
        if res.get("code") == 200:
            data = res.get("data", {})
            self.session_token = data.get("session_token")
            self.expires_at = data.get("expires_at")
            self.cloud_vars = data.get("cloud_vars", {})
            self.heartbeat_interval = data.get("heartbeat_interval", 25)

            if auto_heartbeat:
                self.start_heartbeat()

            return True, res.get("msg", "授权成功"), data
        else:
            return False, res.get("msg", "验证失败"), {}

    def heartbeat_once(self) -> (bool, str):
        """发送单次心跳"""
        if not self.session_token:
            return False, "尚未登录或会话已失效"

        ts = int(time.time())
        payload = {
            "app_id": self.app_id,
            "session_token": self.session_token,
            "hwid": self.hwid,
            "timestamp": ts
        }

        res = self._post("/api/v1/client/heartbeat", payload)
        if res.get("code") == 200:
            return True, "心跳正常"
        else:
            msg = res.get("msg", "心跳失败")
            if self._on_kick_callback:
                self._on_kick_callback(msg)
            return False, msg

    def start_heartbeat(self, on_kick=None):
        """启动后台心跳守护线程"""
        if self._running:
            return
        self._running = True
        self._on_kick_callback = on_kick

        def _loop():
            while self._running:
                time.sleep(self.heartbeat_interval)
                if not self._running:
                    break
                ok, msg = self.heartbeat_once()
                if not ok:
                    print(f"[SparkVerify] 心跳中断/异常: {msg}")
                    self._running = False
                    break

        self._heartbeat_thread = threading.Thread(target=_loop, daemon=True)
        self._heartbeat_thread.start()

    def stop_heartbeat(self):
        self._running = False

    def unbind(self, card_key: str) -> (bool, str):
        """卡密解绑当前设备"""
        ts = int(time.time())
        payload = {
            "app_id": self.app_id,
            "card_key": card_key,
            "hwid": self.hwid,
            "timestamp": ts
        }
        res = self._post("/api/v1/client/unbind", payload)
        return (res.get("code") == 200), res.get("msg", "")

    def logout(self):
        """安全登出并释放会话"""
        self.stop_heartbeat()
        if self.session_token:
            self._post(f"/api/v1/client/logout?session_token={self.session_token}", {})
            self.session_token = None

    def get_var(self, key: str, default: str = "") -> str:
        """获取云端下发变量"""
        return self.cloud_vars.get(key, default)
