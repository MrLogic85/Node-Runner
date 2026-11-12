# World visuals

Rules for drawing the world: parts, the Build canvas, the Training arena
and thumbnails. The UI around it is `docs/UI_DIRECTION.md`; code rules
are `.claude/rules/theme.md`. A part's shapes and sizes are its code
(`StepperPart`, `StepperGeometry` and so on).

## Lines and zoom

- Zoom scales the whole picture, lines included. Text, the 1 px hairlines
  of the grid and the rigid hatch, the selection frame and handles,
  callouts, the arena's scales and the best marker keep their screen size.
- Lines stay crisp at any zoom or UI size, with a one-device-pixel
  feather.
- All beams share one width; part outlines are `stroke-signal`. A stroke
  width may show a setting: it starts at a hairline and its drawing names
  its maximum.
- A thin mark drawn over links may carry a `background` `stroke-hair`
  knock-out edge each side, so it parts from what it crosses.

## Parts

- **Colour says who drives a part** (`docs/UI_DIRECTION.md` →
  "Reference token mapping deviations"): `accent` for what the network
  drives or reads, `detail` for passive parts, `line-strong` on `panel`
  for structure, `danger` for an error.
- **A hinge part (`docs/GLOSSARY.md`) or Wheel stands in for its joint's
  ring,** or a Wheel's hub, in the same place. Joints, hubs, hinge parts
  and tyre bands are filled, so nothing shows through them.
- **A hinge part shows its sides:** one piece turns with the Fixed side
  and another with the Target side.
- **Links run hub to hub:** a link stops at the joint's hub, whatever sits
  on the joint, and ends under its ring, hub or hinge part.
- **A beam sensor is a picture of itself on its beam,** never a circle,
  which would read as a joint: `panel` fill and `accent` lines, upright on
  the beam's built-up side and turning with it.
- **An error keeps the part's shape** and draws in `danger`. A hinge part
  missing a side adds an upright "!" badge at the screen's upper right,
  in thumbnails too, and drops what that side would place. A bare loose
  joint shows a ring and cross; a hinge part or Wheel on a loose joint
  shows the error in its own look instead. A beam too short to train gets
  a "Too short" callout.
- **A tear is said once, at the tear** (`docs/CREATURE_MODEL.md` → "Body" →
  Tearing), and the creature shows the rest. On the followed creature the
  torn end flashes and a `danger` ring leaves the joint, on physics ticks
  so Pause holds them, and an undone triangle's hatch fades. A beam torn
  at both ends keeps being drawn as it falls.

## Part decisions

Choices for one part, which other parts need not follow.

- **Joint:** a bearing in a cell; a cushion or skin has no error look, so
  a loose joint with only those keeps the ring and cross.
- **Rigid triangle:** hatched with 1 px lines in Build and on the followed
  creature; where the lines would come too close (far zoom, thumbnails), a
  faint fill of the same density.
- **Servo:** a housing on the Fixed link, drawn over any link, shortens
  to a stub when a sensor or short link needs the room, so the Fixed link
  always shows; a Piston's or Spring's telescope starts past it. The
  range band turns with the Fixed link; at 360° a line marks where the
  stops meet. The horn is at the Start position in Build.
- **Stepper:** an escapement, with no Servo housing, band or horn. Notch
  size scales with the notch count, so 24 still read as a wheel; too small
  to see, the rim is a plain circle. A step shows as a lifted, hollow pawl
  and a soft wedge to the next notch; no notch is lit, since a thicker
  notch did not read on device. The frequency shows only as the pace.
- **Motor:** six coils, not nine, so they stay legible zoomed out; no live
  cue but the turning. A missing side of a Motor or Brake points to the
  screen's right.
- **Brake:** passive, so `detail`, never `accent`; a plain joint's size,
  since it collides as one. A slip shows as the hand sweeping past the
  pads; a Locked Brake's disc is solid, one piece.
