# UnityRTS Native Server Migration Guide

This document explains how to move the deployed C++ server to another machine without breaking service continuity.

## Main Principle

With the current native server, machine replacement is mainly about carrying over:

- the new release package
- the current `.env`
- the local `data/` directory
- optional `backups/`

If you later move to external MySQL/Redis, the copy checklist becomes smaller, but today the JSON collections are the source of truth for native runtime state.

## Files To Copy

Always copy:

- `.env`
- any custom alert scripts under `scripts/`
- any TLS certificates or key files referenced by `.env`

Copy if you actually use local file persistence:

- `data/`
- `backups/`

Important `data/` files currently include:

- `users.json`
- `friends.json`
- `rooms.json`
- `match_queue.json`
- `pending_matches.json`
- `invites.json`
- `sessions.json`

Copy only if you want old diagnostics:

- `logs/`

## Files You Usually Do Not Need To Copy

- `bin/`
  Use the new package instead of old binaries.
- `.env.example`
  It is only a template.
- old `logs/`
  Optional for troubleshooting, not required for service startup.
- temporary extracted package directories
- build artifacts such as `build/`, `dist/`, `out/`

## Future Database And Redis Checklist

These items matter once you switch the native server over to external MySQL/Redis:

1. Confirm the new machine can reach MySQL.
2. Confirm the new machine can reach Redis.
3. Confirm MySQL account permissions match the old server.
4. Confirm Redis password and bind/firewall rules match the old server.
5. Confirm `.env` points to the same database and Redis endpoints you intend to keep.

## Recommended Migration Procedure

1. Prepare the new server machine.
2. Install required runtime access such as firewall rules and service permissions.
3. Extract the new package.
4. Copy `.env` from the old machine.
5. Copy `data/` and `backups/` if used.
6. Run the one-click deployment script.
7. Validate `GET /health`.
8. Check `logs/server.log`, `logs/guardian.log`, and `logs/alerts.log`.
9. Move traffic to the new machine.
10. Keep the old machine powered but drained until validation is complete.

## Linux Migration Example

On the old server:

```bash
cd /opt/unity-rts-server
tar -czf /tmp/unity-rts-migration.tgz .env data backups
```

On the new server:

```bash
tar -xzf unity-rts-server-cpp-0.1.0-Linux-x86_64.tar.gz -C /tmp
cd /tmp/unity-rts-server-cpp-0.1.0-Linux-x86_64
sudo tar -xzf /tmp/unity-rts-migration.tgz -C .
sudo ./scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts
curl http://127.0.0.1:8080/health
```

## Windows Migration Example

On the old server:

```powershell
Compress-Archive -Path C:\UnityRTS\ServerCpp\.env,C:\UnityRTS\ServerCpp\data,C:\UnityRTS\ServerCpp\backups -DestinationPath C:\Temp\unity-rts-migration.zip -Force
```

On the new server:

```powershell
Expand-Archive .\unity-rts-server-cpp-0.1.0-Windows-AMD64.zip -DestinationPath C:\Deploy\unity-rts-server-cpp -Force
Set-Location C:\Deploy\unity-rts-server-cpp
Expand-Archive C:\Temp\unity-rts-migration.zip -DestinationPath . -Force
.\scripts\deploy-windows.ps1 -InstallDir C:\UnityRTS\ServerCpp
Invoke-RestMethod http://127.0.0.1:8080/health
```

## Rollback Plan

If the new server fails:

1. Stop the guardian on the new server.
2. Point traffic back to the old server.
3. Inspect `alerts.log`, `guardian.log`, and `server.log`.
4. Fix configuration or connectivity issues.
5. Retry the migration after validation.

## Zero-Surprise Rule

To make machine replacement safe, keep these items outside the binary package lifecycle:

- `.env`
- MySQL data
- Redis data
- optional persistent local `data/`
- optional backups

That is the core reason this deployment path remains stable when you replace the server machine.
