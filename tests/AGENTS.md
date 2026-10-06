# AGENTS.md — `tests/`

CLI xUnit tests, run with `dotnet test NodeRunner.slnx`. This file owns
test-code conventions; tools, projects and what each layer tests are in
`docs/TEST_STRATEGY.md`, and each library's test focus is in its
`libs/*/AGENTS.md`.

## Rules

- **Mirror the production layout.** `libs/NodeRunner.ML/Brains/DirectBrain.cs`
  is tested in `tests/NodeRunner.ML.Tests/Brains/DirectBrainTests.cs`.
- **One test class per production class.** When a production file splits,
  split its tests the same way. Test files have no size limit.
- **Names describe behaviour:** `Forward_WithZeroInput_ReturnsZeroesForTanh`,
  not `Test1`.
- **AAA layout:** Arrange, blank line, Act, blank line, Assert.
- **Global usings live in each `*.Tests.csproj`** (`Xunit`, `Shouldly`;
  `NSubstitute` in App.Tests, `NetArchTest.Rules` in Arch.Tests); don't
  import them per file.
- **Disk I/O only in a per-test temp directory.**
- **`NodeRunner.Ui.Tests` never constructs Godot nodes**
  (`docs/TEST_STRATEGY.md`).
