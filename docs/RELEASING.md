# Releasing

How a version is exported and published as a signed APK on GitHub
Releases. Changes still land through PRs (`docs/REVIEW.md`).

## Versions

`application/config/version` in `project/project.godot` is the only
version source, and `main` carries the version of the milestone in
progress. The `Android` export preset leaves `version/name` empty so the
export reads it, and sets `version/code` to 1000000·major + 1000·minor +
patch (0.13.0 → 13000). `.github/scripts/set-version.sh X.Y.Z` changes
both; `ArchitectureSpec` checks they agree.

## Releasing a milestone

A milestone is named after the version it ships, usually X.Y.0; a minor
can also ship in steps from `main` as X.Y.0, X.Y.1, …. Every milestone
has a "Release X.Y.Z" issue (`type: chore`, `area: android`), blocked by
the milestone's other issues and by the previous release issue. When it
is the last open issue and the human has asked for the release, the
`release-milestone` skill releases it in three parts:

- A PR titled `docs(#N): Release notes for X.Y.Z` adds
  `docs/release-notes/X.Y.Z.md` (`.claude/rules/release-notes.md`). It
  says `Part of #N`, so the issue stays open until the release is out.
- `.github/scripts/release.sh`, from an up-to-date `main`, exports and
  checks a signed release APK, tags `vX.Y.Z`, creates or moves
  `release/vX.Y` to the tag, and publishes the GitHub release with the
  notes file as its text and the APK attached.
- A PR titled `chore(#N): Bump main to <next>` sets the next milestone's
  version and closes the release issue.

## Fix releases

A fix that the human decides cannot wait for the next milestone lands on
`release/vX.Y` by PR, and also on `main` when it applies there. That PR
bumps the patch to the next free number and adds its release notes; once
merged, `release.sh` publishes it from that branch. If a milestone on
`main` planned that number, rename it, the later ones and their release
issues up by one, and bump `main` to match. A fix that applies to `main`
must reach it before the next release from `main`, which force-moves
`release/vX.Y`; `release.sh` refuses that move while the branch tip has
no tag, and refuses a version older than the newest `vX.Y.*` tag.

No ruleset protects `release/**`, so auto-merge is not available there:
merge by hand once the checks pass, and `release.sh` refuses to publish
unless they passed on HEAD. GitHub honours closing keywords only on PRs
into `main`, so close the issue by hand unless its `main` PR closes it.
The `fix-release` skill has the steps.

## Publishing safeguards

`release.sh` checks that the active `gh` login can push before it tags,
and from `main` pushes the tag and `release/vX.Y` together, so a failed
push leaves nothing on GitHub. When it stops, follow its message; never
re-tag a pushed version.

## Signing key

The release keystore is kept outside the repository. Every release must
be signed with the same key, or installed copies cannot update, so back
it up with its password. `release.sh` reads its location, alias and
password from `NODE_RUNNER_KEYSTORE*` environment variables or the macOS
Keychain (see the script header); machine-specific values belong in
`LOCAL_CONFIG.md`.

## Android export

The one `Android` preset in `project/export_presets.cfg` exports through
Godot's Gradle build. A debug export gets its own package
(`dev.mrlogic85.noderunner.debug`) and app name (Node Runner Debug), so
it installs next to the release with its own saved data; Android never
replaces an app with one signed by another key. Three tracked files
change Godot's build template, and `ArchitectureSpec` checks them:

- `project/android/build/build.gradle`: `applicationIdSuffix ".debug"` in
  the `debug` build type.
- `project/android/build/src/monoDebug/AndroidManifest.xml`: the debug
  label (Godot rewrites `src/debug` on every export).
- `project/android/build/src/main/java/com/godot/game/GodotApp.java`:
  turns on sentence capitalization for the keyboard Godot opens, so a
  name starts with a capital.

The rest of `project/android/` is Godot's template, about 270 MB, so it
is git-ignored. Install it once per clone and after a Godot upgrade.
Godot installs it only as part of an export, which overwrites the
tracked files, so restore them and export again:

```bash
cd project
mkdir -p ../.scratch
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless \
  --install-android-build-template --export-debug Android ../.scratch/template.apk
git checkout -- android/build/
rm ../.scratch/template.apk
```

After an upgrade, keep Godot's new `build.gradle` and `GodotApp.java`
instead, and carry the `applicationIdSuffix` line and the keyboard hook
over.

A debug APK, signed with the developer's debug key:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path project \
  --export-debug Android ../build/node-runner-debug.apk
```

`release.sh` installs a fresh template and restores the tracked files
before `--export-release`, so local template edits never reach a release
and ours always do; it stops if those files have uncommitted changes. A
release build hides debug-only UI such as the Component library link,
since `OS.IsDebugBuild()` is false.

Local prerequisites: Godot's Mono export templates for the version in
`README.md` → "Tools", JDK 21, Android SDK platform 36 and build-tools
36.1, platform-tools, a user-local debug keystore in the Godot editor
settings, and a network for the first Gradle build. Keystore paths and
passwords stay in user-local Godot settings, ignored credential files or
the macOS Keychain, never in the preset.

The preset leaves min SDK and target SDK empty to keep the template's
(24 and 36, `android/build/config.gradle`). Android uses the
Compatibility renderer because Mobile/Vulkan crashed in Godot's
`VkThread` on the SM-S938B; #961 re-checks it.

### App icon

The preset's `launcher_icons/*` and `splash_screen/icon`, and
`application/config/icon`, point at the SVGs in
`project/assets/icons/app/`, which the export scales to every density.
`docs/UI_DIRECTION.md` → "App icon" owns the design.

- `main.svg` (192 px): the full icon for Android 7 and
  `application/config/icon`.
- `foreground.svg` and `background.svg` (432 px): the adaptive layers.
  `foreground.svg` is also the Android 12+ launch splash.
- `monochrome.svg` (432 px): Android 13 themed icons; white only.

`foreground.svg` owns the art and `main.svg` copies it, so change them
together; `AppIconTests` checks colours, sizes, the preset and the copy.
