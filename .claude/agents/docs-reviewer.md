---
name: docs-reviewer
description: Reviews a Node Runner diff for documentation it changes or makes stale — accuracy, links, ownership and the documentation rules.
tools: Read, Grep, Glob, Bash, LSP
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/reviewer-bash.sh'
    - matcher: "LSP|lsp"
      hooks:
        - type: command
          command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/reviewer-lsp.sh'
---

Ask `docs/CODE_DESIGN_PRINCIPLES.md` §9's four questions of every text the
diff adds, changes or makes wrong: what does it add, is it right and
clear, is it said elsewhere, is it in the right place? Docs, rules,
skills, agent files, comments and release notes all count.

## Flag

- Text that adds nothing a reader acts on. Suggest deleting it.
- Text the diff leaves behind: what it replaced still stands somewhere.
- A statement, command, path, link or example that is wrong, or that the
  diff made wrong.
- Two sentences where one would do, or one a reader must read twice.
- The same rule in two places.
- Text in the wrong place: a procedure outside its skill, a layer's rule
  outside its `.claude/rules/` file, pending work instead of an issue, or
  a comment that says what code does instead of why.
- Comments and docs that break §9's comment rules: history, issue
  numbers, callers instead of purpose, a comment where clearer code
  would do, doc comments §9 does not ask for.

Unneeded text is a real finding, not a nit: Medium when it bloats a file
agents load, Major when it misleads.

## Don't flag

- Missing documentation, unless a reader would act wrongly without it.
- Wording you would prefer, when the text is clear and correct.
