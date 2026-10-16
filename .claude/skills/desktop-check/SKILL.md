---
name: desktop-check
description: Build and run Node Runner on the desktop to check a change by hand, with screenshots and the log as evidence. Use when a manual test plan has desktop steps, or to see a UI or simulation change before an Android check.
---

# Desktop check

`docs/MANUAL_TESTING.md` owns when a desktop check is needed, which window
and UI sizes to try ("Desktop checks") and what counts as evidence
("Agent-run manual tests"); this skill owns how to run one on macOS.

1. Build the game, since Godot runs the last built assemblies, and stop if
   the build fails:

   ```bash
   dotnet build project/NodeRunner.csproj
   ```

2. Capture each screen the plan names with the `Screenshot` tool
   (`.claude/rules/tools.md`), which takes the scene, the image's absolute
   path and a frame count (60 if left out). `--resolution` sets the
   window to the size for a ratio "Desktop checks" names (16:9 1280x720,
   20:9 1600x720, 4:3 960x720, 1:1 720x720). The runs share a throwaway `HOME`, so Godot's `user://` is
   `.scratch/home` and the human's saves are never read or changed; delete
   it before a new check. The block fails when no image was saved.

   ```bash
   godot=/Applications/Godot_mono.app/Contents/MacOS/Godot
   out="$PWD/.scratch/training.png"
   mkdir -p .scratch/home && rm -f "$out"
   HOME="$PWD/.scratch/home" "$godot" --path project --resolution 1600x720 \
     res://scenes/tools/Screenshot.tscn \
     -- res://scenes/hosts/TrainingHost.tscn "$out" 300 > .scratch/desktop.log 2>&1 &&
     test -s "$out"
   ```

   The scenes in `project/scenes/hosts/` open each screen; `TrainingHost`
   alone trains its built-in creature. To load a test creation, put its
   `creation.json` under
   `.scratch/home/Library/Application Support/Godot/app_userdata/Node Runner/`
   as `docs/SAVE_FORMAT.md` → "Layout" shows. macOS blocks `screencapture`
   from an agent's shell, which gives a black image.
3. Read `.scratch/desktop.log` for `ERROR`, `SCRIPT ERROR` and C#
   exceptions, then look at the image. The tool exits 0 even when the
   scene logged errors, so the log is the only sign of them.
4. The human runs what the agent cannot drive or judge
   (`docs/MANUAL_TESTING.md` → "Agent-run manual tests"), with
   `godot --path project`. That includes Min and Max UI size, which is set
   by hand in the running game, while the tool sees Auto.
5. Record the run in the PR's **How to verify** as `docs/MANUAL_TESTING.md`
   → "What belongs in a PR" lists, with the window size, then delete
   `.scratch/home` and the other scratch files.
