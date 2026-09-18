---
id: 0006
title: Set up xUnit test project for the ML engine
status: closed
priority: p2
type: chore
labels: [build, ml, docs, v0.1]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
closed: 2026-09-16
resolution: completed
---

## Resolution

Completed as part of the multi-project solution refactor. Went further than
the original ask:

- `libs/NodeRunner.ML/` is a real class library (net8.0) with its own csproj —
  Godot project and tests both reference it. Design principle §2 is now
  compiler-enforced, not just documented.
- `libs/NodeRunner.Domain/` and `libs/NodeRunner.App/` split out the other
  pure-C# layers with the same guarantee.
- Test projects under `tests/`:
  - `NodeRunner.Domain.Tests/` (1 test)
  - `NodeRunner.ML.Tests/` (1 test)
  - `NodeRunner.App.Tests/` (2 tests — including one that exercises
    NSubstitute)
  - `NodeRunner.Arch.Tests/` (5 architecture rules via NetArchTest)
- Solution: `NodeRunner.slnx` (new .NET 10 XML format)
- Central package management: `Directory.Packages.props` pins xUnit 2.9,
  Shouldly 4.2, NSubstitute 5.1, NetArchTest.Rules 1.3, coverlet 6.0
- Shared build settings: `Directory.Build.props`
- Style rules: `.editorconfig`
- CI (`.github/workflows/ci.yml`) builds `NodeRunner.slnx`, runs
  `dotnet test` with coverage collection, and verifies `dotnet format`.

Verified: `dotnet test NodeRunner.slnx` → **9 tests pass, 0 fail**.

## Original summary

Add an xUnit project under `project/tests/` that references the engine-agnostic
ML code and can be run from the CLI without Godot.

## Original acceptance criteria

- [x] xUnit test project set up with `dotnet test`
- [x] Test project references the ML source via a proper class library
      (`libs/NodeRunner.ML/`)
- [x] `dotnet test` runs from repo root and reports 0 failures
- [x] CI-runnable without Godot installed
- [x] `docs/ARCHITECTURE.md` updated with the chosen sharing mechanism

## Documentation updated

- `docs/ARCHITECTURE.md` — new libs/ split, layer diagram, solution layout
- `docs/TEST_STRATEGY.md` — created; per-layer tooling and expectations
- `docs/CODE_DESIGN_PRINCIPLES.md` — added tooling table
- Root `AGENTS.md` — updated repository layout section
- Per-lib `AGENTS.md` files under `libs/NodeRunner.{Domain,ML,App}/`
- `tests/AGENTS.md` — test conventions

## Original notes

Simplest approach: extract `src/ml/` into its own class library `NodeRunner.ML`
and have both the Godot project and tests reference it. This is what we
actually did.
