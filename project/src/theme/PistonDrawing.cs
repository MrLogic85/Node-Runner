using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Piston between two joints (#451), shared by Build's canvas and the creature in
/// Training: a thin <c>accent</c> rod from ring to ring, a cylinder at its first joint and a cap
/// at its second. The cylinder starts at joint A's edge and is as long as its travel (#870, #835),
/// so a shorter Stroke gives a shorter cylinder; the rod runs on to joint B. Selected, it gets the beam's two <c>halo</c> lines,
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

    /// <summary>Half a Piston's stroke tick, across it; a Spring's spans its seats (<see cref="SpringDrawing.SeatHalf"/>).</summary>
    public const float TickHalf = 4;

    private const float _hairline = 1;
    private const float _dash = 6;
    private const float _minCylinder = 4;
    private const int _cornerSegments = 3;

    // A tick this close to joint B's centre is on it, whichever side rounding puts it.
    private const float _onCentre = 0.01f;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>.</param>
    /// <param name="theme">The theme its fills come from.</param>
    /// <param name="a">Joint A's centre, where the cylinder sits.</param>
    /// <param name="b">Joint B's centre, where the rod ends in its cap.</param>
    /// <param name="radiusA">Joint A's radius: the cylinder starts at its edge.</param>
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
        var cylinderHalf = theme.BeamWidth * _cylinderPerBeam / 2;

        // Like a beam, the rod stops under the joint rings (#626), or, when they meet, runs centre to centre.
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) ?? (a, b);
        canvas.DrawLine(toPixels * rodStart, toPixels * rodEnd, line, theme.BeamWidth * _rodPerBeam * scale, antialiased: true);

        // It never runs past joint B's edge.
        var cylinderStart = a + (along * radiusA);
        var cylinderLength = Math.Min(radiusA + travel, a.DistanceTo(b) - radiusB);
        var cylinderEnd = a + (along * Math.Max(cylinderLength, radiusA + _minCylinder));
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
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// A Piston's stroke (#704): <c>halo</c> ticks at its shortest and longest length from joint A's
    /// centre, and a dashed line from joint B's edge on to the longest when that is past it.
    /// </summary>
    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelSpace"/>; restored afterwards.</param>
    /// <param name="theme">The theme its colour comes from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="radiusB">Joint B's radius: the dashed line starts at its edge, and a tick inside it moves out to it.</param>
    /// <param name="shortest">The Piston's or Spring's shortest length, centre to centre.</param>
    /// <param name="longest">The Piston's or Spring's longest length, centre to centre.</param>
    /// <param name="tickHalf">Half a tick's length across the link; a Spring's spans its seats (#835).</param>
    public static void DrawStroke(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2 a, Vector2 b, float radiusB, float shortest, float longest, float tickHalf)
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
        var longestPoint = a + (along * longest);
        if (a.DistanceTo(b) + radiusB < longest)
        {
            canvas.DrawDashedLine(toPixels * (b + (along * radiusB)), toPixels * longestPoint, glow, _hairline * scale, _dash * scale, antialiased: true);
        }

        foreach (var length in new[] { shortest, longest })
        {
            var tick = b + (along * TickPastB(length - a.DistanceTo(b), radiusB + (_line / 2)));
            canvas.DrawLine(toPixels * (tick + (across * tickHalf)), toPixels * (tick - (across * tickHalf)), glow, _line * scale, antialiased: true);
        }

        canvas.DrawSetTransformMatrix(drawTransform);
    }

    /// <summary>
    /// Where a tick <paramref name="past"/> joint B's centre is drawn: there, or, inside a ring of
    /// <paramref name="clear"/> round the joint, on its edge on that side, so a stop on the joint
    /// reads as a stop rather than a slash through it (#835). On the centre it goes outside.
    /// </summary>
    public static float TickPastB(float past, float clear) =>
        Math.Abs(past) >= clear ? past : past < -_onCentre ? -clear : clear;

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
