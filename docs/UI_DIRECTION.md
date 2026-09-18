# UI direction

Node Runner's default visual direction is a neon sci-fi learning lab: dark
arena, glowing nodes, bright signal paths, and readable console-like controls.
This document is a lightweight compass for 0.1.0. It should guide decisions
without pretending we already know the final app design.

## Product feel

- **Neon simulator:** the app feels like a digital petri dish for synthetic
  life — more Tron/circuit lab than cute toy.
- **Learning by watching:** visuals and controls should help the user connect
  cause and effect: sensors → brain → muscles → movement.
- **Low ceremony:** opening the app should quickly show something alive on
  screen.
- **Experiment-first:** the user should be able to change one thing and see
  what happened.

## Visual language

- Use a near-black or dark-navy base with one primary neon accent.
- Use glowing nodes, thin energy lines, grid/circuit hints, and high-contrast
  readouts to reinforce the "Node Runner" identity.
- Creature anatomy should read as connected nodes and signals, not as a
  hardcoded cartoon skin.
- Motion can use subtle pulses or signal traces, but should never obscure the
  physics or ML concept being taught.

## 0.1.0 screen concept

0.1.0 has one main screen:

- The simulation is the center of attention.
- The creature and ground are visible without setup.
- A tiny HUD overlays or sits beside the simulation.
- HUD controls are limited to what the 0.1.0 loop needs:
  - **Randomize** — create a new random brain once GitHub Issue #13 lands.
  - **Seed/log text** — show enough state to reproduce behavior.

Avoid turning 0.1.0 into a generic developer dashboard. If a control does not
teach an ML/simulation concept or help the user run the 0.1.0 loop, leave it
out.

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

Do not build future UI before its issue, but avoid choices that make inspection
panels, graph views, editor tools, or saved-creature views hard later. The
current roadmap owns feature sequencing; this document only says the main
screen should be able to evolve into simulation + inspection layouts without
rewriting creature/simulation layers.

## Rules for UI changes

- Prefer one clear screen over navigation until there is a second real mode.
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

## 0.2.0 selection feedback

Selected anatomy uses a theme-provided, warm halo with no pulse animation.
The halo is paired with the selected part's existing shape (circle or line),
so selection does not rely on color alone. Keep selection state outside
visual nodes; visuals only render the selected state they receive.

## Non-goals for now

- Pixel-perfect mockups.
- A complete design system or theme token set.
- Responsive layouts for every future mode.
- Final navigation architecture.
- Polished onboarding/tutorial flows.
- Settings menus beyond what the current issue needs.