- **Angle sensor:** its `accent` ring tells it from a plain joint even at
  0° and in thumbnails. The arc runs from the Fixed side's direction to
  the Target's, `AngleSensorDef.ArcGap` outside the joint's outermost
  part, never on a cushion or skin, over a Wheel's tyre; Build's rings sit
  outside it. It fills on an even turn and empties on an odd one, so it
  never jumps; at rest it shows which side is Target. A very small angle
  shows as a short mark, accepted, since a player adds it to a joint that
  moves. Missing a side, no arc. Only drawn: no collider, no tap area.
- **Piston:** a three-part telescope whose sizes come from its shortest
  length, so only the stage and rod slide, sharing the travel, and nothing
  shrinks; it reads as
  three parts at the smallest Stroke. Each tube starts past what covers
  joint A.
- **Spring:** a coilover, passive and narrower than a Piston, all
  `danger` when too short. The wire grows with Stiffness, with fewer
  turns; at rest length the turns stack over a third of the span, so
  preload shows; they never overlap at the shortest length, and their
  number never changes.
- **Wheel:** a see-through face, so what lies inside shows through and
  its own links run over it to the hub; three tubes, the rotation cue.
  A loose Wheel is all `danger`.
- **Rubber:** a skin outside the joint's largest drawn part, so a Wheel
  with Rubber reads as a thicker tyre; over a cushion, under the ring.
- **Touch sensor:** a cushion whose colour is the reading, `accent-soft`
  at 0 and in Build to `accent` at 1, the one world part in
  `accent-soft`. It draws first on its joint.
- **Accelerometer:** a spring and weight showing the proof mass, 1 g at
  half travel. Build hangs it as in a run, so nothing jumps at the start.
- **Camera:** looks along its aim. In Training the followed shadow draws
  only rays that hit the ground, dashed `halo` to a ring at the hit.
- **Memory cell:** a barometer whose needle shows the `m` the network
  reads next. The needle is upright for `m` = 0, a quarter turn right at
  1 and left at −1. In Build and at every trial start it is upright.

## Selection marks

- Every mark sits `SelectionMarks.Gap` outside its own part's drawn edge
  and over it, so no part's mark moves for another part on the same
  joint.
- Each selected part marks itself; a joint with parts on it gets no mark
  of its own.
- **Beam or link:** a `halo` line each side, stopping at the joint's
  ring, hinge part or hub. In a group they run on to the selected joints,
  so the group reads as one outline.
- **Fixed and Target bands:** a hinge part selected alone marks its
  sides, a hatched `detail` band on the Fixed link and an `accent` band
  on the Target link. A Wheel side's band is a ring under its tyre, never
  hatched.
- **Link travel:** `halo` ticks at the stops joined by a dashed line; a
  Spring's rest length is a small ring; every mark at its true length.
- **Camera rays and aim:** while every selected part is a Camera, each
  shows its rays; one selected alone also shows an Aim handle.
- **Group frame:** dashed, with Move, Rotate and Scale handles; after a
  Rotate it stays turned. A box select is dashed, filled `halo` at
  `alpha_soft`, and shows what it would catch as selected.
- Per part: a ring for a Joint, Motor or Brake (an Angle sensor's outside
  its arc), a Wheel's outside the tyre and a Touch sensor's outside the
  cushion; a keyhole round a Servo's ring and housing, and round a
  Stepper's pawl, since a mark on the notched rim would fill every notch;
  `halo` lines for a sensor and skin for a Rubber.

## Draw layers

- Two surfaces: the unselected parts, and over them the selected ones,
  drawn alike. Overlays (camera rays, link travel, drag and move lines,
  the picked link's rings, a replaced link's outline, the selection
  frame) go on top; the knock-out outline and rigid hatch go under both.
  A selected hinge part's side bands sit under the selected surface.
- Parts draw in one group per link, the same in Build and Training and
  never changing while the creature moves. The link whose lower end sits
  highest draws first, so what is lower is nearer; ties keep link id
  order. Within a group: a Wheel's face, the link, its sensor, then each
  joint's bands (cushion, skin, halos, placing marks) and its ring, hub
  or hinge part.
- The selected surface takes a selected joint or joint part with its
  links, a selected link with its ends, every link whose ends both rose,
  and a selected sensor or the sensor on a raised link. A raised end
  lifts its ring, hub or hinge part, not a Wheel's face. While a part is
  placed or a sensor moved, the targets that take it rise with their
  marks.
