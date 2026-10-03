using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws a plain joint (#626), shared by Build's canvas and the creature in Training: an unfilled
/// ring in the beam's colour and width whose outer edge is the joint's radius, so it is drawn at
/// the size it collides at. Drawn in window pixels (<see cref="UiPixelSpace"/>) so it stays crisp at
/// any zoom; <c>drawTransform</c> is the transform the caller draws with, and is restored afterwards.
/// </summary>
public static class JointDrawing
{
    private const int _ringSegments = 48;

    public static void DrawPlain(CanvasItem canvas, VisualTheme theme, Transform2D drawTransform, Vector2 center, float radius)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        var toPixels = UiPixelSpace.Enter(canvas, drawTransform);
        var scale = UiPixelSpace.ScaleOf(toPixels);
        var ring = Math.Max(radius - (theme.BeamWidth / 2), 0) * scale;
        canvas.DrawArc(toPixels * center, ring, 0, Mathf.Tau, _ringSegments, theme.Beam, theme.BeamWidth * scale, antialiased: true);
        canvas.DrawSetTransformMatrix(drawTransform);
    }
}
