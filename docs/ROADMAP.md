# Roadmap

Node Runner is developed in small, always-shippable increments. Each version
below is an **Android APK you can install and demo**. Do not start version N+1
before version N runs end-to-end on a device.

Time estimates assume evening/weekend hobby pace and are rough.

---

## 0.1.0 — "Ryckningar" (Twitches)

**Goal:** Prove the stack works end-to-end.

- Godot 4 + C# project builds and exports to Android
- One hardcoded creature (e.g. a 5-node "worm") lives in a 2D physics scene
- `NeuralNetwork.cs` under `libs/NodeRunner.ML/` — pure C#, feedforward, tanh
  gameplay activation
- Random weights → creature twitches randomly
- Single UI button: **Randomize**

**ML concept introduced:** What a neural network *is* in code — matrix
multiplications with an activation function. No training yet.

**Ship criterion:** APK installed on a real Android device, twitching creature
visible.

---

## 0.2.0 — "Förstå figuren" (Understand the creature)

**Goal:** Make the current hardcoded creature understandable before it becomes
editable or trainable.

- Formalize the creature vocabulary in docs and code (see
  `docs/CREATURE_MODEL.md`):
  - Node = physical attachment point where beams meet
  - Beam = rigid, fixed-length structural connection between two nodes
  - Core = sensor package mounted on a node (rays, pitch, elevation, speed)
  - Motor relation = a controllable rotation between two beams at a node,
    derived from topology, driven by torque toward a brain-set target
  - Model input/output mapping = the contract between body and network
- Add a basic touch-first inspector:
  - Tap a node, beam, or core to select it
  - Highlight the selected element in the neon scene
  - Show its role and important values in a simple panel
- Visualize the control loop at a beginner-friendly level:
  - Which sensor values feed the model
  - Which model outputs drive which motor relations
  - Which motor relations are currently active
- Keep the hardcoded worm as the default demo creature, but make sure it is
  described by the same `CreatureDef` concepts the future editor will use.

**ML concepts introduced:** Observation → action mapping, sensors as features,
outputs as actuators, and the idea that a neural network controls a body through
a defined interface.

**Ship criterion:** A first-time viewer can tap the creature and explain what a
node, beam, core, motor relation, input, and output are in the current demo.

**Done checklist:**

- [ ] Creature vocabulary is documented in durable docs.
- [ ] The hardcoded worm is explainable through that vocabulary.
- [ ] User can select nodes, beams, and cores with touch.
- [ ] Selected creature part is visually highlighted.
- [ ] Inspector panel shows beginner-facing role text and key values.
- [ ] Sensor values, model outputs, and motor-relation activity are visible
  enough to explain the control loop.
- [ ] Randomize keeps inspector/mapping UI consistent with the new brain seed.
- [ ] A first-time viewer can explain node, beam, core, motor relation,
  sensor, input, and output from the app.
- [ ] The release gates in `docs/REVIEW.md` and Android checks in
  `docs/MANUAL_TESTING.md` are satisfied for 0.2.0.

---

## 0.3.0 — "Bygg figuren" (Build the creature)

**Goal:** Let the user construct a small valid creature from the same model
introduced in 0.2.0.

- Construction mode:
  - Place and move nodes
  - Connect two nodes with a beam
  - Attach a core to a node
  - Delete the selected element
  - Return to simulation mode and instantiate the creature
- Validate the creature before simulation:
  - At least one node
  - Every node has at least one beam
  - No missing node references
  - No zero-length beams
- Generate model input/output counts from the creature topology (via
  `MotorTopology`):
  - Inputs = cores' sensor values + motor relations' sensor values
  - Outputs = motor relations
  - Hidden layer = simple documented heuristic
- Keep the editor intentionally limited; this milestone proves the data model,
  not a full authoring tool.

**ML concepts introduced:** Feature engineering (what does the network *see*?),
network-size-to-problem fit, observation → action mapping.

**Ship criterion:** The user can build a simple creature on Android, run it, and
see which motor relations became neural-network outputs.

**Done checklist:**

- [x] Construction workflow is documented before implementation starts (see
  `docs/CONSTRUCTION_MODE.md`).
