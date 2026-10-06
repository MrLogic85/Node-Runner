# AGENTS.md — `src/tools/`

**Developer tools run from the command line or the editor. Never part of the game.**

## Rules

1. **Not shipped.** Each tool has a scene under `project/scenes/tools/`,
   which `export_presets.cfg` excludes from export. Nothing in the game may
   reference a tool.
2. **Thin.** A tool only loads, calls library code, saves and exits with a
   non-zero code on failure. The logic lives in the layer it serves (e.g.
   `UiThemeExpander` in `ui/lib`), where it is tested.
3. **Fail loudly.** Log the full exception and quit with exit code 1.

## What lives here

- `ExpandThemes.cs` — regenerates the palette-derived items of the theme
  files. Run it after editing a palette colour:
  1. Build Debug: `dotnet build project/NodeRunner.csproj`.
  2. `Godot --headless --path project res://scenes/tools/ExpandThemes.tscn`
     (on macOS, `/Applications/Godot_mono.app/Contents/MacOS/Godot`).
  3. A command-line save drops the font `uid`s from `Neon.tres`'s
     `ext_resource` lines; restore them, or re-save in the editor, before
     committing.

  `UiThemeExpanderTests` fail when a theme was edited without regenerating.
