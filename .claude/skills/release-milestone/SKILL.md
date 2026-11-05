---
name: release-milestone
description: Release a Node Runner milestone X.Y.Z from main, from the release notes through the published GitHub release to bumping main to the next version. Use when the human asks to release or publish a version.
---

# Release a milestone

Rules and the PRs' titles: `docs/RELEASING.md` → "Releasing a
milestone". Read `LOCAL_CONFIG.md` first for the `gh` login and the
signing key.

1. Check before you start:
   - The human hasn't asked for this release: answer what they asked,
     release nothing and stop.
   - The release issue `#N` must be the milestone's last open issue.
     Others are open: reply `X.Y.Z has open issues: <numbers>` and stop.

   ```bash
   gh issue list --milestone X.Y.Z --state open
   ```

2. Write the notes with the `write-release-notes` skill and ship them as
   the notes PR with the `ship-pr` skill. Unlike other PRs, wait for this
   one: `release.sh` needs its merge commit's checks to pass on `main`.
   `P` is the number `gh pr create` printed. A check fails: fix it per
   `ship-pr` step 6 and wait again. No checks yet: wait and run it again.

   ```bash
   gh pr checks P --watch --fail-fast || exit 1
   until s=$(gh pr view P --json state -q .state) && [ "$s" != OPEN ]; do sleep 15; done
   [ "$s" = MERGED ] || exit 1   # closed unmerged: stop and ask the human
   sha=$(gh pr view P --json mergeCommit -q .mergeCommit.oid)
   until id=$(gh run list --commit "$sha" --limit 1 --json databaseId -q '.[0].databaseId') && [ -n "$id" ]; do sleep 10; done
   gh run watch "$id" --exit-status
   ```

3. Dry-run the release from an up-to-date `main`. It exports and checks
   the APK, but tags and publishes nothing:

   ```bash
   git switch main && git pull --ff-only
   .github/scripts/set-version.sh   # prints version X.Y.Z
   .github/scripts/release.sh --dry-run
   ```

4. Publish, then check the release page shows the notes and the APK. The
   script stops: do what its message says ("Publishing safeguards").

   ```bash
   .github/scripts/release.sh
   gh release view vX.Y.Z
   ```

5. `<next>` is the lowest open milestone after X.Y.Z; none: ask the
   human. On a branch, run `.github/scripts/set-version.sh <next>` and
   ship it as the bump PR with `ship-pr`.

   ```bash
   gh api 'repos/MrLogic85/Node-Runner/milestones?per_page=100' -q '.[].title' | sort -V
   ```

6. Delete the APK the release left in `build/`.
