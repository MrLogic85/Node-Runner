# Review process

How a change lands in `main`. The `start-issue`, `review-change` and
`ship-pr` skills hold the steps; this document holds the rules.

Every PR is reviewed by the review agents before it is pushed. Human
review is optional, and `nit:` comments don't block.

## Definition of Done

A PR is mergeable when every box is true, or the PR says why a box does
not apply:

- [ ] A linked GitHub Issue and a closing keyword (release-notes PRs use
      `Part of`; `docs/RELEASING.md`)
- [ ] Required checks green (see "Merge gates"), and
      `dotnet build NodeRunner.slnx -warnaserror` clean, since CI does not
      fail on warnings in `project/`
- [ ] Tests as `docs/TEST_STRATEGY.md` asks
- [ ] Review agents ran on the final diff, and the author judged every
      new finding: a Major one is fixed, any other is fixed or dismissed
      with a short reason. Each preexisting finding is fixed here if it
      is simple (`docs/ISSUES.md` → "Creating an issue"); otherwise one of
      Medium or higher has an issue, and a lower one may be noted in the
      PR. For a UI-touching change, `design-lead`'s "insufficient
      evidence" blocks until a rerun reports evidence, or a human waiver
      is recorded in the PR.
- [ ] Docs and the matching `.claude/rules/` files updated where a rule
      changed
- [ ] A change to a saved shape follows `docs/SAVE_FORMAT.md` →
      "Versions and migration"
- [ ] A breaking public API change is noted in the PR, and a change to
      `AGENTS.md`, `docs/ARCHITECTURE.md` or `docs/TEST_STRATEGY.md` has a
      paragraph in the PR saying why
- [ ] No secrets, credentials or personal data
- [ ] Manual testing decided and done per `docs/MANUAL_TESTING.md`

## Choosing reviewers

Each review agent's focus is in `.claude/agents/<name>.md`. Select every
reviewer whose trigger the final diff touches; when unsure, include it.

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
| `build-reviewer` | CI, scripts, project, package or editor config, agent, rule or skill files, or hooks |
| `design-lead` | Anything visible: screens, controls, layout, theme or world visuals |

`design-lead` runs after the other reviewers have finished: it builds and
runs the app, which would overlap their builds and tests.

## Findings format

`AgentFiles.FindingsFormat` in `NodeRunner.Arch.Tests` holds the format,
with what each level means. Each code reviewer's `## Output` copies it
word for word, because agents do not reliably follow a link and fall
back on their own style. `issue-reviewer` and `design-lead` are no code
reviewers and give their own shape in their `## Output`.

## Review receipts

Review runs on the change as it will be committed, measured from where
the branch left `main`: before the commit on the staged change, after it
with no uncommitted change to a tracked file. Untracked files stay out,
as they stay out of the commit, so a human's untracked work need not be
staged or moved.

When no new finding is left unaddressed,
`.github/scripts/review-receipt.sh write` records a receipt, and
`guard-bash.sh` denies a `git push` of a branch unless a receipt matches
what it holds. Committing the reviewed change, or rebasing it onto a
newer `main` without touching its files, keeps the receipt valid; any
other change needs a new one. After a simple, local fix, such as a
rename, a reworded line or a test the finding spelled out, the receipt
needs no new review; after a fix that changes behaviour or design, the
reviewers the fix selects run again. The receipt says which applied.

## Agent, rule and skill files

`.claude/agents/docs-reviewer.md` → "Questions" says which file holds
what. Only the copies "Findings format" names repeat another file.

`README.md` → "Documentation" names each doc's audience. A doc for
agents is terse. A doc for humans explains, and links to the agent doc
for a rule rather than restating it. A doc for both is terse, with the
why a human needs; split it only where the two pull against each other,
keeping one owner per topic.

`AgentFilesSpec`, `RuleFilesSpec`, `SkillFilesSpec` and `DocReferencesSpec`
in `NodeRunner.Arch.Tests` check the frontmatter, folders and references
of these files, and say what to fix. Their messages do not say these:

- `design-lead` is no reviewer: it keeps plain `Bash`, since it runs the
  app and writes screenshots and mockups in `.scratch/`, and has `Skill`
  for `desktop-check`, `android-check` and `render-mockup`.
- The `LSP` tool runs `csharp-ls`, pinned in `dotnet-tools.json`, from
  the `node-runner-csharp` plugin that `.claude/settings.json` enables
  from `.claude/marketplace/`; `LspConfigSpec` checks the chain.
- Committed instructions require only the tools `README.md` → "Tools"
  marks as required, and name a fallback for a recommended one, such as
  `git grep` for `rg`.

## Hooks

Hooks hold what an instruction alone does not reliably hold.
`.claude/settings.json` registers the scripts in `.claude/hooks/` for
every agent, except `reviewer-bash.sh` and `reviewer-lsp.sh`, which each
reviewer's frontmatter registers.

Review agents only read: `reviewer-bash.sh` allows only commands that
read or build, and `reviewer-lsp.sh` only lookups, so a review never
changes what it reviews. The hooks guard against mistakes, not intent:
`guard-bash.sh` reads simple commands and allows what it cannot read,
while `reviewer-bash.sh` denies it.

A hook is a bash script that needs only bash, `git`, `jq` and POSIX
tools. It writes its verdict in Claude Code's JSON shape and always
exits 0, allowing the call when `jq` is missing, because exit code 2
denies a `PreToolUse` call and any other failure shows the human a hook
error. The specs in `NodeRunner.Arch.Tests/Hooks/` run each hook against
a throwaway repository and check the registrations.

## Merge gates

The `main` ruleset requires a PR, linear history, squash merges and the
checks `PR title`, `Build`, `Test & coverage` and `Format check`, with no
approval and no exceptions for administrators. Branches need not be up to
date with `main`, or auto-merge would stall whenever another PR lands
first; the CI run on `main` catches the rare semantic conflict.

## Auto-merge

Every PR enables squash auto-merge when it opens. The author does not
wait for the checks or the merge, but still owns the PR: fix failing
checks, and answer every review comment with a fix or a reason; don't
resolve threads you didn't address. If auto-merge cannot be enabled, keep
the PR open, report the blocker, and never bypass the required checks. A
PR into `release/vX.Y` is merged by hand (`docs/RELEASING.md` → "Fix
releases").

## PR title and commit hygiene

- Format: `type(#123): Description`, for example
  `feat(#11): Add feedforward neural network`. A PR closing several
  issues lists them: `refactor(#497, #498): Description`. CI's `PR title`
  check holds the types.
- The description starts with an uppercase letter.
- The squash commit takes the PR title as its subject, so commits inside
  a PR may be messy.
- Add a `Co-authored-by:` trailer for every human or agent that
  contributed materially.
