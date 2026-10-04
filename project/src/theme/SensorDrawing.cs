using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a sensor on its beam as a small picture of the real thing (#576), shared by Build's canvas
/// and the creature in Training. Callers pass the draw transform they are in, set so the origin is
/// the beam's midpoint and -y is the sensor's top (the beam's built up side). Each picture fits its
/// kind's <see cref="SensorPicture.SizeOf"/> square, which is also its tap area. Everything is
/// drawn at window-pixel resolution (<see cref="UiPixelSpace"/>), so it stays crisp at any zoom
/// (#625), and the caller's transform is restored afterwards.
/// </summary>
public static class SensorDrawing
{
    private const float _line = UiSize.Stroke.Signal;
    private const float _rayDash = 8;
    private const float _hitRadius = 6;

    // Rays are drawn over the creature, so they leave from the camera picture's edge, not across it.
    private const float _rayStart = (float)(SensorPicture.CameraSize / 2);

    // The Accelerometer: an upright frame, a spring from its top, and the weight.
    private const float _frameHalfWidth = 8;
    private const float _frameHalfHeight = 11;
    private const float _frameRadius = 3;
    private const float _weightRadius = 3.5f;
    private const float _weightTravelX = _frameHalfWidth - _weightRadius - 1.5f;
    private const float _weightTravelY = _frameHalfHeight - _weightRadius - 2.5f;
    private const int _springTurns = 3;

    // The camera: a body with a lens ring and a hood on the side it looks out of, twice the
    // Accelerometer's scale (#622). It turns freely within a circle of radius 21, inside its square.
    private const float _bodyHalfLength = 14;
    private const float _bodyHalfHeight = 11;
    private const float _bodyBack = -18;
    private const float _bodyRadius = 4;
    private const float _hoodLength = 8;
    private const float _hoodNarrow = 5;
    private const float _hoodWide = 9;
    private const float _lensRadius = 5;

    private const int _cornerSegments = 4;

    /// <summary>The frame, the spring from its top, and the weight at <paramref name="weightOffset"/> (<see cref="Accelerometer.WeightOffset"/>).</summary>
    public static void DrawAccelerometer(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2D weightOffset, bool selected)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        var line = LineColor(theme, selected);
        DrawShape(pen, theme, RoundedRect(Vector2.Zero, _frameHalfWidth, _frameHalfHeight, _frameRadius), line);

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

        pen.Polyline(spring, line, _line * 0.75f);
        pen.Disc(weight, _weightRadius, line);
    }

    /// <summary>A camera looking along <paramref name="aim"/>, a unit vector in the picture's frame (the middle of its ray fan).</summary>
    public static void DrawCamera(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2 aim, bool selected)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var turn = aim.IsZeroApprox() ? 0 : aim.Angle();
        var bodyCentre = new Vector2(_bodyBack + _bodyHalfLength, 0);
        var body = Turned(RoundedRect(bodyCentre, _bodyHalfLength, _bodyHalfHeight, _bodyRadius), turn);
        var front = _bodyBack + (2 * _bodyHalfLength);
        var hood = Turned(
            [
                new Vector2(front, -_hoodNarrow),
                new Vector2(front + _hoodLength, -_hoodWide),
                new Vector2(front + _hoodLength, _hoodWide),
                new Vector2(front, _hoodNarrow),
            ],
            turn);
        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        var line = LineColor(theme, selected);
        DrawShape(pen, theme, body, line);
        DrawShape(pen, theme, hood, line);
        pen.Ring(bodyCentre.Rotated(turn), _lensRadius, line, _line);
    }

    /// <summary>A selected camera's rays in Build, from <paramref name="origin"/> to each end.</summary>
    public static void DrawRays(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2 origin, IEnumerable<Vector2> ends)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(ends);
        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        foreach (var end in ends)
        {
            DrawRay(pen, theme, origin, end);
        }
    }

    /// <summary>A camera's rays in Training (#623): from <paramref name="origin"/> to each ground hit, with a <c>halo</c> ring at the hit.</summary>
    public static void DrawRayHits(CanvasItem canvas, Transform2D drawTransform, VisualTheme theme, Vector2 origin, IEnumerable<Vector2> hits)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(hits);
        using var pen = UiPixelPen.Begin(canvas, drawTransform);
        foreach (var hit in hits)
        {
            DrawRay(pen, theme, origin, hit);
            pen.Ring(hit, _hitRadius, theme.SelectionGlow, _line);
        }
    }

    private static void DrawRay(UiPixelPen pen, VisualTheme theme, Vector2 origin, Vector2 end)
    {
        if (origin.DistanceTo(end) > _rayStart)
        {
            pen.DashedLine(origin + (origin.DirectionTo(end) * _rayStart), end, theme.SelectionGlow, _line, _rayDash);
        }
    }

    // A selected picture is drawn in halo instead of accent, like a selected part in the reference (#624).
    private static Color LineColor(VisualTheme theme, bool selected) => selected ? theme.SelectionGlow : theme.SensorLine;

    private static void DrawShape(UiPixelPen pen, VisualTheme theme, Vector2[] outline, Color line)
    {
        pen.Polygon(outline, theme.SensorFill);
        pen.Polyline([.. outline, outline[0]], line, _line);
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
