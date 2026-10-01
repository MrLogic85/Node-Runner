# Issue review

GitHub Issues are the source of truth after the migration from file issues.
Issue review happens by reviewing the GitHub Issue text or URL directly — not
by creating a PR just to edit an issue.

Apply the label and project-field rules in `docs/ISSUE_LABELS.md` when
creating, updating, or reviewing an issue. Status below means the project
Status field.

## When to review an issue

Trivial issues can be created and worked directly. Review an issue before
implementation when it is:

- Broad enough that it might need splitting
- Affects architecture, persistence, ML behavior, physics, UI flow, CI, or
  release process
- Ambiguous about expected behavior or acceptance criteria
- Likely to need manual testing
- In Status **Needs review** or **Needs decision**

**Idea** issues are not actionable and are not reviewed or implemented yet.
Shape them first through discussion; once the issue states one clear outcome
and acceptance criteria, set Status to **Needs review**.

**Needs design** issues wait for the design to land in `reference design/`;
then set Status to **Needs review**. Go straight to **Ready** only if the
issue already passed review and the new design does not change its scope or
acceptance criteria.

After review, set Status to **Ready** when the issue is actionable, or
**Blocked** when it waits on another issue. Keep **Needs decision** until the
open decision is settled. When an issue's last open blocked-by issue closes
and nothing else (a person or an external tool) still blocks it, move it from
**Blocked** to **Ready**.

Size set before review is a first estimate. Review confirms or corrects it; an
issue with Size 8 is split into sub-issues before it can become **Ready**.

## How to run issue review

Give the review agent either the GitHub Issue URL or the full issue text.
Ask it to check:

- Scope: one clear problem/outcome, or a suggested split
- Acceptance criteria: concrete, testable, and not implementation-biased
- Ownership: labels and project fields follow `docs/ISSUE_LABELS.md`, with the
  correct milestone and linked docs
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
