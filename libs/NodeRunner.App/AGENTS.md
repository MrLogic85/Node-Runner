# AGENTS.md — `libs/NodeRunner.App`

Application-level pure C#: view-models, service interfaces, repositories,
in-memory fakes. The bridge between the Godot side and the pure ML/Domain
layers.

## Hard rules

- **No `Godot.*` references.** Enforced by `NodeRunner.Arch.Tests`.
- **Every service that Godot code will consume has an interface.** Managers
  and repositories are consumed via their interface, not the concrete type.
  This is what enables mocking with NSubstitute in tests.
- **Constructor injection everywhere.** Static state and singletons are
  forbidden. Composition happens in Godot autoloads (`project/src/managers/`).
- **JSON via `System.Text.Json`.** No third-party serialisers.
- **No `async void`.**

## What lives here

- `ViewModels/` — `MainViewModel`, `PopulationViewModel`, `NetworkViewModel`,
  `ObservableObject` base (raises `INotifyPropertyChanged`)
- `Repositories/` — `ICreatureRepository`, `FileCreatureRepository`,
  `InMemoryCreatureRepository` (test fake, also usable in production)
- `Services/` — `IRngProvider`, `ISettings`, cross-cutting service
  interfaces
- `Results/` — `Result` / `Result<T>` helpers if we go that route (decide when
  first I/O lands)
- `Builders/` — mutable, stateful construction helpers that assemble
  `NodeRunner.Domain` records over several steps (e.g. `CreatureBuilder` for
  0.3.0's construction mode). These hold in-progress state and expose
  `TryBuild(...)` to attempt converting it into an immutable Domain type;
  they belong here rather than in Domain because Domain permits no
  behavior beyond validation (see `libs/NodeRunner.Domain/AGENTS.md`).

## What does NOT live here

- Actual Godot autoloads → `project/src/managers/`
- Godot Nodes → `project/src/{creature,ui,sim}/`
- NN math → `libs/NodeRunner.ML/`
- Data records → `libs/NodeRunner.Domain/`

## Style

- `sealed class` for stateful services; `sealed record` for events/DTOs.
- Interfaces + concrete impl live side by side (`ICreatureRepository.cs` next
  to `FileCreatureRepository.cs`).
- File I/O uses `System.IO`; abstract the *path* via an `IStorageLocation`
  service so the Godot side can inject `user://` paths on Android.

## Tests

`tests/NodeRunner.App.Tests/` — xUnit + Shouldly + NSubstitute.

- View-models: fake dependencies via `Substitute.For<...>()`, drive events,
  assert `INotifyPropertyChanged` firings.
- Repositories: file impls tested against a temp directory; in-memory impls
  double as test doubles for other layers.
