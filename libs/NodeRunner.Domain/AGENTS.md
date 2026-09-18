# AGENTS.md — `libs/NodeRunner.Domain`

Pure C# data types. The vocabulary of the app, no behavior beyond invariants.

## Hard rules

- **No `Godot.*` references.** Enforced by `NodeRunner.Arch.Tests`.
- **No I/O.** No `System.IO`, no `System.Net`, no environment access.
- **No behavior beyond data validation.** Records + enums + constructor
  invariants. Business logic lives elsewhere.
- **Serialisable via `System.Text.Json` without custom converters.**

## What lives here

- `CreatureDef`, `JointDef`, `BoneDef`, `MuscleDef` — anatomy
- `SimulationConfig`, `GaConfig` — hyperparameters
- `Vector2D` — our own `readonly record struct` (Godot.Vector2 stays on the
  Godot side)
- Enums: `Activation`, `SelectionStrategy`, …

## Style

- `sealed record` for value types with named fields
- `readonly record struct` for tiny high-frequency values
- Constructor validates invariants — bad data must not survive construction
- One concept per file

## Tests

`tests/NodeRunner.Domain.Tests/` — xUnit + Shouldly. Cover:
- Invariant violations throw
- JSON round-trip: `Deserialize(Serialize(x)).ShouldBe(x)`
- Record equality holds (compiler gift; one canary test is enough)
