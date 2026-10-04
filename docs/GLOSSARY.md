# Glossary

Domain vocabulary used across code, docs and issues. If a term appears in the
codebase, it should appear here. When you introduce a new term, add it.

Ordered alphabetically within sections.

## Product words

The player's words. They started from `reference design/README.md` → "The
words" (#202); this list now owns them. Code, docs and copy use them; the ML
layer keeps its textbook terms.

- **Beams tool** — The Build rail tool that draws a Beam by default, or a
  link such as a Piston when that row is picked. Its side-panel list is
  headed "Links".
- **Build** — The editor where a Creation's body is drawn and changed. Code
  says Build (`BuildViewModel`, `BuildTool`, `BuildCanvas`), never
  Construction. See: `docs/BUILD_MODE.md`.
- **Creation** — The thing the player saves, names, lists and copies
  (`CreationDef`): a creature body plus its brain and training.
- **Creature** — The body inside a Creation; see "Creature anatomy". Copy
  never calls the saved item a creature.
- **Parts tool** — The first Build rail tool. It opens the Parts tray when
  nothing is selected and otherwise keeps the basic select, drag-to-move and
  pan behaviour.
- **Shadow** — One of the ghost copies that race at once during training.
  The App maps the GA's candidates and population to shadows at its
  boundary (`ITrainingProgressSource.ShadowCount`).

Kept on purpose:

- `Node` / `NodeDef` — the reference uses both Node and Joint; a joint is a
  node where beams meet.
- GA terms (population, candidate, genome, generation, fitness) in
  `NodeRunner.ML` and the sim's `Evolver` — `docs/ML_CONCEPTS.md` teaches them.
- `CreatureElementSelection` / `CreatureElementKind` — replaced by #220.

## Creature anatomy

Long-form descriptions and the sensor/model contract live in
`docs/CREATURE_MODEL.md`. The entries below are quick references.

- **Accelerometer** — A sensor on a beam: a proof mass on a damped spring
  that feels its beam speed up, slow down and tilt (gravity reads as 1 g
  "up"). Gives two model inputs, along and across the beam. See:
  `docs/CREATURE_MODEL.md`.
- **Beam** — A rigid, fixed-length connection between two nodes. Never
  stretches or compresses. Its own `RigidBody2D` at runtime. See:
  `docs/CREATURE_MODEL.md`.
- **Camera** — A sensor on a beam: three rays (left 1, centre, right 1)
  fanned around its aim, which turns in Build (#594; a new one looks
  forward-up, forward and forward-down) and turns with the beam, that read how near
  the ground is, 0 with nothing in range and 1 at contact. Gives three model
  inputs. Formerly the "LOS sensor". Not Godot's `Camera2D`. See:
  `docs/CREATURE_MODEL.md`.
- **Creature** — A single agent's body: nodes + beams (+ optional sensors) +
  Pistons, driven by a brain. See:
  `docs/CREATURE_MODEL.md`.
