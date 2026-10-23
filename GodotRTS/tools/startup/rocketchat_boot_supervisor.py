# -*- coding: utf-8 -*-
"""
Rocket.Chat & AI RTS 协作流水线 开机自启动守护与健康巡检服务
功能：
1. 监控并等待 Docker Desktop 启动就绪
2. 自动启动 Rocket.Chat 及 MongoDB 容器
3. 自动探测并热修补 Rocket.Chat 客户端版本号为 8.8.1（确保手机端永不报 Unsupported version）
4. 自动拉起 Rocket.Chat <-> Dify 多智能体协作桥接服务（Port 5005）
5. 全程静默运行并记录日志至 boot_supervisor.log
"""

import os
import sys
import time
import json
import socket
import logging
import subprocess
import urllib.request
import urllib.error

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

WORKSPACE_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TOOLS_DIR = os.path.join(WORKSPACE_DIR, "tools")
STARTUP_DIR = os.path.join(TOOLS_DIR, "startup")
LOG_FILE = os.path.join(STARTUP_DIR, "boot_supervisor.log")

if not os.path.exists(STARTUP_DIR):
    os.makedirs(STARTUP_DIR, exist_ok=True)

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE, encoding='utf-8'),
        logging.StreamHandler(sys.stdout)
    ]
)

DOCKER_EXE = r"D:\Program Files\Docker\resources\bin\docker.exe"
if not os.path.exists(DOCKER_EXE):
    DOCKER_EXE = "docker"

DOCKER_DESKTOP_EXE = r"D:\Program Files\Docker\Docker Desktop.exe"

PYTHON_EXE = r"E:\python13\python.exe" if os.path.exists(r"E:\python13\python.exe") else sys.executable
PYTHONW_EXE = r"E:\python13\pythonw.exe" if os.path.exists(r"E:\python13\pythonw.exe") else PYTHON_EXE
RC_PORT = int(os.environ.get("RC_PORT", "3001"))


def is_port_open(port: int, host: str = "127.0.0.1") -> bool:
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(1)
        res = s.connect_ex((host, port))
        s.close()
        return res == 0
    except Exception:
        return False


def ensure_docker_desktop():
    try:
        res = subprocess.run(["tasklist"], capture_output=True)
        running_tasks = res.stdout.decode('gbk', errors='ignore').lower()
        if "docker desktop.exe" not in running_tasks:
            logging.info("🚀 Docker Desktop 进程未运行，正在唤醒启动 Docker Desktop...")
            if os.path.exists(DOCKER_DESKTOP_EXE):
                subprocess.run(
                    ["wmic", "process", "call", "create", f'"{DOCKER_DESKTOP_EXE}"'],
                    capture_output=True,
                    text=True
                )
            else:
                logging.warning(f"⚠️ 未找到 Docker Desktop: {DOCKER_DESKTOP_EXE}")
        else:
            logging.info("ℹ️ Docker Desktop 进程已在运行中")
    except Exception as e:
        logging.error(f"启动 Docker Desktop 异常: {e}")


def wait_for_docker(max_wait_seconds: int = 120) -> bool:
    ensure_docker_desktop()
    logging.info("⏳ 正在检查 Docker 守护进程状态...")
    start_time = time.time()
    while time.time() - start_time < max_wait_seconds:
        try:
            res = subprocess.run([DOCKER_EXE, "info"], capture_output=True, timeout=5)
            if res.returncode == 0:
                logging.info(f"✅ Docker 守护进程已就绪 (耗时 {round(time.time() - start_time, 1)}s)")
                return True
        except Exception:
            pass
        time.sleep(3)
    logging.error("❌ 等待 Docker 启动超时！")
    return False


def ensure_rocketchat_containers():
    logging.info("🚀 检查 Rocket.Chat 与 MongoDB 容器...")
    try:
        res = subprocess.run([DOCKER_EXE, "ps", "--format", "{{.Names}}"], capture_output=True, text=True)
        running = res.stdout.split()

        to_start = []
        for c in ["rocketchat-mongo", "rocketchat"]:
            if c not in running:
                to_start.append(c)

        if to_start:
            logging.info(f"启动容器: {to_start}")
            start_res = subprocess.run([DOCKER_EXE, "start"] + to_start, capture_output=True, text=True)
            if start_res.returncode != 0:
                logging.warning(f"docker start 失败，尝试 docker compose up -d: {start_res.stderr.strip()}")
                compose_file = os.path.join(TOOLS_DIR, "rocketchat", "docker-compose.yml")
                subprocess.run([DOCKER_EXE, "compose", "-f", compose_file, "up", "-d"], capture_output=True)
        else:
            logging.info("✅ rocketchat-mongo 与 rocketchat 容器均在运行中")
    except Exception as e:
        logging.error(f"启动容器异常: {e}")


