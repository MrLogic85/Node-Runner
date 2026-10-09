# AGENTS.md — `libs/NodeRunner.Mechanics`

The pure physics of creature parts: the sums the sim runs each step, which
Build and tests reuse so they show and check exactly the same thing (#596).

## Rules

- **Pure and stateless.** Static methods take values and return values.
  State that carries between steps (a `ProofMass`) is a
  `readonly record struct` the caller keeps and passes back.
- **No I/O, no global state, no randomness.**
- **Only what Godot lacks.** A part's own rule, never a rigid-body solver
  (`docs/CODE_DESIGN_PRINCIPLES.md` §2).
- **The brain contract stays in Domain.** Port order, channel keys and each
  output's activation are Domain's (`libs/NodeRunner.Domain/AGENTS.md` says
  why). This library turns a port's value into physics (`OutputSignals`) and
  physics into a reading.

## Scope

- `Accelerometer`, `ProofMass` — the proof-mass step, reading and sensor frame
- `CameraRays` — the camera's ray targets and reading
- `Servo` — a Servo's angle inputs, torque and endpoint force couple
- `Piston` — a Piston's travel, inputs, target length and force
- `Spring` — a Spring's travel
- `Wheel` — a Wheel's weight and thin-ring turning inertia
- `Travel` — the shortest and longest length a Piston's or Spring's stroke
  gives
- `RigidTriangles`, `RigidTriangleDef` — the closed beam triangles of a
  creature
- `OutputSignals` — an output's value as a Servo's target angle and as a
  strength

## Tests

`tests/NodeRunner.Mechanics.Tests/` pins each part with numbers a person
can check by hand: an accelerometer at rest reads 1 g up; a Piston pushes
toward its target, never above its chosen strength, and reaches full force
within its rise time. Tests for the types that stay in Domain stay in
`NodeRunner.Domain.Tests`.
