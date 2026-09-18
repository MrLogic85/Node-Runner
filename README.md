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

**Pre-alpha.** Currently working toward 0.1.0 — a fixed hardcoded creature
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
- Language: **C#** — libraries target **net8.0**, the Godot host targets
  **net9.0** for Godot 4.7 Android templates, and tests target **net10.0**
  (the SDK currently installed)
- Target: **Android** (dev on macOS/Linux/Windows)
- License: **GPLv3**

## Building

```bash
# Prereqs: .NET 10 SDK, Godot 4.7 (Mono/.NET build)
dotnet build NodeRunner.slnx     # builds all libs + tests + Godot csproj
dotnet test  NodeRunner.slnx     # runs xUnit + architecture tests
# Open project/project.godot in Godot and hit F5
```

Android export requires additional setup — see GitHub Issue #10.

## Repository layout

The app separates pure C# domain, ML, and application logic from the Godot
runtime host. Tests mirror those boundaries and enforce the dependency graph.
See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the layer diagram and
solution layout. See [`docs/UI_DIRECTION.md`](docs/UI_DIRECTION.md) for the
current lightweight UI compass.

## Contributing

This is primarily a solo learning project. Issues live in GitHub Issues;
`issues/` is only a read-only archive of the old file tracker. Read
`AGENTS.md` at the repo root for the working process. Suggestions and pull
requests are welcome — expect opinionated review, especially around clarity
of the ML code, since the app is meant to *teach*.

## License

GPLv3 — see `LICENSE`.
