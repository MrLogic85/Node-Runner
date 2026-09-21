A creation **after its first finished training session**. It is the Build scene in its **locked** state (`Build` with `locked = true`, not a second scene), so the parts and the brain shape that the trained model depends on cannot change.

**Top bar.** Back, the name (editable) with the "Saved" cue, a **padlock** icon button in `accent` and the overflow menu (**Stats**, **Power budget**, **Copy creation**, **Delete creation**). Tapping the padlock opens the **Unlock dialog** (see Overlays): unlocking deletes the training, so the dialog names it ("142 generations"), suggests copying first, and needs a press-and-hold.

**Rail.** **Move**, **Beam** and **Joint** (both dashed and locked) and **Select**, and a large **play** button at the bottom, which opens Train setup. In Select the handles are Move and Rotate; Scale is off, because beams keep their length.

**One right-hand panel.** The top of the panel is always a small **brain widget** (about 154 x 96): the network drawn tiny, no numbers. **Tapping the widget opens the Brain view** (see BrainFocus). Below it: nothing selected shows how many generations and the best distance. With a part selected the panel shows that part's **settings** under the widget (they scroll).

**What drives what.** Tap a part and a **soft glow appears behind the other parts it is connected to** through the brain: cyan behind parts that affect it, yellow behind parts it affects. There are no labels or boxes on the canvas. The **brain widget** shows the same thing: it hides all lines except the **strongest paths** (the 20% rule), **cyan** from the senses that drive the part's outputs, **yellow** from the part's own senses to the outputs it affects. A neuron that belongs to the tapped part itself (a servo's angle and speed senses, its target output) gets a `halo` ring, the same ring as on the canvas. If a cyan and a yellow path share a line they are drawn **side by side**, never on top of each other. No numbers.

- **Cyan glow** (`accent`): parts that **affect** the tapped part, that is, the ones whose senses push its outputs hardest (every one within 20% of the strongest).
- **Yellow glow** (`output`): parts the tapped part **affects**, the ones its senses drive most (the top 20%).

A part can be both. A servo senses its angle and speed and outputs a target angle and strength, so tapping one glows cyan behind the parts that drive it and yellow behind the parts it drives. The tapped part itself keeps the `halo` ring, and the widget shows the same neurons with the senses on the left and outputs on the right, so the meaning does not depend on colour alone.

Nothing here adds or removes parts. To change the body, unlock it (resets the training) or copy it.

Build and BuildLocked are **one scene in two states**. In Godot there is a single `Build` scene with a `locked: bool`; there is no second scene. BuildLocked is only the name of the page here that shows the locked state. The flag changes: the rail (Beam and Joint are dashed and locked, and a large play button appears at the bottom), the top bar (Start training becomes a padlock and the overflow menu), the right panel (the Parts tray becomes how trained the creation is and its actions), and the settings (structure rows become read-only). The canvas, the rail, the brain widget and the part settings are the same nodes.