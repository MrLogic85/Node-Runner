# Issues archive

GitHub Issues are the source of truth for active work:
<https://github.com/MrLogic85/Node-Runner/issues>.

This directory is a read-only archive of the file-based tracker that existed
before GitHub Issues were adopted. Do not create, edit, close, or delete issue
files here. Update the corresponding GitHub Issue instead.

See [`MIGRATION.md`](MIGRATION.md) for the mapping from legacy file issues to
GitHub Issues and the decision for closed historical issues.

## Archived layout

```
issues/archive/
├── open/                file issues that were open at migration time
└── closed/              file issues that were already closed before migration
```

## Historical file format

Archived files keep their original Markdown front matter:

`NNNN-short-kebab-title.md`

- `NNNN` = zero-padded 4-digit id, monotonically increasing. Never reused.
- Title is a slug: lower-case, hyphens, no punctuation.

Example front matter:

```markdown
---
id: 0007
title: Creature falls through floor at high time scale
status: open            # open | in-progress | blocked | closed | wontfix
priority: p2            # p0 (drop everything) | p1 | p2 | p3
type: bug               # bug | feature | chore | refactor | docs | test | question | spike
labels: [physics, v1.0]
version: v1.0           # target roadmap version, or "backlog"
created: 2026-09-16
updated: 2026-09-16
---

## Summary

One paragraph: what is wrong / what do we want.

## Context

Why does this matter? Link to `docs/ROADMAP.md` section, other issues, etc.

## Acceptance criteria

- [ ] Concrete, testable condition 1
- [ ] Concrete, testable condition 2

## Notes

Investigation notes, design sketches, open questions. Append as you learn.
```

## Searching the archive

```bash
# File issues that were open at migration time with p1 priority
grep -l "priority: p1" issues/archive/open/*.md

# All archived ML-related issues
grep -l "labels:.*\bml\b" issues/archive/**/*.md

# Archived issues targeting v1.0
grep -l "version: v1.0" issues/archive/**/*.md
```
