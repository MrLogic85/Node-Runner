using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a plain joint in Build and Training (#626): a ring whose outer edge is the collision
/// radius, a fine inner ring and a tint for its <see cref="JointLook"/>. Drawn in window pixels
/// (<see cref="UiPixelSpace"/>); <c>drawTransform</c> is the caller's transform, restored afterwards.
/// </summary>
public static class JointDrawing
{
    private const int _ringSegments = 48;
    private const float _innerRingPerRadius = 0.55f;

    /// <summary>
    /// The joint at <paramref name="center"/> with <paramref name="radius"/>, tinted for its
    /// <paramref name="look"/>; <paramref name="simplified"/> for a shadow, the outer ring only.
    /// </summary>
    public static void DrawPlain(
        CanvasItem canvas,
        VisualTheme theme,
        Transform2D drawTransform,
        Vector2 center,
        float radius,
        JointLook look = JointLook.Plain,
        bool simplified = false)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        var at = toPixels * center;
        if (!simplified)
        {
            var fill = look switch
            {
                JointLook.Selected => theme.SelectionFill,
                JointLook.Loose => theme.DangerFill,
                _ => theme.JointFill,
            };
            canvas.DrawCircle(at, radius * scale, fill, antialiased: true);
        }

        canvas.DrawArc(at, RingCentre(theme.JointRingWidth, radius) * scale, 0, Mathf.Tau, _ringSegments, theme.Beam, theme.JointRingWidth * scale, antialiased: true);

        if (!simplified && look != JointLook.Loose)
        {
            canvas.DrawArc(at, radius * _innerRingPerRadius * scale, 0, Mathf.Tau, _ringSegments, theme.Beam, theme.JointInnerRingWidth * scale, antialiased: true);
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// The drawn part of a beam from <paramref name="a"/> to <paramref name="b"/>: from one ring's
    /// centre line to the other's, so its flat ends hide under rings of radius 5 or more. Null when
    /// the rings meet.
    /// </summary>
    public static (Vector2 Start, Vector2 End)? BeamSpan(float ringWidth, Vector2 a, float radiusA, Vector2 b, float radiusB)
    {
        var length = a.DistanceTo(b);
        var insetA = RingCentre(ringWidth, radiusA);
        var insetB = RingCentre(ringWidth, radiusB);
        if (length <= insetA + insetB)
        {
            return null;
        }

        var along = (b - a) / length;
        return (a + (along * insetA), b - (along * insetB));
    }

    private static float RingCentre(float ringWidth, float radius) => Math.Max(radius - (ringWidth / 2), 0);
}

/// <summary>Which state a plain joint's tint shows.</summary>
public enum JointLook
{
    Plain,
    Selected,
    Loose,
}
