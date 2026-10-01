# Build mode workflow (historical prototype)

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
  creation's Build canvas.
- Build state lives in `NodeRunner.App.ViewModels.BuildViewModel`
  while Build is open. Build always edits a saved creation: + New saves an
  empty "Untitled Creation" with the default brain shape
  (`NewCreationWorkflow`) before Build opens.
- Every edit saves itself; there is no Save button (#368).
  `BuildAutosave` marks the drawing unsaved on each edit, and
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

- Build-mode positions are plain 2D coordinates in the same local
  space the hardcoded creature uses (see `HardcodedCreatureFactory`): no unit
  conversion. The only view math is the Build canvas's zoom and pan
  (`CanvasView`), which never touches saved positions.

## Interactions

The rail holds the reference tools Move, Beam, Joint and Select (#365);
"joint" is the player-facing name for a node.
Build always opens in Move. `BuildGestures` (App) turns pointer
presses, drags and releases into edits for the active tool and into zoom and
pan, and `BuildCanvas` only forwards input and draws. A pointer that
travels at most `TapSlop` view units counts as a tap. Hit tests prefer a node
over a beam under it; hit sizes are finger-sized on screen at any zoom, and
a node's own disc always hits.

- **Move:** tap a node or beam to select it (its settings open), tap empty
  canvas to deselect, drag a node to move it, drag anywhere else (empty
  canvas or a beam) to pan the view (#400). Move never adds a node.
- **Beam:** drag from one node to a different node to join them. The preview
  only snaps to a node the beam could join (`BuildViewModel.CanConnect`);
  releasing anywhere else, including over a node already joined to the start,
  adds nothing. Beam never adds a node.
- **Joint:** tap empty canvas to add a node, or tap a beam to split it at the
  closest point: one change that replaces the beam with two through the new
  node (`BuildViewModel.SplitBeam`).
- **Select (#366):** tap a node to add it and tap a selected one to remove
  it; drag on empty canvas (beams count as empty) for a box that replaces
  the selection; an empty tap clears it. Two or more selected joints get a
  dashed frame with three `UiSelectionHandle`s: **Move** in the middle (or
  drag anywhere inside the frame, or a selected joint), **Rotate** on a stem
  above and **Scale** at the bottom-right corner. Rotate and Scale turn
  about the centre of the joints' bounds; Scale counts only the drag along
  its diagonal and is clamped to `MinSelectionScale`..`MaxSelectionScale`,
  so the group never collapses or reflects. Every drag frame is computed
  from a `SelectionSnapshot` taken when the drag starts, so nothing drifts.
  A move stops the group as a whole at the build area's edge; a turn or
  scale that would leave it is ignored. The frame and handles keep their
  screen size at any zoom, the frame clears the selected joints' halos,
  and each handle hits within `HandleHitRadius`; a tap (not a drag) on a
  joint under a handle still adds or removes that joint. A locked
  creation keeps all three handles: like a move, scaling changes only
  beam lengths, never which parts there are.
- **Sensors:** an Accelerometer (#127) and an LOS sensor (#575) sit on a
  beam. Build cannot place one yet: dragging sensors from the tray onto beams
  is #376, so until then only the seeded Worm and the examples have them
  (from data). Deleting a
  beam deletes its sensors; splitting a beam with the Joint tool moves them,
  with their ids, to the longer half.
- There is no Delete tool: the part settings and selection panels delete the
  selection, and deleting a node removes every beam on it and those beams'
  sensors (`CreatureBuilder.RemoveNode`).
- A locked creation opens in Move with Beam and Joint disabled, and
  `BuildViewModel` refuses topology edits on its own.
- **Two fingers, any tool (#400):** pinch zooms about the point between the
  fingers and dragging both pans. The second finger cancels the first
  finger's gesture, putting back any node it moved and any selection a
  Select press changed, and nothing edits
  until every finger lifts, so navigation never changes the creature.
- **Build area (#400):** joints live inside the fixed
  `BuildViewModel.BuildArea` (x −1152..1152, y −576..576 canvas
  units, about six screens wide at 1×). Placing or moving a joint keeps its
  disc inside; a group move stops as a whole at the edge, and a Joint tap
  outside adds nothing. A faint blueprint grid (the `line` token, fixed
  `BuildGridStep` 48-unit cells) covers exactly the area, and accent corner
  marks two cells long frame it. Grid and corners are fixed parts of the
  picture; zoom never changes their cells or length.
- **View:** `CanvasView` (App) holds zoom and pan and maps view units to
  canvas units. It is not saved: Build opens with the creation centred and
  `FitMargin` (20%) of air on every side, zoomed out if needed but never
  magnified past 1×; an empty creation opens at 1× on the middle of the
  area. The view can show `BuildViewBounds`: the area plus
  `BuildViewMargin` (one cell, 48 canvas units) on every side, the same at
  any zoom. Zooming out stops when all of it is in view (`MinZoom`), up to
  `MaxZoom` in. Along an axis where the bounds are larger than the view,
  panning stops at their edge; along an axis where they fit, they are
  centred. Godot's Camera2D would replace only this clamp, so Build keeps
  `CanvasView` (#564). Zoomed in, the creation can be off screen; zooming
  out finds it. Distances are in view or canvas units (`docs/GLOSSARY.md` →
  Build canvas). How zoom treats lines, the grid and labels is owned by
  `docs/UI_DIRECTION.md` → Reference flow overrides.
- Changing tool mid-gesture, or Android cancelling the touch, cancels the
  gesture the same way.
- `BuildViewModel.StatusMessage` records the outcome of the last
  edit (including `ConnectBeam` refusing a pair as a safety net); the Build
  screen does not show it yet.

## Build to Training

- **Wire into simulation** (issues #72, #469): Build and Training are
  separate scenes, joined only through the save. Start training saves the
  drawing, then opens the creation's training (`TrainingRoute`); it stays in
  Build if the creature cannot train yet. The
  Training scene builds its `Creature` node from the saved `CreatureDef`
  (`Creature.BuildFrom`), which generically derives the model's
  input/output counts (the sensor parts' readings plus `MotorTopology`'s derived
  motor-relation sensor values, and one output per motor relation) for
  whatever anatomy it is given — no special-casing between the hardcoded
  worm and an edited creature.

## Parts tray

With nothing selected, an unlocked creation's side panel shows the Parts
tray (#374): four `UiIconTabs` (Links, On a joint, Sensors, Blocks) pinned at
the top, then a scrolling list with the open tab's name, its parts as compact
`UiPartRow`s and one help line for the tab. `NodeRunner.App.ViewModels.PartTray`
owns the groups, their order, the help lines and each row's state; the screen
only maps parts to glyphs. Every implemented part is unlimited until #525, so
rows show no count. A part not yet implemented is a dashed row with a lock,
and the tab's name row says "Coming later" once. Available rows (today the
Accelerometer and the LOS sensor) do nothing on tap: placing parts from the tray is #376. Beam,
Joint and Select show a short status line with the tool's glyph above the
readiness line; Move shows none. One selected part shows its settings and
several show the selection panel instead.

## Validation

A saved Creation stores any drawing: `CreatureDef` only checks that part
ids are unique and below `NextPartId`, that beams point at existing node
ids and sensors at existing beam ids (one of each kind per beam), so an empty or unfinished creature is still
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

Start training is gated by `BuildViewModel.TryLeave` and
`CanTrain`: a failed `TryLeave` keeps Build open and shows the validation
errors via `StatusMessage` (`BuildViewModel.SetBlockedLeaveMessage`).
The edits are saved first either way. Back never validates: it saves the
drawing as it stands (#474, #368). Training refuses a saved creature that
cannot train and returns to Creations.

## Touch input

- `BuildCanvas` converts raw pointer positions to its own local space
  with `GetGlobalTransformWithCanvas().AffineInverse() * screenPosition`, not
  plain `ToLocal()`. Plain `ToLocal()`/`GetGlobalTransform()` ignore the
  project's `canvas_items` stretch transform, so on a device whose native
  resolution differs from the logical viewport (see `docs/UI_DIRECTION.md`), taps would land on the
  wrong in-canvas position relative to what's rendered. Any future widget
  that hit-tests pointer input against drawn content should use the same
  pattern.
- `project.godot` keeps `input_devices/pointing/emulate_mouse_from_touch`
  enabled so Godot controls receive their native mouse-style input on Android
  touch devices. `BuildCanvas` is the exception: it needs every finger
  for pinch zoom, so it reads `InputEventScreenTouch`/`InputEventScreenDrag`
  by index and ignores the emulated mouse copy
  (`InputEvent.DeviceIdEmulation`). A real mouse still drives one pointer on
  desktop; mouse zoom is out of scope. Godot's Android pan and scale
  gestures (`input_devices/pointing/android/enable_pan_and_scale_gestures`)
  stay off: on the S25 each event arrived twice and two-finger drags mostly
  stopped, and Godot scales pan to a scroll distance, drops large pinch
  steps and gives no pinch while one finger is already dragging, which is
  how Build's two-finger gesture starts (#564).
