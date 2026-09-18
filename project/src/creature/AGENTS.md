# AGENTS.md — `src/creature/`

**The Godot-side representation of a creature: joints, bones, muscles,
sensors, and the brain wiring.**

## Rules

1. **Creatures are built FROM `CreatureDef`.** The `CreatureDef` (in
   `libs/NodeRunner.Domain`) is the source of truth. `Creature.cs` interprets
   it and spawns physics bodies.
2. **No game logic here.** No evolution, no fitness, no UI. Just: "given this
   def and this brain, become a physical thing on screen that reacts to
   forces and applies muscle activations."
3. **Physics uses Godot built-ins.** `RigidBody2D`, `PinJoint2D`,
   `DampedSpringJoint2D`. Do not introduce Box2D.NET or a custom solver.
4. **Sensors return `double[]` in a deterministic order.** That order must
   match what the brain was trained for. Document the order in a comment
   above `Sensors.Read()`.
5. **No allocations in the tick hot path.** Reuse arrays for
   sensor readings and muscle targets.

## What lives here

- `Creature.cs` — root `Node2D` that owns skeleton + brain
- `Skeleton.cs` — spawns and holds joint `RigidBody2D`s and bone joints
- `Muscle.cs` — one actuated joint; `ApplyTarget(double t)` where `t ∈ [-1,1]`
- `Sensors.cs` — reads observations into a `double[]`
- `Creature.tscn` (in `scenes/`) — the scene template

## What does NOT live here

- The brain's math → `libs/NodeRunner.ML/`
- Fitness measurement → `project/src/sim/`
- Save/load → `libs/NodeRunner.App/Repositories/`
- The def data type itself → `libs/NodeRunner.Domain/`

## Style specifics

- Keep `_PhysicsProcess(double delta)` short:
  ```csharp
  public override void _PhysicsProcess(double delta)
  {
      _sensors.Read(_observationBuffer);
      _brain.Forward(_observationBuffer, _actionBuffer);
      _muscles.ApplyAll(_actionBuffer);
  }
  ```
- Head joint = first joint in the `CreatureDef` by convention. Visual head
  markers follow `docs/UI_DIRECTION.md` and must stay themeable.
- No `[Export]` for things that come from a `CreatureDef` — those are set
  programmatically at build time.

## Test expectations

- `docs/MANUAL_TESTING.md` decides when physics changes need manual testing;
  physics feel usually does.
- Unit tests for pure helpers only (e.g. sensor-ordering, muscle target
  clamping) if any are extracted from the Godot classes.
