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
- **Focus:** what the version brings that was not there before, told so a
  player wants to try it. Not a complete guide, and not a list of every
  change.
- **Shape:** a one-line summary, then a short `### ` section of one to three
  plain sentences for each of the few biggest new things, and a `## Fixed`
  paragraph if anything was fixed. No bullet lists. No top-level title:
  GitHub already shows "Node Runner X.Y.Z". A release that players install
  over an older one keeps an `## Install` section and may open with a short
  callout on what carries over.
- **Images:** a screenshot for the biggest sections, taken on the emulator
  and uploaded as a GitHub user attachment, so the link is absolute and
  works on the release page. Alt text says what the picture shows.

```markdown
Your creatures can now be trained, saved and picked up again later.

### Pick up where you left off

Save a creature together with what it has learned, and keep training it
another day.

![The Creations list with a trained creature's card](https://github.com/user-attachments/assets/...)

## Fixed

The camera no longer shakes when a creature vibrates.
```
