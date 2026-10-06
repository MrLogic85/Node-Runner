# Code Design Principles

The implementation rules we hold ourselves to, including how we write
comments and docs.

## 1. Clarity beats cleverness

This is a teaching tool: a student must be able to read the code and
*learn* from it.

- Prefer a straightforward loop over a chained LINQ one-liner
- Name variables for meaning (`fitness`, `mutationRate`) not brevity (`f`, `m`)
- If a piece of ML math looks like Greek, add a one-line comment stating **what
  it does**, and a link/reference for **why**
- Do not "optimize" until a profiler says you must

## 2. Godot first

Godot is a full engine. Using it to its full potential is the default, not
an option.

- Before writing anything, find what Godot already provides (nodes,
  resources, servers, project settings, editor features) and use it fully,
  even if that means learning a new part of the engine, such as physics
  (`RigidBody2D` and joints), theme type variations, or `TranslationServer`
  for text (#682).
- Write our own only when Godot cannot do what we need. Say why in the code
  or the owning doc, naming what was checked. "Ours is simpler" or "we
  didn't know" is not a reason.
- A wrapper that only renames or re-routes a Godot feature is still our own
  solution. Extend the Godot type (subclass, theme variation, resource)
  instead of replacing it.
- The engine-independent `libs/` (§3) cannot call Godot. That does not
  license re-implementing a Godot feature there: hand the Godot side the
  data it needs (values, keys, counts) and let Godot do the work. Logic
  Godot does not offer, such as the ML engine and the domain rules, stays
  ours.
- When existing code turns out to duplicate a Godot feature, file an issue
  to replace it.

## 3. The ML engine is engine-agnostic

Nothing in `libs/` references Godot (`docs/ARCHITECTURE.md`; the arch
tests enforce it). The ML engine's own rules are in
`libs/NodeRunner.ML/AGENTS.md`.

Why: it forces a clean interface, lets us test on the CLI, and means the ML
code could be lifted into another project without rewriting.

The same holds for the creature model in `libs/NodeRunner.Domain/` and
`libs/NodeRunner.Mechanics/` (#452): a part is described as if another
physics engine could run it. Godot body and joint choices live only in
`project/src/creature/`, with a comment saying why.

## 4. Determinism by default

- All randomness flows through one seeded `System.Random` from
  `RngProvider` (`project/src/managers/RngProvider.cs`), passed to whoever
  needs it; never an ad-hoc `new Random()`.
- `System.Random` with a seed gives the same sequence on every .NET 5+
  platform, so a run reproduces on desktop and Android without a custom
  PRNG.
- The run's seed is written to the log and never shown in the app (#959).
- Physics and network updates run on the fixed 60 Hz timestep
  (`_PhysicsProcess`), never `_Process`.
- No wall-clock (`DateTime.Now`) decisions in ML or training code.

Why: a seeded run reproduces in a test, so bugs and regressions are
detectable.

## 5. Small, composable units

- A file should do one thing. Aim for about 300 lines; past that, check
  whether it still does one thing and split it if not.
- **Soft limit, 1000 lines.** A production file past 1000 lines gets reviewed:
  split out functionality, or the change says why it stays whole. Code review
  flags it.
- **Hard limit, 2000 lines.** No `.cs` file under `libs/` or `project/src/`
  may exceed it. `NodeRunner.Arch.Tests` fails the build.
- Test files have no size limit; see `tests/AGENTS.md`.
- Prefer composition over inheritance. A creature *has* nodes, beams, sensors
  and a brain — it doesn't *inherit* from any of them.
- No abstract base classes "just in case". Introduce them when the second
  concrete case appears, not before.

## 6. Data before behavior

- Represent things as data first, then add behavior: a creature is Domain
  records such as `NodeDef`, `BeamDef` and `SensorDef`.
- This makes serialization, diffing and hashing trivial.
- Godot nodes are built *from* these defs; they don't replace them.

## 7. Explicit units

- Angles: **radians**. Always. Convert at the UI boundary.
- Time: **seconds** (double).
- Force / torque / distance: SI, or if we invent our own, document it once
  in `GLOSSARY.md`.
- Never a bare `double angle` — call it `angleRad`.

## 8. Fail loud in dev, gracefully in prod

- Use `Debug.Assert` liberally in dev builds for invariants
  (`Debug.Assert(weights.Length == expected)`).
- Release builds should clamp/log rather than crash the app.
- Never `catch (Exception) { }`. Ever.

## 9. Comments and docs

Comment intent, not mechanics:

```csharp
// BAD: increments the counter
generation++;

// GOOD: we count generations from 1 so the UI matches user intuition
generation++;
```

Assume the reader knows C#. Do not assume they know why *this* algorithm cares
about *that* invariant.

Docs and comments describe what is true now and why. Pending work, such as
TODOs, "move this when touched" lists and wished-for improvements, goes in a
GitHub issue; the doc or comment may link to it.

Documentation follows these rules:

- Each file has one responsibility.
- Nothing is written twice; other files link to the owner.
- One clear sentence beats two.
- Instructions that add nothing are removed: generic advice, restating what
  code, tests or CI enforce without a why, and history that no longer guides
  decisions.
- Layer-specific instructions live in the nearest `AGENTS.md`.

## 10. Testing

`docs/TEST_STRATEGY.md`.

## 11. Version control

`docs/REVIEW.md` → "PR title and commit hygiene".

## 12. Dependencies are a debt

- Every added NuGet package or Godot addon is future maintenance.
- Godot first (§2) applies here too: what Godot provides beats a package,
  an addon or our own version.
- Physics: Godot's `RigidBody2D` and joints, not Box2D.NET.
- ML: we write the algorithms ourselves (forward pass, GA, backprop), so
  they can be learned and shown. A library may speed them up if it stays
  deterministic for a seed and runs on Android (#790).

## 13. UI is the last mile

- Never let a beautiful UI hide a broken simulation. Build sim-first, UI on top.
- All state the UI shows must be readable from the sim, not the other way
  around. Sim doesn't know the UI exists.

---

## Style specifics (C#)

`.editorconfig` and `dotnet format` own formatting and naming. Beyond them:

- `readonly` everywhere it fits
- `sealed` by default on classes, unsealed only when subclassing is planned
- `record` for immutable data, `class` for identity and state
- No `#region`; a file that needs regions is too big

## Anti-patterns we explicitly reject

- Singletons that hold mutable game state (Godot autoloads for pure services
  are fine)
- Reflection-based "magic" wiring
- God objects (`GameManager` that knows everything)
- Deep inheritance chains
- Async/await where a simple coroutine or per-frame update would do
- Premature ECS / DOTS-style architecture
- Re-implementing something Godot already provides (§2)

## Chosen tooling

Changing a choice needs an issue. Test tools are in
`docs/TEST_STRATEGY.md` → "Tools"; target frameworks in
`docs/ARCHITECTURE.md`.

| Concern | Choice | Why |
|---|---|---|
| Solution format | **`.slnx`** (XML) | .NET 10 default; readable diffs, no GUIDs |
| Package versioning | **Central Package Management** (`Directory.Packages.props`) | One source of truth for versions. The Godot csproj opts out (SDK conflicts). |
| Code style | **`.editorconfig` + `dotnet format`** | Editor-agnostic |
| DI | **Hand-rolled constructor injection**, autoloads as composition root | |

- `TreatWarningsAsErrors=true` in `Directory.Build.props`, but off in
  `project/NodeRunner.csproj` because Godot's source generators emit code we
  don't own.
- `GenerateDocumentationFile` is on so `IDE0005` (unused usings) fails the
  build. We suppress the `CS1591` (missing XML docs) it triggers with
  `NoWarn`.
