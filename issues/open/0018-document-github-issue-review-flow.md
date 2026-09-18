---
id: 0018
title: Document GitHub Issue review flow
status: in-progress
priority: p2
type: docs
labels: [docs, github, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Document how non-trivial GitHub Issues are reviewed without returning to
file-based issue PRs.

## Context

Moving issues to GitHub loses the implicit review that came from changing
issue files in pull requests. We still want issue quality for larger work, but
the review should operate on the GitHub Issue URL or body and write results
back as issue comments.

## Acceptance criteria

- [ ] `docs/ISSUE_REVIEW.md` defines when issue review is needed
- [ ] Review can run from a GitHub Issue URL or pasted issue text
- [ ] Review checks scope, acceptance criteria, ownership, dependencies, test
      plan, risk, and missing context
- [ ] Status labels support `needs-review`, `needs-decision`, and `ready`
- [ ] Review results live in GitHub Issue comments after migration
- [ ] Migration issue #0016 requires documenting and verifying the flow
