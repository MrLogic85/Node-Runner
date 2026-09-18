---
id: 0017
title: Document GitHub issue label taxonomy
status: in-progress
priority: p2
type: docs
labels: [docs, github, process]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Document the required and allowed GitHub Issue labels before migrating from
the file-based issue tracker.

## Context

GitHub Issues do not have built-in bug/feature/chore/refactor fields. We want
clean issue titles and metadata in labels, not title prefixes like `[BUG]` or
`[FEAT]`.

## Acceptance criteria

- [ ] Label taxonomy is documented in `docs/ISSUE_LABELS.md`
- [ ] Required label dimensions are explicit
- [ ] Allowed type, priority, area, and optional labels are listed
- [ ] Version tracking uses milestones rather than labels
- [ ] The file-issue migration issue references the taxonomy
- [ ] `issues/README.md` does not maintain a second label list
- [ ] Legacy file labels have deterministic migration mappings
