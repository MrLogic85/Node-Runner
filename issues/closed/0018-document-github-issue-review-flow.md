---
id: 0018
title: Document GitHub Issue review flow
status: closed
priority: p2
type: docs
labels: [docs, github, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
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

- [x] `docs/ISSUE_REVIEW.md` defines when issue review is needed
- [x] Review can run from a GitHub Issue URL or pasted issue text
- [x] Review checks scope, acceptance criteria, ownership, dependencies, test
      plan, risk, and missing context
- [x] Status labels support `needs-review`, `needs-decision`, and `ready`
- [x] Review results live in GitHub Issue comments after migration
- [x] Migration issue #0016 requires documenting and verifying the flow

## Resolution

PR #9 implements the issue-review flow. Commit `4bfa037` adds
`docs/ISSUE_REVIEW.md`, extends the status-label taxonomy, and updates issue
#0016 so the GitHub Issues migration must verify URL/body-based issue review.
