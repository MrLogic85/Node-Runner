using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Spring between two joints (#453) as a coilover (#807), shared by Build's canvas, the
/// creature in Training and the thumbnails: a <c>line-strong</c> rod from ring to ring, a seat
/// plate just outside each joint's edge, a <c>panel</c> damper body in the middle, and a helix wound round it,
/// its front strokes in <c>muted</c> over the body and its back strokes as <c>line-strong</c>
/// hairlines under it. The coil keeps its <see cref="Turns"/>, so they spread as the Spring
/// stretches and bunch as it squeezes, while the body keeps the length it was built with. No
/// <c>accent</c>, as the Spring has no brain ports; all of it turns <c>danger</c> while too short.
/// Selected, it gets the Piston's two <c>halo</c> lines outside its seats. Drawn through
/// <see cref="UiPixelPen"/>, so it stays crisp at any zoom.
/// </summary>
public static class SpringDrawing
{
    /// <summary>How many turns the coil has, whatever its length, while they fit.</summary>
    public const int Turns = 7;

    private const float _line = UiSize.Stroke.Signal;
    private const float _hair = UiSize.Stroke.Hair;
    private const float _rodPerBeam = 0.5f;
    private const float _coilRadiusPerBeam = 4f / 3f;
    private const float _seatHalfPerBeam = 5f / 3f;
    private const float _bodyHalfPerBeam = 0.75f;

    // The coil's front strokes bow this far towards joint B, so the turns read as a helix seen from the side.
    private const float _tilt = 4;
    private const float _lead = 3;
    private const float _minPitch = 3;
    private const float _minSpan = 4;
    private const float _bodyShare = 0.4f;
    private const float _minBody = 16;
    private const float _maxBody = 48;
    private const float _bodyClearance = 12;
    private const float _minBodyLength = 6;
    private const int _halfTurnSegments = 12;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelPen"/>.</param>
    /// <param name="theme">The theme its colours and widths come from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="radiusA">Joint A's radius: the seat sits a short lead outside its edge.</param>
    /// <param name="radiusB">Joint B's radius.</param>
    /// <param name="built">The Spring's built length, centre to centre, which sets the damper body's length.</param>
    /// <param name="danger">Whether it is too short, and drawn in <c>danger</c>.</param>
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
        float built,
        bool danger,
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

        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        var line = danger ? theme.Danger : theme.Beam;
        var coil = danger ? theme.Danger : theme.SpringCoil;
        var along = (b - a).Normalized();
        var across = along.Orthogonal();
        var seatHalf = theme.BeamWidth * _seatHalfPerBeam;

        // Like a Piston's rod, it stops under the joint rings (#626), or, when they meet, runs centre to centre.
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) ?? (a, b);
        pen.Line(rodStart, rodEnd, line, theme.BeamWidth * _rodPerBeam);

        var seatA = a + (along * (radiusA + _lead));
        var seatB = b - (along * (radiusB + _lead));
        var span = (seatB - seatA).Dot(along);
        if (span > _minSpan)
        {
            var radius = theme.BeamWidth * _coilRadiusPerBeam;
            var turns = CoilTurns(span);
            foreach (var stroke in Helix(seatA, seatB, across, turns, radius, front: false))
            {
                pen.Polyline(stroke, line, _hair);
            }

            var length = BodyLength(built - radiusA - radiusB - (2 * _lead), span);
            if (length >= _minBodyLength)
            {
                var middle = (seatA + seatB) / 2;
                var body = PistonDrawing.Cylinder(middle - (along * (length / 2)), middle + (along * (length / 2)), along, across, theme.BeamWidth * _bodyHalfPerBeam);
                pen.Polygon(body, theme.SensorFill);
                pen.Polyline([.. body, body[0]], line, _line);
            }

            foreach (var stroke in Helix(seatA, seatB, across, turns, radius, front: true))
            {
                pen.Polyline(stroke, coil, _line);
            }

            foreach (var seat in new[] { seatA, seatB })
            {
                pen.Line(seat + (across * seatHalf), seat - (across * seatHalf), line, _line);
            }
        }

        if (selected)
        {
            // The seats are its widest part; the gap is measured from their ends.
            var offset = seatHalf + (float)SelectionMarks.Gap;
            SelectionDrawing.DrawLink(canvas, pen.ToPixels, pen.Scale, theme, a, b, radiusA, radiusB, haloA, haloB, offset);
        }
    }

    /// <summary>
    /// The coil's turns over <paramref name="span"/>, seat to seat: <see cref="Turns"/>, or fewer
    /// once they would stack closer than solid.
    /// </summary>
    public static int CoilTurns(float span) =>
        span / Turns >= _minPitch ? Turns : Math.Max(1, (int)(span / _minPitch));

    /// <summary>
    /// The damper body's length: a share of the <paramref name="builtSpan"/> seat to seat, kept as
    /// the Spring moves, but never so long that the coil's current <paramref name="span"/> cannot
    /// show round it.
    /// </summary>
    public static float BodyLength(float builtSpan, float span) =>
        Math.Min(Math.Clamp(_bodyShare * builtSpan, _minBody, _maxBody), span - _bodyClearance);

    /// <summary>
    /// The front or back half of each turn of a helix from <paramref name="start"/> to
    /// <paramref name="end"/>, seen from the side: <paramref name="radius"/> to each side of the
    /// line between them, the front halves bowed <see cref="_tilt"/> towards the end.
    /// </summary>
    private static IEnumerable<Vector2[]> Helix(Vector2 start, Vector2 end, Vector2 across, int turns, float radius, bool front)
    {
        var along = (end - start).Normalized();
        var first = front ? 0 : Mathf.Pi;
        for (var turn = 0; turn < turns; turn++)
        {
            var stroke = new Vector2[_halfTurnSegments + 1];
            for (var step = 0; step <= _halfTurnSegments; step++)
            {
                var angle = (Mathf.Tau * turn) + first + (Mathf.Pi * step / _halfTurnSegments);
                var point = start.Lerp(end, angle / (Mathf.Tau * turns));
                stroke[step] = point + (along * (_tilt * Mathf.Sin(angle))) - (across * (radius * Mathf.Cos(angle)));
            }

            yield return stroke;
        }
    }
}
