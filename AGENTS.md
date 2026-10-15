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
   owner, listed in `README.md` → "Documentation"; write docs with the
   `write-docs` skill.
2. **Godot first, always.** Write our own solution only when Godot cannot
   do it natively, and say why (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
3. **Run the review agents before every commit or push,** with the
   `review-change` skill; then ship with the `ship-pr` skill.
   `docs/REVIEW.md` → "Definition of Done" owns the gate.
4. **Work autonomously.** Pause only when blocked by hardware or when a
   decision genuinely needs a human. Pick work from recent discussions with
   a human or from GitHub, start or resume each issue with the
   `start-issue` skill, and break big issues into milestones or
   sub-issues.
5. **Prefer physical devices for Android checks.** Ask first whether they
   are free, and stop any emulator you start (`docs/MANUAL_TESTING.md` →
   "Android checks").
6. **Record work and decisions on GitHub.** File new work, bugs and
   decisions as issues, including improvements spotted along the way,
   with the `create-issue` skill, and review broad, risky or ambiguous
   issues before implementing them with the `review-issue` skill.
   Don't be afraid to suggest large refactors when they would benefit the code.
7. **Keep code and project terms in English, whatever the chat language.**
   Talk to the human in their language, but write identifiers, part and
   concept names (`docs/GLOSSARY.md`), file names and quoted issue text
   exactly as they are; translating them makes them hard to recognise.

## Working copy

- Work only in the checkout the session started in. Do not create git
  worktrees, extra clones or copies of the repository.
- Do each change on a feature branch (`git switch -c ...`), and switch back
  to `main` once it is merged. `main` changes only through a merged PR
  (`docs/REVIEW.md` → "Merge gates").
- If the checkout has uncommitted changes or is on another branch when work
  should start, stop and ask the human. Never stash, reset or discard them,
  and never work around them.
- Scratch files (screenshots, builds, renders, notes) go in `.scratch/` in
  the checkout, which is git-ignored, never in `/tmp`; delete them when
  they are no longer needed.

Hooks in `.claude/hooks/` enforce what they can of this, and the PR title
format, in both tools (`docs/REVIEW.md` → "Hooks").

## Repository map

- `libs/`: the engine-independent Domain, Mechanics, ML and App layers.
- `project/`: the Godot host (scenes, simulation, composition, UI).
- `tests/`: mirrors the pure-C# layers and enforces the architecture.
- `docs/`: design, process and teaching documents.
- `issues/`: a read-only archive of the old file-based tracker.

`docs/ARCHITECTURE.md` owns the layers and solution layout. Folder- and
layer-specific rules live in `.claude/rules/`; each lists the files it
governs in `paths`, and both tools load it when you read a matching file.
Read the matching rules yourself before creating a file there, or when you
work from a diff alone.

Procedures live as skills in `.claude/skills/<name>/SKILL.md`. Both tools
load one when the task matches its description, or by `/<name>`; when a
directive above names a skill, use it even if it did not load by itself.

## Machine-local setup

`LOCAL_CONFIG.md` (git-ignored, one per clone) holds host-specific state:
GitHub CLI accounts, SSH host aliases and paths. Read it before running
`gh`, `git push` or anything that touches an external account; the primary
`gh` login is not necessarily the one with write access to this repo.

Skills, agents, rules and hook scripts named `local_*`, and
`.claude/settings.local.json`, are git-ignored too (`.gitignore` lists the
folders). Both tools load the local skills, agents and rules, and run the
hooks that `settings.local.json` registers. Use them for one machine's
procedures, such as a test device's lock; committed instructions never
require one to exist.
