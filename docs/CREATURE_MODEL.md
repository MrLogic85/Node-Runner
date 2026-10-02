# Creature model

The vocabulary a Node Runner creature is built from. This is the durable
reference for anyone editing creatures, teaching the app, or wiring inspector
UI. Short glossary entries live in `docs/GLOSSARY.md`; the longer-form
descriptions and the code pointers live here.

This model replaces the 0.1.0 Joint/Bone/Muscle prototype. The vocabulary
below was designed by the project owner, not inferred from the old code — if
you are extending it, keep asking "what would the owner want here" rather
than defaulting to what is easiest to implement.

## Parts: Node, Beam, Sensor, Motor relation

A creature is built from two structural parts (Node, Beam), sensor parts
that sit on beams (the Accelerometer and the Camera), and one derived control concept
(Motor relation). Keeping "what senses" (sensors) and "what thinks" (the
neural model) conceptually separate is the most important rule in this
document — **a sensor is not the brain.**

**A part sits on what it senses or moves** (owner decision, #127): sensors
sense one body, so they sit on a beam; motors, brake and wheel act between
two beams, so they will sit on a joint; spring, piston and wing join two
nodes, so they are links. Each sensor is one clear idea, the way real
sensors are.

Every saved Node, Beam, and sensor has a stable positive integer id from the
creature's single part counter (`CreatureDef.NextPartId`). The counter is
saved with the creature, only increases, and deleted ids are never reused.
Ids are machine identity only: they are not display order, draw order, brain
port order, or names. Display names are optional metadata on parts; they may
be duplicated and are never keys.

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  model  ──outputs──▶  motor relations  ──torque──▶  physical body
   (data)                    (physics)                (control)                                            (physics)
```

### Node

- **Beginner:** A physical attachment point. Beams meet here and can rotate
  relative to each other.
- **Implementation:** `NodeDef` in `libs/NodeRunner.Domain/NodeDef.cs` stores
  an id, optional display name, a position (`Vector2D`) and a radius. At
  runtime each node is its own `RigidBody2D` with a circle collider a little
  smaller than its drawn radius (so it looks like it rests slightly in the
  ground). Its rotation is locked, so it grips instead of rolling like a
  wheel. A node weighs half of every beam it joins: the beam's weight is
  simulated at its two ends. Nodes are what touch the world; every beam is
  pinned to its two nodes (see Beam below).
- **Degree rules** (how many beams touch a node):
  - **0 beams** — not ready. A node with nothing attached is just a loose
    point and cannot be simulated. It can be saved as part of an unfinished
    drawing, but `CreatureReadiness` stops training until it is connected
    or removed.
  - **1 beam** — static/passive end. It contributes no motor relations (a
    dangling tip, like a chain's last link).
  - **2+ beams** — one beam is chosen as that node's *reference beam* (its
    zero-direction); every other beam at the node forms one motor relation
    against it. A node with N beams therefore has N−1 motor relations, not
    N and not the full pairwise count — angles relative to one reference
    fully describe the geometry.

### Beam

- **Beginner:** A rigid, fixed-length connection between two nodes. It never
  stretches or compresses — think steel rod, not rubber band.
- **Implementation:** `BeamDef` in `libs/NodeRunner.Domain/BeamDef.cs` stores
  an id, optional display name, and two node ids (`NodeA`, `NodeB`). At
  runtime it becomes its own
  `RigidBody2D` in `project/src/creature/Creature.cs`, pinned with a
  `PinJoint2D` to each of its two node bodies, so its length is fixed by
  geometry. A beam has **no collider**: it carries motor torque and sensors
  between its nodes, while its weight sits on those nodes (see Node above).
  Its own body is nearly massless, with a turning inertia set as a thin
  solid bar. Parts of the same creature never collide with each other
  (collision exceptions are added pairwise), which is what allows car-like,
  closed-loop construction.
- **Minimum length (#593):** a beam must leave
  `CreatureReadiness.MinimumBeamGap` (52) free between its two joint discs,
  room for the largest sensor picture, the Camera's, with a 4-unit gap on
  each side (#622). A shorter beam can be drawn and saved, but it blocks
  training until its joints move apart.
- **Future ideas (not implemented):** beams breaking on hard impact, joints
  tearing apart under load.

### Sensor

- **Beginner:** A part on a beam that feels something about that beam and
  feeds it to the brain. It is *not* the brain.
- **Implementation:** `SensorDef` in `libs/NodeRunner.Domain/SensorDef.cs`
  stores an id, optional display name, its `SensorKind` and the id of the
  beam it sits on. A sensor measures its own beam, at the beam's midpoint,
  with no position setting.
- **One sensor per beam (#593).** `CreatureDef` rejects a second sensor on
  a beam, of any kind; the App refuses it first with the reason "One sensor
  per beam" (`CreatureBuilder.AddSensor`).
- **Frame fixed as built:** the side of the beam that faces up in the built
  pose is the sensor's "up", and "along" points right as built. The frame
  then turns with the beam and never flips during a run
  (`Accelerometer.UpSign`).
- Kinds today: **Accelerometer** (#127) and **Camera** (#575, #604).
- **Seen and tapped as a picture (#576):** a small picture of the sensor at
  the middle of its beam, upright on the built up side and turned with the
  beam; its tap area is a square there, sized per kind (`SensorPicture`:
  24 for the Accelerometer, 44 for the Camera, #622). The
  Accelerometer's weight hangs on its spring: in Build it swings when the
  beam is moved and settles at rest (`BuildSensorMotion`), in Training it
  follows the live proof mass. The Camera looks along its
  aim. In Build a selected Camera shows its full rays; in Training every
  Camera always shows the rays that hit the ground, up to the hit, with a
  `halo` ring there (#623), and nothing for a ray that sees nothing. Rays
  are drawn over the joints, by draw order, not z-index (Build draws them
  after the joints; Training's `CameraRaysVisual` is added after the joint
  bodies). A tap hits a joint first, then a sensor, then a beam, in Build
  and Training alike (`project/src/theme/SensorDrawing.cs`,
  `project/src/creature/SensorVisual.cs`).

#### Accelerometer

- **Beginner:** A small weight on a spring inside a box. When the beam
  speeds up, slows down or tilts, the weight shifts — that shift is what the
  brain feels. At rest it feels gravity, so it also knows which way is down.
- **Implementation:** a proof mass on a damped spring, in the beam's sensor
  frame. Each physics tick `project/src/creature/AccelerometerSensor.cs`
  measures the beam's acceleration at its midpoint from the change in its
  velocity, turns it into specific force (at rest 1 g "up", so its
  direction gives the tilt) in the beam's frame and steps the proof mass
  (`Accelerometer.Step` in `libs/NodeRunner.Domain/Accelerometer.cs`).
- **Reading:** two brain inputs, along the beam and across it:
  `tanh(−d / d_ref)` of the proof mass displacement `d`, where `d_ref` is
  the displacement at 1 g. At rest on a level beam it reads about 0 along
  and 0.76 (`tanh 1`) across; it always stays in −1…1.
- **The spring is the filter:** it smooths spiky per-tick acceleration from
  contacts but still shows impacts as a spike that decays, and gives the
  reading a short memory of recent motion. Natural frequency, damping and
  `d_ref` are named constants in `Accelerometer`, tuned by playtesting.
  `Step` integrates in substeps of at most 1/60 s, so 2× and 4× training
  speed stay stable.
- **Deterministic:** the proof mass starts at rest for gravity as built,
  on every build and every `ResetPose`, so the same brain, build and map
  give the same readings. The state (`AccelerometerSensor.CurrentProofMass`)
  is exposed for the visual (#576) and SignalFlow, which show exactly what
  the brain reads.
- **There is no speed or elevation sensor:** the brain learns movement from
  acceleration, joint readings and its own outputs.

#### Camera

- **Beginner:** Three rays that tell the brain how near the ground is. A
  new camera looks level: ahead-and-up, straight ahead and ahead-and-down;
  you can turn it in Build. A ray that sees the ground shows where it hits
  it; the nearer the ground, the stronger its reading.
- **Aim (#594):** `SensorDef.Aim`, the angle of the centre ray from the
  beam's direction (from its first node to its second), in radians. The
  three rays fan `CameraRays.Spread` (45°) apart around it, and the camera
  turns with its beam. A camera placed in Build, or loaded without an aim,
  gets `CameraRays.DefaultAim`: its centre ray looks level, forward in the
  world as built, so its rays look forward-up, forward and forward-down
  (#622). Only a
  Camera has an aim.
- **Implementation:** `project/src/creature/CameraSensor.cs` adds three
  `RayCast2D` children at the beam's midpoint, aimed by
  `CameraRays.LocalRayTarget` (`libs/NodeRunner.Domain/CameraRays.cs`) in
  the beam body's frame, so they turn with the beam.
  They see the ground only (collision layer 1), `CameraRays.RayLength`
  (220) long. (It is not Godot's `Camera2D`.)
- **Ray names** are symmetric around the centre ray, seen from the camera
  looking along its rays: **left 1**, **centre**, **right 1** (later also
  left 2 / right 2). With the default aim, left 1 looks forward-up, centre
  forward and right 1 forward-down. Every ray count the camera will offer (1, 3 or 5, #578) has
  a centre ray, so the names of the inner rays survive a rebuild with
  another count.
- **Reading:** three brain inputs, left to right: the ray's **nearness**,
  `1 − distance / range` clamped to 0–1, so `0` when nothing is in range,
  rising linearly to `1` at contact (`CameraRays.Reading`). Nothing seen
  feeds 0, which adds nothing to the brain's weighted sum (see
  `docs/ML_CONCEPTS.md`).
- **Settings in 0.12:** only the aim. Ray count and range come with
  camera settings in 0.14 (#578); their power draw comes with power in
  0.18 (#599).

### Motor relation

- **Beginner:** One controllable rotation between two beams that share a
  node — this is how the model actually moves the body.
- **Implementation:** derived, not stored. `MotorTopology.BuildNodeConnections`
  in `libs/NodeRunner.Domain/MotorTopology.cs` computes, from a
  `CreatureDef`'s topology alone, every relation between beams at a node
  (`NodeConnectionDef`), and marks which of those are motorized. Motor
  relations are not saved parts, so they have no ids of their own; until
  motors become parts (#450) a motor relation is identified by its joint's
  node id and the id of the beam it turns. At runtime,
  `project/src/creature/MotorRelation.cs` wraps each motorized connection.
  Its conventions are `JointMotor`'s (`libs/NodeRunner.Domain/JointMotor.cs`):
  - **Inputs it gives the brain** (#534), both counter-clockwise on screen
    positive:
    - **angle:** how far the turning beam has rotated from its built pose
      relative to the locked (reference) beam, wrapped at ±180° and scaled
      to `[-1, 1]`. 0 means as built. Swapping which beam is locked flips
      the sign.
    - **speed:** the relative angular velocity, softly saturated as
      `tanh(v / MaxAngularVelocity)`.
    These are sensor values *going into* the model, exactly like an
    accelerometer's readings, but a motor relation is not a sensor part.
  - **Output it accepts:** a single target angular velocity in `[-1, 1]`
    of `MaxAngularVelocity`, counter-clockwise positive.
  - **How the physical motor behaves:** it drives torque (capped at a
    static `MaxTorque`) to chase the target velocity. There is no separate
    "friction" concept — a target velocity of 0 combined with available
    torque already produces braking/holding behavior, which is what
    "friction" would have meant anyway.
  - `MaxTorque` and `MaxAngularVelocity` are **static per relation for
    0.2.0** (not model outputs) — fixed constants today, likely exposed as
    creature-building/upgrade parameters later.

#### Rigid triangles have no motor relations

Three beams that close a triangle between three nodes are geometrically
rigid (SSS: three fixed side lengths fully determine all three vertex
angles). `MotorTopology` detects every such closed triangle and excludes the
one motor relation it would otherwise create at each of its three vertices —
the beams stay pinned to their nodes (so the triangle stays connected), the
relation just carries no sensor or brain output, because driving it would
either do nothing or fight the other two vertices.

This generalizes to any rigid, triangulated structure (a larger truss is a
composition of triangles), while correctly leaving non-triangulated closed
shapes (e.g. a bare quadrilateral) with their genuine remaining freedom.

## Sensor–model contract

- **Ports (#534):** every part that affects the brain declares its brain
  channels as ports, `BrainPort(partId, channel, direction)`
  (`libs/NodeRunner.Domain/BrainPort.cs`). The channel is a machine key that
  never changes; display names are separate.
  - **Accelerometer:** inputs `along`, `across`.
  - **Camera:** inputs `left1`, `centre`, `right1`.
  - **Motor relation:** on its joint's node, keyed by the beam it turns:
    inputs `angle:<beamId>` and `speed:<beamId>`, output `target:<beamId>`.
  - Nodes, beams and passive ends declare none.
- **Input count** = `(accelerometer count × 2) + (camera count × 3) +
  (motor relation count × 2)`.
- **Output count** = motor relation count.
- **Order** comes from `BrainPorts.Of` (`libs/NodeRunner.Domain/BrainPorts.cs`):
  ports sorted by part id, then in the order the part declares them; motors
  at the same joint go by the id of the beam they turn. It depends only on
  ids, so moving or resizing parts, or adding one, never reorders the
  other ports. Which beam a motor turns still follows the beam list order
  (`MotorTopology` locks the first beam at a joint) until motors become
  parts (#450). `Creature` reads its sensors and
  motors in its own order and copies each value to its port's place, and
  fails loud if its ports and `BrainPorts` ever disagree.
- A creature with zero motor relations (e.g. a single node/beam) has no
  brain at all — nothing to control, nothing to sense from motor relations.
- **Visible in the UI (issue #42):** Training's BrainFocus sheet shows the
  live sensor readings and each motor relation's model output, refreshed on
  a ~0.15s cadence (not every rendered frame — see `TrainingHost._Process`).
  The SignalFlow stages only count readings and motors until #196 draws
  them. See `Creature.ReadMapping()`.

## Editing identity rules

- Creating a Node, Beam, or sensor takes the current `NextPartId` and then
  advances the counter.
- Removing a Node, Beam, or sensor retires that id forever. Removing a Node
  also removes the beams on it, and removing a Beam removes its sensors;
  surviving parts keep their ids because no list reindexing is needed.
- Beam split by the Joint tool removes the original beam id and creates one
  fresh node id plus two fresh beam ids. The beam's sensors move, with their
  ids, to the longer half (the half at the beam's first node on a tie).
- Saving, loading, moving, renaming, reordering lists, rebuilding a body, and
  copying a whole Creation preserve part ids and the counter.
- Part-to-part references are by stable id. Code that needs an array position
  uses `CreatureDef`'s id-to-index lookups at the boundary.

## Worked example: the 0.2.0 hardcoded creature

`project/src/creature/HardcodedCreatureFactory.cs` builds the first concrete
`CreatureDef` under this model — the same 5-node chain shape as the old
0.1.0 worm, reinterpreted:

```
     beam      beam      beam      beam
   ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐
   │      │ │      │ │      │ │      │
  (N0)───(N1)───(N2)───(N3)───(N4)
   accel + camera
```

- **5 nodes** (`N0`-`N4`) spaced 56 units apart, radius 18.
- **4 beams**, one per adjacent pair, referencing node ids.
- **1 accelerometer and 1 camera**, both on the head beam (`N0`–`N1`).
- **Node degrees:** `N0` and `N4` have 1 beam each (passive ends); `N1`,
  `N2`, `N3` each have 2 beams, giving 3 motor relations total — no closed
  loops, so no triangle exclusions apply here.
- **Sensors:** `1 accelerometer × 2` + `1 camera × 3` +
  `3 motor relations × 2` = 11.
- **Brain outputs:** 3, one per motor relation.

## What this model does not cover yet

These are deliberate future ideas, not oversights — do not build them
without a fresh design conversation:

- Beams breaking on impact; joints tearing apart under load.
- Collision between a creature's own parts (currently always disabled).
- Parts as unlockable resources via an achievement/quest
  progression system, rather than unlimited from the start. Today every
  part is unlimited (#557); achievements (#525) own the first unlocks.
- Sensors on blocks, and sensor types beyond the Accelerometer and the
  Camera.
- Exposing `MaxTorque`/`MaxAngularVelocity` as player- or
  upgrade-configurable settings, rather than fixed constants.
- Whether a motor relation's speed should always be included as an input,
  or made optional/experimental — 0.2.0 includes it; this may be revisited
  once training exists and the difference is measurable.

If you propose adding one of these, update this document first and then the
code.
