# UI direction

The rules for how the app's UI looks. `reference design/` gave the app its
look, words and flows and is still the place to start for a new screen or
control, but it is a guide, not the source of truth: where the two differ,
the app and these docs win, and the reference is never edited to match.
How the world itself is drawn is in `docs/WORLD_VISUALS.md`; how the UI
library's code is written is in `.claude/rules/ui-lib.md`.

## Who owns what

Each UI decision has one owner. A layer below never overrides the one above.
`reference design/` guides each owner but owns nothing.

| Owner | Owns | Must not |
| --- | --- | --- |
| UI library: `project/src/ui/lib`, component scenes in `project/scenes/ui`, the theme files, `UiSize`/`UiLayout`/`UiSpacing` | The design system: every colour, typography, component, state and component dimension (control heights, radius, stroke, icon and font sizes) | Know about screens or app vocabulary |
| Screen scenes: `project/scenes/screens` | Layout: which components, their order, containers, separations, margins, a slot's minimum size, and text | Restyle a component (colour, font, font-size or stylebox overrides) or change its dimensions |
| C#: view-models in `NodeRunner.App`, screen scripts in `project/src/ui/screens` | Functionality: state, rules, actions, formatting | Build a screen's static layout in code or restyle components |

Whoever authors a layout value owns it, so code never re-applies a value a
scene already stores, and what a library component derives in code is
never saved in a scene.

## Product feel

- **Neon simulator:** a digital petri dish for synthetic life, more
  Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls help the player connect
  cause and effect: sensors → network → moving parts → movement.
- **Low ceremony:** opening the app quickly shows something alive.
- **Experiment-first:** change one thing and see what happened.
- **Understanding over creatures:** every screen answers "why did it do
  that?". Controls that neither teach nor drive the core loop do not belong.

## Visual fidelity standard

The UI library and the finished screens are the visual standard. A change
matches their tokens, typography, spacing, radius, strokes, glow,
proportions and hierarchy, and reuses library components instead of
restyling them. A difference from `reference design/` is not a bug by
itself; inconsistency inside the app is: the same thing styled two ways, a
one-off colour or size, or a control that departs from its library
component.

## Tokens

