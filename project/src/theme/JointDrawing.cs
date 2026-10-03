using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a plain joint (#626, owner decision 2026-10-03), shared by Build's canvas and the creature
/// in Training: a bearing in a cell. A thin ring in the beam's colour whose outer edge is the joint's
/// radius, so it is drawn at the size it collides at; a fine inner ring; and a soft tint inside in
/// the joint's state colour (<see cref="JointLook"/>). Drawn in window pixels (<see cref="UiPixelSpace"/>)
/// so it stays crisp at any zoom; <c>drawTransform</c> is the transform the caller draws with, and is
/// restored afterwards. Beams and Pistons stop under the ring (<see cref="BeamSpan"/>).
/// </summary>
public static class JointDrawing
{
    private const int _ringSegments = 48;
    private const float _innerRingPerRadius = 0.55f;

    // Below this radius on screen, in design pixels, the inner ring would blur into the outer one.
    private const float _innerRingShownFrom = 10;

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

        if (!simplified && look != JointLook.Loose && ShowsInnerRing(canvas, drawTransform, radius))
        {
            canvas.DrawArc(at, radius * _innerRingPerRadius * scale, 0, Mathf.Tau, _ringSegments, theme.Beam, theme.JointInnerRingWidth * scale, antialiased: true);
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// Whether a joint of <paramref name="radius"/> drawn by <paramref name="canvas"/> is big enough on
    /// screen for its inner ring. One that does not redraw as the camera zooms checks it to know when to.
    /// </summary>
    public static bool ShowsInnerRing(CanvasItem canvas, Transform2D drawTransform, float radius)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var toPixels = canvas.GetViewport().GetFinalTransform() * canvas.GetGlobalTransformWithCanvas() * drawTransform;
        var designPixel = UiPixelSpace.ScaleOf(canvas.GetViewport().GetFinalTransform());
        return radius * UiPixelSpace.ScaleOf(toPixels) >= _innerRingShownFrom * designPixel;
    }

    /// <summary>
    /// The part of a beam from joint <paramref name="a"/> to joint <paramref name="b"/> that is drawn:
    /// from ring to ring, ending on each ring's centre line. A beam has flat ends, so a joint of at
    /// least five units' radius hides its corners under its ring, and nothing reaches the middle.
    /// Null when the rings meet and nothing is left between them. <paramref name="ringWidth"/> is
    /// the ring's stroke, <see cref="VisualTheme.JointRingWidth"/>.
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
