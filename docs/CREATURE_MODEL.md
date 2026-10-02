# Creature model

The vocabulary a Node Runner creature is built from. This is the durable
reference for anyone editing creatures, teaching the app, or wiring inspector
UI. Short glossary entries live in `docs/GLOSSARY.md`; the longer-form
descriptions and the code pointers live here.

This model replaces the 0.1.0 Joint/Bone/Muscle prototype. The vocabulary
below was designed by the project owner, not inferred from the old code — if
you are extending it, keep asking "what would the owner want here" rather
than defaulting to what is easiest to implement.

## Parts: Node, Beam, Sensor, Piston

A creature is built from two structural parts (Node, Beam), sensor parts
that sit on beams (the Accelerometer and the Camera) and links between two
nodes (the Piston). Joints are passive (#450): a beam turns freely where it
meets another, and only parts with brain ports move the body. Joint motor
parts come with the Servo (#452) and the Velocity motor (#454). Keeping
"what senses" (sensors) and "what thinks" (the neural model) conceptually separate is the most important rule in this
document — **a sensor is not the brain.**

**A part sits on what it senses or moves** (owner decision, #127): sensors
sense one body, so they sit on a beam; motors, brake and wheel act between
two beams, so they will sit on a joint; spring, piston and wing join two
nodes, so they are links. Each sensor is one clear idea, the way real
sensors are.

Every saved Node, Beam, sensor and Piston has a stable positive integer id from the
creature's single part counter (`CreatureDef.NextPartId`). The counter is
saved with the creature, only increases, and deleted ids are never reused.
Ids are machine identity only: they are not display order, draw order, brain
port order, or names. Display names are optional metadata on parts; they may
be duplicated and are never keys.

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  model  ──outputs──▶  pistons  ──force──▶  physical body
   (data)                    (physics)                (control)                                (physics)
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
  - **0 beams** — not ready unless a Piston joins it. A node with nothing
    attached is just a loose point and cannot be simulated. It can be saved as part of an unfinished
    drawing, but `CreatureReadiness` stops training until it is connected
    or removed.
  - **1 beam** — a dangling tip, like a chain's last link.
  - **2+ beams** — a passive joint: the beams turn freely against each
    other unless a closed triangle locks them (see Rigid triangles below).
    A joint has no settings, no angle limits and no brain ports.

### Beam

- **Beginner:** A rigid, fixed-length connection between two nodes. It never
  stretches or compresses — think steel rod, not rubber band.
- **Implementation:** `BeamDef` in `libs/NodeRunner.Domain/BeamDef.cs` stores
  an id, optional display name, and two node ids (`NodeA`, `NodeB`). At
  runtime it becomes its own
  `RigidBody2D` in `project/src/creature/Creature.cs`, pinned with a
  `PinJoint2D` to each of its two node bodies, so its length is fixed by
  geometry. A beam has **no collider**: it carries sensors between its nodes, while its weight sits on those nodes (see Node above).
  Its own body is nearly massless, with a turning inertia set as a thin
  solid bar. Parts of the same creature never collide with each other:
  every creature body sits on collision layer 2 and masks only the ground
  (layer 1). That allows car-like, closed-loop construction and also keeps
  shadows from touching each other.
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
  Camera on the followed shadow shows the rays that hit the ground, up to
  the hit, with a `halo` ring there (#623), and nothing for a ray that sees
  nothing. Rays are drawn over the joints, by draw order, not z-index
  (Build draws them after the joints; Training's `CameraRaysVisual` is added
  after the joint bodies). A tap hits a joint first, then a sensor, then a
  beam, in Build and Training alike (`project/src/theme/SensorDrawing.cs`,
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
  acceleration, Piston length and speed, and its own outputs.

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

#### Rigid triangles

Three beams that close a triangle between three nodes are geometrically
rigid (SSS: three fixed side lengths fully determine all three vertex
angles), so its joints cannot turn. `RigidTriangles.Of`
(`libs/NodeRunner.Domain/RigidTriangles.cs`) finds every such triangle, and
Build hatches it so the player sees which areas are rigid. A larger truss is
a composition of triangles; a bare quadrilateral stays free to fold.

### Piston

- **Beginner:** A powered link between two nodes that pushes them apart or
  pulls them together, in a straight line. The brain chooses how far out it
  goes and how hard it may push.
- **Implementation:** `PistonDef` in `libs/NodeRunner.Domain/PistonDef.cs`
  stores an id, optional display name, two node ids and three settings
  chosen in Build: **Strength** (its most force; 150 N new), **Stroke** (how
  far it moves each way from its built length, as a share of it; ±30% new)
  and **Max speed** (2 m/s new). Its built length is the distance between
  its nodes in the drawing. At runtime `project/src/creature/PistonLink.cs`
  pushes its two node bodies apart or together along the line between them
  every physics tick; it is not a body and has no collider or weight.
- **Not a beam:** it does not hold its length, so it adds no rigidity, and it counts as attached for the node degree rules. A
  Piston cannot join two nodes a beam already joins (the beam would hold
  them rigid), and two nodes hold at most one Piston (`CreatureBuilder.CanAddPiston`).
- **Force** (`Piston.Step` in `libs/NodeRunner.Domain/Piston.cs`): it
  chases the target length from its position output at up to Max speed,
  slowing as it arrives, with at most the strength output's share of its
  Strength. Past either end of its stroke it may use its full Strength,
  whatever the brain asks: the end stops belong to the cylinder. The speed
  control is a PI controller with gains from the reduced mass of its two
  nodes, the lightest load it can move, so it stays steady on a light limb
  tip and holds a load such as the body's weight at its target.
- **Minimum length:** the same as a beam's (`CreatureReadiness.MinimumBeamGap`).
- **Drawn** as a rod from node A to node B with a cylinder at A and a cap at
  B (`project/src/theme/PistonDrawing.cs`), over beams and under joints. A
  selected Piston shows ticks at its shortest and longest lengths.

## Drawing as a shadow

In Training (#385) every shadow except the followed one
is drawn simplified: the same shapes and colours as the followed creature,
without detail, and transparent. This keeps drawing cheap with up to 32
shadows and cuts the clutter when they overlap. Every node, beam and part
visual declares how it draws as a shadow (not at all, simplified, or the same
as on the followed creature), and a test checks that every kind has made that
choice, so a new part cannot forget it: each visual implements
`IShadowVisual` (`project/src/creature/ShadowDrawing.cs`) with a static
`AsShadow` and an `IsShadow` switch, and `Creature.IsShadow` sets them all,
fades the creature and puts it behind the followed one. The fade is per
item (`Modulate` on the creature), not per shadow, so a shadow's beam shows
through its own joints; a `CanvasGroup` per shadow would fade it as one
picture but costs an offscreen pass each, so it is left out for performance.

- **Node:** its ring at its drawn size, without a glyph. Joint parts that make
  a node larger (motor joints, #626; later the Wheel, #129, and a touch
  sensor, #665) only change that size.
- **Beam:** its line, without angle marks, the rigid-area hatch or labels.
- **Links** (Piston, Spring and later Wing): drawn as on the followed
  creature. The Wing may lose detail; #600 decides.
- **Blocks** (Battery, Generator and Fuel tank, 0.18.0): drawn as on the
  followed creature, possibly with less detail; #600 decides.
- **Sensors, Pulse and Camera rays:** not drawn. A hidden sensor also stops
  redrawing every frame.

`docs/UI_DIRECTION.md` owns the transparency.

## Sensor–model contract

- **Ports (#534):** every part that affects the brain declares its brain
  channels as ports, `BrainPort(partId, channel, direction, signal)`
  (`libs/NodeRunner.Domain/BrainPort.cs`). The channel is a machine key that
  never changes; display names are separate. An input carries a reading; an
  output names the physical signal it drives (#535).
  - **Accelerometer:** inputs `along`, `across`.
  - **Camera:** inputs `left1`, `centre`, `right1`.
  - **Piston (#451):** inputs `length` (−1…1 over its stroke, 0 as built)
    and `speed` (`tanh(v / maxSpeed)`, extending positive); position output
    `position` and strength output `strength`.
  - Nodes, beams and joints declare none.
- **Output conventions (#535, `PortSignals`):** the signal fixes the
  output's activation and how a new output starts.
  - **Velocity** (the Velocity motor's target, #454) and **position** use `tanh`:
    −1…1, where 0 means stand still or the built pose.
  - A position target maps piecewise, so 0 stays the built pose even when
    the built pose is off-centre: −1…0 spans fully in…built and 0…1 spans
    built…fully out (`PortSignals.PositionFromTarget`). For the Piston (#451)
    −1 is fully in and +1 fully out.
  - **Strength** uses `sigmoid`: 0…1, the share of the part's **Strength
    setting** used this tick. The setting is the part's maximum force, chosen
    in Build; the strength output is the brain's choice of how much of it to
    use (`PortSignals.StrengthFromOutput`).
  - **New ports start almost passive:** when a part joins a trained brain,
    its incoming weights are 0 and a new strength output starts at bias −4,
    about 2% force. Sigmoid has no dead zone, so mutation can still raise it.
    Position and velocity outputs start at 0.
- **Input count** = `(accelerometer count × 2) + (camera count × 3) +
  (piston count × 2)`.
- **Output count** = `piston count × 2`.
- **Order** comes from `BrainPorts.Of` (`libs/NodeRunner.Domain/BrainPorts.cs`):
  ports sorted by part id, then in the order the part declares them. It
  depends only on ids, so moving or resizing parts, or adding one, never
  reorders the other ports. `Creature` reads its parts in its own order and copies each value to its port's place, and
  fails loud if its ports and `BrainPorts` ever disagree.
- A creature with no outputs (no Piston yet) has no brain at all and cannot train: there is nothing to control.
- **Visible in the UI (issue #42):** Training's BrainFocus sheet shows the
  live sensor readings and each output's value, refreshed on
  a ~0.15s cadence (not every rendered frame — see `TrainingHost._Process`).
  The SignalFlow stages only count readings and moving parts until #196 draws
  them. See `Creature.ReadMapping()`.

## Editing identity rules

- Creating a Node, Beam, sensor or Piston takes the current `NextPartId` and then
  advances the counter.
- Removing a Node, Beam, sensor or Piston retires that id forever. Removing a
  Node also removes the beams and Pistons on it, and removing a Beam removes its sensors;
  surviving parts keep their ids because no list reindexing is needed.
- Beam split by the Joint tool removes the original beam id and creates one
  fresh node id plus two fresh beam ids. The beam's sensors move, with their
  ids, to the longer half (the half at the beam's first node on a tie).
- Saving, loading, moving, renaming, reordering lists, rebuilding a body, and
  copying a whole Creation preserve part ids and the counter.
- Part-to-part references are by stable id. Code that needs an array position
  uses `CreatureDef`'s id-to-index lookups at the boundary.

## Worked example: the Worm

`CreationExamples.CreateWormCreature` (`libs/NodeRunner.App/Services/CreationExamples.cs`)
builds the Worm example, which Training also falls back to: an inchworm
with a flat tail and a high hump at the front, and a Piston under the hump.

```
                  (N3)
                 /    \
  (N1)───────(N2)┄┄┄┄┄┄(N4)
  accel          piston   camera
```

- **4 nodes** spaced 90 units apart, radius 18; `N3` sits 90 units up.
- **3 beams**, one per adjacent pair. Every joint is passive.
- **1 Piston** from `N2` to `N4` with the default settings: pulling in
  raises the hump, pushing out stretches the front forward. The hump is
  high enough that the full ±30% stroke never flattens it (a straight push
  through a flat chain could no longer bend it), and the flat tail makes
  the crawl lopsided, so it has a forward direction. A headless check of
  simple Piston rhythms moved it 1–2.5 m forward in 10 s.
- **1 accelerometer** on the tail beam (`N1`–`N2`) and **1 camera** on the
  front beam (`N3`–`N4`).
- **Inputs:** `1 accelerometer × 2` + `1 camera × 3` + `1 piston × 2` = 7.
- **Outputs:** 2, the Piston's position and strength.

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

If you propose adding one of these, update this document first and then the
code.
