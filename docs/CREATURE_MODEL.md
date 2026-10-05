# Creature model

The vocabulary a Node Runner creature is built from. This is the durable
reference for anyone editing creatures, teaching the app, or wiring inspector
UI. Short glossary entries live in `docs/GLOSSARY.md`; the longer-form
descriptions and the code pointers live here.

This model replaces the 0.1.0 Joint/Bone/Muscle prototype. The vocabulary
below was designed by the project owner, not inferred from the old code — if
you are extending it, keep asking "what would the owner want here" rather
than defaulting to what is easiest to implement.

## Parts: Node, Beam, Sensor, Piston, Spring

A creature is built from two structural parts (Node, Beam), sensor parts
that sit on beams (the Accelerometer and the Camera) and links between two
nodes (the Piston and the Spring). Joints are passive (#450): a beam turns freely where it
meets another, and only parts with brain ports move the body. Joint motor
parts come with the Servo (#452) and the Velocity motor (#454). Keeping
"what senses" (sensors) and "what thinks" (the neural model) conceptually separate is the most important rule in this
document — **a sensor is not the brain.**

**A part sits on what it senses or moves** (owner decision, #127): sensors
sense one body, so they sit on a beam; motors, brake and wheel act between
two beams, so they will sit on a joint; spring, piston and wing join two
nodes, so they are links. Each sensor is one clear idea, the way real
sensors are.

Every saved Node, Beam, sensor, Piston and Spring has a stable positive integer id from the
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
  an id, optional display name and a position (`Vector2D`). Its radius is
  not saved: it follows from what is on the joint (#626). A plain joint,
  today every joint, has radius `NodeDef.PlainJointRadius` (15) and is
  drawn as a ring at that size (`docs/UI_DIRECTION.md`, plain joints). Joint parts (the motors, #452 and #454)
  will make it larger and show their glyph inside. At runtime each node is
  its own `RigidBody2D` with a circle collider at that radius, the size it
  is drawn at. Its rotation is locked, so it grips instead of rolling like a
  wheel. A node weighs a quarter of every beam and half of every link it
  joins: a link's weight is simulated at its two ends, and a beam keeps half
  of its own (see Beam below). Nodes are what touch the world; every beam is
  pinned to its two nodes (see Beam below).
- **Degree rules** (how many beams touch a node):
  - **0 beams** — not ready unless a Piston or Spring joins it. A node with nothing
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
  geometry. A beam has **no collider**: it carries sensors between its nodes.
  Half its weight is its own body, with a turning inertia set as a thin
  solid bar, and a quarter sits on each node (see Node above). Godot's
  solver can't hold a nearly massless body pinned between heavy nodes: under
  Piston load the pins gave way and rigid triangles folded inside out (#794). Parts of the same creature never collide with each other:
  every creature body sits on collision layer 2 and masks only the ground
  (layer 1). That allows car-like, closed-loop construction and also keeps
  shadows from touching each other.
- **Minimum length (#593):** a beam must leave
  `CreatureReadiness.MinimumBeamGap` (52) free between its two joint rings,
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
  aim. In Build a Camera selected alone shows its full rays; in Training every
  Camera on the followed shadow shows the rays that hit the ground, up to
  the hit, with a `halo` ring there (#623), and nothing for a ray that sees
  nothing. Rays are drawn over the joints (Build draws them after the
  joints; Training's `CameraRaysVisual` sits on the creature's overlay
  layer, see "Draw layers" below). A tap hits a joint first, then a sensor, then a
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
  (`Accelerometer.Step` in `libs/NodeRunner.Mechanics/Accelerometer.cs`).
- **Reading:** two brain inputs, along the beam and across it:
  `tanh(−d / d_ref)` of the proof mass displacement `d`, where `d_ref` is
  the displacement at 1 g. At rest on a level beam it reads about 0 along
  and 0.76 (`tanh 1`) across; it always stays in −1…1.
- **The spring is the filter:** it smooths spiky per-tick acceleration from
  contacts but still shows impacts as a spike that decays, and gives the
  reading a short memory of recent motion. Natural frequency, damping and
  `d_ref` are named constants in `Accelerometer`, tuned by playtesting.
  `Step` integrates in substeps of at most 1/60 s, so a longer `dt` stays
  stable. Training always steps 1/60 s (#787); Build's frame-delta
  preview can pass longer ones.
- **Deterministic:** the proof mass starts at rest for gravity as built,
  on every build, and `ResetPose` builds the creature afresh, so the same brain, build and map
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
  gets `SensorDef.DefaultAim`: its centre ray looks level, forward in the
  world as built, so its rays look forward-up, forward and forward-down
  (#622). Only a
  Camera has an aim.
- **Implementation:** `project/src/creature/CameraSensor.cs` adds three
  `RayCast2D` children at the beam's midpoint, aimed by
  `CameraRays.LocalRayTarget` (`libs/NodeRunner.Mechanics/CameraRays.cs`) in
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
(`libs/NodeRunner.Mechanics/RigidTriangles.cs`) finds every such triangle, and
Build hatches it so the player sees which areas are rigid, and so does
Training on the followed creature (#627); the other shadows fill it
faintly instead (#770). There the hatch rides on one of the triangle's
beams, which it cannot move against. A larger truss is
a composition of triangles; a bare quadrilateral stays free to fold.

### Piston

- **Beginner:** A powered link between two nodes that pushes them apart or
  pulls them together, in a straight line. The brain chooses how far out it
  goes and how hard it may push.
- **Implementation:** `PistonDef` in `libs/NodeRunner.Domain/PistonDef.cs`
  stores an id, optional display name, two node ids and four settings
  chosen in Build: **Strength** (its most force; 150 N new), **Stroke** (how
  far it moves each way from its built length, as a share of it; ±30% new),
  **Max speed** (2 m/s new) and **Rise time** (how long its force takes to
  build up to full; 0.2 s new, #801). Its built length is the distance between
  its nodes in the drawing. At runtime `project/src/creature/PistonLink.cs`
  pushes its two node bodies apart or together along the line between them
  every physics tick; it has no collider. It weighs as much as a beam, half
  on each node, plus its end stops' cylinder (below): one and a half beams.
- **Not a beam:** inside its stroke it does not hold its length, so it adds no rigidity, and it counts as attached for the node degree rules. A
  Piston cannot join two nodes a beam already joins (the beam would hold
  them rigid), and two nodes hold at most one Piston (`CreatureBuilder.CanAddPiston`).
- **Force** (`Piston.NextForce` in `libs/NodeRunner.Mechanics/Piston.cs`,
  the owner's formula, #801): it chases the target length from its position
  output at up to Max speed, slowing as it arrives, with at most the
  strength output's share of its Strength (`tF`). The wanted speed is ten
  times the distance left, capped at Max speed, and `dV` is the speed it
  lacks. Each tick
  `F(N+1) = clamp((F(N) + dT · tF / Rise time · tanh(3 · dV / Max speed)) · σ(10 · d · dV / Max speed), ±tF)`,
  where `d` is the sign of the raised force. While it lacks speed its force
  builds up to full within its Rise time; the gate makes it die away once it
  moves as fast as it wants in the direction it pushes. Built from the speed
  it lacks rather than the distance left, it brakes before its target
  instead of swinging past it. A force that builds up cannot jump between
  its limits every tick, so it does not shake on light parts the way an
  instant force did.
- **Not tuned to its load** (owner decision, #801): the force comes from
  the Piston's own settings and outputs, never from the mass it moves, so a
  rebuild does not change how it responds. Holding a load, it sags until it
  lacks enough speed to keep its force up (a quarter of its Strength sags it
  a few world units); a heavy load can make it bob slowly. A light load at
  the shortest Rise time and a low Max speed can swing around its target,
  and a weak Piston with a high Max speed cannot brake in time and overshoots.
  That is for the user's settings, the brain (less strength) and fitness
  (#546).
- **End stops** (#701): its length stays within its stroke, whatever the load.
  Like a real cylinder, the ends are a hard limit, not extra force: a hidden
  cylinder body (no collider) turns freely on node A (the groove can't sit
  on node A itself, whose rotation is locked), and a Godot `GrooveJoint2D`
  lets node B slide only along it between the shortest and longest length (`Creature.CreateEndStops`). Inside the stroke the
  groove pushes nothing along the Piston, so it never works against the
  force; at an end it holds like any joint, giving up to about 15% of the
  stroke for a few ticks on a hard impact. The cylinder weighs half a beam,
  sitting at node A. Godot's joints give far more when their bodies are
  much lighter than the rest: with a weightless Piston and a near-weightless
  cylinder, a joint held only by Pistons tore free of its end stops (#731).
- **Minimum length:** the same as a beam's (`CreatureReadiness.MinimumBeamGap`).
- **Drawn** as a rod from node A to node B with a cylinder at A and a cap at
  B (`project/src/theme/PistonDrawing.cs`), over beams and under joints
  (see "Draw layers"). The
  cylinder is the stroke's share of its built length (±50% draws half). A
  selected Piston shows ticks at its shortest and longest lengths while the
  selection can set its Stroke (#704).

### Spring

- **Beginner:** A springy link between two nodes. Stretch or squeeze it
  and it pulls back toward the length it was drawn at; its damping stops it
  bouncing. It has no brain ports: it stores and gives back energy, so a
  Piston's push can bounce, but it never chooses anything.
- **Implementation:** `SpringDef` in `libs/NodeRunner.Domain/SpringDef.cs`
  stores an id, optional display name, two node ids and two settings chosen
  in Build: **Stiffness** (N/m, 400 new) and **Damping** (N·s/m, 10 new). Its rest length is the
  distance between its nodes in the drawing. At runtime it is a Godot
  `DampedSpringJoint2D` between its two node bodies (`Creature.CreateSprings`);
  it has no collider. It weighs as much as a beam, half on each node.
- **Damping is a plain coefficient** (#801): the force braking the speed
  between its nodes, per unit of speed. Like the Piston it is not tuned to
  the mass it moves, so the same Damping bounces more on heavy nodes than on
  light ones. Godot's spring joint damps on every second solver iteration
  rather than once per step (`godot_joints_2d.cpp`, checked in 4.7), so
  `Creature` divides the coefficient by those passes. If Godot changes
  that, springs will bounce more.
- **Not a beam:** it counts as attached for the node degree rules, but adds
  no rigidity. Two nodes hold at most one link (Piston or Spring), and no
  link joins two nodes a beam already joins (`CreatureBuilder.CanAddSpring`).
- **Stiffness range** 50–2000 N/m. Godot integrates the spring force once
  per step, so a stiff spring on light nodes can blow up; at 2000 N/m the
  lightest pair (two nodes with only the spring) keeps about twice the
  margin at Training's fixed 1/60 s step (#787), and a headless run with no
  damping stayed bounded.
- **Minimum length:** the same as a beam's (`CreatureReadiness.MinimumBeamGap`).
- **Drawn** as a coilover (#807, `project/src/theme/SpringDrawing.cs`): a
  thin `line-strong` rod that stops under the joint rings, a seat plate just
  outside each joint's edge, a `panel` damper body with a `line-strong` outline in
  the middle, and a seven-turn helix wound round it. The helix's front
  strokes are `muted` and drawn over the body. Its back strokes are
  `line-strong` hairlines drawn under it. The turns spread as the Spring
  stretches and bunch as it squeezes. The body keeps the length the Spring
  was built with, but shrinks if the coil would no longer show round it.
  There is no `accent`, because accent marks parts the brain drives. An
  Orchid coil was tried on a device and judged too busy; the hue is kept as
  a possible highlight token (#832). Too short, all of it turns `danger`. Selected, it gets the Piston's
  two halo lines outside its seats (`docs/UI_DIRECTION.md` → Selection). A
  creation card draws the same coilover, scaled down with the creature
  (#770).

## Draw layers

A creature draws in named layers (`project/src/theme/CreatureLayers.cs`,
#767), bottom to top: Training's knock-out outline (#818, see "Drawing as a
shadow"), rigid hatch, underlays such as Build's placing
feedback, beams, links (Pistons and Springs), a selected link, sensors, a selected sensor,
joints, a selected joint, then overlays such as the camera rays. Each part
visual (`project/src/theme/*Part.cs`) puts itself on its layer, so the
picture never depends on the order parts are added. A screen draws its own
marks on an underlay or overlay through `ViewLayer`.

A part draws its selection with itself, never on a separate layer: a mark on
its own layer would weave through the parts around it. Instead a selected
part moves whole up to the selected layer of its kind, so a selected beam
draws over the link that crosses it, mark and all (#766). Training's world
stacks the ground, then every shadow, then the followed creature
(`ArenaLayers`). Build draws its creature with the same parts (#769, through
`CreatureParts`), with the grid and selection box under them. Each world has
its own viewport (`UiWorldView`), so these layers never reach the screen's
handles, notes or dialogs. A creation card's thumbnail (#770) shows the
same `CreatureParts`, scaled to fit its own world, without any edit-only
marks: no selection, loose or too-short tint, stroke ticks or camera rays.

## Drawing as a shadow

In Training (#385) every shadow except the followed one
is drawn simplified: the same shapes and colours as the followed creature,
without detail, and transparent. This keeps drawing cheap with up to 100
shadows and cuts the clutter when they overlap. Every node, beam and part
visual declares how it draws as a shadow (not at all, simplified, or the same
as on the followed creature), and a test checks that every kind has made that
choice, so a new part cannot forget it: each visual implements
`IShadowVisual` (`project/src/creature/ShadowDrawing.cs`) with a static
`AsShadow` and an `IsShadow` switch, and `Creature.IsShadow` sets them all
and puts the creature behind the followed one.

All the shadows fade together, once (#818): where they overlap they do not
add up to a solid mass that hides the followed creature. The arena draws
them in a viewport of their own (`ArenaShadows`), which shares its world and
camera, and shows that picture at the shadows' alpha. A viewport draws an
item only if it and every canvas item above it are on one of the viewport's
visibility layers (`ArenaVisibility`), so the creature's root picks the
viewport and everything inside or above it, the world included, is on both.
A shadow is
therefore one flat picture: its beam does not show through its own joints.
A `CanvasGroup` would fade a group once too, but the parts' own draw layers
escape it, and the shadows are not children of one node. The followed
creature has a knock-out outline instead (`KnockoutVisual`): the arena's
background a little wider than each beam and node, under the whole
creature, so it stands clear of the shadows behind it.

- **Node:** its ring at its collision size, without a glyph. Joint parts
  that make a node larger (the motors, #452 and #454; later the Wheel, #129,
  and a touch sensor, #665) only change that size.
- **Beam:** its line, without angle marks or labels.
- **Rigid triangle:** a faint fill instead of the hatch (#770), as the hatch
  shows when zoomed far out.
- **Links** (Piston, Spring and later Wing): drawn as on the followed
  creature. The Wing may lose detail; #600 decides.
- **Blocks** (Battery, Generator and Fuel tank, 0.18.0): drawn as on the
  followed creature, possibly with less detail; #600 decides.
- **Sensors, Pulse and Camera rays:** not drawn. A hidden sensor also stops
  redrawing every frame.
- **Knock-out outline:** not drawn; only the followed creature has it.

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
    built…fully out (`OutputSignals.PositionFromTarget`). For the Piston (#451)
    −1 is fully in and +1 fully out.
  - **Strength** uses `sigmoid`: 0…1, the share of the part's **Strength
    setting** used this tick. The setting is the part's maximum force, chosen
    in Build; the strength output is the brain's choice of how much of it to
    use (`OutputSignals.StrengthFromOutput`).
  - **New ports start almost passive:** when a part joins a trained brain,
    its incoming weights are 0 and a new strength output starts at bias −4,
    about 2% force. Sigmoid has no dead zone, so mutation can still raise it.
    Position and velocity outputs start at 0.
  - **A rebuild keeps the brain (#516):** saving a Build edit to a trained
    creature refits a brain to the new ports (`DirectBrain.Refit`). Ports
    match by part id, channel and direction: a kept port keeps its weights
    and bias, a new port starts almost passive as above, and a removed
    part's neurons and connections are dropped. Moving nodes or changing a
    part's settings keeps every port. Part ids are never reused, so a part
    removed and added again is a new part that starts passive.
  - **Build refits the brain it opened with (#689):** every save in one
    Build visit refits the brain as it was when Build opened, not the last
    saved one. So a part an Undo brings back in the same visit gets its
    neurons, ids and weights back. A port that brain lacks, from a part
    added this visit, keeps the neuron ids the last save gave it; fresh
    ids start past both brains' `NextNeuronId`, so no neuron id is ever
    reused, though new ports' ids need not be sequential. Generation,
    latest and best always come from the saved training.
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
  them. See `Creature.ReadInputs()`.

## Editing identity rules

- Creating a Node, Beam, sensor, Piston or Spring takes the current `NextPartId` and then
  advances the counter.
- Removing a Node, Beam, sensor, Piston or Spring retires that id forever. Removing a
  Node also removes the beams and links on it, and removing a Beam removes its sensors;
  surviving parts keep their ids because no list reindexing is needed.
- Saving, loading, moving, renaming, reordering lists, rebuilding a body, and
  copying a whole Creation preserve part ids and the counter.
- Part-to-part references are by stable id. Code that needs an array position
  uses `CreatureDef`'s id-to-index lookups at the boundary.

## Worked example: the Walker

`CreationExamples.CreateWalkerCreature` (`libs/NodeRunner.App/Services/CreationExamples.cs`)
builds the Walker (#745), the only example, which the first start copies
and Training falls back to: a walker seen from the side, with a leg hanging
from each end of its back and two Pistons crossing between them.

```
  (N1)────accel────(N2)
   │  ╲          ╱  │
   │    ╲      ╱    │
   │      ╲  ╱      │
   │      ╱  ╲      │
   │    ╱      ╲    │
  (N3)            (N4)
       crossed pistons
```

- **4 nodes:** the back `N1` (0, −120) to `N2` (200, −120), and the feet
  `N3` (−40, 0) and `N4` (240, 0), splayed a little wider than the back.
- **3 beams:** the back `N1`–`N2` and the legs `N1`–`N3` and `N2`–`N4`.
  Every joint is passive.
- **2 Pistons** with the default settings, `N2`–`N3` and `N1`–`N4`. They
  hold the body up and swing the legs; the brain drives nothing else.
- **1 accelerometer** on the back.
- **Inputs:** `1 accelerometer × 2` + `2 pistons × 2` = 6.
- **Outputs:** 4, each Piston's position and strength.
- **Why this one (#745).** It shows learning in the first minutes with the
  default Train setup (8 shadows, 10 s runs). Headless on Flat over 10
  seeds, its median best distance went from 0.8 m in generation 0 to
  6.9 m after 18 generations (3 minutes of play), and no seed stayed below
  3.6 m. It walks upright. The one-Piston Worm it replaced went from
  0.3 m to 0.7 m, and the three-Piston Frog (#811) from 1.1 m to 3.5 m
  with its worst seed at 0.75 m. A two-hump crawler did 1.5 m. A Spring
  between the Walker's feet, or across the Worm's hump, made both worse.

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
