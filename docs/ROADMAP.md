# Roadmap

Node Runner is developed in small, always-shippable increments. Each version
below is an **Android APK you can install and demo**. Do not start version N+1
before version N runs end-to-end on a device. `docs/ML_CONCEPTS.md` defines
which ML ideas each version teaches and how they are made visible.

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

- [x] Creature vocabulary is documented in durable docs.
- [x] The hardcoded worm is explainable through that vocabulary.
- [x] User can select nodes, beams, and cores with touch.
- [x] Selected creature part is visually highlighted.
- [x] Inspector panel shows beginner-facing role text and key values.
- [x] Sensor values, model outputs, and motor-relation activity are visible
  enough to explain the control loop. (Mapping panel, #42; validated
  on-device by #43.)
- [x] Randomize keeps inspector/mapping UI consistent with the new brain seed.
- [x] A first-time viewer can explain node, beam, core, motor relation,
  sensor, input, and output from the app.
- [x] The release gates in `docs/REVIEW.md` and Android checks in
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
- [x] Backprop remains explicitly out of scope for this milestone
  (confirmed: no backprop code exists anywhere in `libs/` or `project/src/`;
  neuroevolution — `GeneticAlgorithm`/`Evolver` — is the only training
  paradigm implemented).
- [x] A first-time viewer can see that behavior improved and identify the
  fitness signal (verified on-device in #52: a fresh seeded run showed Best
  fitness rise from 46.0 at generation 1 to 49.9 at generation 5, with Mean
  fitness rising from ~9 to 22.5 over the same window — both values visible
  on the HUD at every generation boundary).
- [x] The release gates in `docs/REVIEW.md` and Android checks in
  `docs/MANUAL_TESTING.md` are satisfied for 0.4.0 (see #52: on-device
  verification of ≥5 generations, all HUD controls, and construction-mode
  interaction, with no crashes/hangs/exceptions in `adb logcat`).

---

> **Tracking status:** 0.5.0 through 0.9.0 reflect the game-loop-first
> sequence (build → save as Creation → edit/resume training → tune training
> → unlock parts → visualize). GitHub milestones and tracking issues own
> progress and acceptance-criteria state for each version; this document only
> holds the durable narrative (goal, ML concepts, ship criterion). Check the
> linked issue for current status rather than a local checklist.

| Milestone | GitHub tracking issue |
|---|---|
| 0.5.0 Creations | [#93](https://github.com/MrLogic85/Node-Runner/issues/93) |
| 0.6.0 Edit and resume training | [#94](https://github.com/MrLogic85/Node-Runner/issues/94) |
| 0.7.0 Training configuration | [#95](https://github.com/MrLogic85/Node-Runner/issues/95) |
| 0.8.0 Progression: first unlock | [#96](https://github.com/MrLogic85/Node-Runner/issues/96) |
| 0.9.0 Visualize the brain | [#97](https://github.com/MrLogic85/Node-Runner/issues/97) |

## 0.5.0 — "Creations" (Save, list, and manage builds)

**Goal:** Establish the game loop's foundation: a build becomes a durable
Creation the player can return to, instead of disappearing when the app
closes. This is a prerequisite for every later milestone in this sequence
(Edit, training config, progression, and eventually save/load are all
built on top of "a Creation is a real, addressable thing").

- Build mode gains a **Complete** action: validates the anatomy (same
  structural-completeness rules as current construction-mode validation,
  see `docs/CONSTRUCTION_MODE.md` § Validation) and saves it as a Creation.
- A **Creations** screen: list saved Creations, delete, duplicate.
  - Duplicate creates an independent copy, including the current training
    state. The copy can later be reset to a new seed with random weights.
- Domain-level persistence for `CreatureDef` and the current training state
  needed to make duplication faithful. Edit-based resume and generation
  history management remain 0.6.0 concerns.
- Resetting a Creation's training state removes its saved genome and
  generation; the next run starts with a fresh random population and seed.
- No Edit mode yet — a completed Creation is a saved snapshot, not yet
  resumable/trainable-in-place. That distinction is what 0.6.0 adds.

**Ship criterion:** A player can build a creature, tap Complete, close and
reopen the app, and find it again in Creations.

---

## 0.6.0 — "Edit and resume training"

**Goal:** Make a Creation something you come back to and keep improving,
not just a static save file.

- **Edit** mode: opens a saved Creation for editing, but — unlike Build —
  can only move existing parts, not add/remove them. This preserves the
  brain's sensor/motor layer shape (topology-derived, see
  `docs/CREATURE_MODEL.md`), so training can resume in place.
- Edit remembers and resumes training history/progress for that Creation:
  the best genome and generation count are persisted. A seed is not needed
  to resume because the trained model is saved; a new seed is generated only
  when training is explicitly reset to random weights.
- **Rebuild** action (Edit → full Build mode, add/remove parts allowed
  again): copies the Creation into a new Build draft. The original Creation
  remains unchanged, and the new Creation starts without the previous
  training model because topology changes can invalidate the old genome's
  shape.
- This is also the natural point to evaluate whether the current
  construction-mode UI (`project/src/creature/` construction tools) needs
  a genuine reimplementation to support Edit's move-only interaction, or
  can be extended. Run a `design-lead` review pass (see
  `docs/UI_DIRECTION.md` § Design review) before deciding reimplement vs.
  iterate.

**Ship criterion:** A player can leave a Creation mid-training, come back
later, resume training without losing prior progress, and reposition a
part without invalidating that progress.

**Implementation status:** tracked in [#94](https://github.com/MrLogic85/Node-Runner/issues/94).

---

## 0.7.0 — "Training configuration"

**Goal:** Let the player tune how a Creation trains, per session, instead
of fixed engine constants.

- Trial duration and population size (currently fixed:
  `TrialController.TrialDurationTicks` defaults to 600 ticks ≈10s
  in `project/src/sim/TrialController.cs`; `Main.cs`'s
  `_populationSize = 8` configures the `Evolver`, see
  `docs/TRAINING_LOOP.md`) become player-facing, session-scoped settings
  rather than fixed constants. Generations-per-session (running a bounded
  number of generations before stopping/reviewing, rather than evolving
  indefinitely) is a new concept to introduce here, not an existing
  setting.
- Support the workflow the player described: e.g. many short trials (a
  couple of seconds × dozens of runs) before switching to longer sessions
  — a session-level training-profile choice, not a single fixed setup.
- Natural point to also address the GA-plateau behavior observed in
  practice this session (Best fitness flattening ~generation 50): larger
  population and/or a less "competing conventions"-prone recombination
  strategy become things the player can try, not just code-level tuning.

**ML concepts introduced:** Trial/session design as a hyperparameter in
its own right — batch size vs. duration tradeoffs in an evolutionary
context.

**Implementation status:** tracked in [#95](https://github.com/MrLogic85/Node-Runner/issues/95).

---

## 0.8.0 — "Progression: first unlock"

**Goal:** Prove the unlock loop with one concrete, small slice before
expanding it.

- One measurable training milestone (e.g. "reach N meters") unlocks a new
  part globally — available in the Build palette for all future builds,
  not just the Creation that earned it (assumption, flagged for
  confirmation).
- Training UI shows live progress toward the next unlock (distance/metric
  and the "reached at generation G" framing already used for Best
  fitness — see `docs/TRAINING_LOOP.md`).
- Deliberately narrow scope: prove the pattern works end-to-end (train →
  hit threshold → part appears in Build) before adding the other unlock
  conditions already discussed (jump height, an "agility score", etc.) —
  those are expected to keep evolving as the game does, not to be fully
  designed now.

**Ship criterion:** A first-time player can watch a concrete, visible
threshold approach during training and see a new part become available in
Build immediately after crossing it.

**Implementation status:** tracked in [#96](https://github.com/MrLogic85/Node-Runner/issues/96).

---

## 0.9.0 — "Visualisera hjärnan" (Visualize the brain)

**Goal:** Make training genuinely educational, now situated inside the
Edit/inspect step of the established game loop rather than a standalone
feature.

- Live network visualization panel next to the focused creature:
  - Nodes colored by activation (red negative, blue positive, brightness =
    magnitude)
  - Edge thickness/opacity by weight
- Tap a creature → focus it, show its network updating in real time
- Slider panel (builds on 0.7.0's training-configuration UI):
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
