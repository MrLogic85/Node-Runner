# Glossary

Domain vocabulary used across code, docs and issues. If a term appears in the
codebase, it should appear here. When you introduce a new term, add it.

Ordered alphabetically within sections.

## Creature anatomy

Long-form descriptions and the sensor/brain/muscle contract live in
`docs/CREATURE_MODEL.md`. The entries below are quick references.

- **Bone** — Rigid connection between two joints. Does not actuate. Renders as
  a line/rectangle. See: `docs/CREATURE_MODEL.md`.
- **Brain input** — One slot in the neural network's input vector, populated
  one-to-one from a sensor value. See: `docs/CREATURE_MODEL.md`.
- **Brain output** — One slot in the neural network's output vector, consumed
  one-to-one as a muscle target. See: `docs/CREATURE_MODEL.md`.
- **Creature** — A single agent: skeleton (joints + bones) + muscles + sensors
  + brain. See: `docs/CREATURE_MODEL.md`.
- **CreatureDef** — Pure-data description of a creature; the "genome" of the
  body, distinct from the brain's genome. See: `docs/CREATURE_MODEL.md`.
- **Joint** — A point on the creature where bones/muscles attach. Has a
  position and a small radius. Rendered as a circle. Corresponds to a
  `RigidBody2D` in Godot. See: `docs/CREATURE_MODEL.md`.
- **Muscle** — An actuator between two joints. Receives a target activation in
  `[-1, 1]` from the brain each tick and applies a corresponding force/torque.
  See: `docs/CREATURE_MODEL.md`.
- **Sensor** — A source of scalar input to the brain. Examples: joint angle,
  angular velocity, ground-contact boolean, raycast distance. See:
  `docs/CREATURE_MODEL.md`.
- **Skeleton** — The set of joints + bones. What the user starts drawing in
  0.3.0.

## ML

- **Activation** — The nonlinear function applied element-wise after each
  linear layer. Options: tanh, ReLU, sigmoid.
- **Backpropagation (backprop)** — Algorithm that computes gradients of a loss
  with respect to network weights by applying the chain rule from output back
  to input.
- **Brain** — The neural network attached to a creature. A pure function
  `sensors → muscle targets`.
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
- **Population** — The set of creatures alive in one generation (default 20).
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
- **Tick** — One fixed-step update. Sensors → brain → muscles → physics step
  → fitness accumulation.

## App / UX

- **Neon theme** — Current reference visual direction: dark arena, glowing
  nodes, bright signal paths, and readable console-like controls. See
  `docs/UI_DIRECTION.md`; implementation should remain themeable.
- **Focus** — Tapping a creature makes it the "focused" one; its brain is
  shown in the network visualizer.
- **Terrarium** — The user's future collection screen of saved creatures.
- **Time scale** — Simulation speed multiplier (1× / 5× / 20×).
