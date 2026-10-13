# AGENTS.md — `src/theme/`

**How the arena and creatures look: theme colours, drawing helpers and the
shared part visuals that Build, Training and thumbnails draw with (#766).**

## Rules

1. **Plain visual state only.** Code here takes geometry, a `VisualTheme`,
   selection and display state. It never depends on physics bodies,
   `PistonLink`, runtime sensors, view-models or screens; the creature and
   UI layers adapt their own state into it.
2. **Draw layers are named.** A part's place in the draw order is its
   `DrawSlot` (or, for a part outside the draw groups, a `CreatureLayers`
   member) passed to `PartVisual`'s constructor; `CreatureLayers.Of` turns
   slot, group and surface into its `ZIndex`. Whatever holds the parts
   calls `PartVisual.Place` on each, with the `RaisedParts` from
   `DrawGroups.Raised`, whenever the shape or selection changes; an
   unplaced part draws over every group. Training's world uses
   `ArenaLayers`. Only `PartVisual` sets a part's `ZIndex`, so the
   selected-surface switch is never bypassed, and nothing here depends on
   the order parts are added. `docs/WORLD_VISUALS.md` → "Draw layers" owns
   the order; `DrawGroups` and `DrawSlot` live in `libs/NodeRunner.Domain`
   so Build's touches rank parts the same way (#1107). `DrawLayersTests`
   checks that layers are passed by name and that every `ZIndex` is a
   named layer within Godot's range.
3. **Selection is drawn with its part.** A part draws its own marks in its
   `_Draw` and rises whole to the selected surface while selected or
   raised. Never draw a part's mark on a separate layer. A Touch sensor's
   part is the cushion `JointPart` draws, and a Rubber's the skin, so
   `JointPart` draws their marks too. Two exceptions:
   - A Wheel's mark is drawn by its joint (`JointPart`), which draws its
     hub, so it lies over the links and the cushion
     (`docs/WORLD_VISUALS.md` → "Selection marks").
   - A selected joint motor's (a Servo's or Velocity motor's), Brake's or
     Angle sensor's Fixed and Target link bands lie along other parts' links, so the Build canvas draws them on
     `CreatureLayers.SelectedUnderlays`, under the selected surface.
4. **Strokes follow the pen rule** in `project/src/ui/lib/AGENTS.md` →
   Drawing (`UiStrokeGuardTests`). `docs/WORLD_VISUALS.md` owns how parts
   look.

## What lives here

- `VisualTheme.cs` — the arena, creature and selection colours
- `JointDrawing.cs`, `PistonDrawing.cs`, `SpringDrawing.cs`,
  `SensorDrawing.cs`, `SelectionDrawing.cs`, `TriangleHatch.cs`, `ServoGeometry.cs`,
  `HubMotorGeometry.cs`, `BrakeGeometry.cs`, `AngleSensorGeometry.cs`,
  `JointMotorDrawing.cs` — static drawing helpers
- `PartVisual.cs` — the base of every part visual: theme, selection and
  layer, and the redraw on a new pixel scale every view that holds parts
  calls, which also redraws any child a part draws on from its `Draw` signal
  (`VelocityMotorPart`'s and `BrakePart`'s turning layers)
- `JointPart.cs`, `BeamPart.cs`, `ServoPart.cs`, `VelocityMotorPart.cs`, `BrakePart.cs`,
  `AngleSensorPart.cs`, `PistonPart.cs`, `SpringPart.cs`, `WheelPart.cs`, `SensorPart.cs`,
  `HatchPart.cs` — one visual per part kind
- `KnockoutPart.cs` — the arena background around a beam or joint, or
  along a path such as an Angle sensor's arc, under the whole creature
  (#818, #1205)
- `CreatureLayers.cs`, `ArenaLayers.cs` — the named draw layers and
  surfaces
- `ArenaVisibility.cs` — the visibility layers that split Training's
  shadows from the rest of the arena (#818)
- `ViewLayer.cs` — a selected underlay or overlay a screen draws its own
  marks on, between the parts' layers
- `PlacingMark.cs` — whether a beam or joint takes or refuses the part being
  placed; the part draws the mark itself, in its own draw slot (#1107)

## What does NOT live here

- UI styling and controls → `project/src/ui/`
- Physics bodies and runtime state → `project/src/creature/`
