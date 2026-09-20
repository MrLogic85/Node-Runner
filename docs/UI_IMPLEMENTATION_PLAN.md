# UI implementation plan

This document turns the reviewed design direction into an executable plan for
Node Runner. An untracked local directory supplied reference material for
this planning pass, but that material is not part of the repository and is
not required to apply this plan: this tracked document is the durable
implementation contract, and any decision that must persist is captured here
rather than in that ephemeral input. `docs/UI_DIRECTION.md` remains the short
visual compass; this document owns the staged implementation order,
dependencies, and acceptance criteria.

The product principle is simple: the app sells understanding, not creatures.
Every visible control or panel must either teach the causal chain
**Sees -> Decides -> Twists -> Scores** or help the player build, train, save,
resume, or experiment with a creature.

## Target composition

The Android target is landscape-only with a fixed 16:9 composition. The
current Godot project uses a 1280x720 viewport; treat the design spec's
640x360 values as logical design units at 2x until a measured device need
justifies changing project settings.

The stable shell is:

- a quiet top bar with creature identity, one-word run status, mode switch,
  and overflow actions;
- a left arena/build canvas where the creature remains the visual focus;
- a reserved right information surface for SignalFlow, BrainFocus, or
  Creation details;
- a bottom training strip for generation/trial progress and essential
  controls;
- a 48 logical-pixel minimum touch target and no hover-only behavior.

Neon remains the default visual language: dark surfaces, one signal accent,
lit lines, a faint grid, and a warm focus halo. State must never depend on
colour alone.

## Current gap summary

The current behavior is useful but the presentation is still prototype-shaped:

1. Most UI composition and wiring lives in `project/src/Main.cs`, which makes
   the Simulate, Build, Creations, and Edit surfaces difficult to evolve
   independently.
2. Simulate is a text-heavy HUD with raw mapping lines rather than a causal
   arena, generation strip, and SignalFlow.
3. Build uses a horizontal tool row and lacks live motor-relation/brain
   feedback.
4. Edit correctly protects training in code, but hides locked tools instead of
   explaining the Move-only safety model.
5. Creations, duplicate, and delete work, but destructive actions are
   immediate and the list is not yet a card-based, resumable surface.
6. Theme values and sizing are concentrated in code; the design tokens are
   not yet represented by a shared UI adapter.
7. Randomize and Reset are separate visible actions, while the target design
   has one explicit Start over action in an overflow surface.

## Implementation phases

Each phase below is tracked on GitHub for progress and acceptance-criteria
status; the prose here is the durable specification, not a duplicated
checklist.

