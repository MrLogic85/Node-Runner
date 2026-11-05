---
name: write-agent-files
description: Write or change a Node Runner agent, skill, .claude/rules/ file, hook or AGENTS.md in the agreed structure. Use whenever you add, move or reword an instruction for agents; docs in docs/ or README.md use write-docs.
---

# Write agent files

1. Read the questions in `.claude/agents/docs-reviewer.md` →
   "Questions", and choose the kind of file by question 1.
2. Write an agent with a `description` saying when to use it, a `model`
   (`sonnet` by default, `opus` where the focus needs deeper reasoning,
   `haiku` for a light, mechanical task) and these sections, in this order:
   - `## Input`: what it is given, and the doc sections it reads.
   - `## Rules`: only what AGENTS.md, docs and hooks don't say.
   - `## Work`: its steps.
   - `## Output`: for a code reviewer, `AgentFiles.FindingsFormat` word
     for word (`docs/REVIEW.md` → "Findings format").
3. Name only existing docs and headings (`docs/REVIEW.md` → "Agent,
   rule and skill files").
4. Ask the questions of every text you touched.
5. Run the `review-change` skill.
