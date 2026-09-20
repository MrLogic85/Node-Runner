# UI component kit and screen flow

This is the design-first implementation contract for the supplied design
specification. It deliberately comes before adapting the current game HUD.
The existing simulation and persistence remain behind the future screen
contracts until the kit and flow are stable.

## Component inventory

### Global primitives

These controls are app-agnostic and belong under `project/src/ui/lib/`:

| Component | Purpose | States |
| --- | --- | --- |
| `UiPanel` | Token-backed surface with edge line and optional raised treatment | normal, focused, danger |
| `UiActionButton` | Touch-safe verb action | primary, secondary, danger, disabled, locked |
| `UiIconButton` | 48x48 icon action | normal, focused, disabled |
| `UiSegmentedSwitch` | Simulate/Build mode choice | selected, unselected, disabled |
| `UiToolButton` | Icon + label tool action | active, idle, locked |
| `UiSheet` | Dimmed modal surface for risky/notable moments | open, closing |
| `UiToast` | Temporary saved/undo/unlock feedback | visible, dismissing |
| `UiReadout` | Monospace changing number | normal, emphasis |
| `UiOverflowMenu` | Touch-safe menu for rare and destructive actions | open, dismissed |

All interactive controls expose a minimum 48 logical-pixel hit row, visible
focus, and a text label or lock reason. No state is conveyed by colour alone.

### Domain widgets

These controls belong under `project/src/ui/widgets/`:

| Component | Purpose |
| --- | --- |
| `SimulateShell` | Top bar, arena slot, right panel slot, bottom control slot and inline progression row |
| `SignalFlow` | `1 Sees -> 2 Decides -> 3 Twists -> 4 Scores` cards, one expanded at a time |
| `GenerationStrip` | One cell per trial with Waiting/Current/Done states and profile-driven timing |
| `BrainFocus` | Tapped neural network explanation with named inputs/outputs |
| `BuildCanvas` | Grid, anatomy, motor arcs, rigid hatching, validation marks and first-appearance hints |
| `BuildPanel` | Brain preview, one validation line, Start training |
| `ToolRail` | Move, Beam, Core, Delete with Build/Edit locked states |
| `CreationCard` | Named, resumable saved creature |
| `CreationsScreen` | Cards, empty state, Open/Edit, Duplicate and Delete actions |
| `TrainingSettingsPanel` | Current Quick/Standard/Deep profile control and duration/session explanation |
| `EditSafetyPanel` | Move-only explanation, ghosted old position, generations readout, Done, Rebuild body |

Widgets receive presentation data; they do not read `Evolver`, `SaveManager`,
or other managers directly.

## Target product contract: screen flow

```text
First launch
    |
    v
Build (example anatomy) <----> Simulate (training)
    |                              |
    |                              +--> SignalFlow card expands
    |                              +--> BrainFocus overlay
    |                              +--> TrainingSettings sheet
    |                              +--> Start over sheet -> Undo toast
    |
    +--> Edit (saved trained Creation)
    |       |
    |       +--> Done -> Simulate
    |       +--> Rebuild body sheet -> Build new version -> Simulate
    |
    +--> CreationsScreen
            |
            +--> Open -> Simulate
            +--> Edit -> Edit
            +--> Duplicate sheet -> Copy brain / Start fresh
            +--> Delete hold -> Undo toast
```

There is one persistent mode switch between Simulate and Build. Sheets and
overlays do not become additional modes; they preserve their parent screen
and return to it on completion or cancellation.

Simulate and Build are modes of the same main shell, not separate navigation
destinations. The diagram uses them as named destinations only to make
interactions readable.

## Layout model and implementation status

The target Simulate shell is top bar, left arena, fixed 168px right information
panel, and bottom controls. SignalFlow and BrainFocus own the right panel in
the target shell; the current prototype's bottom inspector is transitional
and must not be copied into the new shell.

The inventory below describes target contracts, not completed features.
Phase 1/2 shell and Simulate work precede Phase 3 Build feedback, Phase 4
Creations safety, and Phase 5 SignalFlow/BrainFocus integration. In
particular, the current implementation still hides Edit tools, uses immediate
Creation actions, and has no finished paper/effects-lite switch.

