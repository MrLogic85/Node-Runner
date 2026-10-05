#!/usr/bin/env bash
# Set the app version in its one source and the Android build number derived from it.
# See docs/REVIEW.md → "Releases".
#
#   set-version.sh            print the current version and build number
#   set-version.sh 0.14.0     set the version to 0.14.0 (build number 14000)
#
# The version name lives in project/project.godot (application/config/version); the
# Android preset leaves version/name empty so the export reads it from there. The build
# number is 1000000·major + 1000·minor + patch, so minor and patch must stay below 1000.
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
project="$root/project/project.godot"
presets="$root/project/export_presets.cfg"

usage() { sed -n '2,6p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

if [[ $# -eq 0 ]]; then
  sed -n 's/^config\/version="\(.*\)"$/version \1/p' "$project"
  sed -n 's/^version\/code=\(.*\)$/build   \1/p' "$presets"
  exit 0
fi
[[ $# -eq 1 && $1 =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})$ ]] || usage

version=$1
code=$((BASH_REMATCH[1] * 1000000 + BASH_REMATCH[2] * 1000 + BASH_REMATCH[3]))
(( code <= 2100000000 )) || { echo "Build number $code is above Android's limit." >&2; exit 1; }

grep -q '^config/version=' "$project" || { echo "No config/version in $project" >&2; exit 1; }
grep -q '^version/code=' "$presets" || { echo "No version/code in $presets" >&2; exit 1; }

# Write through a temp file: BSD and GNU sed disagree on `sed -i`.
replace() {
  local file=$1 expression=$2 temp
  temp=$(mktemp)
  sed "$expression" "$file" >"$temp" && cat "$temp" >"$file"
  rm -f "$temp"
}
replace "$project" "s/^config\/version=.*/config\/version=\"$version\"/"
replace "$presets" "s/^version\/code=.*/version\/code=$code/"

echo "version $version"
echo "build   $code"
