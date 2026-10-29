# Code Design Principles

The implementation rules we hold ourselves to, including how we write
comments and docs.

## 1. Clarity beats cleverness

This is a teaching tool: a student must be able to read the code and
*learn* from it.

- Prefer a straightforward loop over a chained LINQ one-liner.
- Name variables for meaning (`fitness`, `mutationRate`), not brevity.
- ML math that looks like Greek gets a one-line comment saying **what**
  it does, and a reference for **why**.
- Do not optimize until a profiler says you must.

## 2. Godot first

Using Godot to its full potential is the default, not an option.

- Before writing anything, find what Godot already provides (nodes,
  resources, servers, project settings, editor features) and use it
  fully, even if that means learning a new part of the engine, such as
  `RigidBody2D` and joints, theme type variations, or `TranslationServer`.
- Write our own only when Godot cannot do what we need, and say why in
  the code or the owning doc, naming what was checked. "Ours is simpler"
  or "we didn't know" is not a reason.
- A wrapper that only renames or re-routes a Godot feature is still our
  own solution. Extend the Godot type (subclass, theme variation,
  resource) instead.
- The engine-independent `libs/` (§3) cannot call Godot, but that does
  not license re-implementing a Godot feature there: hand the Godot side
  the data it needs and let Godot do the work. Logic Godot does not
  offer, such as the ML engine and the domain rules, stays ours.
- Existing code that duplicates a Godot feature gets an issue to replace
  it.

## 3. The ML engine is engine-agnostic

Nothing in `libs/` references Godot (`docs/ARCHITECTURE.md`); the arch
tests enforce it. This forces a clean interface, lets us test on the CLI,
and lets the ML code be lifted into another project. The ML engine's own
rules are in `.claude/rules/ml.md`.

The creature model in `libs/NodeRunner.Domain/` and
`libs/NodeRunner.Mechanics/` is described as if another physics engine
could run it. Godot body and joint choices live only in
`project/src/creature/`, with a comment saying why.

## 4. Determinism by default

A seeded run reproduces in a test, so bugs and regressions are
detectable.

- All randomness flows through one seeded `System.Random` from
  `RngProvider` (`project/src/managers/RngProvider.cs`), passed to whoever
  needs it; never an ad-hoc `new Random()`. A seeded `System.Random`
  gives the same sequence on every .NET platform, so no custom PRNG is
  needed.
- The run's seed is written to the log, not shown in the app.
- Physics and network updates run on the fixed 60 Hz timestep
  (`_PhysicsProcess`), never `_Process`.
- No wall-clock (`DateTime.Now`) decisions in ML or training code.

## 5. Small, composable units

- A file does one thing. Aim for about 300 lines; past that, check
  whether it still does one thing.
- **Soft limit, 1000 lines.** A production file past it is split, or the
  change says why it stays whole.
- **Hard limit, 2000 lines** for a `.cs` file under `libs/` or
  `project/src/`; `NodeRunner.Arch.Tests` enforces it. Test files have no
  limit.
- Prefer composition over inheritance: a creature *has* nodes, beams,
  sensors and a network.
- No abstract base class until the second concrete case appears.

## 6. Data before behavior

A creature is Domain records such as `NodeDef`, `BeamDef` and
`SensorDef` first, which makes serialization, diffing and hashing
trivial. Godot nodes are built *from* these defs; they don't replace
them.

## 7. Explicit units

- Angles: **radians**, converted at the UI boundary. Never a bare
  `double angle`; call it `angleRad`.
- Time: **seconds** (double).
- Force, torque and distance: SI, or our own unit documented once in
  `docs/GLOSSARY.md`.

## 8. Fail loud in dev, gracefully in prod

- Use `Debug.Assert` liberally for invariants
  (`Debug.Assert(weights.Length == expected)`).
- Release builds clamp or log rather than crash.
- Never `catch (Exception) { }`.

## 9. Comments and docs

Comment intent, not mechanics. Assume the reader knows C#, not why
*this* algorithm cares about *that* invariant:

```csharp
// BAD: increments the counter
generation++;

// GOOD: we count generations from 1 so the UI matches user intuition
generation++;
```

- Prefer clearer code, such as a better name or a smaller method, over a
  comment that explains it.
- Say what something is for, not where it is used; callers change.
- Describe what is true now and why. No history ("previously", "no
  longer", "after the refactor"), and no issue numbers except a link to
  an open issue for pending work. Pending work itself, such as TODOs and
  wished-for improvements, goes in a GitHub issue.
- Doc comments (`///`) go only on public API and subtle internals, with
  `<param>` and `<returns>` only where they add to the summary.

Ask four questions of every text you write, change or make wrong:

1. **What does it add; is it needed?** Delete text no reader acts on:
   generic advice, a restatement without a why of what code, tests or CI
   enforce, and history that no longer guides a decision. A change
   removes what it makes unneeded, not only adds.
2. **Is it right and clear?** One clear sentence beats two.
3. **Is it said somewhere else?** Nothing is written twice; other files
   link to the owner, which `README.md` → "Documentation" names.
4. **Is it in the right place?** Each file has one responsibility, as
   `docs/REVIEW.md` → "Agent, rule and skill files" lists.

## 10. Testing

`docs/TEST_STRATEGY.md`.

## 11. Version control

`docs/REVIEW.md` → "PR title and commit hygiene".

## 12. Dependencies are a debt

Every NuGet package or Godot addon is future maintenance, and Godot (§2)
beats a package, an addon or our own version.

- Physics: Godot's `RigidBody2D` and joints, not Box2D.NET.
- ML: we write the algorithms ourselves (forward pass, GA, backprop), so
  they can be learned and shown. A library may speed them up if it stays
  deterministic for a seed and runs on Android (#790).

## 13. UI is the last mile

- Never let a beautiful UI hide a broken simulation: sim first, UI on
  top.
- The UI reads its state from the sim; the sim doesn't know the UI
  exists.

---

## Style specifics (C#)

`.editorconfig` and `dotnet format` own formatting and naming. Beyond
them:

- `readonly` everywhere it fits
- `sealed` by default on classes, unsealed only when subclassing is
  planned
- `record` for immutable data, `class` for identity and state
- No `#region`; a file that needs regions is too big
- A switch over `PartKind` or `CreatureElementKind` is complete or a
  subset. A complete one names every member and has no discard, so a new
  kind fails the build (CS8509). A value per link kind comes from
  `PartKinds.OfLink`, which takes one per link kind. Any other subset
  switch names the category it takes, throws for the rest, and has a test
  that walks every kind against that category.

## Anti-patterns we explicitly reject

- Singletons that hold mutable game state (autoloads for pure services
  are fine)
- Reflection-based "magic" wiring
- God objects (`GameManager` that knows everything)
- Deep inheritance chains
- Async/await where a per-frame update would do
- Premature ECS / DOTS-style architecture

## Chosen tooling

Changing a choice needs an issue. Test tools are in
`docs/TEST_STRATEGY.md` → "Tools"; target frameworks in
`docs/ARCHITECTURE.md`.

| Concern | Choice | Why |
|---|---|---|
| Solution format | **`.slnx`** (XML) | Readable diffs, no GUIDs |
| Package versioning | **Central Package Management** (`Directory.Packages.props`) | One source of truth for versions; the Godot csproj opts out, since its SDK manages its own |
| Code style | **`.editorconfig` + `dotnet format`** | Editor-agnostic |
| DI | **Hand-rolled constructor injection**, autoloads as composition root | |

- `TreatWarningsAsErrors` is on in `Directory.Build.props`, but off in
  `project/NodeRunner.csproj` because Godot's source generators emit code
  we don't own; that project makes only CS8509 an error.
- `GenerateDocumentationFile` is on so `IDE0005` (unused usings) fails
  the build; `NoWarn` suppresses the `CS1591` (missing XML docs) it
  triggers.
