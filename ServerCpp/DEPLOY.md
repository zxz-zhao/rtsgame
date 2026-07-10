# UnityRTS Native Server Deployment

This document describes the non-Docker deployment path for the C++ server package on Linux and Windows.

## Deployment Model

- `unity_rts_server_cpp` is the actual game service process.
- `unity_rts_guardian_cpp` is the watchdog process.
- The operating system starts the guardian on boot.
- The guardian restarts the server after crashes and writes guardian/alert logs.
- The native server now uses MySQL for durable collections and Redis for realtime collections.
- JSON snapshots are still mirrored into `data/` for backup/bootstrap.
- Daily logs are written into dated files such as `logs/server-2026-07-06.log`.
- Redis/MySQL remain the planned next step for larger-scale realtime/persistent storage.

## Package Layout

After packaging or extracting a release artifact, the install root should look like this:

```text
<install-root>/
  bin/
    unity_rts_server_cpp(.exe)
    unity_rts_guardian_cpp(.exe)
  scripts/
  logs/
  data/
  backups/
  .env
  .env.example
  README.md
  DEPLOY.md
  MIGRATION.md
```

## Before First Deployment

1. Open the service ports in the firewall.
2. Extract the release package to a permanent directory.
3. Copy `.env.example` to `.env`.
4. Fill in bind/port/logging settings plus the actual MySQL/Redis connection information.
5. Set `ADMIN_API_SECRET` before enabling `/api/payments/confirm`.
6. If you want the packaged server to load a bundled MySQL client DLL on Windows, keep `libmysql.dll` beside `bin/unity_rts_server_cpp.exe` or set `MYSQL_CLIENT_LIBRARY`.
7. If migrating an existing native deployment, copy the old `data/` directory before first start.

Recommended permanent paths:

- Linux: `/opt/unity-rts-server`
- Windows: `C:\UnityRTS\ServerCpp`

## Linux One-Click Deployment

Requirements:

- `bash`
- `systemd`
- `sudo`
- A package built from `scripts/package-linux.sh`

Example:

```bash
tar -xzf unity-rts-server-cpp-0.1.0-Linux-x86_64.tar.gz -C /tmp
cd /tmp/unity-rts-server-cpp-0.1.0-Linux-x86_64
cp .env.example .env
vim .env
sudo ./scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts
```

What the deployment script does:

- creates the install directory if needed
- copies binaries, docs, scripts, and runtime folders
- preserves an existing `.env`
- installs the `unity-rts-guardian.service` unit
- enables and restarts the service

After deployment:

```bash
sudo systemctl status unity-rts-guardian
curl http://127.0.0.1:8080/health
tail -f /opt/unity-rts-server/logs/server.log
tail -f /opt/unity-rts-server/logs/guardian.log
tail -f /opt/unity-rts-server/logs/alerts.log
```

## Windows One-Click Deployment

Requirements:

- PowerShell 5.1+
- Administrator privileges
- A package built from `scripts/package-windows.ps1`

Example:

```powershell
Expand-Archive .\unity-rts-server-cpp-0.1.0-Windows-AMD64.zip -DestinationPath C:\Deploy\unity-rts-server-cpp
Set-Location C:\Deploy\unity-rts-server-cpp
Copy-Item .env.example .env
notepad .env
.\scripts\deploy-windows.ps1 -InstallDir C:\UnityRTS\ServerCpp
```

What the deployment script does:

- copies the release into the target directory
- preserves an existing `.env`
- creates runtime directories
- registers a scheduled task named `UnityRTSGuardian`
- starts the scheduled task immediately

After deployment:

```powershell
Invoke-RestMethod http://127.0.0.1:8080/health
Get-Content C:\UnityRTS\ServerCpp\logs\server.log -Tail 50 -Wait
Get-Content C:\UnityRTS\ServerCpp\logs\guardian.log -Tail 50 -Wait
Get-Content C:\UnityRTS\ServerCpp\logs\alerts.log -Tail 50 -Wait
```

## Restart And Update

For a normal binary update:

1. Keep the existing `.env`.
2. Stop the guardian service/task.
3. Replace `bin/` and `scripts/` from the new package.
4. Start the guardian service/task again.
5. Check `/health` and logs.

Linux:

```bash
sudo systemctl stop unity-rts-guardian
sudo ./scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts
sudo systemctl restart unity-rts-guardian
```

Windows:

```powershell
Stop-ScheduledTask -TaskName UnityRTSGuardian
.\scripts\deploy-windows.ps1 -InstallDir C:\UnityRTS\ServerCpp
Start-ScheduledTask -TaskName UnityRTSGuardian
```

## Logging And Alerts

- `logs/server-YYYY-MM-DD.log`: application process log
- `logs/guardian.log`: watchdog lifecycle and restart log
- `logs/alerts.log`: alert events

Logging is asynchronous and queue-based to reduce request-thread blocking.

For external alerts, set `ALERT_COMMAND` in `.env`.

Examples:

Linux:

```env
ALERT_COMMAND=/opt/unity-rts-server/scripts/alert-linux.sh ${TITLE} ${MESSAGE}
```

Windows:

```env
ALERT_COMMAND=powershell -ExecutionPolicy Bypass -File "C:/UnityRTS/ServerCpp/scripts/alert-windows.ps1" ${TITLE} ${MESSAGE}
```

## Health Check

The current scaffold exposes:

- `GET /`
- `GET /health`

`/health` returns build, port, storage, and log-path information.

## Native Payment And Battle Notes

- Payment orders and battle sessions are persisted in MySQL and mirrored into `data/*.json`.
- Waiting rooms, matchmaking queue, and pending matches are persisted in Redis and mirrored into `data/*.json`.
- `/api/payments/confirm` is intended for your internal payment callback or GM/admin tooling, not for the public client.
- `/api/result` should be paired with `/api/battle/session/start`; battle results now settle against a server-issued battle id.

## Important Note

This delivery now includes the main Node HTTP gameplay/business APIs, the 8081 battle relay, and the MySQL/Redis storage split. The remaining architecture gap is moving the actual battle simulation to the server side.
