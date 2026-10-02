# UI implementation plan

This document owns implementation order and GitHub dependencies. It does not
repeat product behavior, component definitions, token values, or screen
inventories from `reference design/`. Start from `reference design/index.html`
for those contracts.

## Delivery gates and tracking

Do not rewrite a product scene until both foundation gates are complete.
Earlier token, primitive, and scene issues were implemented against an older
export and are useful code, but they do not prove parity with the current
structured reference.

| Gate | Purpose | Primary issues |
| --- | --- | --- |
| A — Foundations | Exact colors, text styles, spacing, sizes, radius, strokes, and font resources | #226 |
| B — Component Library | Complete reusable component inventory and all states, proven in isolation | #227 |
| C — Creation lifecycle | Autosave, single Build unlocked/locked state, first-training lock, Unlock without resetting training | #229, #371 |
| D — Product scenes | Build parts, Train setup/Training, teaching surfaces, progression, settings | #194-#202, #211, #220, per milestone below |

A rewritten product screen follows "Who owns what" in `docs/UI_DIRECTION.md`
and joins the UI guards in the same PR
([#310](https://github.com/MrLogic85/Node-Runner/issues/310)).

Product scenes follow the milestone phases in `docs/ROADMAP.md` → "Active
plan" (#522). Each milestone that changes UI has a design brief (#531, #542,
#549, #600, #552) that `design-lead` reviews before its screens are built. Where the
plan replaces the reference flow, `docs/UI_DIRECTION.md` → "Reference flow
overrides" records it.

## Gate A — foundations (#226)

**Goal:** make `reference design/tokens.json` an exact, testable Godot
foundation.

- Map every dark/paper color, text style, spacing and size, radius, and stroke.
- Use actual font resources for each required weight.
- Add contract tests that fail when mapped values drift.
- Prove foundations in an isolated specimen at the reference logical scale.

**Exit criteria:** every canonical token has one exact named mapping or a
documented non-runtime reason, with passing automated and visual evidence.

## Gate B — Component Library (#227)

**Goal:** implement every reusable control and state in
`reference design/library.md` before screen migration.

- Map every `c_*` entry to one reusable Godot primitive or shared composition.
- Implement all documented default, pressed, focus, selected, disabled,
  locked, danger, and destructive states.
- Put every component and meaningful state in the Component Gallery.
- Verify internal padding, visible geometry, touch target, typography, radius,
  strokes, and state cues against the reference on Android.

**Exit criteria:** screens can be composed without private visual copies or
one-off styling, and design review has no unresolved fidelity findings.

## Build (0.12.0: #211, #202, #220)

Implement expanded parts in #211, vocabulary in #202, and durable part
identity in #220. The Build and BuildLocked component READMEs own visible
behavior.

## Train, save and load (0.13.0: #229, #371, #194, #195, #137)

Implement the persistence and navigation lifecycle in #229, where the lock
only prevents mistakes and unlocking keeps training (#371), and the TrainSetup
and Training contracts with shadows in the arena. The brain (views and editing)
stays hidden until 0.16.0 (#536).

## Later scenes

- Stats (#198): 0.15.0.
- SignalFlow and BrainFocus/BrainScale (#196, #197, #393): 0.16.0.
- Overlays (#200) and Settings/Themes (#201): 0.17.0.
- Achievements (#199): 0.19.0.

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
