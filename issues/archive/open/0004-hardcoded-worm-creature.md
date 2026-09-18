---
id: 0004
title: Hardcoded worm creature (5 joints) with physics
status: open
priority: p1
type: feature
labels: [creature, physics, v0.1]
version: v0.1
created: 2026-09-16
updated: 2026-09-16
---

## Summary

A single hardcoded creature — a horizontal "worm" of 5 joints connected by
bones with muscles between adjacent joints — lives in a 2D physics scene with
a ground plane.

## Context

v0.1's visible artifact. No brain wiring yet; muscles will be driven by
constant/random targets in #0005. Depends on #0001.

## Acceptance criteria

- [ ] `CreatureDef`, `JointDef`, `BoneDef`, `MuscleDef` records exist in
      `project/src/creature/` per `docs/ARCHITECTURE.md`
- [ ] `Creature.tscn` and `Creature.cs` build a runtime scene from a
      `CreatureDef`
- [ ] Joints are `RigidBody2D` circles, bones are visual + rigid connections
      (`PinJoint2D` or `DampedSpringJoint2D`), muscles are actuated joints
      that respond to a target `[-1, 1]`
- [ ] Ground exists; creature falls onto it and rests without exploding
- [ ] Cartoon look: soft colors, big friendly eyes on the head joint
- [ ] Time scale of the physics loop is not tied to render FPS (fixed
      timestep 60 Hz)

## Notes

- Keep muscle max-force conservative — better to twitch weakly than fling
  bodies offscreen.
- Head joint = first joint in the def; used for camera targeting later.
