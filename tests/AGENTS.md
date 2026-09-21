# AGENTS.md — `tests/`

CLI xUnit tests. Run with `dotnet test NodeRunner.slnx`. Library test projects
remain pure C#. `NodeRunner.Ui.Tests` references Godot types only for static UI
contracts and must not construct nodes or claim runtime rendering coverage.

For the Godot-side tests (Node behaviour, physics, UI), see the eventual
`project/tests/` (GdUnit4) — separate framework, separate lifecycle.

## Tooling

| Concern | Tool |
|---|---|
| Test framework | **xUnit 2.9** |
| Assertions | **Shouldly** (Apache 2.0) — `x.ShouldBe(...)`, `x.ShouldContain(...)` |
| Mocking | **NSubstitute 5** — `Substitute.For<IFoo>()` |
| Architecture rules | **NetArchTest.Rules** + reflection over `AssemblyMarker` |
| Coverage | **Coverlet** — collected on every `dotnet test` run |

Global usings for `Xunit`, `Shouldly` (and `NSubstitute` where relevant) are
declared in each `*.Tests.csproj`. Do not import them per file.

## Projects

| Project | Tests what |
|---|---|
| `NodeRunner.Domain.Tests` | Records, enums, invariants, JSON round-trip |
| `NodeRunner.ML.Tests` | NN math, GA, backprop — the pure engine |
| `NodeRunner.App.Tests` | View-models, repositories, service abstractions |
| `NodeRunner.Arch.Tests` | Layer/dependency rules from `docs/ARCHITECTURE.md` |
| `NodeRunner.Ui.Tests` | Static Godot UI contracts that do not require a scene tree |

## Rules

- **Fast.** Whole suite runs in seconds. No `Thread.Sleep`, no real disk I/O
  outside a per-test temp directory.
- **Deterministic.** Seed all RNG. Never use wall-clock time.
- **One test class per production class.** `NeuralNetworkTests.cs` sits under
  `NodeRunner.ML.Tests/` and mirrors the production layout.
- **AAA layout.** Arrange / blank line / Act / blank line / Assert.
- **Test names describe behaviour.**
  `Forward_WithZeroInput_ReturnsZeroesForTanh()` — not `Test1()`.
- **Fakes live next to interfaces, not in tests.** `InMemoryCreatureRepository`
  in `libs/NodeRunner.App/Repositories/` is production code that also serves
  as a test double. That's fine.

## What we test where

- **Business rules & math** → the lib's own test project. Bulk of test mass.
- **Layer rules** → `NodeRunner.Arch.Tests/ArchitectureSpec.cs`. Add a new
  fact whenever a convention emerges that the compiler can't enforce.
- **Static Godot UI contracts** → `NodeRunner.Ui.Tests`; tests may inspect
  token/style data but must not instantiate Nodes or require a scene tree.
- **Godot Node behaviour** → not here. Add GdUnit4 tests in `project/tests/`
  when lifecycle/input/physics coverage is required (deferred until v1.0-ish).

## Coverage philosophy

We don't chase a coverage percentage. We do enforce that every non-obvious
algorithm has at least one test pinning it down. As libraries stabilise, add
a per-project floor in CI (target: 90% on `NodeRunner.ML`, matches
kappuccino's `:core` gate).
