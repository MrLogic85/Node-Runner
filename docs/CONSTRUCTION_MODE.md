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

## Interactions

The rail holds the reference tools Move, Beam, Joint and Select (#365);
"joint" is the player-facing name for a node.
Build always opens in Move. `ConstructionGestures` (App) turns one pointer's
press, drag and release into edits for the active tool, and
`ConstructionCanvas` only converts input to canvas units and draws. A
pointer that travels at most `TapSlop` counts as a tap. Hit tests prefer a
node over a beam under it.

- **Move:** tap a node or beam to select it (its settings open), tap empty
  canvas to deselect, drag a node to move it. A drag on empty canvas changes
  nothing (#400 makes it pan). Move never adds a node.
- **Beam:** drag from one node to a different node to join them. The preview
  only snaps to a node the beam could join (`ConstructionViewModel.CanConnect`);
  releasing anywhere else, including over a node already joined to the start,
  adds nothing. Beam never adds a node.
- **Joint:** tap empty canvas to add a node, or tap a beam to split it at the
  closest point: one change that replaces the beam with two through the new
  node (`ConstructionViewModel.SplitBeam`).
- **Select:** tap nodes to add them, drag on empty canvas for a box that
  replaces the selection, drag a selected node to move the selection. The
  box with handles is #366.
- **Core (transitional):** the Core row in the Parts tray turns taps on a
  node into adding or removing its core, until parts are dragged from the
  tray onto joints (#376).
- There is no Delete tool: the part settings and selection panels delete the
  selection, and deleting a node removes every beam and core on it
  (`CreatureBuilder.RemoveNode`).
- A locked creation opens in Move with Beam and Joint disabled, and
  `ConstructionViewModel` refuses topology edits on its own.
- Changing tool mid-gesture cancels the gesture without an edit.
- `ConstructionViewModel.StatusMessage` records the outcome of the last
  edit (including `ConnectBeam` refusing a pair as a safety net); the Build
  screen does not show it yet.

## Build to Training

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
