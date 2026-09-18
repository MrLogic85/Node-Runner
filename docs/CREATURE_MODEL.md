# Creature model

The vocabulary a Node Runner creature is built from. This is the durable
reference for anyone editing creatures, teaching the app, or wiring inspector
UI. Short glossary entries live in `docs/GLOSSARY.md`; the longer-form
descriptions and the code pointers live here.

The 0.1.0 worm is the first concrete instance of this model, not a special
one-off scene. Everything the future editor (roadmap 0.3.0) will produce must
fit inside the same model.

## Three layers: structure, physics, control

A creature is deliberately split into three layers. Keeping them apart is
what lets the editor, the simulation, and the brain evolve independently.

1. **Structure (data)** — the shape of the creature as pure data. Lives in
   `libs/NodeRunner.Domain/` as `CreatureDef`. No physics, no rendering, no
   brain. Just "what are the parts and how are they connected".
2. **Physics (bodies and forces)** — how the structure becomes a moving
   thing on screen. Lives in `project/src/creature/` (`Creature.cs`,
   `Muscle.cs`, joint visuals). Godot's `RigidBody2D` for joints and
   `DampedSpringJoint2D` for every connection (bones use a stiff spring,
   muscles use a softer spring the brain modulates) do the actual
   simulation.
3. **Control (sensing and acting)** — how a brain observes the body and
   drives its muscles. Sensors read from the physics layer, the brain maps
   them to muscle targets, and muscles turn those targets into forces.

The simplest way to read it:

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  brain  ──outputs──▶  muscles  ──forces──▶  physical body
   (data)                    (physics)                (control)                                    (physics)
```

## The parts

Each part has two descriptions: a **beginner-facing** one for the inspector
UI and teaching material, and an **implementation-facing** one that points at
the actual code. The GLOSSARY entry is intentionally short; this file is the
long form.

### Joint

- **Beginner:** A point on the creature. It has weight, it can move, and
  other parts attach to it. Think of it as a knot in a rope.
- **Implementation:** `JointDef` in `libs/NodeRunner.Domain/JointDef.cs`
  stores a position (`Vector2D`) and a radius. At runtime it becomes a
  Godot `RigidBody2D` inside `project/src/creature/Creature.cs`. By
  convention the *first* joint in `CreatureDef.Joints` is the head; visuals
  can key off that.

### Bone

- **Beginner:** A stiff stick between two joints. It tries hard to keep
  them the same distance apart, but it's not perfectly rigid — think of a
  thick rubber band, not steel.
- **Implementation:** `BoneDef` in `libs/NodeRunner.Domain/BoneDef.cs`
  stores two joint indices (`JointA`, `JointB`) into
  `CreatureDef.Joints`. At runtime `project/src/creature/Creature.cs`
  realizes each bone as a stiff `DampedSpringJoint2D` (currently
  `stiffness: 85, damping: 12`) between the two `RigidBody2D`s. Bones and
  muscles use the same underlying spring type; a bone is just a "muscle
  with no controller" — passive, never receives a brain output.

### Muscle

- **Beginner:** A springy connection between two joints that the brain can
  squeeze or stretch. Muscles are how the creature *does* anything.
- **Implementation:** `MuscleDef` in `libs/NodeRunner.Domain/MuscleDef.cs`
  stores two joint indices, a `RestLength`, and a `MaxForce`. At runtime
  `project/src/creature/Muscle.cs` wraps a `DampedSpringJoint2D` and
  exposes `ApplyTarget(double t)` with `t ∈ [-1, 1]`, where `t` scales rest
  length and applies a small bending force. Muscles are the only parts a
  brain can actuate.

### Sensor

- **Beginner:** Something the creature can *feel* about itself, expressed
  as a single number the brain can read. "How bent is my back? How fast am
  I turning?"
- **Implementation:** `project/src/creature/Sensors.cs` produces a
  `double[]` of scalars in a deterministic order. In 0.1.0 that order is
  `[sin(clock), cos(clock), (angle₀, angularVelocity₀), (angle₁,
  angularVelocity₁), …]` — a shared oscillator followed by two channels per
  joint. Sensors are the *only* thing the brain sees.

### Brain input

- **Beginner:** One of the numbers the brain reads at each tick.
- **Implementation:** A single slot in the neural network's input vector.
  Brain inputs are populated one-to-one from the sensor buffer produced by
  `Sensors.Read()`. `Sensors` owns the *meaning* (which joint, which
  channel); the brain owns the *math* (matrix multiplication + activation).
  Changing sensor count changes the brain input count.

### Brain output

- **Beginner:** One of the numbers the brain writes each tick to control
  the body.
- **Implementation:** A single slot in the neural network's output vector.
  By design, output `i` drives muscle `i`: it is meant to be passed as the
  `t` to muscle `i`'s `ApplyTarget(t)` (clamped to `[-1, 1]` at the muscle
  boundary). Output count therefore equals muscle count.

  **0.1.0 caveat:** the untrained brain is too quiet to visibly move a
  worm, so `Creature._PhysicsProcess` currently blends each brain output
  with a per-muscle sinusoidal twitch —
  `t = clamp(brainOutᵢ · 0.45 + sin(phaseᵢ) · 0.85, −1, 1)` — before
  handing it to the muscle. The twitch dominates today; it is a visibility
  hack meant to disappear once training exists. The contract described
  above is still what the model *is*; readers checking the code should
  expect that mixing until then.

### CreatureDef

- **Beginner:** The recipe for one creature — its joints, bones, muscles,
  and nothing else.
- **Implementation:** `CreatureDef` in
  `libs/NodeRunner.Domain/CreatureDef.cs` is a `sealed record` holding
  read-only lists of `JointDef`, `BoneDef`, and `MuscleDef`. Its constructor
  enforces the invariants (at least two joints, every bone and muscle
  references existing joint indices). `CreatureDef` is the "genome of the
  body" and is distinct from the *brain's* genome (the weight vector).
  Anything that saves, loads, edits, or spawns a creature works through a
  `CreatureDef`.

## Sensor–brain–muscle contract

The rule that ties the three layers together:

- **Input count** of the brain equals `Sensors.Count` for the creature it's
  attached to.
- **Output count** of the brain equals `CreatureDef.Muscles.Count`.
- **Order matters.** Sensor slot `i` always means the same thing across
  ticks; output slot `i` always drives the same muscle. Reordering either
  side silently invalidates a trained brain.

This is the design contract. In 0.1.0 the muscle target is
`clamp(brainOutᵢ · 0.45 + twitchᵢ · 0.85, −1, 1)` (see the Brain output
section above); the twitch overlay is a temporary visibility hack, not
part of the model.

This contract is why the future editor (0.3.0) will regenerate a brain
whenever the sensor set or muscle list changes: the input/output shape is
derived from the body, not chosen by the user.

## Worked example: the 0.1.0 worm

`project/src/creature/HardcodedWormFactory.cs` builds the first concrete
`CreatureDef`. It looks like this:

```
     bone      bone      bone      bone
   ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐
   │      │ │      │ │      │ │      │
  (J0)───(J1)───(J2)───(J3)───(J4)
   │      │ │      │ │      │ │      │
   └──M0──┘ └──M1──┘ └──M2──┘ └──M3──┘
    muscle   muscle   muscle   muscle
