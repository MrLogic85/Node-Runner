using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Spring between two joints (#453) as a coilover (#807), shared by Build's canvas, the
/// creature in Training and the thumbnails: a <c>line-strong</c> rod from ring to ring, a seat
/// plate just outside each joint's edge, a <c>panel</c> damper body from seat A, and a helix wound round it,
/// its front strokes in <c>muted</c> over the body and its back strokes as <c>line-strong</c>
/// strokes under it, all of the same wire with round ends, each side in one call (#835). Like a Piston's cylinder, the body is as long as the Spring's travel (#835).
/// The coil's wire is thicker the stiffer the Spring, and its turns are wound for its rest length,
/// so a Preload that presses it harder against a stop packs in more of them. No
/// <c>accent</c>, as the Spring has no brain ports; all of it turns <c>danger</c> while too short.
/// Selected, it gets the Piston's two <c>halo</c> lines outside its seats. Drawn through
/// <see cref="UiPixelPen"/>, so it stays crisp at any zoom.
/// </summary>
public static class SpringDrawing
{
    /// <summary>The fewest turns a coil has.</summary>
    public const int MinTurns = 3;

    /// <summary>The seat-to-seat rest span past which a coil's turns grow only with its square root.</summary>
    public const float LongSpan = 200;

    /// <summary>The fewest segments in a half turn, on a coil too small on screen to show its curve.</summary>
    public const int MinHalfTurnSegments = 3;

    /// <summary>The most segments in a half turn, on a coil seen up close.</summary>
    public const int MaxHalfTurnSegments = 12;

    private const float _line = UiSize.Stroke.Signal;
    private const float _rodPerBeam = 0.5f;
    private const float _coilRadiusPerBeam = 4f / 3f;
    private const float _seatHalfPerBeam = 5f / 3f;
    private const float _bodyHalfPerBeam = 0.75f;

    // The coil's front strokes bow this far towards joint B, so the turns read as a helix seen from the side.
    private const float _tilt = 4;
    private const float _lead = 3;

    // The wire runs from a hairline on the softest Spring to as wide as a drawn beam on the stiffest,
    // in proportion (owner decision, #835).
    private const float _maxWire = UiSize.Widget.CreatureBeamWidth;

    // A free coil is this many times as long as stacked solid, as far as a Spring can be squeezed.
    private const float _freePerSolid = 3;

    // Two turns stacked solid sit this much more than the wire apart, as the front strokes bow.
    private const float _solidGap = 2;
    private const float _minSpan = 4;
    private const float _minBodyLength = 6;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelPen"/>.</param>
    /// <param name="theme">The theme its colours and widths come from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="radiusA">Joint A's radius: the seat sits a short lead outside its edge.</param>
    /// <param name="radiusB">Joint B's radius.</param>
    /// <param name="travel">The Spring's travel, its longest length less its shortest, which sets the damper body's length.</param>
    /// <param name="rest">The Spring's rest length, centre to centre, which sets the coil's turns.</param>
    /// <param name="stiffness">The Spring's Stiffness, in N/m, which sets the coil's wire.</param>
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
        float travel,
        float rest,
        double stiffness,
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
        var seatHalf = SeatHalf(theme);

        // Like a Piston's rod, it stops under the joint rings (#626), or, when they meet, runs centre to centre.
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, radiusA, b, radiusB) ?? (a, b);
        pen.Line(rodStart, rodEnd, line, theme.BeamWidth * _rodPerBeam);

        var seatA = a + (along * (radiusA + _lead));
        var seatB = b - (along * (radiusB + _lead));
        var span = (seatB - seatA).Dot(along);
        if (span > _minSpan)
        {
            var radius = theme.BeamWidth * _coilRadiusPerBeam;
            var wire = Wire(stiffness);
            var turns = CoilTurns(SeatSpan(rest, radiusA, radiusB), wire);
            var segments = HalfTurnSegments(radius * pen.Scale);

            // The back strokes are the same wire, so each turn reads as one coil.
            pen.Strokes(Helix(seatA, seatB, across, turns, radius, segments, front: false), line, wire);

            var length = Math.Min(travel, span);
            if (length >= _minBodyLength)
            {
                var body = PistonDrawing.Cylinder(seatA, seatA + (along * length), along, across, theme.BeamWidth * _bodyHalfPerBeam);
                pen.Polygon(body, theme.SensorFill);
                pen.Polyline([.. body, body[0]], line, _line);
            }

            pen.Strokes(Helix(seatA, seatB, across, turns, radius, segments, front: true), coil, wire);

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

    /// <summary>A seat plate's half width, the Spring's widest part; its stroke ticks are as wide (#835).</summary>
    public static float SeatHalf(VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.BeamWidth * _seatHalfPerBeam;
    }

    /// <summary>
    /// The coil's wire, in pixels, for a <paramref name="stiffness"/> in N/m: a hairline at
    /// Build's softest, <see cref="_maxWire"/> at its stiffest, in proportion between (#835).
    /// </summary>
    public static float Wire(double stiffness)
    {
        var share = Math.Clamp((stiffness - SpringDef.SoftestStiffness) / (SpringDef.StiffestStiffness - SpringDef.SoftestStiffness), 0, 1);
        return (float)(UiSize.Stroke.Hair + (share * (_maxWire - UiSize.Stroke.Hair)));
    }

    /// <summary>
    /// The coil's turns: as many as stack solid, <paramref name="wire"/> plus a small gap apart, over
    /// a third of <paramref name="restSpan"/>, seat to seat at the Spring's rest length, and at least
    /// <see cref="MinTurns"/>. Past <see cref="LongSpan"/> they grow only with the square root of
    /// the span, so a long coil does not turn into a grey band of hundreds, yet still gains turns
    /// with its Preload, as there is no most (#835). They never depend on its current length, so
    /// they hold still as it moves.
    /// </summary>
    public static int CoilTurns(float restSpan, float wire)
    {
        var span = restSpan <= LongSpan ? restSpan : LongSpan * MathF.Sqrt(restSpan / LongSpan);
        return Math.Max((int)(span / _freePerSolid / (wire + _solidGap)), MinTurns);
    }

    /// <summary>The distance from seat to seat at a <paramref name="length"/>, centre to centre.</summary>
    public static float SeatSpan(float length, float radiusA, float radiusB) =>
        length - radiusA - radiusB - (2 * _lead);

    /// <summary>
    /// The segments in each half turn of a coil <paramref name="radiusPixels"/> wide on screen:
    /// enough to keep each within about a quarter pixel of the true curve, from
    /// <see cref="MinHalfTurnSegments"/> on a coil too small to show its curve to
    /// <see cref="MaxHalfTurnSegments"/> up close (#835).
    /// </summary>
    public static int HalfTurnSegments(float radiusPixels) =>
        Math.Clamp((int)MathF.Ceiling(2 * MathF.Sqrt(Math.Max(radiusPixels, 0))), MinHalfTurnSegments, MaxHalfTurnSegments);

    /// <summary>
    /// The front or back half of each turn of a helix from <paramref name="start"/> to
    /// <paramref name="end"/>, seen from the side: <paramref name="radius"/> to each side of the
    /// line between them, the front halves bowed <see cref="_tilt"/> towards the end.
    /// </summary>
    private static IEnumerable<Vector2[]> Helix(Vector2 start, Vector2 end, Vector2 across, int turns, float radius, int segments, bool front)
    {
        var along = (end - start).Normalized();
        var first = front ? 0 : Mathf.Pi;
        for (var turn = 0; turn < turns; turn++)
        {
            var stroke = new Vector2[segments + 1];
            for (var step = 0; step <= segments; step++)
            {
                var angle = (Mathf.Tau * turn) + first + (Mathf.Pi * step / segments);
                var point = start.Lerp(end, angle / (Mathf.Tau * turns));
                stroke[step] = point + (along * (_tilt * Mathf.Sin(angle))) - (across * (radius * Mathf.Cos(angle)));
            }

            yield return stroke;
        }
    }
}
