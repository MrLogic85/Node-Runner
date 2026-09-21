The whole app is a hub-and-spoke around **Creations**, the home menu.

- **Creations → + New → Build → Save → Creation.** Build is only for new, unsaved work. Saving locks the parts for good.
- **Creations → Edit (or the card) → Creation.** Copy and Delete are actions on the card; Copy yields an identical creation with its trained model intact, Delete asks once and offers Undo.
- **Creation → Stats, Brain, or Train / Resume → Train setup → Start → Training.** Train setup is where shadows, run length and map are chosen. Training runs on the chosen map.
- **Creations → Achievements** (the trophy). Achievements unlock new parts for Build and new maps for Train setup; a locked map sends you here.
- **Creations → overflow → Settings.** UI size, theme (Neon, Paper or Use phone) and sounds. Back returns to Creations.
- **Build → Brain setup** (the Brain chip in the top bar) sets hidden layers (1 to 3) and neurons per layer (1 to 100). It is available only before Save.
- **Back** always returns exactly one step, never to Creations from deeper screens; Training's Back returns to Creation, and leaves training paused and saved. There is no other navigation bar, and no mode switch.

Every screen's top bar has the same shape: Back, then the title (the creation's name, editable on Creation and Build), spacer, and at most two icons plus overflow.