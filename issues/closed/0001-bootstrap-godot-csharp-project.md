---
id: 0001
title: Bootstrap Godot 4 C# project and repo scaffolding
status: closed
priority: p1
type: chore
labels: [build, chore, v0.1]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
closed: 2026-09-16
---

## Summary

Create the Godot 4 project inside `project/`, configured for C# (.NET), and add
a working `.gitignore`. First commit of an actual buildable project.

## Context

Prerequisite for every other v0.1 issue. See `docs/ROADMAP.md` → v0.1 and
`docs/ARCHITECTURE.md` for the target folder layout.

## Acceptance criteria

- [ ] `project/project.godot` exists and opens cleanly in Godot 4 (latest stable)
- [ ] C# / .NET support enabled; `project/*.csproj` present
- [ ] Empty `Main.tscn` set as the main scene
- [ ] `.gitignore` excludes `.godot/`, `.mono/`, `bin/`, `obj/`, `*.user`,
      `export_presets.cfg` (until we need it)
- [ ] Project builds from CLI: `dotnet build project/*.csproj` succeeds
- [ ] Empty folders created (with `.gitkeep`): `src/ml/`, `src/creature/`,
      `src/sim/`, `src/ui/`, `scenes/`, `tests/`

## Notes

- Godot version to pin: latest stable 4.x at time of setup. Record it in the
  first commit message.
- We are not yet setting up Android export — that's #0002.

## Resolution

Closed 2026-09-16.

- Godot 4.7.2 stable (Mono) installed to `/Applications/Godot_mono.app`
- .NET 10.0.401 SDK installed via Homebrew
- Created `project/` with:
  - `project.godot` — name "Node Runner", main scene `res://scenes/Main.tscn`,
    physics_ticks_per_second=60, mobile renderer, .NET assembly name "NodeRunner"
  - `NodeRunner.csproj` — `Godot.NET.Sdk/4.4.0`, targets `net8.0`,
    nullable enabled, root namespace `NodeRunner`
  - `scenes/Main.tscn` — empty `Node2D` root
  - Empty folders with `.gitkeep`: `src/{ml,creature,sim,ui}`, `tests`
- Verified: Godot imports project cleanly, `dotnet build NodeRunner.csproj`
  succeeds with 0 warnings / 0 errors, `Main.tscn` runs headless without
  errors.
- `.gitignore` at repo root already covers `.godot/`, `bin/`, `obj/`, `*.user`.
