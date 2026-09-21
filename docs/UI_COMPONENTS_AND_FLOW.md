# UI component kit and screen flow

This document is the durable repository summary of `reference design/`. It is
an index and implementation map, not a replacement for the design files. The
component READMEs under `reference design/components/*/README.md` carry the
detailed behavior for each screen/control; implementers must read the relevant
README before changing that surface. When this document and the design package
disagree, treat the design package as the more detailed source and update this
summary.

The app is organized around **Creations**. There is no persistent global
Build/Simulate mode switch in the final target flow.

## Target navigation flow

```text
Creations
  |-- + New -> Build -> Save -> Creation
  |-- card/Edit -> Creation
  |-- Copy/Delete -> toast/sheet -> Creations
  |-- Achievements

Creation
  |-- Train/Resume -> Train setup -> Training
  |-- Brain -> Brain / BrainScale
  |-- Stats
  |-- overflow -> Reset training / Delete creation

Training
  |-- Back -> Creation (paused/saved)
  |-- Brain -> Brain / BrainScale
  |-- Stats
```

Back always returns exactly one step. Training's Back returns to Creation and
leaves training paused/saved. Overlays and sheets are not navigation
destinations.

## Layout model

- Android phone, landscape only, touch only.
- Fixed 640 x 360 logical canvas scaled to device; never reflow.
- Screen edge inset: 8px (`space-2`) plus safe area/cutout handling.
- Top bar: 48px high, same shape everywhere: Back, title, spacer, at most two
  icons, overflow.
- Left area: arena/build canvas.
- Right panel: fixed 168-176px, exactly one panel state visible.
- Build and Creation also have a 56px left tool rail.
- Minimum touch target: `touch` (48px).

## Spacing and component rules

Use the reference design spacing tokens as named rules, not ad-hoc pixel
values. In Godot these names live in `UiSpacing` and should be preferred over
literal margins/separations when composing screens.

| Rule | Token | Value | Use |
| --- | --- | ---: | --- |
| `IconLabelGap` | `space-1` | 4px | Icon-to-label gaps, dense label/value pairs, compact rows inside a card. |
| `ControlGap` | `space-2` | 8px | Gap between sibling controls in a row; screen edge inset; tight panel content. |
| `PanelPadding` | `space-3` | 12px | Default inner padding for right panels, sheets, menus, and cards. |
| `PanelGap` | `space-4` | 16px | Gap between major panels/cards and between sections inside a sheet. |
| `TouchTarget` | `touch` | 48px | Minimum tappable width/height for buttons and interactive rows. |
| `ControlHorizontalPadding` | `space-3` | 12px | Left/right content inset for labelled buttons and segmented options. |
| `ControlVerticalPadding` | `space-2` | 8px | Top/bottom content inset for labelled buttons and segmented options. |
| `FocusRingGap` | `space-1` | 4px | Clear gap between a control edge and its outer focus ring. |

`UiLayout` may expose fixed shell dimensions such as top-bar height and panel
width, but reference spacing values such as screen inset and touch target come
from `UiSpacing` so screens have one vocabulary for margins and gaps.

Component completion means the primitive owns its typography, radius, minimum
touch target, disabled/locked state, and focus/selection border. Screens should
compose components and view-model state; they should not hand-style every
button, chip, slider, or settings row. When a screen needs repeated structure,
extract a reusable `lib/` control if it is app-agnostic, or a `widgets/`
control if it uses Node Runner vocabulary.

Labelled controls are content-sized by default: their text plus the shared
content insets determines their width, equivalent to Android `wrap_content`.
Use horizontal `ExpandFill` only when the reference layout intentionally
shares or fills available width. A visible focus state uses a separate outer
accent ring and must not replace the normal border or change the control's
content geometry.

## Global primitives

Reusable, app-agnostic controls live in `project/src/ui/lib/`.

| Component | Purpose |
| --- | --- |
| Panel/surface | Token-backed `panel`/`panel-raised` surface with `edge` outline |
| Action button | Primary, secondary, danger, disabled, locked-with-reason actions |
| Icon button | 48px icon target with visible focus and label/accessible name |
| Segmented switch | Train/Simulate switch used on Train setup |
| Tool button | Icon + label rail controls, active/idle/locked/dashed states |
| Sheet | Modal surface for risky actions and focused setup |
| Toast | Saved, copied, undo, and unlock feedback |
| Readout | Monospace changing numbers |
| Overflow menu | Rare actions and destructive actions |
| Slider/stepper | Touch-safe numeric input with live value and exact entry path |
| Chip | Status, lock, unlock, reward, brain-shape, and tag labels |

