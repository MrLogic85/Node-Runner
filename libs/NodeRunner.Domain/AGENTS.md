# AGENTS.md — `libs/NodeRunner.Domain`

Pure C# data types. The vocabulary of the app, no behavior beyond invariants.

## Hard rules

- **No `Godot.*` references.** Enforced by `NodeRunner.Arch.Tests`.
- **No I/O.** No `System.IO`, no `System.Net`, no environment access.
- **No behavior beyond data validation.** Records + enums + constructor
  invariants. Business logic lives elsewhere.
  - **Deliberate exceptions:** a few pure, stateless static classes that
    take and return only Domain types. This is the one list of them.
    These stay because a layer that cannot use `NodeRunner.Mechanics`
    needs them (`docs/ARCHITECTURE.md`):
    `BrainPorts` — the brain ports every part declares, their order and
    channel keys (#534), shared by the sim, Build and the brain (ML).
    `PortSignals` — each output signal's activation and passive start
    (#535), the brain's half of the port contract (ML).
    `SensorDef.DefaultAim` — a new Camera's aim, which `CreatureDef` fills
    in at construction (#622).
    These are not part physics and stayed when the physics moved out
    (owner's scope decision on #596); move them only with a new issue:
    `SensorPicture` — the area a tap on a sensor's picture hits, shared by
    Build's canvas and gestures and the creature in Training (#576).
    `SelectionMarks` — the one selection gap, a joint's halo and touch
    reach (#710), shared by Build's gestures and the drawing in Build and
    Training.
    `MapGround` — a map's ground height at x (#443), shared by the arena
    and, later, map previews.
  - **Part physics does not live here.** The sums a part runs each step
    (accelerometer, camera rays, piston, spring, rigid triangles) live in
    `libs/NodeRunner.Mechanics` (#596). Put new physics there.
- **Serialisable via `System.Text.Json` without custom converters.** Saved
  records are the save shape: changing one follows `docs/SAVE_FORMAT.md`.

## What lives here

- `CreatureDef`, `NodeDef`, `BeamDef`, `SensorDef`, `SensorKind`,
  `PistonDef`, `SpringDef` — anatomy
- `SensorPicture` — a sensor picture's tap area (see the exceptions above)
- `SelectionMarks` — the selection gap, joint halo and touch reach (see the
  exceptions above)
- `BrainPort`, `PortDirection`, `PortSignal`, `BrainPortLayout`,
  `BrainPorts`, `PortSignals` — brain ports, their order, channel keys and
  the brain's output conventions (see the exceptions above)
- `BrainDef`, `NeuronDef`, `ConnectionGeneDef`, `NeuronKind`,
  `NeuronActivation` — the saved brain graph (#536)
- `CreationDef`, `TrainingStateDef`, `TrainingRunDef`, `TrainingBestDef`,
  `TrainSettingsDef` — a saved Creation
- `MapDef`, `MapGround`, `FlatGround`, `Maps`, `MapIds` — the maps a
  creation trains on (#443)
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
