# AGENTS.md

Guidance for AI coding agents (Copilot CLI, Claude Code, Cursor, etc.) and
human contributors working in the **Node Runner** repository.

## What this project is

Node Runner is a **learning-by-playing Android app**: the user draws a 2D
creature (nodes, beams, sensors — see `docs/CREATURE_MODEL.md`), a neural
network is generated from it, and the user watches it learn to move through
neuroevolution. The primary goal is **pedagogical**: to make ML concepts
visible, tangible and interactive.

- **Engine:** Godot 4.7 (.NET)
- **Language:** C# for everything
- **Target:** Android, with desktop for development
- **Stage:** `docs/ROADMAP.md` → "Project stage"; it decides whether
  changes to saved data need a migration.

Make sure to read all the documents in `docs/*` and have a good
understanding of the project before starting to work.

## Prime directives for agents

1. **Follow the owning documents and keep them current.** Each topic has one
   owner, listed in `README.md` → "Documentation"; write docs by
   `docs/CODE_DESIGN_PRINCIPLES.md` §9.
2. **Godot first, always.** Write our own solution only when Godot cannot
   do it natively, and say why (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
3. **Run the review agents in `CODEREVIEW.md` before every commit or
   push.** `docs/REVIEW.md` → "Definition of Done" owns the gate.
4. **Work autonomously.** Pause only when blocked by hardware or when a
   decision genuinely needs a human. Pick work from recent discussions with
   a human or from GitHub, and break big issues into milestones or
   sub-issues.
5. **Prefer physical devices for Android checks.** Ask first whether they
   are free, and stop any emulator you start (`docs/MANUAL_TESTING.md` →
   "Android checks").
6. **Record work and decisions on GitHub.** File new work, bugs and
   decisions as issues per `docs/ISSUES.md` → "Creating an issue", and
   review broad, risky or ambiguous issues before implementing them
   (`docs/ISSUES.md` → "Reviewing an issue").

## Repository map

- `libs/`: the engine-independent Domain, Mechanics, ML and App layers.
- `project/`: the Godot host (scenes, simulation, composition, UI).
- `tests/`: mirrors the pure-C# layers and enforces the architecture.
- `docs/`: design, process and teaching documents.
- `issues/`: a read-only archive of the old file-based tracker.

`docs/ARCHITECTURE.md` owns the layers and solution layout. Code folders
with layer-specific rules have their own `AGENTS.md`; read the nearest one
before editing there.

## Machine-local setup

`LOCAL_CONFIG.md` (git-ignored, one per clone) holds host-specific state:
GitHub CLI accounts, SSH host aliases and paths. Read it before running
`gh`, `git push` or anything that touches an external account; the primary
`gh` login is not necessarily the one with write access to this repo.
