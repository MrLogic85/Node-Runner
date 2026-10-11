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
    private const float _hatchWidth = 1.2f;

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
    /// <paramref name="joint"/>: on a circle of <paramref name="reach"/> round it, the joint's hub or,
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

    /// <summary>
    /// The two <c>halo</c> lines along a selected link, a Piston or a Spring, from joint
    /// <paramref name="a"/> to joint <paramref name="b"/>, <paramref name="offset"/> from its axis,
    /// drawn with <paramref name="toPixels"/> (<see cref="UiPixelSpace"/>, already entered). They
    /// stop at the hubs (#1086) or, when a joint is selected too, join its halo (#710).
    /// </summary>
    public static void DrawLink(
        CanvasItem canvas,
        Transform2D toPixels,
        float scale,
        VisualTheme theme,
        Vector2 a,
        Vector2 b,
        float radiusA,
        float radiusB,
        bool haloA,
        bool haloB,
        float offset)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var along = (b - a).Normalized();
        var across = along.Orthogonal();
        var (start, end) = LinkSpan(a, b, radiusA, radiusB, haloA, haloB, offset, along);
        var glow = theme.SelectionGlow;
        var width = theme.SelectedBeamLineWidth * scale;
        canvas.DrawLine(toPixels * (start + (across * offset)), toPixels * (end + (across * offset)), glow, width, antialiased: true);
        canvas.DrawLine(toPixels * (start - (across * offset)), toPixels * (end - (across * offset)), glow, width, antialiased: true);
    }

    /// <summary>
    /// A filled underlay band along a link, optionally hatched. Used by a selected Servo to mark
    /// the Fixed and Target links without hiding their own drawings.
    /// </summary>
    public static void DrawLinkBand(
        UiPixelPen pen,
        Vector2 a,
        Vector2 b,
        float halfWidth,
        Color fill,
        Color edge,
        Color? hatch,
        float hatchSpacing)
    {
        if (a == b)
        {
            return;
        }

        var along = (b - a).Normalized();
        var across = along.Orthogonal();
        var (start, end) = LinkSpan(a, b, 0, 0, false, false, halfWidth, along);
        var length = (end - start).Length();
        if (length <= 0)
        {
            return;
        }

        pen.Polygon(
            [
                start + (across * halfWidth),
                end + (across * halfWidth),
                end - (across * halfWidth),
                start - (across * halfWidth),
            ],
            fill);
        pen.Line(start + (across * halfWidth), end + (across * halfWidth), edge, UiSize.Stroke.Hair);
        pen.Line(start - (across * halfWidth), end - (across * halfWidth), edge, UiSize.Stroke.Hair);
        if (hatch is not { } hatchColor)
        {
            return;
        }

        var hatchLines = new List<Vector2[]>();
        for (var t = -2 * halfWidth; t < length; t += hatchSpacing)
        {
            var t0 = Math.Max(t, 0);
            var t1 = Math.Min(t + (2 * halfWidth), length);
            if (t1 <= t0)
            {
                continue;
            }

            var p0 = start + (along * t0) + (across * (-halfWidth + (t0 - t)));
            var p1 = start + (along * t1) + (across * (-halfWidth + (t1 - t)));
            hatchLines.Add([p0, p1]);
        }

        pen.Strokes(hatchLines, hatchColor, _hatchWidth);
    }

    /// <summary>
    /// <see cref="DrawLinkBand"/> bent round a circle: a filled ring band between two radii,
    /// optionally hatched across. A selected Servo marks a Wheel side with it under the tyre (#1118).
    /// </summary>
    public static void DrawRingBand(
        UiPixelPen pen,
        Vector2 centre,
        float innerRadius,
        float outerRadius,
        Color fill,
        Color edge,
        Color? hatch,
        float hatchSpacing)
    {
        var middle = (innerRadius + outerRadius) / 2;
        pen.Ring(centre, middle, fill, outerRadius - innerRadius, _ringSegments);
        pen.Ring(centre, innerRadius, edge, UiSize.Stroke.Hair, _ringSegments);
        pen.Ring(centre, outerRadius, edge, UiSize.Stroke.Hair, _ringSegments);
        if (hatch is not { } hatchColor)
        {
            return;
        }

        // Evenly spaced round the middle so the lines meet without a seam, each leaning 45° like a link's.
        var count = Math.Max(1, (int)Math.Round(Mathf.Tau * middle / hatchSpacing));
        var lean = (outerRadius - innerRadius) / middle;
        var hatchLines = new Vector2[count][];
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / count;
            hatchLines[i] = [centre + (Vector2.FromAngle(angle) * innerRadius), centre + (Vector2.FromAngle(angle + lean) * outerRadius)];
        }

        pen.Strokes(hatchLines, hatchColor, _hatchWidth);
    }

    /// <summary>
    /// Where a link's selection lines run: to the selected joints' halos, else to the hubs (#1086),
    /// else, when the joints crowd too close for either, centre to centre so the mark never vanishes.
    /// </summary>
    private static (Vector2 Start, Vector2 End) LinkSpan(
        Vector2 a, Vector2 b, float radiusA, float radiusB, bool haloA, bool haloB, float offset, Vector2 along)
    {
        var reachA = haloA ? (float)SelectionMarks.JointHalo(radiusA) : JointDrawing.HubRadius;
        var reachB = haloB ? (float)SelectionMarks.JointHalo(radiusB) : JointDrawing.HubRadius;
        var start = LineEnd(a, b, reachA, offset);
        var end = LineEnd(b, a, reachB, offset);
        if ((end - start).Dot(along) > 0)
        {
            return (start, end);
        }

        return haloA || haloB ? LinkSpan(a, b, radiusA, radiusB, false, false, offset, along) : (a, b);
    }
}
