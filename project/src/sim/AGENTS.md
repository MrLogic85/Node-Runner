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

- `Evaluator.cs` — measures fitness for one trial, accumulates
- `TrialController.cs` — times one fixed-duration trial for one creature,
  resets its pose between trials
- `Evolver.cs` — orchestrates the generation cycle: evaluate every genome
  in fixed parallel slots (one `TrialController` per slot) → GA → next
  generation. Slot 0 reuses the visible creature; additional slots are
  hidden clones whose collision layers isolate their physics.
- `Population.cs` — not currently needed. `Evolver` owns the fixed-slot
  population lifecycle directly; extract it only if that lifecycle grows
  beyond training orchestration.
- `SimulationRunner.cs` (probably) — the top-level `Node` that ties the
  above together; scene entry point. Not yet built — `Main.cs` owns this
  role directly for now.

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
  `NewBestFound`). ViewModels subscribe.
- Fitness accumulation happens in `_PhysicsProcess`, not on generation
  boundary — cheaper and monotonic.

## Test expectations

- Evolver logic (fitness → next-generation genomes) is tested against the
  pure `GeneticAlgorithm` in `libs/NodeRunner.ML/Ga/` with hand-crafted
  populations.
- Physics/scene behavior is verified manually.
