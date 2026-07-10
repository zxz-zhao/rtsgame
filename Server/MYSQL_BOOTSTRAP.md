# MySQL First Deploy

Use this flow the first time you move the server from JSON files to MySQL.

## What already exists

- The game server is in `Server/server.js`.
- Runtime collections live in `Server/data/*.json`.
- MySQL support is already built in through `mysql2`.
- The server stores collections in one JSON-backed key/value table: `rts_kv_store`.

## First deployment steps

1. Copy `Server/.env.example` to `Server/.env`.
2. Fill in `JWT_SECRET`, `MYSQL_HOST`, `MYSQL_PORT`, `MYSQL_USER`, `MYSQL_PASSWORD`, and `MYSQL_DATABASE`.
3. Keep `DB_BACKEND=mysql`.
4. Install packages:

```bash
npm ci --omit=dev
```

5. Preview the migration:

```bash
npm run migrate:mysql:dry
```

6. Bootstrap missing MySQL rows from `Server/data/*.json`:

```bash
npm run migrate:mysql
```

7. Verify that MySQL matches the JSON source snapshot before live traffic starts:

```bash
npm run verify:mysql
```

8. Start the service with PM2:

```bash
npm run pm2:start
```

Or with Docker on a replacement server:

```bash
docker compose -f compose.portable.yaml up -d --build
```

## Notes

- `migrate:mysql` is safe to rerun. It only inserts missing collections by default.
- If MySQL already has different data, the script skips that collection instead of overwriting it.
- To overwrite differing MySQL rows on purpose, run:

```bash
npm run migrate:mysql -- --force
```

- Forced overwrites automatically write a backup file under `Server/backups/` unless you pass `--backup-file`.
- `verify:mysql` is meant for first deployment validation. After production traffic starts, JSON files and MySQL can diverge normally.
- If you want changing app servers to have no effect on account and lobby data, keep MySQL outside the app machine.
- Active WebSocket matches are still in memory, so an app-server switch or restart can interrupt a live match.
