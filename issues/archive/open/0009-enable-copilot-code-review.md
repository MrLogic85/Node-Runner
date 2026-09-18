---
id: 0009
title: Enable Copilot code review on pull requests
status: open
priority: p3
type: chore
labels: [chore, github, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Turn on GitHub's built-in Copilot code review for every PR against `main`.
It's a free advisory reviewer that catches boring bugs and style drift
before the human sees them. CLI cannot configure this — GitHub web UI
action required.

## Context

`docs/REVIEW.md` § "AI reviewer" already describes how we treat
AI-generated findings (advisory, human decides which to action). This
issue makes that AI reviewer actually run.

## Acceptance criteria

- [ ] On GitHub → **Settings → Code review** (or under the repo's
      Copilot settings): enable **Copilot code review** for pull requests
- [ ] Add `copilot-pull-request-reviewer[bot]` as an automatic reviewer
      for all PRs against `main` (or configure equivalent auto-request)
- [ ] Verify by opening a trivial PR (README typo) and confirming Copilot
      posts a review within a few minutes

## Notes

- Copilot review remains advisory and is not a required status check.
