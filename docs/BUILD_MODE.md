# Build mode

How Build behaves: its tools, gestures, selection, panels and validation.
How it looks is in `docs/UI_DIRECTION.md` and `docs/WORLD_VISUALS.md`; when
a Creation is locked is in `docs/TRAINING_LOOP.md` → Product lifecycle
boundary. What each part is, and which have network ports, is in
`docs/CREATURE_MODEL.md`. `reference design/components/Build/`,
`BuildLocked/` and `Navigation/` are its guide.

Wherever a rule here says *joint motor* or *hinge part*
(`docs/GLOSSARY.md`), Build treats every kind alike.

## Mode

- Build always edits a saved creation: + New saves an empty "Untitled
  Creation" in the player's language before Build opens. Build and
  Training share only the save; the play button saves and opens
  Train setup (`docs/TRAINING_LOOP.md` step 1).
- **Autosave.** There is no Save button. Edits are saved once they
  have settled for 0.5 s, and when Build is left or the app pauses or
  closes. A failed save keeps the edits and retries on the next save; Back,
  Start training and Reset training report every failure, background saves
  only the first in a row. Back always leaves, so a failing disk never
  traps the player in Build. A creation opened by + New that has no joints
  when Build is left is removed again.
- **Locked Build.** A locked creation (`CreationLock`) opens like any
  other, and `BuildViewModel` refuses only what would change the
  network's ports. So a part with ports (a sensor, Memory cell, Touch
  sensor, Angle sensor, Piston or joint motor) can be neither added nor
  deleted, and a Camera's Rays cannot change. Everything else works:
  joints, beams, Springs, Wheels, Rubbers and Brakes are added, copied and
  deleted; parts move, rename and change parameters; hinge parts change
  sides through their pickers, since a part keeps its id and ports through
  them; a Beam and a Spring may replace each other.
  - Refused tray rows and sliders are locked, and a tap on one shows
    `BuildViewModel.LockedReason` in a notification.
  - Delete and Copy stay, Unavailable, when they would take a part with
    ports along, also by cascade, or clear a kept hinge part's side
    (`DeleteLockedReason`, `CopyBlockers`). A tap puts "Locked: would
    change the network" on each part that blocks it; Delete also shows
    the notification.
  - The padlock unlocks it for this visit and keeps the training
    (`docs/TRAINING_LOOP.md` step 6).
