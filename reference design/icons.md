# Icons

All icons are pure white (`#ffffff`) on purpose: import them as SVG and tint them in Godot with `modulate` / `self_modulate`, or with the Button icon colour properties. Never bake a colour into the file.

Three kinds, in one folder, `icons/`, each file named `<kind>_<name>.svg` in snake_case as the app names them. **UI icons** (`icon_`) are stroked glyphs on a 24 grid (stroke 2, round caps and joins, `currentColor`), used at 12, 16, 20 and 24. Every file is the plain 24 grid with the drawing 16 on its longer side, centred, so every icon has the same air round it; nothing is scaled to fit. **Marks** (`mark_`) are the launcher glyph used as an icon, drawn 20 on the same grid at the same stroke, because a network drawn 16 fills into lumps: only `mark_brain` today, the Brain button on Training and the default dialog and notification icon. **Part glyphs** (`part_`) are 20-grid pictures of parts.

## UI icons (53)

| Icon | File |
|---|---|
| <img src="icons/icon_beam.svg" width="24" style="background:#0d1424"> | `icon_beam.svg` |
| <img src="icons/icon_bolt.svg" width="24" style="background:#0d1424"> | `icon_bolt.svg` |
| <img src="icons/icon_build.svg" width="24" style="background:#0d1424"> | `icon_build.svg` |
| <img src="icons/icon_chart.svg" width="24" style="background:#0d1424"> | `icon_chart.svg` |
| <img src="icons/icon_check.svg" width="24" style="background:#0d1424"> | `icon_check.svg` |
| <img src="icons/icon_chev_d.svg" width="24" style="background:#0d1424"> | `icon_chev_d.svg` |
| <img src="icons/icon_chev_l.svg" width="24" style="background:#0d1424"> | `icon_chev_l.svg` |
| <img src="icons/icon_chev_r.svg" width="24" style="background:#0d1424"> | `icon_chev_r.svg` |
| <img src="icons/icon_chev_u.svg" width="24" style="background:#0d1424"> | `icon_chev_u.svg` |
| <img src="icons/icon_copy.svg" width="24" style="background:#0d1424"> | `icon_copy.svg` |
| <img src="icons/icon_core.svg" width="24" style="background:#0d1424"> | `icon_core.svg` |
| <img src="icons/icon_distance.svg" width="24" style="background:#0d1424"> | `icon_distance.svg` |
| <img src="icons/icon_edit.svg" width="24" style="background:#0d1424"> | `icon_edit.svg` |
| <img src="icons/icon_elevation.svg" width="24" style="background:#0d1424"> | `icon_elevation.svg` |
| <img src="icons/icon_eye.svg" width="24" style="background:#0d1424"> | `icon_eye.svg` |
| <img src="icons/icon_flag.svg" width="24" style="background:#0d1424"> | `icon_flag.svg` |
| <img src="icons/icon_gear.svg" width="24" style="background:#0d1424"> | `icon_gear.svg` |
| <img src="icons/icon_height.svg" width="24" style="background:#0d1424"> | `icon_height.svg` |
| <img src="icons/icon_joint.svg" width="24" style="background:#0d1424"> | `icon_joint.svg` |
| <img src="icons/icon_lock.svg" width="24" style="background:#0d1424"> | `icon_lock.svg` |
| <img src="icons/icon_map.svg" width="24" style="background:#0d1424"> | `icon_map.svg` |
| <img src="icons/icon_map_flat.svg" width="24" style="background:#0d1424"> | `icon_map_flat.svg` |
| <img src="icons/icon_map_hills.svg" width="24" style="background:#0d1424"> | `icon_map_hills.svg` |
| <img src="icons/icon_map_stairs.svg" width="24" style="background:#0d1424"> | `icon_map_stairs.svg` |
| <img src="icons/icon_menu.svg" width="24" style="background:#0d1424"> | `icon_menu.svg` |
| <img src="icons/icon_model.svg" width="24" style="background:#0d1424"> | `icon_model.svg` |
| <img src="icons/icon_more.svg" width="24" style="background:#0d1424"> | `icon_more.svg` |
| <img src="icons/icon_move.svg" width="24" style="background:#0d1424"> | `icon_move.svg` |
| <img src="icons/icon_mute.svg" width="24" style="background:#0d1424"> | `icon_mute.svg` |
| <img src="icons/icon_parts.svg" width="24" style="background:#0d1424"> | `icon_parts.svg` |
| <img src="icons/icon_paste.svg" width="24" style="background:#0d1424"> | `icon_paste.svg` |
| <img src="icons/icon_pause.svg" width="24" style="background:#0d1424"> | `icon_pause.svg` |
| <img src="icons/icon_phone.svg" width="24" style="background:#0d1424"> | `icon_phone.svg` |
| <img src="icons/icon_play.svg" width="24" style="background:#0d1424"> | `icon_play.svg` |
| <img src="icons/icon_plus.svg" width="24" style="background:#0d1424"> | `icon_plus.svg` |
| <img src="icons/icon_redo.svg" width="24" style="background:#0d1424"> | `icon_redo.svg` |
| <img src="icons/icon_restart.svg" width="24" style="background:#0d1424"> | `icon_restart.svg` |
| <img src="icons/icon_rotate.svg" width="24" style="background:#0d1424"> | `icon_rotate.svg` |
| <img src="icons/icon_scale.svg" width="24" style="background:#0d1424"> | `icon_scale.svg` |
| <img src="icons/icon_select.svg" width="24" style="background:#0d1424"> | `icon_select.svg` |
| <img src="icons/icon_shadow.svg" width="24" style="background:#0d1424"> | `icon_shadow.svg` |
| <img src="icons/icon_share.svg" width="24" style="background:#0d1424"> | `icon_share.svg` |
| <img src="icons/icon_sort.svg" width="24" style="background:#0d1424"> | `icon_sort.svg` |
| <img src="icons/icon_sound.svg" width="24" style="background:#0d1424"> | `icon_sound.svg` |
| <img src="icons/icon_speed.svg" width="24" style="background:#0d1424"> | `icon_speed.svg` |
| <img src="icons/icon_stop.svg" width="24" style="background:#0d1424"> | `icon_stop.svg` |
| <img src="icons/icon_top_speed.svg" width="24" style="background:#0d1424"> | `icon_top_speed.svg` |
| <img src="icons/icon_trash.svg" width="24" style="background:#0d1424"> | `icon_trash.svg` |
| <img src="icons/icon_trophy.svg" width="24" style="background:#0d1424"> | `icon_trophy.svg` |
| <img src="icons/icon_undo.svg" width="24" style="background:#0d1424"> | `icon_undo.svg` |
| <img src="icons/icon_unlock.svg" width="24" style="background:#0d1424"> | `icon_unlock.svg` |
| <img src="icons/icon_warn.svg" width="24" style="background:#0d1424"> | `icon_warn.svg` |
| <img src="icons/icon_x.svg" width="24" style="background:#0d1424"> | `icon_x.svg` |

