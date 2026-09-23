# 企业微信群机器人 Webhook 播报系统 (H:\webbot)

本套系统专为游戏开发进度、代码构建状态与高清效果截图自动推送设计，100% 官方免费、原生免认证。

## 目录结构
```text
H:\webbot\
├── config.json          # 核心配置文件（填入你的企微 Webhook URL）
├── wecom_sender.py      # 核心发送逻辑（Markdown卡片排版、自动截图、大图 Base64+MD5 压缩处理）
├── server.py            # 本地 HTTP API 网关服务（监听 5005 端口，支持任意工具/脚本/curl 触发）
├── send_report.py       # 命令行一键发送工具（自动抓取最新成果清单并附带当前屏幕截图）
├── start_server.bat     # Windows 双击启动本地网关服务
├── test_send.bat        # Windows 双击立即测试发送
└── README.md            # 说明文档
```

## 快速使用

### 1. 配置 Webhook URL
在企业微信群中添加机器人后，复制 Webhook 地址：
- 方式 A：打开 `H:\webbot\config.json`，将 `webhook_url` 替换为你的真实地址。
- 方式 B：在命令行执行：
  ```bash
  E:\python13\python.exe H:\webbot\send_report.py --set-webhook "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=xxxxxx"
  ```

### 2. 手动或一键发送战报 + 截图
```bash
# 默认发送最新战报并自动截取当前屏幕大图推送到群里
E:\python13\python.exe H:\webbot\send_report.py

# 发送自定义标题与说明（附带截屏）
E:\python13\python.exe H:\webbot\send_report.py --title "步兵走路与攻击特效测试" --msg "1. 踏步沙尘粒子正常\\n2. 枪口后坐力自然"

# 指定发送某张已有的游戏高清截图
E:\python13\python.exe H:\webbot\send_report.py --image "E:\code\c++\UnityRTS\battle_clarity.png"
```

### 3. 本地 HTTP 网关（支持任意脚本/编译 Hook 调用）
双击 `start_server.bat` 或运行：
```bash
E:\python13\python.exe H:\webbot\server.py
```
启动后支持：
- 浏览器或 curl 触发截屏发送：`http://127.0.0.1:5005/capture_and_send`
- 任意程序 POST 发送：
  ```bash
  curl -X POST http://127.0.0.1:5005/send -H "Content-Type: application/json" -d "{\"title\":\"编译完成\",\"items\":[\"0 错误通过\",\"步兵特效生效\"],\"capture\":true}"
  ```
