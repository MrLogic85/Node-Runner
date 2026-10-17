using Godot;

namespace NodeRunner.Theme;

/// <summary>
/// The reference's rigid hatch: parallel lines at 45°, running down to
/// the left, <c>spacing</c> apart, clipped to a triangle. They run on under the
/// opaque joints at its corners. The lines are laid out from the
/// origin, so neighbouring triangles' hatches line up.
/// </summary>
public static class TriangleHatch
{
    private static readonly Vector2 _across = new Vector2(1, 1).Normalized();

    /// <summary>
    /// The closest the lines may come on screen, in window pixels. Any closer, zoomed out or in a
    /// small thumbnail, they blur into a wash with moiré, so the triangle is filled instead.
    /// </summary>
    public const float MinPixelSpacing = 3;

    /// <summary>
    /// Whether lines <paramref name="spacing"/> apart, drawn at <paramref name="pixelScale"/>
    /// window pixels per unit, are too close to read, so the triangle is filled instead.
    /// </summary>
    public static bool IsTooDense(float spacing, float pixelScale) => spacing * pixelScale < MinPixelSpacing;

    /// <summary>
    /// The hatch of triangle <paramref name="a"/>, <paramref name="b"/>, <paramref name="c"/>, lines
    /// <paramref name="spacing"/> apart.
    /// </summary>
    public static IReadOnlyList<(Vector2 Start, Vector2 End)> Lines(Vector2 a, Vector2 b, Vector2 c, float spacing)
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
                lines.Add((crossings[0], crossings[1]));
            }
        }

        return lines;
    }
}
