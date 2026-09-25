using Godot;

namespace NodeRunner.Ui.Lib;

internal static class UiDashedBorder
{
    private const float _targetPatternLength = 7;
    private const float _dashRatio = 4f / 7f;

    public static void DrawRoundedRect(CanvasItem canvas, Rect2 rect, float radius, Color color, float width)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.Size.X, rect.Size.Y) * 0.5f);
        var straightWidth = Mathf.Max(0, rect.Size.X - (radius * 2));
        var straightHeight = Mathf.Max(0, rect.Size.Y - (radius * 2));
        var perimeter = (straightWidth * 2) + (straightHeight * 2) + (Mathf.Tau * radius);
        if (perimeter <= 0)
        {
            return;
        }

        var patternCount = Mathf.Max(1, Mathf.RoundToInt(perimeter / _targetPatternLength));
        var patternLength = perimeter / patternCount;
        var dashLength = patternLength * _dashRatio;

        for (var start = 0f; start < perimeter; start += patternLength)
        {
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(dashLength) + 1);
            var points = new Vector2[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var distance = start + (dashLength * index / (sampleCount - 1));
                points[index] = PointOnRoundedRect(rect, radius, distance);
            }

            canvas.DrawPolyline(points, color, width, antialiased: false);
        }
    }

    public static Vector2 PointOnRoundedRect(Rect2 rect, float radius, float distance)
    {
        var straightWidth = Mathf.Max(0, rect.Size.X - (radius * 2));
        var straightHeight = Mathf.Max(0, rect.Size.Y - (radius * 2));
        var arcLength = Mathf.Pi * radius * 0.5f;
        var perimeter = (straightWidth * 2) + (straightHeight * 2) + (arcLength * 4);
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
