# Issue labels

GitHub Issues use labels for metadata. Do **not** encode type, priority, or
area in the issue title with prefixes like `[BUG]` or `[FEAT]`; titles should
describe the problem or desired outcome.

## Required labels

Every open GitHub Issue must have:

- Exactly one `type: ...` label
- Exactly one `priority: ...` label
- At least one `area: ...` label

Use a milestone for roadmap version (`v0.1`, `v1.0`, etc.) instead of version
labels. Leave the milestone empty for backlog work.

## Type labels

Use one:

- `type: bug` — Something behaves incorrectly
- `type: feature` — New user-visible capability
- `type: chore` — Maintenance, setup, dependency, or process work
- `type: refactor` — Internal restructuring without behavior change
- `type: docs` — Documentation-only change
- `type: test` — Test-only change
- `type: question` — Open decision needing discussion
- `type: spike` — Time-boxed investigation that should produce a decision or
  follow-up issue

CI-specific work is usually `type: chore` or `type: bug` plus `area: ci`.
Reserve PR title type `ci` for the squash commit format; issue labels keep
type and area separate.

## Priority labels

Use one:

- `priority: p0` — Broken build/demo, data loss, or unusable core flow; fix now
- `priority: p1` — Blocks the next roadmap version
- `priority: p2` — Should be done for the next version but has workarounds
- `priority: p3` — Nice-to-have, polish, or backlog

## Area labels

Use one or more:

- `area: ml` — Neural networks, GA, backprop, math, determinism
- `area: domain` — Pure domain records, invariants, serialization types
- `area: app` — View-models, repositories, services
- `area: creature` — Creature nodes, joints, bones, muscles
- `area: sim` — Population orchestration, evolver loop, scoring
- `area: physics` — Godot physics behavior and tuning
- `area: ui` — Screens, controls, presentation logic
- `area: visualization` — Network/training/creature visualization
- `area: android` — Export, install, permissions, device behavior
- `area: ci` — GitHub Actions, branch protection, required checks
- `area: docs` — Documentation structure/content
- `area: repo` — Repository process, labels, issues, PR conventions

## Optional labels

Use sparingly:

- `status: blocked` — Waiting on an external decision/tool/person
- `status: in-progress` — Work has started but is not complete
- `status: needs-review` — Issue needs review before implementation
- `status: needs-decision` — Design/product choice required before work starts
- `status: ready` — Reviewed and actionable
- `good-first` — Small, well-bounded issue with low architectural risk
- `pedagogical` — Teaching value is central to the issue
- `maintenance` — Cleanup that prevents drift but does not change behavior

Avoid labels that duplicate GitHub state (`open`, `closed`) or milestones
(`v0.1`, `v1.0`).

## Migration from file issues

When migrating a file issue to GitHub:

- File `type` maps to the matching `type: ...` label. Legacy file issues may
  use `bug`, `feature`, `chore`, `refactor`, `docs`, `test`, `question`, or
  `spike`.
- `priority: p0..p3` maps to the matching `priority: ...` label.
- File status maps as follows:
  - `open` → open GitHub Issue with no status label unless review state needs
    one
  - `in-progress` → open GitHub Issue with `status: in-progress`
  - `blocked` → open GitHub Issue with `status: blocked`
  - `closed` → closed GitHub Issue or archived historical issue
  - `wontfix` → closed GitHub Issue with the original reason preserved in the
    body
- Original `created`, `updated`, `closed`, and file `id` values stay in the
  migrated GitHub Issue body or an immutable archive manifest.
- Existing version fields map to milestones.
- Legacy labels map as follows:
  - `ml` → `area: ml`
  - `creature` → `area: creature`
  - `ui` → `area: ui`
  - `android` → `area: android`
  - `physics` → `area: physics`
  - `docs` → `area: docs`
  - `build` or `ci` → `area: ci`
  - `github`, `process`, `review`, or `chore` → `area: repo`
  - `test` → `type: test` when the issue is test-only, otherwise keep the
    original type and add `area: ci` for CI-test infrastructure
  - `pedagogical`, `maintenance`, and `good-first` keep their optional labels
- If no legacy label maps to an area, add `area: repo` and document the choice
  in the migrated issue body.
- Drop legacy version labels such as `v0.1` after assigning the milestone.
