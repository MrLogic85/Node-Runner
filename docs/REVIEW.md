# Review process

How a change lands in `main`, for humans and AI agents alike.

## TL;DR

1. Branch from `main`; never push to `main` directly (the ruleset rejects
   it).
2. Run the review agents in `CODEREVIEW.md` before every commit or push.
3. Push, open a PR against `main` with the template, and enable squash
   auto-merge.
4. Own the PR until it merges: fix failing checks, answer every review
   comment with a fix or a reason, and don't resolve threads you didn't
   address.
5. Confirm the PR merged and `main` is green. The PR's closing keyword
   closes the issue.

A trivial change (typo, comment) still goes through a PR and CI; skip
manual verification and say "trivial" in the PR.

The local code-review agents in `CODEREVIEW.md` review every PR; #956
decides whether Copilot code review joins them. Human review is optional,
and `nit:` comments don't block.

## Definition of Done

A PR is mergeable when every box is true, or the PR says why a box does
not apply:

- [ ] A linked GitHub Issue and a closing keyword (release-notes PRs use
      `Part of`; see `docs/RELEASING.md`)
- [ ] Required checks green (see "Merge gates"), and
      `dotnet build NodeRunner.slnx -warnaserror` is clean: CI does not
      fail on warnings in `project/`
- [ ] New logic in `libs/NodeRunner.{Domain,Mechanics,ML}/` has unit tests
- [ ] Review agents (`CODEREVIEW.md`) ran on the final diff, and every new
      finding is fixed or dismissed with a written reason. UI-touching
      changes (screens, controls, layout, theme) include "Visual & UX
      design", run by `design-lead` (`.github/agents/design-lead.agent.md`);
      its "insufficient evidence" result blocks until live app access,
      screenshots or recordings, or a human waiver is recorded in the PR.
- [ ] Docs and the nearest `AGENTS.md` updated where behaviour,
      architecture or a rule changed
- [ ] A change to a saved shape updates `docs/SAVE_FORMAT.md` and the
      schemas in `docs/save-schema/`, and adds the migration and test the
      current stage requires (`docs/SAVE_FORMAT.md` → "Versions and
      migration")
- [ ] No secrets, credentials or personal data
- [ ] PR title follows "PR title and commit hygiene"
- [ ] Manual testing decided and done per `docs/MANUAL_TESTING.md`
- [ ] Squash auto-merge enabled

## Merge gates

The `main` ruleset requires a PR, linear history, squash merges and the
checks `PR title`, `Build`, `Test & coverage` and `Format check`; it needs
no approval and has no exceptions for administrators. Branches need not be
up to date with `main`, because auto-merge would otherwise stall whenever
another PR lands first; the CI run on `main` catches the rare semantic
conflict.

## Auto-merge

After opening the PR, enable GitHub's squash auto-merge:

```bash
gh pr merge --auto --squash
```

GitHub owns the wait, so no local process has to outlive the session. The
author still watches the checks while the session lasts, fixes failures,
re-runs the affected review focuses after a corrective change, and confirms
the PR reached `MERGED` with `main` green. If auto-merge cannot be enabled,
keep the PR open, report the blocker, and never bypass the required checks.

## What review should flag

- Layer violations the arch tests don't know to check
- Missing tests for load-bearing math or state changes
- Hidden breaking changes (public API shape, save format) without a note in
  the PR, or without the migration the current stage requires
  (`docs/ROADMAP.md` → "Project stage")
- Changes to `AGENTS.md`, `ARCHITECTURE.md` or `TEST_STRATEGY.md` without a
  paragraph in the PR saying why

## What does NOT block a merge

- Style nits already covered by `.editorconfig`
- A preference between two equally valid designs (raise it once; drop it if
  the author defends it)
- Missing follow-up issues (file them instead)
- No test for compiler-generated record members

## PR title and commit hygiene

- Format: `type(#123): Description`, for example
  `feat(#11): Add feedforward neural network`.
- Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `build`, `ci`,
  `perf`, `style`, `revert`.
- The scope is the issue number with `#`; a PR closing several issues lists
  them: `refactor(#497, #498): Description`.
- The description starts with an uppercase letter.
- PRs are squash-merged with the PR title as the commit subject, so commits
  inside a PR may be messy.
- Add a `Co-authored-by:` trailer for every human or agent that contributed
  materially.

## Releases

Publishing a version is not a change to `main`; `docs/RELEASING.md` owns
it, including its PRs on `release/vX.Y`.