- **Creature element selection** — A selected joint, beam, sensor or Piston,
  represented as a `CreatureElementKind` plus the part's stable id (#220).
  Select holds any mix of them as a `PartSet` (#704).
- **CreatureDef** — Pure-data description of a creature; the "genome" of the
  body, distinct from the brain's genome. See: `docs/CREATURE_MODEL.md`.
- **Joint** — The player-facing name for a node in Build (the reference
  design's Joint tool adds one). Joints are passive (#450): beams turn
  freely there, with no settings, limits or brain ports, unless a closed
  triangle locks them. Not the retired 0.1.0 Joint/Bone/Muscle prototype
  part, and not a Godot physics joint. See:
  `docs/BUILD_MODE.md`.
- **Link** — A non-beam part drawn joint-to-joint by the Beams tool. Today
  that means Piston; Spring and Wing are listed for later. The UI list is
  headed "Links" and also includes Beam, but a Beam remains the structural
  part above.
- **Model input** — One slot in the neural network's input vector, populated
  one-to-one from an input port. See: `docs/CREATURE_MODEL.md`.
- **Model output** — One slot in the neural network's output vector,
  consumed one-to-one by an output port. See: `docs/CREATURE_MODEL.md`.
- **Node** — A physical attachment point where beams meet and can rotate
  relative to each other. Has a position and a small radius. Rendered as a
  ring with a fine inner ring. See: `docs/CREATURE_MODEL.md`.
- **Piston** — A powered link between two nodes (#451) that pushes them
  apart or pulls them together. Not a beam: inside its stroke it adds no
  rigidity; it weighs one and a half beams, and its end stops are hard. Gives the brain its length and speed, and takes a position and a
  strength output. See: `docs/CREATURE_MODEL.md`.
- **Port** — One brain channel a part declares (`BrainPort`): the part's id,
  a channel key that never changes, whether it is an input or an output, and
  the signal it carries (reading, velocity, position or strength).
  The brain's input and output order comes from the ports (`BrainPorts`).
  See: `docs/CREATURE_MODEL.md`.
- **Proof mass** — The accelerometer's inner weight; its displacement is the
  reading. See: `docs/CREATURE_MODEL.md`.
- **Sensor (part)** — A part that sits on a beam and feels that beam
  (`SensorDef`, `SensorKind`); one sensor per beam, at its midpoint.
  Not the brain. See: `docs/CREATURE_MODEL.md`.
- **Stroke** — How far a Piston moves each way from its built length, as a
  share of that length: ±30% means it reaches 70%…130%. Its end stops hold
  it there, whatever the load. See: `docs/CREATURE_MODEL.md`.
- **Strength output / Strength setting** — A powered part's Strength
  setting, chosen in Build, is its maximum force. Its strength output is the
  brain's sigmoid choice, 0…1, of how much of that maximum to use this tick.
  See: `docs/CREATURE_MODEL.md`.

## Build canvas

- **Build area** — The fixed rectangle joints must stay inside
  (`BuildViewModel.BuildArea`), drawn as a faint grid with corner
  marks. See: `docs/BUILD_MODE.md`.
- **Canvas unit** — A distance in creature coordinates, the same as
  `NodeDef.Position` and node radii. Zoom and pan never change it. In
  Training it is also the world (physics) unit.
- **Metre** — 100 world units (`Metres.WorldUnitsPerMetre`, #670). Godot
  defines no 2D metre; its default 2D gravity, 980 units/s², is Earth's
  9.8 m/s² at this scale. The sim, fitness and saves stay in world units;
  text the player reads converts to metres.
- **Move handle** — The Select frame's centre handle. It moves a selected
  group; it is not a rail tool.
- **View unit** — A distance in the Build canvas widget's own space before
  zoom and pan; touch positions, tap slop and most hit sizes use it, so they
  stay finger-sized at any zoom (see `BUILD_MODE.md` for the exceptions). `view = canvas × Zoom + Offset` (`CanvasView`).

## ML

- **Activation** — The nonlinear function applied element-wise after each
  linear layer. Options: tanh, ReLU, sigmoid. Each brain output uses the one
  its signal needs: tanh for velocity and position, sigmoid for strength.
- **Backpropagation (backprop)** — Algorithm that computes gradients of a loss
  with respect to network weights by applying the chain rule from output back
  to input.
- **Brain** — The neural network attached to a creature. A pure function
  `inputs → output targets`. In 0.13 it is direct: every input port
  connects straight to every output port, with no hidden layer (#536).
- **Brain graph** — How a brain is saved (`BrainDef`): neurons keyed by port
  and the connection genes between them, so it never depends on list order
  and later hidden neurons fit without a new format.
- **Connection gene** — One saved connection (`ConnectionGeneDef`): from and
  to neuron ids, a weight, and whether it is enabled. A disabled gene keeps
  its weight but carries no signal.
- **Crossover** — GA operator that combines two parent genomes into a child.
  We use configurable uniform or blend crossover on the flat weight vector.
- **Epoch** (supervised) — One pass through the entire training dataset.
- **Fitness** — Scalar score for a creature after one evaluation run. Higher is
  better. Definition is per-experiment (usually distance travelled).
- **Genome** — Flat `double[]` of all weights + biases in a brain, in a fixed
  canonical order. This is what the GA mutates and recombines. The brain
  graph compiles to it by port (`DirectBrain`).
- **Generation** — One full cycle of GA: evaluate → select → recombine →
  mutate → replace.
- **Loss** — Scalar the supervised trainer minimizes. Lower is better.
- **Mutation** — GA operator that perturbs genome values with Gaussian noise at
  a per-weight probability.
- **NEAT** — *NeuroEvolution of Augmenting Topologies*. A GA variant that
  evolves network structure as well as weights. Reserved for a later roadmap
  milestone.
- **Neuroevolution** — Using evolutionary algorithms (GA) to train neural
  networks. First planned for 0.4.0.
- **Novelty search** — Alternative to fitness-based selection that rewards
  behavioral diversity. Reserved for a later roadmap milestone.
- **Optimizer** — In backprop, the rule for turning a gradient into a weight
  update. SGD, momentum, Adam.
- **Population** — The set of candidate genomes (brains) evaluated in one
  generation. `Evolver` evaluates every candidate at once (up to 100), one
  slot each; creature bodies collide only with the ground. The player sees
  the candidates as **shadows**: the followed one in full, the rest
  simplified and transparent.
- **Reinforcement Learning (RL)** — Training via reward signals from
  environment interaction. Not used in the early roadmap; considered for later.
- **Selection** — GA operator that picks parents for the next generation. We
  use tournament selection.
- **Supervised learning** — Learning from labeled (input, target) pairs via
  backprop. Planned later as an imitation mode after neuroevolution.
- **Tournament selection** — Pick *k* random individuals, keep the fittest.
  Simple, robust, tunable via *k*.

## Simulation

- **TrialMeasurement** — Measures one trial: distance (the fitness, from the
  centre), top speed, elevation and front distance.
- **Shown distance** — The distance the player reads: how far ahead of its
  start the creature's front-most point ended up (#725). Not the fitness,
  which follows the centre. Where it shows: `docs/TRAINING_LOOP.md` → Trial.
- **Evolver** — Component that orchestrates generations: calls the GA, resets
  the scene, assigns new brains.
- **Fixed timestep** — Physics/NN updates happen at a locked 60 Hz regardless
  of frame rate. Required for determinism.
- **Run** — One evaluation episode for a population. Its length is the
  Creation's Run length in Train setup, 10 seconds by default.
- **Seed** — Integer input to the RNG. Written to logs; shown in UI.
- **Tick** — One fixed-step update. Sensors → brain → Pistons → physics step
  → fitness accumulation.
