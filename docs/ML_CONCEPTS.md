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
- **How we show it:** The creature moves at all. Later (0.5.0) the network is
  drawn on-screen with nodes and edges.

## Activation functions

- **What:** The nonlinearity between layers. Without it, a deep network
  collapses to a single linear map.
- **Where:** 0.1.0 hidden-layer support in `libs/NodeRunner.ML/Activation.cs`
  (`Tanh`, `ReLU`, `Sigmoid`); 0.1.0 gameplay uses `Tanh`, and a later
  visualization/tuning milestone exposes a UI toggle.
- **How we show it:** Later slider. Same trained brain, different activation —
  watch behavior change.

## Genetic algorithm (neuroevolution)

- **What:** Search weights by simulating a population, keeping the fit ones,
  recombining them, mutating a bit, repeating.
- **Where:** 0.4.0 · `libs/NodeRunner.ML/`
- **How we show it:** 20 creatures visibly running in parallel. Fitness chart
  climbs. Users can see mutations produce weird outliers.

## Fitness function

- **What:** The scalar that says "this individual was this good". *Choice of
  fitness is the most important design decision in evolutionary ML.*
- **Where:** 0.4.0 · `project/src/sim/`
- **How we show it:** 0.4.0 displays the active fitness value. Later, the user
  can toggle fitness definition ("distance" vs "distance − energy" vs "height
  reached") and see how it changes behavior.

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
- **Where:** 0.1.0 starts with `project/src/creature/Sensors.cs` reading a
  sin/cos oscillator clock, then joint angle and angular velocity in stable
  order; 0.2.0 explains the sensor list; 0.3.0 expands this into
  topology-derived sensors for user-built creatures.
- **How we show it:** 0.1.0 proves observation → action by making the worm
  twitch. 0.2.0 lists what the network sees each tick. A later, uncommitted
  teaching mode may let the user toggle a sensor off, retrain from scratch, and
  see the impact.

## Network capacity (width/depth)

- **What:** Bigger networks can represent more, but need more data/generations
  to train, and can overfit.
- **Where:** 0.5.0 or later sliders
- **How we show it:** Tiny nets can't even walk. Huge nets learn slowly and
  behave erratically. Sweet spot is visible.

## Live activation visualization

- **What:** Which neuron fires at which moment, and how strongly.
- **Where:** 0.5.0 · `project/src/ui/`
- **How we show it:** Nodes glow. Edges pulse. You literally see the thought
  behind each step.

## Backpropagation

- **What:** Compute the gradient of loss w.r.t. every weight, via the chain
  rule, then step against the gradient. The workhorse of modern ML.
- **Where:** Later · `libs/NodeRunner.ML/`
- **How we show it:** "Imitation mode". User demonstrates the first X steps by
  moving joints while bone lengths and constraints stay fixed. The network is
  trained to reproduce the target motion or derived actuator commands. Loss
  curve visualized. First the copy is bad, then it's good.

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

- NEAT (topology evolution)
- Novelty search
- Recurrent networks (memory)
- Reinforcement learning (DQN, policy gradients)
- Curriculum learning
- Adversarial co-evolution
- Attention / small transformer

Each will get its own section when it lands.