Colours, opacities and typography are Theme items, one set per theme
(Neon, Paper), which differ only in colour, soft-fill alpha and glow. Neon
is one skin: layout, state and names never depend on it. Dimensions are
constants every theme shares (`.claude/rules/ui-lib.md` → "Theme and
tokens").

### Reference token mapping deviations

Every reference token has a Godot mapping or a reason here, so a later
token import does not bring a dead one back.

- **`detail`** marks passive parts and details, which the network neither
  drives nor reads: a Spring's coil, a Brake, a Servo's Fixed-side band
  and picker tint. A Target-side band stays `accent` on a Brake too: the
  band colours name the side, not who drives it. `accent` marks what the
  network drives or reads. Structure (beams, joints, rods, seats, bodies)
  stays `line-strong` on `panel`.
- **`accent-soft`** is not a colour but any base colour at the theme's
  `alpha_soft`. **`alpha_shadow`** is the opacity of every Training shadow
  but the followed one.
- **`*-glow` colours** and **`shadow.glow`** are not imported. CSS needs
  glow colours only because it cannot derive an alpha variant, and a CSS
  blur has no 1:1 `StyleBoxFlat` equivalent, so `UiGlow` derives every
  glow from the base colour with values reviewed on Android. Paper turns
  glow off with `effects_enabled`.
- **Dimension tokens** are code constants (see Tokens).

## Screen size and safe area

The canvas grows as `reference design/README.md` → "Screen size"
describes, and parts keep their size. A screen narrower than 16:9 keeps
its width and gains height; 640 × 360 is the smallest canvas the layouts
must fit, not a fixed size. The Creations card row keeps its 16:9 height.

The app runs immersive in both landscape orientations, keeping to the
rotation lock (`userLandscape`). Only the camera cutout is avoided: the
card holding the content is inset, and the background still reaches the
screen edge. Content that needs more room is clipped inside the frame, not
pushed off screen.

## UI size

The UI size is how many device pixels one canvas unit is. It scales
everything around the arena and the Build canvas uniformly, touch targets
included; the world views keep their size and get the space left.

- **Auto**, the default, is one unit per Android dp, because the reference
  is drawn in CSS pixels, which are about dp. A tablet gets more canvas,
  not a bigger UI.
- **Max** is the largest size at which a 640 × 360 canvas fits the safe
  area. Auto and Max follow the window; a fixed size is limited to
  `UiScale.MinPercent`..Max.

## Touch and press feedback

- **Tap only.** Nothing is press-and-hold; where care is needed, a dialog
  names the action.
- **No hover look** (`.claude/rules/ui-lib.md` → "Input and press
  feedback").
- **No touch margins,** unlike the reference's 40-in-48 targets: a
  control's hit area is its drawn size.
- **Press tint:** a held control shows one flat `accent-soft` tint inside
  its corners, with no ripple or animation; `danger` on destructive
  controls, on-accent on Primary ones. Sliding off or scrolling cancels it.
  Controls whose press already changes something (tabs, segments,
  sliders, part rows, stage cards) and selected or disabled ones show
  none.

## Components

The Component Gallery shows every component, and each component's summary
says what it is. Every component follows these rules.

- **The layout decides alignment, never a nudge.** Texts that share a line
  stand on one baseline (`UiBaseline`, or `UiLabel.BaselineOf` in a
  scene), and what sits in a centred layout is centred. No text or icon is
  moved a few pixels to look right: a font that looks off-centre is fixed
  in the theme, for every text that uses it.
- **A state looks the same on every component:**

  | State | Looks | A tap | Means |
  | --- | --- | --- | --- |
  | Disabled | Dashed and dimmed | Does nothing | The action cannot happen, and needs no reason |
  | Unavailable | Like Disabled | Says why | The action cannot happen, and the screen can say why: a later version, a locked Creation, or danger notes on the parts that block it. Used wherever the screen can say why |
  | Locked | Its lock glyph, faded whole | Says how to earn it | Content not earned yet |
  | Selected | `accent-soft` fill and an `accent` border | — | The picked item |
  | Empty | Neutral, never `danger` | — | A start, not a fault |
  | Failed | `danger` | — | Something went wrong |

  A lock glyph means content not earned yet, or a trained Creation's
  padlock; nothing else wears one, so Unavailable has none. `UiCard`'s
  `Locked` kind means "the only choice", not locked content. A new state
  joins this table with the design of the screen that needs it.
- **A screen, a sheet or a dialog:** a screen is a place the player goes,
  routed (`docs/ARCHITECTURE.md` → "Navigation") and left with Back; a
  sheet shows more of the screen under it without leaving it (NetworkFocus
  over a running Training); a dialog asks for one decision and blocks
  until it is answered (`UiDialog`); a notification tells and asks
  nothing.
- **A failed user action** is a Danger notification titled "<Action>
  failed", with the action's icon where one exists ("Create failed" with
  Plus). Its text prefers "Could not <verb> <object>. Try again."; save
  failures may say what stayed unsaved. One action fails with the same
  notification wherever it starts. Route and state notices, such as a
  missing creation or a training prerequisite, are not failures and stay
  Default. Dialog-inline errors and log-only failures are not
  notifications.
- **Icons:** one folder, `project/assets/icons/`, each file named
  `<kind>_<name>.svg`, where the kind is `icon` (UI icon), `part` (part
  glyph) or `mark`. Kinds are named, not nested, so a file says what it
  is; `icons/app/` holds the launcher art, not icons. A UI icon is drawn
  16 on its longer side, centred on a plain 24 viewBox, so icons side by
  side match in size and air. Part glyphs keep their 20 grid. A mark
  echoes the launcher glyph and is drawn 20 on the 24 grid, since at the
  shared stroke a network drawn 16 fills into lumps (`UiIconGridTests`).
  The size an icon is shown at is the component's (`UiButton.ContentLayout`,
  `UiChip`).

