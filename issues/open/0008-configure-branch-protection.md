---
id: 0008
title: Configure branch protection on main
status: open
priority: p2
type: chore
labels: [build, chore, github]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Turn on branch protection rules on `main` so nothing lands without a PR,
and green CI. Human approval is not required because this is a solo project.
Cannot be done from the CLI — GitHub web UI action required.

## Context

The review process (`docs/REVIEW.md`) documents the expected settings.
Without branch protection, "the process" is a suggestion. This issue makes
it enforceable.

## Acceptance criteria

The canonical list of settings lives in `docs/REVIEW.md` § "Branch
protection". This issue is the mechanical checklist to reach that state:

- [ ] Open GitHub → **Settings → Rules → Rulesets** (or the legacy
      Settings → Branches → Branch protection rules UI, whichever the
      account currently exposes) for the `main` branch
- [ ] Enable every setting listed in `docs/REVIEW.md` § "Branch
      protection"
- [ ] Verify that the three required status checks are selectable in the
      UI — they only appear after CI has run at least once, and their
      names must match the job names in `.github/workflows/ci.yml`
      (`Build`, `Test & coverage`, `Format check`)
- [ ] Verify in the ruleset summary that the rule targets `main`, has no
      bypass actors, and requires the three checks above

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
