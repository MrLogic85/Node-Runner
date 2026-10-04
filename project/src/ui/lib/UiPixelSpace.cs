using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Draws antialiased shapes crisp at any zoom or UI size (#624, #633). Godot feathers an antialiased edge by about
/// one unit of the space the points are given in, so under a zoomed transform the feather grows
/// and the edge blurs. Points mapped to the window's pixels keep a one-pixel feather.
/// </summary>
public static class UiPixelSpace
{
    /// <summary>
    /// Switches <paramref name="canvas"/> to drawing in window pixels and returns the map from its
    /// <paramref name="drawTransform"/> space to them. Call <see cref="CanvasItem.DrawSetTransformMatrix"/>
    /// with <paramref name="drawTransform"/> afterwards to go back.
    /// </summary>
    public static Transform2D Enter(CanvasItem canvas, Transform2D drawTransform)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var itemToPixels = ItemToPixels(canvas);
        canvas.DrawSetTransformMatrix(itemToPixels.AffineInverse());
        return itemToPixels * drawTransform;
    }

    /// <summary>Maps <paramref name="canvas"/>'s own space to the window's pixels.</summary>
    public static Transform2D ItemToPixels(CanvasItem canvas) =>
        canvas.GetViewport().GetFinalTransform() * canvas.GetGlobalTransformWithCanvas();

    /// <summary>How many window pixels one unit of the mapped space is.</summary>
    public static float ScaleOf(Transform2D toPixels) => toPixels.BasisXform(Vector2.Right).Length();
}
