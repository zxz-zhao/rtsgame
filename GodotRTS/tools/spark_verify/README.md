# SparkVerify 极验网络验证系统 (开源部署版)

> 对标 **卡密通 (Keyt.cn)** 与 **KeyAuth** 的现代开源轻量级网络验证与卡密授权中枢。  
> 原生支持 **一机一码绑定、设备解绑冷却、客户端心跳保活、防多开锁定、动态云变量、软件更新检测与多语言客户端接入**。

---

## 一、系统核心特性

| 功能模块 | 规格与实现机制 | 对标优势 |
| :--- | :--- | :--- |
| **一机一码 (HWID)** | 组合采集 `CPU ID` + `主板 UUID` + `硬盘序列号`，SHA-256 散列生成机器指纹。 | 严格杜绝卡密复制盗用，支持换绑次数限制与冷却时长控制。 |
| **心跳保活 & 防多开** | 客户端后台线程定时上报心跳（默认 25~30s），服务端 60s 离线自动回收。 | 杜绝单卡多开，离线自动释放，支持管理后台一键踢出。 |
| **全周期卡密管理** | 支持批量生成：天卡、周卡、月卡、季卡、年卡、永久卡、计次卡、自定义天数。 | 支持自定义前缀、制卡备注、一键导出 TXT/CSV、一键冻结/解绑。 |
| **远程机密云变量** | 验证通过后动态下发敏感 API Key、游戏服务器地址、功能开关。 | 敏感数据不写死在本地客户端内存中，无需频繁更新 EXE。 |
| **应用与版本管控** | 远程弹窗公告、新版本检测、强制版本更新直链下发、一键维护停服。 | 随时掌控软件运营生命周期。 |
| **全主流语言 SDK** | 提供 **Python、C# (.NET/Unity/Godot)、C++ (Win32)、Java、易语言 (EPL)** 开箱即用源码。 | 极低对接门槛，5 分钟快速集成。 |

---

## 二、部署运行与管理控制台

### 1. 服务端访问地址
- **Web 管理控制台**: [http://127.0.0.1:8989/admin](http://127.0.0.1:8989/admin)
- **API 根地址**: [http://127.0.0.1:8989](http://127.0.0.1:8989)
- **默认管理员账号**: `admin`
- **默认管理员密码**: `admin123`

### 2. 脚本快捷操作
- **启动服务**: 双击运行 [start_server.bat](file:///e:/code/c++/UnityRTS/GodotRTS/tools/spark_verify/start_server.bat)
- **停止服务**: 双击运行 [stop_server.bat](file:///e:/code/c++/UnityRTS/GodotRTS/tools/spark_verify/stop_server.bat)
- **自动化测试**: 运行 `python test_system.py`

---

## 三、目录架构

```text
tools/spark_verify/
├── backend/                   # 后端核心服务
│   ├── main.py                # FastAPI 路由、管理后台接口、心跳扫毒协程
│   ├── database.py            # SQLite 轻量级存储与卡密全生命周期管理
│   ├── crypto_utils.py        # HMAC-SHA256 签名与 AES-256-GCM 通信加解密
│   └── spark_verify.db        # 自包含单文件数据库 (自动初始化)
├── web/                       # 现代化 Cyber 暗黑响应式管理后台
│   └── index.html             # 仪表大盘、制卡弹窗、在线监控、云变量面板
├── clients/                   # 多语言客户端 SDK 与集成示例
│   ├── python/                # Python 客户端 SDK & 交互式 Demo
│   ├── csharp/                # C# (.NET / Unity / Godot) 异步客户端
│   ├── cpp/                   # C++ (Win32 / WinINet) 客户端
│   ├── java/                  # Java / Android 客户端
│   └── e_lang/                # 易语言完整源码与模块调用说明
├── start_server.bat           # 一键启动批处理脚本
├── stop_server.bat            # 一键停止批处理脚本
└── test_system.py             # 9项核心全链路自动化测试套件
```

---

## 四、客户端 SDK 接入示例

### 1. Python 客户端接入
```python
from spark_verify_client import SparkVerifyClient

client = SparkVerifyClient(
    host="http://127.0.0.1:8989",
    app_id="APP_DEMO_001",
    app_secret="sv_secret_88889999_k9"
)

# 验证卡密并开启后台心跳守护
ok, msg, data = client.verify("SV-MONTH-XXXX-XXXX-XXXX", auto_heartbeat=True)
if ok:
    print(f"验证通过！到期时间: {data['expires_at']}")
    print(f"动态下发云变量: {client.cloud_vars}")
else:
    print(f"授权失败: {msg}")
```

### 2. 易语言客户端接入
参考 [易语言接入源码与调用教程.txt](file:///e:/code/c++/UnityRTS/GodotRTS/tools/spark_verify/clients/e_lang/易语言接入源码与调用教程.txt)，直接将源码复制到易语言程序中即可调用。
