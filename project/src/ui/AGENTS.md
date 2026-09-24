# AGENTS.md — `src/ui/`

**Godot Controls, scenes, screens. The last mile.**

## Rules

1. **UI binds to view-models in `NodeRunner.App`, not to sim/managers
   directly.** Reach for `MainViewModel`, not `Evolver`.
2. **Reusable Controls live in `lib/`.** Screens in `screens/`. App-specific
   composite widgets in `widgets/`.
3. **UI code contains presentation logic only.** Formatting, animation,
   layout, input handling. No fitness math, no ML, no persistence.
4. **Every screen has a matching `.tscn` in `project/scenes/ui/`.** The `.cs`
   file lives here in `src/ui/screens/`.
5. **Visual contracts come from `reference design/`.** Start at its index and
   read the relevant component README/preview, `tokens.json`, and `library.md`.
   `docs/UI_DIRECTION.md` adds repository-specific implementation boundaries.

## Folder layout inside `src/ui/`

```
src/ui/
├── lib/          Reusable, app-agnostic Controls (UiButton, UiCard)
├── screens/      Full-screen scenes (BuildScreen, CreationsScreen)
└── widgets/      App-specific composites (ConstructionCanvas, GenerationStrip)
```

Rule of thumb: if a Control could be lifted into another Godot project
unchanged, it belongs in `lib/`. If it embeds project vocabulary
("creature", "generation", "brain"), it's a `widget/`.

## Rules per subfolder

### `lib/`

- No references to `project/src/sim/`, `project/src/creature/`, or the
  `NodeRunner.Domain` lib. Only primitives and system types.
- Exports parameters via `[Export]` and signals for events.
- One file per Control.

### `screens/`

- One class per screen. Holds child Control references, subscribes to its
  ViewModel, wires input.
- `_Ready()` builds the VM (or receives one via a `Setup(vm)` method called
  by `SceneRouter`) and subscribes.
- `_ExitTree()` unsubscribes. No leaked handlers.

### `widgets/`

- Domain-aware. May depend on `NodeRunner.App` (view-models) and
  `NodeRunner.Domain`.
- Must not depend on `project/src/sim/` directly.
- Reusable across screens.

## Style specifics

- Godot signals for Control-to-Control events. C# events for
  ViewModel-to-Control notifications.
- `[Export]` fields have sensible defaults so the Control renders something
  useful in the editor without setup.
- Use containers and anchors for composition. Fixed reference dimensions must
  come from named `UiTokens`/`UiLayout` values, never one-off screen literals.
- Colors, fonts, typography, spacing, radius, and strokes come from the shared
  token adapter. A screen must not recreate theme values locally.

## Test expectations

- `docs/MANUAL_TESTING.md` decides when UI changes need manual testing and
  what evidence to capture.
- Pure presentation helpers (number formatters, string builders) get unit
  tests.
