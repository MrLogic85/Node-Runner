using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The corner radii of a rounded rectangle. A shape drawn flush against a card's rounded edge
/// rounds only the corners it shares with the card, because a card inside a clipping page
/// cannot clip its own content (<see cref="UiClip"/>).
/// </summary>
public readonly record struct UiCorners(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    public static UiCorners Uniform(float radius) => new(radius, radius, radius, radius);

    public static UiCorners Top(float radius) => new(radius, radius, 0, 0);

    public float Smallest => Mathf.Min(Mathf.Min(TopLeft, TopRight), Mathf.Min(BottomRight, BottomLeft));

    public void ApplyTo(StyleBoxFlat style)
    {
        style.CornerRadiusTopLeft = Mathf.RoundToInt(TopLeft);
        style.CornerRadiusTopRight = Mathf.RoundToInt(TopRight);
        style.CornerRadiusBottomRight = Mathf.RoundToInt(BottomRight);
        style.CornerRadiusBottomLeft = Mathf.RoundToInt(BottomLeft);
    }

    /// <summary>Fills <paramref name="rect"/> on <paramref name="canvas"/> with these corners.</summary>
    public void Fill(CanvasItem canvas, Rect2 rect, Color color)
    {
        var style = new StyleBoxFlat { BgColor = color };
        ApplyTo(style);
        canvas.DrawStyleBox(style, rect);
    }
}
