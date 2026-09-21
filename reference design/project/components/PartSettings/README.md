The **settings panel** for the selected part. It takes the right-hand slot that holds the Parts tray in Build and the actions in Creation, and is never visible at the same time as either. Tap a part or beam to open it; tap empty canvas or the x to close it. Every part has an editable **Name** first, then its main setting, then its connections, then read-only facts. In Build the header carries **Delete** (trash); in Creation it is absent.

**Joint.** Angle limits as a two-thumb range (minimum and maximum, zero marked on the track), plus friction. A joint is not a motor, so it has no strength.

**Parts on a joint** all have **Fixed part** and **Target part** pickers. The part guesses both when it is dropped (a beam on each side), and the pickers change them: fixed in `halo` with a dashed swatch, target in `accent`, both lit on the canvas. **Brake**: max speed, no power. **Servo**: strength. **Stepper**: strength and step size. **Velocity motor**: strength and max speed. **Wheel**: radius, grip, weight; it has no pickers because it is the target of whatever motor is placed on it. **Motor on a wheel** looks like a velocity motor, but **Target is locked to the wheel** (dashed row with a lock) and only the fixed beam can be picked.

**LOS sensor.** Rays as five cells (1 to 5), spread, range and **rotation**. While it is selected the rays are drawn on the canvas.

**Core.** Toggles for its built-in senses (X velocity, Y velocity, Elevation, Tilt); each on is one more input to the brain.

**Spring / damper**: stiffness, damping, rest length and the two nodes it is between. **Wing**: lift, weight, a Flip button (swaps start and end, so the lift side changes) and the beam it sits on. **Piston**: force, stroke, between, and power. **Battery**: capacity and max flow. **Engine**: max output and which fuel tank feeds it (or none, for engines that need no fuel). **Fuel tank**: capacity in seconds of engine time.

Powered parts show a **Power** row with a bolt: "Draws 0.6" (or "Makes 1.0"). Structure that changes the model (length, stroke, what a spring is between) is a dashed, locked row after Save; everything else stays editable.