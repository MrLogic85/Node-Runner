# Save format

This document owns what Node Runner writes to disk. The code follows it; a
change to a saved shape changes this document and the schemas in
`docs/save-schema/` in the same PR.

## Rules

- **No versioning.** No `schemaVersion`, no migration, no fallbacks for older
  files. While the project is pre-alpha (`docs/ROADMAP.md` → "Project
  stage"), a format change means wiping the old saves.
- **Schemas.** `docs/save-schema/` holds a JSON Schema for each file,
  generated from the domain records by .NET's `JsonSchemaExporter`. It is
  the exact field list: types, which fields must be present, which may be
  `null`, allowed enum values and no other fields. `SaveFormatTests` fails
  when the generated schema differs from the committed one and writes the
  new one to the test output folder.
- **Strict loading.** Loading follows the schema: an unknown field, a missing
  required field or a `null` where the schema allows none fails with a
  `JsonException` that names the field. `SaveJson` gets this from
  System.Text.Json's own options; the app does not run a schema validator.
  Those options don't check list items, so the domain records reject a
  `null` part themselves, and values outside their range (such as
  `trainSettings.shadows` above 100), so narrowing a range is a format
  change. A name the records know but do not save (a
  node's `radius`, #626) is skipped, not rejected.
  One example `creation.json`
  (`tests/NodeRunner.App.Tests/Repositories/SaveExamples/`) checks that a
  real file loads.
- **Domain records are the shape.** The files serialize the records in
  `libs/NodeRunner.Domain/` directly, through `SaveJson`
  (`libs/NodeRunner.App/Repositories/`).
- **Planned fields** are listed here with the issue that adds them. That
  issue adds the field and updates this document and the schema.

## Layout

The root is Godot's `user://` folder.

```
progression.json                 player progress
settings.json                    user settings                     (#379)
creations/<id>/
  creation.json                  the Creation
  history.json                   one row per generation            (#541)
  checkpoints/<checkpoint>/                                        (#256)
    meta.json
    creation.json
    history.json
```

`<id>` is the Creation's id as 32 hex digits without dashes. Deleting a
Creation deletes its folder.

## JSON conventions

- UTF-8, indented, camelCase property names.
- Enums are camelCase strings (`"camera"`); numbers are not accepted.
- Every field is written. An optional value is written as `null`.
- Ids are GUIDs (`"0f3c6a52-7d1e-…"`) for Creations and positive integers
  for parts.
- Lengths and positions are in creature units; angles are in radians.

## `creation.json`

Schema: [`save-schema/creation.schema.json`](save-schema/creation.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `id` | GUID | The Creation's id; also its folder name. |
| `name` | string | Shown on the card and in Build. Not empty. A default name is saved in the player's language (#759). |
| `creature` | object | The drawn body; see below. |
| `training` | object or `null` | `null` until a generation has finished. |
| `trainSettings` | `{ shadows, runLengthSeconds }` or `null` | Train setup's values from its last Start (#617). `null` until then; Train setup and Training use the default (8 shadows, 10 s) until Settings stores one (#379). `shadows` is 2–100, `runLengthSeconds` 5–60. |

`creature`:

| Field | Type | Meaning |
|---|---|---|
| `nodes[]` | `{ id, position: { x, y }, name }` | Joints. `name` is `null` until renamed. A joint's radius follows from its parts and is not saved (#626). |
| `beams[]` | `{ id, nodeA, nodeB, name }` | Beams between two node ids. |
| `sensors[]` | `{ id, beamId, kind, name, aim }` | One sensor per beam. `kind` is `accelerometer` or `camera`. `aim` is the Camera's centre ray from its beam, and `null` for other kinds. |
| `pistons[]` | `{ id, nodeA, nodeB, name, strength, stroke, maxSpeed }` | Pistons between two node ids (#451). `strength` is in world force units (100 per newton), `stroke` a share of its built length (0.3 is ±30%), `maxSpeed` in world units per second (100 per m/s). |
| `nextPartId` | int | The next free part id. Higher than every id in use; removed ids are never reused. |

`training`:

| Field | Type | Meaning |
|---|---|---|
| `brain` | object | The best brain of the latest finished generation, as a graph; see below. Reopening Training breeds the next generation from it (warm start, #538). |
| `generation` | int | Finished generations, at least 1. The latest generation is this one. |
| `latest` | `{ distance, topSpeed, elevation, mapId, frontDistance }` | What `brain`'s run measured in the latest generation. It can go down; the Creations card and Build show it (#479). |
| `best` | `{ generation, distance, mapId, frontDistance }` | The best ever on that map. `generation` and `distance` are the highest-scoring run's; `frontDistance` is the furthest any latest front got on that map. Neither goes down; the Training best marker shows `frontDistance` (#479, #388, #725). |

`distance` is the score; `frontDistance` is the distance shown, `null` in a save
from before #725. See `docs/TRAINING_LOOP.md` → "Latest and best ever".

`mapId` is a map's stable id from `Maps` (#443); Flat is `map-flat`.

Latest and best are explained in `docs/TRAINING_LOOP.md` → "Latest and best
ever".

`training.brain` (#536) is a graph keyed by the creature's brain ports
(`docs/CREATURE_MODEL.md` → "Sensor–model contract"), so it never depends on
list order, and later hidden neurons and structural mutation fit without a
new format. 0.13 brains are direct: one input neuron per input port, one
output neuron per output port, and a connection gene from every input to
every output.

| Field | Type | Meaning |
|---|---|---|
| `neurons[]` | `{ id, kind, partId, channel, layer, bias, activation }` | `kind` is `input`, `output` or `hidden`. An input or output neuron names its port by `partId` and `channel`; a hidden neuron has both `null`. Inputs sit in layer 0 with bias 0 and `identity`; other neurons in layer 1 or later. `activation` is `identity`, `tanh`, `sigmoid` or `relu`; a direct-brain output uses its port's signal activation: `tanh` for velocity and position, `sigmoid` for strength (#535). Resuming training fails loud (`DirectBrain.Compile`) if a saved output's activation does not match its port. |
| `connections[]` | `{ from, to, weight, enabled }` | A connection gene between two neuron ids, from a lower layer to a higher one; at most one per pair. A disabled gene keeps its weight but carries no signal. |
| `nextNeuronId` | int | The next free neuron id. Higher than every id in use; ids are never reused. |

A port with no neuron yet starts silent: its connections compile to 0.
Saving a Build edit refits the brain to the edited creature's ports (#516,
`docs/CREATURE_MODEL.md` → "A rebuild keeps the brain"), so the saved brain
always matches the saved creature.

### Planned for `creation.json`

| Field | Issue |
|---|---|
| `revision`: bumped on every saved rebuild | #541 |
| `training.state`: map-loop position, the elites of other fitness functions | #540, #317 |
| `trainSettings`: checked maps, fitness functions | #540, #317 |
| `training.latest` and `training.best` per map | #540 |
| `created`, `updated` | Added with the first feature that shows them. |

## `history.json` (planned, #541)

One row per finished generation: generation, map id, score per fitness
function, `revision`, run length and training time.

## `checkpoints/<checkpoint>/` (planned, #256)

`meta.json` holds the checkpoint's name, kind (auto or manual), creation time
and generation. The folder also holds copies of `creation.json` and
`history.json`. Restoring copies both back, so history rewinds; newer
checkpoints stay restorable.

## `progression.json`

Schema: [`save-schema/progression.schema.json`](save-schema/progression.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `defaultCreationsSeeded` | bool | The example Creations were copied in on first start. Required. |

Planned: unlocked parts and counts (#525), achievements and brain-ability
unlocks (#544).

## `settings.json` (planned, #379)

UI size, theme, sounds and the Shadows default.

## Never saved

- Ports; they follow from the parts.
- Compiled runtime arrays of the network.
- Memory-cell values (#126).
- Physics state.
- A generation that hasn't finished.

## Writing and reading

- Every file is written to a temporary file next to it and then renamed over
  it, so a crash never leaves half a file.
- A `creation.json` that fails to load is skipped and logged; the other
  Creations still list.
- A `progression.json` that fails to load is renamed to
  `progression.json.corrupt-<time>` and progress starts fresh.
