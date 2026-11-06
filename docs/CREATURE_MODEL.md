# Creature model

The rules every part of a Node Runner creature follows, the choices only
one part makes, and the contract between parts and the network. The parts
are in `docs/GLOSSARY.md` → "Creature anatomy"; how they look is in
`docs/WORLD_VISUALS.md`, and where each can be placed in
`docs/BUILD_MODE.md` → "Placing a part". Each part's behaviour as built is
its Mechanics class (`libs/NodeRunner.Mechanics/`), its settings its
`*Def` and `PartParameters`, and its ports `NetworkPorts`.

**The model is not the simulation.** This document describes a part in
words that would hold for any physics engine: no engine body or joint
types, and no rules that differ by how a part happens to be simulated. How
Godot realises each part is in `.claude/rules/creature.md`.

```
CreatureDef ──build──▶ physical body ──sensors──▶ network ──outputs──▶ powered parts ──force──▶ physical body
   (data)                 (physics)               (control)                                      (physics)
```

## Parts

Every part follows these rules.

- **A sensor is not the network.** What senses (sensors) and what thinks
  (the network) stay separate; this is the most important rule here. Each
  sensor is one clear idea, the way real sensors are.
- **A part sits on what it senses or moves:** a sensor that senses one
  beam sits on that beam; a part working between two links, or on the
  ground around a joint, sits on that joint; a part joining two joints is
  a link. The Memory cell senses nothing and sits on a beam only to have a
  place on the creature.
- **One part per slot.** A joint holds at most one hinge part, one Wheel,
  one Rubber and one Touch sensor, and a beam one sensor or Memory cell
  (`CreatureDef` enforces both). Build lets two joints hold one link of any
  kind (`CreatureBuilder`). So a Servo can drive the links of a Wheel's
  joint while the Wheel turns freely between them, or drive the Wheel
  itself.
- **A hinge part's sides.** Fixed and Target are two different sides on
  its joint: Beams, Pistons or Springs, or the Wheel there as one of them.
  To a hinge part the Wheel is a one-sided link pointing along a mark on
  its tyre; everywhere else it stays a joint part. A hinge part works on
  the angle between its sides only, so a Piston or Spring side still
  slides along its length, even under a Locked Brake. A new one takes the two
  lowest-id links, or a lone link as Fixed and the Wheel as Target
  (`JointMotor.DefaultSides`). Deleting a side keeps the part with that
  side empty, and readiness blocks training until the player picks
  another.
- **Only parts with network ports move the body.** A plain joint is
  passive: a beam turns freely where it meets another. A part with no
  ports (Brake, Wheel, Rubber, Spring) changes how the body moves or meets
  the ground, never what it chooses. A part with ports declares them as
  "Sensor–network contract" says.
- **Readings.** An angle that can go all the way round reads as its sine
  and cosine, which never jump (Motor, Stepper, Angle sensor); a Servo,
  which has ends, reads −1 at its lower end, 0 as built and 1 at its
  upper end. A speed reads as `tanh` of the speed over a scale, 0 at rest,
  counter-clockwise or extending positive.
- **A trial starts in the built pose.** No setting moves a joint: a
  Wheel's Radius changes its joint's radius, not where it is. A part with
  a range places it around the built pose: a Servo by Range and Start
  position, a Piston by Stroke and Start position, a Spring by Stroke and
  Coil length (so a preloaded Spring starts pressed against its stop), a
  Stepper's notches from the built angle; a Motor's angle counts from it.
  A part with state starts it the same way every trial and never saves
  it: the Accelerometer's mass at rest under gravity as built, the
  Memory cell's `m` at 0, the Stepper locked at a notch.
- **Which radius counts.** A joint's radius is not saved but follows from
  what sits on it: the largest of a plain joint's, a joint motor's and a
  Wheel's Radius (`CreatureDef.NodeRadius`). Collision and bounds go by it.
  Link ends and a Piston's or Spring's travel go by the hub
  (`NodeDef.HubRadius`), whatever sits on the joint; a sensor's place and a
  link's minimum length go by what is drawn over the end
  (`CreatureDef.CoveredRadius`). A Rubber and a Touch sensor add no
  radius, collider, weight or tap area.
