using Godot;

namespace NodeRunner.Ui.Lib;

public static class UiDashedBorder
{
    private const float _dash = 4;
    private const float _gap = 3;
    private const int _cornerCount = 4;
    private const int _arcSegments = 6;

    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, float radius, Color color, float width) =>
        DrawRoundedRect(canvas, rect, radius, color, width, Transform2D.Identity, _dash, _gap, antialiased: false);

    /// <summary>
    /// The dashed outline of <paramref name="rect"/>, mapped through <paramref name="transform"/>.
    /// Only <paramref name="width"/> is in the transformed units. The dashes are stretched to fit
    /// the perimeter evenly.
    /// </summary>
    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, float radius, Color color, float width, Transform2D transform, float dash, float gap, bool antialiased)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) * 0.5f);
        var straightWidth = Mathf.Max(0, rect.Size.X - (radius * 2));
        var straightHeight = Mathf.Max(0, rect.Size.Y - (radius * 2));
        var perimeter = (straightWidth * 2) + (straightHeight * 2) + (Mathf.Tau * radius);
        if (perimeter <= 0 || dash + gap <= 0)
        {
            return;
        }

        var patternCount = Mathf.Max(1, Mathf.RoundToInt(perimeter / (dash + gap)));
        var patternLength = perimeter / patternCount;
        var dashLength = patternLength * dash / (dash + gap);
        var samplesPerUnit = transform.BasisXform(Vector2.Right).Length();

        for (var start = 0f; start < perimeter; start += patternLength)
        {
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(dashLength * samplesPerUnit) + 1);
            var points = new Vector2[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var distance = start + (dashLength * index / (sampleCount - 1));
                points[index] = transform * PointOnRoundedRect(rect, radius, distance);
            }

            canvas.DrawPolyline(points, color, width, antialiased);
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

    public static Vector2 PointOnRoundedRect(Rect2 rect, float radius, float distance)
    {
        var straightWidth = Mathf.Max(0, rect.Size.X - (radius * 2));
        var straightHeight = Mathf.Max(0, rect.Size.Y - (radius * 2));
        var arcLength = Mathf.Pi * radius * 0.5f;
        var perimeter = (straightWidth * 2) + (straightHeight * 2) + (arcLength * _cornerCount);
        distance = Mathf.PosMod(distance, perimeter);

        if (distance <= straightWidth)
        {
            return new Vector2(rect.Position.X + radius + distance, rect.Position.Y);
        }

        distance -= straightWidth;
        if (distance <= arcLength)
        {
            return ArcPoint(rect.Position + new Vector2(rect.Size.X - radius, radius), radius, -Mathf.Pi * 0.5f, distance);
        }

        distance -= arcLength;
        if (distance <= straightHeight)
        {
            return new Vector2(rect.End.X, rect.Position.Y + radius + distance);
        }

        distance -= straightHeight;
        if (distance <= arcLength)
        {
            return ArcPoint(rect.End - new Vector2(radius, radius), radius, 0, distance);
        }

        distance -= arcLength;
        if (distance <= straightWidth)
        {
            return new Vector2(rect.End.X - radius - distance, rect.End.Y);
        }

        distance -= straightWidth;
        if (distance <= arcLength)
        {
            return ArcPoint(new Vector2(rect.Position.X + radius, rect.End.Y - radius), radius, Mathf.Pi * 0.5f, distance);
        }

        distance -= arcLength;
        if (distance <= straightHeight)
        {
            return new Vector2(rect.Position.X, rect.End.Y - radius - distance);
        }

        distance -= straightHeight;
        return ArcPoint(rect.Position + new Vector2(radius, radius), radius, Mathf.Pi, distance);
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
