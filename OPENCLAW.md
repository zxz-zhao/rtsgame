# OpenClaw 一键安装与接入说明

## 功能清单

- 支持一键安装 OpenClaw，无需手动处理复杂的 Node、npm 和 Gateway 配置。
- 支持通过 `openclaw onboard` 添加模型商/API Key，并在后续切换默认模型。
- 支持微信、QQ Bot、飞书、Telegram 等聊天渠道接入。
- 支持接入自备模型商 API Key 或 OpenAI 兼容网关服务；本仓库不保存、不代管密钥。
- 兼容 Windows、macOS、Linux/WSL2 系统。

## 一键安装

Windows:

```powershell
.\OpenClaw-OneClick-Install.cmd
```

macOS / Linux / WSL2:

```bash
chmod +x ./OpenClaw-OneClick-Install.sh ./Tools/InstallOpenClawUnix.sh
./OpenClaw-OneClick-Install.sh
```

安装后常用命令:

```bash
openclaw --version
openclaw onboard --install-daemon
openclaw dashboard
openclaw gateway status
openclaw doctor
```

## 模型商与渠道

模型商/API Key 通常在 onboarding 流程中配置:

```bash
openclaw onboard --install-daemon
```

如果只想重新配置模型，可在安装后运行:

```bash
openclaw configure
openclaw models set <provider>/<model>
```

聊天渠道也走 OpenClaw 的 Gateway/插件体系。Telegram 通常只需要 Bot Token；微信、QQ Bot、飞书需要按对应平台准备机器人、登录或应用凭据。

## 安全说明

- 不要把 API Key、机器人 Token、会话文件提交到仓库。
- 本地使用时优先把 Gateway 绑定在 `127.0.0.1`。
- 先用最小权限完成接入，再逐步打开文件、Shell 或远程控制能力。

## 参考

- OpenClaw 安装: https://docs.openclaw.ai/install
- OpenClaw 渠道: https://docs.openclaw.ai/channels
- OpenClaw 模型商: https://docs.openclaw.ai/providers
- OpenClaw 平台: https://docs.openclaw.ai/platforms
