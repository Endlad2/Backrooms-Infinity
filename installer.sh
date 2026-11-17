#!/usr/bin/env sh
set -eu

REPO="Endlad2/Backrooms-Infinity"
TAG="${1:-latest}"
ROOT="$HOME/.backrooms_infinity"
GAME="$ROOT/game"
ASSETS="$ROOT/assets"
TEMP="$ROOT/temp"
mkdir -p "$ROOT/levels" "$ROOT/models" "$ROOT/settings" "$GAME" "$ASSETS" "$ROOT/logs" "$ROOT/cache" "$TEMP"

OS="$(uname -s)"
ARCH="$(uname -m)"
case "$OS:$ARCH" in
  Linux:x86_64|Linux:amd64) RID="linux-x64" ;;
  Darwin:x86_64|Darwin:amd64) RID="osx-x64" ;;
  Darwin:arm64|Darwin:aarch64) RID="osx-arm64" ;;
  *) echo "Unsupported platform: $OS $ARCH" >&2; exit 2 ;;
esac
ASSET="Backrooms-$RID.zip"
if [ "$TAG" = "latest" ]; then
  URL="https://github.com/$REPO/releases/latest/download/$ASSET"
else
  URL="https://github.com/$REPO/releases/download/$TAG/$ASSET"
fi
ARCHIVE="$TEMP/$ASSET"
echo "Downloading $URL"
if command -v curl >/dev/null 2>&1; then
  curl -fL "$URL" -o "$ARCHIVE"
elif command -v wget >/dev/null 2>&1; then
  wget -O "$ARCHIVE" "$URL"
else
  echo "curl or wget is required" >&2; exit 3
fi

find "$GAME" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
if command -v unzip >/dev/null 2>&1; then
  unzip -q "$ARCHIVE" -d "$GAME"
elif command -v python3 >/dev/null 2>&1; then
  python3 -m zipfile -e "$ARCHIVE" "$GAME"
else
  echo "unzip or python3 is required" >&2; exit 4
fi

if [ -d "$GAME/assets" ]; then
  cp -R "$GAME/assets/." "$ASSETS/"
fi
chmod +x "$GAME/Backrooms.Launcher" "$GAME/Backrooms.Client" 2>/dev/null || true

if [ "$OS" = "Linux" ]; then
  if command -v xdg-user-dir >/dev/null 2>&1; then DESKTOP="$(xdg-user-dir DESKTOP)"; else DESKTOP="$HOME/Desktop"; fi
  mkdir -p "$DESKTOP"
  SHORTCUT="$DESKTOP/Backrooms Infinity.desktop"
  cat > "$SHORTCUT" <<EOF
[Desktop Entry]
Type=Application
Name=Backrooms Infinity
Exec=$GAME/Backrooms.Launcher
Path=$GAME
Terminal=false
Categories=Game;
EOF
  chmod +x "$SHORTCUT"
  command -v gio >/dev/null 2>&1 && gio set "$SHORTCUT" metadata::trusted true >/dev/null 2>&1 || true
else
  DESKTOP="$HOME/Desktop"
  mkdir -p "$DESKTOP"
  SHORTCUT="$DESKTOP/Backrooms Infinity.command"
  cat > "$SHORTCUT" <<EOF
#!/bin/sh
cd "$GAME"
exec "$GAME/Backrooms.Launcher"
EOF
  chmod +x "$SHORTCUT"
fi

rm -f "$ARCHIVE"
echo "Installed to $ROOT"