def wait_and_patch_version(target_version: str = "8.8.1", max_wait_seconds: int = 90) -> bool:
    logging.info("🔍 等待 Rocket.Chat Web 服务就绪并核验版本契约...")
    start_time = time.time()
    server_ready = False
    
    while time.time() - start_time < max_wait_seconds:
        try:
            req = urllib.request.urlopen(f"http://localhost:{RC_PORT}/api/info", timeout=3)
            data = json.loads(req.read().decode('utf-8'))
            cur_ver = data.get("version", "")
            logging.info(f"Rocket.Chat 当前对外版本响应: '{cur_ver}'")
            if cur_ver == target_version:
                logging.info(f"✅ Rocket.Chat 版本已是 {target_version}，手机端可免阻断正常连入！")
                return True
            else:
                server_ready = True
                break
        except Exception:
            time.sleep(2)

    if not server_ready:
        logging.warning("⚠️ Rocket.Chat 启动响应超时，尝试直接执行热补丁...")

    logging.info(f"🛠️ 正在执行热补丁，将版本伪装锁定为 {target_version}...")
    patch_node_code = f"""
const fs = require('fs');
const file = '/app/bundle/programs/server/app/app.js';
try {{
    let c = fs.readFileSync(file, 'utf8');
    c = c.replace(/const removePatchInfo = [^;]+;/, "const removePatchInfo = version => '{target_version}';");
    c = c.replace(/exports\\.Info\\s*=\\s*\\{{\\s*"version":\\s*"[^"]+"/, 'exports.Info = {{\\n    "version": "{target_version}"');
    fs.writeFileSync(file, c, 'utf8');
    console.log("PATCH_SUCCESS");
}} catch (err) {{
    console.error("PATCH_FAILED: " + err);
    process.exit(1);
}}
"""
    try:
        subprocess.run([DOCKER_EXE, "exec", "-u", "0", "rocketchat", "chmod", "666", "/app/bundle/programs/server/app/app.js"], capture_output=True)
        patch_res = subprocess.run([DOCKER_EXE, "exec", "-u", "0", "rocketchat", "node", "-e", patch_node_code], capture_output=True, text=True)
        if "PATCH_SUCCESS" in patch_res.stdout:
            logging.info(f"✅ app.js 补丁写入成功，正在平滑重启 rocketchat 容器生效...")
            subprocess.run([DOCKER_EXE, "restart", "rocketchat"], capture_output=True)
            
            # 等待重启后版本确认
            for _ in range(30):
                time.sleep(2)
                try:
                    req = urllib.request.urlopen(f"http://localhost:{RC_PORT}/api/info", timeout=3)
                    data = json.loads(req.read().decode('utf-8'))
                    if data.get("version") == target_version:
                        logging.info(f"🎉 验证通过！Rocket.Chat 已成功升级展示为 {target_version}")
                        return True
                except Exception:
                    pass
        else:
            logging.error(f"❌ 补丁执行输出异常: {patch_res.stdout} {patch_res.stderr}")
    except Exception as e:
        logging.error(f"❌ 执行热补丁失败: {e}")

    return False


def ensure_multi_agent_bridge():
    logging.info("🤖 检查 Rocket.Chat <-> Dify 多智能体协作桥接服务...")
    if is_port_open(5005):
        try:
            req = urllib.request.urlopen("http://127.0.0.1:5005/", timeout=2)
            data = json.loads(req.read().decode('utf-8'))
            if data.get("service") == "rocketchat_dify_bridge":
                logging.info("✅ 多智能体协作桥接服务 (Port 5005) 已在健康运行中")
                return
        except Exception:
            pass

    bridge_script = os.path.join(TOOLS_DIR, "rocketchat_dify_bridge.py")
    bridge_log_file = os.path.join(TOOLS_DIR, "bridge.log")
    logging.info(f"启动多智能体协作后台守护进程: {bridge_script} (日志: {bridge_log_file})")
    try:
        cmd = f'"{PYTHONW_EXE}" "{bridge_script}"'
        subprocess.run(["wmic", "process", "call", "create", cmd], capture_output=True, text=True)
        time.sleep(3)
        if is_port_open(5005):
            logging.info("✅ 多智能体协作桥接服务已成功拉起并监听 5005 端口！")
        else:
            logging.warning("⚠️ 桥接服务已派发启动，请关注端口就绪")
    except Exception as e:
        logging.error(f"启动桥接服务失败: {e}")


