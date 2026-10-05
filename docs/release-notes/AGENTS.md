# AGENTS.md — `docs/release-notes/`

One file per published version, `X.Y.Z.md`. `.github/scripts/release.sh`
uses the file, unchanged, as the GitHub release text and refuses to publish
without it. When and by which PR the file is added is in
`docs/RELEASING.md`.

## Writing the notes

An AI agent writes the notes, not a script. The owner approves the text in
the notes PR.

- **Sources:** the milestone's closed issues and the PRs merged since the
  previous release (for a patch, the fixes on `release/vX.Y`). Check what
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
- **First release (0.13.0):** with no earlier release to compare against,
  its notes are a short player guide to the whole app instead: what it is,
  how to install it, a walkthrough of each screen and what is coming
  (owner decision). Later releases use the shape above.

```markdown
Your creatures can now be trained, saved and picked up again later.

## What's new
- Save a creature together with what it has learned, and keep training it later.

## Fixed
- The camera no longer shakes when a creature vibrates.
```
