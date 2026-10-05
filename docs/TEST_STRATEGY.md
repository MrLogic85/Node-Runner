# Test Strategy

How each layer of Node Runner is tested, with what tools, and why. Model
inspired by `kappuccino`'s `docs/TEST_STRATEGY.md`.

## Guiding principles

1. **Fast feedback.** The whole `dotnet test NodeRunner.slnx` run should
   finish in seconds, not minutes. Anything slow is a smell.
2. **Deterministic.** Every random source is a seeded `Random`. Never
   `DateTime.Now`.
3. **Layer boundaries are code.** Architecture rules live in
   `NodeRunner.Arch.Tests`, and UI ownership rules in `NodeRunner.Ui.Tests`,
   not in a wiki.
4. **Test the risky bits, not the trivia.** Compiler-checked things
   (record equality, enum switches) get one canary test at most.
5. **Fakes are production code.** In-memory implementations of repositories
   live next to the interfaces they satisfy, not in a test folder.

## Layer / tool / owner table

| Layer | Lives in | Runtime | Tooling | What's tested |
|---|---|---|---|---|
| Domain | `libs/NodeRunner.Domain/` | net8.0 | xUnit + Shouldly | Invariants, JSON round-trip, record semantics |
| Mechanics | `libs/NodeRunner.Mechanics/` | net8.0 | xUnit + Shouldly | Part physics on hand-checkable numbers |
| ML | `libs/NodeRunner.ML/` | net8.0 | xUnit + Shouldly | Forward pass, GA math, backprop, activation math |
| App | `libs/NodeRunner.App/` | net8.0 | xUnit + Shouldly + NSubstitute | View-models, repositories, service contracts |
| Architecture | `libs/`, `project/src/` | net10.0 tests | xUnit + NetArchTest | No Godot leaks, correct layer graph, file-size limit |
| Static UI contracts | `project/src/`, `project/scenes/`, theme files | net10.0 tests referencing Godot | xUnit + Shouldly + Roslyn | Theme files, component contracts, scene and source guards |
| Godot Nodes | `project/src/{creature,sim,managers,ui}/` | Godot runtime | **GdUnit4** (deferred, v1.0+) | Node lifecycle, physics scenarios |
| End-to-end | full app on device | Android | Manual, per-issue decision | Feel, latency, battery |

