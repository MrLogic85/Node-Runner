# File issue migration

GitHub Issues became the source of truth on 2026-09-18.

The old file-based tracker was moved losslessly under `issues/archive/` so the
original Markdown files remain available but read-only. Open file issues were
migrated to matching GitHub Issues. Closed historical file issues were kept as
archive-only records instead of being recreated as closed GitHub Issues, to
avoid noisy synthetic notifications and duplicate history.

## Migrated open issues

| Legacy id | GitHub Issue | Archived file |
|-----------|--------------|---------------|
| 0002 | #10 | `issues/archive/open/0002-configure-android-export.md` |
| 0003 | #11 | `issues/archive/open/0003-implement-neural-network.md` |
| 0004 | #12 | `issues/archive/open/0004-hardcoded-worm-creature.md` |
| 0005 | #13 | `issues/archive/open/0005-wire-brain-and-randomize-button.md` |
| 0009 | #14 | `issues/archive/open/0009-enable-copilot-code-review.md` |
| 0012 | #15 | `issues/archive/open/0012-preserve-test-results-per-project.md` |
| 0015 | #16 | `issues/archive/open/0015-pin-ci-runner-and-update-actions.md` |
| 0016 | #17 | `issues/archive/open/0016-migrate-file-issues-to-github-issues.md` |

## Verification notes

- GitHub labels from `docs/ISSUE_LABELS.md` were created before issue import.
- Milestone `v0.1` was created and assigned to migrated open issues.
- Issue-review-by-URL/body was verified on #11, and the review result was
  recorded as a GitHub Issue comment.
- The PR title gate was updated to accept GitHub issue numbers such as `#15`
  instead of legacy four-digit file ids.

## Archived closed issues

The following legacy issues were already closed before migration and remain as
archive-only records:

- `issues/archive/closed/0001-bootstrap-godot-csharp-project.md`
- `issues/archive/closed/0006-set-up-xunit-tests.md`
- `issues/archive/closed/0007-set-up-github-repository.md`
- `issues/archive/closed/0008-configure-branch-protection.md`
- `issues/archive/closed/0010-own-pull-request-through-merge.md`
- `issues/archive/closed/0011-fix-format-check-baseline.md`
- `issues/archive/closed/0013-define-manual-testing-decision-process.md`
- `issues/archive/closed/0014-enforce-semantic-pr-title.md`
- `issues/archive/closed/0017-document-github-issue-labels.md`
- `issues/archive/closed/0018-document-github-issue-review-flow.md`
