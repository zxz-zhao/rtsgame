#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
REMOTE="${REMOTE:-origin}"

cd "$APP_DIR"
mkdir -p logs

if ! command -v git >/dev/null 2>&1; then
  echo "git is required for hot-update.sh."
  exit 1
fi

if ! command -v pm2 >/dev/null 2>&1; then
  echo "pm2 is required. Run scripts/install-linux.sh first."
  exit 1
fi

if [ ! -f .env ]; then
  echo "Server/.env is missing. Copy .env.example to .env and fill production values first."
  exit 1
fi

BRANCH="${BRANCH:-$(git rev-parse --abbrev-ref HEAD)}"
if [ "$BRANCH" = "HEAD" ]; then
  echo "Repository is in detached HEAD. Set BRANCH=<branch-name> and retry."
  exit 1
fi

old_rev="$(git rev-parse --short HEAD)"
git fetch "$REMOTE" "$BRANCH"
git pull --ff-only "$REMOTE" "$BRANCH"
new_rev="$(git rev-parse --short HEAD)"

npm ci --omit=dev
npm run check

BUILD_REVISION="$new_rev" pm2 startOrReload ecosystem.config.cjs --env production --update-env
BUILD_REVISION="$new_rev" npm run health
pm2 save

echo "Hot update complete: ${old_rev} -> ${new_rev}"
