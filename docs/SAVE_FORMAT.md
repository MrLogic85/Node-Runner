# Save format

This document owns what Node Runner writes to disk. The code follows it; a
change to a saved shape changes this document, and the golden files, in the
same PR.

## Rules

- **No versioning.** No `schemaVersion`, no migration, no fallbacks for older
  files. While the project is pre-alpha (`docs/ROADMAP.md` → "Project
  stage"), a format change means wiping the old saves.
- **Strict loading.** A field this document doesn't list fails the load with
  a `JsonException` that names it. A missing required part (`brainShape`,
  `training.bestRun`, `progression.json`'s flag) fails too.
- **Golden files.** `tests/NodeRunner.App.Tests/Repositories/Golden/` holds
  one example of each file. `SaveFormatTests` fails when the written shape
  differs from it, and checks that the golden file loads and saves unchanged.
- **Domain records are the shape.** The files serialize the records in
  `libs/NodeRunner.Domain/` directly, through `SaveJson`
  (`libs/NodeRunner.App/Repositories/`).
- **Planned fields** are listed here with the issue that adds them. That
  issue adds the field, updates this document and the golden file.

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

| Field | Type | Meaning |
|---|---|---|
| `id` | GUID | The Creation's id; also its folder name. |
| `name` | string | Shown on the card and in Build. Not empty. |
| `creature` | object | The drawn body; see below. |
| `brainShape.hiddenLayers` | int 1–3 | Hidden layers chosen before training. |
| `brainShape.neuronsPerLayer` | int 1–100 | Neurons in each hidden layer. |
| `training` | object or `null` | `null` until a generation has finished. |

`creature`:

| Field | Type | Meaning |
|---|---|---|
| `nodes[]` | `{ id, position: { x, y }, radius, name }` | Joints. `name` is `null` until renamed. |
| `beams[]` | `{ id, nodeA, nodeB, name }` | Beams between two node ids. |
| `sensors[]` | `{ id, beamId, kind, name, aim }` | One sensor per beam. `kind` is `accelerometer` or `camera`. `aim` is the Camera's centre ray from its beam, and `null` for other kinds. |
| `nextPartId` | int | The next free part id. Higher than every id in use; removed ids are never reused. |

`training`:

| Field | Type | Meaning |
|---|---|---|
| `layerSizes` | int[] | Input, hidden and output layer sizes of the trained network. |
| `bestGenome` | double[] | The best network's weights and biases, laid out as in `docs/ARCHITECTURE.md` → "Neural-network genome layout". |
| `generation` | int | Finished generations. |
| `activation` | string | Hidden-layer activation, e.g. `Tanh`. |
| `bestFitness` | double | The GA's score for `bestGenome`. |
| `bestRun` | `{ distance, topSpeed, elevation, mapId }` | What the best run measured; the Creations card shows it (`docs/TRAINING_LOOP.md`). |

### Planned for `creation.json`

| Field | Issue |
|---|---|
| `revision`: bumped on every saved rebuild | #541 |
| Per-part `locked` flag | #371 |
| `brain`: neurons with ids and ports, connection genes; replaces `layerSizes`, `bestGenome` and `activation`. A neuron id counter joins `nextPartId`. | #536 |
| `training.state`: map-loop position, mutation strength, the elites of every fitness function | #538, #540, #317 |
| `training.settings`: Shadows, checked maps, fitness functions | #528, #540, #317 |
| `training.best`: best result per map, replacing `bestRun` | #540 |
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
