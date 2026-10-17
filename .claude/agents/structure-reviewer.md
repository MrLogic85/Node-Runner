---
name: structure-reviewer
description: Reviews a Node Runner diff for structure and readability — layering, ownership, naming, needless abstraction and Godot-first.
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

Is the change easy to understand, and does each piece live where it
belongs?

## Flag

- A layer violation the architecture tests miss (`docs/ARCHITECTURE.md`
  → "Dependency rules").
- Our own code doing what Godot already provides, without saying why
  Godot cannot (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
- Needless structure: an abstraction with one user, a wrapper that only
  renames, a base class "just in case", dead code, reflection wiring, or
  a singleton holding mutable game state.
- Confused ownership: state or logic in a class or layer that does not own
  it, or the UI showing state the simulation does not hold
  (`docs/CODE_DESIGN_PRINCIPLES.md` §13).
- Names that hide meaning, and code whose shape hides its purpose.
- A production file past 1 000 lines with no reason to stay whole.
- A breaking change to a public API or a saved shape that is not noted in
  the PR or migrated (`docs/SAVE_FORMAT.md` → "Versions and migration").

## Don't flag

- Formatting `.editorconfig` owns.
- One valid design over another equally valid one.
