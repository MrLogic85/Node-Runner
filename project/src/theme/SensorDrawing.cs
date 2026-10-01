using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a sensor on its beam as a small picture of the real thing (#576), shared by Build's canvas
/// and the creature in Training. Callers set the transform first so the origin is the beam's
/// midpoint and -y is the sensor's top (the beam's built up side). Both pictures fit the
/// <see cref="SensorPicture.Size"/> square, which is also their tap area.
/// </summary>
public static class SensorDrawing
{
    private const float _line = UiSize.Stroke.Signal;
    private const float _halo = 3;

    // The Accelerometer: an upright frame, a spring from its top, and the weight.
    private const float _frameHalfWidth = 8;
    private const float _frameHalfHeight = 11;
    private const float _frameRadius = 3;
    private const float _weightRadius = 3.5f;
    private const float _weightTravelX = _frameHalfWidth - _weightRadius - 1.5f;
    private const float _weightTravelY = _frameHalfHeight - _weightRadius - 2.5f;
    private const int _springTurns = 3;

    // The camera: a body with a lens ring and a hood on the side it looks out of.
    private const float _bodyHalfLength = 7;
    private const float _bodyHalfHeight = 5.5f;
    private const float _bodyBack = -9;
    private const float _hoodLength = 4;
    private const float _hoodNarrow = 2.5f;
    private const float _hoodWide = 4.5f;
    private const float _lensRadius = 2.5f;

    private const int _cornerSegments = 4;

    /// <summary>The frame, the spring from its top, and the weight at <paramref name="weightOffset"/> (<see cref="Accelerometer.WeightOffset"/>).</summary>
    public static void DrawAccelerometer(CanvasItem canvas, VisualTheme theme, Vector2D weightOffset, bool selected)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var frame = RoundedRect(Vector2.Zero, _frameHalfWidth, _frameHalfHeight, _frameRadius);
        DrawShape(canvas, theme, frame, selected ? RoundedRect(Vector2.Zero, _frameHalfWidth + _halo, _frameHalfHeight + _halo, _frameRadius + _halo) : null);

        var weight = new Vector2((float)weightOffset.X * _weightTravelX, (float)weightOffset.Y * _weightTravelY);
        var top = new Vector2(0, -_frameHalfHeight);
        var springEnd = weight - new Vector2(0, _weightRadius);
        var spring = new Vector2[(_springTurns * 2) + 2];
        for (var i = 0; i < spring.Length; i++)
        {
            var t = i / (float)(spring.Length - 1);
            var side = i == 0 || i == spring.Length - 1 ? 0 : (i % 2 == 0 ? -2.5f : 2.5f);
            spring[i] = top.Lerp(springEnd, t) + new Vector2(side, 0);
        }

        canvas.DrawPolyline(spring, theme.SensorLine, _line * 0.75f, antialiased: true);
        canvas.DrawCircle(weight, _weightRadius, theme.SensorLine, filled: true, antialiased: true);
    }

    /// <summary>A camera looking along <paramref name="aim"/>, a unit vector in the picture's frame (the middle of its ray fan).</summary>
    public static void DrawCamera(CanvasItem canvas, VisualTheme theme, Vector2 aim, bool selected)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var turn = aim.IsZeroApprox() ? 0 : aim.Angle();
        var bodyCentre = new Vector2(_bodyBack + _bodyHalfLength, 0);
        var body = Turned(RoundedRect(bodyCentre, _bodyHalfLength, _bodyHalfHeight, 2), turn);
        var front = _bodyBack + (2 * _bodyHalfLength);
        var hood = Turned(
            [
                new Vector2(front, -_hoodNarrow),
                new Vector2(front + _hoodLength, -_hoodWide),
                new Vector2(front + _hoodLength, _hoodWide),
                new Vector2(front, _hoodNarrow),
            ],
            turn);
        var halo = selected
            ? Turned(RoundedRect(new Vector2((_bodyBack + front + _hoodLength) / 2, 0), ((front + _hoodLength - _bodyBack) / 2) + _halo, _hoodWide + _halo, 2 + _halo), turn)
            : null;
        DrawShape(canvas, theme, body, halo);
        DrawShape(canvas, theme, hood, null);
        canvas.DrawArc(bodyCentre.Rotated(turn), _lensRadius, 0, Mathf.Tau, 16, theme.SensorLine, _line * 0.75f, antialiased: true);
    }

    /// <summary>A selected camera's rays from <paramref name="origin"/> to each end.</summary>
    public static void DrawRays(CanvasItem canvas, VisualTheme theme, Vector2 origin, IEnumerable<Vector2> ends)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(ends);
        foreach (var end in ends)
        {
            canvas.DrawDashedLine(origin, end, theme.SelectionGlow, UiSize.Stroke.Signal, 8, antialiased: false);
        }
    }

    /// <summary>The middle of a ray fan: the unit vector along the sum of its directions.</summary>
    public static Vector2 Aim(IEnumerable<Vector2> rayDirections)
    {
        ArgumentNullException.ThrowIfNull(rayDirections);
        var sum = Vector2.Zero;
        foreach (var direction in rayDirections)
        {
            sum += direction.Normalized();
        }

        return sum.IsZeroApprox() ? Vector2.Down : sum.Normalized();
    }

    private static void DrawShape(CanvasItem canvas, VisualTheme theme, Vector2[] outline, Vector2[]? halo)
    {
        if (halo is not null)
        {
            canvas.DrawColoredPolygon(halo, theme.SelectionGlow);
        }

        canvas.DrawColoredPolygon(outline, theme.SensorFill);
        canvas.DrawPolyline([.. outline, outline[0]], theme.SensorLine, _line, antialiased: true);
    }

    private static Vector2[] Turned(Vector2[] points, float angle) => [.. points.Select(point => point.Rotated(angle))];

    private static Vector2[] RoundedRect(Vector2 centre, float halfWidth, float halfHeight, float radius)
    {
        var points = new Vector2[4 * (_cornerSegments + 1)];
        Vector2[] corners =
        [
            new(halfWidth - radius, -halfHeight + radius),
            new(halfWidth - radius, halfHeight - radius),
            new(-halfWidth + radius, halfHeight - radius),
            new(-halfWidth + radius, -halfHeight + radius),
        ];
        var index = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var start = (corner - 1) * Mathf.Pi / 2;
            for (var step = 0; step <= _cornerSegments; step++)
            {
                var angle = start + (step * Mathf.Pi / 2 / _cornerSegments);
                points[index++] = centre + corners[corner] + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        return points;
    }
}
