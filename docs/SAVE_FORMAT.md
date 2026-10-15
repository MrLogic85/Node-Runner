# Save format

This document owns what Node Runner writes to disk. A change to a saved
shape changes this document and the schemas in `docs/save-schema/` in the
same PR.

## Rules

- **Old saves keep loading** as the project stage requires
  (`docs/ROADMAP.md` → "Project stage"); see "Versions and migration".
- **Schemas.** `docs/save-schema/` holds a JSON Schema for each file,
  generated from the domain records by .NET's `JsonSchemaExporter`, plus the
  `formatVersion` field of a versioned file, with a Brake's `strength`
  narrowed to a positive number or `"Infinity"`. It is the exact field list:
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
- A number is never `NaN` or infinite, except a Locked Brake's `strength`,
  written as the string `"Infinity"` (#1039): JSON has no infinity, and
  .NET's named-literal number handling reads it back as one, on that field
  only.
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
| `sensors[]` | `{ id, beamId, kind, name, aim, rays, spread, range }` | One sensor per beam. `kind` is `accelerometer` or `camera`. A Camera's `aim` is its centre ray from its beam, `rays` its odd ray count (1, 3 or 5), `spread` the radians between its outer rays and `range` its rays' length in world units (#578); all four are `null` for other kinds. |
| `servos[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, range, start, maxSpeed, riseTime }` | Servo joint motors (#452). `fixedLinkId` and `targetLinkId` name a Beam, Piston or Spring touching `nodeId` or the Wheel on `nodeId` (#1118), or `null` once that link or Wheel is deleted (training is then blocked until the player picks one). `strength` in world torque units (N·m × 10⁴), `range` rad, `maxSpeed` rad/s, `start` 0–1, `riseTime` s. |
| `pistons[]` | `{ id, nodeA, nodeB, name, strength, stroke, start, maxSpeed, riseTime }` | Pistons between two node ids (#451). `strength` in world force units (100 per N), `stroke` (0, 2] (#870, #835, #1211), `start` 0–1, `maxSpeed` world units/s (100 per m/s), `riseTime` s (#801; missing reads as 0.2). |
| `springs[]` | `{ id, nodeA, nodeB, name, stiffness, damping, stroke, coilLength }` | Springs between two node ids (#453). `stiffness` N/m, `damping` N·s/m (#801; saves from before it damp almost nothing), both the same number in world units; `stroke` (0, 2] (#1211) and `coilLength` 0…1 (#835), missing reads as a new Spring's 1 and ⅔. |
| `wheels[]` | `{ id, nodeId, name, radius }` | Wheels on a node id (#129). `radius` in world units, 40–100 (0.4–1 m). Its weight follows from `radius` and is not saved. |
| `touchSensors[]` | `{ id, nodeId, name }` | Touch sensors on a node id (#1043), at most one per node. No settings. |
| `rubbers[]` | `{ id, nodeId, name }` | Rubbers on a node id (#1142), at most one per node: the joint, or its Wheel's tyre, grips the ground. No settings. |
| `velocityMotors[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, maxSpeed, riseTime }` | Motors (#454): a Servo's sides and units, without `range` or `start`. One per node (`docs/CREATURE_MODEL.md` → Node → One part per slot). |
| `brakes[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength }` | Brakes (#1039): a Servo's sides and slot, no ports. `strength` in world torque units (N·m × 10⁴), above 0, or `"Infinity"` when Locked. |
| `angleSensors[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name }` | Angle sensors (#1204): a Servo's sides and slot, three inputs, no settings. |
| `steppers[]` | `{ id, nodeId, fixedLinkId, targetLinkId, name, strength, stepsPerTurn, maxFrequency }` | Steppers (#1036): a Servo's sides and slot. `strength` in world torque units (N·m × 10⁴), above 0; `stepsPerTurn` a whole number 2–24; `maxFrequency` steps per second, above 0. |
| `nextPartId` | int | The next free part id. Higher than every id in use; removed ids are never reused. |

`training` (meaning of latest and best: `docs/TRAINING_LOOP.md` → "Latest
and best ever"):

| Field | Type | Meaning |
|---|---|---|
| `network` | object | The best network of the latest finished generation, as a graph; see below. Reopening Training breeds the next generation from it (warm start, #538). |
| `generation` | int | Finished generations, at least 1. |
| `latest` | `{ distance, topSpeed, elevation, mapId, frontDistance }` | The latest generation's run of `network` (#479). |
| `best` | `{ generation, distance, mapId, frontDistance }` | The best ever on that map (#479, #388, #725). |

`distance` is the score; `frontDistance` is the distance shown, `null` in a
save from before #725. `mapId` is a map's stable id from `Maps` (#443);
Flat is `map-flat`.

`training.network` (#536) is a graph keyed by the creature's network ports
(`docs/CREATURE_MODEL.md` → "Sensor–network contract"), so it never depends on
list order, and hidden neurons and structural mutation fit without a new
format. A direct network has one input neuron per input port, one output
neuron per output port, and a connection gene from every input to every
output.

| Field | Type | Meaning |
|---|---|---|
| `neurons[]` | `{ id, kind, partId, channel, layer, bias, activation }` | `kind` is `input`, `output` or `hidden`. An input or output neuron names its port by `partId` and `channel`; a hidden neuron has both `null`. Inputs sit in layer 0 with bias 0 and `identity`; other neurons in layer 1 or later. `activation` is `identity`, `tanh`, `sigmoid` or `relu`; a direct-network output uses its port's signal activation (#535), and resuming training fails loud (`DirectNetwork.Compile`) if it does not match. |
| `connections[]` | `{ from, to, weight, enabled }` | A connection gene between two neuron ids, from a lower layer to a higher one; at most one per pair. A disabled gene keeps its weight but carries no signal. |
| `nextNeuronId` | int | The next free neuron id. Higher than every id in use; ids are never reused. |

A port with no neuron yet starts silent: its connections compile to 0. The
saved network always matches the saved creature (`docs/CREATURE_MODEL.md` →
"A rebuild keeps the network"), except for a port a later version adds to a
part, such as the Camera's `hit` (#1032): a network saved before it has no
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
| 2 | 3 | #870: a Piston's `stroke` ±s (missing: 0.3) becomes `2s / (1 − s)`, capped at 1, with `start: 0.5`, which keeps its shortest and longest lengths up to ±⅓. Its `length` input went from −1…1 around the built length to 0…1 over its travel, so each enabled connection from it doubles its `weight` and takes the old weight off the target neuron's `bias`: a trained network drives its Pistons as before. |
| 3 | 4 | #835: each Spring gets `stroke: 1` and `coilLength: 0.55`: stops about a third of its gap either side of its drawn length (0.325 in, 0.35 out, #974), resting free near the middle as before. A Piston's travel now runs on the gap between its joints' edges, which shortens it a little, but its saved shape is unchanged, so Piston saves load without migration. |
| 4 | 5 | #129: add required `creature.wheels: []`. |
| 5 | 6 | #578: each Camera gets `rays: 3`, `spread: π/2` and `range: 220`, the fan every Camera had, so it sees and reads as before. |
| 6 | 7 | #1043: add required `creature.touchSensors: []`. |
| 7 | 8 | #1129: a Piston's `position` output went from `tanh`, −1…1, to `sigmoid`, 0…1 like its `length` input. As (tanh(x) + 1) / 2 = sigmoid(2x), its `activation` becomes `sigmoid` and its `bias` and the `weight` of every connection into it, enabled or not, double: a trained network drives its Pistons as before. |
| 8 | 9 | #1144: each Wheel drops its `grip`; every tyre now grips like a bare joint, friction 1. |
| 9 | 10 | #1142: add required `creature.rubbers: []`. |
| 10 | 11 | #454: add required `creature.velocityMotors: []`. |
| 11 | 12 | #1039: add required `creature.brakes: []`. |
| 12 | 13 | #1204: add required `creature.angleSensors: []`. |
| 13 | 14 | #1036: add required `creature.steppers: []`. |
| 14 | 15 | #1263: a Stepper's `power` output neuron becomes its `frequency` output; its old `frequency` input and output neurons and their connections are dropped. |
| 15 | 16 | #1308: `training.brain` becomes `training.network`, unchanged. A file without training has nothing to move; one with both keys fails to load. |

#1086 needs no migration: its saved shape is unchanged, but a saved creature
with a Wheel or Servo moves and senses a little differently, since links and
sensors now measure from the hub (`docs/CREATURE_MODEL.md` → Node, Sensor).

#1303 needs no migration either: a saved `training.best.distance` was
measured from the old centre, the average of the beams' midpoints, and is
compared as-is with new scores measured from the average of the nodes
(`docs/TRAINING_LOOP.md` → Trial). The two agree for a creature that moves
without turning or changing shape and differ a little for one that does.

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
5. Rewrite the pins (see "Pinned saves").

The real files 0.13.0 wrote are checked in as fixtures in
`tests/NodeRunner.App.Tests/Repositories/SaveExamples/0.13.0/` (the Walker
example, untrained and trained, and `progression.json`), and
`CreationVersioningTests` and `ProgressionVersioningTests` load them.
They are never rewritten in place.

### Pinned saves

`EveryPartPinTests` builds one creature that holds every part kind (both
Sensor kinds, Cameras with 1, 3 and 5 rays, a free and a Locked Brake, and so
on). It pins three things about that creature in
`tests/NodeRunner.App.Tests/Repositories/Pins/` (#1219):

- `every-part.creation.json`: its `creation.json`, byte for byte apart from
  line endings;
- `every-part.ports.txt`: its network ports (`NetworkPorts.Of`) in order, each with
  its direction, part id, channel, signal and `NetworkPortLabels` text;
- `every-part.share-code.txt`: its share code, which must read back as the
  same build.

A refactor must leave all three unchanged. A failing pin names the first line
that differs. When the change is deliberate (a saved shape, a port, a label
or a new part kind, which the test asks to be added to the creature), rewrite
the pins with
`NODE_RUNNER_UPDATE_PINS=1 dotnet test tests/NodeRunner.App.Tests --filter EveryPartPinTests`.
Commit the new pins in the same PR, so the review shows what changed.

## Share code

Share build in Build's overflow menu copies the creation as one line of
text to paste in a chat; Import creation in Creations' overflow menu reads
it back (#899). `CreationShareCode` (`libs/NodeRunner.App/Repositories/`)
owns it.

- The code is `NR`, the format version and a dot, then its JSON
  zlib-compressed against that version's share dictionary, in URL-safe
  base64 without padding. It holds the build only, so the copy trains from
  scratch. The Walker example's is about 50 characters, starting `NR16.`,
  and so is two joints and a Spring's (#1094).
- Its JSON is `creation.json` on one line with `training` and
  `trainSettings` `null` and the id `00000000-0000-0000-0000-000000000001`,
  made short (#1094):
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
  has two lines. The first is the template: a `creation.json` in its
  version with every kind of part at Build's settings, named "Untitled
  Creation". The second is a varied creation as a code holds it, so a code
  finds its own shape in the dictionary. `ShareDictionaryTests` makes a new
  version's dictionary. The code leaves out the
  dictionary's compressed bytes: the dictionary is compressed first and
  sync-flushed so its bytes end on a whole byte, and reading compresses it
  again in front of the code. zlib's checksum covers both and catches a
  changed character. It is only checked when the code is whole: a code cut
  by a few characters at its end still loads if its build is complete,
  which then is unchanged.
- A dictionary never changes once released, or its codes stop reading;
  `ShareDictionaryTests` reads a pinned version-6 code. Codes began with
  version 6 in 0.14.0.
- An older code is unpacked with its own version's dictionary and goes
  through the same migrations as a saved file. Import then warns that it
  may differ from the shared creation. A code from a newer version is
  refused as newer.
- The code is someone else's text, so reading it is strict and capped:
  white space is ignored, but any other text around it is not; a code over
  32,768 characters, one that unpacks to over 1 MB, or one whose parts
  fill out to over 1 MB more, is refused as damaged, as is anything the migrations or the strict load fail on. A
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
