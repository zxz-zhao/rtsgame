# Docker Quick Start

If you want the fewest moving parts on a fresh server, use Docker Compose.

## What you need

- Docker Engine or Docker Desktop
- That is all

## Windows

```powershell
cd Server
powershell -ExecutionPolicy Bypass -File .\scripts\docker-up.ps1
```

The first run creates `.env` and exits. Run it again after checking the generated file.

## Linux

```bash
cd Server
bash scripts/docker-up.sh
```

The first run creates `.env` and exits. Run it again after checking the generated file.

## What starts

- `mysql` container
- `unity-rts-server` container

## Default ports

- HTTP API: `8080`
- WebSocket relay: `8081`
- MySQL: `3306`

## Notes

- Data persists in the Docker volume `mysql_data`
- If you want changing app servers to be easier later, use `compose.portable.yaml` with an external MySQL instead
