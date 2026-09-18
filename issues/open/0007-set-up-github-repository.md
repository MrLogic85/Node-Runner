---
id: 0007
title: Set up GitHub repository and initial push
status: in-progress
priority: p2
type: chore
labels: [build, chore, docs]
version: v0.1
created: 2026-09-16
updated: 2026-09-18
---

## Summary

Initialize the project as a git repository, create a GitHub repository for it,
and push the current scaffolding as the first commit(s). Decide and document
the collaboration workflow (branches, PRs, CI).

## Context

The repo currently lives only locally at
`~/Documents/Godot/Node Runner`. We want off-machine backup, history in a
sensible place, and eventually CI to catch regressions.

Note: `AGENTS.md` explicitly forbids agents from running `git commit`/`git push`
without human instruction. This issue's push steps must be executed by (or
under explicit direction from) the human.

## Acceptance criteria

- [ ] `git init` run in the repo root; `main` is the default branch
- [ ] `.gitignore` reviewed and confirmed to cover Godot + .NET artifacts
      (existing one is a good start)
- [ ] `git config user.name` / `user.email` set appropriately for this repo
- [ ] GitHub repository created (name, visibility, description decided —
      see Notes)
- [ ] `origin` remote configured (SSH preferred over HTTPS if user has keys
      set up)
- [ ] Initial commit(s) pushed to `main`:
  - Suggested: one commit per logical group (`chore: repo scaffolding`,
    `chore: initial docs`, `chore: initial issues`, `feat: bootstrap Godot
    project (#0001)`) rather than a single dump
- [ ] `README.md` at repo root added — short project pitch, screenshot
      placeholder, "how to build" section pointing at `docs/`
- [ ] Repo URL added to `README.md` and (if applicable) to `AGENTS.md`
- [ ] Basic branch protection considered: even for a solo repo, requiring
      linear history and disallowing force-push to `main` is cheap insurance
- [ ] CI decision: yes/no for now. If yes, minimal GitHub Actions workflow
      that runs `dotnet build NodeRunner.slnx` and `dotnet test` on push/PR
      (already scaffolded in `.github/workflows/ci.yml`)
- [ ] `docs/ARCHITECTURE.md` (or a new `docs/CONTRIBUTING.md`) updated with
      the chosen branch/PR workflow

## Notes

Decisions to make before starting:

- **Repository name.** `node-runner` is the obvious default. Consider whether
  the project name might change (this app is currently pitched as
  "Critter Lab" in some docs vs. the folder name "Node Runner").
- **Visibility.** Public or private?
  - Public: nice for portfolio, invites contributions, forces good hygiene.
  - Private: fewer eyes on half-baked commits.
- **Account/org.** Personal account or a dedicated org?
- **Branch strategy.** Solo hobby → likely trunk-based on `main` with
  occasional feature branches. Document whichever we pick so agents follow it.
- **License.** If public, pick one (MIT is a low-friction default for
  learning projects). If private, defer.
- **Copilot / agent PRs.** If we plan to use Copilot coding agent or similar
  in the future, we need a repo (this issue) as a prerequisite.

## Decisions (locked 2026-09-18)

- **Repository name:** `Node Runner` (kept — leans into a slightly retro/scifi
  vibe, which the target audience appreciates)
- **Visibility:** public
- **Account:** personal (human sets it up manually)
- **License:** GPLv3 (canonical "GNU" license; strong copyleft fits a learning
  project intended to stay open)
- **Branch strategy:** trunk-based on `main`, short-lived feature branches for
  larger work, PRs even solo so CI runs before merge
- **CI:** yes, minimal from day one — GitHub Actions running
  `dotnet build NodeRunner.slnx` and `dotnet test NodeRunner.slnx` on push
  and PR (already wired in `.github/workflows/ci.yml` — build, test with
  coverage, and `dotnet format --verify-no-changes`)

Steps once decisions are made (rough sketch, human executes the push):

```bash
cd "~/Documents/Godot/Node Runner"
git init -b main
git add .
# Split into logical commits, or one big initial commit for v0
git commit -m "chore: initial project scaffolding"
gh repo create <owner>/node-runner --<public|private> --source=. --remote=origin
git push -u origin main
```

Out of scope for this issue:

- Setting up GitHub Copilot coding agent config — separate issue
- Release automation / tagging — separate issue when we get near v0.1 ship
- Issue tracker migration to GitHub Issues — we're staying file-based per
  `issues/README.md`
