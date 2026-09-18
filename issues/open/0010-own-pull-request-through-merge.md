---
id: 0010
title: Make PR authors own changes through merge
status: in-progress
priority: p2
type: chore
labels: [chore, github, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Define who owns a pull request after it is pushed and how a green change is
merged without depending on a local process remaining alive.

## Context

The review process defined CI as the merge gate but did not assign
responsibility for watching CI, fixing failures, enabling merge, or verifying
the result.

## Acceptance criteria

- [ ] The PR author owns the change from implementation through merge
- [ ] CI failures remain the author's responsibility
- [ ] GitHub-managed squash auto-merge is the default waiting mechanism
- [ ] Corrective code changes trigger relevant validation and code review
- [ ] Completion requires verifying the merge and the health of `main`
- [ ] GitHub setup tracks enabling auto-merge