Controls must not rely on hover or color alone. Disabled controls need a
nearby reason.

## Screens and widgets

### Creations

Home hub. Horizontally scrolling cards, three across. Cards show live
thumbnail, name, one-line stats, optional achievement progress, and Copy/Edit/
Delete actions. + New opens Build. Trophy opens Achievements.

### Build

Only for a new unsaved Creation. Left rail: Move, Beam, Select. Parts tray:
Node, Core, Motor, locked Spring. Brain chip opens Brain setup. Save locks
anatomy and opens Creation. One right panel state at a time: tray, part
settings, or multi-selection.

### Creation

Saved Creation with anatomy locked. Move/Select work; Beam is visible but
locked/dashed; no Delete. Right panel alternates between training summary and
actions, editable non-structural part settings, or multi-selection movement
explanation. Name is editable. Train/Resume opens Train setup; Stats and Brain
open their screens.

### Part settings

Right-panel editor for selected node, beam, core, motor, or spring. Name
first; then main setting, connections, read-only facts. Structural settings
lock after Save. Build includes Delete in the header; Creation omits it.

### Brain setup

Available only before Save. Hidden layers: 1, 2, or 3. Neurons per layer:
1-100. Preview shows senses/outputs from the placed parts and connection
count. Shape locks after Save.

### Train setup

Opened only from Creation. Choices: Shadows (1-32), Run length (5-60s), and
Map. Train/Simulate segmented switch and Start primary. Locked maps open
Achievements.

### Training

Arena-first run screen. Shows leader shadow, faded other shadows, ruler,
best marker, camera follow, bottom GenerationStrip, Pause/Speed, top bar
status, Brain/Stats icons, and achievement progress line. Same screen can
run saved brain in Simulate mode without learning.

### GenerationStrip

One cell per shadow with live distance bar. Leader has accent border and ▲.
Caption says `Generation N · S of T s`; done state briefly shows best result.

### SignalFlow

Right-column causal chain: **1 Senses, 2 Brain, 3 Motors/Outputs, 4 Distance**.
At rest each card is one picture. Tapping expands one card and collapses the
others. Tapping Brain opens Brain.

### Brain / BrainScale

Explains the neural network. Small networks draw neurons/links. Large networks
use square grids and bundled bands. Focused neurons show strongest positive
and negative weights using thickness plus solid/dashed shape, not color alone.

### Stats

Per-map training summary: generations, best distance, time trained, and chart
of best vs average distance. Detail appears on tap.

### Achievements

Player-wide unlocks for parts and maps. Cards show progress/check, goal, and
reward chip. Creations trophy badge clears when opened. Unlock toasts link here.

### Overlays and toasts

Reset training uses hold-to-confirm and names what is lost. Delete asks once.
Both offer 10s Undo. Copy shows toast with Open. Unlock toast is non-blocking.

### Themes

Neon/dark is default. Paper theme and effects-lite use the same token names,
not layout forks. Effects-lite removes glow while preserving fills, borders,
labels, hatches, and other non-color state cues.

## Vocabulary contract

- **Creation:** saved thing the player builds/trains.
- **Beam:** rigid rod between nodes, unlimited in Build.
- **Node:** hinge/pivot point where beams meet.
- **Core:** sensor package on a node; the eyes, never the brain.
- **Motor:** output actuator on a hinge that lets the brain twist a joint.
- **Spring:** unlockable part that pulls back on its own.
- **Shadow:** one ghost copy racing during training.

The durable causal sentence is: cores sense, the brain decides, motors and
other outputs move the body, and distance is the score.

## Current implementation status

The current app has partially migrated Build and Simulate surfaces, but it
does not yet implement the final hub-and-spoke flow. The existing mode switch,
legacy HUD wiring, old Creations popup, and text-heavy mapping surfaces are
transitional. Follow `docs/UI_IMPLEMENTATION_PLAN.md` and the milestone issues
for the implementation order.
