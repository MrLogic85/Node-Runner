# Releasing

How a version is exported and published as a signed APK on GitHub
Releases. Changes still land through PRs (`docs/REVIEW.md`).

## Versions

`application/config/version` in `project/project.godot` is the only version
source, and `main` carries the version of the milestone in progress. The
`Android` export preset leaves `version/name` empty so the export reads it,
and sets `version/code` to 1000000·major + 1000·minor + patch (0.13.0 →
13000, #809). Change both with `.github/scripts/set-version.sh X.Y.Z`;
`ArchitectureSpec` checks they agree.

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

The one `Android` preset in `project/export_presets.cfg` exports through
Godot's Gradle build (#914). A debug export gets its own package
(`dev.mrlogic85.noderunner.debug`) and app name (Node Runner Debug), so it
installs next to the release with its own saved data (#898); Android never
replaces an app with one signed by another key. Two tracked files do this
on top of Godot's build template: `applicationIdSuffix ".debug"` in the
`debug` build type of `project/android/build/build.gradle`, and the label in
`project/android/build/src/monoDebug/AndroidManifest.xml` (Godot rewrites
`src/debug` on every export). `ArchitectureSpec` checks both and the preset.

The rest of `project/android/` is Godot's template, about 270 MB of
libraries before a build, so it is git-ignored. Install it once per clone,
and again after a Godot upgrade. Godot installs it only as part of an
export, which then has to be redone, since the install also overwrites
`build.gradle`:

```bash
cd project
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
  --install-android-build-template --export-debug Android /tmp/template.apk
git checkout -- android/build/build.gradle
```

After an upgrade, keep Godot's new `build.gradle` instead and add the
`applicationIdSuffix` line back.

A debug APK for development, signed with the developer's debug key:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path project \
  --export-debug Android ../build/node-runner-debug.apk
```

`release.sh` runs `--export-release`, passing the release keystore through
Godot's `GODOT_ANDROID_KEYSTORE_RELEASE_*` environment variables. A release
export is not a debug build, so `OS.IsDebugBuild()` is false and debug-only
UI such as the Component library link is hidden (#808).

Local prerequisites: Godot 4.7.2 Mono export templates, JDK 21, Android SDK
platform 36 and build-tools 36.1, platform-tools, and a user-local debug
keystore in the Godot editor settings. The first Gradle build downloads
Gradle and its dependencies, so it needs a network. The committed preset
holds no secrets; keystore paths and passwords stay in user-local Godot
settings, ignored credential files or the macOS Keychain.

Godot's template sets min SDK 24 and target/compile SDK 36
(`android/build/config.gradle`); the preset leaves both empty to keep them.

Android uses the Compatibility renderer because Mobile/Vulkan crashed in
Godot's `VkThread` on the SM-S938B (#104); #961 re-checks it.

### App icon

The preset's `launcher_icons/*` and `splash_screen/icon`, and
`application/config/icon`, point at the SVGs in `project/assets/icons/app/`.
Godot imports each at its declared size and the export scales it to every
density (#820). `docs/UI_DIRECTION.md` → "App icon" owns the design.

- `main.svg` (192 px): the full icon for Android 7 and
  `application/config/icon`.
- `foreground.svg` and `background.svg` (432 px): the adaptive layers.
- `monochrome.svg` (432 px): Android 13 themed icons; white only.
- `splash.svg`: the Android 12+ launch splash. The badge carries its own
  dark disc from when the export could not set the splash background
  (#829).

`foreground.svg` owns the art; `main.svg` and `splash.svg` copy it, so
change them together. `AppIconTests` checks the colours, sizes, the preset
and both copies.
