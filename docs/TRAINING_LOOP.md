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

## Product lifecycle boundary

The training engine does not decide whether a Creation is editable. The App
layer owns the durable lifecycle described by
`reference design/components/Navigation/README.md`:

1. An unlocked Build autosaves and opens Train setup through Start training.
2. Train setup opens Training.
3. The Creation remains unlocked while its first training session is in
   progress.
4. Finishing that session persists the trained state and locks anatomy plus
   brain shape.
5. Later Train or Simulate sessions start from the locked Build state.
6. Unlock is a destructive App operation: after hold-to-confirm it removes
   the trained model/history and returns the same Creation to unlocked Build.

`Evolver` reports training progress and completion; it must not mutate the
Creation lock by itself. The App/persistence orchestration translates a
completed session into the durable lock transition and must make interrupted
sessions explicit rather than treating navigation or Start as completion.

## Trial (issue #49)

- `TrialMeasurement` (`libs/NodeRunner.ML/Ga/TrialMeasurement.cs`) is a
  plain, engine-free tracker: `Reset(startX)` begins a trial,
  `Record(centerX, clearance)` is called every tick, and `Result` is a
  `TrialResult` (issue #420):
  - `Distance` — the **running maximum** forward horizontal distance from
    the start position, not the final position and not cumulative
    distance. This is the fitness. It rewards peak forward progress
    without penalizing a creature that surges forward and then settles or
    wobbles back slightly before the trial ends.
  - `TopSpeed` — the highest forward speed of the centre, averaged over a
    sliding half-second window so one-tick physics jolts do not dominate.
  - `Elevation` — the largest gap between the creature's lowest collision
    point and the ground top; a crawler scores 0.
- `Evolver.BestRun` is the `TrialResult` of the best genome so far.
  `SimulateHost` persists it as `TrainingStateDef.BestRun` (`TrainingRunDef`, with
  `MapId` `flat` until more maps exist) so the Creations card can show it.
- `TrialController` (`project/src/sim/TrialController.cs`) is a `Node` that
  times a fixed-duration trial (`TrialDurationTicks`, default 600 ≈ 10s at
  60Hz) for one `Creature` instance at a time. It does **not** own creature
  creation/destruction or brain assignment — callers are responsible for
  that. `StartTrial(creature)` calls `Creature.ResetPose(GroundTopY)` (teleports
  every beam body back to its built shape and rotation, zeroes velocity,
  and shifts the whole creature so its lowest point just touches the
  ground — no drop from spawn height that would count as elevation) and
  resets the `TrialMeasurement` from the creature's current
  `CenterOfMass.X`. Each tick it records `CenterOfMass.X` and the clearance
  `GroundTopY - Creature.LowestPointY`. `TrialCompleted` fires once the
  tick budget is spent, with the final `TrialResult`.
- `Creature.CenterOfMass` is the average `GlobalPosition` of all beam
  bodies — a simple, cheap stand-in for a true center-of-mass, adequate for
  fitness tracking.
- `Creature.SetBrain(brain, seed)` assigns a specific `NeuralNetwork`
  (validated against the creature's sensor/motor counts) instead of always
  auto-randomizing one via `BuildFrom`. This is how the population/GA step
  (issue #50, below) plugs a candidate genome into a trial.
- Historical wiring (as of issue #49, superseded by #50 and #105 below):
  `Main.cs`
  owned a single `TrialController` directly and restarted trials on
  `TrialCompleted` with a freshly randomized brain each time. As of #50,
  `Evolver` owns trial controllers internally and assigns each generation's
  candidate genomes; `Main.cs` no longer talks to `TrialController` directly.
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
  in deterministic fixed slots. Slot 0 reuses the visible creature and each
  additional slot is a hidden clone; every slot owns one `TrialController`.
  A completed slot receives the next pending genome in index order until the
  generation is complete, then `GeneticAlgorithm.NextGeneration` starts the
  next generation automatically. `Evolver` tracks `Generation`,
  `BestFitness` (running best across all generations), and `MeanFitness`
  (current generation's average), and raises
  `GenerationCompleted`/`NewBestFound`, which the Simulate scene (see
  "The Simulate scene" below) subscribes to.
  - Concurrency is capped at 16 and never exceeds population size. Layer 1 is
    reserved for ground; zero-based slot `i` uses layer `2+i` and collides
    only with ground and its own slot. Core sensor rays remain ground-only.
  - Candidate assignment is deterministic for the same seed, parallel mode,
    slot count, build, and platform. Sequential and parallel fitness parity
    is not promised because physics ordering can differ.
  - `Evolver.Start` retains a one-slot compatibility mode when no creature
    factory is supplied. Production `SimulateHost` supplies the factory and
    uses parallel evaluation.
- `SimulateHost` creates one `Evolver` and selects a session-scoped training
  profile. Quick, Standard, and Deep vary population size, trial duration,
  generation budget, tournament size, mutation rate/strength, and crossover
  strategy. Uniform crossover preserves parent genes; Blend crossover samples
  continuous values between the two parent genes, giving the player a direct
  experiment for the competing-conventions plateau without changing the
  underlying network.
  `StartEvolution()` (`SimulateHost`) always calls `Evolver.Stop()` first (which
  halts the in-progress trial without raising any events), then calls
  `Evolver.Start(...)` again — unless the current creature has no brain
  (an anatomy without motors), in which case it stops and leaves evolution
  idle rather than starting. This is what Reset (which reseeds
  `RngProvider`) and changing the training profile rely on to avoid a stale
  in-flight trial outliving the restart.
- Generation/fitness are logged (`GD.Print`) and shown on the Simulate
  screen (see "The Simulate scene" below).

The first progression milestone uses the running best fitness as its metric:
reaching 50 distance units unlocks a second core slot globally. The unlock is
recorded with the generation that crossed the threshold and remains available
in Build after restarting the app.

## The Simulate scene (issues #51, #469)

This section documents the current prototype wiring, not the target
navigation or presentation. The target is owned by the TrainSetup and Training
component READMEs under `reference design/components/`.

- Simulate is its own routed scene, `SimulateRoute(creationId)`, with
  `SimulateHost` (`project/src/`) as its root. It builds the ground, the
  camera, the creature and the `Evolver` from the creation's save, so
  leaving the scene frees all of them.
  - **Resume.** Opening it starts from the saved `TrainingStateDef`: the
    best genome seeds the population and the generation count continues.
    A creation without training starts from a fresh random population.
  - **Save.** Each finished generation is saved on the thread pool (the
    file round trip would stall physics). Leaving mid-generation drops only
    the generation in progress. The save is guarded by the creation's
    training epoch, so a save still in flight when the training is reset
    is dropped.
  - A session stops after the profile's generation budget. The profile,
    speed and pause belong to the scene and start from Standard, 1x and
    running each time it opens.
  - Run on its own (F6) the scene trains the built-in worm without saving.
- The Simulate screen shows the generation and fitness from
  `TrainingPresentationViewModel`, and its controls are Pause, Speed and
  Reset.
  - **Pause** toggles `GetTree().Paused`. This is the standard Godot
    pause mechanism: every node using the default `Pausable` process mode
    (all slot creatures, `Evolver`, and every `TrialController`) freezes
    immediately —
    physics stops advancing, so trial motion, fitness recording, and
    trial-boundary checks all stop mid-trial and resume exactly where they
    left off. The scene root and the screen's layer are `ProcessMode.Always`
    so the buttons (Pause included) keep responding while paused, and the
    creature and `Evolver` pin themselves back to `Pausable`.
  - **Reset** resets the saved training, reseeds `RngProvider` and
    restarts evolution from a fresh random population.
  - **Speed** cycles a fixed 1x/2x/4x set via `Engine.TimeScale`.
    This scales every physics/process step uniformly and does not affect
    determinism, only how quickly a fixed tick budget plays out. Speed and
    pause are reset in `SimulateHost._Ready()`/`_ExitTree()` since both are
    global engine settings, not scoped to this scene.
- Full neural-network visualization remains out of scope (later milestone).

## Deferred future work

- Parallel slot clones remain hidden. Showing live ghost candidates and a
  solid previous-generation reference is tracked separately in issue #137;
  it must reuse this slot lifecycle rather than create another population.
