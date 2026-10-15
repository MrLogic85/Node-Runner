# AGENTS.md — `libs/NodeRunner.ML`

The neural-network and genetic-algorithm engine. Layer rules (no Godot,
Domain only) are in `docs/ARCHITECTURE.md` and enforced by
`NodeRunner.Arch.Tests`.

## Rules

- **Plain values in and out.** The public API takes and returns primitives
  (`double`, `double[]`, `int`), small structs and Domain records such as
  `NeuralNetworkDef` and `NetworkPortLayout`, so the engine stays testable with plain
  xUnit.
- **`double`, not `float`,** so training maths is consistent and
  reproducible.
- **No I/O, no global state.** No `System.IO`, `System.Net`, static mutable
  fields or `Console.WriteLine`.
- **No allocations in hot paths.** `NeuralNetwork.Forward(input, output,
  scratchA, scratchB)` takes caller-owned buffers, which must be distinct
  arrays (`ValidateDistinctBuffer`); the network keeps no per-call state.
- **Genome layout.** Per layer: weights in row-major output-neuron order,
  then biases. `DirectNetwork.LayerSizes` is `[inputs, outputs]` in
  `NetworkPorts` order, and `DirectNetwork.Compile` matches genes by port, never
  by list order, so a saved network loads whatever its order. A disabled gene
  compiles to 0 and `Evolver.SilenceDisabledGenes` keeps it there
  (`DirectNetwork.DisabledGenes`). The `DirectNetwork` XML doc has the detail.

## Scope

- `NeuralNetwork`, `Activation` — the feedforward network
- `Networks/` — `DirectNetwork` (network graph ↔ runtime `NeuralNetwork`), `GenerationZero`,
  `NewPortExploration`
- `Ga/` — `GeneticAlgorithm`, `CrossoverStrategy`,
  `ParallelEvaluationSchedule`, `TrialMeasurement`
- `Gaussian` — maths the BCL lacks

Backpropagation is #955.

## Tests

`tests/NodeRunner.ML.Tests/` pins:

- `Forward` output shape for `LayerSizes`; same seed and input give the same
  output; activations on hand-worked values
- GA operators: elitism, crossover only from parents, the same seed gives
  the same generation, a tournament picks the fittest of its draws with
  replacement, and mutation hits genes at the rate with noise of the given
  sigma
- Round-trips: `FromGenome(LayerSizes, FlattenGenome(), activation)` and
  `DirectNetwork.Compile(ToNeuralNetworkDef(...))` whatever the saved order
- `Clone` gives an independent network
- `NewPortExploration`: only new ports' genes are redrawn, with generation
  0's spread; elites, trained parts and disabled genes are untouched; the
  same seed gives the same generation
