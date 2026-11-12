# Build mode

The rules every Build edit follows, and the choices only one tool, panel
or part makes. Values are in the code each rule names, and Build's tools,
tabs and rows in its scenes and `PartTray`. How Build looks is in
`docs/UI_DIRECTION.md` and `docs/WORLD_VISUALS.md`; when a Creation is
locked is in `docs/TRAINING_LOOP.md` → "Product lifecycle boundary"; what
each part is, and which have network ports, is in
`docs/CREATURE_MODEL.md`. `reference design/components/Build/`,
`BuildLocked/` and `Navigation/` are its guide.

Wherever a rule here says *joint motor* or *hinge part*
(`docs/GLOSSARY.md`), Build treats every kind alike.

## Rules

1. **Build always edits a saved creation.** + New saves an empty
   creation before Build opens, and one left with no joints is removed
   again. There is no Save button: edits save once they settle and
   whenever Build is left or the app pauses or closes (`BuildHost`). A
   failed save keeps the edits and retries on the next; Back, Start
   training and Reset training report every failure, background saves only
   the first in a row. Back always leaves, so a failing disk never traps
   the player in Build; edits its save could not write are lost, and the
   failure notice says so.
2. **A refused edit changes nothing.** Where the player needs a reason, a
   canvas note at the part gives it until the next touch or a timeout. A
   control that would be refused stays visible, and a tap says why
   (`docs/UI_DIRECTION.md` → "Components", States).
3. **The lock refuses only what would change the network's ports**
   (`CreationLock`, `BuildViewModel.LockedReason`). A locked creation
   opens like any other. A part with ports can be neither added nor
   deleted, also by cascade, a Camera's Rays cannot change, and no delete
   may clear a kept hinge part's side (`DeleteLockedReason`,
   `CopyBlockers`). Everything else works, Undo included: parts without
   ports are added, copied and deleted, parts move, rename and change
   settings, a hinge part changes sides, and a Beam and a Spring replace
   each other. The padlock unlocks it for the visit and keeps the
   training.
   - Refused tray rows and sliders are locked, and a tap shows
     `LockedReason` in a notification. Delete and Copy stay, Unavailable,
     and a tap puts "Locked: would change the network" on each part that
     blocks them; Delete also shows the notification.
