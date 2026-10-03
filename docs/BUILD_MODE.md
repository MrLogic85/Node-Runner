# Build mode workflow

How Build works: its interaction and state flow. It began as the notes for
the 0.3.0 prototype "Bygg figuren" (see `docs/ROADMAP.md`) and is kept up to
date with the app; together with `docs/UI_DIRECTION.md` it owns the current
Build. `reference design/components/Build/README.md`, `BuildLocked/README.md`
and `Navigation/README.md` are its guide. When a Creation counts as locked is
owned by `docs/TRAINING_LOOP.md` → Product lifecycle boundary.

## Mode

- The 0.3.0 prototype had one main screen with two modes, **Simulate** and
  **Build**, and a HUD toggle between them. That toggle is gone: Build and
  Training are separate routed scenes (#469), and Build opens one saved
  creation's Build canvas.
- Build state lives in `NodeRunner.App.ViewModels.BuildViewModel`
  while Build is open. Build always edits a saved creation: + New saves an
  empty "Untitled Creation" (`NewCreationWorkflow`) before Build opens. Its
  brain is direct (#536), so there is no brain to set up.
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
  move-only: nodes can move and cameras can be aimed, but no parts or brain
  shape change. Only what changes the model is locked (#638). The padlock
  in the top bar unlocks it for this visit and keeps the training (#371,
  `docs/TRAINING_LOOP.md` step 6). Once a creation has training, the
  overflow menu also offers Stats (coming soon), Copy creation and Reset
  training (#370); the copy keeps the trained brain. Reset training asks
  first with a press-and-hold (#687).

## Coordinates

- Build-mode positions are plain 2D coordinates in the same local
  space the Worm example uses (see `CreationExamples`): no unit
  conversion. The only view math is the Build canvas's zoom and pan
  (`CanvasView`), which never touches saved positions.

## Interactions

The rail holds the tools Parts, Beams, Joint and Select (#365, #705, #706);
"joint" is the player-facing name for a node. Start training is the play
button at the bottom of the rail in both states (#370); it is dimmed until
the creature can train, and the panel's last line says why.
Build always opens in Parts. `BuildGestures` (App) turns pointer
presses, drags and releases into edits for the active tool and into zoom and
pan, and `BuildCanvas` only forwards input and draws. A pointer that
travels at most `TapSlop` view units counts as a tap. Hit tests prefer a node
over a beam under it; hit sizes are finger-sized on screen at any zoom, and
a node's own ring always hits.

- **Parts:** tap a joint, beam, sensor or Piston to select it (its settings
  open), tap empty canvas to deselect, drag a joint to move it, drag anywhere
  else (empty canvas or a beam) to pan the view (#400). Parts never adds a
  node.
- **Beams:** with nothing selected, the panel lists link types: Beam, Piston
  and later Spring and Wing. Beam is picked each time the tool is entered;
  selecting a part and clearing it keeps the picked link. Tap a joint, beam,
  sensor or Piston to select it; tap empty canvas to clear. Drag from a
  selected joint to move only that joint. Drag from an unselected joint to a
  different joint to draw the picked link. A Beam preview only snaps to a node
  the beam could join (`BuildViewModel.CanConnect`); releasing anywhere else
  adds nothing. A Piston preview uses the refusals and canvas notes in the
  Piston bullet below. Drag anywhere else pans. Beams never adds a node.
- **Joint:** tap empty canvas to add a node, or tap a beam to split it at the
  closest point: one change that replaces the beam with two through the new
  node (`BuildViewModel.SplitBeam`).
- **Select (#366, #704):** two or more selected joints are a *group*, with
  a dashed frame and three `UiSelectionHandle`s: **Move** in the middle (or
  drag anywhere inside the frame, or a selected joint), **Rotate** on a stem
  above and **Scale** at the bottom-right corner.
  - A tap on any joint, beam, sensor or Piston adds or removes it, even
    under a handle; an empty tap clears (`BuildViewModel.ToggleSelected`).
  - With a group, any other drag pans. With none, a drag from a joint moves
    it (selecting only it unless it is selected), and any other drag draws a
    box that selects every part whose centre is in it: a joint's centre, a
    beam's or Piston's midpoint, a sensor's beam midpoint. It replaces the
    selection, so a box can catch only beams.
  - After a Rotate the frame stays turned until the selection changes
    (`docs/UI_DIRECTION.md` → "The Select frame keeps its turn"). Rotate
    and Scale turn about the frame's centre; Scale counts only the drag
    along its diagonal and is clamped to
    `MinSelectionScale`..`MaxSelectionScale`. Every drag frame is computed
    from a `SelectionSnapshot` taken when the drag starts, so nothing
    drifts.
  - A move stops the group at the build area's edge; a turn or scale that
    would leave it is ignored. The frame and handles keep their screen size
    at any zoom, and each handle hits within `HandleHitRadius`. A locked
    creation keeps all three handles: scaling changes only beam lengths.
- **Sensors:** an Accelerometer (#127) and a Camera (#575) sit on a
  beam, one per beam. Drag one from the Parts tray onto a beam to place it
  (#376; see Parts tray below). Deleting a beam deletes its sensor;
  splitting a beam with the Joint tool moves it, with its id, to the
  longer half.
- **Piston (#451, #705):** picked from the Beams link list. Drag joint to
  joint to place one; over a joint that would refuse it, the line and that
  joint's ring turn dashed danger, and dropping there shows the reason at the
  joint: "A beam already joins these nodes" or "These nodes already have a
  piston". Dropping away from a joint says "Drop it on another node." and
  never adds one. The picked link stays after placement until another tool is
  entered. A new Piston is not selected. Taps hit a joint, then a sensor, then
  a Piston, then a beam. Deleting a joint deletes its Pistons.
- **Camera aim (#594, #622):** a Camera selected alone shows its rays and an Aim
  handle out along its centre ray past its picture, in any tool, with no
  stem line. It always sits twice as far from the camera's middle as a
  handle just clear of the picture would, so it follows the zoom smoothly; it may cover a joint, which then can't be tapped there while
  the camera is selected (#639). Its Part settings note adds "Drag
  the round handle to aim it." Dragging the handle turns the camera
  smoothly to look at the finger (`CameraRays.AimAlong`); the aim is saved
  relative to the beam, so the camera turns with it. The handle is hit before
  anything under it, a tap on it does nothing, and a second finger puts the
  aim back (Build has no undo). A locked creation keeps the Aim handle:
  aim changes no brain port (#638).
- There is no Delete tool: the part settings and selection panels delete the
  selection, and deleting a node removes every beam on it and those beams'
  sensors (`CreatureBuilder.RemoveNode`).
- A locked creation opens in Parts with Beams and Joint disabled, and
  `BuildViewModel` refuses topology edits on its own.
- **Two fingers, any tool (#400):** pinch zooms about the point between the
  fingers and dragging both pans. The second finger cancels the first
  finger's gesture, putting back any node it moved and any selection a
  Select press changed, and nothing edits
  until every finger lifts, so navigation never changes the creature.
- **Build area (#400):** joints live inside the fixed
  `BuildViewModel.BuildArea` (x −1152..1152, y −576..576 canvas
  units, about six screens wide at 1×). Placing or moving a joint keeps its
  ring inside; a group move stops as a whole at the edge, and a Joint tap
  outside adds nothing. A faint blueprint grid (the `line` token, fixed
  `BuildGridStep` 48-unit cells) covers exactly the area, and accent corner
  marks two cells long frame it. Grid and corners are fixed parts of the
  picture; zoom never changes their cells or length.
- **View:** `CanvasView` (App) holds zoom and pan and maps view units to
  canvas units. It is not saved: Build opens with the creation centred and
  `FitMargin` (20%) of air on every side, zoomed out if needed but never
  magnified past true size; an empty creation opens at true size on the
  middle of the area. True size is 1× at a UI size root factor of 1; the zoom limits are
  divided by that factor (`CanvasView.UiScale`), so true size and the zoom limits
  keep their size on screen; only the space changes, and with it how far Fit zooms out (#299). The view can show `BuildViewBounds`: the area plus
  `BuildViewMargin` (one cell, 48 canvas units) on every side, the same at
  any zoom. Zooming out stops when all of it is in view (`MinZoom`), up to
  `MaxZoom` in. Along an axis where the bounds are larger than the view,
  panning stops at their edge; along an axis where they fit, they are
  centred. Godot's Camera2D would replace only this clamp, so Build keeps
  `CanvasView` (#564). Zoomed in, the creation can be off screen; zooming
  out finds it. Distances are in view or canvas units (`docs/GLOSSARY.md` →
  Build canvas). How zoom treats lines, the grid and labels is owned by
  `docs/UI_DIRECTION.md` → Departures from the reference.
- Changing tool mid-gesture, or Android cancelling the touch, cancels the
  gesture the same way.
- `BuildViewModel.StatusMessage` records the outcome of the last
  edit (including `ConnectBeam` refusing a pair as a safety net); the Build
  screen does not show it yet.

## Build to Training

- **Wire into simulation** (issues #72, #469): Build and Training are
  separate scenes, joined only through the save. Start training saves the
  drawing, then opens Train setup (`TrainSetupRoute`, #194), whose Start
  opens Training (`TrainingRoute`); it stays in Build if the creature cannot
  train yet. The
  Training scene builds its `Creature` node from the saved `CreatureDef`
  (`Creature.BuildFrom`), which generically derives the model's
  input/output counts (`BrainPorts.Of`: the sensor parts' readings, each Piston's
  inputs, and its outputs) for whatever anatomy it is given — no
  special-casing between the Worm example and an edited creature.

## Parts tray

With nothing selected, an unlocked creation's side panel shows the active
tool's panel: Parts shows the Parts tray (#374), Beams shows the link list
(#705), and Joint and Select show scene-authored short help (#706). The tray has three
`UiIconTabs` (On a joint, Sensors, Blocks) pinned at the top, then a scrolling
list with the open tab's name, its parts as compact `UiPartRow`s and one help
line for the tab. `NodeRunner.App.ViewModels.PartTray`
owns the groups, their order, the help lines and each row's state; the screen
only maps parts to glyphs. Every implemented part is unlimited until #525, so
rows show no count. A part not yet implemented is a dashed row with a lock,
and the tab's name row says "Coming later" once. The available rows (today the
Accelerometer and the Camera) do nothing on tap; they are dragged out instead
(#376). Godot's drag-and-drop carries the part: the row starts it and
floats its glyph above the finger (`UiPartRow.CreateDragPreview`), and
`BuildCanvas` takes the drop in `PartDropZone`, a control over the canvas
that lets touches through except during a part drag. `BuildGestures.DropTargetAt`
finds what the part is over (a joint's ring, a sensor picture's beam, a beam
within reach, then a joint within reach) and `BuildViewModel.PlacePart`
validates and places it with a fresh id, or refuses it and keeps the reason as
`PlacementNote`, a canvas note at that part until the next touch or after
3 s. A drop on empty canvas or back on the panel changes nothing. One selected
part shows its settings and several show the selection panel instead.

## Part settings

One selected joint, beam, sensor or Piston shows its Part settings in the side
panel (#343). The panel's own title row carries the part's glyph and name;
there is no close button, and tapping empty canvas deselects. The rows are
`UiTextField` **Name** first, then what the part is joined to (a joint's
beams, a beam's two joints, a sensor's beam), then a short note, and one
full-width danger **Delete** in its own column after them, absent on a
locked creation. Delete is hold-to-activate, so a slip never removes a
part. Structure is read-only here: a beam's length is drawn, so its note
says "Drag its ends to change the length." instead of a number.
`BuildPresentationViewModel.SinglePart` owns the rows and copy. A part with
no name of its own shows a default (`BuildViewModel.DefaultPartName`: "Node 2",
"Beam 1" or the sensor kind) that follows its place in the lists; renaming
(`RenamePart`, by id, so an edit lands on the part it started on even if
the selection moves) trims the text, and a blank name or the default
clears the part's own name. Names are labels only (#220), so a locked
creation can be renamed too; the rename autosaves like any edit.

### Parameters (#704)

A part's settings are parameters (`PartParameters`), plain data: an id, whether
several selected parts can share one value (`MultiEditable`), and a slider
when the panel shows it (`InPanel`). Each kind of part lists its own
(`CreatureBuilder.ParametersOf`): a Piston has **Max strength** (20–400 N,
step 10), **Stroke** (±10–50%, step 5) and **Max speed** (0.5–4.0 m/s, step
0.1); a Camera has **Aim**, set on the canvas and one Camera at a time.

The selection can change one part's own parameters, or those every selected
part has and can share (`BuildViewModel.EditableParameters`). The panel shows a
slider for each that is `InPanel`, and a slider sets its value on every
selected part (`SetParameter`). The canvas shows what a parameter changes
only while it can be changed: a Piston's stroke ticks while Stroke can, a
Camera's rays and aim handle while Aim can. Parameters change no brain port,
so a locked creation keeps them.

A Piston's rows are Name, then its sliders instead of what it is joined to,
then the note "The brain pushes it out and pulls it in, within its stroke."

## Selection panel

Several selected parts show the selection panel instead (#558, #704). Its
title row carries the Select glyph and "N selected"; there is no close button.
- First a slider for each parameter they share (see Parameters), with the
  note "A slider sets one value for all of them." Differing values look as
  `docs/UI_DIRECTION.md` says.
- With a frame, three `UiInfoRow`s explain its handles (Move, Rotate, Scale).
  With neither settings nor a frame: "These parts share no settings."
- Last a full-width hold-to-activate danger **Delete N**
  (`BuildViewModel.DeleteSelectedParts`), hidden when locked. Its note is
  "Beams on a deleted node go with it." with a joint selected, else "A sensor
  on a deleted beam goes with it." when one would, else none.

`BuildPresentationViewModel.Selection` owns the copy.

## Validation

A saved Creation stores any drawing: `CreatureDef` only checks that part
ids are unique and below `NextPartId`, that beams point at existing node
ids and sensors at existing beam ids (one sensor per beam), so an empty or unfinished creature is still
a Creation (#515). Only training needs a finished creature.
`NodeRunner.App.Lifecycle.CreatureReadiness` is the single source of truth
for that, in two steps: `Problems` lists why the creature cannot be
simulated yet (no nodes, a node with no beam or Piston, a zero-length beam
or Piston, or one shorter than `CreatureReadiness.MinimumBeamGap` between its
joint rings, #593), and `CanTrain` also needs at least one brain output to
drive. Joints are passive (#450), so today that means a Piston; Build says
"Add a piston" until there is one.
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