## Marks (1)

| Mark | File |
|---|---|
| <img src="icons/mark_brain.svg" width="24" style="background:#0d1424"> | `mark_brain.svg` |

## Part glyphs (18)

| Glyph | File |
|---|---|
| <img src="icons/part_accelerometer.svg" width="32"> | `part_accelerometer.svg` |
| <img src="icons/part_battery.svg" width="32"> | `part_battery.svg` |
| <img src="icons/part_beam.svg" width="32"> | `part_beam.svg` |
| <img src="icons/part_brake.svg" width="32"> | `part_brake.svg` |
| <img src="icons/part_core.svg" width="32"> | `part_core.svg` |
| <img src="icons/part_fuel.svg" width="32"> | `part_fuel.svg` |
| <img src="icons/part_generator.svg" width="32"> | `part_generator.svg` |
| <img src="icons/part_los.svg" width="32"> | `part_los.svg` |
| <img src="icons/part_node.svg" width="32"> | `part_node.svg` |
| <img src="icons/part_piston.svg" width="32"> | `part_piston.svg` |
| <img src="icons/part_pulse.svg" width="32"> | `part_pulse.svg` |
| <img src="icons/part_servo.svg" width="32"> | `part_servo.svg` |
| <img src="icons/part_spring.svg" width="32"> | `part_spring.svg` |
| <img src="icons/part_stepper.svg" width="32"> | `part_stepper.svg` |
| <img src="icons/part_touch.svg" width="32"> | `part_touch.svg` |
| <img src="icons/part_velocity.svg" width="32"> | `part_velocity.svg` |
| <img src="icons/part_wheel.svg" width="32"> | `part_wheel.svg` |
| <img src="icons/part_wing.svg" width="32"> | `part_wing.svg` |

## Energy blocks (3)

Battery, Generator and Fuel tank are drawn objects of one size each, with two **eyes** that beams attach to (see Parts in the design system). Each is exported in white layers, sized for the canvas and made at 4x so a texture stays sharp: `<block>-fill.svg` (tint with `bg`), `<block>-lines.svg` (every line and both eyes; tint with `accent`) and one live layer: `battery-cell.svg` (a lit cell, one per charged cell), `fuel-level.svg` (the level line, moved down as fuel is used) and `generator-run.svg` (the stripes, pulsed while it runs). `blocks.json` gives each block's size, its centre (the middle between the eyes), the eye positions and the live layer's positions, in canvas px. Draw blocks behind beams and joints; between blocks the lower one is drawn on top, an order set when a block is dropped, never recomputed while the creation moves.

| Block | Fill | Lines |
|---|---|---|
| `generator` | <img src="blocks/generator-fill.svg" height="40" style="background:#0d1424"> | <img src="blocks/generator-lines.svg" height="40" style="background:#0d1424"> |
| `battery` | <img src="blocks/battery-fill.svg" height="40" style="background:#0d1424"> | <img src="blocks/battery-lines.svg" height="40" style="background:#0d1424"> |
| `fuel` | <img src="blocks/fuel-fill.svg" height="40" style="background:#0d1424"> | <img src="blocks/fuel-lines.svg" height="40" style="background:#0d1424"> |
