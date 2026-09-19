# Training loop design

Durable design notes for 0.4.0 "Den första träningen" (see
`docs/ROADMAP.md`). This is the authoritative description of the
trial/evaluation/evolution flow; individual issues implement slices of it
but do not redefine it here.

## Layering

- `libs/NodeRunner.ML/Ga/` owns the pure GA math (selection, crossover,
  mutation) — Godot-agnostic, unit-tested. Not implemented yet (issue #50).
- `project/src/sim/` owns orchestration that has to touch Godot physics or
  scene lifecycle (trials, populations, generations). This code is verified
  manually per `docs/TEST_STRATEGY.md` (Godot Node code is not yet covered
  by the xUnit suite; GdUnit4 is deferred to v1.0+).
- `project/src/creature/Creature.cs` stays free of evolution/fitness logic;
  it only exposes primitives (`ResetPose`, `SetBrain`, `CenterOfMass`) that
  the sim layer composes.

## Trial (issue #49)

- `Evaluator` (`project/src/sim/Evaluator.cs`) is a plain, dependency-free
  fitness tracker: `Reset(startX)` begins a trial, `Record(currentX)` is
  called every tick, and `Fitness` is the **running maximum** forward
  horizontal distance from the start position — not the final position and
  not cumulative distance. This rewards peak forward progress without
  penalizing a creature that surges forward and then settles or wobbles
  back slightly before the trial ends.
- `TrialController` (`project/src/sim/TrialController.cs`) is a `Node` that
  times a fixed-duration trial (`TrialDurationTicks`, default 600 ≈ 10s at
  60Hz) for one `Creature` instance at a time. It does **not** own creature
  creation/destruction or brain assignment — callers are responsible for
  that. `StartTrial(creature)` calls `Creature.ResetPose()` (teleports every
  beam body back to its built position/rotation and zeroes velocity) and
  resets the `Evaluator` from the creature's current `CenterOfMass.X`.
  `TrialCompleted` fires once the tick budget is spent, with the final
  fitness value.
- `Creature.CenterOfMass` is the average `GlobalPosition` of all beam
  bodies — a simple, cheap stand-in for a true center-of-mass, adequate for
  fitness tracking.
- `Creature.SetBrain(brain, seed)` assigns a specific `NeuralNetwork`
  (validated against the creature's sensor/motor counts) instead of always
  auto-randomizing one via `BuildFrom`. This is how a future population/GA
  step (issue #50) will plug a candidate genome into a trial.
- Current wiring (`Main.cs`): a single `TrialController` runs repeated trials
  of the one on-screen creature, restarting automatically on
  `TrialCompleted` (and whenever the brain is randomized or the creature is
  rebuilt via construction mode). Fitness is currently only logged
  (`GD.Print`); a bound HUD display is issue #51's job, not this slice's.
- `TrialController.ProcessPhysicsPriority` is set below `Creature`'s default
  so a trial-boundary reset always runs before that tick's `Creature`
  motor drive. Without this, a `TrialCompleted` handler that calls
  `StartTrial` synchronously (as `Main.cs` does) would reset the pose
  *after* the outgoing trial's last motor command was already set for that
  physics step, letting stale torque bleed into the new trial's first tick.

## Not yet implemented (issues #50-#52)

- Running more than one creature at once (`Population`, collision-layer
  isolation per `project/src/sim/AGENTS.md`).
- The genetic algorithm itself (`GeneticAlgorithm` in
  `libs/NodeRunner.ML/Ga/`): tournament selection, uniform crossover,
  Gaussian mutation, `NextGeneration(genomes, fitness, rng)`.
- Generation orchestration (`Evolver`): eval → GA → reset cycle, best/mean
  fitness tracking, `GenerationCompleted`/`NewBestFound` events.
- Training HUD (generation, fitness, current/best seed) and run/pause/reset
  /time-scale controls.
