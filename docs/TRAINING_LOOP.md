# Training loop

The rules training follows: when a Creation locks, how a trial is scored,
how generations evolve and what the Training scene keeps. Values are in the
code each rule names. How training looks is in `docs/UI_DIRECTION.md` and
`docs/WORLD_VISUALS.md`, and the ML ideas behind it are in
`docs/ML_CONCEPTS.md`.

## Product lifecycle boundary

- The App layer decides whether a Creation is editable; the training
  engine does not.
- Locked means "has trained at least one generation"
  (`CreationLock.IsLocked`). The lock follows from the saved training and
  is not stored, so `Evolver` knows nothing of it.
- Only what changes the network is locked, so a trained network cannot be
  lost by accident. `docs/BUILD_MODE.md` → "Rules" says what Build
  still allows.
- Saving never needs a finished creature; only training does
  (`CreatureReadiness`). Saving an edit keeps the training and refits its
  network to the edited anatomy (`docs/CREATURE_MODEL.md` → "A rebuild
  keeps the network").
- Each finished generation is saved. Leaving before the first one finishes
  leaves the Creation unlocked.
- Unlocking keeps the training: the padlock in a locked Build asks once and
  opens the body for this visit only, and saves nothing
  (`BuildViewModel.Unlock`). Reset training, in Build's overflow menu and
  behind a confirm dialog, is the only way to start over; Training has no
  Reset.
- Build's play button autosaves and opens Train setup, locked or not.
  Start saves Train setup's values and opens Training in its place, so
  Back from Training returns to Build. Back from Train setup discards its
  changes. Its defaults are in `docs/SAVE_FORMAT.md` → "`settings.json`".
- Simulate plays the saved network with one shadow until the player leaves
  and saves nothing, not even the settings. An untrained Creation has no
  Simulate, so Start trains.
- A creature with no powered part, even one with Memory cells, may train,
  and every shadow stands still. Train setup and the Network sheet warn
  (`PortSignals.AnyPowered`).

## Trial

`TrialMeasurement` records every tick of a trial into a `TrialResult`, in
world units; text the player reads shows metres (`docs/GLOSSARY.md` →
Metre).

- *The centre* is the average position of the creature's joints. After a
  tear only the main piece counts (`docs/CREATURE_MODEL.md` → "Body").
- **The score is `Distance`**, the running maximum of the centre's forward
  distance from its start. It rewards peak progress without penalising a
  creature that surges forward and then settles back.
- **Changing the score** changes what everything tuned on it rewards:
  generation 0's full-strength start and new-port exploration were tuned
  for distance, and saved bests scored the old way no longer compare with
  new ones.
- **The shown distance is `FrontDistance`**, how far ahead of its start the
  front-most point is now, never below 0. Everything the player reads as a
  distance reads it, so the number matches where the creature's nose
  stands on the scale. It is never the score.
- Speeds are taken over a sliding window, so one-tick physics jolts do not
  dominate. The current speed is not a `TrialResult` field, since only the
  followed run shows it.
- `Elevation` is the largest gap between the creature's lowest point and
  the ground once it has landed, so a crawler scores 0.
- **Invalid** means physics blew up: a sample was not finite, or a tracked
  point moved further in one tick than `MaxPlausibleSpeed` allows. An
  invalid trial stops measuring, and its fitness is negative infinity, so
  it ranks below every valid one. It never becomes the latest or the best,
  never counts toward `MeanFitness` and is never shown.
- **Tears:** the first tear freezes every metric at its value before it
  (`TrialMeasurement.Freeze`; a new metric is added there). The creature
  runs on to the end and is still checked for blow-ups, but a tear alone
  does not make the trial invalid, so a creature keeps the progress it
  made whole.
- **Timing:** a trial has a fixed length (`TrialController`) and starts
  from rest in the built shape, its lowest point a clearance above the
  ground. The fall counts as trial time but, since distance is measured
  from the start, not as fitness.
- **The same network runs the same trial**, however it is started: a reset
  leaves no physics state behind, and a trial begins before any creature
  drives. `.claude/rules/sim.md` says how.

### Latest and best ever

Training is noisy, so a later generation can do worse than an earlier one;
that is not a bug. The saved training keeps two records for the map it
trained on:

- **Latest:** the best trial of the most recently finished generation
  (`TrainingStateDef.Network` and `Latest`). It can go down. The Creations
  card, Simulate and the warm start use it, because it is what the
  creature can do now.
- **Best ever:** the highest score any generation got, and which generation
  that was (`TrainingStateDef.Best`). It never goes down. The best marker
  shows it, and it counts only on the map it was reached on.
- `TrainingStateDef.Record` is the rule: every finished generation replaces
  latest, and replaces the best only when it scores higher. A generation
  without a valid trial is not saved.
- Both records keep the score and the shown distance. The score alone picks
  the best. The best's shown distance is the furthest any latest run's
  front has reached on that map (`Evolver.BestShownDistance`), so the best
  marker never moves back and never reads below Latest.

## Generations

- **One population.** `Evolver` runs every shadow at once, one slot per
  candidate: slot 0 is the scene's creature, each other slot a clone with
  its own `TrialController`. A generation ends when every slot has
  finished. Shadows, their visuals and anything else that runs a
  generation use these slots, never a second set of creatures.
- **Determinism.** Candidate assignment is deterministic for the same seed,
  slot count, build and platform. A ranged Run length draws each
  generation's length from the Evolver's seeded RNG after the generation's
  genomes are made, and Fixed draws nothing, so the draw never shifts the
  genomes' RNG sequence.
- **The GA** (`GeneticAlgorithm`) keeps the fittest genomes unchanged as
  elites and fills the rest by tournament selection, Uniform crossover,
  which keeps parent genes, and Gaussian mutation. Only a finite fitness
  can be an elite. `TrainingHost` builds one `Evolver` from Train setup's
  values (`EvolutionSetup`, which holds the fixed ones). There is no
  generation budget.
- **Run length:** Best is plain distance, so a longer Run length reaches
  further. A ranged one mixes lengths into Best, so a Best set on a long
  run is rarely beaten on a short one. That is expected.
- **Collision:** a body hits the ground and nothing else, neither its own
  parts nor another shadow. Camera rays see only the ground.
- **Drawing limits the frame rate, not physics.** So Train setup warns by
  shadows × parts (`ShadowsBudget`), and Training draws only the shadows
  on the strip's current page plus the followed one; the rest race
  undrawn (`Evolver.DrawOnly`).
- **Followed shadow:** one shadow is drawn in full and feeds the Status
  panel, the network view and part selection (`docs/WORLD_VISUALS.md` →
  "Drawing as a shadow"). The player numbers shadows from 1.
  - Every generation opens following shadow 1, slot 0. The player picks
    another with `Evolver.Follow`, and a new leader never takes the camera.
  - Shadow 1 is the elite when the generation opens with one, and replays
    its network exactly. Under a ranged Run length it runs at the new
    length and can score less.
  - A generation with no elite has no replay: in generation 0, shadow 1
    is the first perturbed shadow, and after a generation with no valid
    trial it is an ordinary bred child.
- **Generation 0.** A new Creation has no trained network, so its base
  network holds the built pose at full strength: every drive holds the
  pose or stands still, and every strength output is near full. This is
  how robots start in robotics ML: stiff in a default pose, the network
  learning offsets from it. A full-strength base learned far faster than a
  near-passive one. `GenerationZero` builds the first population from it
  whenever there is no saved network to resume, and holds the values.
  - The last shadow runs the base unchanged, as a reference that stays
    near 0 m.
  - Every other shadow adds noise to every weight and bias, large enough
    to saturate the outputs into visible, decisive motion, so every
    powered part moves from the start.
  - A Piston's length output, a sigmoid, takes more noise, so it spreads
    its lengths as widely as a tanh output would. A Memory cell's
    damped outputs take the noise unscaled, so they can rest between their
    ends.
- **Warm start.** Opening Training resumes from the saved
  `TrainingStateDef`; the generation count and the best ever carry on.
  The saved latest network is the elite, shadow 1, its mutated children
  fill the other shadows (`GeneticAlgorithm.FromElites`), and new ports
  are explored. A disabled connection stays at 0 through mutation and
  crossover.

### New ports are explored

A part or sensor added to a trained network starts almost passive
(`docs/CREATURE_MODEL.md` → "New ports start almost passive"), so mutation
alone would take hundreds of generations to find a use for it.

- In every generation that opens with an elite, `NewPortExploration`
  redraws the new ports' genes in every child as generation 0 draws them.
  The elite stays unchanged and keeps the trained gait. A generation with
  no elite explores nothing.
- Which ports are new is read from the elite, so nothing is saved. A part
  is new while its strength, or a Memory cell's write, keeps its passive
  bias exactly, or while a Stepper's frequency keeps bias 0 with no weight
  into it (`PortSignals`). Detection depends on those start values staying
  exact. All of a new part's outputs are redrawn. An input is new while
  every weight out of it is exactly 0; its weights are redrawn. A disabled
  connection is never redrawn.
- Once an explored child becomes the elite, its ports read as used and
  mutate as usual. When generation 0's unchanged base wins, every weight is
  0, so every input and Stepper reads as new until something beats it.
- Accepted cost: while no explored child beats the elite, every child's
  new-port genes are noise. For a new sensor that noise reaches the trained
  outputs too, so the gait may stall until a draw wins.

## The Training scene

- Training is its own routed scene, `TrainingRoute(creationId, mode)`,
  rooted at `TrainingHost`. The host adds the creature and the `Evolver`,
  so leaving the scene frees them all.
- **No speed-up.** Physics always steps 1/60 s at real time.
  `Engine.TimeScale` would stretch each step instead of running more of
  them, which would change fitness with speed and make stiff Springs blow
  up. Training goes faster by racing more shadows per generation.
- **The world has its own viewport** (`UiWorldView`), so UI layout and
  scale never touch physics distances or gravity.
- **One sample per refresh.** Each refresh samples the followed network
  once into one `NetworkPortValues`; the Inputs and Outputs stages,
  NetworkFocus and the part callout all read it, so they always agree.
- **The camera moves on physics ticks** (`ArenaCamera`), as the creatures
  do, so on a fast screen the world never judders against it.
- **Save.** Each finished generation is saved off the main thread, so the
  file round trip never stalls physics, as one atomic file write. Leaving
  mid-generation drops only the generation in progress. A save still in
  flight when the training is reset is dropped (the training epoch guards
  it). Saves for one creation land in order, and reading a creation
  (`ICreationUpdateCoordinator.Get`) waits for them, so Build opened right
  after Training never shows a stale lock.
- **Pause** freezes the trial mid-run and resumes exactly where it left
  off; the screen keeps responding. Each visit starts unpaused.

## Scene decisions

The choices each element of the Training scene made, with why. Its scenes
and their code hold the layout, numbers and copy.

- **Camera** (`ArenaFraming`, `ArenaFollow`): frames the followed shadow
  from its centre and its box; the other shadows may leave the view.
  - It follows the centre on both axes, eased in two stages, so a gait's
    wobble never shakes the view and a switch to another shadow glides in.
    Each aim leads by the eased speed, so it keeps up with a moving shadow.
  - The ground line never rises above a line low in the view, so a shadow
    on the ground sits low and a hopping gait does not bob the view.
  - Zoom fits the box, widened by a second of travel. It widens quickly and
    narrows only late and slowly, so a stretching gait does not make it
    pump. There is no farthest zoom; only a physics blow-up is stopped.
  - The room speed adds is shared, two thirds in front and a third behind,
    so a fast shadow still shows where it came from.
  - A safe zone keeps the box clear of the view's sides and top, as
    platformer cameras keep a player clear of the screen's edges. It moves
    the view only when the follow alone would not, never for a shadow at
    rest, and after a switch only once the glide has brought the box
    inside, so the switch never jumps.
  - While a part callout shows, a shelf keeps room for it at the top
    (`docs/WORLD_VISUALS.md` → "Training arena"). It lowers the ground
    first, clear of the distance scale, and zooms out only for what is
    still missing. It is held a while after the selection clears and then
    given back slowly, so tapping from part to part does not pump the
    view.
  - It cuts back to the start when the followed shadow starts a new trial,
    and cuts when the arena changes size. It holds still while training is
    paused, except for the shelf.
- **Shadow strip:**
  - A cell's bar is its shown distance against this generation's leader or
    1 m, whichever is farther, so a leader a few millimetres out does not
    swing every bar full. Bars are never measured against the best ever,
    which may come from another Run length.
  - Once the followed run has torn, its cell gets a `danger` cap where its
    distance froze.
  - A sort ranks by distance at that moment and holds until the next one,
    so cells never jump while the player watches. Each generation starts
    over in shadow order.
- **Scales** (`docs/WORLD_VISUALS.md` → "Training arena"): distance and
  height read wherever the camera looks. Distance is counted from where the
  followed creature's front starts each trial, negative behind it. Height
  is counted from the ground's top, even when the ground is out of view.
  Labels step so they never overlap, and a closer step comes back only with
  room to spare, so a zoom resting at the switch does not flicker
  (`DistanceRuler`).
- **Best marker:** marks the best ever on this map at its shown distance,
  hidden until that distance is known.
- **Crash marker:** once the followed run has torn, marks where its
  distance froze, in Training and Simulate alike; it goes when a new trial
  or shadow starts whole.
- **Start sign:** points the way to go, so a creature that walks backwards,
  and so shows 0 m, is seen to have gone the wrong way.
- **Selection:** a tap on the arena selects a part of the followed shadow
  with Build's hit rules (`docs/BUILD_MODE.md` → "Rules"), and a
  part takes a tap within a minimum on-screen reach, so it stays easy to
  hit zoomed out.
- **Status panel** (`SignalFlowPresentationViewModel`):
  - An Outputs dial shows a driven part's drive, its first output that is
    not Strength.
  - Inputs and Outputs show empty with no live network, and hold on pause.
  - A "+N" chip works like a text ellipsis: every meter shows while they
    all fit, and with more the chip counts the rest.
  - A stage with nothing to show says so rather than showing an empty box.
  - The Network card counts learnable parameters, which change only when
    the ports do.
  - The Distance stage's track strip fills towards the best ever. Once the
    followed run has torn, the stage reads the crash in `danger`, the word,
    not colour alone, carrying the state.
- **Slow-motion warning** (`SlowMotionWatch`): when physics falls behind
  real time for a while, the arena shows a Warning chip. It stays long
  after the last slow stretch, so a player who only glances at the arena
  still sees it.
- **NetworkFocus:** opened by the Network button or stage; Android Back
  closes it before leaving the scene. It refreshes often enough that a
  Stepper's tick still shows its rise and drop. Tapping a neuron names the
  ones it is most strongly connected to and fades the other connections;
  tapping empty space clears the selection.
- **Simulate:** no `Evolver`. The creature plays the saved network in one
  trial that never ends, alone, with no generation, shadow strip or save,
  and the best marker stays at the saved best on this map.
