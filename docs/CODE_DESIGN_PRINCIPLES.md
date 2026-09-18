# Code Design Principles

The rules we hold ourselves to. When in doubt, come back here.

## 1. Clarity beats cleverness

This is a teaching tool. A student — including future-you at 22:47 on a
Tuesday — must be able to read the code and *learn* from it.

- Prefer a straightforward loop over a chained LINQ one-liner
- Name variables for meaning (`fitness`, `mutationRate`) not brevity (`f`, `m`)
- If a piece of ML math looks like Greek, add a one-line comment stating **what
  it does**, and a link/reference for **why**
- Do not "optimize" until a profiler says you must

## 2. The ML engine is engine-agnostic

Anything under `libs/NodeRunner.ML/` must satisfy:

- No `using Godot;`
- No references to `Node`, `Vector2`, `Resource`, etc.
- Public API takes and returns primitive types or plain C# records: `double`,
  `double[]`, `int`, small structs
- Use `double`, not `float`, throughout the ML engine so training math is
  consistent and reproducible
- Fully unit-testable outside Godot with plain xUnit

Enforcement: `NodeRunner.Arch.Tests` fails the build if any `Godot.*` type
leaks into `libs/`.

Why: it forces a clean interface, it lets us test on the CLI, and it means the
ML code could be lifted into any other project (or a future desktop tool)
without rewriting.

## 3. Determinism by default

- All randomness flows through a single seeded `System.Random` (or explicit
  `Xoshiro`/PCG later) passed to whoever needs it
- The seed for a simulation run is displayed in the UI and written to the log
- Fixed timestep for physics *and* NN updates — never `_process(delta)` for
  anything training-related. Use `_physics_process` with 60 Hz.
- No `DateTime.Now`-based decisions in ML code

Why: reproducibility is worth more than you'd think. Bug reports become "run
seed 4711". Regressions become detectable.

## 4. Small, composable units

- A file should do one thing. If `Creature.cs` grows past ~300 lines, split it.
- Prefer composition over inheritance. A creature *has* a skeleton, muscles,
  sensors, and a brain — it doesn't *inherit* from any of them.
- No abstract base classes "just in case". Introduce them when the second
  concrete case appears, not before.

## 5. Data before behavior

- Represent things as data first, then add behavior:
  ```csharp
  public sealed record JointDef(Vector2 Position, double Radius);
  public sealed record BoneDef(int JointA, int JointB);
  public sealed record MuscleDef(int JointA, int JointB, double MaxForce);
  ```
- This makes serialization (save/load creatures), diffing (evolution!), and
  hashing (dedup) trivial.
- Godot nodes are built *from* these defs, they don't replace them.

## 6. Explicit units

- Angles: **radians**. Always. Convert at the UI boundary.
- Time: **seconds** (double).
- Force / torque / distance: SI, or if we invent our own, document it once
  in `GLOSSARY.md`.
- Never a bare `double angle` — call it `angleRad`.

## 7. Fail loud in dev, gracefully in prod

- Use `Debug.Assert` liberally in dev builds for invariants
  (`Debug.Assert(weights.Length == expected)`).
- Release builds should clamp/log rather than crash the app.
- Never `catch (Exception) { }`. Ever.

## 8. Comment intent, not mechanics

```csharp
// BAD: increments the counter
generation++;

// GOOD: we count generations from 1 so the UI matches user intuition
generation++;
```

Assume the reader knows C#. Do not assume they know why *this* algorithm cares
about *that* invariant.

## 9. Testing philosophy

- **Unit tests** for `libs/NodeRunner.ML/` and `libs/NodeRunner.Domain/` —
  mandatory. Small, fast, no Godot.
- **Architecture tests** (`NodeRunner.Arch.Tests`) — enforce layer rules that
  the compiler can't. Add a fact whenever a new convention emerges.
- **Property tests** where cheap (a network's output shape equals the output
  layer size for any random input).
- **Manual tests** for creature physics and UI — write a checklist in the
  issue.
- We don't chase 100% coverage. We chase "the tricky parts are pinned down".

See `docs/TEST_STRATEGY.md` for the full tooling table and per-layer detail.

## 10. Version-control hygiene

- One logical change per commit.
- Commit messages: imperative present ("Add tournament selection"), reference
  the issue file (`(#0007)`).
- Do **not** commit generated files: `.godot/`, `.mono/`, `bin/`, `obj/`,
  `*.import` for imported assets is fine but check on a case-by-case basis.
- Full commit/push workflow (code-review gate, DoD, PR process) lives in
  root `AGENTS.md` § "Prime directives" and `docs/REVIEW.md`.

## 11. Dependencies are a debt

- Every added NuGet package or Godot addon is future maintenance.
- No ML libraries. We're building this to learn.
- Physics: use Godot's built-in `RigidBody2D` + joints. Don't pull Box2D.NET.
- Math: `System`, `System.Numerics`. If we need more, we implement it in
  `libs/NodeRunner.ML/Math/`.

## 12. UI is the last mile

- Never let a beautiful UI hide a broken simulation. Build sim-first, UI on top.
- All state the UI shows must be readable from the sim, not the other way
  around. Sim doesn't know the UI exists.

---

## Style specifics (C#)

- Braces on new line (default Godot C# style)
- `PascalCase` for public/protected, `camelCase` for locals & params,
  `_camelCase` for private fields
- `readonly` everywhere it fits
- `sealed` by default on classes, unfrozen only when subclassing is planned
- Prefer `record` for immutable data, `class` for identity/state
- `var` when the type is obvious from the right-hand side; explicit otherwise
- No `#region`. If a file needs regions, it's too big.

## Anti-patterns we explicitly reject

- Singletons that hold mutable game state (Godot autoloads for pure services
  are fine)
- Reflection-based "magic" wiring
- God objects (`GameManager` that knows everything)
- Deep inheritance chains
- Async/await where a simple coroutine or per-frame update would do
- Premature ECS / DOTS-style architecture

## Chosen tooling

Locked in — do not swap without an issue.

| Concern | Choice | Why |
|---|---|---|
| Test framework | **xUnit 2.9** | Standard, mature, fast |
| Assertions | **Shouldly** | Apache 2.0. FluentAssertions changed licence at v8; we avoid that entire debate. |
| Mocking | **NSubstitute 5** | Cleaner API than Moq, no legal drama |
| Architecture rules | **NetArchTest.Rules** | Reflection over assembly markers, keeps layer rules executable |
| Coverage | **Coverlet** | Default; already wired to `dotnet test` |
| Solution format | **`.slnx`** (XML) | .NET 10 default; readable diffs, no GUIDs |
| Package versioning | **Central Package Management** (`Directory.Packages.props`) | One source of truth for lib versions. Godot csproj opts out (SDK conflicts). |
| Namespaces | **File-scoped** | Enforced by `.editorconfig` |
| Code style | **`.editorconfig` + `dotnet format`** | Editor-agnostic |
| DI | **Hand-rolled constructor injection**, autoloads as composition root | Can migrate to Chickensoft.AutoInject later without touching classes |

Notes / quirks:

- `TreatWarningsAsErrors=true` in `Directory.Build.props`, but disabled in
  `project/NodeRunner.csproj` because Godot's source generators emit code we
  don't own.
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is required
  for `IDE0005` (unused usings) to actually fail the build. This triggers
  `CS1591` (missing XML docs); we suppress that with `NoWarn` until the API
  stabilises.
- Test projects target `net10.0` because that's the only runtime installed
  locally; library projects target `net8.0` because that's what Godot's
  runtime ships. `net10.0` tests load `net8.0` libs cleanly.
