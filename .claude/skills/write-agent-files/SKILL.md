---
name: write-agent-files
description: Write or change a Node Runner agent, skill, .claude/rules/ file, hook or AGENTS.md in the agreed structure. Use whenever you add, move or reword an instruction for agents; docs in docs/ or README.md use write-docs.
---

# Write agent files

`docs/REVIEW.md` → "Agent, rule and skill files" owns the structure.

1. Choose the kind of file from that section's table.
2. If a script can check the rule, write a hook (`docs/REVIEW.md` →
   "Hooks") or an arch
   test instead of prose.
3. Write an agent with a `description` saying when to use it, a `model`
   (`sonnet` by default, `opus` where the focus needs deeper reasoning,
   `haiku` for a light, mechanical task) and these sections:
   - `## Input`: what it is given, and the doc sections it reads.
   - `## Rules`: only what `AGENTS.md`, docs and hooks don't say.
   - `## Work`: its steps.
   - `## Output`: for a reviewer, `docs/REVIEW.md` → "Findings format";
     `design-lead` may add proposals in its own shape.

   Until #1407 moves the others, this applies to new and changed agents.
4. Name a doc by its heading, as `` `docs/X.md` → "Heading" ``, wherever
   you reference one.
5. From the file you write, delete what `AGENTS.md`, a doc or a hook
   already says. From other files, delete what your change makes
   unneeded.
6. Run the `review-change` skill.
