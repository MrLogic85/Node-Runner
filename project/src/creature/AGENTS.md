# AGENTS.md — `src/creature/`

**The Godot-side representation of a creature: nodes, beams, sensor parts, Pistons,
and the brain wiring. See `docs/CREATURE_MODEL.md` for
the model this implements.**

## Rules

1. **Creatures are built FROM `CreatureDef`.** The `CreatureDef` (in
   `libs/NodeRunner.Domain`) is the source of truth. `Creature.cs` interprets
   it and spawns physics bodies.
2. **No game logic here.** No evolution, no fitness, no UI. Just: "given this
   def and this brain, become a physical thing on screen that reacts to
   forces and drives its moving parts."
3. **Physics uses Godot built-ins.** Each node and each beam is its own
   `RigidBody2D`; a `PinJoint2D` pins every beam to its two nodes. Only
   nodes collide (a circle each); beams and Pistons have no collider and
   their weight sits on their nodes. Beam rigidity is geometric (fixed pin distance), not
   spring-based. Each Piston also has a hidden, collider-free cylinder body
   pinned to node A and grooved to node B for its end stops (#701; see
   `docs/CREATURE_MODEL.md`): anything that moves or resets every body must
   include it. Do not introduce Box2D.NET or a custom solver.
4. **Joints are passive (#450).** A beam turns freely at its nodes; only
   parts with ports (Pistons today) are driven by the brain.
5. **The brain's input and output order is `BrainPorts.Of` (in
   `NodeRunner.Domain`).** The creature reads its parts in its own
   order and copies each value to its port's place; do not hand-order brain
   slots here.
6. **No allocations in the tick hot path.** Reuse arrays for sensor readings
   and brain outputs.

## What lives here

- `Creature.cs` — root `Node2D` that builds nodes/beams/pins/sensors and each
  Piston's end-stop cylinder from a `CreatureDef` and owns the brain wiring
- `IBeamSensor.cs` — what `Creature` needs from a sensor part: its value
  names, `Read` into the sensor buffer, and `Reset`
- `AccelerometerSensor.cs` — one accelerometer: measures its beam's
  midpoint acceleration each tick, steps the Domain `Accelerometer` proof
  mass and writes its 2 readings into the sensor buffer
- `CameraSensor.cs` — one camera: three `RayCast2D` children aimed as
  built by the Domain `CameraRays`, writing 3 nearness readings
- `NodeVisual.cs` / `BeamVisual.cs` / `PistonVisual.cs` /
  `RigidHatchVisual.cs` — rendering only, no physics: Training's adapters
  over the shared part visuals in `project/src/theme` (`JointPart`,
  `BeamPart`, `PistonPart`, `HatchPart`, #767)
- `SensorVisual.cs` — a sensor's picture (`SensorPart`), a rendering-only
  child of its beam body; the Accelerometer weight follows the live proof mass
- `CameraRaysVisual.cs` — every camera ray that hits the ground, drawn up to
  the hit on the `CreatureLayers.Overlays` layer, over the whole creature
- `ShadowDrawing.cs` — `IShadowVisual`: every visual above declares how it
  draws on a shadow that is not followed (#385); a test checks each one
- `Creature.tscn` (in `scenes/`) — the scene template

## What does NOT live here

- The brain's math → `libs/NodeRunner.ML/`
- Fitness measurement → `project/src/sim/`
- Save/load → `libs/NodeRunner.App/Repositories/`
- The def data types and their pure math → `libs/NodeRunner.Domain/`

## Style specifics

- Keep `_PhysicsProcess(double delta)` short:
  ```csharp
  public override void _PhysicsProcess(double delta)
  {
      ReadSensors(_sensorValues, delta);
      Brain.Forward(_sensorValues, _outputValues, _scratchA, _scratchB);
      foreach (var piston in _pistons)
      {
          piston.Drive(...);
      }
  }
  ```
- No `[Export]` for things that come from a `CreatureDef` — those are set
  programmatically at build time.

## Test expectations

- `docs/MANUAL_TESTING.md` decides when physics changes need manual testing;
  physics feel usually does.
- Unit tests for pure helpers only (e.g. sensor ordering, Piston control) if any are extracted from the Godot classes.
