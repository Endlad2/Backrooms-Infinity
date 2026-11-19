#!/usr/bin/env sh
set -eu

TAG="${1:-latest}"
REPO="${BACKROOMS_REPOSITORY:-Endlad2/Backrooms-Infinity}"
ROOT="$HOME/.backrooms_infinity"
GAME="$ROOT/game"
TMP="${TMPDIR:-/tmp}/backrooms-infinity-$$"
mkdir -p "$ROOT" "$GAME" "$ROOT/assets" "$TMP"
trap 'rm -rf "$TMP"' EXIT INT TERM

if [ "$TAG" = "latest" ]; then
  TAG="$(python3 - "$REPO" <<'PY'
import json, sys, urllib.request
repo=sys.argv[1]
r=urllib.request.Request(f'https://api.github.com/repos/{repo}/releases/latest',headers={'User-Agent':'Backrooms-Infinity-Installer'})
with urllib.request.urlopen(r, timeout=30) as f: print(json.load(f)['tag_name'])
PY
)"
fi

case "$(uname -s)" in
  Linux) ASSET="Backrooms-linux-x64.zip" ;;
  Darwin) ASSET="Backrooms-macos-universal.zip" ;;
  *) echo "Unsupported OS: $(uname -s)" >&2; exit 2 ;;
esac

BASE="https://github.com/$REPO/releases/download/$TAG"
python3 - "$BASE/$ASSET" "$TMP/game.zip" "$BASE/Backrooms-user-assets.zip" "$TMP/assets.zip" <<'PY'
import sys, urllib.request
for url,out in ((sys.argv[1],sys.argv[2]),(sys.argv[3],sys.argv[4])):
    req=urllib.request.Request(url,headers={'User-Agent':'Backrooms-Infinity-Installer'})
    with urllib.request.urlopen(req, timeout=120) as src, open(out,'wb') as dst:
        while True:
            b=src.read(1024*1024)
            if not b: break
            dst.write(b)
PY

rm -rf "$GAME"
mkdir -p "$GAME"
python3 - "$TMP/game.zip" "$GAME" "$TMP/assets.zip" "$ROOT" <<'PY'
import sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as z: z.extractall(sys.argv[2])
with zipfile.ZipFile(sys.argv[3]) as z: z.extractall(sys.argv[4])
PY

if [ "$(uname -s)" = "Linux" ]; then
  EXE="$GAME/Backrooms-Infinity.x86_64"
  chmod +x "$EXE"
  DESKTOP_DIR="${XDG_DESKTOP_DIR:-$HOME/Desktop}"
  mkdir -p "$DESKTOP_DIR"
  cat > "$DESKTOP_DIR/Backrooms-Infinity.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Backrooms Infinity
Exec=$EXE
Path=$GAME
Terminal=false
Categories=Game;
DESKTOP
  chmod +x "$DESKTOP_DIR/Backrooms-Infinity.desktop"
else
  APP="$(find "$GAME" -maxdepth 2 -type d -name '*.app' | head -n 1 || true)"
  [ -n "$APP" ] || { echo "macOS .app not found in release" >&2; exit 3; }
  DESKTOP_DIR="$HOME/Desktop"
  mkdir -p "$DESKTOP_DIR"
  COMMAND="$DESKTOP_DIR/Backrooms Infinity.command"
  printf '#!/bin/sh\nopen %s\n' "$(printf '%s' "$APP" | sed 's/ /\\ /g')" > "$COMMAND"
  chmod +x "$COMMAND"
fi

echo "Installed Backrooms Infinity $TAG to $ROOT"
