# Native Linux Production Deploy

Use this when you do not want Docker and want a more production-style server setup.

This mode uses:

- Node.js LTS runtime
- systemd service management
- external MySQL or a MySQL service installed on the host
- no Docker
- no PM2

## Recommended topology

For higher reliability:

- Run the Node app on an application server
- Run MySQL on a separate database server or managed database
- Keep `DB_BACKEND=mysql`
- Open only TCP `8080` and `8081` to players
- Keep MySQL private to the app server or VPC

## Install prerequisites

Ubuntu/Debian example:

```bash
sudo apt update
sudo apt install -y nodejs npm git
```

For production, prefer installing Node.js LTS from NodeSource or your server image instead of an outdated distro package.
If you use `nvm` or a custom path, pass `NODE_BIN` and `NPM_BIN` when running the installer.

## First install

```bash
git clone <your-repo-url> UnityRTS
cd UnityRTS/Server
sudo APP_DIR="$PWD" RUN_USER="$USER" bash scripts/install-linux-systemd.sh
```

The first run creates `.env` and exits.

Edit `.env`:

```bash
nano .env
```

Required values:

- `JWT_SECRET`
- `DB_BACKEND=mysql`
- `MYSQL_HOST`
- `MYSQL_PORT`
- `MYSQL_USER`
- `MYSQL_PASSWORD`
- `MYSQL_DATABASE`

Run the installer again:

```bash
sudo APP_DIR="$PWD" RUN_USER="$USER" bash scripts/install-linux-systemd.sh
```

## Operations

Check service:

```bash
systemctl status unity-rts-server
```

Follow logs:

```bash
journalctl -u unity-rts-server -f
```

Restart:

```bash
sudo systemctl restart unity-rts-server
```

Stop:

```bash
sudo systemctl stop unity-rts-server
```

## Update

```bash
cd UnityRTS/Server
git pull --ff-only
npm ci --omit=dev
npm run check
npm run migrate:mysql
sudo systemctl restart unity-rts-server
```

## High-concurrency next step

This native deploy removes Docker/PM2 overhead, but horizontal scaling still needs shared realtime state.

The next real scalability step is:

- move WebSocket room/session state from memory to Redis or another shared store
- allow multiple app servers behind a load balancer
- add client reconnect/resume support
