---
name: ship-pr
description: Commit, push and open a Node Runner pull request, enable squash auto-merge and own it until it has merged. Use when a reviewed change is ready to commit, push or open as a PR.
---

# Ship a PR

`docs/REVIEW.md` owns the rules this follows: "Definition of Done",
"Review receipts", "Auto-merge" and "PR title and commit hygiene". A
release's PRs also follow the `release-milestone` or `fix-release`
skill. Read `LOCAL_CONFIG.md` first for the `gh` login and push remote.

1. Check every "Definition of Done" box, including the `review-change`
   skill run on the final diff and the manual testing decision
   (`docs/MANUAL_TESTING.md`; the `plan-manual-tests` skill).
2. Commit with the PR title as the subject and the `Co-authored-by:`
   trailers "PR title and commit hygiene" asks for.
3. Push the branch: `git push -u origin <branch>`. If the hook denies it
   for its receipt, run the `review-change` skill again ("Review
   receipts").
4. Write the body from `.github/pull_request_template.md` to
   `.scratch/pr.md`, with everything the "Definition of Done" asks the PR
   to say and "Notes for the reviewer" from the `review-change` skill's
   `.scratch/review.md`, then open the PR, enable auto-merge and delete
   both files:

   ```bash
   gh pr create --title "type(#N): Description" --body-file .scratch/pr.md
   gh pr merge --auto --squash
   ```

   If auto-merge cannot be enabled, do what "Auto-merge" says.

5. Until it merges, watch the checks (`gh pr checks --watch`) and the
   comments, both the PR's (`gh pr view --comments`) and the inline ones
   (`gh api repos/MrLogic85/Node-Runner/pulls/N/comments`). Answer each
   as "Auto-merge" says; run `review-change` on each fix, and push again.
6. Wait until `gh pr view N --json state -q .state` says `MERGED`; auto-merge
   lands a little after the checks pass. Then wait for the run on its merge
   commit, which appears a little later still, and watch it to the end:

   ```bash
   sha=$(gh pr view N --json mergeCommit -q .mergeCommit.oid)
   until id=$(gh run list --commit "$sha" --limit 1 --json databaseId -q '.[0].databaseId') && [ -n "$id" ]; do sleep 10; done
   gh run watch "$id" --exit-status
   ```

   If it fails, report it and fix it forward with a new issue and PR. Then
   `git switch main && git pull --ff-only`, and delete the branch
   (`git branch -D`, since the squash commit is not its history).
7. If the issue has a parent, set the parent's Status by
   `docs/ISSUES.md` → "Parent issues".
