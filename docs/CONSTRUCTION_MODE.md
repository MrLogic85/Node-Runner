# Construction mode workflow

Durable design notes for 0.3.0 "Bygg figuren" (see `docs/ROADMAP.md`). This is
the authoritative description of the construction-mode interaction and state
flow; individual issues implement slices of it but do not redefine it here.

## Mode

- The main screen has exactly two modes: **Simulate** (default) and
  **Build**. A single HUD toggle button switches between them; its label
  reflects the mode you would switch *to* ("Build" while simulating,
  "Simulate" while building).
- Entering Build hides the running creature and shows an empty (or
  in-progress) construction canvas at the same screen position. Leaving Build
  hides the canvas and shows the running creature again.
- Construction state lives in `NodeRunner.App.ViewModels.ConstructionViewModel`
  for the duration the app is open. It is not persisted across app restarts
  (no save/load yet — that is a later concern, not part of 0.3.0).

## Coordinates

- Construction-mode positions are plain 2D coordinates in the same local
  space the hardcoded creature uses (see `HardcodedCreatureFactory`): no unit
  conversion, no camera-relative math. A placed node's `Vector2D` maps
  directly to a Godot `Vector2` in the canvas's local space.

## Interactions, slice by slice

- **Place/move nodes** (issue #69): tapping empty space places a new node at
  that position. Tapping within a node's hit radius and dragging moves that
  node instead of placing a new one. There is no separate "select" step for
  moving — press-and-drag is the whole interaction.
- **Connect beams / attach cores** (issue #70): not yet implemented. Expected
  shape: selecting two existing nodes in sequence creates a beam between
  them; selecting a single node and a "make core" action attaches a core to
  it. Exact touch affordance (long-press menu vs. mode sub-toggle) is decided
  when #70 starts, not before.
- **Delete + validation messaging** (issue #71): not yet implemented.
  Expected shape: an editable element (node/beam/core) can be selected in
  Build mode and deleted with a dedicated control; invalid creature states
  surface the same beginner-facing messages `CreatureBuilder.TryBuild`
  already returns.
- **Wire into simulation** (issue #72): not yet implemented. Expected shape:
  leaving Build mode with a valid creature calls `CreatureBuilder.TryBuild`
  and, on success, replaces (or offers to replace) the running creature with
  the edited one; the original hardcoded worm remains available.

## Validation

`NodeRunner.App.Builders.CreatureBuilder.TryBuild` is the single source of
truth for whether an in-progress creature can be simulated. UI surfaces its
error messages verbatim; it does not duplicate the validation rules.
