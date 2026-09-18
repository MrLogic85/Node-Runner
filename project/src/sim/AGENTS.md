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

- `Population.cs` — spawns N creatures with N brains, runs them in parallel
  in one scene (collision layers isolate them)
- `Evaluator.cs` — measures fitness per creature per tick, accumulates
- `Evolver.cs` — orchestrates the generation cycle: eval → GA → reset
- `SimulationRunner.cs` (probably) — the top-level `Node` that ties the
  above together; scene entry point

## What does NOT live here

- The GA math (selection/crossover/mutation) → `libs/NodeRunner.ML/Ga/`
- Individual creature physics → `project/src/creature/`
- Persisting best creatures → `libs/NodeRunner.App/Repositories/` via
  `SaveManager`
- Charts, sliders, HUD → `project/src/ui/`

## Style specifics

- Collision layers: reserve layer 1 for the ground, layers 2..(N+1) per
  population slot. Creatures in slot `i` collide with layer 1 and layer `1+i`,
  nothing else. Documented in this AGENTS.md so nobody re-invents it.
- Emit C# events for milestones (`GenerationCompleted`,
  `NewBestFound`). ViewModels subscribe.
- Fitness accumulation happens in `_PhysicsProcess`, not on generation
  boundary — cheaper and monotonic.

## Test expectations

- Evolver logic (fitness → next-generation genomes) is tested against the
  pure `GeneticAlgorithm` in `libs/NodeRunner.ML/Ga/` with hand-crafted
  populations.
- Physics/scene behavior is verified manually.
