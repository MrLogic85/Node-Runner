---
id: 0010
title: Make PR authors own changes through merge
status: closed
priority: p2
type: chore
labels: [chore, github, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
---

## Summary

Define who owns a pull request after it is pushed and how a green change is
merged without depending on a local process remaining alive.

## Context

The review process defined CI as the merge gate but did not assign
responsibility for watching CI, fixing failures, enabling merge, or verifying
the result.

## Acceptance criteria

- [x] The PR author owns the change from implementation through merge
- [x] CI failures remain the author's responsibility
- [x] GitHub-managed squash auto-merge is the default waiting mechanism
- [x] Corrective code changes trigger relevant validation and code review
- [x] Completion requires verifying the merge and the health of `main`
- [x] GitHub setup tracks enabling auto-merge

## Resolution

Implemented by commit `0b1c19f` on branch
`chore/pr-ownership-auto-merge`. `docs/REVIEW.md` now owns the full PR
lifecycle, and issue #0008 includes the required repository settings.
