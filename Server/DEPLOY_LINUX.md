# Linux 部署版本

适用于 Ubuntu / Debian / CentOS / Rocky Linux 等云服务器。服务使用 PM2 单实例运行，端口默认 `8080` 和 `8081`。

## 首次安装

```bash
git clone <your-repo-url> UnityRTS
cd UnityRTS/Server
bash scripts/install-linux.sh
```

第一次运行会自动生成 `Server/.env` 并退出。编辑数据库配置后再运行一次：

```bash
nano .env
bash scripts/install-linux.sh
```

必须修改：

- `MYSQL_HOST`
- `MYSQL_USER`
- `MYSQL_PASSWORD`
- `MYSQL_DATABASE`

如果暂时没有 MySQL，可把 `DB_BACKEND=json`，但正式服务器建议用 MySQL。

## 开机自启

安装脚本启动服务并执行 `pm2 save` 后，再运行：

```bash
pm2 startup
```

按 PM2 输出的命令复制执行一次即可。

## 热更新

```bash
cd UnityRTS/Server
bash scripts/hot-update.sh
```

指定分支：

```bash
BRANCH=main bash scripts/hot-update.sh
```

## 检查

```bash
curl http://127.0.0.1:8080/health
pm2 status
pm2 logs unity-rts-server
```

云服务器安全组/防火墙需要放行 TCP `8080` 和 `8081`。
