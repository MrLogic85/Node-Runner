#!/usr/bin/env bash
# PreToolUse on the edit tools (#1320): denies writes into the read-only issues/ archive and
# into /tmp, since scratch files go in .scratch/ (AGENTS.md → "Working copy").
. "${0%/*}/common.sh"

# Claude Code sends file_path, Copilot path, and Copilot's apply_patch the whole patch as a string.
targets() {
  jq -r '.tool_input
    | if type == "string" then
        split("\n")[] | select(test("^\\*\\*\\* ((Add|Update|Delete) File|Move to): ")) | sub("^\\*\\*\\* [^:]+: "; "")
      else
        (.file_path, .path, .notebook_path) | strings
      end' <<<"$input"
}

# Resolves . and .. without touching the file system, since the file may not exist yet.
normalised() {
  local path=$1 part out=''
  set -f
  case $path in /*) ;; *) path="$cwd/$path" ;; esac
  local IFS=/
  for part in $path; do
    case $part in
      '' | .) ;;
      ..) out=${out%/*} ;;
      *) out="$out/$part" ;;
    esac
  done
  set +f
  echo "${out:-/}"
}

while IFS= read -r target; do
  [ -n "$target" ] || continue
  path=$(normalised "$target")
  case $path in
    /tmp | /tmp/* | /private/tmp | /private/tmp/*)
      deny "Do not write scratch files to /tmp: use .scratch/ in this checkout and delete them when done (AGENTS.md → \"Working copy\")."
      ;;
    "$root"/issues/*)
      deny "issues/ is a read-only archive of the old tracker: work is tracked on GitHub (AGENTS.md → \"Repository map\")."
      ;;
  esac
done < <(targets)
exit 0
