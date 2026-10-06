# Issues

GitHub Issues track all work. Issue metadata lives in three places:

- **Labels** say what an issue *is*: its type and the areas it touches.
- **Project fields** in the
  [Node Runner project](https://github.com/users/MrLogic85/projects/2) say
  where it *stands*: Status, Priority and Size.
- **GitHub itself** holds milestones (the roadmap version), sub-issues
  (parent/child) and blocked-by relationships (dependencies).

Titles describe the problem or outcome, with no `[BUG]`-style prefixes.

## Required metadata

Every open issue has:

- Exactly one `type: ...` label
- At least one `area: ...` label
- A **Priority** and a **Status**
- A **Size** once it is a shaped leaf issue (see "Size")

The milestone is the roadmap version; backlog work has none. Labels never
repeat GitHub state, milestones or project fields.

## Setting project fields

New issues join the project automatically. Read and set fields with
`.github/scripts/issue-fields.sh`:

```bash
.github/scripts/issue-fields.sh 286                       # print Status, Priority, Size
.github/scripts/issue-fields.sh 286 --status Ready
.github/scripts/issue-fields.sh 286 --priority Major --size 3
.github/scripts/issue-fields.sh 286 --size none           # clear a field
```

The script uses the caller's `gh` login, whose token needs the `project`
scope (`gh auth refresh -h github.com -s project`).

## Creating an issue

Automation only sets Status **Needs review**. Whoever creates the issue
sets, in the same step:

1. The rest of the "Required metadata". Use **Idea** instead of Needs
   review if the issue is not shaped yet.
2. Its parent as a sub-issue, and every issue it waits on as a GitHub
   **blocked-by** relationship. "Blocked by #N" in the body does not record
   it.

Then check the tracker:

```bash
.github/scripts/issue-audit.sh   # lists open issues that break these rules
```

## Type labels

Exactly one:

- `type: bug` — Something behaves incorrectly
- `type: feature` — New user-visible capability
- `type: chore` — Maintenance, setup, dependency or process work
- `type: refactor` — Internal restructuring without behaviour change
- `type: docs` — Documentation-only change
- `type: test` — Test-only change
- `type: question` — Open decision needing discussion
- `type: epic` — A large outcome tracked through its sub-issues
- `type: spike` — Time-boxed investigation that ends in a decision or
  follow-up issue

CI work is `type: chore` or `type: bug` plus `area: ci`; the PR title type
`ci` is only for the squash commit.

## Area labels

One or more:

- `area: ml` — Neural networks, GA, backprop, math, determinism
- `area: domain` — Pure domain records, invariants, serialization types
- `area: app` — View-models, repositories, services
- `area: creature` — Creature nodes, beams, sensors
- `area: sim` — Population orchestration, evolver loop, scoring
- `area: physics` — Godot physics behaviour and tuning
- `area: ui` — Screens, controls, presentation logic
- `area: visualization` — Network, training and creature visualization
- `area: audio` — Sound effects, music, audio settings
- `area: android` — Export, install, permissions, device behaviour
- `area: ci` — GitHub Actions, branch protection, required checks
- `area: docs` — Documentation structure and content
- `area: repo` — Repository process, labels, issues, PR conventions

## Optional labels

- `good-first` — Small, well-bounded, low architectural risk
- `maintenance` — Cleanup that prevents drift without changing behaviour

## Priority

Priority measures delivery risk; unsafe process or correctness work
outranks features.

- **1 Critical** — Fix now: broken build or demo, data loss, unusable core
  flow, or a broken delivery gate.
- **2 Major** — Blocks safe progress toward the next roadmap version,
  including work that protects CI, review, release, architecture boundaries
  or issue quality.
- **3 Default** — Important, but safe work can continue meanwhile.
- **4 Minor** — Nice-to-have, polish or backlog.

## Size

Story points, set once the issue is shaped. **Idea**, `type: question`,
`type: epic` and parent issues have no Size. A table view grouped by
Milestone with the Size sum shows each milestone's total.

- **1** — About a one-liner
- **2** — Touches one or two files
- **3** — Touches several files, straightforward
- **5** — Large but manageable; needs complex thinking
- **8** — Too big: split it into sub-issues before it can become **Ready**,
  then clear its Size and estimate only the sub-issues

## Status

- **Idea** — Unshaped; not reviewed or implemented. When discussion gives
  it one clear outcome and acceptance criteria, set **Needs review**.
- **Needs review** — Shaped; review it before implementation (see
  "Reviewing an issue").
- **Needs design** — Waits for a `design-lead` proposal that the owner
  approves, recorded on the issue (`reference design/` may guide it but is
  not updated). Then set **Needs review**, or **Ready** if the issue already
  passed review and the design changes neither its scope nor its acceptance
  criteria.
- **Needs decision** — A product or design choice must be settled first.
- **Blocked** — Waits on another issue, recorded as blocked-by. A wait on an
  external tool or person gets its own issue to block on, so
  `issue-audit.sh` can check every Blocked issue. When the last blocker
  closes, set **Ready**.
- **Ready** — Reviewed and actionable, with no open blocked-by issue.
- **In progress** — Set it when you start.
- **Done** — Set automatically when the issue closes.

`issue-audit.sh` flags missing labels and fields, a Size on a parent, a
Blocked issue without an open blocker, a Ready or In progress issue that
still has one, and a parent that breaks "Parent issues".

### Parent issues

A parent issue (one with sub-issues) only tracks them; it is not reviewed
or implemented itself. Its Status follows its open sub-issues:

- **In progress** once any sub-issue is In progress or closed.
- Otherwise **Ready** once every open sub-issue is Ready or Blocked.
- Otherwise the least-shaped of their statuses: Idea, then Needs design,
  Needs decision and Needs review.

A parent has no blocked-by issues: record each wait on the sub-issue that
waits, and when a blocked issue is split, move its blockers to the new
sub-issues.

## Reviewing an issue

Trivial issues can be worked directly. Review an issue before
implementation when it:

- might need splitting;
- affects architecture, persistence, ML behaviour, physics, UI flow, CI or
  release;
- is ambiguous about behaviour or acceptance criteria;
- likely needs manual testing; or
- is in **Needs review** or **Needs decision**.

Give the review agent the issue URL or full text and ask it to check:

- Scope: one clear outcome, or a suggested split
- Acceptance criteria: concrete, testable, not implementation-biased
- Metadata: labels, fields, milestone and linked docs follow this file
- Dependencies: blocked-by and follow-up relationships are explicit
- Test plan: automated or manual (`docs/MANUAL_TESTING.md`)
- Risk: architecture, data loss, determinism, performance, device
  behaviour, UX surprises
- Missing context: screenshots, logs, seed and settings, reproduction
  steps, design references

Findings are **Major** (do not implement yet), **Medium** (fix before work
starts) or **Minor** (fix when convenient). Post the review summary as an
issue comment; if the review changes the issue, edit its body and say what
changed in the comment.

Each milestone that changes UI has a design brief that `design-lead`
reviews before its screens are built.
