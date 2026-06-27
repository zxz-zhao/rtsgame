#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

mkdir -p logs

if ! command -v node >/dev/null 2>&1; then
  echo "node is required. Install Node.js LTS first."
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required. Install Node.js LTS first."
  exit 1
fi

if ! command -v pm2 >/dev/null 2>&1; then
  echo "pm2 was not found; installing it globally with npm."
  npm install -g pm2
fi

if [ ! -f .env ]; then
  cp .env.example .env
  if command -v openssl >/dev/null 2>&1; then
    secret="$(openssl rand -hex 32)"
    sed -i "s/^JWT_SECRET=.*/JWT_SECRET=${secret}/" .env
  fi
  sed -i "s/^NODE_ENV=.*/NODE_ENV=production/" .env
  echo "Created Server/.env."
  echo "Edit MySQL settings and rerun this script."
  exit 0
fi

npm ci --omit=dev
npm run check
pm2 startOrReload ecosystem.config.cjs --env production --update-env
npm run health
pm2 save

echo "Install complete. Open TCP ports from .env: PORT and WS_PORT."
echo "To enable boot startup, run the command printed by: pm2 startup"
