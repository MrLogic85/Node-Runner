---
name: review-change
description: Review a Node Runner change with the review agents before it is committed or pushed, and act on their findings. Use before every commit or push, and when asked to review a diff, branch or PR.
---

# Review a change

Rules: `docs/REVIEW.md` → "Choosing reviewers", "Review receipts" and
"Definition of Done".

1. Fix the exact change under review, as "Review receipts" measures it:
   - Before the commit: `git add -A`, or name its paths when an untracked
     file that is not part of it must stay out, then
     `git diff --cached "$(.github/scripts/review-receipt.sh base)"`.
   - After it: `git diff "$(.github/scripts/review-receipt.sh base)" HEAD`.
   - Someone else's PR: `gh pr diff N`.
2. Select the reviewers by "Choosing reviewers"; skip the others.
3. Run them in parallel as subagents with this prompt:

   ```text
   Review `<diff command>` for <issue URL>. Reply with findings only.
   ```

   Run `design-lead` after the others finish ("Choosing reviewers"), and
   replace `Reply with findings only.` with `The phone is <free|busy>.`
   Free: claim any device lock before `design-lead` starts and release
   it when it returns.
4. Judge each finding per "Definition of Done": fix it, or dismiss it
   with a short reason. File an issue with the `create-issue` skill only
   where it asks for one.
5. A fix changes behaviour or design: repeat steps 1, 3 and 4 on the new
   diff with only the reviewers the fix selects ("Review receipts").
6. Your own change only: write the receipt's file, `.scratch/review.md`,
   one line per finding, or per reviewer without findings, naming every
   reviewer that ran:

   ```text
   **<reviewer>**: No findings
   **<reviewer>**, <level>: <finding> — <fixed, re-reviewed | fixed, simple | dismissed: <reason>>
   ```

   Stage the change as in step 1, record the receipt and keep the file
   for the PR body:

   ```bash
   .github/scripts/review-receipt.sh write --reviewers docs-reviewer,tests-reviewer .scratch/review.md
   ```

7. Commit and ship it with the `ship-pr` skill.
