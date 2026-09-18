---
id: 0014
title: Enforce semantic PR title format
status: in-progress
priority: p2
type: chore
labels: [build, ci, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Require PR titles to follow `type(#NNNN): Description`, so squash commits on
`main` use a consistent issue-scoped format.

## Context

History currently mixes plain-text squash commit subjects with conventional
commit-style subjects. Since we squash PRs and use the PR title as the final
commit subject, the PR title is the right enforcement point.

## Acceptance criteria

- [ ] CI validates PR titles with `amannn/action-semantic-pull-request`
- [ ] Valid title example: `ci(#0014): Add semantic PR title check`
- [ ] Missing issue scope fails
- [ ] Unknown type fails
- [ ] Lowercase description fails
- [ ] Branch protection requires the `PR title` check
- [ ] `docs/REVIEW.md` owns the format rule
- [ ] PR template points at the format without owning extra rules

## Notes

Configure branch protection after the workflow has landed on `main`, because
the `PR title` check must exist before it can be selected as required.
