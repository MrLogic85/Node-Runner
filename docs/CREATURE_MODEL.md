# Creature model

What each part of a Node Runner creature is and does, and the contract
between its parts and the network. Short definitions are in
`docs/GLOSSARY.md`; how parts look is in `docs/WORLD_VISUALS.md`, and
where each can be placed in `docs/BUILD_MODE.md` → "Placing a part".

**The model is not the simulation.** This document describes a part in
words that would hold for any physics engine: no engine body or joint
types, and no rules that differ by how a part happens to be simulated. How
Godot realises each part is in `.claude/rules/creature.md`.

## Parts: Node, Beam, Sensor, Servo, Motor, Stepper, Brake, Angle sensor, Piston, Spring, Wheel, Rubber, Touch sensor

A creature is built from joints (Node) and the links between them (Beam,
Piston, Spring). Sensors sit on beams (Accelerometer, Camera); joint parts
sit on joints: hinge parts that turn the joint (Servo, Motor, Stepper),
resist it (Brake) or feel it (Angle sensor), and the Wheel, Rubber and
Touch sensor. A plain joint is passive: a beam turns freely where it
meets another, and only parts with network ports move the body. Each
setting's slider range, step and unit are in `PartParameters`
(`libs/NodeRunner.App/ViewModels/PartParameters.cs`).

**A sensor is not the network.** What senses (sensors) and what thinks
(the network) stay separate; this is the most important rule here. Each
sensor is one clear idea, the way real sensors are.

**A part sits on what it senses or moves:** a sensor that senses one beam
sits on that beam; a part working between two links, or on the ground
around a joint, sits on that joint; a part joining two joints is a link.

Every part has a stable id (see "Editing identity rules"). Ids are machine
identity, not display or draw order; network ports are sorted by them
(see "Sensor–network contract"). Display names are optional, may repeat
and are never keys.

```
CreatureDef ──build──▶ physical body ──sensors──▶ network ──outputs──▶ powered parts ──force──▶ physical body
   (data)                 (physics)               (control)                                      (physics)
```

### Air drag

Air slows a moving creature by about a tenth of its speed each second,
and its turning more, so a creature that builds up speed, on Wheels for
example, keeps most of it. Add drag only where a real need shows up, and
record the reason here.

### Node

- **Beginner:** A joint: a point where links meet and can turn against
  each other. Joints are where the creature touches the world.
- `NodeDef`. Its radius is not saved but follows from what sits on it: the
  largest of a plain joint's `NodeDef.PlainJointRadius` (15), a joint
  motor's `JointMotor.Radius` (27) and a Wheel's Radius
  (`CreatureDef.NodeRadius`). Collision and bounds use it. Link ends and
  a Piston's or Spring's travel go by the hub instead (`NodeDef.HubRadius`,
  15), whatever sits on the joint; sensors and a link's minimum length go
  by `CreatureDef.CoveredRadius` (Beam → Minimum length).
- **One part per slot:** a joint holds at most one hinge part (a Servo,
  Motor, Stepper, Brake or Angle sensor), one Wheel, one Rubber and one
  Touch sensor. So a Servo can drive the links of a Wheel's joint while the
  Wheel turns freely between them, or drive the Wheel itself (see Servo).
- **Degree rules** (links touching a joint):
  - **0 links:** a loose point. It can be saved, but `CreatureReadiness`
    blocks training until it is connected or removed.
  - **1 link:** a dangling tip.
  - **2+ links:** the links turn freely against each other unless a hinge
    part holds them or beams close a rigid triangle.
- **No settings:** a plain joint has no settings, angle limits or ports.
- **One piece:** the links must join every linked joint into one piece.
  Separate pieces can be saved, but readiness blocks training until they
  are joined. Joint parts and sensors join nothing. After a tear (Beam →
  Tearing) the same rule over the links still attached finds the **main
  piece**: the one with the most joints, the lowest-numbered on a tie
  (`Mechanics.Tear.MainPiece`).

### Beam

- **Beginner:** A rigid, fixed-length connection between two joints. It
  never stretches or compresses: think steel rod, not rubber band.
- `BeamDef`. Its length is the distance between its joints as drawn.
- **No self-collision:** parts of the same creature never collide with
  each other, which allows car-like, closed-loop builds.
- **Weight:** every beam weighs the same whatever its length; a Piston or
  Spring weighs one and a half beams.
