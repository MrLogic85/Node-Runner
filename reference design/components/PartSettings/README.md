The **settings panel** for the selected part. It takes the right-hand slot that holds the Parts tray in Build and sits under the brain widget in BuildLocked. Tap a part or beam to open it; tap empty canvas or the x to close it. Every part has an editable **Name**, then its main settings, then its connections, then read-only facts. In Build the header carries **Delete** (trash); on a locked creation it is absent.

**Weight.** Only **beams** (and wings, which are beams) have a weight you set. A Core, a node, motors, sensors and blocks have no setting for it. Blocks and wheels show what they **weigh** as a read-only fact; a **fuel tank** gets lighter as the fuel is used.

**Joint.** Angle limits as a two-thumb range with a mark for **now** (the angle the joint has at this moment). The thumbs cannot cross the mark, so a limit can never be set inside the current angle. Friction is separate.

**Parts on a joint** (Brake, Servo, Stepper, Velocity motor) have **Max strength** (how hard the part can push or hold), and pickers for **Fixed part** and **Target part**. A brake needs no power and has no speed; it is passive strength only. **Stepper** adds Step size; **Velocity motor** adds Max speed. The part guesses fixed and target when placed. The picker lists **only the beams that touch the joint**; picking the beam the other role has **swaps** the two, so it cannot be invalid. Fixed is in `halo` with a dashed swatch, target in `accent`, both lit on the canvas. **Wheel**: radius and grip, and read-only weight; a **motor on the wheel** has its target locked to the wheel.

**LOS sensor.** Rays as five cells, spread, range and rotation. Its rays are drawn while it is selected. **Core.** Toggles for its built-in senses; each on is one more input to the brain.

**Links.** **Spring / damper**: stiffness, damping, rest length, the two nodes. **Piston**: max strength, stroke, the two nodes. **Wing**: the two nodes, Flip, lift, weight.

**Blocks.** **Battery**: shows what is **stored**, of what it can hold ("20 / 20 units"); it takes and gives power without a speed limit. **Engine**: which fuel tank feeds it, and nothing else to set: it makes as much as it can, feeds the powered parts first and charges the batteries with the rest. **Fuel tank**: the fuel it holds, in seconds of engine time, and which engine it feeds.

Powered parts show a **Power** row: "Draws up to 0.6", because a part uses less when it is idle. Structure that changes the model (length, stroke, what a link is between) is a dashed locked row on a locked creation.