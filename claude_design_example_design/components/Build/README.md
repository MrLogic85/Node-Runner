A grid, four tools on a 56px left rail (Move, Beam, Core, Delete), and a slim side panel. The active tool has an `accent` border, wash and its label; the others keep labels too.

**Joints appear as you build.** A motor arc shows at once wherever two beams meet. A closed triangle is hatched and tagged "Rigid: no joints". Only tags for what just changed are shown, never a permanent legend; the first time each thing appears, show a one-line hint at the bottom of the canvas and remember it was seen.

**Live validation.** Invalid parts turn `danger` and dashed, with a warn icon and a two-word tag ("Not connected"). The side panel has exactly three things: the **brain preview** ("3 senses · 4 motors"), one status line (the first problem, or nothing when all is well) and **Start training**, disabled while a problem exists and saying why in that status line. Counts of nodes and beams are not shown.

Autosave is a check and "Saved" under the title. Nothing needs a Save button.