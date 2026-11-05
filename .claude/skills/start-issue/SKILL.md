---
name: start-issue
description: Start or resume work on a Node Runner GitHub issue by reading its state and Status and taking the route that fits. Use whenever asked to work on, start, pick up or continue an issue.
---

# Start an issue

Rules: `docs/ISSUES.md` → "Status". Read `LOCAL_CONFIG.md` first for the
`gh` login.

1. Read the issue, its comments, fields, parent, sub-issues and blockers:

   ```bash
   gh issue view N
   gh issue view N --comments
   .github/scripts/issue-fields.sh N
   gh api graphql -F n=N -f query='query($n:Int!){repository(owner:"MrLogic85",name:"Node-Runner"){issue(number:$n){state parent{number} subIssues(first:50){nodes{number state title}} blockedBy(first:20){nodes{number state}}}}}'
   ```

2. Read it critically; agents write most issues. Something looks wrong
   or you disagree: tell the human why and ask before you route. No
   answer: post it as an issue comment and stop.
3. Take the first route that applies:

   | The issue is | Do |
   |---|---|
   | Closed | Reply `#N is closed` and stop. |
   | A parent (has sub-issues) | Start an open sub-issue that is Ready, or In progress without a branch, with this skill. None: reply with the sub-issues' Statuses and stop. |
   | Idea or Needs decision | Ask the human the open question. Answered: record it in the body, set Needs review, route again. No answer: post the question as an issue comment and stop. |
   | Needs design | Run `design-lead` with "Design" and the issue, post its proposal on the issue, ask the human to approve it and stop. Approved: set the Status "Status" gives and route again. |
   | Needs review | Run the `review-issue` skill. Status changed: route again. Still Needs review: reply with the open findings and stop. |
   | Blocked, every blocker closed | Set Ready and route again. |
   | Blocked, or with an open blocker | Set Blocked, reply with the blocker and stop. |
   | Size 8 | Split it as `docs/ISSUES.md` → "Size" says: make each sub-issue with the `create-issue` skill, clear its Size and stop. |
   | In progress with a branch (`git branch -a --list "*/N-*"`) | Check the working copy (step 4), `git switch <type>/N-<slug>` and go to step 5. |
   | Ready, or In progress without a branch | Start it (step 4). |

4. Start it:
   - The working copy (`AGENTS.md` → "Working copy") must be clean and
     on `main`, or on this issue's branch when resuming. Otherwise stop
     and ask the human.
   - `.github/scripts/issue-fields.sh N --status "In progress"`.
   - Follow up your earlier PRs and `main`'s runs (`ship-pr` skill,
     step 6).
   - `git pull --ff-only`, then branch as `<type>/N-<slug>` with the PR
     title's type, for example `fix/1234-joint-drift`.
5. Implement. No manual testing decision in the issue: make one with the
   `plan-manual-tests` skill. Then run the `review-change` and `ship-pr`
   skills.
