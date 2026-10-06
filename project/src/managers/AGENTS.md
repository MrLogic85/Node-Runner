# AGENTS.md — `src/managers/`

Long-lived, app-wide services: Godot autoloads, one instance per app
lifetime, registered in `project/project.godot` → `[autoload]`.

## Rules

1. **Managers own state; UI reads it.**
2. **No UI code.** No `Control` references; only `SceneRouter` changes
   scenes. App-wide UI autoloads live in `project/src/ui/lib/`.
3. **Managers are the composition root.** They build the concrete
   App-layer classes and hand out their interfaces, so everything else takes
   constructor-injected interfaces (`libs/NodeRunner.App/AGENTS.md`).
4. **Code reaches an autoload with `GetNode<T>("/root/<Name>")`.**
5. **Raise C# events (`event Action<...>`) for state changes,** not only
   Godot signals: view-models subscribe to plain events in tests.
6. **Pure logic moves to a plain C# class in `libs/` and is tested there;**
   a manager that needs heavy test setup does too much.

## What lives here

- `RngProvider.cs` — the app's one seeded RNG (`IRngProvider`); reseeds
  from the clock on start and logs the seed
  (`docs/CODE_DESIGN_PRINCIPLES.md` §4)
- `SaveManager.cs` — builds `FileCreationRepository`,
  `CreationUpdateCoordinator`, the workflows, the progression repository and
  `DefaultCreationSeeder`, and hands out their interfaces
- `SceneRouter.cs` — the only place scenes change: keeps the
  `SceneBackStack`, maps each route to its scene in `ScenePaths`, and gives a
  scene implementing `IRoutedScene` its route and the router before it joins
  the tree (`docs/ARCHITECTURE.md` → Navigation)

`docs/MANUAL_TESTING.md` decides when a manager change needs manual
testing.
