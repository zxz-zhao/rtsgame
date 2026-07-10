#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="/opt/unity-rts-server"
SERVICE_NAME="unity-rts-guardian"
RUN_USER="unityrts"
RUN_GROUP="unityrts"
SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_EXAMPLE_SOURCE="$SOURCE_DIR/.env.example"

if [[ ! -f "$ENV_EXAMPLE_SOURCE" ]]; then
  ENV_EXAMPLE_SOURCE="$SOURCE_DIR/config/server.env.example"
fi

sync_dir() {
  local from_dir="$1"
  local to_dir="$2"

  sudo mkdir -p "$to_dir"
  if command -v rsync >/dev/null 2>&1; then
    sudo rsync -a --delete "$from_dir/" "$to_dir/"
  else
    sudo cp -a "$from_dir/." "$to_dir/"
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-dir)
      INSTALL_DIR="$2"
      shift 2
      ;;
    --user)
      RUN_USER="$2"
      shift 2
      ;;
    --group)
      RUN_GROUP="$2"
      shift 2
      ;;
    --service-name)
      SERVICE_NAME="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1"
      exit 1
      ;;
  esac
done

if ! command -v systemctl >/dev/null 2>&1; then
  echo "systemd is required for deploy-linux.sh"
  exit 1
fi

if [[ ! -d "$SOURCE_DIR/bin" ]]; then
  echo "Packaged layout not found. Expected directory: $SOURCE_DIR/bin"
  exit 1
fi

if [[ ! -f "$SOURCE_DIR/.env" && ! -f "$INSTALL_DIR/.env" ]]; then
  echo "No .env found. Create one in the package root before deployment."
  exit 1
fi

sudo mkdir -p "$INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR/bin" "$INSTALL_DIR/scripts" "$INSTALL_DIR/logs" "$INSTALL_DIR/data" "$INSTALL_DIR/backups"

if ! getent group "$RUN_GROUP" >/dev/null 2>&1; then
  sudo groupadd --system "$RUN_GROUP"
fi

if ! id -u "$RUN_USER" >/dev/null 2>&1; then
  sudo useradd --system --home "$INSTALL_DIR" --shell /usr/sbin/nologin --gid "$RUN_GROUP" "$RUN_USER"
fi

sync_dir "$SOURCE_DIR/bin" "$INSTALL_DIR/bin"
sync_dir "$SOURCE_DIR/scripts" "$INSTALL_DIR/scripts"
sync_dir "$SOURCE_DIR/logs" "$INSTALL_DIR/logs"
sync_dir "$SOURCE_DIR/data" "$INSTALL_DIR/data"
sync_dir "$SOURCE_DIR/backups" "$INSTALL_DIR/backups"
sudo install -m 644 "$SOURCE_DIR/README.md" "$INSTALL_DIR/README.md"
sudo install -m 644 "$SOURCE_DIR/DEPLOY.md" "$INSTALL_DIR/DEPLOY.md"
sudo install -m 644 "$SOURCE_DIR/MIGRATION.md" "$INSTALL_DIR/MIGRATION.md"
sudo install -m 644 "$ENV_EXAMPLE_SOURCE" "$INSTALL_DIR/.env.example"

if [[ -f "$SOURCE_DIR/.env" && ! -f "$INSTALL_DIR/.env" ]]; then
  sudo install -m 600 "$SOURCE_DIR/.env" "$INSTALL_DIR/.env"
fi

sudo chown -R "$RUN_USER:$RUN_GROUP" "$INSTALL_DIR"
sudo chmod +x "$INSTALL_DIR"/bin/* || true
sudo chmod +x "$INSTALL_DIR"/scripts/*.sh || true

sudo "$INSTALL_DIR/scripts/install-linux-service.sh" \
  --install-dir "$INSTALL_DIR" \
  --user "$RUN_USER" \
  --group "$RUN_GROUP" \
  --service-name "$SERVICE_NAME"

sudo systemctl daemon-reload
sudo systemctl enable "$SERVICE_NAME"
sudo systemctl restart "$SERVICE_NAME"

echo "Deployment completed."
echo "Service: $SERVICE_NAME"
echo "Install dir: $INSTALL_DIR"
