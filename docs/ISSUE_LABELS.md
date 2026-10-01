# Issue labels and project fields

Issue metadata lives in three places, each owning different things:

- **Labels** own what an issue *is*: its type and the areas it touches.
- **Project fields** in the
  [Node Runner project](https://github.com/users/MrLogic85/projects/2) own
  where an issue *stands*: Status, Priority and Size.
- **GitHub itself** owns the rest: milestones for the roadmap version,
  sub-issues for parent/child, and blocked-by relationships for dependencies.

Do **not** encode type, priority, or area in the issue title with prefixes like
`[BUG]` or `[FEAT]`; titles should describe the problem or desired outcome.

## Required metadata

Every open GitHub Issue must have:

- Exactly one `type: ...` label
- At least one `area: ...` label
- A **Priority** and a **Status** in the project
- A **Size** once it is a shaped leaf issue (see "Size")

Use a milestone for roadmap version (`0.1.0`, `1.0.0`, etc.) instead of version
labels. Leave the milestone empty for backlog work.

## Setting project fields

New issues are added to the project automatically. Read and set the fields
with `.github/scripts/issue-fields.sh`:

```bash
.github/scripts/issue-fields.sh 286                       # print Status, Priority, Size
.github/scripts/issue-fields.sh 286 --status Ready
.github/scripts/issue-fields.sh 286 --priority P1 --size 3
.github/scripts/issue-fields.sh 286 --size none           # clear a field
```

The script uses the caller's `gh` login, whose token needs the `project`
scope (`gh auth refresh -h github.com -s project`). In the web UI the same
fields are in the issue sidebar under the project.

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

## Priority

Project field, one value. Priority measures **delivery risk**, including product behavior,
correctness, CI/review reliability, release safety, and workflow stability.
Roadmap features do not automatically outrank process or correctness work: if
the way we build, test, review, or merge is unsafe, fixing that is higher
priority than adding more feature code on top.

- **P0** — Fix now. Broken build/demo, data loss, unusable core flow,
  or broken required delivery gate that prevents safe work.
- **P1** — Blocks safe progress toward the next roadmap version. Use
  for roadmap-critical features **and** process/correctness work that protects
  CI, review, branch protection, release, architecture boundaries, or issue
  quality.
- **P2** — Important for the next version or for maintainability, but
  safe work can continue with a clear workaround or without accumulating
  serious risk.
- **P3** — Nice-to-have, polish, or backlog

## Area labels

Use one or more:

- `area: ml` — Neural networks, GA, backprop, math, determinism
- `area: domain` — Pure domain records, invariants, serialization types
- `area: app` — View-models, repositories, services
- `area: creature` — Creature nodes, beams, cores
- `area: sim` — Population orchestration, evolver loop, scoring
- `area: physics` — Godot physics behavior and tuning
- `area: ui` — Screens, controls, presentation logic
- `area: visualization` — Network/training/creature visualization
- `area: audio` — Sound effects, music, audio settings
- `area: android` — Export, install, permissions, device behavior
- `area: ci` — GitHub Actions, branch protection, required checks
- `area: docs` — Documentation structure/content
- `area: repo` — Repository process, labels, issues, PR conventions

## Size

Project field, a number in story points. Set it once the issue is shaped;
leave it empty on **Idea** issues and `type: question` issues. To see the
total per milestone, group a table view by Milestone and turn on the Size sum.

- **1** — Simple fix, about a oneliner
- **2** — Easy fix, touches maybe one or two files
- **3** — Medium sized, touches several files, but implementation is
  straightforward
- **5** — Large refactor, but manageable. Requires complex thinking
- **8** — This is too big to manage, split into smaller parts

A Size 8 issue is split into sub-issues before implementation starts. It
then becomes a parent: clear its Size and estimate only the sub-issues.
Parent issues have no Size.

## Status

Project field, one value. `docs/ISSUE_REVIEW.md` owns how an issue moves
between them.

- **Idea** — Unshaped idea; needs discussion and refinement
- **Needs review** — Shaped; review before implementation
- **Needs design** — Waiting for a new or revised design in
  `reference design/`
- **Needs decision** — Design/product choice required before work starts
- **Blocked** — Waiting on another issue, or an external tool or person.
  Record the blocking issue as a GitHub blocked-by relationship too.
- **Ready** — Reviewed and actionable
- **In progress** — Work has started but is not complete. Set it when you
  start.
- **Done** — Closed. Set automatically when the issue closes.

## Optional labels

Use sparingly:

- `good-first` — Small, well-bounded issue with low architectural risk
- `maintenance` — Cleanup that prevents drift but does not change behavior

Avoid labels that duplicate GitHub state (`open`, `closed`), milestones
(`0.1.0`, `1.0.0`) or project fields (status, priority, size).

## Migration from file issues

When migrating a file issue to GitHub:

- File `type` maps to the matching `type: ...` label. Legacy file issues may
  use `bug`, `feature`, `chore`, `refactor`, `docs`, `test`, `question`, or
  `spike`.
- `priority: p0..p3` maps to the matching Priority field value.
- File status maps as follows:
  - `open` → open GitHub Issue with Status **Needs review**
  - `in-progress` → open GitHub Issue with Status **In progress**
  - `blocked` → open GitHub Issue with Status **Blocked**
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
  - `maintenance` and `good-first` keep their optional labels; legacy
    `pedagogical` is dropped
- If no legacy label maps to an area, add `area: repo` and document the choice
  in the migrated issue body.
- Drop legacy version labels such as `v0.1` after assigning the milestone.
