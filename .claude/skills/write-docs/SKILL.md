---
name: write-docs
description: Write or change Node Runner documentation in its single owning document and link to it from the rest. Use whenever you add, move or reword a rule, process, design note or doc.
---

# Write docs

`docs/CODE_DESIGN_PRINCIPLES.md` §9 owns how docs are written; this skill
owns the steps.

1. Find the topic's owner in `README.md` → "Documentation", which also
   names where a review agent's focus lives, or by §9 for a layer's rule
   or a procedure's steps. Add a document only when no owner fits, with
   its row in that table.
2. Change only the owner, and delete what the change makes unneeded.
   Search for other statements of the same thing (`rg` or `git grep`) and
   replace each with a link to the owner, as
   `` `docs/X.md` → "Heading" ``.
3. When the change alters what a `.claude/rules/` file, an agent or a
   skill says, update it in the same change.
4. When you rename or move a heading, update every link to it
   (`git grep -n '"Old heading"'`).
5. Ask §9's four questions of every text you touched.
6. Run the `review-change` skill, which selects `docs-reviewer`.
