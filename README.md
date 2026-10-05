# Node Runner

> Draw a creature. Watch it learn to move. Understand why.

**Node Runner** is a learn-by-playing Android app about neural networks. You
draw a 2D creature from joints, beams, pistons and sensors, and the app
gives it a small neural network: its senses are the inputs and its pistons
the outputs. Then you watch a population of shadows race, generation by
generation, until the brain figures out how to move.

The goal isn't to build a state-of-the-art ML model. It's to make the *shape*
of machine learning (populations, fitness, weights pushing and pulling)
something you can watch, poke and understand.

## Status

**Alpha** (development with testers): creations saved since 0.13.0 keep
loading in later versions. The current milestone and the plan ahead are in
[`docs/ROADMAP.md`](docs/ROADMAP.md) → "Active plan". Published builds are
signed APKs on
[GitHub Releases](https://github.com/MrLogic85/Node-Runner/releases)
(`docs/RELEASING.md`).

## What you can do

- **Build** a Creation from joints, beams, pistons, springs and
  accelerometers, or start from the Walker example. Build checks the body
  and says what is missing before it can train.
- **Train** it: shadows race on flat ground, each finished generation is
  saved, and the best one leads the next.
- **Simulate** the trained brain without learning anything.
- **Look inside the brain** with BrainFocus: which sense pushes which output,
  and how strongly.
- **Change the body after training.** The brain keeps what it learned for the
  parts you keep; new parts start almost unused.

More parts, maps, a growing brain graph, power and achievements are planned;
see the roadmap.

## What's inside

- **Neuroevolution from scratch**: a genetic algorithm trains the network.
  No PyTorch, no ONNX, no ML libraries. All the math is in
  `libs/NodeRunner.ML/`, readable in an evening.
- **A brain that grows** (planned): hidden layers on top of today's direct
  connections, with brain views as it grows.
- **Backpropagation** (later): the other big paradigm, so you get to see both.

## Tech

- Engine: **Godot 4.7** (.NET / C#)
- Language: **C#**. Libraries target **net8.0**, the Godot host targets
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

Android export requires additional setup; see `docs/ARCHITECTURE.md` →
"Android export".

## Repository layout

The app separates pure C# domain, ML, and application logic (`libs/`) from
the Godot runtime host (`project/`). Tests (`tests/`) mirror those
boundaries and enforce the dependency graph. See
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for the layer diagram and
solution layout.

## Documentation

| Topic | Document |
| --- | --- |
| Plan and project stage | [`docs/ROADMAP.md`](docs/ROADMAP.md) |
| Layers and dependencies | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Creature parts and the brain's ports | [`docs/CREATURE_MODEL.md`](docs/CREATURE_MODEL.md) |
| Build mode | [`docs/BUILD_MODE.md`](docs/BUILD_MODE.md) |
| Training loop | [`docs/TRAINING_LOOP.md`](docs/TRAINING_LOOP.md) |
| ML concepts the app teaches | [`docs/ML_CONCEPTS.md`](docs/ML_CONCEPTS.md) |
| Words used in code and UI | [`docs/GLOSSARY.md`](docs/GLOSSARY.md) |
| UI direction | [`docs/UI_DIRECTION.md`](docs/UI_DIRECTION.md) |
| Save format | [`docs/SAVE_FORMAT.md`](docs/SAVE_FORMAT.md) |
| Code rules, tests, review | [`docs/CODE_DESIGN_PRINCIPLES.md`](docs/CODE_DESIGN_PRINCIPLES.md), [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md), [`docs/REVIEW.md`](docs/REVIEW.md) |

## Contributing

This is primarily a solo learning project. Issues live in GitHub Issues;
`issues/` is only a read-only archive of the old file tracker. Read
`AGENTS.md` at the repo root for the working process. Suggestions and pull
requests are welcome. Expect opinionated review, especially around clarity
of the ML code, since the app is meant to *teach*.

## License

GPLv3; see `LICENSE`.
