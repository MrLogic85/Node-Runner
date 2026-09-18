---
id: 0017
title: Document GitHub issue label taxonomy
status: closed
priority: p2
type: docs
labels: [docs, github, process]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
---

## Summary

Document the required and allowed GitHub Issue labels before migrating from
the file-based issue tracker.

## Context

GitHub Issues do not have built-in bug/feature/chore/refactor fields. We want
clean issue titles and metadata in labels, not title prefixes like `[BUG]` or
`[FEAT]`.

## Acceptance criteria

- [x] Label taxonomy is documented in `docs/ISSUE_LABELS.md`
- [x] Required label dimensions are explicit
- [x] Allowed type, priority, area, and optional labels are listed
- [x] Version tracking uses milestones rather than labels
- [x] The file-issue migration issue references the taxonomy
- [x] `issues/README.md` does not maintain a second label list
- [x] Legacy file labels have deterministic migration mappings

## Resolution

PR #9 implements the taxonomy. Commit `4bfa037` adds
`docs/ISSUE_LABELS.md`, updates the file-issue migration criteria, and reduces
`issues/README.md` to a pointer so the label list has one owner.
