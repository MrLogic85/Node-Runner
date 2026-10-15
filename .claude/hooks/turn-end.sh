#!/usr/bin/env bash
# Stop (#1320): when the agent ends a turn, reports once each emulator started and each APK
# written to build/ since the session began. SessionEnd output reaches neither tool's agent or
# user, so the report comes at the end of the turn that left them.
. "${0%/*}/common.sh"

[ "$(jq -r '.stop_hook_active // false' <<<"$input")" = true ] && exit 0
state=$(state_dir) && [ -f "$state/started" ] || exit 0
# Keeps a long session's state from being pruned as stale by another session's start.
touch "$state"

left=$({
  emulator_pids | grep -vxF -f "$state/emulators" | sed 's/^/emulator pid /'
  find "$root/build" -maxdepth 1 -name '*.apk' -newer "$state/started" 2>/dev/null \
    | while IFS= read -r apk; do echo "${apk#"$root"/}"; done
} | grep -vxF -f "$state/warned")
[ -n "$left" ] || exit 0
printf '%s\n' "$left" >>"$state/warned"
list=''
while IFS= read -r item; do list="${list:+$list; }$item"; done <<<"$left"
reason="Started during this session: $list. Stop the emulators you started (adb devices lists them, adb -s <serial> emu kill stops one; docs/MANUAL_TESTING.md → \"Android checks\") and delete the APKs you no longer need, or tell the human why they stay (AGENTS.md → \"Working copy\")."
jq -nc --arg r "$reason" '{decision:"block",reason:$r}'
exit 0
