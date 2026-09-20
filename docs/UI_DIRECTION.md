# UI direction

Node Runner's default visual direction is the tracked design package in
`reference design/`: a neon sci-fi learning lab with a dark arena, glowing
nodes, bright signal paths, and readable console-like controls. This document
is the lightweight visual compass; `reference design/` is the detailed source
of truth for how the app should look and behave. The staged implementation
order and acceptance gates live in `docs/UI_IMPLEMENTATION_PLAN.md`; the
component inventory and screen flow live in `docs/UI_COMPONENTS_AND_FLOW.md`.
Use those documents before starting a larger UI slice.

## Product feel

- **Neon simulator:** the app feels like a digital petri dish for synthetic
  life — more Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls should help the user connect
  cause and effect: sensors → model → motor relations → movement.
- **Low ceremony:** opening the app should quickly show something alive on
  screen.
- **Experiment-first:** the user should be able to change one thing and see
  what happened.
- **Understanding over creatures:** every screen must answer "why did it do
  that?". Controls that neither teach nor drive the core loop do not belong.

## Reference design source

`reference design/` is now a committed product artifact, not disposable
inspiration. Its README, component READMEs, previews, tokens, and design JSON
are the authoritative detailed UI source until implemented or deliberately
revised.

Major target decisions from that package:

- Android phone, landscape only, touch only.
- Fixed 640 x 360 logical canvas scaled to device; do not reflow per screen.
- Creations is the hub; Build is only for new unsaved anatomy; saved Creation
  locks structure while allowing position changes and non-structural settings.
- Training is reached through Creation -> Train setup -> Training.
- Brain setup chooses hidden layers/neurons before Save and is locked after
  Save.
- Achievements unlock parts and maps player-wide.
- Neon is default, but paper/effects-lite must be possible through tokens.

## Visual language

- Use a near-black or dark-navy base with one primary neon accent.
- Use glowing nodes, thin energy lines, grid/circuit hints, and high-contrast
  readouts to reinforce the "Node Runner" identity.
- Creature anatomy should read as connected nodes and signals, not as a
  hardcoded cartoon skin.
- Motion can use subtle pulses or signal traces, but should never obscure the
  physics or ML concept being taught.
- Keep screens quiet: one focus at a time, one picture per idea, and details
  on tap rather than text-heavy dashboards.

## Visual fidelity standard

`reference design/` is visually binding on tokens, layout, typography/text
styles, spacing, radius, stroke widths, dividers, glow, component proportions,
and hierarchy. "Not pixel-perfect" only means Godot does not have to reproduce
HTML/CSS rendering artifacts exactly across fonts, rasterization, and device
scaling. It does **not** mean loose inspiration: visible deviations from the
reference must be deliberate, documented in the PR/issue, or fixed before the
milestone is considered done.

## Active screen concept

The active target is the hub-and-spoke flow in `reference design/components/Navigation/README.md`:

- **Creations** is the home hub.
- **Build** is only for new unsaved anatomy.
- **Creation** is the saved/editable state with locked structure.
- **Train setup** configures shadows, run length, map, and Train/Simulate.
- **Training** visualizes the run and teaching surfaces.
- **Achievements** unlock parts and maps player-wide.

Older single-screen HUD, global Build/Simulate mode-switch, inspector, and
mapping-panel concepts are historical prototype behavior. Do not use those
sections of the old roadmap as active UI direction when they conflict with
`reference design/`.

## Theme boundaries

Tron/neon is the reference theme, not a permanent constraint. Implementation
must stay theme-agnostic:

- Do not hardcode colors, glow strengths, fonts, stroke widths, or icon choices
  inside simulation or ML logic.
- Prefer theme resources, style resources, exported visual settings, or small
  adapter classes at the Godot/UI boundary.
- Keep layout and state flow independent from the theme so a later "paper",
  "cartoon", or "minimal debug" theme can replace the neon skin.
- Keep flavor copy outside core logic; names in code should describe behavior,
  not a specific visual skin.

## Reserved future UI areas

Do not build future UI before its issue, but avoid choices that make the
tracked reference flow harder later. The current rollout is staged in GitHub
milestones 0.10.0 through 0.13.0 and in `docs/UI_IMPLEMENTATION_PLAN.md`.

## Rules for UI changes

- Follow the reference Navigation flow; do not reintroduce a global
  Build/Simulate mode switch unless the design source is deliberately revised.
- Keep sim state observable from the simulation/app layers; UI should not own
  training or physics truth.
- Add controls only when they are tied to a concrete issue and verification
  step.
- Use friendly labels over technical jargon unless the UI is explicitly
  teaching that term.
- Follow the per-issue manual testing decision in `docs/MANUAL_TESTING.md`;
  visible controls and layout changes usually need manual testing.
- If a UI choice feels uncertain, choose the smallest reversible version and
  document what would make us change it.

## Accessibility and readability

- Neon-on-dark must still meet readable contrast, especially for HUD text.
- Do not rely on color alone for state; pair color with shape, label, position,
  or motion.
- Glow and pulse effects should be subtle and removable later for reduced
  motion, battery, and low-end Android performance.
- Critical text should stay crisp; avoid heavy bloom on labels and numbers.

## Design review

Node Runner has a `design-lead` custom agent
(`.github/agents/design-lead.agent.md`) that reviews UI-touching changes
against this document and helps scope new screens/controls before
implementation starts. `docs/REVIEW.md`'s Definition of Done owns when it
is required.

## Non-goals for now

- Pixel-perfect mockups.
- Polished onboarding/tutorial flows.
- Sound and accessibility settings beyond the reference package's current
  placeholders.
- Tablet-specific layout beyond the fixed 640 x 360 phone composition.
