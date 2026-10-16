---
name: review-change
description: Review a Node Runner change with the review agents before it is committed or pushed, and act on their findings. Use before every commit or push, and when asked to review a diff, branch or PR.
---

# Review a change

`docs/REVIEW.md` → "Review agents" owns which reviewer a diff selects and
how they report, "Review receipts" owns when review runs and what a push
needs, and "Definition of Done" owns the gate; this skill owns the steps.

1. Fix the exact change under review, as "Review receipts" measures it:
   - before the commit: `git add -A`, then
     `git diff --cached "$(.github/scripts/review-receipt.sh base)"`;
   - after it: `git diff "$(.github/scripts/review-receipt.sh base)" HEAD`;
   - someone else's PR: `gh pr diff N`.
2. Select the reviewers by "Choosing reviewers".
3. Run the selected agents in parallel as subagents. Give each the
   repository path, the exact diff command and the issue URL, and give
   `design-lead` the evidence "Choosing reviewers" names.
4. Act on each finding as "Definition of Done" says, filing any issue it
   asks for with the `create-issue` skill.
5. After a fix, repeat steps 1, 3 and 4 on the new diff with only the
   reviewers the corrective change selects ("Review receipts").
6. For your own change, record the receipt. Write each reviewer's
   findings and their dispositions, or "No findings", to
   `.scratch/review.md`, run `git add -A`, then run:

   ```bash
   .github/scripts/review-receipt.sh write --reviewers docs-reviewer,tests-reviewer .scratch/review.md
   ```

   naming every reviewer that ran. Keep the file for the PR body.
7. Commit and ship it with the `ship-pr` skill.
