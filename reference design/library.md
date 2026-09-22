# Component library (c_* functions)

One implementation per control. Every screen calls these; a new screen adds a variant here, never a private copy. The signatures are the props to give the matching Godot scene.

## `c_btn(text, kind='secondary', icon=None, w=None, off=False, on=False, compact=False, badge=None)`

The one button (`btn`): kind primary (one per screen), danger or default, an optional icon, and off for disabled (dimmed, dashed border). Icon-only and stacked layouts are `btn icon` and `btn stack`.

## `c_ib(icon, kind='secondary', size=20, on=False, off=False, compact=False, badge=None)`

The icon layout of the one button (`btn icon`): a 40 x 40 box in a 48 x 48 touch area. kind: accent (state `on`), danger, dis (state `off`).

## `c_hold(text, pct=40, w=None, kind='tertiary', icon=None)`

Hold-to-confirm button with a fill that grows while held. Used for every destructive or resetting action; there is no undo.

## `c_slider(label, value, thumbs, steps=(), marker=None, enabled=True, compact=False, _steppers=False)`

The one slider. thumbs is 0.0-1.0, one number or (lo, hi) for a range. steps is a list of at least two labels, evenly spaced. marker (0.0-1.0, name) is a named line across the track, its name shown in the label row, centred over the marker. enabled=False dims it, dashed. Steppers are a layout: stepped_slider puts a minus and a plus (btn icon sm) either side.

## `c_range(label, lo, hi, text, marker=None, steps=(), enabled=True)`

The slider with two thumbs (c_slider with (lo, hi) in 0.0-1.0), with an optional marker.

## `c_toggle(label, on=True, sub=None, dis=False)`

On/off switch row, one size: a control (40px) row, so the switch is always easy to hit.

## `c_check(label, on=False, sub=None, dis=False)`

Checkbox row with an optional sub line.

## `c_seg(opts, sel, full=True, icons=None)`

Segmented control: the chosen option is filled and has a check or bold label.

## `c_pick(label, val, accessory=None, op=False, opts=None, lock=False)`

Picker: a closed row with an optional accessory (icon or swatch, caller-supplied), or the open list with the same accessories, a check on the current choice and an optional note per row.

## `c_menu(items, w=210)`

Overflow menu list of (icon, label, state) items.

## `c_chip(text, icn=None, state='')`

Small fact chip with an optional icon; state warn/bad/ok for colour.

## `c_prog(pct, w='100%', h=6)`

Progress bar.

## `c_textfield(text, state='rest', size='bar', w=None)`

The only text entry: rest (pencil), edit (caret and check), bad (danger and a line of words); size bar or panel.

## `c_name(v, state='rest')`

Name field in a panel.

## `c_value(label, val)`

Label with an editable value.

## `c_readonly(label, val, icn)`

Dashed read-only value with an icon and the reason it is locked.

## `c_power(txt, out=False)`

Power line of a part: draws up to X, or makes/stores.

## `c_meter(label, txt, pct)`

Label with a value and a bar.

## `c_row(glyph, name, cnt='', state='', w=None)`

Parts-tray list row: glyph, name, how many are left; states sel, lock, zero.

## `c_tabs(glyphs, active, w=None)`

Icon tabs; one open at a time.

## `c_panel_head(title, glyph=None, actions=('x',))`

Title row of a side panel: optional glyph, title and icon actions.

## `c_info_row(icn, title, sub)`

Icon, title and one line of help.

## `c_card(inner, kind='panel', w=None, h=None, style='')`

The one Frame surface for panels, cards, tiles, menus and dialogs. Signature c_card(inner, kind, w, h, style). kind: panel (default), sel, pick, lock, warn, hint, ok, glow, raised, menu, dialog; add a size with a space: snug, tight, roomy or flush.

## `c_ring(pct, done=False)`

Progress ring: a touch-size box, a 44 ring centred in it, the percent (or a check when done) centred inside.

## `c_panel(kind, inner, w=None)`

Legacy alias with the arguments swapped: c_panel(kind, inner, w) calls c_card(inner, kind, w). New code calls c_card.
