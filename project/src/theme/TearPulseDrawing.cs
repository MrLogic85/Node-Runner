using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// Draws the pulse a joint gives when a beam end lets go of it: a <c>danger</c> ring that
/// grows out from the joint's ring and fades as it goes (<c>docs/WORLD_VISUALS.md</c> → Tear).
/// </summary>
public static class TearPulseDrawing
{
    /// <summary>How far past the joint's ring the pulse starts, in creature units.</summary>
    public const float StartGap = 3f;

    /// <summary>How far past the joint's ring the pulse ends, in creature units.</summary>
    public const float EndGap = 19f;

    /// <summary>
    /// The pulse round a joint of <paramref name="radius"/> at the canvas origin, <paramref name="grown"/>
    /// from 0 (just let go) to 1 (gone).
    /// </summary>
    public static void Draw(CanvasItem canvas, VisualTheme theme, float radius, float grown)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        using var pen = UiPixelPen.Begin(canvas);
        pen.Ring(Vector2.Zero, radius + Mathf.Lerp(StartGap, EndGap, grown), theme.Danger with { A = 1f - grown }, theme.JointRingWidth);
    }
}
