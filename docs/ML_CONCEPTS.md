# ML Concepts

This file tracks which machine-learning ideas Node Runner teaches, in which
version they land, and — importantly — *how* we make each one visible.

For each concept:
- **What:** one-sentence definition
- **Where:** version + code location
- **How we show it:** the visual/interactive hook

---

## Feedforward neural network

- **What:** A function `f(x) = σ(W₃ σ(W₂ σ(W₁ x + b₁) + b₂) + b₃)`. Layers of
  linear maps with a nonlinearity in between.
- **Where:** 0.1.0 · `libs/NodeRunner.ML/NeuralNetwork.cs`
- **How we show it:** The creature moves at all. The 0.16.0 brain views draw
  the network on-screen with nodes and edges. From 0.13.0 until then the brain
  is direct: inputs connect straight to outputs with no hidden layer (#536).

## Activation functions

- **What:** The nonlinearity between layers. Without it, a deep network
  collapses to a single linear map.
- **Where:** 0.1.0 hidden-layer support in `libs/NodeRunner.ML/Activation.cs`
  (`Tanh`, `ReLU`, `Sigmoid`); 0.1.0 gameplay uses `Tanh`. From 0.13.0 each
  output has its own activation: tanh for position, sigmoid for strength
  (#535).
- **How we show it:** Later slider. Same trained brain, different activation —
  watch behavior change.

## Genetic algorithm (neuroevolution)

- **What:** Search weights by simulating a population, keeping the fit ones,
  recombining them, mutating a bit, repeating.
- **Where:** 0.4.0 · `libs/NodeRunner.ML/Ga/GeneticAlgorithm.cs`. 0.13.0
  resumes from the saved elites instead of restarting (#538); 0.14.0 saves the
  population size (Shadows) per Creation (#528); 0.16.0 puts the GA behind an
  ask/tell optimizer interface (#545).
- **How we show it:** Candidate genomes run concurrently in fixed,
  collision-isolated slots while the fitness chart climbs and mutations
  produce weird outliers. The first candidate is visible; showing the hidden
  parallel candidates as ghosts is tracked separately.

## Fitness function

- **What:** The scalar that says "this individual was this good". *Choice of
  fitness is the most important design decision in evolutionary ML.*
- **Where:** 0.4.0 · `project/src/sim/`
- **How we show it:** 0.4.0 displays the active fitness value. Fitness stays
  distance only until 0.16.0, which adds several fitness functions at once:
  the winner of each one parents the next generation (#317, #546).

## Mutation rate & exploration/exploitation

- **What:** How aggressively we perturb weights per generation. Too low → stuck
  in local optima. Too high → no learning.
- **Where:** 0.4.0 (fixed or simple setting) → later visualization/tuning UI
- **How we show it:** Later slider. Show side-by-side populations with different
  rates. One learns steadily, one thrashes.

## Selection pressure

- **What:** How much better fit individuals dominate the next generation.
  Tournament size is our lever.
- **Where:** 0.4.0 · `libs/NodeRunner.ML/`
- **How we show it:** Later slider. High pressure → fast convergence, low
  diversity. Low pressure → slow, exploratory.

## Feature engineering / observation design

- **What:** What the network gets to *see*. Bad inputs cap performance;
  redundant inputs waste capacity.
- **Where:** 0.1.0 started with `project/src/creature/Sensors.cs` reading a
  sin/cos oscillator clock, then joint angle and angular velocity in stable
  order. 0.2.0 replaced this with a node/beam model with a "core" sensor
  package (rays, pitch, elevation, speed) on a node, and each motor relation
  contributing a relative angle and angular velocity.
  0.3.0 expands this into topology-derived sensors for user-built creatures.
  0.13.0 lets each part declare its ports, with the convention "0 = as
  built" (#534). 0.12.0 replaces that package with sensor parts on beams
  (see `docs/CREATURE_MODEL.md`): an Accelerometer, whose proof mass on a
  damped spring gives two readings along and across its beam and filters
  spiky contacts (#127), and an LOS sensor (#575). There is no speed or
  elevation input: the brain must learn movement from acceleration, joint
  readings and its own outputs; 0.14.0 adds LOS settings
  (#578) and Pulse, a rhythm input (#527).
- **How we show it:** 0.1.0 proves observation → action by making the worm
  twitch. 0.2.0 lists what the network sees each tick. A later, uncommitted
  teaching mode may let the user toggle a sensor off, retrain from scratch, and
  see the impact.

## Network capacity (width/depth)

- **What:** Bigger networks can represent more, but need more data/generations
  to train, and can overfit.
- **Where:** 0.16.0 · hidden layers added on top of kept direct connections
  (#543)
- **How we show it:** Adding a hidden layer keeps the skill the direct brain
  already had. Too little capacity plateaus; huge nets learn slowly and
  behave erratically. Sweet spot is visible.

## Live activation visualization

- **What:** Which neuron fires at which moment, and how strongly.
- **Where:** 0.16.0 brain views (#196, #197, #548)
- **How we show it:** Nodes glow. Edges pulse. You literally see the thought
  behind each step.

## Generalisation across maps

- **What:** A brain that only ever sees one map can overfit to it. Training
  on several maps rewards behavior that works everywhere.
- **Where:** 0.15.0 · map checkboxes and the map loop (#540), stats per map
  (#541)
- **How we show it:** One generation runs on one map. Stats shows a learning
  curve per map, so a creature that is great on one map and useless on
  another is visible at a glance.

## Backpropagation

- **What:** Compute the gradient of loss w.r.t. every weight, via the chain
  rule, then step against the gradient. The workhorse of modern ML.
- **Where:** Later · `libs/NodeRunner.ML/`
- **How we show it:** "Imitation mode". User demonstrates the first X steps by
  moving nodes while beam lengths and constraints stay fixed. The network is
  trained to reproduce the target motion or derived motor-relation commands.
  Loss curve visualized. First the copy is bad, then it's good. #522 records
  an agreed shape ("learn from mother") for a later imitation mode.

## Loss functions

- **What:** The number backprop minimizes. MSE for regression, cross-entropy
  for classification.
- **Where:** Later backprop milestone
- **How we show it:** Loss line chart. Swap MSE ↔ Huber and watch the training
  curve change on the same data.

## Optimizers (SGD / momentum / Adam)

- **What:** Different rules for turning a gradient into a weight update.
- **Where:** Later backprop milestone · `libs/NodeRunner.ML/`
- **How we show it:** Toggle between them on the same problem. Loss curves
  overlaid.

## Overfitting

- **What:** Model memorizes training data, fails on unseen inputs.
- **Where:** Later classifier mini-mode
- **How we show it:** Training accuracy vs held-out accuracy plotted together.
  Diverging curves = overfitting.

## Supervised learning vs evolutionary learning

- **What:** Two paradigms for adjusting weights: gradients (needs
  differentiability & target labels) vs population search (needs only a
  fitness score).
- **Where:** Contrast made explicit after 0.4.0 neuroevolution and the later
  backprop milestone both exist.
- **How we show it:** Both work on similar-looking tasks. Timing, cost, and
  failure modes differ. A short in-app "compare" screen.

---

## Concepts we've reserved for later

Not built yet; parked so we don't forget:

- NEAT (topology evolution); the 0.13.0 brain format is ready for it (#522)
- Novelty search
- Memory cells as a brain part (#126); full recurrent networks later
- Reinforcement learning (DQN, policy gradients)
- Curriculum learning
- Adversarial co-evolution
- Attention / small transformer

Each will get its own section when it lands.