- **Minimum length:** a link must leave `CreatureReadiness.MinimumLinkGap`
  (26) free between what is drawn over its two ends
  (`CreatureDef.CoveredRadius`): a joint motor's ring (27), or else the
  hub (15); no other joint part counts. A Piston or Spring adds
  `CreatureReadiness.TelescopeExtra` (16) for its telescope. So a beam is
  at least 56 long between plain joints, 68 with a joint motor on one end
  and 80 with one on each, and a Piston or Spring 72, 84 and 96. The gap
  holds a sensor's picture. A shorter link can be drawn and saved, but it
  blocks training.
- **Tearing:** in Simulate and Training a beam end lets go of its joint
  when the joint has drifted at least `Tear.DriftLimit` (20 units, 0.2 m)
  from it on `Tear.Ticks` (4) ticks in a row, well above anything a sound
  creature reaches. A creature built so its joints give way breaks instead
  of shaking. Build never tears.
  - **One end gone:** the beam dangles from its other joint, and its loose
    end lands on the ground instead of passing through it.
  - **Both ends gone:** the beam leaves the creature and falls on its own,
    landing on the ground.
  - **Main piece:** from then on the creature's centre, bounds and lowest
    point count only the main piece (Node → One piece), so the camera and
    the trial follow it, and anything that came loose falls or rolls away.
  - **Hinge parts:** one whose Fixed or Target beam has let go at either
    end goes passive at once: it stops its torque, a Brake or Stepper lets
    go of its hold, and its inputs keep their last values. A sensor on a
    beam gone at one end keeps sensing; on a beam gone at both ends, its
    inputs keep their last values.
  - The trial's score freezes at the first tear (`docs/TRAINING_LOOP.md` →
    Trial). How a tear looks is `docs/WORLD_VISUALS.md` → Tear.

### Sensor

- **Beginner:** A part on a beam that feels something about that beam and
  feeds it to the network. It is *not* the network.
- `SensorDef`: its `SensorKind` and the beam it sits on, at most one per
  beam. It has no position setting: it sits and senses midway between
  what is drawn over the beam's two ends (`SensorPicture`), so off the
  midpoint only when a joint motor sits on one end.
- **Frame fixed as built:** the side of the beam that faces up in the
  built pose is the sensor's "up", and "along" points right as built. The
  frame turns with the beam and never flips during a run
  (`Accelerometer.UpSign`).
- Kinds: **Accelerometer** and **Camera**.

#### Accelerometer

- **Beginner:** A small weight on a spring inside a box. When the beam
  speeds up, slows down or tilts, the weight shifts; that shift is what the
  network feels. At rest it feels gravity, so it also knows which way is
  down.
- A proof mass on a damped spring in the sensor's frame (Mechanics:
  `Accelerometer`).
- **Reading:** two inputs, along the beam and across it, in −1…1. At rest
  on a level beam it reads about 0 along and 0.76 across.
- **The spring is the filter:** it smooths spiky per-tick contact forces
  but still shows an impact as a decaying spike, and gives the reading a
  short memory of recent motion.
- **Deterministic:** the proof mass starts at rest under gravity as built,
  so the same network, build and map give the same readings.
- **There is no speed or elevation sensor:** the network learns movement
  from acceleration, its parts' readings and its own outputs.

#### Camera

- **Beginner:** Rays that tell the network how near the ground is. A new
  camera has three, looking level: ahead-and-up, straight ahead and
  ahead-and-down. The nearer the ground a ray sees, the stronger its
  reading.
- **Aim:** `SensorDef.Aim`, the angle of the centre ray from the beam's
  direction (first joint to second). The rays turn with the beam. A new
  camera looks level and forward as built (`SensorDef.DefaultAim`).
- **Settings:** **Rays** (`Rays`, 1, 3 or 5; default 3), **Spread**
  (`Spread`, the angle between the outer rays; default 90°) and **Range**
  (`Range`, how far each ray reaches; default 2.2 m).
  - Rays are odd so the **centre** ray always points along the aim. The
    rays sit evenly and symmetrically around it (`CameraRays.RayAngle`).
    Changing the count adds or removes ports, so a locked creation cannot
    change it (`docs/BUILD_MODE.md` → "Locked Build").
- **Rays see the ground only** (`CameraRays`).
- **Ray names** are symmetric around the centre, seen looking along the
  rays: **far left**, **left**, **centre**, **right**, **far right** (keys
  `left2`, `left1`, `centre`, `right1`, `right2`). The inner rays' keys
  survive a change of count, so they keep their weights.
