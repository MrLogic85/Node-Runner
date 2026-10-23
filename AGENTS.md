# AGENTS.md

Guidance for AI coding agents and human contributors working in the
**Node Runner** repository.

## What this project is

Node Runner is a **learning-by-playing Android app**: the user draws a 2D
creature (`docs/CREATURE_MODEL.md`), a neural network is generated from
it, and the user watches it learn to move through neuroevolution. The
goal is **pedagogical**: to make ML concepts visible, tangible and
interactive.

- Godot (.NET) and C# for everything; versions in `README.md` → "Tools".
- Android, with desktop for development.
- The stage (`docs/ROADMAP.md` → "Project stage") decides whether a
  change to saved data needs a migration.

Read the documents your task touches, not all of `docs/`: `README.md` →
"Documentation" names the owner of each topic.

## How to act

This is how I'd like us to talk to each other.

When I ask a question or think aloud, I want a conversation, not a pull
request. Answer me, tell me what you think and propose what you'd do.
Start building when I ask for it or agree. If we land on something worth
keeping, record it in an issue; you decide when that is.

Be honest rather than agreeable. If you think I'm wrong, say so and tell
me why. If something didn't work, say that plainly: "I couldn't get this
to work" is a useful answer. A claim that something works needs
evidence, and praise needs something to point to.

The rules in this repository have reasons. When a case doesn't fit a
rule, tell me why and wait for my answer before you depart from it.

Talk to me in my language.

## Prime directives

1. **Follow the owning documents and keep them current and short.** Write
   docs with the `write-docs` skill, and agents, skills, rules files,
   hooks and this file with the `write-agent-files` skill.
2. **Godot first, always** (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
3. **Run the review agents before every commit or push** with the
   `review-change` skill, then ship with the `ship-pr` skill.
   `docs/REVIEW.md` → "Definition of Done" owns the gate.
4. **Work autonomously.** Pause only when blocked by hardware or when a
   decision needs a human. Pick work as "How to act" says, or Ready
   issues on GitHub, and start or resume each issue with the
   `start-issue` skill.
5. **Prefer physical devices for Android checks,** but ask first whether
   they are free, and stop any emulator you start. Use the
   `android-check` skill.
6. **Record work and decisions on GitHub.** File bugs, improvements and
   decisions as issues with the `create-issue` skill, and review broad,
   risky or ambiguous issues with the `review-issue` skill. Suggest large
   refactors when they would benefit the code.
7. **Keep code and project terms in English,** whatever language you
   talk in: write identifiers, part and concept names
   (`docs/GLOSSARY.md`), file names and quoted issue text exactly as they
   are; translated, they are hard to recognise.

## Working copy

- Work only in the checkout the session started in. Do not create git
  worktrees, extra clones or copies of the repository.
- Do each change on a feature branch (`git switch -c ...`), and switch
  back to `main` once its PR is open. `main` changes only through a merged
  PR (`docs/REVIEW.md` → "Merge gates").
- If the checkout has uncommitted changes or is on another branch when
  work should start, stop and ask the human. Never stash, reset or discard
  them, and never work around them.
- Scratch files (screenshots, builds, renders, notes) go in the
  git-ignored `.scratch/`, never in `/tmp`; delete them when no longer
  needed.

## Repository map

- `libs/`: the engine-independent Domain, Mechanics, ML and App layers.
- `project/`: the Godot host (scenes, simulation, composition, UI).
- `tests/`: tests of the pure-C# layers, the architecture and static
  Godot contracts.
- `docs/`: design, process and teaching documents.
- `issues/`: a read-only archive of the old file-based tracker.

`docs/ARCHITECTURE.md` owns the layers, and
`docs/CODE_DESIGN_PRINCIPLES.md` §9 where rules and procedures live. Claude
Code loads a `.claude/rules/` file when you read a file its `paths`
match; read the matching rules yourself before creating a file there, or
when you work from a diff alone. When a directive names a skill, use it
even if it did not load by itself.

## Machine-local setup

`LOCAL_CONFIG.md` (git-ignored, one per clone) holds host-specific state:
GitHub CLI accounts, SSH host aliases and paths. Read it before running
`gh`, `git push` or anything that touches an external account; the
primary `gh` login may not be the one with write access to this repo.

Skills, agents, rules and hook scripts named `local_*`, and
`.claude/settings.local.json`, which registers local hooks, are
git-ignored too. They hold one machine's procedures, such as a test
device's lock; committed instructions never require one to exist.
