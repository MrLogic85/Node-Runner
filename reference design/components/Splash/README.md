The first screen, shown while the app loads. It is the Cover artwork, unchanged and centred on a screen frame, with a **Loading** label and a progress bar under it. There is no second drawing of the logo: the Cover and the Splash are one piece of art in two places.

- **Art.** The same SVG as Cover (`bg`, `muted`, `accent`, `halo`, `line-strong` and `danger` tokens, the display and body families). The blocks bleed above and below it, so the art is not clipped to its own box.
- **Loading bar.** The one progress bar of the kit (`c_prog`), `w-well` wide, filled with `accent`. It shows real progress; when the load time is not known, hold it at the last value rather than looping.
- **Behaviour.** Shows on launch, then goes straight to Creations. It is not tappable and has no Back.

**Godot.** A scene `Splash` holds the scene `CoverArt` (the art, one scene shared with the store cover) and the `ProgressBar` used everywhere else. Do not redraw the art in the splash.
