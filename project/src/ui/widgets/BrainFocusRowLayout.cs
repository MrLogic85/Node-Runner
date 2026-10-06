namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Where BrainFocus puts its rows (#908), in the view's coordinates before scrolling. Rows are never
/// closer than one label line. When the tallest column fits at that spacing the rows spread over the
/// card as before; otherwise they keep that spacing and the network scrolls under its pinned headings,
/// both columns together.
/// </summary>
public readonly record struct BrainFocusRowLayout(float Top, float Spacing, float Radius, float ContentHeight, float ViewHeight)
{
    public const float VerticalInset = 10f;

    // Room above the first row for the column headings; the sheet gives the card this much more height.
    public const float HeadingBand = 12f;
    public const float MaxRadius = 16f;
    public const float MinRadius = 3f;
    public const float RadiusPerRow = 0.4f;
    public const float RowSpacingPerFontSize = 1.1f;
    public const float HaloGap = 4f;
    public const float HaloWidth = 3f;

    public bool Scrolls => ContentHeight > ViewHeight;

    /// <summary>The y of row <paramref name="index"/> of a column with <paramref name="count"/> rows.</summary>
    public float RowY(int index, int count, int tallest)
    {
        var bottom = Top + (Spacing * Math.Max(0, tallest - 1));
        return count <= 1 ? (Top + bottom) / 2 : Top + ((bottom - Top) * index / (count - 1));
    }

    // clipTop is where scrolled rows are cut, below the headings.
    public static BrainFocusRowLayout For(float viewHeight, int tallest, float fontSize, float clipTop)
    {
        var minSpacing = fontSize * RowSpacingPerFontSize;
        var top = VerticalInset + HeadingBand;
        var bottom = Math.Max(top + 1, viewHeight - VerticalInset);
        var available = bottom - top;
        if (tallest <= 1 || (tallest - 1) * minSpacing <= available)
        {
            // A single row sits in the middle, sized as if it had the whole height.
            return tallest > 1
                ? new BrainFocusRowLayout(top, available / (tallest - 1), RadiusFor(available / (tallest - 1)), viewHeight, viewHeight)
                : new BrainFocusRowLayout(top + (available / 2), 0, RadiusFor(available), viewHeight, viewHeight);
        }

        // The first and last rows keep room for the selection halo, so it is never cut at either end.
        var radius = RadiusFor(minSpacing);
        var edge = Math.Max(VerticalInset, radius + HaloGap + HaloWidth);
        var scrolledTop = clipTop + edge;
        var content = scrolledTop + ((tallest - 1) * minSpacing) + edge;
        return new BrainFocusRowLayout(scrolledTop, minSpacing, radius, content, viewHeight);
    }

    private static float RadiusFor(float spacing) => Math.Clamp(spacing * RadiusPerRow, MinRadius, MaxRadius);
}