- **Reading:** one input per ray, left to right, then hit. A ray's
  **nearness** is 0 at or beyond its range, rising linearly to 1 at
  contact (`CameraRays.Reading`): a hit at half range reads 0.5. **Hit**
  is 1 when any ray sees the ground, else 0, so far ground, whose nearness
  is almost 0, differs from none. Why nothing seen reads 0 is in
  `docs/ML_CONCEPTS.md` → "Feature engineering / observation design".

### Rigid triangles

Three beams that close a triangle are rigid (three fixed side lengths fix
all three angles), so its joints cannot turn. `RigidTriangles.Of` finds
every such triangle, and Build and Training show them. A larger truss is
a composition of triangles; a bare quadrilateral stays free to fold. A
triangle that loses a beam to a tear can fold again.

### Servo

- **Beginner:** A joint motor that turns to the angle the network picks.
  It holds one link as Fixed and turns another as Target; any other links
  at that joint stay free. It can turn its joint's Wheel too, as if the
  Wheel were a link.
- `ServoDef`; Mechanics: `Servo`.
- **Sides:** Fixed and Target are two different Beams, Pistons or Springs
  on its joint, or the Wheel there. To a hinge part the Wheel is a
  one-sided link whose angle is that of a mark on its tyre, pointing right
  as built (`HingePartSideGeometry.WheelAngle`); everywhere else it stays a
  joint part. A new one takes the two lowest-id links, or a lone link as
  Fixed and the Wheel as Target (`JointMotor.DefaultSides`). Deleting a
  side keeps the Servo with that side empty, and readiness blocks
  training until the player picks another. Every hinge part's sides work
  this way.
- **Angle:** the direction from its joint to the Target side minus the
  direction to the Fixed side, relative to the built pose. Range and Start
  position set the lower and upper ends; past either end it is pushed
  back, harder the further out. Rolling a Wheel on and on is a Motor's
  job.
- **Torque:** it chases a wanted speed, ten times the angle still to go
  and at most Max speed, with a push that closes most of the speed gap at
  once and a holding part that slowly gathers what a steady load needs,
  so it holds weight up to the strength the network chose
  (`Servo.NextMotor`). Under a sudden load it sags a little, then climbs
  back within a second or two. Its torque builds over Rise time and drops
  at once.
- **Settings:** **Max strength** (N·m), **Range** (its total swing),
  **Start position** (where the built angle sits in the range, 0% at the
  lower end), **Max speed** (rpm) and **Rise time** (s).

### Motor (Velocity motor in code)

- **Beginner:** A joint motor that turns at the speed the network chooses,
  round and round if it likes. Like a Servo it holds one side as Fixed and
  turns the other as Target, but the network picks a speed, not an angle.
- `VelocityMotorDef`, saved under `velocityMotors`; Mechanics:
  `VelocityMotor`. Its sides are a Servo's.
- **Angle:** where Target points from Fixed, counter-clockwise positive,
  all the way round. It has no Range and no end stops.
- **Torque:** the Servo's loop (`MotorLoop`) with the wanted speed set
  straight from the network: the speed output times Max speed. It keeps
  that speed against a load up to the strength the network chose; a
  larger load slows it or turns it back.
- **Settings:** **Max strength**, **Max speed** and **Rise time**, as a
  Servo's. Its Max strength tops out at a fifth of a Servo's, so it
  struggles up hills.

### Stepper

- **Beginner:** A joint motor that works like a clock: it holds its joint
  in fixed notches and, as often as the network asks, lets go and pushes
  it on to the next one, either way. Like a Servo it holds one side as
  Fixed and turns the other as Target.
- `StepperDef`, saved under `steppers`; Mechanics: `Stepper`. Its sides
  are a Servo's.
- **Settings:** **Max strength** (torque while stepping, N·m),
  **Notches** (in a full turn, 2–24, saved as `stepsPerTurn`) and **Max
  frequency** (the most steps a second).
- **Notches** are evenly spaced angles between its sides, counted from the
  built angle, so a trial starts locked at a notch. Its angle is measured
  as a Motor's and counts on across whole turns.
- **Frequency:** its one output sets how often it steps, −1…1 of Max
  frequency; its sign is the way, counter-clockwise positive, and 0
  stands still. Its clock is a phase that runs at |frequency| and steps
  each time it wraps. Its tick input reads the phase, a sawtooth from 0 as
  it steps to 1 just before the next, so the network can time other parts
  to its steps.