## Target product contract: interaction contracts

### Simulate

At rest: arena, one-word status, mode switch, overflow, bottom controls,
GenerationStrip, inline unlock progression, and collapsed SignalFlow. Tapping
a body part expands only the matching fixed-width 156px SignalFlow card
(maximum 32px growth; expanded rows scroll when necessary). Tapping Decides
opens BrainFocus. Overflow owns Start over (hold-to-confirm plus ten-second Undo), Training
settings/profile, Restore example when available, and Reset to default. The current
0.8 unlock progress remains visible inline and may also be repeated in the
menu.

### Build

The left rail owns the four tools. `Move` moves existing nodes; Build-only node
placement remains a canvas gesture rather than a fifth tool. Motor relations
appear automatically where geometry creates them, while closed triangles are
hatched and labelled `Rigid: no joints`. Invalid parts are dashed and
danger-marked. The right panel owns brain preview, one first validation line,
and Start training. Autosave presents a Saved cue; there is no Save button.

### Edit

Edit is a safe subset of Build. Move works. Beam, Core, and Delete remain
visible but locked with `Move only · training kept`. Done returns to Simulate.
Rebuild is danger-styled and always opens a sheet explaining that anatomy
creates a new brain and preserves the old Creation as a version.

### Creations

`CreationsScreen` owns the list and empty state. Each card exposes Open/Edit
and a summary of generation and best distance.
Duplicate opens with Copy brain selected and Start fresh as the explicit
alternative. Delete uses hold-to-confirm and a ten-second Undo toast.
These card and safety affordances are Phase 4 targets; the current prototype
still uses immediate actions.

## Migration boundary and build order

### Stage 1: component kit and tokens

Finish the app-agnostic component kit and token adapter, including dark and
paper themes plus effects-lite/reduced-motion behavior.

### Stage 2: screens and interactions with sample data

Build static screen shells and the screen/overlay router. Build Simulate, Build,
Edit, Creations, and overlay interaction contracts against sample data.
Validate screenshots, states, and touch targets before connecting the game.

### Stage 3: game migration

Add presentation view models/adapters that translate current domain/sim state
into the widget contracts. Replace the current `Main.cs` HUD and simulation
wiring one screen at a time, preserving behavior at every migration step.

The concrete phase gates and acceptance criteria are owned by
`docs/UI_IMPLEMENTATION_PLAN.md`; this document defines the contracts that
those phases build toward.

### Current implementation snapshot (2026-09-19)

The current app is still a programmatic `Main.cs` prototype with a text-heavy
HUD, bottom inspector/mapping surface, immediate Creation actions, hidden
Edit tools, and a prototype GenerationStrip. The target flow above is not
implemented yet. The existing strip commit is a data/visual prototype only;
it is not evidence that the Simulate shell or screen flow has been migrated.

The existing `GenerationStrip` is a prototype data visualization and is not
the finished component from this contract until it is placed inside
`SimulateShell` and supports the complete states above.

## Explicit non-goals for the kit phase

- No changes to the simulation algorithm.
- No persistence migration.
- No project viewport or orientation changes.
- No runtime web-font or addon dependency.
- No direct binding from reusable controls to Godot managers.
- No claim that the current HUD has been redesigned.

## Current product decisions carried into the kit

- Training uses the current Quick/Standard/Deep profile model. A settings
  surface may expose the underlying duration, population, generation budget,
  mutation, and crossover explanation, but must preserve profile-change
  restart behavior and next-generation application.
- The 0.8 extra-core unlock is shown inline on Simulate as threshold progress or
  `earned at generation G`; the menu is secondary.
- `Move` is the canonical tool label. Node creation in Build is a canvas
  gesture; Edit exposes only Move.
- SignalFlow and BrainFocus preserve the four-word causal chain
  (**Sees -> Decides -> Twists -> Scores**) and the domain vocabulary in
  `docs/GLOSSARY.md`.
