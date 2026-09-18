# Issues

A dead-simple file-based issue tracker. No GitHub required. Grep-friendly.

## Layout

```
issues/
├── README.md            (this file)
├── open/                active issues
│   ├── 0001-....md
│   ├── 0002-....md
│   └── ...
└── closed/              done / wontfix (never deleted, always moved here)
```

## File naming

`NNNN-short-kebab-title.md`

- `NNNN` = zero-padded 4-digit id, monotonically increasing. Never reused.
- Title is a slug: lower-case, hyphens, no punctuation.

Example: `0007-creature-falls-through-floor.md`

To find the next id: look at the highest-numbered file across **both** `open/`
and `closed/` and add one.

## File format

Each issue is a Markdown file with YAML front-matter:

```markdown
---
id: 0007
title: Creature falls through floor at high time scale
status: open            # open | in-progress | blocked | closed | wontfix
priority: p2            # p0 (drop everything) | p1 | p2 | p3
type: bug               # bug | feature | chore | question | spike
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

## Workflow

1. **Create.** Add a new file in `issues/open/`. Fill front-matter and
   Summary. Acceptance criteria may start rough — sharpen before you start
   coding.
2. **Start.** Change `status: in-progress`. Update `updated:` date.
3. **Block.** If stuck on someone/something else, `status: blocked` and write
   *what* you're blocked on in Notes.
4. **Close.** Move the file to `issues/closed/`. Change `status: closed` (or
   `wontfix`). Add a `## Resolution` section at the bottom describing what was
   done and referencing the commit(s).

Never delete an issue file. Historical record matters.

## Priorities

- **p0** — Broken build / demo. Fix now.
- **p1** — Blocks the next roadmap version.
- **p2** — Should be done for the next version but has workarounds.
- **p3** — Nice to have / polish / backlog.

## Types

- **bug** — Something is wrong.
- **feature** — New user-visible capability.
- **chore** — Infrastructure, refactor, docs, tooling.
- **question** — Open design decision needing discussion.
- **spike** — Time-boxed investigation to reduce uncertainty. Output is
  usually another issue.

## Labels

Free-form, but keep them short and reused. Suggested starter set:

- Areas: `ml`, `ga`, `backprop`, `creature`, `sim`, `ui`, `android`, `physics`,
  `visualization`, `docs`, `build`
- Versions: `v0.1`, `v1.0`, `v1.5`, `v2.0`, `v3.0`, `backlog`
- Meta: `good-first`, `research`, `pedagogical`

## Searching

```bash
# All open p1 issues
grep -l "priority: p1" issues/open/*.md

# All ML-related issues, open or closed
grep -l "labels:.*\bml\b" issues/**/*.md

# Issues targeting v1.0
grep -l "version: v1.0" issues/**/*.md
```
