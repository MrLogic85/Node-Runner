# Releasing

How a version is published as a signed APK on GitHub Releases. Changes still
land through PRs as described in `docs/REVIEW.md`; release-notes rules live
in `docs/release-notes/AGENTS.md`.

## Versions

`main` always carries the version of the milestone in progress
(`application/config/version` in `project/project.godot`; see
`docs/ARCHITECTURE.md` → "Android export" for the build number).

## Releasing a milestone

Every milestone has a "Release X.Y.0" issue (`type: chore`, `area: android`).
When it is the milestone's last open issue:

1. A PR titled `docs(#N): Release notes for X.Y.0` adds
   `docs/release-notes/X.Y.0.md` with `Part of #N`, not a closing keyword, so
   the issue stays open until the release is out. An AI agent writes the
   notes for players, following `docs/release-notes/AGENTS.md`; the owner
   approves the text in that PR.
2. From an up-to-date `main`, run `.github/scripts/release.sh`. It exports a
   signed release APK, checks its version and signature, tags `vX.Y.0`, pushes
   `release/vX.Y` at the tag, and creates the GitHub release with the APK
   attached. The release text is exactly the notes file.
   `release.sh --dry-run` exports and checks without publishing.
3. A PR titled `chore(#N): Bump main to X.(Y+1).0` runs
   `.github/scripts/set-version.sh X.(Y+1).0` and closes the release issue.

## Patch releases

A fix for a released version lands on `release/vX.Y` by PR (also on `main`
when it applies there). That PR bumps the patch with `set-version.sh X.Y.Z`
and adds `docs/release-notes/X.Y.Z.md`; after it merges, `release.sh` from
that branch publishes it. GitHub only honours closing keywords on PRs into
`main`, so close the fix's issue by hand after the patch release unless its
`main` PR closes it. CI runs on `release/**` as on `main`, but no
ruleset protects those branches, so `release.sh` refuses to publish unless
`Build`, `Test & coverage` and `Format check` passed on HEAD.

## Publishing safeguards

`release.sh` checks before tagging that the active `gh` login can push to the
repository (`LOCAL_CONFIG.md` names the account). If `gh release create` still
fails after the tag is pushed, the script prints the command to retry it.

## Signing key

The release keystore is a 4096-bit RSA key kept outside the repo. Every release
must be signed with the same key, or installed copies cannot update; back it up
together with its password. `release.sh` reads its location, alias and password
from `NODE_RUNNER_KEYSTORE*` environment variables or the macOS Keychain (see
the script header). Machine-specific values belong in `LOCAL_CONFIG.md`.