- **Contact.** Every joint, and a Wheel's tyre, meets the ground with
  friction 1 and no bounce; a Rubber doubles the friction.
- **What a tear does** to each kind of part is in "Body" → Tearing; what
  is saved, and how ids behave, in "Editing identity rules". Each
  setting's slider range, step and unit are in `PartParameters`.

## Body

- **Degree rules** (links touching a joint):
  - **0 links:** a loose point. It can be saved, but `CreatureReadiness`
    blocks training until it is connected or removed.
  - **1 link:** a dangling tip.
  - **2+ links:** the links turn freely against each other unless a hinge
    part holds them or beams close a rigid triangle.
- **One piece:** the links must join every linked joint into one piece.
  Separate pieces can be saved, but readiness blocks training until they
  are joined. Joint parts and sensors join nothing.
- **No self-collision:** parts of the same creature never collide with
  each other, which allows car-like, closed-loop builds.
- **Weight:** every beam weighs the same whatever its length; a Piston or
  Spring weighs one and a half beams; a Wheel adds its weight to its
  joint, derived from its Radius and never saved.
- **Minimum length:** a link must leave `CreatureReadiness.MinimumLinkGap`
  free between what is drawn over its two ends, a joint motor's ring or
  else the hub; no other joint part counts. A Piston or Spring adds
  `CreatureReadiness.TelescopeExtra` for its telescope. The gap holds a
  sensor's picture. A shorter link can be drawn and saved, but it blocks
  training.
- **Rigid triangles:** three beams that close a triangle are rigid (three
  fixed side lengths fix all three angles), so its joints cannot turn
  (`RigidTriangles.Of`); Build and Training show them. A larger truss is
  a composition of triangles; a bare quadrilateral stays free to fold. A
  triangle that loses a beam to a tear can fold again.
- **Tearing:** in Simulate and Training a beam end lets go of its joint
  when the joint has drifted from it for a few ticks in a row, well above
  anything a sound creature reaches (`Tear`). A creature built so its
  joints give way breaks instead of shaking. Build never tears.
  - **One end gone:** the beam dangles from its other joint, and its loose
    end lands on the ground instead of passing through it.
  - **Both ends gone:** the beam leaves the creature and falls on its own,
    landing on the ground.
  - **Main piece:** from then on the creature's centre, bounds and lowest
    point count only the main piece, the one with the most joints
    (`Tear.MainPiece`), so the camera and the trial follow it, and
    anything that came loose falls or rolls away.
  - **Hinge parts:** one whose Fixed or Target beam has let go at either
    end goes passive at once: it stops its torque, a Brake or Stepper lets
    go of its hold, and its inputs keep their last values.
  - **Sensors:** one on a beam gone at one end keeps sensing; on a beam
    gone at both ends, its inputs keep their last values. A tear does not
    affect a Memory cell.
  - The trial's score freezes at the first tear (`docs/TRAINING_LOOP.md` →
    Trial). How a tear looks is `docs/WORLD_VISUALS.md` → "Parts".

### Air drag

Air slows a moving creature by about a tenth of its speed each second,
and its turning more, so a creature that builds up speed, on Wheels for
example, keeps most of it. Add drag only where a real need shows up, and
record the reason here.

## Part decisions

Choices for one part, which other parts need not follow.

- **Beam sensors (Accelerometer, Camera):** a sensor has no position
  setting: it sits and senses midway between what is drawn over its
  beam's two ends (`SensorPicture`), so off the midpoint only when a
  joint motor sits on one end. The side of the beam that faces up as
  built is its "up", and "along" points right as built; the frame turns
  with the beam and never flips during a run (`Accelerometer.UpSign`).
- **Accelerometer:** a proof mass on a damped spring, so at rest it feels
  gravity and knows which way is down. The spring is the filter: it
  smooths spiky per-tick contact forces but still shows an impact as a
  decaying spike, and gives the reading a short memory of recent motion.