```

- **5 joints** (`J0`–`J4`) spaced 56 units apart along the x-axis,
  radius 18. `J0` is the head by convention.
- **4 bones**, one per adjacent pair, keeping the worm from stretching
  apart.
- **4 muscles**, colocated with each bone; each muscle can shorten or
  lengthen its segment and applies a small bending force.
- **Sensors:** `Sensors.Count = 2 + 2·5 = 12` — an oscillator clock
  (`sin`, `cos`) plus `(angle, angular velocity)` per joint.
- **Brain inputs:** 12, one per sensor slot.
- **Brain outputs:** 4, one per muscle.
- **Result:** the same body model that 0.3.0's editor will target. The
  worm is not a special scene; it's an early `CreatureDef` with a hardcoded
  factory.

## Control loop, one tick

At each fixed-step tick (`_PhysicsProcess`):

```
   physics state
        │
        ▼
   Sensors.Read()   ──▶  double[] observation  (length = Sensors.Count)
                                │
                                ▼
                        Brain.Forward()
                                │
                                ▼
                        double[] action        (length = Muscles.Count)
                                │
                                ▼
                Muscle.ApplyTarget(t) per muscle
                                │
                                ▼
                        forces applied
                                │
                                ▼
                Godot physics integrates
```

The loop is deterministic and allocation-free in the hot path — the
`observation` and `action` arrays are reused every tick.

In 0.1.0 an extra step sits between `action` and `ApplyTarget`: the
twitch blend documented above. Once the brain is trained (0.4.0+),
`action[i]` will be passed straight to `Muscle[i]`.

## What this model does *not* cover (yet)

- **Global sensors** (ground contact, ray casts, goal direction) are
  reserved for a later milestone. When they arrive they extend the sensor
  order; they do not change the vocabulary.
- **Non-muscle actuators** (thrusters, grippers) are not part of the model
  today.
- **Body-level state** (energy, damage, age) belongs to the simulation, not
  the creature model.

If you propose adding one of these, update this document first and then
the code.
