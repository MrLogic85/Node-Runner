using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>An axis-aligned rectangle from <see cref="Min"/> to <see cref="Max"/>, used for the Build canvas's area, visible area and content.</summary>
public readonly record struct CanvasRect(Vector2D Min, Vector2D Max)
{
    public double Width => Max.X - Min.X;

    public double Height => Max.Y - Min.Y;

    public Vector2D Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2);

    public bool Contains(Vector2D point) =>
        point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;

    /// <summary>The point nearest <paramref name="point"/> that is at least <paramref name="inset"/> inside every edge.</summary>
    public Vector2D Clamp(Vector2D point, double inset = 0) => new(
        Math.Clamp(point.X, Min.X + inset, Math.Max(Min.X + inset, Max.X - inset)),
        Math.Clamp(point.Y, Min.Y + inset, Math.Max(Min.Y + inset, Max.Y - inset)));
}
