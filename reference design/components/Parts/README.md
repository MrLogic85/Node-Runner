Every part of a creation, each with one glyph that never changes (20px grid, 2px stroke, round caps; `line-strong` for passive, `accent` for anything that acts or senses). The tray in Build has the same groups.

**Structure.** **Beam**: a rigid rod, drawn, unlimited. **Node**: where beams meet, made by drawing beams. Where two or more beams meet it is a **Joint**, and a joint has angle limits (see PartSettings).

**On a joint.** **Servo** (holds an angle), **Stepper** (moves in steps), **Velocity motor** (holds a speed), **Brake** (slows the joint, passive) and **Wheel** (only on an empty joint; a motor can then sit on top of it). A joint is never a motor by itself and holds one part. A wheel is a lit ring with a hub, drawn without spokes.

**Sensors.** **Core** (its own sensors: x and y velocity, elevation, tilt) and **LOS sensor** (line of sight, 1 to 5 rays). Both are dropped on a joint, and a joint holds one part, so a joint with a motor cannot also have a sensor.

**Between two nodes.** **Spring / damper**, **Piston** (a powered spring) and **Wing** (a beam with a lift side; see Wing). They are placed the same way: pick one in the tray and drag from one node to another, as with the Beam tool. A spring or damper limits movement and does not act as a beam; a wing does, and still counts for rigidity.

**Blocks.** **Battery**, **Generator** and **Fuel tank** are drawn as objects, not as beams. Each has four fixed joints at its corners; beams are attached to those joints and to nothing else, so blocks never share a joint with each other or with a beam's node. A block moves and **rotates as one piece** (select it and use the rotate handle); its joints go with it and the beams attached to them stretch, as when a node is moved. A block is as rigid as a triangulated frame and has no joint inside it.

**Placing.** The tray shows one line of help per tab. On a joint: drop it, valid joints ring in `halo`, a joint that already holds a part is refused with "One part per joint". Between two nodes: drag from the first node to the second. Block: drag out from the tray, then rotate.

The causal chain every screen serves: cores and sensors sense the world, the brain works out what to do, motors and other outputs move the body, the body moves, distance is the score.