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
`reference design/components/Navigation/README.md`. One owner decision
overrides it and `reference design/components/BuildLocked/README.md` (#369,
2026-09-30): "locked" means exactly "has trained at least one generation",
not "a training session has finished".

1. An unlocked Build autosaves and opens Train setup through Start training.
   Saving a Creation never needs a finished creature; only training does
   (`CreatureReadiness`, #515). An edit saved from an unlocked Build replaces
   the anatomy and drops any training, even a generation
   that finished while Build was open, so no genome outlives its anatomy.
2. Train setup opens Training.
3. Each finished generation is saved. Once the Creation has trained at
   least one generation it is locked (`CreationLock.IsLocked`, #369): the
   anatomy stays as the trained model needs it, so the model cannot
   be lost by accident. Only what changes the model is locked: joints can
   still move and cameras can still be aimed (#638). The lock is derived from the training, not stored.
4. Leaving Training before the first generation finishes leaves the Creation
   unlocked.
5. Later Train or Simulate sessions start from the locked Build state.
6. Unlock is a destructive App operation: after hold-to-confirm it removes
   the trained model/history and returns the same Creation to unlocked Build.

`Evolver` reports training progress; it does not know about the lock. The
lock follows from the saved training alone, so there is no separate lock
transition to keep in step with it.

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
    point and the ground top, counted once the creature has landed
    (clearance at most `TrialMeasurement.LandedClearance`), so the starting
    drop doesn't count; a crawler scores 0.
  - `IsValid` — false when physics blew up (#650): a sample was NaN or
    infinite, or the centre or lowest point moved further in one tick than
    `TrialMeasurement.MaxPlausibleSpeed` (10 000 units/s) allows. Motors
    turn at most 6 rad/s and the Worm moves under 200 units/s, so only a
    blow-up gets near the limit. Once invalid, the trial stops measuring.
  - `Fitness` — what the GA scores: `Distance`, or negative infinity for an
    invalid trial so it ranks below every valid one. `Evolver` logs each
    invalid trial with its generation and candidate. Invalid results never
    become `BestRun`, never count toward `MeanFitness`, and must never be
    shown as real results (for example in Stats, #541).
- `Evolver.BestRun` is the `TrialResult` of the best genome so far.
  `TrainingHost` persists it as `TrainingStateDef.BestRun` (`TrainingRunDef`, with
  `MapId` `flat` until more maps exist) so the Creations card can show it.
- `TrialController` (`project/src/sim/TrialController.cs`) is a `Node` that
  times a fixed-duration trial (`TrialDurationTicks`, default 600 ≈ 10s at
  60Hz) for one `Creature` instance at a time. It does **not** own creature
  creation/destruction or brain assignment — callers are responsible for
  that. `StartTrial(creature)` calls `Creature.ResetPose` (teleports
  every node and beam body back to its built shape and rotation, zeroes velocity,
  and shifts the whole creature so its lowest point is
  `TrialController.StartClearance`, 6 creature units, above the ground) and
  resets the `TrialMeasurement` from the creature's current
  `CenterOfMass.X`. Every trial, in every parallel slot, starts from this
  same small drop (#649). The fall counts as trial time; distance is
  measured from the start X, so the drop doesn't change fitness. Each tick it records `CenterOfMass.X` and the clearance
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
  fitness, random)` keeps the fittest `elitismCount` genomes unchanged
  (only genomes with a finite fitness can be elites), then fills the rest via tournament selection, the configured crossover
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
  (current generation's average over valid trials, 0 if none were valid), and raises
  `GenerationCompleted`/`NewBestFound`, which the Training scene (see
  "The Training scene" below) subscribes to.
  - Concurrency is capped at 16 and never exceeds population size. Layer 1 is
    reserved for ground; zero-based slot `i` uses layer `2+i` and collides
    only with ground and its own slot. Camera rays see the ground only
    (mask 1), never another creature.
  - Candidate assignment is deterministic for the same seed, parallel mode,
    slot count, build, and platform. Sequential and parallel fitness parity
    is not promised because physics ordering can differ.
  - `Evolver.Start` retains a one-slot compatibility mode when no creature
    factory is supplied. Production `TrainingHost` supplies the factory and
    uses parallel evaluation.
- `TrainingHost` creates one `Evolver` with the fixed Standard training
  profile (population, trial duration, generation budget, tournament size,
  mutation rate/strength and crossover strategy). The Quick/Deep choice and
  its settings sheet were removed with the Training shell (#386); training
  setup returns with TrainSetup (#194). Uniform crossover preserves parent
  genes; Blend crossover samples continuous values between the two parent
  genes, giving a later experiment for the competing-conventions plateau
  without changing the underlying network.
  `StartEvolution()` (`TrainingHost`) runs once when the scene opens. It
  calls `Evolver.Start(...)` unless the creature has no brain (an anatomy
  without motors), in which case evolution stays idle. `Evolver.Stop()`
  halts the in-progress trial without raising any events.
- Generation/fitness are logged (`GD.Print`) and shown on the Training
  screen (see "The Training scene" below).

Training unlocks nothing today. The first slice (a second sensor-package slot
at 50 fitness) was removed in #557: every part is unlimited until achievements
arrive in 0.19 (#525).

## The Training scene (issues #51, #469, #386)

This section documents the current wiring. The target presentation is owned
by the TrainSetup and Training component READMEs under `reference design/components/`.

- Training is its own routed scene, `TrainingRoute(creationId)`, with
  `TrainingHost` (`project/src/hosts/`) as its root. `TrainingHost.tscn` instances the
  Training screen (`TrainingScreen.tscn`) and authors the world inside the
  screen's arena viewport: the backdrop, the ground and its collision shape,
  the spawn marker and the camera. The host adds the creature and the
  `Evolver` from the creation's save to that world, so leaving the scene
  frees all of them.
  - **World view.** The world renders in its own `SubViewport` through
    `UiWorldView`, so the UI layout and scale never touch physics distances
    or gravity. The viewport renders at the screen's pixel density to keep
    the creature crisp, and a tap on the arena is turned into a world
    position for part selection.
  - **Resume.** Opening it starts from the saved `TrainingStateDef`: its
    brain graph is compiled by port (`DirectBrain`, #536) and seeds the
    population, and the generation count continues. A disabled connection
    stays at 0 through mutation and crossover.
    A creation without training starts from a fresh random population.
  - **Save.** Each finished generation is saved on the thread pool (the
    file round trip would stall physics). Leaving mid-generation drops only
    the generation in progress. The save is guarded by the creation's
    training epoch, so a save still in flight when the training is reset
    is dropped.
  - A session stops after the profile's generation budget. Speed and pause
    belong to the scene and start from 1x and running each time it opens.
  - Run on its own (F6) the scene trains the built-in worm without saving.
- The Training screen's top bar shows the creation's name, the status
  ("Training · Flat ground") and Brain and Stats buttons; unlock progress
  is not shown here (#488). Beside the arena, the SignalFlow column
  shows the Senses → Brain → Outputs → Distance stages from
  `SignalFlowPresentationViewModel`. Under the arena are Pause, Speed and the
  generation caption from `TrainingPresentationViewModel`.
  - **Pause** toggles `GetTree().Paused`. This is the standard Godot
    pause mechanism: every node using the default `Pausable` process mode
    (all slot creatures, `Evolver`, and every `TrialController`) freezes
    immediately —
    physics stops advancing, so trial motion, fitness recording, and
    trial-boundary checks all stop mid-trial and resume exactly where they
    left off. The scene root is `ProcessMode.Always`, so the screen and its
    buttons (Pause included) keep responding while paused, and the
    creature and `Evolver` pin themselves back to `Pausable`.
  - **Speed** cycles a fixed 1x/2x/4x set via `Engine.TimeScale`.
    This scales every physics/process step uniformly and does not affect
    determinism, only how quickly a fixed tick budget plays out. Speed and
    pause are reset in `TrainingHost._Ready()`/`_ExitTree()` since both are
    global engine settings, not scoped to this scene.
  - **Brain** (the button or the Brain stage) opens the BrainFocus sheet;
    Android Back closes it before leaving the scene. BrainFocus shows the
    direct brain (#536): an Inputs column named by port ("Accelerometer:
    along", "Front knee: speed"), an Outputs column named by joint, the
    enabled connections and live activations. Nothing is selected at first;
    tapping an output names the two senses that drive it most, tapping a
    sense names the outputs it drives most, and other connections fade;
    tapping empty space clears the selection (`docs/UI_DIRECTION.md`). The brain cannot be edited until 0.16.0. **Stats** shows a
    placeholder notice until the Stats screen (#198).
  - There is no Reset: training is reset from Build.
- Full neural-network visualization remains out of scope (later milestone).

## Deferred future work

- Parallel slot clones remain hidden. Showing live ghost candidates and a
  solid previous-generation reference is tracked separately in issue #137;
  it must reuse this slot lifecycle rather than create another population.
