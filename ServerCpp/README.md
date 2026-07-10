# UnityRTS Native Server

This is the native cross-platform C++ server for UnityRTS.

## Current Scope

- Native executable for Windows and Linux
- Built-in HTTP server
- `/health` endpoint
- Runtime config loading from `.env`
- Async log pipeline with daily files and size rotation
- Guardian/watchdog process with restart policy
- Native install/package layout for standalone deployment
- One-click deployment scripts for Linux and Windows
- MySQL-backed durable collections plus Redis-backed realtime collections
- Local JSON snapshots kept in `data/` as cold backup / migration seed
- Persistent server-side session tokens that survive process restarts
- Payment order persistence with protected confirmation callback
- Server-issued battle session records for result settlement
- Native 8081 battle WebSocket relay with room membership auth and command validation

## HTTP APIs Ported

The following Node HTTP APIs are now available in `ServerCpp`:

- `/api/register`
- `/api/login`
- `/api/guest`
- `/api/profile`
- `/api/lobby`
- `/api/presence`
- `/api/tasks/claim`
- `/api/tech/start`
- `/api/tech/speedup`
- `/api/friends`
- `/api/leaderboard`
- `/api/friends/add`
- `/api/friends/invite`
- `/api/invites`
- `/api/invites/respond`
- `/api/rooms`
- `/api/rooms/create`
- `/api/rooms/join`
- `/api/rooms/leave`
- `/api/match/join`
- `/api/match/cancel`
- `/api/payments/catalog`
- `/api/payments`
- `/api/payments/create`
- `/api/payments/confirm`
- `/api/battle/session/start`
- `/api/result`

## Security Additions

- Daily logs are written as `logs/server-YYYY-MM-DD.log` and still rotate by size into `.1`.
- Recharge flow is split into:
  - client-visible order creation: `/api/payments/create`
  - protected confirmation callback: `/api/payments/confirm`
- `/api/payments/confirm` requires `x-admin-secret` and `ADMIN_API_SECRET` to be configured.
- `/api/result` no longer trusts arbitrary free-form battle rewards. It now requires a server-known battle session and records one report per user per battle.
- A battle only settles after all expected participants report, or it falls into `review` if both sides claim victory.

## Remaining Gaps

`ServerCpp` now covers the HTTP business APIs, the 8081 battle relay, and the MySQL/Redis storage path.

The major remaining gap is full server-authoritative battle simulation. Commands are now server-authenticated and validated before relay, but the actual RTS simulation still runs on the clients.

## Payment Flow Notes

This tree now includes a native payment order scaffold for RMB -> gems:

1. Client calls `/api/payments/catalog` to read gem products.
2. Client calls `/api/payments/create` after login to create a pending order.
3. Your payment backend or manual ops callback calls `/api/payments/confirm` with `x-admin-secret`.
4. The server credits gems exactly once and records the processed order id on the user.

This is a protected order-credit flow, but it is still not a full third-party RMB SDK integration yet.

## Build

### Windows (MSVC)

```powershell
cmake --preset windows-msvc-release
cmake --build --preset windows-msvc-release
cpack --config build/windows-msvc-release/CPackConfig.cmake -C Release
```

### Windows (MinGW)

```powershell
cmake --preset windows-mingw-release
cmake --build --preset windows-mingw-release
cpack --config build/windows-mingw-release/CPackConfig.cmake
```

### Linux

```bash
cmake --preset linux-gcc-release
cmake --build --preset linux-gcc-release
cpack --config build/linux-gcc-release/CPackConfig.cmake
```

Packages are written to `ServerCpp/dist/`.

## Runtime Layout

Each package contains:

- `bin/unity_rts_server_cpp`
- `bin/unity_rts_guardian_cpp`
- `scripts/`
- `DEPLOY.md`
- `MIGRATION.md`
- `.env.example`
- `logs/`
- `data/`
- `backups/`

## Deployment

Packaging:

- Windows: `./scripts/package-windows.ps1`
- Linux: `./scripts/package-linux.sh`

Native deployment:

- Linux: `./scripts/deploy-linux.sh --install-dir /opt/unity-rts-server --user unityrts --group unityrts`
- Windows: `.\scripts\deploy-windows.ps1 -InstallDir C:\UnityRTS\ServerCpp`

See:

- `DEPLOY.md` for one-click install and service registration
- `MIGRATION.md` for machine-to-machine migration and copy checklist

## Current Storage Split

- MySQL: `users`, `sessions`, `friends`, `invites`, `payment_orders`, `battle_sessions`
- Redis: `rooms`, `match_queue`, `pending_matches`
- JSON files in `data/`: snapshot mirror and bootstrap seed when a remote collection is empty
