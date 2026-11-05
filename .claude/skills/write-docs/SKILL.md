---
name: write-docs
description: Write or change Node Runner documentation in its single owning document and link to it from the rest. Use whenever you add, move or reword a rule, process, design note or doc in docs/ or README.md; agents, skills, rules files, hooks and AGENTS.md use write-agent-files.
---

# Write docs

1. Read the questions in `.claude/agents/docs-reviewer.md` →
   "Questions". Put text that question 1 places outside `docs/` where it
   says.
2. Find the topic's owner in `README.md` → "Documentation". Add a
   document only when no owner fits, with its row in that table.
3. Change only the owner, and delete what the change makes unneeded.
   Search for other statements of the same thing (`rg` or `git grep`) and
   replace each with a link to the owner, as
   `` `docs/X.md` → "Heading" ``.
4. When the change alters what a `.claude/rules/` file, an agent, a
   skill, a hook or AGENTS.md says, update it in the same change with
   the `write-agent-files` skill.
5. When you rename or move a heading, update every link to it
   (`git grep -n '"Old heading"'`).
6. Ask the questions of every text you touched.
7. Run the `review-change` skill, which selects `docs-reviewer`.
