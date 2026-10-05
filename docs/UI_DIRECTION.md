# UI direction

`reference design/` is a guide, not the source of truth. It gave the app its
look, words and flows, and it is still the place to start for a screen or
control the app does not have yet: read its `index.html`, the relevant
component README and preview, `tokens.json` and `library.md`. But the app has
moved on from it. Where the two differ, the app and these docs win: the UI
library owns how components look and behave, screen scenes own layout, and
the product docs (this file, `docs/BUILD_MODE.md`, `docs/TRAINING_LOOP.md`,
`docs/GLOSSARY.md`) own rules, flows and words. A new decision is recorded in
the doc that owns its topic; the reference is never edited to match.

`docs/UI_IMPLEMENTATION_PLAN.md` owns UI delivery order.

## Who owns what

Each UI decision has one owner. A layer below never overrides the one above.
`reference design/` is not in the table: it guides each owner but owns
nothing.

| Owner | Owns | Must not |
| --- | --- | --- |
| UI library: `project/src/ui/lib`, component scenes in `project/scenes/ui`, the theme files, `UiSize`/`UiLayout`/`UiSpacing` | The design system: every colour, typography, component, state and component dimension (control heights, radius, stroke, icon and font sizes) | Know about screens or app vocabulary |
| Screen scenes: `project/scenes/screens` | Layout: which components, their order, containers, separations, margins, a slot's minimum size, and text | Restyle a component (colour, font, font-size or stylebox overrides) or change its dimensions |
| C#: view-models in `NodeRunner.App`, screen scripts in `project/src/ui/screens` | Functionality: state, rules, actions, formatting | Build a screen's static layout in code or restyle components |