- **Camera:** its rays see the ground only. The ray count is odd, so the
  centre ray always points along the aim. Ray keys are symmetric around
  the centre, so the inner rays' keys survive a change of count and keep
  their weights; a change of count adds or removes ports, so a locked
  creation cannot change it (`docs/BUILD_MODE.md` → "Locked Build"). The
  hit input tells far ground, whose nearness is almost 0, from none. Why
  nothing seen reads 0 is in `docs/ML_CONCEPTS.md` → "Feature engineering
  / observation design".
- **Memory cell:** a note the network writes to itself: one number, `m`,
  it moves towards a target and reads back every tick, so it can act on
  what happened a moment ago. The loop runs outside the network, which
  stays feed-forward. At full write `m` settles over
  `MemoryCell.WriteTime`, like a Piston's force over its Rise time. A new
  cell changes nothing: its target starts at 0 and `m`
  at 0, so `m` stays exactly 0 until training moves its weights. Write is
  the rate `m` moves at, not a Strength.
- **Servo:** its angle is the direction to the Target side minus the
  direction to the Fixed side, relative to the built pose; past either end
  of its range it is pushed back, harder the further out. Rolling a Wheel
  on and on is a Motor's job. It holds a load up to the strength the
  network chose: under a sudden load it sags a little, then climbs back
  within a second or two. Its torque builds over Rise time and drops at
  once.
- **Motor** (Velocity motor in code): a Servo's loop with the wanted speed
  set straight from the network; no Range and no end stops. Its Max
  strength tops out at a fifth of a Servo's, so it struggles up hills; a
  load past its strength slows it or turns it back.
- **Stepper:** a clock. Between steps it holds its joint rigid at a notch,
  network or not, as a Locked Brake does. The network picks when and
  which way, never how hard: a step is full torque, then braking just in
  time to arrive still. An overshoot still counts one step, and the next
  step aims at the exact notch, so the error never adds up; a push too
  weak to reach a notch in time pulls back to the nearest one, so it
  doesn't count. Its tick input lets the network time other parts to its
  steps.
- **Brake:** no ports and no power; friction between its sides, always
  on. It has no preferred angle: it holds wherever the joint stopped, and
  past its Strength the joint slips, slowed by the full Strength. It feels
  a push a step late, so a sudden heavy push turns the joint a few degrees
  before it holds. **Locked** turns the two sides as one: only the angle
  is locked, so a Piston or Spring side still slides; where the sides
  cannot be held rigid it holds as endless friction, which gives a little
  under a sudden heavy push.
- **Angle sensor:** applies no force, so a creature moves the same with or
  without it. On the same sides it reads the same angle as a Motor, but
  its speed scale is its own (`AngleSensor.SpeedScale`), so retuning a
  Motor never changes what a trained network reads.
- **Wheel:** it turns freely on its joint and rolls; on its own it never
  pushes. It may sit on a loose joint, which then blocks training by the
  degree rules. It changes no link, and Wheels may overlap when built. A
  free Wheel coasts for a long time but does not spin forever.
- **Rubber:** a skin that only grips; on a Wheel's joint it is the tyre.
  Its skin is drawn outside the joint (`RubberDef.Skin`) but adds nothing
  else (see "Which radius counts").
- **Touch sensor:** reads how far the nearest ground, in any direction,
  reaches into its cushion (`TouchSensorDef.Cushion`), so a slope reads as
  flat ground does; on a Wheel's joint the tyre touching counts. No
  smoothing: the cushion shows what the network gets. Only the ground
  counts, never the creature's own parts or other shadows.
- **Piston:** it moves the gap between its joints' hubs, like a real
  cylinder. A three-part telescope, so its Stroke runs up to 200% and the
  gap can at most triple (`Piston.ShortestLength`, `Piston.LongestLength`).
  Its force chases the length its position output asks for and brakes
  before it instead of swinging past. It is not tuned to its load: the
  force comes from its own settings and outputs, never from the mass it
  moves, so a rebuild does not change how it responds; coping with a heavy
  load is for the player's settings, the network and fitness. Inside its
  travel nothing but its own force moves it; its end stops hold up to a
  few times what the creature can put on them by itself (`EndStop`), and
  past that, or under a hard impact, they give. It adds no rigidity but
  counts as a link for the degree rules.
