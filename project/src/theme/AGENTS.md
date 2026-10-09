# AGENTS.md — `src/theme/`

**How the arena and creatures look: theme colours, drawing helpers and the
shared part visuals that Build, Training and thumbnails draw with (#766).**

## Rules

1. **Plain visual state only.** Code here takes geometry, a `VisualTheme`,
   selection and display state. It never depends on physics bodies,
   `PistonLink`, runtime sensors, view-models or screens; the creature and
   UI layers adapt their own state into it.
2. **Draw layers are named.** A part's place in the draw order is a
   `CreatureLayers` member passed to `PartVisual`'s constructor, and
   Training's world uses `ArenaLayers`. Only `PartVisual` sets a part's
   `ZIndex`, so the selected-layer switch is never bypassed, and nothing
   here depends on the order parts are added. `docs/WORLD_VISUALS.md` →
   "Draw layers" owns the order; `DrawLayersTests` checks that layers are
   passed by name and that every `ZIndex` is a named layer.
3. **Selection is drawn with its part.** A part draws its own marks in its
   `_Draw` and rises whole to its kind's selected layer while selected.
   Never draw a mark on a separate layer.
4. **Strokes follow the pen rule** in `project/src/ui/lib/AGENTS.md` →
   Drawing (`UiStrokeGuardTests`). `docs/WORLD_VISUALS.md` owns how parts
   look.

## What lives here

- `VisualTheme.cs` — the arena, creature and selection colours
- `JointDrawing.cs`, `PistonDrawing.cs`, `SpringDrawing.cs`,
  `SensorDrawing.cs`, `SelectionDrawing.cs`, `TriangleHatch.cs`, `ServoGeometry.cs` — static drawing helpers
- `PartVisual.cs` — the base of every part visual: theme, selection and
  layer, and the redraw on a new pixel scale every view that holds parts calls
- `JointPart.cs`, `BeamPart.cs`, `ServoPart.cs`, `PistonPart.cs`, `SpringPart.cs`,
  `WheelPart.cs`, `SensorPart.cs`, `HatchPart.cs` — one visual per part kind
- `KnockoutPart.cs` — the arena background around a beam or joint, under the
  whole creature (#818)
- `CreatureLayers.cs`, `ArenaLayers.cs` — the named draw layers
- `ArenaVisibility.cs` — the visibility layers that split Training's
  shadows from the rest of the arena (#818)
- `ViewLayer.cs` — an underlay or overlay a screen draws its own marks on,
  between the parts' layers

## What does NOT live here

- UI styling and controls → `project/src/ui/`
- Physics bodies and runtime state → `project/src/creature/`