- **Step:** it lets go and drives the joint to the neighbouring notch with
  at most Max strength, like a clock's tick: full torque, then braking
  just in time to arrive still. The network picks when and which way,
  never how hard. It locks again within 2% of a notch spacing of the
  notch or once past it, so on a heavy joint the overshoot still counts
  one step; the next step aims at the exact notch, so the error never adds
  up. A step that reaches no notch within 0.5 s pulls the joint to the
  nearest notch instead, so a push too weak doesn't count.
- **Lock:** between steps it holds the joint rigid at its notch, network
  or not, as a Locked Brake does.

### Brake

- **Beginner:** A brake on a joint: it stops its two sides turning
  against each other, up to its Strength. It has no network ports and
  draws no power.
- `BrakeDef`, saved under `brakes`; Mechanics: `Brake`. Its sides are a
  Servo's.
- **Settings:** **Strength**, the most torque it resists with (N·m), or
  **Locked** (`BrakeDef.Locked`), an endless Strength.
- **Holding:** friction between its sides, always on, network or not.
  Within Strength the joint stays put; past it the joint slips, slowed by
  the full Strength, and grips again once the push is back within
  Strength. It has no preferred angle: it holds wherever the joint
  stopped. It feels a push a step late, so a sudden heavy push turns the
  joint a few degrees before it holds, and a sudden release kicks it back
  about as far.
- **Locked:** the two sides turn as one, so no push can turn the joint. A
  Piston or Spring side still slides: only the angle is locked. Where the
  sides cannot be held rigid (both are links that slide at the joint), it
  holds as endless friction, which gives a little under a sudden heavy
  push.

### Angle sensor

- **Beginner:** A sensor on a joint that tells the network the angle
  between two of its sides, and how fast it is changing, without moving
  them.
- `AngleSensorDef`, saved under `angleSensors`; Mechanics: `AngleSensor`.
  Its sides are a Servo's. It has no settings and applies no force, so a
  creature moves the same with or without it.
- **Reading:** `angleSin` and `angleCos`, the sine and cosine of the angle
  between its sides, measured as a Motor's, and `speed`,
  `tanh(ω / 2π rad/s)`, counter-clockwise positive. Its speed scale, 60
  rpm (`AngleSensor.SpeedScale`), is its own, so retuning a Motor never
  changes what a trained network reads.

### Wheel

- **Beginner:** A wheel on a joint. It turns freely there and rolls on the
  ground, so a body on wheels can roll instead of walk. It has no network
  ports: on its own it never pushes; something else has to move the body.
- `WheelDef`. It sits on one joint, even a loose one, which then blocks
  training by the degree rules. A Wheel changes no link and moves no
  joint, and Wheels may overlap when built.
