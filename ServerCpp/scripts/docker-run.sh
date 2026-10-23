#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

MODE="${1:-fullstack}"

cd "$ROOT_DIR"
mkdir -p logs data backups

if [ ! -f .env ]; then
  echo "==> Creating .env from config/server.env.example..."
  cp config/server.env.example .env
fi

if [ "$MODE" = "standalone" ]; then
  echo "==> Starting UnityRTS C++ Server in standalone (zero-dependency) mode..."
  docker compose -f compose.standalone.yaml up -d --build
else
  echo "==> Starting UnityRTS C++ Server with MySQL and Redis..."
  docker compose -f compose.yaml up -d --build
fi

echo "==> Server started! Checking health..."
sleep 3
docker compose ps
