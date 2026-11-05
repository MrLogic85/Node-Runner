---
name: render-mockup
description: Render a Node Runner design proposal as one PNG mockup per alternative, in Neon and Paper, from HTML/SVG with the app's tokens. Use when a design choice needs pictures before the human can decide.
---

# Render a mockup

Rules: `docs/UI_DIRECTION.md`. A mockup is a sketch for a decision, not
the app: check a built screen with the `desktop-check` skill instead.

1. `mkdir -p .scratch/design-<topic>`.
2. Write one HTML file per alternative, with Neon and Paper side by side
   at the smallest canvas, 640 × 360, unless the proposal needs another
   ratio (`docs/UI_DIRECTION.md` → "Screen size and safe area"). Take
   every value from the app:
   - colours: the `NodeRunner/colors/<token>` entries in
     `project/assets/themes/Neon.tres` and `Paper.tres`;
   - type: the `Ui<Style>` font sizes and fonts there, with `@font-face`
     rules pointing at `project/assets/fonts/` by absolute `file://` URL;
   - sizes, spacing, radii and panel widths: `UiSize.cs`, `UiSpacing.cs`
     and `UiLayout.cs` in `project/src/ui/lib/`, and the screen scene
     being changed.

   Label each image with the alternative's name and "Sketch". Mark the
   recommended one.
3. Render each file in one shell call. It finds Chrome itself; `CHROME`
   names another browser, `CHROME=none` forces step 4. `size` is the
   whole page's CSS size, both themes and labels included; the PNG is
   twice that, for sharpness.

   ```bash
   dir="$PWD/.scratch/design-<topic>" name=<option> size=<width>,<height>
   chrome="${CHROME:-$(command -v google-chrome chromium | head -1)}"
   mac="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
   if [ -z "$chrome" ] && [ -x "$mac" ]; then chrome="$mac"; fi
   if [ -z "$chrome" ] || [ "$chrome" = none ]; then echo "not rendered: no Chrome"; else
     rm -f "$dir/$name.png"
     "$chrome" --headless=new --disable-gpu --hide-scrollbars \
       --force-device-scale-factor=2 --virtual-time-budget=3000 \
       --window-size="$size" --screenshot="$dir/$name.png" \
       "file://$dir/$name.html" 2>"$dir/$name.log" && test -s "$dir/$name.png" \
       && echo "rendered: $dir/$name.png" || echo "not rendered: see $dir/$name.log"
   fi
   ```

   Look at each image. Fix the HTML and render again until it shows what
   you mean.
4. It prints "not rendered" and the log shows no fix: report the HTML
   files instead.
5. Delete everything in the folder but the files you report, and reply:

   ```text
   Mockups<, not rendered>: <paths>. Recommended: <alternative>. Delete `.scratch/design-<topic>` once the human has seen them or they are posted to the issue.
   ```
