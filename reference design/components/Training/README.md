Named **Training** in the app; the same screen runs a saved brain in **Simulate**.

**Arena.** Plain: the background, the ground (a `panel` fill under an `accent` edge), the ruler and what is on it. Every **shadow** (a copy of the creation with a slightly different brain) races at once. **Shadow 1**, the previous best, is followed by default: it is drawn in full with a knock-out outline in the background colour, and the camera keeps its centre 43% from the left. The other shadows are drawn simplified (a joint's outer ring, a beam's line, links as they are, no sensors) and fade together at `alpha_shadow`; the leader is not marked. Only the shadows on the strip's page are drawn, plus the followed one.

- **Camera.** It zooms out to fit the followed shadow and a second of its travel, keeps the ground 80% down, rises after a shadow that climbs past the top margin, and cuts back to the start when the shadow begins a new trial.
- **Ruler.** Counted from where the followed creature's front starts, labels every 1 m at the closest zoom and minor ticks every 0.5 m; labels thin out to 2, 5, 10 m as the view zooms out. Ticks and labels keep their screen size. Every distance shown is the creature's front.
- **Targets.** The best distance on this map ("Best 9.4 m") and the next distance-based achievement ("Marathon 50 m") are both the callout's flag in its `ok` kind. The nearest one not reached yet is pinned to the arena's top right with a chevron; one that has been passed stands on the ground at its distance, a flag on a dashed line. The ground marks fade to `alpha_shadow` while a part's name shows, since it may cover them.
- **Start sign.** An `ink` arrow sign at 0 m, half a metre tall in the world, pointing the way to go, so a creature that walks backwards is seen to.
- **A tapped part** of the followed shadow keeps its selection look, and its Build name shows in a `halo` callout above the whole creature, with a line down to it.
- **"Too many shadows!"** A Warning chip at the arena's top left when physics falls behind real time for 3 s; it stays 60 s after the last slow stretch.

**Under the arena.** **Pause** (a stacked button; it becomes Play while paused) and, while training, the caption "Generation 37" with a bar that fills as the followed run goes by, over the **shadow strip** (see GenerationStrip). There is no speed control: physics always runs in real time, and training goes faster by racing more shadows.

**Status.** Beside the arena a side panel titled **Status** holds the signal flow: 1 Senses, 2 Brain, 3 Outputs, 4 Distance (see SignalFlow). Only the Brain stage looks like a button (raised, with glow) and opens the brain; the others are plain frames. The panel collapses to its tab, and the arena widens.

**Top bar.** Back (to Build), the creation's name over its status ("Training · Flat ground", or "Simulating · Flat ground"), and **Brain** (the Brain mark) and **Stats** as secondary icon buttons; Stats is unavailable until it is built and a tap says it comes later. No overflow.

**Simulate** plays the latest brain on its own in one run that never ends, and saves nothing: no generation caption, no strip and no shadows; the best marker stays at the saved best.