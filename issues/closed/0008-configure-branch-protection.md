---
id: 0008
title: Configure branch protection and auto-merge
status: closed
priority: p2
type: chore
labels: [build, chore, github]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
---

## Summary

Turn on branch protection rules on `main` so nothing lands without a PR and
green CI, and enable GitHub-managed auto-merge. Human approval is not required
because this is a solo project.

## Context

The review process (`docs/REVIEW.md`) documents the expected settings.
Without branch protection, "the process" is a suggestion. This issue makes
it enforceable.

## Acceptance criteria

The canonical list of settings lives in `docs/REVIEW.md` § "Branch
protection". This issue is the mechanical checklist to reach that state:

- [x] Open GitHub → **Settings → Rules → Rulesets** (or the legacy
      Settings → Branches → Branch protection rules UI, whichever the
      account currently exposes) for the `main` branch
- [x] Enable every setting listed in `docs/REVIEW.md` § "Branch
      protection"
- [x] Enable **Allow auto-merge** under GitHub → Settings → General →
      Pull Requests
- [x] Keep **Allow squash merging** enabled
- [x] Verify that the three required status checks are selectable in the
      UI — they only appear after CI has run at least once, and their
      names must match the job names in `.github/workflows/ci.yml`
      (`Build`, `Test & coverage`, `Format check`)
- [x] Verify in the ruleset summary that the rule targets `main`, has no
      bypass actors, and requires the three checks above
- [x] Open a disposable documentation PR, run
      `gh pr merge --auto --squash`, and confirm GitHub accepts the
      auto-merge request

If `docs/REVIEW.md` and the resulting GitHub configuration diverge, the
doc is wrong: update it in the same PR that fixes the configuration.

## Notes

- The required status-check names must match the job names in
  `.github/workflows/ci.yml`. If we rename a job later, update the rule.
- CI needs to have run at least once before those checks appear as
  selectable options in the settings UI. First push already triggered it,
  so they should be visible.
- If GitHub prompts to choose between "Branch protection rules" (legacy) and
  "Rulesets" (new), pick Rulesets — it's what GitHub is standardising on.

## Resolution

Repository ruleset `main` (ID `23648449`) now targets the default branch and
requires a pull request, squash merge, up-to-date branches, linear history,
and the `Build`, `Test & coverage`, and `Format check` jobs. It has no bypass
actors and requires no human approval. Repository auto-merge is enabled.

PR #2 verified the complete flow: GitHub held the merge until all three jobs
passed, then squash-merged automatically. Post-merge CI on `main` also passed.
