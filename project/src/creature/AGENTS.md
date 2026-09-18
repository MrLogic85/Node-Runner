# AGENTS.md — `src/creature/`

**The Godot-side representation of a creature: nodes, beams, cores, motor
relations, sensors, and the brain wiring. See `docs/CREATURE_MODEL.md` for
the model this implements.**

## Rules

1. **Creatures are built FROM `CreatureDef`.** The `CreatureDef` (in
   `libs/NodeRunner.Domain`) is the source of truth. `Creature.cs` interprets
   it and spawns physics bodies.
2. **No game logic here.** No evolution, no fitness, no UI. Just: "given this
   def and this brain, become a physical thing on screen that reacts to
   forces and drives motor relations."
3. **Physics uses Godot built-ins.** Each beam is its own `RigidBody2D`;
   `PinJoint2D` connects beams that share a node. Beam rigidity is
   geometric (fixed collision-shape length), not spring-based. Do not
   introduce Box2D.NET or a custom solver.
4. **`MotorTopology.BuildNodeConnections` (in `NodeRunner.Domain`) is the
   single source of truth for which beam pairs get a physical pin and which
   of those are motorized.** Do not re-derive this logic here.
5. **Sensors return `double[]` in a deterministic order.** That order must
   match what the brain was trained for. Document the order in a comment
   above `Creature.ReadSensors()`.
6. **No allocations in the tick hot path.** Reuse arrays for sensor readings
   and motor targets.

## What lives here

- `Creature.cs` — root `Node2D` that builds beams/pins/cores from a
  `CreatureDef` and owns the brain wiring
- `MotorRelation.cs` — one controllable connection; drives torque (capped at
  a static `MaxTorque`) to chase a target angular velocity
- `CoreSensors.cs` — reads one core's sensor values (rays, pitch, elevation,
  speed) into a `double[]`
- `NodeVisual.cs` / `BeamVisual.cs` — rendering only, no physics
- `HardcodedCreatureFactory.cs` — the first concrete `CreatureDef`
- `Creature.tscn` (in `scenes/`) — the scene template

## What does NOT live here

- The brain's math → `libs/NodeRunner.ML/`
- Fitness measurement → `project/src/sim/`
- Save/load → `libs/NodeRunner.App/Repositories/`
- The def data type itself and `MotorTopology` → `libs/NodeRunner.Domain/`

## Style specifics

- Keep `_PhysicsProcess(double delta)` short:
  ```csharp
  public override void _PhysicsProcess(double delta)
  {
      ReadSensors(_sensorValues);
      Brain.Forward(_sensorValues, _motorTargets, _scratchA, _scratchB);
      for (var i = 0; i < _motorRelations.Length; i++)
      {
          _motorRelations[i].Drive(_motorTargets[i]);
      }
  }
  ```
- No `[Export]` for things that come from a `CreatureDef` — those are set
  programmatically at build time.

## Test expectations

- `docs/MANUAL_TESTING.md` decides when physics changes need manual testing;
  physics feel usually does.
- Unit tests for pure helpers only (e.g. sensor-ordering, motor-relation
  target clamping) if any are extracted from the Godot classes.
