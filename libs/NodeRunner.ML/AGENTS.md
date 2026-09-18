# AGENTS.md — `libs/NodeRunner.ML`

The neural-network and evolutionary-algorithm engine.

Shared architecture, design, and testing rules live in
`docs/ARCHITECTURE.md`, `docs/CODE_DESIGN_PRINCIPLES.md`, and
`docs/TEST_STRATEGY.md`. This file owns only constraints specific to this
folder.

## Local constraints

- **No I/O, no global state.** No `System.IO`, no `System.Net`, no static
  mutable fields, no `Console.WriteLine` in training loops.
- **No allocations in hot paths.** `Forward(...)` overloads should accept
  reusable output buffers.

## Scope

- Feedforward neural networks and activation functions
- Genetic algorithms and evolutionary operators
- Backpropagation and optimisers
- ML-specific math not provided by the BCL
