---
id: 0011
title: Fix the format-check baseline
status: closed
priority: p1
type: chore
labels: [build, test, ci]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
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

- [x] The naming violations are corrected without weakening
      `.editorconfig`
- [x] `dotnet format NodeRunner.slnx --verify-no-changes --no-restore`
      exits successfully
- [x] Build and tests remain green

## Notes

Complete this before enabling the required `Format check` in issue #0008.

## Resolution

Commit `d7f6857` renamed the three private static assembly fields to match the
configured `_camelCase` convention. The CI-equivalent format command, solution
build, and all five architecture tests pass locally.