- **Overflow menu.** It follows training, not the lock. Stats and Reset
  training show only on a trained creation, Copy creation and Share build
  only when something is drawn, and Power budget and Delete
  creation always; Stats (#198) and Power budget (#460) say "Coming
  soon". Share build
  copies the build, not its training, as a code that Import creation adds
  back (`docs/SAVE_FORMAT.md` → "Share code"). Copy creation saves
  first, keeps any trained network, names the copy "Copy of …" and opens
  it in place of the original. Reset training and Delete creation each ask
  first in a dialog.
- **Undo and Redo (`BuildHistory`)** work on a locked creation too.
  One finished action that changes the body is one step: a placed part, a
  link, a delete, a part rename, or a whole drag or slider move.
  Selection, tool, the grid, zoom and pan never count, and a cancelled
  gesture adds nothing and keeps Redo. A visit holds 100 steps and starts
  empty. Renaming the creation, Unlock, Reset training, Copy creation and
  Delete creation cannot be undone.
  - Undo keeps the selected parts that still exist; undoing a delete
    selects the deleted parts again and gets their trained weights back
    (`docs/CREATURE_MODEL.md` → "Build refits the network it opened
    with").
  - Each edit has an owner, the canvas or a slider, so one never closes
    the other's. Undo and Redo do nothing while an edit is open, so a
    second finger cannot undo under a drag.

## Interactions

The rail holds the tools Joint, Links, Parts and Select, and the play
button at its bottom in every state. "Joint" is the player-facing
name for a node. Build opens in Joint, locked or not.
`BuildGestures` turns pointer input into edits, zoom and pan;
`BuildCanvas` only forwards input and draws.

- **What a touch hits.** A pointer that travels at most `TapSlop`
  view units is a tap. A tap or drop hits the part drawn on top
  (`docs/WORLD_VISUALS.md` → Draw layers), in Build and Training alike:
  a joint's ring, hinge part or Wheel hub, each with the selection gap; a
  sensor's or Memory cell's picture; or a link near its line, outside its ends' hubs. Off
  every part, Build falls back to finger-sized reach: a link, then a beam.
  A Wheel's face and tyre never take a press or drop, so links and
  joints inside it stay reachable; only a released tap that reaches
  nothing else, not even a handle, falls back to a Wheel's disc and
  selects that Wheel (`DrawGroups.WheelDiscAt`). Joints, sensors and
  Memory cells hit in canvas units, so they grow with zoom; beams, links and handles
  keep finger-sized areas at any zoom.
- **Every tool shares one selection model;** the tools differ
  only in their main action, below.
  - A tap on a part adds it to the selection or removes it, even under a
    handle. A tap on empty canvas clears the selection.
  - **Parts stand in for their joint**: a joint with parts
    on it is never selected itself; only a bare joint is. A tap on it
    steps down its stack (the `JointSlot` order: hinge part, Wheel,
    Rubber, Touch sensor): the first tap adds the top part, each next tap
    swaps it for the one below, and a tap on the last, or with more than
    one of them selected, removes them (`ToggleJointSelected`). A selected
    part moves its joint and counts as a joint for the group.
  - Two or more selected joints are a *group*, with a frame and Move,
    Rotate and Scale handles. Rotate and Scale turn about the frame's
    centre, and every drag is computed from a `SelectionSnapshot` taken
    when it starts, so nothing drifts.
  - A drag is settled by where it starts, first match wins: a handle; in
    Links with a link picked, a joint that can start the link; a selected
    joint or a joint with a selected part, which moves the selection;
    inside a group's frame, which moves the group; an unselected joint,
    which is selected alone (or its top part is) and moved; a sensor,
    which moves to another beam. Any other drag draws a box in Select and
    pans in the other tools.
  - A group move stops at the build area's edge; a turn or scale that
    would leave it is ignored.
- **Joint:** with nothing selected, a tap on empty canvas adds a joint.
  Beams are never split.
- **Links:** the panel lists Beam, Piston, Spring and a
  locked Wing. Beam is picked when Build opens; the pick, or none, then
  stays for the visit and is not saved. Tapping the picked row or
  empty canvas clears it. Drag from an unselected joint to another
  to draw the picked link; the new link is not selected. With none picked,
  Links works like the other tools.
  - The drag line shows whether the drop will attach, before a finger
    covers the target; a refused drop shows the reason at the joint
    (`BuildViewModel.CanConnectLink`).
  - A link dropped on a pair another kind joins replaces it, with its own
    default settings. A beam with a sensor or Memory cell is never
    replaced, and a hinge part keeps holding the new link in the same role
    (`docs/CREATURE_MODEL.md` → "Editing identity rules"). One Undo brings
    the old link back.
- **Parts:** drag a part from the tray onto the creature, or pick it and
  tap where it goes (Parts tray). Parts never adds a joint.
- **Select:** a drag that would pan in the other tools draws
  a box. It selects every part whose centre is in it and replaces the
  selection, so a box can catch only beams.
- **Placing a part.** Each part knows where it goes and refuses anywhere
  else with its own canvas note (`CreatureBuilder.*Reason`). A beam sensor
  or Memory cell goes on a beam, one part per beam. A joint part goes on a
  joint, one per slot (`docs/CREATURE_MODEL.md` → Node → One part per
  slot): the hinge part slot (joint motor, Brake or Angle sensor), Wheel,
  Rubber and Touch sensor. A hinge part picks its sides as
  `docs/CREATURE_MODEL.md` → "Servo" says; with fewer than two sides it is
  placed anyway and its note asks for another link or a Wheel: drop
  first, finish later. A placement is one Undo step, and a drop selects
  the part.
  - **Rubber and Touch sensor** add no tap area (`docs/CREATURE_MODEL.md`
    → Rubber, Touch sensor), so their joint selects them by the stack
    rule. Their panel shows only Name and the note, and they cannot be
    dragged to another joint.
- **Moving a sensor:** drag its picture to another beam, in any
  tool, locked or not. It keeps its id, name and settings, so its ports
  and trained weights stay (`CreatureBuilder.MoveSensor`); a Camera keeps
  the direction it looks in the world. A refused drop leaves it with the
  reason as a note; a drop on empty canvas leaves it silently.
- **Camera aim:** a Camera selected alone shows an Aim handle
  along its centre ray, in any tool. Dragging it turns the camera toward
  the finger; the aim is saved relative to the beam. The handle is hit
  before anything under it.
- **Deleting:** the Part settings and selection panels delete the
  selection. Deleting a joint removes its links, its parts and the
  sensors and Memory cells on those beams (`CreatureBuilder.RemoveNode`);
  deleting a beam removes its sensor or Memory cell. Deleting a joint part leaves its joint, unless the same
  delete takes every part and link on a joint that had a link: then the
  joint goes too, so a box that clears an area leaves no bare joint.
  Deleting a side a hinge part holds leaves that role missing.
- **Two fingers, any tool:** pinch zooms and dragging both pans.
  The second finger cancels the first finger's gesture, putting back
  anything it moved or selected, so navigation never edits the creature.
  Changing tool mid-gesture or Android cancelling the touch cancels the
  same way.
- **Build area:** joints live inside `BuildViewModel.BuildArea`
  (12 × 6 m), small enough that any creature fits the Training view
  without zooming far out. Every joint Build places or moves lands on
  whole units, 1 cm (`CreatureBuilder.OnWholeUnits`), so saves and share
  codes hold short numbers.
- **Grid:** square cells of the Creation's grid size, 0.25, 0.5 or 1 m
  (`docs/SAVE_FORMAT.md` → "`creation.json`"), each of which fills the
  area exactly. The area, its margin and its corner marks stay measured
  in half metres (`BuildAreaCell`). With Snap to grid on, a placed joint
  goes to the nearest grid point that keeps its ring inside the area. A
  moved selection, one joint or a group, moves so that the joint nearest a
  grid point lands on it, the lowest id on a tie, picked again each drag
  frame; the group keeps its shape, and keeping it inside the area wins,
  so at an edge it may sit off the grid. Rotate and Scale do not snap,
  and a joint already placed stays where it is when the grid changes.
  Build's grid menu (`docs/UI_DIRECTION.md` → "Build") changes the size
  and snap at once, and they save with Build's other edits, also on a
  locked Creation, since the grid is not part of the creature.
- **View:** `CanvasView` holds zoom and pan, which are never saved. Build
  opens with the creation fitted with `FitMargin` of air, never magnified
  past true size. The zoom limits are divided by the UI size, so
  true size and the limits keep their size on screen. The view can show
  `BuildViewBounds`, stretched while a selected Camera's rays reach past
  it (`CanvasView.Reach`), and eases back once they stop showing
  and no finger is down.
- A refused edit changes nothing; where the player needs a reason, a
  canvas note at the part gives it until the next touch or for 3 s.

## Parts tray

With nothing selected, the side panel shows the active tool's panel:
Parts the Parts tray, Links the link list, and Joint and
Select short help. One selected part shows its Part settings, and
several the selection panel. Picking a tool on the rail, even the active
one, clears the selection so its panel shows.

- Three tabs, Moving parts, Sensors and Blocks, group parts by what they
  do, not where they go, so a tab may mix beam and joint parts.
  `PartTray` owns the groups, their order, the help line and each row's
  state. Each visit opens on the first tab with an available part, so a
  tab of padlocks never reads as every part being locked; the tab
  then stays for the visit.
- Parts are unlimited, so rows show no count. Every planned part
  has a locked row, so the tray shows what is coming; a tap on it names
  the version that brings it (`PartTray.ComingLaterReason`).
- A drop lands where a tap would hit, except that a joint part lands
  first on the joint whose placing ring it is inside, ahead of the beams
  the ring crosses (`PlacingTargets.JointRingOf`). A drop on empty
  canvas or back on the panel changes nothing.
- A tap on a row picks it (`BuildViewModel.PickedPart`): its
  `PartInfo` and where it goes show under it, and the canvas marks where
  it can go. A canvas tap then places it as a drop there would.
  The placed part is not selected, so the part stays picked for the next
  one. Tapping the row again or empty canvas, another tab or tool, a
  selection, collapsing the panel, or Back clears the pick; Back leaves
  Build only once nothing is picked.

## Part settings

One selected part shows its Part settings: a title with its glyph
and kind, so a renamed part still says what it is; **Name**; what it is
joined to (a beam's joints, a sensor's or Memory cell's beam); its parameters; its note
(`PartInfo`); and a full-width **Delete**. Delete acts at once, with no
dialog, since Undo brings the part back. Structure is read-only: a
beam's length is drawn, not a number. `BuildPresentationViewModel.SinglePart`
owns the rows and copy. The panel scrolls to the top when it shows
another part, but keeps its place when a picker, Undo or Redo changes the
part it shows.

**On this joint.** When the selected part's joint holds two or
more parts, a strip of glyph tabs under the title, in stack order, lets
the player select each of them alone.

### Parameters

A part's settings are `PartParameters`, listed per kind by
`CreatureBuilder.ParametersOf`; what each means is in
`docs/CREATURE_MODEL.md`. `PartParameters` owns each slider's range,
steps and units, and whether it is `Advanced`. A Motor is all about
speed, so its Max speed is basic. The panel shows Name, the basic
sliders and controls such as a hinge part's side pickers, then a closed
Advanced section with the rest; whether it is open lasts the visit.

- **Side pickers.** A hinge part's "Fixed part" and "Target part" list
  its joint's links and Wheel ("part", since a side can be a Wheel). An
  empty role reads "Pick a Fixed part" in danger. With both
  roles set, each list marks the other role's part "swaps", since picking
  it swaps them. A joint with fewer than two sides gets a note
  that no pick could fix it. Changes follow `docs/CREATURE_MODEL.md` →
  "Editing identity rules".
- **Uneven stops** (Rise time, Rays) sit evenly along the slider, so the
  short ones are as easy to pick.
- **No effect.** Spread is disabled while every selected Camera has one
  ray, and a tap says why (`PartParameter.NoEffectReason`).

A finger on a slider shows what that setting does in a hint card at the
canvas's top right (`ParameterScale.Help`), until 2 s after it
lifts.

Several selected parts show the parameters they all have
(`ShownParameters`); a slider sets its value on every one. A Piston and a
Spring share Stroke, but a Piston and a Servo share no Start position:
one is along a stroke, the other a rotation. The canvas shows what
a parameter changes only while it can be changed: link travel ticks, a
Spring's rest length, a Camera's rays and Aim handle. When a Camera
slider makes its rays outgrow the view, the view eases out toward them,
never in.

## Names

- A part with no name of its own shows a default that follows its place
  in the lists (`BuildViewModel.DefaultPartName`). Renaming goes by id,
  so it lands on the part it started on even if the selection moves. A
  blank name, or the default left unchanged, clears the part's own name.
- A name stops at `NameLimits`, and a copy's "Copy of …" is cut to
  fit. Every default part name fits, which is why an Accelerometer is
  named "Accel 1". A longer saved name is never cut by the field.
- Names are labels only, so a locked creation can be renamed, and
  two creations may share a name.

## Selection panel

Several selected parts show the selection panel, titled "N selected"
(wording: `BuildPresentationViewModel.Selection`): the
parameters they share, the group frame's handles explained, and
full-width **Copy N** and **Delete N**.

- Delete acts at once; its note says what else it removes.
- Copy duplicates the selection aside by 0.5 m, or 1 m on the 1 m grid
  (`CopyStep`), so it lands on the grid and never covers the original, with
  settings but not names, and selects the copy. A part copies only with
  what it sits on: a link with both joints, a sensor or Memory cell with
  its beam, and a
  joint part with its joint. A joint's other parts come only when
  selected. A copied hinge part uses the copies of its sides and leaves a
  side empty whose part was not copied. Copy is Unavailable while the
  selection holds a part it cannot take; a tap marks each such part.
  `BuildViewModel.CopySelectedParts` and `CopyBlockers` own the
  rules.

## Validation

A saved Creation stores any drawing: `CreatureDef` checks only ids and
references, so an empty or unfinished creature is still a Creation.
Only training needs a finished creature, and `CreatureReadiness`
is the single source of truth for that (rules: `docs/CREATURE_MODEL.md`);
the UI shows its messages and never repeats its rules. Build's readiness
chip shortens them to one line and treats an empty canvas as a start, not
a blocker (`docs/UI_DIRECTION.md` → "Build").

The play button is dimmed until the creature can train, but still answers
a tap: it starts nothing and puts a note on each blocker at that
moment (a loose joint, a piece cut off from the rest, or a hinge part
missing a side) until it is fixed or the next canvas touch. "Too short"
notes always show. Back never validates: it saves the
drawing as it stands. Training refuses a saved creature that
cannot train and returns to Creations.
