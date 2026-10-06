using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Piston between two joints (#451), shared by Build's canvas and the creature in
/// Training: a thin <c>accent</c> rod from ring to ring, a cylinder at its first joint and a cap
/// at its second. The cylinder starts at joint A's edge and is as long as its travel, which is on
/// the gap between the joints' edges (#870, #835), so a shorter Stroke gives a shorter cylinder; the rod runs on to joint B. Selected, it gets the beam's two <c>halo</c> lines,
/// which stop at the joints' edges or join a selected joint's halo (#710). Its stroke ticks are
/// drawn apart, by <see cref="DrawStroke"/>, so a view can put them over the joints. Drawn in window
/// pixels (<see cref="UiPixelSpace"/>) so it stays crisp at any zoom; <c>drawTransform</c> is the
/// transform the caller draws with, and is restored afterwards.
/// </summary>
public static class PistonDrawing
{
    private const float _line = UiSize.Stroke.Signal;
    private const float _rodPerBeam = 0.5f;
    private const float _cylinderPerBeam = 7f / 3f;
    private const float _cylinderRadius = 2;
    private const float _capLength = 10;

    private const float _hairline = 1;
    private const float _dash = 6;
    private const float _minCylinder = 4;
    private const int _cornerSegments = 3;

    private const float _restRadius = 3;
    private const int _restSegments = 24;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>.</param>
    /// <param name="theme">The theme its fills come from.</param>
    /// <param name="a">Joint A's centre, where the cylinder sits.</param>
    /// <param name="b">Joint B's centre, where the rod ends in its cap.</param>
    /// <param name="radiusA">Joint A's radius: the rod and cylinder start at its edge.</param>
    /// <param name="radiusB">Joint B's radius: the cap sits at its edge.</param>
    /// <param name="travel">The Piston's travel: its longest length less its shortest.</param>
    /// <param name="line">The rod, cylinder and cap colour: <c>accent</c>, or <c>danger</c> while too short.</param>
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
        float travel,
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
        var along = (b - a).Normalized();
        var across = along.Orthogonal();
        var cylinderHalf = CylinderHalf(theme);

        // Like a beam, the rod stops under the joint rings (#626), or, when they meet, runs centre to centre.
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) ?? (a, b);
        canvas.DrawLine(toPixels * rodStart, toPixels * rodEnd, line, theme.BeamWidth * _rodPerBeam * scale, antialiased: true);

        // The travel is on the gap between the joints' edges (#835), so at its shortest joint B's edge
        // meets the cylinder's end at most; it never runs past joint B's edge.
        var cylinderStart = a + (along * radiusA);
        var cylinderLength = Math.Min(radiusA + Math.Max(travel, _minCylinder), a.DistanceTo(b) - radiusB);
        var cylinderEnd = a + (along * Math.Max(cylinderLength, radiusA + _minCylinder));
        var cylinder = Cylinder(cylinderStart, cylinderEnd, along, across, cylinderHalf);
        canvas.DrawColoredPolygon([.. cylinder.Select(point => toPixels * point)], theme.SensorFill);
        canvas.DrawPolyline([.. cylinder.Append(cylinder[0]).Select(point => toPixels * point)], line, _line * scale, antialiased: true);

        var cap = b - (along * radiusB);
        canvas.DrawLine(toPixels * (cap + (across * (_capLength / 2))), toPixels * (cap - (across * (_capLength / 2))), line, _line * scale, antialiased: true);

        if (selected)
        {
            SelectionDrawing.DrawLink(canvas, toPixels, scale, theme, a, b, radiusA, radiusB, haloA, haloB, SelectionOffset(theme));
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// Half a Piston's stroke tick, across it: out to the cylinder's outer edge, so the shortest tick is
    /// as wide as the cylinder whose end it marks (#931). A Spring's spans its seats
    /// (<see cref="SpringDrawing.SeatHalf"/>); both stop a gap inside the selection outline.
    /// </summary>
    public static float TickHalf(VisualTheme theme) => CylinderHalf(theme) + (_line / 2);

    private static float CylinderHalf(VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.BeamWidth * _cylinderPerBeam / 2;
    }

    // The selection outline's distance from the axis, a gap past the cylinder outline's outer edge.
    private static float SelectionOffset(VisualTheme theme) => TickHalf(theme) + (float)SelectionMarks.Gap;

    /// <summary>
    /// A Piston's stroke (#704): <c>halo</c> ticks at its shortest and longest length from joint A's
    /// centre, joined by a dashed line, over the joints like a Camera's rays. A Spring's also rings
    /// its rest length (#835); the line leaves the ring's hole clear.
    /// </summary>
    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>; restored afterwards.</param>
    /// <param name="theme">The theme its colour comes from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="shortest">The Piston's or Spring's shortest length, centre to centre.</param>
    /// <param name="longest">The Piston's or Spring's longest length, centre to centre.</param>
    /// <param name="tickHalf">Half a tick's length across the link; a Spring's spans its seats (#835).</param>
    /// <param name="rest">A Spring's rest length, centre to centre, ringed; none for a Piston.</param>
    public static void DrawStroke(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2 a, Vector2 b, float shortest, float longest, float tickHalf, float? rest = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        if (a == b)
        {
            return;
        }

        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        var along = (b - a).Normalized();
        var across = along.Orthogonal();
        var glow = theme.SelectionGlow;
        // The dashed line leaves the rest ring's hole clear, so the ring still reads as a ring.
        var clear = _restRadius + (_line / 2);
        var (gapStart, gapEnd) = rest is { } ringAt ? (ringAt - clear, ringAt + clear) : (longest, longest);
        foreach (var (from, to) in new[] { (shortest, Math.Min(longest, gapStart)), (Math.Max(shortest, gapEnd), longest) })
        {
            if (from < to)
            {
                canvas.DrawDashedLine(toPixels * (a + (along * from)), toPixels * (a + (along * to)), glow, _hairline * scale, _dash * scale, antialiased: true);
            }
        }

        foreach (var length in new[] { shortest, longest })
        {
            var tick = a + (along * length);
            canvas.DrawLine(toPixels * (tick + (across * tickHalf)), toPixels * (tick - (across * tickHalf)), glow, _line * scale, antialiased: true);
        }

        if (rest is { } restLength)
        {
            canvas.DrawArc(toPixels * (a + (along * restLength)), _restRadius * scale, 0, Mathf.Tau, _restSegments, glow, _line * scale, antialiased: true);
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>A rounded rectangle from start to end, 2 × half thick, walked round its corners in order.</summary>
    internal static Vector2[] Cylinder(Vector2 start, Vector2 end, Vector2 along, Vector2 across, float half)
    {
        var middle = (start + end) / 2;
        var halfLength = start.DistanceTo(end) / 2;
        var radius = Math.Min(_cylinderRadius, Math.Min(half, halfLength));
        Vector2[] corners =
        [
            new(halfLength - radius, -half + radius),
            new(halfLength - radius, half - radius),
            new(-halfLength + radius, half - radius),
            new(-halfLength + radius, -half + radius),
        ];
        var points = new Vector2[4 * (_cornerSegments + 1)];
        var index = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var first = (corner - 1) * Mathf.Pi / 2;
            for (var step = 0; step <= _cornerSegments; step++)
            {
                var angle = first + (step * Mathf.Pi / 2 / _cornerSegments);
                var local = corners[corner] + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                points[index++] = middle + (along * local.X) + (across * local.Y);
            }
        }

        return points;
    }
}
