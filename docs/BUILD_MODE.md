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
  empty "Untitled Creation", named in the player's language
  (`NewCreationWorkflow`), before Build opens. Its brain is direct (#536),
  so there is no brain to set up.
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
  overflow menu also offers Stats (coming soon) and Reset training (#370).
  Reset training asks first in a dialog confirmed with a tap (#687, #866). Copy creation is
  there for anything drawn, trained or not; the copy, named "Copy of …",
  keeps any trained brain and Build opens it in place of the original
  (#840).
- Undo and Redo (#689, `BuildViewModel.Undo`/`Redo`, `BuildHistory`) are
  icon buttons in the top bar between the padlock and the overflow
  (`docs/UI_DIRECTION.md` → "Build has Undo"), and work on a locked
  creation too. Each finished action is one step: a placed part, a link, a
  delete, a part rename, a whole drag (a joint, a selection's move/turn/scale, a camera's aim) or a
  whole slider drag. A step only counts if the body changed. Selection,
  tool, zoom and pan never count, and a cancelled gesture adds nothing and
  keeps Redo. A new step clears Redo; the history holds 100 steps and
  lasts for one Build visit, so opening Build (also after Train setup,
  Reset training or Copy creation) starts it empty. Renaming the creation, Unlock, Reset
  training and Copy creation are not steps. Undo keeps the selected parts
  that still exist, except that undoing a delete selects the deleted parts
  again (#878), and Redo of that delete clears them. A selected Servo
  stays selected when Undo or Redo swaps its links, though its id changes
  (#911). Undo never lowers
  `NextPartId`, so part ids stay unique (#220). Saves refit the brain Build opened with (`OpenedBrain`,
  `docs/CREATURE_MODEL.md` → "Build refits the brain it opened with"), so
  an undone delete gets its trained weights back. Each edit has an owner,
  the canvas or the slider, so one never closes or drops the other's; a
  canvas tap while a slider is held is a step of its own. Undo and Redo do
  nothing while an edit is open, so a second finger cannot undo under a
  drag.

## Coordinates

- Build-mode positions are plain 2D coordinates in the same local
  space the Walker example uses (see `CreationExamples`): no unit
  conversion. The only view math is the Build canvas's zoom and pan
  (`CanvasView`), which never touches saved positions.

## Interactions

The rail holds the tools Parts, Links, Joint and Select (#365, #705, #706, #913);
"joint" is the player-facing name for a node. Start training is the play
button at the bottom of the rail in both states (#370); it is dimmed until
the creature can train, and the panel's last line says why.
Build always opens in Parts. `BuildGestures` (App) turns pointer
presses, drags and releases into edits for the active tool and into zoom and
pan, and `BuildCanvas` only forwards input and draws. A pointer that
travels at most `TapSlop` view units counts as a tap. Hit tests prefer a node
over a beam under it. A joint's touch area (its ring plus the selection
gap, #710) and a sensor's picture are in canvas units, so they grow and
shrink with the drawing; beams, links and handles have finger-sized hit
sizes on screen at any zoom.

- **Every tool (#746, #803)** shares one selection model; the tools differ
  only in their main action, below.
  - A tap on any joint, beam, sensor or link adds it to the selection or
    removes it, even under a handle (`BuildViewModel.ToggleSelected`). A
    tap on empty canvas clears the selection; a tap on a handle over empty
    canvas does nothing.
  - Two or more selected joints are a *group*, with a dashed frame and
    three `UiSelectionHandle`s: **Move** in the middle, **Rotate** on a stem
    above and **Scale** at the bottom-right corner.
  - A drag is settled by where it starts, first match wins: a handle moves,
    turns or scales the selection; a selected joint moves the selection;
    in Links, an unselected joint draws a link; anywhere inside a group's
    frame moves the group; an unselected joint is selected alone and moved.
    Any other drag draws a box in Select and pans in the other tools.
  - After a Rotate the frame stays turned until the selection changes
    (`docs/UI_DIRECTION.md` → "The selection frame keeps its turn"). Rotate
    and Scale turn about the frame's centre; Scale counts only the drag
    along its diagonal and is clamped to
    `MinSelectionScale`..`MaxSelectionScale`. Every drag frame is computed
    from a `SelectionSnapshot` taken when the drag starts, so nothing
    drifts.
  - A move stops the group at the build area's edge; a turn or scale that
    would leave it is ignored. The frame and handles keep their screen size
    at any zoom, and each handle hits within `HandleHitRadius`. A locked
    creation keeps all three handles: scaling changes only beam lengths.
- **Parts:** with nothing selected, the panel shows the Parts tray; drag a
  part from it onto the creature (see Parts tray below). Parts never adds a
  node.
- **Links:** with nothing selected, the panel lists link types: Beam, Piston,
  Spring and later Wing, with no group header, since the panel's title
  already says Links (#913); the locked Wing row shows only its lock, with
  no "Coming later" line. Beam is picked when Build opens; the picked link
  then stays for the visit, across tool switches and selections (#874). It
  is not saved. Drag from an
  unselected joint to a different joint to draw the picked link. Every
  link, a Beam too (#877), uses the refusals and canvas notes in the Piston
  bullet below (`BuildViewModel.CanConnectLink`); a pair a Beam already
  joins refuses another Beam with "A beam already joins these nodes".
  While dragging, the line is dashed over no joint, solid once it will
  attach, and dashed danger with a crossed ring at its midpoint when the
  joint would refuse (#920). Links never adds a node.
- **Joint:** with nothing selected, a tap on empty canvas adds a node; with
  a selection, that tap only clears it. A beam tap selects the beam like in
  every tool: beams are never split (#746).
- **Select (#366, #704):** a drag on empty canvas, or from a beam, sensor or
  link, outside any group's frame draws a box. The box selects every part whose
  centre is in it: a joint's centre, a beam's or link's midpoint, a
  sensor's beam midpoint. It replaces the selection, so a box can catch
  only beams.
- **Sensors:** an Accelerometer (#127) and a Camera (#575) sit on a
  beam, one per beam. Drag one from the Parts tray onto a beam to place it
  (#376; see Parts tray below). Deleting a beam deletes its sensor. The
  tray holds the Camera back as "Coming later" (#852): on the Flat map it
  only adds complexity, so it returns with maps that have terrain (#855). A saved
  creation that already has one keeps it, and it works as below.
- **Piston (#451, #705):** picked from the Links tool's list. Drag joint to
  joint to place one; over a joint that would refuse it, the line and that
  joint's ring turn dashed danger, with a crossed ring on the line (#920),
  and dropping there shows the reason at the joint: "A beam already joins
  these nodes" or "These nodes already have a piston". Dropping away from a
  joint places nothing and shows nothing. The picked link stays after
  placement. A new Piston is not selected. Taps hit a joint, then a sensor,
  then a Piston, then a beam. Deleting a joint deletes its Pistons.
- **Spring (#453):** placed like a Piston, with the same refusals; a pair
  that has a Spring refuses another link with "These nodes already have a
  spring". Taps treat Pistons and Springs alike as links: after a sensor
  and before a beam, the nearest link is hit, a Spring on a tie. Deleting a
  joint deletes its Springs. A locked creation cannot add one.
- **Servo (#452, #577):** dragged from Parts → On a joint onto a node with
  two or more links (Beam, Piston or Spring). Dropping on a beam, sensor or link
  refuses with "Joint parts go on a joint", on a one-link node with "A Servo
  needs two links at its joint"; a joint that already has one
  refuses with "One part per joint". A good drop chooses the two lowest-id
  links as Fixed and Target, records one undo step and selects the Servo.
  Tapping that joint selects the Servo, but dragging still moves the joint.
  Deleting a held link keeps the Servo with that role missing and blocks
  training until a replacement is picked or the Servo is deleted.
- **Camera aim (#594, #622):** a Camera selected alone shows its rays and an Aim
  handle out along its centre ray past its picture, in any tool, with no
  stem line. It always sits twice as far from the camera's middle as a
  handle just clear of the picture would, so it follows the zoom smoothly; it may cover a joint, which then can't be tapped there while
  the camera is selected (#639). Its Part settings note adds "Drag
  the round handle to aim it." Dragging the handle turns the camera
  smoothly to look at the finger (`CameraRays.AimAlong`); the aim is saved
  relative to the beam, so the camera turns with it. The handle is hit before
  anything under it, a tap on it does nothing, and a second finger puts the
  aim back; a finished aim drag is one Undo step. A locked creation keeps the Aim handle:
  aim changes no brain port (#638).
- There is no Delete tool: the part settings and selection panels delete the
  selection, and deleting a node removes every link on it, its Servo, and
  those beams' sensors (`CreatureBuilder.RemoveNode`).
- A locked creation opens in Parts with Links and Joint disabled, and
  `BuildViewModel` refuses topology edits on its own.
- **Two fingers, any tool (#400):** pinch zooms about the point between the
  fingers and dragging both pans. The second finger cancels the first
  finger's gesture, putting back any node it moved and any selection its
  press changed, and nothing edits
  until every finger lifts, so navigation never changes the creature.
- **Build area (#400):** joints live inside the fixed
  `BuildViewModel.BuildArea` (x −600..600, y −300..300 canvas
  units, 12 × 6 m), small enough that any creature fits the Training view
  without zooming out far (#884). Placing or moving a joint keeps its
  ring inside; a group move stops as a whole at the edge, and a Joint tap
  outside adds nothing. A faint blueprint grid (the `line` token, fixed
  `BuildGridStep` cells of 50 units, half a metre) covers exactly the area, and accent corner
  marks two cells long frame it. Grid and corners are fixed parts of the
  picture; zoom never changes their cells or length.
- **View:** `CanvasView` (App) holds zoom and pan and maps view units to
  canvas units. It is not saved: Build opens with the creation centred and
  `FitMargin` (20%) of air on every side, zoomed out if needed but never
  magnified past true size; an empty creation opens at true size on the
  middle of the area. True size is 1× at a UI size root factor of 1; the zoom limits are
  divided by that factor (`CanvasView.UiScale`), so true size and the zoom limits
  keep their size on screen; only the space changes, and with it how far Fit zooms out (#299). The view can show `BuildViewBounds`: the area plus
  `BuildViewMargin` (one cell, 50 canvas units) on every side, the same at
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
- A refused edit (for example `ConnectBeam` refusing a pair as a safety
  net) changes nothing; where the player needs a reason, the canvas note
  next to the part gives it.

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
  special-casing between the Walker example and an edited creature.

## Parts tray

With nothing selected, an unlocked creation's side panel shows the active
tool's panel: Parts shows the Parts tray (#374), Links shows the link list
(#705), and Joint and Select show scene-authored short help (#706). The tray has three
`UiIconTabs` (On a joint, Sensors, Blocks) pinned at the top, then a scrolling
list with the open tab's name, its parts as compact `UiPartRow`s and one help
line for the tab. Each Build visit opens the tray on the first tab with an
available part (`PartTray.OpeningGroup`, today On a joint), so a tab of padlocks
never reads as every part being locked (#887). The open tab then stays for the
visit, across tool switches (#874). `NodeRunner.App.ViewModels.PartTray`
owns the groups, their order, the help lines and each row's state; the screen
only maps parts to glyphs. Every implemented part is unlimited until #525, so
rows show no count. A part not yet implemented is a dashed row with a lock,
and the tab's name row says "Coming later" once; the Camera is held back the
same way (#852). The available rows (today Servo and Accelerometer) do nothing on tap; they are dragged out instead
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

One selected joint, beam, sensor or link shows its Part settings in the side
panel (#343). The panel's own title row carries the part's glyph and name;
there is no close button, and tapping empty canvas deselects. The rows are
`UiTextField` **Name** first, then what the part is joined to (a beam's two
joints, a sensor's beam; a joint, a Piston and a Spring list nothing, as
the canvas shows them, #913), then a short note, and one
full-width danger **Delete** in its own column after them, absent on a
locked creation. Delete acts on a tap, with no dialog; Undo brings the
part back, selected again (#866, #878). Structure is read-only here: a beam's length is drawn, so its note
says "Drag its ends to change the length." instead of a number.
`BuildPresentationViewModel.SinglePart` owns the rows and copy. A part with
no name of its own shows a default (`BuildViewModel.DefaultPartName`: "Node 2",
"Beam 1", "Accel" or "Camera"; at most 10 characters, see `docs/UI_DIRECTION.md`
"Name length") that follows its place in the lists; renaming
(`RenamePart`, by id, so an edit lands on the part it started on even if
the selection moves) trims the text, and a blank name, or the default the
field showed in the player's language left unchanged, clears the part's own
name. Names are labels only (#220), so a locked
creation can be renamed too; the rename autosaves like any edit.

### Parameters (#704)

A part's settings are parameters (`PartParameters`), plain data: an id, whether
several selected parts can share one value (`MultiEditable`), and a slider
when the panel shows it (`InPanel`). Each kind of part lists its own
(`CreatureBuilder.ParametersOf`): a Piston has **Max strength** (20–400 N,
step 10), **Stroke** (10–100% of its shortest gap between its joints' edges, step 5), **Start
position** (0–100%, step 5; where its drawn length sits in its travel, #870),
**Max speed** (0.5–4.0 m/s, step
0.1) and **Rise time** (0.1, 0.2, 0.5 or 1 s, evenly spaced along the slider
so the short ones are as easy to pick, #801); a Spring has **Stiffness**
(50–2000 N/m, step 50), **Damping** (0–100 N·s/m, step 1), **Stroke** (as
the Piston's) and **Coil length** (0–100%, step 1; moves its rest length evenly
from half its drawn gap short of its shortest stop to as far past its
longest, #835; a new Stroke keeps it); a Servo has
**Max strength** (5–200 N·m), **Range** (20°–360°), **Start position**
(0–100%), **Max speed** (30°/s–720°/s) and **Rise time**, followed by
Fixed/Target link pickers; a Camera has **Aim**, set on the canvas and one
Camera at a time.

The selection can change one part's own parameters, or those every selected
part has and can share (`BuildViewModel.EditableParameters`). The panel shows a
slider for each that is `InPanel`, and a slider sets its value on every
selected part (`SetParameter`). The canvas shows what a parameter changes
only while it can be changed: a Piston's stroke ticks while Stroke or Start
position can, a Spring's, with a ring at its rest length, while Stroke or Coil length can, a Camera's rays and aim handle while Aim can. Parameters change
no brain port, so a locked creation keeps them.

A Piston's rows are Name, then its sliders instead of what it is joined to,
then the note "The brain pushes it out and pulls it in, within its stroke."
A Servo's rows are Name, sliders, "Fixed link" and "Target link" pickers,
then "The brain picks an angle and how much of its max strength to use."
When a role is missing, the picker reads "Pick a Fixed link" or "Pick a
Target link" in danger colour, and its list holds only the real links. If
the joint has fewer than two links, the note under it and the canvas
callout say "A Servo needs two links at its joint" instead, and the
Play-blocked reason asks to connect another link there, because no pick
could fix it. Changing a picker follows
`docs/CREATURE_MODEL.md` → "Editing identity rules".
A Spring's are Name, its sliders, then "It springs toward the ring, which
Coil length moves. With the ring past an end mark, it starts pressed against
that end. Damping stops it bouncing." A Piston
and a Spring selected together share Stroke.

## Selection panel

Several selected parts show the selection panel instead (#558, #704). Its
title row carries the Select glyph and "N selected"; there is no close button.
- First a slider for each parameter they share (see Parameters), with the
  note "A slider sets one value for all of them." Differing values look as
  `docs/UI_DIRECTION.md` says.
- With a frame, three `UiInfoRow`s explain its handles (Move, Rotate, Scale).
  With neither settings nor a frame: "These parts share no settings."
- Last a full-width danger **Delete N**, which acts on a tap (Undo restores it)
  (`BuildViewModel.DeleteSelectedParts`), hidden when locked. Its note is
  "Links on a deleted node go with it." with a joint selected (#913), else "A sensor
  on a deleted beam goes with it." when one would, else none.

`BuildPresentationViewModel.Selection` owns the copy.

## Validation

A saved Creation stores any drawing: `CreatureDef` only checks that part
ids are unique and below `NextPartId`, that beams point at existing node
ids and sensors at existing beam ids (one sensor per beam), so an empty or unfinished creature is still
a Creation (#515). Only training needs a finished creature.
`NodeRunner.App.Lifecycle.CreatureReadiness` is the single source of truth
for that, in two steps: `Problems` lists why the creature cannot be
simulated yet (no nodes, a node with no beam or link, a Servo missing a
Fixed or Target link, a zero-length beam or link, or one shorter than `CreatureReadiness.MinimumBeamGap` between its
joint rings, #593), and `CanTrain` is true when there are none. It needs
no powered part (#845): "Add a motor or piston" would stop being right as more
powered parts come, so a creature with nothing for its brain to drive
trains and stands still, and Train setup warns about it
(`docs/TRAINING_LOOP.md` step 2).
`CreatureBuilder.TryBuild` applies `Problems` to the in-progress creature.
UI surfaces those messages and does not duplicate the rules. The one
exception is Build's readiness line, which shortens the errors for the
narrow side panel (for example "1 node not connected"); it only changes the
wording.

Start training is gated by `BuildViewModel.TryLeave` and
`CanTrain`: a failed `TryLeave` keeps Build open, and the readiness line
already says why. The dimmed play button can still be tapped (#844): the
tap starts nothing, and `BuildViewModel.ShowTrainingBlockers` gives each
joint loose at that moment a "Not connected" canvas note until it is
joined or removed. Too-short parts always have their "Too short" note.
The edits are saved first either way. Back never validates: it saves the
drawing as it stands (#474, #368). Training refuses a saved creature that
cannot train and returns to Creations.

## Touch input

- `BuildCanvas` lives in the world of the `BuildView` `UiWorldView` (#769).
  The view takes every pointer event in its `_GuiInput`, maps it into the
  world's pixels and pushes it in, where `BuildCanvas` reads it in
  `_UnhandledInput`. It maps those positions to its own space with
  `GetGlobalTransformWithCanvas().AffineInverse() * position`, which includes
  the world viewport's canvas transform; plain `ToLocal()` does not. Any
  widget that hit-tests pointer input against drawn content should do the
  same. A part dragged from the tray is not a pointer event in
  the world: the canvas asks the root viewport about the drag and maps the
  slot's mouse position into its own space (`SlotTransform`).
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
