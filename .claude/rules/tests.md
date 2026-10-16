---
paths:
  - "tests/**"
---

# Test rules — `tests/`

CLI xUnit tests. This file owns test-code conventions; how to run them,
tools, projects and what each layer tests are in `docs/TEST_STRATEGY.md`,
and each library's test focus is in its rule in `.claude/rules/`
(`domain.md`, `mechanics.md`, `ml.md`, `app.md`).

## Rules

- **Mirror the production layout.** `libs/NodeRunner.ML/Networks/DirectNetwork.cs`
  is tested in `tests/NodeRunner.ML.Tests/Networks/DirectNetworkTests.cs`.
- **One test class per production class.** When a production file splits,
  split its tests the same way. Test files have no size limit.
- **Names describe behaviour:** `Forward_WithZeroInput_ReturnsZeroesForTanh`,
  not `Test1`.
- **AAA layout:** Arrange, blank line, Act, blank line, Assert.
- **Global usings live in each `*.Tests.csproj`;** don't import them per
  file.
- **Disk I/O only in a per-test temp directory.** The exceptions are committed
  fixtures, pins and share dictionaries, which a test may read from the
  checkout (`TestRepository.Root`). It rewrites them only when its opt-in
  `NODE_RUNNER_UPDATE_*` variable is set. The hook specs make their
  throwaway repositories under the test output rather than `/tmp`
  (`docs/REVIEW.md` → "Hooks").
- **`NodeRunner.Ui.Tests` never constructs Godot nodes**
  (`docs/TEST_STRATEGY.md`).
