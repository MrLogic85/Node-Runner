A **wing** is a beam that gives lift on one side. It is placed like a Spring or a Piston: pick it in the **Links** tab of the Parts tray (limited in number, "2 left"), then drag from one node to another, as with the Beam tool. It is not converted from a beam you already drew; it is its own beam between two nodes, so it still counts as a beam for rigidity and triangles and, unlike a spring, it has a weight.

**Definitions.** The node on the left is **start**, the one on the right is **end**. Start to end is the beam's direction, and the **normal** is that direction turned a quarter turn: for a beam that runs left to right the normal points up. The normal is the lift direction and is drawn as a dashed `accent` arrow with a lit plate on that side, so the lift side reads in grayscale too. S and E are labelled on the joints.

**Two sources of lift**, both applied at the centre of the wing (a `halo` dot marks it):

- **Gliding.** Lift whenever the speed along the beam is not zero. The sign does not matter, so moving end to start lifts the same way as start to end.
- **Flapping.** Lift when the velocity along the normal is negative, that is when the wing moves down against its lift side. Moving up gives none.

**Settings** (see PartSettings): Name, **Flip** (swaps start and end and so turns the normal over), Lift strength and Weight. A wing needs no power. Which two nodes it's between is structure, not a setting — it's drawn on the canvas, the same as its own length would be, and the panel does not repeat it.

**Placement.** While dragging, the start node is ringed in `halo` and a dashed ring follows to the node it would end on. Two nodes hold one wing between them.