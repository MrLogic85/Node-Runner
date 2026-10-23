# Manual testing

Manual testing is decided per change; add no ritual checks just because a
PR exists.

## Decision point

Every issue states whether manual testing is expected. If it is silent,
the PR author decides before opening the PR and records the decision in
**How to verify**.

Manual testing is usually required when the change affects:

- Godot scenes, nodes, physics, input, rendering or UI
- Android export, install, permissions, file paths or device behaviour
- Save/load behaviour that a user can observe
- Simulation feel, timing, determinism or training visualization
- Anything where "it compiles" does not prove the user experience

It is usually not required for documentation, pure-C# logic covered by
tests, build or CI metadata proven by its check, or issue-tracker
bookkeeping. When in doubt, write one small manual check in the issue.

## What belongs in an issue

Manual checks live with the issue they prove, in this format:

```markdown
## Manual test plan

Environment:
- Desktop OS / Android device and API level:

Steps:
1. ...

Expected:
- ...

Evidence:
- Screenshot / recording:
- Seed and settings:
```

## What belongs in a PR

The PR's **How to verify** section records what actually ran:

- Automated commands and their results
- Manual checks performed, and those skipped with a reason
- The environment, when behaviour may depend on device, OS, Godot
  version, screen size or input method
- Screenshots or recordings when visual or physics behaviour matters,
  including every new screen or state a UI change adds

## Agent-run manual tests

"Manual" describes the user-visible workflow, not who runs it. An agent
may run a check whose result it can observe with its tools, and records
the evidence it used before claiming a pass. A check that needs
subjective judgement, such as touch or animation feel, or whose runtime
is missing, is left to the human with the reason; an agent never reports
it as passed. The `plan-manual-tests` skill writes the plan, and the
`desktop-check` and `android-check` skills run it.

## Desktop checks

Desktop comes first because it is fast. A change to how screens fill the
display is checked in the desktop window at 16:9, 20:9, 4:3 and 1:1, and
on Android on a wide phone and a phone with a camera cutout in both
landscape orientations (`docs/UI_DIRECTION.md` → "Screen size and safe
area"). A layout change is also seen at Min, Auto and Max UI size
(`docs/UI_DIRECTION.md` → "UI size").

## Android checks

An Android check is required when the change affects export, install,
device input, permissions, file paths, performance, battery, or anything
likely to differ from desktop.

Use a connected physical phone first: touch feel, density, performance
and rendering artifacts are easiest to judge there. The phones belong to
the owner, so ask first whether they are free. Otherwise use a configured
emulator, and stop it as soon as you are done, since emulators load the
local processor. If neither is available, record the missing
prerequisite.

A subagent cannot ask, so it uses an emulator unless its caller's prompt
says the phone is free. The caller then claims any device lock before
the subagent starts and releases it after the subagent returns.

Evidence: the `adb install` result, an `adb logcat` excerpt for launch or
runtime failures, a screenshot or recording, and the device model,
Android version, build type, seed and relevant settings.
