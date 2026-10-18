#!/usr/bin/env bash
# Builds the plugin and packages dist/OC2-QoL-Mods-v<version>.zip for a GitHub release.
# Needs the game installed locally (for the build) and .work/bepinex.zip.
set -euo pipefail
cd "$(dirname "$0")"

VERSION=$(grep -oE 'BepInPlugin\([^)]*"([0-9]+\.[0-9]+\.[0-9]+)"\)' src/OC2Mods.cs | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
NAME="OC2-QoL-Mods-v$VERSION"
STAGE="dist/$NAME"

bash build.sh
[ -f .work/BepInEx-LICENSE.txt ] || curl -sSL -o .work/BepInEx-LICENSE.txt \
  https://raw.githubusercontent.com/BepInEx/BepInEx/v5.4.23.3/LICENSE

rm -rf "$STAGE" "dist/$NAME.zip"
mkdir -p "$STAGE/BepInEx_x86"
unzip -q .work/bepinex.zip -d "$STAGE/BepInEx_x86"
cp out/OC2Mods.dll dist-src/install.ps1 dist-src/uninstall.ps1 dist-src/common.ps1 \
   dist-src/install.cmd dist-src/uninstall.cmd dist-src/README.txt LICENSE "$STAGE/"
cp .work/BepInEx-LICENSE.txt "$STAGE/BepInEx-LICENSE.txt"

(cd dist && 7z a -tzip -bso0 "$NAME.zip" "$NAME" >/dev/null)
echo "Packaged dist/$NAME.zip"