## Component decisions

Choices only one component makes, and why.

- **Buttons:** Primary, Secondary (the default), Tertiary (destructive)
  and Flat.
- **Popups:** dialogs (centred) and notifications (bottom centre, one at
  a time) share `UiPopupCard`, whose border and glow take the type colour.
  Only Warning and Danger show an overline. Actions are text cells in a
  `UiCardActions` bar flush with the frame; the confirm action is Primary,
  Tertiary on a Danger popup and Flat on a Warning one. A dialog covers
  notifications. A notification's `Id` and `Replaceable` keep repeated tap
  answers, such as Build's lock note, from piling up. A copy to the
  clipboard confirms at once (`UiClipboard.Copy`), beside Android's own
  toast.
- **Menus:** no row hairlines, unlike the reference; spacing and the press
  tint separate rows. A menu has one density, Standard or Compact. Menus
  other than a picker open upward when there is no room below.
- **`UiPicker`:** opens its list as wide as the field, always below its
  row, never over its own heading. A picker with nothing picked shows its
  prompt in `danger`. In a hinge part's side pickers only the current
  Fixed part (`detail`) and Target part (`accent`) are tinted, in both
  lists, to match the canvas bands; other options keep `line-strong`,
  since `muted` would make them look disabled.
- **Toggles and checkboxes:** transparent rows with solid indicator
  outlines; a disabled row is 50% opacity, not dashed.
- **Segmented switch:** the selected segment's icon does not change.
- **Shell:** `panel` over the frame card's `bg`. A toolbar's content
  scrolls sideways when it does not fit, and a toolbar text field fills
  the toolbar's width rather than sizing to its text. `UiButtonBar` takes
  an `edge` divider, not the reference's `line`.
- **`UiSidePanel`:** collapses to a narrow tab with its title on its side.
- **Sliders:** where selected parts differ, the readout shows `low–high`
  between Marker ends, with no thumb. Uneven stops sit evenly along the
  track.
- **Tray rows and tabs:** glyph tabs share the strip's width. The picked
  row's info line fades in right under it, in the Parts tray and the
  Links list alike.

## Screen decisions

What each screen does is in `docs/BUILD_MODE.md` and
`docs/TRAINING_LOOP.md`; its scene shows how it is laid out. These are the
choices each screen made, and the precedents a new screen looks to.

- **Creations:** a card shows its latest result as one stat per row with
  its unit, not the reference's bare numbers in one row.
- **Import:** there is no field for the code: Paste reads the clipboard
  only when tapped. The preview draws at Build's true size, never larger,
  so it shows what Build opens with. The parts are fact rows, not buttons.
  "You already have one by this name." replaces the reference's "You
  already have a {0}.", which fits no article to every name or language.
- **Settings:** each change applies and saves at once, with no Save
  button, so the screen itself is the preview. Use phone's theme picture
  is split Neon | Paper, so it does not read as a fourth theme.
- **Build:**
  - The grid button's icon is crossed out when snap is off; the drawn
    grid shows the size. Its menu stays open while the size or snap
    changes.
  - A part's description (`PartInfo`) is one short sentence saying what
    the part does, not how it is wired or what else it needs. A placement
    hint is at most one short sentence. Neither names other parts.
  - The readiness chip is one line, so every reason has a short form. It
    takes no taps, and it is not a callout, so it never reads as a note
    whose part is missing. On an empty canvas it is neutral, a start
    rather than a fault.
- **Train setup:** locked maps are disabled cards with a lock, not the
  library's `Locked` card ("the only choice"); unlike a locked part row,
  a tap on one does nothing yet.
