The causal chain as four stacked cards joined by `accent` arrows, in the order things happen: **1 Senses**, **2 Brain**, **3 Outputs**, **4 Distance**. The words are the parts the player just built: sensors sense, the brain is the network, motors move the body, and distance is the score.

**At rest** each card is one picture and no captions: a row of small level bars (one per sense), a tiny network, half-dials (one per motor) and the distance readout.

**Senses can be many.** A creature can have many sensors, and each has several readings. At rest the card shows the eight most active as bars and a `+6` chip for the rest, and the header carries the total. **Tap a sensor on the body** and Senses expands to that sensor's readings (named rows: "Touch", "Speed", "Angle"...), most active first, in a card about 170px tall that **scrolls** (thin scroll thumb, fade at the foot). The other three cards collapse to one-line headers so the column never overflows; tap one to swap.

**Outputs, not only motors.** The card is always called Outputs, because motors are not the only output. As soon as a creation has other output parts (a spring's stiffness, for instance) the card is titled **Outputs**, keeps the dials for motors and adds one bar row per other output with its part glyph. More rows scroll in the same way.

Tapping a body part outlines the matching card in `halo`. Tapping Brain opens the Brain screen. Cards are 156px wide inside the 176 side panel titled **Status**, which collapses to a 28 tab. Only the Brain card looks like a button, the raised frame with its glow, and opens the brain; the others are plain frames without glow.

**Built so far.** The app shows the four cards as headers with a count ("9 readings", "2 motors", "12.4 m"); the pictures here are the design they grow into (#196).

**A large brain.** The Brain card must stay one glance tall whatever the network size, so it never draws neurons as circles beyond twelve per layer. Past that, each hidden layer becomes a small **grid of squares** (one per neuron, at most 10 x 10, brightness = activity), the senses and motors are a few dots at the ends, and faint bands show the layers are connected. The header carries the shape ("64 · 32" for two hidden layers) instead of a picture of every neuron. The card is still one tap to the Brain screen, where BrainScale takes over.