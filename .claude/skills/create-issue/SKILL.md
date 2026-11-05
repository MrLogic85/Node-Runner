---
name: create-issue
description: File a new Node Runner GitHub issue with its labels, project fields, milestone, parent and blockers. Use whenever you file a bug, improvement, decision or follow-up, or split an issue.
---

# Create an issue

Rules: `docs/ISSUES.md`. Read `LOCAL_CONFIG.md` first for the `gh`
login.

1. A simple problem outside the current task (`docs/ISSUES.md` →
   "Creating an issue"): fix it in your current change and stop. No
   current change: file it below as a *quick fix*.
2. Search for a duplicate: `gh issue list --state all --search "<words>"`.
   Found: comment there. A quick fix: also set it Ready unless it is In
   progress, and start it as step 7 says. Otherwise stop.
3. Write `.scratch/issue.md`: outcome, context with evidence (files,
   lines, logs, issue numbers), acceptance criteria and test plan. Title
   it per "Creating an issue".
4. Create it with the labels "Required metadata" asks for, its milestone
   if it has one, its parent and its blockers, then delete
   `.scratch/issue.md`:

   ```bash
   gh issue create --title "…" --body-file .scratch/issue.md \
     --label "type: …" --label "area: …" [--milestone "0.x.y"] \
     [--parent P] [--blocked-by B1,B2]
   ```

5. Set Status, Priority and Size per "Creating an issue", "Status" and
   "Size"; a quick fix is Ready:
   `.github/scripts/issue-fields.sh N --status … --priority … [--size …]`.
6. Run `.github/scripts/issue-audit.sh` and fix what it lists for the new
   issue.
7. Reply `Filed #N: <title>`. Start a quick fix with the `start-issue`
   skill: at once, or once the human agrees if it came up in a
   conversation with them.
