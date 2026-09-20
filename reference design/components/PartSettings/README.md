The **settings panel** for the selected part. It occupies the right-hand slot that holds the Parts tray in Build and the actions in Creation, and is never visible at the same time as either. Tap a part or beam to open it; tap empty canvas or the x to close it.

Every part has an editable **Name** first. Then the settings that part has:

- **Node:** weight, grip.
- **Beam:** weight. Length is shown read-only: in Build it is set by dragging the ends, in Creation it is locked.
- **Core:** what it **reads** (ground touch, tilt and so on), range, sensitivity.
- **Motor:** strength, range, and two pickers: **Fixed part** (the side that stays put, `halo`, dashed swatch) and **Target part** (the side the motor turns, `accent`). Usually both are beams, but a target can also be a wheel. The two parts are lit on the canvas with matching tags.
- **Spring:** stiffness, damping, rest length (read-only after saving).

A sliders row is a label, its value in `readout-sm`, and a 22px track with an 18px thumb; the whole row is at least 48px to touch. A dashed, locked row means "fixed after Save". The header carries **Delete** (trash icon, `danger`) in Build, because there is no Delete tool; it is absent in Creation, where parts cannot be removed. Deleting several parts at once is done from the selection panel (see Build). The list will grow as parts do, so every part follows this order: Name, then main setting, then connections, then read-only facts.