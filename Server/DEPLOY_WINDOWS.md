# Windows Server Deployment

Applies to Windows Server 2019/2022.

The service runs as a single PM2 process and restores through a scheduled task on login.

## Prerequisites

Install:

- Node.js LTS
- Git for Windows
- MySQL 8.x or a compatible server

## First install

```powershell
git clone <your-repo-url> UnityRTS
cd UnityRTS\Server
powershell -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1
```

The first run creates `Server\.env` and exits.

Edit it, then run the installer again:

```powershell
notepad .env
powershell -ExecutionPolicy Bypass -File .\scripts\install-windows.ps1
```

Required values:

- `JWT_SECRET`
- `MYSQL_HOST`
- `MYSQL_PORT`
- `MYSQL_USER`
- `MYSQL_PASSWORD`
- `MYSQL_DATABASE`

If you really need a temporary no-MySQL setup, you can set `DB_BACKEND=json`, but production deployment should use MySQL.

## First deploy data safety

Before letting players in, you can run the migration flow manually:

```powershell
npm run migrate:mysql:dry
npm run migrate:mysql
npm run verify:mysql
```

That migrates `Server\data\*.json` into MySQL and verifies the imported content.

## Hot update

```powershell
cd UnityRTS\Server
powershell -ExecutionPolicy Bypass -File .\scripts\hot-update.ps1 -Branch main
```

## Checks

```powershell
npm run health
pm2 status
pm2 logs unity-rts-server
```

Open inbound Windows Firewall and cloud security group rules for:

- TCP `8080`
- TCP `8081`

## Startup

`install-windows.ps1` registers the scheduled task `UnityRTS-PM2-Resurrect`, which runs `pm2 resurrect` after user login.

If you later need true background startup without login, wrap PM2 with NSSM or a dedicated Windows service.
