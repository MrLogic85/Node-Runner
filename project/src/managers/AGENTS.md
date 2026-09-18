# AGENTS.md — `src/managers/`

**Long-lived, app-wide services. Godot autoloads.**

## Rules

1. **Managers are Godot autoloads** — registered in `project.godot`'s
   `[autoload]` section. One instance per app lifetime.
2. **Managers own state; UI reads it.** They are the source of truth for
   things like current settings, current save slot, active RNG seed.
3. **No UI code in managers.** No `Control` references, no scene changes.
   Scene routing is one specific manager's job (`SceneRouter`), not every
   manager's business.
4. **Access via typed helpers, not string paths.** Prefer a small
   `Services.SaveManager` locator over
   `GetNode<SaveManager>("/root/SaveManager")` scattered across the codebase.
5. **Managers depend on `NodeRunner.App` service interfaces, not concrete
   implementations.** Managers are the **composition root**: they instantiate
   the concrete `FileCreatureRepository`, `RngProvider`, etc., and hand them
   to view-models via constructor injection. Higher-level orchestrators;
   repositories are dumb data pipes.

## What lives here

- `RngProvider.cs` — the seeded RNG for the current simulation, exposes seed
  in the UI, logs it, can be reseeded
- `SettingsManager.cs` — user preferences (mutation rate defaults, time
  scale, etc.)
- `SaveManager.cs` — orchestrates saving/loading creatures via
  `ICreatureRepository`
- `SceneRouter.cs` — the only place `GetTree().ChangeSceneToFile(...)` is
  called
- `Services.cs` — static locator that resolves autoloads by type

## What does NOT live here

- Simulation logic → `project/src/sim/`
- UI state formatting → `libs/NodeRunner.App/ViewModels/`
- Actual file I/O implementations → `libs/NodeRunner.App/Repositories/`
- Pure ML → `libs/NodeRunner.ML/`

## Style specifics

- Inherit from `Godot.Node` (usually) so autoload works.
- Emit C# events (`event Action<...>`) for state changes, not just Godot
  signals — viewmodels prefer subscribing to plain events for testability.
- Keep `_Ready()` short. Heavy init goes in an explicit `InitializeAsync()`
  called by the router or a boot scene.
- Configuration is passed in via `SettingsManager`, not read directly from
  `ProjectSettings`.

## Autoload registration

When you add a manager, register it in `project/project.godot`:

```ini
[autoload]

RngProvider="*res://src/managers/RngProvider.cs"
SettingsManager="*res://src/managers/SettingsManager.cs"
SaveManager="*res://src/managers/SaveManager.cs"
SceneRouter="*res://src/managers/SceneRouter.cs"
```

The leading `*` means the node is added to the scene tree.

## Test expectations

- Pure logic in a manager (e.g. seed derivation) is extracted into a plain
  C# helper class and tested there.
- `docs/MANUAL_TESTING.md` decides when a manager change needs manual
  testing. If a manager needs heavy test setup, it's probably doing too much —
  split it.
