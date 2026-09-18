---
id: 0016
title: Migrate file-based issue tracker to GitHub Issues
status: open
priority: p2
type: chore
labels: [chore, github, docs]
version: v0.1
created: 2026-09-18
updated: 2026-09-18
---

## Summary

Move the current file-based issue tracker under `issues/` to GitHub Issues at
https://github.com/MrLogic85/Node-Runner/issues.

## Context

The project started with a file-based issue tracker so work could begin before
GitHub was configured. GitHub repository setup, branch protection, PR title
checks, and auto-merge now exist, so the native GitHub Issues workflow can
become the issue source of truth.

The migration must preserve useful history from existing issue files while
avoiding duplicate long-term ownership between GitHub Issues and `issues/`.

## Acceptance criteria

- [ ] Inventory every file in `issues/open/` and `issues/closed/`
- [ ] Create matching GitHub Issues for every open file issue
- [ ] Create or update GitHub labels according to `docs/ISSUE_LABELS.md`
- [ ] Preserve id, title, status, priority, type, labels, version, created,
      updated, closed, context, acceptance criteria, and relevant notes in the
      GitHub Issue body or immutable archive manifest
- [ ] Decide how closed historical file issues are represented in GitHub
      Issues: migrate as closed issues, keep as archive, or summarize in one
      migration issue
- [ ] Add links between migrated GitHub Issues and their original file issue
      paths
- [ ] Update `AGENTS.md`, `docs/REVIEW.md`, `.github/pull_request_template.md`,
      and `issues/README.md` so GitHub Issues are the source of truth
- [ ] Update `docs/ISSUE_LABELS.md` if migration reveals a needed label that is
      not already allowed
- [ ] Document GitHub Issue review flow from `docs/ISSUE_REVIEW.md` in
      `AGENTS.md`, `docs/REVIEW.md`, and the migration notes
- [ ] Preserve every existing issue file in an immutable archive, either by
      keeping `issues/` read-only or moving it losslessly to a documented
      archive path
- [ ] Update the `PR title` workflow, `docs/REVIEW.md`, and PR template so
      PR titles and closing references use GitHub issue numbers instead of
      four-digit file issue ids
- [ ] Verify end to end that a migrated GitHub issue can be referenced by a PR
      title and closing keyword without failing CI
- [ ] Verify end to end that a non-trivial GitHub Issue can be reviewed by
      passing its URL or body to a review agent, with results recorded as an
      issue comment

## Notes

Do not delete file-based history. Migration may move files only if the new
location preserves each issue file's content and commit history.
