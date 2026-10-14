# AGENTS.md — `src/creature/`

The Godot side of a creature: bodies, joints, sensors, Servos, Velocity motors, Pistons,
Springs, Wheels and the brain wiring. `docs/CREATURE_MODEL.md` owns the model; this
file owns how Godot realises it.

## Rules

1. **Built from `CreatureDef`.** The def is the source of truth; `Creature`
   turns it into bodies. No `[Export]` for anything a def sets.
2. **No game logic.** No evolution, fitness or UI: given a def and a brain,
   become a physical thing that reacts to forces and drives its parts.
3. **Bodies and joints.** Each node and each beam is its own `RigidBody2D`
   (`CanSleep = false`), and a `PinJoint2D` pins every beam to its two
   nodes, so beams are rigid by geometry, not springs. Those pins hold only
   as well as the solver converges: `project.godot` sets
   `physics/2d/solver/solver_iterations=24`, as Godot's default 16 let a
   creature of 19 nodes and 37 beams stretch its beams up to 26 % and turn
   rigid triangles inside out; at 24 they stretch at most about 4 %, for
   about 5 % more time per physics tick on a phone (#1229, #1245). Long
   trusses still bend (#1229). Only nodes collide (a
   circle each); beams and links have no collider. No body uses continuous
   collision detection (#1171): the ground is an endless half-plane that a
   node cannot tunnel through, and Godot's ray CCD sets a fast body's
   velocity outright, which launched rolling creatures. Revisit this when
   grounds get shapes (#91). A link's weight sits on
   its nodes; a beam keeps half its own (`docs/CREATURE_MODEL.md` → Beam).
   Bodies set no `LinearDamp` or `AngularDamp` of their own, so only the
   project's physics defaults slow them (`docs/CREATURE_MODEL.md` → Air
   drag, #1112); a Wheel's spin is the one exception (`Creature.Wheels.cs`).
4. **End stops.** Each Piston and Spring has a hidden, collider-free
   cylinder body pinned to node A and a `GrooveJoint2D` to node B that holds
   its length between the stops (`Creature.CreateEndStops`, #701). Anything
   that moves or resets every body must include the cylinders. Each groove's
   force is capped at `Mechanics.EndStop.MaxForce` (`CapEndStops`, #1115):
   Godot's solver cannot settle stops that close a loop with beams, and an
   uncapped stop stores a push that throws the creature into the air.
5. **Springs.** A Spring is a `DampedSpringJoint2D` between its nodes
   (#453), which has no stops of its own, so it gets the cylinder above.
   Each tick `SpringLink` sets its rest length to `Spring.StepRestLength`:
   Godot's spring would push a preload past a stop into the stop's joints,
   which gave way (#835). Behaviour: `docs/CREATURE_MODEL.md` → Spring.
6. **Servos and Velocity motors.** A joint motor (`ServoJoint`, or
   `VelocityMotorJoint`, #454) measures and drives its two sides through one
   `HingePartSides`, as every hinge part does. Each side is a `HingePartSide`: a link
   (`HingePartLinkSide`) gets equal-and-opposite force couples
   (`HingePartSideGeometry.CoupleForTorque`) on the joint body and the link's far body; the
   joint's Wheel (`HingePartWheelSide`, #1118) gets a plain body torque on the
   Wheel body, with the signs of `HingePartSideGeometry.WheelAngle`/`WheelBodyTorque` and
   the Wheel's own inertia. A Servo adds a soft end-stop torque outside its
   range; a Velocity motor has no stops, and at zero strength no torque.
   No Godot angular joint: Pistons and Springs slide on a groove, and force
   couples work for every link kind without link-kind branches. The lever arm is clamped to
   `HingePartSideGeometry.MinimumLeverArm` so a compressed Spring cannot make unbounded
   force. Motor and stop are integrated explicitly, so the gains are scaled
   to the live inertia (`HingePartSideGeometry.LinkInertia` or the Wheel's, `Servo.StableEndStopGains`) to
   stay stable; the holding part is not capped, so a Servo holds a load up
   to its strength.
7. **Accelerometer.** `AccelerometerSensor` differences the velocity where
   its picture sits on its beam (Godot's `GetVelocityAtLocalPosition`, #1086) each
   tick into specific force and steps the Mechanics
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
    rotation and give it the Wheel's weight and inertia; add no
    body or joint of its own (`Creature.Wheels.cs` says how).
12. **A Touch sensor is a `ShapeCast2D` on its joint's body.** It casts the
    joint's cushion in place, masked like the body, to find the nearest
    ground in any direction; a contact monitor only says on or off, and the
    reading is analog (`Touch.Reading`, #1045). Add no area or body of its
    own (`TouchSensor.cs` says how).
13. **A Rubber is its joint body's material, nothing new.** Give that body
    a Rough `PhysicsMaterial` at `Rubber.Friction`; add no body, shape or
    collider (`Creature.Rubbers.cs` says how, #1142).
14. **A Brake is friction on its `HingePartSides`, or a weld when Locked.** A
    `BrakeJoint` turns its sides (rule 6) by `Mechanics.Brake`'s torque
    every tick, brain or not (#1040), using `HingePartSides.BrakeInertia`,
    which must never read heavier than the joint. A Locked Brake is a
    second Godot joint instead, as no Godot angular joint can brake;
    `Creature.Links.cs` (WeldSides) says how, and when it stays friction.
15. **An Angle sensor only reads its `HingePartSides`.** An `AngleSensorJoint`
    reads the angle and speed between its sides as a Motor does (#1204),
    and never applies torque or adds a joint.
16. **A Stepper holds and steps its `HingePartSides` by torque.** A
    `StepperJoint` turns its sides (rule 6) by `Mechanics.Stepper`'s torque
    every tick, brain or not (#1037): with no brain it only holds its
    notch. Its hold reads `HingePartSides.BrakeInertia`, as a Brake's does.

## What lives here

- `Creature.cs`, `Creature.Links.cs`, `Creature.Wheels.cs`,
  `Creature.Rubbers.cs` — builds bodies, pins, sensors, links (each with its
  end-stop cylinder), Wheels and Rubbers, and wires the brain
- `Creature.Selection.cs` — what a tap selects and each part's draw group,
  with the selection raised (#1107)
- `IBeamSensor.cs` — what `Creature` needs from a beam sensor: value names,
  `Read`, `Reset`
- `TouchSensor.cs` — a Touch sensor: how near its joint body is to the ground
- `AccelerometerSensor.cs`, `CameraSensor.cs` — the sensors (a camera is
  one `RayCast2D` child per ray, aimed and sized by `CameraRays`)
- `HingePartSides.cs` — a hinge part's two sides: angle, speed, inertia, torque
- `ServoJoint.cs`, `VelocityMotorJoint.cs`, `PistonLink.cs`, `SpringLink.cs` — the driven parts
- `BrakeJoint.cs` — a Brake's friction on its sides
- `StepperJoint.cs` — a Stepper's hold and steps on its sides
- `AngleSensorJoint.cs` — an Angle sensor's reading of its sides
  (`AngleSensorVisual.cs` draws its arc from the same `HingePartSides`, #1205)
- `AngleSensorTurn.cs` — the turn Training's Angle sensor arc is on, from
  `HingePartSides.ContinuousAngle`, a physics tick behind the bodies (#1243)
- `*Visual.cs` — rendering only: Training's adapters over the shared part
  visuals in `project/src/theme` (#767); `ShadowDrawing.cs` holds
  `IShadowVisual`
- `project/scenes/Creature.tscn` — the scene template

## Tests

`docs/MANUAL_TESTING.md` decides when a physics change needs manual
testing; physics feel usually does. Pure sums move to
`libs/NodeRunner.Mechanics` and are unit tested there.