- **Spring:** stores and gives back energy, so a Piston's push can bounce,
  but it never chooses anything. Its stops sit on the hub gap, as a
  Piston's; its rest length is its drawn length unless Coil length puts it
  past a stop (`Spring`). A preload stays inside the stop: as on a real
  preloaded spring, its joints feel nothing until a load beats it. A stop
  that carries the creature's weight still chatters a little and can
  creep. Damping is a plain coefficient, not tuned to the mass it moves,
  so the same Damping bounces more on heavy joints than on light ones.
  Stiffness tops out where the lightest pair of joints stays stable at
  Training's fixed step. It adds no rigidity, as a Piston.

## Sensor–network contract

- **Ports:** every part that affects the network declares its channels as
  ports, `NetworkPort(partId, channel, direction, signal)`, in
  `NetworkPorts`. The channel is a machine key that never changes; display
  names are separate. An input carries a reading; an output names the
  physical signal it drives. The player sees each port as its part's name
  and a label, plus the short label and range its port bar shows; an
  output's label is the quantity it sets, so the Piston's position output
  reads **length** like its input. All of these come from the port's
  entry in its kind's `PartKindDisplays` row; a new port must add one, or
  showing its part throws.
- **Output conventions (`PortSignals`):** the signal fixes the output's
  activation and how a new output starts.
  - **Velocity** and **position** use `tanh`: −1…1, where 0 means stand
    still or a Servo's built pose. A Servo's angle output maps piecewise,
    so 0 stays the built pose even when that is off-centre
    (`OutputSignals.PositionFromTarget`).
  - **Length** uses `sigmoid`: 0…1, on the same scale as the Piston's
    `length` input, from its shortest to its longest length.
  - **Strength** uses `sigmoid`: 0…1, the share of the part's Strength
    setting used this tick. The setting is the part's maximum, chosen in
    Build; the output is the network's choice of how much of it to use.
  - **Frequency** uses `tanh`: −1…1 of a Stepper's Max frequency,
    counter-clockwise positive; 0 stands still.
  - A Memory cell's **target** uses `dampedTanh` and its **write**
    `dampedSigmoid`, the sum divided by a damping before `tanh` or
    `sigmoid` (`PortSignals.TanhDamping` and `SigmoidDamping`), so they can
    rest between their ends instead of saturating.
  - **New ports start almost passive:** when a part joins a trained
    network, its incoming weights are 0, and a new strength or write
    output starts at a bias that gives about 2% of its force or rate.
    Position, velocity, length and frequency outputs start at bias 0: a
    length output asks for the middle of its travel, and a Stepper stands
    still. The trained gait therefore never breaks; training explores the
    new port (`docs/TRAINING_LOOP.md` → "New ports are explored").
    Generation 0 starts its own way (`docs/TRAINING_LOOP.md` → Generations
    → Generation 0).
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
  goes down. Ids are machine identity, not display or draw order. Display
  names are optional, may repeat and are never keys.
- Removing a part retires its id forever, so a part removed and added
  again is a new part. Removing a joint also removes the links and joint
  parts on it, and removing a beam removes its sensor or Memory cell; the
  survivors keep their ids.
- Saving, loading, moving, renaming, rebuilding a body and copying a whole
  Creation keep part ids and the counter.
- A hinge part keeps its id through every change of its sides: filling an
  empty one, picking another, swapping the two, or any link replacing the
  link it held. Its ports may change meaning or sign, but the network
  keeps their weights and adapts through training.
- Parts refer to each other by id. Code that needs an array position uses
  `CreatureDef`'s id-to-index lookups at the boundary.

## The Walker

`CreationExamples.CreateWalkerCreature` builds the Walker, the example the
first start copies and Training falls back to. It is the example because
it shows learning in the first minutes with the default Train setup and
walks upright; a change to it keeps that.
