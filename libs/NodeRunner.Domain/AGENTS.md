# AGENTS.md — `libs/NodeRunner.Domain`

Pure C# data types: the vocabulary of the app, with no behaviour beyond
invariants.

## Rules

- **No I/O.** No `System.IO`, `System.Net` or environment access.
- **Records, enums and constructor invariants only.** The constructor
  validates, so bad data never survives construction; logic lives in the
  layers above.
- **Part physics goes in `libs/NodeRunner.Mechanics`** (#596).
- **Serialisable with `System.Text.Json` without custom converters.** Saved
  records are the save shape: changing one follows `docs/SAVE_FORMAT.md`.

### Exceptions: helpers kept here

Pure, stateless static classes over Domain types, or immutable values
computed from them, kept because a layer that
cannot use Mechanics needs them (`docs/ARCHITECTURE.md`). This is the one
list; move one only with a new issue.

| Type | Why it is here | Issue |
|---|---|---|
| `BrainPorts` | Every part's brain ports, their order and channel keys, shared by the sim, Build and ML | #534 |
| `PortSignals` | Each output's activation and passive start: the brain's half of the port contract (ML) | #535 |
| `SensorDef.DefaultAim` | A new Camera's aim, filled in by `CreatureDef` at construction | #622 |
| `SensorPicture` | The area a tap on a sensor's picture hits, shared by Build and Training | #576 |
| `SelectionMarks` | The selection gap, a joint's halo and touch reach, shared by Build's gestures and the drawing in Build and Training | #710 |
| `MapGround` | A map's ground height at x, part of `MapDef` and read by the arena | #443 |
| `DrawGroups`, `DrawSlot`, `RaisedParts` | The creature's draw order and the parts a selection raises, shared by the drawing and the touches in Build and Training | #1107 |

## What lives here

- `CreatureDef`, `NodeDef`, `BeamDef`, `SensorDef`, `SensorKind`,
  `ServoDef`, `PistonDef`, `SpringDef`, `WheelDef`, `LinkRef` — anatomy
- `CreatureElementKind`, `CreatureElementSelection` — what is selected
- `BrainPort`, `PortDirection`, `PortSignal`, `BrainPortLayout` — brain ports
- `BrainDef`, `NeuronDef`, `ConnectionGeneDef`, `NeuronKind`,
  `NeuronActivation` — the saved brain graph (#536)
- `CreationDef`, `TrainingStateDef`, `TrainingRunDef`, `TrainingBestDef`,
  `TrainSettingsDef` — a saved Creation; `ProgressionDef` — saved progress
- `MapDef`, `FlatGround`, `Maps`, `MapIds` — the maps a creation trains on
- `Vector2D` — our own `readonly record struct`; `Godot.Vector2` stays on
  the Godot side

## Tests

`tests/NodeRunner.Domain.Tests/`: invariant violations throw; JSON
round-trips (`Deserialize(Serialize(x)).ShouldBe(x)`); one record-equality
canary, since the compiler writes equality.
