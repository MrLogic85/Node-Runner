# Review process

How a change lands in `main`. Applies to humans and AI agents equally.

## TL;DR

1. Branch from `main` (or use a Copilot-agent PR).
2. Push and open a PR against `main`. The PR template auto-populates.
3. CI must be green. AI reviewers post advisory comments once they are
   enabled (see § "AI reviewer" below).
4. Squash-merge. The issue file was already moved to `issues/closed/` as
   part of the PR — nothing extra to do post-merge.

Never push straight to `main`. Branch protection will reject direct pushes
once it is configured; until then, treat it as a hard convention.

## Roles

### Author (human or AI)

- Owns the change. Reads the linked issue, `docs/ARCHITECTURE.md`, and the
  local `AGENTS.md` before starting.
- Keeps the diff small. If it grows past one clear intent, splits it.
- Fills the PR template, confirms the DoD, and explains anything that does
  not apply.
- Responds to review comments; does not resolve conversations they didn't
  address.

### Human reviewer

- Human review is optional; this is a solo project and no approval is
  required to merge.
- Runs the checks in `## How to verify` locally when the change is risky
  (physics, ML, saving) — CI is necessary but not sufficient for those.
- Checks the DoD against reality.
- Reviews the *design*, not just the diff. "Does this belong in this layer?
  Are we creating debt we won't pay?"
- Flags: layer violations, missing tests for pure-C# logic, hidden
  breaking changes, unreviewed AGENTS.md / ARCHITECTURE.md changes.
- Nits are welcome but must be labelled `nit:` and are non-blocking.

### AI reviewer (Copilot code review, local code-review agents)

- Copilot code review runs automatically on every PR **once enabled on the
  repository**. Until that is configured, only the local pass below runs.
- **Local code-review agents** defined in `CODEREVIEW.md` are dispatched by
  the author (or the assistant working on their behalf) before every
  commit. Each focus area runs as its own parallel agent so findings stay
  scoped. New findings block the commit; preexisting findings are
  informational.
- Findings are advisory. The human reviewer decides which to action.
- If an AI reviewer flags a real bug and the author disagrees, they must
  respond with reasoning, not just resolve the thread.
- AI review is not a substitute for human review, but it catches boring
  bugs and style drift before the human sees them.

### AI author (Copilot coding agent)

- Follows the same PR template.
- Ends every session with a summary of what it did and what it deferred —
  the human reviewer reads this before the diff.
- May move and complete the issue resolved by its PR. Other files under
  `issues/closed/` are historical records and must not be edited.

## Definition of Done

A PR is mergeable when every box is true (or a skipped box is justified in
the PR):

- [ ] Linked issue file is moved to `issues/closed/` as part of the PR
- [ ] `dotnet build NodeRunner.slnx` clean, 0 warnings
- [ ] `dotnet test NodeRunner.slnx` all green (unit + arch)
- [ ] Any new logic in `libs/NodeRunner.{ML,Domain}/` has unit tests
- [ ] `dotnet format NodeRunner.slnx --verify-no-changes` passes
- [ ] Local code-review agents (`CODEREVIEW.md`) dispatched; new findings
      addressed or explicitly dismissed
- [ ] Docs updated where behavior/architecture changed
- [ ] Local `AGENTS.md` reflects any new rule that emerged
- [ ] Nothing under `libs/` uses `using Godot;` — arch tests enforce this
- [ ] No secrets, credentials, or personal data
- [ ] Commit messages: imperative, reference issue (`(#0003)`)
- [ ] Manually verified on desktop; on device if the change reaches physics
      or UI

## Merge gates

GitHub-enforced gates are deliberately limited for this solo project:

- A pull request is required
- Failing build, test, or format check
- The branch must be up to date

The DoD and local code-review gate are completed before the branch is pushed;
they are process rules, not required GitHub approvals.

## What review should flag

- Layer violations that snuck past the arch tests (arch tests only catch
  what they know to check)
- Missing tests for load-bearing math or state changes
- Hidden breaking changes (public API shape shift, save-file format
  change) without a heads-up in the PR description
- Changes to `AGENTS.md`, `ARCHITECTURE.md`, or `TEST_STRATEGY.md` without
  a paragraph in the PR explaining why

## What does NOT block a merge

- Style nits already covered by `.editorconfig` (raise in a `nit:` comment)
- Personal preference between two equally valid designs (raise once; drop
  if the author defends it)
- Missing follow-up issues (file them; do not block the current PR)
- Not adding a test for compiler-generated record members

## Commit hygiene

- Squash-merge PRs. Individual commits inside the PR can be messy; the
  merge commit tells the story.
- Merge-commit message: imperative, references the issue.
  Example: `Add feedforward NeuralNetwork with Xavier init (#0003)`
- Include a `Co-authored-by:` trailer for every human or agent that
  contributed materially.

## Branch protection (recommended settings)

Configure on GitHub → Settings → Rules → Rulesets (or the legacy Branch
protection rules UI) for the `main` branch. Required settings:

- Require a pull request before merging
- Require status checks to pass: `Build`, `Test & coverage`, `Format check`
- Require branches to be up to date before merging
- Require linear history
- Do not allow force pushes
- Do not allow branch deletion
- Include administrators

Until this is configured, treat the rules above as a hard convention. See
the issue tracker for the current setup task.

## Local pre-push checklist

For your own sanity before opening the PR:

```bash
dotnet format NodeRunner.slnx
dotnet build  NodeRunner.slnx
dotnet test   NodeRunner.slnx
```

If any of these fail locally, CI will fail too. Save the round trip.

Additionally, dispatch the code-review agents defined in `CODEREVIEW.md`
against the staged diff. This is a hard rule for agents (see root
`AGENTS.md` § "Prime directives"), and a strong habit for humans.

## When the reviewer is *you*

Solo work still gets a PR. The value is:

- A durable diff to read a year from now with rested eyes
- CI catches the change you were 90% sure was harmless
- Copilot review flags what tired-you missed
- Forces a written PR description, which is future-you's context

Merge when CI is green without waiting for external approval, but *never*
skip the DoD checklist.

## When the change is trivial

Docs typo, one-line comment, gitignore fix. Still a PR, still CI, but skip
the manual verification steps and note "trivial" in the PR body. The
history is worth more than the ceremony.
