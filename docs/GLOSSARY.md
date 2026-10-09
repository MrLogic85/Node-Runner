# Glossary

Project words used across code, docs and the UI, one line each; the linked
document owns the detail. Add a term when you introduce one. Textbook ML
terms are taught in `docs/ML_CONCEPTS.md`. Entries are alphabetical within
each section.

## Product words

The player's words: code, docs and copy use them, and the ML layer keeps its
textbook terms (population, candidate, genome).

- **Best ever** — The highest score any generation reached on a map, and
  which generation it was; it never goes down. See:
  `docs/TRAINING_LOOP.md` → "Latest and best ever".
- **Build** — The editor where a Creation's body is drawn and changed. Code
  says Build (`BuildViewModel`, `BuildTool`, `BuildCanvas`), never
  Construction. As a noun, a creation's build is its body without its
  training, what Share build shares. See: `docs/BUILD_MODE.md`.
- **Creation** — The thing the player saves, names, lists and copies
  (`CreationDef`): a creature body plus its brain and training.
- **Creature** — The body inside a Creation (`CreatureDef`): joints, beams,
  sensors and links, driven by a brain. `CreatureDef` is the body's pure
  data, distinct from the brain's genome. Copy never calls the saved item a
  creature. See: `docs/CREATURE_MODEL.md`.
- **Followed shadow** — The one shadow drawn in full; the camera frames it
  and it feeds the brain view and signal flow. See:
  `docs/TRAINING_LOOP.md` → Generations.
- **Latest** — The best trial of the most recently finished generation; it
  can go down. See: `docs/TRAINING_LOOP.md` → "Latest and best ever".
