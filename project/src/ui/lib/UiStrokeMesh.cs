using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Lays out many round-ended strokes as one triangle list, for <see cref="UiPixelPen.Strokes"/>
/// (#835). Each stroke has a solid core as wide as the stroke and a feather around it that fades
/// to clear, like Godot's antialiased lines. Godot draws one polyline per call and gives it no
/// round ends, and the Compatibility renderer builds a buffer for every call, so a coil of hundreds
/// of strokes drawn one call each cost a phone most of its frame.
/// </summary>
public static class UiStrokeMesh
{
    // A miter grows to at most twice the half width, so a sharp bend does not spike.
    private const float _minMiterDot = 0.5f;
    private const int _minCapSegments = 2;
    private const int _maxCapSegments = 8;

    // Half as many pairs of cap segments as pixels round its outer edge's half width, give or take.
    private const float _capPairsPerPixel = 0.75f;

    // Corners across each point: clear, solid, solid, clear; and the three bands between them.
    private const int _across = 4;
    private const int _bands = _across - 1;

    /// <summary>
    /// The triangles of <paramref name="strokes"/>, each <paramref name="halfWidth"/> to either side
    /// of its points, feathered <paramref name="feather"/> beyond that: their corners, which of them
    /// are solid rather than the clear edge of the feather, and the corners of each triangle, three
    /// by three. A stroke of fewer than two distinct points draws nothing.
    /// </summary>
    public static (Vector2[] Points, bool[] Solid, int[] Indices) Build(IEnumerable<Vector2[]> strokes, float halfWidth, float feather)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var mesh = new Mesh(halfWidth, feather);
        foreach (var stroke in strokes)
        {
            mesh.Add(stroke);
        }

        return ([.. mesh.Points], [.. mesh.Solid], [.. mesh.Indices]);
    }

    /// <summary>
    /// The segments in each half-round end: enough that its edge reads as a curve, and even, so one
    /// corner sits on its tip.
    /// </summary>
    public static int CapSegments(float halfWidth, float feather) =>
        Math.Clamp(2 * (int)MathF.Ceiling((halfWidth + feather) * _capPairsPerPixel), _minCapSegments, _maxCapSegments);

    private sealed class Mesh(float halfWidth, float feather)
    {
        private readonly float _outer = halfWidth + feather;
        private readonly int _capSegments = CapSegments(halfWidth, feather);

        public List<Vector2> Points { get; } = [];

        public List<bool> Solid { get; } = [];

        public List<int> Indices { get; } = [];

        public void Add(Vector2[] stroke)
        {
            var points = Distinct(stroke);
            if (points.Count < 2)
            {
                return;
            }

            var first = Points.Count;
            for (var index = 0; index < points.Count; index++)
            {
                var (normal, stretch) = Miter(points, index);
                Corner(points[index] + (normal * _outer * stretch), solid: false);
                Corner(points[index] + (normal * halfWidth * stretch), solid: true);
                Corner(points[index] - (normal * halfWidth * stretch), solid: true);
                Corner(points[index] - (normal * _outer * stretch), solid: false);
            }

            for (var index = 0; index < points.Count - 1; index++)
            {
                var near = first + (index * _across);
                var far = near + _across;
                for (var band = 0; band < _bands; band++)
                {
                    Quad(near + band, near + band + 1, far + band + 1, far + band);
                }
            }

            Cap(points[0], (points[0] - points[1]).Normalized());
            Cap(points[^1], (points[^1] - points[^2]).Normalized());
        }

        private static List<Vector2> Distinct(Vector2[] stroke)
        {
            var points = new List<Vector2>(stroke.Length);
            foreach (var point in stroke)
            {
                if (points.Count == 0 || !points[^1].IsEqualApprox(point))
                {
                    points.Add(point);
                }
            }

            return points;
        }

        // The left normal at a point, and how far to stretch it so the stroke keeps its width round a bend.
        private static (Vector2 Normal, float Stretch) Miter(List<Vector2> points, int index)
        {
            var before = index > 0 ? (points[index] - points[index - 1]).Normalized().Orthogonal() : (Vector2?)null;
            var after = index < points.Count - 1 ? (points[index + 1] - points[index]).Normalized().Orthogonal() : (Vector2?)null;
            if (before is null || after is null)
            {
                return ((before ?? after)!.Value, 1);
            }

            var normal = (before.Value + after.Value).Normalized();
            if (!normal.IsFinite() || normal == Vector2.Zero)
            {
                return (after.Value, 1);
            }

            return (normal, 1 / Math.Max(normal.Dot(after.Value), _minMiterDot));
        }

        // A half-round end at an end point, bulging towards outward: a fan of solid core and a ring of feather.
        private void Cap(Vector2 centre, Vector2 outward)
        {
            var side = outward.Orthogonal();
            var hub = Corner(centre, solid: true);
            var ring = Points.Count;
            for (var step = 0; step <= _capSegments; step++)
            {
                var angle = Mathf.Pi * step / _capSegments;
                var direction = (side * Mathf.Cos(angle)) + (outward * Mathf.Sin(angle));
                Corner(centre + (direction * halfWidth), solid: true);
                Corner(centre + (direction * _outer), solid: false);
            }

            for (var step = 0; step < _capSegments; step++)
            {
                // Each step round the ring adds a solid corner, then a clear one beyond it.
                var core = ring + (step * 2);
                var next = core + 2;
                Indices.AddRange([hub, core, next]);
                Quad(core, core + 1, next + 1, next);
            }
        }

        private int Corner(Vector2 point, bool solid)
        {
            Points.Add(point);
            Solid.Add(solid);
            return Points.Count - 1;
        }

        private void Quad(int a, int b, int c, int d) => Indices.AddRange([a, b, c, a, c, d]);
    }
}
