A **wing** is a beam that gives lift on one side. It is a part you drag from the **Beams and links** tab of the Parts tray onto an existing beam (limited in number, "2 left"), not a separate shape. It keeps the beam's two nodes, so it still counts as a beam for rigidity and triangles. Converting a beam from its settings is a fair alternative if dragging turns out too fiddly; both end up the same thing.

**Definitions.** The node on the left is **start**, the one on the right is **end**. Start to end is the beam's direction, and the **normal** is that direction turned a quarter turn: for a beam that runs left to right the normal points up. The normal is the lift direction and is drawn as a dashed `accent` arrow with a lit plate on that side, so the lift side reads in grayscale too. S and E are labelled on the joints.

**Two sources of lift**, both applied at the centre of the wing (a `halo` dot marks it):

- **Gliding.** Lift whenever the speed along the beam is not zero. The sign does not matter, so moving end to start lifts the same way as start to end.
- **Flapping.** Lift when the velocity along the normal is negative, that is when the wing moves down against its lift side. Moving up gives none.

**Settings** (see PartSettings): Name, the beam it is on (locked after Save), **Flip** (swaps start and end and so turns the normal over), Lift strength and Weight. A wing needs no power.

**Placement.** While a wing is dragged over a beam the beam is ringed in `halo` with "Drop on a beam". A beam holds one wing.