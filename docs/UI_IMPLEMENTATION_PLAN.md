# UI implementation plan

This document turns `reference design/` into the executable implementation
plan for Node Runner. The reference design is now the product UI source of
truth: it is not a loose inspiration folder. Keep the design package tracked
until the app has implemented the described flow, tokens, screens, and
interaction rules. The component READMEs in that package are intentionally
detailed and authoritative for their surfaces; this plan sequences them, it
does not replace them.

`docs/UI_DIRECTION.md` owns the short visual compass. `docs/UI_COMPONENTS_AND_FLOW.md`
owns the durable screen/component inventory. This document owns staged
delivery, dependencies, and GitHub tracking.

## Product target

Node Runner is a landscape-only Android touch app on a 640 x 360 logical
canvas, scaled to device. The product is a neon learning lab that teaches
the causal ML chain:

```text
cores sense -> brain decides -> motors/outputs move -> distance scores
```

Every visible control must either teach that loop or help the player build,
save, train, resume, inspect, or safely manage a Creation.

## Major design changes from the previous prototype direction

- **Creations is the home hub.** The old Build/Simulate mode-switch mental
  model is replaced by hub-and-spoke navigation.
- **Build is only for new unsaved anatomy.** Saving locks structure forever
  so the neural-network shape remains valid.
- **Creation is the saved/editable state.** A saved Creation can be moved,
  renamed, and tuned through non-structural settings, but not structurally
  changed; structural changes require a new Creation.
- **Training is a separate flow.** Creation -> Train setup -> Training.
  Simulating a saved brain uses the same Training screen with learning off.
- **Brain setup happens before Save.** Hidden layers and neurons are chosen in
  Build and locked afterward.
- **SignalFlow and Brain are teaching surfaces, not raw logs.** Raw lists are
  hidden behind taps; at rest the UI is quiet.
- **Achievements unlock parts and maps player-wide.** Locked maps/parts point
  to Achievements.
- **Themes are token-driven.** Neon is default, paper and effects-lite prove
  the theme boundary.

## Milestones and tracking

| Milestone | Purpose | Primary issues |
| --- | --- | --- |
| 0.10.0 | Adopt the new reference design, remove old reference, establish tokens/shell/primitives | #186, #187, #188 |
| 0.11.0 | Implement the core Creation flow: Creations hub, Build, Brain setup, saved Creation, Part settings | #189, #190, #191, #192, #193 |
| 0.12.0 | Implement Training and explanation surfaces: Train setup, Training arena, SignalFlow, Brain/BrainScale, Stats | #194, #195, #196, #197, #198 |
| 0.13.0 | Implement unlocks and polish: Achievements, safe overlays, paper/effects-lite, vocabulary/model alignment | #199, #200, #201, #202 |

Related existing issues have been linked into the new rollout where they fit:
#91, #92, #105, #127, #137, and #175.

## Phase 0 — reference adoption (#186)

**Goal:** make the new design package durable and remove the old design
example.

- Track `reference design/` in the repository.
- Remove `claude_design_example_design/`.
- Update docs and review process to point at `reference design/` as the
  active app target.
- Preserve all supplied README, preview, token, CSS, and JSON files needed for
  future implementation and design review.

**Exit criteria:** a clean clone contains the full current design source, and
no active documentation points to the deleted old design directory.

## Phase 1 — tokens, themes, and shell (#187, #188)

**Goal:** establish the reusable UI foundation before screen migration.

- Map the canonical token names from `reference design/tokens.json` into the
  Godot/C# token adapter:
  `bg`, `panel`, `panel-raised`, `line`, `line-strong`, `ink`, `muted`,
  `accent`, `edge`, `accent-soft`, `accent-glow`, `on-accent`, `halo`,
  `danger`, plus spacing, touch, radius, and stroke roles.
- Implement the fixed 640 x 360 landscape composition scaled to Android.
- Centralize top bar, side panel, left rail, bottom strip, and minimum touch
  target dimensions.
- Build reusable controls for panels, action/icon/tool buttons, segmented
  switches, sheets, toasts, readouts, sliders, chips, lock states, danger
  states, focus rings, and overflow menus.
- Prove controls in component/gallery screenshots on Android phone or emulator.

**Exit criteria:** UI changes can be built from tokenized controls without
duplicating colors, sizes, or one-off panel styles.

## Phase 2 — Creations, Build, and saved Creation (#189-#193)

**Goal:** implement the app's new hub-and-spoke core loop.

- Make Creations the home hub with cards, autosave/Saved cue, Achievements
  entry, and + New.
- Rework Build as the only place that adds/removes anatomy.
- Add the Parts tray, Move/Beam/Select rail, part counts, validation, and
  disabled Save reasons.
- Add Brain setup before Save.
- Add the saved Creation screen with locked structure, editable name, position
  editing, non-structural part settings, training summary/actions,
  Stats/Brain/Train navigation, and overflow actions.
- Add Part settings and multi-selection panels that occupy the single right
  panel slot.

**Exit criteria:** a player can create a new valid Creation, save it, reopen
it from the Creations hub, move it without breaking training compatibility,
and understand why structural edits are locked.

## Phase 3 — Train setup, Training, SignalFlow, Brain, Stats (#194-#198)

**Goal:** make the learning loop visible and explainable.

- Add Train setup with Shadows, Run length, map picker, and Train/Simulate.
- Implement the Training screen: leader shadow, faded other shadows, ruler,
  best marker, camera follow, bottom GenerationStrip, progress caption, and
  top bar achievement progress.
- Implement SignalFlow cards: 1 Senses, 2 Brain, 3 Motors/Outputs, 4 Distance.
- Implement Brain/BrainScale for small and large networks.
- Add Stats per map.

**Exit criteria:** a player can start/resume training, watch simultaneous
shadows run, inspect why the body moved, and see progress without raw log
lists dominating the screen.

## Phase 4 — Achievements, overlays, themes, vocabulary (#199-#202)

**Goal:** complete the shipped design surface.

- Add Achievements and player-wide unlocks for parts and maps.
- Standardize toasts, sheets, Undo, hold-to-confirm, and destructive action
  copy.
- Implement paper theme, effects-lite, and reduced-motion switches on top of
  the shared tokens.
- Align vocabulary and glyphs: Creation, Beam, Node, Core, Motor, Spring,
  hinges, rigid triangles, and locked structural settings.

**Exit criteria:** the app's UI vocabulary, unlock model, safe actions, and
theme behavior match `reference design/`.

## Review and verification gates

Every visible UI issue in this plan requires:

- `design-lead` Visual & UX review per `CODEREVIEW.md`;
- live app access on Android phone first, configured emulator fallback, or
  fresh screenshots/recordings;
- screenshots for every new screen/state introduced by the issue;
- manual-test decision recorded per `docs/MANUAL_TESTING.md`;
- no direct dependency from reusable UI controls to simulation, ML, or
  persistence managers.

If a UI review returns **insufficient evidence**, the issue is not done until
the evidence or a human waiver is recorded.
