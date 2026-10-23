#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "==> Building UnityRTS C++ Server Linux binary via Docker builder..."
cd "$ROOT_DIR"

mkdir -p dist

IMAGE_TAG="unity-rts-cpp-builder:temp"
docker build --target builder -t "$IMAGE_TAG" -f Dockerfile .

CONTAINER_ID=$(docker create "$IMAGE_TAG")
echo "==> Extracting compiled binaries from container $CONTAINER_ID..."

TMP_DIR="$(mktemp -d)"
docker cp "$CONTAINER_ID:/src/build/linux/unity_rts_server_cpp" "$TMP_DIR/"
docker cp "$CONTAINER_ID:/src/build/linux/unity_rts_guardian_cpp" "$TMP_DIR/"
docker rm "$CONTAINER_ID" >/dev/null

echo "==> Packaging Linux tarball..."
PKG_NAME="unity-rts-server-cpp-0.1.0-Linux-x86_64"
PKG_DIR="$TMP_DIR/$PKG_NAME"
mkdir -p "$PKG_DIR/bin" "$PKG_DIR/scripts" "$PKG_DIR/logs" "$PKG_DIR/data" "$PKG_DIR/backups"

mv "$TMP_DIR/unity_rts_server_cpp" "$PKG_DIR/bin/"
mv "$TMP_DIR/unity_rts_guardian_cpp" "$PKG_DIR/bin/"
chmod +x "$PKG_DIR/bin/"*

cp "$ROOT_DIR/config/server.env.example" "$PKG_DIR/.env.example"
cp "$ROOT_DIR/config/server.env.example" "$PKG_DIR/.env"
cp "$ROOT_DIR/README.md" "$PKG_DIR/"
cp "$ROOT_DIR/DEPLOY.md" "$PKG_DIR/"
cp "$ROOT_DIR/MIGRATION.md" "$PKG_DIR/"
cp "$ROOT_DIR/scripts/"*.sh "$PKG_DIR/scripts/"
chmod +x "$PKG_DIR/scripts/"*.sh

tar -czf "$ROOT_DIR/dist/$PKG_NAME.tar.gz" -C "$TMP_DIR" "$PKG_NAME"
rm -rf "$TMP_DIR"

echo "==> Linux package successfully generated at dist/$PKG_NAME.tar.gz"
