# UnityRTS C++ 原生服务端 Linux 部署方案与指南

本文档提供针对 **Linux 环境（Ubuntu / Debian / CentOS / Rocky Linux / 云服务器）** 的服务端完整部署指南。

---

## 核心架构与端口

服务端包含两个核心端口：
- **`8080 TCP` (HTTP API)**：账号注册、登录、游客体验、大厅状态、科技树、匹配队列、支付订单验证、战报结算。
- **`8081 TCP` (WebSocket Relay)**：实时战斗房间、帧同步与战斗指令（移动、攻击、技能释放等）路由分发。
- **健康检查地址**：`http://<服务器IP>:8080/health`（返回 HTTP 200 即正常）。

守护进程架构：
- `unity_rts_guardian_cpp`（看门狗守护进程）：监控服务端主进程并在发生异常退出或崩溃时根据重启策略自动拉起，记录 `logs/guardian.log`。
- `unity_rts_server_cpp`（核心服务进程）：多线程高并发事件驱动处理 HTTP 与 WebSocket 流量。

---

## 方案一：Docker / Docker Compose 一键部署（强烈推荐，生产级）

无需在 Linux 宿主机安装 C++ 编译环境或特定版本的 glibc/依赖库，隔离性与可移植性最强。

### 1. 完整生产栈（C++ 服务端 + MariaDB + Redis）

在 Linux 服务器上执行：

```bash
cd ServerCpp

# 启动全套服务（包含 C++ 服务端、MySQL/MariaDB 持久化数据库、Redis 7 实时缓存）
bash scripts/docker-run.sh
```

或者手动使用 docker compose 命令：
```bash
docker compose up -d --build
```

查看容器运行状态与健康指标：
```bash
docker compose ps
docker compose logs -f rts-server-cpp
```

### 2. 轻量免依赖独立模式（零外部数据库，内置文件持久化）

适用于快速测试、小型个人服务器或演示环境，无需拉取或启动 MySQL/Redis：

```bash
cd ServerCpp
bash scripts/docker-run.sh standalone
```
或者：
```bash
docker compose -f compose.standalone.yaml up -d --build
```
此时数据将自动持久化存储在当前目录的 `data/*.json` 之中。

### 3. 停止与重启
```bash
# 停止容器
docker compose down

# 重启服务
docker compose restart rts-server-cpp
```

---

## 方案二：Linux 宿主机原生部署（Systemd 服务模式）

适用于腾讯云/阿里云/华为云 VPS 裸机部署。

### 1. 安装系统依赖库

**Ubuntu / Debian**:
```bash
sudo apt update
sudo apt install -y build-essential cmake ca-certificates libmariadb-dev libmariadb3 curl
```

**CentOS / Rocky Linux / RHEL**:
```bash
sudo dnf install -y gcc-c++ make cmake mariadb-devel mariadb-connector-c curl
```

### 2. 编译并打包 Linux 发布包

在项目 `ServerCpp` 目录下运行：
```bash
bash scripts/package-linux.sh
```
执行完毕后，将在 `dist/` 目录下生成 `unity-rts-server-cpp-0.1.0-Linux-x86_64.tar.gz`。

> **提示**：如果本地为 Windows 环境，可通过已提供的 `bash scripts/build-linux-docker.sh` 或 `powershell scripts/build-linux-docker.ps1`，借助 Docker 容器自动交叉编译出 Linux 发行包。

### 3. 一键安装为 Systemd 系统服务

将发布包解压后，执行一键部署脚本：
```bash
# 默认安装至 /opt/unity-rts-server 并配置专属服务账号 unityrts
sudo bash scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts
```

部署脚本会自动：
1. 创建 `/opt/unity-rts-server` 目录及 `bin/`、`scripts/`、`logs/`、`data/`、`backups/`。
2. 同步二进制可执行文件与运行脚本。
3. 创建系统专用非特权运行账号 `unityrts`。
4. 注册并启动 `/etc/systemd/system/unity-rts-guardian.service` 服务。

### 4. Systemd 运维管理命令

```bash
# 查看看门狗与服务运行状态
systemctl status unity-rts-guardian

# 查看实时运行日志
journalctl -u unity-rts-guardian -f

# 查看核心服务每日日志
tail -f /opt/unity-rts-server/logs/server-$(date +%F).log

# 重启服务
sudo systemctl restart unity-rts-guardian

# 停止服务
sudo systemctl stop unity-rts-guardian
```

---

## 防火墙配置（开放 8080 与 8081 端口）

若使用云厂商安全组（阿里云/腾讯云/AWS等），请在安全组入方向规则中放行：
- `TCP 8080` (客户端 HTTP 接入)
- `TCP 8081` (客户端 WebSocket 对战帧中继)

Linux 操作系统防火墙放行命令：

**UFW (Ubuntu / Debian)**:
```bash
sudo ufw allow 8080/tcp comment "UnityRTS HTTP API"
sudo ufw allow 8081/tcp comment "UnityRTS WebSocket Relay"
sudo ufw reload
```

**Firewalld (CentOS / Rocky Linux)**:
```bash
sudo firewall-cmd --zone=public --add-port=8080/tcp --permanent
sudo firewall-cmd --zone=public --add-port=8081/tcp --permanent
sudo firewall-cmd --reload
```

---

## 客户端连接配置

部署完成后，在客户端 (`GodotRTS`) 进行连接测试：

1. 打开客户端网络单例配置脚本 `scripts/network/NetClient.cs`：
   ```csharp
   [Export]
   public string ServerUrl { get; set; } = "http://<你的Linux服务器公网IP>:8080";
   ```
2. 战局对战转发模块 `scripts/network/GameRelay.cs` 会根据 `NetClient.Instance.ServerUrl` 自动提取 Host 并建立 WebSocket 连接：
   ```csharp
   var err = socket.ConnectToUrl($"ws://{host}:8081");
   ```
3. 验证联通性：
   - 可以在终端或浏览器访问：`http://<你的Linux服务器公网IP>:8080/health`
   - 返回 `{"status":"ok",...}` 即表示 Linux 服务端已就绪！
