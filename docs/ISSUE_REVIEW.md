# Issue review

GitHub Issues are the source of truth after the migration from file issues.
Issue review happens by reviewing the GitHub Issue text or URL directly — not
by creating a PR just to edit an issue.

Apply the required label rules in `docs/ISSUE_LABELS.md` when creating,
updating, or reviewing an issue.

## When to review an issue

Trivial issues can be created and worked directly. Review an issue before
implementation when it is:

- Broad enough that it might need splitting
- Affects architecture, persistence, ML behavior, physics, UI flow, CI, or
  release process
- Ambiguous about expected behavior or acceptance criteria
- Likely to need manual testing
- Marked with `status: needs-review` or `status: needs-decision`

After review, replace `status: needs-review` with `status: ready` when the
issue is actionable. Keep `status: needs-decision` until the open decision is
settled.

## How to run issue review

Give the review agent either the GitHub Issue URL or the full issue text.
Ask it to check:

- Scope: one clear problem/outcome, or a suggested split
- Acceptance criteria: concrete, testable, and not implementation-biased
- Ownership: labels follow `docs/ISSUE_LABELS.md`, with the correct milestone
  and linked docs
- Dependencies: blocked-by/follow-up relationships are explicit
- Test plan: whether automated or manual testing is expected
- Risk: architecture, data loss, determinism, performance, Android/device
  behavior, and UX surprises
- Missing context: screenshots, logs, seed/settings, reproduction steps, or
  design references

The reviewer reports findings as **Major**, **Medium**, or **Minor**. Major
findings mean the issue should not be implemented yet. Medium findings should
usually be fixed before work starts. Minor findings can be fixed opportunistically.

## Where review results live

For GitHub Issues, put the review summary in an issue comment. If a review
changes the issue materially, edit the issue body and add a short comment
summarizing what changed.

Archived file issues under `issues/archive/` are read-only historical records.
Do not append review notes there; comment on the corresponding GitHub Issue.
