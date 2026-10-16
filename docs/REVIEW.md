# Review process

How a change lands in `main`, for humans and AI agents alike.

## TL;DR

1. Branch from `main` as `<type>/<issue>-<slug>`, with the PR title's type,
   for example `fix/1234-joint-drift`; never push to `main` directly (the
   ruleset rejects it).
2. Run the review agents (see "Review agents") before every commit or
   push, with the `review-change` skill; a push needs its receipt (see
   "Review receipts").
3. Push, open a PR against `main` with the template, and enable squash
   auto-merge, with the `ship-pr` skill.
4. Own the PR until it merges and `main` is green (see "Auto-merge"). The
   PR's closing keyword closes the issue.

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
- [ ] Squash auto-merge enabled (see "Auto-merge")

## Review agents

Each review agent owns one focus and the documents it is judged against.
Its definition lives in `.claude/agents/<name>.md` (see "Agent, rule and
skill files"). This section owns when the agents run, how they report
and how their files are written.

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
| `build-reviewer` | CI, scripts, project, package or editor config, agent, rule or skill files, or hooks |
| `design-lead` | Anything visible: screens, controls, layout, theme or world visuals |

The `review-change` skill runs them. `design-lead` needs live app access
or fresh screenshots or recordings from the target device
(`docs/MANUAL_TESTING.md` → "Android checks"), and the `reference
design/` path when it guides a new surface. Without them it reports
**insufficient evidence** (see "Definition of Done").

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
- Find a C# symbol's definition, callers or implementations with the
  `LSP` tool when the agent has it, since it resolves overloads and `?.`
  calls that a text search misses; otherwise, or when it fails to start,
  use `git grep` (#1352).

The levels rank findings:

- **Major**: wrong behaviour, a wrong statement, or a broken rule in the
  owning documents.
- **Medium**: a risk or gap worth fixing now.
- **Minor**: a small, actionable fix.

The level does not change how a finding gates a commit: "Definition of
Done" treats every new finding alike. Issue reviews use
`issue-reviewer` (`docs/ISSUES.md` → "Reviewing an issue"), which reports
in its own format and finding levels.

### Review receipts

Review runs before the commit, on the change the index stages, measured
from where the branch left `main`; after a commit it runs the same way
with no uncommitted change to a tracked file. When no new finding is
left unaddressed, the `review-change` skill records a receipt for your
own change with `.github/scripts/review-receipt.sh write`, which refuses
while a tracked file has unstaged changes. Untracked files stay out of
the receipt, as they stay out of the commit, so a human's untracked work
need not be staged or moved (#1360). The receipt,
`.git/review-receipts/<key>.md`, holds the date, branch, base and tree,
the reviewers that ran, and each finding with its result or disposition
(#1321).

The key hashes the raw diff (modes, blob ids and paths) from the newest
common ancestor with `main` or `origin/main` to the reviewed tree.
Committing that same content, or rebasing it onto a newer `main` without
touching its files, keeps the receipt valid. Any other change, such as a
fix after a finding, needs the reviewers the corrective change selects to
run again, and then a new receipt. An empty change needs none, and
receipts older than 30 days are pruned.

`guard-bash.sh` denies `git push` of a branch or commit of this
repository unless a receipt matches what it holds. A commit earlier in
the same line is measured from the index, from the working tree's
tracked files with `commit -a` (or `--all`), or from the whole working
tree after `git add -A` (or `--all`, `.` at the top, `:/`).
After a git command earlier in the line that changes what the push sends
in a way the hook cannot measure, the push must be its own command: a
`git add` or `commit` of some paths, or a `merge`, `rebase`, `pull`,
`cherry-pick`, `am`, `revert`, `reset`, `restore`, `rm`, `mv`, `apply`,
`update-ref`, a `branch` that forces, renames or copies, `switch -C`,
`checkout -B`, a `switch -c` or `checkout -b` from a start point, or a
`fetch` into a branch. That list guards against mistakes and is not
complete; the hook measures what the branch holds when it runs, and a
branch created earlier in the line from `HEAD` is measured as `HEAD`.
Tags, deletes, dry runs, `release/vX.Y` branches (`docs/RELEASING.md`)
and a name that is no branch or commit pass. When the check cannot
measure the branch, for example without a `main`, the push is denied
with its reason; when the script is missing, it is allowed.

### Agent, rule and skill files

- Each agent is one `.claude/agents/<name>.md`, which both tools load
  (#1314, #1359). The folder holds nothing else, apart from git-ignored
  `local_*` agents (`AGENTS.md` → "Machine-local setup") and dot-files
  such as Finder's `.DS_Store`, and there is no
  `.github/agents/`: Copilot would load it first and Claude Code not at
  all.
- The frontmatter has only `name` (the file's `<name>`), a one-line
  `description`, `tools` and `hooks`; a reviewer (`*-reviewer`) has
  `tools: Read, Grep, Glob, Bash, LSP` and registers `reviewer-bash.sh`
  and `reviewer-lsp.sh` (see "Hooks") with the block below, and no agent
  sets `model`. A value with `: ` or ` #` is invalid YAML unless quoted;
  prefer ` — `.
- A hook in an agent's frontmatter uses Claude Code's shape: each entry
  holds an inner `hooks` list, with a `matcher` where the event takes
  one. Claude Code drops the whole agent if it finds Copilot's flat shape.
  The LSP tool is `LSP` in Claude Code and `lsp` in Copilot, whose
  matchers are case-sensitive. A reviewer's block:

  ```yaml
  hooks:
    PreToolUse:
      - matcher: "Bash"
        hooks:
          - type: command
            command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/reviewer-bash.sh'
      - matcher: "LSP|lsp"
        hooks:
          - type: command
            command: '"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/reviewer-lsp.sh'
  ```
- The `LSP` tool runs `csharp-ls` through `dotnet tool run`, pinned in
  `dotnet-tools.json`: Copilot reads `.github/lsp.json`, and Claude Code
  the `node-runner-csharp` plugin, which `.claude/settings.json` enables
  from the marketplace in `.claude/marketplace/` (#1352). `LspConfigSpec`
  checks that these agree; `README.md` → "Tools" owns the setup.
- A rule in `.claude/rules/<topic>.md` starts with frontmatter that has
  only `paths`: at least one quoted glob, one per line, each an existing
  file or `<folder>/**` (#1315).
- Each skill is `.claude/skills/<name>/SKILL.md`, which both tools load
  (#1314). Its frontmatter has only `name`, the folder's name in lowercase
  letters, digits and hyphens, at most 64 (a git-ignored `local_*` skill
  keeps its underscore), and a one-line `description` of at most 1024
  characters saying what it does and when to use it, since that is what
  the model matches a task against. Both limits are Claude Code's. A skill
  that the docs name exists.
- `AgentFilesSpec`, `RuleFilesSpec` and `SkillFilesSpec` in
  `NodeRunner.Arch.Tests` check the rules above (#1334, #1315, #1317), so
  `Test & coverage` fails when an agent, rule or skill would load in only
  one tool, or never.
- Each reviewer's file tells it not to edit files.
- Committed agent instructions require only the tools `README.md` →
  "Tools" marks as required; for a recommended tool they name a fallback
  that always exists, such as `git grep` for `rg`.

## Hooks

`.claude/settings.json` registers the scripts in `.claude/hooks/`, and both
tools run them for every agent, not only reviewers (#1320). The exceptions
are `reviewer-bash.sh` and `reviewer-lsp.sh`, which each reviewer's
frontmatter registers (see "Agent, rule and skill files"), so they run only
in reviewers (#1321, #1352). They hold what an instruction alone does not
reliably hold:

| Event | Script | Does |
| --- | --- | --- |
| `SessionStart` | `session-start.sh` | Tells the agent the branch and any uncommitted changes, records the user's running emulators, and in a new session runs `dotnet tool restore` for the `LSP` tool, waiting at most `NODE_RUNNER_RESTORE_WAIT` seconds (10 by default) and saying so when it fails or is still running |
| `PreToolUse` on `Bash` | `guard-bash.sh` | Denies the git commands `AGENTS.md` → "Working copy" forbids, a commit or push to `main`, a push without a review receipt (see "Review receipts"), `gh pr create` without `--title`, and a literal PR title CI would reject in `gh pr create` or `gh pr edit` |
| `PreToolUse` on `Bash`, in reviewers | `reviewer-bash.sh` | Allows only commands that read or build: read-only `git` and `gh` commands, `rg`, `grep`, `ls`, `cat`, `sed -n` with line ranges, `find` without actions, `bash -n`, `shellcheck`, `review-receipt.sh check` and `base`, `dotnet build`, `dotnet test` and similar; denies the rest, and every redirect into a file, with a reason |
| `PreToolUse` on `LSP`, in reviewers | `reviewer-lsp.sh` | Allows only lookups such as `findReferences` and `goToDefinition`; denies the rest, since Copilot's `rename` writes the files |
| `PreToolUse` on edits | `guard-edit.sh` | Denies writes into `/tmp` and `issues/` |
| `Stop` | `turn-end.sh` | Blocks the turn's end once for each emulator or APK in `build/` that appeared during the session |
| `SessionEnd` | `session-end.sh` | Removes the session's state in `.git/agent-hooks/`; a session start also prunes state idle for over two days |

The hooks guard against mistakes, not intent. `guard-bash.sh` reads simple
commands, follows `cd`, `env -C`, `git -C`, `bash -c` and a `git switch` or
`git checkout` of a branch earlier in the line, and allows what it cannot
read: a substitution inside double quotes or arithmetic, a wrapper such
as `xargs` or `timeout`, a title held in a variable, a nested `env -C`, a
detached HEAD or a checkout of `@{-1}`. It assumes an earlier switch
succeeded, even after `||`. `reviewer-bash.sh` reads commands the same
way, and also every substitution, quoted or not, but denies what it does
not know, including `bash -c`, `xargs`, `env`, `awk`, a substitution that
never closes and a program run by a path outside the system folders. It
reads text, not values, so a command hidden in a variable's value and
expanded later, as by `${x@P}` or arithmetic, still runs; it stops
mistakes, not a reviewer set on writing. It trusts `dotnet build`,
`dotnet test` and `dotnet format --verify-no-changes`, which run the
reviewed code, but allows only their options that select, filter or
quiet the run, and it does not hide the environment or readable files.
`design-lead` is not a `*-reviewer` and keeps plain `Bash`, since it
saves screenshots outside the repository.
`guard-edit.sh` sees only the edit tools and only `/tmp` itself, so a
shell redirect into `/tmp` or a write into macOS's `$TMPDIR` passes.
The `Stop` hook reports any emulator the user starts during the
session, including another session's but not one whose path holds a
space, and the APK a release (`docs/RELEASING.md`) leaves in `build/`;
say why it stays.

A hook is a bash script that needs only bash, `git`, `jq` and POSIX
tools, plus a fractional `sleep`, which macOS and GNU accept;
`session-start.sh` also runs `dotnet` when it is installed, and skips it
otherwise. It writes its verdict in both tools' JSON shapes, always
exits 0, and allows the call when `jq` is missing, the reviewer hooks
included, because both tools block the call when a `PreToolUse` hook
fails; `session-start.sh` then says the hooks are off, as it does when it
cannot record the session's state for `Stop`. The settings command is
`"${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"/.claude/hooks/<name>.sh`:
quoted, since the checkout path may hold spaces, and found from the
repository root when `CLAUDE_PROJECT_DIR` is unset, so a session started
in a subfolder still finds it.
`guard-bash.sh` copies CI's PR title rule.

The specs in `NodeRunner.Arch.Tests/Hooks/` run each hook and
`review-receipt.sh` against a throwaway repository, check each script's
event and matcher in `settings.json`, and compare the title rule with
`ci.yml`; `AgentFilesSpec` checks each reviewer's hook block. A `local_*`
hook is registered in `.claude/settings.local.json` (`AGENTS.md` →
"Machine-local setup").

## Merge gates

The `main` ruleset requires a PR, linear history, squash merges and the
checks `PR title`, `Build`, `Test & coverage` and `Format check`; it needs
no approval and has no exceptions for administrators. Branches need not be
up to date with `main`, because auto-merge would otherwise stall whenever
another PR lands first; the CI run on `main` catches the rare semantic
conflict.

## Auto-merge

Every PR enables GitHub's squash auto-merge as soon as it opens (the
`ship-pr` skill has the commands). GitHub owns the wait, so no local
process has to outlive the session. The author still owns the PR while
the session lasts, until it reached `MERGED` with `main` green: fix
failing checks, answer every review comment with a fix or a reason, and
don't resolve threads you didn't address. If auto-merge cannot be
enabled, keep the PR open, report the blocker, and never bypass the
required checks. The one exception is a PR into `release/vX.Y`, which is
merged by hand (`docs/RELEASING.md` → "Fix releases").

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
