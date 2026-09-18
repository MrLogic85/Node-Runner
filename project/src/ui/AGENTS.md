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
5. **Visual direction comes from `docs/UI_DIRECTION.md`.** Keep UI code
   themeable; do not bake a specific skin into screen logic.

## Folder layout inside `src/ui/`

```
src/ui/
├── lib/          Reusable, app-agnostic Controls (LabeledSlider, LineChart)
├── screens/      Full-screen scenes (MainScreen, TerrariumScreen)
└── widgets/      App-specific composite widgets (NetworkVisualizer, CreatureIcon)
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
- No hardcoded pixel sizes for mobile — use anchors, container-based layout.
- Colors and fonts come from a shared Theme resource (add when it becomes
  painful; not on day one).

## Test expectations

- `docs/MANUAL_TESTING.md` decides when UI changes need manual testing and
  what evidence to capture.
- Pure presentation helpers (number formatters, string builders) get unit
  tests.
