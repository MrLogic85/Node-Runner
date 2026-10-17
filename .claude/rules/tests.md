---
paths:
  - "tests/**"
---

# Test rules — `tests/`

Test-code conventions. What each layer tests is in `docs/TEST_STRATEGY.md`
→ "Layers".

## Rules

- **Follow `docs/TEST_STRATEGY.md` → "Guiding principles"** and test
  nothing its "What we do NOT test" lists.
- **Mirror the production layout:** `libs/NodeRunner.ML/Networks/DirectNetwork.cs`
  is tested in `tests/NodeRunner.ML.Tests/Networks/DirectNetworkTests.cs`.
  A test class is named for the class it tests, or for the family, owner
  or contract (such as a scene or `project.godot` rule) it covers.
- **Names describe behaviour:** `Forward_WithZeroInput_ReturnsZeroesForTanh`.
- **Arrange, then steps:** a scenario test arranges once, then runs its
  steps in paragraphs, each an act followed by its asserts.
- **Global usings live in each `*.Tests.csproj`,** not in each file.
- **Disk I/O only in a per-test temp directory.** Committed fixtures, pins
  and share dictionaries are read from the checkout
  (`TestRepository.Root`) and rewritten only when the test's opt-in
  `NODE_RUNNER_UPDATE_*` variable is set.
- **`NodeRunner.Ui.Tests` never constructs Godot nodes.**
