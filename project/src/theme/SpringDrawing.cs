using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Spring between two joints (#453), shared by Build's canvas and the creature in
/// Training: a zigzag coil in <c>line-strong</c>, like the Links tab's spring icon, with a short
/// straight lead at each end that stops under the joint rings. The coil keeps its count of peaks,
/// so they spread as the Spring stretches and bunch as it squeezes. It is the Piston cylinder's
/// width, and selected it gets the same two <c>halo</c> lines. <c>line-strong</c>, not
/// <c>accent</c>, as the Spring has no brain ports. Drawn in window pixels
/// (<see cref="UiPixelSpace"/>) so it stays crisp at any zoom; <c>drawTransform</c> is the
/// transform the caller draws with, and is restored afterwards.
/// </summary>
public static class SpringDrawing
{
    /// <summary>How many peaks the coil has, whatever its length.</summary>
    public const int Peaks = 4;

    private const float _line = UiSize.Stroke.Signal;
    private const float _coilPerBeam = 7f / 3f;
    private const float _lead = 4;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>.</param>
    /// <param name="theme">The theme its widths come from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="radiusA">Joint A's radius: the coil starts at its edge.</param>
    /// <param name="radiusB">Joint B's radius: the coil ends at its edge.</param>
    /// <param name="line">The coil colour: <c>line-strong</c>, or <c>danger</c> while too short.</param>
    /// <param name="selected">Whether to draw the selection halo.</param>
    /// <param name="haloA">Whether joint A is selected too, so the selection lines end on its halo ring.</param>
    /// <param name="haloB">Whether joint B is selected too.</param>
    public static void Draw(
        CanvasItem canvas,
        Transform2D drawTransform,
        VisualTheme theme,
        Vector2 a,
        Vector2 b,
        float radiusA,
        float radiusB,
        Color line,
        bool selected,
        bool haloA = false,
        bool haloB = false)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        if (a == b)
        {
            return;
        }

        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        var half = theme.BeamWidth * _coilPerBeam / 2;

        // Like a beam, it stops under the joint rings (#626), or, when they meet, runs straight centre to centre.
        if (JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) is var (start, end))
        {
            var along = (b - a).Normalized();
            var coilStart = a + (along * radiusA);
            var coilEnd = b - (along * radiusB);
            var lead = Math.Min(_lead, coilStart.DistanceTo(coilEnd) / 4);
            Vector2[] points = [start, .. Coil(coilStart + (along * lead), coilEnd - (along * lead), half), end];
            canvas.DrawPolyline([.. points.Select(point => toPixels * point)], line, _line * scale, antialiased: true);
        }
        else
        {
            canvas.DrawLine(toPixels * a, toPixels * b, line, _line * scale, antialiased: true);
        }

        if (selected)
        {
            // The gap is measured from the coil's outer edge.
            var offset = half + (_line / 2) + (float)SelectionMarks.Gap;
            SelectionDrawing.DrawLink(canvas, toPixels, scale, theme, a, b, radiusA, radiusB, haloA, haloB, offset);
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// The coil from <paramref name="start"/> to <paramref name="end"/>, both on its centre line:
    /// <see cref="Peaks"/> peaks <paramref name="half"/> to alternate sides, evenly along it.
    /// </summary>
    public static Vector2[] Coil(Vector2 start, Vector2 end, float half)
    {
        var across = (end - start).Normalized().Orthogonal();
        var points = new Vector2[Peaks + 2];
        points[0] = start;
        for (var peak = 1; peak <= Peaks; peak++)
        {
            var side = peak % 2 == 1 ? -1 : 1;
            points[peak] = start.Lerp(end, (peak - 0.5f) / Peaks) + (across * (half * side));
        }

        points[^1] = end;
        return points;
    }
}
