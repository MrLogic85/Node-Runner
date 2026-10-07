# Build mode

How Build behaves: its tools, gestures, selection, panels and validation.
How it looks is in `docs/UI_DIRECTION.md` and `docs/WORLD_VISUALS.md`; when
a Creation is locked is in `docs/TRAINING_LOOP.md` → Product lifecycle
boundary. `reference design/components/Build/README.md`,
`BuildLocked/README.md` and `Navigation/README.md` are its guide.

## Mode

- Build state lives in `BuildViewModel` while Build is open. Build always
  edits a saved creation: + New saves an empty "Untitled Creation", named in
  the player's language (`NewCreationWorkflow`), before Build opens. Its
  brain is direct (#536), so there is no brain to set up.
- Build and Training share only the save (#469); the play button saves and
  opens Train setup (`docs/TRAINING_LOOP.md` step 1).
- **Autosave (#368).** There is no Save button. Every edit is saved once
  edits have settled for 0.5 s, and when Build is left (Back, Start
  training, another scene) or the app pauses or closes. A failed save keeps
  the edits unsaved and tries again on the next save; Back, Start training
  and Reset training report every failure, background saves only the first
  in a row. Back always leaves, so a failing disk never traps the player in
  Build. A creation opened by + New that has no nodes when Build is left is
  removed again; if Android ends the app in the background, it stays in
  the list.
- **Locked Build.** A locked creation (`CreationLock`) opens in Joint like
  any other, and `BuildViewModel` refuses on its own what changes the model
  (#638, #896). Joints, beams and Springs have no brain ports, so they can
  be added and deleted; joints move, cameras aim, the selection handles all
  work (scaling changes only beam lengths), parameters change and parts can
  be renamed. A part with ports (a sensor, Piston or Servo) can be neither
  added nor deleted: every tray row and the Piston row are locked, the
  tray's help line says "Unlock to add parts.", and tapping a locked row
  shows "Locked: the model is trained for these parts."
  (`BuildViewModel.LockedReason`) in a notification. Delete stays,
  Unavailable, when it would take a part with ports along, also by cascade,
  or clear a Servo's link (`BuildViewModel.DeleteLockedReason`); a tap shows
  the same notification. Copy is hidden. There is no training summary: the
  Creations card shows the latest training (#479). The padlock unlocks it
  for this visit and keeps the training (`docs/TRAINING_LOOP.md` step 6).
- **Overflow menu.** It follows training, not the lock. A trained creation
  lists Stats (#198) and Power budget (#460), both unavailable, then Copy
  creation, Reset training and Delete creation; an untrained one lists
  Power budget, Copy creation and Delete creation. Copy creation is offered
  for anything drawn, trained or not (#840): it saves edits first, keeps
  any trained brain, names the copy "Copy of …", opens it in place of the
  original and confirms with a notification. Reset training (#370, #687)
  and Delete creation each ask first in a dialog confirmed with a tap
  (#866); Reset training's names the generations lost.
- **Undo and Redo (#689, `BuildHistory`)** sit in the top bar and work on a
  locked creation too; each is disabled when there is nothing to undo or
  redo. One finished action that changes the body is one step: a placed
  part, a link, a delete, a part rename, or a whole drag (a joint, a
  selection's move, turn or scale, a camera's aim, a slider). Selection,
  tool, zoom and pan never count, and a cancelled gesture adds nothing and
  keeps Redo. A new step clears Redo. A visit holds 100 steps; opening
  Build, also after Train setup, Reset training or Copy creation, starts
  empty. Renaming the creation, Unlock, Reset training, Copy creation and
  Delete creation are not steps and cannot be undone.
  - Undo keeps the selected parts that still exist; undoing a delete
    selects the deleted parts again and Redo of it clears them (#878). A
    selected Servo stays selected when Undo or Redo swaps its links (#911).
    An undone delete gets its trained weights back
    (`docs/CREATURE_MODEL.md` → "Build refits the brain it opened with").
  - Each edit has an owner, the canvas or a slider, so one never closes or
    drops the other's; a canvas tap while a slider is held is a step of its
    own. Undo and Redo do nothing while an edit is open, so a second finger
    cannot undo under a drag.

## Interactions

The rail holds the tools Joint, Links, Parts and Select (#365, #705, #706,
#913); "joint" is the player-facing name for a node. Start training is the
play button at the bottom of the rail in both states (#370). Build opens in
Joint, the default tool, locked or not (#896). `BuildGestures`
turns pointer presses, drags and releases into edits for the active tool
and into zoom and pan; `BuildCanvas` only forwards input and draws.

- A pointer that travels at most `TapSlop` view units is a tap. A tap hits a
  joint first, then a sensor, then a link (the nearest, a Spring on a tie),
  then a beam, in Build and Training alike. A joint's touch area (its ring
  plus the selection gap, #710) and a sensor's picture are in canvas units,
  so they grow and shrink with the drawing; beams, links and handles have
  finger-sized hit areas on screen at any zoom.
- **Every tool (#746, #803)** shares one selection model; the tools differ
  only in their main action, below.
  - A tap on any joint, beam, sensor or link adds it to the selection or
    removes it, even under a handle (`BuildViewModel.ToggleSelected`). A
    tap on empty canvas clears the selection; a tap on a handle over empty
    canvas does nothing.
  - Two or more selected joints are a *group*, with a frame and three
    handles: **Move** in the middle, **Rotate** on a stem above and
    **Scale** at the bottom-right corner.
  - A drag is settled by where it starts, first match wins: a handle moves,
    turns or scales the selection; a selected joint moves the selection;
    in Links, an unselected joint draws a link; anywhere inside a group's
    frame moves the group; an unselected joint is selected alone and moved.
    Any other drag draws a box in Select and pans in the other tools.
  - After a Rotate the frame stays turned with the group until the
    selection changes. Rotate and Scale turn about the frame's centre;
    Scale counts only the drag along its diagonal and is clamped to
    `MinSelectionScale`..`MaxSelectionScale`. Every drag frame is computed
    from a `SelectionSnapshot` taken when the drag starts, so nothing
    drifts.
  - A move stops the group at the build area's edge; a turn or scale that
    would leave it is ignored. The frame and handles keep their screen size
    at any zoom, and each handle hits within `HandleHitRadius`.
- **Joint:** with nothing selected, a tap on empty canvas adds a joint;
  with a selection, that tap only clears it. Beams are never split (#746).
- **Links (#705, #913):** with nothing selected, the panel lists Beam,
  Piston, Spring and a locked Wing row (#130). The picked link's row shows
  what it does right under it (`PartInfo`), and one help line says "Drag
  from joint to joint to add the picked link." Beam is picked when Build
  opens; the pick then stays for the visit, across tool switches and
  selections (#874), and is not saved. Drag from an unselected joint to
  another joint to draw the picked link; a new link is not selected, and
  dropping away from a joint places nothing.
  - While dragging, the line shows whether it will attach (#920): dashed
    over no joint, solid once it will attach, and dashed danger with a
    crossed ring when that joint would refuse. Dropping there shows the
    reason at the joint (`BuildViewModel.CanConnectLink`): "A beam already
    joins these joints", "These joints already have a piston", "These
    joints already have a spring", or "A sensor sits on this beam".
  - A Piston or Spring dropped on a pair a beam joins replaces that beam
    (#849), unless a sensor sits on it or the creation is locked, which
    shows "A beam already joins these joints"; while dragging, the beam is
    outlined. A Servo that held the beam holds the new link in the same
    role, under a new id. One Undo brings the beam back. A Beam never
    replaces a link.
- **Parts:** with nothing selected, the panel shows the Parts tray; drag a
  part from it onto the creature (see Parts tray). Parts never adds a
  joint.
- **Select (#366, #704):** a drag on empty canvas, or from a beam, sensor
  or link, outside any group's frame draws a box that shows the parts it
  would catch as selected. It selects every part whose centre is in it (a
  joint's centre, a beam's or link's midpoint, a sensor's beam midpoint)
  and replaces the selection, so a box can catch only beams.
- **Sensors (#127, #575):** dragged from the tray onto a beam, one per
  beam (#376). The tray holds the Camera back as a locked row (#852): on
  the Flat map it only adds complexity, so it returns with terrain (#855).
  A saved creation that already has one keeps it working.
- **Servo (#452, #577):** dragged from Parts → Moving parts onto any joint.
  Dropping on a beam, sensor or link refuses with "Servos go on a joint"; a
  joint that already has one refuses with "One part per joint". A good drop
  takes the two lowest-id links as Fixed and Target, is one Undo step and
  selects the Servo. On a joint with fewer than two links the missing roles
  stay empty and the Servo shows "A Servo needs two links at its joint"
  until another link is drawn there and picked: drop first, finish later.
  Tapping that joint selects the Servo, but dragging still moves the joint.
- **Camera aim (#594, #622):** a Camera selected alone shows its rays and an
  Aim handle out along its centre ray, in any tool; the handle may cover a
  joint, which then cannot be tapped there (#639). Dragging the handle
  turns the camera to look at the finger (`CameraRays.AimAlong`); the aim
  is saved relative to the beam. The handle is hit before anything under
  it, a tap on it does nothing, and a second finger puts the aim back.
- **Deleting:** there is no Delete tool; the Part settings and selection
  panels delete the selection. Deleting a joint removes every link on it,
  its Servo and those beams' sensors (`CreatureBuilder.RemoveNode`), and
  deleting a beam removes its sensor. Deleting a link a Servo holds keeps
  the Servo with that role missing, which blocks training until a
  replacement is picked or the Servo is deleted.
- **Two fingers, any tool (#400):** pinch zooms about the point between the
  fingers and dragging both pans. The second finger cancels the first
  finger's gesture, putting back any joint it moved and any selection its
  press changed, and nothing edits until every finger lifts, so navigation
  never changes the creature.
- **Build area (#400):** joints live inside `BuildViewModel.BuildArea`
  (x −600..600, y −300..300 canvas units, 12 × 6 m), small enough that any
  creature fits the Training view without zooming out far (#884). Placing
  or moving a joint keeps its ring inside; a group move stops as a whole at
  the edge, and a Joint tap outside adds nothing. A grid of `BuildGridStep`
  cells (50 units, half a metre) covers exactly the area.
- **View:** `CanvasView` holds zoom and pan and maps view units to canvas
  units; it never touches saved positions and is not saved. Build opens
  with the creation centred and `FitMargin` (20%) of air on every side,
  zoomed out if needed but never magnified past true size; an empty
  creation opens at true size in the middle of the area. True size is 1× at
  a UI size root factor of 1; the zoom limits are divided by that factor
  (`CanvasView.UiScale`), so true size and the limits keep their size on
  screen and only how far Fit zooms out changes (#299). The view can show
  `BuildViewBounds`, the area plus one cell on every side: zooming out
  stops when all of it is in view (`MinZoom`), and panning stops at its
  edge or centres it where it fits. Distances are in view or canvas units
  (`docs/GLOSSARY.md` → Build canvas).
- Changing tool mid-gesture, or Android cancelling the touch, cancels the
  gesture the same way. A refused edit changes nothing; where the player
  needs a reason, a canvas note next to the part gives it.

## Parts tray

With nothing selected, the side panel shows the active tool's panel: Parts
shows the Parts tray (#374), Links the link list (#705), and Joint and
Select short help (#706). One selected part shows its Part settings and
several the selection panel instead.

- Three tabs, Moving parts, Sensors and Blocks, group parts by what they
  do, not where they go: Moving parts holds the motors, Brake and Wheel,
  and Sensors every sensor, on a beam or, like the Touch sensor (#665), on
  a joint. Each part decides its own placement. `PartTray` owns the groups,
  their order, the help lines and each row's state.
- Each visit opens on the first tab with an available part
  (`PartTray.OpeningGroup`), so a tab of padlocks never reads as every part
  being locked (#887). The open tab then stays for the visit (#874).
- Parts are unlimited (#525), so rows show no count. Every planned part has
  a locked row, so the tray shows what is coming: the Touch sensor (#665)
  and the Pulse (#527) are locked rows in Sensors, and the Camera is held
  back the same way (#852).
- An available row does nothing on tap; it is dragged out (#376). The drop
  lands on what the part is over (a joint's ring, a sensor picture's beam,
  a beam within reach, then a joint within reach), and
  `BuildViewModel.PlacePart` places it with a fresh id or refuses it with a
  canvas note at that part (`PlacementNote`) until the next touch or for
  3 s. A drop on empty canvas or back on the panel changes nothing.

## Part settings

One selected joint, beam, sensor or link shows its Part settings in the side
panel (#343). The title carries the part's glyph and kind ("Accelerometer",
"Joint"; `PartSettingsPresentation.Title`), so a renamed part still says
what it is; there is no close button, and tapping empty canvas deselects.
The rows are **Name**, then what the part is joined to (a beam's two
joints, a sensor's beam; joints and links list nothing, as the canvas shows
them, #913), then its note, and last a full-width **Delete**, Unavailable on
a locked creation when deleting would change the model (see Locked Build).
Delete acts on a tap, with no dialog; Undo brings the part
back, selected (#866, #878). Structure is read-only here: a beam's length
is drawn, not a number. A part's note is its `PartInfo` line, the same as
its row in the Links list; `BuildPresentationViewModel.SinglePart` owns the
rows and copy. The panel scrolls back to the top when it shows another
part, tool or selection count; a Servo whose links a picker, Undo or Redo
changed is still the same part, so the panel keeps its place
(`PartSettingsPresentation.PanelId`, #910).

### Parameters (#704)

A part's settings are parameters (`PartParameters`): an id, whether several
selected parts can share one value (`MultiEditable`), and a slider when the
panel shows it (`InPanel`). Each kind lists its own
(`CreatureBuilder.ParametersOf`); what each means is in
`docs/CREATURE_MODEL.md`.

Each parameter is basic or `Advanced` for every part that has it (#903):
Start position, Max speed, Rise time, Damping and Coil length (#989) are
advanced. The panel shows Name, the basic sliders and any other controls,
like a Servo's link pickers, then a closed Advanced section with the rest, left out when there
is none, then the part's note. The section stays open or closed across selections
until the next Build visit (`BuildViewModel.AdvancedSettingsOpen`).

- **Piston:** Max strength (20–400 N, step 10), Stroke (10–100%, step 5),
  Start position (0–100%, step 5, #870), Max speed (0.5–4.0 m/s, step 0.1)
  and Rise time (0.1, 0.2, 0.5 or 1 s, evenly spaced along the slider so
  the short ones are as easy to pick, #801).
- **Spring:** Stiffness (50–2000 N/m, step 50), Damping (0–100 N·s/m,
  step 1), Stroke (as the Piston's) and Coil length (0–100%, step 1, #835;
  a new Stroke keeps it).
- **Servo:** Max strength (5–200 N·m), Range (20°–360°), Start position
  (0–100%), Max speed (30°/s–720°/s) and Rise time, plus "Fixed link" and
  "Target link" pickers. A missing role's picker reads "Pick a
  Fixed link" or "Pick a Target link" in danger colour and lists only the
  real links. If the joint has fewer than two links, its note and canvas
  callout say "A Servo needs two links at its joint" instead, and the
  play-blocked reason asks to connect another link there, because no pick
  could fix it. Changing a picker follows `docs/CREATURE_MODEL.md` →
  "Editing identity rules".
- **Camera:** Aim, set on the canvas, one Camera at a time.

A finger on a slider shows what that setting does in a box at the canvas's
top right (`ParameterScale.Help`, one short line each, #867). It stays while
the finger is down, scrolling included, and 2 s after it lifts; touching
another slider swaps it, and it hides at once when the panel shows something
else or collapses. Turning it off belongs to Settings (#381).

The selection can change one part's own parameters, or those every selected
part has and can share (`BuildViewModel.EditableParameters`); a Piston and
a Spring selected together share Stroke, but a Piston and a Servo share no
Start position: one is along a stroke, the other a rotation (#867). A slider sets its value on every
selected part (`SetParameter`). The canvas shows what a parameter changes
only while it can be changed: a Piston's stroke ticks while Stroke or Start
position can, a Spring's ticks and rest-length ring while Stroke or Coil
length can, a Camera's rays and Aim handle while Aim can. Parameters change
no brain port, so a locked creation keeps them.

## Names

- A part with no name of its own shows a default that follows its place in
  the lists (`BuildViewModel.DefaultPartName`: "Joint 2", "Beam 1", "Accel
  1", "Camera 1", a sensor numbered among its kind). Renaming (`RenamePart`)
  goes by id, so an edit lands on the part it started on even if the
  selection moves. It trims the text, and a blank name, or the default left
  unchanged, clears the part's own name.
- A name field stops at `NameLimits`: 40 characters for a creation and 10
  for a part, and a copy's "Copy of …" name is cut to fit (#868). Every
  default part name fits, which is why an Accelerometer is named "Accel 1"
  while the tray and the Part settings title say "Accelerometer". A longer
  saved name is never cut by the field: it shows whole, cannot grow, and
  only gets shorter as the player deletes.
- Names are labels only (#220), so a locked creation can be renamed too; a
  rename autosaves like any edit.

## Selection panel

Several selected parts show the selection panel instead (#558, #704), titled
"N selected"; there is no close button.
- First a slider for each parameter they share, split into basic and
  Advanced like a part's, then the note "A slider sets one value for all of
  them."
- With a frame, three rows explain its handles (Move, Rotate, Scale). With
  neither settings nor a frame: "These parts share no settings."
- Last full-width **Copy N**, hidden when locked, and **Delete N**, which a
  locked creation makes Unavailable like a part's Delete. Delete acts on a
  tap (Undo restores it), and its note says what else a delete removes.
- Copy (#937) duplicates the joints, beams and Springs one grid step aside,
  with their settings but not their names, and selects the copy; Undo
  removes it. A selected joint's Servo is not copied with it. Copy is
  dimmed while the selection holds a part with brain ports, since a copy
  would change the network, or a link without both its joints. A tap then
  puts a danger note on each such part until the selection changes.
  `BuildViewModel.CopySelectedParts` and `CopyBlockers` own these rules.

`BuildPresentationViewModel.Selection` owns the panel's wording.

## Validation

A saved Creation stores any drawing: `CreatureDef` checks only that part ids
are unique and below `NextPartId` and that references point at existing
parts (one sensor per beam), so an empty or unfinished creature is still a
Creation (#515). Only training needs a finished creature, and
`CreatureReadiness` is the single source of truth for that: `Problems` lists
why the creature cannot be simulated yet (the readiness rules are in
`docs/CREATURE_MODEL.md`), and `CanTrain` is true when there are none. It
needs no powered part (#845, `docs/TRAINING_LOOP.md` step 2).
`CreatureBuilder.TryBuild` applies `Problems` to the creature in progress.
The UI shows those messages and does not duplicate the rules; Build's
readiness line only shortens them for the narrow side panel ("1 joint not
connected").

The play button is dimmed until the creature can train, and the panel's
last line says why; `BuildViewModel.TryLeave` and `CanTrain` gate it. The
dimmed button can still be tapped
(#844): the tap starts nothing, and `BuildViewModel.ShowTrainingBlockers`
gives each joint loose at that moment a "Not connected" canvas note until
it is joined or removed. A creation in separate pieces gets the same note
on every piece's joint nearest another piece, none being the main one, until
it is one piece (#930); a piece split off later waits for the next tap. Too-short parts always have their "Too short"
note. The edits are saved first either way. Back never validates: it saves
the drawing as it stands (#474). Training refuses a saved creature that
cannot train and returns to Creations.
