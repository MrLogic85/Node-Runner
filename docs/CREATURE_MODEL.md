# Creature model

What each part of a Node Runner creature is and does, and the contract
between its parts and the brain. Short definitions are in
`docs/GLOSSARY.md`; how parts look is in `docs/WORLD_VISUALS.md`.

**The model is not the simulation** (#452). This document describes a part
in words that would hold for any physics engine: no engine body or joint
types, and no rules that differ by how a part happens to be simulated. How
Godot realises each part is in `project/src/creature/AGENTS.md` and the code
comments there.

## Parts: Node, Beam, Sensor, Servo, Piston, Spring

A creature is built from two structural parts (Node, Beam), sensor parts
that sit on beams (the Accelerometer and the Camera), a joint motor (the
Servo) and links between two nodes (the Piston and the Spring). Plain joints
are passive (#450): a beam turns freely where it meets another, and only
parts with brain ports move the body. "What senses" (sensors) and "what
thinks" (the brain) stay separate; this is the most important rule here:
**a sensor is not the brain.**

**A part sits on what it senses or moves** (#127): a sensor senses one body,
so it sits on a beam; a part acting between two beams sits on a joint
(Servo); one joining two nodes is a link (Piston, Spring). Each sensor is
one clear idea, the way real sensors are.

Every part has a stable id (see "Editing identity rules"). Ids are machine
identity only: not display, draw or brain-port order, and not names.
Display names are optional, may repeat and are never keys.

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  model  ──outputs──▶  servos + pistons  ──force──▶  physical body
   (data)                    (physics)                (control)                                (physics)
```

### Node

- **Beginner:** A physical attachment point. Links meet here and can rotate
  relative to each other. Nodes are where the creature touches the world.
- `NodeDef`. Its radius is not saved but follows from what is on the joint
  (#626): a plain joint is `NodeDef.PlainJointRadius` (15), a Servo joint
  `ServoDef.JointRadius` (27).
- **Degree rules** (links touching a node, counting Beams, Pistons and
  Springs):
  - **0 links:** a loose point that cannot be simulated. It can be saved,
    but `CreatureReadiness` stops training until it is connected or removed.
  - **1 link:** a dangling tip.
  - **2+ links:** a passive joint unless a Servo sits on it: the links turn
    freely against each other unless beams close a rigid triangle. A plain
    joint has no settings, angle limits or brain ports.
- **One piece (#930):** the links must join every linked joint into one
  piece. Separate pieces can be saved, but `CreatureReadiness` stops training
  until they are joined or all but one are removed. Servos and sensors join
  nothing.

### Beam

- **Beginner:** A rigid, fixed-length connection between two nodes. It never
  stretches or compresses: think steel rod, not rubber band.
- `BeamDef`. Its length is the distance between its nodes in the drawing,
  and it carries sensors between them.
- **No self-collision:** parts of the same creature never collide with each
  other. That allows car-like, closed-loop construction and keeps shadows
  from touching each other.
- **Weight:** every beam weighs the same, whatever its length (#794); a
  Piston or Spring weighs one and a half beams, its cylinder included
  (#731).
- **Minimum length (#593):** a beam must leave
  `CreatureReadiness.MinimumBeamGap` (52) free between its two joint rings:
  room for the largest sensor picture, the Camera's, with a 4-unit gap on
  each side (#622). A shorter beam can be drawn and saved, but it blocks
  training until its joints move apart.

### Sensor

- **Beginner:** A part on a beam that feels something about that beam and
  feeds it to the brain. It is *not* the brain.
- `SensorDef`: its `SensorKind` and the beam it sits on. A sensor measures
  its own beam at the beam's midpoint, with no position setting, and shows
  as a small picture there (#576).
- **One sensor per beam (#593).** `CreatureDef` rejects a second one of any
  kind.
- **Frame fixed as built:** the side of the beam that faces up in the built
  pose is the sensor's "up", and "along" points right as built. The frame
  then turns with the beam and never flips during a run
  (`Accelerometer.UpSign`).
- Kinds: **Accelerometer** (#127) and **Camera** (#575, #604).

#### Accelerometer

- **Beginner:** A small weight on a spring inside a box. When the beam
  speeds up, slows down or tilts, the weight shifts; that shift is what the
  brain feels. At rest it feels gravity, so it also knows which way is down.
- A proof mass on a damped spring in the beam's sensor frame, driven by the
  beam's specific force at its midpoint (1 g "up" at rest, so its direction
  gives the tilt). Mechanics: `Accelerometer`.
- **Reading:** two brain inputs, along the beam and across it, always in
  −1…1 (`Accelerometer.Reading`). At rest on a level beam it reads about 0
  along and 0.76 across.
- **The spring is the filter:** it smooths spiky per-tick acceleration from
  contacts but still shows an impact as a spike that decays, and gives the
  reading a short memory of recent motion.
- **Deterministic:** the proof mass starts at rest under gravity as built,
  on every build, so the same brain, build and map give the same readings.
  The visual and SignalFlow show the same proof mass the brain reads.
- **There is no speed or elevation sensor:** the brain learns movement from
  acceleration, Piston length and speed, and its own outputs.

#### Camera

- **Beginner:** Three rays that tell the brain how near the ground is. A
  new camera looks level: ahead-and-up, straight ahead and ahead-and-down;
  you can turn it in Build. The nearer the ground a ray sees, the stronger
  its reading.
- **Aim (#594):** `SensorDef.Aim`, the angle of the centre ray from the
  beam's direction (first node to second). The three rays fan
  `CameraRays.Spread` (45°) apart around it and turn with the beam. A new
  or unaimed camera gets `SensorDef.DefaultAim`: level, forward in the world
  as built (#622). Aim is its one setting.
- **Rays:** three rays from the beam's midpoint, `CameraRays.RayLength`
  (220) long, that see the ground only (`CameraRays`).
- **Ray names** are symmetric around the centre ray, seen from the camera
  looking along its rays: **left**, **centre**, **right** (keys `left1`,
  `centre`, `right1`; five rays add **far left** / **far right**, `left2` /
  `right2`). Every ray count the camera will offer (1, 3 or 5, #578) has a
  centre ray, so the inner rays' keys survive a rebuild with another count.
- **Reading:** four inputs (`CameraRays.Read`). First each ray's
  **nearness**, left to right: 0 when nothing is in range and rising
  linearly to 1 at contact (`CameraRays.Reading`); a hit at half range
  reads 0.5. Nothing seen adds nothing to the brain's weighted sum
  (`docs/ML_CONCEPTS.md`). Last, **hit**: 1 when any ray sees the ground,
  else 0, so far ground, whose nearness is almost 0, differs from none
  (#1032). It comes after every ray, whatever the ray count.

### Rigid triangles

Three beams that close a triangle are rigid (three fixed side lengths fix
all three angles), so its joints cannot turn. `RigidTriangles.Of` finds
every such triangle, and Build and Training show them (#627). A larger
truss is a composition of triangles; a bare quadrilateral stays free to
fold.

### Servo

- **Beginner:** A motor on a joint. It holds one link as Fixed and turns
  another as Target; any other links at that joint stay free.
- `ServoDef`; Mechanics: `Servo`. Fixed and Target may be Beams, Pistons or
  Springs. A missing link id is allowed, so deleting a held link keeps the
  Servo, and readiness blocks training until the player picks a
  replacement or deletes the Servo.
- **Angle:** the direction from its joint to the Target link's other joint
  minus the direction to the Fixed link's other joint, relative to the
  built pose. Range and Start position set the lower and upper range ends;
  past either end it is pushed back, harder the further out.
- **Torque:** it chases a wanted speed, ten times the angle still to go and
  at most Max speed, with a push that closes most of the speed gap at once
  and a holding part that slowly gathers what a steady load needs, so it
  can hold weight up to the strength the brain chose (`Servo.NextMotor`).
  Under a sudden load it sags a little, then climbs back within a second or
  two. Its torque builds to that strength over Rise time and drops at once.
- **Settings:** Max strength (torque, N·m in the panel), Range (20°–360°),
  Start position (0–100% inside the range), Max speed (°/s) and Rise time
  (s).

### Piston

- **Beginner:** A powered link between two nodes that pushes them apart or
  pulls them together, in a straight line. The brain chooses how far out it
  goes and how hard it may push.
- `PistonDef`; Mechanics: `Piston`. Settings: **Strength** (its most
  force), **Stroke** (how much the gap between its joints' edges can grow,
  as a share of its shortest), **Start position** (where its drawn length
  sits in that travel, 0% at the shortest, 100% at the longest), **Max
  speed** and **Rise time** (how long its force takes to build to full,
  #801). Its force acts along the line between its nodes.
- **Travel** (#870: a real cylinder; #835: from the joints' edges): it moves
  the gap between its joints' edges, so a Servo's bigger joint shortens it.
  A cylinder is at most as long as the shortest gap, so the gap can at most
  double. The drawn gap sits Start position of the way from the shortest to
  the longest (`Piston.ShortestLength`, `Piston.LongestLength`). Example: two
  plain joints drawn 1 m apart leave a 0.7 m gap; with Stroke 100%, Start
  0% gives 1…1.7 m, 50% 0.77…1.23 m and 100% 0.65…1 m.
- **Force** (`Piston.NextForce`, #801): it chases the length its position
  output asks for, slowing as it arrives, with at most the strength
  output's share of its Strength. The force builds over Rise time from the
  speed it lacks and dies away once it moves as fast as it wants, so it
  brakes before its target instead of swinging past, and does not shake on
  light parts.
- **Not tuned to its load** (#801): the force comes from the Piston's own
  settings and outputs, never from the mass it moves, so a rebuild does not
  change how it responds. A held load sags it a little and a heavy one can
  make it bob; a light load at the shortest Rise time and a low Max speed
  can swing round its target, and a weak Piston with a high Max speed
  overshoots. Those are for the player's settings, the brain and fitness
  (#546).
- **End stops** (#701): its length stays within its travel under ordinary
  loads; a hard impact can give a little before it is pushed back. Inside
  the travel nothing but its own force moves it.
- **Not a beam:** it adds no rigidity but counts as attached for the degree
  rules. Two nodes hold at most one link (Piston or Spring).
- **Minimum length:** the same as a beam's.

### Spring

- **Beginner:** A springy link between two nodes. Stretch or squeeze it
  and it pulls back toward the length it was drawn at; its damping stops it
  bouncing. It has no brain ports: it stores and gives back energy, so a
  Piston's push can bounce, but it never chooses anything. Like a car's
  spring it only moves so far: a new Spring hangs free at its drawn length
  and squeezes to half of it.
- `SpringDef`; Mechanics: `Spring`. Settings: **Stiffness** (N/m),
  **Damping** (N·s/m), **Stroke** (as a Piston's) and
  **Coil length** (where its rest length sits, #835).
- **Travel and rest length** (#835, #974): on the gap between its joints'
  edges, like a Piston's. Coil length moves the rest length evenly from half
  the gap short of the shortest stop to half the gap past the longest, so a
  short Stroke can be pressed as hard against a stop as a long one. Between
  the stops it rests at its drawn length and the stops sit round it as a
  Piston's of the same Stroke round a Start position there, so, as on a
  Piston, the travel is longer the nearer the shortest stop. Past a stop
  the stops stay as a Piston's at that end, Start 0% or 100%, the drawn
  length sits on that stop and the Spring starts pressed against it, harder
  the further out (`Spring.ShortestLength`, `Spring.LongestLength`,
  `Spring.RestLength`). Example: two plain joints drawn 1 m apart (a 0.7 m
  gap) with Stroke 100%: Coil length 0% gives 1…1.7 m resting at 0.65 m,
  50% 0.83…1.35 m and 100% 0.65…1 m resting at 1.35 m. Changing either
  setting never moves a node.
- **End stops** are hard, as a Piston's (#701). **A preload stays inside the
  stop** (#835): as on a real preloaded spring, its nodes feel nothing until
  a load beats the preload. A stop that carries the creature's weight still
  chatters a little and can creep (#935).
- **Damping is a plain coefficient** (#801), not tuned to the mass it
  moves, so the same Damping bounces more on heavy nodes than on light ones.
- **Not a beam:** the same as a Piston.
- **Stiffness range** 50–2000 N/m: firm at the top without making the
  lightest pair of nodes unstable at Training's fixed step (#787).
- **Minimum length:** the same as a beam's.

## Sensor–model contract

- **Ports (#534):** every part that affects the brain declares its brain
  channels as ports, `BrainPort(partId, channel, direction, signal)`. The
  channel is a machine key that never changes; display names are separate.
  An input carries a reading; an output names the physical signal it drives
  (#535). The player sees each port as its part's name and a label
  (`BrainPortLabels`); an output's label is the quantity it sets, so the
  Piston's position output reads **length** like its input (#869).
  - **Accelerometer:** inputs `along`, `across`.
  - **Camera:** inputs `left1`, `centre`, `right1`, `hit` (#1032).
  - **Piston (#451):** inputs `length` (0 at its shortest, 1 at its longest,
    its Start position as drawn, #870) and `speed` (`tanh(v / maxSpeed)`,
    extending positive); outputs `position` and `strength`.
  - **Servo (#452):** inputs `angle` (−1 lower end, 0 built, +1 upper end)
    and `speed` (`tanh(ω / maxSpeed)`, counter-clockwise positive); outputs
    `angle` and `strength`.
  - Nodes, beams and plain joints declare none.
- **Output conventions (#535, `PortSignals`):** the signal fixes the
  output's activation and how a new output starts.
  - **Velocity** (#454) and **position** use `tanh`: −1…1, where 0 means
    stand still, a Servo's built pose, or the middle of a Piston's travel.
  - A Piston's position output maps in a straight line (#870): −1 is its
    shortest and +1 its longest length (`Piston.TargetLength`), so it holds
    its drawn length at `2p − 1`.
  - A Servo's angle output maps piecewise, so 0 stays the built pose even
    when that is off-centre: −1…0 spans lower end…built and 0…1 spans
    built…upper end (`OutputSignals.PositionFromTarget`).
  - **Strength** uses `sigmoid`: 0…1, the share of the part's Strength
    setting used this tick. The setting is the part's maximum, chosen in
    Build; the output is the brain's choice of how much of it to use.
  - **New ports start almost passive:** when a part joins a trained brain,
    its incoming weights are 0 and a new strength output starts at bias −4,
    about 2% force. Sigmoid has no dead zone, so mutation can still raise
    it. Position and velocity outputs start at 0.
  - **A rebuild keeps the brain (#516):** saving a Build edit to a trained
    creature refits its brain to the new ports (`DirectBrain.Refit`). Ports
    match by part id, channel and direction: a kept port keeps its weights
    and bias, a new port starts almost passive, and a removed part's
    neurons and connections are dropped. Moving nodes or changing settings
    keeps every port; a Servo whose links change gets a new id, so its
    weights reset.
  - **Build refits the brain it opened with (#689):** every save in one
    Build visit refits the brain as it was when Build opened, not the last
    saved one, so a part an Undo brings back gets its neurons, ids and
    weights back. A port that brain lacks keeps the neuron ids the last save
    gave it; fresh ids start past both brains' `NextNeuronId`, so no neuron
    id is reused. Generation, latest and best always come from the saved
    training.
- **Order** comes from `BrainPorts.Of`: ports sorted by part id, then in the
  order the part declares them. It depends only on ids, so moving,
  resizing or adding a part never reorders the other ports.

## Editing identity rules

- Every saved part has a positive integer id from the creature's single
  counter (`CreatureDef.NextPartId`), saved with the creature. Creating a
  part takes `NextPartId` and advances it; it never goes down.
- Removing a part retires its id forever, so a part removed and added again
  is a new part. Removing a Node also removes the beams and links on it,
  and removing a Beam removes its sensor; the survivors keep their ids.
- Saving, loading, moving, renaming, rebuilding a body and copying a whole
  Creation keep part ids and the counter.
- Changing a Servo's Fixed or Target link gives it a new id, because the
  meaning and sign of its ports changed, so its old weights are not reused.
- Parts refer to each other by id. Code that needs an array position uses
  `CreatureDef`'s id-to-index lookups at the boundary.

## Worked example: the Walker

`CreationExamples.CreateWalkerCreature` builds the Walker (#745), the only
example, which the first start copies and Training falls back to: a walker
seen from the side, with a leg hanging from each end of its back and two
Pistons crossing between them.

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
  hold the body up and swing the legs.
- **1 accelerometer** on the back.
- **Brain:** 6 inputs (2 from the accelerometer, 2 per Piston) and 4
  outputs (each Piston's position and strength).
- **Why this one (#745):** it shows learning in the first minutes with the
  default Train setup (8 shadows, 10 s runs) and walks upright.
