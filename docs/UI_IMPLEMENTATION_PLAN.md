# UI implementation plan

This document owns UI implementation order and the gates a UI change passes;
GitHub blocked-by relationships own dependencies. It does not repeat product
behavior, component definitions, token values, or screen inventories from
`reference design/`. Start from `reference design/index.html` for those
contracts.

## Delivery gates

The foundations (exact tokens and font resources) and the Component Library
(every reusable control and state, proven in the Component Gallery) are done.
A product screen is rewritten on top of them, never with private visual copies
or one-off styling.

A rewritten product screen follows "Who owns what" in `docs/UI_DIRECTION.md`
and joins the UI guards in the same PR
([#310](https://github.com/MrLogic85/Node-Runner/issues/310)).

## Order

Product scenes follow the milestone phases in `docs/ROADMAP.md` → "Active
plan"; the GitHub milestones list their issues. The Creation lifecycle
(autosave, one Build screen that is unlocked or locked, the lock on first
training, and unlocking without resetting training) comes before Train setup
and Training. Each milestone that changes UI has a design brief that
`design-lead` reviews before its screens are built. Where the plan replaces
the reference flow, `docs/UI_DIRECTION.md` → "Reference flow overrides"
records it.

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
