# Test Strategy

How each layer of Node Runner is tested, with what tools, and why. Model
inspired by `kappuccino`'s `docs/TEST_STRATEGY.md`.

## Guiding principles

1. **Fast feedback.** The whole `dotnet test NodeRunner.slnx` run should
   finish in seconds, not minutes. Anything slow is a smell.
2. **Deterministic.** Every random source is a seeded `Random`. Never
   `DateTime.Now`.
3. **Layer boundaries are code.** Architecture rules live in
   `NodeRunner.Arch.Tests`, not in a wiki.
4. **Test the risky bits, not the trivia.** Compiler-checked things
   (record equality, enum switches) get one canary test at most.
5. **Fakes are production code.** In-memory implementations of repositories
   live next to the interfaces they satisfy, not in a test folder.

## Layer / tool / owner table

| Layer | Lives in | Runtime | Tooling | What's tested |
|---|---|---|---|---|
| Domain | `libs/NodeRunner.Domain/` | net8.0 | xUnit + Shouldly | Invariants, JSON round-trip, record semantics |
| ML | `libs/NodeRunner.ML/` | net8.0 | xUnit + Shouldly | Forward pass, GA math, backprop, activation math |
| App | `libs/NodeRunner.App/` | net8.0 | xUnit + Shouldly + NSubstitute | View-models, repositories, service contracts |
| Architecture | (all libs) | net10.0 tests | xUnit + NetArchTest | No Godot leaks, correct layer graph |
| Godot Nodes | `project/src/{creature,sim,managers,ui}/` | Godot runtime | **GdUnit4** (deferred, v1.0+) | Node lifecycle, physics scenarios |
| End-to-end | full app on device | Android | Manual, per-issue decision | Feel, latency, battery |

Tests target `net10.0` (only runtime installed locally). Libs target `net8.0`
(Godot's runtime). This works because `net10.0` can load `net8.0` assemblies.

## Tools — locked-in choices

| Tool | Version | Purpose |
|---|---|---|
| xUnit | 2.9 | Test framework |
| Shouldly | 4.2 | Assertions (`x.ShouldBe(...)`, `x.ShouldContain(...)`) |
| NSubstitute | 5.1 | Mocking (`Substitute.For<IFoo>()`) |
| NetArchTest.Rules | 1.3 | Architecture assertions |
| coverlet.collector | 6.0 | Coverage on every `dotnet test` |
| Microsoft.NET.Test.Sdk | 17.11 | Test host |

Versions are pinned via central package management in
`Directory.Packages.props`. To bump one, open an issue.

Global usings for `Xunit`, `Shouldly` are declared in each test csproj.

## Per-project focus

### `NodeRunner.Domain.Tests`

Cover:
- Constructor invariants (bad data throws with a helpful message)
- JSON round-trip: `Deserialize(Serialize(x)).ShouldBe(x)`
- Record equality holds (one canary test is enough — the compiler owns the
  rest)

Do **not** test getters/setters, `==`/`!=`, `.Equals` overrides on records.
The compiler synthesises them.

### `NodeRunner.ML.Tests`

Cover:
- Output shape correctness for a given `LayerSizes`
- Deterministic-with-same-seed: `nn1.Forward(x)` == `nn2.Forward(x)` when
  both were seeded identically
- Activation functions on hand-worked values (tanh(0) = 0, sigmoid(0) = 0.5)
- GA operators: tournament selection picks the fittest with size = pop,
  crossover mixes both parents, mutation stays within sigma bounds
  statistically
- Genome round-trip: `FromGenome(nn.LayerSizes, nn.FlattenGenome(), act)`
  produces an equivalent network
- Clone independence: mutating a clone doesn't touch the original

Target: >90% coverage on this project once it stabilises. This is the ML
engine. It has to be right.

### `NodeRunner.App.Tests`

Cover:
- View-model property-change notifications fire in the expected order
- Repositories: `FileCreatureRepository` against a temp directory
  (`Path.GetTempPath()` + `IDisposable` cleanup); `InMemoryCreatureRepository`
  as a lightweight fake
- Services: contract tests for each `IService` interface. Substitute
  dependencies with NSubstitute.
- No async-void, no swallowed exceptions

### `NodeRunner.Arch.Tests`

Executable rules. If someone adds `using Godot;` to `libs/NodeRunner.ML/`,
CI fails.

Current facts (see `ArchitectureSpec.cs`):
- No lib references `Godot.*`
- `NodeRunner.Domain` references nothing but the BCL
- `NodeRunner.ML` references only `NodeRunner.Domain`
- `NodeRunner.App` references only `NodeRunner.Domain` and `NodeRunner.ML`

Add a fact whenever a convention emerges that we've decided to enforce.

### Godot-side tests (deferred)

When we need to test a `Creature` node's physics response or a `Screen`'s
input handling, we add **GdUnit4** and put tests under `project/tests/`.
Separate runner (Godot editor invokes it), separate lifecycle. Don't force it
into `NodeRunner.slnx`.

We're deferring this to at least v1.0 — pre-v1, physics and UI are verified
manually when the issue/change needs it. `docs/MANUAL_TESTING.md` owns that
decision process and how agents can run or defer manual checks.

## Test file layout

Mirror the production layout inside each test project:

```
libs/NodeRunner.ML/
├── NeuralNetwork.cs
├── Activation.cs
└── Ga/
    └── TournamentSelection.cs

tests/NodeRunner.ML.Tests/
├── NeuralNetworkTests.cs
├── ActivationTests.cs
└── Ga/
    └── TournamentSelectionTests.cs
```

One test class per production class. Test method names describe behaviour:

```csharp
[Fact]
public void Forward_WithZeroInput_ReturnsZeroesForTanh() { ... }

[Fact]
public void FromGenome_WithSizeMismatch_Throws() { ... }
```

AAA layout inside each test:

```csharp
[Fact]
public void Forward_WithZeroInput_ReturnsZeroesForTanh()
{
    var nn = new NeuralNetwork(new[] { 2, 3, 1 }, Activation.Tanh, seed: 0);
    var input = new double[2];

    var output = nn.Forward(input);

    output.Length.ShouldBe(1);
    output[0].ShouldBe(0, tolerance: 1e-9);
}
```

## What we do NOT test

- Compiler-generated record members (`Equals`, `GetHashCode`, `ToString`)
- Trivial getters/setters
- .NET BCL behaviour
- Godot engine behaviour (that's Godot's job)
- UI layout down to the pixel

## Running the suite

```bash
dotnet test NodeRunner.slnx                                # all tests
dotnet test tests/NodeRunner.ML.Tests/NodeRunner.ML.Tests.csproj   # one project
dotnet test NodeRunner.slnx --collect:"XPlat Code Coverage"       # + coverage
```

CI runs the last form on every PR and every push to `main`. Coverage
artefacts are uploaded but not (yet) gated on a percentage — that comes when
the codebase has enough surface to make a threshold meaningful.