Tests target `net10.0` (only runtime installed locally). Libs target `net8.0`
(Godot's runtime). This works because `net10.0` can load `net8.0` assemblies.

## Tools — locked-in choices

| Tool | Version | Purpose |
|---|---|---|
| xUnit | 2.9 | Test framework |
| Shouldly | 4.2 | Assertions (`x.ShouldBe(...)`, `x.ShouldContain(...)`) |
| NSubstitute | 5.1 | Mocking (`Substitute.For<IFoo>()`) |
| NetArchTest.Rules | 1.3 | Architecture assertions |
| coverlet.collector | 6.0 | Coverage on every `dotnet test` |
| Microsoft.NET.Test.Sdk | 17.11 | Test host |

Versions are pinned via central package management in
`Directory.Packages.props`. To bump one, open an issue.

Global usings for `Xunit`, `Shouldly` are declared in each test csproj.

## Per-project focus

### `NodeRunner.Domain.Tests`

Cover:
- Constructor invariants (bad data throws with a helpful message)
- JSON round-trip: `Deserialize(Serialize(x)).ShouldBe(x)`
- Record equality holds (one canary test is enough — the compiler owns the
  rest)

Do **not** test getters/setters, `==`/`!=`, `.Equals` overrides on records.
The compiler synthesises them.

### `NodeRunner.Mechanics.Tests`

Cover each part's physics with numbers a person can check by hand: an
accelerometer at rest reads 1 g up, a Piston pushes toward its target and
never uses more than its chosen strength, and its force builds to full within its
Rise time. Tests for
types that stay in Domain stay in `NodeRunner.Domain.Tests`.

### `NodeRunner.ML.Tests`

Cover:
- Output shape correctness for a given `LayerSizes`
- Deterministic-with-same-seed: `nn1.Forward(x)` == `nn2.Forward(x)` when
  both were seeded identically
- Activation functions on hand-worked values (tanh(0) = 0, sigmoid(0) = 0.5)
- GA operators: tournament selection picks the fittest with size = pop,
  crossover mixes both parents, mutation stays within sigma bounds
  statistically
- Genome round-trip: `FromGenome(nn.LayerSizes, nn.FlattenGenome(), act)`
  produces an equivalent network
- Direct brain round-trip: `DirectBrain.Compile(DirectBrain.ToBrainDef(ports, genome, previous), ports)`
  gives the genome back, whatever the order of the saved neurons and genes
- Clone independence: mutating a clone doesn't touch the original

Target: >90% coverage on this project once it stabilises. This is the ML
engine. It has to be right.

### `NodeRunner.App.Tests`

Cover:
- View-model property-change notifications fire in the expected order
- Repositories: `FileCreatureRepository` against a temp directory
  (`Path.GetTempPath()` + `IDisposable` cleanup); `InMemoryCreatureRepository`
  as a lightweight fake
- Services: contract tests for each `IService` interface. Substitute
  dependencies with NSubstitute.
- No async-void, no swallowed exceptions

### `NodeRunner.Arch.Tests`

Executable rules. If someone adds `using Godot;` to `libs/NodeRunner.ML/`,
CI fails.

Current facts (see `ArchitectureSpec.cs`):
- No lib references `Godot.*`
- `NodeRunner.Domain` references nothing but the BCL
- `NodeRunner.Mechanics` references neither `NodeRunner.ML` nor `NodeRunner.App`,
  and `NodeRunner.Domain` does not reference it
- `NodeRunner.ML` references neither `NodeRunner.App` nor `NodeRunner.Mechanics`
- No production `.cs` file (`libs/`, `project/src/`) exceeds 2000 lines
  (`docs/CODE_DESIGN_PRINCIPLES.md` §5)
- Every Android export preset has `version/code` equal to 1000000·major +
  1000·minor + patch of `application/config/version`, and an empty
  `version/name` (#809)
- The `Android Debug` preset matches `Android` apart from its identity
  (`docs/ARCHITECTURE.md` → "Android export", #898)

Add a fact whenever a convention emerges that we've decided to enforce.

### `NodeRunner.Ui.Tests`

`docs/UI_DIRECTION.md` ("Who owns what") splits the UI between the UI
library, scenes and C#. These tests guard those boundaries and
run without the Godot scene tree. They cover five concerns.

**1. Values come from their owner.** A value is authored once, so a root Theme
swap restyles everything:

- theme files: every palette authors every token colour, and the derived items
  (text-colour variations, base control colours) match their palette
- typography is authored once, in the project theme, with a font and size for
  every variation
- the app icon's colour SVGs use only Neon palette colours and the
  monochrome layer only white, the launcher and splash icons copy the
  foreground art in order, and both export presets and the project icon point at
  them at their native sizes (`AppIconTests`, #820)
- saved scenes pin no stylebox, colour, font or font size on any node, store
  no generated icon texture, and do not store the properties a library
  component derives on its own node (`SceneDerivedStateTests`)
- C# source (`UiSourceGuardTests`, a Roslyn scan with types bound) has no colour
  literals anywhere in `project/src`; `project/src/ui/lib` pins no colour
  override (it selects a generated Theme variation) and names every
  number. Dimensions should come from `UiSize`/`UiLayout`/`UiSpacing`; the
  test checks that a number is named, not where the name points. Identity,
  halving and doubling stay inline; the test owns the exact list. No code in
  `project/src/ui` sets `ZIndex`; it orders drawing by the tree (#463). Code
  sets every `CanvasLayer` level from `UiLayers`, which keep their order, and a
  scene saves no level over the screen (`UiLayersTests`, #768). Outside it, a `ZIndex` names
  a `CreatureLayers` or `ArenaLayers` layer, and the layers keep their order
  (`DrawLayersTests`, #767). Only the shared part visuals in `theme/` call
  the helpers that paint a joint, Piston, Spring, sensor or selection mark, so a view
  shows its parts through them (`SharedPartVisualsTests`, #766, #769). Only the app's
  `UiNotificationLayer` (and Popup Gallery) creates a `UiNotification`, so
  notifications outlive scene changes (#472).
- UI text is translated once (`UiTextTranslationTests`, #682). Only
  `UiTextTranslation` takes a `UiText`, and it translates with
  `TranslationServer.Translate`/`TranslatePlural`, and `FormatNumber` for
  digits; plural selection and digits themselves are checked on device (#751,
  #756). The creation-name labels turn their own auto-translation off, and
  a `UiCalloutLayer` turns it off for its callouts (#758). Popup text is
  never put together in code, also through a host's own `Notify` wrapper;
  the galleries are left out (#773, rule in `docs/UI_DIRECTION.md`).
  `project/src` uses no .NET string casing; that text is translated before
  the TextServer cases it, in the language's own way, and redrawn when the
  language changes is checked on device (#776, #778). A `ui/lib` component
  that puts its own text into an inner control, by setting it, through a
  factory or helper, or into a nested component, also shares its translation context with that control
  (`UiTranslationContextTests`, #777); the translation it picks is checked
  on device. The committed translation template,
  `project/locale/messages.pot`, lists exactly the text the scenes and code
  show (`TranslationTemplateTests`, #778; regenerate it as
  `docs/LOCALIZATION.md` says).

**2. Screens reuse the library.** Every canonical component maps to one
reusable control, and paired specimens (slider and range, power and value
rows, every button) share that control instead of copying it
(`UiComponentContractsTests`). `UiComponentContracts` maps each component to
its `c_*` entry, and the test reads every `c_*` entry under
`reference design/`: each must be mapped or listed as having no component of
its own, and each mapped name must exist in the reference
([#315](https://github.com/MrLogic85/Node-Runner/issues/315)). Screens rewritten
under [#310](https://github.com/MrLogic85/Node-Runner/issues/310) are listed in
`RewrittenUi` and held to stricter guards: their scenes are built from library
components, widget scenes and plain layout containers, and every `%Name` their
script or its `project/src` base classes bind exists in the scene with a
matching type (`RewrittenSceneTests`). The guard reads literal
`GetNode<T>("%Name")` calls, so helpers take nodes, not paths. Their screen scripts declare no numbers, their
widget scripts name every number as the library does, and neither builds nor
restyles controls (`UiSourceGuardTests`). In widgets that draw
(`RewrittenUi.DrawnWidgets`), every literal inside `_Draw` and its `Draw*`
helpers skips the number rule: proportions, strokes, dash lengths, segment
counts and alphas stay inline where they are drawn. Numbers elsewhere in the
file (hit radii, timers, thresholds) are still named. A drawn widget may also
set the `Position` of scene-authored controls so they follow its drawing
(the selection handles, #366); sizes stay in the scene. Gallery pages join the lists like
product screens. A screen joins the lists when it is rewritten;
[#310](https://github.com/MrLogic85/Node-Runner/issues/310) tracks the rest as
child issues.

**3. Behaviour is tested apart from layout.** Rules and state live in
`NodeRunner.App` view-models (`NodeRunner.App.Tests`), and component
behaviour lives in pure contract functions in the library
(`UiComponentContracts`, slider values, progress), tested here. Neither
depends on how a scene arranges its nodes, so a layout can change in the
editor without breaking a test. Whether a scene's controls reach the right
view-model action is verified on device until Godot-side tests exist.

**4. Saved scenes survive export.** Export re-packs every scene, and
`PackedScene.Pack` drops nodes added inside an instance unless the scene marks
that instance `[editable]`. Such a scene still loads on desktop, so the bug
only shows on device. `SceneEditableChildrenTests` requires the marker
([#333](https://github.com/MrLogic85/Node-Runner/issues/333)). A node added
inside an instance also disappears when the instanced scene is restructured
and its saved parent path no longer exists; Godot only warns when the scene
is instantiated. `SceneParentPathTests` resolves every saved parent path, and
the path of every override block, through the nodes the scene creates and,
recursively, its instanced scenes
([#407](https://github.com/MrLogic85/Node-Runner/issues/407)). It also
requires sibling nodes to have distinct names: Godot renames a duplicate
sibling on load, so the children saved under it lose their parent (#496).

**5. Every route opens its scene.** `SceneRouterTests` requires every App
route to map to an existing scene, and a route that carries arguments to open
a scene whose root script implements `IRoutedScene`, so the arguments reach it
([#468](https://github.com/MrLogic85/Node-Runner/issues/468)).

Do not write tests that lock a scene's arrangement: which components it uses,
their order and its layout sizes are free to change in the editor. Do not
instantiate Nodes or claim to prove rendering. Scene lifecycle, input, layout
and visual fidelity remain Godot/device verification concerns.

### Godot-side tests (deferred)

When we need to test a `Creature` node's physics response or a `Screen`'s
input handling, we add **GdUnit4** and put tests under `project/tests/`.
Separate runner (Godot editor invokes it), separate lifecycle. Don't force it
into `NodeRunner.slnx`.

We're deferring this to at least v1.0 — pre-v1, physics and UI are verified
manually when the issue/change needs it. `docs/MANUAL_TESTING.md` owns that
decision process and how agents can run or defer manual checks.

## Test file layout

Mirror the production layout inside each test project:

```
libs/NodeRunner.ML/
├── NeuralNetwork.cs
├── Activation.cs
└── Ga/
    └── TournamentSelection.cs

tests/NodeRunner.ML.Tests/
├── NeuralNetworkTests.cs
├── ActivationTests.cs
└── Ga/
    └── TournamentSelectionTests.cs
```

One test class per production class. Test files have no size limit, but they
follow the file they test: when a production file is split, split its tests
the same way, so each test class still mirrors one production class.

Test method names describe behaviour:

```csharp
[Fact]
public void Forward_WithZeroInput_ReturnsZeroesForTanh() { ... }

[Fact]
public void FromGenome_WithSizeMismatch_Throws() { ... }
```

AAA layout inside each test:

```csharp
[Fact]
public void Forward_WithZeroInput_ReturnsZeroesForTanh()
{
    int[] layers = [2, 3, 1];
    var nn = NeuralNetwork.FromGenome(layers, new double[NeuralNetwork.GenomeLength(layers)], Activation.Tanh);
    var input = new double[2];

    var output = nn.Forward(input);

    output.Length.ShouldBe(1);
    output[0].ShouldBe(0, tolerance: 1e-9);
}
```

## What we do NOT test

- Compiler-generated record members (`Equals`, `GetHashCode`, `ToString`)
- Trivial getters/setters
- .NET BCL behaviour
- Godot engine behaviour (that's Godot's job)
- A scene's arrangement: component choice, order and layout sizes belong to
  the scene
- Code shape (sealed/abstract, member kinds, base types) — test the values a
  contract maps to, or guard against the hardcoding it is meant to prevent

## Running the suite

```bash
dotnet test NodeRunner.slnx                                # all tests
dotnet test tests/NodeRunner.ML.Tests/NodeRunner.ML.Tests.csproj   # one project
dotnet test NodeRunner.slnx --collect:"XPlat Code Coverage"       # + coverage
```

CI runs the last form on every PR and every push to `main`. Coverage
artefacts are uploaded but not (yet) gated on a percentage — that comes when
the codebase has enough surface to make a threshold meaningful.
