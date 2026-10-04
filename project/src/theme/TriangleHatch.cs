using Godot;

namespace NodeRunner.Theme;

/// <summary>
/// The reference's rigid hatch (#612): parallel lines at 45°, running down to
/// the left, <c>spacing</c> apart, clipped to a triangle, and kept out of the
/// joints at its corners (#626). The lines are laid out from the origin, so
/// neighbouring triangles' hatches line up.
/// </summary>
public static class TriangleHatch
{
    private static readonly Vector2 _across = new Vector2(1, 1).Normalized();

    /// <summary>
    /// The closest the lines may come on screen, in window pixels. Any closer, zoomed out or in a
    /// small thumbnail, they blur into a wash with moiré, so the triangle is filled instead (#770).
    /// </summary>
    public const float MinPixelSpacing = 3;

    /// <summary>
    /// Whether lines <paramref name="spacing"/> apart, drawn at <paramref name="pixelScale"/>
    /// window pixels per unit, are too close to read, so the triangle is filled instead.
    /// </summary>
    public static bool IsTooDense(float spacing, float pixelScale) => spacing * pixelScale < MinPixelSpacing;

    /// <summary>
    /// The hatch of triangle <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>, lines
    /// <paramref name="spacing"/> apart, kept <paramref name="jointRadius"/> away from its corners.
    /// </summary>
    public static IReadOnlyList<(Vector2 Start, Vector2 End)> Lines(Vector2 a, Vector2 b, Vector2 c, float spacing, float jointRadius = 0)
    {
        var lines = new List<(Vector2, Vector2)>();
        if (spacing <= 0)
        {
            return lines;
        }

        var corners = new[] { a, b, c };
        var levels = new[] { _across.Dot(a), _across.Dot(b), _across.Dot(c) };
        var first = (int)MathF.Floor(MathF.Min(levels[0], MathF.Min(levels[1], levels[2])) / spacing) + 1;
        var last = (int)MathF.Ceiling(MathF.Max(levels[0], MathF.Max(levels[1], levels[2])) / spacing) - 1;
        var crossings = new List<Vector2>(2);
        for (var step = first; step <= last; step++)
        {
            var level = step * spacing;
            crossings.Clear();
            for (var edge = 0; edge < 3; edge++)
            {
                var next = (edge + 1) % 3;
                var from = levels[edge];
                var to = levels[next];
                if ((from < level) == (to < level))
                {
                    continue;
                }

                crossings.Add(corners[edge].Lerp(corners[next], (level - from) / (to - from)));
            }

            if (crossings.Count == 2 && !crossings[0].IsEqualApprox(crossings[1]))
            {
                AddOutside(lines, crossings[0], crossings[1], corners, jointRadius);
            }
        }

        return lines;
    }

    // Adds the parts of start–end outside the corner circles. Not Geometry2D: SegmentIntersectsCircle
    // gives one crossing, ClipPolylineWithPolygon needs polygons, and both need the engine.
    private static void AddOutside(List<(Vector2, Vector2)> lines, Vector2 start, Vector2 end, Vector2[] corners, float radius, int corner = 0)
    {
        if (corner == corners.Length || radius <= 0)
        {
            lines.Add((start, end));
            return;
        }

        // Where the line start + t·(end − start) is inside the circle, as t from enter to leave.
        var along = end - start;
        var toStart = start - corners[corner];
        var a = along.Dot(along);
        var b = 2 * toStart.Dot(along);
        var c = toStart.Dot(toStart) - (radius * radius);
        var discriminant = (b * b) - (4 * a * c);
        if (discriminant <= 0)
        {
            AddOutside(lines, start, end, corners, radius, corner + 1);
            return;
        }

        var root = MathF.Sqrt(discriminant);
        var enter = (-b - root) / (2 * a);
        var leave = (-b + root) / (2 * a);
        if (enter > 0)
        {
            AddOutside(lines, start, start + (along * MathF.Min(enter, 1)), corners, radius, corner + 1);
        }

        if (leave < 1)
        {
            AddOutside(lines, start + (along * MathF.Max(leave, 0)), end, corners, radius, corner + 1);
        }
    }
}
