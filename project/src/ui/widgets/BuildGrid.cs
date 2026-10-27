using Godot;
using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Build's faint blueprint grid over <see cref="BuildViewModel.BuildArea"/>, the only place joints
/// can go: square cells drawn as hairlines that stay one pixel wide, behind Build's canvas and
/// Import's preview.
/// </summary>
public static class BuildGrid
{
    /// <summary>
    /// Draws the grid lines <paramref name="step"/> apart that cross <paramref name="shown"/>, in
    /// world units placed on <paramref name="item"/> by <paramref name="toItem"/>.
    /// </summary>
    public static void Draw(CanvasItem item, Transform2D toItem, CanvasRect shown, double step, Color color)
    {
        var area = BuildViewModel.BuildArea;
        var top = (float)area.Min.Y;
        var bottom = (float)area.Max.Y;
        var left = (float)area.Min.X;
        var right = (float)area.Max.X;
        item.DrawSetTransformMatrix(toItem);
        for (var x = area.Min.X; x <= area.Max.X; x += step)
        {
            if (x >= shown.Min.X && x <= shown.Max.X)
            {
                item.DrawLine(new Vector2((float)x, top), new Vector2((float)x, bottom), color, -1);
            }
        }

        for (var y = area.Min.Y; y <= area.Max.Y; y += step)
        {
            if (y >= shown.Min.Y && y <= shown.Max.Y)
            {
                item.DrawLine(new Vector2(left, (float)y), new Vector2(right, (float)y), color, -1);
            }
        }

        item.DrawRect(new Rect2(left, top, right - left, bottom - top), color, filled: false, width: -1);
        item.DrawSetTransformMatrix(Transform2D.Identity);
    }
}
