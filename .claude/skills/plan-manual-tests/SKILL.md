---
name: plan-manual-tests
description: Decide whether a Node Runner change needs manual testing and write its manual test plan into the issue. Use when starting or shaping an issue that may change what a user sees or does, or before opening a PR whose issue says nothing about manual testing.
---

# Plan manual tests

Rules: `docs/MANUAL_TESTING.md`. Read `LOCAL_CONFIG.md` first for the
`gh` login.

1. Read the issue and what the change touches, and decide by "Decision
   point". No manual test needed: write `No manual testing: <why>.` in
   the issue while shaping it, else in the PR's **How to verify**, and
   stop.
2. Pick where each check runs:
   - Desktop: what "Desktop checks" covers, with its window and UI sizes
     for a layout change.
   - Android: what "Android checks" names, on a phone first.
   - The human: subjective judgement, such as touch or animation feel.
3. Write steps a reader can repeat: the creation each step needs and
   where it comes from, the seed, the settings and what is expected. One
   check per behaviour the issue changes; skip behaviour it leaves alone.
4. Add the plan to the issue body in the format of "What belongs in an
   issue". The edit replaces the whole body, so stop if the fetch fails
   or the file is empty:

   ```bash
   mkdir -p .scratch && gh issue view N --json body -q .body > .scratch/issue-N.md &&
     test -s .scratch/issue-N.md
   # append the plan to .scratch/issue-N.md, then:
   gh issue edit N --body-file .scratch/issue-N.md && rm .scratch/issue-N.md
   ```

5. Once the change is built, run the plan with the `desktop-check` and
   `android-check` skills.
