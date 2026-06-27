# UnityRTS 服务端部署

服务端是 Node.js + Express + WebSocket。账号、大厅、匹配等数据可写入 MySQL；正在进行的 WebSocket 对局仍在内存中，所以热更新会优雅重启进程，但正在打的局会断开。

按服务器系统选择一份部署文档：

- Linux：`Server/DEPLOY_LINUX.md`
- Windows Server：`Server/DEPLOY_WINDOWS.md`

客户端登录界面的服务器地址填云服务器 IP 或域名，例如：

```text
203.0.113.10:8080
```

WebSocket 会使用同一个 IP 的 `8081` 端口，所以防火墙和云安全组必须同时放行 `8080` 和 `8081`。

当前 PM2 配置固定单实例。原因是房间和 WebSocket 对局状态在内存里，多实例会导致同一房间玩家可能落到不同进程。要做到真正不停服，需要下一步把房间/对局状态移到 Redis 或数据库，并让 WebSocket 支持重连恢复。
