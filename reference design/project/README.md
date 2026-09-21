Node Runner teaches what a neural network is by letting the player build a small creature and watch it learn to walk. The thing being sold is the understanding, not the creature: every screen must answer "why did it do that?". A control that neither teaches something nor drives the core loop does not belong.

The feeling is a **neon lab bench**, not a cute toy: a dark arena, glowing parts, high-contrast readouts. The audience is a curious beginner who has heard of machine learning but never touched it. Android phone, landscape only, touch only: no keyboard, no hover, no right-click.

## How the app is organised

The app is hub and spoke around the **Creations** menu (see Navigation). **Build** is only for a new creation: draw as many beams as you like, and drag limited parts (Node, Core, Motor, Spring) from a collapsible **Parts tray**, and choose the shape of the brain (1 to 3 hidden layers, 1 to 100 neurons each) in **Brain setup**, and tap any part to edit its settings. **Save** locks the parts for good so the machine-learning model can never be broken. A saved creation opens on the **Creation** screen: drag things around, but add or remove nothing and resize no beam; the name at the top is editable; from there you open **Stats**, the **Brain**, or **Train / Resume**, which goes through **Train setup** (shadows, run length, map) to **Training**. **Achievements** unlock new parts and maps. **Settings** (UI size, theme, sounds) opens from the Creations overflow menu. From Creations a card can be **copied** (identical, brain intact), **edited** or **deleted**.

## Canvas and layout

Design on a 640 x 360 (16:9) canvas and scale the whole canvas to the device; never reflow. Every top bar is 48px with the same shape: Back, title, spacer, at most two icons and an overflow. Below it an arena or canvas on the left and one fixed side panel (168 to 176px) on the right; Build and Creation have a 56px tool rail on the left. Touch targets are at least `touch` (48px). Keep 8px (`space-2`) from screen edges and inset for the display cutout and gesture bar.

## Keep it quiet

A phone in landscape has about 360px of height, so each screen shows only what the player needs right now.

- At rest a screen shows one picture per idea and at most one sentence of text. Captions, legends and counts appear only on tap, or when something is wrong.
- One focus at a time: one expanded card, one halo, one hint. Tapping something new collapses the old.
- Text has three levels only: a title (`heading`), one line of `body`, and short labels. No paragraph is shown unprompted; a first-time hint is one line and appears once.
- Numbers you must have (distance, generations) get `readout`; everything else is a shape with a name on tap.
- Prefer a bar, dial or hatch to a number, and a dashed or filled shape to a colour legend.
- Settings and rare actions (Start over, Restore example, Reset to default) live in the overflow menu, not on the screen.

## Selecting a part

The rail is **Move** (also tap to select one part), **Beam** and **Select** (several at once, to move or, in Build, delete). In Build and Creation, tapping a part or beam opens its **settings** (Name, plus weight for beams, strength for motors and so on; see PartSettings). The settings take the place of the Parts tray in Build and of the actions panel in Creation: the right-hand panel is only ever one of them. A motor has a **Fixed part** and a **Target part**, usually two beams but possibly a wheel, and both are lit on the canvas. Settings that change the structure (beam length) are locked after Save; everything else stays editable.

## The words

Use exactly these words and glyphs (see Vocabulary). **Beam**: a rigid rod, drawn freely. **Node** and **Joint**: where beams meet; a joint has angle limits and is never a motor by itself. **Core**: a node with built-in senses (velocity, elevation, tilt); it is not an eye. Parts you drag in, limited in number: on a joint, **Brake**, **Servo**, **Stepper**, **Velocity motor** and **Wheel**; on a node, the **LOS sensor** (1 to 5 rays, the eyes); between two nodes, **Spring / damper** and **Piston**; on a beam, the **Wing** (a beam with a lift side); and the rigid **Blocks** **Battery**, **Engine** and **Fuel tank**. A node holds a motor or a sensor, never both. A closed triangle or a block is rigid and takes no part inside it. The product word for a saved thing is **creation**; **shadow** is one of the ghost copies that race at once during training.

The causal chain is the sentence to serve: cores and sensors sense the world, the brain works out what to do, motors (and any other outputs) move the body, the body moves, distance is the score. The numbered stages **1 Senses, 2 Brain, 3 Motors, 4 Distance** carry it on every screen that shows live data.

## Rules that fix the known problems

- **No dead air.** Trials run one at a time (about 10 s each, 8 per generation). The GenerationStrip makes a generation a shape and its one line says "try 5 of 8".
- **Numbers are a diagram, not a log.** Sensors and motors live in SignalFlow (bars and dials, named on tap), neurons in BrainFocus. Never a scrolling text list of raw values.
- **One reset.** The only reset is Reset training, in the Creation overflow, with hold-to-confirm and a 10 s Undo toast. Delete asks once and offers Undo too.
- **Nothing is disposable.** Creations autosave with a visible "Saved" cue, are named (editable), listed and resumable; Copy makes an identical creation with its brain.
- **The camera follows.** The creature stays about 43% from the left, the ruler scrolls, and a dashed best marker sits ahead.
- **Build gives live feedback.** Part counts ("1 left"), rigidity, and one validation line update as you build; Save is disabled with the reason instead of failing later.
- **Saved means locked.** On a saved creation only moving is allowed; Beam is dashed and locked (there is no Delete), the tray is gone, and one chip says why.

## Decisions already made

- **Editing a saved creation** never changes its anatomy, so training is never wiped or salvaged; a different shape is a new creation (Copy does not allow it either, since a copy is identical).
- **Copy** keeps the brain and training exactly.
- **Unlocked parts and maps** are player-wide, earned in Achievements, and available to every creation.

## Power

Powered parts (servo, stepper, velocity motor, LOS sensor, piston) draw from a shared supply made by engines and stored in batteries; fuel tanks feed engines. When draw is more than output every powered part gets the same fraction of its force, and when everything is spent the run ends. Show it with the bolt chip and the Power budget (see Power), never with colour alone.

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

Neon (`dark`) is the reference theme and the default; every screen in this system is drawn in it. Everything reads from tokens, so a theme is a set of values: `dark` (neon) and `paper` (light, no glow) ship, and the Themes card shows the same screen in each. The player chooses Neon, Paper or Use phone in Settings (Use phone follows the phone's light or dark setting). `data-effects="lite"` turns off `glow` and `accent-glow` for low-end devices; motion (signal pulses on edges, firing glow, ring timers) must also be switchable and snaps to values under reduced motion. No hardcoded colours, fonts, stroke widths or icons in core logic; keep sim state in the sim layer and let the UI observe it.

## Copy

Plain words first: "Senses", "Brain", "Motors", "How long is each try". Teach a term once, in one sentence, where it appears (node, beam, core, motor relation). Sentence case in copy; upper case only in labels and stage names. No emoji, no exclamation marks. Name what is lost before asking for confirmation ("142 generations of training").

## Iconography

A stroked glyph set on a 24px grid, 2px stroke, round caps, `currentColor`: menu, back, more, play, pause, speed, restart, plus, copy, trash, lock, unlock, check, x, warn, move, beam, core (eye), build, flag, brain. The part glyphs (node, beam, core, motor arc) are the same drawings used in the arena.

## Not yet drawn

Empty state for a first launch, onboarding, accessibility settings beyond UI size, music, map details, and a tablet layout.
