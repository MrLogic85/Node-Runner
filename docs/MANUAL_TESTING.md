# Manual testing

Manual testing is decided per issue and per change. There is no shared smoke
suite yet; do not add ritual checks just because a PR exists.

## Decision point

Every issue should state whether manual testing is expected. If the issue is
silent, the PR author decides before opening the PR and records the decision
in **How to verify**.

Manual testing is usually required when the change affects:

- Godot scenes, nodes, physics, input, rendering, or UI
- Android export, install, permissions, file paths, or device behavior
- Save/load behavior that a user can observe
- Simulation feel, timing, determinism, or training visualization
- Anything where "it compiles" does not prove the user experience

Manual testing is usually not required for:

- Pure documentation changes
- Pure-C# logic fully covered by unit and architecture tests
- Build/CI metadata that is already proven by the relevant GitHub check
- Issue-tracker bookkeeping

When in doubt, write one small manual check in the issue instead of guessing.

## What belongs in an issue

Manual checks live with the issue they prove. Keep them concrete and
observable:

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

Do not copy a generic checklist between issues. Reuse the format, not the
test cases.

## What belongs in a PR

The PR's **How to verify** section records what actually ran:

- Automated commands and their results
- Manual checks performed
- Manual checks intentionally skipped, with a reason
- Environment details when behavior may depend on device, OS, Godot version,
  screen size, or input method
- Evidence links or attachments when visual/physics behavior matters

## Agent-run manual tests

An AI agent can run a manual test only when the environment is available and
the expected result is observable from tools. Prefer agent-run tests for:

- CLI commands (`dotnet`, export scripts, import checks)
- Godot headless operations when Godot is installed and scriptable
- Android install/log/screenshot flows when a device is connected, unlocked,
  and trusted by `adb`
- Screenshot or recording inspection when files are available to the session

Before claiming a manual test passed, the agent must capture the evidence it
used: command output, logs, screenshot path, recording path, seed, or
settings.

Agent-run tests are not enough when the result is subjective or requires real
touch feel, animation feel, or visual judgement that cannot be captured in
the session. In those cases, the agent records the blocker and leaves the
manual check for the human.

## Desktop checks

Desktop is the first manual target because it is fast and close to the editor:

- Open `project/project.godot` in Godot.
- Run the relevant scene.
- Verify the issue's expected behavior.
- Capture a screenshot or short recording for UI/visual behavior.

If Godot is unavailable to the agent, the PR should say so and leave the
desktop check for the human.

## Android checks

Android checks are required when the issue affects export, install, device
input, permissions, file paths, performance, battery, or any behavior likely
to differ from desktop.

Agent-run Android checks require:

- Android export configured
- A connected device visible in `adb devices`
- Device unlocked and authorized
- A non-secret debug signing setup

Useful evidence:

- `adb install` result
- `adb logcat` excerpt for launch/runtime failures
- Screenshot or screen recording from the device
- Device model, Android version, build type, seed, and relevant settings

If no device is available, record the missing prerequisite rather than
pretending the Android check passed.
