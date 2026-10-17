# Test Strategy

How each layer of Node Runner is tested, with what tools, and why.
Test-code conventions are in `.claude/rules/tests.md`.

## Guiding principles

1. **A few tests that cover a lot.** Drive a behaviour through its public
   entry with a realistic input, and assert what the user or the next
   layer relies on. A test that can only fail when another does is
   merged or deleted.
2. **Test the risky bits, not the trivia.** New logic in Domain,
   Mechanics and ML has unit tests; branching, parsing, numerics and
   state changes are pinned down, not coverage percentages.
3. **Fast.** `dotnet test NodeRunner.slnx` finishes in seconds; anything
   slow is a smell.
4. **Deterministic,** by `docs/CODE_DESIGN_PRINCIPLES.md` §4.
5. **Rules that can be checked are code.** Layer and file rules live in
   `NodeRunner.Arch.Tests`, static Godot source contracts in
   `NodeRunner.Ui.Tests`. Add a test when we decide to enforce a
   convention.

## Layers

| Layer | Lives in | Tooling | What's tested |
|---|---|---|---|
| Domain | `libs/NodeRunner.Domain/` | xUnit + Shouldly | Invariants, JSON round-trip |
| Mechanics | `libs/NodeRunner.Mechanics/` | xUnit + Shouldly | Part physics on hand-checkable numbers |
| ML | `libs/NodeRunner.ML/` | xUnit + Shouldly | Forward pass, GA operators, activation math, genome round-trips |
| App | `libs/NodeRunner.App/` | xUnit + Shouldly + NSubstitute | View-models, repositories, services |
| Architecture | `libs/`, `project/`, agent files, LSP config, hooks | xUnit + NetArchTest | Layer graph, file size, Android export, agent, rule and skill files, hooks |
| Static Godot contracts | `project/src/`, `project/scenes/`, theme files | xUnit + Shouldly + Roslyn | See "UI contracts" and "Physics setup guards" |
| Godot Nodes | `project/src/` | Manual (`docs/MANUAL_TESTING.md`) | Node lifecycle, physics behaviour, input |
| End-to-end | full app on device | Manual, decided per change | Feel, latency, battery |

Godot-side automated tests are #957.

## Tools

Changing a tool needs an issue; `Directory.Packages.props` pins the
versions.

| Tool | Purpose | Why |
|---|---|---|
| xUnit | Test framework | Standard, mature, fast |
| Shouldly | Assertions | Apache 2.0; FluentAssertions changed licence at v8 |
| NSubstitute | Mocking | Cleaner API than Moq |
| NetArchTest.Rules | Architecture assertions | Keeps layer rules executable |
| coverlet | Coverage | The `dotnet test` default |
| Microsoft.CodeAnalysis.CSharp (Roslyn) | UI source guards | Checks real syntax and types, not text |

## UI contracts

`NodeRunner.Ui.Tests` reads theme files, scenes and C# source without
the Godot scene tree. It guards:

- Values come from their owner, so a root Theme swap restyles everything
  (`docs/UI_DIRECTION.md` → "Who owns what", `.claude/rules/ui-lib.md`).
- Draw order and shared part visuals (`docs/WORLD_VISUALS.md` → "Draw
  layers", `.claude/rules/theme.md`).
- Text is translated once, by Godot (`docs/LOCALIZATION.md`).
- Screens reuse the UI library (`.claude/rules/ui.md`).
- Saved scenes survive export, which re-packs every scene: a node added
  inside an instance that is not `[editable]`, under a stale parent path
  or beside a same-named sibling vanishes only on device.
- Saved enum numbers keep their meaning, and every route opens its scene
  (`docs/ARCHITECTURE.md` → "Navigation").

Behaviour is tested apart from layout: rules and state live in App
view-models, and component behaviour in pure contract functions in the
UI library, both tested without a scene. Whether a scene's controls
reach the right view-model action is checked on device.

## Physics setup guards

How creature bodies behave is checked by hand, but `NodeRunner.Ui.Tests`
guards the settings known to break them: no CCD on creature bodies, at
least 24 2D solver iterations (`.claude/rules/creature.md`), and Earth's
gravity at the metre scale (`docs/GLOSSARY.md` → "Metre").

## What we do NOT test

- Compiler-generated record members (`Equals`, `GetHashCode`, `ToString`)
- Trivial getters and setters, or setting a value and reading it back
  through code with no logic
- .NET BCL or Godot engine behaviour
- A scene's arrangement: component choice, order and layout sizes belong
  to the scene and change freely in the editor
- Rendering, by instantiating Nodes
- Code shape (sealed/abstract, member kinds, base types): test the values
  a contract maps to, or guard against the hardcoding it prevents

## Running the suite

```bash
dotnet test NodeRunner.slnx                                         # all tests
dotnet test tests/NodeRunner.ML.Tests/NodeRunner.ML.Tests.csproj    # one project
dotnet test NodeRunner.slnx --collect:"XPlat Code Coverage"         # + coverage
```

CI runs the coverage form and uploads the report; a coverage gate is
#958.