- **Training:**
  - Only the Network stage looks like a button, raised with glow, since
    only it opens something; the other stages are plain frames.
  - A stage's empty-state line is Body `muted`, not Caption like the
    header note, so a sentence stays readable on a phone.
  - Only the followed shadow's strip cell is marked (`accent`), since
    the lead changes too often to mark.
  - While the run is paused the subtitle reads "Paused · {map}" and the
    Pause icon replaces the `accent` dot, so the pause shows in shape and
    word, not colour alone.
- **NetworkFocus:** titled "Network", after the stage that opens it;
  NetworkFocus is the component's name, not player copy. "INPUTS" and
  "OUTPUTS" head the label columns and rows scroll under them; every row
  keeps its label, at least one line apart. Only the tapped neuron gets
  the `halo` ring; its strongest partners get an `ink` label and
  full-strength links, and a sentence names them.

## Departures from the reference

Where the app differs from `reference design/` and someone might follow
the guide back by mistake. Departures stated in this doc's own sections
are not repeated, and what the app does not have yet is in its issues.
The last column owns the detail (BM = `docs/BUILD_MODE.md`, CM =
`docs/CREATURE_MODEL.md`, TL = `docs/TRAINING_LOOP.md`, WV =
`docs/WORLD_VISUALS.md`).

| Reference | Ours | Owner |
| --- | --- | --- |
| Start training in the top bar | Play at the bottom of Build's rail in both states | — |
| Rail says Beam and Move; Links tab | Links and Parts tools | BM → Interactions |
| Part counts ("1 left") | Unlimited | BM → Parts tray |
| Core part | Sensors on beams | CM → Parts |
| No Memory cell | A Memory cell in the Sensors tab, on a beam like a sensor, drawn as a barometer | BM → Interactions, WV → Part decisions |
| Piston: one cylinder as long as its travel; Spring: one damper body | A three-part telescope, starting past a Fixed Servo's housing | WV → Part decisions |
| Draw order by kind; taps hit joints first | Draw groups per link; a tap hits what is drawn on top | WV → Draw layers, BM → Interactions |
| "Sensors go on a beam" | Each part names itself: "Cameras go on a beam" | BM → Interactions |
| Touch sensor on a beam; a joint holds one part | Touch sensor on a joint, as a cushion, sharing the joint with other joint parts; its own glyph | CM → Parts, WV → Part decisions |
| "Senses" for the brain's inputs | "Inputs", the machine-learning word | Screen decisions |
| The eight most active senses as bars | The inputs that fit, in port order, with a chip for the rest; the order never moves | TL → Scene decisions |
| Output dials in `accent` | In `output`, like the callout's output bars | WV → Training arena |
| The followed shadow about 43% from the left | Its centre in the middle, the ground never above 80% down | TL → Scene decisions |
| Callout above the whole creature; ground always 80% down | Callout docked at the arena top; the camera keeps a shelf for it | WV → Training arena, TL → Scene decisions |
| Ruler hanging from the ground line | Distance and height scales on the view's bottom and left edges | WV → Training arena, TL → Scene decisions |
| Targets pinned top right with a chevron | Best stays on the ground; nothing is drawn off screen | WV → Training arena |
| UI size multiplies the canvas | Pixels per unit | UI size |
| Camera rays as five cells | A Rays slider that snaps to 1, 3 or 5, so a centre ray always follows the aim | CM → Part decisions |
| Velocity motor | Motor; Velocity motor only in code and the save format | `docs/GLOSSARY.md` |
| Stepper as a Servo with a dashed dial | A detent wheel with a pawl; its own glyph | WV → Part decisions |
| Servo Max speed in °/s | In rpm, like the Motor's | `PartParameters` |
| Max speed always under Advanced | The Motor's is basic | BM → Parameters |
| Brake "Max strength 30 N·m" | "Strength", ending in Locked | `PartParameters` |
| Neon the default theme | Use phone the default | `docs/SAVE_FORMAT.md` |
| A theme card's picture has a corner circle; a line under the cards names the phone's theme | Neither: the circle read as a radio beside the check, and the screen already shows the phone's theme | — |
| `UiSidePanel`'s collapsed title at 0.08em | Label letter spacing | — |
