using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>The marks a joint motor, a Servo or a Velocity motor, draws over itself.</summary>
public static class JointMotorDrawing
{
    private const int _ringSegments = 64;

    /// <summary>The selection halo round a motor of <paramref name="radius"/>.</summary>
    public static void DrawHalo(UiPixelPen pen, VisualTheme theme, float radius)
    {
        ArgumentNullException.ThrowIfNull(theme);
        pen.Ring(Vector2.Zero, (float)SelectionMarks.JointHalo(radius), theme.SelectionGlow, theme.SelectionRingWidth, _ringSegments);
    }

    /// <summary>
    /// The "!" badge of a motor missing a link, on its edge at the screen's upper right,
    /// upright however the motor turns.
    /// </summary>
    public static void DrawDangerBadge(UiPixelPen pen, VisualTheme theme, float radius)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var turn = pen.ToPixels.Rotation;
        var up = Vector2.Up.Rotated(-turn);
        var badge = Vector2.FromAngle((-Mathf.Pi / 4) - turn) * radius;
        pen.Disc(badge, 8, theme.ArenaBackground);
        pen.Disc(badge, 7, theme.Danger);
        pen.Line(badge + (up * 4.2f), badge - (up * 1.2f), theme.ArenaBackground, 2);
        pen.Disc(badge - (up * 3.6f), 1.2f, theme.ArenaBackground);
    }
}
