#!/usr/bin/env bash
set -euo pipefail

PRESET="${1:-linux-gcc-release}"
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$PROJECT_DIR"

if ! command -v cmake >/dev/null 2>&1; then
  echo "cmake is required to package the native server."
  exit 1
fi

cmake --preset "$PRESET"
cmake --build --preset "$PRESET"

CPACK_CONFIG="$PROJECT_DIR/build/$PRESET/CPackConfig.cmake"
if [ ! -f "$CPACK_CONFIG" ]; then
  echo "CPackConfig.cmake was not generated at $CPACK_CONFIG"
  exit 1
fi

cpack --config "$CPACK_CONFIG"
echo "Linux package completed. Check dist/ under $PROJECT_DIR."
