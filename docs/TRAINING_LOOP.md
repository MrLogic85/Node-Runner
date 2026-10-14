# Training loop

How training behaves: when a Creation locks, how a trial is measured, how
generations evolve and what the Training scene does. How it looks is in
`docs/UI_DIRECTION.md` and `docs/WORLD_VISUALS.md`.

## Product lifecycle boundary

The training engine does not decide whether a Creation is editable; the App
layer owns that. "Locked" means "has trained at least one generation", not
the reference's "a training session has finished" (#369).

1. Build's play button autosaves and opens Train setup, unlocked or locked.
   Saving never needs a finished creature; only training does
   (`CreatureReadiness`, #515). Saving an edit keeps any training, even a
   generation that finished while Build was open, and refits its brain to
   the edited anatomy (`docs/CREATURE_MODEL.md` → "A rebuild keeps the
   brain"); Training then resumes from the refitted brain.
2. Train setup (`TrainSetupRoute`, #194) sets Shadows and Run length, filled
   from the Creation's saved values or the default (#617). Start saves them
   and opens Training in Train setup's place, so Back from Training returns
   to Build; Back from Train setup discards the changes. Simulate (#702)
   plays the saved brain with one shadow until the player leaves and saves
   nothing, not even the settings. It needs a trained Creation, so an
   untrained one has no Train or Simulate switch: Start trains (#843). A
   creature with no powered part may train too (#845): its brain has no
   outputs, so every shadow stands still and nothing is learned, and Train
   setup warns "Warning, no powered parts added! There is nothing to
   train". Flat ground is the only map (#540); its card takes its name from
   `Maps.Default` (#444).
3. Each finished generation is saved. Once the Creation has trained at
   least one generation it is locked (`CreationLock.IsLocked`), so the
   trained model cannot be lost by accident. Only what changes the model is
   locked (#638); `docs/BUILD_MODE.md` → Locked Build says what Build still
   allows. The lock follows from the saved training alone and is not
   stored, so `Evolver` knows nothing of it.
4. Leaving Training before the first generation finishes leaves the
   Creation unlocked.
5. Later Train or Simulate sessions start from the locked Build state.
6. Unlocking keeps the training (#371). The padlock in a locked Build asks
   once (a plain confirm) and opens the body for this Build visit only
   (`BuildViewModel.Unlock`). Nothing about it is saved: edits keep the
   training through step 1, and Build opens the Creation locked again next
   time. Reset training, in the overflow menu, is the only way to start
   over; a dialog confirmed with a tap asks first (#687, #866).

## Trial

- `TrialMeasurement` records every tick of a trial; its result is a
  `TrialResult` (#420):
  - `Distance`: the **running maximum** forward distance of the centre from
    its start, not the final or cumulative distance. This is the fitness:
    it rewards peak progress without penalising a creature that surges
    forward and then settles back a little.
  - `TopSpeed`: the highest forward speed of the centre, over a sliding
    half-second window so one-tick physics jolts do not dominate.
  - `Elevation`: the largest gap between the creature's lowest point and
    the ground, counted once it has landed, so the starting drop does not
    count; a crawler scores 0.
  - `FrontDistance`: how far ahead of its start the creature's front-most
    point is at the latest tick, never below 0 (#725). It is the **shown
    distance**: the distance scale, the shadow strip, SignalFlow's Distance stage,
    the best marker and the Creations card read it, so the number on screen
    matches where the creature's nose stands on the scale. It is not the
    score; the GA ranks by `Distance`.
  - `IsValid`: false when physics blew up (#650): a sample was NaN or
    infinite, or a tracked point moved further in one tick than
    `TrialMeasurement.MaxPlausibleSpeed` allows, far above anything a
    creature reaches. An invalid trial stops measuring.
  - **Tears (#962, #1256):** the first tear (`docs/CREATURE_MODEL.md` →
    Beam → Tearing) freezes `Distance`, `TopSpeed`, `Elevation` and
    `FrontDistance` at their values before it (`TrialMeasurement.Freeze`).
    The creature runs on to the end of the trial, and its samples are still
    checked, now on the main piece (`Rebase`): a later blow-up still makes
    the trial invalid, but a tear alone does not. A creature that breaks
    keeps the progress it made whole, so a tear at or after its peak costs
    nothing.
  - `Fitness`: `Distance`, or negative infinity for an invalid trial so it
    ranks below every valid one. `Evolver` logs each invalid trial. Invalid
    results never become the latest or the best, never count toward
    `MeanFitness` and are never shown as real results.
  - All are in world units; text the player reads shows metres
    (`docs/GLOSSARY.md` → Metre).
- **Latest and best ever (#479).** Training is noisy, so a later generation
  can do worse than an earlier one; that is not a bug. The saved training
  keeps two records for the map it trained on:
  - **Latest:** the best trial of the most recently finished generation
    (`Evolver.LatestGenome`/`LatestRun`, saved as `TrainingStateDef.Brain`
    and `Latest`). It can go down. The Creations card shows it, and
    Simulate and the warm start use its brain, because that is what the
    creature can do now.
  - **Best ever:** the highest score any generation got, and which
    generation that was (`Evolver.BestFitness`/`BestGeneration`, saved as
    `TrainingStateDef.Best`). It never goes down. The best marker shows it.
  - `TrainingStateDef.Record` is the rule: every finished generation
    replaces latest, and replaces the best only when it scores higher. A
    generation without a valid trial has no latest, so it is not saved.
  - Both records keep the score and the shown distance (#725). The score
    alone picks the best; the best's shown distance is the furthest any
    latest run's front has ended on that map (`Evolver.BestShownDistance`),
    so the best marker never moves back and never reads below Latest. A
    save from before #725 has no front distance: Latest shows its score,
    and the best marker stays hidden until the next generation is saved.
- **Trial timing.** `TrialController` times a fixed-length trial
  (`TrialDurationTicks`, 600 ≈ 10 s) for one creature; callers own the
  creature and its brain. Every trial, in every slot, starts from rest in
  the built shape with its lowest point `TrialController.StartClearance`
  above the ground (#649). The fall counts as trial time, but distance is
  measured from the start, so it does not change fitness.
- **The same brain runs the same trial (#798).** The followed shadow
  replays the previous best, so it must end where that run ended: a reset
  leaves no physics state behind, and a trial begins before any creature
  drives, however it is started (`project/src/sim/AGENTS.md`).

## Generations

- `GeneticAlgorithm` keeps the fittest `elitismCount` genomes unchanged
  (only a finite fitness can be an elite) and fills the rest by tournament
  selection, crossover and Gaussian mutation (`docs/ML_CONCEPTS.md`).
- `Evolver` runs one generation in fixed slots: slot 0 is the scene's
  creature, each other slot a clone, and every slot owns one
  `TrialController`. A finished slot takes the next pending genome in index
  order until the generation is complete; then the next generation starts.
  `Evolver`'s slots are the only population: shadows, their visuals and
  anything else that runs a generation use them, never a second set of
  creatures.
  - Every shadow runs at once, one slot per candidate, up to
    `Creature.MaximumShadows` (100, #787). Drawing limits the frame rate,
    not physics (#787). `ShadowsBudget` warns by shadows × parts from a
    30-part reference (smooth up to 32 shadows, a caution up to 64, then
    "too many"), under Train setup's Shadows slider (#318).
  - **Collision layers:** layer 1 is the ground; every creature body uses
    layer 2 with mask 1, so a body hits the ground and nothing else, neither
    its own parts nor another shadow. Camera rays see the ground only.
  - **Followed shadow (#385):** one shadow is drawn in full, above the
    others, and feeds SignalFlow, the brain view and part selection; every
    other shadow is drawn simplified (`docs/WORLD_VISUALS.md` → "Drawing as
    a shadow"). Shadow `i` is slot `i`, which runs candidate `i`. The
    default is shadow 1: the resumed genome or the elite, i.e. the previous
    best, which replays its run exactly; in a fresh generation 0 it is the
    first perturbed shadow. The player picks another with `Evolver.Follow`;
    a new leader never takes it, and every new generation opens on shadow 1
    again (#894). The camera frames the followed shadow.
  - **Shadow strip (#387):** cells ordered worst on the left to best on the
    right. A bar is the shown distance so far against this generation's
    leader, whose bar is full; not against the best ever, which may come
    from another run length. Only the followed cell is marked, since the
    lead changes too often. The strip has as many places as fit its width,
    at least 3 (#791). Up to that many shadows each get a cell, in shadow
    order with shadow 1 on the right; past that it pages, with a "worse"
    button and a last button that sorts on the first page and pages back
    up on later ones. A sort ranks by distance at that moment and holds
    until the next, so cells never jump while the player watches; the
    followed shadow can sort off the page. A new generation starts over in
    shadow order on the first page. The caption reads "Generation N",
    counted from 1, with a bar that fills as the followed run goes by
    (#715).
  - **Drawn shadows (#284):** Training draws only the shadows on the
    strip's current page, plus the followed one. The rest keep racing and
    counting, undrawn (`Evolver.DrawOnly`). A wider screen therefore draws
    more shadows. There is no setting to hide them: Simulate shows the
    creature alone.
  - Candidate assignment is deterministic for the same seed, slot count,
    build and platform.
- `TrainingHost` creates one `Evolver` from the Creation's Train setup
  values (`TrainSettingsDef` via `EvolutionSetup`, #617): Shadows is the
  population and Run length the trial duration. A Creation without Train
  setup values uses `TrainSettingsDef.Default` (8 shadows, 10 s; #379 makes
  it a setting). Tournament size (3, or the Shadows count if smaller),
  mutation rate and strength are fixed, and Training uses Uniform
  crossover, which keeps parent genes (#545 compares Blend). There is no
  generation budget. Best is plain distance, so a longer Run length
  reaches further (#703). A creature with nothing to drive still starts:
  its shadows stand still (#845).
- **Generation 0 (#537, #810).** A new Creation has no trained brain, so
  its base brain holds the built pose at full strength: all weights 0,
  positions at the built pose, strength at bias 3 (about 95%). A Servo's
  angle output holds its built pose at 0, and a Motor's speed
  output at 0 means standing still, so it holds its joint; a Stepper's
  frequency output starts at bias 0, half its Max frequency, and its power
  at 0, so it pushes neither way (#1036); a Piston's position output gets
  the bias that asks for its drawn length, clamped to ±6, about 0.25%
  short of either end (`Piston.DrawnPositions`, #870, #1129). A brain kept after its Start position
  changes keeps its old bias. This is how robots start in robotics ML:
  stiff in a default pose, the network learning offsets from it.
  `GenerationZero` builds the first population from it whenever there is
  no saved brain to resume:
  - The last shadow runs the base brain unchanged, as a reference that
    stays near 0 m.
  - Every other shadow perturbs it: Gaussian noise on every weight (σ 1.5)
    and bias (σ 0.5), so every Piston and joint motor pushes from the start, and
    a Motor turns at the speed its noise picks. A
    Piston's length output, a sigmoid, gets twice that noise and bias limit:
    sigmoid(2x) = (tanh(x) + 1) / 2, so it spreads its lengths as widely as
    a tanh output would (#1129).
  - A full-strength base learned far faster than a near-passive one, and
    σ 1.5 saturates the outputs into visible, decisive motion (#785).
    Backprop needs a different start (#955).
- **New ports are explored (#1180).** A part or sensor added to a trained
  brain starts almost passive (`docs/CREATURE_MODEL.md` → "New ports start
  almost passive"), so mutation alone would take hundreds of generations to
  find a use for it. Instead, in every generation that opens with an elite,
  `NewPortExploration` redraws the new ports' genes in every child as
  generation 0 draws them: the base brain's values plus its noise. The
  elite, shadow 1, stays unchanged and keeps the trained gait. A
  generation with no elite (every trial invalid) explores nothing.
  - Which ports are new is read from the best elite's values, so nothing is
    saved and exploration survives closing the app. A powered part is new
    while its strength bias, or a Stepper's frequency bias (#1036), is
    exactly −4; all its outputs' weights and
    biases are redrawn. An input is new while every weight out of it is
    exactly 0; its weights into every output are redrawn. A disabled
    connection is never redrawn.
  - Once an explored child becomes the best, it is the elite and its ports
    no longer read as new, so they mutate as usual. When generation 0's
    unchanged base wins, every weight is 0, so every input reads as new
    until something beats it.
  - Accepted cost: while no explored child beats the elite, every child's
    new-port genes are noise. For a new sensor that noise reaches the
    trained outputs too, so the gait may stall until a draw wins.

## The Training scene

- Training is its own routed scene, `TrainingRoute(creationId, mode)`, with
  `TrainingHost` as its root. `TrainingHost.tscn` authors the world inside
  the Training screen's arena viewport (background, ground, ruler, best
  marker, start sign, spawn marker, shadows' parent and camera); the host
  adds the creature and the `Evolver`, so leaving the scene frees them all.
  - **Simulate (#702).** No `Evolver`: the creature plays the saved brain
    in one trial that never ends, so there is no generation, shadow strip,
    save or count. The header reads "Simulating" with no generation
    caption, the Distance card reads the run's front distance, and the best
    marker stays at the saved best on this map.
  - **Camera (#668, #675, #1169, #1190).** `ArenaCamera` frames the followed
    shadow through `ArenaFraming`, from its centre of mass and its box. It moves on physics ticks, as the creatures do, so on a
    120 Hz screen the world never judders against it (#909). The other
    shadows may leave the view.
    - *Centre (#1169):* it follows the shadow's centre of mass on both
      axes, through one `ArenaFollow` per axis, eased in two stages so a
      gait's wobble never shakes the view and a switch to another shadow
      glides in. Each follower's aim leads by the eased speed, so its
      eased point keeps up with a moving shadow instead of trailing it.
      At rest the centre of mass shows in the middle of the view; *Ground*,
      *Lead* and *Safe zone* below move it from there.
    - *Ground:* the ground line never rises above 80% down the view,
      unless the callout shelf lowers that limit. A shadow on the ground
      therefore sits low, with the ground at 80%, and a hopping gait does
      not bob the view; the camera follows a shadow up only once
      centring it would put the ground below that line, or the safe zone
      holds its top.
    - *Zoom* fits the shadow's box inside side margins, standing on the
      ground below a top margin with headroom, and centred in the air
      clear of the top margin above and below. Speed on either axis
      widens it by one second of travel. Zoom 1, the closest, shows one
      world unit per view pixel. There is no farthest zoom for play
      (#884); only a physics blow-up is stopped, at a view 500 m wide.
      It widens quickly and narrows only after the shadow has needed less
      room for 1.5 s, then slowly, so a stretching gait does not make it
      pump.
    - *Lead (#1190):* the room speed adds is shared, two thirds in front
      and a third behind, so a fast shadow keeps room behind it to show
      where it came from. The view slides ahead by a sixth of the room it
      shows beyond the rest view, up to a sixth of a second of travel: at
      a steady speed the centre of mass sits half the rest view plus a
      third of the added room from the back edge, tending to a third of
      the view. Leading into a fall stops at the ground limit.
    - *Safe zone (#1190):* the shadow's box keeps 10% of the view clear
      to the sides and the top, as platformer cameras keep a player clear
      of the screen's edges. The ground limit holds the bottom. Where the
      box reaches nearer the edge than that at rest, its margin there is
      half the room it has at rest, so the zone never moves a shadow at
      rest and always fits. It moves the view as little as keeps the box
      inside, and only when the follow alone would not: a sudden start or
      launch, when the eased speed lags. A gait stretching the box has
      room, so the view follows the centre of mass and does not shake. A
      box too wide for both sides' margins, before the zoom has widened
      for it, is centred. After a switch to another shadow the zone holds
      only once the glide has brought its box inside, so the switch never
      jumps. The ground limit and callout shelf come first: the zone's top
      is measured in the whole view, so it never pushes a shadow that the
      shelf holds at its 25% floor.
    - *Callout shelf (#1137):* while a part callout shows, the camera
      keeps a shelf at the top as tall as the callout, its leader and
      `S1` margins (`docs/WORLD_VISUALS.md` → Part callout). It lowers
      the ground first, to at most 95% down but never into the distance
      scale's band along the bottom (#1170), and zooms out only for what
      is still missing; a shelf that already fits moves nothing. Zooming
      out for the shelf never shrinks the shadow below 25% of the view's
      height; past that the callout covers its top. The shelf opens as
      quickly as the zoom widens. When the selection clears, the callout
      goes at once but the shelf is held 1.5 s, then given back as slowly
      as the zoom narrows, so tapping from part to part does not pump the
      view.
    - It cuts back to the start, zoom and height included, when the
      followed shadow starts a new trial, since a new trial is a new scene.
      It also cuts when the arena changes size, the shelf included, and
      holds still while training is paused, except that the callout
      shelf still opens and closes.
  - **Ground.** Flat ground (`Maps.Flat`, "Flat ground") is the only map
    (#540), and its ground is endless. A resumed best ever counts only on
    the map it was reached on.
  - **Scales (#1170).** Two scales on the view's edges, so distance and
    height read wherever the camera looks (`docs/WORLD_VISUALS.md` →
    Training arena). Distance is counted from where the followed
    creature's front starts each trial (0 m, #725), negative behind it.
    Height is counted from the ground's top (0 m), with nothing below the
    ground; it shows true heights even when the ground is out of view.
    Both share one label step: labels come every 1, 2, 5, 10 … m so they
    never overlap, the ticks thinning with them; a closer label step comes
    back only with 20% room to spare, so a zoom resting at the switch does
    not flicker (`DistanceRuler`, #884).
  - **Best marker (#388).** Marks the best ever on this map at its shown
    distance, with a flag reading "Best 4.2 m". It is behind every
    creature, keeps its screen size, is hidden until its distance is known
    and jumps when a generation's front passes it. Off screen it shows
    nothing. It fades while a part's name shows, since the name may cover
    it.
  - **Start sign (#848).** A "Start" signpost at 0 m pointing the way to
    go, in Training and Simulate alike, so a creature that walks backwards
    (and so shows 0 m) is seen to have gone the wrong way. It is behind
    every creature, fades with the best marker and zooms with the creatures
    (#882).
  - **World view.** The world renders in its own viewport (`UiWorldView`),
    so UI layout and scale never touch physics distances or gravity. A tap
    on the arena selects a part of the followed shadow; a part takes a tap
    within 16 px of it on screen at least, so it stays easy to hit zoomed
    out; off every part, a Wheel's disc stands for its joint, as in Build
    (`docs/BUILD_MODE.md` → Interactions, #1143); a tap on a joint with parts on it selects them all
    (`docs/BUILD_MODE.md` → "Parts stand in for their joint", #1066). The
    selected parts' Build names show in one callout docked at the arena
    top (#388, #1137), with a live bar for each of their brain ports
    (#1064, `docs/WORLD_VISUALS.md` → Part callout). Each
    BrainFocus refresh samples the followed brain once into one
    `BrainPortValues` keyed by `BrainPort`; BrainFocus and the callout
    both read it, so they always agree. Each port's range and short label
    are in `docs/CREATURE_MODEL.md` → "Sensor–model contract".
  - **Resume (warm start, #538).** Opening Training continues from the
    saved `TrainingStateDef`: the generation count goes on, and the saved
    brain (the latest generation's best) is the elite: it runs unchanged as
    shadow 1 and its mutated children fill the other shadows
    (`GeneticAlgorithm.FromElites`), with any new ports explored (#1180,
    "New ports are explored" above). A disabled connection stays at 0
    through mutation and crossover. The best ever starts from the saved
    best, so a worse generation never lowers it.
  - **Save.** Each finished generation is saved on the thread pool, so the
    file round trip never stalls physics, as one atomic file write.
    Leaving mid-generation drops only the generation in progress. The save
    is guarded by the creation's training epoch, so one still in flight
    when the training is reset is dropped. Saves for one creation land in
    order, and reading a creation (`ICreationUpdateCoordinator.Get`) waits
    for them, so Build opened right after Training never shows a stale
    lock (#370).
- The top bar shows the creation's name, the status ("Training · Flat
  ground") and Brain and Stats buttons; unlock progress is not shown (#488).
  Beside the arena a side panel titled "Status" shows the Inputs → Brain →
  Outputs → Distance stages (`SignalFlowPresentationViewModel`, #813);
  Outputs counts the parts the brain drives, Servos, Motors and Pistons
  alike ("2 parts"). The stages count readings and parts (#196). Collapsing the panel widens the arena, and the camera refits.
  Under the arena are Pause and the generation caption.
  - **Pause** freezes the trial mid-run and resumes exactly where it left
    off; the screen keeps responding. Each visit starts unpaused.
  - **No speed-up** (#787). Physics always steps 1/60 s at real time.
    Godot's `Engine.TimeScale` stretches each step instead of running more
    of them, which changed fitness with speed and made stiff Springs blow
    up. Training goes faster by racing more shadows per generation.
  - **Slow-motion warning** (#318). When a frame needs more than Godot's 8
    physics steps, physics falls behind real time. `SlowMotionWatch`
    compares physics ticks with real time in 0.5 s windows (a single hitch
    counts at most 0.25 s). After 3 s below 0.9× the arena shows a Warning
    chip, "Too many shadows!", not a notification. It stays 60 s after the
    last slow stretch, so a player who only glances at the arena still sees
    it. Pause restarts the measuring and leaves the chip as it is.
  - **Brain** (the button or the Brain stage) opens BrainFocus; Android
    Back closes it before leaving the scene. It shows the direct brain
    (#536): inputs named by port ("Accel: along"), outputs named by joint,
    the enabled connections and live activations, refreshed every ~0.15 s
    rather than every frame (#42). Nothing is selected at first; tapping an
    output names the two inputs that drive it most, tapping an input names
    the outputs it drives most, and the other connections fade; tapping
    empty space clears the selection.
  - **Stats** shows a placeholder (#198).
  - There is no Reset: training is reset from Build.
