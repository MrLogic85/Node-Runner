# AGENTS.md — `docs/release-notes/`

One file per published version, `X.Y.Z.md`. `.github/scripts/release.sh`
uses it unchanged as the GitHub release text and refuses to publish without
it. `docs/RELEASING.md` says when and by which PR it is added.

## Writing the notes

An AI agent writes the notes, not a script; the owner approves the text in
the notes PR.

- **Sources:** the milestone's closed issues and the PRs merged since the
  previous release from `main`, minus fixes a fix release has already
  shipped (for a fix release, the fixes on `release/vX.Y`). Check what
  actually changed for the player, not only the titles.
- **Audience:** players, in English. Say what they can now do or will
  notice, not how it was built.
- **Content:** what the version adds and what it fixes.
- **Leave out:** issue and PR numbers, PR lists, code names, class or file
  names, internal refactors, tests, CI and tooling. A change a player cannot
  see does not belong here.
- **Shape:** a one-line summary, then `## What's new` and `## Fixed` as they
  apply. Short, plain sentences, one bullet per change; group small related
  changes into one bullet. No top-level title: GitHub already shows
  "Node Runner X.Y.Z".

```markdown
Your creatures can now be trained, saved and picked up again later.

## What's new
- Save a creature together with what it has learned, and keep training it later.

## Fixed
- The camera no longer shakes when a creature vibrates.
```
