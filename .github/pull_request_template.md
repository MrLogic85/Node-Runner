<!--
PR template. Delete sections that don't apply. The Definition of Done is
owned by docs/REVIEW.md; do not copy it into this template.
-->

## What & why

<!-- 1–3 sentences. What does this PR do, and why? Link the issue. -->

Closes `issues/closed/<NNNN>-<slug>.md`

## Scope

<!-- Bullet the concrete changes. If a change isn't in this list, split it out. -->

-

## How to verify

<!-- The exact commands / steps a reviewer runs to check this. Fill in. -->

```bash
dotnet build NodeRunner.slnx
dotnet test  NodeRunner.slnx
```

Manual testing decision (`docs/MANUAL_TESTING.md`):

- Required? <!-- yes/no + why -->
- If required, result/evidence:
- If skipped, reason:

## Screenshots / recordings

<!-- Drop into the PR description; delete this section if not applicable. -->

## Definition of Done

Confirm that [`docs/REVIEW.md`](docs/REVIEW.md#definition-of-done) is
satisfied. Explain any item that does not apply in **Notes for the reviewer**.

## Notes for the reviewer

<!-- Anything worth calling out: shortcuts taken, follow-ups filed, design
     alternatives considered and rejected. -->
