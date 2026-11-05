---
name: change-app-icon
description: Change Node Runner's launcher icon or Android launch splash and check it on a device. Use when asked to change, redraw or fix the app icon, its themed (monochrome) layer or the launch splash.
---

# Change the app icon

Rules: `docs/UI_DIRECTION.md` → "App icon" for the design,
`docs/RELEASING.md` → "App icon" for the files.

1. Agree the change with the human and the `design-lead` agent. The
   design changes: update `docs/UI_DIRECTION.md` → "App icon" first.
2. Edit the SVGs in `project/assets/icons/app/` per `docs/RELEASING.md`
   → "App icon", starting with the one that owns the art.
3. Run the icon tests:

   ```bash
   dotnet test tests/NodeRunner.Ui.Tests --filter AppIconTests
   ```

4. Check it on a device with the `android-check` skill: the launcher
   icon, a themed icon on Android 13+, and the launch splash. Attach the
   screenshots to the PR.
