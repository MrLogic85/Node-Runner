# Component library (c_* functions)

One implementation per control. Every screen calls these; a new screen adds a variant here, never a private copy. The signatures are the props to give the matching Godot scene.

## `c_btn(text, kind='secondary', icon=None, w=None, off=False, on=False, compact=False, badge=None, unavailable=False)`

kind: primary, secondary, tertiary or flat. off/on are the disabled/selected states; compact is the small size.
kind can also carry a layout word (icon, stack) for buttons with no text, e.g. c_btn('', 'icon secondary', 'gear');
the play button is c_btn('', 'primary stack', 'play') — a stack button (like a tool rail cell) with no label.
badge: a small halo counter in the corner, for any kind or layout, row, icon or stack alike.
The layout picks the icon size, never the caller: icon (16) beside text in a row, icon-lg (20) with no text and in a stack.
unavailable: looks like off (dashed, dimmed) but still takes a tap, so the screen can answer it -- the reason play is
blocked, or that a feature comes in a later version. Never a substitute for off on a control that does nothing.

## `c_ib(icon, kind='secondary', on=False, off=False, compact=False, badge=None, unavailable=False)`

An icon-only button: the same btn system as c_btn, just the icon layout. kind: primary, secondary, tertiary or flat.
badge: a small halo counter in the corner. The icon is always icon-lg (20): a button with no text gets the larger
size, the same as a stacked button, and no call site picks its own.

## `c_num(n)`

A ringed step number, 16px, accent on both ring and digit (the .num class). The mark in the top-left corner
of a stage card's header, or leading a chain chip -- every numbered step uses this, not a hand-written span.
Lives here, not in ui17's control kit, because the old signal-flow pipeline (gen10, gen13) needs it too and
can't import ui17 without a circular import; ui17 pulls this in the same way it pulls in ic() and ICONS.

## `c_stage(n, title, body='', em='', height=None, sel=False, collapsed=False, style='', opens=None)`

One numbered card of the signal-flow column: c_num leading the header (the stage name, an optional
right-aligned note), then whatever that stage needs to show. collapsed=True is the 28px strip used when
only one stage is expanded at a time -- same header, no body. sel=True is the frame `pick`: the chosen
stage card, the same frame Menu and Dialog use for "this is the open one" (see Rule: frames) -- never a
literal 'sel' class of its own. Only a stage that opens something looks like a button (opens, the Brain stage by
default): the raised frame with its glow. The others are plain frames without glow, so they never read as tappable.

## `c_round_button(icn, col=None, fill=None, r=13)`

A circular icon badge: the circle is a frame, the same idea as .pnl's variants (sel, lock, warn...), just
round instead of a rounded rect -- an icon centred inside a coloured ring. Selection handles (move, rotate,
scale) are this, placed on the canvas around a selection. sv_handle draws the identical shape straight into
the canvas's own SVG (the canvas is one drawing at absolute coordinates, not flowed HTML) -- keep the two in
step if this one's geometry changes.

## `c_hold(text, pct=40, w=None, kind='tertiary', icon=None)`

Hold-to-activate: an isolated fill span in that kind's selected colour, 50% opacity, sweeps under the content — same
technique c_btn's box-shadow uses for the selected glow, just on a plain background instead. Works for row (text and/or
icon), icon-only and stack layouts, put `icon`/`stack` in kind exactly as c_btn does, e.g. c_hold('', 45, kind='primary icon', icon='play').

## `c_prog(pct, w='100%', h=6)`

A bare progress bar: c_slider's own track and fill (thumbs=None), no label/value row around it. w
constrains it, since without a label row's box there is nothing else to size it by. h is accepted only so
old call sites do not break -- the track is always 4px now, matching the slider it shares code with.

## `c_meter(label, txt, pct, pad=True)`

Label with a value and a bar: c_slider with thumbs=None -- the slider's own track and fill, just with no
thumb to drag. pad=False for a row inside c_rows: the list owns the gap between rows, this one carries none
of its own.

## `c_slider(label, value, thumbs, steps=(), marker=None, enabled=True, compact=False, _steppers=False, pad=True, pct=None, spread=None)`

