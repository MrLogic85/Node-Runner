# Shared by the repository hooks (docs/REVIEW.md → "Hooks", #1320). Sourced, never run.
# A failing PreToolUse hook denies every matching tool call, so the hooks never exit non-zero
# and allow the call when a tool they need is missing.
export LC_ALL=C
command -v jq >/dev/null 2>&1 || exit 0
hooks_dir=$(cd "${BASH_SOURCE[0]%/*}" && pwd)
input=$(cat)
cwd=$(jq -r '.cwd // empty' <<<"$input")
[ -d "$cwd" ] || cwd=$PWD
root=${CLAUDE_PROJECT_DIR:-$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null)}
root=$(cd "${root:-$cwd}" 2>/dev/null && pwd) || root=$cwd

# Prints one JSON object with both Copilot's top-level fields and Claude Code's
# hookSpecificOutput, so each tool shows the agent the reason (#1314).
deny() {
  jq -nc --arg r "$1" '{permissionDecision:"deny",permissionDecisionReason:$r,
    hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
  exit 0
}

# This session's state folder inside the git dir, so it stays out of the working tree.
state_dir() {
  local git_dir id
  git_dir=$(git -C "$root" rev-parse --absolute-git-dir 2>/dev/null) || return 1
  id=$(jq -r '.session_id // .sessionId // empty' <<<"$input" | tr -cd 'A-Za-z0-9_-')
  [ -n "$id" ] || return 1
  echo "$git_dir/agent-hooks/$id"
}

# PIDs of this user's running Android emulators: processes whose program is qemu-system*.
emulator_pids() {
  ps -U "$(id -u)" -o pid=,args= | awk '{ pid = $1; sub(/^ *[0-9]+ +/, ""); if (/^([^ ]*\/)?qemu-system[^\/ ]*( |$)/) print pid }'
}
