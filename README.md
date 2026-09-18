# Node Runner

> Draw a creature. Watch it learn to move. Understand why.

**Node Runner** is a learn-by-playing Android app about neural networks. You
sketch a 2D creature — joints, bones, muscles — and the app auto-generates a
neural network that controls it. Then you sit back and watch a small
population evolve, generation by generation, until it figures out how to move.

The goal isn't to build a state-of-the-art ML model. It's to make the *shape*
of machine learning — populations, fitness, weights firing, gradients
descending — something you can watch, poke, and understand.

## Status

**Pre-alpha.** Currently working toward v0.1 — a fixed hardcoded creature
that twitches on your phone. See `docs/ROADMAP.md`.

## What's inside

- **Neuroevolution from scratch** — genetic algorithms training a feedforward
  neural network. No PyTorch, no ONNX, no ML libraries. All the math is in
  `libs/NodeRunner.ML/`, readable in an evening.
- **Backpropagation** (from v3.0) — the other big paradigm, so you get to see
  both.
- **Live network visualization** (from v2.0) — nodes glow when they fire,
  edges thicken with weight. You see the thought behind each step.

## Tech

- Engine: **Godot 4.7** (.NET / C#)
- Language: **C#** — libraries target **net8.0** (Godot's runtime), tests
  target **net10.0** (the SDK currently installed)
- Target: **Android** (dev on macOS/Linux/Windows)
- License: **GPLv3**

## Building

```bash
# Prereqs: .NET 10 SDK, Godot 4.7 (Mono/.NET build)
dotnet build NodeRunner.slnx     # builds all libs + tests + Godot csproj
dotnet test  NodeRunner.slnx     # runs xUnit + architecture tests
# Open project/project.godot in Godot and hit F5
```

Android export requires additional setup — see issue `#0002` in
`issues/open/`.

## Repository layout

```
Node Runner/
├── AGENTS.md                     Guidance for AI agents & humans
├── LICENSE                       GPLv3
├── NodeRunner.slnx               The C# solution
├── Directory.Build.props         Shared MSBuild props
├── Directory.Packages.props      Central package versions
├── .editorconfig                 Style rules
├── docs/
│   ├── ROADMAP.md                Versioned feature plan
│   ├── ARCHITECTURE.md           Layered architecture, module boundaries
│   ├── CODE_DESIGN_PRINCIPLES.md Coding standards, do's and don'ts
│   ├── TEST_STRATEGY.md          How each layer is tested
│   ├── ML_CONCEPTS.md            Which ML ideas each version teaches
│   └── GLOSSARY.md               Domain vocabulary
├── issues/
│   ├── README.md                 File-based issue tracker conventions
│   ├── open/                     Active issues
│   └── closed/                   Historical, never deleted
├── libs/                         Pure C# — no Godot
│   ├── NodeRunner.Domain/        Records, enums, invariants
│   ├── NodeRunner.ML/            Neural network + GA + backprop
│   └── NodeRunner.App/           View-models, services, repositories
├── project/                      Godot project root
│   ├── project.godot
│   ├── NodeRunner.csproj         References the three libs
│   ├── scenes/
│   └── src/
│       ├── creature/             Godot Nodes for creatures
│       ├── sim/                  Simulation orchestration
│       ├── managers/             Autoloads / composition root
│       └── ui/
│           ├── lib/              Reusable Controls
│           ├── screens/          Full-screen scenes
│           └── widgets/          App-specific composite widgets
└── tests/                        xUnit — pure C# only
    ├── NodeRunner.Domain.Tests/
    ├── NodeRunner.ML.Tests/
    ├── NodeRunner.App.Tests/
    └── NodeRunner.Arch.Tests/    NetArchTest layer rules
```

Every folder in `libs/` and `project/src/` has its own `AGENTS.md`
describing its rules and boundaries. Start there if you're editing that
layer.

## Contributing

This is primarily a solo learning project. Issues live in `issues/`
(file-based, not GitHub Issues). Read `AGENTS.md` at the repo root for the
working process. Suggestions and pull requests are welcome — expect
opinionated review, especially around clarity of the ML code, since the app
is meant to *teach*.

## License

GPLv3 — see `LICENSE`.
