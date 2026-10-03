using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiDashedBorder
{
    private const float _dash = 4;
    private const float _gap = 3;
    private const int _cornerCount = 4;
    private const int _arcSegments = 6;

    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, float radius, Color color, float width) =>
        DrawRoundedRect(canvas, rect, UiCorners.Uniform(radius), color, width);

    /// <summary>
    /// The dashed outline of <paramref name="rect"/> with its own radius for each corner, in a
    /// <see cref="CanvasItem._Draw"/> that has not set a draw transform. Drawn in window pixels
    /// (<see cref="UiPixelPen"/>), so all four sides look the same at any UI size (#733).
    /// </summary>
    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, UiCorners corners, Color color, float width)
    {
        using var pen = UiPixelPen.Begin(canvas);
        DrawRoundedRect(canvas, rect, corners, color, width * pen.Scale, pen.ToPixels, _dash, _gap);
    }

    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, float radius, Color color, float width, Transform2D toPixels, float dash, float gap) =>
        DrawRoundedRect(canvas, rect, UiCorners.Uniform(radius), color, width, toPixels, dash, gap);

    /// <summary>
    /// The dashed outline of <paramref name="rect"/>, mapped through <paramref name="toPixels"/>.
    /// Only <paramref name="width"/> is in the transformed units. The dashes are stretched to fit
    /// the perimeter evenly, each dash pulled in at both ends by <see cref="UiPixelPen.DashTrim"/>.
    /// It is antialiased, so <paramref name="toPixels"/> should map to window pixels
    /// (<see cref="UiPixelPen.ToPixels"/>).
    /// </summary>
    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, UiCorners corners, Color color, float width, Transform2D toPixels, float dash, float gap)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        corners = Clamp(rect, corners);
        var perimeter = Perimeter(rect, corners);
        if (perimeter <= 0 || dash + gap <= 0 || toPixels.BasisXform(Vector2.Right).Length() <= 0)
        {
            return;
        }

        var patternCount = Mathf.Max(1, Mathf.RoundToInt(perimeter / (dash + gap)));
        var patternLength = perimeter / patternCount;
        var dashLength = patternLength * dash / (dash + gap);
        var samplesPerUnit = toPixels.BasisXform(Vector2.Right).Length();
        var trim = UiPixelPen.DashTrim(dashLength * samplesPerUnit, width) / samplesPerUnit;
        var drawnLength = dashLength - (2 * trim);

        for (var start = trim; start < perimeter; start += patternLength)
        {
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(drawnLength * samplesPerUnit) + 1);
            var points = new Vector2[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var distance = start + (drawnLength * index / (sampleCount - 1));
                points[index] = toPixels * PointOnRoundedRect(rect, corners, distance);
            }

            canvas.DrawPolyline(points, color, width, antialiased: true);
        }
    }

    /// <summary>
    /// The closed outline of <paramref name="rect"/> with rounded corners. Only the arcs are
    /// sampled, so a filled polygon stays cheap to triangulate.
    /// </summary>
    public static Vector2[] RoundedRectPoints(Rect2 rect, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) * 0.5f);
        if (radius <= 0)
        {
            return [rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y), rect.Position];
        }

        var centers = new[]
        {
            rect.Position + new Vector2(rect.Size.X - radius, radius),
            rect.End - new Vector2(radius, radius),
            new Vector2(rect.Position.X + radius, rect.End.Y - radius),
            rect.Position + new Vector2(radius, radius),
        };
        var points = new Vector2[(_cornerCount * (_arcSegments + 1)) + 1];
        for (var corner = 0; corner < _cornerCount; corner++)
        {
            var startAngle = (corner - 1) * Mathf.Pi * 0.5f;
            for (var step = 0; step <= _arcSegments; step++)
            {
                var angle = startAngle + (Mathf.Pi * 0.5f * step / _arcSegments);
                points[(corner * (_arcSegments + 1)) + step] = centers[corner] + (Vector2.FromAngle(angle) * radius);
            }
        }

        points[^1] = points[0];
        return points;
    }

    public static Vector2 PointOnRoundedRect(Rect2 rect, float radius, float distance) =>
        PointOnRoundedRect(rect, Clamp(rect, UiCorners.Uniform(radius)), distance);

    /// <summary>
    /// The point <paramref name="distance"/> along the outline, clockwise from the top edge's
    /// left end. The radii must already fit <paramref name="rect"/>.
    /// </summary>
    public static Vector2 PointOnRoundedRect(Rect2 rect, UiCorners corners, float distance)
    {
        var (left, top, right, bottom) = (rect.Position.X, rect.Position.Y, rect.End.X, rect.End.Y);
        var quarter = Mathf.Pi * 0.5f;
        distance = Mathf.PosMod(distance, Perimeter(rect, corners));

        // Each side runs from the end of one corner to the start of the next, then that corner's arc.
        (Vector2 From, Vector2 To, float Radius, Vector2 Center, float Angle)[] sides =
        [
            (new(left + corners.TopLeft, top), new(right - corners.TopRight, top), corners.TopRight, new(right - corners.TopRight, top + corners.TopRight), -quarter),
            (new(right, top + corners.TopRight), new(right, bottom - corners.BottomRight), corners.BottomRight, new(right - corners.BottomRight, bottom - corners.BottomRight), 0),
            (new(right - corners.BottomRight, bottom), new(left + corners.BottomLeft, bottom), corners.BottomLeft, new(left + corners.BottomLeft, bottom - corners.BottomLeft), quarter),
            (new(left, bottom - corners.BottomLeft), new(left, top + corners.TopLeft), corners.TopLeft, new(left + corners.TopLeft, top + corners.TopLeft), Mathf.Pi),
        ];

        foreach (var side in sides)
        {
            var straight = side.From.DistanceTo(side.To);
            if (distance <= straight)
            {
                return straight > 0 ? side.From.Lerp(side.To, distance / straight) : side.From;
            }

            distance -= straight;
            var arc = quarter * side.Radius;
            if (distance <= arc)
            {
                return ArcPoint(side.Center, side.Radius, side.Angle, distance);
            }

            distance -= arc;
        }

        return sides[0].From;
    }

    // No corner may be larger than half the shorter side.
    private static UiCorners Clamp(Rect2 rect, UiCorners corners)
    {
        var most = Mathf.Min(rect.Size.X, rect.Size.Y) * 0.5f;
        return new UiCorners(
            Mathf.Clamp(corners.TopLeft, 0, most),
            Mathf.Clamp(corners.TopRight, 0, most),
            Mathf.Clamp(corners.BottomRight, 0, most),
            Mathf.Clamp(corners.BottomLeft, 0, most));
    }

    private static float Perimeter(Rect2 rect, UiCorners corners)
    {
        var top = Mathf.Max(0, rect.Size.X - corners.TopLeft - corners.TopRight);
        var right = Mathf.Max(0, rect.Size.Y - corners.TopRight - corners.BottomRight);
        var bottom = Mathf.Max(0, rect.Size.X - corners.BottomRight - corners.BottomLeft);
        var left = Mathf.Max(0, rect.Size.Y - corners.BottomLeft - corners.TopLeft);
        var arcs = Mathf.Pi * 0.5f * (corners.TopLeft + corners.TopRight + corners.BottomRight + corners.BottomLeft);
        return top + right + bottom + left + arcs;
    }

    private static Vector2 ArcPoint(Vector2 center, float radius, float startAngle, float distance)
    {
        if (radius <= 0)
        {
            return center;
        }

        var angle = startAngle + (distance / radius);
        return center + (Vector2.FromAngle(angle) * radius);
    }
}