4. **Ids survive what they can,** so ports and trained weights stay
   (`docs/CREATURE_MODEL.md` → "Editing identity rules"): a moved sensor
   or Memory cell keeps its id, name and settings
   (`CreatureBuilder.MoveBeamPart`); a hinge part keeps its id through any
   change of its sides; Undo of a delete brings back the deleted parts and
   their trained weights (`docs/CREATURE_MODEL.md` → "Sensor–network
   contract", Build refits the network it opened with).
5. **One finished change to the body is one Undo step** (`BuildHistory`):
   a placed part, a link, a delete, a part rename, or a whole drag or
   slider move. Selection, tool, the grid, zoom and pan never count; a
   cancelled gesture adds nothing and keeps Redo. A visit's history starts
   empty. Renaming the creation, Unlock, Reset training, Copy creation and
   Delete creation cannot be undone; Reset training and Delete creation ask
   first in a dialog.
   - Undo keeps the selected parts that still exist, and undoing a delete
     selects the deleted parts again.
   - Each edit has one owner, the canvas or a slider, so one never closes
     the other's. Undo and Redo do nothing while an edit is open, so a
     second finger cannot undo under a drag.
6. **Delete acts at once, with no dialog,** since Undo brings it back.
7. **A touch hits the part drawn on top** (`docs/WORLD_VISUALS.md` →
   "Draw layers"), in Build and Training alike: a joint's ring, hinge part
   or Wheel hub, a sensor's or Memory cell's picture, or a link near its
   line outside its ends' hubs. Off every part, finger-sized reach finds a
   link, then a beam. A Wheel's face and tyre never take a press or drop,
   so what is inside them stays reachable; only a tap that reaches nothing
   else selects the Wheel (`DrawGroups.WheelDiscAt`). Joints, sensors and
   Memory cells hit in canvas units, so they grow with zoom; beams, links
   and handles stay finger-sized at any zoom (`BuildGestures`).
8. **Every tool shares one selection model;** the tools differ only in
   their main action.
   - A tap on a part adds it to the selection or removes it, even under a
     handle; a tap on empty canvas clears the selection.
   - Parts stand in for their joint: a joint with parts on it is never
     selected itself. A tap on it steps down its stack in `JointSlot`
     order, and a tap on the last, or with more than one selected, removes
     them (`ToggleJointSelected`). A selected part moves its joint and
     counts as a joint for the group.
   - Two or more selected joints are a group, with a frame and Move,
     Rotate and Scale handles. Every drag is computed from the
     `SelectionSnapshot` taken when it starts, so nothing drifts.
   - A drag is settled by where it starts, first match wins; the order is
     in `BuildGestures`. A drag on nothing draws a box in Select and pans
     in the other tools.
9. **Navigation never edits.** Two fingers pinch to zoom and drag to pan
   in any tool. The second finger, a tool change or Android cancelling
   the touch cancels the gesture and puts back what it moved or selected.
10. **Each part knows where it goes** and refuses anywhere else with its
    own note (`CreatureBuilder.*Reason`): a beam sensor or Memory cell on
    a beam, one per beam; a joint part on a joint, one per slot
    (`docs/CREATURE_MODEL.md` → "Parts", One part per slot). Drop first,
    finish later: a hinge part with fewer than two sides is placed anyway,
    and its note asks for another link or a Wheel. A placement is one Undo
    step, and a drop selects the part.
11. **Joints stay in the build area and land on whole units**
    (`BuildViewModel.BuildArea`, `CreatureBuilder.OnWholeUnits`), so any
    creature fits the Training view and saves and share codes hold short
    numbers. A group move stops at the edge, and a turn or scale that would
    leave it is ignored.
12. **The grid** (`BuildGridDef`) fills the area exactly, and the area
    stays measured in its own cells (`BuildAreaCell`). With Snap on, a
    placed joint goes to the nearest grid point that keeps it inside the
    area. A moved selection moves so
    that the joint nearest a grid point lands on it, the lowest id on a
    tie; the group keeps its shape, and staying inside the area wins, so
    at an edge it may sit off the grid. Rotate and Scale do not snap, and
    a joint stays where it is when the grid changes.
13. **The canvas shows what a setting changes only while it can be
    changed:** link travel, a Spring's rest length, a Camera's rays and
    Aim handle. When that outgrows the view, the view eases out to show it,
    never in (`CanvasView.Reach`), and eases back once it stops showing
    and no finger is down.
14. **Readiness has one owner.** A saved creation stores any drawing:
    `CreatureDef` checks only ids and references. Only training needs a
    finished creature, and `CreatureReadiness` decides it (rules:
    `docs/CREATURE_MODEL.md`); the UI shows its messages and never repeats
    its rules. Back never validates. The play button is dimmed until the
    creature can train, but a tap on it starts nothing and puts a note on
    each blocker until it is fixed or the next canvas touch. Training
    refuses a saved creature that cannot train and returns to Creations.
15. **Names are labels only,** so a locked creation can be renamed and
    two creations may share a name. A part with no name shows a default
    from its place in the lists (`BuildViewModel.DefaultPartName`).
    Renaming goes by id, so it lands on the part it started on. A blank
    name, or the default left unchanged, clears the part's own name. A
    name stops at `NameLimits`, every default fits it, a copy's "Copy of …"
    is cut to fit, and a longer saved name is never cut by the field.

## Decisions

The choices one tool, panel or part made, with why.

- **Joint:** with nothing selected, a tap on empty canvas adds a joint.
  Beams are never split.
- **Links:** the pick, or none, lasts the visit and is not saved; tapping
  the picked row or empty canvas clears it. A drag from an unselected
  joint to another draws the picked link, which is not selected. The drag
  line shows whether the drop will attach before a finger covers the
  target, and a refused drop shows the reason at the joint
  (`BuildViewModel.CanConnectLink`). A link dropped on a pair another kind
  joins replaces it with its own default settings, except a beam with a
  sensor or Memory cell, which is never deleted silently. A hinge part
  keeps holding the new link in the same role
  (`docs/CREATURE_MODEL.md` → "Editing identity rules"), and one Undo
  brings the old link back.
- **Parts tray:** tabs group parts by what they do, not where they go.
  Each visit opens on the first tab with an available part, so a tab of
  padlocks never reads as every part locked; the tab then stays for the
  visit. Parts are unlimited, so rows show no count. Every planned part
  or link has a locked row in the tray or the link list, so Build shows
  what is coming, and a tap names the version that brings it
  (`PartTray.ComingLaterReason`, `BuildLinkList.ComingLaterReason`).
- **Placing from the tray:** a drop lands where a tap would hit, except
  that a joint part lands first on the joint whose placing ring it is
  inside (`PlacingTargets.JointRingOf`); a drop on empty canvas or on the
  panel changes nothing. A tap on a row picks it: its `PartInfo` shows
  under it and the canvas marks where it can go, and a canvas tap places
  it. The placed part is not selected, so the pick stays for the next one.
  A tap on the row, empty canvas, another tab or tool, a selection,
  collapsing the panel or Back clears the pick; Back leaves Build only
  once nothing is picked. Parts never adds a joint.
- **Select:** a box selects every part whose centre is in it and replaces
  the selection, so a box can catch only beams.
- **Rubber and Touch sensor** add no tap area, so their joint selects them
  by the stack. Their panel shows only Name and the note, and they cannot
  be dragged to another joint.
- **Moving a sensor or Memory cell** works in any tool, locked or not. A
  Camera keeps the direction it looks in the world. A refused drop leaves
  it with the reason as a note; a drop on empty canvas leaves it silently.
- **Camera aim:** a Camera selected alone shows an Aim handle along its
  centre ray in any tool, hit before anything under it. The aim is saved
  relative to the beam.
- **Deleting** a joint removes its links, its parts and the sensors and
  Memory cells on those beams (`CreatureBuilder.RemoveNode`); deleting a
  beam removes its sensor or Memory cell. Deleting a joint part leaves its
  joint, unless the same delete takes every part and link on a joint that
  had a link, so a box that clears an area leaves no bare joint. Deleting
  a side a hinge part holds leaves that role missing.
- **Side panel:** with nothing selected it shows the active tool's panel;
  one part shows its Part settings, several the selection panel. Picking a
  tool, even the active one, clears the selection so its panel shows.
- **Part settings** (`BuildPresentationViewModel.SinglePart`): the title
  shows the part's glyph and kind, so a renamed part still says what it
  is. Structure is read-only: a beam's length is drawn, not a number. The
  panel scrolls to the top for another part, but keeps its place when a
  picker, Undo or Redo changes the part it shows. When the part's joint
  holds two or more parts, glyph tabs in stack order select each alone.
- **Parameters:** `PartParameters` owns each setting's range, steps, units
  and whether it is `Advanced`; `CreatureBuilder.ParametersOf` lists them
  per kind. The basic ones and controls such as side pickers show first,
  then a closed Advanced section. A Motor is all about speed, so its Max
  speed is basic. Uneven stops sit evenly along the slider, so the short
  ones are as easy to pick. A setting with no effect is disabled and a
  tap says why (`PartParameter.NoEffectReason`).
- **Several parts** show the settings they all have (`ShownParameters`),
  and a slider sets every one. A Piston and a Spring share Stroke, but a
  Piston and a Servo share no Start position: one is along a stroke, the
  other a rotation.
- **Side pickers:** "Fixed part" and "Target part" say "part", since a
  side can be a Wheel. An empty role asks for a pick in danger.
  With both roles set, each list marks the other role's part "swaps",
  since picking it swaps them. A joint with fewer than two sides gets a
  note that no pick could fix it.
- **Copy** (`BuildViewModel.CopySelectedParts`, `CopyStep`) lands aside by
  a grid-aligned step, so it never covers the original, copies settings
  but not names, and selects the copy. A part copies only with what it
  sits on: a link with both joints, a sensor or Memory cell with its beam,
  a joint part with its joint; a joint's other parts come only when
  selected. A copied hinge part uses the copies of its sides and leaves a
  side empty whose part was not copied. Copy is Unavailable while the
  selection holds a part it cannot take, and a tap marks each one.
- **Overflow menu:** follows training, not the lock. Share build copies
  the build, not its training (`docs/SAVE_FORMAT.md` → "Share code"). Copy
  creation saves first, keeps any trained network, so the player can
  change one and keep the other, names it "Copy of …" (cut to fit, rule
  15) and opens it in place of the original. Creations' copy does the
  same (`Saves.Duplicate`).
- **Grid menu:** changes size and snap at once, and they save with
  Build's other edits, also on a locked Creation, since the grid is not
  part of the creature.
- **View** (`CanvasView`): zoom and pan are never saved. Build opens with
  the creation fitted, never magnified past true size. The zoom limits are
  divided by the UI size, so true size and the limits keep their size on
  screen.
- **"Too short" notes** always show, not only after a tap on the play
  button.
