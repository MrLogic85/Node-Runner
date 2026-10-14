# Creature model

What each part of a Node Runner creature is and does, and the contract
between its parts and the brain. Short definitions are in
`docs/GLOSSARY.md`; how parts look is in `docs/WORLD_VISUALS.md`.

**The model is not the simulation** (#452). This document describes a part
in words that would hold for any physics engine: no engine body or joint
types, and no rules that differ by how a part happens to be simulated. How
Godot realises each part is in `project/src/creature/AGENTS.md` and the code
comments there.

## Parts: Node, Beam, Sensor, Servo, Motor, Stepper, Brake, Angle sensor, Piston, Spring, Wheel, Rubber, Touch sensor

A creature is built from two structural parts (Node, Beam), sensor parts
that sit on beams (the Accelerometer and the Camera), joint parts (the
Servo, the Motor and the Stepper, which turn their joint, the Brake, which resists
that, the Angle sensor, which feels it, the Wheel, which rolls, the Rubber, which grips, and the Touch sensor, which feels the ground) and links between two nodes
(the Piston and the Spring). Plain joints
are passive (#450): a beam turns freely where it meets another, and only
parts with brain ports move the body. "What senses" (sensors) and "what
thinks" (the brain) stay separate; this is the most important rule here:
**a sensor is not the brain.**

**A part sits on what it senses or moves** (#127): a sensor senses one body,
so it sits on a beam, or on a joint, a body of its own (Touch sensor); a
part acting or sensing between two beams, or on the ground around a joint, sits on that
joint (Servo, Motor, Stepper, Brake, Angle sensor, Wheel, Rubber); one joining two nodes is a link (Piston, Spring). Each sensor is
one clear idea, the way real sensors are.

Every part has a stable id (see "Editing identity rules"). Ids are machine
identity only: not display, draw or brain-port order, and not names.
Display names are optional, may repeat and are never keys.

**Air drag** (#1112): air slows a moving creature by about a tenth of its
speed each second, and slows its turning more, so a creature that builds up
speed, on Wheels for example, keeps most of it. A Wheel's spin barely slows
(see Wheel). Drag is added only where a real need shows up, with the reason
recorded here.

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  model  ──outputs──▶  servos + pistons  ──force──▶  physical body
   (data)                    (physics)                (control)                                (physics)
```

### Node

- **Beginner:** A physical attachment point. Links meet here and can rotate
  relative to each other. Nodes are where the creature touches the world.
- `NodeDef`. Its radius is not saved but follows from what is on the joint
  (#626, #129): the largest of the plain joint's `NodeDef.PlainJointRadius`
  (15) and each part on it, a joint motor's
  `JointMotor.Radius` (27) or a Wheel's Radius (`CreatureDef.NodeRadius`). Collision and bounds use it.
  A Rubber or a Touch sensor adds none. Taps, sensors and links do not use it (#1086):
  a joint is tapped at its hub or
  joint motor, and a sensor and a link's minimum length go by what covers
  its ends (`CreatureDef.CoveredRadius`; Beam → Minimum length). A Piston's
  or Spring's travel measures from hub to hub, `NodeDef.HubRadius` (15)
  from each joint's centre, whatever sits on the joint.
- **One part per slot** (#1044): a joint holds at most one Wheel, one
  hinge part (a Servo, Motor, Stepper, Brake or Angle sensor, #1204, #1036), one Rubber (#1142) and one Touch sensor
  (#1043), so a Servo can drive the links of a Wheel's joint while the
  Wheel turns freely between them, or drive the Wheel itself (see Servo),
  and a Rubber and a Touch sensor may share its joint with both.
- **Degree rules** (links touching a node, counting Beams, Pistons and
  Springs):
  - **0 links:** a loose point that cannot be simulated. It can be saved,
    but `CreatureReadiness` stops training until it is connected or removed.
  - **1 link:** a dangling tip.
  - **2+ links:** a passive joint unless a joint motor sits on it: the links turn
    freely against each other unless beams close a rigid triangle. A plain
    joint has no settings, angle limits or brain ports.
- **One piece (#930):** the links must join every linked joint into one
  piece. Separate pieces can be saved, but `CreatureReadiness` stops training
  until they are joined or all but one are removed. Joint motors and sensors join
  nothing. After a tear (Beam → Tearing) the same rule over the links still
  attached finds the **main piece**: the one with the most joints, the
  lowest-numbered on a tie (`Mechanics.Tear.MainPiece`).

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
- **Minimum length (#593, #1212, #1210):** a link must leave
  `CreatureReadiness.MinimumLinkGap` (26) free between what is drawn over
  its two ends (`CreatureDef.CoveredRadius`): a joint motor's ring (27), or
  else the hub (15). A Wheel, Brake, Touch sensor or Rubber adds nothing; a
  Wheel lies under its links. The link type may add more
  (`CreatureReadiness.MinimumLength`): a beam adds nothing, and a Piston or
  Spring adds `CreatureReadiness.TelescopeExtra` (16) for its telescope
  (`docs/WORLD_VISUALS.md` → Piston). So a beam is at least 56 long centre to
  centre between plain joints, 68 with a joint motor on one end and 80 with
  one on each, and a Piston or Spring 72, 84 and 96. The
  owner halved the free gap, which had room for the whole Camera picture
  (#622, #1212); the picture is centred in the gap and reaches about 9
  units under each end (#1086). A Servo's Fixed-link housing stub may take
  part of the gap (`docs/WORLD_VISUALS.md` → Servo). A shorter link can be
  drawn and saved, but it blocks training until its joints move apart.
- **Tearing (#962, #1256):** in Simulate and Training a beam end lets go of
  its joint when the joint has drifted at least `Tear.DriftLimit` (20 units,
  0.2 m) from it on `Tear.Ticks` (4) ticks in a row, the limit #1255 chose
  well above anything a sound creature reaches. A creature built so its
  joints give way breaks instead of shaking. Ends are checked in beam
  order, the first end before the second, every tick.
  - **One end gone:** the beam dangles from its other joint.
  - **Both ends gone:** the beam leaves the creature at once: it is hidden
    and no longer moves.
  - **Main piece:** from then on the creature's centre, bounds and lowest
    point count only the main piece (One piece above), so the camera and
    the trial follow it, and a joint or Wheel that came loose falls or rolls
    away on its own.
  - **Joint motors and sensors:** a Servo, Motor, Stepper, Brake or Angle
    sensor whose Fixed or Target beam has let go at either end goes passive
    in the same tick: it stops its torque, a Brake or Stepper lets go of its
    weld, and its inputs keep the last values read before the tear. A sensor on a beam
    that has let go at one end keeps sensing; on a beam gone at both ends,
    its inputs keep their last values.
  - The trial's score freezes at the first tear (`docs/TRAINING_LOOP.md` →
    Trial), and a new trial starts whole. Build never tears.

### Sensor

- **Beginner:** A part on a beam that feels something about that beam and
  feeds it to the brain. It is *not* the brain.
- `SensorDef`: its `SensorKind` and the beam it sits on. A sensor measures
  its own beam where it sits, with no position setting, and shows as a small
  picture there (#576): midway between what is drawn over the beam's two
  ends, a joint motor or else the hub (`CreatureDef.CoveredRadius`), so
  `(d + c1 − c2) / 2` from joint 1's centre for joints `d` apart covered
  `c1` and `c2` (#1086, `SensorPicture.Shift`). That is the midpoint unless
  a joint motor sits on one end. A Wheel does not move it: the Wheel is
  see-through under its links.
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
  beam's specific force where it sits (1 g "up" at rest, so its direction
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

- **Beginner:** Rays that tell the brain how near the ground is. A new
  camera has three, looking level: ahead-and-up, straight ahead and
  ahead-and-down; you can turn it and change its rays, spread and range in
  Build. The nearer the ground a ray sees, the stronger its reading.
- **Aim (#594):** `SensorDef.Aim`, the angle of the centre ray from the
  beam's direction (first node to second). The rays turn with the beam. A
  new or unaimed camera gets `SensorDef.DefaultAim`: level, forward in the
  world as built (#622). It is set with a canvas handle, not the panel.
- **Settings (#578):** three panel sliders, each saved on the `SensorDef`.
  A Camera saved without them gets the defaults, which are the fan every
  camera had before, so old creatures see exactly as before.

  | Setting | Field | Values | Default |
  |---|---|---|---|
  | **Rays** | `Rays` | 1, 3 or 5 | 3 |
  | **Spread** | `Spread` | 15°–90° | 90° |
  | **Range** | `Range` | 1.0–10.0 m (100–1000 world units) | 2.2 m (220) |

  - **Rays** are odd so one ray, the **centre**, always points along the
    aim. Changing the count adds or removes ports, so a locked creation
    cannot change it (`docs/BUILD_MODE.md` → "Locked Build").
  - **Spread** is the angle between the outer rays; the rays sit evenly
    spaced and symmetric around the aim (`CameraRays.RayAngle`), so three
    rays at 90° are 45° apart. With one ray it has no effect.
  - **Range** is how far each ray reaches from the camera. A Worm
    beam is 0.7 m, so the range runs from a little over one beam to about
    fourteen (#1097). The sliders' steps are in `docs/BUILD_MODE.md` →
    "Parameters (#704)".
  - Build and import keep these within the sliders
    (`CreatureBuilder.IsWithinBuildLimits`); `SensorDef` itself accepts any
    odd count up to 5, a spread up to 180° and any positive range.
- **Rays:** from where the camera sits, as long as the camera's range; they
  see the ground only (`CameraRays`).
- **Ray names** are symmetric around the centre ray, seen from the camera
  looking along its rays: **far left**, **left**, **centre**, **right**,
  **far right** (keys `left2`, `left1`, `centre`, `right1`, `right2`). One
  ray is just `centre`, three add `left1` / `right1`, five add `left2` /
  `right2`. The inner rays' keys survive a change of count, so they keep
  their weights (#516).
- **Reading:** one input per ray, then hit (`CameraRays.Read`). First each
  ray's **nearness**, left to right: 0 at or beyond the camera's own range
  and rising linearly to 1 at contact (`CameraRays.Reading`); a hit at half
  its range reads 0.5. Nothing seen adds nothing to the brain's weighted
  sum (`docs/ML_CONCEPTS.md`). Last, **hit**: 1 when any ray sees the
  ground, else 0, so far ground, whose nearness is almost 0, differs from
  none (#1032). It comes after every ray, whatever the ray count.

### Rigid triangles

Three beams that close a triangle are rigid (three fixed side lengths fix
all three angles), so its joints cannot turn. `RigidTriangles.Of` finds
every such triangle, and Build and Training show them (#627). A larger
truss is a composition of triangles; a bare quadrilateral stays free to
fold. A triangle that loses a beam to a tear (Beam → Tearing) can fold
again, so it loses its hatch at once.

A triangle is only as rigid as the joints that pin its beams to their
nodes; how the simulation keeps them from giving way under load is in
`project/src/creature/AGENTS.md` → Bodies and joints.

### Servo

- **Beginner:** A joint motor that turns to the angle the brain picks. It holds one link as Fixed and turns
  another as Target; any other links at that joint stay free. It can turn
  its joint's Wheel too, as if the Wheel were a link.
- `ServoDef`; Mechanics: `Servo`. Fixed and Target may be Beams, Pistons or
  Springs, or the Wheel on the Servo's joint (#1118): to a joint motor the Wheel
  is a one-sided link, its angle that of a mark on its tyre, pointing right
  on screen as built (`HingePartSideGeometry.WheelAngle`), so the same
  link ids name it and saves need no change. Everywhere else the Wheel
  stays a joint part: it counts toward no link rule. A Servo
  needs two sides, two links or a link and the Wheel
  (`JointMotor.HasTwoSides`); a new one takes the two lowest-id links, or a
  lone link as Fixed and the Wheel as Target (`JointMotor.DefaultSides`).
  Within its Range it turns the Wheel back and forth; rolling on and on is
  a Motor's job. A missing link id is allowed, so deleting
  a held link or Wheel keeps the Servo, and readiness blocks training
  until the player picks a replacement or deletes the Servo.
- **Angle:** the direction from its joint to the Target link's other joint
  (or the Wheel's tyre mark) minus the direction to the Fixed one, relative to the
  built pose. Range and Start position set the lower and upper range ends;
  past either end it is pushed back, harder the further out.
- **Torque:** it chases a wanted speed, ten times the angle still to go and
  at most Max speed, with a push that closes most of the speed gap at once
  and a holding part that slowly gathers what a steady load needs, so it
  can hold weight up to the strength the brain chose (`Servo.NextMotor`).
  Under a sudden load it sags a little, then climbs back within a second or
  two. Its torque builds to that strength over Rise time and drops at once.
- **Settings:** **Max strength** (torque, N·m), **Range** (its total
  swing, °), **Start position** (where the built angle sits in the range,
  0% at the lower end, 100% at the upper), **Max speed** (rpm) and **Rise
  time** (s). Their ranges are in `docs/BUILD_MODE.md` → Parameters.

### Motor (Velocity motor in code)

- **Beginner:** A joint motor that turns at the speed the brain
  chooses, round and round if it likes. Like a Servo it holds one side as
  Fixed and turns the other as Target, but the brain picks a speed, not an
  angle.
- The code and save format call it the Velocity motor: `VelocityMotorDef`
  (#454), saved under `velocityMotors`; Mechanics: `VelocityMotor`. Its sides follow
  the Servo's rules: a Beam, Piston, Spring or its joint's Wheel, two
  different ones, either missing after a deletion (`JointMotor`). It takes
  the joint's hinge-part slot (see Node → One part per slot).
- **Angle:** the angle between its sides (#1207): the Target side's
  direction minus the Fixed side's, counter-clockwise positive, measured as
  a Servo's sides are. Unlike a Servo's, it is not taken from the built
  pose, so a Motor reads where Target points from Fixed all the way round.
  It has no Range and no end stops.
- **Torque:** the Servo's loop (`MotorLoop`), with the wanted speed set
  straight from the brain: the speed output times Max speed
  (`VelocityMotor.NextMotor`). It keeps that speed against a load up to the
  strength the brain chose; a larger load slows it or turns it back.
- **Settings:** **Max strength** (torque, N·m), **Max speed** (rpm) and
  **Rise time** (s, as a Servo's); their ranges are in `docs/BUILD_MODE.md`
  → Parameters. Its Max strength tops out at a fifth of a Servo's, so it
  struggles up hills.

### Stepper

- **Beginner:** A joint motor that works like a clock: it holds its joint
  in fixed notches and, as often as the brain asks, lets go and pushes it
  on to the next one. Like a Servo it holds one side as Fixed and turns
  the other as Target.
- `StepperDef` (#1036), saved under `steppers`. Its sides follow the
  Servo's rules and it takes the joint's hinge-part slot (see Servo, and
  Node → One part per slot). It is saved, loaded, shared and has brain
  ports, and steps in the simulation (#1037); placing it in Build comes
  with #1038. `docs/WORLD_VISUALS.md` → Parts says how it is drawn.
- **Settings:** **Max strength** (torque while stepping, N·m), **Steps
  per turn** (notches in a full turn, 2–24) and **Max frequency** (the
  most steps each second); their slider ranges are in `docs/BUILD_MODE.md`
  → Parameters. Its ports are in Sensor–model contract.
- **Notches:** Steps per turn evenly spaced angles between its sides,
  counted from the built angle, so a trial starts locked at a notch. Its
  angle is measured as a Motor's and counts on across whole turns.
- **Frequency:** its running frequency follows the Frequency output with
  a time constant of 1 s, so a brain can't jerk it, and its frequency
  input reads it. At that rate it lets go, at most once a physics step.
- **Release:** each time it lets go it unlocks, picks the neighbouring
  notch in Power's direction and drives the joint there with at most
  |Power| × Max strength, like a clock's tick: full torque, then braking
  just in time, tick by tick, to arrive still. It has no top speed; Max
  strength alone sets how quick a step is (a new Stepper steps a light
  joint in a few physics steps). Power changing sign mid-release doesn't
  turn it round. There is no dead zone: at Power 0 it still lets go, and
  the joint turns freely. The release ends, and the Stepper locks, once
  the joint is within 2 % of a notch spacing of that notch, or has passed
  it, or the one behind if a load stronger than the push carries it back.
  It never measures how heavy the joint is, so on a joint heavier than it
  reads (a foot on the ground) it brakes late; it then locks where the
  joint passed the notch, a little past it, and the overshoot still
  counts one step. The next step drives to the exact notch angle, so the
  error never adds up. If it reaches neither notch within 0.5 s, it pulls
  the joint with Max strength to the nearest notch, so a push too weak
  doesn't count a step, and locks it there. If the next release comes due
  first, that nearest notch counts as reached and the next step goes on
  from it.
- **Lock:** between releases it holds the joint locked, brain or not
  (`Mechanics.Stepper`): Godot welds its sides rigid as they stand, as a
  Locked Brake's (`Creature.Links.cs`, `Welder`), so no load turns it.
  Where no weld fits it holds as endless friction instead, as a Locked
  Brake does then (see Brake).

### Brake

- **Beginner:** A brake on a joint: it stops its two sides turning against
  each other, up to its Strength (#456). It has no brain ports and draws no power.
- `BrakeDef` (#1039), saved under `brakes`: it is saved, loaded, validated,
  shared and simulated (#1040), and placed in Build (#1041). Its
  sides and hinge-part slot are a Servo's (see Servo, and Node → One part per slot),
  so one with a missing side blocks training like a Servo. Unlike a joint
  motor it has no ports; Editing identity rules says what that means for
  its id.
- **Settings:** **Strength**, the most torque it resists with (N·m), or
  **Locked** (`BrakeDef.Locked`), an endless Strength.
- **Holding:** friction between its sides, always on, brain or not
  (`Mechanics.Brake`). Each step it stops the turning between them and
  holds against everything else pushing on the joint, with at most its
  Strength. Within Strength the joint stays put; past it the joint slips,
  slowed by the full Strength, and grips again once the push is within
  Strength. It has no preferred angle: it holds wherever the joint
  stopped. It feels a push a step late, so a sudden heavy push turns the
  joint a few degrees before it holds, and a sudden release kicks it back
  about as far.
- **Locked:** the two sides are welded together and turn as one, so no
  push can turn the joint. A Piston or Spring side still slides: only the
  angle is locked. When both sides are Pistons or Springs that slide at the
  joint, it holds as endless friction instead, which gives a little under a
  sudden heavy push.

### Angle sensor

- **Beginner:** A sensor on a joint that tells the brain the angle between
  two of its sides, links or its Wheel, and how fast it is changing,
  without moving them.
- `AngleSensorDef` (#1204), saved under `angleSensors`; Mechanics:
  `AngleSensor`. Its sides and hinge-part slot are a Servo's (see Servo, and Node →
  One part per slot), so one with a missing side blocks training like a
  Servo, and a new one takes the same default sides. It is not a joint
  motor: it has no settings and no torque, and applies no force, so a
  creature moves the same with or without it.
- **Reading:** inputs `angleSin` and `angleCos`, the sine and cosine of
  the angle between its sides, measured as a Motor's (see Motor → Angle,
  #1207), and `speed`, `tanh(ω / 2π rad/s)`, counter-clockwise positive.
  Its speed scale, 60 rpm (`AngleSensor.SpeedScale`), is its own: it
  does not follow any Motor's Max speed, so retuning a Motor never changes
  what a trained brain reads.

### Wheel

- **Beginner:** A wheel on a joint. It turns freely there and rolls on the
  ground, so a body on wheels can roll instead of walk. It has no brain
  ports: on its own it never pushes; something else has to move the body.
- `WheelDef` (#129). It sits on one joint. Build takes any joint, even a
  loose one, which is drawn `danger` and blocks training by the degree
  rules (see Node). The links keep their own rules: a Wheel changes no link
  and moves no node, and Wheels may overlap when built.
- **Settings:** **Radius** 0.4–1 m (the joint's radius, see Node).
- **Grip:** its tyre grips like a bare joint, friction 1 (#1144), or
  twice that with a Rubber on its joint (see Rubber).
- **Spin:** a free wheel coasts for a long time but does not spin
  forever.
- **Weight** (Mechanics: `Wheel.Mass`): 3 kg per metre of Radius, 1.2 kg
  at 0.4 m, added to its joint. Derived, never saved.
- **Driving it:** a joint motor on its joint may name it as
  Fixed or Target (#1118, see Servo), and so may a Brake or an Angle sensor.

### Rubber

- **Beginner:** A rubber skin on a joint. The joint, or its wheel's tyre,
  grips the ground harder, so it slips less when it pushes or brakes. It
  has no brain ports: it changes how the body meets the ground, not what
  the brain does.
- `RubberDef` (#1142). It sits on one joint, at most one per joint, and
  may share it with the other joint parts (see Node → One part per slot).
  A Wheel's tyre is its joint's, so a Rubber on a Wheel's joint is its tyre.
- **Grip** (Mechanics: `Rubber.Friction`): friction 2 against the ground,
  twice a bare joint's or a tyre's 1. No bounce, as for every joint.
- **Size:** it adds no radius, weight or collider (see Node); its skin,
  3 units (`RubberDef.Skin`) outside the joint, is only drawn and adds no
  tap area. It has no settings.
- How it is placed and drawn: `docs/BUILD_MODE.md` → Rubber and
  `docs/WORLD_VISUALS.md` → Rubber.

### Touch sensor

- **Beginner:** A sensor on a joint that tells the brain how close that
  joint is to touching the ground.
- `TouchSensorDef` (#665, #1043). It sits on one joint, at most one per
  joint, and may share it with the other joint parts (see Node → One part
  per slot). A Wheel's tyre is its joint's, so a tyre on the ground counts.
- **Reading:** its one input, `touch`, is how far the nearest ground
  reaches into its cushion (#1045, `Touch.Reading` in Mechanics): 0 at the
  cushion's outer edge, 5 units (`TouchSensorDef.Cushion`) outside the
  joint, or farther, rising to 1 where the ground meets the joint. The
  nearest ground in any direction counts, so a slope or step reads as flat
  ground does. No smoothing: the cushion shows what the brain gets. Only the ground counts, never the creature's own parts or other
  shadows. A freshly built or reset body reads 0 until it senses the ground.
- **Size:** it adds no radius and does not change the joint's collider (see
  Node); its cushion only sets how far it feels, and adds no tap area. It
  has no settings.
- How it is placed and drawn: `docs/BUILD_MODE.md` → Touch sensor and
  `docs/WORLD_VISUALS.md` → Touch sensor.

### Piston

- **Beginner:** A powered link between two nodes that pushes them apart or
  pulls them together, in a straight line. The brain chooses how far out it
  goes and how hard it may push.
- `PistonDef`; Mechanics: `Piston`. Settings: **Strength** (its most
  force), **Stroke** (how much the gap between its joints' hubs can grow,
  as a share of its shortest), **Start position** (where its drawn length
  sits in that travel, 0% at the shortest, 100% at the longest), **Max
  speed** and **Rise time** (how long its force takes to build to full,
  #801). Its force acts along the line between its nodes.
- **Travel** (#870: a real cylinder; #835, #1086: from the joints' hubs): it
  moves the gap between its joints' hubs, `Travel.HubRadii` short of its
  length whatever sits on its joints.
  It is a three-part telescope (#1208): its stage and rod each slide out at
  most the shortest gap, so Stroke runs up to 200% and the gap can at most
  triple (#1211). The drawn gap sits Start position of the way from the
  shortest to the longest (`Piston.ShortestLength`, `Piston.LongestLength`).
  Example: two plain joints drawn 1 m apart leave a 0.7 m gap; with Stroke
  100%, Start 0% gives 1…1.7 m, 50% 0.77…1.23 m and 100% 0.65…1 m; with
  Stroke 200%, 1…2.4 m, 0.65…1.35 m and 0.53…1 m.
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
  loads; a hard impact can give a little before it is pushed back, more
  past the cap below. Inside the travel nothing but its own force moves it.
- **A stop's force is capped** (#1115) at three times what the creature
  can put on it by itself: all its Pistons at full Strength plus its
  weight. Past that the stop slides. Example: a creature of 2.4 kg with one
  150 N Piston holds 3 × (150 + 23.5) = 520 N.
- **Not a beam:** it adds no rigidity but counts as attached for the degree
  rules. Two nodes hold at most one link (Piston or Spring).
- **Minimum length:** a link's (Beam → Minimum length, #1212, #1210).

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
  hubs, like a Piston's. Coil length moves the rest length evenly from half
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
  50% 0.83…1.35 m and 100% 0.65…1 m resting at 1.35 m; with Stroke 200%,
  0% gives 1…2.4 m resting at 0.65 m, 50% 0.77…1.7 m and 100% 0.53…1 m
  resting at 1.35 m. Changing either
  setting never moves a node.
- **End stops** are hard and capped, as a Piston's (#701, #1115). **A preload stays inside the
  stop** (#835): as on a real preloaded spring, its nodes feel nothing until
  a load beats the preload. A stop that carries the creature's weight still
  chatters a little and can creep (#935).
- **Damping is a plain coefficient** (#801), not tuned to the mass it
  moves, so the same Damping bounces more on heavy nodes than on light ones.
- **Not a beam:** the same as a Piston.
- **Stiffness range** 50–2000 N/m: firm at the top without making the
  lightest pair of nodes unstable at Training's fixed step (#787).
- **Minimum length:** a link's (Beam → Minimum length, #1212, #1210).

## Sensor–model contract

- **Ports (#534):** every part that affects the brain declares its brain
  channels as ports, `BrainPort(partId, channel, direction, signal)`. The
  channel is a machine key that never changes; display names are separate.
  An input carries a reading; an output names the physical signal it drives
  (#535). The player sees each port as its part's name and a label; an
  output's label is the quantity it sets, so the Piston's position output
  reads **length** like its input (#869). The Training part callout's port
  bars (#1064) also show each port with a short label, such as "speed", and
  the range its value spans, −1…1 or 0…1. All three come from the port's
  entry in its kind's `PartKindDisplays` row (#1223), with the kind's title
  and default name; a new port must add an entry there, or showing its
  part throws.
  - **Accelerometer:** inputs `along`, `across`.
  - **Camera:** one input per ray from left to right, then `hit` (#1032,
    #578): `centre`, `hit` for one ray; `left1`, `centre`, `right1`, `hit`
    for three; `left2`, `left1`, `centre`, `right1`, `right2`, `hit` for
    five.
  - **Piston (#451):** inputs `length` (0 at its shortest, 1 at its longest,
    its Start position as drawn, #870) and `speed` (`tanh(v / maxSpeed)`,
    extending positive); outputs `position` and `strength`.
  - **Servo (#452):** inputs `angle` (−1 lower end, 0 built, +1 upper end)
    and `speed` (`tanh(ω / maxSpeed)`, counter-clockwise positive); outputs
    `angle` and `strength`.
  - **Motor (#454):** inputs `angleSin` and `angleCos` (the sine
    and cosine of the angle between its sides, #1207, so a full turn never
    jumps; Target a quarter turn counter-clockwise from Fixed reads 1 and 0)
    and `speed` (as a Servo's); outputs `speed` (Velocity,
    −1…1 of Max speed, counter-clockwise positive) and `strength`.
  - **Stepper (#1036):** inputs `angleSin` and `angleCos` (as a Motor's)
    and `frequency` (0…1 of Max frequency, the rate it runs at); outputs
    `frequency` (Frequency) and `power` (Power).
  - **Touch sensor (#1043):** input `touch` (Touch sensor → Reading).
  - **Angle sensor (#1204):** inputs `angleSin`, `angleCos` and `speed`
    (Angle sensor → Reading); no outputs.
  - Nodes, beams, plain joints, Springs, Wheels, Brakes and Rubbers declare
    none.
- **Output conventions (#535, `PortSignals`):** the signal fixes the
  output's activation and how a new output starts.
  - **Velocity** (#454) and **position** use `tanh`: −1…1, where 0 means
    stand still or a Servo's built pose.
  - **Length** (#1129) uses `sigmoid`: 0…1, on the same scale as the
    Piston's `length` input. A Piston's position output drives it and maps
    in a straight line (#870): 0 is its shortest and 1 its longest length
    (`Piston.TargetLength`), so it holds its drawn length at its Start.
  - A Servo's angle output maps piecewise, so 0 stays the built pose even
    when that is off-centre: −1…0 spans lower end…built and 0…1 spans
    built…upper end (`OutputSignals.PositionFromTarget`).
  - **Strength** uses `sigmoid`: 0…1, the share of the part's Strength
    setting used this tick. The setting is the part's maximum, chosen in
    Build; the output is the brain's choice of how much of it to use.
  - **Frequency** (#1036) uses `sigmoid`: 0…1 of a Stepper's Max
    frequency.
  - **Power** (#1036) uses `tanh`: −1…1, the share of a Stepper's Max
    strength it pushes with each step, counter-clockwise positive.
  - **New ports start almost passive:** when a part joins a trained brain,
    its incoming weights are 0 and a new strength or frequency output starts
    at bias −4, about 2% force or 2% of Max frequency. Position, velocity,
    length and power outputs start at bias 0: a length output then asks for
    the middle of its travel. Generation 0 starts its own way
    (`docs/TRAINING_LOOP.md` → Generations → Generation 0). The saved brain
    keeps this start so the trained gait never breaks; training redraws the
    new port's genes in every child instead of mutating them, until one
    beats the elite (#1180, `docs/TRAINING_LOOP.md` → "New ports are
    explored").
  - **A rebuild keeps the brain (#516):** saving a Build edit to a trained
    creature refits its brain to the new ports (`DirectBrain.Refit`). Ports
    match by part id, channel and direction: a kept port keeps its weights
    and bias, a new port starts almost passive, and a removed part's
    neurons and connections are dropped. Moving nodes, changing settings or
    changing a joint motor's or Angle sensor's sides keeps every port, except a Camera's Rays,
    which add or remove its outer rays' ports while the rays it keeps keep
    their weights (#578).
  - **Build refits the brain it opened with (#689):** every save in one
    Build visit refits the brain as it was when Build opened, not the last
    saved one, so a part an Undo brings back gets its neurons, ids and
    weights back. A port that brain lacks keeps the neuron ids the last save
    gave it; fresh ids start past both brains' `NextNeuronId`, so no neuron
    id is reused. Generation, latest and best always come from the saved
    training.
- **Order** comes from `BrainPorts.Of`: ports sorted by part id, then in the
  order the part declares them (its kind's entry in `PartKinds`). It depends only on ids, so moving,
  resizing or adding a part never reorders the other ports.

## Editing identity rules

- Every saved part has a positive integer id from the creature's single
  counter (`CreatureDef.NextPartId`), saved with the creature. Creating a
  part takes `NextPartId` and advances it; it never goes down.
- Removing a part retires its id forever, so a part removed and added again
  is a new part. Removing a Node also removes the beams, links, joint
  parts and Touch sensor on it, and removing a Beam removes its sensor; the survivors keep
  their ids.
- Saving, loading, moving, renaming, rebuilding a body and copying a whole
  Creation keep part ids and the counter.
- A joint motor, Brake or Angle sensor keeps its id through every change of its Fixed or
  Target side: filling an empty one, picking another link or the Wheel,
  swapping the two, or any link replacing the link it held (#1176, #1177).
  A motor's or Angle sensor's ports may change meaning or sign, but the
  brain keeps their weights and adapts through training.
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
