# Code review prompts

This file owns the review process and its focus areas. It does **not** own
architecture, implementation, testing, or product rules. Review agents must
read the documents named by their focus and evaluate the diff against those
sources instead of inventing stricter rules.

## How to run

1. Select every focus area relevant to the diff.
2. Dispatch each selected section as a separate parallel review agent.
   Use `code-review` for code/process focus areas and `design-lead` for the
   "Visual & UX design" focus area.
3. Give every agent the repository path and exact diff or commit range.
4. Include the selected section body verbatim.

If the review scope is not stated, ask before dispatching.

Every agent must:

- Read root `AGENTS.md`, the nearest local `AGENTS.md` files, and the
  authoritative documents named in its prompt.
- Stay within the supplied diff. Mention preexisting problems separately;
  they do not block the current change.
- Report only actionable correctness, maintainability, or process problems.
  Do not turn preferences into findings.
- Classify findings as **Major**, **Medium**, or **Minor**, mark each as
  **new** or **preexisting**, and include file, lines, evidence, impact, and a
  suggested direction. Do not implement fixes.
- Return "No findings" when nothing clears that bar.

New findings block commit and push until fixed or explicitly dismissed with a
written reason. `AGENTS.md` owns that gate.

---

## Visual & UX design

Review UI-touching changes for whether they are attractive, intuitive, and
close enough to the current design reference for the issue.

Use the `design-lead` custom agent, not the generic `code-review` agent.
Give it access to the user-facing result, not only the diff. Prefer a
connected Android phone for UI review. If no phone is available, the reviewer
may start and use an Android emulator/AVD when the local environment has one
configured. Otherwise provide fresh screenshots/recordings from the target
device/form factor with the exact screens and interactions under review.
Include the design reference path and the issue/PR goal in the prompt. If
neither live app access nor visual evidence is available, the design review
must report **insufficient evidence** instead of guessing from code.
**Insufficient evidence is a blocking, non-passing review outcome** for this
focus area: the gate is not satisfied until the author supplies live app
access, fresh screenshots/recordings, or records an explicit human waiver in
the PR.

Authoritative sources:

- `docs/UI_DIRECTION.md` for product feel, visual language, theme boundaries,
  accessibility/readability rules, and non-goals
- `docs/UI_IMPLEMENTATION_PLAN.md` and
  `docs/UI_COMPONENTS_AND_FLOW.md` for staged UI rollout and screen flow
- `docs/MANUAL_TESTING.md` for device/screenshot evidence expectations
- The nearest `project/src/**/AGENTS.md` files for UI layering constraints

Optional supplied evidence:

- `claude_design_example_design/`, screenshots, recordings, or other design
  references attached to the issue/PR when they are the active look-and-feel
  input. These are review inputs, not durable repository authority; if the
  reference is not in the clone, attach or link it in the PR.

Judge the experience, not just the code. Check whether the screen/control:

- Looks intentional and polished enough for the current milestone
- Feels intuitive on Android touch: clear affordances, no dead controls,
  readable labels, and sensible primary/secondary actions
- Follows the design example's layout, spacing, contrast, rhythm, corner
  radius, dividers, glow, and visual hierarchy closely enough without becoming
  pixel-perfect
- Preserves Node Runner's neon learning-lab identity and the issue's teaching
  goal
- Uses state indicators that do not rely on color alone
- Avoids unplanned future UI or generic developer-dashboard chrome
- Has screenshot/manual-test evidence when the change is visible, including
  before/after or design-reference comparison when that is what the issue is
  trying to improve

Do not block on personal taste, exact pixel matching, or missing design-system
tokens unless the result is visibly inconsistent, confusing, inaccessible, or
contradicts the agreed design direction. Report findings in the same format as
other review sections: **Major**, **Medium**, or **Minor**, **new** or
**preexisting**, with screen/file, evidence, impact, and a concrete suggested
direction.

---

## Documentation

Review documentation changed or made stale by the diff.

Authoritative sources:

- `docs/CODE_DESIGN_PRINCIPLES.md` for documentation and commenting standards
- `docs/ARCHITECTURE.md` for the current system shape
- `docs/ROADMAP.md`, `docs/ML_CONCEPTS.md`, and `docs/GLOSSARY.md` for their
  respective subject matter
