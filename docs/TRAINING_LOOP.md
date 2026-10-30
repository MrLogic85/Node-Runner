# Training loop

How training behaves: when a Creation locks, how a trial is measured, how
generations evolve and what the Training scene does. How it looks is in
`docs/UI_DIRECTION.md` and `docs/WORLD_VISUALS.md`; the ML ideas behind it
are in `docs/ML_CONCEPTS.md`.

## Product lifecycle boundary

The training engine does not decide whether a Creation is editable; the App
layer owns that. "Locked" means "has trained at least one generation".

1. Build's play button autosaves and opens Train setup, locked or not.
   Saving never needs a finished creature; only training does
   (`CreatureReadiness`). Saving an edit keeps any training and refits its
   network to the edited anatomy (`docs/CREATURE_MODEL.md` → "A rebuild
   keeps the network"); Training resumes from the refitted network.
2. Train setup (`TrainSetupRoute`) sets Shadows, Run length and its
   mode, filled from the Creation's saved values or, before its first
   Start, the Shadows default from Settings and 10 s (`docs/SAVE_FORMAT.md` →
   "`settings.json`"). Start
   saves them and opens Training in Train setup's place, so Back from
   Training returns to Build; Back from Train setup discards the changes.
   **Simulate** plays the saved network with one shadow until the player
   leaves and saves nothing, not even the settings; an untrained Creation
   has no Simulate, so Start trains. A creature with no powered part, even
   one with Memory cells, may train: every shadow stands still, and Train
   setup and the Network sheet warn "Warning, no powered parts added!
   There is nothing to train" (`PortSignals.AnyPowered`).
3. Each finished generation is saved. Once a Creation has trained a
   generation it is locked (`CreationLock.IsLocked`), so the trained
   network cannot be lost by accident. Only what changes the network is
   locked; `docs/BUILD_MODE.md` → "Locked Build" says what Build still
   allows. The lock follows from the saved training and is not stored, so
   `Evolver` knows nothing of it. Leaving before the first generation
   finishes leaves the Creation unlocked.
4. Unlocking keeps the training. The padlock in a locked Build asks once
   and opens the body for this Build visit only (`BuildViewModel.Unlock`);
   nothing about it is saved. Reset training, in the overflow menu, is the
   only way to start over, behind a confirm dialog.

## Trial

- `TrialMeasurement` records every tick of a trial into a `TrialResult`.
  *The centre* is the average position of the creature's joints; after a
  tear only the main piece counts (`docs/CREATURE_MODEL.md` → Beam →
  Tearing).
  - `Distance`: the **running maximum** forward distance of the centre
    from its start. This is the fitness: it rewards peak progress without
    penalising a creature that surges forward and then settles back.
  - `TopSpeed`: the highest forward speed of the centre over a sliding
    half-second window, so one-tick physics jolts do not dominate.
  - `TrialMeasurement.CurrentSpeed`, not a `TrialResult` field since only
    the followed run shows it: the centre's signed forward speed now over
    the same window, 0 until the window is full.
  - `Elevation`: the largest gap between the creature's lowest point and
    the ground once it has landed; a crawler scores 0.
  - `FrontDistance`: how far ahead of its start the creature's front-most
    point is at the latest tick, never below 0. It is the **shown
    distance**: the scale, the shadow strip, the Distance stage, the best
    marker and the Creations card read it, so the number on screen matches
    where the creature's nose stands on the scale. It is not the score.
  - `IsValid`: false when physics blew up: a sample was NaN or infinite,
    or a tracked point moved further in one tick than
    `TrialMeasurement.MaxPlausibleSpeed` allows. An invalid trial stops
    measuring.
  - **Tears:** the first tear freezes `Distance`, `TopSpeed`, `Elevation`,
    `FrontDistance` and the current speed at their values before it
    (`TrialMeasurement.Freeze`). The creature runs on to the end of the
    trial and is still checked for blow-ups, but a tear alone does not make
    the trial invalid. A creature that breaks keeps the progress it made
    whole, so a tear at or after its peak costs nothing.
  - `Fitness`: `Distance`, or negative infinity for an invalid trial so it
    ranks below every valid one. Invalid results never become the latest
    or the best, never count toward `MeanFitness` and are never shown.
  - All are in world units; text the player reads shows metres
    (`docs/GLOSSARY.md` → Metre).
- **Latest and best ever.** Training is noisy, so a later generation can
  do worse than an earlier one; that is not a bug. The saved training
  keeps two records for the map it trained on:
  - **Latest:** the best trial of the most recently finished generation
    (`TrainingStateDef.Network` and `Latest`). It can go down. The
    Creations card shows it, and Simulate and the warm start use its
    network, because that is what the creature can do now.
  - **Best ever:** the highest score any generation got, and which
    generation that was (`TrainingStateDef.Best`). It never goes down. The
    best marker shows it.
  - `TrainingStateDef.Record` is the rule: every finished generation
    replaces latest, and replaces the best only when it scores higher. A
    generation without a valid trial is not saved.
  - Both records keep the score and the shown distance. The score alone
    picks the best; the best's shown distance is the furthest any latest
    run's front has reached on that map (`Evolver.BestShownDistance`), so
    the best marker never moves back and never reads below Latest.
- **Trial timing.** `TrialController` times a fixed-length trial (the Run
  length; 600 ticks is 10 s). Every trial starts from rest in the built
  shape with its lowest point `TrialController.StartClearance` (6 units)
  above the ground. The fall counts as trial time, but distance is
  measured from the start, so it does not change fitness.
- **The same network runs the same trial.** The followed shadow replays
  the previous best, so it must end where that run ended: a reset leaves
  no physics state behind, and a trial begins before any creature drives,
  however it is started (`.claude/rules/sim.md`).

## Generations

- `GeneticAlgorithm` keeps the fittest `elitismCount` genomes (1)
  unchanged and fills the rest by tournament selection, crossover and
  Gaussian mutation (`docs/ML_CONCEPTS.md`). Only a finite fitness can be
  an elite.
- `TrainingHost` creates one `Evolver` from the Train setup values
  (`EvolutionSetup`): Shadows is the population and Run length, one length
  or a range drawn from each generation (below), the trial duration.
  Tournament size (3, or the Shadows count if smaller), mutation
  rate (0.1) and strength (0.3) are fixed, with Uniform crossover, which
  keeps parent genes. There is no generation budget. Best is plain
  distance, so a longer Run length reaches further.
- **Ranged Run length:** each generation draws one length, a 5 s step
  inside the range with both ends included, and all its shadows run it.
  The draw takes the Evolver's seeded RNG after the generation's genomes
  are made, so a seed gives the same lengths, and so the same run, every
  time; Fixed draws nothing, so it leaves the genomes' RNG sequence alone.
  Best then mixes lengths: a Best set on a long run is rarely beaten on a
  short one, and the elite, run again at the new length, can score less.
- `Evolver` runs every shadow at once, one slot per candidate, up to
  `Creature.MaximumShadows` (100): slot 0 is the scene's creature, each
  other slot a clone with its own `TrialController`. The generation ends
  when every slot has finished. These slots are the only population:
  shadows, their visuals and anything else that runs a generation use
  them, never a second set of creatures. Candidate assignment is
  deterministic for the same seed, slot count, build and platform.
  - Drawing limits the frame rate, not physics. `ShadowsBudget` warns
    under Train setup's Shadows slider by shadows × parts, from a 30-part
    reference: smooth up to 32 shadows, a caution up to 64, then too many.
  - **Collision layers:** layer 1 is the ground; every creature body uses
    layer 2 with mask 1, so a body hits the ground and nothing else,
    neither its own parts nor another shadow. Camera rays see the ground
    only.
  - **Followed shadow:** one shadow is drawn in full, above the others,
    and feeds the Status panel, the network view and part selection; the
    others are drawn simplified (`docs/WORLD_VISUALS.md` → "Drawing as a
    shadow"). The player numbers shadows from 1; shadow 1 is slot 0, the
    elite, which replays the previous best exactly (in a fresh generation
    0 it is the first perturbed shadow). Every generation opens following
    shadow 1; the player picks another with `Evolver.Follow`, and a new
    leader never takes the camera.
  - **Shadow strip:** a cell per shadow, its bar the shown distance so far
    against this generation's leader or 1 m, whichever is farther, so a
    leader that has crept a few millimetres does not swing every bar full.
    Bars are not measured against the best ever, which may come from
    another run length. Only the followed cell is marked, since the lead
    changes too often; once the followed run has torn, its cell gets a
    `danger` cap where its distance froze. The strip has as many places as
    fit its width, at least 3. Past that it pages, with a "worse" button
    and a last button that sorts on the first page and pages back on later
    ones. A sort ranks by distance at that moment and holds until the
    next, so cells never jump while the player watches. Each generation
    starts over in shadow order. The caption reads "Generation N", counted
    from 1, with a bar that fills as the followed run goes by.
  - **Drawn shadows:** Training draws only the shadows on the strip's
    current page, plus the followed one; the rest race and count undrawn
    (`Evolver.DrawOnly`). Simulate shows the creature alone.
- **Generation 0.** A new Creation has no trained network, so its base
  network holds the built pose at full strength: all weights 0, every
  strength output at bias 3 (about 95%), a Servo's angle, a Motor's speed
  and a Stepper's frequency at 0 (hold the built pose, stand still, hold
  the notch), a Memory cell's target and write at 0 (so the reference
  shadow's `m` stays 0), and a Piston's position at the bias that asks for its drawn
  length, clamped to ±6 (about 0.25% short of either end). This is how
  robots start in robotics ML: stiff in a default pose, the network
  learning offsets from it. `GenerationZero` builds the first population
  from it whenever there is no saved network to resume:
  - The last shadow runs the base network unchanged, as a reference that
    stays near 0 m.
  - Every other shadow adds Gaussian noise to every weight (σ 1.5) and
    bias (σ 0.5), so every powered part moves from the start. A Piston's
    position output, a sigmoid, gets twice that noise and bias limit:
    sigmoid(2x) = (tanh(x) + 1) / 2, so it spreads its lengths as widely
    as a tanh output would.
  - A full-strength base learned far faster than a near-passive one, and
    σ 1.5 saturates the outputs into visible, decisive motion. Backprop
    would need a different start
    ([#955](https://github.com/MrLogic85/Node-Runner/issues/955)).
- **New ports are explored.** A part or sensor added to a trained network
  starts almost passive (`docs/CREATURE_MODEL.md` → "New ports start
  almost passive"), so mutation alone would take hundreds of generations
  to find a use for it. Instead, in every generation that opens with an
  elite, `NewPortExploration` redraws the new ports' genes in every child
  as generation 0 draws them. The elite stays unchanged and keeps the
  trained gait. A generation with no elite (every trial invalid) explores
  nothing.
  - Which ports are new is read from the elite, so nothing is saved. A
    powered part is new while its strength bias is exactly −4, a Memory
    cell while its write bias is, or a Stepper while its frequency bias and every weight into it are exactly
    0; all its outputs' genes are redrawn. An input is new while every
    weight out of it is exactly 0; its weights are redrawn. A disabled
    connection is never redrawn.
  - Once an explored child becomes the elite, its ports read as used and
    mutate as usual. When generation 0's unchanged base wins, every weight
    is 0, so every input and Stepper reads as new until something beats
    it.
  - Accepted cost: while no explored child beats the elite, every child's
    new-port genes are noise. For a new sensor that noise reaches the
    trained outputs too, so the gait may stall until a draw wins.

## The Training scene

- Training is its own routed scene, `TrainingRoute(creationId, mode)`,
  rooted at `TrainingHost`. `TrainingHost.tscn` authors the world inside
  the arena viewport; the host adds the creature and the `Evolver`, so
  leaving the scene frees them all.
  - **Simulate.** No `Evolver`: the creature plays the saved network in
    one trial that never ends, so there is no generation, shadow strip or
    save. The header reads "Simulating", and the best marker stays at the
    saved best on this map.
  - **Camera.** `ArenaCamera` frames the followed shadow through
    `ArenaFraming`, from its centre (Trial above) and its box. It moves on
    physics ticks, as the creatures do, so on a 120 Hz screen the world
    never judders against it. The other shadows may leave the view.
    - *Centre:* it follows the shadow's centre on both axes through one
      `ArenaFollow` per axis, eased in two stages so a gait's wobble never
      shakes the view and a switch to another shadow glides in. Each aim
      leads by the eased speed, so it keeps up with a moving shadow.
    - *Ground:* the ground line never rises above 80% down the view, so a
      shadow on the ground sits low and a hopping gait does not bob the
      view.
    - *Zoom* fits the shadow's box inside the margins, widened by one
      second of travel at its speed. Zoom 1, the closest, shows one world
      unit per view pixel. There is no farthest zoom; only a physics
      blow-up is stopped, at a view 500 m wide. It widens quickly and
      narrows only after the shadow has needed less room for 1.5 s, then
      slowly, so a stretching gait does not make it pump.
    - *Lead:* the room speed adds is shared, two thirds in front and a
      third behind, so a fast shadow keeps room behind it to show where it
      came from.
    - *Safe zone:* the shadow's box keeps 10% of the view clear to the
      sides and top, as platformer cameras keep a player clear of the
      screen's edges, but never moves a shadow at rest. It moves the view
      as little as keeps the box inside, and only when the follow alone
      would not, such as a sudden launch. After a switch to another shadow
      it holds only once the glide has brought the box inside, so the
      switch never jumps.
    - *Callout shelf:* while a part callout shows, the camera keeps room at
      the top for it (`docs/WORLD_VISUALS.md` → Part callout). It lowers
      the ground first, to at most 95% down but clear of the distance
      scale, and zooms out only for what is still missing, never shrinking
      the shadow below 25% of the view's height. When the selection
      clears, the shelf is held 1.5 s and then given back slowly, so
      tapping from part to part does not pump the view.
    - It cuts back to the start when the followed shadow starts a new
      trial, and cuts when the arena changes size. It holds still while
      training is paused, except for the callout shelf.
  - **Ground.** Flat ground (`Maps.Flat`) is the only map
    ([#540](https://github.com/MrLogic85/Node-Runner/issues/540)), and its
    ground is endless. A best ever counts only on the map it was reached
    on.
  - **Scales.** Two scales on the view's edges, so distance and height
    read wherever the camera looks (`docs/WORLD_VISUALS.md` → Training
    arena). Distance is counted from where the followed creature's front
    starts each trial, negative behind it. Height is counted from the
    ground's top, and shows true heights even when the ground is out of
    view. Labels come every 1, 2, 5, 10 … m so they never overlap; a
    closer step comes back only with 20% room to spare, so a zoom resting
    at the switch does not flicker (`DistanceRuler`).
  - **Best marker.** Marks the best ever on this map at its shown
    distance, with a flag reading "Best 4.2 m". It stays hidden until its
    distance is known.
  - **Crash marker.** Once the followed run has torn, a flag reading
    "Crash 3.4 m" marks where its distance froze, in Training and Simulate
    alike; it goes when a new trial or shadow starts whole.
  - **Start sign.** A "Start" signpost at 0 m pointing the way to go, so a
    creature that walks backwards, and so shows 0 m, is seen to have gone
    the wrong way.
  - **World view.** The world renders in its own viewport
    (`UiWorldView`), so UI layout and scale never touch physics distances
    or gravity. A tap on the arena selects a part of the followed shadow
    with the same hit rules as Build (`docs/BUILD_MODE.md` →
    Interactions), and a part takes a tap within 16 px of it on screen at
    least, so it stays easy to hit zoomed out. The selected parts' names
    show in one callout with a live bar for each of their network ports
    (`docs/WORLD_VISUALS.md` → Part callout). Each refresh samples the
    followed network once into one `NetworkPortValues`; the Inputs and
    Outputs stages, NetworkFocus and the callout all read it, so they
    always agree.
  - **Resume (warm start).** Opening Training continues from the saved
    `TrainingStateDef`: the generation count goes on, and the saved
    network (the latest) is the elite, shadow 1, with its mutated children
    filling the other shadows (`GeneticAlgorithm.FromElites`) and any new
    ports explored. A disabled connection stays at 0 through mutation and
    crossover. The best ever starts from the saved best.
  - **Save.** Each finished generation is saved off the main thread, so
    the file round trip never stalls physics, as one atomic file write.
    Leaving mid-generation drops only the generation in progress. A save
    still in flight when the training is reset is dropped (the training
    epoch guards it). Saves for one creation land in order, and reading a
    creation (`ICreationUpdateCoordinator.Get`) waits for them, so Build
    opened right after Training never shows a stale lock.
- The top bar shows the creation's name, the status
  (`docs/UI_DIRECTION.md` → "Training") and the Network and Stats
  buttons. Beside the arena the **Status** panel shows the Inputs →
  Network → Outputs → Distance stages
  (`SignalFlowPresentationViewModel`). Inputs counts the input ports and
  shows them in port order as upright bars, filled like the callout's
  port bars; the bars are empty with no live network and hold on pause.
  Outputs counts the output ports and shows a dial per driven part in
  port order, at its drive, the first output that is not Strength (a
  Servo's angle, a Motor's speed, a Stepper's frequency, a Piston's
  length); like the bars they are empty with no live network and hold on
  pause. A card's "+N" chip works like a text ellipsis: the card shows
  every meter while they all fit its row, and with more it shows the
  ones that fit beside a chip at the right edge that counts the rest. A
  creature with no inputs says "No sensors" in the Inputs card, and one
  with no driven parts "No driven parts" in Outputs, rather than an
  empty box. The Network card gives the network's size in learnable
  parameters, such as "91 parameters" or "45.2k parameters"; it changes
  only when the ports do. The Distance
  stage shows the shown distance beside the current speed, signed, such
  as "−0.8 m/s", and under them a track strip that fills towards the
  best ever (the best marker's distance) at its `ink` marker. The strip's
  full length, named at its end, is the farther of the best and the
  distance, so with no best the strip is full with the marker at 0 m,
  and past the best the marker moves left. Once the followed run has torn
  the stage reads "Crash 3.4 m" in `danger`, the word, not colour alone,
  carrying the state, and the strip freezes in `danger` with a cap where
  it stopped, without the speed. Collapsing the panel widens the arena.
  Under the arena are Pause and the generation caption.
  - **Pause** freezes the trial mid-run and resumes exactly where it left
    off; the screen keeps responding. Each visit starts unpaused.
  - **No speed-up.** Physics always steps 1/60 s at real time:
    `Engine.TimeScale` would stretch each step instead of running more of
    them, which would change fitness with speed and make stiff Springs blow
    up. Training goes faster by racing more shadows per generation.
  - **Slow-motion warning.** When a frame needs more than Godot's 8
    physics steps, physics falls behind real time. `SlowMotionWatch`
    compares physics ticks with real time in 0.5 s windows, a single hitch
    counting at most 0.25 s. After 3 s below 0.9× the arena shows a
    Warning chip, "Too many shadows!", which stays 60 s after the last
    slow stretch so a player who only glances at the arena still sees it.
  - **Network** (the button or the Network stage) opens NetworkFocus;
    Android Back closes it before leaving the scene. It shows the direct
    network: inputs and outputs named by port ("Accel: along"), the
    enabled connections and live activations, refreshed about 30 times
    a second, often enough that a Stepper's tick still shows its rise and
    drop. Tapping an output names the two inputs that drive it most,
    tapping an input names the outputs it drives most, and the other
    connections fade; tapping empty space clears the selection.
  - **Stats** shows a placeholder
    ([#198](https://github.com/MrLogic85/Node-Runner/issues/198)).
  - There is no Reset: training is reset from Build.
