# Creature model

The vocabulary a Node Runner creature is built from. This is the durable
reference for anyone editing creatures, teaching the app, or wiring inspector
UI. Short glossary entries live in `docs/GLOSSARY.md`; the longer-form
descriptions and the code pointers live here.

This model replaces the 0.1.0 Joint/Bone/Muscle prototype. The vocabulary
below was designed by the project owner, not inferred from the old code — if
you are extending it, keep asking "what would the owner want here" rather
than defaulting to what is easiest to implement.

## Four parts: Node, Beam, Core, Motor relation

A creature is built from two structural parts (Node, Beam), one sensor part
(Core), and one derived control concept (Motor relation). Keeping "what
senses" (Core) and "what thinks" (the neural model) conceptually separate is
the most important rule in this document — **a Core is a sensor package, not
the brain.**

```
CreatureDef  ──build──▶  physical body  ──sensors──▶  model  ──outputs──▶  motor relations  ──torque──▶  physical body
   (data)                    (physics)                (control)                                            (physics)
```

### Node

- **Beginner:** A physical attachment point. Beams meet here and can rotate
  relative to each other.
- **Implementation:** `NodeDef` in `libs/NodeRunner.Domain/NodeDef.cs` stores
  a position (`Vector2D`) and a radius. A node has no `RigidBody2D` of its
  own — physically it's just the shared point where beam bodies are pinned
  together (see Beam below).
- **Degree rules** (how many beams touch a node):
  - **0 beams** — invalid. A node with nothing attached is just a loose
    point and cannot be simulated; `CreatureDef`'s constructor rejects it
    before simulation can start.
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
  two node indices (`NodeA`, `NodeB`). At runtime it becomes its own
  `RigidBody2D` in `project/src/creature/Creature.cs`, sized to the distance
  between its two nodes' positions. Beams from the same creature never
  collide with each other (collision exceptions are added pairwise), which
  is what allows car-like, closed-loop construction.
- **Future ideas (not implemented):** beams breaking on hard impact, joints
  tearing apart under load.

### Core

- **Beginner:** A sensor package mounted on a node. It is *not* the brain —
  it's where the model's inputs come from.
- **Implementation:** `CoreDef` in `libs/NodeRunner.Domain/CoreDef.cs` stores
  the node it is mounted on. At runtime `project/src/creature/CoreSensors.cs`
  reads 6 values per core, all relative to the mounting beam's own frame:
  three fixed rays (down, forward, forward-down), pitch (the mounting beam's
  rotation), elevation (height above the world origin), and speed (the
  mounting beam's linear speed).
- A `CreatureDef` can have zero, one, or many cores — the data model does
  not assume exactly one.
- **Future ideas (not implemented):** more rays or additional sensor types
  once a performance budget is known; cores (and nodes/beams generally) as
  an unlockable resource via an achievement/quest progression system.

### Motor relation

- **Beginner:** One controllable rotation between two beams that share a
  node — this is how the model actually moves the body.
- **Implementation:** derived, not stored. `MotorTopology.BuildNodeConnections`
  in `libs/NodeRunner.Domain/MotorTopology.cs` computes, from a
  `CreatureDef`'s topology alone, every physical pin between beams at a node
  (`NodeConnectionDef`), and marks which of those are motorized. At runtime,
  `project/src/creature/MotorRelation.cs` wraps each motorized connection:
  - **Sensors it exposes:** `relativeAngle` (signed, normalized to
    `[-1, 1]` representing `[-180°, +180°]` — never `[0°, 360°]`, to avoid a
    discontinuity at the wrap-around point) and `relativeAngularVelocity`
    (also normalized). Both are genuinely sensor values *going into* the
    model, exactly like a core's rays or pitch — a motor relation is not
    part of a core and a core is not the source of these values.
  - **Output it accepts:** a single `targetAngularVelocity` in `[-1, 1]`,
    scaled by a static `MaxAngularVelocity`.
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
the physical pin still exists (so the triangle stays connected), it just
carries no sensor or brain output, because driving it would either do
nothing or fight the other two pins.

This generalizes to any rigid, triangulated structure (a larger truss is a
composition of triangles), while correctly leaving non-triangulated closed
shapes (e.g. a bare quadrilateral) with their genuine remaining freedom.

## Sensor–model contract

- **Input count** = `(core count × 6) + (motor relation count × 2)`.
- **Output count** = motor relation count.
- **Order matters and is fixed at build time:** every core (in `CreatureDef`
  order) contributes its 6 values first, then every motor relation (in the
  order `MotorTopology` produced it) contributes its 2 values. Output slot
  `i` always drives motor relation `i`. Reordering either side silently
  invalidates a trained brain. See the comment above
  `Creature.ReadSensors()` for the authoritative order.
- A creature with zero motor relations (e.g. a single node/beam) has no
  brain at all — nothing to control, nothing to sense from motor relations.

## Worked example: the 0.2.0 hardcoded creature

`project/src/creature/HardcodedCreatureFactory.cs` builds the first concrete
`CreatureDef` under this model — the same 5-node chain shape as the old
0.1.0 worm, reinterpreted:

```
     beam      beam      beam      beam
   ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐
   │      │ │      │ │      │ │      │
  (N0)───(N1)───(N2)───(N3)───(N4)
   core
```

- **5 nodes** (`N0`-`N4`) spaced 56 units apart, radius 18.
- **4 beams**, one per adjacent pair.
- **1 core**, mounted on `N0`.
- **Node degrees:** `N0` and `N4` have 1 beam each (passive ends); `N1`,
  `N2`, `N3` each have 2 beams, giving 3 motor relations total — no closed
  loops, so no triangle exclusions apply here.
- **Sensors:** `1 core × 6` + `3 motor relations × 2` = 12.
- **Brain outputs:** 3, one per motor relation.

## What this model does not cover yet

These are deliberate future ideas, not oversights — do not build them
without a fresh design conversation:

- Beams breaking on impact; joints tearing apart under load.
- Collision between a creature's own parts (currently always disabled).
- Node/Beam/Core as unlockable resources via an achievement/quest
  progression system, rather than unlimited from the start.
- More than 3 rays, or additional sensor types, once ray-casting
  performance is a known quantity (especially on Android).
- Exposing `MaxTorque`/`MaxAngularVelocity` as player- or
  upgrade-configurable settings, rather than fixed constants.
- Whether `relativeAngularVelocity` should always be included as a sensor,
  or made optional/experimental — 0.2.0 includes it; this may be revisited
  once training exists and the difference is measurable.

If you propose adding one of these, update this document first and then the
code.
