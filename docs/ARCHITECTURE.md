# Architecture

Where code lives and which way it may depend.

## Layer diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     UI (Godot Controls)                          │
│  project/src/ui/lib · screens · widgets · project/scenes/        │
└─────────────────────────────────────────────────────────────────┘
                          ▲                │
              observes VM │                │ user input
                          │                ▼
┌─────────────────────────────────────────────────────────────────┐
│               ViewModels + Repositories + Services               │
│                       libs/NodeRunner.App/                       │
│         plain C# — INotifyPropertyChanged / events               │
│         no Godot references, arch-tested                         │
└─────────────────────────────────────────────────────────────────┘
                          ▲                │
             sim events   │                │ commands / services
                          │                ▼
┌─────────────────────────────────────────────────────────────────┐
│  Simulation & Creature (Godot)      Managers (Godot autoloads)   │
│  project/src/{sim,creature}/        project/src/managers/        │
│                                     the composition root         │
└─────────────────────────────────────────────────────────────────┘
                          ▲                │
                          │                ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Mechanics (pure C#)                          │
│                   libs/NodeRunner.Mechanics/                     │
│   Accelerometer · CameraRays · Servo · Piston · Spring           │
└─────────────────────────────────────────────────────────────────┘
                          ▲                │
                          │                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Domain data (pure C#)                         │
│                    libs/NodeRunner.Domain/                       │
│      CreatureDef · NodeDef · BeamDef · SensorDef · Vector2D      │
└─────────────────────────────────────────────────────────────────┘
                                           │
                                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ML engine (pure C#)                         │
│                       libs/NodeRunner.ML/                        │
│          NeuralNetwork · Activation · Networks · Ga              │
└─────────────────────────────────────────────────────────────────┘
```

The four libraries in `libs/` are .NET 8 class libraries with **no
`Godot.*` references**. The Godot project (`project/`) targets .NET 9 for
Godot 4.7's Android export templates, references the libraries, and provides
the runtime host: scenes, physics, input, rendering. `NodeRunner.App`
references the System.Text.Json 9 package, the same version the host runs,
for strict save loading (`docs/SAVE_FORMAT.md`).

`tests/NodeRunner.Arch.Tests/ArchitectureSpec.cs` enforces the dependency
rules for the libraries only: no lib references Godot, Domain references no
other lib, Mechanics only Domain, and ML neither App nor Mechanics. Nothing
tests the `project/` rules below; review must catch a breach.

## Dependency rules

Arrows only go **downward** across layer boundaries.

- **UI** (`project/src/ui/`) depends on: `NodeRunner.App` (ViewModels),
  `NodeRunner.Domain` (for display types), `NodeRunner.Mechanics` (to draw a
  part as the sim moves it), `project/src/theme/`, `project/src/ui/lib`
  (Controls).
  **Never** on: sim, managers, `NodeRunner.ML`.
- **App** (`libs/NodeRunner.App/` — ViewModels, Repositories, Services)
  depends on: `NodeRunner.Domain`, `NodeRunner.Mechanics`, `NodeRunner.ML`.
  **Never** on: Godot, sim, creature.
- **Sim & Creature** (`project/src/{sim,creature}/`) depend on:
  `NodeRunner.Domain`, `NodeRunner.Mechanics`, `NodeRunner.ML`,
  `project/src/theme/`, and Godot.
  **Never** on: UI, ViewModels, Managers. A host adapts sim events for
  view-models: `EvolverTrainingProgressSource` turns `Evolver`'s events into
  the App's `ITrainingProgressSource`.
- **Theme** (`project/src/theme/`) depends on: `NodeRunner.Domain`,
  `project/src/ui/lib` and Godot. It draws the arena and creature parts from
  plain visual state (`.claude/rules/theme.md`).
- **Managers** (`project/src/managers/`) depend on: `NodeRunner.App` (for
  repository/service interfaces), `NodeRunner.Domain`, Godot.
  Managers are the **composition root** — they wire concrete implementations
  into ViewModels at startup.
- **Scene roots** (the `*Host` scenes: scripts and helpers in
  `project/src/hosts/`, scenes in `project/scenes/hosts/`) may depend on
  every project layer above. They wire a screen's signals to managers and
  the navigator; keep game rules out of them. Nothing depends on them.
- **Domain** (`libs/NodeRunner.Domain/`) depends on: nothing but the .NET BCL.
  Its `Vector2D` is our own `readonly record struct`, **not**
  `Godot.Vector2`; the creature layer converts at its boundary.
  `docs/CREATURE_MODEL.md` owns the model its records encode.
- **Mechanics** (`libs/NodeRunner.Mechanics/`) depends on: `NodeRunner.Domain`
  only. It holds the pure physics of creature parts — the sums the sim runs
  each step and Build reuses to draw the same thing (#596). What stays in
  Domain, and why, is listed in `.claude/rules/domain.md`.
- **ML** (`libs/NodeRunner.ML/`) depends on: `NodeRunner.Domain` only.

## Solution layout

```
Node Runner/
├── NodeRunner.slnx                 # solution (new .NET 10 XML format)
├── Directory.Build.props           # shared C# settings
├── Directory.Packages.props        # central package versions (CPM)
├── .editorconfig                   # style rules
├── dotnet-tools.json               # local .NET tools: csharp-ls for the agents' LSP tool
├── libs/                           # pure C#, no Godot
│   ├── NodeRunner.Domain/          # data records, enums, invariants
│   ├── NodeRunner.Mechanics/       # pure part physics: sensors, servos, pistons, springs
│   ├── NodeRunner.ML/              # neural networks, GA
│   └── NodeRunner.App/             # viewmodels, services, repositories
├── project/                        # Godot project (targets net9.0)
│   ├── project.godot
│   ├── NodeRunner.csproj           # references the four libs
│   ├── NodeRunner.sln              # classic .sln required by Godot .NET export
│   ├── scenes/                     # hosts/ (screen hosts), screens/, ui/, widgets/, tools/
│   └── src/
│       ├── creature/               # Godot Nodes for creatures
│       ├── sim/                    # simulation orchestration
│       ├── hosts/                  # routed scene roots that wire screens to managers
│       ├── managers/               # service autoloads / composition root
│       ├── theme/                  # arena (world) visuals and shared creature parts, not UI styling
│       ├── tools/                  # editor/CLI tools; their scenes are not exported
│       └── ui/
│           ├── lib/                # reusable Controls
│           ├── screens/            # full-screen scenes
│           └── widgets/            # app-specific composite widgets
└── tests/                          # xUnit — libs plus static Godot source contracts
    ├── NodeRunner.Domain.Tests/
    ├── NodeRunner.Mechanics.Tests/
    ├── NodeRunner.ML.Tests/
    ├── NodeRunner.App.Tests/
    ├── NodeRunner.Arch.Tests/      # layer rules, source conventions, agent files, skills, hooks
    └── NodeRunner.Ui.Tests/        # static Godot source contracts; no scene tree
```

## The tick

At 60 Hz (`_physics_process`) the creature first lets go of any beam end
past the tear limit (`docs/CREATURE_MODEL.md` → Beam → Tearing), then each
Spring sets its rest length and each Brake holds its joint, network or not
(`Mechanics.Brake`). With no network, each Stepper then keeps its notch locked
(`Mechanics.Stepper`) and the tick ends; with one, the creature runs four
steps:

1. **Sense:** every part writes its readings into one `double[]` in
   `NetworkPorts` order (`docs/CREATURE_MODEL.md` → "Sensor–network contract").
2. **Think:** `Network.Forward` writes each output port's value into
   caller-owned buffers, with no per-tick allocations.
3. **Act:** each Servo, then each Motor, then each Piston, then each
   Stepper drives toward its outputs;
   Mechanics computes the torque or force and the creature layer applies it
   to the bodies.
4. **Score:** `TrialMeasurement` records the trial (`docs/TRAINING_LOOP.md`
   → Trial).

`docs/TRAINING_LOOP.md` owns trials, generations and what the Training
scene shows.

## Navigation

Each screen is one scene. Navigating replaces the current scene (#326): the
scene left is closed, not paused, and Back rebuilds it from its route, so
anything the player expects to find again is saved before the scene closes.

- A screen (`ui/screens/`) knows nothing of saves or the router: it emits
  signals, and its host (`project/src/hosts/`) wires them to `SaveManager`
  and the navigator. Gallery pages have nothing to save, so they are routed
  directly.
- `SceneRoute` is one sealed record per scene; its properties are the plain
  values the scene is built from (a creation id). Every route is declared in
  `libs/NodeRunner.App/Navigation/`, because the test that keeps routes to
  plain values scans only that assembly.
- `SceneNavigation` opens a route with two choices: **keep current** (does
  the scene left stay in the history) and **launch mode** (*unique* drops
  earlier entries of the same scene, *stacked* pushes on top).
- `SceneBackStack` holds the history. Its root (Creations) is never
  removed; Back on the root returns null, and Android leaves the app.
- `ISceneNavigator` and `IRoutedScene` live in App so UI scenes need not
  know the manager. The `SceneRouter` autoload (`project/src/managers/`)
  implements the navigator and is the only code that changes scenes. Godot
  opens the main scene itself, so its host takes the router from the
  autoload.
- Android Back and Escape go through a `UiBackHandler` (`ui/lib`) on each
  routed screen: an open menu, dialog or sheet takes Back first, then the
  scene goes one step back (#474). `.claude/rules/ui.md` says how to
  code it.
- What must outlive a scene change lives in `ui/lib` autoloads, not
  `managers/`, because managers hold no UI: `Notifications`
  (`UiNotificationLayer`, #472), `BackPress` (one go-back per Back press,
  #506, #838), `SafeArea` (display-cutout insets, #513) and `UiScale` (the
  UI size, #299; how it scales without touching the world:
  `.claude/rules/ui-lib.md`).

## UI text and translation

Godot translates UI text (`TranslationServer`, gettext PO); the English
source text is the msgid (#682). Text written in a scene is translated by
its Control. Text App builds for the player crosses as a `UiText`
(`NodeRunner.App/ViewModels`, #752): the message, its plural and count, its
arguments and a context. App never renders it as English, so its tests
compare `UiText` values. `UiTextTranslation` (`ui/widgets`) is the one place
that turns a `UiText` into the player's language. A workflow that saves a
default name takes a `Func<UiText, string>`, and the hosts pass
`UiTextTranslation.Now`, since managers hold no UI (#759).

`docs/LOCALIZATION.md` → "Text rules" owns what the text says and how it is
translated; `.claude/rules/ui.md` owns how UI code shows it.

## Threading

Physics runs on Godot's thread. `Evolver` evaluates a whole generation (up
to 100 candidates) at once in parallel slots of one scene; this is parallel
evaluation, not multithreaded physics. Training saves are the one
thread-pool write (#113, `docs/TRAINING_LOOP.md` → Save).
