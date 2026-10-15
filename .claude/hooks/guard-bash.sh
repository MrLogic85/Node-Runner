#!/usr/bin/env bash
# PreToolUse on Bash (#1320): denies the git commands AGENTS.md → "Working copy" forbids, a
# commit on main or a push that updates it, and a PR title CI's `PR title` check would fail.
# Each check_* function returns to allow, and denies through deny(), which ends the script.
. "${0%/*}/common.sh"

main_branch=main
# Must match .github/workflows/ci.yml → semantic-pr-title; HookSettingsSpec fails when they differ.
title_types='feat fix docs chore refactor test build ci perf style revert'
title_pattern="^(${title_types// /|})\\((#[1-9][0-9]*(, #[1-9][0-9]*)*)\\): [A-Z].+\$"

working_copy='AGENTS.md → "Working copy"'

# One line per switch or checkout earlier in the command line: the checkout's top level, a
# tab, and the branch it moved to; '?' when the hook cannot tell, which allows the call.
switches=''

top_of() { git -C "$1" rev-parse --show-toplevel 2>/dev/null; }

branch_in() {
  local top line branch=''
  top=$(top_of "$1")
  while IFS= read -r line; do
    [ "${line%%$'\t'*}" = "$top" ] && branch=${line#*$'\t'}
  done <<<"$switches"
  if [ -n "$branch" ]; then
    echo "$branch"
  else
    git -C "$1" symbolic-ref --short -q HEAD 2>/dev/null
  fi
}

# Records where `git switch` or `git checkout` with these arguments leaves HEAD. A checkout
# with paths, or of a single path, restores files and leaves HEAD where it was; a trailing
# `--` with nothing after it only marks the branch.
note_switch() {
  local dir=$1 sub=$2 first='' count=0 target='' track=0
  shift 2
  while [ $# -gt 0 ] && [ -z "$target" ]; do
    case $1 in
      -c | -C | --create | --force-create | -b | -B | --orphan) target=$2 ;;
      --create=* | --force-create=* | --orphan=*) target=${1#*=} ;;
      -[fqmt][cCbB] | -[fqmt][fqmt][cCbB]) target=$2 ;;
      -[cCbB]?*) target=${1#-?} ;;
      --detach | -d | -) target='?' ;;
      -t | --track | --track=*) track=1 ;;
      -p | --patch) return 0 ;;
      --) [ "$sub" = checkout ] && [ $# -gt 1 ] && return 0 ;;
      -*) ;;
      *) count=$((count + 1)); [ -n "$first" ] || first=$1 ;;
    esac
    shift
  done
  if [ -z "$target" ] && [ -n "$first" ]; then
    if [ $track = 1 ]; then
      # git creates a local branch named after the remote one.
      target=${first#*/}
    elif [ "$sub" = switch ]; then
      target=$first
      case $first in *@\{*) target='?' ;; esac
    elif [ $count = 1 ]; then
      if git -C "$dir" show-ref -q --verify "refs/heads/$first"; then
        target=$first
      elif git -C "$dir" rev-parse -q --verify "$first^{commit}" >/dev/null; then
        target='?'
      elif [ -n "$(git -C "$dir" for-each-ref --count=1 --format=x "refs/remotes/*/$first")" ]; then
        # git creates a local branch tracking the remote one.
        target=$first
      fi
    fi
  fi
  [ -n "$target" ] && switches="$switches
$(top_of "$dir")	$target"
  return 0
}

path_in() {
  case $2 in
    /*) echo "$2" ;;
    *) echo "$1/$2" ;;
  esac
}

check_git() {
  local dir=$1 i=0 sub a
  shift
  local -a args=("$@")
  while [ $i -lt ${#args[@]} ]; do
    a=${args[$i]}
    case $a in
      -C) dir=$(path_in "$dir" "${args[$((i + 1))]}"); i=$((i + 2)) ;;
      -c | --git-dir | --work-tree | --namespace | --exec-path | --config-env | --super-prefix) i=$((i + 2)) ;;
      -*) i=$((i + 1)) ;;
      *) break ;;
    esac
  done
  sub=${args[$i]}
  args=("${args[@]:$((i + 1))}")
  case $sub in
    stash)
      case ${args[0]} in
        list | show) ;;
        *) deny "git stash is not allowed ($working_copy): never stash or discard uncommitted changes; if they block your work, stop and ask the human." ;;
      esac
      ;;
    worktree)
      [ "${args[0]}" = list ] || deny "git worktree is not allowed ($working_copy): work only in this checkout, on a feature branch."
      ;;
    clone)
      deny "git clone is not allowed ($working_copy): work only in this checkout; do not copy the repository anywhere."
      ;;
    reset)
      for a in "${args[@]}"; do
        [ "$a" = --hard ] && deny "git reset --hard is not allowed ($working_copy): it discards uncommitted changes; stop and ask the human."
      done
      ;;
    commit)
      for a in "${args[@]}"; do
        [ "$a" = --dry-run ] && return
      done
      [ "$(branch_in "$dir")" = "$main_branch" ] \
        && deny "Do not commit on $main_branch ($working_copy): create a feature branch with git switch -c; $main_branch changes only through a merged PR (docs/REVIEW.md → \"Merge gates\")."
      ;;
    push) check_push "$dir" "${args[@]}" ;;
    switch | checkout) note_switch "$dir" "$sub" "${args[@]}" ;;
  esac
}

check_push() {
  local dir=$1 i=0 a all=0 tags=0 dst branch
  shift
  local -a args=("$@") positional=()
  while [ $i -lt ${#args[@]} ]; do
    a=${args[$i]}
    case $a in
      --repo | -o | --push-option | --receive-pack | --exec) i=$((i + 1)) ;;
      --all | --mirror | --branches) all=1 ;;
      --tags) tags=1 ;;
      --) positional+=("${args[@]:$((i + 1))}"); break ;;
      -*) ;;
      *) positional+=("$a") ;;
    esac
    i=$((i + 1))
  done
  local reason="Do not push to $main_branch ($working_copy): push your feature branch and open a PR; tags and release/vX.Y branches are fine (docs/RELEASING.md)."
  [ $all = 1 ] && deny "$reason"
  branch=$(branch_in "$dir")
  if [ ${#positional[@]} -lt 2 ]; then
    [ $tags = 0 ] && [ "$branch" = "$main_branch" ] && deny "$reason"
    return
  fi
  for a in "${positional[@]:1}"; do
    a=${a#+}
    dst=${a#*:}
    case $dst in HEAD | @) dst=$branch ;; esac
    dst=${dst#refs/heads/}
    [ "$dst" = "$main_branch" ] && deny "$reason"
  done
}

check_gh() {
  local i=0 a action=$2 title='' has_title=0
  [ "$1" = pr ] || return
  case $action in create | edit) ;; *) return ;; esac
  shift 2
  local -a args=("$@")
  while [ $i -lt ${#args[@]} ]; do
    a=${args[$i]}
    case $a in
      -t | --title) title=${args[$((i + 1))]}; has_title=1; i=$((i + 1)) ;;
      --title=*) title=${a#--title=}; has_title=1 ;;
      -t?*) title=${a#-t}; has_title=1 ;;
      -b | --body | -F | --body-file | -B | --base | -H | --head | -a | --assignee | -l | --label \
        | -m | --milestone | -p | --project | -r | --reviewer | -R | --repo | -T | --template | --recover \
        | --add-label | --remove-label | --add-reviewer | --remove-reviewer | --add-assignee \
        | --remove-assignee | --add-project | --remove-project) i=$((i + 1)) ;;
    esac
    i=$((i + 1))
  done
  if [ $has_title = 0 ]; then
    [ "$action" = create ] && deny "Pass the PR title with --title so it can be checked: type(#123): Description (docs/REVIEW.md → \"PR title and commit hygiene\")."
    return
  fi
  # A title built at run time is checked by CI only.
  case $title in *'$'* | *'`'*) return ;; esac
  [[ $title =~ $title_pattern ]] \
    || deny "PR title \"$title\" fails CI's PR title check: use type(#123): Description, or type(#123, #124): Description, with a type from: $title_types (docs/REVIEW.md → \"PR title and commit hygiene\")."
}

check_line() {
  local dir=$2 depth=$3 name here
  local -a w
  while IFS=$'\037' read -r -a w; do
    # env -C changes the directory for its one command only.
    here=$dir
    while [ ${#w[@]} -gt 0 ]; do
      case ${w[0]} in
        [A-Za-z_]*=*) [[ ${w[0]} =~ ^[A-Za-z_][A-Za-z0-9_]*= ]] || break ;;
        command | builtin | exec | nohup | time | sudo | if | then | else | elif | do | while | until | '!' | '{') ;;
        env)
          while [ ${#w[@]} -gt 1 ]; do
            case ${w[1]} in
              -u | --unset) w=("${w[@]:1}") ;;
              -C | --chdir) here=$(path_in "$dir" "${w[2]}"); w=("${w[@]:1}") ;;
              --chdir=*) here=$(path_in "$dir" "${w[1]#--chdir=}") ;;
              -C?*) here=$(path_in "$dir" "${w[1]#-C}") ;;
              -* | [A-Za-z_]*=*) ;;
              *) break ;;
            esac
            w=("${w[@]:1}")
          done
          ;;
        *) break ;;
      esac
      w=("${w[@]:1}")
    done
    [ ${#w[@]} -gt 0 ] || continue
    name=${w[0]##*/}
    case $name in
      git) check_git "$here" "${w[@]:1}" ;;
      gh)
        w=("${w[@]:1}")
        while [ ${#w[@]} -gt 0 ]; do
          case ${w[0]} in
            -R | --repo) w=("${w[@]:2}") ;;
            --repo=*) w=("${w[@]:1}") ;;
            *) break ;;
          esac
        done
        check_gh "${w[@]}"
        ;;
      cd) [ ${#w[@]} -gt 1 ] && dir=$(path_in "$here" "${w[1]}") ;;
      bash | sh | zsh)
        if [ "$depth" -lt 3 ]; then
          local j=1
          while [ $j -lt ${#w[@]} ] && [[ ${w[$j]} == -* ]]; do
            if [[ ${w[$j]} == -*c* && ${w[$j]} != --* ]]; then
              check_line "${w[$((j + 1))]}" "$here" $((depth + 1))
              break
            fi
            j=$((j + 1))
          done
        fi
        ;;
    esac
  done < <(printf '%s' "$1" | awk -f "$hooks_dir/words.awk")
}

check_line "$(jq -r '.tool_input.command // empty' <<<"$input")" "$cwd" 0
exit 0
