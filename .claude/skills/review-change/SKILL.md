---
name: review-change
description: Review a Node Runner change with the review agents before it is committed or pushed, and act on their findings. Use before every commit or push, and when asked to review a diff, branch or PR.
---

# Review a change

`docs/REVIEW.md` → "Review agents" owns which reviewer a diff selects and
how they report, and "Definition of Done" owns the gate; this skill owns
the steps.

1. Fix the exact change under review, measured from where the branch
   left `main`:
   - before the commit: `git add -A`, then
     `git diff --cached "$(git merge-base main HEAD)"`;
   - after it: `git diff main...HEAD`;
   - someone else's PR: `gh pr diff N`.
2. Select the reviewers by "Choosing reviewers".
3. Run the selected agents in parallel as subagents. Give each the
   repository path, the exact diff command and the issue URL, and give
   `design-lead` the evidence "Choosing reviewers" names.
4. Act on each finding:
   - **new**: fix it, or dismiss it with a written reason for the PR's
     "Notes for the reviewer";
   - **preexisting**: file it with the `create-issue` skill;
   - `design-lead`'s **insufficient evidence** blocks until the
     "Definition of Done" says it may pass.
5. Run the reviewers that the corrective change selects again, on the
   new diff, until no new finding is left unaddressed.
6. For your own change, commit and ship it with the `ship-pr` skill.
