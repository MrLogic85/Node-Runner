# Training loop design

Durable design notes for 0.4.0 "Den första träningen" (see
`docs/ROADMAP.md`). This is the authoritative description of the
trial/evaluation/evolution flow; individual issues implement slices of it
but do not redefine it here.

## Layering

- `libs/NodeRunner.ML/Ga/` owns the pure GA math (selection, crossover,
  mutation) — Godot-agnostic, unit-tested.
- `project/src/sim/` owns orchestration that has to touch Godot physics or
  scene lifecycle (trials, populations, generations). This code is verified
  manually per `docs/TEST_STRATEGY.md` (Godot Node code is not yet covered
  by the xUnit suite; GdUnit4 is deferred to v1.0+).
- `project/src/creature/Creature.cs` stays free of evolution/fitness logic;
  it only exposes primitives (`ResetPose`, `SetBrain`, `CenterOfMass`) that
  the sim layer composes.
- `project/src/managers/RngProvider.cs` is the single seeded RNG source for
  a run (a Godot autoload; see `docs/CODE_DESIGN_PRINCIPLES.md` §3 and
  `project/src/managers/AGENTS.md`). Sim/ML code receives a `Random` from
  it explicitly rather than constructing its own.

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
  auto-randomizing one via `BuildFrom`. This is how the population/GA step
  (issue #50, below) plugs a candidate genome into a trial.
- Historical wiring (as of issue #49, superseded by #50 below): `Main.cs`
  owned a single `TrialController` directly and restarted trials on
  `TrialCompleted` with a freshly randomized brain each time. As of #50,
  `Evolver` owns the `TrialController` internally and assigns each
  generation's candidate genomes in turn; `Main.cs` no longer talks to
  `TrialController` directly.
- `TrialController.ProcessPhysicsPriority` is set below `Creature`'s default
  so a trial-boundary reset always runs before that tick's `Creature`
  motor drive. Without this, a `TrialCompleted` handler that calls
  `StartTrial` synchronously (as `Evolver` does) would reset the pose
  *after* the outgoing trial's last motor command was already set for that
  physics step, letting stale torque bleed into the new trial's first tick.

## Generations (issue #50)

- `GeneticAlgorithm` (`libs/NodeRunner.ML/Ga/GeneticAlgorithm.cs`) is pure,
  Godot-agnostic math over flat genome vectors (`double[]`, see
  `NeuralNetwork.FlattenGenome`/`FromGenome`). `NextGeneration(genomes,
  fitness, random)` keeps the fittest `elitismCount` genomes unchanged,
  then fills the rest via tournament selection, the configured crossover
  strategy (Uniform or Blend), and per-gene Gaussian mutation (Box-Muller).
  Fully unit-tested
  (`tests/NodeRunner.ML.Tests/GeneticAlgorithmTests.cs`), including
  determinism-given-a-seed and elitism preserving the exact best genome.
- `Evolver` (`project/src/sim/Evolver.cs`) orchestrates one generation cycle
  for a single creature: it evaluates every genome in the current
  generation **sequentially**, one `TrialController` trial each (assigning
  each candidate brain via `Creature.SetBrain` before the trial), then
  calls `GeneticAlgorithm.NextGeneration` and starts evaluating the next
  generation automatically. It tracks `Generation`, `BestFitness` (running
  best across all generations), and `MeanFitness` (current generation's
  average), and raises `GenerationCompleted`/`NewBestFound`, which
  `Main.cs`'s training HUD (see "Training HUD (issue #51)" below) subscribes
  to.
  - Evaluating candidates one at a time on one creature — rather than
    running a parallel population — is a deliberate, explicitly
    roadmap-sanctioned simplification ("repeated trials of one creature").
    It avoids collision-layer/population-lifecycle work for this slice;
    running N creatures in parallel remains available as a later
    optimization if evaluation speed becomes a problem.
- `Main.cs` creates one `Evolver` and selects a session-scoped training
  profile. Quick, Standard, and Deep vary population size, trial duration,
  generation budget, tournament size, mutation rate/strength, and crossover
  strategy. Uniform crossover preserves parent genes; Blend crossover samples
  continuous values between the two parent genes, giving the player a direct
  experiment for the competing-conventions plateau without changing the
  underlying network.
  `StartEvolution()` (`Main.cs`) always calls `Evolver.Stop()` first (which
  halts the in-progress trial without raising any events), then calls
  `Evolver.Start(...)` again — unless the current creature has no brain
  (a just-cleared construction-mode anatomy), in which case it stops and
  leaves evolution idle rather than starting. This is what
  restarting-on-rebuild/"Randomize" (which reseeds `RngProvider`) relies on
  to avoid a stale in-flight trial for the old creature outliving the
  rebuild.
- Generation/fitness are logged (`GD.Print`) and shown in the training HUD
  (see below).

The first progression milestone uses the running best fitness as its metric:
reaching 50 distance units unlocks a second core slot globally. The unlock is
recorded with the generation that crossed the threshold and remains available
in Build after restarting the app.

## Training HUD (issue #51)

- `Main.cs` adds a training panel below the top Randomize/Build/Seed row
  (mutually exclusive with the construction tool row — training and
  construction modes never show at once): "Gen: N", "Best: X.X (gen G)",
  "Mean: X.X", and Run/Pause, Reset, and time-scale buttons.
  - **Generation/Best/Mean** update only when `Evolver.GenerationCompleted`
    fires (once per completed generation), driven by
    `Main.UpdateTrainingLabels()`. "Best" shows the running best fitness
    and the generation it was found at (tracked via `NewBestFound`); there
    is no per-genome reproducibility seed to show (see #50's tradeoffs),
    so "current/best seed" from the original issue scope became "run seed
    (existing Seed label) + best generation."
  - **Run/Pause** toggles `GetTree().Paused`. This is the standard Godot
    pause mechanism: every node using the default `Pausable` process mode
    (creature, `Evolver`, `TrialController`) freezes immediately —
    physics stops advancing, so trial motion, fitness recording, and
    trial-boundary checks all stop mid-trial and resume exactly where they
    left off. The `Hud` `CanvasLayer` is set to `ProcessMode.Always` so its
    buttons (Pause included) keep responding while paused — otherwise
    pausing would lock out the only way to un-pause.
  - **Reset** reseeds `RngProvider` and restarts evolution from a fresh
    random population — identical to "Randomize"'s existing behavior,
    exposed as its own control per the issue's acceptance criteria.
  - **Time-scale** cycles a fixed 1x/2x/4x set via `Engine.TimeScale`.
    This scales every physics/process step uniformly and does not affect
    determinism, only how quickly a fixed tick budget plays out. It's
    reset to 1x in `Main._Ready()`/`_ExitTree()` since it's a global engine
    setting, not scoped to this scene.
  - Toggling construction mode always resumes first (`GetTree().Paused =
    false`) — the construction canvas uses the default `Pausable` process
    mode, so editing while paused would silently not work even though the
    Build button (on the `Always`-mode Hud layer) stayed tappable.
  - `Main.cs` now owns scene composition, construction UI, the inspector,
    *and* this training presentation directly subscribing to `Evolver`.
    That's a growing pile of responsibility in one file; extracting the
    training panel into its own UI widget backed by an App-layer view
    model (matching how `CreatureInspectorViewModel` already works) is a
    reasonable later cleanup, tracked in issue #106.
- Full neural-network visualization remains out of scope (later milestone).

## Deferred future work

- Running more than one creature at once in parallel (`Population`,
  collision-layer isolation per `project/src/sim/AGENTS.md`). #50
  intentionally evaluates candidates one at a time on a single creature
  instead — see above. Tracked in issue #105. This is not #52's scope: #52
  is a validation-only issue (manual release check for 0.4.0), not an
  implementation issue.
