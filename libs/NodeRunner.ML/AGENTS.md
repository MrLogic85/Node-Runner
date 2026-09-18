# AGENTS.md — `libs/NodeRunner.ML`

The neural-network and evolutionary-algorithm engine. Pure C#. This is the
strictest layer in the codebase.

## Hard rules

- **No `Godot.*` references.** Enforced by `NodeRunner.Arch.Tests`.
- **No I/O, no global state.** No `System.IO`, no `System.Net`, no static
  mutable fields, no `Console.WriteLine` in training loops.
- **Randomness is injected.** Everything that samples takes a `Random` (later
  possibly `IRng`) as a parameter.
- **No allocations in hot paths.** `Forward(...)` overloads should accept
  reusable output buffers.
- **All public types are unit-tested** in `tests/NodeRunner.ML.Tests/`.

## What lives here

- `NeuralNetwork.cs` — feedforward
- `Activation.cs` (+ `Activations/` if it grows) — tanh, ReLU, sigmoid
- `Ga/` — `GeneticAlgorithm`, `Selection`, `Crossover`, `Mutation` (v1.0)
- `Backprop/` — backprop + optimisers (v3.0)
- `Math/` — helpers not covered by `System.Numerics`

## Style

- Prefer `double[]` flat row-major over jagged 2D. Cache-friendly, easy to
  serialise.
- `double`, not `float`.
- Every algorithm has a one-line comment stating what it does, plus a
  reference link when it comes from a paper.
- Introduce interfaces when the second concrete case appears — not before.

## Tests

`tests/NodeRunner.ML.Tests/` — xUnit + Shouldly. Deterministic, fast, no I/O.

Cover: output shape, deterministic-with-same-seed, genome round-trip, clone
independence, activation function correctness on hand-worked values.
