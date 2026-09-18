---
id: 0013
title: Define per-issue manual testing decision process
status: closed
priority: p2
type: chore
labels: [test, docs, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
closed: 2026-09-18
resolution: completed
---

## Summary

Document how manual testing is chosen per issue/change without introducing a
shared smoke-test suite yet.

## Context

Manual testing should be intentional. Some changes need desktop or Android
verification; pure documentation, CI, and well-covered pure-C# logic usually
do not. The process also needs to say when an AI agent can run manual tests
and what evidence it must capture.

## Acceptance criteria

- [x] No generic smoke suite is introduced
- [x] Each issue/PR must decide whether manual testing is required
- [x] The decision criteria are documented
- [x] The PR template records manual test result, evidence, or skip reason
- [x] Agent-run manual tests describe required environment and evidence
- [x] Human-only/manual blockers are explicit

## Resolution

PR #4 implements the process. Commit `1a74daf` adds
`docs/MANUAL_TESTING.md` and updates `docs/REVIEW.md`,
`docs/TEST_STRATEGY.md`, the PR template, and relevant local `AGENTS.md`
files to point to it.
