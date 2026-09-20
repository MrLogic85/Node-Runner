Node Runner teaches what a neural network is by letting the player build a small creature and simulate it learning to walk. The thing being sold is the understanding, not the creature: every screen must answer "why did it do that?". A control that neither teaches something nor drives the core loop does not belong.

The feeling is a **neon lab bench**, not a cute toy: a dark arena, glowing parts, high-contrast readouts. The audience is a curious beginner who has heard of machine learning but never touched it. Android phone, landscape only, touch only: no keyboard, no hover, no right-click.

## Canvas and layout

Design on a 640 x 360 (16:9) canvas and scale the whole canvas to the device; never reflow. Top bar is 48px (`panel`, `heading` title, one word of status). Below it, an arena or canvas on the left and one fixed side panel (168 to 176px) on the right; Build has a 56px tool rail on the left. Touch targets are at least `touch` (48px). Keep 8px (`space-2`) from screen edges and inset for the display cutout and gesture bar.

## Keep it quiet

A phone in landscape has about 360px of height, so each screen shows only what the player needs right now.

- At rest a screen shows one picture per idea and at most one sentence of text. Captions, legends and counts appear only on tap, or when something is wrong.
- One focus at a time: one expanded card, one halo, one hint. Tapping something new collapses the old.
- Text has three levels only: a title (`heading`), one line of `body`, and short labels. No paragraph is shown unprompted; a first-time hint is one line and appears once.
- Numbers you must have (distance, generations) get `readout`; everything else is a shape with a name on tap.
- Prefer a bar, dial or hatch to a number, and a dashed or filled shape to a colour legend.
- Settings and rare actions (Start over, Restore example, Reset to default) live in the overflow menu, not on the screen.

## The four words

Use exactly these words and glyphs (see Vocabulary): **Node** (attachment point where beams meet and pivot), **Beam** (rigid rod between two nodes, never stretches), **Core** (a sensor package on a node: the eyes, never the brain) and **Motor relation** (a joint the brain can drive, which appears automatically where two beams share a node). Joints come from geometry, so show them the moment they appear, and label a closed triangle as rigid with no joints.

The causal chain is the sentence to serve: cores read the world, the brain decides, motors twist the body, the body moves, distance is the score. The numbered stages **1 Sees, 2 Decides, 3 Twists, 4 Scores** carry it on every screen that shows live data.

## Rules that fix the known problems

- **No dead air.** Trials run one at a time (about 10 s each, 8 per generation). The GenerationStrip makes a generation a shape and its one line says "try 5 of 8".
- **Numbers are a diagram, not a log.** Sensors and motors live in SignalFlow (bars and dials, named on tap), neurons in BrainFocus. Never a scrolling text list of raw values.
- **One reset.** There is only Start over, in the overflow menu, with hold-to-confirm and a 10 s Undo toast. Randomize and Reset do not exist as separate buttons.
- **Nothing is disposable.** Creatures autosave with a visible "Saved" cue, are named, listed and resumable; the example creature can always be restored.
- **The camera follows.** The creature stays about 43% from the left, the ruler scrolls, and a dashed best marker sits ahead.
- **Build gives live feedback.** Joints, rigidity, a brain preview and one validation line update as parts change; Start training is disabled with a reason instead of failing on exit.
- **Safe versus unsafe edits are legible.** Edit mode allows Move only; Beam, Core and Delete are dashed and locked with a sentence why.

## Answers to the open questions (recommendations, all reversible)

- **Rebuilding an anatomy** wipes the brain, because the network shape changes, but keeps the old creature as a saved version ("Walker-1 (v1)") so nothing is lost by accident. Salvaging weights can be added later as a third choice.
- **Duplicating** asks "Copy brain or Start fresh?" with Copy brain preselected: forking a trained brain is the fastest way to run an experiment.
- **Unlocked parts** belong to the player and are available to every creature, but the toast and the list credit the creature that earned them.

## Colour, type, and never colour alone

Base is `bg`; surfaces are `panel` and `panel-raised`. There is one signal colour, `accent`. `halo` means "you tapped this", `danger` means invalid or destructive. Body text is `ink`, captions `muted`. State always pairs colour with a shape, label, position, hatch, dash or icon; dashed edges mean "pushes against", a hatched triangle means rigid, a lock means unavailable. Contrast must survive a phone in daylight: text at 4.5:1 or better, marks 3:1, in both themes.

Type: `heading` and `stage` in Chakra Petch, `body`, `label` and `caption` in Barlow, every changing number in `readout`, `readout-lg` or `readout-sm` in JetBrains Mono. These families are a proposal; bundle the files in the Godot project and swap the names in `tokens.json` to change them. Sentences never go below `body` (13px); `caption` (10px) is for one to three words in dense cards.

## The Tron feel

The neon theme should read as a lit grid: dark surfaces, thin bright lines, light that comes from the lines rather than fills.

- **Lit lines.** Motor arcs, cores, the firing node, the ground trail and corner brackets use `accent` at 2px with the `glow` class (a soft drop shadow in `accent-glow`). Resting beams and node outlines stay `line-strong`, so light means "live".
- **Grid floor.** The arena and the build canvas sit on a faint 24 to 32px line grid in `line`, faded toward the edges so the creature stays the brightest thing.
- **Light trail.** The ground line behind the creature is a gradient that fades in from the left and shows the distance it has run; ahead of the creature it is an unlit `line-strong` rule.
- **Edge lighting.** Frame, top bar, panels and cards are outlined with `edge` (a cyan-tinted hairline in neon, plain grey in paper) plus a faint `accent-glow` halo.
- **HUD corners and traces.** Two `accent` corner brackets top left and right of the arena and canvas, and one or two circuit traces in `edge`; never more, and never near text.
- **Removable.** All of the above use `accent-glow`, `glow` or the `.glow` class, so paper and `data-effects="lite"` turn the light off and keep the grid, lines and shapes.

## Theming and effects

Neon (`dark`) is the reference theme and the default; every screen in this system is drawn in it. Everything reads from tokens, so a theme is a set of values: `dark` (neon) and `paper` (light, no glow) ship, and the Themes card shows the same screen in each. `data-effects="lite"` turns off `glow` and `accent-glow` for low-end devices; motion (signal pulses on edges, firing glow, ring timers) must also be switchable and snaps to values under reduced motion. No hardcoded colours, fonts, stroke widths or icons in core logic; keep sim state in the sim layer and let the UI observe it.

## Copy

Plain words first: "Sees", "Decides", "Twists", "How long is each try". Teach a term once, in one sentence, where it appears (node, beam, core, motor relation). Sentence case in copy; upper case only in labels and stage names. No emoji, no exclamation marks. Name what is lost before asking for confirmation ("142 generations of training").

## Iconography

A stroked glyph set on a 24px grid, 2px stroke, round caps, `currentColor`: menu, back, more, play, pause, speed, restart, plus, copy, trash, lock, unlock, check, x, warn, move, beam, core (eye), build, flag, brain. The part glyphs (node, beam, core, motor arc) are the same drawings used in the arena.

## Not yet drawn

Empty state for a first launch, onboarding, sound and accessibility settings, the full brain compare view, and a tablet layout.
