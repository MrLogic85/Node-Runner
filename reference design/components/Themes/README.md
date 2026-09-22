Two proofs that the neon skin is a reference, not a commitment.

**Paper** is a second colour theme (`data-theme="paper"`): warm off-white ground, white panels, teal `accent` (#006d77), amber `halo` and brick `danger`, all at 4.5:1 for text and 3:1 for marks on every ground, and the `glow` shadow and `accent-glow` set to none. **Effects lite** (`data-effects="lite"` on any ancestor, defined in bundle.css) keeps the neon colours but turns `glow` and `accent-glow` off, for low-end devices and reduced-motion. Firing neurons, the active tool and the current strip cell keep their fills and borders, so no state is lost when glow disappears.

Implementation: components currently read colours, fonts, radii and strokes from the shared `UiTokens` adapter. Migrating those values to one native Godot `Theme` resource per theme, with automatic propagation, is tracked by #236. Swapping a theme changes shared values, never screen-specific styling. Icons are stroked glyphs using `currentColor`, so they follow the theme.

Every preview in this system pins its own theme on a wrapper element (`data-theme="dark"` for the neon screens, `data-theme="paper"` for the paper frame), so the viewer's light or dark mode never changes what a card shows. In the app, set the theme once on the root node.
