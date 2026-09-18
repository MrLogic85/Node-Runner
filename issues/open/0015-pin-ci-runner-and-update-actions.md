---
id: 0015
title: Pin CI runner and update GitHub Actions versions
status: open
priority: p2
type: chore
labels: [build, ci, maintenance]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Remove current GitHub Actions infrastructure warnings by pinning the runner
image and updating actions when Node 24-based versions are available.

## Context

CI currently passes, but GitHub annotates runs with:

- `actions/checkout@v4`, `actions/setup-dotnet@v4`, and
  `actions/upload-artifact@v4` target Node.js 20 and are being forced to run
  on Node.js 24.
- `ubuntu-latest` will migrate to Ubuntu 26 beginning 2026-10-19.

These are not product failures, but they reduce reproducibility and will
become noisy once the pipeline grows to include Android export.

## Acceptance criteria

- [ ] All CI jobs use `ubuntu-24.04`, not `ubuntu-latest`
- [ ] For each action in `.github/workflows/ci.yml`, use the latest major
      version whose release notes state Node 24 support
- [ ] If an action has no Node 24-compatible major version yet, keep the
      current major version and record that decision in this issue's
      Resolution
- [ ] `Build`, `Test & coverage`, `Format check`, and `PR title` still pass
- [ ] Branch-protection required-check names remain unchanged

## Notes

Do not change required-check names unless the branch protection ruleset is
updated in the same PR.
