# AGENTS.md — `src/sim/`

Training orchestration: trials, slots and generations.

## Rules

1. **Owns the training loop:** run every candidate for a trial, score it,
   evolve, repeat.
2. **Never reaches UI, view-models or managers.** It raises C# events that
   hosts adapt (`docs/ARCHITECTURE.md` → Dependency rules).
3. **`Evolver`'s slots are the only population**
   (`docs/TRAINING_LOOP.md` → Generations).
4. **Collision isolation** follows `docs/TRAINING_LOOP.md` → Collision
   layers; no layer scheme of its own.
5. **Trial timing.** A trial begins before any creature drives that tick,
   however it is started, so the same brain replays the same run
   (`docs/TRAINING_LOOP.md` → The same brain runs the same trial):
   - `TrialController.ProcessPhysicsPriority = -100`, below `Creature`'s
     0, so the new brain drives the trial's first step.
   - `StartTrial` begins at once inside a physics tick
     (`Engine.IsInPhysicsFrame`) and otherwise at the start of the next
     tick.
   - `Creature.ResetPose` builds every body and joint afresh, because
     Godot's joints and contacts remember their last push and would apply
     it again (#798).

## What lives here

- `TrialController.cs` — times one fixed-length trial for one creature,
  resets its pose and feeds `TrialMeasurement` each tick; raises
  `TrialStarted` and `TrialCompleted`. Simulate (#702) runs one with no end
  (`int.MaxValue` ticks).
- `Evolver.cs` — the generation cycle: every genome in fixed parallel slots
  (a creature and a `TrialController` each) → `GeneticAlgorithm` → next
  generation. Owns the followed shadow (`Follow`, `FollowedCreature`) and
  raises `GenerationCompleted`, `TrainingProgressChanged`,
  `FollowedShadowChanged` and `FollowedTrialStarted`.
- `ArenaGround.cs` — the arena's ground from the map's `MapGround`: the
  layer-1 collider, fill and edge. Ground is flat
  (`WorldBoundaryShape2D`); #91 adds shaped ground.

## Tests

The GA logic lives in `libs/NodeRunner.ML` (`GeneticAlgorithm`,
`ParallelEvaluationSchedule`) and is unit tested there. Physics and scene
behaviour are verified manually (`docs/MANUAL_TESTING.md`).
