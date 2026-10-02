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
    `Accelerometer` is the second: the proof-mass step, reading and sensor
    frame, shared by the sim and the visual (#576) and unit-tested here.
    `CameraRays` is the third: the camera's as-built ray targets and
    reading, shared by the sim and the sensor picture.
    `SensorPicture` is the fourth: the area a tap on a sensor's picture
    hits, shared by Build's canvas and gestures and by the
    creature in Training (#576).
    `BrainPorts` and `JointMotor` are the fifth and sixth: the brain ports
    every part declares and their order, and a joint motor's port values
    (#534), shared by the sim, Build and the brain.
- **Serialisable via `System.Text.Json` without custom converters.** Saved
  records are the save shape: changing one follows `docs/SAVE_FORMAT.md`.

## What lives here

- `CreatureDef`, `NodeDef`, `BeamDef`, `SensorDef`, `SensorKind`,
  `NodeConnectionDef` — anatomy
- `Accelerometer`, `ProofMass` — the accelerometer's pure math (see the
  exception above)
- `CameraRays` — the camera's pure math (see the exception above)
- `SensorPicture` — a sensor picture's tap area (see the exception above)
- `MotorTopology` — derives `NodeConnectionDef`s from a `CreatureDef` (see
  the exception above)
- `BrainPort`, `PortDirection`, `BrainPortLayout`, `BrainPorts`,
  `JointMotor` — brain ports and their order (see the exception above)
- `BrainDef`, `NeuronDef`, `ConnectionGeneDef`, `NeuronKind`,
  `NeuronActivation` — the saved brain graph (#536)
- `CreationDef`, `TrainingStateDef`, `TrainingRunDef` — a saved Creation
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
