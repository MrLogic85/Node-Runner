# AGENTS.md

Guidance for AI coding agents (Copilot CLI, Claude Code, Cursor, etc.) working
in the **Node Runner** repository. Human contributors should also read this — it
is the single source of truth for how work is done here.

## What this project is

Node Runner is a **learning-by-playing Android app** where the user draws a 2D
creature (joints, bones, muscles), a neural network is generated from it, and
the user watches it learn to move via neuroevolution and (later) backprop. The
primary goal is **pedagogical**: to make ML concepts visible, tangible, and
interactive. The secondary goal is **the author's own ML education** — code
should be written from scratch where reasonable, not pulled from ML libraries.

- **Engine:** Godot 4 (latest stable)
- **Language:** C# (.NET) for everything
- **Target:** Android (primary), desktop for development
- **Not targeted (yet):** iOS, web, consoles

Read `docs/ROADMAP.md` for what version we are building toward, and
`docs/ARCHITECTURE.md` for how the code is organized.

## Prime directives for agents

1. **Small, verifiable changes.** Prefer one focused PR/commit per issue. If a
   task grows, split it and update `issues/`.
2. **Never commit or push without explicit instruction.** Stage changes and let
   the human review. This applies even in "autopilot" modes.
3. **Keep the ML engine engine-agnostic.** Code under `libs/NodeRunner.ML/`
   must not import `Godot.*`. It takes `double[]` in and returns `double[]`
   out. This is what makes it unit-testable and pedagogically clean. The
   architecture tests in `tests/NodeRunner.Arch.Tests/` fail the build if this
   rule is broken.
4. **Determinism matters.** All randomness must flow from a seeded RNG that is
   logged. "Why did generation 47 explode?" must be reproducible.
5. **Prefer clarity over cleverness.** This is a teaching tool. Code that a
   student can read and learn from beats code that is 5% faster.
6. **Ask when in doubt.** If a design decision is not covered by
   `docs/CODE_DESIGN_PRINCIPLES.md`, stop and ask the human.

## Repository layout (target)

```
Node Runner/
├── AGENTS.md                     # this file — repo-wide rules
├── README.md                     # user-facing overview
├── LICENSE                       # GPLv3
├── NodeRunner.slnx               # the C# solution
├── Directory.Build.props         # shared MSBuild props
├── Directory.Packages.props      # central package versions (CPM)
├── .editorconfig                 # C# style rules
├── docs/
│   ├── ROADMAP.md                # versioned feature plan
│   ├── ARCHITECTURE.md           # how code is organized, key modules
│   ├── CODE_DESIGN_PRINCIPLES.md # coding standards, do's and don'ts
│   ├── TEST_STRATEGY.md          # how each layer is tested, tooling table
│   ├── ML_CONCEPTS.md            # which ML ideas each version teaches
│   └── GLOSSARY.md               # domain vocabulary (joint, muscle, fitness…)
├── issues/
│   ├── README.md                 # how the file-based tracker works
│   ├── open/                     # active issues
│   └── closed/                   # completed / wontfix (moved here on close)
├── libs/                         # PURE C# — no Godot, no I/O side effects
│   ├── NodeRunner.Domain/  AGENTS.md — data records, enums, invariants
│   ├── NodeRunner.ML/      AGENTS.md — neural net + GA + backprop engine
│   └── NodeRunner.App/     AGENTS.md — viewmodels, services, repositories
├── project/                      # Godot project root (project.godot lives here)
│   ├── NodeRunner.csproj         # references the three libs above
│   ├── scenes/
│   └── src/
│       ├── creature/       AGENTS.md — Godot Nodes for creatures
│       ├── sim/            AGENTS.md — orchestration (population, evolver)
│       ├── managers/       AGENTS.md — autoloads (composition root)
│       └── ui/             AGENTS.md — Controls + screens (lib, screens, widgets)
├── tests/                  AGENTS.md — pure-C# tests (xUnit)
│   ├── NodeRunner.Domain.Tests/
│   ├── NodeRunner.ML.Tests/
│   ├── NodeRunner.App.Tests/
│   └── NodeRunner.Arch.Tests/    # NetArchTest — layer rules
├── .github/workflows/            # CI — build, test, coverage
└── .gitignore
```

**Each folder inside `libs/` and `project/src/` has its own `AGENTS.md`.**
These are authoritative for rules specific to that layer (e.g. "no
`using Godot;` in `libs/NodeRunner.ML/`"). Read the local one before
editing that folder.

Do not create files outside this layout without updating this document first.

## Working process

### Before starting work

1. Read the issue in `issues/open/`. If none exists for what you're about to do,
   create one first (see `issues/README.md`).
2. Read the relevant docs (`ARCHITECTURE.md` at minimum).
3. Confirm you understand the scope. If the issue is vague, ask.

### While working

- Match existing style. Run `dotnet format` on C# files before finishing.
- New public types get a one-line XML doc comment. Private helpers don't need
  comments unless the intent is non-obvious.
- If you add a new ML concept, add a short entry to `docs/ML_CONCEPTS.md`
  explaining what it teaches.
- If you introduce new vocabulary, add it to `docs/GLOSSARY.md`.

### Finishing

- Update the issue: move it from `issues/open/` to `issues/closed/` with a
  status line at the top. Do not delete it.
- If the change affects architecture, update `docs/ARCHITECTURE.md` in the same
  commit.
- Summarize what you did in the commit message, referencing the issue file:
  `Fix creature falls through floor (#0007)`.
- **Do not run `git commit` or `git push`** unless the human explicitly asks.

## Definition of Done for an issue

An issue is "done" when all apply:

- [ ] Code compiles without warnings (`dotnet build NodeRunner.slnx`)
- [ ] Any new logic in `libs/NodeRunner.ML/` or `libs/NodeRunner.Domain/` has
      unit tests
- [ ] `dotnet test NodeRunner.slnx` passes (including architecture tests)
- [ ] Manually verified on desktop (Android verification is per-issue)
- [ ] Docs updated if behavior/architecture changed
- [ ] Issue file moved to `issues/closed/` with a resolution note

## What agents should NOT do without asking

- Add dependencies (NuGet packages, GDExtensions, addons)
- Rename or move files/folders in bulk
- Change project settings in `project.godot` or `.csproj`
- Introduce a new ML paradigm (RL, transformers, etc.) — these are roadmap
  decisions
- Rewrite existing modules "for clarity" — propose in an issue first
- Touch anything under `issues/closed/` (it is the historical record)
