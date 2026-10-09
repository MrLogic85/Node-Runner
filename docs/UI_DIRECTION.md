# UI direction

How the app's UI looks, and why. `reference design/` gave the app its look,
words and flows and is still the place to start for a new screen or control,
but it is a guide, not the source of truth: where the two differ, the app and
these docs win, and the reference is never edited to match. How the world
itself is drawn (parts, selection, shadows, arena marks) is in
`docs/WORLD_VISUALS.md`. Code rules for the UI library are in
`project/src/ui/lib/AGENTS.md`.

## Who owns what

Each UI decision has one owner. A layer below never overrides the one above.
`reference design/` guides each owner but owns nothing.

| Owner | Owns | Must not |
| --- | --- | --- |
| UI library: `project/src/ui/lib`, component scenes in `project/scenes/ui`, the theme files, `UiSize`/`UiLayout`/`UiSpacing` | The design system: every colour, typography, component, state and component dimension (control heights, radius, stroke, icon and font sizes) | Know about screens or app vocabulary |
| Screen scenes: `project/scenes/screens` | Layout: which components, their order, containers, separations, margins, a slot's minimum size, and text | Restyle a component (colour, font, font-size or stylebox overrides) or change its dimensions |
| C#: view-models in `NodeRunner.App`, screen scripts in `project/src/ui/screens` | Functionality: state, rules, actions, formatting | Build a screen's static layout in code or restyle components |

