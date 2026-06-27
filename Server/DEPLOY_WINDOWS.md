# Windows Server 部署版本

适用于 Windows Server 2019/2022。服务使用 PM2 单实例运行，并通过计划任务在登录时恢复 PM2 进程。

## 首次安装

先安装：

- Node.js LTS
- Git for Windows
- MySQL 8.x 或兼容版本

然后在 PowerShell 中执行：

```powershell
git clone <your-repo-url> UnityRTS
cd UnityRTS\Server
powershell -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1
```

第一次运行会自动生成 `Server\.env` 并退出。编辑数据库配置后再运行一次：

```powershell
notepad .env
powershell -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1
```

必须修改：

- `MYSQL_HOST`
- `MYSQL_USER`
- `MYSQL_PASSWORD`
- `MYSQL_DATABASE`

如果暂时没有 MySQL，可把 `DB_BACKEND=json`，但正式服务器建议用 MySQL。

## 热更新

```powershell
cd UnityRTS\Server
powershell -ExecutionPolicy Bypass -File .\scripts\hot-update.ps1 -Branch main
```

## 检查

```powershell
npm run health
pm2 status
pm2 logs unity-rts-server
```

Windows 防火墙需要放行入站 TCP `8080` 和 `8081`。云服务器安全组也要同时放行这两个端口。

## 开机自启

`install-windows.ps1` 默认注册计划任务 `UnityRTS-PM2-Resurrect`，用户登录 Windows 后会执行 `pm2 resurrect` 恢复服务。

如果需要“不登录也启动”的服务模式，建议后续用 NSSM 或专门的 Windows service 包装 PM2；当前脚本先走稳定简单的登录自启。
