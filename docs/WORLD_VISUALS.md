# World visuals

How the world is drawn in Build, Training and the creation thumbnails.
What a part is and does is in `docs/CREATURE_MODEL.md`; how Build and
Training behave is in `docs/BUILD_MODE.md` and `docs/TRAINING_LOOP.md`;
the UI around the world is in `docs/UI_DIRECTION.md`. Drawing code rules
are in `.claude/rules/theme.md`. Sizes live in each part's geometry
class (`ServoGeometry`, `StepperGeometry` and so on); this doc keeps what
each drawing means and why.

## Lines and zoom

- Zoom scales the whole picture, lines included. Text, the 1 px hairlines
  of the grid and the rigid hatch, the selection frame and handles,
  callouts, the arena's scales and the best marker keep their screen size.
- Lines stay crisp at any zoom or UI size, with a one-device-pixel
  feather.
- Beams are `UiSize.Widget.CreatureBeamWidth` wide; part outlines are
  `stroke-signal`. A stroke width may show a setting: it starts at a
  hairline and its drawing names its maximum.
- A thin mark drawn over links may carry a `background` `stroke-hair`
  knock-out edge each side, so it parts from what it crosses.

## Parts

Colour says who drives a part (`docs/UI_DIRECTION.md` → "Reference token
mapping deviations"): `accent` for what the network drives or reads,
`detail` for passive parts, `line-strong` on `panel` for structure, and
`danger` for an error. A hinge part (`docs/GLOSSARY.md`) stands in for
its joint's ring, or a Wheel's hub, in the same place. A rule that names
only joint motors means Servos, Motors and Steppers alike.

A hinge part missing a side keeps its shape and draws in `danger` with an
upright "!" badge at the screen's upper right; thumbnails draw it the
same. What else it draws depends on the part: a Servo has no housing
without a Fixed link, a Stepper no pawl without a Target side, and an
Angle sensor no arc.

### Joint

A bearing in a cell: a `line-strong` ring of radius
`NodeDef.PlainJointRadius`, a hairline inner ring and a soft tint on an
opaque disc (`halo` when selected, `danger` when loose). A bare loose
joint shows a `danger` ring and cross. A hinge part or Wheel on a loose
joint shows the error in its own `danger` look instead; a cushion or skin
has none, so a loose joint with only those keeps the ring and cross.

Every link runs from hub to hub: it stops `NodeDef.HubRadius` from each
joint's centre, whatever sits on the joint, and ends under the ring or hub
there, so it runs over a Wheel's face and under a ring, hub or hinge part.

### Beam and rigid triangle

A beam is a plain line, `danger` with a "Too short" callout when too short
to train.

Build and the followed creature hatch each rigid triangle with 1 px lines
riding on one of its beams, into the corners. Where the lines would come
closer than `TriangleHatch.MinPixelSpacing` (far zoom, thumbnails) and on
every other shadow, the triangle gets a faint fill of the same density
instead. A triangle a tear has undone loses its hatch.

### Tear

A beam end letting go (`docs/CREATURE_MODEL.md` → Beam → Tearing) is said
once, at the tear, and then the creature is left to show the rest:

- **Torn end:** the beam is drawn out to its tip. On the followed creature
  its last `BeamPart.TornEndLength` flash `danger` while the pulse runs.
- **Pulse:** on the followed creature a `danger` ring leaves the joint
  once and fades as it slows (`TearPulseVisual`). It runs on physics
  ticks, so Pause holds it.
- **Hatch:** on the followed creature an undone triangle's hatch fades
  quickly, before a folding triangle carries its lines out of itself.
- **Free beam:** a beam torn at both ends keeps being drawn as it falls.

### Servo

A housing, a range dial and a horn (`ServoGeometry`):

- the joint's `accent` ring;
- a `panel` housing box with an `accent` outline on the Fixed link, drawn
  over any link. It shortens to a stub when a sensor or a short link needs
  the room, so the Fixed link always shows; a Piston's or Spring's
  telescope starts past it;
- the range, a band inside the ring from end stop to end stop, turning
  with the Fixed link. At 360° the stops meet and a line across the band
  marks them;
- a two-armed horn along the Target link, at the Start position in Build.

A Wheel side has no link, so it points along the Wheel's built rightward
direction and turns with the Wheel. Missing a side, the range band is
placed as if the Target pointed opposite the Fixed link.

### Stepper

A detent wheel and a pawl, like a clock's escapement, with no Servo
housing, range band or horn (`StepperGeometry`, `StepperPart`): a body
disc, an `accent` rim with one V notch per step, an `accent` pawl and a
hub. Notch size scales with the notch count, so 24 notches still read as a
wheel; when a notch would be too small to see (thumbnails, far zoom,
shadows), the rim is a plain circle.

The rim turns with the Fixed side and the pawl with the Target side. Notch
0 lies on the Target side's built direction, so the pawl sits in it as
built. Notches count counter-clockwise on screen, as the mechanics count
them.

In Training it shows the Stepper's state (`StepperLooks`); the part never
reads physics:

- **Hold:** the pawl seated and filled.
- **Step:** the pawl lifts, hollow, and a soft wedge runs from it to the
  notch it steps to, showing the way it turns. Hollow and lifted against
  filled and seated is the step, so no notch is lit (a thicker notch did
  not read on device).

The frequency is not drawn: it shows as the pace of the steps.

### Motor

A hub motor in section (`HubMotorGeometry`): an `accent` ring, six
magnets round its inside, six coils round the centre (six, not nine, so
they stay legible zoomed out) and a centre ring with an axle dot. The
magnets turn with the Target side and the coils with the Fixed side;
nothing else moves, and it has no other live cue. On a Wheel as Target,
its solid magnets sit on the tyre's tubes. A missing side of a Motor or
Brake points to the screen's right.

### Brake

A disc brake (`BrakeGeometry`), a plain joint's size, since its joint
collides as a plain joint, and `detail`, since it is passive. A caliper of
pads turns with the Fixed side and a disc with a hand with the Target
side, so a slip shows as the hand sweeping past the pads. A Locked
Brake's disc is solid, with no hand: one piece.

### Angle sensor

A ring and an arc (`AngleSensorPart`, `AngleSensorGeometry`):

- The ring stands in for the joint's ring, `accent`, which tells it from
  a plain joint even at 0° and in thumbnails.
- The arc runs from the Fixed side's direction to the Target side's, over
  the links, with a knock-out edge. Its ends show the angle the network
  reads (`docs/CREATURE_MODEL.md` → Angle sensor → Reading); which part of
  the ring it covers shows how the sides got there. On an even turn it
  runs counter-clockwise from Fixed to Target, so it fills; on an odd
  turn from Target to Fixed, so it empties. With the sides in line it is
  a dot on an even turn and a full ring on an odd one, so it never jumps.

The arc lies `AngleSensorDef.ArcGap` outside the joint's outermost part,
never on a cushion or skin; a Wheel changes nothing, so the arc runs over
the tyre. The extra room over the selection gap lengthens the arc between
two close sides; a very small angle still shows as a short mark, which is
accepted, since a player adds an Angle sensor to a joint that moves. The
arc is only drawn: no collider, no tap area.

At rest (Build, thumbnails, a trial's first frame) the angle is under a
full turn, so a Target just clockwise of Fixed draws an almost full ring,
and swapping the sides draws the rest: the part shows which side is
Target. Training counts turns from the built pose every physics tick
(`HingePartSides.ContinuousAngle`). Missing a side, it draws no arc.

In Build, no link ring or placing ring round its joint covers the arc:
they sit outside it (`BuildViewModel.RingRadiusAt`).

### Piston

A three-part telescope from joint A to joint B: an outer cylinder, a
middle stage and an `accent` rod, tubes outlined in `accent` on `panel`,
with a cap across the rod at joint B (`TelescopeGeometry`). Each tube
starts past what covers joint A, a Fixed Servo's housing included. The
sizes come from the shortest length only, so as the Piston moves only the
stage and the rod slide, sharing the travel, and nothing shrinks. Even at
the smallest Stroke it reads as three parts.

### Spring

A coilover: a seat plate past each joint's cover, and the Piston's
telescope, passive and narrower to fit inside the coil, in `line-strong`.
A helix winds round it, `detail` front strokes over the telescope and
`line-strong` back strokes under it. With no room between the seats there
is no coil. Passive: `detail`, no `accent`; all `danger` when too short.

- The wire grows with Stiffness, from a hairline to a beam's width; a
  stiffer Spring gets fewer, thicker turns.
- At rest length the turns stack over a third of the seat-to-seat span,
  so preload shows as more or less coil. They are never more than fit wire
  to wire at the Spring's shortest length, so a long Stroke squeezes them
  without overlapping.
- Turns spread and bunch as the Spring moves; their number never changes.

### Wheel

A tyre on a hub, standing in for its joint, with no tint:

- a see-through face: only the tyre band is opaque, so what lies inside
  shows through, and its own links run over it to the hub;
- a `detail` tyre ring whose outer edge is the Radius, and an inner line;
- three `detail` tubes in the tyre band, the rotation cue, a third of a
  turn apart at every Radius, at rotation 0 in Build;
- the hub, while no hinge part sits on the joint, drawn where the joint's
  ring would go, so links end under it and only the hub, not the face,
  covers them.

A loose Wheel is all `danger`. Thumbnails draw it in full.

### Rubber

A solid `detail` skin, `RubberDef.Skin` wide, just outside the joint's
largest drawn part (the tyre on a Wheel's joint), so a Wheel with a
Rubber reads as a thicker tyre. It lies over a cushion and under the
ring, hub or hinge part. It is only drawn. Shadows do not draw it.

### Touch sensor

A cushion, `TouchSensorDef.Cushion` wide, just outside the joint's largest
drawn part. Its colour is the network's reading: `accent-soft` at 0 and in
Build, solid `accent` at 1, blended between, the one world part in
`accent-soft`. It draws first on its joint, so everything else on the
joint may cover it. It is only drawn. Thumbnails draw it at rest.

### Sensors

A beam sensor is a picture of itself on its beam, never a circle, which
would read as a joint: `panel` fill and `accent` lines (`halo` when
selected), upright on the beam's built-up side and turning with it.

- **Accelerometer:** a frame with a spring to a round weight, which shows
  the proof mass the network reads, with 1 g at half its travel. In Build
  it hangs on the same spring as in a run, so nothing jumps when a run
  starts.
- **Camera:** a body, lens ring and hood, looking along its aim. Its rays
  leave from the picture's edge. In Training the followed shadow draws
  only rays that hit the ground, dashed `halo` to a ring at the hit.

## Selection marks

Every mark sits `SelectionMarks.Gap` outside its own part's drawn edge and
over it, so no part's mark moves for another part on the same joint. A
joint with parts on it gets no mark of its own; each selected part marks
itself.

- **Joint, Motor, Brake, Angle sensor:** a `halo` ring round the part (an
  Angle sensor's outside its arc).
- **Beam or link:** two `halo` lines, one on each side, stopping at the
  joint's ring, hinge part or hub. In a group they run on to the selected
  joints, so the group reads as one outline.
- **Sensor:** its lines turn `halo`. **Rubber:** its skin turns `halo`.
  **Touch sensor:** a ring outside the cushion.
- **Wheel:** a ring outside the tyre, one of its joint's bands ("Draw
  layers"), over the links and cushion and under the hub.
- **Servo:** a keyhole outline round ring and housing. **Stepper:** a ring
  with a keyhole round the pawl that turns with it; it cannot follow the
  notched rim, which would fill every notch.
- **Fixed and Target bands:** a hinge part selected alone also marks its
  sides, a hatched `detail` band on the Fixed link and an `accent` band
  on the Target link. A Wheel side's band is a ring under its tyre,
  never hatched; its colour alone tells the side.
- **Link travel:** `halo` ticks at the stops joined by a dashed line; a
  Spring's rest length is a small ring. Every mark sits at its true
  length.
- **Camera rays and aim:** while every selected part is a Camera, each
  shows its rays; one selected alone also shows an Aim handle.
- **Group frame:** a dashed frame with Move, Rotate and Scale handles;
  after a Rotate it stays turned with the group. A box select is dashed,
  filled `halo` at `alpha_soft`, and shows what it would catch as
  selected.

## Draw layers

A creature draws on two surfaces, the unselected parts and over them the
selected ones, which draw alike. On top of both go the overlays (camera
rays, link travel, drag and move lines, the picked link's rings, a
replaced link's outline, the selection frame). Training's knock-out
outline and the rigid hatch sit under both. A selected hinge part's side
bands sit under the selected surface.

Parts draw in groups, one per link (`DrawGroups`), so the drawing is the
same in Build and Training and never changes while the creature moves.
The link whose lower end sits highest on screen draws first, so what is
lower is nearer; ties keep link id order. Within a group: a Wheel's face,
the link, its sensor, then each joint's bands (cushion, skin, halos,
placing marks) and its ring, hub or hinge part. So a link covers a
Wheel's face, and a ring or hub covers its link's end. Hinge parts,
joints, hubs and tyre bands are filled, so nothing shows through them.

The selected surface takes a selected joint or joint part with its links,
a selected link with its ends, every link whose ends both rose (so a
raised hinge part never hides it), and a selected sensor or the sensor on
a raised link. A
raised end lifts its ring, hub or hinge part but not a Wheel's face. While
a part is placed or a sensor moved, the targets that take it rise with
their marks. A touch hits what is drawn on top (`docs/BUILD_MODE.md` →
Interactions).

In Training the best and crash markers and the start sign are behind the
ground, the shadows above it, and the followed creature above every
shadow. The world starts at Godot's lowest `ZIndex` (`ArenaLayers`), so
each creature gets its whole set of layers; links past
`DrawGroups.MaxGroups` share the last group.

## Build canvas

- **Grid:** a `line` grid of half-metre cells covering exactly the build
  area, with `accent` corner marks. It is a blueprint of where joints can
  go, so the Training arena has none.
- **Link drag line:** dashed `halo` over no joint, solid once it will
  attach, dashed `danger` with a crossed ring where the joint would
  refuse. It is all the feedback, since a finger often covers the target.
  A link it will replace is outlined with dashed `line-strong` lines.
- **Picked link:** each joint a drag can start from has a dashed `halo`
  ring (`JointHalo`), dashed as the drag line before it attaches, so it
  never reads as the solid selection ring. Selected joints are left
  unmarked. No ring is `danger` until a drag, since a refusal depends on
  the target.
- **Placing a part:** the glyph rides on a raised tile above the finger.
  Targets that take the part are `halo`, taken ones dashed `danger`; a
  joint's ring has the size the part will have there
  (`PlacingTargets.JointRingOf`), and that ring is where a tap or drop
  lands. The part also shows itself where it would land, a hinge part
  with the sides it would take. A picked part marks the same targets from
  the moment it is picked, so a tap can find them.
- **Moving a sensor:** no tile follows the finger. The sensor stays on its
  beam, drawn as selected, beams are outlined as for a tray drag, and its
  picture shows on a beam that takes it. A drag line ties it to the
  finger, with an arrow ring pointing along the move when it fits.
- **Callouts:** a note out past its part, joined by a leader in its
  colour, at screen size. Near an edge it slides along the edge, never to
  the part's other side. Overlapping callouts stack, most important
  nearest, and every note shows; notes of the same kind and text join
  into one callout with a leader to each part.

## Training arena

- **Background:** plain; motion reads against the scales.
- **Scales:** distance along the view's bottom edge, height up the left,
  behind the creatures, with no grid; what they count is
  `docs/TRAINING_LOOP.md` → Scales. Labels are cut off by the view's
  edges rather than fading, so none blinks as the view shakes.
- **Best and crash markers:** a dashed line up to a flag
  (`ArenaFlagMarker`), "Best 4.2 m", and "Crash 3.4 m" in `danger` one row
  lower, so the two never cover each other. A flag always flies right of
  its line and is cut off at the view's edge; it never changes side. Each
  fades while a part callout shows. When each shows is
  `docs/TRAINING_LOOP.md` → Training scene.
- **Start sign:** an `ink` signpost reading "Start" at 0 m, sized in the
  world, so it zooms with the creatures.
- **Part callout:** each selected part of the followed shadow keeps its
  selection look, and its name shows in a `halo` callout docked at the
  arena top, over the view's middle, where the camera keeps a creature at
  rest. Unlike Build's callouts it never moves; only its leader follows
  the part. The camera keeps a shelf clear for it
  (`docs/TRAINING_LOOP.md` → Camera). Several selected parts share one
  callout, a row each in their stack order.
- **Port bars:** a part with network ports adds its glyph and, under its
  name, a line of its inputs in `accent` and one of its outputs in
  `output`. Each line starts with a muted **IN** or **OUT**, so colour is
  never the only cue. Each port is a label and a `Meter` bar; a −1…1 port
  fills from a tick at 0, a 0…1 port from the left, and values outside
  clamp. A Wheel, Rubber or Brake shows its glyph and "No network
  ports"; a Joint, Beam or Spring shows only its name. The Status panel's
  input bars and output dials take the same colours and fill rule.
- **Slow-motion chip:** a Warning chip, "Too many shadows!", top left.

## Thumbnails

A Creations card draws its creature with the same parts, scaled to fit and
centred, never past half its Build size. Edit-only marks are not shown: no
selection, loose or too-short tint, travel ticks or camera rays.

## Drawing as a shadow

The followed shadow is drawn in full. Every other shadow, the leader
included, is drawn simplified in its normal colours, so up to 100 stay
cheap and their overlaps readable.

- All shadows fade together, once, at `alpha_shadow`, so overlaps do not
  darken into a mass. Each is one flat picture.
- The followed creature has a knock-out outline in the arena background,
  wider than each beam and joint, so it stands clear. It runs past a
  cushion or skin, so they cut the ground line where they rest on it, and
  along an Angle sensor's arc, so the arc never merges into the ground.
- The leader is not marked: the lead changes too often.

How each part draws as a shadow (the rule is `.claude/rules/creature.md`
rule 10):

- **Joint, Angle sensor:** the outer ring only.
- **Servo:** its ring, housing outline, one range arc and one horn line,
  plus the 360° line.
- **Motor:** its outer ring and a circle round the coils; it does not
  turn.
- **Brake:** its outer ring and its disc's ring; it does not turn.
- **Stepper:** a plain rim circle and the pawl's outline, which turns as
  on the followed creature, so each step still shows.
- **Wheel:** the circle at its Radius and its joint's ring.
- **Beam:** its line. **Rigid triangle:** a faint fill.
- **Tear:** no flash or pulse; an undone triangle's fill goes at once.
- **Piston, Spring:** as on the followed creature.
- **Sensors, Touch cushions, Rubber skins and Camera rays:** not drawn.