- **Links tool** — The Build rail tool that draws the picked link, such as
  a Beam (picked when Build opens) or a Piston; with none picked it works
  like the other tools (#1057). Its rail label and side-panel title say
  "Links" (#913); in code it is `BuildTool.Beam`. See:
  `docs/BUILD_MODE.md` → Interactions.
- **Locked** — A Creation that has trained at least one generation: Build
  keeps what the trained model needs (`CreationLock.IsLocked`, #369). See:
  `docs/TRAINING_LOOP.md` → "Product lifecycle boundary".
- **Parts tool** — The first Build rail tool. It opens the Parts tray when
  nothing is selected. See: `docs/BUILD_MODE.md` → Interactions.
- **Run length** — How long one trial lasts, set in Train setup; 10 s by
  default.
- **Shadow** — One of the copies that race at once in a generation. The App
  maps the GA's candidates to shadows at its boundary
  (`ITrainingProgressSource.ShadowCount`). See: `docs/TRAINING_LOOP.md` →
  Generations.
- **Shadow strip** — Training's row of cells, one per shadow, that ranks
  them and picks the followed one (#387). See: `docs/TRAINING_LOOP.md` →
  Generations.
- **Share code** — One line of text holding a creation's build, which Share
  build copies and Import creation reads (#899, `CreationShareCode`). See:
  `docs/SAVE_FORMAT.md` → "Share code".
- **Shown distance** — The distance the player reads: how far ahead of its
  start the creature's front-most point ended up (#725). Not the fitness,
  which follows the centre. See: `docs/TRAINING_LOOP.md` → Trial.
- **Simulate** — Plays a Creation's saved brain with one shadow and saves
  nothing (#702). See: `docs/TRAINING_LOOP.md` → "Product lifecycle
  boundary".
- **Train setup** — The screen between Build and Training that sets Shadows
  and Run length (#194).

## Creature anatomy

`docs/CREATURE_MODEL.md` owns every part's behaviour and the sensor–model
contract; `docs/WORLD_VISUALS.md` owns how parts look.

- **Accelerometer** — A sensor: a proof mass on a damped spring that feels
  its beam speed up, slow down and tilt. Two inputs, along and across the
  beam.
- **Beam** — A rigid, fixed-length connection between two joints.
- **Camera** — A sensor: three rays around its aim that read how near the
  ground is, and whether any sees it. Four inputs. Not Godot's `Camera2D`.
- **Coil length** — Where a Spring's rest length sits relative to its end
  stops, 0…100%.
- **Creature element selection** — A selected joint, beam, sensor or link: a
  `CreatureElementKind` plus the part's stable id (#220). A selection holds
  any mix of them as a `PartSet` (#704), in every tool (#803).
- **Fixed / Target** — The two links a Servo uses: it holds Fixed and turns
  Target.
- **Joint** — A point where links meet and can turn; in code a node
  (`NodeDef`). A plain joint is passive (#450); a Servo can sit on it. Not a
  Godot physics joint. See: `docs/BUILD_MODE.md`.
- **Link** — A joint-to-joint connection a Servo may use as Fixed or Target:
  Beam, Piston or Spring. The Links tool lists them all, Beam included. Say
  Links for the group and Beam for a beam (#913).
- **Model input / Model output** — One slot of the brain's input or output
  vector, filled from or consumed by one port.
- **Piston** — A powered link that pushes its two joints apart or pulls them
  together (#451). Not a beam: inside its stroke it adds no rigidity.
- **Port** — One brain channel a part declares (`BrainPort`): part id,
  channel key, input or output, and the signal it carries. `BrainPorts`
  gives the brain's order.
- **Proof mass** — The accelerometer's inner weight; its displacement is the
  reading.
- **Range** — How far a Servo may turn, 20°–360°.
- **Rigid triangle** — Three beams closing a triangle, whose joints cannot
  turn; Build hatches it (`RigidTriangles`).
- **Sensor (part)** — A part on a beam that feels that beam (`SensorDef`,
  `SensorKind`); one per beam. Not the brain.
- **Servo** — A powered joint part (#452) that holds one link as Fixed and
  turns another as Target.
- **Spring** — A passive link (#453) that pulls back toward its rest length;
  no brain ports.
- **Start position** — Where a part's drawn pose sits in its travel or
  range, 0…100%.
- **Strength output / Strength setting** — The setting is a powered part's
  maximum force, chosen in Build; the output is the brain's 0…1 choice of
  how much of it to use this tick.
- **Stroke** — How far a Piston or Spring travels between its end stops.

## Build canvas

- **Build area** — The fixed rectangle joints must stay inside
  (`BuildViewModel.BuildArea`). See: `docs/BUILD_MODE.md`.
- **Canvas unit** — A distance in creature coordinates, the same as
  `NodeDef.Position` and node radii. Zoom and pan never change it. In
  Training it is also the world (physics) unit.
- **Metre** — 100 world units (`Metres.WorldUnitsPerMetre`, #670). Godot
  defines no 2D metre; its default 2D gravity, 980 units/s², is Earth's
  9.8 m/s² at this scale. The sim, fitness and saves stay in world units;
  text the player reads converts to metres.
- **Move handle** — The selection frame's centre handle. It moves a selected
  group; it is not a rail tool.
- **View unit** — A distance in the Build canvas widget's own space before
  zoom and pan; touch positions, tap slop and most hit sizes use it, so they
  stay finger-sized at any zoom (`docs/BUILD_MODE.md` lists the
  exceptions). `view = canvas × Zoom + Offset` (`CanvasView`).

## ML

- **Activation** — The nonlinearity after each layer. A brain output uses
  the one its signal needs (#535).
- **Brain** — The neural network attached to a creature: a pure function
  from inputs to output targets. It is direct: every input port connects
  straight to every output port (#536).
- **Brain graph** — How a brain is saved (`BrainDef`): neurons keyed by port
  and the connection genes between them. See: `docs/SAVE_FORMAT.md`.
- **Connection gene** — One saved connection (`ConnectionGeneDef`): from and
  to neuron ids, a weight, and whether it is enabled.
- **Crossover** — Combines two parent genomes into a child; training uses
  uniform crossover.
- **Fitness** — A trial's score; higher is better. It is the distance the
  creature's centre got.
- **Genome** — The flat `double[]` of a brain's weights and biases that the
  GA mutates and recombines; the brain graph compiles to it by port
  (`DirectBrain`).
- **Generation** — One GA cycle: evaluate, select, recombine, mutate.
- **Mutation** — Gaussian noise added to genome values at a per-weight
  probability.
- **Population** — The candidates of one generation, shown as shadows.
- **Selection** — Picks the parents of the next generation; we use
  tournament selection.
- **Warm start** — Training resumes by breeding the next generation from the
  saved brain (#538).

## Simulation

- **Evolver** — Runs generations (`project/src/sim/Evolver.cs`): one slot
  per candidate, all at once (up to 100); it calls the GA, resets each
  slot's trial and assigns new brains.
- **Fixed timestep** — Physics and brain updates run at a locked 60 Hz
  whatever the frame rate, for determinism.
- **Seed** — The integer the run's RNG starts from.
- **Tick** — One fixed step: sensors, brain, Servos and Pistons, then the
  physics step and the trial measurement. See: `docs/ARCHITECTURE.md` →
  "The tick".
- **Trial** — One shadow's run of Run length; `TrialMeasurement` records its
  distance (the fitness), front distance, top speed and elevation. See:
  `docs/TRAINING_LOOP.md` → Trial.
