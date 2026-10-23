# Save format

This document owns what Node Runner writes to disk. A change to a saved
shape changes this document and the schemas in `docs/save-schema/` in the
same PR.

## Rules

- **Old saves keep loading** as "Project stage" requires; see "Versions
  and migration".
- **Schemas.** `docs/save-schema/` holds a JSON Schema for each file,
  generated from the domain records by .NET's `JsonSchemaExporter`. It is
  the exact field list: types, required fields, which may be `null`,
  allowed enum values and no other fields. `SaveFormatTests` keeps it in
  step with the records.
- **Strict loading.** An unknown field, a missing required field or a
  wrong `null` fails with a `JsonException` naming the field; `SaveJson`
  gets this from System.Text.Json's own options, not a schema validator.
  Those options don't check list items, so the domain records reject a
  `null` part and out-of-range values (such as `trainSettings.shadows`
  above 100) themselves; narrowing a range is a format change. A name the
  records know but do not save (a node's `radius`) is skipped. One example
  `creation.json`
  (`tests/NodeRunner.App.Tests/Repositories/SaveExamples/`) checks that a
  real file loads.
- **Domain records are the shape.** The files serialize the records in
  `libs/NodeRunner.Domain/` directly, through `SaveJson`; the one field
  outside them is `formatVersion`.

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
- A number is never `NaN` or infinite, except a Locked Brake's `strength`,
  written as the string `"Infinity"`: JSON has no infinity, and
  .NET's named-literal number handling reads it back as one, on that field
  only.
- Names are the player's free text, saved as JSON strings and never part
  of a path, so any name reads back as typed. The one exception: half of a
  UTF-16 surrogate pair is written as `U+FFFD`.
- The format has no length cap, so any saved name loads unchanged. The
  game's own limits are in `docs/BUILD_MODE.md` → "Names".

## `creation.json`

Schema: [`save-schema/creation.schema.json`](save-schema/creation.schema.json).

| Field | Type | Meaning |
|---|---|---|
| `formatVersion` | int | Written first; see "Versions and migration". |
| `id` | GUID | The Creation's id; also its folder name. |
| `name` | string | Not empty. A default name is saved in the player's language. |
| `creature` | object | The drawn body; see below. |
| `training` | object or `null` | `null` until a generation has finished. |
| `trainSettings` | `{ shadows, runLengthSeconds }` or `null` | Train setup's values from its last Start; `shadows` is 2–100, `runLengthSeconds` 5–60. `null` until the first Start; until then Train setup uses `TrainSettingsDef.Default` (8 shadows, 10 s). |

`creature` (what each part means: `docs/CREATURE_MODEL.md`):

| Field | Type | Meaning |
|---|---|---|
| `nodes[]` | `{ id, position: { x, y }, name }` | Joints. `name` is `null` until renamed. A joint's radius follows from its parts and is not saved. |
| `beams[]` | `{ id, nodeA, nodeB, name }` | Beams between two node ids. |
| `sensors[]` | `{ id, beamId, kind, name, aim, rays, spread, range }` | One sensor per beam. `kind` is `accelerometer` or `camera`. A Camera's `aim` is its centre ray from its beam, `rays` its odd ray count (1, 3 or 5), `spread` the radians between its outer rays and `range` its rays' length in world units; all four are `null` for other kinds. |
| `servos[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, range, start, maxSpeed, riseTime }` | Servo joint motors. `fixedLinkId` and `targetLinkId` name a Beam, Piston or Spring touching `nodeId` or the Wheel on `nodeId`, or `null` once that link or Wheel is deleted (training is then blocked until the player picks one). `strength` in world torque units (N·m × 10⁴), `range` rad, `maxSpeed` rad/s, `start` 0–1, `riseTime` s. |
| `pistons[]` | `{ id, nodeA, nodeB, name, strength, stroke, start, maxSpeed, riseTime }` | Pistons between two node ids. `strength` in world force units (100 per N), `stroke` (0, 2], `start` 0–1, `maxSpeed` world units/s (100 per m/s), `riseTime` s (missing reads as 0.2). |
| `springs[]` | `{ id, nodeA, nodeB, name, stiffness, damping, stroke, coilLength }` | Springs between two node ids. `stiffness` N/m and `damping` N·s/m, both the same number in world units; `stroke` (0, 2] and `coilLength` 0…1, missing reads as a new Spring's 1 and ⅔. |
| `wheels[]` | `{ id, nodeId, name, radius }` | Wheels on a node id. `radius` in world units, 40–100 (0.4–1 m). Its weight follows from `radius` and is not saved. |
| `touchSensors[]` | `{ id, nodeId, name }` | Touch sensors on a node id, at most one per node. No settings. |
| `rubbers[]` | `{ id, nodeId, name }` | Rubbers on a node id, at most one per node: the joint, or its Wheel's tyre, grips the ground. No settings. |
| `velocityMotors[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, maxSpeed, riseTime }` | Motors: a Servo's sides and units, without `range` or `start`. One per node (`docs/CREATURE_MODEL.md` → Node → One part per slot). |
| `brakes[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength }` | Brakes: a Servo's sides and slot, no ports. `strength` in world torque units (N·m × 10⁴), above 0, or `"Infinity"` when Locked. |
| `angleSensors[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name }` | Angle sensors: a Servo's sides and slot, three inputs, no settings. |
| `steppers[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, stepsPerTurn, maxFrequency }` | Steppers: a Servo's sides and slot. `strength` in world torque units (N·m × 10⁴), above 0; `stepsPerTurn` a whole number 2–24; `maxFrequency` steps per second, above 0. |
| `nextPartId` | int | The next free part id. Higher than every id in use; removed ids are never reused. |

`training` (meaning of latest and best: `docs/TRAINING_LOOP.md` → "Latest
and best ever"):

| Field | Type | Meaning |
|---|---|---|
| `network` | object | The best network of the latest finished generation, as a graph; see below. Reopening Training breeds the next generation from it (warm start). |
| `generation` | int | Finished generations, at least 1. |
| `latest` | `{ distance, topSpeed, elevation, mapId, frontDistance }` | The latest generation's run of `network`. |
| `best` | `{ generation, distance, mapId, frontDistance }` | The best ever on that map. |

`distance` is the score; `frontDistance` is the distance shown, or `null`
when the save has none, and then Latest shows its score and the best
marker stays hidden until the next generation is saved. `mapId` is a
map's stable id from `Maps`; Flat is `map-flat`.

`training.network` is a graph keyed by the creature's network ports
(`docs/CREATURE_MODEL.md` → "Sensor–network contract"), so it never depends on
list order, and hidden neurons and structural mutation fit without a new
format. A direct network has one input neuron per input port, one output
neuron per output port, and a connection gene from every input to every
output.

| Field | Type | Meaning |
|---|---|---|
| `neurons[]` | `{ id, kind, partId, channel, layer, bias, activation }` | `kind` is `input`, `output` or `hidden`. An input or output neuron names its port by `partId` and `channel`; a hidden neuron has both `null`. Inputs sit in layer 0 with bias 0 and `identity`; other neurons in layer 1 or later. `activation` is `identity`, `tanh`, `sigmoid` or `relu`; a direct-network output uses its port's signal activation, and resuming training fails loud (`DirectNetwork.Compile`) if it does not match. |
| `connections[]` | `{ from, to, weight, enabled }` | A connection gene between two neuron ids, from a lower layer to a higher one; at most one per pair. A disabled gene keeps its weight but carries no signal. |
| `nextNeuronId` | int | The next free neuron id. Higher than every id in use; ids are never reused. |

A port with no neuron yet starts silent: its connections compile to 0. The
saved network always matches the saved creature (`docs/CREATURE_MODEL.md` →
"A rebuild keeps the network"), except for a port a later version adds to a
part, such as the Camera's `hit`: a network saved before it has no
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

## Project stage

The project stage decides how changes treat data already saved on a
device (Creations, training, progression, settings). `README.md` →
"Status" names the current stage.

| Stage | Saved user data |
| --- | --- |
| **Development with testers** (alpha) | Every Creation and Progression save from 0.13.0 on must load in every later version: a change to their shape migrates older files on load (see "Versions and migration"). Other saved data may still be reset when migrating it is not worth it. |
| **Released** | Everything saved must be migrated. |

## Versions and migration

The baseline is the 0.13.0 shape: every `creation.json` and
`progression.json` that 0.13.0 writes.

- A versioned file starts with `formatVersion`, a whole number. Both files
  have it. The 0.13.0 shape is version 1, and a file without the field is
  version 1.
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
| 2 | 3 | A Piston's `stroke` ±s (missing: 0.3) becomes `2s / (1 − s)`, capped at 1, with `start: 0.5`, which keeps its shortest and longest lengths up to ±⅓. Its `length` input went from −1…1 around the built length to 0…1 over its travel, so each enabled connection from it doubles its `weight` and takes the old weight off the target neuron's `bias`: a trained network drives its Pistons as before. |
| 3 | 4 | Each Spring gets `stroke: 1` and `coilLength: 0.55`: stops about a third of its gap either side of its drawn length (0.325 in, 0.35 out), resting free near the middle as before. A Piston's travel runs on the gap between its joints' edges, which shortened it a little with no change to its saved shape, so Piston saves need no migration. |
| 4 | 5 | Add required `creature.wheels: []`. |
| 5 | 6 | Each Camera gets `rays: 3`, `spread: π/2` and `range: 220`, the fan every Camera had, so it sees and reads as before. |
| 6 | 7 | Add required `creature.touchSensors: []`. |
| 7 | 8 | A Piston's `position` output went from `tanh`, −1…1, to `sigmoid`, 0…1 like its `length` input. As (tanh(x) + 1) / 2 = sigmoid(2x), its `activation` becomes `sigmoid` and its `bias` and the `weight` of every connection into it, enabled or not, double: a trained network drives its Pistons as before. |
| 8 | 9 | Each Wheel drops its `grip`; every tyre grips like a bare joint, friction 1. |
| 9 | 10 | Add required `creature.rubbers: []`. |
| 10 | 11 | Add required `creature.velocityMotors: []`. |
| 11 | 12 | Add required `creature.brakes: []`. |
| 12 | 13 | Add required `creature.angleSensors: []`. |
| 13 | 14 | Add required `creature.steppers: []`. |
| 14 | 15 | A Stepper's `power` output neuron becomes its `frequency` output; its old `frequency` input and output neurons and their connections are dropped. |
| 15 | 16 | `training.brain` becomes `training.network`, unchanged. A file without training has nothing to move; one with both keys fails to load. |

Two changes of meaning need no migration, as no migration could keep the
old meaning:

- **Links end at the hub, and sensors sit by what covers the joint**
  (`docs/CREATURE_MODEL.md` → Node, Sensor): the saved shape is the same, but a saved creature with a
  Wheel or Servo moves and senses a little differently.
- **The centre is the average of the joints** (`docs/TRAINING_LOOP.md` →
  Trial): a saved `training.best.distance` measured from the average of
  the beams' midpoints is compared as-is with new scores. The two agree
  for a creature that moves without turning or changing shape and differ
  a little for one that does.

### Changing a saved shape

A change to the shape of a file the project stage protects adds a migration,
so the current version goes up by one and older files load in the new shape,
with a test that loads a file in the previous version. A change that keeps
the shape but changes what a saved value means is migrated too when a
migration can keep the old meaning; otherwise "Versions and migration"
records why it needs none. The `change-saved-shape` skill has the steps.

The real files 0.13.0 wrote are checked in as fixtures in
`tests/NodeRunner.App.Tests/Repositories/SaveExamples/0.13.0/` (the Walker
example, untrained and trained, and `progression.json`), and
`CreationVersioningTests` and `ProgressionVersioningTests` load them.
They are never rewritten in place.

### Pinned saves

`EveryPartPinTests` builds one creature that holds every part kind (both
Sensor kinds, Cameras with 1, 3 and 5 rays, a free and a Locked Brake, and so
on). It pins three things about that creature in
`tests/NodeRunner.App.Tests/Repositories/Pins/`:

- `every-part.creation.json`: its `creation.json`, byte for byte apart from
  line endings;
- `every-part.ports.txt`: its network ports (`NetworkPorts.Of`) in order,
  each with its direction, part id, channel, signal and
  `NetworkPortLabels` text;
- `every-part.share-code.txt`: its share code, which must read back as the
  same build.

A refactor must leave all three unchanged. When the change is deliberate
(a saved shape, a port, a label or a new part kind, which the test asks to
be added to the creature), rewrite the pins with
`NODE_RUNNER_UPDATE_PINS=1 dotnet test tests/NodeRunner.App.Tests --filter EveryPartPinTests`.
Commit the new pins in the same PR, so the review shows what changed.

## Share code

Share build in Build's overflow menu copies the creation as one line of
text to paste in a chat; Import creation in Creations' overflow menu reads
it back. `CreationShareCode` (`libs/NodeRunner.App/Repositories/`)
owns it.

- The code is `NR`, the format version and a dot, then its JSON
  zlib-compressed against that version's share dictionary, in URL-safe
  base64 without padding. It holds the build only, so the copy trains from
  scratch. The Walker example's is about 50 characters, starting `NR16.`,
  and so is two joints and a Spring's.
- Its JSON is `creation.json` on one line with `training` and
  `trainSettings` `null` and the id `00000000-0000-0000-0000-000000000001`,
  made short:
  - **Rounded.** Every joint is on whole world units, as Build places them
    (`CreatureBuilder.OnWholeUnits`), and every other setting is kept to 3
    decimals when its slider still allows the result.
  - **Renumbered.** Part ids become 1, 2, 3 … in the order of the old ids,
    which keeps the network's ports in their order (`NetworkPorts`). A part
    leaves out its id when it is one more than the one before it in its
    list, and the creature leaves out `nextPartId`, one more than its
    largest id.
  - **Without defaults.** A field equal to the dictionary's template, or to
    it rounded as above, is left out, and so is an empty list. The nth part
    in a list is compared with the template's nth in that list, or its
    first past its last.
  Reading puts back what was left out, from the dictionary that made the
  code, so a later change to Build's defaults does not change old codes.
- A share dictionary (`libs/NodeRunner.App/Repositories/ShareDictionaries/`)
  has two lines: the template, a `creation.json` in its version with every
  kind of part at Build's settings, and a varied creation as a code holds
  it, so a code finds its own shape in the dictionary.
  `ShareDictionaryTests` makes a new version's dictionary. The dictionary
  is compressed in front of the code and its bytes left out, so zlib's
  checksum covers both and catches a changed character; a code cut short
  at its end still loads if its build is complete.
- A dictionary never changes once released, or its codes stop reading;
  `ShareDictionaryTests` reads a pinned version-6 code, the first version
  with share codes.
- An older code is unpacked with its own version's dictionary and goes
  through the same migrations as a saved file. Import then warns that it
  may differ from the shared creation. A code from a newer version is
  refused as newer.
- The code is someone else's text, so reading it is strict and capped:
  white space is ignored, but any other text around it is not; a code over
  32,768 characters, one that unpacks to over 1 MB, or one whose parts
  fill out to over 1 MB more, is refused as damaged, as is anything the
  migrations or the strict load fail on. A build with no joints is
  refused.
- Its name is cut to the creation name limit. A build Build could not
  make is refused as damaged: a name blank once cut, a joint outside the
  Build area or a setting off its slider
  (`CreatureBuilder.IsWithinBuildLimits`).
- The saved copy gets a new id and the name the player gives it on Import
  (`docs/BUILD_MODE.md` → "Names").

## Writing and reading

- Every file is written to a temporary file next to it and then renamed over
  it, so a crash never leaves half a file.
- A `creation.json` that fails to load is skipped and logged; the other
  Creations still list.
- A `progression.json` that fails to load is renamed to
  `progression.json.corrupt-<time>` and progress starts fresh.
