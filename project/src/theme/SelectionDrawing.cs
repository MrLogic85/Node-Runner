using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// How a selected part is marked (#624), shared by Build's canvas and the creature in Training: a
/// joint gets an unfilled <c>halo</c> ring with a gap around it, like the reference, and a beam a
/// thin <c>halo</c> line along each side. Both are drawn in window pixels (<see cref="UiPixelSpace"/>)
/// so they stay sharp at any zoom; <c>drawTransform</c> is the transform the caller draws with, and
/// is restored afterwards. Every mark sits <see cref="SelectionMarks.Gap"/> outside its part (#710).
/// </summary>
public static class SelectionDrawing
{
    private const int _ringSegments = 64;

    /// <summary>The ring around a selected joint at <paramref name="center"/>, <paramref name="radius"/> out.</summary>
    public static void DrawJoint(CanvasItem canvas, VisualTheme theme, Transform2D drawTransform, Vector2 center, float radius)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        canvas.DrawArc(toPixels * center, radius * scale, 0, Mathf.Tau, _ringSegments, theme.SelectionGlow, theme.SelectionRingWidth * scale, antialiased: true);
        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// Where a selected link's lines, <paramref name="offset"/> from its axis, end at the joint at
    /// <paramref name="joint"/>: on a circle of <paramref name="reach"/> round it, the joint's edge or,
    /// when the joint is selected too, its halo ring, so a group reads as one outline (#710).
    /// </summary>
    public static Vector2 LineEnd(Vector2 joint, Vector2 other, float reach, float offset) =>
        joint + (joint.DirectionTo(other) * (float)SelectionMarks.LineEnd(reach, offset));

    /// <summary>The two lines along a selected beam from <paramref name="start"/> to <paramref name="end"/>, <paramref name="offset"/> from its centre line.</summary>
    public static void DrawBeam(CanvasItem canvas, Transform2D drawTransform, Color color, float offset, float width, Vector2 start, Vector2 end)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (start == end)
        {
            return;
        }

        var side = (end - start).Normalized().Orthogonal() * offset;
        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var pixelWidth = width * UiPixelSpace.ScaleOf(toPixels);
        canvas.DrawLine(toPixels * (start + side), toPixels * (end + side), color, pixelWidth, antialiased: true);
        canvas.DrawLine(toPixels * (start - side), toPixels * (end - side), color, pixelWidth, antialiased: true);
        canvas.DrawSetTransformMatrix(drawTransform);
    }
}
