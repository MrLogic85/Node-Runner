---
name: review-change
description: Review a Node Runner change with the review agents before it is committed or pushed, and act on their findings. Use before every commit or push, and when asked to review a diff, branch or PR.
---

# Review a change

Rules: `docs/REVIEW.md` → "Choosing reviewers", "Review receipts" and
"Definition of Done".

1. Fix the exact change under review, as "Review receipts" measures it:
   - before the commit: stage it with `git add -A`, or name its paths
     when an untracked file that is not part of it must stay out, then
     `git diff --cached "$(.github/scripts/review-receipt.sh base)"`;
   - after it: `git diff "$(.github/scripts/review-receipt.sh base)" HEAD`;
   - someone else's PR: `gh pr diff N`.
2. Select the reviewers by "Choosing reviewers".
3. Run the selected agents in parallel as subagents, each with this
   prompt, and give `design-lead` the evidence its file asks for:

   ```text
   Review `<diff command>` for <issue URL>.
   Report only problems in your focus worth acting on, judged by this
   repository's rules, not stricter ones of your own. Read the
   `.claude/rules/` files whose `paths` match the changed files. For each: its level
   (Major: wrong behaviour, a wrong statement or a broken rule; Medium: a
   risk or gap worth fixing now; Minor: a small fix), new or preexisting
   (outside the diff), file and lines, evidence, impact and a direction.
   Find C# definitions and callers with the LSP tool when you have it,
   otherwise with git grep. Prefer fixing code to documenting a limit.
   If nothing qualifies, say "No findings".
   ```
4. Judge each finding as "Definition of Done" says: fix it, or dismiss it
   with a short reason. File an issue with the `create-issue` skill only
   where it asks for one.
5. After a fix that changes behaviour or design, repeat steps 1, 3 and 4
   on the new diff with only the reviewers the fix selects ("Review
   receipts").
6. For your own change, record the receipt. Write each reviewer's
   findings and their dispositions, or "No findings", and for each fix
   whether it was re-reviewed or simple, to
   `.scratch/review.md`, stage the change as in step 1, then run:

   ```bash
   .github/scripts/review-receipt.sh write --reviewers docs-reviewer,tests-reviewer .scratch/review.md
   ```

   naming every reviewer that ran. Keep the file for the PR body.
7. Commit and ship it with the `ship-pr` skill.
