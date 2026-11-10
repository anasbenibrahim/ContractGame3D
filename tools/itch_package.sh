#!/usr/bin/env bash
# Packages the Windows, Linux and Android builds for itch.io, and optionally uploads them with butler.
#
#   tools/itch_package.sh                      # make clean folders + zips in Builds/itch/
#   tools/itch_package.sh push <user>/<game>   # same, then upload with butler (run "butler login" once first)
#
# Expects the builds made by the editor (CONTRACT > Build) or headless:
#   -executeMethod Contract.EditorTools.BuildPipeline.BuildWindowsCI
#   -executeMethod Contract.EditorTools.BuildPipeline.BuildLinuxCI
#   -executeMethod Contract.EditorTools.BuildPipeline.BuildAndroidCI
# IL2CPP / Burst backup folders ("..._DoNotShip", "..._ButDontShipItWithYourGame") are never shipped.
set -euo pipefail
cd "$(dirname "$0")/.."

OUT=Builds/itch
VERSION=$(grep -m1 "bundleVersion:" ProjectSettings/ProjectSettings.asset | awk '{print $2}')
rm -rf "$OUT" && mkdir -p "$OUT"

stage() { # stage <source dir> <channel>
  local src=$1 channel=$2
  if [ ! -d "$src" ]; then echo "skip $channel: $src not built"; return; fi
  mkdir -p "$OUT/$channel"
  rsync -a --exclude '*_BackUpThisFolder_ButDontShipItWithYourGame' --exclude '*_BurstDebugInformation_DoNotShip' "$src/" "$OUT/$channel/"
  (cd "$OUT/$channel" && zip -qr "../ContractSeoulShadows-$VERSION-$channel.zip" .)
  echo "$channel: $(du -sh "$OUT/$channel" | cut -f1) -> $OUT/ContractSeoulShadows-$VERSION-$channel.zip"
}

stage Builds/PC/Contract_SeoulShadows_Windows windows
stage Builds/PC/Contract_SeoulShadows_Linux linux
chmod +x "$OUT/linux/Contract_SeoulShadows.x86_64" 2>/dev/null || true
if [ -f Builds/Android/ContractSeoulShadows.apk ]; then
  cp Builds/Android/ContractSeoulShadows.apk "$OUT/ContractSeoulShadows-$VERSION-android.apk"
  echo "android: $OUT/ContractSeoulShadows-$VERSION-android.apk"
fi

if [ "${1:-}" = "push" ]; then
  TARGET=${2:?usage: tools/itch_package.sh push <itch-user>/<game>}
  BUTLER=$(command -v butler || echo "$HOME/.local/bin/butler")
  [ -d "$OUT/windows" ] && "$BUTLER" push "$OUT/windows" "$TARGET:windows" --userversion "$VERSION"
  [ -d "$OUT/linux" ] && "$BUTLER" push "$OUT/linux" "$TARGET:linux" --userversion "$VERSION"
  [ -f "$OUT/ContractSeoulShadows-$VERSION-android.apk" ] && "$BUTLER" push "$OUT/ContractSeoulShadows-$VERSION-android.apk" "$TARGET:android" --userversion "$VERSION"
  "$BUTLER" status "$TARGET"
fi
