# AGENTS.md

Guidance for AI coding agents (Copilot CLI, Claude Code, Cursor, etc.) working
in the **Node Runner** repository. Human contributors should also read this — it
is the entry point to the documents that own the project's rules.

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

Read `docs/ROADMAP.md` for what version we are building toward,
`docs/ARCHITECTURE.md` for how the code is organized, and `docs/REVIEW.md`
for how changes land in `main`. Read `docs/UI_DIRECTION.md` before adding
visible controls or changing screen layout.

## Prime directives for agents

1. **Follow the owning documents.** `docs/ARCHITECTURE.md` owns layers and
   dependencies, `docs/CODE_DESIGN_PRINCIPLES.md` owns implementation rules,
   `docs/TEST_STRATEGY.md` owns testing, and `docs/REVIEW.md` owns how changes
   land. The nearest local `AGENTS.md` adds only layer-specific instructions.
2. **Run code review before committing or pushing.** Dispatch the
   reviews in `CODEREVIEW.md` against the staged diff (or the range about
   to be pushed). Report the findings. Only commit/push once each *new*
   finding has either been addressed in the diff or is explicitly judged
   as not useful — by the human, or by the agent with a written
   justification. Preexisting findings do not block the current commit;
   new findings introduced by the diff do until they are addressed or
   dismissed. Code-review clean = commit/push authorised.
3. **Ask when in doubt.** If a design decision is not covered by
   `docs/CODE_DESIGN_PRINCIPLES.md`, stop and ask the human.

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

## Working process

- Work from a GitHub Issue and follow the lifecycle in `docs/REVIEW.md`.
- For non-trivial or ambiguous issues, use the issue-review flow in
  `docs/ISSUE_REVIEW.md` before implementation.
- Treat archived file issues under `issues/archive/` as immutable historical
  records. Do not edit them; update the corresponding GitHub Issue instead.
- Read the owning documents and nearest local `AGENTS.md` before editing.
- Keep each change focused on the issue; ask if scope or design is unclear.
- Follow `docs/REVIEW.md` when finishing the change. The code-review gate in
  the prime directives applies before every commit and push.

## Machine-local setup

Host-specific state (GitHub CLI accounts, SSH host aliases, per-machine
paths) lives in `LOCAL_CONFIG.md` at the repo root. That file is git-ignored;
each clone maintains its own. Read it before running `gh`, `git push`, or any
command that touches an external account — the primary `gh` login on a
machine is not necessarily the account that has write access to this repo.

## Definition of Done

Authoritative Definition of Done lives in `docs/REVIEW.md`. Follow the list
there; do not maintain a second copy.

## What agents should NOT do without asking

- Add dependencies (NuGet packages, GDExtensions, addons)
- Rename or move files/folders in bulk
- Change project settings in `project.godot` or `.csproj`
- Introduce a new ML paradigm (RL, transformers, etc.) — these are roadmap
  decisions
- Rewrite existing modules "for clarity" — propose in an issue first
