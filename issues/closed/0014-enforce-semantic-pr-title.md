---
id: 0014
title: Enforce semantic PR title format
status: closed
priority: p2
type: chore
labels: [build, ci, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
---

## Summary

Require PR titles to follow `type(#NNNN): Description`, so squash commits on
`main` use a consistent issue-scoped format.

## Context

History currently mixes plain-text squash commit subjects with conventional
commit-style subjects. Since we squash PRs and use the PR title as the final
commit subject, the PR title is the right enforcement point.

## Acceptance criteria

- [x] CI validates PR titles with `amannn/action-semantic-pull-request`
- [x] Valid title example: `ci(#0014): Add semantic PR title check`
- [x] Missing issue scope fails
- [x] Unknown type fails
- [x] Lowercase description fails
- [x] Branch protection requires the `PR title` check
- [x] `docs/REVIEW.md` owns the format rule
- [x] PR template points at the format without owning extra rules

## Notes

Configure branch protection after the workflow has landed on `main`, because
the `PR title` check must exist before it can be selected as required.

## Resolution

PR #5 added the `PR title` CI job using
`amannn/action-semantic-pull-request@v6`, documented the
`type(#NNNN): Description` format in `docs/REVIEW.md`, and kept the PR
template as a pointer rather than a second owner.

Ruleset `main` (ID `23648449`) now requires `PR title`, `Build`,
`Test & coverage`, and `Format check`. The title parser was locally checked
against the valid example above and invalid examples for missing scope,
unknown type, and lowercase description.
