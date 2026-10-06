# Save format

This document owns what Node Runner writes to disk. The code follows it; a
change to a saved shape changes this document and the schemas in
`docs/save-schema/` in the same PR.

## Rules

- **Old saves keep loading.** The project is in alpha (`docs/ROADMAP.md` →
  "Project stage"): every `creation.json` and `progression.json` written by
  0.13.0 or later must load in every later version. See "Versions and
  migration" below.
- **Schemas.** `docs/save-schema/` holds a JSON Schema for each file,
  generated from the domain records by .NET's `JsonSchemaExporter`, plus
  the `formatVersion` field of a versioned file. It is
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
  (`libs/NodeRunner.App/Repositories/`). The one field outside the records
  is `formatVersion` (see "Versions and migration").
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
- Names are the player's free text, saved as JSON strings and never part
  of a path, so any name reads back as typed. The one exception: half of a
  UTF-16 surrogate pair is written as `U+FFFD` (#837).
- The format has no length cap, so a longer name from an older save loads
  unchanged. The game's own limits are in `docs/UI_DIRECTION.md`
  "Name length" (#868).

## `creation.json`

Schema: [`save-schema/creation.schema.json`](save-schema/creation.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | int | The file's format version, written first; see "Versions and migration". Missing in 0.13.0 files. |
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
| `servos[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, range, start, maxSpeed, riseTime }` | Servo joint motors (#452). `fixedLinkId` and `targetLinkId` may reference a Beam, Piston or Spring touching `nodeId`, and are nullable so deleting a held link keeps the Servo but blocks training until the player picks a replacement. `strength` is world torque units (N·m × 10⁴), `range` and `maxSpeed` are radians/radians per second, `start` is 0–1, and `riseTime` is seconds. Required since format version 2; version 1 saves are migrated with an empty list. |
| `pistons[]` | `{ id, nodeA, nodeB, name, strength, stroke, start, maxSpeed, riseTime }` | Pistons between two node ids (#451). `strength` is in world force units (100 per newton), `stroke` how much the gap between its joints' edges can grow as a share of its shortest (0 < stroke ≤ 1; 1 doubles it, #870, #835), `start` where its drawn length sits in that travel (0 shortest … 1 longest, #870), `maxSpeed` in world units per second (100 per m/s), `riseTime` in seconds (#801; missing in older saves, read as 0.2). |
| `springs[]` | `{ id, nodeA, nodeB, name, stiffness, damping, stroke, coilLength }` | Springs between two node ids (#453). `stiffness` is in N/m (the same number in world units), `damping` a coefficient in N·s/m, the same number in world units (#801; before it was a share of critical damping, so older saves damp almost nothing). `stroke` is in (0, 1], as a Piston's, and `coilLength` in 0…1, where its rest length sits from half its drawn gap, between its joints' edges, short of its shortest stop to as far past its longest (#835); missing, they take a new Spring's 1 and ⅔. Version 3 saves are migrated (see below). |
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
| `formatVersion` | int | The file's format version, written first; see "Versions and migration". Missing in 0.13.0 files. |
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

## Versions and migration

The baseline is the 0.13.0 shape: every `creation.json` and
`progression.json` that 0.13.0 writes, documented above (#744).

- A versioned file starts with `formatVersion`, a whole number. The 0.13.0
  shape is version 1, and a file without the field is version 1.
  Both files have it (#872, #873).
- Loading reads the version, runs the migrations from that version to the
  current one in order, then loads the result strictly as above.
- A file without the field or in an older version is written back in the
  current version right away, when it is loaded. The write is skipped if
  the file changed after it was read.
- A file newer than the app, one whose `formatVersion` is not a version the
  app knows, or one whose migration fails, is handled as a file that fails
  to load (see "Writing and reading") and is never overwritten.
- `VersionedSaveFile` (`libs/NodeRunner.App/Repositories/`) does this for
  any file. A `SaveMigration` edits the file's JSON one version up, so it
  can rename, move or fill in fields before the strict load. It throws
  `InvalidDataException` for a file it can't change; any other exception
  is a bug and is not caught.

Current `creation.json` migrations:

| From | To | Change |
|---|---|---|
| 1 | 2 | Add required `creature.servos: []` to pre-Servo saves. |
| 2 | 3 | #870: a Piston's `stroke` was ±s of its built length (missing: 0.3). It becomes `2s / (1 − s)` with `start: 0.5`, which keeps its shortest and longest lengths. A Piston can now at most double, so a stroke above ±⅓ becomes 1 (about ±33%). Its `length` input went from −1…1 around the built length to 0…1 over its travel, so each brain connection from it doubles its `weight` and takes the old weight off the `bias` of the neuron it feeds (an enabled connection only): a trained brain drives its Pistons as before, exactly while their stroke was within ±⅓. Since #835 a Piston's travel is on the gap between its joints' edges, which shortens it a little; the saved shape did not change, so saves load as they were (owner decision). |
| 3 | 4 | #835: a Spring had no travel. Each one gets `stroke: 1` and `coilLength: 0.5`, so it has stops a quarter of its gap between its joints' edges either side of its drawn length, rests in the middle of them, and is free there as before. |

### Changing a saved shape

1. Change the record, this document and the schema as usual.
2. Append a `SaveMigration` to the file's list
   (`FileCreationRepository.Format` for `creation.json`,
   `FileProgressionRepository.Format` for `progression.json`). It turns a file
   in the previous version into the new shape; the current version goes up
   by one.
3. Add a test that loads a file in the previous version, and keep the
   0.13.0 fixtures loading.

The real files 0.13.0 wrote are checked in as fixtures in
`tests/NodeRunner.App.Tests/Repositories/SaveExamples/0.13.0/` (the Walker
example, untrained and trained, and `progression.json`), and
`CreationVersioningTests` and `ProgressionVersioningTests` load them.
They are never rewritten in place.

## Writing and reading

- Every file is written to a temporary file next to it and then renamed over
  it, so a crash never leaves half a file.
- A `creation.json` that fails to load is skipped and logged; the other
  Creations still list.
- A `progression.json` that fails to load is renamed to
  `progression.json.corrupt-<time>` and progress starts fresh.
