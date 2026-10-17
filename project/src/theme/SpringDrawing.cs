using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a Spring between two joints as a coilover; <c>docs/WORLD_VISUALS.md</c> → "Spring" owns its look.
/// Each side of the helix is one call of the same wire. The damper inside the coil is a
/// three-part telescope like a Piston's, narrower and in <c>line-strong</c>, placed by
/// <see cref="TelescopeGeometry"/>.
/// Drawn through <see cref="UiPixelPen"/>, so it stays crisp at any zoom.
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
    private const float _cylinderHalfPerBeam = 5f / 6f;
    private const float _stageHalfPerBeam = 0.5f;

    // The coil's front strokes bow this far towards joint B, so the turns read as a helix seen from the side.
    private const float _tilt = 4;
    private const float _lead = 3;

    // The wire runs from a hairline on the softest Spring to as wide as a drawn beam on the stiffest,
    // in proportion (owner decision).
    private const float _maxWire = UiSize.Widget.CreatureBeamWidth;

    // A free coil is this many times as long as stacked solid, as far as a Spring can be squeezed.
    private const float _freePerSolid = 3;

    // Two turns stacked solid sit this much more than the wire apart, as the front strokes bow.
    private const float _solidGap = 2;
    private const float _minSpan = 4;

    /// <param name="canvas">The CanvasItem drawing it, inside its draw call.</param>
    /// <param name="drawTransform">The canvas's draw transform, for <see cref="UiPixelPen"/>.</param>
    /// <param name="theme">The theme its colours and widths come from.</param>
    /// <param name="a">Joint A's centre.</param>
    /// <param name="b">Joint B's centre.</param>
    /// <param name="radiusA">Joint A's drawn radius, whose halo the selection lines join; the rod starts at its hub.</param>
    /// <param name="radiusB">Joint B's drawn radius.</param>
    /// <param name="coveredA">How far what is drawn over joint A reaches (<see cref="ServoGeometry.LinkCover"/>); the cylinder and seat start there.</param>
    /// <param name="coveredB">How far what is drawn over joint B reaches; its seat sits a short lead outside.</param>
    /// <param name="shortest">The Spring's shortest length, centre to centre, which sets its telescope's sizes.</param>
    /// <param name="longest">The Spring's longest length, centre to centre.</param>
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
        float coveredA,
        float coveredB,
        float shortest,
        float longest,
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

        // Like a Piston's rod, it runs hub to hub, or, when they meet, centre to centre.
        const float hub = JointDrawing.HubRadius;
        var (rodStart, rodEnd) = JointDrawing.BeamSpan(theme.JointRingWidth, a, hub, b, hub) ?? (a, b);
        pen.Line(rodStart, rodEnd, line, theme.BeamWidth * _rodPerBeam);

        var length = a.DistanceTo(b);
        var telescope = TelescopeGeometry.Place(TelescopeGeometry.Spring, length, shortest, longest, coveredA, coveredB);
        var span = SeatSpan(length, coveredA, coveredB);
        var seatA = a + (along * (coveredA + _lead));
        var seatB = seatA + (along * span);
        var radius = theme.BeamWidth * _coilRadiusPerBeam;
        var wire = Wire(stiffness);
        var turns = CoilTurns(SeatSpan(rest, coveredA, coveredB), SeatSpan(shortest, coveredA, coveredB), wire);
        var segments = HalfTurnSegments(radius * pen.Scale);
        var hasCoil = span > _minSpan;

        // The back strokes are the same wire, so each turn reads as one coil.
        if (hasCoil)
        {
            pen.Strokes(Helix(seatA, seatB, across, turns, radius, segments, front: false), line, wire);
        }

        // The stage runs from under the cylinder, so only its mouth end shows.
        if (telescope.HasStage)
        {
            DrawTube(pen, theme, PistonDrawing.Cylinder(a + (along * telescope.Start), a + (along * telescope.StageEnd), along, across, theme.BeamWidth * _stageHalfPerBeam), line);
        }

        if (telescope.HasCylinder)
        {
            DrawTube(pen, theme, PistonDrawing.Cylinder(a + (along * telescope.Start), a + (along * telescope.CylinderEnd), along, across, theme.BeamWidth * _cylinderHalfPerBeam), line);
        }

        if (hasCoil)
        {
            pen.Strokes(Helix(seatA, seatB, across, turns, radius, segments, front: true), coil, wire);

            foreach (var seat in new[] { seatA, seatB })
            {
                pen.Line(seat + (across * seatHalf), seat - (across * seatHalf), line, _line);
            }
        }

        if (selected)
        {
            SelectionDrawing.DrawLink(canvas, pen.ToPixels, pen.Scale, theme, a, b, radiusA, radiusB, haloA, haloB, SelectionOffset(theme));
        }
    }

    private static void DrawTube(UiPixelPen pen, VisualTheme theme, Vector2[] tube, Color line)
    {
        pen.Polygon(tube, theme.SensorFill);
        pen.Polyline([.. tube, tube[0]], line, _line);
    }

    /// <summary>The selection outline's distance from the axis: the seats are its widest part, and the gap is measured from their ends.</summary>
    public static float SelectionOffset(VisualTheme theme) => SeatHalf(theme) + (float)SelectionMarks.Gap;

    /// <summary>A seat plate's half width, the Spring's widest part; its stroke ticks are as wide.</summary>
    public static float SeatHalf(VisualTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return theme.BeamWidth * _seatHalfPerBeam;
    }

    /// <summary>
    /// The coil's wire, in pixels, for a <paramref name="stiffness"/> in N/m: a hairline at
    /// Build's softest, <see cref="_maxWire"/> at its stiffest, in proportion between.
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
    /// with its Coil length, as there is no most. They are never more than fit wire to wire,
    /// with no gap, over <paramref name="shortestSpan"/>, seat to seat at its shortest length, so they
    /// do not overlap when it is squeezed, unless that is fewer than <see cref="MinTurns"/>. With no
    /// gap the cap binds less, so a plain 1 m Spring at Stroke 200% still gains turns with its Coil
    /// length; where it binds, as on a short Spring or one with a motor on its joint, a longer Coil
    /// length can show fewer turns. They never depend
    /// on its current length, so they hold still as it moves.
    /// </summary>
    public static int CoilTurns(float restSpan, float shortestSpan, float wire)
    {
        var span = restSpan <= LongSpan ? restSpan : LongSpan * MathF.Sqrt(restSpan / LongSpan);
        var pitch = wire + _solidGap;
        return Math.Max(Math.Min((int)(span / _freePerSolid / pitch), (int)(shortestSpan / wire)), MinTurns);
    }

    /// <summary>
    /// The distance from seat to seat at a <paramref name="length"/>, centre to centre: a lead outside
    /// what covers each end (<see cref="ServoGeometry.LinkCover"/>); <see cref="Draw"/> places the seats by it.
    /// </summary>
    public static float SeatSpan(float length, float coveredA, float coveredB) =>
        length - coveredA - coveredB - (2 * _lead);

    /// <summary>
    /// The segments in each half turn of a coil <paramref name="radiusPixels"/> wide on screen:
    /// enough to keep each within about a quarter pixel of the true curve, from
    /// <see cref="MinHalfTurnSegments"/> on a coil too small to show its curve to
    /// <see cref="MaxHalfTurnSegments"/> up close.
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
