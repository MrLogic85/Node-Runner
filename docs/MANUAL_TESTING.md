# Manual testing

Manual testing is decided per change; do not add ritual checks just because
a PR exists.

## Decision point

Every issue states whether manual testing is expected. If it is silent, the
PR author decides before opening the PR and records the decision in **How
to verify**.

Manual testing is usually required when the change affects:

- Godot scenes, nodes, physics, input, rendering or UI
- Android export, install, permissions, file paths or device behaviour
- Save/load behaviour that a user can observe
- Simulation feel, timing, determinism or training visualization
- Anything where "it compiles" does not prove the user experience

It is usually not required for documentation, pure-C# logic covered by
unit and architecture tests, build/CI metadata proven by its GitHub check,
or issue-tracker bookkeeping. When in doubt, write one small manual check in
the issue.

## What belongs in an issue

Manual checks live with the issue they prove. Reuse this format, not
another issue's test cases:

```markdown
## Manual test plan

Environment:
- Desktop OS:
- Godot version:
- Android device / API level: (if applicable)

Steps:
1. Open `project/project.godot`.
2. Run the main scene.
3. ...

Expected:
- ...

Evidence:
- Screenshot / recording:
- Seed and settings:
- Notes:
```

## What belongs in a PR

The PR's **How to verify** section records what actually ran:

- Automated commands and their results
- Manual checks performed, and those skipped with a reason
- Environment details when behaviour may depend on device, OS, Godot
  version, screen size or input method
- Screenshots or recordings when visual or physics behaviour matters,
  including every new screen or state a UI change adds

## Agent-run manual tests

"Manual" describes the user-visible workflow, not who runs it. An agent may
run manual checks when the runtime is available and the result is
observable from its tools: launching Godot, exporting, installing with
`adb`, driving a connected device, capturing screenshots and reading
`adb logcat`. Before claiming a pass, it records the evidence it used
(command output, logs, screenshot or recording path, seed, settings). When a
check needs subjective judgement, such as touch or animation feel, or its
runtime is missing, the agent records the missing prerequisite and leaves
the check to the human; it never reports such a check as passed.

## Desktop checks

Desktop is the first target because it is fast and close to the editor.

When an issue changes how screens fill the display, its test plan considers
the desktop window at 16:9, 20:9, 4:3 and 1:1, and on Android a wide phone
and a phone with a camera cutout in both landscape orientations
(`docs/UI_DIRECTION.md` → "Screen size and safe area"). A layout change is
also seen at Min (50%), Auto and Max UI size (`docs/UI_DIRECTION.md` → "UI
size"). UI size is set for the session on the Colors & Styles page
(Settings: #201), and restarting the app returns to Auto. On a phone, Max is
the tightest canvas (640 × 360 inside the safe area).

## Android checks

Android checks are required when the issue affects export, install, device
input, permissions, file paths, performance, battery, or anything likely to
differ from desktop.

Use a connected physical phone first, because touch feel, screen density,
performance and rendering artifacts are easiest to judge there. The phones
belong to the owner: ask first whether they are free to use. Otherwise use
a configured Android emulator/AVD, and stop any emulator you started as
soon as you are done, since emulators are heavy for the local processor.
If neither is available, record the missing prerequisite.

An agent-run Android check needs Android export configured
(`docs/RELEASING.md` → "Android export"), a device or emulator that is
unlocked, authorized and listed by `adb devices`, and a non-secret debug
signing setup.

Useful evidence: the `adb install` result, an `adb logcat` excerpt for
launch or runtime failures, a screenshot or recording from the device, and
the device model, Android version, build type, seed and relevant settings.