- [x] User can place at least two nodes with Android touch.
- [x] User can move nodes before simulation starts.
- [x] User can connect nodes with beams.
- [x] User can attach a core to a node.
- [x] User can delete a node or beam (Delete tool, tap to act); a core is
  removed via the Core tool's tap-to-toggle (see `docs/CONSTRUCTION_MODE.md`
  for why this doesn't use a separate select-then-delete step).
- [x] A non-empty, invalid creature is blocked with understandable validation
  messages when attempting to leave Build mode; an untouched empty canvas
  may always leave (see `docs/CONSTRUCTION_MODE.md`'s Validation section).
- [x] A valid edited creature can be converted into a `CreatureDef`.
- [x] Simulation can instantiate and run the edited creature.
- [x] Model input count comes from the creature's core sensor values plus
  `MotorTopology`'s derived motor-relation sensor values (see this
  document's Model section above).
- [x] Model output count equals motor-relation count.
- [x] The original hardcoded worm still works.
- [ ] The release gates in `docs/REVIEW.md` and Android checks in
  `docs/MANUAL_TESTING.md` are satisfied for 0.3.0.

---

## 0.4.0 — "Den första träningen" (First training)

**Goal:** Train a creature through neuroevolution after the body model and basic
UI are understandable.

- Run a small population of creatures or repeated trials of one creature
  (see `docs/TRAINING_LOOP.md`)
- Genetic algorithm from scratch:
  - Fitness = distance travelled or another visible objective
  - Tournament selection
  - Uniform crossover of weight vectors
  - Gaussian mutation with tunable rate
- Show generation, best fitness, mean fitness, and current best seed/genome
  (in practice: run seed plus the generation the current best was found at
  — there's no per-genome reproducibility seed; see `docs/TRAINING_LOOP.md`)
- Add run/pause/reset and time-scale controls where they fit the 0.2 UI shell
- Keep backprop out of this milestone; evolution is the first training paradigm
  because it fits physics-driven locomotion without target labels.

**ML concepts introduced:** Genetic algorithms, fitness functions, mutation
rate, selection pressure, emergent behavior.

**Ship criterion:** A first-time viewer says "oh cool, it improved" within a
short demo, and can see the fitness signal that caused the improvement.

**Done checklist:**

- [x] A fixed-duration trial can run and reset cleanly.
- [x] Fitness scoring is implemented and visible.
- [x] Candidate brains/genomes can be evaluated under comparable conditions.
- [x] Genetic algorithm advances generations using selection, crossover, and
  mutation.
- [x] Best fitness and mean fitness are tracked across generations.
- [x] Training UI shows generation, fitness, and current run seed/best
  generation (redefined from "seed or genome" — no per-genome
  reproducibility seed exists; see `docs/TRAINING_LOOP.md`).
- [x] Run/pause/reset controls work with Android touch.
- [x] Time-scale control exists if it fits the 0.2 UI shell cleanly.
- [ ] Backprop remains explicitly out of scope for this milestone.
- [ ] A first-time viewer can see that behavior improved and identify the
  fitness signal.
- [ ] The release gates in `docs/REVIEW.md` and Android checks in
  `docs/MANUAL_TESTING.md` are satisfied for 0.4.0.

---

## 0.5.0 — "Visualisera hjärnan" (Visualize the brain)

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

## Later — "Andra pelaren: Backprop" (Second pillar: backprop)

**Goal:** Introduce gradient-based training.

- **Imitation mode:** the player demonstrates the first X steps by moving
  nodes while beam lengths and constraints stay fixed. Recorded target motion
  or derived motor-relation commands become supervised data. Backprop trains
  the network to imitate. Release and observe.
- **Classification mini-mode:** draw creatures, label them ("hopper", "crawler",
  "swimmer"). Train a classifier. Visualize the decision boundary.
- Live loss curve
- Optimizer toggle: SGD / SGD + momentum / Adam (with tooltip explaining each)

**ML concepts introduced:** Backpropagation, loss functions, learning rate,
epochs, optimizers, supervised learning, overfitting.

**Ship criterion:** App now presents *two paradigms* (evolutionary and
gradient-based) and the user can articulate the difference.

---

## Later — Advanced toppings (order not fixed)

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
