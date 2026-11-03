#!/usr/bin/env bash
# Exports the iOS Xcode project from the running Editor (local Unity license) and publishes it as the
# `ios-latest` GitHub release asset that Codemagic's ios-testflight workflow builds and signs.
#   Tools/ios-export.sh            # needs the iOS Build Support module: unity install-modules -m ios
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG=~/.config/unity3d/Editor.log
unity command --project-path "$ROOT" editor.playmode stop >/dev/null 2>&1 || true
unity status --until-ready >/dev/null
# Synchronous: a delayCall would wait for an Editor tick that an unfocused Editor may never get.
unity command --project-path "$ROOT" script eval --timeout 3000 --code \
  'UnityEditor.PlayerSettings.iOS.appleDeveloperTeamID = "PH4AKDQ4Q7"; Ironhorse.EditorTools.BuildScript.BuildIos(); return 0;' >/dev/null
grep "\[Ironhorse\] iOS build" "$LOG" | tail -1
! tail -50 "$LOG" | grep -q "\[Ironhorse\] iOS build failed"
rm -f /tmp/ios-xcode.zip
(cd "$ROOT" && zip -qry /tmp/ios-xcode.zip ios -x "ios/*_DoNotShip/*")
ls -lh /tmp/ios-xcode.zip
gh release view ios-latest >/dev/null 2>&1 || gh release create ios-latest --prerelease --title "iOS Xcode export" --notes "Exported Xcode project for Codemagic (Tools/ios-export.sh)."
gh release upload ios-latest /tmp/ios-xcode.zip --clobber
echo "uploaded; start the ios-testflight workflow in Codemagic"