The one slider, configured by data -- and, with thumbs=None, the one progress bar too (c_prog and c_meter
above are thin wrappers over exactly this): the same track and the same fill, just no thumb to drag, and
pct (0-100) stands in for thumbs' 0.0-1.0 position. thumbs: 0.0-1.0, one number or a (lo, hi) pair (a
range), or None for a plain fill with no handle (pct then required). steps: the caller's labels, at least
two, always evenly placed (thumbed sliders only). marker: (0.0-1.0, name), e.g. (0.03, 'default 4').
enabled=False dims it, dashed like a disabled button. Steppers are a layout, see stepped_slider. pad=False
for a row inside c_rows: the list owns the gap between rows, this one carries none of its own. label and
value may both be None for a bare bar with no readout row at all.

## `c_range(label, lo, hi, text, marker=None, steps=(), enabled=True, pad=True)`

The slider with two thumbs: the same component, configured with (lo, hi) in 0.0-1.0.

## `c_toggle(label, on=True, sub=None, dis=False)`

One toggle, one size: a control (40px) row, so the switch always sits in a touch target big enough to hit.

## `c_check(label, on=False, sub=None, dis=False)`

Checkbox row with an optional sub line.

## `c_seg(opts, sel, full=True, icons=None)`

Segmented control: the chosen option is filled and has a check or bold label.

## `c_pick(label, val, accessory=None, op=False, opts=None, lock=False, dis=False, pad=True)`

The one picker: a closed row (label, an optional small accessory, the value, a chevron) that opens a list of choices under it.
accessory is any small HTML the caller builds (an icon, a swatch); the picker does not know what it means. lock shows a lock instead of a
chevron and tints the row in accent: there is no other choice to make, ever (a Drive only has one wheel to attach to). dis is the ordinary
disabled state every other control has: dims the whole row to 50% and dashes its border, chevron unchanged; the picker still works, it just
can't be opened right now, and a line nearby should say why. The two never combine. opts (open=True) is [(name, accessory, selected, note)]:
selected shows a check, note is a short word ("swaps") for a choice that needs explaining instead of being refused. pad=False for a row
inside c_rows: the list owns the gap between rows, this one carries none of its own.

## `c_menu(items, w=200, compact=False, marks=False)`

The menu: rows in the menu frame (kind menu, with its glow). items: mi() rows, mi_toggle() rows and MI_DIVIDER.
Two sizes: standard rows are at least touch (48) high, body-strong, icon-lg (the overflow menu); compact rows at least
control-sm (32), small-strong, icon (the list a picker opens). A row is laid out as: the icon or lead, the label with its
optional note under it, and, when marks=True, an icon-sm check in accent on the chosen row; all centred vertically.
w: a fixed width in px (200 by default), or 'fit' to fit the content (never narrower than what it hangs from).

## `c_chip(text, icn=None, kind='neutral')`

Small fact chip, control-xs high, icon-sm. kind: neutral (default, no class needed beyond chip), warn (halo), danger,
ok (accent) — border and text/icon always share the one colour, set by the class, never inline. One size only.

## `c_call(x, y, text, col=None, icn=None, kind='warn')`

Callout: a small note that points at a spot in a figure (position is data, set by the caller).
kind shares chip's names: warn (halo, the default), danger (explains a refusal), ok (accent, marks a target)
- colour comes from the class, never inline, unless col overrides it for a one-off case.

## `c_textfield(text, state='rest', size='bar', w=None)`

The only text entry. size 'bar' (control high, heading text) sits in a top bar and fills the bar's field, whatever its text; 'panel' (control-sm high, body-strong) fills a settings panel. States rest, edit, bad are classes of .field.

## `c_name(v, state='rest', pad=True)`

Name field in a panel. pad=False for a row inside c_rows: the list owns the gap between rows, this one carries none of its own.

## `c_value(label, val, icn=None, color=None, pad=True)`

A label and its value on one line. With icn (and the colour to tint it and the value), the value carries a small icon — this is what c_power is: c_value with a fixed label and a bolt.
pad=False for a row inside c_rows: the list owns the gap between rows, this one carries none of its own.

## `c_power(txt, out=False, pad=True)`

A c_value row with the label fixed to "Power" and a bolt icon: accent when the part feeds power back, ink when it only draws.

## `c_note(txt, pad=True)`

A single line of muted hint text in a panel. margin-top only, no margin-bottom:on its own it collapses with whatever row comes
before, so the gap above it is always the same. pad=False for a row inside c_rows: the list owns the gap between rows, this one
carries none of its own.

