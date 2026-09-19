# UI implementation plan

This document turns the reviewed design direction into an executable plan for
Node Runner. The local `claude_design_example_design/` directory is the
reference material supplied for this planning pass; this tracked document is
the durable implementation contract. `docs/UI_DIRECTION.md` remains the short
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
   the Watch, Build, Creations, and Edit surfaces difficult to evolve
   independently.
2. Watch is a text-heavy HUD with raw mapping lines rather than a causal
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

### Phase 1 — UI shell extraction (next)

**Goal:** make visual work safe without changing behavior.

- Extract the current HUD composition from `Main.cs` into a Watch/Main screen
  composition and small widgets under `project/src/ui/`.
- Introduce widgets for `TopBar`, `ModeSwitch`, `TrainingControls`,
  `BuildToolRail`, and `InfoPanel`.
- Keep existing ViewModels, persistence, simulation, and touch behavior
  intact while moving presentation code.
- Add an App-layer presentation seam where UI currently reads simulation or
  manager state directly. UI must not acquire new dependencies on
  `project/src/sim/` or managers.
- Preserve the current 1280x720 project settings and neon theme during this
  extraction.

**Exit criteria:**

- Watch, Build, and current Creations behavior still work on Android.
- `Main.cs` no longer owns every panel's layout details.
- No architecture-test regression and no new UI-to-simulation dependency.

### Phase 2 — Watch shell and GenerationStrip (first visible redesign)

**Goal:** show what training is doing without making the screen a dashboard.

- Add a fixed Watch shell: top bar, arena, reserved right panel, and bottom
  training strip.
- Expose current candidate index and population size from `Evolver` through a
  presentation-facing state/event; do not let UI inspect private sim fields.
- Render one generation as candidate cells with the current trial highlighted
  and a caption such as `Generation 5 · try 3 of 8`.
- Add the arena ruler, distance trail, and best-distance marker.
- Move detailed Best/Mean/profile/unlock information behind an expanded
  strip or overflow surface; keep only the essential readout visible.
- Replace visible Randomize/Reset with a single Start over surface only after
  equivalent behavior and a safe confirmation/undo path exist.

**Exit criteria:**

- The player can see trial progress before a generation completes.
- Pause, Run, speed, Build, and overflow remain usable while paused.
- Saved Creation resume still shows the correct generation and trial state.
- Android screenshots show no text crowding and no raw sensor log on the main
  Watch surface.

### Phase 3 — Build teaching surface

**Goal:** make anatomy-to-brain structure understandable while building.

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
  Watch surface; state what training will be lost and provide Undo where
  practical.
- Use visible locked states and reasons on touch; never rely on tooltips.
- Prefer `Generation 5 · try 3 of 8` and `Reach 50 fitness to unlock` over
  unexplained developer-facing labels.

## Review and verification gates

Every phase touching visible UI requires:

- the relevant `design-lead` review against this plan and `UI_DIRECTION.md`;
- full build/test/format/diff validation for the smallest affected scope;
- Android export/install/start verification when layout, touch, or device
  behavior changes;
- screenshots for Watch at rest, mid-generation, Build, Edit, and any
  unlock/confirmation state introduced by the phase;
- no new direct UI dependency on simulation or managers;
- a focused commit after review findings are addressed.

The next implementation slice is **Phase 1 plus the minimum Phase 2
GenerationStrip seam**. It is deliberately smaller than a full navigation
rewrite and can be reverted without changing the domain or persistence model.
