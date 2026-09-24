# UI direction

Node Runner's UI source of truth is the tracked package in
`reference design/`. Start at its `index.html`, then read the relevant
component README and preview. `tokens.json` owns visual values, `library.md`
owns reusable controls, and the component READMEs own screens and interaction
flows. Do not copy those contracts into `docs/`.

This document contains only repository-specific direction that the design
package does not own. `docs/UI_IMPLEMENTATION_PLAN.md` owns delivery order and
GitHub dependencies.

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
In particular, CSS-only translucent color variants need not become additional
Godot color tokens: shared effect definitions may derive alpha from the
style's semantic color token. Keep effect settings centralized and preserve
the paper theme and Effects Lite suppression of glow.

The current Godot host uses the `UiTokens` adapter. The reference's native
`Theme` resource and `Window.content_scale_factor` guidance describes a target,
not completed host wiring; theme/scale propagation is tracked in
[issue #236](https://github.com/MrLogic85/Node-Runner/issues/236).

For buttons, the Component Library's **Buttons** paragraph defines the four
current kinds. Older reference summaries still call `secondary` "default"
and `tertiary` "danger"; `on` and `off` are states, not kinds.
Hold-to-activate is available across kinds and layouts, not only destructive
buttons. Standard icon buttons have a 40px visible frame inside a 48px target.
Compact row/icon buttons use 32px height (32px width for icons) with no extra
touch inset; stacked buttons retain their 48px target. Inspector close uses
the shared flat compact icon button, not a custom header button.
The Android-reviewed shared glow uses base colours with 12% opacity
and 10px extent rather than separate button/control glow variants.

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

Component Gallery's toolbar overflow menu toggles **Debug bounds** live.
Bounds are off by default; `ShowDebugBounds` also supports runtime changes,
and `ui/component_gallery_debug_bounds` can enable them at startup for a
debugging export.

Parts tray tabs use persistent native toggle buttons in a `ButtonGroup`,
with the reference's part glyphs and accent-soft selected treatment, not a
solid accent fill. Each native target is at least 48px wide and high; the
visible frame is 32px high with 4px between tabs. Four tabs therefore need
204px rather than the HTML specimen's 176px strip. The gallery shows one
unframed interactive specimen. See
[issue #249](https://github.com/MrLogic85/Node-Runner/issues/249).

Glow is a visual effect outside a control's layout rectangle. Components must
not add layout padding just to make glow visible, because that breaks placement
and popup anchoring. Parents that intentionally clip children, especially
scroll viewports, must either provide container-level bleed/inset for glowing
content or accept/document clipped glow. Product popups should live in an
unclipped overlay layer rather than inside clipped scroll content. See
[issue #252](https://github.com/MrLogic85/Node-Runner/issues/252).

Overflow menus default to the existing fixed token width, and can opt into
content-wrapping width through `UiOverflowMenu.WidthMode`. The legacy `Width`
property remains the fixed row width override for compatibility; `0` keeps the
token default. Wrap-content menus remove the fixed row width but keep native
button rows, 48px touch height, row padding, icon gap, and semantic coloring.
See [issue #251](https://github.com/MrLogic85/Node-Runner/issues/251).

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