## `c_row(glyph, name, cnt='', state='', w=None)`

Parts-tray list row: glyph, name, how many are left; states sel, lock, zero.

## `c_tabs(glyphs, active, w=None)`

Icon tabs; one open at a time.

## `c_panel_head(title, glyph=None, actions=('x',))`

Title row of a side panel: an optional glyph, the title, and icon actions (compact flat icon buttons, by icon name).

## `c_rows(rows, delete=None, locked=False)`

The body of a part's settings panel: its rows in one column with the panel's own gap between them (var(--space-1)) — the panel
owns that spacing, not the rows, so build each row with pad=False and let this join them. delete, if given, is the label of a
full-width danger button at the end (its own bigger gap above, space-2, since an action is not another row).

## `c_inspector(title, glyph, rows, delete=None)`

A part's settings panel: c_panel_head (glyph, title, close) then c_rows (its rows sharing one gap, and an optional Delete).

## `c_info_row(icn, title, sub, handle=True, glyph=False)`

A handle explained: the handle itself (c_round_button, exactly as it sits on the canvas), a title and a note.
handle=False: a plain muted 16 icon in its place (a part glyph with glyph=True), for a tool's help rows.

## `c_card_actions(buttons)`

Card action bar: the row of actions along the bottom of a flush card (a creation, a checkpoint), always visible,
never a hidden menu. It is a layout style, not a button of its own: buttons are ordinary stacked buttons (c_btn, or
c_hold for hold-to-activate) and keep all their behaviour -- the bar only restyles them. Each one stretches to its
share of the width and touch high (that whole cell is what you tap), loses its frame and background, and gets a
hairline between it and the next; the bar draws a hairline above. A destructive action is the tertiary kind, so it
is in the danger colour. buttons: e.g. [c_btn('Copy', 'flat stack', 'copy'), c_btn('Delete', 'tertiary stack', 'trash')].

## `c_card(inner, kind='panel', w=None, h=None, style='', glow=False, disabled=False)`

The one frame (Frame surface) for panels, cards, tiles, menus and dialogs. kind: panel, sel, lock, warn, hint, raised, pick, menu, dialog (pick, menu and dialog are owned by the stage card, menu and dialog components — not picked freely); add a size with a space: snug, tight, roomy or flush (no padding, clips a thumbnail). lock tints the frame in accent, dashed: there is no other choice, ever (the same idea as c_pick's lock) -- it is not dimmed, and it never means "can't use this right now". glow is a separate on/off, not a kind: any frame can glow, in that frame's own border colour (edge-glow for the plain panel, accent-glow for sel and lock, danger-glow for warn, halo-glow for hint, line-strong-glow for raised) -- never a fixed colour, and never picked on its own. disabled is a third, independent on/off: dashed line-strong border, dimmed, no glow, on any frame regardless of kind -- for something temporarily unavailable (the same idea as c_pick's dis and c_slider's enabled=False). A frame that was locked and also temporarily unavailable is both: kind='lock', disabled=True.

## `c_ring(pct, done=False)`

Progress ring: a touch-size box, a 44 ring centred in it, the percent (or a check when done) centred inside.

## `c_bar_cell(fill=0.0, sel=False, icon=None)`

Bar cell: a square control-size cell on panel with a line-strong hairline, either a bar from the bottom (inset
space-1, its height the value 0..1, line-strong) or a muted icon-lg. Selected: a 2 px accent border and an accent bar.

## `c_status(text, ok=True)`

The readiness line, the last line of Build's side panel: an accent check and "Ready to train", or a danger warn and
the short reason training cannot start. Small text in the icon's colour, the icon icon-sm. It sits at the panel's foot,
after everything else.

## `c_hint(title, text)`

What a setting does: a box at the canvas's top right while a finger is on its slider, and 2 s after it lifts. The
setting's name in overline accent, one short line under it. Touching another slider swaps it.

## `c_expand(title, open_=False, body='')`

Expand section: a full-width control-sm header, its title in overline muted and a 16 muted chevron at the end
(right when closed, down when open), no frame, only the press tint. Open, its rows follow with the panel's own gap.
It starts closed every time Build opens and keeps its state while the selection changes. Build's part settings put
the less used sliders in one called Advanced.