- **Settings:** **Radius** 0.4–1 m (the joint's radius, see Node).
- **Grip:** its tyre grips like a bare joint, friction 1, or twice that
  with a Rubber on its joint.
- **Spin:** a free wheel coasts for a long time but does not spin forever.
- **Weight:** 3 kg per metre of Radius, added to its joint
  (`Wheel.Mass`); derived, never saved.
- **Driving it:** a hinge part on its joint may use it as a side (see
  Servo).

### Rubber

- **Beginner:** A rubber skin on a joint. The joint, or its wheel's tyre,
  grips the ground harder, so it slips less when it pushes or brakes. It
  has no network ports: it changes how the body meets the ground, not what
  the network does.
- `RubberDef`; Mechanics: `Rubber`. On a Wheel's joint it is the tyre.
- **Grip:** friction 2 against the ground, twice a bare joint's or a
  tyre's. No bounce, as for every joint.
- **Size:** it adds no radius, weight or collider; its skin,
  `RubberDef.Skin` (3 units) outside the joint, is only drawn and adds no
  tap area. It has no settings.

### Touch sensor

- **Beginner:** A sensor on a joint that tells the network how close that
  joint is to touching the ground.
- `TouchSensorDef`; Mechanics: `Touch`. On a Wheel's joint the tyre
  touching the ground counts.
- **Reading:** its one input, `touch`, is how far the nearest ground
  reaches into its cushion: 0 at the cushion's outer edge,
  `TouchSensorDef.Cushion` (5 units) outside the joint, or farther,
  rising to 1 where the ground meets the joint. The nearest ground in any
  direction counts, so a slope reads as flat ground does. No smoothing:
  the cushion shows what the network gets. Only the ground counts, never
  the creature's own parts or other shadows.
- **Size:** it adds no radius or collider and no tap area. It has no
  settings.

### Piston

- **Beginner:** A powered link that pushes its two joints apart or pulls
  them together, in a straight line. The network chooses how far out it
  goes and how hard it may push.
- `PistonDef`; Mechanics: `Piston`. Settings: **Strength** (its most
  force), **Stroke** (how much the gap between its joints' hubs can grow,
  as a share of its shortest), **Start position** (where its drawn length
  sits in that travel, 0% at the shortest), **Max speed** and **Rise
  time** (how long its force takes to build to full).
- **Travel:** it moves the gap between its joints' hubs, like a real
  cylinder. It is a three-part telescope: its stage and rod each slide out
  at most the shortest gap, so Stroke runs up to 200% and the gap can at
  most triple (`Piston.ShortestLength`, `Piston.LongestLength`). Example:
  two plain joints drawn 1 m apart leave a 0.7 m gap; with Stroke 100%,
  Start 0% gives 1…1.7 m, 50% 0.77…1.23 m and 100% 0.65…1 m.
- **Force** (`Piston.NextForce`): it chases the length its position
  output asks for, slowing as it arrives, with at most the strength
  output's share of its Strength. The force builds over Rise time from the
  speed it lacks and dies away once it moves as fast as it wants, so it
  brakes before its target instead of swinging past.
- **Not tuned to its load:** the force comes from its own settings and
  outputs, never from the mass it moves, so a rebuild does not change how
  it responds. A heavy load sags or bobs it; coping with that is for the
  player's settings, the network and fitness.
- **End stops:** its length stays within its travel under ordinary loads;
  inside the travel nothing but its own force moves it. A stop's force is
  capped at three times what the creature can put on it by itself, all its
  Pistons at full Strength plus its weight (`EndStop.MaxForce`); past
  that, or under a hard impact, the stop gives.
- **Not a beam:** it adds no rigidity but counts as a link for the degree
  rules. Two joints hold at most one link of any kind.

### Spring

- **Beginner:** A springy link between two joints. Stretch or squeeze it
  and it pulls back toward its rest length; its damping stops it
  bouncing. It has no network ports: it stores and gives back energy, so
  a Piston's push can bounce, but it never chooses anything. Like a car's
  spring it only moves so far.
- `SpringDef`; Mechanics: `Spring`. Settings: **Stiffness** (N/m),
  **Damping** (N·s/m), **Stroke** (as a Piston's) and **Coil length**
  (where its rest length sits).
- **Travel and rest length:** its stops sit on the gap between its
  joints' hubs, like a Piston's. Coil length moves the rest length evenly
  from half the gap short of the shortest stop to half the gap past the
  longest, so a short Stroke can be pressed as hard against a stop as a
  long one. Between the stops it rests at its drawn length; past a stop
  the drawn length sits on that stop and the Spring starts pressed against
  it (`Spring.ShortestLength`, `Spring.LongestLength`,
  `Spring.RestLength`). Changing either setting never moves a joint.
- **End stops** are a Piston's. **A preload stays inside the stop:** as on
  a real preloaded spring, its joints feel nothing until a load beats the
  preload. A stop that carries the creature's weight still chatters a
  little and can creep.
- **Damping is a plain coefficient,** not tuned to the mass it moves, so
  the same Damping bounces more on heavy joints than on light ones.
- **Stiffness** tops out at 2000 N/m, firm without making the lightest
  pair of joints unstable at Training's fixed step.
- **Not a beam:** as a Piston.

## Sensor–network contract

- **Ports:** every part that affects the network declares its channels as
  ports, `NetworkPort(partId, channel, direction, signal)`. The channel is
  a machine key that never changes; display names are separate. An input
  carries a reading; an output names the physical signal it drives. The
  player sees each port as its part's name and a label, plus the short
  label and range its port bar shows; an output's label is the quantity it sets, so the
  Piston's position output reads **length** like its input. All of these
  come from the port's entry in its kind's `PartKindDisplays` row; a new
  port must add one, or showing its part throws.
  - **Accelerometer:** inputs `along`, `across`.
  - **Camera:** one input per ray from left to right, then `hit`: `centre`,
    `hit` for one ray; `left1`, `centre`, `right1`, `hit` for three;
    `left2`, `left1`, `centre`, `right1`, `right2`, `hit` for five.
  - **Piston:** inputs `length` (0 at its shortest, 1 at its longest) and
    `speed` (`tanh(v / maxSpeed)`, extending positive); outputs `position`
    and `strength`.
  - **Servo:** inputs `angle` (−1 lower end, 0 built, +1 upper end) and
    `speed` (`tanh(ω / maxSpeed)`, counter-clockwise positive); outputs
    `angle` and `strength`.
  - **Motor:** inputs `angleSin` and `angleCos` (Target a quarter turn
    counter-clockwise from Fixed reads 1 and 0) and `speed` (as a Servo's);
    outputs `speed` (Velocity) and `strength`.
  - **Stepper:** inputs `angleSin`, `angleCos` (as a Motor's) and `tick`
    (see Stepper → Frequency); output `frequency`.
  - **Touch sensor:** input `touch`.
  - **Angle sensor:** inputs `angleSin`, `angleCos` and `speed`; no
    outputs.
  - Joints, beams, Springs, Wheels, Brakes and Rubbers declare none.
- **Output conventions (`PortSignals`):** the signal fixes the output's
  activation and how a new output starts.
  - **Velocity** and **position** use `tanh`: −1…1, where 0 means stand
    still or a Servo's built pose. A Servo's angle output maps piecewise,
    so 0 stays the built pose even when that is off-centre: −1…0 spans
    lower end…built and 0…1 built…upper end
    (`OutputSignals.PositionFromTarget`).
  - **Length** uses `sigmoid`: 0…1, on the same scale as the Piston's
    `length` input, from its shortest to its longest length
    (`Piston.TargetLength`).
  - **Strength** uses `sigmoid`: 0…1, the share of the part's Strength
    setting used this tick. The setting is the part's maximum, chosen in
    Build; the output is the network's choice of how much of it to use.
  - **Frequency** uses `tanh`: −1…1 of a Stepper's Max frequency,
    counter-clockwise positive; 0 stands still.
  - **New ports start almost passive:** when a part joins a trained
    network, its incoming weights are 0 and a new strength output starts
    at bias −4, about 2% force. Position, velocity, length and frequency
    outputs start at bias 0: a length output asks for the middle of its
    travel, and a Stepper stands still. The trained gait therefore never
    breaks; training explores the new port (`docs/TRAINING_LOOP.md` →
    "New ports are explored"). Generation 0 starts its own way
    (`docs/TRAINING_LOOP.md` → Generations → Generation 0).
  - **A rebuild keeps the network:** saving a Build edit to a trained
    creature refits its network to the new ports (`DirectNetwork.Refit`).
    Ports match by part id, channel and direction: a kept port keeps its
    weights and bias, a new port starts almost passive, and a removed
    part's neurons and connections are dropped. Moving joints, changing
    settings or changing a hinge part's sides keeps every port, except a
    Camera's Rays, which add or remove its outer rays' ports.
  - **Build refits the network it opened with:** every save in one Build
    visit refits the network as it was when Build opened, not the last
    saved one, so a part an Undo brings back gets its neurons, ids and
    weights back. Fresh neuron ids start past both networks'
    `NextNeuronId`, so no id is reused.
- **Order** comes from `NetworkPorts.Of`: ports sorted by part id, then in
  the order the part's kind declares them (`PartKinds`). It depends only
  on ids, so moving, resizing or adding a part never reorders the others.

## Editing identity rules

- Every saved part has a positive integer id from the creature's single
  counter (`CreatureDef.NextPartId`), saved with the creature. It never
  goes down.
- Removing a part retires its id forever, so a part removed and added
  again is a new part. Removing a joint also removes the links and joint
  parts on it, and removing a beam removes its sensor; the survivors keep
  their ids.
- Saving, loading, moving, renaming, rebuilding a body and copying a whole
  Creation keep part ids and the counter.
- A hinge part keeps its id through every change of its sides: filling an
  empty one, picking another, swapping the two, or any link replacing the
  link it held. Its ports may change meaning or sign, but the network
  keeps their weights and adapts through training.
- Parts refer to each other by id. Code that needs an array position uses
  `CreatureDef`'s id-to-index lookups at the boundary.

## Worked example: the Walker

`CreationExamples.CreateWalkerCreature` builds the Walker, the example
the first start copies and Training falls back to. Seen from the side, it
has a back beam carrying an Accelerometer, a leg beam hanging from
each end, the feet splayed a little wider than the back, and two Pistons
crossing from each end of the back to the opposite foot. Every joint is
passive; the Pistons hold the body up and swing the legs. Its network has
6 inputs (2 from the Accelerometer, 2 per Piston) and 4 outputs (each
Piston's position and strength). It is the example because it shows
learning in the first minutes with the default Train setup and walks
upright.
