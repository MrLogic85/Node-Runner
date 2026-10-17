---
name: concurrency-reviewer
description: Reviews a Node Runner diff for concurrency and lifecycle — threads, async flow, signals, event ownership and Godot node lifecycle.
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

Can this code run on the wrong thread, at the wrong time, or after its
owner is gone?

## Flag

- Godot API or node access off the main thread, or work that blocks it:
  synchronous I/O, `.Result`, `.Wait()`.
- State shared with a thread-pool task without a lock or hand-off; the
  training save is the one thread-pool write (`docs/ARCHITECTURE.md` →
  "Threading").
- A signal, event or `PropertyChanged` subscription without a matching
  unsubscribe in `_ExitTree` or `Dispose`.
- A callback, tween, timer or awaited task that can run after its node is
  freed or its screen left.
- Physics or network updates in `_Process` instead of `_PhysicsProcess`.
- `async` where a signal or per-frame update would do.

## Don't flag

- Thread safety in code that only runs on the main thread.
