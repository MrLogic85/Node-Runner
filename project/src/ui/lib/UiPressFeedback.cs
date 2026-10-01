using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The one press feedback (#286): while a control is held down it shows an <c>accent</c> tint at
/// <c>alpha-soft</c> over its own fill. Hover shows nothing, because desktop is not a target yet
/// and on a phone the pointer Godot emulates from touch stays where the finger lifted. A selected
/// or disabled control shows no tint. A destructive control tints with <c>danger</c> instead, so
/// committing to it never flashes the "go" colour. On a fill that is already <c>accent</c> (a
/// primary button) the tint is <c>on-accent</c>, so the press still shows. Colours come from the
/// theme, so Neon and Paper follow.
/// </summary>
public static class UiPressFeedback
{
    /// <summary>True while <paramref name="button"/> is held down and is neither selected nor disabled.</summary>
    public static bool Shows(BaseButton button, bool selected)
    {
        ArgumentNullException.ThrowIfNull(button);
        return !selected && !button.Disabled && Shows(button.GetDrawMode());
    }

    /// <summary>Pressed draw modes show the tint; hover alone does not.</summary>
    public static bool Shows(BaseButton.DrawMode mode) =>
        mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed;

    /// <summary>
    /// The tint's colour over a control filled with <paramref name="fill"/>;
    /// <paramref name="danger"/> marks a destructive control.
    /// </summary>
    public static UiTokens.Color TintColorOver(UiTokens.Color fill, bool danger) =>
        danger ? UiTokens.Color.Danger
        : fill == UiTokens.Color.Accent ? UiTokens.Color.OnAccent
        : UiTokens.Color.Accent;

    public static Color Tint(Control control, UiTokens.Color fill, bool danger) =>
        UiThemeLookup.Color(control, TintColorOver(fill, danger)).WithAlpha(UiThemeLookup.Alpha(control, UiTokens.Alpha.Soft));

    /// <summary>
    /// Draws the tint over <paramref name="control"/>'s whole rect, rounded by
    /// <paramref name="corners"/>, for a control filled with <paramref name="fill"/>.
    /// </summary>
    public static void Draw(Control control, UiCorners corners, UiTokens.Color fill, bool danger)
    {
        ArgumentNullException.ThrowIfNull(control);
        corners.Fill(control, new Rect2(Vector2.Zero, control.Size), Tint(control, fill, danger));
    }
}
