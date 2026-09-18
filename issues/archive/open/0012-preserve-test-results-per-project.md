---
id: 0012
title: Preserve CI test results per project
status: open
priority: p2
type: chore
labels: [build, test, ci]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Prevent test projects from overwriting each other's TRX result file in CI.

## Context

The solution-wide test command uses one fixed
`TestResults/test-results.trx` name. Each project overwrites the previous
project's output, so the uploaded artifact contains incomplete diagnostics.

## Acceptance criteria

- [ ] Every test project writes a distinct TRX result
- [ ] The CI artifact contains results from all test projects
- [ ] Test and coverage behavior remains unchanged