- Root and local `AGENTS.md` files for agent instructions

Check that statements, links, commands, examples, diagrams, and ownership
pointers are accurate and durable. Flag duplicated rules whose copies can
drift; each rule should live in its owning document and be referenced
elsewhere.

---

## Structure & readability

Review whether the change remains understandable and belongs in the right
place.

Authoritative sources:

- `docs/ARCHITECTURE.md` for layers and dependencies
- `docs/CODE_DESIGN_PRINCIPLES.md` for code structure, naming, style, and
  rejected patterns
- The nearest local `AGENTS.md` for layer-specific constraints

Trace affected callers and dependencies far enough to verify the change.
Flag unnecessary abstractions, dead paths, confused ownership, or code whose
shape hides its purpose. Do not flag formatting already owned by
`.editorconfig`.

---

## ML correctness & determinism

Review ML behavior, numerical correctness, and reproducibility.

Authoritative sources:

- `docs/CODE_DESIGN_PRINCIPLES.md` for numeric and determinism rules
- `docs/ML_CONCEPTS.md` for intended teaching behavior
- `docs/TEST_STRATEGY.md` for required ML evidence
- `libs/NodeRunner.ML/AGENTS.md` for ML-layer constraints

Work through the relevant math and state transitions rather than relying only
on test names. Check edge cases, shapes, cloning/serialization behavior, and
seeded repeatability where applicable. Require citations only when the design
principles require them.

---

## Concurrency & lifecycle

Review threading, async flow, event ownership, and Godot lifecycle behavior.

Authoritative sources:

- `docs/ARCHITECTURE.md`, especially its threading model
- `docs/CODE_DESIGN_PRINCIPLES.md` for determinism and concurrency choices
- The nearest `project/src/**/AGENTS.md` files for Godot-layer constraints

Follow work across callbacks, signals, tasks, and node enter/exit boundaries.
Flag races, blocking of the Godot main thread, leaked subscriptions/resources,
or lifecycle work that can execute after its owner is gone.

---

## Error handling & resilience

Review failure behavior and invalid-state handling.

Authoritative source:

- `docs/CODE_DESIGN_PRINCIPLES.md` § "Fail loud in dev, gracefully in prod"

Trace both success and failure paths. Flag swallowed errors, misleading
success, lost context, unsafe partial state, or behavior that contradicts the
documented development/release policy.

---

## Security & privacy

Review trust boundaries affected by the diff.

Authoritative sources:

- Root and local `AGENTS.md` files
- `docs/CODE_DESIGN_PRINCIPLES.md`
- The issue's acceptance criteria and platform configuration

Check input, storage, logs, permissions, credentials, and personal data where
relevant. Do not invent web-service threat models for a local Android app or
create requirements absent from the product/design documents.

---

## Test quality

Review whether tests prove the changed behavior at the right layer.

Authoritative sources:

- `docs/TEST_STRATEGY.md`
- `docs/ARCHITECTURE.md`
- The nearest test and production `AGENTS.md` files

Inspect meaningful branches and failure cases, not just coverage. Flag tests
that assert implementation trivia, cannot fail for the intended regression,
or use the wrong test boundary/tool.

---

## Performance & resources

Review user-visible performance and resource ownership where the diff touches
a hot path or long-lived state.

Authoritative sources:

- `docs/CODE_DESIGN_PRINCIPLES.md` for the project's optimization policy
- `docs/ARCHITECTURE.md` for runtime and tick boundaries
- The issue's performance acceptance criteria, if any

Trace allocations, I/O, resource lifetime, and unbounded growth in relevant
paths. Distinguish measured risks from speculative micro-optimization.

---

## Build & tooling

Review CI, project configuration, dependencies, packaging, and developer
tooling.

Authoritative sources:

- `docs/CODE_DESIGN_PRINCIPLES.md` § "Chosen tooling"
- `docs/TEST_STRATEGY.md`
- `docs/REVIEW.md` for CI and merge requirements
- `.github/workflows/`, `Directory.Build.props`,
  `Directory.Packages.props`, project files, and `.editorconfig` for the
  executable configuration

Verify that documented commands and required check names match what actually
runs, configuration works on a clean machine, and dependency/tool changes are
deliberate and compatible with the project.
