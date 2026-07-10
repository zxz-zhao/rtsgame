#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="/opt/unity-rts-server"
SERVICE_NAME="unity-rts-guardian"
RUN_USER="unityrts"
RUN_GROUP="unityrts"

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

UNIT_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

cat <<EOF | sudo tee "$UNIT_FILE" >/dev/null
[Unit]
Description=UnityRTS Guardian Service
After=network.target

[Service]
Type=simple
User=${RUN_USER}
Group=${RUN_GROUP}
WorkingDirectory=${INSTALL_DIR}
ExecStart=${INSTALL_DIR}/bin/unity_rts_guardian_cpp --env ${INSTALL_DIR}/.env --server ${INSTALL_DIR}/bin/unity_rts_server_cpp
Restart=always
RestartSec=3
KillSignal=SIGTERM
TimeoutStopSec=20
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
EOF

echo "Installed systemd unit: $UNIT_FILE"
