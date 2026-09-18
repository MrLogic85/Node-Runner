---
id: 0002
title: Configure Android export and produce first APK
status: open
priority: p1
type: chore
labels: [build, android, v0.1]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
---

## Summary

Get Godot's Android export working locally and produce a debug APK that
installs and launches on a real device.

## Context

v0.1's ship criterion is "APK installed on a real Android device". This issue
unlocks that. Depends on #0001.

## Acceptance criteria

- [ ] Godot Android export template installed for the pinned Godot version
- [ ] JDK, Android SDK, build-tools and platform-tools installed and
      referenced from Godot Editor Settings
- [ ] Debug keystore generated and configured
- [ ] `export_presets.cfg` committed (values only; secrets go in
      `.env`/user-only files, ignored)
- [ ] `godot --headless --export-debug "Android" build/nodrunner.apk` works
      from CLI
- [ ] APK installs via `adb install` on at least one physical device and
      launches the empty Main scene without crashing

## Notes

- Target min API: TBD — pick the lowest that Godot 4 supports comfortably.
  Document the choice in `docs/ARCHITECTURE.md` when landed.
- Do not enable Mono/.NET AOT yet; JIT is fine for dev.
