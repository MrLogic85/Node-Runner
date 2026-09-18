---
id: 0003
title: Implement engine-agnostic NeuralNetwork.cs (feedforward)
status: open
priority: p1
type: feature
labels: [ml, v0.1, pedagogical]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
---

## Summary

Write a pure-C# feedforward neural network in
`libs/NodeRunner.ML/NeuralNetwork.cs` with a small API and no dependency on
Godot.

## Context

The foundational ML class. Everything else — GA, backprop, visualization —
consumes this. Design principles §2 (engine-agnostic) apply strictly and are
enforced by `NodeRunner.Arch.Tests`.

See `docs/ARCHITECTURE.md` for the intended API sketch and
`docs/ML_CONCEPTS.md` → "Feedforward neural network".

## Acceptance criteria

- [ ] `NeuralNetwork` class exists under `libs/NodeRunner.ML/`
- [ ] Constructor takes `int[] layerSizes`, `Activation activation`, and a
      seeded `Random` for initial weight init
- [ ] Weights initialized with sensible defaults (e.g. Xavier for tanh)
- [ ] `Forward(double[] input) : double[]` is pure and deterministic
- [ ] `Clone()` produces a deep copy
- [ ] `FlattenGenome() : double[]` and
      `FromGenome(int[] layers, double[] genome, Activation act) : NeuralNetwork`
      round-trip losslessly
- [ ] `Activation` enum: `Tanh`, `ReLU`, `Sigmoid`
- [ ] Zero `using Godot;` (enforced by `NodeRunner.Arch.Tests`)
- [ ] Unit tests in `tests/NodeRunner.ML.Tests/NeuralNetworkTests.cs` cover:
      output shape, determinism with same seed, genome round-trip, clone
      independence, activation functions on hand-worked values

## Notes

- Storage: `double[][] weights` where `weights[layer]` is a flat row-major
  matrix of size `outSize * inSize`. Simple and cache-friendly enough for
  v0.1.
- Use `double`, not `float`. See design principles.
