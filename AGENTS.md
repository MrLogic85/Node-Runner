# AGENTS.md

Guidance for AI coding agents (Copilot CLI, Claude Code, Cursor, etc.) working
in the **Node Runner** repository. Human contributors should also read this — it
is the entry point to the documents that own the project's rules.

## What this project is

Node Runner is a **learning-by-playing Android app** where the user draws a 2D
creature (nodes, beams, cores — see `docs/CREATURE_MODEL.md`), a neural
network is generated from it, and the user watches it learn to move via
neuroevolution and (later) backprop. The
primary goal is **pedagogical**: to make ML concepts visible, tangible, and
interactive.

- **Engine:** Godot 4 (latest stable)
- **Language:** C# (.NET) for everything
- **Target:** Android (primary), desktop for development
- **Not targeted (yet):** iOS, web, consoles

Read `docs/ROADMAP.md` for what version we are building toward,
`docs/ARCHITECTURE.md` for how the code is organized, docs/CODE_DESIGN_PRINCIPLES.md
for how to write good code, and `docs/REVIEW.md`
for how changes land in `main`. Read `docs/UI_DIRECTION.md` before adding
visible controls or changing screen layout. Read docs/ML_CONCEPTS.md when
changing what the app teaches or how a concept is made visible.

## Prime directives for agents

1. **Follow the owning documents.** `docs/ARCHITECTURE.md` owns layers and
   dependencies, `docs/CODE_DESIGN_PRINCIPLES.md` owns implementation rules,
   `docs/TEST_STRATEGY.md` owns testing, and `docs/REVIEW.md` owns how changes
   land. The nearest local `AGENTS.md` adds only layer-specific instructions.
2. **Run code review agents before committing or pushing.** Dispatch the
   reviews in `CODEREVIEW.md` against the staged diff (or the range about
   to be pushed). Only commit/push once each *new*
   finding has either been addressed or judged
   as not useful — by the human, or by the agent.
   Code-review clean = commit/push authorised.
3. Always try to continue to work autonomously, only pause when you are stuck
   due to hardware issues or when you guninely need input from a human. Pick work
   tasks from recent discussions with a human or from GitHub.
4. When new milestones, feature bugs are found or discussed. Add or update them
   on GitHub. Dont leave desicions undocumented. Review broad, risky, or ambiguous
   issues under docs/ISSUE_REVIEW.md before implementation.

## Architecture map

`docs/ARCHITECTURE.md` owns the detailed layer map and dependency graph.
Conceptually:

- `libs/` contains the engine-independent Domain, ML, and App layers.
- `project/` is the Godot host: scenes, simulation, composition, and UI.
- `tests/` mirrors the pure-C# layers and enforces architecture boundaries.
- `docs/` owns durable design, process, roadmap, and teaching material.
- `issues/` is a read-only archive of the old file-based tracker.

Folders inside `libs/` and `project/src/` have local `AGENTS.md` files.
Read the nearest one before editing that layer.

## Machine-local setup

Host-specific state (GitHub CLI accounts, SSH host aliases, per-machine
paths) lives in `LOCAL_CONFIG.md` at the repo root. That file is git-ignored;
each clone maintains its own. Read it before running `gh`, `git push`, or any
command that touches an external account — the primary `gh` login on a
machine is not necessarily the account that has write access to this repo.

## Definition of Done

Authoritative Definition of Done lives in `docs/REVIEW.md`. Follow the list
there; do not maintain a second copy.
