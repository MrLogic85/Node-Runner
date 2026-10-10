# ML Concepts

Which machine-learning ideas Node Runner teaches and how each is made
visible. For each concept:

- **What:** one-sentence definition
- **Where:** code location and issue
- **How we show it:** the visual or interactive hook

Live knobs for the GA and activations, side-by-side runs and switching a
sensor off are #960.

## Feedforward neural network

- **What:** Layers of linear maps with a nonlinearity in between.
- **Where:** `libs/NodeRunner.ML/NeuralNetwork.cs`; the direct brain
  (`Brains/DirectBrain`, #536)
- **How we show it:** The creature moves at all. The brain is direct, inputs
  straight to outputs, and BrainFocus draws it with nodes and edges (#536).

## Activation functions

- **What:** The nonlinearity between layers. Without it, a deep network
  collapses to a single linear map.
- **Where:** `libs/NodeRunner.ML/Activation.cs` (`Tanh`, `ReLU`,
  `Sigmoid`); each output uses its signal's activation, tanh for position,
  sigmoid for strength (#535).
- **How we show it:** Swapping the activation of a trained brain and
  watching its behaviour change (#960).

## Genetic algorithm (neuroevolution)

- **What:** Search weights by simulating a population, keeping the fit ones,
  recombining them, mutating a bit, repeating.
- **Where:** `libs/NodeRunner.ML/Ga/GeneticAlgorithm.cs`. Training resumes
  from the saved brain (#538), and Shadows sets the population size per
  Creation (#528).
- **How we show it:** Every candidate races at once as a shadow behind the
  followed creature, the best marker moves forward, and mutations produce
  weird outliers.

## Fitness function

- **What:** The scalar that says "this individual was this good". *Choice of
  fitness is the most important design decision in evolutionary ML.*
- **Where:** `libs/NodeRunner.ML/Ga/TrialMeasurement.cs`
- **How we show it:** Fitness is how far the creature's centre got, but the
  app shows where its front ended (#725, `docs/TRAINING_LOOP.md` → Trial),
  so the number on screen matches the ruler. The GA ranks by a number the
  player does not see directly. Several fitness functions at once, each
  winner a parent of the next generation, are #317 and #546.

## Mutation rate & exploration/exploitation

- **What:** How aggressively we perturb weights per generation. Too low →
  stuck in local optima. Too high → no learning.
- **Where:** `GeneticAlgorithm`; the rate is fixed.
- **How we show it:** Side-by-side populations with different rates: one
  learns steadily, one thrashes (#960).

## Selection pressure

- **What:** How much better fit individuals dominate the next generation.
  Tournament size is our lever.
- **Where:** `GeneticAlgorithm`; the tournament size is fixed.
- **How we show it:** A tournament-size knob: high pressure converges fast
  with low diversity, low pressure is slow and exploratory (#960).

## Feature engineering / observation design

- **What:** What the network gets to *see*. Bad inputs cap performance;
  redundant inputs waste capacity.
- **Where:** Sensor parts on beams (Accelerometer, Camera;
  `docs/CREATURE_MODEL.md`) and Servo and Piston readings. There is no
  speed or elevation input: the brain learns movement from what its parts
  feel. Pulse, a rhythm input, is #527.
- **Why "nothing seen" reads 0 (#604):** a zero input adds nothing to the
  brain's weighted sum, so the weight on a camera ray only matters while
  something is in view. Mutating that weight adds no noise while the
  camera sees nothing, and the brain learns to react to something being
  there. Reading 1 for "nothing" would act like an extra bias that vanishes
  the moment the ground appears. The reading is also linear and smooth at
  the range edge: something just inside range reads about 0, the same as
  nothing, so the input never jumps, and half the bar means half the
  range.
- **How we show it:** BrainFocus lists every sense by its part with its
  live value.

## Live activation visualization

- **What:** Which neuron fires at which moment, and how strongly.
- **Where:** BrainFocus for the direct brain (#536), and the selected
  part's live sense and output values in Training's part callout (#1064,
  `docs/WORLD_VISUALS.md` → "Port bars"); brain views with hidden layers
  are #196, #197 and #548.
- **How we show it:** Nodes glow. Edges pulse. You literally see the thought
  behind each step.

## Concepts with an issue

Each issue owns its hook; these lines say what the concept teaches.

- **Network capacity (#543):** a hidden layer added on top of the kept
  direct connections keeps the skill the brain had; too little capacity
  plateaus, a huge net learns slowly and erratically.
- **Generalisation across maps (#540, #541):** a brain that only sees one
  map overfits to it; Stats shows a learning curve per map.
- **Cost of acting (#128, #458, #460):** a power budget that ends the run
  when spent makes the brain trade force now against staying in the run.
- **Checkpoints (#256):** training is a path, not only its end point;
  restoring one rewinds the history.
- **Gradient training (#955):** backprop, loss functions, optimizers and
  overfitting, taught through an imitation mode and contrasted with
  evolution.
- **Further paradigms:** NEAT (#522), novelty search (#546), memory cells
  (#126) and the rest of #965.
