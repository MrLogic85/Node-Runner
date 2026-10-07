# AGENTS.md — `src/ui/lib/`

**The component library: reusable, app-agnostic Controls, their scenes in
`project/scenes/ui/`, the theme files and the dimension constants.**

`docs/UI_DIRECTION.md` owns how it looks; this file, how its code is
written. Its rules bind all UI code (`../AGENTS.md`).

## Boundaries

- No references to `project/src/sim/`, `project/src/creature/` or the
  `NodeRunner.Domain` lib; only primitives and system types.
- Parameters are `[Export]`s, events are signals, one file per Control.
- A plain `Control` returns a computed size from `_GetMinimumSize`, leaving
  `custom_minimum_size` to the scene. Button and container subclasses
  cannot (their native minimum size ignores the script), so `UiButton` owns
  its `custom_minimum_size`.
- A `[Tool]` component that writes a property on itself or a node of its own
  scene (a theme override, `clip_children`, text, an icon, a computed size)
  lists it in a `UiUnsavedState` and calls `Handle` first in
  `_Notification`, or Godot saves the derived value in the scene (#309).
  Add own-node properties to `SceneDerivedStateTests`.
- Never copy a component's generated internals into a scene; fill its slot
  through Editable Children.

## Theme and tokens

- **The Theme owns what differs between palettes**: colours, opacities
  (`UiTokens.Alpha`), the `effects_enabled` flag and type variations.
  **Constants own dimensions**: `UiSize` (`Space`, `Control`, `Icon`,
  `Radius`, `Stroke`, `Widget`), `UiLayout` (shell and surface sizes),
  `UiSpacing` (gap roles): every palette shares them, pure tests can use
  them, and Godot would round them as theme constants.
  Never re-declare one locally. An editor-picked dimension is a `UiTokens`
  identifier (`UiTokens.Size.Stroke`) that `UiThemeLookup.Size` maps to its
  constant.
- **Select theme values by name, never by copying them.** Set
  `ThemeTypeVariation`; a value read and written back as an
  `AddTheme*Override` goes stale on a theme swap. A control has one
  variation, so combined axes (typography × text colour, size × kind) are
  variations `UiThemeExpander` generates and chains (`UiNoteMuted` →
  `UiNote` → `Label`). Colours a control draws are read in `_Draw`, which
  Godot re-queues on a theme change. Overrides are only for per-instance
  values (`IconTint`) and theme-independent constants. Never pass palette
  data to controls or walk a subtree to propagate style.
- `UiThemeLookup` resolves `UiTokens` through the inherited Theme and
  reports a missing item in debug builds. A soft fill is a base colour
  `.WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft))`; colour maths
  is `UiColorExtensions`. C# writes no colour literal; only `Colors.White`
  and `Colors.Transparent` serve as neutral modulation.
- Text takes its style by name: `UiThemeLookup.ApplyTextStyle(control,
  typography, color)` on a Label, Button or LineEdit (or `UiLabel`'s Text
  Style and Text Color), never `ApplyTypography` plus a copied `font_color`.
  Other state colours (hover, placeholder, caret) come from the base type;
  letter case from the typography (`UiTokens.IsUppercase`).
- **Theme files in `project/assets/themes/` are the source of truth.**
  `Neon.tres` is the project theme (`gui/theme/custom`): the Neon palette
  plus everything palettes share (typography, default font,
  `line_spacing`, container separations). Its default separation, 8, equals
  `UiSize.Space.S2`; change both together. `Paper.tres` holds only its
  palette; a colour missing there silently falls back to Neon.
- Colour-derived items (`UiThemeExpander.DerivedColors`: text-colour
  variations, the `UiButton`, `UiBadge` and `UiIconTab` variations, base
  `Label`/`Button`/`LineEdit` colours) are generated. Never hand-edit them;
  regenerate them as `project/src/tools/AGENTS.md` says.
- Each typography's `FontVariation` in `Neon.tres` sets
  `spacing_top`/`spacing_bottom` to reach the token's `lineHeight`: top =
  floor(adjustment / 2), bottom the rest, adjustment = `lineHeight` − font
  height. `Label.line_spacing` stays 0, not Godot's 3. Recompute when a
  style's size or line height changes (#278).
- Screens inherit Neon. Assign another `Theme` only to switch palette, in
  `_EnterTree` or before `AddChild`, never in `_Ready`: authored children
  run `_Ready` first and would measure against Godot's default theme (#301).
- A `NotificationThemeChanged` handler is only for caches that cannot be a
  variation (draw geometry, styleboxes, icon tints). Godot sends it on every
  tree entry, before `_Ready`, so a rebuilding handler checks
  `IsNodeReady()`. One that writes overrides on its own node re-triggers
  itself; wrap it in `UiThemeRefresh.Guarded`. An override of
  `_Notification` calls `base._Notification(what)`, or the base refresh
  never runs.

## Drawing

- **Every stroke in `_Draw` goes through `UiPixelPen` (#733).** Godot's
  antialiased primitives feather about one unit in draw space, which the
  stretch and UI size blow up into a blur; a hard stroke rounds to 1 or 2
  device pixels by position. The pen feathers one device pixel at any
  scale, with widths in units. Thin filled rects (dividers) count as strokes. `pen.DashedLine`
  trims each dash (`DashTrim`) so the feather does not fill the gap.
- `UiStrokeGuardTests` allows only these outside the pen: width `-1`
  hairlines, filled `DrawRect` areas, `pen.Polygon` and
  `UiBoundsDebugOverlay`. Godot cannot feather `DrawColoredPolygon`, so keep
  a polygon's edge under an antialiased outline, or draw a square or diamond
  as one wide `pen.Line`.
- Many strokes of one colour go in one `pen.Strokes` call (`UiStrokeMesh`):
  on Compatibility each draw call builds its own buffer, and a Spring drawn
  in hundreds of calls cost a phone most of its frame (#835).
- A part visual bakes the pixel scale into its strokes, so every view that
  holds parts calls `PartVisual.RedrawOnNewPixelScale` whenever it may have
  zoomed (`CreatureParts` on each draw, `TrainingHost` each frame, paused
  too).
- `DrawStyleBox` is exempt: Godot divides a `StyleBoxFlat`'s feather by the
  oversampling. Keep `StyleBoxFlat.AntiAliasing` on (#732).

## Icon filtering

Icons sample `Linear`; text and other textures keep the project's `Nearest`
(#734). At a fractional UI size an icon lands off whole device pixels, where
`Nearest` doubles or drops rows; text under `Linear` looks soft.
`UiIconFilterGuardTests` fails an icon host that does none of these:

- A node whose textures are all icons calls `UiIcons.UseIconFilter`; its
  text children call `UiIcons.UseTextFilter`. A scene-placed `[Tool]`
  component hides the derived filter with `UiIcons.HideIconFilter`.
- A `Button` that draws its own text beside an icon wraps the icon in
  `UiLinearIcon`: Godot's `CanvasTexture` draws a `DPITexture` unscaled,
  losing its oversampling.
- An icon on a scene-authored node sets `texture_filter = 2` in the scene.

SVGs stay white and take colour from modulate or icon colours,
never `color_map`. Button icons use `UiIcons.Apply` without a tint; state
colours come from `Button` or a generated variation (`UiIconTab`). `ui/lib`
sets no colour override outside `UiIcons`' tint helper (#338).

## UI size, safe area and worlds

- The `UiScale` autoload applies the UI size once, as the root window's
  `ContentScaleFactor` (`UiScale.RootFactor`). Nothing else multiplies by
  it: tokens, scenes and components keep their numbers.
- That factor scales every canvas item, so a world view undoes it:
  `UiWorldView` sizes its SubViewport at the slot times `RootFactor`, and
  its screen-size overlays apply the factor again. Build's world sets
  `ScalesWithUi` instead, and `CanvasView` divides its zoom limits by it.
- A thumbnail world's SubViewport update mode is Disabled; it calls
  `RequestRender` when it refreshes, so scrolling renders nothing (#770).
  Each world's render target costs about 1 MB on a phone, so a long list
  reuses its cards (#786). Every world sets `disable_3d`.
- `UiSafeArea` (the `SafeArea` autoload) turns
  the display safe area into canvas-unit insets that `UiFrame` adds to its
  card margin. `UiFrame`'s maximum size is the
  visible canvas, propagated to the card, so too much content is clipped,
  not pushed off screen (#737).

## Input and press feedback

- **No hover look**, since hover only lingers after a tap: a `hover`
  stylebox repeats `normal`, `hover_pressed` repeats `pressed`, and nothing
  branches on `DrawMode.Hover` (`UiPressFeedbackTests`). The held look is
  `UiPressFeedback`.
- **One input source per tap.** Touch reaches controls as Godot's emulated
  mouse, so a control reads mouse events (`PointerInput`), never touch too,
  which would handle a tap twice. `UiMenu` swallows both copies of a
  dismissing tap.
- **Drags scroll through buttons.** A Godot button made in code sets
  `MouseFilter = Pass`, so a drag that starts on it still scrolls its
  ScrollContainer (`UiNativeScroll`, `UiButtonScrollGuardTests`, #1001).

## UI levels

- No code under `src/ui/` sets `ZIndex`: it sorts across the whole
  `CanvasLayer` and draws a raised control through everything above it
  (#463, #768). Order by the tree: add an overlay last, or a part that must
  draw last with `InternalMode.Back`. Marks in a world go on a `ViewLayer`,
  whose order `project/src/theme/AGENTS.md` owns.
- The levels in a Viewport or Window are the `UiLayers` CanvasLayers:
  `Screen`, `Overlay` (menus), `Notification`. A dialog is an embedded
  Window over every level of its Viewport, so it covers notifications.
- A menu that floats over its opener sits in a `UiLevelLayer`, which stays
  in the opener's subtree (lifetime, unique names) and hands on the theme
  and visibility a `CanvasLayer` cuts. A scene saves no level: its
  `CanvasLayer` is a `UiLevelLayer` or a world backdrop, and code sets any
  other level from `UiLayers` (`UiLayersTests`).
- Only `UiNotificationLayer` (the `Notifications` autoload) creates app
  notifications, through `UiNotificationLayer.Enqueue`, so they outlive
  scene changes (#472). Dialogs stay in the scene that opens them.

## Clipping and glow

- Glow draws outside the layout rectangle. Never add layout padding to show
  it, which breaks placement and anchoring; a clipping parent gives bleed
  or accepts clipped glow, and popups live in an unclipped layer (#252).
- Godot cannot nest `clip_children` (the inner node draws nothing), so
  `UiCard.ClipContent` and `UiMenu` clip through `UiClip` only when no
  ancestor does. A card inside a
  page does not clip, so content flush with its edge rounds the shared
  corners itself (`UiCorners`). `ClipContent` is off by default: each
  clipping card renders through an extra buffer.

## Components

- `UiButton` is the only button class. Godot's Button has no `uppercase`,
  so an inner `UiLabel` draws the native `Text`. Author `IconId`
  (`UiIconId.None`, never null) and `Kind = Flat`, not the derived native
  `Icon` or `Flat`. `ContentLayout` picks the icon size, never the call site
  (#358).
- A button group with a look of its own (as `UiCardActions`) implements
  `IUiButtonDesigner` rather than restyling its `UiButton`s.
- A stepped slider sets `UiSlider.Step` from the value's `SettingRange`
  (`SettingSlider.Step`, `ParameterSlider.Step`). Never round the value
  alone: the thumb and readout disagree and the thumb jumps (#711). Uneven
  stops use `SettingRange.Of(stops)` (#801).
- A message in a drawing goes through `UiCalloutLayer` (#593), fed by the
  view-model's notes, most important first (`BuildViewModel.CanvasNotes`).
- `UiMenuActionItem` uses `MouseFilter.Pass` so its menu also gets the
  click; use `Stop` only on an item that owns its action.
- A bar's `UiTextField` has a fixed width (#485): `LineEdit` has no "…", and
  no scene setting caps a text-sized width at the space left.
- Layout widths a scene authors have no C# constant, since a scene cannot
  read one (#300). The reference values: `w-dialog` 300, `w-card` 326,
  `w-sheet` 720, `w-sheet-wide` 880, `w-brain` 460, `w-well` 250, `h-well`
  64, `w-tile` 156, `h-stage` 170, `h-thumb` 100, `w-col-xs` 40, `w-col-md`
  76, `w-col-lg` 96, `w-col-xl` 128.
- `UiDialogContent.tscn` and `UiNotificationContent.tscn` are the runtime
  layout, not mocks: keep their `%` names.

## Gallery pages

- The library pages (Component Gallery, Toolbars, Colors & Styles, Popup
  Gallery) share `GalleryScreen`. Each page scene authors its own `UiFrame`,
  toolbar and menu under the same unique names (`UiFrame`, `Toolbar`,
  `ThemeSwitcher`, the `ToolbarMenu*` items) with their `[editable path=…]`
  lines: without them export drops every node added inside
  (`SceneEditableChildrenTests`).
- A new colour token, icon, icon size or text style needs a specimen in
  `ColorsAndStylesScreen.tscn` in its group (`inventory_icon`,
  `inventory_icon_size`, `inventory_text_style`).
- `ui/component_gallery=true` or `ui/popup_gallery=true` makes a
  development export start on that page; `ui/component_gallery_debug_bounds`
  turns debug bounds on.
