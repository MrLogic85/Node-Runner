A creation that **has trained at least one generation**. It is the Build scene in its **locked** state, not a second scene (see Build), and the lock only guards what would change the model: the model is trained for these parts. It is accident prevention, not a wall.

**Top bar.** Back, the name (still editable), the **padlock** (a selected secondary icon button), Undo, Redo and the overflow (Stats, coming soon; Power budget, coming soon; Copy creation; **Reset training**; Delete creation).

**What stays editable.** It opens in Joint like any other creation. Moving joints, the selection frame with all three handles (scaling only changes beam lengths), renaming, every part's sliders (they change no brain port), and adding or deleting **beams, joints and springs**, which have no brain ports. Copy N stays, unless the selection holds a sensor, Piston or Servo ("Locked: would change the model"). Undo and Redo work.

**What is locked.** Adding or deleting parts with brain ports: sensors and pistons (later servos and motors). The Parts tray shows every row locked and each tab's help line says "Unlock to add parts."; the Links list locks the Piston row. **Delete** is never hidden: it is unavailable (dimmed and dashed, still tappable) when the deletion would change the brain, including a joint that would take a piston with it, a beam that would take its sensor, and a link a Servo holds. Tapping a locked row or a locked Delete shows a notification (default, the lock icon, titled Build): "Locked: the model is trained for these parts." A Servo's link pickers are disabled.

**Unlock.** The padlock opens a default dialog, "Unlock {name}?", confirmed with a tap on **Unlock**. Unlocking **keeps the training** and lasts until Build is left. Adding or removing a part keeps the training but adds or removes that part of the model; removing a part and adding the same kind back does not bring its training back (Undo does).

**Reset training.** A separate danger item in the overflow, "Reset training?": the creation forgets its generations and keeps its body; the dialog suggests copying first.

**No training summary, no brain widget.** The Creations card shows the latest training; the brain views and "what drives what" come with the brain graph (0.16).