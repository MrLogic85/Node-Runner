**Share build** in a creation's overflow (Build and BuildLocked, after Copy creation, with the share icon) copies the creation's **build** as one line of text, a **share code** (`NR4.…`, about 70 characters for the Walker), to paste in a chat. It confirms with a default notification, "Code copied", which says the code holds the build, not the training. **Import creation** in Creations' overflow (with the paste icon) opens **Import**, which reads such a code, shows the build and adds it. Back returns to Creations and adds nothing.

**The top bar.** Back, the title, **Paste** and **Add to Creations**. The next step is the one primary: Paste until a build shows, then Add to Creations, with Paste secondary. Add is disabled while nothing shows or the name is blank. There is no field for the code: Paste reads the clipboard only when tapped.

**The card** on the left takes the width. Before a paste it says what to do, "Copy a creation's share code, then tap Paste.", centred in `muted`. With a build it shows it on Build's grid, as large as Build at 1:1 and never larger, so what it previews is what Build opens with (a card on Creations stops at half and has no grid). It pans and zooms; nothing is selected.

**The info column** beside it, at least `w-side` wide, shows only with a build:

- **Name**, the build's own name, editable. Two creations may share a name; when the player already has one by that name, a muted line under the field says so ("You already have a Strider."), so they can rename it before adding.
- **One row per kind of part** the build has, in the Parts tray's order: Joints, Beams, Accelerometers, Cameras, Servos, Pistons, Springs. Each is a fact, not a button (`c_fact_row`): the icon, the name in `body`, the count in `readout-sm`, no frame.
- **"Shared creations come untrained."** in `muted`, worded so nobody expects a trained one: the code never holds a brain.

**Add to Creations** saves the build under the name in the field and opens it in Build in place of Import, so Back from Build lands on Creations, where the new card is (the same as an example's Copy). If it cannot be saved, a `danger` notification "Import failed" says "Could not add Strider. Try again." and Import stays.

**Refused.** The card shows a `danger` warn, a title and one line, and the info column hides:

- **Nothing to paste.** "Copy a creation's share code first, then tap Paste." When the clipboard is empty.
- **Not a share code.** "This text is not a Node Runner creation." When any text that does not start like a code.
- **This code is damaged.** "Part of it is missing or changed. Ask for the code again." When it starts right but does not unpack, or breaks a build limit.
- **Made in a newer version.** "Update Node Runner to open this creation." When the code's format is newer than the app.
- **Nothing to build.** "This creation has no parts yet." When the code holds an empty build.

A new paste replaces what the card shows, a build or a refusal.

**Planned, with Achievements.** A code may hold a part the player has not unlocked. Its row then shows a muted lock before the count and a line says "Unlock the Camera to train it."; the build can still be added and edited, and play in Build stays unavailable, with that reason, until the part is unlocked.

**Later.** Opening a code from a link or a QR code (app #1034) and from Android's share sheet (#1035).