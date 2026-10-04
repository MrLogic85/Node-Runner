# AGENTS.md — `libs/NodeRunner.Mechanics`

The pure physics of creature parts: the sums the sim runs each step, which
Build and tests reuse so they show and check exactly the same thing (#596).

Shared architecture, design, and testing rules live in
`docs/ARCHITECTURE.md`, `docs/CODE_DESIGN_PRINCIPLES.md`, and
`docs/TEST_STRATEGY.md`. This file owns only constraints specific to this
folder.

## Local constraints

- **Depends on `NodeRunner.Domain` only.** No Godot, ML or App. Enforced by
  `NodeRunner.Arch.Tests`.
- **Pure and stateless.** Static methods that take values and return values.
  State that carries between steps (a `ProofMass`) is a
  `readonly record struct` the caller keeps and passes back.
- **No I/O, no global state, no randomness.**
- **Godot does the physics it can.** This library holds only what Godot has
  no built-in for — a part's own rule, not a rigid-body solver
  (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
- **The brain contract is not here.** Port order, channel keys and each
  output's activation stay in Domain (`libs/NodeRunner.Domain/AGENTS.md`
  lists why). This library turns a port's value into physics
  (`OutputSignals`) and physics into a reading.

## Scope

- `Accelerometer`, `ProofMass` — the proof-mass step, reading and sensor frame
- `CameraRays` — the camera's ray targets and reading
- `Piston` — a Piston's inputs and force
- `RigidTriangles`, `RigidTriangleDef` — the closed beam triangles of a
  creature
- `OutputSignals` — an output's value as a Piston's target length and
  strength

## Tests

`tests/NodeRunner.Mechanics.Tests/` — xUnit + Shouldly. Pin each part's
behavior with numbers a person can check by hand.
