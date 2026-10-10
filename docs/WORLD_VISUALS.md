# World visuals

How the world is drawn in Build, Training and the creation thumbnails.
What a part is and does is in `docs/CREATURE_MODEL.md`; how Build and
Training behave is in `docs/BUILD_MODE.md` and `docs/TRAINING_LOOP.md`; the
UI around the world is in `docs/UI_DIRECTION.md`. Drawing code rules are in
`project/src/theme/AGENTS.md`.

## Lines and zoom

- Zoom scales the whole picture, lines included (#400). Text, the 1 px
  hairlines of the grid and the rigid hatch, the selection frame and its
  handles (#366), callouts, the ruler and the best marker keep their screen
  size.
- Lines stay crisp at any zoom or UI size, with a one-device-pixel feather
  (#733).
- Beams are 6 wide (`UiSize.Widget.CreatureBeamWidth`); part outlines are
  `stroke-signal`. A stroke width may show a setting (#835): it starts at a
  hairline and its drawing names its maximum.

## Parts

### Joint

A bearing in a cell (#626): a `line-strong` `stroke-signal` ring whose
outer edge is `NodeDef.PlainJointRadius`, a `stroke-hair` inner ring at 0.55
of that radius, and an `alpha_soft` tint (`line-strong`, `halo` when
selected, `danger` when loose) on an opaque `background` disc. A loose joint
shows a `danger` ring and cross instead of the inner ring. Beams and Piston
rods stop flat under the ring.

### Beam and rigid triangle

A beam is a plain line. A beam too short to train is `danger`, with a
`danger` "Too short" callout on its upper side.

Build and the followed creature hatch each rigid triangle (#627) with 1 px
lines riding on one of its beams. Where the lines would come closer than
`TriangleHatch.MinPixelSpacing` (zoomed far out, in a thumbnail) and on
every other shadow, the triangle gets a faint fill of the same density
instead (#770).

### Servo

A housing, a range dial and a horn (#452, #577):

- the joint's `accent` ring;
- a `panel` housing box with an `accent` outline on the Fixed link, at most
  8 units past the ring, drawn over a Piston's cylinder as over any link
  (#925), and left out when a sensor needs the room;
- the range, a `panel` band with an `accent` outline inside the ring from
  end stop to end stop, turning with the Fixed link. At 360° the stops meet
  and an `accent` line across the band marks them (#922);
- a two-armed horn along the Target link, at the Start position in Build.

A Servo missing a link draws a `danger` ring, a `danger` tint and an
upright "!" badge.

### Piston

A telescoping rod from joint A to joint B. The cylinder starts at the first
joint's edge and is as long as the travel, so a shorter Stroke gives a
shorter cylinder (#835). The cap sits at the second joint's edge.

### Spring

A coilover (#807): a thin `line-strong` rod, a seat plate outside each
joint, and a `panel` damper body as long as the travel. A helix winds round
the body, `detail` front strokes over it (#1059) and `line-strong` back
strokes under it, all with round ends.

- The wire grows with Stiffness, from a hairline at 50 N/m to a beam's
  width at 2000 N/m; a stiffer Spring gets fewer, thicker turns.
- At rest length the turns stack solid over a third of the seat-to-seat
  span, at least 3, and past 2 m grow only with the square root of the span.
  Preload shows as more or less coil.
- Turns spread and bunch as the Spring moves; their number never changes.
- Passive: `detail`, no `accent`. All `danger` when too short.

### Wheel

A tyre on a hub, standing in for its joint (#129); the same geometry in
every theme, with no tint:

- an opaque `background` face of the Wheel's Radius, so parts drawn before
  it (Draw layers) do not show through; its own links, drawn over it, end
  on the tyre ring (#1086 takes them to the hub);
- the tyre, a `detail` `stroke-signal` ring whose outer edge is the Radius,
  and a `detail` `stroke-hair` inner line 6 units in;
- the rotation cue: three `detail` tubes in the tyre band, a third of a turn
  apart, each a 26° arc 3.75 units inside the edge, `stroke-signal` with
  round ends. They turn with the wheel in Training and sit at rotation 0 in
  Build. Their number stays the same at every Radius;
- the hub, while no motor sits on the joint: a `detail` `stroke-signal` ring
  the size of a plain joint with a 2.6-unit axle dot.

A loose Wheel is all `danger`. Thumbnails draw it in full, tubes
included.

### Sensors

A picture of itself at its beam's midpoint (#127, #576), never a circle,
which would read as a joint: `panel` fill, 2-wide `accent` lines (`halo`
when selected), upright on the beam's built-up side and turning with it.

- **Accelerometer:** a 16 × 22 rounded frame with a zigzag spring to a round
  weight. The weight shows the proof mass the brain reads, with 1 g at half
  its travel. In Build it hangs at rest and swings on the same spring as in
  a run, so nothing jumps when a run starts.
- **Camera:** a body, lens ring and hood at twice that scale, looking along
  its aim. Its rays leave from the picture's edge. In Training the followed
  shadow draws only rays that hit the ground: dashed `halo` to the hit, with
  a 6-unit `halo` ring there (#623).

## Selection marks

Every mark sits `SelectionMarks.Gap` (3) outside the part (#710) and is
drawn over it.

- **Joint:** an unfilled 2-wide `halo` ring.
- **Beam or link:** two 2-wide `halo` lines, one on each side, stopping at
  the joint edges (outside a Spring's seats). In a selected group they run on
  to the selected joints' rings, so the group reads as one outline.
- **Sensor:** its lines turn `halo`.
- **Wheel:** the joint's ring, outside the tyre. The Wheel stands in for its
  joint, which has no mark of its own.
- **Servo:** a keyhole `halo` outline. Selected alone, a hatched `detail`
  band also marks its Fixed link and an `accent` band its Target link.
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
starts with a selected Servo's bands (Selection marks). On top
of both go the overlays (camera rays, link travel marks, the link drag and
sensor move lines, the picked link's start rings, the beam a link drag
replaces, the selection frame). Training's knock-out outline and the rigid
hatch sit under both surfaces.

Parts draw in groups, one per link, so the drawing is the same in Build and
Training and never changes while the creature moves (`DrawGroups`). The link
whose lower end sits highest on screen draws first, so what is lower is
nearer; ties keep link id order. Within a group: a Wheel (with its joint's
first link), the link, its sensor, then a joint's ring or Servo (with its
joint's last link). So a link covers a Wheel and a ring covers its link's
end. Wheels, Servos and joints are filled, so nothing shows through them.

The selected surface takes a selected joint, Servo or Wheel with its links
and their ends, a selected link with its ends, every link whose ends both
rose, so a raised Wheel or Servo never hides it, and a selected sensor or
the sensor on a raised link. A link with one raised end stays behind, so
it can look cut at the rim of a Wheel that rose as another link's end. While a part is placed or a sensor moved, the
beams or joints that take it rise alone, with their `halo`, over a Servo
they end on or cross too. Each placing mark is drawn with its beam or joint,
under the rod or around the ring, so a refused mark stays in its part's
place (Build canvas → Placing a part). A touch hits what is drawn on top
(`docs/BUILD_MODE.md` → Interactions). In Training the best marker and start
sign are behind the ground, the shadows above it, and the followed creature
above every shadow.

## Build canvas

- **Grid (#400, #884):** a `line` grid of half-metre cells covering exactly
  the build area, with two-cell `accent` corner marks. It is a blueprint of
  where joints can go, so the Training arena has none (#668).
- **Link drag line (#920):** dashed `halo` over no joint, solid `halo` once
  it will attach, dashed `danger` with a crossed `danger` ring where the
  joint would refuse. It is drawn over the creature and is all the feedback:
  a finger often covers the target. A Piston or Spring that will replace a
  beam (#849) outlines it with two dashed `line-strong` lines over the
  creature.
- **Picked link (#1057):** while a link is picked, each joint a drag draws
  it from has a dashed `halo` ring at `JointHalo` (12 dashes, the selection
  ring's width), over the creature. It is dashed, as the drag line is
  before it will attach, so it never reads as the solid selection ring.
  Selected joints are left unmarked, and so is a selected Servo's joint,
  which still draws a link but has its own outline there. No ring is
  `danger` until a drag, since a refusal depends on the target. A link
  drag hides the rings; its start joint takes the solid ring.
- **Placing a part:** the glyph rides on a 48 px raised tile with an
  `accent` line above the finger. A dragged sensor shows free beams `halo`,
  taken beams dashed `danger`, and its picture where it would land. A
  dragged Servo or Wheel rings free joints `halo` and joints holding a part
  dashed `danger`, each ring at the size the part will have there
  (`PartTray.PlacingRingRadius`); a dragged Wheel also shows itself on
  a free joint it would land on. A picked part (#1016) marks the same free and taken targets
  from the moment it is picked, so a tap can find them. The targets that
  take it rise over the other parts (Draw layers). A Servo's or Wheel's
  ring is also where a tap or drop lands (`docs/BUILD_MODE.md` → Parts tray).
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
  leader to each part; a callout with lines under its text never joins.

## Training arena

- **Background:** plain; motion reads against the ruler.
- **Ruler:** labels every 1 m at the closest zoom, minor ticks every 0.5 m,
  at screen size (`docs/TRAINING_LOOP.md` → Ruler).
- **Best marker (#388):** a dashed marker whose flag reads "Best 4.2 m". It
  flies left of its line where it would run past the view's right edge
  (#886), and fades to `alpha_shadow` while a part callout shows.
- **Start sign (#848):** an `ink` arrow signpost reading "Start" at 0 m,
  half a metre tall in the world, so it zooms with the creatures (#882).
- **Part callout (#388):** the followed shadow's tapped part keeps its
  selection look, and its name shows in a `halo` callout above the whole
  creature, so it never covers the body.
- **Port bars (#1064):** a part with brain ports adds its glyph before the
  name and, under the name, a line of its senses in `accent`, then a line
  of its outputs in `output`. Each port is a muted `Caption` label and a
  bar, `Meter` sized (26 × 4, `line` track, round ends); at most three per
  row, more wrap, with labels and bars in their own columns so wrapped bars
  line up. A −1…1 port fills from the centre, past a 1-wide muted
  tick at 0; a 0…1 port fills from the left. Values outside the range
  clamp. A Piston's target length is drawn 0…1, as (v+1)/2 of its −1…1
  output, so it looks like the measured length it is compared with. A Wheel shows its glyph and a muted "No brain ports"; a Joint,
  Beam or Spring shows only its name.
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
  `stroke-signal` wider than each beam and joint, so it stands clear.
- The leader is not marked: the lead changes too often (#387).

How each part draws as a shadow (the rule is `project/src/creature/AGENTS.md`
rule 10):

- **Joint:** its outer ring only.
- **Servo:** its ring, housing outline, one range arc and one horn line,
  plus the 360° line.
- **Wheel:** the plain circle at its Radius: no tubes, line or hub.
- **Beam:** its line.
- **Rigid triangle:** a faint fill instead of the hatch.
- **Links (Piston, Spring):** drawn as on the followed creature.
- **Sensors and Camera rays:** not drawn.
- **Knock-out outline:** only the followed creature has one.