Whoever authors a layout value owns it; code never re-applies a value a
scene already stores
([#331](https://github.com/MrLogic85/Node-Runner/issues/331)). What a library
component derives in code is never saved in a scene
([#309](https://github.com/MrLogic85/Node-Runner/issues/309)).

## Product feel

- **Neon simulator:** a digital petri dish for synthetic life, more
  Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls help the player connect
  cause and effect: sensors → model → moving parts → movement.
- **Low ceremony:** opening the app quickly shows something alive.
- **Experiment-first:** change one thing and see what happened.
- **Understanding over creatures:** every screen answers "why did it do
  that?". Controls that neither teach nor drive the core loop do not belong.

## Visual fidelity standard

The app's UI library and its finished screens are the visual standard. A
change matches their tokens, typography, spacing, radius, stroke widths,
glow, proportions and hierarchy, and reuses library components instead of
restyling them. A difference from `reference design/` is not a bug by
itself; inconsistency inside the app is: two screens that style the same
thing differently, a one-off colour or size, or a control that departs from
its library component.

## Tokens

Colours, opacities and typography are Theme items, one set per theme (Neon,
Paper), which differ only in colour, soft-fill alpha and glow. Neon is one
skin: layout, state and names never depend on it. Dimensions are constants
every theme shares (`project/src/ui/lib/AGENTS.md` → "Theme and tokens").

### Reference token mapping deviations

Every reference token has a named Godot mapping or a reason here, so a later
token import does not bring back a dead one.

- **`detail`** marks a passive detail of a part: the Servo's Fixed-link band
  and picker icon.
- **`*-glow` colours** are not imported. CSS needs them only because it
  cannot derive an alpha variant; `UiGlow` derives glow from the base colour,
  and Paper turns glow off with `effects_enabled`.
- **`accent-soft`** is not a colour: it is any base colour at the theme's
  `alpha_soft` (Neon 31, Paper 26, out of 255). `alpha_shadow` (82, 0.32 in
  both themes) is the opacity of every Training shadow but the followed one
  (#385).
- **`shadow.glow`** is not imported. A CSS blur radius has no 1:1
  `StyleBoxFlat` equivalent, so `UiGlow` owns the Android-reviewed values
  (extent 10, opacity 12%) for every glowing surface.
- **Dimension tokens** are code constants (see Tokens).

## Screen size and safe area

The canvas grows as `reference design/README.md` → "Screen size" describes,
and parts keep their size. A screen narrower than 16:9 keeps 640 units of
width and gains height. 640 × 360 is the smallest canvas the layouts fit,
not a fixed size. The Creations card row keeps its 16:9 height (#520).

The app runs immersive in both landscape orientations (#513). Godot asks
Android for `userLandscape`, which keeps to the rotation lock. Only the
camera cutout is avoided: the card holding the content is inset, and the
background still reaches the screen edge. Content that needs more room is
clipped inside the frame and toolbar (#737).

## UI size

The UI size (#299, #738) is how many device pixels one canvas unit is, as a
percentage: 100% is one pixel per unit. It scales everything around the
arena and the Build canvas uniformly, touch targets included, with no
floor; the world views keep their size and get the space left.

- **Auto**, the default, is one unit per Android dp, snapped to 5%, because
  the reference is drawn in CSS pixels, which are about dp. A tablet gets
  more canvas, not a bigger UI.
- **Max** is the largest size, floored to 5%, at which a 640 × 360 canvas
  fits the safe area.
- **Min** is 50%.

On an S25 Auto is 280% and Max 300%. Auto and Max follow the window; a
fixed size is limited to the range. Colors & Styles sets the UI size for the
session (Settings: #201).

## Touch and press feedback

- **Tap only.** Nothing is press-and-hold (#866); where care is needed, a
  dialog names the action.
- **No hover look** (`project/src/ui/lib/AGENTS.md` → "Input and press
  feedback").
- **No touch margins** (#275), unlike the reference's 40-in-48 targets.
  Buttons are 40 high in a row, 32 compact and 48 × 48 stacked; toolbar
  icon actions are stacked (#964). A segment
  is at least 48 wide and 40 high.
- **Press tint** (#286, #325): a held control shows one flat `accent` tint
  at `alpha_soft` inside its corners, with no ripple or animation. Sliding
  off or scrolling cancels it. Destructive controls tint `danger`, Primary
  ones on-accent (#964). Selected and disabled controls, and controls whose
  press already changes something (tabs, segments, sliders, part rows, the
  Brain stage card, BrainFocus rows), show none.

## Components

The Component Gallery shows every component; these are the decisions.

### Reference component mapping

`UiComponentContracts.ReferenceEntryFor` maps each component to its `c_*`
entry (#315). The entries that are not a component of their own (#306):

- **`c_round_button`** is `UiSelectionHandle`, which `UiInfoRow` also
  draws; tool help rows show only a muted glyph (#706).
- **`c_rows`** is a plain container with a `space-1` gap and an optional
  danger Delete (#343).
- **`c_expand`** is `UiExpandSection`, which folds rows such as Advanced
  (#928): a `control-sm` `Overline` `muted` header with a chevron, no border
  or fill.
- **`c_hint`** is `UiHintCard`, the slider hint at the canvas's top right
  (#867).
- **`c_bar_cell`** is `UiBarCell`, a cell of the shadow strip (#791).
- **`c_status`** is Build's readiness row, a `UiIcon` and a `UiLabel` in the
  side panel's scene.
- **`c_prog`, `c_meter`** are a `UiSlider` with two Rounded ends.
- **`c_power`** and read-only facts are `UiValueRow`.
- **`c_card_actions`** is `UiCardActions`, a bar of text cells.
- **`c_textfield`** in a bar fills the toolbar width rather than sizing to
  its text (#485).
- **`c_call` in a figure** is `UiCalloutLayer` (#593); the look is in
  `docs/WORLD_VISUALS.md` → "Build canvas".
- **`c_fact_row`** is an authored row of a `UiIcon` and two `UiLabel`s,
  as on Import (#899).
- **`c_field_line`** is `UiTextField`'s error line; its muted fact line
  comes with #1061.
- **`c_panel_head`** is dropped; a side panel's header is `UiSidePanel`'s
  own row.

### Buttons and icons

- **Kinds:** Primary, Secondary (default), Tertiary (destructive) and Flat.
- **Unavailable** (#841): dashed and dimmed like disabled, but tappable, so
  the screen can say why: a later version, what blocks the action, as
  danger notes on the parts that block it (#844, #937), also when it is
  the lock that makes them block it (#990), or a locked Creation, as a
  notification (#896); a locked Delete shows both (#987).
  It has no lock glyph, which would read as the creation padlock.
- **Icon sizes** (#358, #422): 16 beside text, 20 on a textless or stacked
  button and in a `touch` row, 16 in a `control-sm` row or inside a ring,
  12 on a chip (16 for a part glyph, never 12). Only the canvas Move handle
  is filled `accent-soft`.
- **Icon files** (#1029): one folder, `project/assets/icons/`, each file
  named `<kind>_<name>.svg` in snake_case, where the kind is `icon` for a
  UI icon, `part` for a part glyph and `mark` for a mark. Kinds are named,
  not nested, so a file says what it is. `icons/app/` holds the launcher
  art, not icons.
- **Icon drawing** (#1010): every UI icon is drawn 16 on its longer side,
  centred on a plain 24 viewBox, so icons side by side match in size and
  air. Part glyphs keep their 20 grid. A mark echoes the launcher glyph
  and is the one icon drawn 20 on the 24 grid, larger than its
  neighbours: at the shared stroke a network drawn 16 fills into lumps
  (#1018). `icon_model` keeps the 16 rule by stopping its lines short of
  the middle column. `UiIconGridTests` reads each file's kind from its
  prefix and enforces its grid.
- **Toggles and checkboxes:** transparent rows with solid indicator
  outlines; a disabled row is 50% opacity, not dashed.
- **Segmented switch:** the selected segment has an `accent-soft` fill and a
  2 px outline; its icon does not change (#247).

### Popups

Dialogs (centred) and notifications (bottom centre, one at a time) share
`UiPopupCard`; border and glow take the type colour: `accent`, `halo`,
`danger` (#355). Only Warning and Danger show an overline, unlike the
reference (#692). Actions are text cells in a `UiCardActions` bar flush with
the frame (#353): Default uses Primary, Danger uses Tertiary, Cancel is
Secondary, and Warning actions use Flat (#964). A dialog covers
notifications; a notification waits until it closes. A notification with an
`Id` is not queued again while that Id shows or waits; a repeat restarts the
showing one's time. A `Replaceable` notification gives way to any newer one,
closing at once or leaving the queue: tap answers such as Build's lock and
"comes in version" notes use both (#1004). A failed copy shows the same
Danger notification whether it started in Creations, Examples or Build,
built once in `CreationActions.TryCopy` (#1013); a failed import shows its
own, built beside it in `CreationActions.TryImport` (#899). A failed user
action uses a Danger notification titled "<Action> failed", with the
action's icon where one exists: "Create failed" with Plus, "Start failed"
with Play (#1015). For a specific action, prefer "Could not <verb>
<object>. Try again." Generic UI-boundary failures may say "The action
could not be completed. Please try again.", and save failures may say what
stayed unsaved. Route and state-validation notices, such as a missing
creation or a training prerequisite, are not action failures and stay
Default; dialog-inline errors and log-only failures are not notifications.
A copy to the
clipboard confirms through `UiClipboard.Copy`, which waits for Android's
own toast.

### Menus and pickers

Menus have no row hairlines, unlike the reference (#696); spacing and the
press tint separate rows. A menu has one density, Standard (48) or Compact
(32), and clips its square highlights to its rounded surface. A divider is a
`line` hairline. A `UiPicker` with nothing picked shows its prompt in
`danger`.

### Shell: toolbar, button bar, side panel

The shell is filled `panel` over the frame card's `bg` (#347).

- **`UiToolbar`** (#319): 48 high. Content that does not fit scrolls
  sideways. Creations has no Back and insets its title 48 to match.
- **`UiButtonBar`** (#320): 56 wide, with an `edge` divider, not the
  reference's `line`. Locked tools keep the 20 px icon.
- **`UiSidePanel`** (#321): 176 wide, its header an optional icon and title
  and a bare 16 px `muted` chevron. Content scrolls under the header. The
  chevron collapses it in 200 ms to a 28 px tab with the title on its side,
  in Label letter spacing rather than the reference's 0.08em.

### Cards, sliders, value rows, tray tabs

- **Cards:** content flush with a card's edge rounds the corners it shares
  with it. Card action text is `accent` for Primary, `danger` for Tertiary
  and `ink` otherwise.
- **Sliders:** each end is Rounded, Thumb or Marker. Where selected parts
  differ the readout shows `low–high` between Marker ends, with no thumb
  (#704). Uneven stops sit evenly along the track (#801). `TouchStarted`
  and `TouchEnded` bracket a finger on a slider: it ends when the finger
  lifts, even after a scroll took over, or when the slider is disabled or
  hidden.
- **Hint card** (`UiHintCard`, #867): a `UiCard` that ignores input, fades
  in over 0.12 s, and after `HideAfterLinger` stays `LingerSeconds` (2 s)
  then fades out over 0.2 s. The scene that uses it authors its children.
- **Value rows:** label left, readout right, no padding.
- **Tray tabs** (#330): glyph tabs sharing the strip's width, 4 px apart, 32
  high, selected with `accent-soft`. **Tray rows** are `control-sm`
  `UiPartRow`s with 20 px glyphs; a locked row shows only its lock and fades
  whole (#374, #964). A row is a native button: a tap that does not turn
  into a drag picks it (#805). The picked row has an `accent-soft` fill, an
  `accent` signal border and an `accent` glyph, and a `UiPickList` fades its
  info line in right under it over 0.12 s, in the Parts tray and the Links
  list alike. A locked row answers a tap with a notification
  titled after the screen, with the padlock icon (#896, #992); its text
  is Build's (`docs/BUILD_MODE.md` → Locked Build).

## Screens

What each screen does is in `docs/BUILD_MODE.md` and
`docs/TRAINING_LOOP.md`. This is how they look.

### Creations

A card shows its creature, "12 generations", and the latest result as one
stat per row with its unit, not the reference's bare numbers in one row
(#479, #846). The `distance` icon is `|->` and `elevation` lies on its side.
The trophy is Unavailable (#199).

### Import

Import creation in Creations' overflow menu (`icon_paste`) opens it (#899).
Its toolbar holds Back, the title, Secondary Paste and Primary Add to
Creations, enabled only while a build shows and its name is not blank.
There is no field for the code: Paste reads the clipboard only when
tapped. The left card says what to do, shows the build, or shows why the
code was refused under a danger `icon_warn`. The build draws on Build's
grid, as large as Build at 1:1, never larger: what it previews is what
Build opens with (cards stop at half and show no grid). Beside it, a
column at least 176 px wide holds a Name field with the build's name and
one authored row per kind of part it has (icon, name in `Body`, count in
`ReadoutSmall`, no border: they are facts, not buttons). Add saves it
under that name and opens it in Build in place of Import, like an
example's Copy. Build's Share build uses `icon_share`, three linked nodes,
and confirms with a "Code copied" notification.

### Build

- **Rail:** Joint, Links, Parts, Select, and the Primary play button at
  the bottom in every state (#370, #706). Parts uses the project's own
  `icon_parts.svg`: three tiles and a lifted diamond.
- **Top bar:** the name field, padlock, Undo and Redo as Secondary icon
  buttons (#689), and the overflow, where Stats (#198) and Power budget say
  "Coming soon".
- **Side panel:** the tool's panel or the selection's settings
  (`docs/BUILD_MODE.md`); its last line says why play is dimmed.
- **Setting hint** (#867): a finger on a Part settings slider shows a
  240 px `Frame` hint card at the canvas's top right, `space-3` in: the
  setting's name in `Overline` `accent` over one `Body` `ink` line, no icon.

### Train setup

Shadows and Run length are `UiSlider`s; in Simulate both are empty dashed
tracks, off their scale. A creature with no powered part gets a `halo` warn
row at the top. The note under Shadows is `muted`, or the warn icon in
`halo` or `danger` (#318). Locked maps are disabled cards with a lock, not
the library's `Locked` card ("the only choice"); each card shows a
`MapPreview`, `accent` on the map in use. There are no profiles (#194).

### Training

The signal flow is a collapsible `UiSidePanel` titled "Status" (#813). The
"Generation 37" caption ends in a bare `UiSlider` filling as the run goes by
(#715). Only the Brain stage looks like a button, Raised with glow; the
other stages are plain frames without glow (#847). Only the followed
shadow's strip cell is marked (`accent`). Stats is Unavailable (#198).

### BrainFocus

BrainFocus shows the direct brain, inputs straight to outputs (#536; hidden
layers: #549).

- "SENSES" and "OUTPUTS" (#660) sit over the label columns, flush with
  their inner edge; the sheet's margins are 12 (bottom 8).
- Every row keeps its `Caption` label (#908), at least one line apart;
  rows scroll under the pinned headings.
- Only the tapped neuron gets the `halo` ring; its strongest partners get
  an `ink` label and full-strength links, and a sentence names them.
- With no powered part the no-power warning replaces the hint and legend.

## App icon

The launcher icon is the Brain glyph (the 2-3-2 network) in Neon colours
(#820). Inputs are `accent`, connections fade through `line_strong` to
`output`, hidden neurons are `ink`, and the centre neuron wears the `halo`
focus ring. Neurons sit on `panel_raised` discs over a vignette to
`background` with a faint `muted` dot grid. Glow is the base colour at low
alpha. Neurons stay inside the 66 dp safe circle. The monochrome layer is a
flat white silhouette. The Android 12+ launch splash is the foreground
alone on `background`, the colour of the window behind the first frame, so
launch stays one dark field (#829). `docs/RELEASING.md` → "App icon" lists
the files.

## Departures from the reference

Where the app differs from `reference design/` and someone might follow the
guide back by mistake. Departures stated in this doc's own sections are not
repeated. The last column owns the detail (BM = `docs/BUILD_MODE.md`, TL =
`docs/TRAINING_LOOP.md`, WV = `docs/WORLD_VISUALS.md`).

Import's new inputs in the reference are planned, not departures (#1061).

| Reference | Ours | Owner |
| --- | --- | --- |
| Start training in the top bar | Play on the rail in both states (#370) | Screens → Build |
| Rail says Beam and Move; Links tab | Links and Parts tools (#705, #706, #913) | BM → Interactions |
| Part counts ("1 left") | Unlimited (#374, #525) | BM → Parts tray |
| Core part | Sensors on beams (#127) | WV → Sensors |
| "Sensors go on a beam" | Each sensor names itself: "Cameras go on a beam" (#1053) | BM → Sensors |
| Brain setup screen; hidden neurons | Removed; the direct brain (#536) | Screens → BrainFocus |
| Speed control | None (#787) | TL → No speed-up |
| Targets pinned top right with a chevron; achievement targets | Best stays on the ground, nothing off screen, until #488 | WV → Training arena |
| UI size multiplies the canvas | Pixels per unit (#299, #738) | UI size |
