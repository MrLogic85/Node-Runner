#!/usr/bin/env bash
# Build a signed release APK of the version in project/project.godot and publish it as a
# GitHub release. See docs/REVIEW.md → "Releases".
#
#   release.sh              tag, export, verify and publish vX.Y.Z
#   release.sh --dry-run    export and verify only; no tag, branch or release
#
# Run X.Y.0 from an up-to-date main and X.Y.Z (Z > 0) from an up-to-date release/vX.Y whose
# CI checks passed. A new minor also creates release/vX.Y at the tag. The gh login must be
# able to push to the repo (see LOCAL_CONFIG.md). Signing key (never committed):
#   NODE_RUNNER_KEYSTORE           default ~/Documents/Godot/node-runner-release.jks
#   NODE_RUNNER_KEYSTORE_ALIAS     default noderunner
#   NODE_RUNNER_KEYSTORE_PASSWORD  default: macOS Keychain item "node-runner-release-keystore"
# Also: GODOT (Godot Mono binary), ANDROID_HOME.
set -euo pipefail

usage() { sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }
dry_run=false
case ${1:-} in
  '') ;;
  --dry-run) dry_run=true ;;
  *) usage ;;
esac

root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root"
repo=MrLogic85/Node-Runner
required_checks=("Build" "Test & coverage" "Format check")
fail() { echo "release: $*" >&2; exit 1; }

godot=${GODOT:-/Applications/Godot_mono.app/Contents/MacOS/Godot}
android_home=${ANDROID_HOME:-$HOME/Library/Android/sdk}
build_tools=$(ls -d "$android_home"/build-tools/* 2>/dev/null | sort -V | tail -n 1 || true)
[[ -x $godot ]] || fail "Godot not found at $godot; set GODOT."
[[ -n $build_tools && -x $build_tools/apksigner && -x $build_tools/aapt2 ]] \
  || fail "No apksigner/aapt2 under $android_home/build-tools; set ANDROID_HOME."

version=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project/project.godot)
code=$(sed -n 's/^version\/code=\(.*\)$/\1/p' project/export_presets.cfg)
[[ $version =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]] || fail "No X.Y.Z config/version in project.godot."
major=${BASH_REMATCH[1]} minor=${BASH_REMATCH[2]} patch=${BASH_REMATCH[3]}
(( code == 10#$major * 1000000 + 10#$minor * 1000 + 10#$patch )) \
  || fail "version/code $code does not match $version; run .github/scripts/set-version.sh $version."
tag="v$version"
release_branch="release/v$major.$minor"
apk="$root/build/node-runner-$version.apk"

if ! $dry_run; then
  if (( patch == 0 )); then expected=main; else expected=$release_branch; fi
  [[ $(git branch --show-current) == "$expected" ]] || fail "Release $version from $expected."
  [[ -z $(git status --porcelain) ]] || fail "The working tree is not clean."
  git fetch --quiet origin "$expected" --tags
  [[ $(git rev-parse HEAD) == "$(git rev-parse "origin/$expected")" ]] || fail "$expected is not in sync with origin."
  ! git rev-parse -q --verify "refs/tags/$tag" >/dev/null || fail "Tag $tag already exists."
  if (( patch == 0 )); then
    [[ -z $(git ls-remote --heads origin "$release_branch") ]] || fail "$release_branch already exists."
  fi
  [[ $(gh api "repos/$repo" --jq .permissions.push 2>/dev/null) == true ]] \
    || fail "The active gh login cannot push to $repo; see LOCAL_CONFIG.md."
  passed=$(gh api "repos/$repo/commits/$(git rev-parse HEAD)/check-runs?per_page=100" \
    --jq '.check_runs[] | select(.conclusion == "success") | .name') || fail "Could not read CI checks."
  for check in "${required_checks[@]}"; do
    grep -qxF "$check" <<<"$passed" || fail "CI check \"$check\" has not passed on HEAD."
  done
fi

keystore=${NODE_RUNNER_KEYSTORE:-$HOME/Documents/Godot/node-runner-release.jks}
alias=${NODE_RUNNER_KEYSTORE_ALIAS:-noderunner}
[[ -f $keystore ]] || fail "No keystore at $keystore; set NODE_RUNNER_KEYSTORE."
password=${NODE_RUNNER_KEYSTORE_PASSWORD:-$(security find-generic-password -s node-runner-release-keystore -w 2>/dev/null || true)}
[[ -n $password ]] || fail "No keystore password: add Keychain item node-runner-release-keystore or set NODE_RUNNER_KEYSTORE_PASSWORD."
# Godot 4.7 runs keytool through a shell with the password inside "…" (OS_Unix::execute).
[[ $password != *[\$\`\\\"!]* ]] || fail "The keystore password must not contain \$ \` \\ \" or !; Godot passes it through a shell."

mkdir -p build
rm -f "$apk"
echo "Exporting $tag ($code) to ${apk#"$root"/}"
(cd project && GODOT_ANDROID_KEYSTORE_RELEASE_PATH=$keystore \
  GODOT_ANDROID_KEYSTORE_RELEASE_USER=$alias \
  GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD=$password \
  "$godot" --headless --export-release Android "$apk")
[[ -f $apk ]] || fail "Godot did not write $apk."

badging=$("$build_tools/aapt2" dump badging "$apk" 2>/dev/null) || fail "aapt2 could not read $apk."
package=$(head -n 1 <<<"$badging")
[[ $package == *"versionCode='$code'"* && $package == *"versionName='$version'"* ]] \
  || fail "APK reports $package; expected $code / $version."
[[ $badging != *application-debuggable* ]] || fail "APK is debuggable."
"$build_tools/apksigner" verify --print-certs "$apk" | grep -E 'Signer.*certificate (DN|SHA-256)'

if $dry_run; then
  echo "Dry run: verified $apk; nothing tagged or published."
  exit 0
fi

git tag -a "$tag" -m "Node Runner $version"
git push origin "$tag"
if (( patch == 0 )); then
  git push origin "$tag^{commit}:refs/heads/$release_branch"
fi
gh release create "$tag" "$apk" --repo "$repo" --verify-tag --title "Node Runner $version" --generate-notes \
  || fail "$tag is pushed but the release was not created. Retry: gh release create $tag ${apk#"$root"/} --repo $repo --verify-tag --title \"Node Runner $version\" --generate-notes"

if (( patch == 0 )); then
  echo "Next: open a PR on main that runs .github/scripts/set-version.sh $major.$((minor + 1)).0"
fi
