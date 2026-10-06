# AGENTS.md — `src/ui/`

**Godot Controls, scenes, screens. The last mile.**

`lib/AGENTS.md` holds the theme, drawing, icon, input and UI-level rules.
They bind every file under `src/ui/`, not only the library.

## Rules

1. **UI binds to view-models in `NodeRunner.App`, not to sim/managers
   directly.** Reach for `BuildViewModel`, not `Evolver`.
2. **Reusable Controls live in `lib/`.** Screens in `screens/`. App-specific
   composite widgets in `widgets/`.
3. **UI code contains presentation logic only.** Formatting, animation,
   layout, input handling. No fitness math, no ML, no persistence.
4. **Every screen has a matching `.tscn` in `project/scenes/screens/`.** The
   `.cs` file lives here in `src/ui/screens/`. Reusable component scenes
   (e.g. `UiStageCard`, `UiFrame`) live in `project/scenes/ui/` instead, and
   widget scenes (e.g. `CreationCard`) in `project/scenes/widgets/`.
   The scene owns the layout and the script owns behaviour
   (`docs/UI_DIRECTION.md` → "Who owns what").
5. **Reuse library components and tokens** (`docs/UI_DIRECTION.md`).
6. **A new screen, widget or gallery script joins `RewrittenUi`**
   (`tests/NodeRunner.Ui.Tests/RewrittenSceneTests.cs`) in the same PR, so
   the scene and source guards cover it from the start.

## Folder layout inside `src/ui/`

```
src/ui/
├── lib/          Reusable, app-agnostic Controls (UiButton, UiCard)
├── screens/      Full-screen scenes (BuildScreen, CreationsScreen)
└── widgets/      App-specific composites (BuildCanvas, CreationCard)
```

Rule of thumb: if a Control could be lifted into another Godot project
unchanged, it belongs in `lib/`. If it embeds project vocabulary
("creature", "generation", "brain"), it's a `widget/`.

## Rules per subfolder

### `screens/`

- One class per screen. Holds child Control references, subscribes to its
  ViewModel, wires input.
- A routed screen implements `IRoutedScene`: `SceneRouter` calls
  `Enter(route, navigator)` before it joins the tree, and `_Ready()` builds
  the VM from the route and subscribes. A screen that needs saves stays
  unrouted and emits signals; its host in `project/src/hosts/` is the routed scene.
  See `docs/ARCHITECTURE.md` → Navigation.
- Android Back: add a `UiBackHandler` with `InternalMode.Front`, give it a
  `CanTakeBack` as its summary describes, and handle `BackRequested`. Do not
  set `QuitOnGoBack` in a screen.
- Android sends several Back signals for one press, on key-down, key
  repeat and release (#506, godotengine/godot#123454). `UiBackPress`
  lets one through per Back key-down or gesture (#838). Anything else that acts on
  `NotificationWMGoBackRequest` or `GoBackRequested` calls
  `UiBackPress.TryTake(this)` once it has decided to act, and acts only if
  it returns true. Taking uses up the press, so never take it before
  checking your own condition. `UiBackPressGuardTests` enforces the call.
- `_ExitTree()` unsubscribes. No leaked handlers.
- A screen script declares no numbers: its numeric exports take their value
  from its scene.

### `widgets/`

- Domain-aware. May depend on `NodeRunner.App` (view-models),
  `NodeRunner.Domain` and `NodeRunner.Mechanics` (to draw a part as the sim
  moves it).
- Must not depend on `project/src/sim/` directly.
- Reusable across screens.
- Every number is named. In a drawn widget (`RewrittenUi.DrawnWidgets`),
  literals inside `_Draw` and its `Draw*` helpers stay inline. A drawn
  widget may set the `Position` of controls its scene authors (selection
  handles, #366); their sizes stay in the scene.

## Style specifics

- Godot signals for Control-to-Control events. C# events for
  ViewModel-to-Control notifications.
- `[Export]` fields have sensible defaults so the Control renders something
  useful in the editor without setup.
- `RewrittenSceneTests` reads literal `GetNode<T>("%Name")` calls, so a
  helper takes the node, not its path.
- Use containers and anchors for composition. Dimensions set in C# come from
  named `UiSize`/`UiLayout`/`UiSpacing` values, never one-off literals or a
  local copy. A scene owns the paddings and sizes it authors; do not
  re-apply them from code (#331).
- Order drawing with the tree, not `ZIndex` (`lib/AGENTS.md` → "UI levels").
  Inside a world (`UiWorldView`) holding creature parts, draw a view's own
  marks on a `ViewLayer`, not as a last child: the parts sit on
  `CreatureLayers` (`project/src/theme/AGENTS.md`).

## Text

`docs/LOCALIZATION.md` → "Text rules" owns what the text says; this is how
code shows it.

- Show a `UiText` with `label.ShowText(text)` or `button.ShowText(text)`
  (`widgets/UiTextTranslation.cs`). It sets the control's `TextSource` and
  turns Godot's own translation off. Other components take text through
  their `…Source` properties and `UiTextTranslation.Source(text)`. Only
  `UiTextTranslation` reads a `UiText`.
- Text drawn in code asks `UiTextTranslation.Source` and redraws when the
  language changes. `UiCalloutLayer` turns auto-translation off for its
  callouts.
- A label showing player-written text sets `auto_translate_mode = Disabled`
  on itself only. A part's own name crosses as `UiText.AsWritten`.
- Text cased in code goes through `UiThemeLookup.LetterCase`, never .NET
  casing (#776, `UiTextTranslationTests`).
- A component that passes its text to an inner control calls
  `UiTranslation.ShareContext(this, inner)` first, so the context goes with
  it (#777).
- A number argument is an `int`/`long` or a `FixedNumber`, wrapped with
  `UiText.Number`.

## Touch input

- A widget that hit-tests pointer input against what it draws maps the
  position with `GetGlobalTransformWithCanvas().AffineInverse() * position`.
  `ToLocal()` misses the world viewport's canvas transform.
- `UiWorldView` pushes events into its world; `BuildCanvas` reads them in
  `_UnhandledInput`. A tray drag maps through `SlotTransform`.
- Keep Godot's `emulate_mouse_from_touch` on. `BuildCanvas` is the exception
  that reads `ScreenTouch`/`ScreenDrag` by index and skips events whose
  device is `InputEvent.DeviceIdEmulation`.
- Keep `input_devices/pointing/android/enable_pan_and_scale_gestures` off:
  it doubled events on a Galaxy S25 and broke pinch (#564).

## Test expectations

- `docs/MANUAL_TESTING.md` decides when UI changes need manual testing and
  what evidence to capture.
- Pure presentation helpers (number formatters, string builders) get unit
  tests.
