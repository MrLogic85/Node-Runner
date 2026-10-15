---
name: release-milestone
description: Release a Node Runner milestone X.Y.Z from main, from the release notes through the published GitHub release to bumping main to the next version. Use when the human asks to release or publish a version.
---

# Release a milestone

`docs/RELEASING.md` → "Releasing a milestone" owns the rules, the PRs'
titles and what `release.sh` does; this skill owns the steps. Read
`LOCAL_CONFIG.md` first for the `gh` login and the signing key.

1. Stop unless the human has asked for this release. Find the release
   issue `#N` and check it is the milestone's last open issue; if others
   are open, stop and report them:

   ```bash
   gh issue list --milestone X.Y.Z --state open
   ```

2. Write the notes with the `write-release-notes` skill and ship them as
   the notes PR with the `ship-pr` skill, which waits until the merge
   commit's checks pass on `main`; `release.sh` needs them.
3. Dry-run the release from an up-to-date `main`. It exports and checks
   the APK, but tags and publishes nothing:

   ```bash
   git switch main && git pull --ff-only
   .github/scripts/set-version.sh   # prints version X.Y.Z
   .github/scripts/release.sh --dry-run
   ```

4. Publish, then check the release page shows the notes and the APK. If
   the script stops, do what its message says ("Publishing safeguards").

   ```bash
   .github/scripts/release.sh
   gh release view vX.Y.Z
   ```

5. Find `<next>`, the lowest open milestone after X.Y.Z; if there is
   none, ask the human. On a branch, run
   `.github/scripts/set-version.sh <next>` and ship it as the bump PR
   with `ship-pr`.

   ```bash
   gh api 'repos/MrLogic85/Node-Runner/milestones?per_page=100' -q '.[].title' | sort -V
   ```

6. Delete the APK the release left in `build/`.
