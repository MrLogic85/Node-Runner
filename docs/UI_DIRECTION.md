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
`UiButton.Selected` exposes that selected state in C# and the Inspector.
Native `Disabled` is the sole availability setting; UiButton has no inverse
`Enabled` property. Disabling cancels a hold and dims the custom stack/progress
content as well as the native button visuals.
All button text, including the neuron stepper's plus/minus signs, uses
`LabelText` with the layout's normal typography and padding.
Inherited `Text` and `Icon` remain visible but read-only in the Inspector.
Their generated values are not saved; author `LabelText` and `IconId` instead.
Native `Flat` is hidden in the Inspector; use `Kind = Flat` for the canonical style.
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
These sizes come from `ControlHeight`, `ControlSmall`, and `TouchTarget`.
Add UiButton directly via Add Node. Its exported `Icon Id` selects a canonical
icon or `None`; no nullable/icon-only wrapper is needed. Existing serialized
Row/Stacked enum values remain stable. C# callers use `UiIconId.None` instead
of null. Any locally authored, unsaved old `Compact` setting must be replaced
by selecting `RowCompact`; reopen scenes after rebuilding to refresh Inspector.
Row icons are 16px with or without text, including compact; stacked icons are
20px with or without text. Textless row buttons need no separate icon layout.
Inspector close uses the shared flat compact row button. Toolbar icon actions
may use Stacked pending their own component review; the human accepted the
temporary visual change and will discuss the removed touch margins with the
designer. This deliberately supersedes the reference's 40px-in-48px button
target, not the touch geometry of other controls.
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
confirmation button. Default actions use Primary buttons, Warning actions use
Flat (human decision pending designer review), and Danger actions use Tertiary.
Cancel and action have exactly equal width and height, expanding evenly across
the action row with a single token-sized gap, including busy and retry states.
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
gestures/animations, while hidden or unfocused, and when the host sets `Paused`
for a modal dialog. Modal pause/focus loss cancels unfinished drags to rest and
pauses a committed exit until resumed. Resize cancels unfinished drags and
retargets an outgoing animation. Clear/dismiss/teardown cancel pending animation.
The gallery wires modal pause to `UiDialog.Open`/`Finished`.
Unexpected notification callback exceptions are logged and surfaced as a Danger
notification before the remaining queue. Modal input/focus is isolated from the
gallery. Theme changes clear its notification queue; tokens apply to the next
opened dialog/notification.
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
It restores `QuitOnGoBack` when closed or removed. The gallery itself can run
with F6. Editor authoring is being introduced in
[issue #282](https://github.com/MrLogic85/Node-Runner/issues/282). Dialog content
and notification content, as well as Popup Gallery, are scene-authored.

Open `project/scenes/ui/PopupGalleryScreen.tscn` to edit the actual gallery.
The header keeps a horizontally scrolling, right-aligned theme selector.
Below it, a vertical ScrollContainer holds dialog and notification specimens
in wrapping HFlowContainers plus a status label. Labels, order, spacing, and
layout live in the scene; its signal connections bind each button to the demo
callbacks in `PopupGalleryScreen.cs`. Keep the unique `Background`,
`UiSegmentedSwitch`, `Disclaimer`, and `Status` names and the root
`MarginContainer` binding. Theme changes update existing controls instead of
rebuilding the page, preserving authored layout and scroll position.
Gallery launcher buttons use ordinary clicks; hold requirements belong to the
dialogs they open. F6 exercises the same scene that the Component Gallery opens.

#### Editing a dialog in Godot

For new scene-authored text, add **UiLabel** from Godot's Add Node dialog.
Its **Text Style** dropdown selects one of the 17 canonical `UiTokens` text
styles. `[Tool]` updates typography in the editor; native Label still owns
text, wrapping, alignment, sizing and rendering. The derived Theme,
auto-font-sizing, uppercase and LabelSettings Inspector fields are hidden;
their values are controlled by the preset rather than serialized per node.
Uppercase is display-only and never changes the authored Text. Color and
layout remain normal Label properties; this is not global theme propagation.
Code may supply `Tokens`, while style values remain defined only in `UiTokens`.
The label derives a private typography-only Theme; colors still inherit normally.
Godot's dynamic font/font-size/line-spacing overrides remain visible by human
decision; no Inspector plugin is needed. Manual overrides can take effect until
the preset is reapplied (Text Style or Tokens changes, or scene load/reentry),
at which point UiLabel clears those overrides. Use Text Style for durable
typography choices, not those transient overrides.
This is an authoring API, not a restriction on what arbitrary C# can change.
Existing Labels are not automatically migrated.

Open `project/scenes/ui/UiDialogContent.tscn` in the 2D editor. This is the
actual scene instantiated by `UiDialog`, not a separate mock. Build the C#
project once after script changes so Godot can run its editor previews.

- `Card/Column/Heading/Titles/Title` and `Card/Column/BodyScroll/Content`:
  edit the Label's Text for the standalone specimen.
- `Card/Column/Actions/Cancel` and `Confirm`: edit **Label Text** (the UiButton
  export), not the inherited Button Text. Toggle Confirm's Visible for a
  one-button specimen; the abort button fills the row.
- `Card`: edit Custom Minimum Size X for the desired card width. Containers
  own child placement; `Column` and `Actions` expose separation under Theme
  Overrides / Constants. The view keeps the card centered, constrains long
  content to the viewport and keeps the action buttons equal.
- Root `Type` and `Theme Preview`: preview semantic type and Neon/Paper/
  Effects Lite. Typography and semantic colors still come from shared tokens.
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

Root **Type** controls the card variant, semantic overline and icon/color;
**Theme Preview** selects Neon, Paper or Effects Lite. Card **Size Variant**
and **Glow** remain normal shared-card options. Root Custom Minimum Size X
is the preferred width (initially the 326px card-width token); the runtime
host narrows it to the available viewport and restores that preferred width
when space becomes available again.

F6 shows the standalone card without callbacks, expiry or swipe. Use Popup
Gallery to exercise those interactions. Runtime `UiNotificationSpec` replaces
the specimen title/message/type, and the host supplies tokens, focus/input
and bottom-center placement. The authored hierarchy, typography choices,
spacing and preferred width are reused unchanged. Queue, expiry, click
callbacks, pause and swipe animation remain in `UiNotification`.

`UiNotificationSpec.Icon` optionally selects a canonical glyph through
`new UiNotificationIcon(UiIconId.Trophy)` or
`new UiNotificationIcon(UiPartIconId.Spring)`. Omit it (or use null) to retain
the type's default: Model for Default, Warn for Warn/Danger. The selected
glyph keeps the semantic tint and Large icon size; it does not change the
type label or card variant. Arbitrary textures and `UiIconId.None` are not
accepted. The override is runtime data, not a new Inspector field.

```csharp
var dialog = new UiDialog { Tokens = tokens };
AddChild(dialog);
dialog.Open(new UiDialogSpec(
    UiPopupType.Warn, "Continue?", "Review the changes.", "Continue",
    async () => await ApplyChangesAsync(), // Returns UiDialogResult.
    holdToAction: false));
dialog.Finished += confirmed => { /* Host reacts to completion or cancellation. */ };

var notifications = new UiNotification { Tokens = tokens };
AddChild(notifications);
notifications.Enqueue(new UiNotificationSpec(
    UiPopupType.Default, "Saved", "Your changes are saved.",
    OnClick: () => false));

notifications.Enqueue(new UiNotificationSpec(
    UiPopupType.Default, "New part unlocked: Spring", "Reached 10 m.",
    Icon: new(UiPartIconId.Spring)));
```

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
