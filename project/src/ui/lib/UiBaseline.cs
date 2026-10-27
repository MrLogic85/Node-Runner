using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Stands labels that share a line on one baseline. Containers align boxes, not baselines, so each
/// label starts at the top of the line and is as tall as the line's largest top margin and ascent
/// plus its own descent and bottom margin, with its text at the bottom. The line's container
/// places the line; the labels' metrics come from the theme, so a row that rebuilds on a theme
/// change aligns them again.
/// </summary>
public static class UiBaseline
{
    /// <summary>A label's extent above its baseline (top margin and ascent) and below it (descent and bottom margin).</summary>
    public readonly record struct Extent(float Above, float Below);

    /// <summary>The height each label takes so that all of them, top-aligned, stand on one baseline.</summary>
    public static float[] Heights(IReadOnlyList<Extent> labels)
    {
        var above = labels.Count == 0 ? 0 : labels.Max(label => label.Above);
        return [.. labels.Select(label => above + label.Below)];
    }

    /// <summary>
    /// Aligns the visible labels on one baseline. A hidden label gets its own height back, so it
    /// does not move the others.
    /// </summary>
    public static void Align(params Label[] labels)
    {
        foreach (var label in labels.Where(label => !label.Visible))
        {
            Release(label);
        }

        var shown = labels.Where(label => label.Visible).ToArray();
        var heights = Heights([.. shown.Select(ExtentOf)]);
        for (var index = 0; index < shown.Length; index++)
        {
            Stand(shown[index], heights[index]);
        }
    }

    /// <summary>Starts a label at the top of its line, <paramref name="height"/> tall, with its text at the bottom.</summary>
    public static void Stand(Label label, float height)
    {
        label.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        label.VerticalAlignment = VerticalAlignment.Bottom;
        label.CustomMinimumSize = label.CustomMinimumSize with { Y = height };
    }

    /// <summary>Hands a label its own height, vertical size flags and vertical alignment back at their defaults.</summary>
    public static void Release(Label label)
    {
        label.CustomMinimumSize = label.CustomMinimumSize with { Y = 0 };
        label.SizeFlagsVertical = Control.SizeFlags.Fill;
        label.VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>The label's extent around its baseline, from its theme font and normal stylebox.</summary>
    public static Extent ExtentOf(Label label)
    {
        var font = label.GetThemeFont("font");
        var fontSize = label.GetThemeFontSize("font_size");
        var box = label.GetThemeStylebox("normal");
        return new Extent(
            box.GetMargin(Side.Top) + font.GetAscent(fontSize),
            font.GetDescent(fontSize) + box.GetMargin(Side.Bottom));
    }
}
