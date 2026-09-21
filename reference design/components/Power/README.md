Powered parts need power. The design says so where it matters: a chip on the canvas, a screen to understand it, and the same chip while training.

**The rule.** Servos, steppers, velocity motors, LOS sensors and pistons draw power up to a maximum that follows their strength; they draw less when idle. An **Engine** makes as much as it can and shares it between the parts, then charges the batteries with what is left. It has no output setting. A **Battery** stores a limited amount and gives and takes power without a speed limit, so it covers peaks. A **Fuel tank** is used up by the engine. If what the parts want is more than what is available, every powered part gets the same fraction of its force: 2 wanted and 1 made means 50%. Brakes, springs and dampers are passive. When the battery and the fuel are both gone, the run ends.

**How long it lasts.** The right-hand summary says "Battery lasts 20 s" and "Fuel lasts 90 s", and says both are **at full draw**: the real time is longer when parts use less or sit idle, because they only draw what they actually use.

**Power budget.** Opened from the overflow menu of Build and BuildLocked, or by tapping the chip. The left column lists what makes power and what each part can use at most. The right card is one number, the **strength every powered part gets**, with a bar and a plain sentence. Enough is `accent` with a check; too little is `halo` with a warn icon.

**Power chip.** A chip in the top left of the canvas, only when the creation has powered parts. In Build it reads "Uses 1.6 · makes 1.0 · 62%"; while training it shows the battery percentage and a small bar. **Limited** adds a warn icon and "50% strength" in `halo`; **Empty** is `danger` with "Out of power", and that shadow's run ends. Tapping it opens a popover with battery, fuel, draw, output and the resulting strength.

**Not designed yet:** charging and refuelling between runs, and how several engines share one fuel tank.