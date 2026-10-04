using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Piston between two joints (#451), shared by Build's canvas and the creature in
/// Training: a thin <c>accent</c> rod from ring to ring, a cylinder at its first joint and a cap
/// at its second. The cylinder is the stroke's share of the built length from joint A's centre, so
/// ±50% draws half the Piston. Selected, it gets the
/// beam's two <c>halo</c> lines, which stop at the joints' edges or join a selected joint's halo
/// (#710), and, unless it is in a group, ticks at its shortest and longest length. Drawn in window
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
    private const float _tickLength = 8;
    private const float _hairline = 1;
    private const float _dash = 6;
    private const float _minCylinder = 4;
    private const int _cornerSegments = 3;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>.</param>
    /// <param name="theme">The theme its fills come from.</param>
    /// <param name="a">Joint A's centre, where the cylinder sits.</param>
    /// <param name="b">Joint B's centre, where the rod ends in its cap.</param>
    /// <param name="radiusA">Joint A's radius: the cylinder starts at its edge.</param>
    /// <param name="radiusB">Joint B's radius: the cap sits at its edge.</param>
    /// <param name="shortest">The Piston's shortest length, centre to centre.</param>
    /// <param name="longest">The Piston's longest length, centre to centre.</param>
    /// <param name="line">The rod, cylinder and cap colour: <c>accent</c>, or <c>danger</c> while too short.</param>
    /// <param name="selected">Whether to draw the selection halo.</param>
    /// <param name="showStroke">Whether a selected Piston also shows its stroke ticks: only while its Stroke can be set (#704).</param>
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
        float shortest,
        float longest,
        Color line,
        bool selected,
        bool showStroke = true,
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
        var cylinderHalf = theme.BeamWidth * _cylinderPerBeam / 2;

        // Like a beam, the rod stops under the joint rings (#626), or, when they meet, runs centre to centre.
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) ?? (a, b);
        canvas.DrawLine(toPixels * rodStart, toPixels * rodEnd, line, theme.BeamWidth * _rodPerBeam * scale, antialiased: true);

        // Half the travel, (longest − shortest) / 2, is the stroke's share of the built length.
        var cylinderStart = a + (along * radiusA);
        var cylinderEnd = a + (along * Math.Max((longest - shortest) / 2, radiusA + _minCylinder));
        var cylinder = Cylinder(cylinderStart, cylinderEnd, along, across, cylinderHalf);
        canvas.DrawColoredPolygon([.. cylinder.Select(point => toPixels * point)], theme.SensorFill);
        canvas.DrawPolyline([.. cylinder.Append(cylinder[0]).Select(point => toPixels * point)], line, _line * scale, antialiased: true);

        var cap = b - (along * radiusB);
        canvas.DrawLine(toPixels * (cap + (across * (_capLength / 2))), toPixels * (cap - (across * (_capLength / 2))), line, _line * scale, antialiased: true);

        if (selected)
        {
            // The gap is measured from the cylinder outline's outer edge.
            var offset = cylinderHalf + (_line / 2) + (float)SelectionMarks.Gap;
            SelectionDrawing.DrawLink(canvas, toPixels, scale, theme, a, b, radiusA, radiusB, haloA, haloB, offset);
            if (showStroke)
            {
                DrawStroke(canvas, toPixels, scale, theme, a, b, along, across, shortest, longest);
            }
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    private static void DrawStroke(
        CanvasItem canvas,
        Transform2D toPixels,
        float scale,
        VisualTheme theme,
        Vector2 a,
        Vector2 b,
        Vector2 along,
        Vector2 across,
        float shortest,
        float longest)
    {
        var glow = theme.SelectionGlow;
        var longestPoint = a + (along * longest);
        if (a.DistanceTo(b) < longest)
        {
            canvas.DrawDashedLine(toPixels * b, toPixels * longestPoint, glow, _hairline * scale, _dash * scale, antialiased: true);
        }

        foreach (var tick in new[] { a + (along * shortest), longestPoint })
        {
            canvas.DrawLine(toPixels * (tick + (across * (_tickLength / 2))), toPixels * (tick - (across * (_tickLength / 2))), glow, _line * scale, antialiased: true);
        }
    }

    // A rounded rectangle from start to end, 2 × half thick, walked round its corners in order.
    private static Vector2[] Cylinder(Vector2 start, Vector2 end, Vector2 along, Vector2 across, float half)
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
