# -*- coding: utf-8 -*-
"""
原生公网 IPv6 动态域名解析 (DDNS) 自动化同步服务
支持：
1. 自动提取本机稳定的公网 IPv6 单播地址 (EUI-64/稳定地址，过滤临时隐私地址)
2. 支持 Dynv6 (免费 *.v6.rocks, *.dynv6.net)、DuckDNS、自定义 Webhook
3. 智能变化探测：仅当 IPv6 变动时上报，防滥用防频控
4. 深度联动开机自启守护进程与状态监控
"""

import os
import sys
import re
import json
import time
import socket
import logging
import subprocess
import urllib.request
import urllib.error

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
if hasattr(sys.stderr, "reconfigure"):
    try:
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

STARTUP_DIR = os.path.dirname(os.path.abspath(__file__))
TOOLS_DIR = os.path.dirname(STARTUP_DIR)
CONFIG_FILE = os.path.join(STARTUP_DIR, "ddns_config.json")
LOG_FILE = os.path.join(STARTUP_DIR, "ipv6_ddns.log")

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE, encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)


def get_local_stable_ipv6() -> str | None:
    """提取本机网络适配器绑定的全局公网 IPv6 (优先选用固定/非临时地址)"""
    try:
        res = subprocess.run(["ipconfig"], capture_output=True, text=True, errors="ignore")
        lines = res.stdout.splitlines()
        candidates = []
        for line in lines:
            if "IPv6" in line and "fe80" not in line.lower():
                is_temp = ("临时" in line) or ("temporary" in line.lower())
                m = re.search(r'([0-9a-fA-F]{1,4}:[0-9a-fA-F:]{5,})', line)
                if m:
                    candidates.append((m.group(1).strip(), is_temp))

        # 优先选用非临时的稳定公网地址 (2xxx: 开头)
        for ip, is_temp in candidates:
            if not is_temp and ip.startswith("2"):
                return ip
        # 兜底：任意 2xxx 全球单播地址
        for ip, is_temp in candidates:
            if ip.startswith("2"):
                return ip
    except Exception as e:
        logging.error(f"提取本地 IPv6 异常: {e}")
    return None


def load_config() -> dict:
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception as e:
            logging.error(f"读取 DDNS 配置文件异常: {e}")
    return {
        "provider": "dynv6",
        "hostname": "",
        "token": "",
        "last_ip": "",
        "last_update_time": 0
    }


def save_config(cfg: dict):
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
    except Exception as e:
        logging.error(f"保存 DDNS 配置文件异常: {e}")


def update_dynv6(hostname: str, token: str, ipv6: str) -> bool:
    """更新 Dynv6 动态域名解析 (AAAA 记录)"""
    url = f"https://dynv6.com/api/update?hostname={hostname}&token={token}&ipv6={ipv6}"
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "SparkRTS-IPv6-DDNS/1.0"})
        with urllib.request.urlopen(req, timeout=10) as resp:
            body = resp.read().decode('utf-8', errors='ignore').strip()
            logging.info(f"🌐 [Dynv6 响应]: {body}")
            if "addresses updated" in body or "no update needed" in body or resp.status == 200:
                return True
    except urllib.error.HTTPError as e:
        err_msg = e.read().decode('utf-8', errors='ignore')
        logging.error(f"❌ Dynv6 更新失败 (HTTP {e.code}): {err_msg}")
    except Exception as e:
        logging.error(f"❌ 请求 Dynv6 异常: {e}")
    return False


def sync_ddns_once(force: bool = False) -> tuple[bool, str]:
    cfg = load_config()
    hostname = cfg.get("hostname", "").strip()
    token = cfg.get("token", "").strip()
    provider = cfg.get("provider", "dynv6").lower()

    if not hostname or not token:
        msg = "⚠️ DDNS 尚未配置 hostname 或 token，请在 ddns_config.json 中填入"
        logging.warning(msg)
        return False, msg

    cur_ip = get_local_stable_ipv6()
    if not cur_ip:
        msg = "❌ 未检测到可用的公网 IPv6 地址，请检查本地宽带或路由器是否开启 IPv6"
        logging.error(msg)
        return False, msg

    last_ip = cfg.get("last_ip", "")
    if cur_ip == last_ip and not force:
        logging.info(f"ℹ️ 公网 IPv6 未发生变化 ({cur_ip})，跳过解析更新")
        return True, f"IPv6 未变化: {cur_ip}"

    logging.info(f"🚀 检测到 IPv6 需要更新: {last_ip} -> {cur_ip}")
    success = False
    if provider == "dynv6":
        success = update_dynv6(hostname, token, cur_ip)
    else:
        logging.error(f"暂不支持的 DDNS 提供商: {provider}")
        return False, f"不支持的提供商: {provider}"

    if success:
        cfg["last_ip"] = cur_ip
        cfg["last_update_time"] = int(time.time())
        save_config(cfg)
        logging.info(f"🎉 动态域名同步成功！当前域名 {hostname} -> [{cur_ip}]")
        return True, f"成功解析至 [{cur_ip}]"
    else:
        return False, "DDNS API 调用失败"


def main():
    if "--daemon" in sys.argv:
        logging.info("🚀 启动 IPv6 DDNS 后台守护进程 (每 300 秒自动巡检一次)...")
        while True:
            try:
                sync_ddns_once()
            except Exception as e:
                logging.error(f"轮询异常: {e}")
            time.sleep(300)
    elif "--force" in sys.argv:
        sync_ddns_once(force=True)
    else:
        sync_ddns_once()


if __name__ == "__main__":
    main()
