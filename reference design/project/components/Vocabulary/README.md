The parts of a creation, each with one glyph that never changes between screens (20px grid, 2px stroke, round caps; `line-strong` for passive, `accent` for anything that acts or senses).

**Structure.** **Beam** (rigid rod, `stroke-beam` 3px, drawn, no limit), **Node** (where beams meet; where two or more beams meet it is a **Joint**, and a joint has **angle limits**, a minimum and a maximum) and **Core** (a node with its own built-in sensors: x and y velocity, elevation and tilt). A core is *not* an eye and not the brain.

**On a joint.** A joint is never a motor by itself. It gets a job from one placed part: **Brake** (limits speed, needs no power), **Servo** (holds an angle), **Stepper** (moves in fixed steps), **Velocity motor** (holds a speed) or **Wheel** (only on an empty joint). A placed part guesses its settings, including which beam is **Fixed** and which is the **Target**, and both can be changed (except that a wheel is always the target).

**On a node.** **LOS sensor** (line of sight): 1 to 5 rays, which are the creature's eyes. Sensors are separate units and can be rotated.

**Between two nodes.** **Spring / damper** and **Piston** are placed like beams but do not act as beams, they only limit movement. A piston is a spring that is powered.

**Blocks.** **Battery**, **Engine** and **Fuel tank** are rigid squares with a joint in each corner, as stiff as a triangulated frame.

**Rules.** A node holds a motor or a sensor, never both. A triangle or a block is rigid: no joint inside it, so nothing can be placed there. Powered parts (servo, stepper, velocity motor, LOS sensor, piston) draw from a shared supply; see Power.

The causal chain every screen serves: cores and sensors sense the world, the brain works out what to do, motors and other outputs move the body, the body moves, distance is the score.