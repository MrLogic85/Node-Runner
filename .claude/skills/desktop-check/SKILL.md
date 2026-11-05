---
name: desktop-check
description: Build and run Node Runner on the desktop to check a change by hand, with screenshots and the log as evidence. Use when a manual test plan has desktop steps, or to see a UI or simulation change before an Android check.
---

# Desktop check

Rules: `docs/MANUAL_TESTING.md` → "Desktop checks". For macOS.

1. Build the game, since Godot runs the last built assemblies. The build
   fails: stop.

   ```bash
   dotnet build project/NodeRunner.csproj
   ```

2. Capture each screen the plan names with the `Screenshot` tool
   (`project/src/tools/Screenshot.cs`). It takes the scene, the image's
   absolute path and a frame count (default 60).
   - `--resolution` sets the window for a ratio "Desktop checks" names:
     16:9 1280x720, 20:9 1600x720, 4:3 960x720, 1:1 720x720.
   - The runs share a throwaway `HOME`, so Godot's `user://` is under
     `.scratch/home` and the human's saves are never touched. Delete it
     before a new check.
   - The block fails when no image was saved.

   ```bash
   godot=/Applications/Godot_mono.app/Contents/MacOS/Godot
   out="$PWD/.scratch/training.png"
   mkdir -p .scratch/home && rm -f "$out"
   HOME="$PWD/.scratch/home" "$godot" --path project --resolution 1600x720 \
     res://scenes/tools/Screenshot.tscn \
     -- res://scenes/hosts/TrainingHost.tscn "$out" 300 > .scratch/desktop.log 2>&1 &&
     test -s "$out"
   ```

   - Screens: the scenes in `project/scenes/hosts/`. `TrainingHost` alone
     trains its built-in creature.
   - A test creation: put its `creation.json` under
     `.scratch/home/Library/Application Support/Godot/app_userdata/Node Runner/`
     as `docs/SAVE_FORMAT.md` → "Layout" shows.
   - Not `screencapture`: macOS blocks it from an agent's shell, which
     gives a black image.
3. Read `.scratch/desktop.log` for `ERROR`, `SCRIPT ERROR` and C#
   exceptions, then look at the image. The tool exits 0 even when the
   scene logged errors, so the log is the only sign of them.
4. Leave to the human, with `godot --path project`, what you cannot drive
   or judge (`docs/MANUAL_TESTING.md` → "Agent-run manual tests"). That
   includes Min and Max UI size: it is set by hand in the running game,
   and the tool sees Auto.
5. Record the run in the PR's **How to verify** per
   `docs/MANUAL_TESTING.md` → "What belongs in a PR", with the window
   size. Then delete `.scratch/home` and the other scratch files.
