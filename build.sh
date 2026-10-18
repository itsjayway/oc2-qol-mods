#!/usr/bin/env bash
# Builds OC2Mods.dll against the installed game and BepInEx (Mono 2.0 / .NET 3.5 profile).
set -euo pipefail
cd "$(dirname "$0")"

GAME="${OC2_GAME_DIR:-/c/Program Files (x86)/Steam/steamapps/common/Overcooked! 2}"
MANAGED="$GAME/Overcooked2_Data/Managed"
BEPINEX="${BEPINEX_CORE:-.work/bepinex/BepInEx/core}"
CSC="/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe"

if [ ! -f "$BEPINEX/BepInEx.dll" ]; then
  mkdir -p .work/bepinex
  unzip -q -o .work/bepinex.zip -d .work/bepinex
fi

mkdir -p out
refs=()
for f in mscorlib System System.Core Assembly-CSharp Assembly-CSharp-firstpass \
         UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule UnityEngine.InputModule UnityEngine.TextRenderingModule; do
  [ -f "$MANAGED/$f.dll" ] && refs+=("/r:$(cygpath -w "$MANAGED/$f.dll")")
done
refs+=("/r:$(cygpath -w "$BEPINEX/BepInEx.dll")" "/r:$(cygpath -w "$BEPINEX/0Harmony.dll")")

MSYS2_ARG_CONV_EXCL='*' "$CSC" /nologo /noconfig /nostdlib+ /target:library /optimize+ \
  /out:out\\OC2Mods.dll "${refs[@]}" src\\*.cs
echo "Built out/OC2Mods.dll"
