using Godot;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a beam as one round-ended stroke on Build's canvas. Plain joints are open rings (#626), so
/// the beam ends show at each joint's centre; round ends make beams meeting there read as one clean
/// pivot instead of overlapping square corners. Training's beams are <see cref="Line2D"/> nodes with
/// round cap modes; Build draws every beam in its canvas's <c>_Draw</c>, and Godot's immediate-mode
/// lines (<c>DrawLine</c>, <c>DrawPolyline</c>, RenderingServer polylines) have no cap mode, so this
/// builds the same shape as one polygon.
/// </summary>
public static class BeamDrawing
{
    private const int _capSegments = 8;

    public static void DrawRounded(CanvasItem canvas, Vector2 start, Vector2 end, Color color, float width)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var radius = width / 2;
        var along = end - start;
        if (along.LengthSquared() < 1e-6f)
        {
            canvas.DrawCircle(start, radius, color, antialiased: false);
            return;
        }

        var angle = along.Angle();
        var points = new Vector2[(_capSegments + 1) * 2];
        for (var i = 0; i <= _capSegments; i++)
        {
            var sweep = Mathf.Pi * i / _capSegments;
            points[i] = end + (Vector2.FromAngle(angle - (Mathf.Pi / 2) + sweep) * radius);
            points[_capSegments + 1 + i] = start + (Vector2.FromAngle(angle + (Mathf.Pi / 2) + sweep) * radius);
        }

        canvas.DrawColoredPolygon(points, color);
    }
}
