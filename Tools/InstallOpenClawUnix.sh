#!/usr/bin/env bash
set -euo pipefail

INSTALLER_URL="${OPENCLAW_INSTALLER_URL:-https://openclaw.ai/install.sh}"

show_help() {
  cat <<'EOF'
OpenClaw one-click installer for macOS, Linux, and WSL2.

This wrapper uses the official OpenClaw installer and forwards common flags.

Usage:
  ./OpenClaw-OneClick-Install.sh [options]

Options:
  --no-onboard              Install only; skip OpenClaw onboarding.
  --onboard                 Force onboarding after install.
  --no-prompt               Disable installer prompts.
  --dry-run                 Print installer actions without applying changes.
  --npm                     Install from npm.
  --git                     Install from the OpenClaw GitHub checkout flow.
  --install-method <npm|git>
  --version <version|tag>   Install a specific OpenClaw version or tag.
  --help                    Show this help.

Examples:
  ./OpenClaw-OneClick-Install.sh
  ./OpenClaw-OneClick-Install.sh --no-onboard
  ./OpenClaw-OneClick-Install.sh --install-method git --version main
EOF
}

case "${1:-}" in
  -h|--help)
    show_help
    exit 0
    ;;
esac

if ! command -v curl >/dev/null 2>&1; then
  echo "curl was not found. Install curl, then rerun this script." >&2
  exit 1
fi

if ! command -v bash >/dev/null 2>&1; then
  echo "bash was not found. Install bash, then rerun this script." >&2
  exit 1
fi

case "$(uname -s)" in
  Darwin|Linux)
    ;;
  *)
    echo "This installer supports macOS, Linux, and WSL2. On Windows, run OpenClaw-OneClick-Install.cmd." >&2
    exit 1
    ;;
esac

echo "OpenClaw one-click installer"
echo "Downloading official installer from: $INSTALLER_URL"
echo

curl -fsSL --proto '=https' --tlsv1.2 "$INSTALLER_URL" | bash -s -- "$@"
