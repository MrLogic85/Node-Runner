using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Alpha helpers for theme colors. These are plain color math, not theme lookups.</summary>
public static class UiColorExtensions
{
    /// <summary>Returns the color with its alpha replaced by <paramref name="alpha"/>.</summary>
    public static Color WithAlpha(this Color color, float alpha) =>
        new(color.R, color.G, color.B, alpha);

    /// <summary>Returns the color with its current alpha scaled by <paramref name="factor"/>.</summary>
    public static Color ScaleAlpha(this Color color, float factor) =>
        new(color.R, color.G, color.B, color.A * factor);
}
