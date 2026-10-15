---
name: start-issue
description: Start or resume work on a Node Runner GitHub issue by reading its state and Status and taking the route that fits. Use whenever asked to work on, start, pick up or continue an issue.
---

# Start an issue

`docs/ISSUES.md` → "Status" owns what each Status means; this skill owns
what to do about it. Read `LOCAL_CONFIG.md` first for the `gh` login.

1. Read the issue, its comments, fields, parent, sub-issues and blockers:

   ```bash
   gh issue view N
   gh issue view N --comments
   .github/scripts/issue-fields.sh N
   gh api graphql -F n=N -f query='query($n:Int!){repository(owner:"MrLogic85",name:"Node-Runner"){issue(number:$n){state parent{number} subIssues(first:50){nodes{number state title}} blockedBy(first:20){nodes{number state}}}}}'
   ```

2. Take the first route that applies:

   | The issue is | Do |
   |---|---|
   | Closed | Report that it is closed and stop. |
   | A parent (it has sub-issues) | Pick an open sub-issue that is Ready, or In progress without a branch, and start it with this skill; if none is, report the sub-issues' Statuses and stop. |
   | Idea or Needs decision | Ask the human the open question. Once answered, record the answer in the body, set Needs review and route again. If nobody answers, post the question as an issue comment and stop. |
   | Needs design | Run `design-lead` on it, post its proposal on the issue, and ask the human to approve it; the Status that follows is in `docs/ISSUES.md` → "Status". Stop until it is approved. |
   | Needs review | Run the `review-issue` skill. If the Status changed, route again; if it is still Needs review, report the open findings and stop. |
   | Blocked with every blocker closed | Set Ready and route again. |
   | Blocked, or Ready or In progress with an open blocker | Set Blocked if it is not, report the blocker and stop. |
   | Size 8 | Split it into sub-issues (`docs/ISSUES.md` → "Size"), each made with the `create-issue` skill, and stop. |
   | In progress | If it has a branch (`git branch -a --list "*/N-*"`), check the working copy (step 3), `git switch <type>/N-<slug>` and carry on from step 4; otherwise start it as Ready. |
   | Ready | Start it (step 3). |

3. Start it:
   - Check the working copy (`AGENTS.md` → "Working copy"): it must be
     clean and on `main`, or on this issue's branch when resuming. If not,
     stop and ask the human.
   - `.github/scripts/issue-fields.sh N --status "In progress"`.
   - `git pull --ff-only`, then branch with the name in `docs/REVIEW.md`
     → "TL;DR".
4. Implement, then run the `review-change` skill and the `ship-pr` skill.
