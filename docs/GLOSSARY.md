# Glossary

Project words used across code, docs and the UI, one line each; the linked
document owns the detail. Add a term when you introduce one. Textbook ML
terms are taught in `docs/ML_CONCEPTS.md`.

## Product words

The player's words: code, docs and copy use them, and the ML layer keeps its
textbook terms (population, candidate, genome).

- **Best ever** — The highest score any generation reached on a map; it
  never goes down. See: `docs/TRAINING_LOOP.md` → "Latest and best ever".
- **Build** — The editor where a Creation's body is drawn and changed. Code
  says Build (`BuildViewModel`, `BuildTool`, `BuildCanvas`), never
  Construction. As a noun, a creation's build is its body without its
  training, what Share build shares. See: `docs/BUILD_MODE.md`.
- **Creation** — The thing the player saves, names, lists and copies
  (`CreationDef`): a creature body plus its network and training.
- **Creature** — The body inside a Creation (`CreatureDef`): joints, links,
  sensors and joint parts, as pure data apart from the network. Copy never
  calls the saved item a creature. See: `docs/CREATURE_MODEL.md`.
- **Followed shadow** — The one shadow drawn in full; the camera frames it
  and it feeds the network view and the Status panel. See:
  `docs/TRAINING_LOOP.md` → Generations.
- **Latest** — The best trial of the most recently finished generation; it
  can go down. See: `docs/TRAINING_LOOP.md` → "Latest and best ever".
- **Links tool** — The Build rail tool that draws the picked link, such as
  a Beam or a Piston. The UI says "Links"; in code it is `BuildTool.Beam`.
  See: `docs/BUILD_MODE.md` → Interactions.
- **Locked** — A Creation that has trained at least one generation, so
  Build keeps what the trained network needs (`CreationLock.IsLocked`).
  See: `docs/TRAINING_LOOP.md` → "Product lifecycle boundary".
- **Parts tool** — The Build rail tool that opens the Parts tray when
  nothing is selected. See: `docs/BUILD_MODE.md` → Interactions.
- **Run length** — How long one trial lasts, set in Train setup.
- **Shadow** — One of the copies that race at once in a generation; the
  App's word for the GA's candidate. See: `docs/TRAINING_LOOP.md` →
  Generations.
- **Shadow strip** — Training's row of cells, one per shadow, that ranks
  them and picks the followed one. See: `docs/TRAINING_LOOP.md` →
  Generations.
- **Share code** — One line of text holding a creation's build, which Share
  build copies and Import creation reads (`CreationShareCode`). See:
  `docs/SAVE_FORMAT.md` → "Share code".
- **Shown distance** — The distance the player reads: how far ahead of its
  start the creature's front-most point is. Not the fitness, which follows
  the centre. See: `docs/TRAINING_LOOP.md` → Trial.
- **Simulate** — Plays a Creation's saved network with one shadow and saves
  nothing. See: `docs/TRAINING_LOOP.md` → "Product lifecycle boundary".
- **Train setup** — The screen between Build and Training that sets Shadows
  and Run length.

## Creature anatomy

`docs/CREATURE_MODEL.md` owns every part's behaviour and the sensor–network
contract; `docs/WORLD_VISUALS.md` owns how parts look.

- **Accelerometer** — A sensor on a beam that feels it speed up, slow down
  and tilt, along and across the beam.
- **Angle sensor** — A hinge part that reads the angle between its Fixed
  and Target sides and how fast it changes; it applies no force.
- **Beam** — A rigid, fixed-length link between two joints.
- **Brake** — A hinge part with no ports that resists its sides' turning
  with up to its Strength; a Locked Brake holds them rigid, which is not a
  Locked Creation.
- **Camera** — A sensor on a beam whose rays read how near the ground is
  within its range. Not Godot's `Camera2D`.
- **Coil length** — Where a Spring's rest length sits between its end
  stops, 0…100%.
- **Creature element selection** — A selected part: a
  `CreatureElementKind` plus the part's stable id. A selection holds any
  mix of them as a `PartSet`.
- **Cushion** — The band a Touch sensor feels in, outside its joint.
- **Fixed / Target** — The two sides a hinge part uses: Target turns
  against Fixed. Each is a link, or the Wheel on its joint. See:
  `docs/CREATURE_MODEL.md` → "Servo".
- **Hinge part** — A joint motor, Brake or Angle sensor: a part that works
  between a Fixed and a Target side, one per joint (`IHingePartDef`). See:
  `docs/CREATURE_MODEL.md` → Node → One part per slot.
- **Hub** — The circle at every joint's centre where links end and a
  Piston's or Spring's travel is measured from, whatever sits on the joint
  (`NodeDef.HubRadius`). See: `docs/CREATURE_MODEL.md` → Node.
- **Joint** — A point where links meet and can turn; in code a node
  (`NodeDef`). A plain joint is passive; a hinge part, a Wheel, a Rubber
  and a Touch sensor can each sit on it. Not a Godot physics joint. See:
  `docs/CREATURE_MODEL.md` → Node.
- **Joint motor** — A powered hinge part: a Servo, a Motor or a Stepper
  (`IJointMotorDef`, `JointMotor`).
