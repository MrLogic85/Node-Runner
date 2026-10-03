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
  a run (a Godot autoload; see `docs/CODE_DESIGN_PRINCIPLES.md` §4 and
  `project/src/managers/AGENTS.md`). Sim/ML code receives a `Random` from
  it explicitly rather than constructing its own.

## Product lifecycle boundary

The training engine does not decide whether a Creation is editable. The App
layer owns the durable lifecycle, guided by
`reference design/components/Navigation/README.md`. Unlike the reference
(Navigation, BuildLocked), "locked" means exactly "has trained at least one
generation", not "a training session has finished" (#369, 2026-09-30).

1. An unlocked Build autosaves and opens Train setup through Start training.
   Saving a Creation never needs a finished creature; only training does
   (`CreatureReadiness`, #515). Saving an edit keeps any training, even a
   generation that finished while Build was open, and refits its brain to
   the edited anatomy (#516, `docs/CREATURE_MODEL.md` → "A rebuild keeps
   the brain"). Generation, latest and best are kept; Training then resumes
   from the refitted brain.
2. Train setup opens Training.
3. Each finished generation is saved. Once the Creation has trained at
   least one generation it is locked (`CreationLock.IsLocked`, #369): the
   anatomy stays as the trained model needs it, so the model cannot
   be lost by accident. Only what changes the model is locked: joints can
   still move and cameras can still be aimed (#638). The lock is derived from the training, not stored.
4. Leaving Training before the first generation finishes leaves the Creation
   unlocked.
5. Later Train or Simulate sessions start from the locked Build state.
6. Unlocking keeps the training (#371). The padlock in a locked Build asks
   once (a plain confirm, no hold) and then opens the body for this Build
   visit only (`BuildViewModel.Unlock`). Nothing about it is saved: edits
   keep the training through step 1, and the Creation is locked again the
   next time Build opens it. A Creation that is unlocked and not changed
   trains on from its saved state. Reset training, in the overflow menu, is
   the only way to start over; it asks first with a press-and-hold (#687).

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
    `TrialMeasurement.MaxPlausibleSpeed` (10 000 units/s) allows. Pistons
    move at most their Max speed (200 units/s by default) and the Worm
    crawls well under that, so only a blow-up gets near the limit. Once invalid, the trial stops measuring.
  - `Fitness` — what the GA scores: `Distance`, or negative infinity for an
    invalid trial so it ranks below every valid one. `Evolver` logs each
    invalid trial with its generation and candidate. Invalid results never
    become `LatestRun` or the best, never count toward `MeanFitness`, and must never be
    shown as real results (for example in Stats, #541).
  - `Distance`, `TopSpeed` (units/s), `Elevation` and `Fitness` are in
    world units. Text the player reads shows them in metres (see
    `docs/GLOSSARY.md` → Metre).
- **Latest and best ever (#479).** Training is noisy, so a later
  generation can do worse than an earlier one; that is not a bug. The
  saved training keeps two records for the map it trained on (`flat`
  until more maps exist):
  - **Latest:** the best trial of the most recently finished generation:
    `Evolver.LatestGenome`/`LatestRun`, saved as `TrainingStateDef.Brain`
    and `Latest` (`TrainingRunDef`). It can go down. The Creations card
    and Build's training summary show it, and Simulate and the warm start
    use its brain, because that is what the creature can do now.
  - **Best ever:** the furthest any generation got, and which generation
    that was: `Evolver.BestFitness`/`BestGeneration`, saved as
    `TrainingStateDef.Best` (`TrainingBestDef`). It never goes down. The
    Training top bar's "Best" shows it, and later Stats.
  - `TrainingStateDef.Record` is the rule: every finished generation
    replaces latest, and replaces the best only when it goes further. A
    generation without a valid trial has no latest, so it isn't saved.
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
  in deterministic fixed slots. Slot 0 reuses the scene's creature and each
  additional slot is a clone; every slot owns one `TrialController`.
  A completed slot receives the next pending genome in index order until the
  generation is complete, then `GeneticAlgorithm.NextGeneration` starts the
  next generation automatically. `Evolver` tracks `Generation`,
  `BestFitness` and `BestGeneration` (the best ever, across all
  generations), `LatestGenome` and `LatestRun` (the best valid trial of the
  latest finished generation), and `MeanFitness` (current generation's
  average over valid trials, 0 if none were valid), and raises
  `GenerationCompleted`/`TrainingProgressChanged`, which the Training scene
  (see "The Training scene" below) subscribes to.
  - Every shadow runs at once: there is one slot per candidate, and
    `Evolver.Start` rejects a population above `Creature.MaximumShadows`
    (32). Layer 1 is the ground; every creature body uses layer 2 with mask
    1, so a body hits the ground and nothing else, neither its own parts
    nor another shadow. Camera rays see the ground only (mask 1).
  - Measured on a Galaxy S25 with the Worm (#384): 32 hidden shadows keep
    a 10 s generation at 8.3 ms frames (120 Hz) and about 366 MiB PSS;
    drawing all 32 at 30 % opacity raised p95 to 16.6 ms and PSS to about
    436 MiB. With simplified shadows (#385) 32 shadows stay at 8.3 ms and
    about 366 MiB.
  - Followed shadow (#385): one shadow is drawn in full, two z steps above
    the others, and feeds signal flow, the brain and part selection; every
    other shadow is drawn simplified (`docs/CREATURE_MODEL.md` → "Drawing
    as a shadow") at `alpha_shadow`. Shadow `i` is slot `i`, which runs
    candidate `i`. The default is shadow 1 (slot 0): the resumed genome or
    the elite `GeneticAlgorithm` puts first, i.e. the previous best, and in
    a fresh generation 0 the first perturbed shadow. `Evolver.Follow` is the only way
    it changes, so a new leader never takes it, and it stays on that slot
    across generations. `Evolver` exposes `FollowedShadow`,
    `HasPreviousBest` and `ShadowDistances`; `TrainingPresentationViewModel`
    turns them into `ShadowStanding` rows (followed, leader = furthest
    running shadow, previous best) and `Follow(number)` for the shadow strip
    (#387). The camera frames the followed shadow (see "The Training
    scene" → Camera).
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
- **Generation 0 (#537).** A new Creation has no trained brain, so its
  base brain is the passive one every new port starts with (#535): all
  weights 0, positions at the built pose, strength at bias −4 (about 2%).
  `GenerationZero` (`libs/NodeRunner.ML/Brains/`) builds the first
  population from it, and `Evolver.Start` uses it whenever there is no
  saved brain to resume:
  - The last shadow runs the base brain unchanged, as a reference that
    stays near 0 m. Generation 0 never changes the base brain.
  - Every other shadow perturbs it: Gaussian noise on every weight
    (σ 1.5) and bias (σ 0.5), covering position and strength outputs.
  - Each perturbed shadow also wakes one strength output, chosen at
    random, to a bias between −1 and 3 (about 27–95% of its Strength).
    So every perturbed shadow can move, and no generation 0 stands still.
  - The spread was chosen with the Worm, headless over three seeds: every
    perturbed shadow moved, the base shadow stayed at 0 m, and the best
    reached about 3–4 m. Weight noise from σ 0.5 to 3 gave similar
    distances, so the middle was kept; the owner confirmed the spread on
    a Galaxy S25.
  - Resuming a saved brain is not generation 0; see "Resume" below.
- Generation/fitness are logged (`GD.Print`) and shown on the Training
  screen (see "The Training scene" below).

Training unlocks nothing today. The first slice (a second sensor-package slot
at 50 fitness) was removed in #557: every part is unlimited until achievements
arrive in 0.19 (#525).

## The Training scene (issues #51, #469, #386)

This section documents the current wiring. The TrainSetup and Training
component READMEs under `reference design/components/` guide its presentation.

- Training is its own routed scene, `TrainingRoute(creationId)`, with
  `TrainingHost` (`project/src/hosts/`) as its root. `TrainingHost.tscn` instances the
  Training screen (`TrainingScreen.tscn`) and authors the world inside the
  screen's arena viewport: the background, the ground and its collision
  shape, the ruler, the spawn marker and the camera. The host adds the
  creature and the `Evolver` from the creation's save to that world, so
  leaving the scene frees all of them.
  - **Camera (#668, #675).** `ArenaCamera` frames the followed shadow
    through `ArenaFraming`, read every frame from its centre
    (`Creature.CenterOfMass`, the point its distance is measured from) and
    its box (`Creature.Bounds`, the node colliders). Only the followed
    shadow decides the framing; the others may leave the view.
    - *Sideways* `ArenaFollow` keeps the centre 43% from the left, as in
      the reference. Smoothing has two stages, both in pure code (the
      `Camera2D`'s own smoothing is off): the focus eases toward the
      centre (`EaseRate`), then the shown point toward the aim
      (`ShownEaseRate`). Together they damp a gait's wobble so the
      view never shakes, and a switch to another shadow
      (`ArenaCamera.Retarget`, on `FollowedShadowChanged`) glides in and
      settles instead of jumping. Both stages trail a moving target, so the
      aim leads it by the creature's slowly eased speed times that lag: a
      fast creature stays at 43% instead of drifting off the right edge.
    - *Zoom* fits the shadow's box inside side margins and below a top
      margin with headroom to spare, and widens further with its speed (one
      second of travel, `SpeedLookaheadSeconds`). Zoom 1, the closest, shows
      one world unit per view pixel; 0.25, the farthest, shows four times
      as much. It widens quickly; it narrows
      only after the shadow has needed less room for 1.5 s, then slowly, so
      a stretching gait does not make it pump.
    - *Height:* the ground stays 80% down the view at any zoom, so zooming
      never bobs the view. A shadow that rises into its headroom does not
      move the camera; once its top passes the top margin the camera eases
      up after it, and back down when it lands.
    - When the followed shadow starts a new trial
      (`Evolver.FollowedTrialStarted`) the camera cuts back to the start,
      zoom and height included, since a new trial is a new scene (owner
      decision). It also cuts when the arena changes size, and holds still
      while training is paused. Everything runs
      on scaled time, so 2x and 4x look the same, only faster.
  - **Ground and background.** The ground's collider is a
    `WorldBoundaryShape2D` through the `Ground` node, so it has no end; its
    fill and edge reach ±1 000 000 units (10 km), far past any trial, and the
    fill as deep, so no zoom shows its bottom. The
    background is a plain `ArenaBackground` fill on a `CanvasLayer` behind
    the world, so it does not move with the camera. There is no grid
    (owner decision, `docs/UI_DIRECTION.md`).
  - **Ruler.** `ArenaRuler` draws `DistanceRuler`'s marks along the ground
    edge: a long tick every metre and a minor one every half metre, counted
    from where the visible creature's centre starts each trial (0 m),
    negative behind it. Every metre is labelled ("3 m"), or every 2, 5,
    10, … m when the camera zooms out so far that labels would overlap.
    Labels come back closer only with 20% room to spare, so a zoom resting
    at the switch does not make them flicker. Ticks and labels keep their
    screen size at any zoom. It draws only what
    the camera shows.
  - **World view.** The world renders in its own `SubViewport` through
    `UiWorldView`, so the UI layout and scale never touch physics distances
    or gravity. The viewport renders at the screen's pixel density to keep
    the creature crisp, and a tap on the arena is turned into a world
    position for part selection. A part takes a tap within 16 px of it on
    screen at least, so a joint or sensor stays easy to hit when the camera
    zooms out.
  - **Resume (warm start, #538).** Opening it starts from the saved
    `TrainingStateDef`: its brain graph is compiled by port
    (`DirectBrain`, #536) and the generation count continues. The saved
    brain, the latest generation's best, is the elite, the parent of the
    next generation: `GeneticAlgorithm.FromElites` runs it unchanged as
    shadow 1 and fills the other shadows with its mutated children, so
    training picks up where it stopped instead of starting over. A
    disabled connection stays at 0 through mutation and crossover. The
    Evolver's best ever starts from the saved `best`, so the top bar keeps
    showing it and a worse generation never lowers it.
    A creation without training starts at generation 0 (see "Generation 0" above).
  - **Save.** Each finished generation is saved on the thread pool (the
    file round trip would stall physics), as one atomic file write. Leaving
    or closing the app mid-generation drops only the generation in
    progress; reopening continues from the last finished one. The save is guarded by the creation's
    training epoch, so a save still in flight when the training is reset
    is dropped. Saves for one creation land in order, and reading a
    creation (`ICreationUpdateCoordinator.Get`) waits for them, so Build
    opened right after Training never shows a stale lock or summary (#370).
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

- Shadow visuals must reuse this slot lifecycle rather than create another
  population.