- A touch hits what is drawn on top (`docs/BUILD_MODE.md` →
  "Rules").
- In Training the best and crash markers and the start sign are behind
  the ground, the shadows above it, and the followed creature above every
  shadow, each creature with its whole set of layers.

## Build canvas

- **Grid:** a `line` grid of half-metre cells covering exactly the build
  area, with `accent` corner marks. It is a blueprint of where joints can
  go, so the Training arena has none.
- **Link drag line:** dashed `halo` over no joint, solid once it will
  attach, dashed `danger` with a crossed ring where the joint would
  refuse. It is all the feedback, since a finger often covers the target.
  A link it will replace is outlined with dashed `line-strong` lines.
- **Picked link:** each joint a drag can start from has a dashed `halo`
  ring, so it never reads as the solid selection ring. Selected joints
  are left unmarked. No ring is `danger` until a drag, since a refusal
  depends on the target.
- **Placing a part:** the glyph rides on a raised tile above the finger.
  Targets that take the part are `halo`, taken ones dashed `danger`; a
  joint's ring has the size the part will have there, and is where a tap
  or drop lands. The part shows itself where it would land, a hinge part
  with the sides it would take. A picked part marks its targets at once,
  so a tap can find them.
- **Moving a sensor or Memory cell:** no tile follows the finger. The
  part stays on its beam, drawn as selected, beams are outlined as for a
  tray drag, and its picture shows on a beam that takes it. A drag line
  ties it to the finger, with an arrow ring along the move when it fits.
- **Callouts:** a note past its part, at screen size, joined by a leader
  in its colour. Near an edge it slides along the edge, never to the
  part's other side. Overlapping callouts stack, most important nearest,
  and every note shows; notes of the same kind and text join into one
  callout with a leader to each part.

## Training arena

- **Background:** plain; motion reads against the scales.
- **Scales:** distance along the view's bottom edge, height up the left,
  behind the creatures, with no grid (`docs/TRAINING_LOOP.md` → "Scene
  decisions"). Labels are cut off by the view's edges rather than fading,
  so none blinks as the view shakes.
- **Best and crash markers:** a dashed line up to a flag, "Best 4.2 m",
  and "Crash 3.4 m" in `danger` one row lower, so the two never cover
  each other. A flag always flies right of its line and is cut off at the
  view's edge. Each fades while a part callout shows. When each shows is
  `docs/TRAINING_LOOP.md` → "Scene decisions".
- **Start sign:** an `ink` signpost reading "Start" at 0 m, sized in the
  world, so it zooms with the creatures.
- **Part callout:** each selected part of the followed shadow keeps its
  selection look, and its name shows in a `halo` callout docked at the
  arena top, over the view's middle. It never moves; only its leader
  follows the part (`docs/TRAINING_LOOP.md` → "Scene decisions").
  Several selected parts share one callout, a row each in their stack
  order.
- **Port bars:** under the part's name, a line of its inputs in `accent`
  and one of its outputs in `output`, each led by a muted **IN** or
  **OUT**, so colour is never the only cue. A −1…1 port fills from a tick
  at 0, a 0…1 port from the left, and values outside clamp. A part with
  no ports but a glyph shows "No network ports". The Status panel's input
  bars and output dials take the same colours and fill rule.

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
  along an Angle sensor's arc.
- The leader is not marked: the lead changes too often.

What each part keeps (`.claude/rules/creature.md` rule 10):

- **Joint, Angle sensor:** the outer ring. **Beam:** its line. **Rigid
  triangle:** a faint fill; an undone one's goes at once.
- **Servo:** ring, housing outline, one range arc and one horn line, plus
  the 360° line.
- **Motor, Brake:** the outer ring and a circle round the coils or the
  disc's ring; they do not turn.
- **Stepper:** a plain rim and the pawl's outline, which turns, so each
  step still shows.
- **Wheel:** the circle at its Radius and its joint's ring.
- **Piston, Spring:** as on the followed creature.
- **Not drawn:** sensors, Memory cells, cushions, skins, camera rays and
  a tear's flash and pulse.
