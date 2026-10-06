# Releasing

How a version is exported and published as a signed APK on GitHub
Releases. Changes still land through PRs (`docs/REVIEW.md`).

## Versions

`application/config/version` in `project/project.godot` is the only version
source, and `main` carries the version of the milestone in progress. Both
export presets leave `version/name` empty so the export reads it, and set
`version/code` to 1000000·major + 1000·minor + patch (0.13.0 → 13000,
#809). Change the version and both codes with
`.github/scripts/set-version.sh X.Y.Z`; `ArchitectureSpec` checks they
agree.

## Releasing a milestone

Every milestone has a "Release X.Y.0" issue (`type: chore`, `area: android`).
When it is the milestone's last open issue:

1. A PR titled `docs(#N): Release notes for X.Y.0` adds
   `docs/release-notes/X.Y.0.md` (`docs/release-notes/AGENTS.md`) with
   `Part of #N`, not a closing keyword, so the issue stays open until the
   release is out.
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

## Android export

`project/export_presets.cfg` has two presets. `Android` builds the release.
`Android Debug` builds a debug APK with its own package
(`dev.mrlogic85.noderunner.debug`) and app name (Node Runner Debug), so it
installs next to the release with its own saved data (#898); Android never
replaces an app with one signed by another key. Godot presets cannot
inherit, so `Android Debug` repeats every option of `Android`: change both.
`ArchitectureSpec` fails if they differ in anything but the preset name,
package and app name.

A debug APK for development, signed with the developer's debug key:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path project \
  --export-debug "Android Debug" ../build/node-runner-debug.apk
```

`release.sh` runs `--export-release`, passing the release keystore through
Godot's `GODOT_ANDROID_KEYSTORE_RELEASE_*` environment variables. A release
export is not a debug build, so `OS.IsDebugBuild()` is false and debug-only
UI such as the Component library link is hidden (#808).

Local prerequisites: Godot 4.7.2 Mono export templates, JDK 21, Android SDK
platform and build-tools, platform-tools, and a user-local debug keystore in
the Godot editor settings. The committed presets hold no secrets; keystore
paths and passwords stay in user-local Godot settings, ignored credential
files or the macOS Keychain.

The export does not use Gradle, so Godot's Android template sets min SDK 24
and target/compile SDK 36; do not override them in `export_presets.cfg`
without enabling Gradle export.

Android uses the Compatibility renderer because Mobile/Vulkan crashed in
Godot's `VkThread` on the SM-S938B (#104); #961 re-checks it.

### App icon

The presets' `launcher_icons/*` and `splash_screen/icon`, and
`application/config/icon`, point at the SVGs in `project/assets/icons/app/`.
Godot imports each at its declared size and the export scales it to every
density (#820). `docs/UI_DIRECTION.md` → "App icon" owns the design.

- `main.svg` (192 px): the full icon for Android 7 and
  `application/config/icon`.
- `foreground.svg` and `background.svg` (432 px): the adaptive layers.
- `monochrome.svg` (432 px): Android 13 themed icons; white only.
- `splash.svg`: the Android 12+ launch splash. The non-Gradle export cannot
  set the splash background, which stays light, so the badge carries its
  own dark disc.

`foreground.svg` owns the art; `main.svg` and `splash.svg` copy it, so
change them together. `AppIconTests` checks the colours, sizes, both presets
and both copies.
