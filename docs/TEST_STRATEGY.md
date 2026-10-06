# Test Strategy

How each layer of Node Runner is tested, with what tools, and why. Test-code
conventions (layout, naming, AAA) are in `tests/AGENTS.md`; each library's
test focus is in its `libs/*/AGENTS.md`.

## Guiding principles

1. **Fast feedback.** `dotnet test NodeRunner.slnx` finishes in seconds;
   anything slow is a smell.
2. **Deterministic.** Tests follow the determinism rules in
   `docs/CODE_DESIGN_PRINCIPLES.md` §4.
3. **Layer boundaries are code.** The library layer rules live in
   `NodeRunner.Arch.Tests` and UI ownership rules in `NodeRunner.Ui.Tests`.
4. **Test the risky bits, not the trivia.** Unit tests are mandatory for
   Domain, Mechanics and ML. Compiler-checked things (record equality, enum
   switches) get one canary test at most. We chase "the tricky parts are
   pinned down", not 100% coverage.

## Layers

| Layer | Lives in | Tooling | What's tested |
|---|---|---|---|
| Domain | `libs/NodeRunner.Domain/` | xUnit + Shouldly | Invariants, JSON round-trip |
| Mechanics | `libs/NodeRunner.Mechanics/` | xUnit + Shouldly | Part physics on hand-checkable numbers |
| ML | `libs/NodeRunner.ML/` | xUnit + Shouldly | Forward pass, GA operators, activation math, genome round-trips |
| App | `libs/NodeRunner.App/` | xUnit + Shouldly + NSubstitute | View-models, repositories, services |
| Architecture | `libs/`, `project/` | xUnit + NetArchTest | Layer graph, source conventions, export presets |
| Static UI contracts | `project/src/`, `project/scenes/`, theme files | xUnit + Shouldly + Roslyn | See "UI contracts" |
| Godot Nodes | `project/src/` | Manual (`docs/MANUAL_TESTING.md`) | Node lifecycle, physics, input |
| End-to-end | full app on device | Manual, decided per change | Feel, latency, battery |

Godot Node behaviour is verified manually; GdUnit4 tests are #957.

## Tools

Changing a tool needs an issue. `Directory.Packages.props` pins the
versions.

| Tool | Purpose | Why |
|---|---|---|
| xUnit | Test framework | Standard, mature, fast |
| Shouldly | Assertions | Apache 2.0; FluentAssertions changed licence at v8 |
| NSubstitute | Mocking | Cleaner API than Moq |
| NetArchTest.Rules | Architecture assertions | Keeps layer rules executable |
| coverlet | Coverage on every `dotnet test` | Default, already wired |
| Microsoft.CodeAnalysis.CSharp (Roslyn) | UI source guards | Parses and binds C#, so hardcoding rules check real syntax and types, not text |

## Architecture tests

`NodeRunner.Arch.Tests` (`ArchitectureSpec.cs`) guards the layer graph
(`docs/ARCHITECTURE.md`), the file-size limit
(`docs/CODE_DESIGN_PRINCIPLES.md` §5) and the export presets
(`docs/RELEASING.md` → "Android export"). Add a fact when we decide to
enforce a convention.

## UI contracts

`NodeRunner.Ui.Tests` reads theme files, scenes and C# source without the
Godot scene tree. Each concern's rule lives in the owning doc:

| Concern | Tests | Rule owner |
|---|---|---|
| Values come from their owner, so a root Theme swap restyles everything | `UiTokensTests`, `UiThemeExpanderTests`, `SceneDerivedStateTests`, `UiSourceGuardTests`, `AppIconTests` | `docs/UI_DIRECTION.md` → "Who owns what", `project/src/ui/lib/AGENTS.md`, `docs/RELEASING.md` → "App icon" |
| Draw order and shared part visuals | `UiLayersTests`, `DrawLayersTests`, `SharedPartVisualsTests` | `project/src/ui/lib/AGENTS.md`, `project/src/theme/AGENTS.md` |
| Text is translated once, by Godot | `UiTextTranslationTests`, `UiTranslationContextTests`, `TranslationTemplateTests` | `docs/LOCALIZATION.md`, `project/src/ui/AGENTS.md` |
| Screens reuse the library | `UiComponentContractsTests`, `RewrittenSceneTests`, `UiSourceGuardTests` | `docs/UI_DIRECTION.md` → "Who owns what", `project/src/ui/AGENTS.md` |
| Saved scenes survive export | `SceneEditableChildrenTests`, `SceneParentPathTests` | Export re-packs every scene, and a node added inside an instance that is not `[editable]`, under a stale parent path, or beside a same-named sibling vanishes only on device (#333, #407, #496) |
| Every route opens its scene | `SceneRouterTests` | `docs/ARCHITECTURE.md` → "Navigation" |

Behaviour is tested apart from layout: rules and state live in App
view-models, and component behaviour in pure contract functions in the UI
library (`UiComponentContracts`, slider values, progress), both tested
without a scene. Whether a scene's controls reach the right view-model
action is checked on device.

## What we do NOT test

- Compiler-generated record members (`Equals`, `GetHashCode`, `ToString`)
- Trivial getters and setters
- .NET BCL or Godot engine behaviour
- A scene's arrangement: component choice, order and layout sizes belong to
  the scene and change freely in the editor
- Rendering, by instantiating Nodes
- Code shape (sealed/abstract, member kinds, base types): test the values a
  contract maps to, or guard against the hardcoding it prevents

## Running the suite

```bash
dotnet test NodeRunner.slnx                                # all tests
dotnet test tests/NodeRunner.ML.Tests/NodeRunner.ML.Tests.csproj   # one project
dotnet test NodeRunner.slnx --collect:"XPlat Code Coverage"       # + coverage
```

CI runs the coverage form on every PR and every push to `main`, and uploads
coverage without a threshold (#958).