Whoever builds a piece of layout owns its values. A scene, screen or component,
owns the paddings, separations and slot sizes it authors: it may match a
`UiSize`/`UiLayout` value but does not have to, and code does not re-apply
static values a scene already stores. Layout built in C# takes its dimensions
from `UiSize`/`UiLayout`/`UiSpacing`. UI scaling (#299) does not depend on
this: Godot's own scaling scales a number written in a scene exactly as it
scales one set from a token, so the factor is applied once at the UI root and
the tokens themselves are never scaled
([#331](https://github.com/MrLogic85/Node-Runner/issues/331)). The reverse
also holds: what a library component derives in code is not saved into any
scene ([#309](https://github.com/MrLogic85/Node-Runner/issues/309)). That
covers its styleboxes and constants, clipping, the styling, text and icons it
applies to nodes of its own scene, and sizes it computes. Godot saves every
theme override and never shows them to `_ValidateProperty`, so a `[Tool]`
component lists those properties in a `UiUnsavedState`: it clears them while
the editor saves and restores them after. A plain `Control` that computes its
size returns it from `_GetMinimumSize`, leaving `custom_minimum_size` to the
scene. Button and container subclasses cannot, because their native minimum
size ignores the script, so UiButton owns its `custom_minimum_size`. Screen scripts are thin: they bind scene nodes by unique name,
subscribe to their view-model and forward input. Content that varies at
runtime (one card per creation) is instantiated from library components or
scenes. Widgets in `project/src/ui/widgets` are components that draw
app-specific content: they may use the app's vocabulary and view-models, and
follow the library's styling and size rules; their scenes live in
`project/scenes/widgets`. Screens not yet rewritten still build themselves
in code; they move to this split as they are rewritten
([#310](https://github.com/MrLogic85/Node-Runner/issues/310)), and
`RewrittenUi` in the UI tests is the one list of those that have.
`docs/TEST_STRATEGY.md` lists the guard for each boundary.

## Departures from the reference

Decisions where the app deliberately differs from `reference design/`, so
nobody follows the guide back by mistake. Each names its issue. A difference
does not need an entry to be valid; the app wins either way. Add one when the
reference would mislead someone working on that surface.

- **Unlocking keeps training (#371, 0.13.0).** The reference's padlock resets
  training after a warning, and its "One reset" rule says training is only
  lost by unlocking (`reference design/README.md`, BuildLocked and Overlays).
  Instead the lock only prevents accidental changes, and a rebuild keeps the
  brain through port matching (#516). So the padlock's Unlock dialog is a
  plain confirm, not a press-and-hold. Owner decision; the lifecycle rule
  lives in `docs/TRAINING_LOOP.md` step 6. The "One reset" warning moves to
  Reset training in the overflow (#687): a danger item that opens a danger
  dialog, "Reset training?", naming the generations lost, with "Hold to
  reset". It sits under Copy creation, directly above Delete creation.
- **Destructive actions may sit side by side if each asks first (#687).**
  The reference never places two destructive actions side by side
  (Overlays). Instead two may be neighbours when each opens a dialog and
  confirms with a press-and-hold: a mis-tap then only opens the wrong
  dialog, which names its action. A destructive action that acts on a tap,
  or confirms with a plain tap, never sits next to another. Example: Reset
  training and Delete creation at the bottom of Build's overflow. Owner
  decision.
- **Play is on the rail in both states (#370, 0.13.0).** The reference puts
  Start training in the unlocked top bar and the play button at the bottom
  of the rail only when locked. Instead the primary play button sits at
  the bottom of the rail in both states, so it never moves; the locked top
  bar keeps the padlock. A dimmed play button's reason is the panel's last
  line. Stats sits in the overflow menu, "Coming soon" until 0.15.0.
  The overflow follows training, not the lock: a trained creation lists
  Stats, Power budget, Copy creation, Reset training and Delete creation in
  either state, an untrained one Power budget and Delete creation. Copy
  saves edits first, keeps the brain and confirms with a notification.
  Checkpoints wait for #256. Owner decision.
- **Build has Undo (#689).** The reference has no Undo. Instead Undo and
  Redo are Secondary icon buttons in Build's top bar, after the padlock and
  before the overflow, in every state. They never name the step, are
  disabled when there is nothing to undo or redo, and give no feedback
  beyond the canvas updating. A locked creation's moves and aims are
  undoable, so they work while locked too. They sit in the bar, not the
  overflow, because the overflow has to fit a landscape phone and a menu
  does not scroll. This covers Build body edits only; Reset training and
  Delete creation stay final. Owner decision.
- **Popups name only warnings and danger (#692).** The reference's popup
  overline spells the type, "DEFAULT" included. Instead a default dialog or
  notification has no overline; the others read "Warning" and "Danger".
- **Menus have no row hairlines (#696).** The reference's Standard menu
  draws a 1px line between rows. Instead rows sit without lines; spacing and
  the press tint separate them. Owner decision.
- **Build zoom scales lines too (#400).** The reference keeps a block's
  lines at 2px and its eyes node-sized at any zoom. Instead zoom scales the
  whole picture, lines included, the build area's corner marks too. Four
  things keep their screen size: text labels, the build grid's hairlines
  and the rigid-triangle hatch (1 px at any zoom, so both stay faint; where
  its lines would come closer than `TriangleHatch.MinPixelSpacing` on
  screen, zoomed far out or in a thumbnail, the triangle gets a faint fill of
  the same density instead, #770), and
  the selection frame with its handles, which are controls to grab rather than
  part of the picture (#366).
- **The Build grid marks the build area (#400).** The reference's grid floor
  fills the canvas at 24 to 32px, fades toward the edges, and has fixed HUD
  corner brackets. In Build the `line` grid instead covers exactly the
  build area, with no fade, in fixed 48-canvas-unit cells (1 px hairlines,
  above), and the `accent` corner marks sit on the area's corners, two cells
  long. Cells and marks zoom with the picture; zoom never changes their
  count or length. The view shows the area plus one cell
  (`BuildViewMargin`) on every side and nothing beyond it, at any zoom;
  only where the whole view is zoomed out past the area along one axis is
  the area centred on that axis. Owner decision: the grid is a blueprint
  showing where joints can go.
- **Locked Select can scale (#366).** The reference turns Scale off in
  BuildLocked because beams keep their length. Instead a locked creation
  keeps all three selection handles: moving a joint already changes the
  lengths of its beams, so scaling changes nothing the lock protects.
  Owner decision. Likewise a locked Camera can still be aimed (#638):
  only what changes the model is locked.
- **The selection frame keeps its turn (owner decision 2026-10-03).** The
  reference's frame is always upright, with a hint under it. Ours drops the
  hint, which was in the way, and after a Rotate the frame and its handles
  stay turned with the group until the selection changes. The reference
  has no box select; ours is dashed like the frame, filled `halo` at
  `alpha_soft`, and shows the parts it would catch as selected.
- **Links live in the Beams tool (#705).** The reference tray has a Links tab
  and the rail says Beam. Instead the rail and side-panel title say Beams,
  and the Beams tool's panel lists Beam, Piston, Spring and Wing. The Parts
  tray starts at On a joint.
- **Parts replaces the reference rail's Move label (#706).** The rail starts
  Parts, Beams, Joint, Select. Parts opens the Parts tray when nothing is
  selected. Its glyph is the
  project-owned `assets/icons/ui/parts.svg`, like the accelerometer glyph:
  three rounded tiles plus one lifted diamond.
- **One selection model in every tool (#746).** In the reference only Move
  selects one part and only Select selects several, and a Joint tap on a
  beam splits it. Instead every tool adds or removes a tapped part and shows
  the group frame and handles; Select alone draws a box, and Joint adds a
  joint only on an empty tap with nothing selected. Beams are never split.
- **Tool panels give help when empty (#706).** The reference keeps the tray
  in the side panel. Instead, with nothing selected, Parts shows the tray,
  Beams shows Links, and Joint and Select show short help rows. A selected
  part or selection replaces any tool panel.
- **No locked-canvas chip (#706).** The reference's locked Build shows a
  "Parts locked · drag to move" chip on the canvas. The owner removed it:
  next to the Parts tool it reads as that tool being locked, and the
  padlock and the Training panel already say the body is locked.
- **Piston settings are sliders (#451).** The reference's Piston panel
  lists what it joins ("Between"), its power draw and its weight. Instead
  its Part settings show four `UiSlider`s, Max strength, Stroke, Max
  speed and Rise time (#801), and leave those rows out: a Piston's weight is fixed, not a
  setting (#731), power comes in 0.18.0, and the canvas already shows its two joints. Owner decision: every
  setting is editable, and like a Camera's aim a locked creation keeps them.
  Four sliders and Delete are taller than the panel at Max UI size; the
  side panel's content scrolls (see `UiSidePanel`).
- **Differing values on a slider (#704).** The reference has none. Several
  selected Pistons share their sliders; where their values differ the readout
  shows `low–high` and the span has Marker ends and no thumb. A touch sets
  one value and gives it a thumb.
- **Latest, not best, on the card and in Build (#479, 0.13.0).** The
  reference BuildLocked panel shows the best distance. Instead the
  Creations card values and the Build training summary show the latest
  generation's result, which can drop: Build reads "Latest distance
  4.0 m", while the card keeps the reference's plain "12 generations".
  The Training arena's best marker (and later Stats) shows the best ever.
  Owner decision.
- **No Brain setup (#536).** The reference's Brain setup screen (hidden
  layers, neurons per layer) and its **Brain setup** item in the Build
  overflow menu (Navigation and Build top bar in `reference design/README.md`)
  are removed: a direct brain has no hidden layers to choose. From 0.16.0
  layers are added in the Brain view instead (#543, designed by #549). Owner
  decision.
- **The brain stays visible (#536).** The Training Brain button, the
  Signal flow Brain stage and BrainFocus show the direct brain, inputs
  straight to outputs. Owner decision. The BuildLocked brain widget waits
  for the 0.16.0 brain views (#393, #372); until then locked Build shows
  no brain button (#370, owner decision). The reference
  BrainFocus assumes hidden neurons, so these are best guesses until #549:
  - nothing is selected at first;
  - tapping an output highlights the senses that drive it directly, and the
    sentence reads like "Rear knee is driven most by Front knee: speed and
    Accelerometer: along.";
  - tapping a sense highlights the outputs it drives;
  - only the tapped neuron gets the `halo` ring; its strongest partners show
    through an `ink` label and full-strength links;
  - a row is a tap band across its label and dot; tapping anywhere else
    clears the selection;
  - Signal flow leaves out the hidden-layer size header ("64 · 32").
  - the column headings "SENSES" and "OUTPUTS" (#660) sit over the label
    columns, flush with the labels' inner edge, instead of centred over the
    dots, so a large first dot never runs into them. The sheet's margins
    shrink to 12 (bottom inset 8) to give the card the heading band, so
    as many rows keep their labels as before.
- **Shadows are drawn simplified (#385).** The reference draws the leader in
  full and the other shadows faded but fully detailed, at opacity 0.32 and 0.2
  (Training; GenerationStrip). Instead the followed shadow, which need not be
  the leader, is drawn in full. Every other shadow, the leader included, is
  drawn simplified (`docs/CREATURE_MODEL.md` → "Drawing as a shadow") in its
  normal colours at one alpha, a new `UiTokens.Alpha` entry `alpha_shadow`
  (start at 0.32, the reference's nearer shadow; tune on device). All the
  shadows fade together as one picture, so overlaps do not darken into a
  solid mass, and the followed creature has a knock-out outline in the
  arena background colour, `stroke-signal` wide, around its beams and
  joints (#818). The leader
  is not marked anywhere (#387: it flickers). The reference's camera follows the
  leader; here it follows the previous best by default (shadow 1 in
  generation 0) and never switches to the leader by itself. Owner decision.
- **The shadow strip pages and has a shorter caption (#387).** Past 8
  shadows the reference shows the best 7, sorted, and a sort button.
  Instead the strip holds as many places as fit its width, cells keeping
  their size (#791), and past that many shadows a "worse" chevron, the
  shadows and a sort button, which becomes a "better" chevron on later
  pages; the order is the
  shadows' own until the player sorts, since nobody is best when a
  generation starts (`docs/TRAINING_LOOP.md` → Shadow strip). The caption
  is only "Generation 37": no time and no "Following shadow 5 · 10.3 m".
  Only the followed shadow's cell is marked (`accent`); the leader gets no
  mark, as the lead changes too often and flickers. Owner decisions.
- **No speed control in Training (#787).** The reference's bottom row has
  Pause, speed and the shadow strip. Instead there is only Pause and the
  strip: physics always runs at real time, and training goes faster by
  racing more shadows (`docs/TRAINING_LOOP.md` → No speed-up). Owner
  decision.
- **Training's signal flow is a side panel (#813).** The reference's
  Training column has no header and cannot collapse. Instead it is a
  `UiSidePanel` titled "Status" that collapses like Build's, so both
  screens share one panel. Owner decision.
- **No grid in Training (#668).** The reference draws a faint grid behind
  the Training arena. Instead the arena background is plain: the grid is a
  Build blueprint, not part of the world. Motion shows against the ruler
  along the ground, labelled every 1 m at the closest zoom (the reference
  labels every 2 m) with a minor tick every 0.5 m; zoomed out, labels thin
  out (#675, `docs/TRAINING_LOOP.md` → Ruler). Owner decision. The reference does not say what
  the camera does between trials: a new trial cuts back to the start, and
  switching shadows glides (`docs/TRAINING_LOOP.md` → Camera). Owner decision.
- **Training camera zooms and rises (#675).** The reference's Training
  camera has a fixed zoom and height. Instead it zooms out to fit the
  followed shadow and further the faster it moves, and follows it up once
  it rises past the top margin (`docs/TRAINING_LOOP.md` → Camera). The
  ground stays at the same height on screen while zooming. As in Build
  (#400) zoom scales the picture, the ground edge included; the ruler's
  ticks and labels keep their screen size, and labels thin out rather than
  overlap (`docs/TRAINING_LOOP.md` → Ruler). Owner decision.
- **Best marker reads its distance (#388).** The reference's dashed best
  marker's flag reads "best". Instead it reads "Best 4.2 m", the best ever
  on this map, since a zoomed-out ruler labels only every 5 or 10 m. It is
  drawn behind every creature, keeps its screen size, is hidden until
  its distance is known, jumps when a generation's front goes past it
  (#725) and has no
  off-screen indicator (`docs/TRAINING_LOOP.md` → Best marker). Owner
  decision.
- **Distances are shown from the creature's front (#725).** Every distance
  the player reads (ruler, shadow strip, signal flow, best marker, cards,
  Build, and later Stats) counts from the creature's front-most part, where
  it ended, never below 0, so the number matches where its nose stands on
  the ruler. The score the GA ranks by still follows the centre
  (`docs/TRAINING_LOOP.md` → Trial). Owner decision.
- **A tapped part is named in a callout (#388).** The reference rings a
  tapped part of the leader in `halo` with its name. Instead the followed
  shadow's part keeps its usual selection look, and its Build name shows
  in a `halo` callout straight above the whole creature, its leader down
  to the part, so it never covers the body; the best marker fades to
  `alpha_shadow` meanwhile, since the name may cover its flag
  (`docs/TRAINING_LOOP.md` → World view). Owner decision.
- **Train setup has no profiles and few choices yet (#194, 0.13.0).** The
  reference's Train setup offers Shadows from 1 to 32, power checkbox and
  map row. Instead Shadows runs from 2 (with one, the only shadow is the
  unchanged best brain, so nothing is learned) to 100 (#787: more shadows
  replace the removed speed-up), and the locked maps are
  shown but disabled with "comes in a later version", not the reference's
  Achievements, which the player cannot act on yet. Simulate (#702) plays
  until the player leaves, so unlike the reference both sliders dim:
  Shadows reads 1, below its 2–100 scale, and Run length "Until you leave".
  Neither value is on its scale, so both tracks are empty dashed lines with
  no thumb or fill. It
  plays the latest brain, not the reference's best (`docs/TRAINING_LOOP.md`
  → Generations), and is a disabled segment until the Creation has trained,
  with the note under the switch saying why. Owner decision. Run until power is out (0.18) is disabled with the
  reference's "Needs a battery or generator". Locked maps are disabled cards with a lock,
  not the library's `Locked` card, which means "the only choice". Each map
  card shows the reference's picture of its ground (`MapPreview`: one line,
  accent only on the map in use). There is no Quick/Standard/Deep profile: Shadows and Run length are the only training
  settings, saved per Creation (#617). Owner decision.
- **No part counts until 0.19.0 (#374, 0.12.0).** The reference limits the
  parts you place and shows counts ("1 left") in the tray and in Build
  feedback (`reference design/README.md`, Build and "Rules that fix the known
  problems"). Instead every implemented part is unlimited and the tray shows
  no counts; parts not yet implemented show "Coming later". Counts and
  achievement locks (#525) bring the reference behavior back.
- **No Core; sensors sit on beams (#127, 0.12.0).** The reference has a Core
  part on a joint with toggles for its built-in senses (Parts, PartSettings,
  SignalFlow, Training). Instead Core is removed: an Accelerometer and a
  Camera sit on a beam, one sensor per beam, at its midpoint. The designer is
  not available, so these are best guesses; a design review may change them:
  - *Parts tray:* the Sensors tab lists Accelerometer, then Camera, with
    the help line "Drag onto a beam. A beam holds one sensor." There
    is no Core row.
  - *Glyphs:* Camera uses the reference `los` glyph. Accelerometer uses the
    project-owned `accelerometer` part glyph (an upright frame with a weight
    on a spring), which is not in the reference package. The reference
    `core` glyphs are unused.
  - *On a beam:* a sensor is drawn as a picture of itself, not as a badge
    with a glyph, at the beam's midpoint, in panel fill with 2 px `accent`
    lines. The Accelerometer is a 16 × 22 rounded frame with a zigzag spring
    from its top to a round weight. The Camera is a camera (body, lens ring
    and hood) at twice that scale, so it reads on a phone, that looks along
    its rays. Neither is a circle, so it never reads as a joint. Selected,
    its lines turn `halo`. Its tap area is a square turned with the beam,
    24 for the Accelerometer and 44 for the Camera. Pictures, rays and rings
    stay crisp at any Build or Training zoom.
  - *Beams:* a creature's beams are 6 wide in Build, Training and the
    creation thumbnails. A beam must leave 52 free between its joint rings, room for the Camera with a gap on each side; a shorter beam
    can still be drawn, is drawn in `danger`, and blocks training. Its
    canvas note is a `danger` callout "Too short", out past its joints on
    the beam's upper side with a leader line to the beam's middle (see
    `c_call` in a figure). The readiness line says "1 beam too short".
  - *Thumbnails:* a Creations card shows its creature with the same parts,
    scaled down to fit `Space.S3` inside the thumbnail and centred, but never
    past half its size in Build at 1:1 (#770). Edit-only marks (selection,
    loose and too-short tint, stroke ticks, camera rays) are not shown.
  - *Selection:* every selection mark sits one shared gap
    (`SelectionMarks.Gap`, 3) outside the part's edge (#710). A selected
    joint gets an unfilled 2-wide `halo` ring that gap outside it; a selected
    beam or link (Piston, Spring) gets two 2-wide `halo` lines along it, that
    gap outside each side. A link's lines stop at the joint edges. In a
    selected group, a beam's or link's lines run on to the selected joints' halo rings, so
    the group reads as one outline. All are drawn over the part, in Build and
    Training. A part draws its marks with itself, and a selected part rises
    whole over the other parts of its kind, never over the kinds drawn above
    it (`docs/CREATURE_MODEL.md` → "Draw layers").
  - *Orientation:* the side of the beam that faces up as built is the
    sensor's top, and it then turns with the beam; it never flips during a
    run. The Camera looks along its aim. A Camera selected alone draws its rays
    from the midpoint plus an Aim handle (the rotate handle's glyph) out
    along its centre ray past the picture, with no stem; dragging it turns
    the camera smoothly, with no snap. In Training a Camera on the followed
    shadow draws only the rays that hit the ground, dashed `halo` up to the
    hit and a 6-unit `halo` ring there; other shadows draw no rays
    (`docs/CREATURE_MODEL.md` → "Drawing as a shadow"). Rays leave from the
    picture's edge and are drawn over the joints.
  - *Order:* joints, then sensors, then beams, for both tapping and drawing.
    Dragging from a sensor never moves it.
  - *Placing:* the dragged part's glyph rides on a 48 px raised tile with an
    `accent` line, centred above the finger. While dragging, beams without a
    sensor show `halo`. A beam that already has one shows a dashed `danger`
    stroke, and dropping there shows "One sensor per beam" as a danger
    callout at that beam. Dropping on a joint shows "Sensors go on a beam".
    The callout goes at the next touch or after 3 s. Over a free beam, the
    sensor's picture shows at its midpoint where it would land. Dropping on
    empty canvas cancels silently.
  - *Part settings:* sensors show Name, the beam they are on, a note and
    Delete; no settings until #578. Accelerometer: "Feels how its beam
    speeds up, slows down and tilts." Camera: "Three rays see how near the
    ground is."
  - *In motion:* the Accelerometer's weight moves inside its frame by the
    proof-mass displacement, clamped to the frame, with 1 g at half the
    weight's travel and the spring stretched from the frame's top to it. In
    Build the weight hangs in the gravity rest pose for the beam's current
    angle, swings when the beam is moved and settles again, with the same
    spring as in a run, so nothing jumps when a run starts. No glow where
    the theme or `data-effects="lite"` turns glow off.
  - *Copy:* Training and SignalFlow say "sensor" where the reference says
    "core".
- **Plain joints are a bearing in a cell (#626, owner decisions 2026-10-03).**
  The reference draws a plain joint as an r 6 circle filled with `panel` and a
  `line-strong` stroke ⅔ of the 3-wide beam, so the joint covers the beam
  ends. Instead `JointDrawing` draws, in Build and Training, a
  `line-strong` `stroke-signal` ring whose outer edge is
  `NodeDef.PlainJointRadius`, a `stroke-hair` inner ring at 0.55 of it
  (not on a loose joint, which shows a `danger` ring and cross), and an `alpha_soft` tint: `line-strong`, `halo`
  when selected, `danger` when loose. Lines scale with the zoom. A Training
  shadow draws the outer ring only. Beams and Piston rods stop flat under
  the ring. Motor joints come with #452 and #454.
- **UI size is pixels per unit, with no touch floor and no over-size layout
  (#299, #738).** The reference's % is a multiple of the 640 x 360 canvas
  (50–400%, default 100%, marks at 50/100/200/400), keeps 48px controls under
  100% and opens side panels over the arena above about 200%. Instead the % is
  device pixels per unit, the default is Auto and the range is Min to Max per
  screen; everything around the arena and the Build canvas scales uniformly,
  touch targets included. See "UI size". #381 decides how Settings presents it.
- **Parts tray details (#374, best guess).** The reference tray has no reason
  on a locked row. Instead:
  - Tray rows are compact `UiPartRow`s (`control-sm` high) that still use
    the `icon-lg` glyph.
  - A part not yet implemented shows only its lock; the tab's name row ends
    with a `muted` lock at `icon-sm` and "Coming later" in `t-note`. This is
    temporary until those parts ship; #525's achievement locks need their
    own reason.
  - Locked and "0 left" rows fade as a whole (glyph, name and lock), not
    only their fill.

## Product feel

- **Neon simulator:** the app feels like a digital petri dish for synthetic
  life — more Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls should help the user connect
  cause and effect: sensors → model → moving parts → movement.
- **Low ceremony:** opening the app should quickly show something alive on
  screen.
- **Experiment-first:** the user should be able to change one thing and see
  what happened.
- **Understanding over creatures:** every screen must answer "why did it do
  that?". Controls that neither teach nor drive the core loop do not belong.

## Visual fidelity standard

The app's own UI library and its finished screens are the visual standard:
a change matches their tokens, typography, spacing, radius, stroke widths,
glow, proportions and hierarchy, and reuses library components instead of
restyling them. `reference design/` guides new surfaces and settles questions
the app has not answered yet; a difference from it is not a bug by itself.
Inconsistency inside the app is: two screens that style the same thing
differently, a one-off colour or size, or a control that departs from its
library component.

### Reference token mapping deviations

Every canonical token has either an exact named Godot mapping or a
documented non-runtime reason. The reasons live here, so a later
token import does not recreate the dead mappings.

- **`*-glow` color tokens** (`line-strong-glow`, `ink-glow`, `edge-glow`,
  `accent-glow`, `halo-glow`, `danger-glow`) are **not** imported. They exist in
  the package only because CSS cannot derive an alpha variant from an existing
  custom property. `UiGlow` derives them from the base colour instead, and their
  transparent paper values are expressed by `effects_enabled`.
- **`accent-soft`** is **not** a colour of its own either: it is `accent` at
  the palette's soft-fill opacity. Each theme file authors that opacity once as
  `NodeRunner/constants/alpha_soft` (0–255, because theme constants are
  integers: Neon 31, Paper 26), and a control that wants a soft fill asks for
  `UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft))`.
  Any base colour can use the same alpha. `alpha_shadow` (82 = 0.32 in both
  themes) is the opacity of every Training shadow except the followed one
  (#385); `VisualTheme.ShadowAlpha` reads it. Tests reject alpha-only colour
  entries in the theme files.
- **`shadow.glow`** (`0 0 16px #19f0ff40`) is **not** imported as a
  `glow_radius` constant. A CSS `box-shadow` blur radius has no 1:1 equivalent
  in `StyleBoxFlat.ShadowSize`, and matching the reference's perceived glow on
  device required recalibration, not the literal number. `UiGlow` owns the
  Android-reviewed values (extent 10, opacity 12%) for every glowing surface.
  The paper theme suppresses glow through `effects_enabled`,
  not through a zero radius. An earlier import did map the literal `16`; the
  constant was never read by any control and has been removed.
- **Dimension tokens** (`spacing`, `radius`, `stroke`, and `layout`) are
  constants in `UiSize` and `UiLayout` rather than Godot theme entries. Every
  palette shares the same dimensions — Neon and Paper differ only
  in colour, soft-fill alpha, and glow — so routing them through the Theme added
  a lookup and a per-palette copy without ever allowing a different value. Plain
  constants also keep `stroke` fractions exact, because Godot rounds theme
  constants to integers. Godot performs no scaling of theme constants, so
  nothing is lost: the UI size (issue
  [#299](https://github.com/MrLogic85/Node-Runner/issues/299), see "UI size")
  works on the root window's content scale, not on token values. If a future theme ever needs its own
  dimensions (a compact or dense mode), that token moves back into the Theme.
- **Scene-authored layout widths** have no C# constant
  ([#300](https://github.com/MrLogic85/Node-Runner/issues/300)). A scene
  owns its layout and cannot read a C# constant, so a constant only copies the
  value. `UiLayout` keeps only the values C# reads. Use these reference values
  in the scene directly: `w-dialog` 300 (`UiDialogContent.tscn`), `w-card` 326
  (`UiNotificationContent.tscn`), `w-sheet` 720, `w-sheet-wide` 880,
  `w-brain` 460, `w-well` 250, `h-well` 64, `w-tile` 156, `h-stage` 170,
  `h-thumb` 100, `w-col-xs` 40, `w-col-md` 76, `w-col-lg` 96 and `w-col-xl`
  128. `h-screen` (312) needs no
  value at all: the screen's container gives the body the height left under
  the top bar.

### Reference component mapping

`UiComponentContracts.ReferenceEntryFor` maps each library component to its
`c_*` entry, and `ReferenceEntriesWithoutComponent` lists the entries built
another way; a test keeps both in step with `reference design/`
([#315](https://github.com/MrLogic85/Node-Runner/issues/315)). Some `c_*`
entries are not components of their own in Godot
([issue #306](https://github.com/MrLogic85/Node-Runner/issues/306)):

- **`c_round_button`** is `UiSelectionHandle`: the round icon badge that the
  canvas handles are, and that `c_info_row` shows (`UiInfoRow` draws it through
  `UiSelectionHandle.DrawRoundButton`). `UiInfoRow.ShowHandle = false` omits
  that round button and shows only a muted 16 px glyph in the same column; it
  is used for Joint and Select tool help (#706).
- **`c_rows`** has no component of its own. It is the body of a part's
  settings panel: a plain container whose separation is the panel's
  `space-1` gap, holding rows that carry no outer padding of their own, and an
  optional full-width danger Delete at the end with `space-2` above it
  ([#343](https://github.com/MrLogic85/Node-Runner/issues/343)).
- **`UiSlider`** draws a filled span from `Low` to `High`, each a
  `UiSliderEnd` that is Rounded, Thumb (draggable) or Marker (square with a
  tick). One value is Rounded + Thumb, a range Thumb + Thumb. With no thumb a
  touch raises `TrackPressed` and its screen decides.
- **`c_prog` and `c_meter`** are `UiSlider` with two Rounded ends (no
  thumb). `c_meter` is the same slider with a label and value, as the reference
  says.
- **`c_power`** and read-only facts (for example "Weighs") are `UiValueRow`. The
  reference has no separate read-only value component.
- **`c_card_actions`** is `UiCardActions`, a container of plain `UiButton`s
  rather than a control of its own, so each cell keeps the button's
  behaviour (hold to activate, disabled, badge). The container implements
  `IUiButtonDesigner`: a `UiButton` whose *direct* parent is a designer takes
  its colours, content layout and corners from it (`UiButtonDesign`),
  and the container sizes the cells itself. The button's kind only picks the
  text colour: Primary is accent, Tertiary (destructive) is danger, Secondary
  and Flat are ink. A cell may be text only (no icon), as in `UiDialog`. The
  outer cells round their bottom
  corners to the card's, since a card inside a page cannot clip. Other button groups that need a
  look of their own use the same API. A held cell shows the shared press
  tint inside its own corners (see "Press feedback").
- **`c_textfield`** in a bar does not size to its text, as the Build
  reference shows ("Walker-1 ✎" with empty toolbar space after it). The field
  fills the toolbar width instead. Godot's `LineEdit` has no "…" for long text,
  and no scene setting can cap a width that grows with the text at the space
  left in the bar, so sizing to text would need hand-written text measurement.
  The owner chose a fixed width
  ([#485](https://github.com/MrLogic85/Node-Runner/issues/485)).
- **`c_call` in a figure** goes through `UiCalloutLayer`, the one way to put
  a message in a drawing (#593). The figure's view model lists notes (kind,
  the part it is about, text; for Build `BuildViewModel.CanvasNotes`), most
  important first, and the figure turns each into a spot, a direction clear
  of the part and how far the part reaches that way. `UiCalloutLayout` (its
  own class, apart from any screen) decides where each goes, and the layer
  shows them. Every note is shown; none is left out:
  - A callout sits out past its part, joined to the spot by a leader line
    in the callout's colour, with no dot at the spot (#633). The leader is
    drawn at window-pixel resolution (`UiPixelSpace`), so it stays crisp.
    The callout keeps its screen size at any zoom.
  - Near an edge it is pushed along the edge, never to the part's other
    side, so it does not jump while the user pans or zooms.
  - Callouts that would overlap form one stack, a column growing away from
    the first one's part, in list order (the most important nearest). A note
    with the same text as one in the stack adds only its leader to that
    callout. A stack that grows into another takes it in.
  - Leaders are drawn behind all callouts.
- **`c_panel_head`** is dropped by human decision: it is not part of the future
  design exports, so there is no panel header component. A side panel's
  header, including the inspector's (`c_inspector`), is `UiSidePanel`'s own
  header row; its icon actions are decided when the Build inspector is
  migrated.

## Screen size and safe area

The canvas grows as `reference design/README.md` → "Screen size" describes,
and the same parts keep their size. In this project `window/stretch/aspect="expand"`
implements it. A screen narrower than 16:9 (4:3 tablets, square foldables)
keeps 640 units of width and gains height instead, which the reference does
not cover. 640 x 360 (`UiLayout.CanvasWidth`/`CanvasHeight`) is the reference
canvas, the smallest the layouts fit, not a fixed size; see "UI size". The card row on Creations and Examples
keeps its 16:9 height on a taller canvas instead of stretching: `CardInset`
does not expand and its minimum height is what is left under the top bar at
360 units, so the row sits at the top with the extra height empty below
(#520). `CardScroll` scrolls vertically with a hidden bar, so that minimum
does not push the screen past a window shortened by a safe-area inset; it
sets `scroll_horizontal_by_default` so a mouse wheel still moves the row.

On Android the app runs immersive, so the status and navigation bars are
hidden and only the camera cutout has to be avoided. Godot's
`DisplayServer.GetDisplaySafeArea()` reports that area, and the `SafeArea`
autoload (`UiSafeArea`) turns it into canvas-unit insets. `UiFrame` adds them
at runtime to the card margin its scene authors, so each screen's background
still reaches the screen edge and only the card with the content is inset.
`UiFrame` is never larger than the window: it sets its maximum size to the
visible canvas, and its scene propagates that maximum down to the card, so a
screen whose content needs more room is clipped by the card instead of
growing the frame off screen (#737).
A 180-degree turn moves the cutout without changing the window size or
sending a Godot event, so on a phone the autoload also polls. Dialogs are
centred and notifications sit at the bottom centre, so both stay clear of a
cutout without an inset. The game runs in both landscape orientations
(`window/handheld/orientation` is sensor landscape) (#513). Android's sensor
landscape ignores the phone's rotation lock; the orientation that respects it
(`userLandscape`) is not offered by Godot's setting and would need a Gradle
build, so a lying-flat phone may flip 180 degrees.

## UI size

The UI size (#299, #738) is how many device pixels one canvas unit is, as a
percentage: 100% is one pixel per unit. It scales everything around the arena
and the Build canvas uniformly, touch targets included, with no floor. The
range is per screen:

- **Auto**, the default, is one unit per Android dp (`ScreenGetDpi() / 160`,
  snapped to 5%), because the reference is drawn in CSS pixels, which are about
  dp. A tablet gets more canvas, not a bigger UI. Android buckets and caps
  `ScreenGetScale()`, so a phone does not use it; a desktop uses it as its HiDPI
  factor.
- **Max** is the largest size, floored to 5%, at which a 640 x 360 canvas fits
  the safe area. A small, dense phone lands on it.
- **Min** is 50%.

On an S25 (2340 x 1080, 450 dpi, a 96-pixel cutout) Auto is 280% and Max 300%.
Auto and Max follow the window. A fixed size is kept and limited to the range,
so it comes back when a window grows again (`UiSizeChoice`).

The `UiScale` autoload owns it and applies it once, as the root window's
`Window.ContentScaleFactor`. Under `canvas_items` stretch Godot first scales
the 640 x 360 base by `min(width / 640, height / 360)` window pixels, so the
root factor is the UI size over that stretch (`UiScale.RootFactor`); the
stretch stays fractional. `UiScale` recomputes Auto, Max and the root factor
when the window or safe area changes. Fonts are re-rasterized for the new
scale. Nothing else multiplies by it: tokens, scenes and components keep their
numbers, and `UiSafeArea` already converts the cutout through the visible
canvas, so its inset keeps its pixels at every size. Neon and Paper are Themes
and are independent of it.

`ContentScaleFactor` is not UI-only; it scales every canvas item in the root
window. The two views of a world undo the root factor for themselves, so they
keep their size on screen and only get the space that is left:

- `UiWorldView` lays its SubViewport out at its slot's size times the root
  factor, so the arena keeps its units per pixel. Its screen-size overlays (the
  ruler, the best marker) apply the root factor again, so they follow the UI
  size. Build's world sets `ScalesWithUi` instead: its SubViewport keeps the
  slot's size and scales with the UI like the rest of the screen, and
  `CanvasView` handles the root factor for it. A creation thumbnail's world
  scales with the UI too and authors its SubViewport's update mode as
  Disabled: it renders once (`RequestRender`) when the thumbnail refreshes or
  is fitted again, then keeps that picture, so scrolling renders nothing
  (#770). Each such world still holds its own render target, about 1 MB on a
  phone, so a long list of them needs to reuse its cards (#786). Every world is 2D, so its
  SubViewport sets `disable_3d` and allocates no 3D buffers.
- `CanvasView` divides its zoom limits by the root factor (`UiScale`): fitting
  never magnifies past true size, and pinch zoom stops at `MaxZoom` times true
  size on screen, whatever the UI size. Build's finger-sized hit radii and handles are in view units, so they
  scale with the UI, as touch targets should (`BUILD_MODE.md` lists the
  canvas-unit exceptions).

Icons (`UiIcons`) and the choice indicators are Godot `DPITexture`s, sized in
canvas units (#631). Godot re-rasterizes them, like fonts, at the viewport's
oversampling (the stretch times the factor), so a change needs no pass of our
own. Icon SVGs stay white and take colour from modulate or the control's icon
colours rather than `color_map`.

Settings (#201) chooses and saves the value; until then the Colors & Styles
page sets it for the session (Min, 100%, Auto or Max; a restart returns to Auto). The Settings slider
snaps to 5% steps (`UiScale.Snap`).

640 x 360 is the smallest canvas the layouts fit, which is why Max stops
there. A screen's root has no 640 x 360 minimum, so a desktop window too small
for it still lays out. A screen whose content still needs more room
keeps its frame and toolbar on screen and clips the content (see "Screen
size and safe area").

## App icon

The launcher icon is the Brain glyph (`model.svg`, the 2-3-2 network) lit in
the Neon palette (#820). The inputs are `accent` like the sensors, the
connections fade from `accent` through `line_strong` to `output`, the outputs
are `output`, and the centre neuron wears the brain view's `halo` focus ring.
Each neuron sits on a `panel_raised` disc, and the background is a
`panel_raised` → `panel` → `background` vignette with a faint `muted` dot grid.
Glow is the base colour at low alpha, as in `UiGlow`; the SVGs use no filters.

The sources are in `project/assets/icons/app/`:

- `main.svg`: the full icon, 192 px (Android 7 and `application/config/icon`).
  It copies the foreground art, scaled down in a group.
- `foreground.svg` and `background.svg`: the adaptive layers, 432 px. The
  neurons and their discs stay inside the 66 dp safe circle (radius 132 px),
  so every launcher mask shows them; only the faint outer glow of the
  inputs and outputs may be clipped.
- `monochrome.svg`: one flat white silhouette for Android 13 themed icons,
  with thicker connections and solid nodes.
- `splash.svg`: the foreground on a dark disc that fills the Android 12+
  launch splash mask (radius 144 px). Godot's non-Gradle export cannot set
  the splash background, which stays light, so the badge carries the dark
  ground. It also copies the foreground art.

`foreground.svg` owns the art; change `main.svg` and `splash.svg` with it.
Every colour in the four colour SVGs is a Neon token, and the monochrome
layer is white only, as Android themed icons require. The hidden neurons
are `ink` and the 192 px border is `line`. The dot grid is `muted` at low
alpha rather than the Build grid's `line`, because `line` dots vanish at
launcher size. `AppIconTests` checks
the colours, the sizes, the export preset and both copies.

## Text and translation

Text is translated once, by Godot (#682; `docs/ARCHITECTURE.md` → "UI text
and translation"). `docs/LOCALIZATION.md` owns the translation template and
how to add a language:

- **Text written in a scene** is English and doubles as its translation key.
  The Control translates it itself (`auto_translate_mode` Inherit).
- **Text a view-model builds** arrives as a `UiText`. Show it with
  `UiTextTranslation.ShowText(label, text)` on a `UiLabel` or `UiButton`:
  it gives the control a `TextSource` that translates the text, and the
  control asks it again when the language changes. Meanwhile the control's
  own auto-translation is off, so the text is not translated a second time.
  A component that shows code-set text inside, such as `UiSidePanel`'s
  title, `UiStageCard`'s note, `UiSlider`'s label, readout and step labels,
  `UiPartRow`'s name, `UiTextField`'s placeholder, a `UiDialogSpec`'s
  title or content, a `UiDialogResult`'s failure or a `UiNotificationSpec`'s
  message, has a matching `…Source` property that takes
  `UiTextTranslation.Source(text)` and turns auto-translation off on that
  leaf only. Other components get one when they first need it. A host puts
  popup text it builds, such as "Delete {0}?", into a `UiText` too, and
  passes fixed text such as "Reset training?" as plain English. Godot
  translates whole messages only, so text is never put together in code
  (#773). Text drawn
  in code, such as the arena ruler, the best flag, BrainFocus's labels and
  the Build canvas notes, asks `UiTextTranslation.Source` itself and draws
  again when the language changes. A `UiCalloutLayer` shows the text it is
  given, already translated or as the player wrote it, so it turns
  auto-translation off for its callouts (#758).
- **Text the player wrote**, such as a creation's name, is never
  translated. Its label sets `auto_translate_mode = Disabled` on itself
  only, so the static text around it still translates. A part's own name
  crosses inside App text as `UiText.AsWritten`, an argument that is shown
  as written, while a default name such as "Node 2" is translated (#757).
  The part name field shows the translated default as its text, so leaving
  it unchanged keeps the default instead of saving it as an own name. A
  creation's default name, such as "Untitled Creation" or "Copy of Walker", is
  saved in the language the player has when it is made and is their own
  text from then on (#759).
- **Uppercase** is display only and comes after translating, so the
  translation key stays the authored text. A Label uses its own
  `Uppercase`. Text cased in code, such as the side panel's vertical tab,
  BrainFocus's headings and a segmented switch's segments (Godot's Button
  has no `uppercase`), goes through `UiThemeLookup.LetterCase`, which uses
  the TextServer in the current locale like a Label. Invariant casing is
  wrong in some languages, so `project/src` uses no `string` upper- or
  lower-casing at all (#776). `UiVerticalLabel` translates its `Text` like a Label. An exported
  build cases by the language only with
  `internationalization/locale/include_text_server_data`, which a new
  language turns on (`docs/LOCALIZATION.md`).
- **Translation context** tells two meanings of the same English apart,
  such as "Run" the verb and "Run" the noun. Set `translation_context` on
  the component in the Inspector. Godot does not pass it on to child
  controls, so a component that shows its own text through an inner control
  calls `UiTranslation.ShareContext(this, inner)` before it sets the text.
  The inner control then translates with the component's context (#777).
  Text given through a `…Source` replaces the scene text and is a `UiText`,
  so its context comes from the `UiText`, not the Inspector.
- Counted text is one whole sentence per plural form, and Godot picks the
  form for the language. Never add an "s" in code.
- **Numbers** are arguments, never part of the English: "{0} m", not
  "2.5 m". A whole number is an `int` or `long`; a number with decimals is a
  `FixedNumber`, which keeps how many decimals it shows; `UiText.Number`
  shows a number alone. `UiTextTranslation` writes them with a point and
  Western digits, then `TranslationServer.FormatNumber` swaps in the
  language's own digits, as Godot's number fields do (#756). Godot keeps
  the point in most languages, Swedish too, so we do not use .NET cultures.

## Press feedback

A control shows it is held with one flat tint, `UiPressFeedback`
([#286](https://github.com/MrLogic85/Node-Runner/issues/286)). There is no
ripple, glow, animation or hover look; on a touch screen hover only lingers
after a tap.

- The tint shows only while the control is held (`Pressed` or `HoverPressed`
  draw mode). Sliding off cancels it, and so does a scroll that starts on the
  control.
- It is accent at `Alpha.Soft`, filled inside the control's own corners and
  drawn under its content, badge and hold progress.
- A destructive control (Tertiary button, danger card cell, Danger menu row)
  tints with danger instead, so committing to a delete never flashes the
  "go" colour.
- Over an accent fill (Primary) the tint is on-accent at `Alpha.Soft`
  instead, since accent over accent does not show.
- Both exceptions are best guesses until a design review.
- Selected and disabled controls show no tint: selected already has its own
  look, and disabled does not react.

Every control that reacts to a tap shows it
([#325](https://github.com/MrLogic85/Node-Runner/issues/325)):

- The tint covers exactly the control's tap area. Parts of it that are
  buttons of their own show their own tint instead.
- `UiButton` (with every kind, format and button-group design such as
  `UiCardActions`), `UiMenuActionItem`, the picker's closed row and the
  switch and checkbox rows (`UiChoiceRow`) tint while held. A choice row's
  tint reaches `Space.S2` past its sides so its corners clear the text and
  the indicator.
- The creation card tints above its action bar, over the thumbnail too; a
  drag-scroll of the card row clears it.
- A notification whose tap does something sets `UiCard.ShowsPress`; a swipe
  clears it.
- The side panel's collapse chevron and collapsed tab tint their touch area.
- Controls whose press already changes something need no tint: tabs, segmented
  switches and stage cards select on press, a part row starts a drag, and a
  slider moves.

Two rules keep it that way; `UiPressFeedbackTests` guards the first:

- **No hover look.** A `hover` stylebox repeats `normal`, and a button that
  sets `pressed` also sets `hover_pressed` to the same box, so Godot's
  default theme never shows through. Nothing branches on `DrawMode.Hover`.
- **One input source per tap.** Touch reaches controls as Godot's emulated
  mouse events, so a control reads mouse events (through `PointerInput`) and
  not touch events too, which would handle one tap twice. The build canvas
  needs several fingers, so it reads touch and skips the emulated mouse copy;
  `UiMenu`'s outside-tap dismissal swallows both copies of the tap so neither
  reaches the screen below.

## Immediate-mode drawing and antialiasing

The canvas is 640x360 logical units stretched (`canvas_items`) up to the
device, and the UI size multiplies that again. Godot's antialiased
immediate-mode primitives (`DrawLine`, `DrawArc`, `DrawCircle`, ...) add a
fixed ~1-unit feather in draw space, so drawn in local units the feather
grows with the stretch into a blurry halo. Drawn hard instead, a 1-unit
stroke rounds to 1 or 2 device pixels by position, so dashed borders and
rings look uneven at fractional sizes (#733). `Nearest` texture filtering
and `msaa_2d` (unsupported on Compatibility/GLES3) fix neither.

**Rule: every stroke in `_Draw()` is antialiased in window pixels through
`UiPixelPen` (#733).** The feather is then one device pixel at any stretch,
UI size or Build zoom; widths are in units and the pen scales them. Dashes
are pulled in at each end by the length their feather adds (`DashTrim`), so
the feather does not fill the gap and dash and gap keep their designed lengths. Thin
lines drawn as filled rects, such as dividers, count as strokes.
`JointDrawing`, `PistonDrawing`, `SelectionDrawing`, `UiCalloutLayer`,
`MapPreview` and the Build selection frame predate the pen and map with
`UiPixelSpace` directly; move them to the pen when touched.
`UiStrokeGuardTests` fails any `DrawLine`, `DrawPolyline`, `DrawArc`,
`DrawDashedLine`, `DrawCircle`, `DrawMultiline` or outline `DrawRect` that
is not `antialiased: true` inside a method that opens a pen, calls
`UiPixelSpace.Enter` or takes a `Transform2D toPixels`, and any call to a
`toPixels` helper from outside such a method. Allowed exceptions:

- Width `-1` hairlines (Build grid and hatch): always one device pixel.
- Filled `DrawRect` area fills and `pen.Polygon`: Godot cannot feather
  `DrawColoredPolygon`, so keep a polygon's edge under an antialiased
  outline (sensors, Best marker), or draw a square or diamond as one wide
  `pen.Line` (the brain's negative neuron), since line ends are feathered.
- `UiBoundsDebugOverlay`: debug only.

A part visual bakes the window pixel scale into its strokes, so it must redraw
when that scale changes (a zoom, UI size or screen change). Every view that
holds parts calls `PartVisual.RedrawOnNewPixelScale` as it may have zoomed:
Build's `CreatureParts` on each draw, a creation thumbnail's on each refresh,
Training's `TrainingHost` each frame (also while paused) for every creature
in its world. It
redraws the parts once the scale has moved by `PixelScaleTolerance`, so a
gliding camera does not redraw every creature every frame.

The rule does not cover `DrawStyleBox`: Godot divides a `StyleBoxFlat`'s
feather by the viewport's oversampling, so it stays about one device pixel at
any stretch or UI size, even when drawn from `_Draw()`. Keep
`StyleBoxFlat.AntiAliasing` on, its default (#732).

### Icon filtering

SVG icons rasterize at the viewport's oversampling, but at fractional UI
sizes the image does not land on whole device pixels, so the project's
`Nearest` filter doubles or drops rows. **Rule: icons sample `Linear`;
text and other textures keep the project's `Nearest` (#734).** Where an
icon is already pixel-aligned, `Linear` and `Nearest` give identical
pixels. Text under `Linear` looks soft, so `project.godot` keeps `Nearest`.

- A node whose textures are all icons calls `UiIcons.UseIconFilter` (or
  sets `UiIcons.IconFilter`); its text children call
  `UiIcons.UseTextFilter`. Scene-placed `[Tool]` components hide the
  derived filter with `UiIcons.HideIconFilter`.
- A `Button` that draws its own text next to an icon wraps the icon in
  `UiLinearIcon`, which draws it on a `Linear` child canvas item. Godot's
  `CanvasTexture` can set a filter but draws a `DPITexture` unscaled,
  losing its oversampling.
- Icons drawn on scene-authored nodes set `texture_filter = 2` in the
  scene.

`UiIconFilterGuardTests` fails any icon host without one of these.

## Theme boundaries

Tron/neon is the reference theme, not a permanent constraint. Implementation
must stay theme-agnostic:

- Do not hardcode colors, glow strengths, fonts, stroke widths, or icon choices
  inside simulation or ML logic.
- Prefer theme resources, style resources, exported visual settings, or small
  adapter classes at the Godot/UI boundary.
- Keep layout and state flow independent from the theme so a later "paper",
  "cartoon", or "minimal debug" theme can replace the neon skin.
- Keep flavor copy outside core logic; names in code should describe behavior,
  not a specific visual skin.

The reference's HTML/CSS structure is not a Godot class or node hierarchy.
CSS-only translucent color variants and the CSS glow shadow do not become
Godot tokens; see "Reference token mapping deviations" above. Preserve the
paper theme's transparent glow behavior.

The Godot host uses native `Theme` inheritance for the values that actually
change between skins, and plain constants for the values that do not.

- **Theme** owns colours, palette opacities (`UiTokens.Alpha`), the
  `effects_enabled` flag, and type variations: one per typography, plus
  generated combinations such as typography × text colour (`UiNoteMuted`).
  Controls pick a
  variation by name instead of copying resolved values into overrides, so a
  root Theme swap restyles them natively. `UiTokens` is the
  static set of typed identifiers for exactly those, and `UiThemeLookup`
  resolves them through a control's inherited Theme: `Color`, `Alpha`, `Flag`,
  `Font`, `FontSize`, `ApplyTypography`, `ApplyTextStyle` (the
  typography × text-colour variation of a Label, Button or LineEdit), plus the `CreateStyleBox` /
  `CreateFrameStyleBox` / `CreateRaisedStyleBox` builders. A missing theme item
  is reported as an error in debug builds instead of resolving silently.
- **Theme files are the source of truth.** Each value is authored once, in the
  Godot theme editor or the `.tres` text:
  - `project/assets/themes/Neon.tres` is the project theme (`gui/theme/custom`),
    so the editor and every screen inherit it without a runtime assignment. It
    authors the Neon palette (`NodeRunner` colours, `effects_enabled`) **and**
    everything that is the same in every palette: typography variations
    (`UiBody`, `UiNote`, …, each a `FontVariation` plus `font_size`), the
    default font, `line_spacing`, container separations.
    The default container separation (8) is the theme's value for plain
    containers; it equals `UiSize.Space.S2`, which code-built layout uses, so
    change both together.
  - `Paper.tres` authors only the Paper palette. Godot resolves a variation
    chain from the first theme that declares it and then looks each item up
    through the whole theme chain, so a Paper screen gets Paper colours and the
    project theme's fonts. A colour missing from Paper would silently fall back
    to Neon; the tests require every palette colour in every file.
  - Colour-derived items (text-colour variations such as `UiNoteMuted`, the
    `UiButton`, `UiBadge` and `UiIconTab` variations, and the base
    `Label`/`Button`/`LineEdit` text, icon and caret colours) are regenerated
    by `UiThemeExpander`.
    `UiThemeExpander.DerivedColors` lists them; the expander clears every
    colour of those types and rewrites them, leaving other items untouched.
    After editing a palette colour, build the Debug assembly
    (`dotnet build project/NodeRunner.csproj`) and run
    `Godot --headless --path project res://scenes/tools/ExpandThemes.tscn`.
    Saving from the command line drops the font `uid`s from `Neon.tres`'s
    `ext_resource` lines; restore them (or open and re-save the theme in the
    editor) before committing. `UiThemeExpanderTests` fail if a file was
    edited without regenerating.
  - `UiThemes` loads the files (`For(UiTokenType)`).
- **Constants** own dimensions: `UiSize` for the component scale (`Space`,
  `Control`, `Icon`, `Radius`, `Stroke`, `Widget`), `UiLayout` for
  shell and surface dimensions, and `UiSpacing` for the semantic gap roles.
  These need no control and no theme, so they are usable from pure tests and
  from `[Tool]` scripts. Component-library code takes every dimension from
  them and names any other number; `UiSourceGuardTests` enforces this. When a
  component lets the editor pick a dimension, the choice is a `UiTokens`
  identifier (`UiTokens.Size.Stroke`), and `UiThemeLookup.Size` maps it to its
  `UiSize` constant; `UiSize` stays the one place the value is defined.
- Plain colour maths lives in `UiColorExtensions` (`WithAlpha`, `ScaleAlpha`),
  not in the lookup. C# never writes a colour literal: colours come from the
  Theme and may be derived with an alpha, and only `Colors.White` and
  `Colors.Transparent` serve as neutral modulation.

Custom-drawn and cached controls refresh their drawing or layout locally on
theme change. No per-control palette propagation or subtree adapter is used.
Issue [#236](https://github.com/MrLogic85/Node-Runner/issues/236) tracks the
native theme migration. The UI size is independent of the theme; see
"UI size".

For buttons, the Component Library's **Buttons** paragraph defines the four
current kinds. Older reference summaries still call `secondary` "default"
and `tertiary` "danger"; `on` and `off` are states, not kinds.
`UiButton.Selected` exposes that selected state in C# and the Inspector.
Native `Disabled` is the sole availability setting; UiButton has no inverse
`Enabled` property. Disabling cancels a hold and dims the custom stack/progress
content as well as the native button visuals.
All button text, including the neuron stepper's plus/minus signs, is authored
in the native `Text` property, with the layout's normal typography and padding.
As for UiLabel, `Text` is stored exactly as written (it is also the
translation key, except on a label showing a `UiText`); an internal UiLabel renders it with the `Label` (row) or
`Overline` (stacked) typography, so letter case follows
`UiTokens.IsUppercase` and never changes the stored text. Godot's Button has
no `uppercase`, so the native text is kept but made transparent by the
generated `UiButton` theme variation and excluded from the measured size.
Author `IconId` for the icon. Native `Icon`, `Flat`, text and icon alignment,
overrun, autowrap and clip settings are derived and hidden in the Inspector;
use `Kind = Flat` for the canonical flat style.
`Kind` defaults to `Secondary`, including the Inspector's Reset action.
`UiSegmentedSwitch` is also available through Add Node with an editor preview.
Edit `Segments`, `SelectedIndex`, and `MatchWidth` in the Inspector. Each
`Segments` entry is a `UiSegment` resource: expand it and edit
`Text`, the `IconId` dropdown (`None` means no icon) and `Disabled`, which
shows a segment that cannot be chosen yet (Simulate in Train setup before
the Creation has trained, #702)
like a disabled `UiButton`: a dashed outline over a 50% fill and content,
with the same corners as an enabled segment in that place. Resource edits update
the preview directly. New or cleared resource slots are automatically populated
with independent resources (numbered text, `IconId = None`); remove an array
entry to delete a segment. Generated buttons are internal children recovered
after C# assembly reloads. Adding an extra child to a ready switch emits a warning
in Output, not a persistent configuration warning; that child is never restyled
or removed by segment updates. The former parallel `Options`, `Icons`, and `IconIds`
arrays are removed; locally authored switches must move those values into
segment resources.
Hold-to-activate is available across kinds and layouts, not only destructive
buttons. `HoldToActivate` enables it; `HoldDurationSeconds` only sets the
duration. A new button uses an ordinary click even though the configured hold
duration defaults to 0.8 seconds. Under the human-approved simplification in
[issue #275](https://github.com/MrLogic85/Node-Runner/issues/275), buttons have
no invisible touch margin: visible and clickable bounds are the same.
`UiButton` is the only button class. In the editor-authoring follow-up, the
human requested one `Content Layout` choice: `Row` (40px height/minimum width),
`RowCompact` (32px), or `Stacked` (48x48px). The separate `Compact` boolean is
removed; both row options share rendering and differ only in size.
These sizes come from `UiSize.Control.Default`, `UiSize.Control.Small`, and
`UiSize.Control.Touch`.
Add UiButton directly via Add Node. Its exported `Icon Id` selects a canonical
icon or `None`; no nullable/icon-only wrapper is needed. Existing serialized
Row/Stacked enum values remain stable. C# callers use `UiIconId.None` instead
of null.
The layout picks the icon size, never the call site (#358): a row icon beside
text is 16px, including compact; a row button with no text and every stacked
button use 20px. Textless row buttons need no separate icon layout.
Inspector close uses the shared flat compact row button. Toolbar icon actions
may use Stacked pending their own component review; the human accepted the
temporary visual change and will discuss the removed touch margins with the
designer. This deliberately supersedes the reference's 40px-in-48px button
target, not the touch geometry of other controls.
The Android-reviewed shared glow uses base colours with 12% opacity
and 10px extent rather than separate button/control glow variants.

Other icon sizes follow the reference's rules
([#422](https://github.com/MrLogic85/Node-Runner/issues/422)):
- **Chips.** `UiChip` has one size (`control-xs`). Its icon is `icon-sm` (12),
  or `icon` (16) when it is a part glyph. `GlyphSizedIcon` gives a UI icon the
  same 16px when its chip sits beside glyph chips, such as a map reward among
  part rewards.
- **List rows.** A list row's leading icon follows the row height:
  `icon-lg` (20) in a `touch` row (a Standard menu row, a part row) and
  `icon` (16) in a `control-sm` row (a Compact menu row, the picker's list).
- **Icons inside a ring.** An icon inside a ring is `icon` (16). The selection
  handle and the info row that shows it both draw the same round button
  (`UiSelectionHandle.DrawRoundButton`). As in the reference, only the
  canvas Move handle is filled `accent-soft` (over `bg`, so it stays opaque).
- **Part glyphs.** A part glyph is never drawn at `icon-sm`; `UiIcons.Load`
  rejects that pairing.
- **A lone icon in a scene.** `UiIcon` places one canonical icon beside text a
  scene authors, such as the padlock, stat and map icons on a Creations card
  (#350). The scene picks its `IconId`, `IconSize` and theme `Color`; it draws
  the icon itself, so it follows a theme swap and saves nothing derived.

Toggle and checkbox rows follow the Component Library's rendered specimens:
transparent rows, solid indicator outlines, and 50% opacity for the whole
disabled row. The dashed disabled treatment for buttons/sliders does not
apply to these indicators. Native Godot `CheckButton` and `CheckBox` own input,
state, accessibility roles and indicator rendering through themed textures.
Only label/help layout and theme assets are customized; disabled textures
dim the fully composed indicator once. The obsolete dense toggle size is removed
in [issue #245](https://github.com/MrLogic85/Node-Runner/issues/245).

Segmented controls use native toggle `Button`s in a `ButtonGroup`; Godot owns
exclusive selection and input. The rendered reference keeps option icons
unchanged when selected, unlike `library.md`'s older check/bold wording.
Selection uses the accent-soft fill and 2px outline, not a replacement check.
Content-width segments retain their own widths; full-width segments share the
assigned width equally. Both keep 40px visible height within 48px minimum
touch targets. See [issue #247](https://github.com/MrLogic85/Node-Runner/issues/247).
The human approved wider numeric segments to preserve a 48px target per
option rather than copying the narrower HTML specimen. The existing shared
font adapter still rounds label tracking from 0.04em (0.48px at 12px) to 1px;
this change does not claim exact CSS tracking or line-box equivalence.
Component Gallery and Colors & Styles use Godot `ScrollContainer` native
scrolling; controls inside them rely on Godot's native input dispatch for
tap, drag, fling, focus, and caret behavior.

Slider title/readout rows use native `HBoxContainer` layout; only track-relative
markers and step labels are positioned manually. Inspector theme updates keep
unchanged row controls in the tree, preserving focus and input state. Sliders
also reconnect their resize handling when removed and re-added to the tree.
Slider minimum height ends at the thumb/marker extent or the last visible
text row, using the same track position as rendering rather than adding
another track diameter below its centre.

A slider whose value moves in steps sets `UiSlider.Step` (the distance between
two stops on the 0…1 track, like Godot's `Range.step`), so a dragged thumb
stops only where the value does (#711). The step comes from the value's
`SettingRange` through `SettingSlider.Step` or `ParameterSlider.Step`; never
round the value alone, or the thumb and the readout disagree and the thumb
jumps while dragged.
A setting whose useful values are uneven, like Rise time's 0.1, 0.2, 0.5 and
1 s, uses `SettingRange.Of(stops)` instead (#801): the stops sit evenly along
the track whatever the gaps between them, so the short ones are as easy to
pick as the long ones, and the thumb snaps only to them. They show no step
labels, like every other Build slider; the readout shows the value.

Inspector facts and Power share `UiValueRow`: a label on the left and a readout
on the right, optionally prefixed by a small icon. Power is a value-row
configuration, not a separate control.
Value rows have no vertical padding or fixed minimum height; text/icon content
determines their height and the parent container owns spacing between rows.
Note rows follow the same spacing rule and use native container layout to grow
with wrapped text, without a fixed line count or clipping.

All UI library pages (Component Gallery, **Toolbars**, **Colors & Styles** and
**Popup Gallery**) share `GalleryScreen`: Back, the theme switch, debug bounds
and the overflow menu that moves between pages
([issue #333](https://github.com/MrLogic85/Node-Runner/issues/333)). Each page
scene authors its own copy of the `UiFrame`, toolbar and menu under the same
unique names and marks its own page as selected; keep the copies identical,
including the `[editable path=…]` lines for `UiFrame` and the toolbar. Without
them the scene still loads on desktop, but export drops every node added inside
those instances; `SceneEditableChildrenTests` guards this. Component Gallery,
Colors & Styles and Popup Gallery hold their content in a `ContentFrame` inset
`S3` (12px) from the card, inside a `Scroll` with a hidden scrollbar, so the
inset and every line of content scroll together under the toolbar; the Toolbars
page is laid out as Build is, without one.
Each page is its own scene, opened by `SceneRouter` from its route
(`docs/ARCHITECTURE.md`, Navigation). The pages replace each other: the menu
opens the next page in place of the current one, carrying the theme and debug
bounds in the route, so Back from any page returns to Creations (human decision
on [issue #468](https://github.com/MrLogic85/Node-Runner/issues/468)). A routed
page shows Back and also takes Android Back and Escape, after an open menu or
dialog has handled them. A page run on its own with F6 has no Back and leaves
Android Back to its default. The Toolbars page mirrors the reference's ComponentToolbars page. Its
own toolbar is the `UiToolbar` specimen; by human decision on
[issue #319](https://github.com/MrLogic85/Node-Runner/issues/319) it shows no
separate Editing/Locked specimens. Under it the page shows one `UiButtonBar`
down its left edge (#320) and one `UiSidePanel` down its right edge showing the
Parts tray (#321), both laid out as Build uses them.

Component Gallery's toolbar overflow menu toggles **Debug bounds** live.
Bounds are off by default; `ShowDebugBounds` also supports runtime changes,
and `ui/component_gallery_debug_bounds` can enable them at startup for a
debugging export.

The same menu opens **Popup Gallery**, the interactive specimens for reusable
`UiDialog` and `UiNotification` in `ui/lib`, tracked in
[issue #281](https://github.com/MrLogic85/Node-Runner/issues/281).
The gallery consumes the actual components; only its callbacks are demonstrations
that do not mutate product data. Designer review and rollout to existing product
overlays (#200) remain separate.
`ui/popup_gallery=true` (or `ui/component_gallery=true`) makes a development
export open that page on start, with Back to Creations.
Dialogs support Default/Warn/Danger and independent `HoldToAction` on the
confirmation button. Dialogs and notifications share `UiPopupCard`, the
reference's dialog/toast frame: its border and glow take the severity colour,
accent for Default, halo for Warn and danger for Danger (#355). It is not a
`UiCard` variant, since the popup type picks it, not the screen. Default actions use Primary buttons, Warning actions use
Flat (human decision pending designer review), and Danger actions use Tertiary.
Cancel is Secondary. Both sit as text-only cells in a `UiCardActions` bar flush
with the bottom of the dialog frame (#353): they share its width equally with
a hairline between them, so Primary reads as accent text, Flat and Secondary as
ink and Tertiary as danger, and a hold fill covers only its own cell.
`ActionText` is optional: null, empty or whitespace omits the action button and
the abort button fills the entire row. `AbortText` defaults to `"Cancel"` and
must be nonblank. `Action` is non-nullable with a default implementation returning
`UiDialogResult.Success`; supplying an action label alone therefore gives a
working confirmation button. A custom callback may be passed in the constructor
or set with an object initializer. Omitting the action label means that no
callback can be triggered, even if one was supplied. Abort always reports
`Finished(false)`; successful confirmation reports `Finished(true)`.
`UiDialogSpec.Action` is a `Func<Task<UiDialogResult>>`: success closes, failure
shows its user-facing error and permits retry. Both buttons are disabled while
the callback runs; repeated activation, Cancel, Escape, Android Back and window
close requests cannot dismiss it during that time. Expected failures return
`UiDialogResult.Failure(message)`; unexpected callback exceptions are logged
and show a generic failure, never success. Late completions after owner teardown
do not access freed UI. Outside presses do not dismiss.
The gallery's Action error callback waits, fails once and then succeeds on retry;
there is no simulation flag in the component contract.
Notifications use the same three semantic types and an optional click callback:
true dismisses, false/no action does not. A horizontal drag follows the finger;
releasing at least 48 logical pixels sideways slides the card fully offscreen
in that direction (220ms). A shorter swipe eases back to rest (180ms). Both use
cubic ease-out. Vertical gestures do not move or dismiss the card, and dragging
never invokes the click action, even when the pointer returns to its origin.
They queue one at a time; the next card appears only after the outgoing swipe
finishes. Cards expire after five seconds of idle display. Expiry pauses during
gestures/animations, while hidden or unfocused, and while any `UiDialog` is
open. The modal pause needs no host wiring: an open dialog sits in the
`UiDialog.ModalGroup` scene-tree group and tells every notification (group
`UiNotification.Group`) to re-check when it opens or closes, so the pause holds
until the last dialog closes. Modal pause/focus loss cancels unfinished drags to
rest and pauses a committed exit until resumed. Resize cancels unfinished drags
and retargets an outgoing animation. Clear/dismiss/teardown cancel pending
animation.
Unexpected notification callback exceptions are logged and surfaced as a Danger
notification before the remaining queue. Modal input/focus is isolated from the
gallery. Dialogs and notifications read the Theme inherited from their host;
theme changes do not require rebinding token packages.
Placement, timings, swipe threshold and appearance are proposals for the
designer, not new product-wide rules. All actions are demonstrations only.
`UiDialog` uses an embedded, borderless `Window` with
`Transient`/`Exclusive` for modal input isolation and native focus navigation.
Its transparent viewport covers the gallery for the scrim and unclipped card
effects. Content uses our `UiCard` and `UiButton`, including hold behavior;
it does not hide or replace `AcceptDialog`'s built-in buttons. Godot renders it,
not Android's system dialog. Escape/Android Back and action outcomes are wired
by the component. `EditorToaster` is editor-only, not a runtime Notification
component. Both components work outside the gallery. Add them to the scene tree
before opening; `UiDialog` requires the host viewport's `GuiEmbedSubwindows`.
It restores `QuitOnGoBack` when closed or removed. Godot sends Android Back
only to the main window's own nodes, never into a child `Window`, so the dialog
listens to the main window's `GoBackRequested` and cancels like Abort, deferred
so that the same Back press does not also quit the app. The gallery itself can run
with F6. Editor authoring is being introduced in
[issue #282](https://github.com/MrLogic85/Node-Runner/issues/282). Dialog content
and notification content, as well as Popup Gallery, are scene-authored.

Component Gallery is fully scene-authored. Open
`project/scenes/screens/ComponentGalleryScreen.tscn` and expand
`UiFrame/MarginContainer/Card/Shell/Scroll/ContentFrame/Content` to edit the
component sections. They are the actual runtime controls, not editor-only
copies. The scene may group and rename those sections freely; tests should
cover component behavior, not lock the gallery's visual arrangement.
Edit normal exported properties, UiLabel Text Style presets and native
container separations; theme switching does not recreate or reset those
choices. Demo content inside a component's slot, such as the stage cards'
`Stack/Body` text, is added through Editable Children on that instance.
Keep the unique binding names: `UiFrame`, `Toolbar`, `ThemeSwitcher` and the
`ToolbarMenuDebugBounds`, `ToolbarMenuComponents`, `ToolbarMenuToolbars`,
`ToolbarMenuColorsAndStyles` and `ToolbarMenuPopupGallery` menu items on every
page that carries the gallery toolbar, plus `Scroll` and `ContentFrame` in the
Component Gallery. Do not copy generated component internals into the scene.

Open `project/scenes/screens/PopupGalleryScreen.tscn` to edit the actual popup
gallery. Under the gallery toolbar, `Scroll/ContentFrame` holds the disclaimer
and the dialog and notification specimens in wrapping HFlowContainers plus a
status label, all scrolling together. Labels, order, spacing, and layout live in
the scene; its signal connections bind each button to the demo callbacks in
`PopupGalleryScreen.cs`. Keep the unique `ContentFrame`, `Disclaimer`, and
`Status` names. Theme changes update existing controls instead of rebuilding
the page, preserving authored layout and scroll position. Switching theme
clears queued notifications; an open dialog is modal, so the toolbar cannot
switch theme under it.

Colors & Styles authors its whole inventory in
`project/scenes/screens/ColorsAndStylesScreen.tscn` (#496): the colours in a
Neon and a Paper card that each set their own `theme`, icon sizes, the icon
set, and the text styles. Each colour is a `UiSwatch`, a library control that
draws the token its scene picks as a bordered square. The page leaves out what
is only a base colour with an alpha (the glows and `accent-soft`), and radius
and surface samples; radius shows on every component in the Component Gallery.
Scene tests check that every colour token appears once per theme and that each
icon, icon size and text style has one specimen, found by its Godot group
(`inventory_icon`, `inventory_icon_size`, `inventory_text_style`); a new token
needs a new row in the scene. Colour rows sit in a native `HFlowContainer` at a
fixed width, so the last odd row keeps its width.
Gallery launcher buttons use ordinary clicks; hold requirements belong to the
dialogs they open. F6 exercises the same scene that the Component Gallery opens.

#### Editing a dialog in Godot

For new scene-authored text, add **UiLabel** from Godot's Add Node dialog.
Its **Text Style** dropdown selects one of the 17 canonical `UiTokens` text
styles. `[Tool]` updates the native `ThemeTypeVariation` in the editor; native
Label still owns text, wrapping, alignment, sizing and rendering. The Theme,
Theme Type Variation, uppercase and LabelSettings Inspector
fields are hidden; typography variations come from the inherited native Theme.
Godot cannot hide the dynamic Theme Overrides fields. They stay usable for
experimenting in the Inspector but must not be saved: a scene guard test fails
if a UiLabel saves a color, font or font-size override.
Uppercase is display-only and never changes the authored Text; which styles are
uppercase is fixed per typography (`UiTokens.IsUppercase`). Its **Text Color**
dropdown lists every `UiTokens.Color`, but only text colours
(`UiTokens.IsTextColor`) are accepted; any other choice is rejected with an
error and the previous colour is kept. Together with Text Style it picks one
generated variation (e.g. `UiNoteMuted`), so no colour override is needed.
Layout remains a normal Label property. UiLabel does not accept or store a
`UiTokens` package and does not assign a private Theme; its typography variation
and palette colors come from the inherited screen Theme. The canonical style
definitions are the typography variations authored in the project theme `Neon.tres`.
Changing Text Style
selects a new variation but does not clear an unsaved Inspector override.
This is an authoring API, not a restriction on what arbitrary C# can change.
Existing Labels are not automatically migrated.

Open `project/scenes/ui/UiDialogContent.tscn` in the 2D editor. This is the
actual scene instantiated by `UiDialog`, not a separate mock. Build the C#
project once after script changes so Godot can run its editor previews.

- `Card/Stack/Body/Column/Heading/Titles/Title` and
  `Card/Stack/Body/Column/BodyScroll/Content`: edit the Label's Text for the
  standalone specimen.
- `Card/Stack/Actions/Cancel` and `Confirm`: edit the button's **Text**. Toggle Confirm's Visible for a
  one-button specimen; the abort button fills the row.
- `Card`: edit Custom Minimum Size X for the desired card width. The card is
  `Flush`, so `Body`'s margins are the dialog's padding and the `Actions` bar
  reaches the frame. Containers own child placement; `Column` exposes its
  separation under Theme Overrides / Constants. The view keeps the card
  centered and constrains long content to the viewport.
- Root `Type` and `Theme Preview`: preview semantic type and Neon/Paper.
  Typography and semantic colors still come from shared tokens.
  Previewing an error is possible by showing the named `Error` Label.

F6 on this content scene shows the standalone visual specimen; it has no
action callbacks or modal host. Use F6 on `PopupGalleryScreen.tscn` to exercise
the real modal, including hold, busy, retry and dismissal. Runtime
`UiDialogSpec` supplies title/content/button labels, type and hold state;
those values intentionally replace specimen text, while the authored node
hierarchy, spacing and card width remain the shared runtime layout.

Keep the named `%` nodes (unique names) when rearranging containers; these
are the view's binding points. Button/card editor previews run via `[Tool]`;
button internals are generated without scene ownership and must not be
copied into the authored scene. Editor previews never emit action callbacks.
No external installation is required. While pairing, avoid editing
the same scene file simultaneously and save before handing it over.

#### Editing a notification in Godot

Open `project/scenes/ui/UiNotificationContent.tscn`. Its root is the shared
UiCard with a notification binding script, not a separate preview. Edit
`Column/Heading/Titles/Title` and `Column/Message` for standalone specimen
text. Their UiLabel **Text Style**, wrapping, container arrangement and
separations remain scene-owned. Keep the unique `Title`, `Message`,
`SemanticType`, and `SemanticIcon` names when rearranging nodes.

Root **Type** sets the frame's severity border and glow (`UiPopupCard`), the
semantic overline and the icon colour; **Theme Preview** selects Neon or
Paper. Card **Size Variant** remains a normal shared-card option;
the popup frame glows wherever the theme enables effects (Neon, not Paper). Root Custom Minimum Size X
is the preferred width (initially the 326px card-width token); the runtime
host narrows it to the available viewport and restores that preferred width
when space becomes available again.

F6 shows the standalone card without callbacks, expiry or swipe. Use Popup
Gallery to exercise those interactions. Runtime `UiNotificationSpec` replaces
the specimen title/message/type, and the host supplies the inherited Theme,
focus/input and bottom-center placement. The authored hierarchy, typography choices,
spacing and preferred width are reused unchanged. Queue, expiry, click
callbacks, pause and swipe animation remain in `UiNotification`.

`UiNotificationSpec.Icon` optionally selects a canonical glyph through
`new UiNotificationIcon(UiIconId.Trophy)` or
`new UiNotificationIcon(UiIconId.PartSpring)`. Omit it (or use null) to retain
the type's default: Model for Default, Warn for Warn/Danger. The selected
glyph keeps the semantic tint and Large icon size; it does not change the
type label or frame colour. Arbitrary textures and `UiIconId.None` are not
accepted. The override is runtime data, not a new Inspector field.
`UiDialogSpec.Icon` works the same way for dialogs: the reference lets whoever
raises a dialog pick its icon, so the Delete dialog shows the trash glyph.

```csharp
var dialog = new UiDialog();
AddChild(dialog);
dialog.Open(new UiDialogSpec(
    UiPopupType.Warn, "Continue?", "Review the changes.", "Continue",
    async () => await ApplyChangesAsync(), // Returns UiDialogResult.
    holdToAction: false));
dialog.Finished += confirmed => { /* Host reacts to completion or cancellation. */ };

// Product screens raise notifications on the app-wide layer, from any node.
UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
    UiPopupType.Default, "Saved", "Your changes are saved.",
    OnClick: () => false));

UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
    UiPopupType.Default, "New part unlocked: Spring", "Reached 10 m.",
    Icon: new(UiIconId.PartSpring)));
```

The app's notifications live on `UiNotificationLayer`, the `Notifications`
autoload: a `CanvasLayer` on the `UiLayers.Notification` level, over screens
and menus, that runs while the tree
is paused. It outlives scene changes, so a notification raised just before
the router changes scene still shows after it
([#472](https://github.com/MrLogic85/Node-Runner/issues/472)). Screens do not
add their own `UiNotification`; only Popup Gallery keeps one, so its specimens
follow the gallery's theme switch. Dialogs stay in the scene that opens them:
none has to outlive a scene change, and a scene change closes an open dialog
with its scene.

UI levels ([#768](https://github.com/MrLogic85/Node-Runner/issues/768)): no
code in the UI sets `ZIndex`, because a `ZIndex` sorts across the whole
`CanvasLayer` and draws a raised part through everything above it. The levels
inside one Viewport or Window are the named CanvasLayers in `UiLayers`:
`Screen` (the Viewport's own canvas), `Overlay` (menus) and `Notification`.
Dialogs are embedded Windows, and every Window draws over all the levels of
the Viewport it is embedded in. The Window stack, from the bottom up, is
therefore:
- the root window, with the screen, its menus and then notifications;
- an open dialog;
- a menu that the dialog opens, on the dialog's own `Overlay` level.

A dialog thus covers notifications. A notification queued while a dialog is
open waits until the dialog closes. A menu that floats over its opener sits
in a `UiLevelLayer`, which stays in the opener's subtree, so the menu keeps
the opener's lifetime and unique names. A `CanvasLayer` cuts Theme and
visibility inheritance, so the level layer gives its controls the opener's
theme and hides when the opener does. A scene saves no level of its own: its
`CanvasLayer` is a `UiLevelLayer`, which keeps `Overlay`, or a world backdrop
under the screen, and code sets any other level from `UiLayers`. Training's
world draws in its own SubViewport, whose only layer is the arena backdrop
under it, so its menus and dialogs always draw over the world.

Parts tray tabs use persistent native toggle buttons in a `ButtonGroup`,
with the reference's part glyphs and accent-soft selected treatment, not a
solid accent fill. The tabs share the strip's width equally with 4px between
them (the reference's `flex: 1`), 32px high, so the owner sets the width: four
tabs fit the side panel's content width, about 35px each (#330). The gallery shows one unframed interactive specimen
at the reference's 176px. See
[issue #249](https://github.com/MrLogic85/Node-Runner/issues/249).

Glow is a visual effect outside a control's layout rectangle. Components must
not add layout padding just to make glow visible, because that breaks placement
and popup anchoring. Parents that intentionally clip children, especially
scroll viewports, must either provide container-level bleed/inset for glowing
content or accept/document clipped glow. Product popups should live in an
unclipped overlay layer rather than inside clipped scroll content. See
[issue #252](https://github.com/MrLogic85/Node-Runner/issues/252).

`UiToolbar` is the top bar for every screen
([issue #319](https://github.com/MrLogic85/Node-Runner/issues/319)): 48px, the
component's own Back and Overflow buttons, and between them `%ToolbarContent`,
where the screen authors its title and actions in the editor. `ShowBack` and
`ShowOverflow` hide the fixed buttons. `%ToolbarContent` sits in an 8px
(`space-2`) side margin instead of the HBox separation, so the gap to Back
and Overflow stays the same and the content keeps 8px from the edge when
they are hidden. Creations, the only in-app screen without Back as the root of
navigation (the standalone gallery entry is a debug page), insets its title
48px (`TitleInset`, the Back button's touch width) so the title sits where
titles after Back do. The reference's empty 40px `w-col-xs` slot leaves it
8px short. Nodes a screen adds live under
`Toolbar/HBoxContainer/Field/MarginContainer/ToolbarContent`. `Field` is a
`ScrollContainer` that scrolls sideways with a hidden bar, so the field has no
minimum width: content wider than the space between Back and Overflow is
clipped and can be dragged, and never pushes them out (#737). The field clips
only while its content overflows, so actions that fit keep their whole glow. The toolbar also owns the overflow
`Menu`: it anchors it under Overflow, opens it and makes it dismissible; the
screen authors the items and decides what each does. `BackPressed` is its only
signal. It fills with `panel`, like the button bar and side panel, over the
`bg` of `UiFrame`'s card (`UiFrameCard`, the reference's `.frame`), so the
shell stands out from the screen's content (#347).

`UiButtonBar` is the vertical button bar down the left edge
([issue #320](https://github.com/MrLogic85/Node-Runner/issues/320)): 56px
wide, one touch target plus `space-2` (the reference token is `w-rail`), with
a divider down its right edge and a `panel` background (#347). Its width, padding
and separation live in `UiButtonBar.tscn` (#335). Unlike `UiToolbar` it has no fixed buttons: `%ButtonBarContent` is a plain
VBox, and everything in it (which tools, which is selected or locked, a play
button at the bottom) belongs to the screen. By human decision on #320 it
deliberately differs from the reference's `.rail`: the divider is in `edge`
rather than `line`, and locked tools keep the 20px icon of
every stacked button rather than a 16px lock.

`UiSidePanel` is the reference's SideBar, the fixed panel on the right
([issue #321](https://github.com/MrLogic85/Node-Runner/issues/321)); by human
decision it is a panel, not a bar, because it holds any content. It is 176px
(`UiLayout.SidePanelWidth`, the reference token `w-side`) with a `panel`
background and a divider down its left edge. Its header row is its own: an
optional `IconId`, an optional `Title` and a chevron. Like the reference's
`side-handle`, the chevron is a bare 16px `muted` icon, not a button, centred
in a 32px touch area; both chevrons match (human decision on #358). Code owns only the
panel's own width, which it animates when collapsing; its paddings,
separations and slot sizes live in `UiSidePanel.tscn` (#335). The header
reaches past the padding on the right so the chevron's icon lines up with the
content's right edge. The screen authors
the content below it in `%SidePanelContent` and decides what the panel shows.
That content scrolls under the fixed header with a hidden bar
(`%SidePanelScroll`, #801) when it is taller than the panel, as a Piston's
settings are at Max UI size. The scroll reaches the panel's sides and bottom
and the padding sits inside it, so content is clipped at the panel's edges
rather than short of them, and a slider thumb's glow is not cut. A screen
part that should fill the rest of the panel (the Parts tray, a spacer) still
does, since the content fills the scroll while it fits.
Tapping the chevron sets `Collapsed`: the panel shrinks to a 28px tab
(`UiLayout.SidePanelTabWidth`; the reference hardcodes 28px, it is not a
token) holding a left chevron (the icon set names it `back`) and the title
turned on its side, and
tapping the tab expands it again. Both take 200ms (human decision on #321): the
content keeps its width and slides out past the panel's edge, fading out
over the first half, and the tab, pinned to the panel's right edge, fades in
over the second half so the two never overlap; expanding is the reverse. `CollapsedChanged` reports each change as it
starts; the panel never collapses on its own.
The tab's title is a `UiVerticalLabel`, which draws its text a quarter turn
clockwise because a Container resets a child's rotation. By human decision on
#321 only the chevron collapses the panel (the title does nothing), and
the tab's title keeps the Label typography's letter spacing rather than the
reference's wider `0.08em`, which has no token.

`UiCard.ClipContent` clips the card's content to its rounded shape and draws
the border over it, the way the reference's `overflow: hidden` frames do, so
content that reaches the edge (a square background, a button's glow)
never covers the corners. It is off by default because each clipping card
renders through an extra buffer; `UiFrame`'s card turns it on. Godot cannot nest
`clip_children` (the inner node draws nothing), so `UiCard` and `UiMenu` clip
only when no ancestor already does (`UiClip`, re-checked below a card whose
clipping changes); a menu floated in a `UiLevelLayer` is outside its opener's clipping and clips again. Inside a clipping card, a static menu's row wash is
therefore not rounded at the menu's own corners. For the same reason a card
inside a page does not clip, so content drawn flush against its edge rounds
the corners it shares with the card itself (`UiCorners`): the creature
thumbnail at the top of a Creations card and the outer cells of
`UiCardActions` do. `ClipContent` on such a card still draws its border over
that content.

`UiMenu` is a generic overlay container with a token-backed border/background.
It vertically lays out arbitrary direct child controls. A child gets first
chance to consume input. An unconsumed click bubbles to the menu, which emits
the clicked visible-child index on release over the row the press started on
(so a row shows its press first and sliding off cancels), then consumes the
event before it reaches content behind the overlay. It handles mouse buttons
only: on a phone every touch also arrives as an emulated mouse event, so
handling both would click twice. A `Dismissible` menu (the toolbar's overflow
menu) closes on a tap outside it, Escape or Android Back and emits `Closed`.
While open it takes Android Back over, so that Back does not quit the app. The
dismissing tap is swallowed whole and never reaches what is under the menu. What an item does
is the screen's decision. Selection belongs to individual menu items rather
than the menu, so sectioned and nested menu layouts can manage each selectable
item independently. Item highlights remain square; the menu clips all children
to its rounded surface. `Follow` keeps a menu attached to a
normalized point on an anchor control while scrolling or relayout moves that
control. A following menu must sit in a `UiLevelLayer`, which floats it
over its screen or dialog (see UI levels above).
Menus default to the fixed menu-width token and can opt into content-wrapping
width through `WidthMode`. The menu's `Compact` toggle overrides all direct
`UiMenuItem` children to the matching 32px or 48px row variant, so one menu
cannot accidentally mix densities. The abstract `UiMenuItem` base owns
availability, size, padding, and the square selected highlight.
`UiMenuActionItem` is the recommended optional action child: an icon centred on
the row, the label with an optional note under it, and a check for the selected
choice, laid out by containers over a transparent Button that handles press,
hover and focus. The row shows the shared press tint while held (see "Press
feedback"), and nothing on hover. `Kind` distinguishes
only Default and Danger actions; availability uses the base item's independent
`Disabled` property. It listens for and emits its own `Activated` signal but
defaults to `MouseFilter.Pass`, so the menu's index handler also receives the
click. Set an item to `Stop` only when that item explicitly owns and consumes
the action. `UiMenuToggleItem` composes the standard `UiToggleRow` inside a
Standard or Compact menu row and owns the menu-specific horizontal padding; it consumes its
own input so toggling it does not also invoke the menu's index action. Other
controls remain valid children. `UiMenuItemDivider` is a non-interactive
separator in the `Line` colour at `UiSize.Stroke.Hair` width. It uses the base item's Standard or Compact padding and does not emit
the menu's index-click signal.
See [issue #251](https://github.com/MrLogic85/Node-Runner/issues/251).

## CSS line-height mapping to Godot

**Investigated and visually approved on Android 2026-09-24 in
[issue #278](https://github.com/MrLogic85/Node-Runner/issues/278).**
The previous shared typography adapter derived `Label.line_spacing` from
`LineHeight - FontSize`, clamped to zero. This is not a CSS line-height mapping:
the natural font height can differ from its font size, and Label adds spacing
only between lines.

The shared adapter now uses a `FontVariation` per resolved text style/font size,
without modifying the shared base font:

```text
adjustment = target LineHeight - baseFont.GetHeight(fontSize)
SpacingTop = floor(adjustment / 2)
SpacingBottom = round(adjustment) - SpacingTop
Label.line_spacing = 0
```

Each typography variation's `FontVariation` in `Neon.tres` stores the result of
this formula as `spacing_top`/`spacing_bottom` (letter spacing as
`spacing_glyph`, rounded to at least 1px). Changing a style's size or line
height means recomputing those spacings for its font and size. Target line
heights are the `lineHeight` of each style in `reference design/tokens.json`.

The zero line-spacing default lives once in the project theme
`project/assets/themes/Neon.tres`, loaded through `gui/theme/custom` in
`project.godot`. Godot's built-in Label default is **3px**, not zero. New Labels
inherit the project default without per-component overrides; the typography
variations adjust font metrics rather than Label spacing. Do not subtract those 3px from font heights, since that
would shorten single-line boxes and affect controls that do not add Label's gap.

Top/bottom spacing changes every line's metrics, including a single line and
the outer edges of a multiline block. Letter spacing is the same variation's
`spacing_glyph` in `Neon.tres`. Resolve spacing for the actual font size;
these properties are pixel additions, not relative multipliers.

Godot 4.7.2 Mono and Chrome 153 were measured with the same repository font
files. The following are three-line block heights in pixels; the comparison
column uses corrected `line_spacing = target - natural font height`, not the
previous adapter's formula.

| Style | Natural line height | Target line height | CSS block | Corrected Label spacing | FontVariation block |
|---|---:|---:|---:|---:|---:|
| Note (Barlow 11) | 14 | 14 | 42 | 42 | 42 |
| Body (Barlow 13) | 16 | 18 | 54 | 52 | 54 |
| Readout medium (JetBrains Mono 12) | 17 | 16 | 48 | 49 | 48 |
| Small (Barlow 12) | 15 | 16 | 48 | 47 | 48 |

FontVariation also produced the target heights for one and two explicit lines
and for the inspector note text wrapped at widths of 100, 160, and 240 pixels.
Negative spacing worked: Readout needed -1px total. Note needs no added spacing,
rather than the previous adapter's extra 3px between lines. Browser `normal`
line-height was not always equal to Godot's natural font height, so it should
not be used as the subtraction baseline for the Godot adapter.

This establishes line-box heights, not complete rendering equivalence.
Godot's integer spacing cannot reproduce CSS half-pixel leading exactly for
odd adjustments. The human approved the installed Component Gallery on Android;
runtime checks covered all 17 styles in Neon/Paper and preserved the then-current
32px compact and 48px ordinary icon targets (button geometry changed separately
in #275 above). This does not establish equivalence for arbitrary
fallback glyphs, scaling, or every native text control.

References: [CSS leading and half-leading](https://www.w3.org/TR/CSS2/visudet.html#leading),
[Godot FontVariation](https://docs.godotengine.org/en/stable/classes/class_fontvariation.html),
and [Label line_spacing](https://docs.godotengine.org/en/stable/classes/class_label.html#class-label-theme-constant-line-spacing).

## Rules for UI changes

- Finish and verify the token/typography/size foundation before the Component
  Library, then finish the Component Library before rewriting product scenes.
- Do not build a surface before its issue is ready.
- Keep sim state observable from the simulation/app layers; UI should not own
  training or physics truth.
- Follow the per-issue manual testing decision in `docs/MANUAL_TESTING.md`;
  visible controls and layout changes usually need manual testing.

## Design review

Node Runner has a `design-lead` custom agent
(`.github/agents/design-lead.agent.md`) that reviews UI-touching changes
against this document and helps scope new screens/controls before
implementation starts. `docs/REVIEW.md`'s Definition of Done owns when it
is required.
