# Component library (c_* functions)

One implementation per control. Every screen calls these; a new screen adds a variant here, never a private copy. The signatures are the props to give the matching Godot scene.

## `c_btn(text, kind='secondary', icon=None, w=None, off=False, on=False, compact=False, badge=None)`

The one button (`btn`): kind primary (one per screen), danger or default, an optional icon, and off for disabled (dimmed, dashed border). Icon-only and stacked layouts are `btn icon` and `btn stack`.

## `c_ib(icon, kind='secondary', size=20, on=False, off=False, compact=False, badge=None)`

The icon layout of the one button (`btn icon`): a 40 x 40 box in a 48 x 48 touch area. kind: accent (state `on`), danger, dis (state `off`).

## `c_round_button(icn, col=None, fill=None, r=13)`

A circular icon badge: the circle is a frame, the same idea as .pnl's variants (sel, lock, warn...), just
round instead of a rounded rect -- an icon centred inside a coloured ring. Selection handles (move, rotate,
scale) are this, placed on the canvas around a selection. sv_handle draws the identical shape straight into
the canvas's own SVG (the canvas is one drawing at absolute coordinates, not flowed HTML) -- keep the two in
step if this one's geometry changes.

## `c_hold(text, pct=40, w=None, kind='tertiary', icon=None)`

Hold-to-confirm button with a fill that grows while held. Used for every destructive or resetting action; there is no undo.

## `c_prog(pct, w='100%', h=6)`

Progress bar.

## `c_meter(label, txt, pct, pad=True)`

Label with a value and a bar.

## `c_slider(label, value, thumbs, steps=(), marker=None, enabled=True, compact=False, _steppers=False, pad=True, pct=None)`

The one slider. thumbs is 0.0-1.0, one number or (lo, hi) for a range. steps is a list of at least two labels, evenly spaced. marker (0.0-1.0, name) is a named line across the track, its name shown in the label row, centred over the marker. enabled=False dims it, dashed. Steppers are a layout: stepped_slider puts a minus and a plus (btn icon sm) either side.

## `c_range(label, lo, hi, text, marker=None, steps=(), enabled=True, pad=True)`

The slider with two thumbs (c_slider with (lo, hi) in 0.0-1.0), with an optional marker.

## `c_toggle(label, on=True, sub=None, dis=False)`

On/off switch row, one size: a control (40px) row, so the switch is always easy to hit.

## `c_check(label, on=False, sub=None, dis=False)`

Checkbox row with an optional sub line.

## `c_seg(opts, sel, full=True, icons=None)`

Segmented control: the chosen option is filled and has a check or bold label.

## `c_pick(label, val, accessory=None, op=False, opts=None, lock=False, dis=False, pad=True)`

Picker: a closed row with an optional accessory (icon or swatch, caller-supplied), or the open list with the same accessories, a check on the current choice and an optional note per row. lock: no other choice, ever (accent tint). dis: the ordinary disabled state, temporary (dimmed, dashed, says why nearby).

## `c_menu(items, w=210)`

Overflow menu list of (icon, label, state) items.

## `c_chip(text, icn=None, kind='neutral', lg=False)`

Small fact chip with an optional icon; state warn/bad/ok for colour.

## `c_call(x, y, text, col=None, icn=None, kind='warn')`

Callout: a small note that points at a spot in a figure (position is data, set by the caller).
kind shares chip's names: warn (halo, the default), danger (explains a refusal), ok (accent, marks a target)
- colour comes from the class, never inline, unless col overrides it for a one-off case.

## `c_textfield(text, state='rest', size='bar', w=None)`

The only text entry: rest (pencil), edit (caret and check), bad (danger and a line of words); size bar or panel.

## `c_name(v, state='rest', pad=True)`

Name field in a panel.

## `c_value(label, val, icn=None, color=None, pad=True)`

Label with its value on one line.

## `c_power(txt, out=False, pad=True)`

A c_value row with the label fixed to "Power" and a bolt icon: draws up to X, or makes/stores.

## `c_note(txt, pad=True)`

One line of muted hint text below the rows of a panel.

## `c_row(glyph, name, cnt='', state='', w=None)`

Parts-tray list row: glyph, name, how many are left; states sel, lock, zero.

## `c_tabs(glyphs, active, w=None)`

Icon tabs; one open at a time.

## `c_panel_head(title, glyph=None, actions=('x',))`

Title row of a side panel: optional glyph, title and icon actions.

## `c_rows(rows, delete=None)`

The body of a settings panel: its rows (each built with pad=False) in one column sharing one gap, and an optional Delete button.

## `c_inspector(title, glyph, rows, delete=None)`

A part's settings panel: c_panel_head then c_rows.

## `c_info_row(icn, title, sub)`

Icon, title and one line of help.

## `c_card(inner, kind='panel', w=None, h=None, style='', glow=False, disabled=False)`

The one Frame surface for panels, cards, tiles, menus and dialogs. Signature c_card(inner, kind, w, h, style, glow, disabled). kind: panel (default), sel, lock, warn, hint, raised (pick, menu and dialog are owned by the stage card, menu and dialog components, not picked freely); add a size with a space: snug, tight, roomy or flush. lock tints the frame in accent, dashed: no other choice, ever. glow is a separate on/off, never a kind of its own: any frame can glow, in that frame's own border colour. disabled is a third, independent on/off: dashed line-strong border, dimmed, no glow, on any kind -- for something temporarily unavailable.

## `c_ring(pct, done=False)`

Progress ring: a touch-size box, a 44 ring centred in it, the percent (or a check when done) centred inside.
