#!/usr/bin/env bash
set -euo pipefail

TITLE="${1:-UnityRTS alert}"
MESSAGE="${2:-No message provided}"

echo "$(date '+%Y-%m-%d %H:%M:%S') [ALERT-HOOK] ${TITLE} :: ${MESSAGE}"
