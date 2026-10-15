# Review process

How a change lands in `main`, for humans and AI agents alike.

## TL;DR

1. Branch from `main`; never push to `main` directly (the ruleset rejects
   it).
2. Run the review agents (see "Review agents") before every commit or
   push.
3. Push, open a PR against `main` with the template, and enable squash
   auto-merge.
4. Own the PR until it merges: fix failing checks, answer every review
   comment with a fix or a reason, and don't resolve threads you didn't
   address.
5. Confirm the PR merged and `main` is green. The PR's closing keyword
   closes the issue.

A trivial change (typo, comment) still goes through a PR and CI; skip
manual verification and say "trivial" in the PR.

The local review agents review every PR; Copilot code review is not used
(#956). Human review is optional, and `nit:` comments don't block.

## Definition of Done

A PR is mergeable when every box is true, or the PR says why a box does
not apply:

- [ ] A linked GitHub Issue and a closing keyword (release-notes PRs use
      `Part of`; see `docs/RELEASING.md`)
- [ ] Required checks green (see "Merge gates"), and
      `dotnet build NodeRunner.slnx -warnaserror` is clean: CI does not
      fail on warnings in `project/`
- [ ] New logic in `libs/NodeRunner.{Domain,Mechanics,ML}/` has unit tests
- [ ] Review agents (see "Review agents") ran on the final diff, and
      every new finding is fixed or dismissed with a written reason. Each
      preexisting finding has an issue (`docs/ISSUES.md` → "Creating an
      issue"). For a UI-touching change, `design-lead`'s "insufficient
      evidence" result blocks until live app access, screenshots or
      recordings, or a human waiver is recorded in the PR.
- [ ] Docs and the matching `.claude/rules/` files updated where behaviour,
      architecture or a rule changed
- [ ] A change to a saved shape updates `docs/SAVE_FORMAT.md` and the
      schemas in `docs/save-schema/`, and adds the migration and test the
      current stage requires (`docs/SAVE_FORMAT.md` → "Versions and
      migration")
- [ ] A breaking change to a public API is noted in the PR, and a change
      to `AGENTS.md`, `docs/ARCHITECTURE.md` or `docs/TEST_STRATEGY.md`
      has a paragraph in the PR saying why
- [ ] No secrets, credentials or personal data
- [ ] PR title follows "PR title and commit hygiene"
- [ ] Manual testing decided and done per `docs/MANUAL_TESTING.md`
- [ ] Squash auto-merge enabled

## Review agents

Each review agent owns one focus and the documents it is judged against.
Its definition lives in `.github/agents/<name>.agent.md`, and Claude Code
finds the same file through a symlink in `.claude/agents/`. This section
owns when the agents run, how they report and how their files are
written.

### Choosing reviewers

Select every reviewer whose trigger the final diff touches; when unsure,
include it.

| Agent | Selected when the diff touches |
|---|---|
| `docs-reviewer` | Docs, or behaviour, rules or commands that docs describe |
| `structure-reviewer` | Code, scenes or the solution layout |
| `ml-reviewer` | Networks, evolution, scoring, numerics or seeded randomness |
| `concurrency-reviewer` | Threads, tasks, async flow, signals, events or node lifecycle |
| `errors-reviewer` | Failure paths: I/O, parsing, validation, exceptions, invalid state |
| `security-reviewer` | Input, storage, logs, permissions, credentials or personal data |
| `tests-reviewer` | Tests, or logic that needs them |
| `performance-reviewer` | Per-frame or per-tick paths, training loops or long-lived state |
| `build-reviewer` | CI, scripts, project, package or editor config, agent files or hooks |
| `design-lead` | Anything visible: screens, controls, layout, theme or world visuals |

Then:

1. Give each agent the repository path, the exact diff or commit range,
   and the issue goal.
2. Run independent reviews in parallel.
3. Give `design-lead` live app access or fresh screenshots or recordings
   from the target device (`docs/MANUAL_TESTING.md` → "Android checks"),
   and the `reference design/` path when it guides a new surface. Without
   them it reports **insufficient evidence** (see "Definition of Done").

### Reporting

Every review agent must:

- Read root `AGENTS.md`, the `.claude/rules/` files whose `paths` match
  the diff, and the documents its definition names, and judge the diff
  against them without inventing stricter rules.
- Stay within the diff. Mention preexisting problems separately; they do
  not block.
- Report only actionable correctness, maintainability or process
  problems, not preferences.
- Classify each finding by level, mark it **new** or **preexisting**, and
  give file, lines, evidence, impact and a suggested direction.
- Return "No findings" when nothing clears that bar.

The levels rank findings:

- **Major**: wrong behaviour, a wrong statement, or a broken rule in the
  owning documents.
- **Medium**: a risk or gap worth fixing now.
- **Minor**: a small, actionable fix.

The level does not change how a finding gates a commit: "Definition of
Done" treats every new finding alike. Issue reviews use
`issue-reviewer` (`docs/ISSUES.md` → "Reviewing an issue"), which reports
in its own format and finding levels.

### Agent and rule files

- Each agent is one `.github/agents/<name>.agent.md`; `.claude/agents/`
  holds a relative symlink `<name>.md` to it, so both tools load the same
  file (#1314). Neither folder holds anything else, apart from git-ignored
  `local_*` agents (`AGENTS.md` → "Machine-local setup").
- The frontmatter has only `name` (the file's `<name>`), a one-line
  `description`, `tools` and `hooks`; a reviewer (`*-reviewer`) has
  `tools: Read, Grep, Glob, Bash`, and no agent sets `model`. A value
  with `: ` or ` #` is invalid YAML unless quoted; prefer ` — `.
- A hook in an agent's frontmatter uses Claude Code's shape: each entry
  holds an inner `hooks` list, with a `matcher` where the event takes
  one. Claude Code drops the whole agent if it finds Copilot's flat shape.
- A rule in `.claude/rules/<topic>.md` starts with frontmatter that has
  only `paths`: at least one quoted glob, one per line, each an existing
  file or `<folder>/**` (#1315).
- `AgentFilesSpec` and `RuleFilesSpec` in `NodeRunner.Arch.Tests` check the
  rules above (#1334, #1315), so `Test & coverage` fails when an agent or
  rule would load in only one tool, or never.
- Each reviewer's file tells it not to edit files.
- Committed agent instructions require only the tools `README.md` →
  "Tools" marks as required; for a recommended tool they name a fallback
  that always exists, such as `git grep` for `rg`.

## Merge gates

The `main` ruleset requires a PR, linear history, squash merges and the
checks `PR title`, `Build`, `Test & coverage` and `Format check`; it needs
no approval and has no exceptions for administrators. Branches need not be
up to date with `main`, because auto-merge would otherwise stall whenever
another PR lands first; the CI run on `main` catches the rare semantic
conflict.

## Auto-merge

After opening the PR, enable GitHub's squash auto-merge:

```bash
gh pr merge --auto --squash
```

GitHub owns the wait, so no local process has to outlive the session. The
author still watches the checks while the session lasts, fixes failures,
re-runs the review agents the corrective change selects, and confirms
the PR reached `MERGED` with `main` green. If auto-merge cannot be enabled,
keep the PR open, report the blocker, and never bypass the required checks.

## What does NOT block a merge

- Style nits already covered by `.editorconfig`
- A preference between two equally valid designs (raise it once; drop it if
  the author defends it)
- Missing follow-up issues (file them instead)
- No test for compiler-generated record members

## PR title and commit hygiene

- Format: `type(#123): Description`, for example
  `feat(#11): Add feedforward neural network`.
- Types: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `build`, `ci`,
  `perf`, `style`, `revert`.
- The scope is the issue number with `#`; a PR closing several issues lists
  them: `refactor(#497, #498): Description`.
- The description starts with an uppercase letter.
- PRs are squash-merged with the PR title as the commit subject, so commits
  inside a PR may be messy.
- Add a `Co-authored-by:` trailer for every human or agent that contributed
  materially.

## Releases

Publishing a version is not a change to `main`; `docs/RELEASING.md` owns
it, including its PRs on `release/vX.Y`.
