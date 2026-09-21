How the newer parts behave on the Build canvas.

**Parts tray categories.** The tray is now a row of five tabs, each a glyph: **Nodes** (Node, Core), **On a joint** (Brake, Servo, Stepper, Velocity motor, Wheel), **On a node** (LOS sensor), **Between nodes** (Spring/damper, Piston) and **Blocks** (Battery, Engine, Fuel tank). Counts ("2 left") and locks work as before. One tab is open at a time, which keeps the tray a single short list.

**Placing.** Drag a part; valid targets ring in `halo` and refused ones do not. A part placed on a joint **guesses** its settings, including a fixed and a target beam, and the settings panel changes them. **A node holds a motor or a sensor, never both**: dropping one on a node that has the other shows a `danger` dashed ring and the tag "A node holds a motor or a sensor, not both".

**Blocks** are rigid squares with a joint in each corner, hatched like a triangle and as stiff as one, carrying their glyph in the middle. You join them to the rest with beams at the corner joints. A **Fuel tank** touching an **Engine** feeds it.

**LOS sensor.** Selecting it draws its rays (1 to 5, dashed `accent`, ending in a dot where they stop) from the node, and a small handle on the middle ray rotates the whole fan. The rays are shown only while the sensor is selected.

**Wheels** go on an empty joint. A motor placed on top of a wheel picks one beam as **Fixed** (lit in `halo`) and the wheel as **Target** (lit in `accent`, and locked: the settings show a dashed row with a lock).

**Power chip.** As soon as a creation has any powered part the top bar shows a bolt chip, "draw / output". It turns `halo` with a warn icon when draw exceeds output, and opens the Power budget (see Power).