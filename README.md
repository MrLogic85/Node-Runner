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

**Alpha** (`docs/ROADMAP.md` → "Project stage"). Signed APKs are on
[GitHub Releases](https://github.com/MrLogic85/Node-Runner/releases).

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

A genetic algorithm trains the network; all the math is in
`libs/NodeRunner.ML/`, readable in an evening.
What comes next is in [`docs/ROADMAP.md`](docs/ROADMAP.md) and the
[milestones](https://github.com/MrLogic85/Node-Runner/milestones).

## Building

Built with Godot 4.7 (.NET/C#) for Android.

```bash
# Prereqs: .NET 10 SDK, Godot 4.7 (Mono/.NET build)
dotnet build NodeRunner.slnx     # builds all libs + tests + Godot csproj
dotnet test  NodeRunner.slnx     # runs xUnit + architecture tests
# Open project/project.godot in Godot and hit F5
```

Android export: `docs/RELEASING.md` → "Android export".

## Documentation

| Topic | Document |
| --- | --- |
| Agent and contributor entry point | [`AGENTS.md`](AGENTS.md) |
| Vision, project stage, plan | [`docs/ROADMAP.md`](docs/ROADMAP.md) |
| Layers, dependencies, solution layout | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Creature parts and the brain's ports | [`docs/CREATURE_MODEL.md`](docs/CREATURE_MODEL.md) |
| Build mode | [`docs/BUILD_MODE.md`](docs/BUILD_MODE.md) |
| Training loop | [`docs/TRAINING_LOOP.md`](docs/TRAINING_LOOP.md) |
| ML concepts the app teaches | [`docs/ML_CONCEPTS.md`](docs/ML_CONCEPTS.md) |
| Words used in code and UI | [`docs/GLOSSARY.md`](docs/GLOSSARY.md) |
| UI direction | [`docs/UI_DIRECTION.md`](docs/UI_DIRECTION.md) |
| How parts and the arena look | [`docs/WORLD_VISUALS.md`](docs/WORLD_VISUALS.md) |
| Text and translation | [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) |
| Save format | [`docs/SAVE_FORMAT.md`](docs/SAVE_FORMAT.md) |
| Code and doc rules | [`docs/CODE_DESIGN_PRINCIPLES.md`](docs/CODE_DESIGN_PRINCIPLES.md) |
| Automated tests | [`docs/TEST_STRATEGY.md`](docs/TEST_STRATEGY.md) |
| Manual tests | [`docs/MANUAL_TESTING.md`](docs/MANUAL_TESTING.md) |
| Issues, labels and project fields | [`docs/ISSUES.md`](docs/ISSUES.md) |
| How a change lands | [`docs/REVIEW.md`](docs/REVIEW.md), [`CODEREVIEW.md`](CODEREVIEW.md) |
| Releasing | [`docs/RELEASING.md`](docs/RELEASING.md) |

## Contributing

This is primarily a solo learning project; suggestions and pull requests
are welcome. Work is tracked in GitHub Issues (`docs/ISSUES.md`), and
`AGENTS.md` is the working process. Expect opinionated review, especially
around clarity of the ML code, since the app is meant to *teach*.

## License

GPLv3; see `LICENSE`.
