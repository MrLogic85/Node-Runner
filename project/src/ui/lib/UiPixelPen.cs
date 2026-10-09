using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Draws antialiased strokes and discs in window pixels (<see cref="UiPixelSpace"/>, #733), so their
/// edges keep a one-pixel feather at any UI size or zoom. Points, widths, radii and dashes are in
/// the caller's draw space; the transform may scale, turn and move, but not mirror or stretch.
/// Disposing it restores the caller's draw transform.
/// </summary>
public readonly struct UiPixelPen : IDisposable
{
    private const int _ringSegments = 32;

    /// <summary>Godot's antialiasing feather, in window pixels (<c>FEATHER_SIZE</c>).</summary>
    private const float _feather = 1.25f;

    private readonly CanvasItem _canvas;
    private readonly Transform2D _drawTransform;

    private UiPixelPen(CanvasItem canvas, Transform2D drawTransform)
    {
        _canvas = canvas;
        _drawTransform = drawTransform;
        ToPixels = UiPixelSpace.Enter(canvas, drawTransform);
        Scale = UiPixelSpace.ScaleOf(ToPixels);
    }

    /// <summary>The map from the caller's draw space to window pixels.</summary>
    public Transform2D ToPixels { get; }

    /// <summary>How many window pixels one unit of the caller's draw space is.</summary>
    public float Scale { get; }

    /// <summary>A pen for a <see cref="CanvasItem._Draw"/> that has not set a draw transform.</summary>
    public static UiPixelPen Begin(CanvasItem canvas) => Begin(canvas, Transform2D.Identity);

    /// <summary>
    /// A pen for points in <paramref name="drawTransform"/>'s space. Disposing it leaves
    /// <paramref name="drawTransform"/> as the canvas's draw transform.
    /// </summary>
    public static UiPixelPen Begin(CanvasItem canvas, Transform2D drawTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        return new UiPixelPen(canvas, drawTransform);
    }

    public void Line(Vector2 from, Vector2 to, Color color, float width) =>
        _canvas.DrawLine(ToPixels * from, ToPixels * to, color, width * Scale, antialiased: true);

    public void Polyline(Vector2[] points, Color color, float width) =>
        _canvas.DrawPolyline(Mapped(points), color, width * Scale, antialiased: true);

    /// <summary>
    /// Many round-ended polylines of one colour and width, laid out by <see cref="UiStrokeMesh"/>
    /// and drawn in one call, so a coil of hundreds of strokes stays cheap (#835).
    /// </summary>
    public void Strokes(IEnumerable<Vector2[]> strokes, Color color, float width)
    {
        var toPixels = ToPixels;
        var (points, solid, indices) = UiStrokeMesh.Build(strokes.Select(stroke => Mapped(stroke, toPixels)), width * Scale / 2, _feather);
        if (indices.Length == 0)
        {
            return;
        }

        var clear = color with { A = 0 };
        var colors = solid.Select(isSolid => isSolid ? color : clear).ToArray();
        RenderingServer.CanvasItemAddTriangleArray(_canvas.GetCanvasItem(), indices, points, colors);
    }

    /// <summary>A dashed line laid out by <see cref="DashSpans"/>.</summary>
    public void DashedLine(Vector2 from, Vector2 to, Color color, float width, float dash, float? gap = null)
    {
        var start = ToPixels * from;
        var end = ToPixels * to;
        var length = start.DistanceTo(end);
        var spans = DashSpans(length, dash * Scale, (gap ?? dash) * Scale, width * Scale);
        if (spans.Length == 0)
        {
            return;
        }

        var direction = (end - start) / length;
        var points = new Vector2[spans.Length * 2];
        for (var index = 0; index < spans.Length; index++)
        {
            points[index * 2] = start + (direction * spans[index].Start);
            points[(index * 2) + 1] = start + (direction * spans[index].End);
        }

        _canvas.DrawMultiline(points, color, width * Scale, antialiased: true);
    }

    /// <summary>
    /// Where the dashes go along a line <paramref name="length"/> pixels long: it starts and ends
    /// on a dash, <paramref name="dash"/> and <paramref name="gap"/> stretched evenly to fit, each
    /// dash pulled in by <see cref="DashTrim"/>. A line no longer than a dash is one solid span.
    /// </summary>
    public static (float Start, float End)[] DashSpans(float length, float dash, float gap, float width)
    {
        if (length <= 0)
        {
            return [];
        }

        if (length <= dash || dash <= 0)
        {
            return [(0, length)];
        }

        var count = Mathf.Max(1, Mathf.RoundToInt((length + gap) / (dash + gap)));
        var stretch = length / ((count * dash) + ((count - 1) * gap));
        var trim = DashTrim(dash * stretch, width);
        var spans = new (float Start, float End)[count];
        for (var index = 0; index < count; index++)
        {
            var dashStart = index * (dash + gap) * stretch;
            spans[index] = (dashStart + trim, dashStart + (dash * stretch) - trim);
        }

        return spans;
    }

    /// <summary>
    /// How far each end of a <paramref name="dash"/> pixels long is pulled in so its feather does
    /// not fill the gap, keeping at least one pixel of dash. Godot 4.7 feathers an antialiased
    /// line's ends by <c>FEATHER_SIZE · min(width / 2, 1)</c> (<c>canvas_item_add_line</c>); the
    /// linear fade adds half that length.
    /// </summary>
    public static float DashTrim(float dash, float width) =>
        Mathf.Clamp((dash - 1) / 2, 0, _feather / 2 * Mathf.Clamp(width / 2, 0, 1));

    public void Arc(Vector2 centre, float radius, float startAngle, float endAngle, int segments, Color color, float width)
    {
        var turn = ToPixels.Rotation;
        _canvas.DrawArc(ToPixels * centre, radius * Scale, startAngle + turn, endAngle + turn, segments, color, width * Scale, antialiased: true);
    }

    public void Ring(Vector2 centre, float radius, Color color, float width, int segments = _ringSegments) =>
        _canvas.DrawArc(ToPixels * centre, radius * Scale, 0, Mathf.Tau, segments, color, width * Scale, antialiased: true);

    /// <summary>A ring of <paramref name="dashes"/> equal dashes, each half its share of the turn, from angle 0.</summary>
    public void DashedRing(Vector2 centre, float radius, int dashes, int segmentsPerDash, Color color, float width)
    {
        for (var dash = 0; dash < dashes; dash++)
        {
            var from = dash * Mathf.Tau / dashes;
            Arc(centre, radius, from, from + (Mathf.Tau / dashes / 2), segmentsPerDash, color, width);
        }
    }

    public void Disc(Vector2 centre, float radius, Color color) =>
        _canvas.DrawCircle(ToPixels * centre, radius * Scale, color, filled: true, antialiased: true);

    /// <summary>A filled polygon. Godot does not feather polygons, so its edges stay hard.</summary>
    public void Polygon(Vector2[] points, Color color) =>
        _canvas.DrawColoredPolygon(Mapped(points), color);

    public void Dispose() => _canvas.DrawSetTransformMatrix(_drawTransform);

    private Vector2[] Mapped(Vector2[] points) => Mapped(points, ToPixels);

    private static Vector2[] Mapped(Vector2[] points, Transform2D toPixels) => [.. points.Select(point => toPixels * point)];
}
