# Node Runner

> Draw a creature. Watch it learn to move. Understand why.

**Node Runner** is a learn-by-playing Android app about neural networks. You
draw a 2D creature from joints, beams, pistons and sensors, and the app
gives it a small neural network: its sensor readings are the inputs and its
pistons the outputs. Then you watch a population of shadows race, generation by
generation, until the network figures out how to move.

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
- **Simulate** the trained network without learning anything.
- **Look inside the network** with NetworkFocus: which input pushes which output,
  and how strongly.
- **Change the body after training.** The network keeps what it learned for the
  parts you keep; new parts start almost unused.

A genetic algorithm trains the network; all the math is in
`libs/NodeRunner.ML/`, readable in an evening. What comes next is in
[`docs/ROADMAP.md`](docs/ROADMAP.md) and the [milestones](https://github.com/MrLogic85/Node-Runner/milestones).

## Building

Built with Godot (.NET/C#) for Android. Install the tools in "Tools"
below, then:

```bash
dotnet build NodeRunner.slnx     # builds all libs + tests + Godot csproj
# Open project/project.godot in Godot and hit F5
```

Tests: `docs/TEST_STRATEGY.md` → "Running the suite". Android export:
`docs/RELEASING.md` → "Android export".

### Tools

Every tool a script, doc or agent relies on; machine-specific state goes
in `LOCAL_CONFIG.md` (`AGENTS.md` → "Machine-local setup").

| Tool | Required or recommended for | macOS install |
| --- | --- | --- |
| .NET 10 SDK | Required: building and testing | `brew install --cask dotnet-sdk` |
| Godot 4.7.2, .NET build | Required: running the app | `brew install --cask godot-mono` |
| `git` 2.31+ | Required: the hooks and `.github/scripts/review-receipt.sh` | Xcode Command Line Tools, or `brew install git` |
| `gh` 2.94+ | Required: scripts in `.github/scripts/` and the skills | `brew install gh` |
| `jq` | Required: the same scripts, the hooks, which are off without it, and the hook specs | `brew install jq` |
| `adb` | Required: Android checks | `brew install --cask android-platform-tools` |
| `rg` (ripgrep) | Recommended: fast search that skips build output | `brew install ripgrep` |
| `csharp-ls` | Recommended: the agents' `LSP` tool; without it they use `git grep` | Pinned in `dotnet-tools.json`; a new agent session runs `dotnet tool restore`. Claude Code loads it only in a trusted folder |
| `gettext` | Recommended: adding a language; Poedit does the same | `brew install gettext` |
| Google Chrome | Recommended: `design-lead`'s mockups; without it they stay HTML | `brew install --cask google-chrome` |

The `dotnet-sdk` cask installs the newest SDK; the build needs 10.x. The
scripts' `gh` login needs the `project` scope (`docs/ISSUES.md` →
"Setting project fields"). Releases need more: `docs/RELEASING.md` →
"Android export".

## Documentation

| Topic | Document |
| --- | --- |
| Agent and contributor entry point | [`AGENTS.md`](AGENTS.md) |
| Vision, project stage, plan | [`docs/ROADMAP.md`](docs/ROADMAP.md) |
| Layers, dependencies, solution layout | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| Creature parts and the network's ports | [`docs/CREATURE_MODEL.md`](docs/CREATURE_MODEL.md) |
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
| Tools to install | `README.md` → "Tools" |
| How a change lands, review agents, hooks | [`docs/REVIEW.md`](docs/REVIEW.md) |
| What each review agent checks | [`.claude/agents/`](.claude/agents/) |
| Procedures (skills) | [`.claude/skills/`](.claude/skills/) |
| Releasing | [`docs/RELEASING.md`](docs/RELEASING.md) |

## Contributing

This is primarily a solo learning project; suggestions and pull requests
are welcome. Work is tracked in GitHub Issues (`docs/ISSUES.md`), and
`AGENTS.md` is the working process. Expect opinionated review of the ML
code's clarity, since the app is meant to *teach*.

## License

GPLv3; see `LICENSE`.
