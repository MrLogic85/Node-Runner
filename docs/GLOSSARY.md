# Glossary

Domain vocabulary used across code, docs and issues. If a term appears in the
codebase, it should appear here. When you introduce a new term, add it.

Ordered alphabetically within sections.

## Creature anatomy

Long-form descriptions and the sensor/model contract live in
`docs/CREATURE_MODEL.md`. The entries below are quick references.

- **Beam** — A rigid, fixed-length connection between two nodes. Never
  stretches or compresses. Its own `RigidBody2D` at runtime. See:
  `docs/CREATURE_MODEL.md`.
- **Core** — A sensor package mounted on a node: rays, pitch, elevation,
  speed. **Not the neural model** — it only produces sensor readings. See:
  `docs/CREATURE_MODEL.md`.
- **Creature** — A single agent: nodes + beams (+ optional cores) + the
  sensors/motor relations they derive + a brain. See:
  `docs/CREATURE_MODEL.md`.
- **Creature element selection** — A selected node, beam, or core,
  represented as a `CreatureElementKind` plus its zero-based index in the
  corresponding `CreatureDef` list.
- **CreatureDef** — Pure-data description of a creature; the "genome" of the
  body, distinct from the brain's genome. See: `docs/CREATURE_MODEL.md`.
- **Model input** — One slot in the neural network's input vector, populated
  one-to-one from a sensor value (a core's or a motor relation's). See:
  `docs/CREATURE_MODEL.md`.
- **Model output** — One slot in the neural network's output vector,
  consumed one-to-one as a motor relation's target angular velocity. See:
  `docs/CREATURE_MODEL.md`.
- **Motor relation** — A controllable rotation between two beams sharing a
  node, derived from the creature's topology (not stored data). Exposes
  `relativeAngle`/`relativeAngularVelocity` sensors and accepts a
  `targetAngularVelocity` output, driven by torque capped at a static
  `MaxTorque`. See: `docs/CREATURE_MODEL.md`.
- **Node** — A physical attachment point where beams meet and can rotate
  relative to each other. Has a position and a small radius. Rendered as a
  circle. See: `docs/CREATURE_MODEL.md`.

## ML

- **Activation** — The nonlinear function applied element-wise after each
  linear layer. Options: tanh, ReLU, sigmoid.
- **Backpropagation (backprop)** — Algorithm that computes gradients of a loss
  with respect to network weights by applying the chain rule from output back
  to input.
- **Brain** — The neural network attached to a creature. A pure function
  `sensors → motor relation targets`.
- **Crossover** — GA operator that combines two parent genomes into a child.
  We use uniform crossover on the flat weight vector.
- **Epoch** (supervised) — One pass through the entire training dataset.
- **Fitness** — Scalar score for a creature after one evaluation run. Higher is
  better. Definition is per-experiment (usually distance travelled).
- **Genome** — Flat `double[]` of all weights + biases in a brain, in a fixed
  canonical order. This is what the GA mutates and recombines.
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
  generation. As of 0.4.0 these are evaluated sequentially via repeated
  trials of one creature (default size 8); running each as its own
  simultaneous creature is a possible later optimization.
- **Reinforcement Learning (RL)** — Training via reward signals from
  environment interaction. Not used in the early roadmap; considered for later.
- **Selection** — GA operator that picks parents for the next generation. We
  use tournament selection.
- **Supervised learning** — Learning from labeled (input, target) pairs via
  backprop. Planned later as an imitation mode after neuroevolution.
- **Tournament selection** — Pick *k* random individuals, keep the fittest.
  Simple, robust, tunable via *k*.

## Simulation

- **Evaluator** — Component that measures fitness for each creature during a
  run.
- **Evolver** — Component that orchestrates generations: calls the GA, resets
  the scene, assigns new brains.
- **Fixed timestep** — Physics/NN updates happen at a locked 60 Hz regardless
  of frame rate. Required for determinism.
- **Run** — One evaluation episode for a population, typically 10 seconds
  (600 ticks).
- **Seed** — Integer input to the RNG. Written to logs; shown in UI.
- **Tick** — One fixed-step update. Sensors → brain → motor relations → physics step
  → fitness accumulation.

## App / UX

- **Neon theme** — Current reference visual direction: dark arena, glowing
  nodes, bright signal paths, and readable console-like controls. See
  `docs/UI_DIRECTION.md`; implementation should remain themeable.
- **Focus** — Tapping a creature makes it the "focused" one; its brain is
  shown in the network visualizer.
- **Terrarium** — The user's future collection screen of saved creatures.
- **Time scale** — Simulation speed multiplier (1× / 5× / 20×).
