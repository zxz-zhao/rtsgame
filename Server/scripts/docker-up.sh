#!/usr/bin/env bash
set -euo pipefail

APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$APP_DIR"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required. Install Docker first."
  exit 1
fi

if [ ! -f .env ]; then
  cp .env.example .env
  if command -v openssl >/dev/null 2>&1; then
    secret="$(openssl rand -hex 32)"
    sed -i "s/^JWT_SECRET=.*/JWT_SECRET=${secret}/" .env
  fi
  echo "Created Server/.env."
  echo "Review MYSQL_PASSWORD and MYSQL_ROOT_PASSWORD if needed, then rerun this script."
  exit 0
fi

docker compose -f compose.fullstack.yaml up -d --build
echo "Docker full-stack deployment started."
echo "Check status with: docker compose -f compose.fullstack.yaml ps"
