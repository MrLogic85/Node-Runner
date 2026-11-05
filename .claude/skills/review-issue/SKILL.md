---
name: review-issue
description: Review a Node Runner GitHub issue before implementation with the issue-reviewer agent, act on the findings and set its Status. Use when an issue is in Needs review or Needs decision, or is broad, risky or ambiguous.
---

# Review an issue

Rules: `docs/ISSUES.md` → "Reviewing an issue". Read `LOCAL_CONFIG.md`
first for the `gh` login.

1. Run the `issue-reviewer` agent with the issue URL.
2. Act on each finding by its level. Settle what the code, docs and
   earlier discussion settle. Ask the human a product or design choice
   they don't; no answer: post it as an issue comment.
3. The review changes the issue: edit its body, and its labels or
   relationships per `docs/ISSUES.md` → "Required metadata". File other
   problems it found with the `create-issue` skill.
4. Post the summary as an issue comment:

   ```text
   ## Issue review

   `issue-reviewer` found <counts by level>. <All are settled | Open: <findings>>. The body records what changed.

   - **<Level>, <issue section>**: <what changed, or why dismissed>

   Size <n>. Status: <Status>.
   ```

5. Set or confirm its Size; split a Size 8 (`docs/ISSUES.md` → "Size").
6. Set the Status that follows with `.github/scripts/issue-fields.sh`;
   after a split, on the sub-issues, and on the parent per "Parent
   issues".
