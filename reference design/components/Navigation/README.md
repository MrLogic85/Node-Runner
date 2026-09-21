The whole app is a hub-and-spoke around **Creations**, the home menu. **Build and BuildLocked are one scene in two states**: unlocked and locked.

- **Creations → + New → Build.** Build is the unlocked state. Everything autosaves; there is no Save. Its top bar has **Start training**, and an overflow with **Brain setup**, **Power budget** and **Delete**.
- **Build → Start training → Train setup → Training.** When a training session has finished, the creation **locks**. From then on tapping its card opens it in the locked state (BuildLocked).
- **Creations → tap a trained card → BuildLocked** (locked). Its **padlock** opens the Unlock dialog; unlocking returns to Build and deletes the training, after a warning and a hold to confirm. **Copy** a creation first to keep the trained one.
- **BuildLocked → play (bottom of the rail) → Train setup → Training.** Train setup is where Train or Simulate, shadows, run length and map are chosen.
- **BuildLocked → overflow → Stats** and **Power budget.** Tapping the **brain widget** opens the **Brain** view.
- **Creations → Achievements** (the trophy). Achievements unlock new parts for Build and new maps for Train setup.
- **Creations → overflow → Settings.** UI size, theme (Neon, Paper or Use phone) and sounds; nothing to reset there. **Restore example** is in the same menu.
- **Back** returns exactly one step. Training's Back returns to the creation and leaves training saved. There is no navigation bar and no mode switch.

Every top bar has the same shape: Back, then the title (the creation's name, editable in Build and BuildLocked), spacer, and at most two icons plus overflow.