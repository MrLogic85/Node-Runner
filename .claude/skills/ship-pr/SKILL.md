---
name: ship-pr
description: Commit, push and open a Node Runner pull request, enable squash auto-merge, and follow up your open PRs and main's runs. Use when a reviewed change is ready to commit, push or open as a PR, and when a task starts or the session ends.
---

# Ship a PR

Rules: `docs/REVIEW.md`. A release's PRs also follow the
`release-milestone` or `fix-release` skill. Read `LOCAL_CONFIG.md` first
for the `gh` login and push remote.

1. Check every "Definition of Done" box but the required checks, which
   the ruleset waits for. No manual testing decision: make one with the
   `plan-manual-tests` skill.
2. Commit with the PR title as the subject and the `Co-authored-by:`
   trailers "PR title and commit hygiene" asks for.
3. `git push -u origin <branch>`. Denied by the hook for its receipt:
   run the `review-change` skill again ("Review receipts").
4. Write `.scratch/pr.md` from `.github/pull_request_template.md`, with
   all that "Definition of Done" asks the PR to say, and "Notes for the
   reviewer" from `.scratch/review.md`. Open the PR, enable auto-merge
   and delete both files:

   ```bash
   gh pr create --title "type(#N): Description" --body-file .scratch/pr.md
   gh pr merge --auto --squash
   ```

   Auto-merge cannot be enabled: do what "Auto-merge" says.
5. Don't wait for the checks or the merge. Delete the local branch (the
   remote keeps it) and go on to the next task:
   `git switch main && git branch -D <branch>`. A PR into
   `release/vX.Y`: stay on it, as the `fix-release` skill says.
6. When the next task starts and before the session ends, follow up the
   PRs you opened ("Auto-merge"). A new session finds them in the recent
   list; the human's PRs share the login, so ask if one is unclear. For
   each PR `P`:

   ```bash
   gh pr list --author @me --state all --limit 10 --json number,state,headRefName,title
   gh pr view P --json state,statusCheckRollup,comments,reviews
   gh api repos/MrLogic85/Node-Runner/pulls/P/comments
   gh run list --branch main --limit 1 --json conclusion,displayTitle
   ```

   - Open, with a failing check or an unanswered comment: answer it per
     "Auto-merge": `git fetch origin <branch> && git switch <branch>`,
     fix it, run `review-change`, push again, then step 5 again.
   - Merged, and its issue has a parent: set the parent's Status per
     `docs/ISSUES.md` → "Parent issues".
   - The latest run on `main` failed: report it and fix it forward with
     a new issue and PR.
