# World visuals

How the world is drawn in Build, Training and the creation thumbnails.
What a part is and does is in `docs/CREATURE_MODEL.md`; how Build and
Training behave is in `docs/BUILD_MODE.md` and `docs/TRAINING_LOOP.md`; the
UI around the world is in `docs/UI_DIRECTION.md`. Drawing code rules are in
`project/src/theme/AGENTS.md`.

## Lines and zoom

- Zoom scales the whole picture, lines included (#400). Text, the 1 px
  hairlines of the grid and the rigid hatch, the selection frame and its
  handles (#366), callouts, the arena's scales and the best marker keep their screen
  size.
- Lines stay crisp at any zoom or UI size, with a one-device-pixel feather
  (#733).
- Beams are 6 wide (`UiSize.Widget.CreatureBeamWidth`); part outlines are
  `stroke-signal`. A stroke width may show a setting (#835): it starts at a
  hairline and its drawing names its maximum.
- A thin mark drawn over links may carry a `background` `stroke-hair`
  knock-out edge each side, so it parts from what it crosses (#1205).

## Parts

Every part in a joint's hinge-part slot (`docs/CREATURE_MODEL.md` →
Node → One part per slot), a *joint motor* (`docs/GLOSSARY.md` → Joint
motor), Brake or Angle sensor, stands in for its joint's ring in the same
place. A rule that names only joint motors means Servos and Velocity motors
alike. A Stepper (#1036) has no picture until #1038: its joint keeps its
ring, at the joint motor's radius, and shows the Stepper selected.

### Joint

A bearing in a cell (#626): a `line-strong` `stroke-signal` ring whose
outer edge is `NodeDef.PlainJointRadius`, a `stroke-hair` inner ring at 0.55
of that radius, and an `alpha_soft` tint (`line-strong`, `halo` when
selected, `danger` when loose) on an opaque `background` disc. A bare loose
joint shows a `danger` ring and cross instead of the inner ring. A joint motor,
Brake, Angle sensor or Wheel on a loose joint shows the error in its own `danger` look instead, with
no ring or cross over it (#1140). A Touch sensor's cushion and a Rubber's
skin have no `danger` look, so a loose joint with only those keeps the ring
and cross.

Every link (beam, Piston, Spring) runs from hub to hub (#1086): it stops flat
`NodeDef.HubRadius` (a plain joint's radius) from each joint's centre,
whatever sits on the joint. Its drawing ends under the middle of the ring
or hub ring there, so it runs over a Wheel's face and under a ring, hub,
joint motor, Brake or Angle sensor.

### Beam and rigid triangle

A beam is a plain line. A beam too short to train is `danger`, with a
`danger` "Too short" callout on its upper side.

Build and the followed creature hatch each rigid triangle (#627) with 1 px
lines riding on one of its beams. The lines run into the corners (#1198);
"Draw layers" says what covers them there. Where the lines would come closer than
`TriangleHatch.MinPixelSpacing` (zoomed far out, in a thumbnail) and on
every other shadow, the triangle gets a faint fill of the same density
instead (#770). A triangle a tear has undone loses its hatch
(`docs/CREATURE_MODEL.md` → Rigid triangles; Tear below).

### Tear

How a beam end letting go (`docs/CREATURE_MODEL.md` → Beam → Tearing)
looks in Simulate and Training (#1257); a shadow's tear is under Drawing
as a shadow. It is said once, at the tear, and then the creature is left
to show the rest:

- **Torn end:** with no joint ring over it, the beam is drawn out to its
  tip, and its knock-out outline ends flat there. On the followed creature
  the last `BeamPart.TornEndLength` (8 units) flash `danger` for as long as
  the pulse runs, then turn back to the beam's colour (owner, #1257: the
  tear is said once; a broken beam or motor may get a lasting look later).
- **Pulse:** on the followed creature a `danger` ring leaves the joint the
  end let go of, once per joint, from R + 3 out to R + 19, fading out
  over 0.4 s as it slows (`TearPulseVisual`). It runs on physics ticks, so
  Pause holds it.
- **Hatch:** on the followed creature a rigid triangle the tear undid
  fades out over 0.2 s,
  short enough that a folding triangle does not carry its lines out of
  itself.
- **Free beam:** a beam torn at both ends keeps being drawn as it falls
  (`docs/CREATURE_MODEL.md` → Beam → Tearing).

### Servo

A housing, a range dial and a horn (#452, #577):

- the joint's `accent` ring;
- a `panel` housing box with an `accent` outline on the Fixed link, at most
  8 units past the ring, drawn over any link (#925); a Piston's or Spring's
  telescope starts past it (#1210). It is shortened to a 3-unit stub when a sensor or a short link needs
  the room, so the Fixed link always shows. On a beam near its minimum
  length the ring and stub may cover a sensor picture's ends (#1086,
  #1212);
- the range, a `panel` band with an `accent` outline inside the ring from
  end stop to end stop, turning with the Fixed link. At 360° the stops meet
  and an `accent` line across the band marks them (#922);
- a two-armed horn along the Target link, at the Start position in Build.

A Wheel side (#1118) has no link to point along, so it points along the
Wheel's built rightward direction, turning with the Wheel: the horn points
that way as Target, and as Fixed the housing is only the ring and the range
turns with the Wheel.

A Servo missing a side keeps its shape on its usual fill, but draws all
of it in `danger`, like a loose Wheel, with an upright "!" badge at the
screen's upper right (#1111). It has no housing without a Fixed link, and
only the horn's hub without a Target link. Its range band is placed as
for a complete Servo; without a Target link, as if the Target pointed
opposite the Fixed link, or to the screen's left when it has neither.
Thumbnails draw it as in Build, badge included.

### Motor

A hub motor in section, standing in for the joint's ring (#454). Its parts
are in `HubMotorGeometry`:

- an opaque `panel` disc on a `background` disc, out to the joint motor's
  radius, with an `accent` ring just inside it;
- six magnets round the inside of the ring, alternating solid `accent` and
  `accent` outlines on the `panel`;
- six coils round the centre, each an `accent` stem with a curved cap
  (six, not nine, so they stay legible zoomed out);
- an `accent` centre ring with an axle dot.

The magnets turn with the Target side and the coils with the Fixed side,
each along its link, or with the Wheel as a Wheel side. Nothing else moves,
and it has no other live cue. Build draws it at rest. On a Wheel it stands
in for the hub, and as Target its solid magnets sit on the tyre's tubes.

A Motor missing a side draws in `danger` with the Servo's "!"
badge; a missing side points to the screen's right.

### Brake

A disc brake, standing in for the joint's ring or a Wheel's hub (#1041).
It is a plain joint's size, as its joint collides as a plain joint does,
and `detail`, as it is passive. Its parts are in `BrakeGeometry`:

- an opaque `panel` disc on a `background` disc, with a `detail` ring
  just inside its edge;
- a caliper: four pads either side of the Fixed side;
- a disc: a ring and an axle dot, with a hand along the Target side that
  runs out past the ring toward the pads.

The caliper turns with the Fixed side and the disc with the Target side,
so a slip shows as the hand sweeping past the pads. A Locked Brake's disc
is solid, with no hand: one piece. It has no other live cue.

A Brake missing a side draws in `danger` with the Servo's "!" badge, as a
Motor does.

### Angle sensor

A ring and an arc (#1205), drawn by one part (`AngleSensorPart`; the angles
are in `AngleSensorGeometry`):

- the ring, standing in for the joint's ring or a Wheel's hub (no axle
  dot), as a Brake does: an opaque `panel` disc on a `background` disc
  with a plain joint's ring (outer edge `NodeDef.PlainJointRadius`,
  `stroke-signal`) and inner ring, both `accent`. The `accent` ring tells it
  from a plain joint, even at 0° and in thumbnails;
- the arc, a plain `accent` `stroke-signal` line with round ends, no
  arrowhead or tick, from the Fixed side's direction to the Target side's,
  each end on its side's centre line, over the links, with a knock-out
  edge that parts it from the links it crosses, a Piston's cylinder or a
  beam in its angle, and rims its round ends where they lie on the two
  sides' links. Its ends show the angle the brain reads
  (`docs/CREATURE_MODEL.md` → Angle sensor → Reading); which part of the
  ring it covers shows how the sides got there (#1243). Counting the angle
  on across whole turns, counter-clockwise on screen, on an even turn it
  runs counter-clockwise from the Fixed side to the Target side, so it
  fills, and on an odd turn counter-clockwise from the Target side to the
  Fixed side, so it empties. With the sides in line its ends meet in a dot
  2 across on an even turn and close a full ring on an odd one.

The arc's centre line is `AngleSensorDef.ArcGap` (7: `SelectionMarks.Gap`
plus 4) plus half its width outside the joint's outermost part: 23 on a
bare joint, 26 with a Rubber's skin, 28 with a Touch sensor's cushion,
never on it. The extra 4 lengthens the arc between two close sides (#1230);
below about 10° the arc is still only a short mark, which is accepted: a
player adds an Angle sensor to a joint that moves. Likewise an almost full
ring's gap below about 10° may close to the links' width. A Wheel changes
nothing, so on its joint the arc runs over the tyre's inside and tubes. A
Wheel side points along the Wheel's tube 0, its built rightward direction,
turning with the Wheel (#1118). The arc is only drawn: it adds no collider
and no tap area.

At rest, in Build, thumbnails and each trial's first frame, the angle is
0 to under a full turn, so the arc always runs counter-clockwise from the
Fixed side: a Target side just clockwise of it draws an almost full ring,
and swapping Fixed and Target draws the rest of the ring, so the part shows
which side is Target (#1231). Dragging a node across the Fixed side in
Build switches between the dot and an almost full ring. Training follows
the sides' live directions and counts their turns from the built pose,
starting again with every trial, every physics tick however many a frame
runs (`HingePartSides.ContinuousAngle`). There the arc changes way round
only as a dot or a full ring, so it never jumps. It has no cue for speed.
An Angle sensor missing a side, as on a loose joint (#1140), draws its
ring in `danger` with the Servo's "!" badge and no arc.
Thumbnails draw it as in Build, badge included.

In Build, no link or placing ring round its joint covers the arc (#1206):
a link's rings (Build canvas → Picked link and Link drag line) and a
placing ring, taking or refused (→ Placing a part), sit on its selection
halo (Selection marks) whenever they would lie inside it, counting an
Angle sensor, Rubber or Touch sensor being placed on that joint
(`BuildViewModel.RingRadiusAt`).

### Piston

A three-part telescope from joint A to joint B (#1210): an outer cylinder at
joint A, a middle stage and an `accent` rod 3 wide, running hub to hub. Both
tubes are an `accent` outline (`stroke-signal`) on `panel`, the cylinder half 7
wide and the stage half 4.5. They draw rod, stage, cylinder, then the cap, a
bar 10 across the rod at the edge of what covers joint B (#1086).

`TelescopeGeometry` places the parts; all lengths run from joint A's centre:

- **Cover:** each tube starts at the edge of what covers joint A, and the cap
  sits at the edge of what covers joint B: the joint motor's ring or the hub,
  plus the Servo's housing when the link is that Servo's Fixed link
  (`ServoGeometry.LinkCover`).
- **Sizes:** the cylinder reaches half the travel past joint A's hub, since
  the travel is on the gap between the hubs (#870, #835, #1086), so a cover
  past the hub hides part of it. The stage and the rod each slide out up to
  half the travel, sharing the movement evenly. The sizes come from
  the shortest length only, so as the Piston moves only the stage and the rod
  move, and nothing shrinks.
- **Smallest parts:** a 6 cylinder, a 5 stage lip and 5 of rod, so at Stroke
  10% it still reads as three parts. When the shortest length leaves less
  room, the cylinder gets shorter first; with less than all three, the three
  shrink in proportion.
- **Ends:** no tube or cap draws past joint B's cover; the rod still runs on
  under it. A tube that would end at or before its start is left out. Past
  the stops in Training the rod takes up the difference.

### Spring

A coilover (#807): a seat plate, half 10, just outside what covers each
joint (#1086), and the Piston's three-part telescope (#1210), placed by the same
rule but passive and narrower, to fit inside the coil: a `line-strong` rod 3
wide, and `line-strong` outlines on `panel`, the cylinder half 5 and the stage
half 3. Its smallest parts are a 9 cylinder, a 5 stage lip and 7 of rod, so
they show past the seats. A helix winds round the telescope, `detail` front
strokes over it (#1059) and `line-strong` back strokes under it, all with
round ends. It draws rod, back strokes, stage, cylinder, front strokes, then
the seats; with no room between the seats there is no coil.

- The wire grows with Stiffness, from a hairline at 50 N/m to a beam's
  width at 2000 N/m; a stiffer Spring gets fewer, thicker turns.
- At rest length the turns stack solid over a third of the seat-to-seat
  span, at least 3, and past 2 m grow only with the square root of the span.
  Preload shows as more or less coil. They are never more than fit wire to
  wire, with no gap, between the seats at the Spring's shortest length, so
  a long Stroke squeezes them without overlapping, down to the 3-turn
  minimum (#1211). With no gap the cap binds less, so a 1 m Spring at
  Stroke 200% still shows more turns at a longer Coil length; where it
  binds, as on a short Spring or one with a motor on its joint, a longer
  Coil length can show fewer. Both spans are between the seats as drawn,
  so a Servo's housing leaves fewer turns.
- Turns spread and bunch as the Spring moves; their number never changes.
- Passive: `detail`, no `accent`. All `danger` when too short.

### Wheel

A tyre on a hub, standing in for its joint (#129); the same geometry in
every theme, with no tint:

- a see-through face (#1086): only the tyre band, between the Radius and
  the inner line, is an opaque `background`, so what lies inside the tyre
  shows through, and its own links, drawn over it, run on to the hub;
- the tyre, a `detail` `stroke-signal` ring whose outer edge is the Radius,
  and a `detail` `stroke-hair` inner line 6 units in;
- the rotation cue: three `detail` tubes in the tyre band, a third of a turn
  apart, each a 26° arc 3.75 units inside the edge, `stroke-signal` with
  round ends. They turn with the wheel in Training and sit at rotation 0 in
  Build. Their number stays the same at every Radius;
- the hub, while no joint motor, Brake or Angle sensor sits on the joint: an opaque `background` disc
  the size of a plain joint with a `detail` `stroke-signal` ring and a
  2.6-unit axle dot. The joint draws it where its ring would go, over the
  links (Draw layers), so links end under it like under a ring and only the
  hub, not the whole face, is drawn over them (#1086).

A loose Wheel is all `danger`. Thumbnails draw it in full, tubes
included.

### Rubber

A skin around its joint (#1142): a solid `detail` band 3 units wide
(`RubberDef.Skin`) just outside the joint's largest drawn radius R, the
tyre on a Wheel's joint, so a Wheel with a Rubber reads as a thicker tyre.
It draws on its joint after a Touch sensor's cushion, so it covers the
cushion's inner part, and under the ring, hub, joint motor, Brake or Angle
sensor, which may cover part of it. Off a Wheel's joint, an Angle
sensor's arc goes round it (Angle sensor). The skin is only drawn
(`docs/CREATURE_MODEL.md` → Rubber → Size). Thumbnails draw it; shadows do not.

### Touch sensor

A cushion around its joint (#665, #1045): a ring 5 units wide (`TouchSensorDef.Cushion`)
just outside the joint's largest drawn radius R, the tyre on a Wheel's
joint. Its colour is the brain's reading
(`docs/CREATURE_MODEL.md` → Touch sensor → Reading): `accent` at
`alpha_soft` at 0 and in Build, solid `accent` at 1, blended between. It
draws first on its joint, under the ring, hub, joint motor, Brake or
Angle sensor, which may cover part of it. Off a Wheel's joint, an Angle
sensor's arc goes round it (Angle sensor). It is drawn only
(`docs/CREATURE_MODEL.md` → Touch sensor → Size). Thumbnails draw it at
rest.

### Sensors

A beam sensor is a picture of itself on its beam (#127, #576), never a circle,
which would read as a joint: `panel` fill, 2-wide `accent` lines (`halo`
when selected), upright on the beam's built-up side and turning with it,
where `docs/CREATURE_MODEL.md` → Sensor puts it (#1086).

- **Accelerometer:** a 16 × 22 rounded frame with a zigzag spring to a round
  weight. The weight shows the proof mass the brain reads, with 1 g at half
  its travel. In Build it hangs at rest and swings on the same spring as in
  a run, so nothing jumps when a run starts.
- **Camera:** a body, lens ring and hood at twice that scale, looking along
  its aim. Its rays leave from the picture's edge. In Training the followed
  shadow draws only rays that hit the ground: dashed `halo` to the hit, with
  a 6-unit `halo` ring there (#623).

## Selection marks

Every mark sits `SelectionMarks.Gap` (3) outside its own part's drawn edge
(#710) and is drawn over it, so no part's mark moves for another part on the
same joint: a Touch sensor's sits outside its cushion and an Angle sensor's
outside its arc, the others inside the cushion.

- **Joint:** an unfilled 2-wide `halo` ring.
- **Beam or link:** two 2-wide `halo` lines, one on each side, stopping at
  the joint's ring or joint motor, or at the hub over a Wheel (outside a Spring's
  seats). In a selected group they run on
  to the selected joints' rings, so the group reads as one outline.
- **Sensor:** its lines turn `halo`.
- **Touch sensor:** the joint's ring mark outside the cushion, at R + 8
  (#1045). The joint gets no mark of its own.
- **Rubber:** its skin turns `halo` (#1142), with no ring of its own, which
  would fall where the skin already is. The joint gets no mark of its own.
- **Wheel:** the joint's ring, outside the tyre. The Wheel stands in for its
  joint, which has no mark of its own. Its joint draws the mark with the
  hub, over the links and a Touch sensor's cushion, however it was chosen
  (`docs/BUILD_MODE.md` → Interactions). With a joint motor on the same joint, each selected part shows its
  own mark (#1044).
- **Servo:** a keyhole `halo` outline. Selected alone, a hatched `detail`
  band also marks its Fixed link and an `accent` band its Target link. A
  Wheel side's band is a ring under its tyre, as wide as a beam's band and
  centred on the tyre band, so it shows past both edges of the tyre like a
  link's band past the link, the inner edge through the see-through face
  (#1118). The ring is never hatched (owner's choice); its `detail` or
  `accent` colour alone tells Fixed from Target.
- **Motor:** a 2-wide `halo` ring round it, with the Servo's
  Fixed and Target bands.
- **Brake:** a 2-wide `halo` ring round it, like a joint's, with the
  Servo's Fixed and Target bands in their colours (`docs/UI_DIRECTION.md`
  → Reference token mapping deviations → `detail`).
- **Angle sensor:** a 2-wide `halo` ring a Gap outside the arc's outer
  edge (centre line at the arc's radius + 5, 28 on a bare joint), with the
  Servo's Fixed and Target bands; its ring and arc stay `accent`.
- **Link travel (#704, #835, #931):** stops are `halo` `stroke-signal`
  ticks, as wide as a Piston's cylinder or a Spring's seats,
  joined by a dashed line; a Spring's rest length is a 3-unit `halo` ring.
  Every mark sits at its true length.
- **Camera rays and aim:** while every selected part is a Camera, each
  shows its own rays, with their count, spread and range (#578); one
  selected alone also shows an Aim handle out along its centre ray.
- **Group frame:** a dashed frame with Move, Rotate and Scale handles; after
  a Rotate it stays turned with the group. A box select is dashed, filled
  `halo` at `alpha_soft`, and shows what it would catch as selected.

## Draw layers

A creature draws on two surfaces, the unselected parts and over them the
selected ones, which draw alike (#1107). Each surface draws from the bottom
up: its parts, then the joints joined to no link; the selected surface
starts with a selected joint motor's, Brake's or Angle sensor's bands (Selection marks). On top
of both go the overlays (camera rays, link travel marks, the link drag and
sensor move lines, the picked link's start rings, the link a link drag
replaces, the selection frame). Training's knock-out outline and the rigid
hatch sit under both surfaces.

Parts draw in groups, one per link, so the drawing is the same in Build and
Training and never changes while the creature moves (`DrawGroups`). The link
whose lower end sits highest on screen draws first, so what is lower is
nearer; ties keep link id order. Within a group: a Wheel (with its joint's
first link), the link, its sensor, then a joint's Touch cushion, Rubber skin, ring,
Wheel hub, joint motor, Brake or Angle sensor (with its joint's last
link). So a link covers a Wheel's face and a ring or hub covers its link's
end. An Angle sensor's arc draws with its ring, after its joint's links,
skin and cushion, so it lies over the links and a Wheel's tubes. Joint
motors, Brakes, Angle sensors, joints, hubs and tyre bands are filled, so
nothing shows through them; a Wheel's face inside the tyre is see-through.

The selected surface takes a selected joint or joint part with its links
and their ends, a selected link with its ends, every link whose ends both
rose, so a raised joint motor never hides it, and a selected sensor or the
sensor on a raised link. A raised end lifts its ring, hub or joint motor but not
a Wheel's face, so the Wheel's other links still run over it to the hub
(#1086). A link with one raised end stays behind, so it can look cut at the
edge of a joint motor that rose as another link's end. While a part is placed or a sensor moved, the
beams or joints that take it rise alone, with their `halo`, over a joint motor
they end on or cross too. Each placing mark is drawn with its beam or joint,
under the rod or around the ring, so a refused mark stays in its part's
place (Build canvas → Placing a part). A touch hits what is drawn on top
(`docs/BUILD_MODE.md` → Interactions). In Training the best and crash markers and the start
sign are behind the ground, the shadows above it, and the followed creature
above every shadow.

## Build canvas

- **Grid (#400, #884):** a `line` grid of half-metre cells covering exactly
  the build area, with two-cell `accent` corner marks. It is a blueprint of
  where joints can go, so the Training arena has none (#668).
- **Link drag line (#920):** dashed `halo` over no joint, solid `halo` once
  it will attach, dashed `danger` with a crossed `danger` ring where the
  joint would refuse; its joint rings are clear of an Angle sensor's arc
  (Angle sensor). It is drawn over the creature and is all the feedback:
  a finger often covers the target. A link that will replace another
  (#849, #1176) outlines it with two dashed `line-strong` lines over the
  creature, at that link's selection offset and from hub to hub.
- **Picked link (#1057):** while a link is picked, each joint a drag draws
  it from has a dashed `halo` ring at `JointHalo` (12 dashes, the selection
  ring's width), over the creature, clear of an Angle sensor's arc (Angle
  sensor). It is dashed, as the drag line is
  before it will attach, so it never reads as the solid selection ring.
  Selected joints are left unmarked, and so is a selected joint motor's joint,
  which still draws a link but has its own outline there. No ring is
  `danger` until a drag, since a refusal depends on the target. A link
  drag hides the rings; its start joint takes the solid ring.
- **Placing a part:** the glyph rides on a 48 px raised tile with an
  `accent` line above the finger. A dragged sensor shows free beams `halo`,
  taken beams dashed `danger`, and its picture where it would land. A
  dragged joint part rings the joints that take it `halo` and the joints
  that refuse it dashed `danger` (slot rules: `docs/BUILD_MODE.md`), each ring at the size the part will have there
  (`PlacingTargets.JointRingOf`): a joint motor's, Brake's or Angle sensor's edge, a new Wheel, a
  Rubber's skin, or a Touch sensor's cushion round the joint's largest
  part; a refused joint's ring is round its own edge. Either is clear of an
  Angle sensor's arc (Angle sensor). Every dragged joint part also shows
  itself on a free joint it would land on: a Servo, Motor, Brake or Angle
  sensor selected, with the sides it would take, a Wheel, a Rubber as its
  skin and a Touch sensor as its cushion. A picked part (#1016) marks the same free and taken targets
  from the moment it is picked, so a tap can find them. The targets that
  take it rise over the other parts (Draw layers). A joint part's ring is
  also where a tap or drop lands (`docs/BUILD_MODE.md` → Parts tray).
- **Moving a sensor (#806):** no tile follows the finger. The sensor stays
  on its beam drawn as selected, beams are outlined as for a tray drag
  (its own beam `halo`), and its picture shows on another beam that takes
  it, a Camera looking the same way in the world. A link drag line (#1021)
  ties the sensor's picture to the finger, or to its picture on a beam
  that takes it. Unless refused, a `halo` ring like the crossed one,
  holding an arrow (#1024), points along the move at its middle; a line
  too short to show it beside its ends has none.
- **Callouts (#593, #633):** a note out past its part, joined by a leader in
  its colour, at screen size. Near an edge it slides along the edge, never
  to the part's other side. Overlapping callouts stack in a column, most
  important nearest; leaders are behind all callouts and every note shows.
  In a stack, notes of the same kind and text join into one callout with a
  leader to each part; a callout with lines or sections under its text
  never joins.
- **Readiness** is a chip, not a callout, so it never reads as a note
  whose part is missing (`docs/UI_DIRECTION.md` → "Build").

## Training arena

- **Background:** plain; motion reads against the scales.
- **Scales (#1170):** distance along the view's bottom edge, ticks rising
  from the edge with labels above them; height up the left edge, ticks
  reaching in with each label sitting on its tick, so 0 m reads above the
  ground line. Behind the creatures, with no grid: labels `ReadoutMedium`
  in `muted`, ticks `line-strong`; what they count is in
  `docs/TRAINING_LOOP.md` → Scales. Distance labels slide in and out under
  the view's sides, cut off by its edge like the ground (#1191). The
  height scale stops above the distance scale's band; its labels fade into
  the band and before the slow-motion chip and the lowest flag showing along the top.
- **Best and crash markers (#388, #1257):** a dashed line up to a flag,
  `ArenaFlagMarker`. The best marker's flag reads "Best 4.2 m" in the
  marker ink; the crash marker's, "Crash 3.4 m" in `danger`, hangs one
  row lower, so the two never cover each other when a replayed best
  crashes at its best. A flag always flies right of its line and is cut
  off at the view's right edge like the scales; it never changes side
  (owner, #1257, replacing #886's flip). Each fades to `alpha_shadow`
  while a part callout shows. When each shows is `docs/TRAINING_LOOP.md` →
  Training scene.
- **Start sign (#848):** an `ink` arrow signpost reading "Start" at 0 m,
  half a metre tall in the world, so it zooms with the creatures (#882).
- **Part callout (#388):** each selected part of the followed shadow keeps
  its selection look, and its name shows in a `halo` callout. The callout
  docks at the arena top, `S1` down, centred over the view's middle,
  where the camera keeps a creature at rest (#1169), and pushed inside
  the arena and clear of the slow-motion chip (#1137). Unlike Build's callouts (Callouts
  above), it never moves with its part; only its leader follows the part,
  and a taller callout grows downward. The camera keeps a shelf clear for
  it (`docs/TRAINING_LOOP.md` → Camera). Several selected parts share
  one callout (#1066): a row each, in their stack order
  (`docs/BUILD_MODE.md` → Interactions), with its own glyph, name and
  lines, split by a `line` hairline with `S1` above and below.
- **Port bars (#1064):** a part with brain ports adds its glyph before the
  name and, under the name, a line of its inputs in `accent`, then a line
  of its outputs in `output`. Each line starts with a muted `Overline`
  word, **IN** or **OUT** (short for Inputs and Outputs), so colour is
  never the only cue (#1150). The callout has two columns: the glyph above
  the words, as wide as the widest of them in any of its rows, then the
  name above the bars, `S2` apart; stacked rows share the columns. Each
  port is a muted `Caption` label and a bar, `Meter` sized (26 × 4, `line`
  track, round ends); at most three per row, more wrap, with labels and
  bars in their own columns so wrapped bars line up, and the word shows on
  a line's first row only. A −1…1 port fills from the centre, past a
  1-wide muted tick at 0; a 0…1 port fills from the left. Values outside
  the range clamp. A Wheel, Rubber or Brake shows its glyph and a muted "No brain
  ports"; a Joint, Beam or Spring shows only its name.
- **Slow-motion chip (#318):** a Warning chip, "Too many shadows!", top
  left, over the Best flag when that scrolls past.

## Thumbnails

A Creations card draws its creature with the same parts, scaled to fit
`Space.S3` inside the thumbnail and centred, never past half its Build size
(#770). Edit-only marks are not shown: no selection, loose or too-short
tint, stroke ticks or camera rays.

## Drawing as a shadow

The followed shadow is drawn in full (#385). Every other shadow, the leader
included, is drawn simplified in its normal colours, so up to 100 stay cheap
and their overlaps readable.

- All shadows fade together, once, at `alpha_shadow` (0.32, #818), so
  overlaps do not darken into a mass. Each is one flat picture.
- The followed creature has a knock-out outline in the arena background,
  `stroke-signal` wider than each beam and joint, so it stands clear. On a
  Touch sensor's joint it runs past the cushion, to R + 7, so the cushion
  cuts the ground line where it rests on it; on a Rubber's joint without
  one it runs past the skin the same way (#1142). It also runs along an
  Angle sensor's arc, as along a beam, so the arc never merges into the
  ground line (#1205).
- The leader is not marked: the lead changes too often (#387).

How each part draws as a shadow (the rule is `project/src/creature/AGENTS.md`
rule 10):

- **Joint:** its outer ring only.
- **Servo:** its ring, housing outline, one range arc and one horn line,
  plus the 360° line.
- **Motor:** its outer ring and a circle round the coils; no
  magnets, and it does not turn.
- **Brake:** its outer ring and its disc's ring; it does not turn.
- **Angle sensor:** a plain shadow joint, its outer ring only: shadows
  draw no sensors.
- **Wheel:** the plain circle at its Radius: no tubes or line, and its
  joint's outer ring at the hub, like any shadow joint.
- **Beam:** its line.
- **Rigid triangle:** a faint fill instead of the hatch.
- **Tear:** no flash and no pulse; a torn end is drawn out to its tip, and
  an undone triangle's fill goes at once, with no fade.
- **Links (Piston, Spring):** drawn as on the followed creature.
- **Sensors, Touch cushions, Rubber skins and Camera rays:** not drawn.
- **Knock-out outline:** only the followed creature has one.
