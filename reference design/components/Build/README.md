Build and BuildLocked are **one scene in two states**. In Godot there is a single `Build` scene with a `locked: bool`; there is no second scene. BuildLocked is only the name of the page here that shows the locked state. The flag changes: the rail (Beam and Joint are dashed and locked, and a large play button appears at the bottom), the top bar (Start training becomes a padlock and the overflow menu), the right panel (the Parts tray becomes how trained the creation is and its actions), and the settings (structure rows become read-only). The canvas, the rail, the brain widget and the part settings are the same nodes. Build is the **unlocked** state: parts can be added and removed and beams resized. Everything **autosaves**; there is no Save button. The name at the top is editable, with a small "Saved" cue.

**Top bar.** Back, the name, **Start training** (the one primary; dimmed with the reason in the tray if a piece is not connected) and the overflow menu: **Brain setup**, **Power budget** and **Delete creation**. Start training goes to Train setup. The creation **locks itself once a training session has finished**; from then on it opens in its locked state (see BuildLocked) and can be unlocked again at the cost of its training.

**Four tools on the 56px rail.**

- **Move** is the default. Drag a node or a block to move it; **tap one part or beam to select it** (its settings open on the right), tap empty canvas to deselect. Move never adds a joint.
- **Beam** joins two existing joints with a beam, unlimited: drag from one joint to another. It never makes a joint.
- **Joint** adds a joint: tap empty canvas, or tap a beam to split it there. A joint has angle limits and holds one part.
- **Select** picks several parts by box or by tapping. A selection rings the chosen joints in `halo` and shows a dashed box with three handles like a photo editor: **move** (in the middle, or drag inside), **rotate** (on a stem above) and **scale** (at the bottom right corner, its arrows along the diagonal that points out of the box: drag closer or further to move the group together or apart). Beams stretch as nodes move. The panel says what the handles do and holds **Delete**.

There is no Delete tool. A single part is deleted from its settings (trash in the header); several from the selection panel.

**Parts tray, four tabs**, each opening one short list with one line of help (see Parts): **Links** (Spring, Piston, Wing: pick, then drag from node to node), **On a joint** (Brake, Servo, Stepper, Velocity motor, Wheel: drag onto a joint), **Sensors** (Core, LOS sensor: drag onto a joint) and **Blocks** (Battery, Generator, Fuel tank: drag out, rotate, join beams to the corners). You start with a limited number of each ("1 left"); "0 left" is dimmed; an achievement-locked part is dashed with a lock. While a part is dragged, joints that can take it ring in `halo` and a joint that already holds a part shows a `danger` dashed ring and "One part per joint".

**Blocks are objects, not beams.** A block has four fixed corner joints that beams attach to. Blocks never share a joint. A block moves and rotates as one piece and the beams on its corners stretch as when a node is moved. Selecting a block shows its settings and a rotate handle.

**Power chip.** When the creation has powered parts a chip sits at the top left of the canvas: "Uses 1.6 · makes 1.0 · 62%", in `halo` with a bolt when draw is more than output. Tapping it opens the Power budget. It is not in the top bar.

**One right-hand panel, three states.** Nothing selected: the Parts tray. One part selected: its settings (see PartSettings). Several: the selection panel.