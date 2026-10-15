#!/usr/bin/env bash
# SessionStart (#1320): tells the agent the branch and any uncommitted changes, and records the
# running emulators so turn-end.sh reports only those this session starts.
command -v jq >/dev/null 2>&1 || {
  echo 'jq is not installed, so the repository hooks are off (README.md → "Tools").'
  exit 0
}
. "${0%/*}/common.sh"

note=''
# A resumed or compacted session keeps its state, and its changes are its own.
case $(jq -r '.source // empty' <<<"$input") in resume | compact) fresh=0 ;; *) fresh=1 ;; esac
if state=$(state_dir); then
  if [ -f "$state/started" ]; then
    fresh=0
  else
    find "${state%/*}" -mindepth 1 -maxdepth 1 -type d -mmin +2880 -exec rm -rf {} + 2>/dev/null
    mkdir -p "$state" && emulator_pids >"$state/emulators" && : >"$state/warned" && touch "$state/started"
  fi
fi
[ -n "$state" ] && [ -f "$state/started" ] \
  || note="
Could not record the session's state, so the end-of-turn check for emulators and APKs is off."

if ! changes=$(git -C "$root" status --porcelain 2>/dev/null); then
  context="Could not read the working copy (git status failed in $root). Ask the human before starting work (AGENTS.md → \"Working copy\")."
else
  branch=$(git -C "$root" symbolic-ref --short -q HEAD) || branch='(detached HEAD)'
  if [ -z "$changes" ]; then
    summary="no uncommitted changes."
  else
    count=$(printf '%s\n' "$changes" | wc -l | tr -d ' ')
    summary="$count uncommitted changes:
$(printf '%s\n' "$changes" | head -n 10)"
    [ "$count" -gt 10 ] && summary="$summary
… and $((count - 10)) more."
  fi
  if [ $fresh = 1 ]; then
    context="Working copy at session start: branch $branch, $summary"
    [ -n "$changes" ] && context="$context
If these are not yours, or this is not the branch for your task, stop and ask the human before starting work (AGENTS.md → \"Working copy\")."
  else
    context="Working copy now: branch $branch, $summary"
  fi
fi
context="$context${note:-}"
jq -nc --arg c "$context" '{additionalContext:$c,hookSpecificOutput:{hookEventName:"SessionStart",additionalContext:$c}}'
exit 0
