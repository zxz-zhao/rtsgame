#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
SERVICE_NAME="${SERVICE_NAME:-unity-rts-server}"
RUN_USER="${RUN_USER:-$(id -un)}"
NODE_BIN="${NODE_BIN:-$(command -v node || command -v nodejs || true)}"
NPM_BIN="${NPM_BIN:-$(command -v npm || true)}"

cd "$APP_DIR"
mkdir -p logs backups

if [ "$(id -u)" -ne 0 ]; then
  echo "Run with sudo/root so the systemd service can be installed."
  echo "Example: sudo APP_DIR=$APP_DIR RUN_USER=$(id -un) bash scripts/install-linux-systemd.sh"
  exit 1
fi

if [ -z "$NODE_BIN" ] || [ ! -x "$NODE_BIN" ]; then
  echo "node is required. Install Node.js LTS first."
  echo "If you use nvm or a custom install path, export NODE_BIN=/full/path/to/node and retry."
  exit 1
fi

if [ -z "$NPM_BIN" ] || [ ! -x "$NPM_BIN" ]; then
  echo "npm is required. Install Node.js LTS first."
  echo "If you use nvm or a custom install path, export NPM_BIN=/full/path/to/npm and retry."
  exit 1
fi

if [ ! -f .env ]; then
  cp .env.example .env
  if command -v openssl >/dev/null 2>&1; then
    secret="$(openssl rand -hex 32)"
    sed -i "s/^JWT_SECRET=.*/JWT_SECRET=${secret}/" .env
  fi
  echo "Created $APP_DIR/.env."
  echo "Edit MySQL settings and rerun this script."
  exit 0
fi

npm ci --omit=dev
npm run check
npm run migrate:mysql

SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=UnityRTS Node Game Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=${RUN_USER}
WorkingDirectory=${APP_DIR}
EnvironmentFile=${APP_DIR}/.env
Environment=NODE_ENV=production
ExecStartPre=${NPM_BIN} run migrate:mysql
ExecStart=${NODE_BIN} server.js
Restart=always
RestartSec=5
KillSignal=SIGTERM
TimeoutStopSec=20
LimitNOFILE=65535
StandardOutput=journal
StandardError=journal
SyslogIdentifier=${SERVICE_NAME}

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl restart "$SERVICE_NAME"

echo "Installed and started ${SERVICE_NAME}."
echo "Check status: systemctl status ${SERVICE_NAME}"
echo "View logs: journalctl -u ${SERVICE_NAME} -f"
