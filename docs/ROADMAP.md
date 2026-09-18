# Roadmap

Node Runner is developed in small, always-shippable increments. Each version
below is an **Android APK you can install and demo**. Do not start version N+1
before version N runs end-to-end on a device.

Time estimates assume evening/weekend hobby pace and are rough.

---

## v0.1 — "Ryckningar" (Twitches)

**Goal:** Prove the stack works end-to-end.

- Godot 4 + C# project builds and exports to Android
- One hardcoded creature (e.g. a 5-joint "worm") lives in a 2D physics scene
- `NeuralNetwork.cs` under `libs/NodeRunner.ML/` — pure C#, feedforward, tanh
  gameplay activation
- Random weights → creature twitches randomly
- Single UI button: **Randomize**

**ML concept introduced:** What a neural network *is* in code — matrix
multiplications with an activation function. No training yet.

**Ship criterion:** APK installed on a real Android device, twitching creature
visible.

---

## v1.0 — "Den första vandringen" (The first walk) — MVP

**Goal:** See evolution actually work.

- 20 copies of the hardcoded creature run in parallel
- Genetic algorithm from scratch:
  - Fitness = distance travelled in 10 seconds
  - Tournament selection
  - Uniform crossover of weight vectors
  - Gaussian mutation with tunable rate
- Live fitness graph (best + mean per generation)
- Time-scale button: 1× / 5× / 20×
- Neon visuals: readable dark arena, glowing creature nodes, and a clear
  theme-specific head marker

**ML concepts introduced:** Genetic algorithms, fitness functions, mutation
rate, selection pressure, emergent behavior.

**Ship criterion:** A first-time viewer says "oh cool, it learned to walk"
within 2 minutes of opening the app.

---

## v1.5 — "Rita din varelse" (Draw your creature)

**Goal:** Turn the demo into an app.

- Drawing mode: tap for joints, drag for bones, double-tap a bone to make it a
  muscle
- Constraints: at least two ground-contact points, max ~20 joints
- Automatic NN sizing from creature topology:
  - Inputs = joint angles + angular velocities + ground-contact booleans
  - Outputs = target muscle activations
  - Hidden layer: heuristic (e.g. 2 × input count)
- "Terrarium" screen: list of saved creatures with their best fitness

**ML concepts introduced:** Feature engineering (what does the network *see*?),
network-size-to-problem fit, observation → action mapping.

---

## v2.0 — "Visualisera hjärnan" (Visualize the brain)

**Goal:** Make it genuinely educational.

- Live network visualization panel next to the focused creature:
  - Nodes colored by activation (red negative, blue positive, brightness =
    magnitude)
  - Edge thickness/opacity by weight
- Tap a creature → focus it, show its network updating in real time
- Slider panel:
  - Mutation rate
  - Population size
  - Hidden-layer count & width
  - Activation function (ReLU / tanh / sigmoid)
- Side-by-side mode: two populations, two hyperparameter sets, same seed

**ML concepts introduced:** Activation functions, network capacity,
hyperparameter sensitivity, interpretability.

---

## v3.0 — "Andra pelaren: Backprop" (Second pillar: backprop)

**Goal:** Introduce gradient-based training.

- **Imitation mode:** the player controls muscles manually via touch sliders
  for 20 seconds. Recorded (sensor → action) pairs become supervised data.
  Backprop trains the network to imitate. Release and observe.
- **Classification mini-mode:** draw creatures, label them ("hopper", "crawler",
  "swimmer"). Train a classifier. Visualize the decision boundary.
- Live loss curve
- Optimizer toggle: SGD / SGD + momentum / Adam (with tooltip explaining each)

**ML concepts introduced:** Backpropagation, loss functions, learning rate,
epochs, optimizers, supervised learning, overfitting.

**Ship criterion:** App now presents *two paradigms* (evolutionary and
gradient-based) and the user can articulate the difference.

---

## v4+ — Advanced toppings (order not fixed)

Picked from as time and interest allow:

- **NEAT** — topology evolves, not just weights. Very visual.
- **Novelty search** — reward for new behavior instead of raw fitness.
- **Reinforcement Learning mode** — DQN or policy gradients as a third training
  method.
- **Environments** — hills, water, ceilings, platforms, climbing walls.
- **Curriculum learning** — auto-scaling difficulty.
- **Recurrent networks** (GRU/LSTM) — memory tasks.
- **Adversarial** — a predator that also evolves; prey must survive.
- **Attention / small transformer** — if we ever get there, huge visualization
  payoff.

Each of these should get its own detailed roadmap doc in `docs/` when it's next
on deck.

---

## Non-goals (for now)

Explicitly out of scope so we don't drift:

- Online multiplayer or account systems
- Social sharing / creature marketplace
- iOS, web, or console builds
- Real-time collaborative editing
- 3D
- Using existing ML libraries (PyTorch, ONNX, ML-Agents). We build from scratch.
- Monetization
