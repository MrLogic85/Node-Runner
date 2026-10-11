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
- **Audience:** players, in English. Say what they can now do, not how it
  was built.
- **Focus:** only what the version brings that was not there before, told
  so a player wants to try it: the few biggest new things, not every change
  and not a guide. Renames, polish and small improvements to things that
  already existed stay out.
- **Fixes:** usually left out. Name a fix only when players clearly felt the
  bug, such as a crash or lost creations, in one plain sentence under
  `## Fixed`.
- **Leave out:** issue and PR numbers, PR lists, code names, class or file
  names, internal refactors, tests, CI and tooling.
- **Shape:** a one-line summary, then one `### ` section per new thing, each
  one to three short, plain sentences. No bullet lists. No top-level title:
  GitHub already shows "Node Runner X.Y.Z". A release that players install
  over an older one keeps an `## Install` section and may open with a short
  callout on what carries over.
- **Images:** a landscape screenshot for most sections, showing the new thing
  in use. Use one example creation that shows off the new parts across the
  images; the owner may build it. Take them on a phone or the emulator as
  `docs/MANUAL_TESTING.md` → "Android checks" says, crop the black edge on
  the left, and upload them as GitHub user attachments, so the links are
  absolute and work on the release page. Alt text says what the picture
  shows.
- **Check:** every sentence against the code and against the previous notes,
  so nothing old is sold as new. The design lead agent can draft livelier
  wording.

```markdown
Your creatures can now be trained, saved and picked up again later.

### Pick up where you left off

Save a creature together with what it has learned, and keep training it
another day.

![The Creations list with a trained creature's card](https://github.com/user-attachments/assets/...)
```
