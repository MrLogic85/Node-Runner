# Save format

This document owns what Node Runner writes to disk. A change to a saved
shape changes this document and the schemas in `docs/save-schema/` in the
same PR.

## Rules

- **Old saves keep loading** as the project stage requires
  (`docs/ROADMAP.md` → "Project stage"); see "Versions and migration".
- **Schemas.** `docs/save-schema/` holds a JSON Schema for each file,
  generated from the domain records by .NET's `JsonSchemaExporter`, plus the
  `formatVersion` field of a versioned file. It is the exact field list:
  types, required fields, which may be `null`, allowed enum values and no
  other fields. `SaveFormatTests` keeps it in step with the records.
- **Strict loading.** An unknown field, a missing required field or a
  wrong `null` fails with a `JsonException` naming the field; `SaveJson`
  gets this from System.Text.Json's own options, not a schema validator.
  Those options don't check list items, so the domain records reject a
  `null` part and out-of-range values (such as `trainSettings.shadows`
  above 100) themselves; narrowing a range is a format change. A name the
  records know but do not save (a node's `radius`, #626) is skipped. One
  example `creation.json` (`tests/NodeRunner.App.Tests/Repositories/SaveExamples/`)
  checks that a real file loads.
- **Domain records are the shape.** The files serialize the records in
  `libs/NodeRunner.Domain/` directly, through `SaveJson`
  (`libs/NodeRunner.App/Repositories/`). The one field outside the records
  is `formatVersion`.

## Layout

The root is Godot's `user://` folder.

```
progression.json                 player progress
creations/<id>/
  creation.json                  the Creation
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
  unchanged. The game's own limits are in `docs/BUILD_MODE.md` → "Names" (#868).

## `creation.json`

Schema: [`save-schema/creation.schema.json`](save-schema/creation.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | int | Written first; see "Versions and migration". |
| `id` | GUID | The Creation's id; also its folder name. |
| `name` | string | Not empty. A default name is saved in the player's language (#759). |
| `creature` | object | The drawn body; see below. |
| `training` | object or `null` | `null` until a generation has finished. |
| `trainSettings` | `{ shadows, runLengthSeconds }` or `null` | Train setup's values from its last Start (#617); `shadows` is 2–100, `runLengthSeconds` 5–60. `null` until the first Start; until then Train setup uses `TrainSettingsDef.Default` (8 shadows, 10 s; #379). |

`creature` (what each part means: `docs/CREATURE_MODEL.md`):

| Field | Type | Meaning |
|---|---|---|
| `nodes[]` | `{ id, position: { x, y }, name }` | Joints. `name` is `null` until renamed. A joint's radius follows from its parts and is not saved (#626). |
| `beams[]` | `{ id, nodeA, nodeB, name }` | Beams between two node ids. |
| `sensors[]` | `{ id, beamId, kind, name, aim }` | One sensor per beam. `kind` is `accelerometer` or `camera`. `aim` is the Camera's centre ray from its beam, and `null` for other kinds. |
| `servos[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, range, start, maxSpeed, riseTime }` | Servo joint motors (#452). `fixedLinkId` and `targetLinkId` name a Beam, Piston or Spring touching `nodeId`, or `null` once that link is deleted (training is then blocked until the player picks one). `strength` in world torque units (N·m × 10⁴), `range` rad, `maxSpeed` rad/s, `start` 0–1, `riseTime` s. |
| `pistons[]` | `{ id, nodeA, nodeB, name, strength, stroke, start, maxSpeed, riseTime }` | Pistons between two node ids (#451). `strength` in world force units (100 per N), `stroke` (0, 1] (#870, #835), `start` 0–1, `maxSpeed` world units/s (100 per m/s), `riseTime` s (#801; missing reads as 0.2). |
| `springs[]` | `{ id, nodeA, nodeB, name, stiffness, damping, stroke, coilLength }` | Springs between two node ids (#453). `stiffness` N/m, `damping` N·s/m (#801; saves from before it damp almost nothing), both the same number in world units; `stroke` (0, 1] and `coilLength` 0…1 (#835), missing reads as a new Spring's 1 and ⅔. |
| `nextPartId` | int | The next free part id. Higher than every id in use; removed ids are never reused. |

`training` (meaning of latest and best: `docs/TRAINING_LOOP.md` → "Latest
and best ever"):

| Field | Type | Meaning |
|---|---|---|
| `brain` | object | The best brain of the latest finished generation, as a graph; see below. Reopening Training breeds the next generation from it (warm start, #538). |
| `generation` | int | Finished generations, at least 1. |
| `latest` | `{ distance, topSpeed, elevation, mapId, frontDistance }` | The latest generation's run of `brain` (#479). |
| `best` | `{ generation, distance, mapId, frontDistance }` | The best ever on that map (#479, #388, #725). |

`distance` is the score; `frontDistance` is the distance shown, `null` in a
save from before #725. `mapId` is a map's stable id from `Maps` (#443);
Flat is `map-flat`.

`training.brain` (#536) is a graph keyed by the creature's brain ports
(`docs/CREATURE_MODEL.md` → "Sensor–model contract"), so it never depends on
list order, and hidden neurons and structural mutation fit without a new
format. A direct brain has one input neuron per input port, one output
neuron per output port, and a connection gene from every input to every
output.

| Field | Type | Meaning |
|---|---|---|
| `neurons[]` | `{ id, kind, partId, channel, layer, bias, activation }` | `kind` is `input`, `output` or `hidden`. An input or output neuron names its port by `partId` and `channel`; a hidden neuron has both `null`. Inputs sit in layer 0 with bias 0 and `identity`; other neurons in layer 1 or later. `activation` is `identity`, `tanh`, `sigmoid` or `relu`; a direct-brain output uses its port's signal activation (#535), and resuming training fails loud (`DirectBrain.Compile`) if it does not match. |
| `connections[]` | `{ from, to, weight, enabled }` | A connection gene between two neuron ids, from a lower layer to a higher one; at most one per pair. A disabled gene keeps its weight but carries no signal. |
| `nextNeuronId` | int | The next free neuron id. Higher than every id in use; ids are never reused. |

A port with no neuron yet starts silent: its connections compile to 0. The
saved brain always matches the saved creature (`docs/CREATURE_MODEL.md` →
"A rebuild keeps the brain"), except for a port a later version adds to a
part, such as the Camera's `hit` (#1032): a brain saved before it has no
neuron for it, so it plays as before, and the next refit adds one. Such a
port needs no migration.

## `progression.json`

Schema: [`save-schema/progression.schema.json`](save-schema/progression.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | int | Written first; see "Versions and migration". |
| `defaultCreationsSeeded` | bool | The example Creations were copied in on first start. Required. |

## Never saved

- Ports; they follow from the parts.
- Compiled runtime arrays of the network.
- Physics state.
- A generation that hasn't finished.

## Versions and migration

The baseline is the 0.13.0 shape: every `creation.json` and
`progression.json` that 0.13.0 writes (#744).

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
  any file; a `SaveMigration` edits the file's JSON one version up before
  the strict load.

Current `creation.json` migrations:

| From | To | Change |
|---|---|---|
| 1 | 2 | Add required `creature.servos: []` to pre-Servo saves. |
| 2 | 3 | #870: a Piston's `stroke` ±s (missing: 0.3) becomes `2s / (1 − s)`, capped at 1, with `start: 0.5`, which keeps its shortest and longest lengths up to ±⅓. Its `length` input went from −1…1 around the built length to 0…1 over its travel, so each enabled connection from it doubles its `weight` and takes the old weight off the target neuron's `bias`: a trained brain drives its Pistons as before. |
| 3 | 4 | #835: each Spring gets `stroke: 1` and `coilLength: 0.55`: stops about a third of its gap either side of its drawn length (0.325 in, 0.35 out, #974), resting free near the middle as before. A Piston's travel now runs on the gap between its joints' edges, which shortens it a little, but its saved shape is unchanged, so Piston saves load without migration. |

### Changing a saved shape

1. Change the record, this document and the schema as usual.
2. Append a `SaveMigration` to the file's list
   (`FileCreationRepository.Format` for `creation.json`,
   `FileProgressionRepository.Format` for `progression.json`). It turns a file
   in the previous version into the new shape; the current version goes up
   by one.
3. Add a test that loads a file in the previous version, and keep the
   0.13.0 fixtures loading.
4. For `creation.json`, add the new version's share dictionary: run the App
   tests with `NODE_RUNNER_UPDATE_SHARE_DICTIONARY=1`, then again to build
   it in (see "Share code").

The real files 0.13.0 wrote are checked in as fixtures in
`tests/NodeRunner.App.Tests/Repositories/SaveExamples/0.13.0/` (the Walker
example, untrained and trained, and `progression.json`), and
`CreationVersioningTests` and `ProgressionVersioningTests` load them.
They are never rewritten in place.

## Share code

Share build in Build's overflow menu copies the creation as one line of
text to paste in a chat; Import creation in Creations' overflow menu reads
it back (#899). `CreationShareCode` (`libs/NodeRunner.App/Repositories/`)
owns it.

- The code is `NR`, the format version and a dot, then `creation.json`
  on one line with `training` and `trainSettings` `null` and the id
  `00000000-0000-0000-0000-000000000001`, zlib-compressed against that
  version's share dictionary, in URL-safe base64 without padding. The
  Walker's is about 70 characters, starting `NR4.`. It holds the build
  only, so the copy trains from scratch.
- A share dictionary is a `creation.json` in its version with every kind
  of part (`libs/NodeRunner.App/Repositories/ShareDictionaries/`), so a
  code only holds where its build differs from it. The code leaves out the
  dictionary's compressed bytes: the dictionary is compressed first and
  sync-flushed so its bytes end on a whole byte, and reading compresses it
  again in front of the code. zlib's checksum covers both and catches a
  changed character. It is only checked when the code is whole: a code cut
  by a few characters at its end still loads if its build is complete,
  which then is unchanged.
- A dictionary never changes once released, or its codes stop reading;
  `ShareDictionaryTests` reads a pinned version-4 code.
- An older code is unpacked with its own version's dictionary and goes
  through the same migrations as a saved file. A code from a newer version
  is refused as newer.
- The code is someone else's text, so reading it is strict and capped:
  white space is ignored, but any other text around it is not; a code over
  32,768 characters, or one that unpacks to over 1 MB, is refused as
  damaged, as is anything the migrations or the strict load fail on. A
  build with no joints is refused.
- Its name is cut to the creation name limit. A build Build could not
  make is refused as damaged: a name blank once cut, a joint outside the
  Build area or a setting off its slider (`CreatureBuilder.IsWithinBuildLimits`).
- The saved copy gets a new id and the name the player gives it on Import
  (`docs/BUILD_MODE.md` → "Names").

## Writing and reading

- Every file is written to a temporary file next to it and then renamed over
  it, so a crash never leaves half a file.
- A `creation.json` that fails to load is skipped and logged; the other
  Creations still list.
- A `progression.json` that fails to load is renamed to
  `progression.json.corrupt-<time>` and progress starts fresh.
