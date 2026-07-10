# Linux Deployment

Applies to Ubuntu, Debian, CentOS, Rocky Linux, and similar cloud servers.

The server runs as a single PM2 instance and listens on `8080` and `8081` by default.

## First install

```bash
git clone <your-repo-url> UnityRTS
cd UnityRTS/Server
bash scripts/install-linux.sh
```

The first run creates `Server/.env` and exits.

Edit it, then run the installer again:

```bash
nano .env
bash scripts/install-linux.sh
```

Required values:

- `JWT_SECRET`
- `MYSQL_HOST`
- `MYSQL_PORT`
- `MYSQL_USER`
- `MYSQL_PASSWORD`
- `MYSQL_DATABASE`

If you absolutely need a temporary fallback, set `DB_BACKEND=json`, but a real deployment should use MySQL.

## First deploy data safety

Before switching real traffic over, run:

```bash
npm run migrate:mysql:dry
npm run migrate:mysql
npm run verify:mysql
```

That imports `Server/data/*.json` into MySQL and verifies the result.

## Boot startup

After the install script starts the service and runs `pm2 save`, enable startup:

```bash
pm2 startup
```

Run the command PM2 prints for your distro.

## Hot update

```bash
cd UnityRTS/Server
bash scripts/hot-update.sh
```

For a specific branch:

```bash
BRANCH=main bash scripts/hot-update.sh
```

## Checks

```bash
curl http://127.0.0.1:8080/health
pm2 status
pm2 logs unity-rts-server
```

Open these ports in both the host firewall and your cloud security group:

- TCP `8080`
- TCP `8081`
