# Architecture

High-level map of where things live and how they talk. Details change as we
build; the *shape* below should stay stable.

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
│                    Domain data (pure C#)                         │
│                    libs/NodeRunner.Domain/                       │
│           CreatureDef · NodeDef · BeamDef · SensorDef · Vector2D          │
└─────────────────────────────────────────────────────────────────┘
                                           │
                                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                      ML engine (pure C#)                         │
│                       libs/NodeRunner.ML/                        │
│    NeuralNetwork · activations · ga · backprop · math            │
└─────────────────────────────────────────────────────────────────┘
```

The three libraries in `libs/` are pure .NET 8 class libraries with **no
`Godot.*` references**. The Godot project (`project/`) targets .NET 9 for
Godot 4.7 Android export templates, references the libraries, and provides the
runtime host: scenes, physics, input, rendering.

Enforcement: `tests/NodeRunner.Arch.Tests/ArchitectureSpec.cs` fails the build
if any lib imports `Godot`, or if the layer graph below is violated.

## Dependency rules

Arrows only go **downward** across layer boundaries.

- **UI** (`project/src/ui/`) depends on: `NodeRunner.App` (ViewModels),
  `NodeRunner.Domain` (for display types), `project/src/ui/lib` (Controls).
  **Never** on: sim, managers, `NodeRunner.ML`.
- **App** (`libs/NodeRunner.App/` — ViewModels, Repositories, Services)
  depends on: `NodeRunner.Domain`, `NodeRunner.ML`.
  **Never** on: Godot, sim, creature.
- **Sim & Creature** (`project/src/{sim,creature}/`) depend on:
  `NodeRunner.Domain`, `NodeRunner.ML`, and Godot.
  **Never** on: UI, ViewModels, Managers.
- **Managers** (`project/src/managers/`) depend on: `NodeRunner.App` (for
  repository/service interfaces), `NodeRunner.Domain`, Godot.
  Managers are the **composition root** — they wire concrete implementations
  into ViewModels at startup.
- **Scene roots** (the `*Host` scenes: scripts and helpers in
  `project/src/hosts/`, scenes in `project/scenes/hosts/`) may depend on
  every project layer above. They wire a screen's signals to managers and
  the navigator; keep game rules out of them. Nothing depends on them.
- **Domain** (`libs/NodeRunner.Domain/`) depends on: nothing but the .NET BCL.
- **ML** (`libs/NodeRunner.ML/`) depends on: `NodeRunner.Domain` only.

Every source folder has an `AGENTS.md` with its specific rules. Read those
before editing.

## Solution layout

```
Node Runner/
├── NodeRunner.slnx                 # solution (new .NET 10 XML format)
├── Directory.Build.props           # shared C# settings
├── Directory.Packages.props        # central package versions (CPM)
├── .editorconfig                   # style rules
├── libs/                           # pure C#, no Godot
│   ├── NodeRunner.Domain/          # data records, enums, invariants
│   ├── NodeRunner.ML/              # neural nets, GA, backprop
│   └── NodeRunner.App/             # viewmodels, services, repositories
├── project/                        # Godot project (targets net9.0)
│   ├── project.godot
│   ├── NodeRunner.csproj           # references the three libs
│   ├── NodeRunner.sln              # classic .sln required by Godot .NET export
│   ├── scenes/                     # hosts/ (screen hosts), screens/, ui/, widgets/, tools/
│   └── src/
│       ├── creature/               # Godot Nodes for creatures
│       ├── sim/                    # simulation orchestration
│       ├── hosts/                  # routed scene roots that wire screens to managers
│       ├── managers/               # service autoloads / composition root
│       ├── theme/                  # arena (world) visuals, not UI styling
│       ├── tools/                  # editor/CLI tools; their scenes are not exported
│       └── ui/
│           ├── lib/                # reusable Controls
│           ├── screens/            # full-screen scenes
│           └── widgets/            # app-specific composite widgets
└── tests/                          # xUnit — libs plus static UI contracts
    ├── NodeRunner.Domain.Tests/
    ├── NodeRunner.ML.Tests/
    ├── NodeRunner.App.Tests/
    ├── NodeRunner.Arch.Tests/      # layer rules and source conventions
    └── NodeRunner.Ui.Tests/        # static Godot UI contracts; no scene tree
```

Static UI token/style contracts run in xUnit by referencing the Godot project
without constructing Nodes. Godot-side behavioral tests (Node lifecycle,
physics, input, rendered layout) will land later in
`project/tests/` using GdUnit4 — a separate framework with its own lifecycle,
kept out of the pure-C# solution.

## Android export

`project/export_presets.cfg` defines the `Android` debug export preset for the
0.1.0 APK:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot \
  --headless --path project \
  --export-debug Android ../build/node-runner-0.1.0-debug.apk
```

Local prerequisites are Godot 4.7.2 Mono export templates, JDK 21, Android SDK
platform/build-tools, platform-tools, and a user-local debug keystore configured
in Godot editor settings. The committed preset contains values only; keystore
paths/passwords stay in user-local Godot settings or ignored credential files.

For the non-Gradle debug export, Godot 4.7.2 currently emits min SDK 24 and
target/compile SDK 36 from its Android template. Do not override min/target SDK
in `export_presets.cfg` unless Gradle export is enabled in a later issue.

The project uses Godot's Compatibility/OpenGL renderer by default because the
Mobile/Vulkan renderer previously crashed in Godot's Android `VkThread` on the
SM-S938B test device. Issue #104 revisited Mobile/Vulkan on 2026-09-20 with
Godot 4.7.2 on a Samsung SM-S911B (Android 15 / API 35): a debug APK exported,
installed, and launched without `FATAL EXCEPTION`, `SIGSEGV`, `VkThread`, or
ANR logcat signals in a short smoke test. Keep Compatibility/OpenGL as the
default until Mobile/Vulkan has a longer stability/performance pass showing a
clear benefit on target devices.

## Key data types (informal)

```csharp
// libs/NodeRunner.ML/
public sealed class NeuralNetwork
{
    public int[] LayerSizes { get; }
    public double[][] Weights { get; }        // weights[layer] row-major
    public double[][] Biases  { get; }
    public Activation Activation { get; }

    public double[] Forward(double[] input);
    public void Forward(double[] input, double[] output);
    public void Forward(double[] input, double[] output, double[] scratchA, double[] scratchB);
    public NeuralNetwork Clone();
    public double[] FlattenGenome();
    public static NeuralNetwork FromGenome(int[] layers, double[] genome, Activation act);
}

// libs/NodeRunner.ML/Ga/
public sealed class GeneticAlgorithm
{
    public GeneticAlgorithm(
        int tournamentSize,
        double mutationRate,
        double mutationStrength,
        int elitismCount = 1,
        CrossoverStrategy crossoverStrategy = CrossoverStrategy.Uniform);

    public double[][] NextGeneration(double[][] genomes, double[] fitness, Random rng);
}

// libs/NodeRunner.Domain/
public sealed record NodeDef(int Id, Vector2D Position, double Radius, string? Name = null);
public sealed record BeamDef(int Id, int NodeA, int NodeB, string? Name = null);   // node ids
public sealed record SensorDef(int Id, int BeamId, SensorKind Kind, string? Name = null, double? Aim = null); // beam id; Aim: Camera only
public sealed record CreatureDef(NodeDef[] Nodes, BeamDef[] Beams, SensorDef[] Sensors, int NextPartId);
public sealed record NodeConnectionDef(int NodeIndex, int ReferenceBeamIndex, int OtherBeamIndex, bool IsMotorized);

public static class MotorTopology
{
    public static IReadOnlyList<NodeConnectionDef> BuildNodeConnections(CreatureDef creature);
}

public static class Accelerometer   // proof mass on a damped spring, pure math
{
    public static ProofMass Step(ProofMass state, Vector2D specificForceG, double dt);
    public static Vector2D Reading(ProofMass state);
    public static Vector2D SpecificForce(Vector2D acceleration, double gravity);
}

public static class CameraRays      // the camera's three rays around its aim, pure math
{
    public static double DefaultAim(Vector2D nodeA, Vector2D nodeB);
    public static double AimAlong(double worldAngle, Vector2D nodeA, Vector2D nodeB);
    public static Vector2D LocalRayTarget(int ray, double aim);
    public static double Reading(double? hitDistance);
}

public static class SensorPicture   // a sensor picture's tap area at its beam's middle, sized per kind
{
    public static double SizeOf(SensorKind kind);
    public static bool Contains(SensorKind kind, Vector2D point, Vector2D nodeA, Vector2D nodeB);
}
```

Note: `Vector2D` in `NodeRunner.Domain` is our own `readonly record struct`,
**not** `Godot.Vector2`. The creature layer converts at its boundary.

See `docs/CREATURE_MODEL.md` for the full Node/Beam/Sensor/motor-relation
model these types encode — including how motor relations are derived from a
`CreatureDef`'s topology, and why sensors sit on beams and are not the neural
model.

## The tick

At 60 Hz (`_physics_process`), for the creature currently under evaluation:

1. **Sense.** Each sensor part reads its values in part order (an
   accelerometer steps its proof mass and reads 2, along and across its
   beam; a camera reads its 3 rays' nearness); each motor relation reads 2 (relative angle, relative angular velocity) →
   `double[]`, in the fixed order documented in `docs/CREATURE_MODEL.md`.
2. **Think.** `Brain.Forward(input, output, scratchA, scratchB)` writes a
   target angular velocity in `[-1, 1]` per motor relation, without
   per-tick allocations.
3. **Act.** `MotorRelation.Drive(target)` scales the target by a static
   `MaxAngularVelocity` and drives torque (capped at a static `MaxTorque`)
   to chase it.
4. **Score.** `TrialMeasurement` records distance, top speed and elevation
   for this trial; distance is the fitness.

After N ticks (say 600 = 10 s at 60 Hz) each slot's trial ends. `Evolver`
records its fitness, assigns the slot the next pending genome, and, once every
genome in the current generation has completed, produces the next generation
via `GeneticAlgorithm.NextGeneration(...)`. The first slot reuses the visible
creature; up to 15 hidden clones run alongside it. Each slot owns a
`TrialController`, resets independently between trials, and uses an isolated
collision layer. See `docs/TRAINING_LOOP.md` for the full design.

`Evolver` raises `GenerationCompleted`/`NewBestFound` events; the Training
scene's root, `TrainingHost`, saves the training after each finished
generation, and `NewBestFound` reaches `TrainingPresentationViewModel`
through `EvolverTrainingProgressSource`. Training unlocks nothing (#557). The
Training screen's caption follows `TrainingPresentationViewModel`, the
SignalFlow stages are polled every ~0.15s, and Pause and Speed arrive as screen
signals. A dedicated `PopulationViewModel`
in the App layer remains a possible later refactor if this logic outgrows
`TrainingHost` — not required yet.

For 0.2.0 the hardcoded creature keeps its beam bodies awake (`CanSleep =
false`). Random brains produce visible, if uncoordinated, motor-relation
movement without any twitch-hack overlay — the old 0.1.0 CPG/twitch blend was
tied to the retired Muscle model and does not carry over.

## Navigation

Screens are moving to one scene each, where navigating replaces the current
scene (#326): a left scene is closed, not paused, and Back rebuilds it from
its route. #468 is routing them one by one. Creations (the root and the
main scene), Examples, Build, Training and the component-library pages are
routed scenes. Build (`BuildRoute`, #363) edits one saved creation and saves
each edit as it settles and before it is left (#368); + New saves an empty
creation first and opens it with `IsNew` (see `docs/BUILD_MODE.md`
for when Build removes it again). Its layout
is authored in `BuildScreen.tscn` (#364): the Build canvas is a
`Node2D` inside the screen's clipped canvas slot, placed and scaled in the
scene, so Build has no camera and taps reach the canvas through the UI. Training
(`TrainingRoute`, #469) trains one saved creation: it
builds the creature and the `Evolver` from the creation's save, resumes from its
last finished generation and saves each finished one, so leaving drops only
the generation in progress. Its layout is authored in `TrainingScreen.tscn`
(#386); the physics world is authored in `TrainingHost.tscn` inside the screen's
`UiWorldView`, a `SubViewport` with its own camera, so UI scale never changes
physics distances.

A screen stays in `ui/screens/` and knows nothing of saves or the router's
type: it emits signals. The routed scene that holds it is a small host in
`project/src/hosts/` (`CreationsHost`, `ExamplesHost`, `BuildHost`, `TrainingHost`) that wires
those signals to `SaveManager` and the navigator. The standalone gallery
pages have nothing to save, so they are routed directly.

- `SceneRoute` is one sealed record per scene. The record type is the scene;
  its properties are the plain arguments it is built from (a creation id, an
  achievement id). Every route is declared in `libs/NodeRunner.App/Navigation/`,
  because the test that keeps routes to plain values scans only that assembly.
- `SceneNavigation` opens a route with two choices: **keep current** (does
  the scene navigated from stay in the history) and **launch mode**
  (*unique* drops earlier entries of the same scene, *stacked* pushes on top).
- `SceneBackStack` holds the history. Its root (Creations) is never
  removed; opening the root's scene returns to it. `Back()` returns the
  previous route, or null on the root, where Android leaves the app.
  `ReturnToRoot()` clears everything above the root (e.g. after Delete).
  `ReplaceCurrent()` gives the current entry new arguments without reopening
  it: Start training on a new creation drops `BuildRoute.IsNew`, so Back from
  training rebuilds it as an ordinary creation that is kept even if emptied.
- `ISceneNavigator` is what a scene asks to navigate; `IRoutedScene` is how
  a scene receives its route and navigator before it joins the tree. Both
  live in App so UI scenes need not know the manager.
- The `SceneRouter` autoload (`project/src/managers/`) implements
  `ISceneNavigator` over the history and is the only code that changes scenes.
  Godot opens the main scene (Creations) itself at startup, so its host
  takes the router from the autoload instead of `Enter`.
- Android Back and Escape: a routed screen adds a `UiBackHandler`
  (`ui/lib`) in front of its children. While it is in the tree Back does
  not quit; it asks the screen to go back unless an open menu or dialog in
  the screen takes Back first. On the root nothing holds Back, so Android
  leaves the app.
  Build and Training hold Back the same way: an open dialog, sheet or menu
  closes first, then the scene goes one step back (#474). Training has a
  Back button in its top bar and no Build/Simulate mode switch.

Because a scene is rebuilt from its route, anything the player expects to
find again is saved before the scene closes. What must outlive a scene
change lives outside the scenes: notifications are queued on the
`Notifications` autoload (`UiNotificationLayer`, #472). The `BackPress`
autoload (`UiBackPress`, #506) follows the Back key across scenes so one
Android Back press acts once. The `SafeArea` autoload (`UiSafeArea`, #513)
keeps one set of display-cutout insets for every screen's frame. The
`UiScale` autoload (#299) holds the UI size and applies it to the root
window. All four live in `ui/lib`, not `managers/`, because managers hold no UI.

## Threading

- Single-threaded for v1. Godot's physics runs on one thread, but `Evolver`
  evaluates up to 16 candidates concurrently in one scene using the
  collision-isolated slots specified in `docs/TRAINING_LOOP.md`. This is
  parallel evaluation, not multithreaded physics.
- If profiling later shows the need for more throughput, brains can be
  forward-passed off the main thread since they are pure functions on
  `double[]`. Physics remains on Godot's thread.

## Save format

`docs/SAVE_FORMAT.md` owns the files, their layout and every saved field.

## Neural-network genome layout

Neural-network genomes are flattened per layer transition: weights in
row-major output-neuron order, then biases for that layer. 0.1.0 networks use
the configured activation for hidden layers and `Tanh` for the output layer so
motor-relation targets stay in `[-1, 1]`. Hot paths use the overload that
accepts caller-owned output and scratch buffers; those buffers must be
distinct arrays. The network itself does not keep per-call scratch state.

## Open questions

Open design questions are tracked as GitHub Issues rather than listed here,
so they get labels, milestones, and a closing decision instead of going
stale in prose. Of the three questions previously recorded in this section:
the large-network-visualization question belongs to the 0.16.0 brain views
(#196, #197, #393); the sensor-configurability question is decided on issue #107 (sensors are
configured through their own part settings, the camera first in #578);
and the ViewModel-base question is resolved: `INotifyPropertyChanged` per
`libs/NodeRunner.App/AGENTS.md`.
