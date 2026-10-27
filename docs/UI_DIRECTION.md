# UI direction

How the app's UI looks, and why. `reference design/` gave the app its look,
words and flows and is still the place to start for a new screen or
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
640 units of width and gains height; 640 × 360 is the smallest canvas the
layouts must fit, not a fixed size. The Creations card row keeps its 16:9
height.

The app runs immersive in both landscape orientations, keeping to the
rotation lock (`userLandscape`). Only the camera cutout is avoided: the
card holding the content is inset, and the background still reaches the
screen edge. Content that needs more room is clipped inside the frame, not
pushed off screen.

## UI size

The UI size is how many device pixels one canvas unit is, as a
percentage. It scales everything around the arena and the Build canvas
uniformly, touch targets included; the world views keep their size and
get the space left.

- **Auto**, the default, is one unit per Android dp, because the reference
  is drawn in CSS pixels, which are about dp. A tablet gets more canvas,
  not a bigger UI.
- **Max** is the largest size at which a 640 × 360 canvas fits the safe
  area. **Min** is 50%.

Auto and Max follow the window; a fixed size is limited to the range.
Until Settings has its UI size row (#1275), Colors & Styles sets the UI
size for the session.

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

The Component Gallery shows every component; these are the decisions.

### Reference component mapping

`UiComponentContracts.ReferenceEntryFor` maps each component to its `c_*`
entry. The entries with no component of their own
(`ReferenceEntriesWithoutComponent`):

- **`c_inspector`** is Build's Part settings panel, authored in its scene.
  **`c_panel_head`** is `UiSidePanel`'s own header, and **`c_rows`** a
  plain container with a `space-1` gap and an optional danger Delete.
- **`c_status`** is Build's readiness chip, a `UiChip` (Screens → Build).
- **`c_fact_row`** is an authored row of a `UiIcon` and two `UiLabel`s,
  as on Import. The trailing count's `BaselineOf` names the label before
  it, so the two stand on one baseline in their different fonts.
- **`c_field_line`** is `UiTextField`'s one line: its error, or else its
  muted fact.

### Buttons and icons

- **Kinds:** Primary, Secondary (default), Tertiary (destructive) and Flat.
- **Unavailable:** dashed and dimmed like disabled, but tappable, so the
  screen can say why: a later version, a locked Creation, or danger notes
  on the parts that block the action. It has no lock glyph, which would
  read as the creation padlock.
- **Icon sizes:** 16 beside text, 20 on a textless or stacked button and
  in a `touch` row, 16 in a `control-sm` row or inside a ring, 12 on a
  chip (16 for a part glyph, never 12). `ContentLayout` picks it.
- **Icon files:** one folder, `project/assets/icons/`, each file named
  `<kind>_<name>.svg`, where the kind is `icon` (UI icon), `part` (part
  glyph) or `mark`. Kinds are named, not nested, so a file says what it
  is. `icons/app/` holds the launcher art, not icons.
- **Icon drawing:** every UI icon is drawn 16 on its longer side, centred
  on a plain 24 viewBox, so icons side by side match in size and air. Part
  glyphs keep their 20 grid. A mark echoes the launcher glyph and is drawn
  20 on the 24 grid, since at the shared stroke a network drawn 16 fills
  into lumps. `UiIconGridTests` enforces each kind's grid.
- **Toggles and checkboxes:** transparent rows with solid indicator
  outlines; a disabled row is 50% opacity, not dashed.
- **Segmented switch:** the selected segment has an `accent-soft` fill and
  an outline; its icon does not change.

### Popups

Dialogs (centred) and notifications (bottom centre, one at a time) share
`UiPopupCard`, whose border and glow take the type colour. Only Warning
and Danger show an overline. Actions are text cells in a `UiCardActions`
bar flush with the frame; the confirm action is Primary, Tertiary on a
Danger popup and Flat on a Warning one. A dialog covers notifications.
Notification `Id` and `Replaceable` keep repeated tap answers, such as
Build's lock note, from piling up.

- A failed user action is a Danger notification titled "<Action> failed",
  with the action's icon where one exists ("Create failed" with Plus).
  Its text prefers "Could not <verb> <object>. Try again."; save failures
  may say what stayed unsaved. One action fails with the same
  notification wherever it starts.
- Route and state notices, such as a missing creation or a training
  prerequisite, are not failures and stay Default. Dialog-inline errors
  and log-only failures are not notifications.
- A copy to the clipboard confirms at once (`UiClipboard.Copy`), beside
  Android's own toast.

### Menus and pickers

- Menus have no row hairlines, unlike the reference; spacing and the press
  tint separate rows. A menu has one density, Standard or Compact.
- A `UiPicker` fills its parent, ends its value in an ellipsis, and opens
  its list as wide as the field, always below its row, never over its own
  heading. A long list scrolls with the side panel. Other menus, such as
  the overflow, open upward when there is no room below.
- A picker with nothing picked shows its prompt in `danger`.
- In a hinge part's side pickers only the current Fixed part (`detail`)
  and Target part (`accent`) are tinted, in both lists, to match the
  canvas bands; the reference tints every option. Other options keep
  `line-strong`, since `muted` would make them look disabled.

### Shell and panels

- The shell is filled `panel` over the frame card's `bg`. A toolbar's
  content scrolls sideways when it does not fit, and a toolbar text field
  fills the toolbar's width rather than sizing to its text.
- `UiButtonBar` takes an `edge` divider, not the reference's `line`.
- `UiSidePanel` collapses to a narrow tab with its title on its side, in
  Label letter spacing rather than the reference's 0.08em.
- **Cards:** content flush with a card's edge rounds the corners it shares
  with it.
- **Meters:** `UiMeterBar` also stands upright, 8 × 24 and fully rounded,
  for the Status panel's Inputs bars, and `UiDial`, a 30 × 24 half dial
  from the SignalFlow reference, shows its Outputs. `UiTrackBar`, a
  6-tall strip with a marker and an optional cap, shows how far the
  Distance card has got; like the callout's meter none has a `c_*`
  entry.
- **Sliders:** where selected parts differ, the readout shows `low–high`
  between Marker ends, with no thumb. Uneven stops sit evenly along the
  track. A disabled slider is dashed; one the screen can explain answers a
  tap like an Unavailable button.
- **Tray rows and tabs:** glyph tabs share the strip's width, selected
  with `accent-soft`; Part settings' "On this joint" strip reuses them. A
  locked row shows only its lock and fades whole. The picked row has an
  `accent-soft` fill, an `accent` border and glyph, and its info line
  fades in right under it, in the Parts tray and the Links list alike.

## Screens

What each screen does is in `docs/BUILD_MODE.md` and
`docs/TRAINING_LOOP.md`. This is how they look.

### Creations

A card shows its creature, its generations and the latest result as one
stat per row with its unit, not the reference's bare numbers in one row.
The trophy is Unavailable until achievements exist (#199).

### Import

Import previews a pasted build before it is added. There is no field for
the code: Paste reads the clipboard only when tapped. Paste is the Primary
until a build shows, then Add to Creations. The preview draws on the Build
area's half-metre grid at Build's true size, never larger, so it shows what Build opens
with. Beside it, the parts are fact rows, not buttons. The reference's
"You already have a {0}." fits no article to every name or language, so
the app says "You already have one by this name.".

### Settings

Settings is the first item in the Creations overflow. Its rows sit in one
centred column, `w-form` wide. Each change applies and saves at once,
with no Save button, so the screen itself is the preview. The Theme row is
three `UiThemeCard`s, each a `UiThemePreview` drawn in that card's own
palette over its name; Use phone's picture is split Neon | Paper, so it
does not read as a fourth theme. The Grid default row, the grid a new
Creation starts with, is a `UiSegmentedSwitch` of the sizes, with Snap
to grid as a `UiToggleRow` below it.

### Build

- **Rail:** the tools and the Primary play button at the bottom in every
  state.
- **Top bar:** the name field, padlock, Undo, Redo and the overflow.
- **Part copy:** a part's description (`PartInfo`) is one short sentence
  saying what the part does, not how it is wired or what else it needs. A
  placement hint is at most one short sentence. Neither names other parts.
- **Readiness:** a chip at the canvas's bottom right says whether the
  creature can train: `accent` with a check when ready, `danger` with a
  warning and a short reason when blocked, and neutral with the Joint icon
  on an empty canvas, which is a start rather than a fault. It is one
  line, so every reason has a short form. It takes no taps, and it is not
  a callout, so it never reads as a note whose part is missing.
- **Setting hint:** a finger on a slider shows a hint card at the canvas's
  top right: the setting's name in `Overline` `accent` over one `Body`
  line, no icon.

### Train setup

Shadows and Run length are `UiSlider`s. A creature with no powered part
gets a `halo` warn row at the top. Locked maps are disabled cards with a
lock, not the library's `Locked` card ("the only choice"); each card shows
a `MapPreview`, `accent` on the map in use. There are no profiles.

### Training

The signal flow is a collapsible `UiSidePanel` titled "Status". Only the
Network stage looks like a button, raised with glow; the other stages are
plain frames. A stage's empty-state line sits in its body in Body
`muted`, not Caption like the header note, so a sentence stays readable
on a phone. Only the followed shadow's strip cell is marked (`accent`).
Stats is Unavailable (#198).

### NetworkFocus

NetworkFocus shows the direct network, inputs straight to outputs (hidden
layers: #549). The sheet is titled "Network", after the stage that opens
it; NetworkFocus is the component's name, not player copy.

- "INPUTS" and "OUTPUTS" head the label columns; rows scroll under them.
- Every row keeps its label, at least one line apart.
- Only the tapped neuron gets the `halo` ring; its strongest partners get
  an `ink` label and full-strength links, and a sentence names them.
- With no powered part the no-power warning replaces the hint and legend.

## App icon

The launcher icon is the Network glyph (the 2-3-2 network) in Neon
colours. Inputs are `accent`, connections fade through `line_strong` to
`output`, hidden neurons are `ink`, and the centre neuron wears the `halo`
focus ring. Neurons sit on `panel_raised` discs over a vignette to
`background` with a faint `muted` dot grid. Glow is the base colour at low
alpha. Neurons stay inside the 66 dp safe circle. The monochrome layer is
a flat white silhouette. The Android 12+ launch splash is the foreground
alone on `background`, the colour of the window behind the first frame,
so launch stays one dark field. `docs/RELEASING.md` → "App icon" lists the
files.

## Departures from the reference

Where the app differs from `reference design/` and someone might follow
the guide back by mistake. Departures stated in this doc's own sections
are not repeated. The last column owns the detail (BM =
`docs/BUILD_MODE.md`, TL = `docs/TRAINING_LOOP.md`, WV =
`docs/WORLD_VISUALS.md`).

| Reference | Ours | Owner |
| --- | --- | --- |
| Start training in the top bar | Play on the rail in both states | Screens → Build |
| Rail says Beam and Move; Links tab | Links and Parts tools | BM → Interactions |
| Part counts ("1 left") | Unlimited | BM → Parts tray |
| Core part | Sensors on beams | WV → Sensors |
| Piston: one cylinder as long as its travel; Spring: one damper body | A three-part telescope, starting past a Fixed Servo's housing | WV → Piston, Spring |
| Draw order by kind; taps hit joints first | Draw groups per link; a tap hits what is drawn on top | WV → Draw layers, BM → Interactions |
| "Sensors go on a beam" | Each part names itself: "Cameras go on a beam" | BM → Interactions |
| Touch sensor on a beam; a joint holds one part | Touch sensor on a joint, as a cushion, sharing the joint with other joint parts; its own glyph | BM → Interactions, WV → Touch sensor |
| "Senses" for the brain's inputs | "Inputs", the machine-learning word | Screens → NetworkFocus, WV → Port bars |
| The eight most active senses as bars | The inputs that fit, in port order, with a chip for the rest; the order never moves | TL → The Training scene |
| Output dials in `accent` | In `output`, like the callout's output bars | WV → Port bars |
| Brain setup screen; hidden neurons | Removed; the direct network | Screens → NetworkFocus |
| Speed control | None | TL → No speed-up |
| The followed shadow about 43% from the left | Its centre in the middle, the ground never above 80% down | TL → Camera |
| Callout above the whole creature; ground always 80% down | Callout docked at the arena top; the camera keeps a shelf for it | WV → Training arena, TL → Camera |
| Ruler hanging from the ground line | Distance and height scales on the view's bottom and left edges | WV → Training arena, TL → Scales |
| Targets pinned top right with a chevron; achievement targets | Best stays on the ground, nothing off screen, until #488 | WV → Training arena |
| UI size multiplies the canvas | Pixels per unit | UI size |
| Camera rays as five cells | A Rays slider that snaps to 1, 3 or 5, so a centre ray always follows the aim | `docs/CREATURE_MODEL.md` → Camera |
| Velocity motor | Motor; Velocity motor only in code and the save format | `docs/GLOSSARY.md` |
| Stepper as a Servo with a dashed dial | A detent wheel with a pawl; its own glyph | WV → Stepper |
| Servo Max speed in °/s | In rpm, like the Motor's | `docs/CREATURE_MODEL.md` → Servo |
| Max speed always under Advanced | The Motor's is basic | BM → Parameters |
| Brake "Max strength 30 N·m" | "Strength", ending in Locked | `PartParameters` |
| Brake "Power: None: passive" | No Power row until the Power budget (#460) | — |
| Settings rows UI size, Sounds and Theme; Neon the default | Theme only until #743, #1275 and #1440; Use phone the default | Screens → Settings, `docs/SAVE_FORMAT.md` |
| A theme card's picture has a corner circle; a line under the cards names the phone's theme | Neither: the circle read as a radio beside the check, and the screen already shows the phone's theme | Screens → Settings |
