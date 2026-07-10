# UnityRTS Server Deployment

The server is `Node.js + Express + WebSocket`.

- Account, lobby, room, invite, and queue data can run on MySQL.
- Live WebSocket match state is still in process memory.
- Because of that, PM2 is configured as a single instance.
- Hot updates are graceful for the process, but active matches will still disconnect during restart.

Choose the platform-specific guide:

- Linux: `Server/DEPLOY_LINUX.md`
- Windows Server: `Server/DEPLOY_WINDOWS.md`
- Native Linux without Docker/PM2: `Server/NATIVE_DEPLOY_LINUX.md`

If you want the simplest possible setup, use Docker Compose:

- Quick start: `Server/DOCKER_QUICKSTART.md`
- One-command stack: `Server/compose.fullstack.yaml`

## First deploy recommendation

For a production-like first deploy, use MySQL and run the explicit migration flow before opening the server to players:

1. Create `Server/.env` from `Server/.env.example`
2. Fill in:
   - `JWT_SECRET`
   - `MYSQL_HOST`
   - `MYSQL_PORT`
   - `MYSQL_USER`
   - `MYSQL_PASSWORD`
   - `MYSQL_DATABASE`
3. Keep `DB_BACKEND=mysql`
4. Run:

```bash
npm run migrate:mysql:dry
npm run migrate:mysql
npm run verify:mysql
```

Detailed notes are in `Server/MYSQL_BOOTSTRAP.md`.

## Portable deployment pattern

If you want to replace the app server later without losing persistent data:

1. Keep MySQL outside the app machine
2. Point `MYSQL_HOST` in `.env` to that external database
3. Start the app through:

```bash
docker compose -f compose.portable.yaml up -d --build
```

That makes the Node service disposable. You can recreate it on another server and keep the same MySQL data.

## Client connection

Set the client login server field to your public server IP or domain, for example:

```text
203.0.113.10:8080
```

WebSocket uses the same host on port `8081`, so your firewall and cloud security group must allow both:

- TCP `8080`
- TCP `8081`

## Scaling note

PM2 stays single-instance on purpose. Room and in-progress match state still live in memory, so multi-instance deployment would split players across processes.

If you later want real zero-downtime scaling, the next step is:

- move room/match session state to Redis or another shared store
- add reconnect/resume support for WebSocket sessions
