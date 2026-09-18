# AGENTS.md — `libs/NodeRunner.Domain`

Pure C# data types. The vocabulary of the app, no behavior beyond invariants.

## Hard rules

- **No `Godot.*` references.** Enforced by `NodeRunner.Arch.Tests`.
- **No I/O.** No `System.IO`, no `System.Net`, no environment access.
- **No behavior beyond data validation.** Records + enums + constructor
  invariants. Business logic lives elsewhere.
  - **One deliberate exception:** `MotorTopology` is a pure, stateless static
    class that derives how a creature's beams connect and rotate at each
    node (see `docs/CREATURE_MODEL.md`). It is allowed here because both the
    physics layer (`project/src/creature/`) and any future tooling need the
    exact same answer, and Domain is the only layer both can depend on
    without violating `docs/ARCHITECTURE.md`'s layer graph. Any new class
    like it must stay side-effect-free and take/return only Domain types.
- **Serialisable via `System.Text.Json` without custom converters.**

## What lives here

- `CreatureDef`, `NodeDef`, `BeamDef`, `CoreDef`, `NodeConnectionDef` —
  anatomy
- `MotorTopology` — derives `NodeConnectionDef`s from a `CreatureDef` (see
  the exception above)
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
