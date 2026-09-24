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

The current Godot host uses the `UiTokens` adapter. A small project-wide native
`Theme` supplies the Label line-spacing default described below; the reference's
full native theme and `Window.content_scale_factor` guidance remains a target,
not completed host wiring. Theme/scale propagation is tracked in
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

The zero line-spacing default lives once in
`project/assets/themes/UiDefaults.tres`, loaded through `gui/theme/custom` in
`project.godot`. Godot's built-in Label default is **3px**, not zero. New Labels
inherit the project default without per-component overrides; `ApplyTextStyle`
adjusts font metrics rather than Label spacing. Do not subtract those 3px from font heights, since that
would shorten single-line boxes and affect controls that do not add Label's gap.

Top/bottom spacing changes every line's metrics, including a single line and
the outer edges of a multiline block. Preserve the adapter's existing glyph
spacing when creating the variation. Resolve spacing for the actual font size;
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
runtime checks covered all 17 styles in Neon/Paper and preserved 32px compact
and 48px ordinary icon targets. This does not establish equivalence for arbitrary
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