| Phase | GitHub tracking issue |
|---|---|
| 1. Component kit and visual tokens | [#119](https://github.com/MrLogic85/Node-Runner/issues/119) |
| 2. Static screens and interaction flow | [#120](https://github.com/MrLogic85/Node-Runner/issues/120) |
| 3. Game migration and teaching surfaces | [#121](https://github.com/MrLogic85/Node-Runner/issues/121) (Simulate: #98; Main.cs HUD wiring: #106) |
| 4. Creations and safe actions | [#122](https://github.com/MrLogic85/Node-Runner/issues/122) (durable repository: #93) |
| 5. SignalFlow and BrainFocus (0.9) | [#97](https://github.com/MrLogic85/Node-Runner/issues/97), [#101](https://github.com/MrLogic85/Node-Runner/issues/101) |

### Phase 1 — Component kit and visual tokens

**Goal:** establish the reusable design system before connecting it to the
current game.

- Finish `project/src/ui/lib/` primitives and the canonical token adapter.
- Prove dark, paper, and effects-lite/readable states with sample data.
- Keep controls app-agnostic and independent of simulation, persistence, and
  managers.
- Add component-level screenshot fixtures or a small sample host before any
  game wiring.

**Exit criteria:**

- The component kit renders all documented states with 48px touch targets.
- Theme/effects changes update controls without rebuilding the screen.
- No architecture-test regression and no UI-to-simulation dependency.

### Phase 2 — Static screens and interaction flow

**Goal:** make the complete target flow usable with sample data before game
state is connected.

- Build `SimulateShell`, `BuildScreen`, `EditScreen`, and `CreationsScreen` with
  sample data.
- Build the mode switch, overflow, sheets, toasts, SignalFlow, BrainFocus,
  GenerationStrip, and safe-action transitions.
- Validate the entire touch flow and screenshots without reading `Evolver` or
  `SaveManager`.

**Exit criteria:**

- Every target flow in `docs/UI_COMPONENTS_AND_FLOW.md` is reachable with
  sample data.
- Risky actions have hold-to-confirm/Undo states and every overlay returns to
  its parent mode.
- Android screenshots show the intended 640x360 logical composition without
  relying on current game state.

### Phase 3 — Game migration and teaching surfaces

**Goal:** connect the stable screens to the current game one screen at a time.

- Extract presentation view models/adapters from the current `Main.cs` wiring.
- Migrate Simulate first, then Build/Edit, then Creations and persistence actions.
- Preserve simulation, persistence, and current product decisions while
  replacing the prototype HUD.
- Replace the horizontal tool row with a left rail: Move, Beam, Core, Delete.
  Keep the underlying `ConstructionTool.Place` name temporarily if that
  avoids an unnecessary domain rename.
- Add a right-side Build panel with:
  - `Brain it will get`;
  - input/core and motor-relation counts;
  - one first validation line;
  - a disabled Start training/Complete action with its reason.
- Extend `ConstructionCanvas` using existing topology helpers to show motor
  relations, rigid triangles, and invalid/disconnected states.
- In Edit, keep all tools visible but visibly locked and show
  `Move only · training kept`.
- Keep Rebuild as a separated, named danger action explaining that it creates
  a new body and brain while preserving the original Creation.

**Exit criteria:**

- A first-time player can explain Node, Beam, Core, and Motor relation from
  the Build screen.
- Invalid anatomy is explained before the player attempts to leave Build.
- Edit and Rebuild safety is visible without relying on hidden controls.

### Phase 4 — Creations and safe actions

**Goal:** make persistence feel like a creative workspace, not a file list.

- Replace the current popup list with a Creation card/screen surface:
  thumbnail, name, generation/fitness summary, unlock credit, and clear
  Open/Edit actions.
- Duplicate opens a sheet with `Copy brain` preselected and `Start fresh` as
  the explicit alternative.
- Delete names what will be lost and offers a 10-second Undo toast.
- Show a visible Saved state after autosave/generation persistence.
- Add Restore example only when an example Creation is represented by durable
  data; do not invent a second source of truth.

**Exit criteria:**

- No destructive Creation action is accidental.
- A player can identify which Creation earned an unlock and resume it.
- Duplicate semantics remain compatible with the 0.5 decision that training
  data is copied.

### Phase 5 — SignalFlow and BrainFocus (0.9)

**Goal:** visualize the causal ML loop.

- Replace the raw Mapping panel with four compact stages:
  `Sees`, `Decides`, `Twists`, `Scores`.
- Use bars/dials/cards for live values; raw lists remain available only after
  tapping or expanding a stage.
- Tapping Decides opens BrainFocus: activations colour neurons, weights affect
  edge thickness/opacity, and the view remains readable without printing
  numbers on every node.
- Add the 0.9 settings/compare surfaces only after the basic visualization
  teaches the single-population loop.

## Theme and asset strategy

Adopt the supplied token roles as the naming source for a UI adapter. The
design JSON keys are canonical: `bg`, `panel`, `panel-raised`, `line`,
`line-strong`, `ink`, `muted`, `accent`, `edge`, `accent-soft`,
`accent-glow`, `on-accent`, `halo`, and `danger`, plus spacing, touch, radius,
and stroke roles. C# may expose idiomatic PascalCase properties
(`PanelRaised`, `AccentGlow`, and so on), but each property must map
explicitly to one canonical key; do not create a second token vocabulary.

Do this without adding dependencies or changing project settings:

- expand `VisualTheme` behind the existing neon adapter first;
- move shared panel/button styles out of `Main.cs`;
- add an effects-lite switch before adding more glow or pulse animation;
- defer bundled Chakra Petch, Barlow, and JetBrains Mono fonts until the
  asset/import path is deliberately chosen;
- defer the paper theme until the neon token path is used consistently.

## Copy and interaction rules

- Use plain words first: `Sees`, `Decides`, `Twists`, `Scores`.
- Teach each technical term once where it appears.
- Keep one focus at a time: one expanded card, halo, or hint.
- Use `Start over` rather than separate Randomize and Reset in the polished
  Simulate surface; state what training will be lost and provide Undo where
  practical.
- Use visible locked states and reasons on touch; never rely on tooltips.
- Prefer `Generation 5 · try 3 of 8` and `Reach 50 fitness to unlock` over
  unexplained developer-facing labels.

## Review and verification gates

Every phase touching visible UI follows the merge and Definition of Done
requirements in `docs/REVIEW.md`. In addition to those requirements, UI
phases specifically require:

- the relevant `design-lead` review against this plan and `UI_DIRECTION.md`;
- screenshots for Simulate at rest, mid-generation, Build, Edit, and any
  unlock/confirmation state introduced by the phase;
- no new direct UI dependency on simulation or managers.

The next implementation slice is **Phase 1 component-kit completion**, then
Phase 2 sample-data screens and interaction flow. The earlier GenerationStrip
commit remains a useful data/visual prototype, but it is not the finished
Simulate redesign and must not be treated as a reason to migrate the current
HUD before the target screens are proven.