def ensure_cloudflare_tunnel():
    logging.info("🌐 检查 Cloudflare 公网隧道 (手机 5G/外网连入通道)...")
    cloudflared_exe = os.path.join(TOOLS_DIR, "cloudflared.exe")
    if not os.path.exists(cloudflared_exe):
        logging.warning("⚠️ 未找到 cloudflared.exe，跳过公网隧道自启")
        return

    try:
        res = subprocess.run(["tasklist", "/FI", "IMAGENAME eq cloudflared.exe"], capture_output=True, text=True)
        if "cloudflared.exe" in res.stdout:
            logging.info("✅ Cloudflare 隧道守护进程已在运行中")
        else:
            log_path = os.path.join(TOOLS_DIR, "tunnel_rocketchat.log")
            logging.info(f"正在拉起 Cloudflare 隧道: {cloudflared_exe}")
            cmd = f'"{cloudflared_exe}" tunnel --protocol http2 --url http://127.0.0.1:{RC_PORT} --logfile "{log_path}"'
            subprocess.run(["wmic", "process", "call", "create", cmd], capture_output=True, text=True)
            time.sleep(5)

        # 提取最新隧道 URL 并写入文本备查
        log_path = os.path.join(TOOLS_DIR, "tunnel_rocketchat.log")
        if os.path.exists(log_path):
            with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
            for line in reversed(lines):
                if ".trycloudflare.com" in line:
                    import re
                    m = re.search(r"https://[a-zA-Z0-9\-_]+\.trycloudflare\.com", line)
                    if m:
                        public_url = m.group(0)
                        url_file = os.path.join(STARTUP_DIR, "current_tunnel_url.txt")
                        with open(url_file, "w", encoding="utf-8") as uf:
                            uf.write(public_url + "\n")
                        logging.info(f"🌐 最新公网隧道地址: {public_url}")
                        break
    except Exception as e:
        logging.error(f"启动或检测公网隧道异常: {e}")


def ensure_ipv6_ddns():
    logging.info("📡 检查原生公网 IPv6 DDNS 动态域名同步状态...")
    ddns_script = os.path.join(STARTUP_DIR, "ipv6_ddns_updater.py")
    cfg_file = os.path.join(STARTUP_DIR, "ddns_config.json")
    if os.path.exists(ddns_script) and os.path.exists(cfg_file):
        try:
            with open(cfg_file, "r", encoding="utf-8") as f:
                cfg = json.load(f)
            if cfg.get("hostname") and cfg.get("token"):
                res = subprocess.run([PYTHON_EXE, ddns_script, "--force"], capture_output=True, text=True, errors="ignore")
                for line in res.stdout.splitlines():
                    if line.strip():
                        logging.info(f"[IPv6 DDNS] {line.strip()}")
            else:
                logging.info("ℹ️ IPv6 DDNS 尚未填入 hostname / token，请在 ddns_config.json 填入即可生效")
        except Exception as e:
            logging.error(f"执行 DDNS 检查异常: {e}")


def main():
    logging.info("=" * 60)
    logging.info("🌟 星火 RTS / Rocket.Chat 全套服务开机自启动守护巡检开始")
    logging.info("=" * 60)

    # 1. 确保 Docker 运行
    if not wait_for_docker(max_wait_seconds=90):
        logging.error("Docker 未能启动，自启流程终止")
        return

    # 2. 确保容器运行
    ensure_rocketchat_containers()

    # 3. 校验并修补版本号
    wait_and_patch_version("8.8.1")

    # 4. 确保 AI 多智能体桥接服务运行
    ensure_multi_agent_bridge()

    # 5. 确保原生 IPv6 DDNS 域名同步
    ensure_ipv6_ddns()

    # 6. 确保公网隧道服务运行（临时兜底通道）
    ensure_cloudflare_tunnel()

    logging.info("=" * 60)
    logging.info("🎉 全套服务巡检与开机自启完成！手机客户端随时可连入！")
    logging.info("=" * 60)


if __name__ == "__main__":
    main()

