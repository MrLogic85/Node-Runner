---
paths:
  - "libs/NodeRunner.App/**"
  - "tests/NodeRunner.App.Tests/**"
---

# App rules — `libs/NodeRunner.App`

Application-level pure C#: view-models, services, repositories and their
in-memory fakes. The bridge between the Godot side and the pure layers.

## Rules

- **Every service Godot code consumes has an interface,** and callers use
  it, so tests substitute it with NSubstitute.
- **Constructor injection, no static state or singletons.** The composition
  root is `SaveManager` (`.claude/rules/managers.md`).
- **JSON via `System.Text.Json` only** (`SaveJson`).
- **No `async void`,** so every fault reaches a caller.
- **Fakes live next to their interfaces.** `InMemoryCreationRepository` is
  production code that also serves as a test double.
- **Never call `ICreationUpdateCoordinator.Get` while holding a creation's
  lock** (inside an `UpdateIfPresent` update) **or from a queued save:**
  `Get` waits for that creation's queued saves, so it deadlocks (#370).
- **A `SaveMigration` edits the JSON one version up** and throws
  `InvalidDataException` for a file it cannot change, which loads as a bad
  file. Any other exception is a bug and is not caught (`VersionedSaveFile`;
  policy in `docs/SAVE_FORMAT.md` → Versions and migration).
- **File paths come from `IStorageLocation`,** so Godot injects `user://`
  paths on Android.

## What lives here

- `ViewModels/` — screen state and presentation: `BuildViewModel` and its
  `BuildGestures`, `CreationsPresentationViewModel`,
  `TrainingPresentationViewModel` (shadows), the Network focus and Signal flow
  presentations
- `Repositories/` — `ICreationRepository` (`FileCreationRepository`,
  `InMemoryCreationRepository`), `CreationShareCode`,
  `IProgressionRepository` (file and in-memory), `VersionedSaveFile`, `SaveJson`, `FilePersistenceExceptions`
- `Services/` — `IRngProvider`, `ICreationUpdateCoordinator`, the
  workflows (`INewCreationWorkflow`, `IBuildEditWorkflow`,
  `ICreationDuplicateWorkflow`, `IExampleCopyWorkflow`,
  `ICreationImportWorkflow`), `EvolutionSetup`,
  `CreationExamples`, `DefaultCreationSeeder`, `ShadowsBudget`,
  `SlowMotionWatch`
- `Builders/` — `CreatureBuilder`, the in-progress Build creature, with
  `TryBuild` into an immutable `CreatureDef`; it holds state, so it cannot
  live in Domain
- `Navigation/` — routes and history; declare every route here, because the
  plain-value test scans only this assembly (`docs/ARCHITECTURE.md` →
  Navigation)
- `Lifecycle/` — `CreationLock`, `CreatureReadiness`: lifecycle rules read
  from Domain records (`docs/TRAINING_LOOP.md` → Product lifecycle boundary)

## Tests

`tests/NodeRunner.App.Tests/`:

- View-models: assert the state an action leaves, and a
  `PropertyChanged` event only where a view relies on it. Fake
  repositories with the in-memory ones and other services with
  `Substitute.For<...>()`.
- File repositories run against a per-test temp directory
  (`Path.GetTempPath()`, cleaned up in `Dispose`).
