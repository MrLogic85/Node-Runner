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
│           CreatureDef · NodeDef · BeamDef · CoreDef · Vector2D            │
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
│   ├── scenes/
│   └── src/
│       ├── creature/               # Godot Nodes for creatures
│       ├── sim/                    # simulation orchestration
│       ├── managers/               # autoloads / composition root
│       └── ui/
│           ├── lib/                # reusable Controls
│           ├── screens/            # full-screen scenes
│           └── widgets/            # app-specific composite widgets
└── tests/                          # xUnit — libs plus static UI contracts
    ├── NodeRunner.Domain.Tests/
    ├── NodeRunner.ML.Tests/
    ├── NodeRunner.App.Tests/
    ├── NodeRunner.Arch.Tests/      # NetArchTest layer rules
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
public sealed record NodeDef(Vector2D Position, double Radius);
public sealed record BeamDef(int NodeA, int NodeB);
public sealed record CoreDef(int NodeIndex);
public sealed record CreatureDef(NodeDef[] Nodes, BeamDef[] Beams, CoreDef[] Cores);
public sealed record NodeConnectionDef(int NodeIndex, int ReferenceBeamIndex, int OtherBeamIndex, bool IsMotorized);

public static class MotorTopology
{
    public static IReadOnlyList<NodeConnectionDef> BuildNodeConnections(CreatureDef creature);
}
```

Note: `Vector2D` in `NodeRunner.Domain` is our own `readonly record struct`,
**not** `Godot.Vector2`. The creature layer converts at its boundary.

See `docs/CREATURE_MODEL.md` for the full Node/Beam/Core/motor-relation model
these types encode — including how motor relations are derived from a
`CreatureDef`'s topology, and why a Core is a sensor package rather than the
neural model.

## The tick

At 60 Hz (`_physics_process`), for the creature currently under evaluation:

1. **Sense.** Each core reads 6 values (rays, pitch, elevation, speed); each
   motor relation reads 2 (relative angle, relative angular velocity) →
   `double[]`, in the fixed order documented in `docs/CREATURE_MODEL.md`.
2. **Think.** `Brain.Forward(input, output, scratchA, scratchB)` writes a
   target angular velocity in `[-1, 1]` per motor relation, without
   per-tick allocations.
3. **Act.** `MotorRelation.Drive(target)` scales the target by a static
   `MaxAngularVelocity` and drives torque (capped at a static `MaxTorque`)
   to chase it.
4. **Score.** `Evaluator` accumulates fitness for this trial.

After N ticks (say 600 = 10 s at 60 Hz) each slot's trial ends. `Evolver`
records its fitness, assigns the slot the next pending genome, and, once every
genome in the current generation has completed, produces the next generation
via `GeneticAlgorithm.NextGeneration(...)`. The first slot reuses the visible
creature; up to 15 hidden clones run alongside it. Each slot owns a
`TrialController`, resets independently between trials, and uses an isolated
collision layer. See `docs/TRAINING_LOOP.md` for the full design.

`Evolver` raises `GenerationCompleted`/`NewBestFound` events; `Main.cs`
subscribes to both, logs the former, and drives a training HUD panel
(generation/best/mean, run/pause/reset/time-scale controls) from them. A
dedicated `PopulationViewModel` in the App layer remains a possible later
refactor if this HUD logic outgrows `Main.cs` — not required yet.

For 0.2.0 the hardcoded creature keeps its beam bodies awake (`CanSleep =
false`). Random brains produce visible, if uncoordinated, motor-relation
movement without any twitch-hack overlay — the old 0.1.0 CPG/twitch blend was
tied to the retired Muscle model and does not carry over.

## Threading

- Single-threaded for v1. Godot's physics runs on one thread, but `Evolver`
  evaluates up to 16 candidates concurrently in one scene using the
  collision-isolated slots specified in `docs/TRAINING_LOOP.md`. This is
  parallel evaluation, not multithreaded physics.
- If profiling later shows the need for more throughput, brains can be
  forward-passed off the main thread since they are pure functions on
  `double[]`. Physics remains on Godot's thread.

## Save format (v1.5+)

Creatures and their trained brains save as JSON via `FileCreatureRepository`:

```json
{
  "def":   { "nodes": [...], "beams": [...], "cores": [...] },
  "brain": { "layers": [8, 12, 4], "genome": [...], "activation": "Tanh" },
  "meta":  { "seed": 4711, "generation": 137, "fitness": 42.7 }
}
```

Round-trip: `CreatureDef` + `NeuralNetwork` → JSON → same objects. Tested.

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
the large-network-visualization question is now tracked on issue #97
(0.9.0); the sensor-configurability question is now tracked on issue #107;
and the ViewModel-base question is resolved: `INotifyPropertyChanged` per
`libs/NodeRunner.App/AGENTS.md`.
