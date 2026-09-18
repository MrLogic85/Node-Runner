---
id: 0013
title: Define per-issue manual testing decision process
status: in-progress
priority: p2
type: chore
labels: [test, docs, review]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
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

- [ ] No generic smoke suite is introduced
- [ ] Each issue/PR must decide whether manual testing is required
- [ ] The decision criteria are documented
- [ ] The PR template records manual test result, evidence, or skip reason
- [ ] Agent-run manual tests describe required environment and evidence
- [ ] Human-only/manual blockers are explicit
