# Construction mode workflow (historical prototype)

Durable design notes for 0.3.0 "Bygg figuren" (see `docs/ROADMAP.md`). This is
the authoritative description of that shipped prototype's interaction and
state flow, not the current product target. It remains useful when maintaining
the transitional implementation.

The active target is owned by `reference design/components/Build/README.md`,
`reference design/components/BuildLocked/README.md`, and
`reference design/components/Navigation/README.md`. When a Creation counts as
locked is overridden in `docs/TRAINING_LOOP.md` → Product lifecycle boundary.

## Mode

- The 0.3.0 prototype had one main screen with two modes, **Simulate** and
  **Build**, and a HUD toggle between them. That toggle is gone: Build and
  Training are separate routed scenes (#469), and Build opens one saved
  creation's construction canvas.
- Construction state lives in `NodeRunner.App.ViewModels.ConstructionViewModel`
  while Build is open. Build always edits a saved creation: + New saves an
  empty "Untitled Creation" with the default brain shape
  (`NewCreationWorkflow`) before Build opens.
- Every edit saves itself; there is no Save button (#368).
  `ConstructionAutosave` marks the drawing unsaved on each edit, and
  `BuildHost` saves it once edits have settled for 0.5 s, and when Build is
  left (Back, Start training, another scene) or the app pauses or closes.
  A creation opened by + New that has no nodes when Build is left is removed
  again, so + New then Back leaves no empty creation behind. A save that
  fails keeps the edits unsaved and tries again on the next save; Back,
  Start training and Reset training report every failure, background saves
  only the first in a row. Back still leaves, so a failing disk never traps
  the player in Build. If Android ends the app while it is in the
  background, an empty + New creation stays in the list (accepted on #368).
- A saved creation opens fully editable until it is locked
  (`CreationLock`, see `docs/TRAINING_LOOP.md`). A locked one opens
  move-only: nodes can move, but no parts or brain shape change.

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
- **Delete + validation messaging** (issue #71): implemented. A fourth tool,
  Delete, is added to the tool row. Tapping a node deletes it and cascades to
  every beam/core attached to it (`CreatureBuilder.RemoveNode`'s documented
  behavior); tapping a beam (hit-tested against its line segment, not just
  its endpoints) deletes just that beam, leaving its nodes in place. Cores
  are removed via the existing Core tool's tap-to-toggle, not the Delete
  tool, since a core has no separate touch target from its node. This
  intentionally does not reuse the shared `SelectionViewModel` (which drives
  Simulate-mode part inspection): construction-mode edits are transient,
  index-based, and already follow the same "tap immediately acts" pattern as
  the Beam/Core tools, so adding a persistent cross-mode selection concept
  here would add lifecycle risk (stale indices if mode switches mid-edit)
  without a corresponding benefit.
- **Wire into simulation** (issues #72, #469): Build and Training are
  separate scenes, joined only through the save. Start training saves the
  drawing, then opens the creation's training (`TrainingRoute`); it stays in
  Build if the creature cannot train yet. The
  Training scene builds its `Creature` node from the saved `CreatureDef`
  (`Creature.BuildFrom`), which generically derives the model's
  input/output counts (cores' sensor values plus `MotorTopology`'s derived
  motor-relation sensor values, and one output per motor relation) for
  whatever anatomy it is given — no special-casing between the hardcoded
  worm and an edited creature.

## Validation

A saved Creation stores any drawing: `CreatureDef` only checks that part
indices point at existing nodes, so an empty or unfinished creature is still
a Creation (#515). Only training needs a finished creature.
`NodeRunner.App.Lifecycle.CreatureReadiness` is the single source of truth
for that, in two steps: `Problems` lists why the creature cannot be
simulated yet (no nodes, a node without beams, a zero-length beam), and
`CanTrain` also needs at least one motor relation for the brain to drive.
`CreatureBuilder.TryBuild` applies `Problems` to the in-progress creature.
UI surfaces those messages and does not duplicate the rules. The one
exception is Build's readiness line, which shortens the errors for the
narrow side panel (for example "1 node not connected"); it only changes the
wording.

Start training is gated by `ConstructionViewModel.TryLeave` and
`CanTrain`: a failed `TryLeave` keeps Build open and shows the validation
errors via `StatusMessage` (`ConstructionViewModel.SetBlockedLeaveMessage`).
The edits are saved first either way. Back never validates: it saves the
drawing as it stands (#474, #368). Training refuses a saved creature that
cannot train and returns to Creations.

## Touch input

- `ConstructionCanvas` converts raw pointer positions to its own local space
  with `GetGlobalTransformWithCanvas().AffineInverse() * screenPosition`, not
  plain `ToLocal()`. Plain `ToLocal()`/`GetGlobalTransform()` ignore the
  project's `canvas_items` stretch transform, so on a device whose native
  resolution differs from the 1280x720 viewport, taps would land on the
  wrong in-canvas position relative to what's rendered. Any future widget
  that hit-tests pointer input against drawn content should use the same
  pattern.
- `project.godot` keeps `input_devices/pointing/emulate_mouse_from_touch`
  enabled so Godot controls receive their native mouse-style input on Android
  touch devices. App pointer helpers consume those native pointer events
  instead of branching on raw touch events.
