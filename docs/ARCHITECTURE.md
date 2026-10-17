# Architecture

Where code lives and which way it may depend.

## Layers

The four libraries in `libs/` are .NET 8 class libraries with **no
`Godot.*` references**, so the rules, physics sums and ML can be tested
without the engine. The Godot project (`project/`) targets .NET 9 for
Godot's Android export templates, references the libraries and provides
the runtime host: scenes, physics, input, rendering. `NodeRunner.App`
references System.Text.Json 9, the version the host runs, for strict save
loading (`docs/SAVE_FORMAT.md`).

`tests/NodeRunner.Arch.Tests/ArchitectureSpec.cs` enforces the rules for
the libraries only. Nothing tests the `project/` rules; review must catch a
breach.

## Dependency rules

| Layer | Where | Depends on | Never on |
|---|---|---|---|
| **UI** | `project/src/ui/` (`lib`, `screens`, `widgets`) | App, Domain, Mechanics (to draw a part as the sim moves it), Theme, `ui/lib` | Sim, Managers, ML |
| **Scene roots** | `project/src/hosts/`, `project/scenes/hosts/` | every project layer | (nothing depends on them) |
| **Managers** | `project/src/managers/` | App, Domain, Godot | UI |
| **Sim & Creature** | `project/src/{sim,creature}/` | Domain, Mechanics, ML, Theme, Godot | UI, App, Managers |
| **Theme** | `project/src/theme/` | Domain, `ui/lib`, Godot | |
| **App** | `libs/NodeRunner.App/` | Domain, Mechanics, ML | Godot, Sim, Creature |
| **Mechanics** | `libs/NodeRunner.Mechanics/` | Domain | |
| **ML** | `libs/NodeRunner.ML/` | Domain | App, Mechanics |
| **Domain** | `libs/NodeRunner.Domain/` | the .NET BCL | |

- **App** holds the ViewModels, repositories and services in plain C#
  (`INotifyPropertyChanged`, events). The UI observes a ViewModel and sends
  it the player's input.
- **Managers** are the **composition root**: Godot autoloads that wire
  concrete implementations into ViewModels at startup.
- **Scene roots** (the `*Host` scenes) wire a screen's signals to managers
  and the navigator (`.claude/rules/hosts.md`).
- **Sim** raises C# events and a host adapts them for ViewModels:
  `EvolverTrainingProgressSource` turns `Evolver`'s events into the App's
  `ITrainingProgressSource`.
- **Theme** draws the arena and creature parts from plain visual state,
  not UI styling (`.claude/rules/theme.md`).
- **Mechanics** holds the pure physics of creature parts: the sums the sim
  runs each step and Build reuses to draw the same thing. What stays
  in Domain, and why, is in `.claude/rules/domain.md`.
- **Domain**'s `Vector2D` is our own `readonly record struct`, not
  `Godot.Vector2`; the creature layer converts at its boundary.
  `docs/CREATURE_MODEL.md` owns the model its records encode.

## Solution layout

```
Node Runner/
├── NodeRunner.slnx                 # solution (XML format)
├── Directory.Packages.props        # central package versions (CPM)
├── dotnet-tools.json               # local .NET tools: csharp-ls for the agents' LSP tool
├── libs/                           # pure C#, no Godot
│   ├── NodeRunner.Domain/          # data records, enums, invariants
│   ├── NodeRunner.Mechanics/       # pure part physics
│   ├── NodeRunner.ML/              # neural networks, GA
│   └── NodeRunner.App/             # viewmodels, services, repositories, routes
├── project/                        # Godot project
│   ├── NodeRunner.sln              # classic .sln required by Godot .NET export
│   ├── scenes/                     # hosts/, screens/, ui/, widgets/, tools/
│   └── src/
│       ├── creature/               # Godot nodes for creatures
│       ├── sim/                    # simulation orchestration
│       ├── hosts/                  # routed scene roots
│       ├── managers/               # autoloads / composition root
│       ├── theme/                  # arena and creature-part visuals
│       ├── tools/                  # editor/CLI tools; not exported
│       └── ui/{lib,screens,widgets}/
└── tests/                          # xUnit, one project per lib, plus:
    ├── NodeRunner.Arch.Tests/      # layer rules, conventions, agent files, skills, hooks
    └── NodeRunner.Ui.Tests/        # static Godot source contracts; no scene tree
```

## The tick

Physics runs at a fixed 60 Hz (`_physics_process`). Each tick
`TrialController` first records the trial (`TrialMeasurement`,
`docs/TRAINING_LOOP.md` → Trial). The creature then lets go of any beam
end past the tear limit (`docs/CREATURE_MODEL.md` → Beam → Tearing), and
each Spring sets its rest length and each Brake holds, network or not.
With no network each Stepper holds its notch and the tick ends; with one:

1. **Sense:** every part writes its readings into one `double[]` in
   `NetworkPorts` order (`docs/CREATURE_MODEL.md` → "Sensor–network
   contract").
2. **Think:** `Network.Forward` writes the outputs into caller-owned
   buffers, with no per-tick allocations.
3. **Act:** each Servo, Motor, Piston, then Stepper drives toward its
   outputs; Mechanics computes the torque or force and the creature layer
   applies it to the bodies.

## Navigation

Each screen is one scene. Navigating replaces the current scene:
the scene left is closed, not paused, and Back rebuilds it from its route,
so anything the player expects to find again is saved before it closes.

- A screen knows nothing of saves or the router: it emits signals, and its
  host wires them to `SaveManager` and the navigator. Gallery pages save
  nothing, so they are routed directly.
- A route (`SceneRoute`) is one sealed record per scene whose properties
  are the plain values the scene is built from. Declare every route in
  `libs/NodeRunner.App/Navigation/`: the test that keeps routes to plain
  values scans only that assembly.
- The history's root (Creations) is never removed; Back on the root leaves
  the app on Android.
- `ISceneNavigator` and `IRoutedScene` live in App so scenes need not know
  the manager. The `SceneRouter` autoload implements the navigator and is
  the only code that changes scenes.
- Android Back and Escape go through a `UiBackHandler` on each routed
  screen: an open menu, dialog or sheet takes Back first, then the scene
  goes back (how to code it: `.claude/rules/ui.md`).
- What must outlive a scene change lives in `ui/lib` autoloads, not
  `managers/`, because managers hold no UI: `Notifications`, `BackPress`,
  `SafeArea` and `UiScale`.

## UI text and translation

Godot translates UI text (`TranslationServer`, gettext PO); the English
source text is the msgid. Text written in a scene is translated by
its Control. Text App builds for the player crosses as a `UiText`:
message, plural and count, arguments and context. App never renders it as
English, so its tests compare `UiText` values. `UiTextTranslation`
(`ui/widgets`) is the one place that turns a `UiText` into the player's
language. A workflow that saves a default name takes a
`Func<UiText, string>`, and the hosts pass `UiTextTranslation.Now`, since
managers hold no UI.

`docs/LOCALIZATION.md` → "Text rules" owns what the text says;
`.claude/rules/ui.md` owns how UI code shows it.

## Threading

Physics runs on Godot's thread. `Evolver` runs a whole generation at once
in parallel slots of one scene: parallel evaluation, not multithreaded
physics. The training save is the one thread-pool write
(`docs/TRAINING_LOOP.md` → Save).
