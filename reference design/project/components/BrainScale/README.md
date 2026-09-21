How the Brain screen stays readable when the network is big (up to 3 hidden layers of 100 neurons, and many senses). The rule: **draw the count you can read, summarise the rest, and let a tap ask for detail.**

**Level of detail by layer size.**

- **Up to 12 neurons in a layer:** the node-link drawing from BrainFocus, every neuron a circle, every link drawn on tap.
- **13 to 100:** the layer becomes a **grid of squares**, one per neuron (10 x 10 at most), brightness = how active it is right now. Circles at this size would overlap and cost too much to draw.
- **Senses and motors** are always named and never squares. More than six senses show the six most active and a "+8 more" label; tapping it lists all in a scrolling panel (see SignalFlow).

**Links are bundled, not drawn.** With nothing selected, connections between two layers appear only as a soft translucent band from one column to the next: "these are densely connected" without ten thousand lines. Never draw more than about 40 lines at once.

**Tap a square to focus.** It is ringed in `halo`. Only its **strongest links** are drawn: the top three coming in and the top four going out, each a curve whose thickness is the weight; a solid `accent` curve is positive and a dashed `ink` curve is negative (shape carries the sign), with a number pill on the strongest. The neurons at the far ends of those curves get a thin `halo` outline so the eye can follow.

**The card summarises the rest.** Its bars list the top three per side, exactly like BrainFocus, then one line says what was left out: "+11 inputs, +29 outputs, all under 0.10". Tapping that line opens the full sorted list, which scrolls.

**Touch.** Squares are 10px, below the 48px target, so a tap selects the nearest square and shows a **loupe** (the neuron and its neighbours enlarged) until the finger lifts. Pinch to zoom, drag to pan, and **Fit** resets. **Most active first** re-orders the lists by current activity rather than by weight.

**Locked.** The chip in the top bar shows the shape ("2 layers · 64, 32") as set in Brain setup; it cannot change here.