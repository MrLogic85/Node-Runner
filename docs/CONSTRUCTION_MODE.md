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
- **Connect beams / attach cores** (issue #70): implemented. A tool sub-row
  (Place / Beam / Core) below the mode toggle selects the active
  interaction. In the Beam tool, tapping a node selects it (shown with a
  selection-glow ring); tapping a second, different node connects them with
  a beam, tapping the same node again clears the selection, and tapping a
  pair that is already connected surfaces a status message instead of
  throwing. In the Core tool, tapping a node attaches a core if it doesn't
  have one, or removes it if it does. `ConstructionViewModel.StatusMessage`
  carries all of this feedback and is shown in the Build-mode inspector
  panel alongside the active tool name.
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

## Touch input

- `ConstructionCanvas` converts raw pointer positions to its own local space
  with `GetGlobalTransformWithCanvas().AffineInverse() * screenPosition`, not
  plain `ToLocal()`. Plain `ToLocal()`/`GetGlobalTransform()` ignore the
  project's `canvas_items` stretch transform, so on a device whose native
  resolution differs from the 1280x720 viewport, taps would land on the
  wrong in-canvas position relative to what's rendered. Any future widget
  that hit-tests pointer input against drawn content should use the same
  pattern.
- `project.godot` sets `input_devices/pointing/emulate_mouse_from_touch` to
  `false`. Godot's default emulates a mouse event from every touch event;
  since `PointerInput` already handles both `InputEventScreenTouch` and
  mouse events, leaving emulation on double-fires every tap/drag handler on
  real touch devices (observed as, e.g., a beam selection being made and
  immediately cleared by the "second" tap). Desktop development still gets
  real mouse input, so nothing is lost by disabling the emulation.

