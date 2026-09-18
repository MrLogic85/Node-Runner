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
│           CreatureDef · JointDef · configs · Vector2D            │
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
└── tests/                          # xUnit — pure C# only
    ├── NodeRunner.Domain.Tests/
    ├── NodeRunner.ML.Tests/
    ├── NodeRunner.App.Tests/
    └── NodeRunner.Arch.Tests/      # NetArchTest layer rules
```

Godot-side tests (Node behaviour, physics, UI) will land later in
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

The project uses Godot's Compatibility/OpenGL renderer for 0.1.0 because the
Mobile/Vulkan renderer crashed in Godot's Android `VkThread` on the SM-S938B
test device. Revisit Vulkan only behind a separate compatibility issue.

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
    public double MutationRate { get; init; }
    public double MutationSigma { get; init; }
    public int TournamentSize { get; init; }

    public double[][] NextGeneration(double[][] genomes, double[] fitness, Random rng);
}

// libs/NodeRunner.Domain/
public sealed record JointDef(Vector2D Position, double Radius);
public sealed record BoneDef(int JointA, int JointB);
public sealed record MuscleDef(int JointA, int JointB, double RestLength, double MaxForce);
public sealed record CreatureDef(JointDef[] Joints, BoneDef[] Bones, MuscleDef[] Muscles);
```

Note: `Vector2D` in `NodeRunner.Domain` is our own `readonly record struct`,
**not** `Godot.Vector2`. The creature layer converts at its boundary.

## The tick

At 60 Hz (`_physics_process`), for each creature in the population:

1. **Sense.** `Sensors` reads an oscillator clock plus joint angles and angular
   velocities → `double[]`. 0.1.0 uses the clock as a simple central pattern
   input so a resting random brain still produces changing muscle targets;
   ground-contact sensors are added when topology-derived sensors land.
2. **Think.** `Brain.Forward(input, output, scratchA, scratchB)` writes muscle
   targets in `[-1, 1]` without per-tick allocations.
3. **Act.** `Muscle.ApplyTarget(target)` maps each target to a spring
   contraction/extension around the base rest length and a perpendicular bend
   force so the hardcoded worm visibly twitches.
4. **Score.** `Evaluator` accumulates fitness for this creature.

After N ticks (say 600 = 10 s at 60 Hz), the `Evolver` collects fitness scores
and produces the next generation via `GeneticAlgorithm.NextGeneration(...)`.
New brains are assigned; positions reset; loop continues.

The `Evolver` raises `GenerationCompleted` events which `PopulationViewModel`
subscribes to, which the UI in turn observes.

For 0.1.0 the hardcoded creature keeps its joint bodies awake (`CanSleep =
false`) and uses deliberately punchy muscle pulses. This is a demo constraint:
the goal is obvious visible twitching, not stable walking or plausible muscle
physiology yet.

## Threading

- Single-threaded for v1. Godot's physics runs on one thread; we run 20
  creatures in one scene using collision layers to isolate them.
- If we ever need more parallelism, brains can be forward-passed off the main
  thread since they're pure functions on `double[]` — but only after profiling
  shows we need it.

## Save format (v1.5+)

Creatures and their trained brains save as JSON via `FileCreatureRepository`:

```json
{
  "def":   { "joints": [...], "bones": [...], "muscles": [...] },
  "brain": { "layers": [8, 12, 4], "genome": [...], "activation": "Tanh" },
  "meta":  { "seed": 4711, "generation": 137, "fitness": 42.7 }
}
```

Round-trip: `CreatureDef` + `NeuralNetwork` → JSON → same objects. Tested.

Neural-network genomes are flattened per layer transition: weights in
row-major output-neuron order, then biases for that layer. 0.1.0 networks use
the configured activation for hidden layers and `Tanh` for the output layer so
muscle targets stay in `[-1, 1]`. Hot paths use the overload that accepts
caller-owned output and scratch buffers; those buffers must be distinct arrays.
The network itself does not keep per-call scratch state.

## Open questions (revisit before v2)

- Do we need our own RNG (Xoshiro/PCG) for cross-platform determinism, or is
  `System.Random` fine? → decide when we ship v1 on Android and check parity
  with desktop runs.
- How do we visualize very large networks without cluttering the screen?
  Group neurons? Collapse layers?
- Should sensors be user-configurable at draw time, or auto-derived from
  topology? Auto-derived for v1, revisit at v2.
- ViewModel base: raw C# events, `INotifyPropertyChanged`, or a small custom
  observable? Currently favoring `INotifyPropertyChanged` per
  `libs/NodeRunner.App/AGENTS.md`.
