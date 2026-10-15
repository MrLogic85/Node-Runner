---
name: create-issue
description: File a new Node Runner GitHub issue with its labels, project fields, milestone, parent and blockers. Use whenever you file a bug, improvement, decision or follow-up, or split an issue.
---

# Create an issue

`docs/ISSUES.md` owns what an issue needs and when to file one; this skill
owns the steps. Read `LOCAL_CONFIG.md` first for the `gh` login.

1. Search for a duplicate: `gh issue list --state all --search "<words>"`.
   If one exists, comment there instead.
2. Write the body to `.scratch/issue.md`: the outcome, the context with
   evidence (files, lines, logs, issue numbers), acceptance criteria and a
   test plan. Title it as `docs/ISSUES.md` says.
3. Create it with the labels "Required metadata" asks for, the milestone
   if it belongs to one, and its parent and blockers:

   ```bash
   gh issue create --title "…" --body-file .scratch/issue.md \
     --label "type: …" --label "area: …" [--milestone "0.x.y"] \
     [--parent P] [--blocked-by B1,B2]
   ```

   Then delete `.scratch/issue.md`.
4. Set Status, Priority and Size as "Creating an issue", "Status" and
   "Size" say:
   `.github/scripts/issue-fields.sh N --status … --priority … [--size …]`.
5. Run `.github/scripts/issue-audit.sh` and fix what it lists for the new
   issue.
6. Tell the human its number.
