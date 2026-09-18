---
id: 0011
title: Fix the format-check baseline
status: open
priority: p1
type: chore
labels: [build, test, ci]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Make the existing `Format check` CI job pass before it becomes a required
branch-protection check.

## Context

`dotnet format NodeRunner.slnx --verify-no-changes --no-restore` currently
reports `IDE1006` for private static fields in
`tests/NodeRunner.Arch.Tests/ArchitectureSpec.cs`. Requiring this check while
the baseline is red would block every merge.

## Acceptance criteria

- [ ] The naming violations are corrected without weakening
      `.editorconfig`
- [ ] `dotnet format NodeRunner.slnx --verify-no-changes --no-restore`
      exits successfully
- [ ] Build and tests remain green

## Notes

Complete this before enabling the required `Format check` in issue #0008.