- **Link** — A joint-to-joint part a hinge part may use as a side: Beam,
  Piston or Spring. Say Links for the group and Beam for a beam.
- **Motor** — A joint motor that turns its Target side at the speed the
  network picks, round and round if it likes. Code and saves call it the
  Velocity motor (`VelocityMotorDef`, `velocityMotors`).
- **Network input / Network output** — One slot of the network's input or
  output vector, filled from or consumed by one port.
- **Piston** — A powered link that pushes its two joints apart or pulls
  them together. Not a beam: inside its stroke it adds no rigidity.
- **Port** — One network channel a part declares (`NetworkPort`): part id,
  channel key, input or output, and its signal. `NetworkPorts` gives the
  network's order.
- **Proof mass** — The Accelerometer's inner weight; its displacement is
  the reading.
- **Range** — How far a Servo may turn, or how far a Camera's rays reach.
- **Rigid triangle** — Three beams closing a triangle, whose joints cannot
  turn; Build hatches it (`RigidTriangles`).
- **Sensor (part)** — A part that feels: on a beam, an Accelerometer or
  Camera (`SensorDef`); on a joint, a Touch sensor or an Angle sensor. Not
  the network.
- **Rubber** — A joint part with no ports that makes its joint, or its
  Wheel's tyre, grip the ground harder.
- **Servo** — A joint motor that turns its Target side to the angle the
  network picks.
- **Skin** — A Rubber's band outside its joint; only drawn.
- **Spring** — A link with no ports that pulls back toward its rest
  length.
- **Stepper** — A joint motor that holds its joint in notches and, at the
  frequency the network picks, steps to the next one.
- **Start position** — Where a part's drawn pose sits in its travel or
  range, 0…100%.
- **Strength output / Strength setting** — The setting is a powered part's
  maximum force, chosen in Build; the output is the network's 0…1 choice of
  how much of it to use this tick.
- **Stroke** — How far a Piston or Spring travels between its end stops.
- **Tear** — A beam end letting go of its joint in Simulate and Training
  after the joint has pulled too far from it; the creature goes on as its
  **main piece**. See: `docs/CREATURE_MODEL.md` → Beam → Tearing.
- **Tick input** — A Stepper's input that shows when it steps next. Not a
  **Tick**.
- **Touch sensor** — A sensor on a joint that reads how close the joint is
  to touching the ground.
- **Wheel** — A joint part with no ports that turns freely on its joint and
  rolls on the ground.

## Build canvas

- **Build area** — The fixed rectangle joints must stay inside
  (`BuildViewModel.BuildArea`). See: `docs/BUILD_MODE.md`.
- **Canvas unit** — A distance in creature coordinates, as in
  `NodeDef.Position`. Zoom and pan never change it. In Training it is also
  the world (physics) unit.
- **Metre** — 100 world units (`Metres.WorldUnitsPerMetre`). Godot defines
  no 2D metre; its default 2D gravity, 980 units/s², is Earth's 9.8 m/s² at
  this scale. The sim, fitness and saves stay in world units; text the
  player reads converts to metres.
- **Move handle** — The selection frame's centre handle that moves a
  selected group; not a rail tool.
- **View unit** — A distance in the Build canvas widget's own space before
  zoom and pan, so touch sizes stay finger-sized at any zoom.
  `view = canvas × Zoom + Offset` (`CanvasView`).

## ML

- **Activation** — The nonlinearity after each layer; a network output uses
  the one its signal needs.
- **Connection gene** — One saved connection (`ConnectionGeneDef`): from and
  to neuron ids, a weight, and whether it is enabled.
- **Crossover** — Combines two parent genomes into a child; training uses
  uniform crossover.
- **Fitness** — A trial's score; higher is better. It is how far the
  creature's centre got.
- **Genome** — The flat `double[]` of a network's weights and biases that
  the GA mutates and recombines (`DirectNetwork`).
- **Generation** — One GA cycle: evaluate, select, recombine, mutate.
- **Mutation** — Gaussian noise added to genome values at a per-weight
  probability.
- **Network graph** — How a network is saved (`NeuralNetworkDef`): neurons
  keyed by port and the connection genes between them. See:
  `docs/SAVE_FORMAT.md`.
- **Neural network** — The network attached to a creature: a pure function
  from inputs to output targets, direct from every input port to every
  output port. Say "network" for short; not "brain" or "model".
- **Population** — The candidates of one generation, shown as shadows.
- **Selection** — Picks the parents of the next generation; we use
  tournament selection.
- **Warm start** — Training resumes by breeding the next generation from
  the saved network.

## Simulation

- **Evolver** — Runs generations (`project/src/sim/Evolver.cs`): one slot
  per candidate, all at once.
- **Fixed timestep** — Physics and network updates run at a locked 60 Hz
  whatever the frame rate, for determinism.
- **Seed** — The integer the run's RNG starts from.
- **Tick** — One fixed step: the trial measurement, then sensors, network
  and powered parts, then the physics step. See: `docs/ARCHITECTURE.md` →
  "The tick".
- **Trial** — One shadow's run of Run length, measured by
  `TrialMeasurement`. See: `docs/TRAINING_LOOP.md` → Trial.
