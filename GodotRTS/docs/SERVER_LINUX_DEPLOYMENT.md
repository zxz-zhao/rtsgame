# RTS 服务端 Linux 部署方案与操作指南

本项目的网络架构采用 **解耦的实时游戏服务架构**，支持完整在 Linux 环境下进行容器化或物理机部署。

---

## 一、网络服务拓扑与端口

服务端在生产环境中承担两项核心职责：
1. **HTTP 业务 API (TCP 8080)**：
   - 用户体系：游客一键试玩 (`/api/guest`)、账号注册 (`/api/register`)、密码登录 (`/api/login`)。
   - 大厅与社交：个人资料 (`/api/profile`)、大厅列表 (`/api/lobby`)、好友系统 (`/api/friends`)、心跳与在线状态 (`/api/presence`)。
   - 战斗调度：房间创建/加入 (`/api/rooms`)、天梯自动匹配 (`/api/match`)、对战 Session 鉴权、战报结算与防作弊校验 (`/api/result`)。
   - 商业化与支付：商品目录、充值订单创建与服务端安全回调 (`/api/payments`)。
   - 健康检查：`/health`（用于负载均衡器与 Docker 自动保活检测）。

2. **WebSocket 战局中继 Relay (TCP 8081)**：
   - 负责对局开启后房间内所有玩家（Host 与 Peer）之间的秒级/毫秒级帧同步与战术指令中继转发（`move`, `attack`, `skill`, `formation` 等）。

---

## 二、部署方式一：C++ 高性能原生服务端 (推荐)

路径：`UnityRTS/ServerCpp`  
技术栈：现代 C++17、事件驱动网络模型、多线程异步日志、双进程看门狗（Guardian）。

### 1. Docker Compose 一键拉起（最省心、零环境污染）

在 Linux 服务器拉取代码后进入 `ServerCpp` 目录：

```bash
cd ServerCpp

# 方式 A：完整生产环境（包含 C++ 核心、MariaDB 数据库、Redis 7 实时缓存）
bash scripts/docker-run.sh
# 或直接执行：docker compose up -d --build

# 方式 B：轻量免外部数据库模式（内置 JSON 持久化，无需外部 DB）
bash scripts/docker-run.sh standalone
# 或直接执行：docker compose -f compose.standalone.yaml up -d --build
```

**常用运维操作**：
```bash
# 查看容器运行状态与健康度
docker compose ps

# 查看实时运行日志
docker compose logs -f rts-server-cpp

# 停止容器服务
docker compose down
```

### 2. 裸机/物理 VPS 原生部署（Systemd 服务守护）

支持 Ubuntu 20.04/22.04/24.04、Debian 11/12、CentOS 8/9、Rocky Linux：

```bash
# 1. 安装基础编译与运行时依赖
sudo apt update && sudo apt install -y build-essential cmake ca-certificates libmariadb-dev libmariadb3 curl

# 2. 编译并生成 Linux 安装包
cd ServerCpp
bash scripts/package-linux.sh
# 生成发布归档：dist/unity-rts-server-cpp-0.1.0-Linux-x86_64.tar.gz

# 3. 执行一键安装脚本（自动配置专属非特权用户与 Systemd 守护进程）
sudo bash scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts

# 4. 常用管理指令
systemctl status unity-rts-guardian       # 查看状态
sudo systemctl restart unity-rts-guardian  # 重启服务
sudo systemctl stop unity-rts-guardian     # 停止服务
journalctl -u unity-rts-guardian -f        # 查看日志
```

---

## 三、部署方式二：Node.js 参考服务端

路径：`UnityRTS/Server`  
技术栈：Node.js 20+、Express/WebSocket、MySQL。

### 1. Docker 容器化启动
```bash
cd Server
docker compose -f compose.fullstack.yaml up -d --build
```

### 2. PM2 宿主机运行
```bash
cd Server
npm ci --omit=dev
cp .env.example .env
# 编辑 .env 配置好数据库连接
pm2 start ecosystem.config.cjs
pm2 save
pm2 startup
```

---

## 四、部署方式三：Godot 4 Linux Dedicated Server 导出（可选）

如果需要运行带有 Godot 物理权威模拟或无头测试的 Dedicated Server，可以在 Godot 4 中直接导出：

在 `GodotRTS/export_presets.cfg` 中已配置好预设 `[preset.2]` **Linux Dedicated Server**：
- 导出平台：`Linux/X11`
- 运行模式：`dedicated_server = true`（无视窗，无音频，纯后台运行）
- 默认导出产物：`Build/LinuxServer/GodotRTS.x86_64`

导出命令（在拥有 Godot 4 Mono 可执行文件的机器上）：
```bash
godot --headless --export-release "Linux Dedicated Server" Build/LinuxServer/GodotRTS.x86_64
```

---

## 五、防火墙与安全组配置

在云服务器控制台（如腾讯云、阿里云、华为云、AWS）的安全组入站规则中，确保放通以下端口：

| 协议 | 端口 | 用途 | 访问来源 |
| :--- | :--- | :--- | :--- |
| **TCP** | **8080** | HTTP API (登录/大厅/匹配/支付) | `0.0.0.0/0` |
| **TCP** | **8081** | WebSocket Relay (实时对战指令中继) | `0.0.0.0/0` |

Linux 本地防火墙开放示例：
```bash
# Ubuntu UFW
sudo ufw allow 8080/tcp comment "RTS HTTP API"
sudo ufw allow 8081/tcp comment "RTS WebSocket Relay"
sudo ufw reload

# CentOS / Rocky Firewalld
sudo firewall-cmd --zone=public --add-port=8080/tcp --permanent
sudo firewall-cmd --zone=public --add-port=8081/tcp --permanent
sudo firewall-cmd --reload
```

---

## 六、客户端连接 Linux 服务端

客户端只需要指定服务器公网 IP 或域名即可：

1. 编辑客户端配置文件或脚本 [NetClient.cs](file:///g:/code/c++/UnityRTS/GodotRTS/scripts/network/NetClient.cs)：
   ```csharp
   [Export]
   public string ServerUrl { get; set; } = "http://<你的Linux服务器IP>:8080";
   ```
2. 对战转发模块 [GameRelay.cs](file:///g:/code/c++/UnityRTS/GodotRTS/scripts/network/GameRelay.cs) 将自动解析主机并连接 `ws://<你的Linux服务器IP>:8081`。
3. 验证方式：
   在浏览器或命令行访问：
   ```bash
   curl http://<你的Linux服务器IP>:8080/health
   ```
   返回正常 JSON（如 `{"status":"ok",...}`）即代表服务端已成功在 Linux 上线。
