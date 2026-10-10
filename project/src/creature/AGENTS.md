# AGENTS.md — `src/creature/`

The Godot side of a creature: bodies, joints, sensors, Servos, Pistons,
Springs, Wheels and the brain wiring. `docs/CREATURE_MODEL.md` owns the model; this
file owns how Godot realises it.

## Rules

1. **Built from `CreatureDef`.** The def is the source of truth; `Creature`
   turns it into bodies. No `[Export]` for anything a def sets.
2. **No game logic.** No evolution, fitness or UI: given a def and a brain,
   become a physical thing that reacts to forces and drives its parts.
3. **Bodies and joints.** Each node and each beam is its own `RigidBody2D`
   (`CanSleep = false`), and a `PinJoint2D` pins every beam to its two
   nodes, so beams are rigid by geometry, not springs. Only nodes collide (a
   circle each); beams and links have no collider. A link's weight sits on
   its nodes; a beam keeps half its own (`docs/CREATURE_MODEL.md` → Beam).
4. **End stops.** Each Piston and Spring has a hidden, collider-free
   cylinder body pinned to node A and a `GrooveJoint2D` to node B that holds
   its length between the stops (`Creature.CreateEndStops`, #701). Anything
   that moves or resets every body must include the cylinders.
5. **Springs.** A Spring is a `DampedSpringJoint2D` between its nodes
   (#453), which has no stops of its own, so it gets the cylinder above.
   Each tick `SpringLink` sets its rest length to `Spring.StepRestLength`:
   Godot's spring would push a preload past a stop into the stop's joints,
   which gave way (#835). Behaviour: `docs/CREATURE_MODEL.md` → Spring.
6. **Servos.** `ServoJoint` applies equal-and-opposite force couples
   (`Servo.CoupleForTorque`) to the joint body and each link's far body,
   plus a soft end-stop torque outside its range. No Godot angular joint:
   Pistons and Springs slide on a groove, and force couples work for every
   link kind without link-kind branches. The lever arm is clamped to
   `Servo.MinimumLeverArm` so a compressed Spring cannot make unbounded
   force. Motor and stop are integrated explicitly, so the gains are scaled
   to the live inertia (`Servo.LinkInertia`, `Servo.StableEndStopGains`) to
   stay stable; the holding part is not capped, so a Servo holds a load up
   to its strength.
7. **Accelerometer.** `AccelerometerSensor` differences its beam's midpoint
   velocity each tick into specific force and steps the Mechanics
   `Accelerometer`, which substeps at most `Accelerometer.MaxSubstep`
   (1/60 s), so Build's longer frame steps stay stable.
8. **Brain order is `BrainPorts.Of`.** The creature copies each part's value
   to its port's place; never hand-order brain slots here.
9. **No allocations in the tick.** Reuse the sensor, output and scratch
   buffers; keep `_PhysicsProcess` a short sequence of named steps.
10. **Every visual declares its shadow drawing.** Each visual implements
    `IShadowVisual` (static `AsShadow`, an `IsShadow` switch);
    `Creature.IsShadow` sets them all, and `ShadowDrawingTests`
    (`NodeRunner.Ui.Tests`) checks each one. Shadows are drawn in the
    `ArenaShadows` viewport chosen by `ArenaVisibility` layers, because a
    `CanvasGroup` lets the parts' own draw layers escape. The look is in
    `docs/WORLD_VISUALS.md` → Drawing as a shadow.
11. **A Wheel is its joint's body, nothing new.** Unlock that body's
    rotation and give it the Wheel's weight, inertia and material; add no
    body or joint of its own (`Creature.Wheels.cs` says how).

## What lives here

- `Creature.cs`, `Creature.Links.cs`, `Creature.Wheels.cs` — builds bodies,
  pins, sensors, links (each with its end-stop cylinder) and Wheels, and
  wires the brain
- `Creature.Selection.cs` — what a tap selects and each part's draw group,
  with the selection raised (#1107)
- `IBeamSensor.cs` — what `Creature` needs from a sensor: value names,
  `Read`, `Reset`
- `AccelerometerSensor.cs`, `CameraSensor.cs` — the sensors (a camera is
  one `RayCast2D` child per ray, aimed and sized by `CameraRays`)
- `ServoJoint.cs`, `PistonLink.cs`, `SpringLink.cs` — the driven parts
- `*Visual.cs` — rendering only: Training's adapters over the shared part
  visuals in `project/src/theme` (#767); `ShadowDrawing.cs` holds
  `IShadowVisual`
- `project/scenes/Creature.tscn` — the scene template

## Tests

`docs/MANUAL_TESTING.md` decides when a physics change needs manual
testing; physics feel usually does. Pure sums move to
`libs/NodeRunner.Mechanics` and are unit tested there.
