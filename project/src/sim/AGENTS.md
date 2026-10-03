# AGENTS.md — `src/sim/`

**Simulation orchestration. Populations, evaluation, evolution.**

## Rules

1. **Owns the training loop.** This is where "run a population for N ticks,
   score them, evolve, repeat" lives.
2. **Depends on `NodeRunner.ML` and `project/src/creature/`, exposes state
   to view-models in `NodeRunner.App`.** Never reaches into UI or managers
   directly.
3. **Deterministic given a seed.** All randomness threads through the RNG
   from `RngProvider`. No `DateTime.Now`-based decisions.
4. **Fixed timestep.** Uses `_PhysicsProcess`, never `_Process`.
5. **One simulation "run" is one class instance.** Runs are cheap to create
   and destroy; state accumulates in the run, not in statics.

## What lives here

- `TrialController.cs` — times one fixed-duration trial for one creature,
  resets its pose between trials, and feeds `TrialMeasurement`
  (`libs/NodeRunner.ML/Ga`) each tick. Simulate (#702) runs one with no end
  (`int.MaxValue` ticks) straight from `TrainingHost`
- `Evolver.cs` — orchestrates the generation cycle: evaluate every genome
  in fixed parallel slots (one `TrialController` per slot) → GA → next
  generation. Slot 0 reuses the visible creature; additional slots are
  clones drawn as shadows; creature bodies collide only with the ground.
  `Evolver` owns which shadow is followed (`Follow`, `FollowedCreature`).
- `ArenaGround.cs` — the arena's ground, built from the map's `MapGround`
  along the scene's ground line: the layer-1 collider, fill and edge.
  Flat only (Godot's `WorldBoundaryShape2D`); shaped grounds and chunks
  come with #91.
- `Population.cs` — not currently needed. `Evolver` owns the fixed-slot
  population lifecycle directly; extract it only if that lifecycle grows
  beyond training orchestration.
- `SimulationRunner.cs` (probably) — the top-level `Node` that ties the
  above together; scene entry point. Not yet built — the Training scene's
  root, `TrainingHost` in `project/src/hosts/`, owns this role directly for now.

## What does NOT live here

- The GA math (selection/crossover/mutation) → `libs/NodeRunner.ML/Ga/`
- Individual creature physics → `project/src/creature/`
- Persisting best creatures → `libs/NodeRunner.App/Repositories/` via
  `SaveManager`
- Charts, sliders, HUD → `project/src/ui/`

## Style specifics

- Collision isolation must follow the canonical slot allocation in
  `docs/TRAINING_LOOP.md`; do not introduce an independent layer scheme here.
- Emit C# events for milestones (`GenerationCompleted`,
  `TrainingProgressChanged`). ViewModels subscribe.
- Fitness accumulation happens in `_PhysicsProcess`, not on generation
  boundary — cheaper and monotonic.

## Test expectations

- Evolver logic (fitness → next-generation genomes) is tested against the
  pure `GeneticAlgorithm` in `libs/NodeRunner.ML/Ga/` with hand-crafted
  populations.
- Physics/scene behavior is verified manually.
