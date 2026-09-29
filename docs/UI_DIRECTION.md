# UI direction

Node Runner's UI source of truth is the tracked package in
`reference design/`. Start at its `index.html`, then read the relevant
component README and preview. `tokens.json` owns visual values, `library.md`
owns reusable controls, and the component READMEs own screens and interaction
flows. Do not copy those contracts into `docs/`.

This document contains only repository-specific direction that the design
package does not own. `docs/UI_IMPLEMENTATION_PLAN.md` owns delivery order and
GitHub dependencies.

## Who owns what

Each UI decision has one owner. A layer below never overrides the one above.

| Owner | Owns | Must not |
| --- | --- | --- |
| `reference design/` | The design: tokens, components, screens, flows | — (never edited here; record ambiguities instead) |
| UI library: `project/src/ui/lib`, component scenes in `project/scenes/ui`, the theme files, `UiSize`/`UiLayout`/`UiSpacing` | The Godot interpretation of the design: every colour, typography, component, state and component dimension (control heights, radius, stroke, icon and font sizes) | Know about screens or app vocabulary |
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
`project/scenes/widgets`. Most product screens predate this split and still
build themselves in code; they move to it as they are rewritten
([#310](https://github.com/MrLogic85/Node-Runner/issues/310)), starting with
Creations ([#314](https://github.com/MrLogic85/Node-Runner/issues/314)).
`docs/TEST_STRATEGY.md` lists the guard for each boundary.

## Product feel

- **Neon simulator:** the app feels like a digital petri dish for synthetic
  life — more Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls should help the user connect
  cause and effect: sensors → model → motor relations → movement.
- **Low ceremony:** opening the app should quickly show something alive on
  screen.
- **Experiment-first:** the user should be able to change one thing and see
  what happened.
- **Understanding over creatures:** every screen must answer "why did it do
  that?". Controls that neither teach nor drive the core loop do not belong.

## Visual fidelity standard

`reference design/` is visually binding on tokens, layout, typography/text
styles, spacing, radius, stroke widths, dividers, glow, component proportions,
and hierarchy. "Not pixel-perfect" only means Godot does not have to reproduce
HTML/CSS rendering artifacts exactly across fonts, rasterization, and device
scaling. It does **not** mean loose inspiration: visible deviations from the
reference must be deliberate, documented in the PR/issue, or fixed before the
milestone is considered done. When the package is contradictory or incomplete,
record the ambiguity instead of editing it or choosing silently.

### Reference token mapping deviations

Gate A requires every canonical token to have either an exact named Godot
mapping or a documented non-runtime reason. The reasons live here, so a later
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
  Any base colour can use the same alpha. Tests reject alpha-only colour
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
  nothing is lost: UI scaling (issue
  [#299](https://github.com/MrLogic85/Node-Runner/issues/299)) works on the
  rendered viewport, not on token values. If a future theme ever needs its own
  dimensions (a compact or dense mode), that token moves back into the Theme.
- **Scene-authored layout widths** have no C# constant
  ([#300](https://github.com/MrLogic85/Node-Runner/issues/300)). A scene
  owns its layout and cannot read a C# constant, so a constant only copies the
  value. `UiLayout` keeps only the values C# reads. Use these reference values
  in the scene directly: `w-dialog` 300 (`UiDialogContent.tscn`), `w-card` 326
  (`UiNotificationContent.tscn`), `w-sheet` 720, `w-sheet-wide` 880,
  `w-brain` 460, `w-well` 250, `h-well` 64, `w-tile` 156, `h-stage` 170,
  `h-thumb` 100, `w-col-xs` 40 and `w-col-xl` 128. `h-screen` (312) needs no
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
  `UiSelectionHandle.DrawRoundButton`).
- **`c_rows`** has no component of its own. It is the body of a part's
  settings panel: a plain container whose separation is the panel's
  `space-1` gap, holding rows that carry no outer padding of their own, and an
  optional full-width danger Delete at the end with `space-2` above it
  ([#343](https://github.com/MrLogic85/Node-Runner/issues/343)).
- **`c_prog` and `c_meter`** are `UiSlider` with `ValueKind` Progress (no
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
  look of their own use the same API. The pressed look of a cell waits for
  [#286](https://github.com/MrLogic85/Node-Runner/issues/286).
- **`c_panel_head`** is dropped by human decision: it is not part of the future
  design exports, so there is no panel header component. A side panel's
  header, including the inspector's (`c_inspector`), is `UiSidePanel`'s own
  header row; its icon actions are decided when the Build inspector is
  migrated.

## Immediate-mode drawing and antialiasing

The project renders at a low logical canvas (`window/size/viewport_width=640`,
`viewport_height=360`) and relies on `window/stretch/mode="canvas_items"` to
scale everything up to the device's physical resolution (e.g. 3x on a
1920x1080 display). Any `_Draw()` override that calls an immediate-mode
`CanvasItem` primitive (`DrawArc`, `DrawLine`, `DrawPolyline`,
`DrawDashedLine`, `DrawRect`, `DrawCircle`, ...) with `antialiased: true`
records its AA "feather" as extra geometry sized in local/object-space at
draw time. That feather is not recomputed against the canvas stretch
transform — it just scales up along with the rest of the shape. At 3x
stretch, a feather meant to be ~1 physical pixel becomes ~3 physical pixels,
producing a visibly soft/blurry halo around rings, dashed borders, and other
hand-drawn strokes (first noticed on `UiNumber`'s ring and `UiCard`'s
disabled dashed border).

`StyleBoxFlat`-based rendering (used for `UiCard`'s normal/rounded borders)
does not have this problem — it uses a separate, scale-aware rendering path.
Setting `textures/canvas_textures/default_texture_filter` to `Nearest` (done
in `project.godot`) fixes texture-sampling blur (fonts, sprites) but does
**not** fix this, since the AA feather is extra vector geometry, not a
texture-filtering artifact; it was empirically confirmed to still be blurry
under `Nearest` filtering. `anti_aliasing/quality/msaa_2d` is also enabled in
`project.godot`, but has no effect at all under the project's
Compatibility/GLES3 renderer (Godot logs `2D MSAA is not yet supported for
GLES3`); see the Compatibility/OpenGL renderer note in
`docs/ARCHITECTURE.md`.

**Rule: all immediate-mode `_Draw()` calls in this project must pass
`antialiased: false`.** This has been applied across every existing call
site (`UiNumber`, `UiDashedBorder`, `UiProgressRing`, `UiSlider`,
`UiSelectionHandle`, `UiButton`, `UiBoundsDebugOverlay`,
`BuildScreen`, `SimulateScreen`, `BrainFocusNetworkView`,
`ConstructionCanvas`, `CreatureThumbnail`, `BeamVisual`). Any new `_Draw()` code
must follow the same rule; a stray edge without antialiasing reads as a
sharp 1px line at any stretch factor, while `antialiased: true` reads as a
blurry, stretch-factor-wide halo.

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
native theme migration. User-selectable UI-only scaling is separate work tracked in
[issue #299](https://github.com/MrLogic85/Node-Runner/issues/299). Do not assume
`Window.content_scale_factor` is UI-only; the scaling issue owns verifying a
mechanism that leaves the simulation at its existing scale and applies the UI
factor only once.

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
translation key); an internal UiLabel renders it with the `Label` (row) or
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
`Text` and the `IconId` dropdown (`None` means no icon). Resource edits update
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
of null. Any locally authored, unsaved old `Compact` setting must be replaced
by selecting `RowCompact`; reopen scenes after rebuilding to refresh Inspector.
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
- **List rows.** A list row's leading icon is `icon-lg` (20): the Standard
  menu row and the part row. The picker's option list is a compact menu row
  and keeps the 16px accessory the reference draws there.
- **Icons inside a ring.** An icon inside a ring is `icon` (16). The selection
  handle and the info row that shows it both draw the same round button
  (`UiSelectionHandle.DrawRoundButton`).
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
A page starts with the gallery's theme and debug bounds and hands them back when
it closes. A page opened from Component Gallery's menu opens on top of it, so
Back returns there. A page opened on its own (`ui/popup_gallery`) hands over
to Component Gallery, keeping theme and debug bounds, both on Back and when
its menu opens another page. A page with a Back action also takes Android Back and Escape, after an
open menu or dialog has handled them; a page without one, such as a standalone
Component Gallery, leaves Android Back to its default and quits. The Toolbars page mirrors the reference's ComponentToolbars page. Its
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
`ui/popup_gallery=true` starts it directly for a development export.
When hosted beneath `Main`'s `Node2D`, the gallery explicitly follows the
viewport size; when hosted beneath a `Control`, it fills its parent via anchors.
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

Component Gallery is migrating one component at a time. Open
`project/scenes/screens/ComponentGalleryScreen.tscn` and expand
`UiFrame/MarginContainer/Card/Shell/Scroll/ContentFrame/Content` to edit the
authored component sections. They are the actual runtime controls, not
editor-only copies; components still missing a section are tracked in
[issue #306](https://github.com/MrLogic85/Node-Runner/issues/306). The scene may group and rename
those sections freely; tests should cover component behavior, not lock the
gallery's visual arrangement.
Edit normal exported properties, UiLabel Text Style presets and native
container separations; theme switching does not recreate or reset those
choices.
Keep the unique binding names: `UiFrame`, `Toolbar`, `ThemeSwitcher` and the
`ToolbarMenuDebugBounds`, `ToolbarMenuComponents`, `ToolbarMenuToolbars`,
`ToolbarMenuColorsAndStyles` and `ToolbarMenuPopupGallery` menu items on every
page that carries the gallery toolbar, plus `Scroll`, `ContentFrame` and
`RuntimeSections` in the Component Gallery. The toolbar and authored component
sections are scene-owned; the remaining component sections are still built at
runtime in `RuntimeSections`, so they appear with F6 but are deliberately not
yet authored in the editor. Do not copy generated component internals into the
scene.

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

Colors & Styles authors only its frame, toolbar, menu, `Scroll` and an empty
`ContentFrame` in `project/scenes/screens/ColorsAndStylesScreen.tscn`; the
inventory inside is built in code.
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

var notifications = new UiNotification();
AddChild(notifications);
notifications.Enqueue(new UiNotificationSpec(
    UiPopupType.Default, "Saved", "Your changes are saved.",
    OnClick: () => false));

notifications.Enqueue(new UiNotificationSpec(
    UiPopupType.Default, "New part unlocked: Spring", "Reached 10 m.",
    Icon: new(UiIconId.PartSpring)));
```

Parts tray tabs use persistent native toggle buttons in a `ButtonGroup`,
with the reference's part glyphs and accent-soft selected treatment, not a
solid accent fill. The tabs share the strip's width equally with 4px between
them (the reference's `flex: 1`), 32px high, so the owner sets the width: four
tabs fit the side panel's content width, about 35px each (#330, replacing the
48px-wide tabs of #249). The gallery shows one unframed interactive specimen
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
they are hidden. Creations, the only in-app screen without Back (the standalone gallery
entry is a debug page), insets its title
48px (`TitleInset`, the Back button's touch width) so the title sits where
titles after Back do. The reference's empty 40px `w-col-xs` slot leaves it
8px short. Nodes a screen adds live under
`Toolbar/HBoxContainer/MarginContainer/ToolbarContent`. The toolbar also owns the overflow
`Menu`: it anchors it under Overflow, opens it and makes it dismissible; the
screen authors the items and decides what each does. `BackPressed` is its only
signal. It fills with `panel`, like the button bar and side panel, over the
`bg` of `UiFrame`'s card (`UiFrameCard`, the reference's `.frame`), so the
shell stands out from the screen's content (#347).

`UiButtonBar` is the vertical button bar down the left edge
([issue #320](https://github.com/MrLogic85/Node-Runner/issues/320)): 56px
wide, one touch target plus `space-2` (the reference token is `w-rail`), with
a divider down its right edge and a `panel` background (#347). Its width, padding
and separation live in `UiButtonBar.tscn` (#335); the code-built Build screen
uses the same width as `UiLayout.ButtonBarWidth`. Unlike `UiToolbar` it has no fixed buttons: `%ButtonBarContent` is a plain
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
in a 32px touch area; both chevrons match (human decision on #358, replacing
the compact flat `UiButton` and `ink` colour from #321). Code owns only the
panel's own width, which it animates when collapsing; its paddings,
separations and slot sizes live in `UiSidePanel.tscn` (#335). The header
reaches past the padding on the right so the chevron's icon lines up with the
content's right edge. The screen authors
the content below it in `%SidePanelContent` and decides what the panel shows.
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
clipping changes); a menu opened as an overlay is top-level and clips again. Inside a clipping card, a static menu's row wash is
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
to its rounded surface. `Follow` keeps the top-level menu attached to a
normalized point on an anchor control while scrolling or relayout moves that
control.
Menus default to the fixed menu-width token and can opt into content-wrapping
width through `WidthMode`. The menu's `Compact` toggle overrides all direct
`UiMenuItem` children to the matching 32px or 48px row variant, so one menu
cannot accidentally mix densities. The abstract `UiMenuItem` base owns
availability, size, padding, and the square selected highlight.
`UiMenuActionItem` is the recommended optional action child: an icon centred on
the row, the label with an optional note under it, and a check for the selected
choice, laid out by containers over a transparent Button that handles press,
hover and focus. The row shows accent soft while pressed or hovered. `Kind` distinguishes
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
