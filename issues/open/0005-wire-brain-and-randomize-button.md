---
id: 0005
title: Wire brain to muscles; Randomize button (v0.1 ship)
status: open
priority: p1
type: feature
labels: [ml, creature, ui, v0.1]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
---

## Summary

Attach a `NeuralNetwork` to the hardcoded worm as its brain. Each physics tick:
read joint angles as inputs, forward through the net, apply outputs as muscle
targets. Add a "Randomize" button in the UI that reseeds the brain with new
random weights.

## Context

This is the v0.1 ship milestone. Depends on #0003 and #0004. After this, we
have "spastic worm on Android" — ugly but complete.

## Acceptance criteria

- [ ] `Sensors.cs` reads joint angles into a `double[]` in a stable order
- [ ] `Muscle.ApplyTarget(double t)` maps `t ∈ [-1, 1]` to motor torque
- [ ] Creature has a `Brain` field; per `_physics_process` tick:
      `sensors → brain.Forward → muscle targets` (no allocations in hot path)
- [ ] Simple HUD scene with a "Randomize" button (Godot `Button`)
- [ ] Button click generates a fresh RNG seed, logs it, and rebuilds the
      brain
- [ ] Ship criterion met: APK built via #0002 pipeline, installed on device,
      worm visibly twitches, Randomize changes behavior

## Notes

- Brain size for v0.1: `[inputs, 8, muscleCount]` with tanh. Small enough to
  reason about, big enough to move.
- Input order must be deterministic — bake it once and document in a comment
  above `Sensors.Read()`.
